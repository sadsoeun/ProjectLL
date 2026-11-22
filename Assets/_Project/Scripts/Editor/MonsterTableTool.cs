using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Validates Data/Tables/MonsterTable.csv against Docs/monster-table-schema.md and writes every Implemented row
    /// into its prefab (Monster table fields + BehaviorGraphAgent graph). Missing icons of Implemented rows are
    /// rendered from the prefab. Validation errors abort before any asset is touched.
    ///   Tools/MyGame/Monsters/Validate Monster Table
    ///   Tools/MyGame/Monsters/Apply Monster Table
    /// Code entry points: MonsterTableTool.Validate(), MonsterTableTool.Apply(), MonsterTableTool.Load().
    /// </summary>
    public static class MonsterTableTool
    {
        public const string TablePath = "Assets/_Project/Data/Tables/MonsterTable.csv";
        private const int IconSize = 256;
        public const string FxFolder = "Assets/_Project/Prefabs/Monsters/Common/FX";
        public const string SeedProjectilePath = FxFolder + "/FX_SeedProjectile.prefab";

        /// <summary>Table columns copied 1:1 into Monster fields of the same name.</summary>
        private static readonly string[] AppliedColumns =
        {
            "hp", "moveSpeed", "chaseSpeed", "patrolRange", "viewDistance", "viewHalfAngle", "loseDistance",
            "canJump", "jumpSpeed", "jumpInterval", "canAttack", "attackRange", "attackInterval", "attackDamage",
            "stompable", "contactKills", "rewardCoins",
        };

        [MenuItem("Tools/MyGame/Monsters/Validate Monster Table")]
        public static void ValidateMenu() => Debug.Log(Validate());

        [MenuItem("Tools/MyGame/Monsters/Apply Monster Table")]
        public static void ApplyMenu() => Debug.Log(Apply());

        /// <summary>Parses the table; throws with every error listed if it is invalid. Warnings go to <paramref name="warnings"/>.</summary>
        public static MonsterTable Load(List<string> warnings)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath)
                        ?? throw new FileNotFoundException("Monster table not found: " + TablePath);
            var errors = new List<string>();
            var table = MonsterTable.Parse(asset.text, errors);
            if (errors.Count == 0)
                CheckRules(table, errors, warnings, iconsMayBeGenerated: true);
            if (errors.Count > 0)
                throw new InvalidOperationException($"Monster table has {errors.Count} error(s):\n  " + string.Join("\n  ", errors));
            return table;
        }

        /// <summary>Validation only. Returns the report; throws on errors.</summary>
        public static string Validate()
        {
            var warnings = new List<string>();
            var table = Load(warnings);
            return Report($"[MonsterTableTool] Valid: {table.Records.Count} row(s).", warnings);
        }

        /// <summary>Validates, renders missing icons and writes every Implemented row into its prefab. Returns the report.</summary>
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the monster table.");

            var warnings = new List<string>();
            var table = Load(warnings);
            var sb = new StringBuilder($"[MonsterTableTool] Applied {TablePath}:\n");
            foreach (var r in table.Records)
            {
                if (!r.IsImplemented)
                {
                    sb.AppendLine($"  {r.id} {r.devName}: {r.status} (skipped)");
                    continue;
                }
                bool iconMade = false;
                if (!File.Exists(r.iconPath))
                {
                    RenderIcon(r.prefabPath, r.iconPath);
                    iconMade = true;
                }
                ApplyToPrefab(r);
                sb.AppendLine($"  {r.id} {r.devName}: {r.prefabPath} <- {Path.GetFileName(r.aiGraphPath)}{(iconMade ? ", icon rendered" : "")}");
            }
            AssetDatabase.SaveAssets();
            return Report(sb.ToString().TrimEnd(), warnings);
        }

        private static string Report(string head, List<string> warnings)
        {
            if (warnings.Count == 0)
                return head;
            return head + $"\n  {warnings.Count} warning(s):\n    " + string.Join("\n    ", warnings);
        }

        // ------------------------------------------------------------------ rules (Docs/monster-table-schema.md)

        private static void CheckRules(MonsterTable table, List<string> errors, List<string> warnings, bool iconsMayBeGenerated)
        {
            foreach (var r in table.Records)
            {
                string w = $"id {r.id}";
                void Err(string m) => errors.Add($"{w}: {m}");
                void Warn(string m) => warnings.Add($"{w}: {m}");

                if (!InRange(r.id, 1001, 1999) && !InRange(r.id, 2001, 2999) && !InRange(r.id, 9001, 9999))
                    Err("id must be in 1001-1999, 2001-2999 or 9001-9999.");
                if (r.status != MonsterTable.StatusImplemented && r.status != MonsterTable.StatusPlanned && r.status != MonsterTable.StatusDeprecated)
                    Err($"status '{r.status}' must be Implemented, Planned or Deprecated.");
                if (string.IsNullOrEmpty(r.devName))
                    Err("devName is empty.");
                if (r.nameKey != $"monster.{r.id}.name")
                    Err($"nameKey must be 'monster.{r.id}.name' (got '{r.nameKey}').");
                CheckPath(r.prefabPath, ".prefab", "prefabPath", Err);
                CheckPath(r.iconPath, ".png", "iconPath", Err);
                if (!string.IsNullOrEmpty(r.iconPath) && Path.GetFileName(r.iconPath) != $"icon_monster_{r.id}.png")
                    Err($"iconPath file name must be icon_monster_{r.id}.png.");
                if (!string.IsNullOrEmpty(r.aiGraphPath))
                    CheckPath(r.aiGraphPath, ".asset", "aiGraphPath", Err);

                if (!r.TryGetMovement(out var movement))
                    Err($"movement '{r.movement}' must be ground, fly or burrow.");
                else if (movement == MonsterMovement.Burrow && r.stompable && r.hp > 1)
                    Warn("a stompable burrower patrols after emerging; hp > 1 is allowed but unusual.");
                if (r.hp < 1) Err("hp must be >= 1.");
                if (r.moveSpeed < 0f || r.chaseSpeed < 0f || r.patrolRange < 0f || r.viewDistance < 0f || r.loseDistance < 0f
                    || r.jumpSpeed < 0f || r.jumpInterval < 0f || r.attackRange < 0f || r.attackInterval < 0f
                    || r.attackDamage < 0 || r.rewardCoins < 0)
                    Err("speeds, distances, times, damage and rewards must not be negative.");
                if (r.viewHalfAngle < 0f || r.viewHalfAngle > 180f) Err("viewHalfAngle must be within 0-180.");
                if (r.viewDistance > 0f && (r.viewHalfAngle <= 0f || r.loseDistance < r.viewDistance))
                    Err("a seeing monster needs viewHalfAngle > 0 and loseDistance >= viewDistance.");
                if (r.viewDistance <= 0f && (r.viewHalfAngle != 0f || r.loseDistance != 0f || r.chaseSpeed != 0f))
                    Err("a blind monster (viewDistance 0) must have viewHalfAngle, loseDistance and chaseSpeed 0.");
                if (r.canJump ? r.jumpSpeed <= 0f || r.jumpInterval <= 0f : r.jumpSpeed != 0f || r.jumpInterval != 0f)
                    Err(r.canJump ? "canJump needs jumpSpeed and jumpInterval > 0." : "canJump false needs jumpSpeed and jumpInterval 0.");
                if (r.canAttack ? r.attackRange <= 0f || r.attackInterval <= 0f || r.attackDamage <= 0
                                : r.attackRange != 0f || r.attackInterval != 0f || r.attackDamage != 0)
                    Err(r.canAttack ? "canAttack needs attackRange, attackInterval and attackDamage > 0."
                                    : "canAttack false needs attackRange, attackInterval and attackDamage 0.");

                if (!r.TryGetAttack(out var attack))
                    Err($"attack '{r.attack}' must be none, lunge, charge, leap, melee, slam or shoot.");
                else if (attack != MonsterAttack.None && !r.canAttack)
                    Err("an attack other than none needs canAttack true.");
                else if (attack == MonsterAttack.None && r.canAttack && movement != MonsterMovement.Fly)
                    Warn("canAttack is set but attack is none (only fly monsters attack without it: the dive).");
                else if (attack != MonsterAttack.None && movement != MonsterMovement.Ground)
                    Err("attacks are implemented for ground monsters only (fly monsters dive, burrowers emerge).");

                if (!LocalizationTable.TryGet("en", r.nameKey, out _))
                    Warn($"nameKey '{r.nameKey}' is not in LocalizationTable.");

                if (!r.IsImplemented)
                    continue;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(r.prefabPath) == null)
                    Err($"prefab not found: {r.prefabPath}");
                if (string.IsNullOrEmpty(r.aiGraphPath))
                    Err("an Implemented monster needs an aiGraphPath (Behavior graph).");
                else if (AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(r.aiGraphPath) == null)
                    Err($"Behavior graph not found: {r.aiGraphPath}");
                if (!iconsMayBeGenerated && !File.Exists(r.iconPath))
                    Err($"icon not found: {r.iconPath}");
                if (r.canJump)
                    Warn("canJump is set but Monster does not implement jumping yet (data only).");
            }
        }

        private static bool InRange(int v, int min, int max) => v >= min && v <= max;

        private static void CheckPath(string path, string extension, string column, System.Action<string> err)
        {
            if (string.IsNullOrEmpty(path))
                err($"{column} is empty.");
            else if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(extension, StringComparison.Ordinal))
                err($"{column} must be an 'Assets/...{extension}' path (got '{path}').");
        }

        // ------------------------------------------------------------------ apply

        private static void ApplyToPrefab(MonsterRecord r)
        {
            var root = PrefabUtility.LoadPrefabContents(r.prefabPath);
            try
            {
                var monster = root.GetComponent<Monster>()
                              ?? throw new InvalidOperationException($"{r.prefabPath} has no Monster component (run Tools/MyGame/Monsters/Build Missing Monster Assets).");
                var agent = root.GetComponent<BehaviorGraphAgent>()
                            ?? throw new InvalidOperationException($"{r.prefabPath} has no BehaviorGraphAgent.");

                EditorWire.Set(monster, "monsterId", r.id);
                r.TryGetMovement(out var movement);
                EditorWire.Set(monster, "movement", (int)movement);
                r.TryGetAttack(out var attack);
                EditorWire.Set(monster, "attack", (int)attack);
                WireAttackFx(monster, attack);
                foreach (string column in AppliedColumns)
                    EditorWire.Set(monster, column, typeof(MonsterRecord).GetField(column).GetValue(r));

                var graph = MonsterSetup.LoadRuntimeGraph(r.aiGraphPath)
                            ?? throw new InvalidOperationException($"Behavior graph {r.aiGraphPath} has no runtime graph (open it once in the Behavior editor).");
                EditorWire.Set(agent, "m_Graph", graph);

                PrefabUtility.SaveAsPrefabAsset(root, r.prefabPath, out bool ok);
                if (!ok)
                    throw new InvalidOperationException("Failed to save prefab " + r.prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Telegraph / strike effects (every monster gets them; flyers use the alert for the dive) and the seed shot.</summary>
        private static void WireAttackFx(Monster monster, MonsterAttack attack)
        {
            EditorWire.Set(monster, "alertFxPrefab", LoadFx("FX_MonsterAlert"));
            EditorWire.Set(monster, "shockLinesFxPrefab", LoadFx("FX_MonsterShockLines"));
            EditorWire.Set(monster, "swooshFxPrefab", LoadFx("FX_MonsterSwoosh"));
            EditorWire.Set(monster, "shockwaveFxPrefab", LoadFx("FX_MonsterShockwave"));
            EditorWire.Set(monster, "dustFxPrefab", LoadFx("FX_MonsterDust"));
            if (attack == MonsterAttack.Shoot)
            {
                var seed = AssetDatabase.LoadAssetAtPath<MonsterProjectile>(SeedProjectilePath)
                           ?? throw new InvalidOperationException("projectile prefab not found: " + SeedProjectilePath);
                EditorWire.Set(monster, "projectilePrefab", seed);
            }
        }

        private static CartoonFx LoadFx(string name) =>
            AssetDatabase.LoadAssetAtPath<CartoonFx>($"{FxFolder}/{name}.prefab")
            ?? throw new InvalidOperationException($"FX prefab not found: {FxFolder}/{name}.prefab (run Tools/MyGame/Monsters/Build Monster FX).");

        // ------------------------------------------------------------------ icons

        /// <summary>Renders the prefab (three-quarter view, transparent background) to a 256x256 sprite PNG.</summary>
        private static void RenderIcon(string prefabPath, string iconPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                         ?? throw new InvalidOperationException("prefab not found: " + prefabPath);
            SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(iconPath).Replace('\\', '/'));

            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                    ps.gameObject.SetActive(false);

                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    throw new InvalidOperationException(prefabPath + " has no renderers to draw an icon from.");
                var bounds = renderers[0].bounds;
                foreach (var rend in renderers)
                    bounds.Encapsulate(rend.bounds);

                var lightGo = new GameObject("IconLight");
                SceneManagerMove(lightGo, scene);
                lightGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;

                var camGo = new GameObject("IconCamera");
                SceneManagerMove(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = bounds.extents.magnitude * 0.85f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 50f;
                Vector3 viewDir = new Vector3(0.75f, 0.35f, -1f).normalized; // from the face side, slightly above
                camGo.transform.position = bounds.center + viewDir * 10f;
                camGo.transform.LookAt(bounds.center);
                cam.targetTexture = rt;

                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(iconPath, tex.EncodeToPNG());
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene)
            => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
