using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Drives the Leaf Explorer face: random blink plus a few expressions. Expression names follow the SD Unity-chan face-layer
    /// vocabulary so CharacterFaceEvents calls keep working.
    /// Skin is never painted over: the body albedo is swapped for a copy with the eyes / mouth erased (same texture, same Toon2Tone
    /// lighting) and the ExplorerFace overlay only draws lashes, lines and the mouth interior. Values go through MaterialPropertyBlocks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeafExplorerFace : MonoBehaviour
    {
        private static readonly int EyeId = Shader.PropertyToID("_Eye");
        private static readonly int MouthId = Shader.PropertyToID("_Mouth");
        private static readonly int BlinkId = Shader.PropertyToID("_Blink");
        private static readonly int WinkId = Shader.PropertyToID("_Wink");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [SerializeField] private Renderer faceRenderer;
        [Header("Body albedo variants (eyes / mouth erased); empty = overlay only")]
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Texture texEyes;
        [SerializeField] private Texture texEyesMouth;
        [SerializeField] private Texture texRightEyeMouth;
        [SerializeField] private Texture texMouth;
        [Header("Random blink")]
        [SerializeField] private bool blinkEnabled = true;
        [SerializeField, Min(0.2f)] private float minInterval = 2.2f;
        [SerializeField, Min(0.2f)] private float maxInterval = 5.0f;
        [Tooltip("Whole blink: the eye is closed for about two to three frames.")]
        [SerializeField, Min(0.04f)] private float blinkDuration = 0.10f;
        [SerializeField, Range(0f, 1f)] private float doubleBlinkChance = 0.18f;

        private MaterialPropertyBlock faceBlock, bodyBlock;
        private int eye, mouth;
        private bool wink;
        private float blinkLevel;   // 0 open, 1 closed
        private bool blinking => blinkLevel > 0.01f;
        private float BlinkClosedTime => Mathf.Min(blinkDuration, 0.10f);   // serialized older values (0.16 s) are clamped to a fast blink
        private float nextBlinkTime;
        private float blinkStart = -1f;
        private bool pendingDouble;
        private string current = "default";
        private Texture originalBase;
        private int appliedVariant = -2;

        public string Current => current;

        private void Awake()
        {
            if (faceRenderer == null)
                faceRenderer = FindRenderer("Face");
            if (bodyRenderer == null)
                bodyRenderer = FindRenderer("Body");
            if (bodyRenderer != null && bodyRenderer.sharedMaterial != null)
            {
                var m = bodyRenderer.sharedMaterial;
                originalBase = m.HasProperty(BaseMapId) ? m.GetTexture(BaseMapId) : (m.HasProperty(MainTexId) ? m.GetTexture(MainTexId) : null);
            }
            faceBlock = new MaterialPropertyBlock();
            bodyBlock = new MaterialPropertyBlock();
            ScheduleBlink();
            Apply();
        }

        private Renderer FindRenderer(string part)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains(part))
                    return r;
            return null;
        }

        private void OnEnable()
        {
            blinkStart = -1f;
            blinkLevel = 0f;
            ScheduleBlink();
            if (faceBlock != null)
                Apply();
        }

        private void OnDisable()
        {
            // leave the body on its original albedo
            appliedVariant = -2;
            eye = mouth = 0;
            wink = false;
            blinkLevel = 0f;
            if (bodyRenderer != null && originalBase != null && bodyBlock != null)
            {
                bodyRenderer.GetPropertyBlock(bodyBlock);
                bodyBlock.SetTexture(BaseMapId, originalBase);
                bodyRenderer.SetPropertyBlock(bodyBlock);
            }
        }

        /// <summary>Expression by name: default, smile, smile2/happy, bright, surprise, damaged/hurt/strain, eye_close/relux, sad, angry/scold, wink.</summary>
        public void SetExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                expression = "default";
            string key = expression.ToLowerInvariant();
            int at = key.IndexOf('@');
            if (at >= 0)
                key = key.Substring(0, at);
            current = key;
            wink = false;
            switch (key)
            {
                case "smile2": case "happy": case "joy": eye = 2; mouth = 2; break;
                case "bright": eye = 0; mouth = 2; break;                 // open eyes, big open smile
                case "wink": eye = 0; mouth = 2; wink = true; break;      // right eye closed (happy arc), left eye open
                case "surprise": eye = 0; mouth = 1; break;
                case "damaged": case "hurt": case "strain": eye = 3; mouth = 3; break;
                case "eye_close": case "relux": case "sleep": eye = 1; mouth = 0; break;
                case "sad": case "confuse": eye = 4; mouth = 3; break;
                case "angry": case "scold": eye = 0; mouth = 4; break;
                case "smile": eye = 0; mouth = 5; break;
                default: eye = 0; mouth = 0; break; // painted neutral smile
            }
            Apply();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float level = 0f;
            if (blinkEnabled && eye == 0 && !wink)
            {
                if (blinkStart < 0f && now >= nextBlinkTime)
                {
                    blinkStart = now;
                    pendingDouble = Random.value < doubleBlinkChance;
                }
                if (blinkStart >= 0f)
                {
                    if (now - blinkStart >= BlinkClosedTime)
                    {
                        blinkStart = -1f;
                        if (pendingDouble) { pendingDouble = false; nextBlinkTime = now + 0.12f; }
                        else ScheduleBlink();
                    }
                    else
                    {
                        level = 1f;   // open -> closed -> open: the painted eye has no matching half-closed state, so the blink is a fast two-state flick
                    }
                }
            }
            if (!Mathf.Approximately(level, blinkLevel))
            {
                blinkLevel = level;
                Apply();
            }
        }

        private void ScheduleBlink() => nextBlinkTime = Time.unscaledTime + Random.Range(minInterval, Mathf.Max(minInterval, maxInterval));

        // Which albedo variant matches the current state: -1 original, 0 eyes, 1 eyes+mouth, 2 right eye+mouth, 3 mouth.
        private int Variant()
        {
            bool bothEyes = eye != 0 || blinking;
            bool rightEyeOnly = wink && eye == 0 && !blinking;
            bool mouthErased = mouth != 0;
            if (bothEyes) return mouthErased ? 1 : 0;
            if (rightEyeOnly) return 2;
            return mouthErased ? 3 : -1;
        }

        private void Apply()
        {
            if (faceBlock == null)
                faceBlock = new MaterialPropertyBlock();
            if (faceRenderer != null)
            {
                faceRenderer.GetPropertyBlock(faceBlock);
                faceBlock.SetFloat(EyeId, eye);
                faceBlock.SetFloat(MouthId, mouth);
                faceBlock.SetFloat(BlinkId, blinkLevel);
                faceBlock.SetFloat(WinkId, wink ? 1f : 0f);
                faceRenderer.SetPropertyBlock(faceBlock);
            }
            ApplyBody();
        }

        private void ApplyBody()
        {
            if (bodyRenderer == null || originalBase == null)
                return;
            int variant = Variant();
            if (variant == appliedVariant)
                return;
            Texture tex = variant switch { 0 => texEyes, 1 => texEyesMouth, 2 => texRightEyeMouth, 3 => texMouth, _ => originalBase };
            if (tex == null)
                tex = originalBase;
            if (bodyBlock == null)
                bodyBlock = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(bodyBlock);
            bodyBlock.SetTexture(BaseMapId, tex);
            bodyRenderer.SetPropertyBlock(bodyBlock);
            appliedVariant = variant;
        }
    }
}
