using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Ambient animation baked into the Green Meadow prefabs, so every scene using them (stages, title backdrop) animates:
    ///   water  - GM_Water / GM_WaterLight -> MyGame/Ambient/Water (Sea; waterfall faces get falling streaks), GM_Foam -> Water (Foam);
    ///   wind   - foliage of V01/V02/V03/V04/B02 gets per-prefab wind material variants (Materials/Wind/*), height-weighted
    ///            in object space because the meshes carry no vertex colours; trunks and terrain stay static;
    ///   idle   - windmill rotor spins, clouds drift, mist at the waterfall base.
    /// Menu: Tools/MyGame/Art/Apply Green Meadow Ambient. Idempotent.
    /// </summary>
    public static class GreenMeadowAmbient
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/GreenMeadow/";
        private const string MaterialDir = "Assets/_Project/Art/Environment/GreenMeadow/Materials/";
        private const string WindMaterialDir = MaterialDir + "Wind/";

        private struct WindSpec
        {
            public string[] Materials;     // base materials that sway on this prefab
            public float Start, End;       // object-space height range of the sway weight
            public float Strength, Speed, Flutter;
        }

        private static readonly string[] Foliage = { "GM_FoliageDark", "GM_GrassDark", "GM_GrassMid", "GM_GrassLight" };

        private static readonly Dictionary<string, WindSpec> Wind = new()
        {
            { "V01_BroadleafOak", new WindSpec { Materials = Foliage, Start = 1.2f, End = 4.0f, Strength = 0.05f, Speed = 0.8f } },
            { "V02_RoundShrub", new WindSpec { Materials = Foliage, Start = 0f, End = 0.76f, Strength = 0.02f, Speed = 0.9f } },
            { "V03_GrassTuft", new WindSpec { Materials = Foliage, Start = 0f, End = 0.5f, Strength = 0.05f, Speed = 1.3f, Flutter = 0.04f } },
            { "V04_FlowerCluster", new WindSpec { Materials = new[] { "GM_GrassMid", "GM_GrassLight", "GM_FlowerPink", "GM_FlowerWhite", "GM_Coin" }, Start = 0f, End = 0.44f, Strength = 0.04f, Speed = 1.3f, Flutter = 0.04f } },
            { "B02_ForestMass", new WindSpec { Materials = Foliage, Start = 2f, End = 7f, Strength = 0.08f, Speed = 0.6f } },
        };

        [MenuItem("Tools/MyGame/Art/Apply Green Meadow Ambient")]
        public static void ApplyMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            var log = new System.Text.StringBuilder("[GreenMeadowAmbient]\n");

            foreach (var name in new[] { "GM_Water", "GM_WaterLight" })
                WorldMapAmbient.MakeWater(LoadMat(name), "_MODE_SEA", 0, 3000);
            WorldMapAmbient.MakeWater(LoadMat("GM_Foam"), "_MODE_FOAM", 2, 3001);
            log.AppendLine("  water: GM_Water, GM_WaterLight (sea + falling streaks), GM_Foam (foam)");

            foreach (var (prefab, spec) in Wind)
                Edit(prefab, root =>
                {
                    var r = root.GetComponent<Renderer>();
                    var mats = r.sharedMaterials;
                    int swapped = 0;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string baseName = mats[i].name.Split(new[] { "_Wind_" }, System.StringSplitOptions.None)[0];
                        if (!spec.Materials.Contains(baseName)) continue;
                        mats[i] = WindVariant(LoadMat(baseName), prefab, spec);
                        swapped++;
                    }
                    r.sharedMaterials = mats;
                    log.AppendLine($"  wind: {prefab} {swapped} materials, sway {spec.Strength} m over y {spec.Start}..{spec.End}");
                });

            Edit("A01_Windmill", root =>
            {
                var rotor = root.transform.Find("A01_Windmill_Rotor");
                var spin = rotor.GetComponent<AmbientSpin>() ?? rotor.gameObject.AddComponent<AmbientSpin>();
                spin.Configure(Vector3.forward, 30f); // rotor faces -Z, hub at its pivot
                log.AppendLine("  idle: A01_Windmill rotor spins (local Z, 30 deg/s)");
            });

            Edit("B05_CloudMass", root =>
            {
                if (root.GetComponent<AmbientDrift>() == null) root.AddComponent<AmbientDrift>();
                log.AppendLine("  idle: B05_CloudMass drifts");
            });

            Edit("B04_WaterfallCliff", root =>
            {
                var old = root.transform.Find("FX_Mist");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var mist = (GameObject)PrefabUtility.InstantiatePrefab(WorldMapAmbient.EffectPrefab("Mist"), root.transform);
                mist.name = "FX_Mist";
                mist.transform.localPosition = new Vector3(0f, 0.5f, -1.6f); // the pool at the foot of the falls
                log.AppendLine("  idle: B04_WaterfallCliff mist at the pool");
            });

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static Material LoadMat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat") ?? throw new System.InvalidOperationException("Missing material " + name);

        private static void Edit(string prefabName, System.Action<GameObject> change)
        {
            string path = PrefabDir + prefabName + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material WindVariant(Material source, string prefab, WindSpec spec)
        {
            SceneFlowSetup.EnsureFolder(WindMaterialDir.TrimEnd('/'));
            string path = $"{WindMaterialDir}{source.name}_Wind_{prefab.Substring(0, 3)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(WorldMapAmbient.WindShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            mat.SetColor("_EmissionColor", source.IsKeywordEnabled("_EMISSION") ? source.GetColor("_EmissionColor") : Color.black);
            mat.SetFloat("_WindStrength", spec.Strength);
            mat.SetFloat("_WindSpeed", spec.Speed);
            mat.SetFloat("_Flutter", spec.Flutter);
            mat.SetFloat("_HeightWeight", 1f);
            mat.SetVector("_HeightRange", new Vector4(spec.Start, spec.End, 0f, 0f));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
