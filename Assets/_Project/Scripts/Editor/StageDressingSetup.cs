using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Stage quality pass, per theme kit: a gradient sky backdrop (MyGame/Sky/ToonSky) with gentle distance haze for the
    /// worlds that had a flat sky, and the auto-dressing prop lists the stage builder scatters behind the lane and on its
    /// lips (StageLayoutBuilder.BuildAutoDressing). Idempotent; values stay editable on the kits / materials afterwards
    /// (existing sky materials are never overwritten). Rebuild the stages to see the result.
    /// </summary>
    public static class StageDressingSetup
    {
        private const string ThemeDir = "Assets/_Project/Data/Themes/";
        private const string SkyDir = "Assets/_Project/Art/Materials/Sky/";
        private const string PropDir = "Assets/_Project/Prefabs/Environment/";

        [MenuItem("Tools/MyGame/Art/Setup Stage Dressing")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new System.Text.StringBuilder();

            // ---- W1 GreenMeadow: mesh clouds already exist, so the sky is a clean gradient + sun glow.
            Sky("GreenMeadow", log, new Color(0.27f, 0.53f, 0.90f), new Color(0.74f, 0.88f, 0.98f), 0.20f, 0.75f,
                new Color(1f, 0.96f, 0.80f), new Vector2(0.72f, 0.74f), 0.26f,
                Color.white, new Color(0.80f, 0.87f, 0.96f), new Vector2(2.2f, 7f), 0f, new Vector2(0.5f, 0f), 0.004f);
            Kit("GreenMeadow", log, kit =>
            {
                kit.useAtmosphere = true;
                kit.skyColor = new Color(0.55f, 0.75f, 0.95f);
                kit.fogColor = new Color(0.72f, 0.85f, 0.96f);
                kit.fogStart = 38f;
                kit.fogEnd = 260f;
                kit.sunColor = new Color(1f, 0.98f, 0.94f);
                kit.sunIntensity = 1.1f;
                kit.dressSmall = Props("GreenMeadow/V03_GrassTuft", "GreenMeadow/V04_FlowerCluster", "GreenMeadow/V03_GrassTuft",
                    "GreenMeadow/V05_StoneCluster");
                kit.dressMedium = Props("GreenMeadow/V02_RoundShrub", "GreenMeadow/V02_RoundShrub", "GreenMeadow/V05_StoneCluster");
                kit.dressLarge = Props("GreenMeadow/V01_BroadleafOak");
                kit.dressDepth = 16f;
                kit.dressMidDensity = 0.5f;
                kit.dressLipDensity = 0.45f;
                kit.backFillFarBand = 6f; // the forest masses behind the pits get ground under them
            });

            // ---- W2 SunDunes: warm desert sky with a few high clouds; raised ground gets a back fill like the others.
            Sky("SunDunes", log, new Color(0.24f, 0.50f, 0.86f), new Color(0.98f, 0.86f, 0.66f), 0.20f, 0.7f,
                new Color(1f, 0.90f, 0.66f), new Vector2(0.70f, 0.70f), 0.30f,
                new Color(1f, 0.97f, 0.92f), new Color(0.93f, 0.80f, 0.70f), new Vector2(2.4f, 7f), 0.30f, new Vector2(0.55f, 0.97f), 0.003f);
            Kit("SunDunes", log, kit =>
            {
                kit.useAtmosphere = true;
                kit.skyColor = new Color(0.56f, 0.72f, 0.92f);
                kit.fogColor = new Color(0.96f, 0.84f, 0.68f);
                kit.fogStart = 40f;
                kit.fogEnd = 260f;
                kit.sunColor = new Color(1f, 0.96f, 0.88f);
                kit.sunIntensity = 1.1f;
                kit.backFillDepth = 8f;
                kit.backFillRaisedOnly = true;
                kit.dressSmall = Props("SunDunes/Props/SD_V01_DryGrassTuft", "SunDunes/Props/SD_V04_DesertMarigold",
                    "SunDunes/Props/SD_V01_DryGrassTuft", "SunDunes/Props/SD_V03_BarrelCactus");
                kit.dressMedium = Props("SunDunes/Props/SD_V02_SageShrub", "SunDunes/Props/SD_P03_Rock_Set_A",
                    "SunDunes/Props/SD_P03_Rock_Set_B", "SunDunes/Props/SD_P02_Cactus_Round", "SunDunes/Props/SD_P03_Rock_Set_C");
                kit.dressLarge = Props("SunDunes/Props/SD_P02_Cactus_Tall", "SunDunes/Props/SD_P01_Palm");
                kit.dressDepth = 8f;
                kit.dressMidDensity = 0.4f;
                kit.dressLipDensity = 0.4f;
            });

            // ---- W3 FrostPeaks: sky and atmosphere already tuned; dressing only.
            Kit("FrostPeaks", log, kit =>
            {
                kit.dressSmall = Props("FrostPeaks/Props/FP_P04_WinterShrub");
                kit.dressMedium = Props("FrostPeaks/Props/FP_P03_SnowBoulders", "FrostPeaks/Props/FP_P04_WinterShrub",
                    "FrostPeaks/Props/FP_P05_IceCrystals", "FrostPeaks/Props/FP_P02_YoungFir");
                kit.dressLarge = Props("FrostPeaks/Props/FP_P01_SnowFir", "FrostPeaks/Props/FP_P02_YoungFir");
                kit.dressDepth = 8f;
                kit.dressMidDensity = 0.4f;
                kit.dressLipDensity = 0.3f;
            });

            // ---- W4 CoralCoast: bright tropical sky with puffy clouds.
            Sky("CoralCoast", log, new Color(0.16f, 0.50f, 0.90f), new Color(0.72f, 0.92f, 0.98f), 0.20f, 0.7f,
                new Color(1f, 0.97f, 0.84f), new Vector2(0.74f, 0.76f), 0.24f,
                Color.white, new Color(0.76f, 0.88f, 0.97f), new Vector2(2.2f, 6.5f), 0.36f, new Vector2(0.4f, 0.97f), 0.004f);
            Kit("CoralCoast", log, kit =>
            {
                kit.dressSmall = Props("CoralCoast/Props/CC_P05_Shells", "CoralCoast/Props/CC_P02_BeachPlant");
                kit.dressMedium = Props("CoralCoast/Props/CC_P02_BeachPlant", "CoralCoast/Props/CC_P12_CoastRocks",
                    "CoralCoast/Props/CC_P04_DomeCoral", "CoralCoast/Props/CC_P03_BranchCoral");
                kit.dressLarge = new GameObject[0];
                kit.dressDepth = 5f;
                kit.dressMidDensity = 0.45f;
                kit.dressLipDensity = 0.3f;
            });

            // ---- W5 EmberFortress: backdrop stays as it is (calm lava, dusk sky); sparse dressing only.
            Kit("EmberFortress", log, kit =>
            {
                kit.dressSmall = Props("EmberFortress/Props/EF_P04_AshShrub");
                kit.dressMedium = Props("EmberFortress/Props/EF_P02_CooledLavaRocks", "EmberFortress/Props/EF_P04_AshShrub",
                    "EmberFortress/Props/EF_P01_ObsidianCrystal");
                kit.dressLarge = new GameObject[0];
                kit.dressDepth = 3f;
                kit.dressMidDensity = 0.35f;
                kit.dressLipDensity = 0.25f;
            });

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static void Kit(string theme, System.Text.StringBuilder log, System.Action<StageThemeKit> edit)
        {
            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(ThemeDir + theme + ".asset");
            if (kit == null)
            {
                log.AppendLine("MISSING kit " + theme);
                return;
            }
            edit(kit);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyDir + "M_Sky_" + theme + ".mat");
            if (kit.skyMaterial == null && sky != null)
                kit.skyMaterial = sky;
            EditorUtility.SetDirty(kit);
            log.AppendLine($"{theme}: small {Count(kit.dressSmall)}, medium {Count(kit.dressMedium)}, large {Count(kit.dressLarge)}, " +
                           $"depth {kit.dressDepth}, sky {(kit.skyMaterial != null ? kit.skyMaterial.name : "-")}, atmosphere {kit.useAtmosphere}");
        }

        private static int Count(GameObject[] a) => a == null ? 0 : a.Length;

        private static GameObject[] Props(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (var n in names)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(PropDir + n + ".prefab");
                if (p != null)
                    list.Add(p);
                else
                    Debug.LogWarning("[StageDressingSetup] prefab not found: " + n);
            }
            return list.ToArray();
        }

        private static void Sky(string theme, System.Text.StringBuilder log, Color zenith, Color horizon, float horizonHeight, float power,
            Color sun, Vector2 sunPos, float sunSize, Color cloud, Color cloudShade, Vector2 cloudScale, float cover, Vector2 band, float speed)
        {
            string path = SkyDir + "M_Sky_" + theme + ".mat";
            if (File.Exists(path))
                return; // hand-tuned values win
            var shader = Shader.Find("MyGame/Sky/ToonSky");
            if (shader == null)
            {
                log.AppendLine("MISSING shader MyGame/Sky/ToonSky");
                return;
            }
            var m = new Material(shader);
            m.SetColor("_ZenithColor", zenith);
            m.SetColor("_HorizonColor", horizon);
            m.SetFloat("_HorizonHeight", horizonHeight);
            m.SetFloat("_GradientPower", power);
            m.SetColor("_SunColor", sun);
            m.SetVector("_SunPos", sunPos);
            m.SetFloat("_SunSize", sunSize);
            m.SetColor("_CloudColor", cloud);
            m.SetColor("_CloudShade", cloudShade);
            m.SetVector("_CloudScale", cloudScale);
            m.SetFloat("_CloudCover", cover);
            m.SetVector("_CloudBand", band);
            m.SetFloat("_CloudSpeed", speed);
            AssetDatabase.CreateAsset(m, path);
            log.AppendLine("created " + path);
        }
    }
}
