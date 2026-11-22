using UnityEngine;

namespace MyGame
{
#if UNITY_WEBGL
    /// <summary>Web builds have no Unity microphone API: this stand-in reports "no device", so recording is simply unavailable.</summary>
    internal static class Microphone
    {
        public static readonly string[] devices = new string[0];
        public static AudioClip Start(string device, bool loop, int seconds, int frequency) => null;
        public static int GetPosition(string device) => 0;
        public static bool IsRecording(string device) => false;
        public static void End(string device) { }
    }
#endif

    /// <summary>
    /// One microphone take for the Voice Studio (default input device, Unity Microphone API; PC). Begin, poll every frame
    /// (Tick), then End for the raw mono samples. Time is measured from the samples the device delivered, so the start-up
    /// latency of the microphone is not part of the take.
    /// </summary>
    public sealed class VoiceRecorder
    {
        private const float StartTimeout = 2.5f; // seconds without a single sample = the device did not start

        private AudioClip clip;
        private float limit;
        private float waited;
        private readonly float[] window = new float[1024];

        public bool IsRecording => clip != null;
        /// <summary>Seconds recorded so far.</summary>
        public float Elapsed { get; private set; }
        /// <summary>Loudest sample of the last few milliseconds (0..1), for the live meter.</summary>
        public float Level { get; private set; }
        /// <summary>The device delivered nothing within the start timeout.</summary>
        public bool Failed { get; private set; }

        /// <summary>False on platforms without microphone recording (web): the Voice Studio is hidden there.</summary>
#if UNITY_WEBGL
        public const bool Supported = false;
#else
        public const bool Supported = true;
#endif

        public static bool HasDevice => Microphone.devices != null && Microphone.devices.Length > 0;

        /// <summary>Starts the default microphone. False when there is no device or it refuses to start.</summary>
        public bool Begin(float maxSeconds, int sampleRate)
        {
            Cancel();
            if (!HasDevice)
                return false;
            limit = Mathf.Max(0.2f, maxSeconds);
            // A little head-room: the buffer must not wrap before the limit is noticed on a slow frame.
            clip = Microphone.Start(null, false, Mathf.CeilToInt(limit) + 2, sampleRate);
            Elapsed = 0f;
            Level = 0f;
            waited = 0f;
            Failed = false;
            return clip != null;
        }

        /// <summary>Call every frame while recording. True once the take is over (limit reached, device stopped or failed).</summary>
        public bool Tick(float unscaledDeltaTime)
        {
            if (clip == null)
                return true;
            int position = Microphone.GetPosition(null);
            if (position <= 0)
            {
                waited += unscaledDeltaTime;
                if (waited >= StartTimeout || !Microphone.IsRecording(null))
                {
                    Failed = true;
                    return true;
                }
                return false;
            }
            Elapsed = position / (float)clip.frequency;

            int count = Mathf.Min(window.Length, position);
            float peak = 0f;
            if (count > 0 && clip.channels == 1)
            {
                // GetData fills the whole array, so read a window that ends at the write position.
                int start = Mathf.Max(0, position - window.Length);
                if (start + window.Length <= clip.samples)
                {
                    clip.GetData(window, start);
                    for (int i = 0; i < count; i++) // the window starts at max(0, position - length): its first 'count' samples are recorded
                        peak = Mathf.Max(peak, Mathf.Abs(window[i]));
                }
            }
            Level = peak;
            return Elapsed >= limit || !Microphone.IsRecording(null);
        }

        /// <summary>Stops the microphone and returns the take as mono samples (null when nothing was captured).</summary>
        public float[] End(out int sampleRate)
        {
            sampleRate = 0;
            if (clip == null)
                return null;
            int position = Microphone.GetPosition(null);
            bool stillRecording = Microphone.IsRecording(null);
            Microphone.End(null);
            var recorded = clip;
            clip = null;
            // A stopped device reports position 0: the whole buffer was filled then.
            int frames = stillRecording ? position : recorded.samples;
            frames = Mathf.Min(frames, Mathf.CeilToInt(limit * recorded.frequency), recorded.samples);
            float[] mono = null;
            if (frames > 0)
            {
                int channels = Mathf.Max(1, recorded.channels);
                var data = new float[frames * channels];
                recorded.GetData(data, 0);
                mono = new float[frames];
                for (int i = 0; i < frames; i++)
                    mono[i] = data[i * channels];
                sampleRate = recorded.frequency;
            }
            Object.Destroy(recorded);
            return mono;
        }

        /// <summary>Stops the microphone and drops the take.</summary>
        public void Cancel()
        {
            if (clip == null)
                return;
            Microphone.End(null);
            Object.Destroy(clip);
            clip = null;
        }

        /// <summary>
        /// Cleans a raw take: cuts the silence before and after the voice, scales it to a steady loudness and fades the
        /// edges so it never clicks. Null (tooQuiet = true) when nothing audible was recorded.
        /// </summary>
        public static float[] Clean(float[] raw, int sampleRate, VoiceStudioConfig config, float maxSeconds, out bool tooQuiet)
        {
            tooQuiet = true;
            if (raw == null || raw.Length == 0 || sampleRate <= 0)
                return null;

            // Remove the DC offset some microphones add (it would otherwise defeat the silence threshold).
            double sum = 0d;
            for (int i = 0; i < raw.Length; i++)
                sum += raw[i];
            float offset = (float)(sum / raw.Length);
            float peak = 0f;
            for (int i = 0; i < raw.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(raw[i] - offset));
            if (peak < config.minPeak)
                return null;

            float threshold = peak * config.silenceFraction;
            int first = 0, last = raw.Length - 1;
            while (first < raw.Length && Mathf.Abs(raw[first] - offset) < threshold) first++;
            while (last > first && Mathf.Abs(raw[last] - offset) < threshold) last--;
            first = Mathf.Max(0, first - Mathf.RoundToInt(config.leadPadSeconds * sampleRate));
            last = Mathf.Min(raw.Length - 1, last + Mathf.RoundToInt(config.tailPadSeconds * sampleRate));
            int length = Mathf.Min(last - first + 1, Mathf.RoundToInt(maxSeconds * sampleRate));
            if (length < sampleRate / 50) // under 20 ms: a click, not a voice
                return null;

            float gain = Mathf.Min(config.maxGain, config.targetPeak / peak);
            int fadeIn = Mathf.Min(length / 2, Mathf.RoundToInt(0.005f * sampleRate));
            int fadeOut = Mathf.Min(length / 2, Mathf.RoundToInt(0.03f * sampleRate));
            var cleaned = new float[length];
            for (int i = 0; i < length; i++)
            {
                float envelope = 1f;
                if (i < fadeIn) envelope = i / (float)fadeIn;
                else if (i >= length - fadeOut) envelope = (length - 1 - i) / (float)fadeOut;
                cleaned[i] = Mathf.Clamp((raw[first + i] - offset) * gain * envelope, -1f, 1f);
            }
            tooQuiet = false;
            return cleaned;
        }
    }
}
