using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>Highlight geometry: world point / HUD element -> hole in the overlay's local pixels (any canvas render mode).</summary>
    public static class TutorialSpot
    {
        /// <summary>
        /// Circle around a world point. radius is in metres (projected), never smaller than minPixels.
        /// False when the point is behind the camera or outside the screen (the caller falls back to the player).
        /// </summary>
        public static bool TryWorld(RectTransform overlay, Camera cam, Vector3 world, float radius, float minPixels, out SpotShape spot)
        {
            spot = default;
            if (overlay == null || cam == null)
                return false;
            Vector3 vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0f || vp.x < 0.02f || vp.x > 0.98f || vp.y < 0.02f || vp.y > 0.98f)
                return false;
            Rect r = overlay.rect;
            Vector3 edge = cam.WorldToViewportPoint(world + cam.transform.right * radius);
            float pixels = Mathf.Max(minPixels, Mathf.Abs(edge.x - vp.x) * r.width);
            pixels = Mathf.Min(pixels, r.height * .3f);
            spot = SpotShape.Circle(new Vector2((vp.x - .5f) * r.width, (vp.y - .5f) * r.height), pixels);
            return true;
        }

        /// <summary>Rounded rectangle around a UI element, grown by padding.</summary>
        public static bool TryRect(RectTransform overlay, RectTransform element, float padding, float corner, out SpotShape spot)
        {
            spot = default;
            if (overlay == null || element == null || !element.gameObject.activeInHierarchy)
                return false;
            var corners = new Vector3[4];
            element.GetWorldCorners(corners);
            Vector2 min = overlay.InverseTransformPoint(corners[0]), max = overlay.InverseTransformPoint(corners[2]);
            Vector2 half = (max - min) * .5f + new Vector2(padding, padding);
            if (half.x <= 1f || half.y <= 1f)
                return false;
            spot = SpotShape.Rect((min + max) * .5f, half, corner);
            return true;
        }
    }

    /// <summary>
    /// Runs one explanation (a sequence of pages) on a TutorialExplainView: follows the highlight target every frame,
    /// advances on Confirm / click once the page was readable long enough, and skips on a held Cancel.
    /// Shared by the stage and world-map tutorials. Uses unscaled time (the game is frozen meanwhile).
    /// </summary>
    public sealed class TutorialExplainRunner
    {
        private readonly TutorialExplainView view;
        private readonly TutorialExplainSettings settings;
        private readonly Func<TutorialExplainPage, SpotShape> resolve;
        private TutorialExplainPage[] pages;
        private int index;
        private float skipHeld;
        private bool closing;
        private ExplainResult pending = ExplainResult.Done;

        public bool IsRunning => pages != null;
        public int PageIndex => index;

        public TutorialExplainRunner(TutorialExplainView view, TutorialExplainSettings settings, Func<TutorialExplainPage, SpotShape> resolve)
        {
            this.view = view; this.settings = settings; this.resolve = resolve;
        }

        public void Open(TutorialExplainPage[] explainPages)
        {
            pages = explainPages;
            index = 0; skipHeld = 0f; closing = false; pending = ExplainResult.Done;
            view.Open(Content(), resolve(pages[0]));
            view.SetSkipProgress(0f);
        }

        private TutorialExplainContent Content()
        {
            var p = pages[index];
            return new TutorialExplainContent { titleKey = p.titleKey, bodyKey = p.bodyKey, glyphs = p.glyphs, page = index, pageCount = pages.Length };
        }

        /// <summary>Call every frame while running. Done / Skipped are returned once, after the fade-out.</summary>
        public ExplainResult Poll()
        {
            if (pages == null)
                return ExplainResult.Done;
            if (closing)
            {
                if (view.IsOpen)
                    return ExplainResult.Running;
                pages = null;
                return pending;
            }
            view.SetSpot(resolve(pages[index]));

            var input = TutorialUi.Input;
            if (input == null)
                return ExplainResult.Running;

            // Hold Cancel to skip the whole tutorial; releasing early drains the ring.
            skipHeld = input.CancelHeld
                ? skipHeld + Time.unscaledDeltaTime
                : Mathf.Max(0f, skipHeld - Time.unscaledDeltaTime * 2.5f);
            view.SetSkipProgress(skipHeld / settings.skipHoldSeconds);
            if (skipHeld >= settings.skipHoldSeconds)
            {
                Finish(ExplainResult.Skipped);
                return ExplainResult.Running;
            }

            if (view.CanAdvance && (input.ConfirmPressed || input.PointerClicked))
            {
                if (index + 1 < pages.Length)
                {
                    index++;
                    view.ShowPage(Content(), resolve(pages[index]));
                }
                else
                    Finish(ExplainResult.Done);
            }
            return ExplainResult.Running;
        }

        private void Finish(ExplainResult result)
        {
            pending = result;
            closing = true;
            view.Close();
        }

        /// <summary>Drops the explanation at once (death, retry, scene exit).</summary>
        public void Cancel()
        {
            pages = null;
            closing = false;
            view.HideImmediate();
        }
    }
}
