using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Shows each logo with fade in / hold / fade out, then moves to the next scene (spec 5.1).</summary>
    public sealed class LogoSequencer : MonoBehaviour
    {
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private Image logoImage;
        public const string DefaultLogoResourcePath = "Brand/HamzterStartup";
        [Tooltip("Logo images shown in order. Empty uses the HAMZTER startup logo.")]
        [SerializeField] private Sprite[] logoSprites = new Sprite[0];
        [SerializeField, Min(0f)] private float fadeInTime = 0.8f;
        [SerializeField, Min(0f)] private float holdTime = 1.5f;
        [SerializeField, Min(0f)] private float fadeOutTime = 0.8f;
        [SerializeField] private bool allowSkip = true;

        private bool skipRequested;
        private bool finished;

        private void Awake()
        {
            Assert.IsNotNull(logoGroup, "LogoSequencer.logoGroup is not assigned.");
            Assert.IsNotNull(logoImage, "LogoSequencer.logoImage is not assigned.");
            if (logoSprites == null || logoSprites.Length == 0)
                logoSprites = new[] { Resources.Load<Sprite>(DefaultLogoResourcePath) };
            logoImage.type = Image.Type.Simple;
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
            logoImage.rectTransform.sizeDelta = new Vector2(1050f, 700f);
            logoGroup.alpha = 0f;
        }

        private void Start()
        {
            // Settings were already loaded by GameSystems during bootstrap.
            if (GameSystems.Exists)
                GameSystems.Instance.InputMode.SetMode(InputMode.UI);
            StartCoroutine(Run());
        }

        private void Update()
        {
            if (!allowSkip || skipRequested)
                return;
            if (AnySkipInput())
                skipRequested = true;
        }

        private static bool AnySkipInput()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        private IEnumerator Run()
        {
            int count = Mathf.Max(1, logoSprites.Length);
            for (int i = 0; i < count; i++)
            {
                bool hasSprite = logoSprites.Length > 0 && logoSprites[i] != null;
                if (hasSprite)
                {
                    logoImage.sprite = logoSprites[i];
                    logoImage.color = Color.white;
                }
                else
                {
                    Debug.LogWarning("[LogoSequencer] Missing logo sprite; skipping this entry.");
                    continue;
                }

                skipRequested = false;
                yield return Fade(0f, 1f, fadeInTime, stopOnSkip: true);
                logoGroup.alpha = 1f;
                float t = 0f;
                while (t < holdTime && !skipRequested)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return Fade(1f, 0f, fadeOutTime, stopOnSkip: false);
                logoGroup.alpha = 0f;
            }
            GoNext();
        }

        private IEnumerator Fade(float from, float to, float duration, bool stopOnSkip)
        {
            float t = 0f;
            while (t < duration)
            {
                if (stopOnSkip && skipRequested)
                    yield break;
                t += Time.unscaledDeltaTime;
                logoGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
        }

        private void GoNext()
        {
            if (finished)
                return;
            finished = true;
            if (!GameSystems.Exists)
            {
                Debug.LogError("[LogoSequencer] GameSystems missing.");
                return;
            }
            var systems = GameSystems.Instance;
            if (systems.Settings.Data.hasSelectedLanguage)
                IntroSequencePlayer.Play(systems, () => systems.Loader.Load(SceneId.Title));
            else
                systems.Loader.Load(SceneId.LanguageSelect);
        }
    }
}
