using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Builds the boss prefabs from the delivered models. Reads Docs/art/bosses/manifest/*.json (category "Bosses"), sets up the
    /// toon material (MyGame/Toon/Lit, emission map for the glowing parts), strips the "Armature|" / "Name|Name|" take prefixes
    /// of the clips, creates an Animator controller with one state per clip (Idle / Move / Roar / Anticipate / Attack / Stunned /
    /// Hit / Defeat / Shake; BossController plays them by name), a Visual prefab (Generic rig) and the gameplay prefab
    /// (Rigidbody + trigger BoxCollider + BossController; the "WeakPoint" bone becomes the stomp height). The gameplay prefab is
    /// stored in Data/Bosses/Boss_{id}.asset (BossData.prefab). An optional static acorn mesh (manifest extra_meshes) becomes the
    /// falling hazard of the Sprout King. Idempotent. Menu: Tools/MyGame/Art/Import Bosses.
    /// </summary>
    public static class BossImporter
    {
        private const string PrefabDir = BossSetup.PrefabDir;
        private static readonly string[] ClipNames = { "Idle", "Move", "Roar", "Anticipate", "Attack", "Stunned", "Hit", "Defeat", "Shake", "Charge", "Slam" };
        // Optional takes: Shake (rain), Charge / Slam (dedicated attack moves; BossController falls back to Attack without them).
        private static readonly string[] OptionalClips = { "Shake", "Charge", "Slam" };
        private static readonly string[] LoopClips = { "Idle", "Move", "Stunned", "Charge" };
        private static readonly string[] GlowParts = { "Core", "Crest", "Crown", "Gem" };

        // boss_id fallback when a manifest does not carry it
        private static readonly Dictionary<string, int> BossIds = new Dictionary<string, int>
        {
            ["GM_BOSS_SproutKing"] = 9001,
            ["SD_BOSS_SunGuardian"] = 9002,
            ["FP_BOSS_FrostMammoth"] = 9003,
        };

        private static string ManifestDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/art/bosses/manifest"));

        [MenuItem("Tools/MyGame/Art/Import Bosses")]
        public static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing bosses.");
            var log = new StringBuilder("[BossImporter]\n");
            AssetDatabase.Refresh();
            log.AppendLine("  " + BossSetup.EnsureAssets());
            if (!Directory.Exists(ManifestDir))
            {
                log.AppendLine("  ! manifest folder missing: " + ManifestDir);
                return log.ToString().TrimEnd();
            }
            int imported = 0;
            foreach (var file in Directory.GetFiles(ManifestDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                try
                {
                    var m = JObject.Parse(File.ReadAllText(file));
                    if ((string)m["category"] != "Bosses")
                        continue;
                    if (ImportBoss(m, log))
                        imported++;
                }
                catch (Exception e)
                {
                    log.AppendLine($"  ! {Path.GetFileName(file)}: {e.GetType().Name}: {e.Message}");
                    Debug.LogException(e);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.AppendLine($"  bosses imported: {imported}");
            return log.ToString().TrimEnd();
        }

        private static bool ImportBoss(JObject m, StringBuilder log)
        {
            string name = (string)m["name"];
            string fbx = (string)m["fbx"];
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fbx))
                throw new InvalidOperationException("manifest needs name and fbx.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
            {
                log.AppendLine($"  - {name}: FBX not found ({fbx}), skipped");
                return false;
            }
            int bossId = (int?)m["boss_id"] ?? (BossIds.TryGetValue(name, out int known) ? known : 0);
            var data = BossCatalog.Load(bossId);
            if (data == null)
                throw new InvalidOperationException($"{name}: no BossData for boss id {bossId} (add boss_id to the manifest).");

            var mi = SunDunesImporter.ConfigureModel(fbx, true);
            ConfigureClips(mi);

            string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
            var albedo = SunDunesImporter.ConfigureTexture($"{dir}/{(string)m["albedo"]}", true);
            var emission = m["emission"]?.Type == JTokenType.String ? SunDunesImporter.ConfigureTexture($"{dir}/{(string)m["emission"]}", true) : null;
            var mat = SunDunesImporter.EnsureToonMaterial($"{dir}/M_{name}.mat", albedo, emission, false, false);

            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
            var controller = BuildController($"{dir}/{name}.controller", clips);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            SceneFlowSetup.EnsureFolder(PrefabDir);

            // Visual prefab: model + Animator (generic rig).
            string visualPath = $"{PrefabDir}/{name}_Visual.prefab";
            var visualRoot = new GameObject(name + "_Visual");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visualRoot.transform);
                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // the weak point bone must keep moving off screen too
                ApplyMaterial(visualRoot, mat);
                PrefabUtility.SaveAsPrefabAsset(visualRoot, visualPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visualRoot);
            }

            // Gameplay prefab.
            string gameplayPath = $"{PrefabDir}/{name}.prefab";
            SceneFlowSetup.NewScene();
            var root = new GameObject(name);
            root.AddComponent<Rigidbody>();
            root.AddComponent<BoxCollider>();
            var boss = root.AddComponent<BossController>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(visualPath), visual);
            visualInstance.transform.localPosition = Vector3.zero;
            var weak = FindDeep(visualInstance.transform, "WeakPoint");
            var glow = visualInstance.GetComponentsInChildren<Renderer>(true)
                .Where(r => GlowParts.Any(p => r.name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (glow.Count == 0)
                glow = visualInstance.GetComponentsInChildren<Renderer>(true).ToList();
            EditorWire.Set(boss, "visual", visual);
            EditorWire.Set(boss, "animator", visualInstance.GetComponentInChildren<Animator>());
            EditorWire.Set(boss, "weakPoint", weak);
            EditorWire.Set(boss, "glowRenderers", glow);
            EditorWire.Set(boss, "tintBase", false);
            bool weakFound = weak != null; // read before SavePrefab destroys the scene objects
            SceneFlowSetup.SavePrefab(root, gameplayPath);

            // Re-load: model reimport / NewScene can unload the instance loaded above (MissingReferenceException).
            data = BossCatalog.Load(bossId);
            data.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(gameplayPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            string acorn = ImportAcorn(m, bossId, log);

            string missing = ClipNames.Where(c => !OptionalClips.Contains(c) && clips.All(x => x.name != c)).Aggregate("", (a, c) => a + " " + c);
            log.AppendLine($"  + {name} (boss {bossId}): clips {clips.Count} ({string.Join(", ", clips.Select(c => c.name))}), weak point {(weakFound ? "found" : "MISSING")}" +
                           $"{(string.IsNullOrEmpty(missing) ? "" : ", missing clips:" + missing)}{acorn}");
            return true;
        }

        /// <summary>Optional static acorn (manifest extra_meshes): becomes the boss's falling hazard prefab.</summary>
        private static string ImportAcorn(JObject m, int bossId, StringBuilder log)
        {
            if (!(m["extra_meshes"] is JArray extras))
                return "";
            foreach (var e in extras.OfType<JObject>())
            {
                if (!((bool?)e["static"] ?? false))
                    continue;
                string fbx = (string)e["fbx"];
                var model = string.IsNullOrEmpty(fbx) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (model == null)
                    continue;
                SunDunesImporter.ConfigureModel(fbx, false);
                string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
                string n = (string)e["name"];
                var albedo = SunDunesImporter.ConfigureTexture($"{dir}/{(string)e["albedo"]}", true);
                var mat = SunDunesImporter.EnsureToonMaterial($"{dir}/M_{n}.mat", albedo, null, false, false);
                string path = $"{PrefabDir}/BossFalling_{n}.prefab";
                SceneFlowSetup.NewScene();
                var root = new GameObject("BossFalling_" + n);
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var sphere = root.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.35f; // a little more forgiving than the visual (0.45 m) would suggest
                root.AddComponent<BossHazard>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.transform.localPosition = Vector3.zero;
                ApplyMaterial(root, mat);
                var prefab = SceneFlowSetup.SavePrefab(root, path);
                var owner = BossCatalog.Load(bossId); // re-load after NewScene (see ImportBoss)
                owner.fallingPrefab = prefab;
                EditorUtility.SetDirty(owner);
                AssetDatabase.SaveAssetIfDirty(owner);
                return $", falling hazard {n}";
            }
            return "";
        }

        private static void ApplyMaterial(GameObject root, Material mat)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        private static string ClipBaseName(string take)
        {
            int bar = take.LastIndexOf('|');
            return bar >= 0 ? take.Substring(bar + 1) : take;
        }

        private static void ConfigureClips(ModelImporter mi)
        {
            // Always start from the FBX takes so takes added to the model later (e.g. Charge / Slam) are picked up.
            var source = mi.defaultClipAnimations;
            bool dirty = mi.clipAnimations == null || mi.clipAnimations.Length != source.Length;
            var clips = source.Select(c =>
            {
                string baseName = ClipBaseName(c.takeName);
                bool loop = LoopClips.Contains(baseName);
                if (c.name != baseName || c.loopTime != loop || !c.lockRootPositionXZ)
                    dirty = true;
                c.name = baseName;
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootPositionXZ = true;   // the game moves the boss
                c.lockRootHeightY = true;
                c.lockRootRotation = true;
                return c;
            }).ToArray();
            if (!dirty)
                return;
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }

        /// <summary>One state per clip, Idle is the default. BossController cross-fades between them by state name (no parameters).</summary>
        private static AnimatorController BuildController(string path, List<AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            int row = 0;
            foreach (var name in ClipNames)
            {
                var clip = clips.FirstOrDefault(c => c.name == name);
                if (clip == null)
                    continue;
                var state = sm.AddState(name, new Vector3(300f * (row / 5), 120f * (row % 5), 0f));
                state.motion = clip;
                if (name == "Idle")
                    sm.defaultState = state;
                row++;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
