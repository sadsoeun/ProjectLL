using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Original cel explorer: fixed body, root-pinned hair motion and an eye-only blink. No breathing deformation.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleExplorerCharacter : MonoBehaviour
    {
        RectTransform art, hero;
        RawImage[] eyelids;
        float clock, nextBlink = 3.6f;

        public bool Initialize(RectTransform artwork)
        {
            if (hero != null) return true;
            var open = Resources.Load<Texture2D>("TitleArt/Explorer");
            var closed = Resources.Load<Texture2D>("TitleArt/ExplorerBlink");
            if (open == null) return false;
            art = artwork;
            hero = GoldUiTheme.Rect("LeafExplorerIllustration", art);
            var image = hero.gameObject.AddComponent<RawImage>();
            image.texture = open;
            image.raycastTarget = false;
            hero.gameObject.AddComponent<TitleHairWind>();
            if (closed != null && closed.width == open.width && closed.height == open.height)
            {
                // Only the eye region changes; the silhouette, body and hair remain perfectly still.
                eyelids = new[] {
                    Eye("BlinkLeft", closed, new Rect(433, 341, 121, 91)),
                    Eye("BlinkRight", closed, new Rect(609, 337, 72, 91))
                };
            }
            SetBlink(false);
            Layout();
            return true;
        }

        RawImage Eye(string name, Texture2D texture, Rect pixels)
        {
            var rect = GoldUiTheme.Rect(name, hero);
            rect.anchorMin = new Vector2(pixels.xMin / texture.width, 1 - pixels.yMax / texture.height);
            rect.anchorMax = new Vector2(pixels.xMax / texture.width, 1 - pixels.yMin / texture.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = new Rect(rect.anchorMin, rect.anchorMax - rect.anchorMin);
            image.raycastTarget = false;
            return image;
        }

        void Update()
        {
            if (IntroTitleTransition.IsActive || (GameSystems.Exists && GameSystems.Instance.Loader.IsBusy)) return;
            clock += Time.unscaledDeltaTime;
            SetBlink(clock >= nextBlink && clock < nextBlink + .13f);
            if (clock >= nextBlink + .13f) nextBlink = clock + Random.Range(3.2f, 5.4f);
        }

        public void SetBlink(bool closed)
        {
            if (eyelids == null) return;
            foreach (var eye in eyelids) eye.enabled = closed;
        }

        // Camera framing is separate from character animation: proportions and breathing remain fixed.
        public void ApplyCamera(float zoom,Vector2 panInDesignSpace)
        {
            Layout();
            if(hero==null||art==null)return;
            hero.anchoredPosition=hero.anchoredPosition*zoom+panInDesignSpace*(art.rect.height/1080f);
            hero.localScale=Vector3.one*zoom;
        }

        void LateUpdate() { Layout(); }
        public void Layout()
        {
            if (hero == null || art == null) return;
            float scale = art.rect.height / 1080f;
            hero.anchorMin = hero.anchorMax = hero.pivot = new Vector2(.5f, .5f);
            hero.anchoredPosition = new Vector2(530, -25) * scale;
            hero.sizeDelta = new Vector2(640, 960) * scale;
            hero.localScale = Vector3.one;
        }
    }
}

