using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MyGame.EditorTools
{
    public static class WorldMapEncounterBaker
    {
        const int Views = 11, Tile = 128, Resolution = 256, Columns = 16;
        const string SheetPath = "Assets/_Project/Art/UI/WorldMapEncounterViews.png";
        const string AtlasPath = "Assets/_Project/Resources/WorldMapEncounters.asset";

        [MenuItem("Tools/MyGame/World Map/Bake Encounter Silhouettes")]
        public static void Bake()
        {
            var db = AssetDatabase.LoadAssetAtPath<StageDatabase>("Assets/_Project/Data/StageDatabase.asset");
            var ids = new List<int>();
            var stages = new List<WorldMapEncounterAtlas.Stage>();
            var tiles = new List<Color32[]>();
            var cardOwner = new Dictionary<int, string>(); // monster id -> the stage whose card shows it
            foreach (var stage in db.Stages)
            {
                var scene = EditorSceneManager.OpenPreviewScene($"Assets/_Project/Scenes/Stages/{stage.SceneName}.unity");
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var bosses = roots.SelectMany(r => r.GetComponentsInChildren<BossController>()).ToArray();
                    var models = new List<int>();
                    void Add(Component actor, int id)
                    {
                        int model = ids.IndexOf(id);
                        if (model < 0)
                        {
                            var visual = (Transform)new SerializedObject(actor).FindProperty("visual").objectReferenceValue;
                            if (!visual) throw new InvalidDataException($"Missing visual on {stage.StageId}/{actor.name}");
                            model = ids.Count;
                            tiles.AddRange(Capture(visual, actor is BossController));
                            ids.Add(id);
                        }
                        if (!models.Contains(model)) models.Add(model);
                    }
                    if (bosses.Length > 0)
                        foreach (var boss in bosses) Add(boss, boss.Data.bossId);
                    else
                    {
                        if (stage.StageId.EndsWith("_B", StringComparison.Ordinal))
                            throw new InvalidDataException($"Missing boss in {stage.SceneName}");
                        var enemies = roots.SelectMany(r => r.GetComponentsInChildren<Monster>()).OrderBy(m => m.transform.position.x).ToArray();
                        if (enemies.Length == 0)
                            throw new InvalidDataException($"{stage.StageId} has no monsters: every stage card needs one or two.");
                        // The card shows the stage's featured monsters (layout `featuredMonsters`), not every monster placed in it.
                        int[] featured = Featured(stage.StageId);
                        if (featured.Length == 0)
                            featured = enemies.Select(e => e.MonsterId).Distinct().ToArray();
                        foreach (int id in featured)
                        {
                            var enemy = enemies.FirstOrDefault(e => e.MonsterId == id)
                                        ?? throw new InvalidDataException($"{stage.StageId}: featured monster {id} is not placed in the stage (rebuild the stage?).");
                            if (cardOwner.TryGetValue(id, out string other))
                                throw new InvalidDataException($"Monster {id} is on the cards of both {other} and {stage.StageId}.");
                            cardOwner[id] = stage.StageId;
                            Add(enemy, id);
                        }
                    }
                    stages.Add(new WorldMapEncounterAtlas.Stage { stageId = stage.StageId, models = models.ToArray() });
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            WriteAtlas(stages, ids, tiles);
        }

        [Serializable] private sealed class CardData { public int[] featuredMonsters; }

        private static int[] Featured(string stageId)
        {
            string path = $"Assets/_Project/Data/StageLayouts/{stageId}.json";
            if (!File.Exists(path)) return Array.Empty<int>();
            return JsonUtility.FromJson<CardData>(File.ReadAllText(path))?.featuredMonsters ?? Array.Empty<int>();
        }

        private static List<Color32[]> Capture(Transform source, bool boss)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                // The gameplay controllers turn the Visual root to face the player. Its model faces +Z.
                var model = Object.Instantiate(source.gameObject, Vector3.zero, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(model, scene);
                foreach (var ps in model.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                var renderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                if (renderers.Length == 0) throw new InvalidDataException($"No renderable mesh on {source.name}");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                var cameraGo = new GameObject("Encounter Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.targetTexture = rt;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(1, 0, 1, 0);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(.01f, bounds.extents.magnitude * 1.07f);
                camera.nearClipPlane = .01f;
                camera.farClipPlane = camera.orthographicSize * 8 + 10;
                var lightGo = new GameObject("Encounter Light", typeof(Light));
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                lightGo.transform.rotation = Quaternion.Euler(32, -36, 0);

                var frames = new Color32[Views][];
                var cropMin = new Vector2Int(Resolution, Resolution);
                var cropMax = new Vector2Int(-1, -1);
                for (int i = 0; i < Views; i++)
                {
                    float yaw = (boss ? 52f : 31f) + Mathf.Lerp(-18, 18, i / (Views - 1f));
                    var direction = Quaternion.Euler(0, yaw, 0) * new Vector3(0, .23f, 1f).normalized;
                    camera.transform.position = bounds.center + direction * (camera.orthographicSize * 3 + 1);
                    camera.transform.LookAt(bounds.center);
                    camera.Render();
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0, false);
                    var pixels = frames[i] = readback.GetPixels32();
                    Color32 background = pixels[0];
                    for (int p = 0; p < pixels.Length; p++)
                    {
                        if (pixels[p].Equals(background) || pixels[p].a < 12) { pixels[p].a = 0; continue; }
                        var xy = new Vector2Int(p % Resolution, p / Resolution);
                        cropMin = Vector2Int.Min(cropMin, xy);
                        cropMax = Vector2Int.Max(cropMax, xy);
                    }
                }
                if (cropMax.x < cropMin.x) throw new InvalidDataException($"Empty silhouette on {source.name}");
                return frames.Select(p => Engrave(p, cropMin, cropMax)).ToList();
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(readback);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // Fit all views to the same box, then ink the outline and the visible changes of material or shading.
        private static Color32[] Engrave(Color32[] pixels, Vector2Int min, Vector2Int max)
        {
            var result = new Color32[Tile * Tile];
            float step = Mathf.Max(max.x - min.x + 1, max.y - min.y + 1) / (Tile - 10f);
            Vector2 center = (Vector2)(min + max) * .5f;
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                int px = Mathf.RoundToInt(center.x + (x - (Tile - 1) * .5f) * step);
                int py = Mathf.RoundToInt(center.y + (y - (Tile - 1) * .5f) * step);
                if (px <= 1 || py <= 1 || px >= Resolution - 2 || py >= Resolution - 2) continue;
                int p = py * Resolution + px;
                Color32 c = pixels[p];
                if (c.a == 0) continue;
                float edge = Mathf.Max(Mathf.Max(Contrast(c, pixels[p-2]), Contrast(c, pixels[p+2])),
                    Mathf.Max(Contrast(c, pixels[p-2*Resolution]), Contrast(c, pixels[p+2*Resolution])));
                float luminance = (.28f * c.r + .57f * c.g + .15f * c.b) / 255;
                Color fill = Color.Lerp(new Color32(110, 63, 21, 255), new Color32(241, 184, 79, 255), .18f + luminance * .82f);
                Color gold = Color.Lerp(fill, new Color32(255, 230, 156, 255), edge);
                gold.a = c.a / 255f;
                result[y * Tile + x] = gold;
            }
            return result;
        }

        private static float Contrast(Color32 a, Color32 b)
        {
            if (b.a == 0) return 1;
            return Mathf.Clamp01((Mathf.Abs(a.r-b.r) + Mathf.Abs(a.g-b.g) + Mathf.Abs(a.b-b.b)) / 220f);
        }

        private static void WriteAtlas(List<WorldMapEncounterAtlas.Stage> stages, List<int> ids, List<Color32[]> tiles)
        {
            var sheet = new Texture2D(Columns * Tile, Mathf.CeilToInt((tiles.Count+1f)/Columns) * Tile, TextureFormat.RGBA32, false);
            try
            {
                sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
                sheet.SetPixels32(0, 0, 8, 8, Enumerable.Repeat((Color32)Color.white, 64).ToArray());
                for (int i = 0; i < tiles.Count; i++)
                    sheet.SetPixels32((i+1)%Columns * Tile, (i+1)/Columns * Tile, Tile, Tile, tiles[i]);
                sheet.Apply();
                SceneFlowSetup.EnsureFolder("Assets/_Project/Art/UI");
                File.WriteAllBytes(SheetPath, sheet.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(sheet); }
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var atlas = AssetDatabase.LoadAssetAtPath<WorldMapEncounterAtlas>(AtlasPath);
            if (!atlas)
            {
                atlas = ScriptableObject.CreateInstance<WorldMapEncounterAtlas>();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }
            atlas.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            atlas.viewCount = Views;
            atlas.tileSize = Tile;
            atlas.creatureIds = ids.ToArray();
            atlas.stages = stages.ToArray();
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssetIfDirty(atlas);
            Debug.Log($"World map portraits baked for {stages.Count} stages, {ids.Count} creatures.");
        }
    }
}
