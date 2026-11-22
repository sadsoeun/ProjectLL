using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>
    /// View of one world-map node. The pad keeps its toon materials; each state recolours the stone, gold trim and
    /// inset (emblem disc + gems) through per-slot property blocks. Open nodes breathe their inset glow and show a
    /// spinning ground halo with a ripple; cleared nodes turn gold (the flag is optional, off by default).
    /// </summary>
    public sealed class WorldMapNode : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int RippleId = Shader.PropertyToID("_Ripple");

        [SerializeField] private WorldMapNodeData data;

        [Header("Pad")]
        [SerializeField] private Renderer padRenderer;
        [SerializeField, Min(0)] private int stoneSlot;
        [SerializeField, Min(0)] private int trimSlot = 1;
        [SerializeField, Min(0)] private int insetSlot = 2;
        [Tooltip("Ground halo (MyGame/WorldMap/NodeRing); shown while the node is open.")]
        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private GameObject clearedFlag;
        [Tooltip("Raise the flag on cleared nodes. Off: the gold pad alone marks a clear.")]
        [SerializeField] private bool showClearedFlag;

        [Header("Colours")]
        [Tooltip("Inset and halo colour while open (blue for stages, red for bosses).")]
        [SerializeField] private Color accentColor = new Color32(0x36, 0xAE, 0xEA, 0xFF);
        [SerializeField] private Color stoneColor = new Color(0.80f, 0.83f, 0.90f);
        [SerializeField] private Color trimColor = new Color(1f, 0.76f, 0.30f);
        [SerializeField] private Color lockedStoneColor = new Color(0.52f, 0.53f, 0.58f);
        [SerializeField] private Color lockedTrimColor = new Color(0.46f, 0.43f, 0.42f);
        [SerializeField] private Color lockedInsetColor = new Color(0.20f, 0.22f, 0.29f);
        [SerializeField] private Color clearedInsetColor = new Color(1f, 0.80f, 0.36f);
        [Tooltip("Inset and halo colour of a mastered node (cleared with every collectible).")]
        [SerializeField] private Color masterColor = new Color(1f, 0.84f, 0.18f);

        [Header("Animation")]
        [SerializeField, Range(0f, 2f)] private float openGlowMin = 0.25f;
        [SerializeField, Range(0f, 2f)] private float openGlowMax = 0.7f;
        [SerializeField, Range(0f, 1f)] private float clearedGlow = 0.18f;
        [SerializeField, Min(0.1f)] private float breathSpeed = 2.2f;
        [SerializeField] private float ringSpinDegreesPerSecond = 18f;
        [SerializeField, Min(0.2f)] private float ripplePeriod = 1.8f;
        [Tooltip("Ripple speed and halo brightness multiplier during the unlock attention.")]
        [SerializeField, Min(1f)] private float attentionBoost = 2.2f;
        [Tooltip("Peak extra emission of the inset and trim while a node turns master gold (first master clear).")]
        [SerializeField, Range(0f, 4f)] private float masterRevealFlash = 1.6f;

        private MaterialPropertyBlock block;
        // Lazily created: survives a script reload during Play Mode (non-serialized fields are reset then).
        private MaterialPropertyBlock Block => block ??= new MaterialPropertyBlock();
        private float attentionUntil;
        private float rippleTime;
        private bool revealing;
        private float revealStart, revealSeconds = 1f;
        private Vector3 ringScale = Vector3.one;
        private bool ringScaleKnown;

        public WorldMapNodeData Data => data;
        public NodeState State { get; private set; } = NodeState.Locked;
        /// <summary>Cleared with every collectible.</summary>
        public bool Mastered { get; private set; }
        /// <summary>World position the avatar stands on.</summary>
        public Vector3 Anchor => transform.position;

        private void Awake()
        {
            Assert.IsNotNull(data, "WorldMapNode.data is not assigned.");
            Assert.IsNotNull(padRenderer, "WorldMapNode.padRenderer is not assigned.");
        }

        /// <param name="mastered">Cleared with every collectible: the node sparkles like an open node, in gold.</param>
        public void Apply(NodeState state, bool mastered = false)
        {
            State = state;
            Mastered = mastered && state == NodeState.Cleared;
            bool locked = state == NodeState.Locked;
            SetSlot(stoneSlot, locked ? lockedStoneColor : stoneColor, Color.black);
            SetSlot(trimSlot, locked ? lockedTrimColor : trimColor, Color.black);
            switch (state)
            {
                case NodeState.Locked: SetSlot(insetSlot, lockedInsetColor, Color.black); break;
                case NodeState.Open: SetInsetGlow(openGlowMin); break;
                case NodeState.Cleared:
                    if (Mastered) SetInsetGlow(openGlowMin);
                    else SetSlot(insetSlot, clearedInsetColor, clearedInsetColor * clearedGlow);
                    break;
            }
            if (ringRenderer != null)
                ringRenderer.gameObject.SetActive(Sparkles);
            if (clearedFlag != null)
                clearedFlag.SetActive(showClearedFlag && state == NodeState.Cleared);
        }

        /// <summary>Strong halo burst for a few seconds (newly unlocked node).</summary>
        public void PlayUnlockAttention(float seconds = 3f)
        {
            attentionUntil = Time.unscaledTime + seconds;
        }

        /// <summary>First master clear: show the plain cleared look until <see cref="PlayMasterReveal"/>.</summary>
        public void PrepareMasterReveal()
        {
            revealing = false;
            Apply(State, false);
        }

        /// <summary>Blends the pad from cleared colours to master gold with a flash, the halo growing in.</summary>
        public void PlayMasterReveal(float seconds, float attentionSeconds)
        {
            if (State != NodeState.Cleared) return;
            Apply(NodeState.Cleared, true);
            CacheRingScale();
            revealing = true;
            revealStart = Time.unscaledTime;
            revealSeconds = Mathf.Max(0.05f, seconds);
            attentionUntil = revealStart + revealSeconds + Mathf.Max(0f, attentionSeconds);
            SampleReveal(0f);
        }

        /// <summary>Ends a running (or pending) reveal on the final master look.</summary>
        public void FinishMasterReveal()
        {
            if (State != NodeState.Cleared) return;
            bool wasRevealing = revealing;
            revealing = false;
            if (!Mastered || wasRevealing)
            {
                Apply(NodeState.Cleared, true);
                if (ringScaleKnown && ringRenderer != null) ringRenderer.transform.localScale = ringScale;
            }
        }

        private void CacheRingScale()
        {
            if (ringScaleKnown || ringRenderer == null) return;
            ringScale = ringRenderer.transform.localScale;
            ringScaleKnown = true;
        }

        private void SampleReveal(float k)
        {
            float e = Mathf.SmoothStep(0f, 1f, k);
            float flash = Mathf.Sin(Mathf.Clamp01(k * 1.3f) * Mathf.PI) * masterRevealFlash;
            Color inset = Color.Lerp(clearedInsetColor, masterColor, e);
            Color emission = Color.Lerp(clearedInsetColor * clearedGlow, masterColor * openGlowMax, e) + masterColor * flash;
            SetSlot(insetSlot, inset, emission);
            SetSlot(trimSlot, trimColor, trimColor * flash * 0.6f);
            if (ringRenderer != null)
                ringRenderer.transform.localScale = ringScale * Mathf.Lerp(0.05f, 1f, 1f - Mathf.Pow(1f - e, 3f));
        }

        private void Update()
        {
            if (!Sparkles)
                return;

            bool attention = Time.unscaledTime < attentionUntil;
            float boost = attention ? attentionBoost : 1f;
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * breathSpeed * boost);
            if (revealing)
            {
                float k = (Time.unscaledTime - revealStart) / revealSeconds;
                if (k >= 1f)
                {
                    revealing = false;
                    SetSlot(trimSlot, trimColor, Color.black);
                    if (ringRenderer != null) ringRenderer.transform.localScale = ringScale;
                }
                else SampleReveal(k);
            }
            if (!revealing)
                SetInsetGlow(Mathf.Lerp(openGlowMin, openGlowMax, breath) * (attention ? 1.5f : 1f));

            if (ringRenderer == null)
                return;
            float dt = Time.unscaledDeltaTime;
            rippleTime = (rippleTime + dt * boost / ripplePeriod) % 1f;
            ringRenderer.transform.Rotate(0f, ringSpinDegreesPerSecond * dt, 0f, Space.World);
            var block = Block;
            ringRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, GlowColor);
            block.SetFloat(IntensityId, (1.3f + 0.5f * breath) * boost);
            block.SetFloat(RippleId, rippleTime);
            ringRenderer.SetPropertyBlock(block);
        }

        /// <summary>Open nodes and mastered nodes animate (inset breathing + halo).</summary>
        private bool Sparkles => State == NodeState.Open || Mastered;
        private Color GlowColor => Mastered ? masterColor : accentColor;
        private void SetInsetGlow(float glow) => SetSlot(insetSlot, GlowColor, GlowColor * glow);

        private void SetSlot(int slot, Color baseColor, Color emission)
        {
            var block = Block;
            padRenderer.GetPropertyBlock(block, slot);
            block.SetColor(BaseColorId, baseColor);
            block.SetColor(EmissionColorId, emission);
            padRenderer.SetPropertyBlock(block, slot);
        }
    }
}
