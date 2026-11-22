using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Material inspector for "MyGame/Toon/2Tone".
/// All keywords / blend state / queue / pass toggles are derived from float + texture properties by
/// <see cref="SetupMaterial"/>, so materials configured from script stay consistent:
///     mat.SetFloat("_RimOn", 1); ToonShaderGUI.SetupMaterial(mat);
/// </summary>
public class ToonShaderGUI : ShaderGUI
{
    public const string ShaderName = "MyGame/Toon/2Tone";

    const string OutlinePass = "SRPDefaultUnlit";

    static readonly string[] Sections =
    {
        "Surface", "Base", "Two Tone", "Shadows", "Shade Map", "Face",
        "Rim", "Specular", "Emission", "Outline", "Additional Lights", "Advanced"
    };

    MaterialEditor _editor;
    MaterialProperty[] _props;

    // ------------------------------------------------------------------------------------------
    // Inspector
    // ------------------------------------------------------------------------------------------
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        _editor = materialEditor;
        _props = properties;

        EditorGUI.BeginChangeCheck();

        if (Section(0))
        {
            P("_Surface");
            P("_AlphaClip");
            if (F("_AlphaClip") > 0.5f) P("_Cutoff", 1);
            P("_Cull");
            P("_CastShadows");
            P("_ReceiveShadows");
            P("_QueueOffset");
            P("_DepthOffsetFactor");
            P("_DepthOffsetUnits");
            EndSection();
        }

        if (Section(1))
        {
            var baseMap = Find("_BaseMap");
            var baseColor = Find("_BaseColor");
            if (baseMap != null)
            {
                _editor.TexturePropertySingleLine(new GUIContent("Base Map", "Albedo (sRGB). Alpha used for clip / transparency."), baseMap, baseColor);
                _editor.TextureScaleOffsetProperty(baseMap);
            }
            EndSection();
        }

        if (Section(2))
        {
            P("_LitColor");
            P("_ShadowColor");
            P("_Threshold");
            P("_BandSoftness");
            P("_ShadowStrength");
            EditorGUILayout.LabelField("Shadow albedo = HSV(albedo) (sRGB space)", EditorStyles.miniLabel);
            P("_ShadowHueShift");
            P("_ShadowSaturation");
            P("_ShadowValue");
            P("_MainLightColorInfluence");
            P("_IndirectStrength");
            EndSection();
        }

        if (Section(3))
        {
            EditorGUILayout.HelpBox("Extra bias in metres, applied in ShadowCaster on top of URP's light/pipeline bias.", MessageType.None);
            P("_ShadowDepthBias");
            P("_ShadowNormalBias");
            EndSection();
        }

        if (Section(4))
        {
            var shadeMap = Find("_ShadeMap");
            if (shadeMap != null)
                _editor.TexturePropertySingleLine(new GUIContent("Shade Map", "Linear (sRGB OFF). R: threshold offset (0.5 = none, >0.5 more lit), G: rim mask, B: specular mask. Enabled when assigned."), shadeMap);
            if (shadeMap != null && shadeMap.textureValue != null) P("_ShadeMapStrength", 1);
            EndSection();
        }

        if (Section(5))
        {
            P("_FaceShading");
            if (F("_FaceShading") > 0.5f)
            {
                EditorGUI.indentLevel++;
                P("_FaceForward");
                P("_FaceUp");
                P("_FaceVectorSpace");
                P("_FaceNormalBlend");
                var sdf = Find("_FaceSDF");
                if (sdf != null)
                    _editor.TexturePropertySingleLine(new GUIContent("Face SDF", "Linear. R = SDF authored for light from the character's right. Enabled when assigned."), sdf);
                if (sdf != null && sdf.textureValue != null) P("_FaceSDFFlip");
                EditorGUI.indentLevel--;
            }
            EndSection();
        }

        if (Section(6))
        {
            P("_RimOn");
            if (F("_RimOn") > 0.5f) { P("_RimColor", 1); P("_RimWidth", 1); P("_RimLitOnly", 1); }
            EndSection();
        }

        if (Section(7))
        {
            P("_SpecularOn");
            if (F("_SpecularOn") > 0.5f) { P("_SpecularColor", 1); P("_SpecularSize", 1); }
            EndSection();
        }

        if (Section(8))
        {
            P("_EmissionOn");
            if (F("_EmissionOn") > 0.5f)
            {
                var map = Find("_EmissionMap");
                var col = Find("_EmissionColor");
                if (map != null) _editor.TexturePropertyWithHDRColor(new GUIContent("Emission Map"), map, col, false);
            }
            EndSection();
        }

        if (Section(9))
        {
            P("_OutlineOn");
            if (F("_OutlineOn") > 0.5f)
            {
                if (F("_Surface") > 0.5f)
                    EditorGUILayout.HelpBox("Outline is disabled for Transparent surfaces.", MessageType.Info);
                P("_OutlineColor", 1); P("_OutlineWidth", 1); P("_OutlineAlbedoMix", 1); P("_OutlineDistanceClamp", 1);
            }
            EndSection();
        }

