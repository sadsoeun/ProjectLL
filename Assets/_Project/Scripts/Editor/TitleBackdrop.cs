using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Green Meadow diorama behind the title menu, built from the Stage 1 environment prefabs (visual only).
    /// Called by the scene generator (CreateTitleScene) and by
    ///   Tools/MyGame/Title/Rebuild Title Backdrop  - replaces the backdrop in the existing 03_Title scene.
    /// The menu sits in the screen centre, so the middle of the ground stays low and the logo area (top centre) free of clouds.
    /// </summary>
    public static class TitleBackdrop
    {
        public const string RootName = "Backdrop";
        private const string ScenePath = SceneFlowSetup.ScenesDir + "/03_Title.unity";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/GreenMeadow/";

        // Placeholder primitives of the first title layout; removed when the backdrop is applied.
        private static readonly string[] LegacyNames = { "Ground", "Block_A", "Block_B", "Block_C" };

        private struct Item
        {
            public string Prefab;
            public Vector3 Position;
            public float RotY;
            public float Scale;

            public Item(string prefab, float x, float y, float z, float rotY = 0f, float scale = 1f)
            {
                Prefab = prefab;
                Position = new Vector3(x, y, z);
                RotY = rotY;
                Scale = scale;
            }
        }

        private static readonly Item[] Props =
        {
            // landmarks and trees behind the play line
            new("A01_Windmill", -11f, 0f, 12f, 20f),
            new("A02_Watchtower", 13f, 0f, 14f, -20f),
            new("V01_BroadleafOak", -15f, 0f, 6f),
            new("V01_BroadleafOak", -5f, 0f, 10f, 40f, 0.9f),
            new("V01_BroadleafOak", 4.5f, 0f, 11f, 120f),
            new("V01_BroadleafOak", 16f, 0f, 7f, 200f),
            new("V02_RoundShrub", -11f, 0f, 1.5f),
            new("V02_RoundShrub", -5.5f, 0f, 3.5f, 30f, 0.8f),
            new("V02_RoundShrub", 6.5f, 0f, 4f, 160f),
            new("V02_RoundShrub", 12.5f, 0f, 1f, 80f, 1.2f),
            new("P01_TimberFence", -7.5f, 0f, 1.2f),
            new("P01_TimberFence", -5.5f, 0f, 1.2f),
            new("P01_TimberFence", 8f, 0f, 1.5f),
            new("P01_TimberFence", 10f, 0f, 1.5f),
            new("P02_DirectionSign", -11.5f, 0f, -1f),
            new("V05_StoneCluster", -7f, 0f, 3f, 20f, 0.8f),
            new("V05_StoneCluster", 13.5f, 0f, -1.5f, -40f, 0.7f),
            // ground dressing near the front edge
            new("V04_FlowerCluster", -9.5f, 0f, -2.5f),
            new("V04_FlowerCluster", -4f, 0f, -2.5f, 90f),
            new("V04_FlowerCluster", 5f, 0f, -2.8f, 45f),
            new("V04_FlowerCluster", 8.5f, 0f, -1.2f, 200f),
            new("V03_GrassTuft", -13f, 0f, -2.5f),
            new("V03_GrassTuft", -6.5f, 0f, -3f, 60f),
            new("V03_GrassTuft", -4.5f, 0f, -0.5f, 120f),
            new("V03_GrassTuft", 4f, 0f, -1.5f, 200f),
            new("V03_GrassTuft", 7f, 0f, -3.2f, 30f),
            new("V03_GrassTuft", 11f, 0f, -2f, 300f),
            // far background
            // Eye height is y = 3 and the horizon sits ~30% from the top of the screen; far pieces stay low so the
            // logo band (top ~6-25%, centre) shows sky.
            new("B02_ForestMass", -16f, -1f, 20f),
            new("B02_ForestMass", 3f, -1.5f, 22f, 180f),
            new("B02_ForestMass", 22f, -1f, 20f),
            new("B01_RollingHill", -26f, -3.5f, 34f),
            new("B01_RollingHill", 0f, -4f, 38f, 0f, 1.2f),
            new("B01_RollingHill", 27f, -3.5f, 33f, 180f),
            new("B03_SeaStackCluster", -40f, -4f, 46f),
            new("B04_WaterfallCliff", 40f, -4f, 44f, -20f),
            new("B05_CloudMass", -34f, 12f, 66f),
            new("B05_CloudMass", 32f, 14f, 70f, 180f),
            new("B05_CloudMass", -52f, 7f, 80f, 0f, 0.9f),
            new("B05_CloudMass", 55f, 8f, 84f, 0f, 0.9f),
        };

        [MenuItem("Tools/MyGame/Title/Rebuild Title Backdrop")]
        public static void RebuildMenu() => Debug.Log(Rebuild());

        /// <summary>Opens 03_Title, replaces the backdrop and saves the scene. Returns a report.</summary>
        public static string Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding the title backdrop.");
            SceneFlowSetup.SaveDirtyScenesQuietly();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var env = GameObject.Find("Environment");
            if (env == null)
                throw new InvalidOperationException("03_Title has no 'Environment' root.");
            int count = Build(env.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"[TitleBackdrop] {count} objects placed in {ScenePath}.";
        }

        /// <summary>Replaces Environment/Backdrop (and the old placeholder blocks). Returns the number of placed objects.</summary>
        public static int Build(Transform env)
        {
            foreach (string name in LegacyNames)
            {
                var old = env.Find(name);
                if (old != null)
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var previous = env.Find(RootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(env, false);
            int count = 0;

            // Ground: rows of grass cliff (pivot top-left, depth centre) out to z = 20, rock below the front face.
            // Rows further back see a wider strip, so they run wider.
            var ground = new GameObject("Ground").transform;
            ground.SetParent(root, false);
            for (int x = -30; x < 30; x += 2)
            {
                for (int row = 0; row < 6; row++)
                {
                    float z = -2f + row * 4f;
                    // Each row sits 1 cm lower so the grass overhangs of neighbouring rows never z-fight.
                    if (Mathf.Abs(x + 1) < 14f + z) // tile centre inside the visible strip at this depth
                        count += Place(ground, new Item("T01_GrassCliffCore", x, -0.01f * row, z));
                }
                if (x >= -18 && x < 18)
                {
                    count += Place(ground, new Item("T04_BareRockCore", x, -2f, -2f));
                    count += Place(ground, new Item("T04_BareRockCore", x, -4f, -2f));
                }
            }

            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            foreach (var item in Props)
                count += Place(props, item);
            return count;
        }

        private static int Place(Transform parent, Item item)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + item.Prefab + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[TitleBackdrop] Missing prefab: " + item.Prefab);
                return 0;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = item.Position;
            go.transform.localRotation = Quaternion.Euler(0f, item.RotY, 0f);
            go.transform.localScale = Vector3.one * item.Scale;
            return 1;
        }
    }
}
