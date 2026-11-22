using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>Full-screen black overlay. Blocks pointer input while visible or fading.</summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;

        public float Alpha => group.alpha;

        private void Awake()
        {
            Assert.IsNotNull(group, "ScreenFader.group is not assigned.");
        }

        public void SetAlpha(float alpha)
        {
            group.alpha = Mathf.Clamp01(alpha);
            group.blocksRaycasts = group.alpha > 0.001f;
        }

        /// <summary>Fades to the target alpha using unscaled time (works while timeScale is 0).</summary>
        public IEnumerator FadeTo(float target, float duration)
        {
            target = Mathf.Clamp01(target);
            float start = group.alpha;
            group.blocksRaycasts = true;
            if (duration > 0f && !Mathf.Approximately(start, target))
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                    yield return null;
                }
            }
            SetAlpha(target);
        }
    }
}
