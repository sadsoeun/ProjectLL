using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Art prefabs the stage builder uses for a theme (layout JSON "theme"). Asset: Assets/_Project/Data/Themes/{theme}.asset.
    /// An empty slot means "keep the primitive visual" for the element the slot replaces. Pivot and size conventions of the
    /// prefabs are in Docs/prop-table-schema.md. Editor only: the builder copies the art into the generated scene.
    /// </summary>
    public sealed class StageThemeKit : ScriptableObject
    {
        public const string Dir = "Assets/_Project/Data/Themes";

        [Header("Terrain modules (2 x 2 x 4, pivot top-left / depth centre)")]
        public GameObject groundCore;       // T01
        public GameObject groundEndLeft;    // T02
        public GameObject groundEndRight;   // T03
        public GameObject rockCore;         // T04

        [Header("Terrain variants (optional): same size, pivot and seam profiles as the core module")]
        public GameObject[] groundCoreVariants; // T01_*_V*: picked per cell together with groundCore
        public GameObject[] rockCoreVariants;   // T04_*_V*: picked per cell together with rockCore
        public GameObject iceSurface;           // T05-like cap module for blocks with surface "ice" (schema v1.5); empty = groundCore

        [Header("Platforms (pivot top-left / depth centre)")]
        public GameObject floatingPlatform; // T06, 3 x 1 x 4, used for platform blocks thicker than 0.5 m
        public GameObject timberPlatform;   // T07, 3 x 0.4 x 4, used for platform blocks up to 0.5 m
        public GameObject crumbleSlab;      // T11 fractured slab for blocks with surface "crumble": 3 x 0.5 x 4, chunks under "Visual"; empty = themed platform + crack marks
        public GameObject platformSupport;  // T10 pier: pivot = head top centre, shaft behind the lane, 24 m tall (sinks into whatever is below); empty = none

        [Header("Gameplay visuals")]
        public GameObject checkpointVisual; // G01, pivot bottom centre (pole only; the banner stays a primitive)
        public GameObject goalVisual;       // G02, pivot bottom centre
        public GameObject coinVisual;       // G03a, pivot centre
        public GameObject gemVisual;        // G03b, pivot centre

        [Header("Gimmick visuals (optional, schema v1.4): empty = the primitive look")]
        public GameObject fallerVisual;     // falling hazard (SunDunes: SD_G08_Platform_Breakaway, pivot top-left / depth centre, 3 x 0.4 x 4)
        public GameObject moverVisual;      // moving platform (SunDunes: SD_G07_Platform_Thin, pivot top-left / depth centre, 3 x 0.4 x 4)
        public GameObject spikeTrapVisual;  // timed spike trap unit (SunDunes: SD_P12_SpikeTrap, pivot bottom centre, 1.5 m wide, children Spike_01..03 rise +0.58 local Y)
        public GameObject switchVisual;     // pressure plate (SunDunes: SD_P11_SunSwitch, pivot bottom centre, children Plate / Emblem)
        public GameObject gateVisual;       // sliding gate (SunDunes: SD_P13_SunGate, pivot bottom centre, 1 x 3 x 1.5)
        public GameObject spikeVisual;      // static spikes unit (replaces the red pyramids), pivot bottom centre

        [Header("Boss arena (optional): empty = the primitive look")]
        public GameObject arenaWallVisual;  // layout block kind "wall", authored 2 x 20 x 4, pivot = block centre (arena face at -X)
        public GameObject arenaGateVisual;  // boss entrance SlidingGate, authored 1 x 8 x 3, pivot bottom centre (falls back to gateVisual)

        [Header("Atmosphere (optional): sky colour, distance fog and sun tint applied by a StageAtmosphere in the stage")]
        public bool useAtmosphere;
        public Color skyColor = new Color(0.55f, 0.75f, 0.95f);
        public Color fogColor = new Color(0.6f, 0.7f, 0.85f);
        [Min(0f)] public float fogStart = 20f;
        [Min(1f)] public float fogEnd = 140f;
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1.1f;
        public Material skyMaterial;  // optional backdrop quad (MyGame/Sky/ToonSky) far behind the level; empty = flat sky colour

        [Header("Screen post-processing (optional): volume profile applied by a StageScreenWeather in the stage")]
        public UnityEngine.Rendering.VolumeProfile screenFxProfile;  // e.g. FrostPeaks_ScreenFx (blizzard + frozen edges)

        [Header("Back fill (optional): ground continued behind the play lane so mid-ground props stand on something")]
        [Min(0f)] public float backFillDepth;   // metres behind the lane's back edge; 0 = none (themes with a floor prop)
        public Material backFillBody;
        [Tooltip("Themes that already have a floor prop at y = 0: only fill behind ground raised above it.")]
        public bool backFillRaisedOnly;           // earth / cliff below the grass cap (seen at pit sides)
        [Min(0f)] public float backFillFarBand;   // metres at the far side of the back fill that continue across pits (scenery behind a pit keeps ground under it); 0 = off

        [Header("Ambient life (optional)")]
        [Header("Auto dressing (optional): small props scattered by the builder so the ground never reads as an empty plane")]
        public GameObject[] dressSmall;    // tufts, flowers, pebbles (pivot bottom centre)
        public GameObject[] dressMedium;   // shrubs, rocks, young trees
        public GameObject[] dressLarge;    // trees / tall rocks for the row that hides the far edge of the back ground
        [Min(0f)] public float dressDepth;                     // metres behind the lane to dress; 0 = off
        [Range(0f, 1f)] public float dressMidDensity = 0.5f;   // behind the lane
        [Range(0f, 1f)] public float dressLipDensity = 0.5f;   // small props on the front and back lips of the lane

        [Min(0)] public int ambientSeagulls;  // background gulls added to the stage (CoralCoast); 0 = none

        public static string PathFor(string theme) => $"{Dir}/{theme}.asset";

        /// <summary>Letters, digits, '_' and '-' only (the theme name is a file name).</summary>
        public static bool IsValidThemeName(string theme) => StageMapPaths.IsValidStageId(theme);
    }

    /// <summary>
    /// Theme kit assets: creating / refreshing them from the art folder convention and listing them for the map tool.
    /// Menu: Tools/MyGame/Map Tool/Create Or Refresh Theme Kit.
    /// </summary>
    public static class ThemeKitTool
    {
        public const string DefaultTheme = "GreenMeadow";
        public const string PrefabRoot = "Assets/_Project/Prefabs/Environment";

        // field name -> file name prefix of the prefab ({ID}_{Name}.prefab, Docs/prop-table-schema.md)
        private static readonly (string field, string idPrefix)[] Slots =
        {
            ("groundCore", "T01_"), ("groundEndLeft", "T02_"), ("groundEndRight", "T03_"), ("rockCore", "T04_"),
            ("floatingPlatform", "T06_"), ("timberPlatform", "T07_"),
            ("checkpointVisual", "G01_"), ("goalVisual", "G02_"), ("coinVisual", "G03a_"), ("gemVisual", "G03b_"),
        };

        [MenuItem("Tools/MyGame/Map Tool/Create Or Refresh Theme Kit")]
        public static void CreateOrRefreshMenu() => Debug.Log(CreateOrRefresh());

        /// <summary>Names of all theme kit assets (file name without extension), sorted.</summary>
        public static List<string> FindThemeNames()
        {
            var names = new List<string>();
            if (!AssetDatabase.IsValidFolder(StageThemeKit.Dir))
                return names;
            foreach (var guid in AssetDatabase.FindAssets("t:StageThemeKit", new[] { StageThemeKit.Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".asset", StringComparison.Ordinal) && Path.GetDirectoryName(path).Replace('\\', '/') == StageThemeKit.Dir)
                    names.Add(Path.GetFileNameWithoutExtension(path));
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>
        /// Creates the GreenMeadow kit if missing and fills its empty slots from Prefabs/Environment/{theme}/{ID}_*.prefab
        /// (slots that already hold a prefab are kept). Also refreshes the other existing kits. Returns the report.
        /// </summary>
        public static string CreateOrRefresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating theme kits.");

            var themes = FindThemeNames();
            if (!themes.Contains(DefaultTheme))
                themes.Insert(0, DefaultTheme);

            var sb = new StringBuilder("[ThemeKitTool] Theme kits:\n");
            foreach (var theme in themes)
                sb.AppendLine(RefreshOne(theme));
            AssetDatabase.SaveAssets();
            return sb.ToString().TrimEnd();
        }

        private static string RefreshOne(string theme)
        {
            string path = StageThemeKit.PathFor(theme);
            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(path);
            bool created = false;
            if (kit == null)
            {
                SceneFlowSetup.EnsureFolder(StageThemeKit.Dir);
                kit = ScriptableObject.CreateInstance<StageThemeKit>();
                AssetDatabase.CreateAsset(kit, path);
                created = true;
            }

            string folder = $"{PrefabRoot}/{theme}";
            var so = new SerializedObject(kit);
            var filled = new List<string>();
            var empty = new List<string>();
            foreach (var (field, prefix) in Slots)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                    throw new InvalidOperationException("StageThemeKit has no field " + field);
                if (prop.objectReferenceValue != null)
                    continue;
                var prefab = FindPrefab(folder, prefix);
                if (prefab != null)
                {
                    prop.objectReferenceValue = prefab;
                    filled.Add(field);
                }
                else
                {
                    empty.Add(field);
                }
            }
            FillVariants(so, "groundCoreVariants", "groundCore", folder, filled);
            FillVariants(so, "rockCoreVariants", "rockCore", folder, filled);
            if (so.ApplyModifiedPropertiesWithoutUndo())
                EditorUtility.SetDirty(kit);

            return $"  {path}: {(created ? "created" : "existing")}, filled {filled.Count} ({string.Join(", ", filled)}), empty {empty.Count}";
        }

        /// <summary>
        /// Fills an empty variant array with {core prefab name}_V*.prefab files next to the core prefab, sorted by name.
        /// A non-empty array is kept (hand edits win), like the single slots.
        /// </summary>
        private static void FillVariants(SerializedObject so, string arrayField, string coreField, string folder, List<string> filled)
        {
            var array = so.FindProperty(arrayField);
            var core = so.FindProperty(coreField).objectReferenceValue as GameObject;
            if (array == null || core == null || array.arraySize > 0 || !AssetDatabase.IsValidFolder(folder))
                return;
            string corePath = AssetDatabase.GetAssetPath(core);
            string dir = Path.GetDirectoryName(corePath).Replace('\\', '/');
            string prefix = Path.GetFileNameWithoutExtension(corePath) + "_V";
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') == dir && Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal))
                    paths.Add(path);
            }
            if (paths.Count == 0)
                return;
            paths.Sort(StringComparer.Ordinal);
            array.arraySize = paths.Count;
            for (int i = 0; i < paths.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            filled.Add($"{arrayField}[{paths.Count}]");
        }

        private static GameObject FindPrefab(string folder, string idPrefix)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return null;
            string best = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileName(path);
                if (file.StartsWith(idPrefix, StringComparison.Ordinal) && (best == null || string.CompareOrdinal(path, best) < 0))
                    best = path;
            }
            return best != null ? AssetDatabase.LoadAssetAtPath<GameObject>(best) : null;
        }
    }
}