        if (Section(10))
        {
            P("_AdditionalLightStrength");
            P("_AdditionalLightTint");
            P("_AdditionalLightThreshold");
            EndSection();
        }

        if (Section(11))
        {
            _editor.EnableInstancingField();
            EditorGUI.BeginDisabledGroup(true);
            foreach (var t in _editor.targets)
            {
                var m = t as Material;
                if (m == null) continue;
                EditorGUILayout.IntField("Render Queue (auto)", m.renderQueue);
                break;
            }
            EditorGUI.EndDisabledGroup();
            EndSection();
        }

        if (EditorGUI.EndChangeCheck())
        {
            foreach (var t in materialEditor.targets)
            {
                var m = t as Material;
                if (m != null) SetupMaterial(m);
            }
        }
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        base.AssignNewShaderToMaterial(material, oldShader, newShader);
        SetupMaterial(material);
    }

    public override void ValidateMaterial(Material material)
    {
        SetupMaterial(material);
    }

    // ------------------------------------------------------------------------------------------
    // Script-callable setup (batch tools)
    // ------------------------------------------------------------------------------------------
    /// <summary>
    /// Derives keywords, blend/ZWrite, render queue, RenderType tag and pass enables from the
    /// material's float/texture properties. Safe to call repeatedly.
    /// </summary>
    public static void SetupMaterial(Material m)
    {
        if (m == null || m.shader == null) return;

        bool transparent = Get(m, "_Surface", 0f) > 0.5f;
        bool alphaClip = Get(m, "_AlphaClip", 0f) > 0.5f;

        // surface / blend
        SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", transparent);
        SetKeyword(m, "_ALPHATEST_ON", alphaClip);

        int queue;
        if (transparent)
        {
            Set(m, "_SrcBlend", (float)BlendMode.SrcAlpha);
            Set(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            Set(m, "_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            queue = (int)RenderQueue.Transparent;
        }
        else
        {
            Set(m, "_SrcBlend", (float)BlendMode.One);
            Set(m, "_DstBlend", (float)BlendMode.Zero);
            Set(m, "_ZWrite", 1f);
            m.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            queue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
        }
        m.renderQueue = queue + Mathf.RoundToInt(Get(m, "_QueueOffset", 0f));

        // shadows
        SetKeyword(m, "_RECEIVE_SHADOWS_OFF", Get(m, "_ReceiveShadows", 1f) < 0.5f);

        // texture-driven features
        SetKeyword(m, "_SHADEMAP", HasTex(m, "_ShadeMap"));
        bool face = Get(m, "_FaceShading", 0f) > 0.5f;
        SetKeyword(m, "_FACE_SHADING", face);
        SetKeyword(m, "_FACE_SDF", face && HasTex(m, "_FaceSDF"));

        // optional layers
        SetKeyword(m, "_RIM_ON", Get(m, "_RimOn", 0f) > 0.5f);
        SetKeyword(m, "_SPECULAR_ON", Get(m, "_SpecularOn", 0f) > 0.5f);
        bool emission = Get(m, "_EmissionOn", 0f) > 0.5f;
        SetKeyword(m, "_EMISSION", emission);
        m.globalIlluminationFlags = emission ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                                             : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

        // passes
        bool outline = Get(m, "_OutlineOn", 0f) > 0.5f && !transparent && Get(m, "_OutlineWidth", 0f) > 0f;
        m.SetShaderPassEnabled(OutlinePass, outline);
        m.SetShaderPassEnabled("ShadowCaster", Get(m, "_CastShadows", 1f) > 0.5f);
        m.SetShaderPassEnabled("DepthOnly", !transparent);
        m.SetShaderPassEnabled("DepthNormals", !transparent);
    }

    // ------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------
    static float Get(Material m, string name, float fallback) => m.HasProperty(name) ? m.GetFloat(name) : fallback;

    static void Set(Material m, string name, float v)
    {
        if (m.HasProperty(name)) m.SetFloat(name, v);
    }

    static bool HasTex(Material m, string name) => m.HasProperty(name) && m.GetTexture(name) != null;

    static void SetKeyword(Material m, string keyword, bool on)
    {
        if (on) m.EnableKeyword(keyword);
        else m.DisableKeyword(keyword);
    }

    MaterialProperty Find(string name) => FindProperty(name, _props, false);

    float F(string name)
    {
        var p = Find(name);
        return p != null ? p.floatValue : 0f;
    }

    void P(string name, int indent = 0)
    {
        var p = Find(name);
        if (p == null) return;
        EditorGUI.indentLevel += indent;
        _editor.ShaderProperty(p, p.displayName);
        EditorGUI.indentLevel -= indent;
    }

    static bool Section(int index)
    {
        string key = "ToonShaderGUI.Fold." + Sections[index];
        bool open = SessionState.GetBool(key, index <= 2);
        bool now = EditorGUILayout.Foldout(open, Sections[index], true, EditorStyles.foldoutHeader);
        if (now != open) SessionState.SetBool(key, now);
        if (now) EditorGUI.indentLevel++;
        return now;
    }

    static void EndSection()
    {
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }
}
