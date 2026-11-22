using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>Volume control, looping BGM and pooled sound effects (SfxBank).</summary>
    public sealed class AudioService : MonoBehaviour
    {
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("Optional UI click sound. Empty = silent.")]
        [SerializeField] private AudioClip uiClickClip;
        [Tooltip("Gameplay / UI sound effects played by Sfx.Play(SfxId). Empty = silent.")]
        [SerializeField] private SfxBank sfxBank;
        [Tooltip("Extra voices for pitched effects (PlayOneShot cannot change pitch per sound).")]
        [SerializeField, Range(1, 16)] private int sfxVoices = 8;
        [Tooltip("Seconds of the equal-power crossfade when the BGM changes (0 = cut).")]
        [SerializeField, Min(0f)] private float bgmCrossfadeSeconds = 1.5f;

        private SettingsService settings;
        private float duck = 1f;
        private Coroutine duckRoutine;

        // Two BGM sources: 'bgmCurrent' fades in the requested clip while 'bgmPrevious' fades the old one out.
        private AudioSource bgmCurrent, bgmPrevious;
        private float currentWeight = 1f, previousWeight;
        private Coroutine fadeRoutine;
        private AudioSource[] voices;
        private int nextVoice;
        private readonly float[] lastPlayed = new float[64];

        public void Initialize(SettingsService settingsService)
        {
            Assert.IsNotNull(bgmSource, "AudioService.bgmSource is not assigned.");
            Assert.IsNotNull(sfxSource, "AudioService.sfxSource is not assigned.");
            Assert.IsNotNull(settingsService);
            settings = settingsService;
            settings.Changed += ApplyVolumes;
            CreateBgmPair();
            ApplyVolumes();
            CreateVoices();
            for (int i = 0; i < lastPlayed.Length; i++)
                lastPlayed[i] = -100f;
        }

        private void CreateBgmPair()
        {
            var go = new GameObject("BgmCrossfadeSource");
            go.transform.SetParent(bgmSource.transform, false);
            var alt = go.AddComponent<AudioSource>();
            alt.playOnAwake = false;
            alt.loop = true;
            alt.spatialBlend = bgmSource.spatialBlend;
            alt.priority = bgmSource.priority;
            alt.outputAudioMixerGroup = bgmSource.outputAudioMixerGroup;
            bgmCurrent = bgmSource;
            bgmPrevious = alt;
        }

        private void CreateVoices()
        {
            voices = new AudioSource[sfxVoices];
            for (int i = 0; i < voices.Length; i++)
            {
                var go = new GameObject("SfxVoice" + i);
                go.transform.SetParent(sfxSource.transform, false);
                var v = go.AddComponent<AudioSource>();
                v.playOnAwake = false;
                v.loop = false;
                v.spatialBlend = 0f;
                v.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
                voices[i] = v;
            }
        }

        /// <summary>Plays a bank sound effect with its volume, pitch jitter and repeat limit.</summary>
        public void Play(SfxId id)
        {
            if (sfxBank == null || voices == null || !sfxBank.TryGet(id, out var e))
                return;
            int key = (int)id;
            if (key >= 0 && key < lastPlayed.Length)
            {
                if (Time.unscaledTime - lastPlayed[key] < e.minInterval)
                    return;
                lastPlayed[key] = Time.unscaledTime;
            }
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.Stop();
            v.clip = e.clip;
            v.volume = Mathf.Clamp01(settings.Data.sfxVolume) * e.volume;
            v.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
            v.Play();
        }

        private void OnDestroy()
        {
            if (settings != null)
                settings.Changed -= ApplyVolumes;
        }

        private void ApplyVolumes()
        {
            var d = settings.Data;
            AudioListener.volume = Mathf.Clamp01(d.masterVolume);
            float baseVolume = Mathf.Clamp01(d.bgmVolume) * duck;
            if (bgmCurrent == null)
            {
                bgmSource.volume = baseVolume;
                return;
            }
            // Equal-power curve keeps the loudness steady through the crossfade.
            bgmCurrent.volume = baseVolume * Mathf.Sin(currentWeight * Mathf.PI * 0.5f);
            bgmPrevious.volume = baseVolume * Mathf.Sin(previousWeight * Mathf.PI * 0.5f);
        }

        /// <summary>
        /// Plays a looping BGM with a crossfade. Null fades the current one out. Same clip keeps playing (spec 3.3).
        /// </summary>
        public void PlayBgm(AudioClip clip) => PlayBgm(clip, bgmCrossfadeSeconds);

        public void PlayBgm(AudioClip clip, float fadeSeconds)
        {
            if (bgmCurrent == null)
            {
                // Not initialized yet: plain playback on the main source.
                if (clip == null) { bgmSource.Stop(); bgmSource.clip = null; return; }
                if (bgmSource.clip == clip && bgmSource.isPlaying) return;
                bgmSource.clip = clip; bgmSource.loop = true; bgmSource.Play();
                return;
            }

            if (clip != null && bgmCurrent.clip == clip && bgmCurrent.isPlaying)
                return; // already playing or fading in
            if (clip == null && bgmCurrent.clip == null)
                return;

            if (clip != null && bgmPrevious.clip == clip && bgmPrevious.isPlaying)
            {
                // Coming back to the clip that is fading out: fade it back in from where it is.
                Swap();
            }
            else
            {
                // Mid-fade change: keep the louder clip as the one fading out, drop the quieter one.
                if (bgmPrevious.isPlaying && previousWeight > currentWeight)
                    Swap();
                bgmPrevious.Stop();
                Swap();
                bgmCurrent.clip = clip;
                currentWeight = 0f;
                if (clip != null)
                {
                    bgmCurrent.loop = true;
                    bgmCurrent.Play();
                }
                else
                    bgmCurrent.Stop();
            }

            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            if (fadeSeconds <= 0f || !isActiveAndEnabled)
            {
                currentWeight = 1f;
                previousWeight = 0f;
                FinishFade();
                return;
            }
            fadeRoutine = StartCoroutine(FadeRoutine(fadeSeconds));
        }

        private void Swap()
        {
            (bgmCurrent, bgmPrevious) = (bgmPrevious, bgmCurrent);
            (currentWeight, previousWeight) = (previousWeight, currentWeight);
        }

        private IEnumerator FadeRoutine(float seconds)
        {
            float rate = 1f / seconds;
            while (currentWeight < 1f || previousWeight > 0f)
            {
                float step = Time.unscaledDeltaTime * rate;
                currentWeight = Mathf.Min(1f, currentWeight + step);
                previousWeight = Mathf.Max(0f, previousWeight - step);
                ApplyVolumes();
                yield return null;
            }
            fadeRoutine = null;
            FinishFade();
        }

        private void FinishFade()
        {
            bgmPrevious.Stop();
            bgmPrevious.clip = null;
            if (bgmCurrent.clip == null)
                bgmCurrent.Stop();
            ApplyVolumes();
        }

        /// <summary>Plays a clip that is not in the bank (e.g. a boss's own hit sound) on the pooled voices.</summary>
        public void PlaySfx(AudioClip clip, float volume, float pitchJitter)
        {
            if (clip == null || voices == null)
                return;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.Stop();
            v.clip = clip;
            v.volume = Mathf.Clamp01(settings.Data.sfxVolume) * Mathf.Clamp01(volume);
            v.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            v.Play();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(settings.Data.sfxVolume));
        }

        public void PlayUiClick()
        {
            if (uiClickClip != null)
                PlaySfx(uiClickClip);
            else
                Play(SfxId.UiClick);
        }

        /// <summary>Scales BGM volume to the target (0..1) over the duration, using unscaled time.</summary>
        public void DuckBgm(float target, float duration)
        {
            if (!isActiveAndEnabled)
                return;
            if (duckRoutine != null)
                StopCoroutine(duckRoutine);
            duckRoutine = StartCoroutine(DuckRoutine(Mathf.Clamp01(target), duration));
        }

        private IEnumerator DuckRoutine(float target, float duration)
        {
            float start = duck;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                duck = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                ApplyVolumes();
                yield return null;
            }
            duck = target;
            ApplyVolumes();
            duckRoutine = null;
        }
    }
}
