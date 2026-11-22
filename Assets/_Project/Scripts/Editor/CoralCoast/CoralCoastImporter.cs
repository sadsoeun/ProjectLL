using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Imports the CORAL COAST (world 4) art described in Docs/art/coralcoast-contract.md. Same pipeline as the Frost Peaks
    /// importer (Sun Dunes texture / material / model / LOD / animator helpers): reads Docs/art/coralcoast/manifest/*.json,
    /// builds the prefabs under Prefabs/Environment/CoralCoast/{Ground,Props,Gimmicks,Architecture,Background} and
    /// Prefabs/Monsters/Stage04, then fills the map-tool data: PropTable.csv rows (CC_*), Data/Themes/CoralCoast.asset and
    /// MonsterTable.csv rows (1301–1304). The sea surface (B08) gets the animated MyGame/Ambient/Water material.
    /// Idempotent (prefab GUIDs are kept on re-run). Menu: Tools/MyGame/Art/Import Coral Coast.
    /// </summary>
    public static class CoralCoastImporter
    {
        private const string EnvArtRoot = "Assets/_Project/Art/Environment/CoralCoast";
        private const string EnvPrefabRoot = "Assets/_Project/Prefabs/Environment/CoralCoast";
        private const string MonsterPrefabRoot = "Assets/_Project/Prefabs/Monsters/Stage04";
        private const string Theme = "CoralCoast";
        private const string WaterShader = "MyGame/Ambient/Water";

        private static string ManifestDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/art/coralcoast/manifest"));

        private static readonly string[] Categories = { "Ground", "Props", "Gimmicks", "Architecture", "Background" };

        /// <summary>Background tint (multiplies the albedo): far layers drift toward the sea haze so the warm play strip reads first.</summary>
        private static Color BackgroundTint(string name) =>
            name.Contains("B07") ? new Color(0.86f, 0.9f, 0.9f)
            : name.Contains("B06") ? new Color(0.9f, 0.93f, 0.94f)
            : name.Contains("B05") || name.Contains("B04") ? new Color(0.84f, 0.92f, 0.95f)
            : new Color(0.88f, 0.94f, 0.96f);

        [MenuItem("Tools/MyGame/Art/Import Coral Coast")]
        public static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing Coral Coast.");
            var log = new StringBuilder("[CoralCoastImporter]\n");
            AssetDatabase.Refresh();
            var manifests = LoadManifests(log);
            log.AppendLine($"  manifests: {manifests.Count}");

            var envPrefabs = new Dictionary<string, string>();
            var monsters = new Dictionary<string, (string visual, string gameplay)>();
            foreach (var m in manifests)
            {
                string cat = (string)m["category"];
                try
                {
                    if (cat == "Monsters")
                    {
                        var r = ImportMonster(m, log);
                        if (r.visual != null)
                            monsters[(string)m["name"]] = r;
                    }
                    else if (Categories.Contains(cat))
                    {
                        string p = ImportEnvironment(m, cat, log);
                        if (p != null)
                            envPrefabs[(string)m["name"]] = p;
                    }
                    else
                    {
                        log.AppendLine($"  ! {(string)m["name"]}: unknown category '{cat}'");
                    }
                }
                catch (Exception e)
                {
                    log.AppendLine($"  ! {(string)m["name"]}: {e.GetType().Name}: {e.Message}");
                    Debug.LogException(e);
                }
            }
            AssetDatabase.SaveAssets();

            log.AppendLine(UpdatePropTable(envPrefabs));
            log.AppendLine(UpdateThemeKit());
            log.AppendLine(UpdateMonsterTable(monsters));
            if (monsters.Count > 0)
            {
                try { log.AppendLine("  " + MonsterTableTool.Apply().Replace("\n", "\n  ")); }
                catch (Exception e) { Debug.LogException(e); log.AppendLine("  ! Apply Monster Table failed: " + e.Message); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString().TrimEnd();
        }

        // ------------------------------------------------------------------ manifests

        private static List<JObject> LoadManifests(StringBuilder log)
        {
            var list = new List<JObject>();
            if (!Directory.Exists(ManifestDir))
            {
                log.AppendLine("  ! manifest folder missing: " + ManifestDir);
                return list;
            }
            foreach (var file in Directory.GetFiles(ManifestDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                try
                {
                    var token = JToken.Parse(File.ReadAllText(file));
                    if (token is JArray arr) list.AddRange(arr.OfType<JObject>());
                    else if (token is JObject obj) list.Add(obj);
                }
                catch (Exception e)
                {
                    log.AppendLine($"  ! {Path.GetFileName(file)}: {e.Message}");
                }
            }
            return list.Where(m => !string.IsNullOrEmpty((string)m["name"]) && !string.IsNullOrEmpty((string)m["fbx"])).ToList();
        }

        /// <summary>CC_T01_SandCliffCore_V2 -> T01.</summary>
        private static string ConceptId(string assetName)
        {
            var parts = assetName.Split('_');
            return parts.Length > 1 ? parts[1] : assetName;
        }

        /// <summary>Albedo-derived toon shadow (warm sand / limestone: slight hue shift toward violet like the key art).</summary>
        private static void ApplyHsvShadow(Material mat)
        {
            if (!mat.HasProperty("_ShadowFromAlbedo"))
                return;
            mat.SetFloat("_ShadowFromAlbedo", 1f);
            mat.SetFloat("_ShadowHueShift", -0.03f);
            mat.SetFloat("_ShadowSaturation", 1.3f);
            mat.SetFloat("_ShadowValue", 0.7f);
            mat.SetColor("_ShadowColor", new Color(0.96f, 0.94f, 1f));
            EditorUtility.SetDirty(mat);
        }

        private static Material EnsureWaterMaterial(string fileName, bool foam)
        {
            string path = $"{EnvArtRoot}/Background/Materials/{fileName}";
            var shader = Shader.Find(WaterShader);
            if (shader == null)
                return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Mode", foam ? 2f : 0f);
            foreach (var k in new[] { "_MODE_SEA", "_MODE_FLOW", "_MODE_FOAM" })
                mat.DisableKeyword(k);
            mat.EnableKeyword(foam ? "_MODE_FOAM" : "_MODE_SEA");
            // Key art sea: clear teal, not realistic; restrained waves so the play strip stays calm to read.
            mat.SetColor("_BaseColor", new Color(0.12f, 0.56f, 0.72f, 0.86f));   // deep #1E8FB8
            mat.SetColor("_ShallowColor", new Color(0.22f, 0.78f, 0.78f, 0.62f)); // teal #39C6C8
            mat.SetColor("_FoamColor", new Color(0.92f, 0.98f, 0.98f, 1f));
            mat.SetFloat("_WaveHeight", foam ? 0.02f : 0.05f);
            mat.SetFloat("_WaveSpeed", 0.8f);
            mat.SetFloat("_DepthFade", 2.2f);
            mat.SetFloat("_FoamWidth", 0.5f);
            mat.SetFloat("_RippleScale", 0.5f);
            mat.SetFloat("_RippleSpeed", 0.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ environment

        private static string ImportEnvironment(JObject m, string cat, StringBuilder log)
        {
            string name = (string)m["name"];
            string fbx = (string)m["fbx"];
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
            {
                log.AppendLine($"  - {name}: FBX not found ({fbx}), skipped");
                return null;
            }
            SunDunesImporter.ConfigureModel(fbx, false);
            string texDir = $"{EnvArtRoot}/{cat}/Textures/";
            int texMax = cat == "Background" ? 4096 : 2048;
            var albedo = SunDunesImporter.ConfigureTexture(texDir + (string)m["albedo"], true, texMax);
            var emission = m["emission"]?.Type == JTokenType.String ? SunDunesImporter.ConfigureTexture(texDir + (string)m["emission"], true, texMax) : null;
            if (albedo == null)
                log.AppendLine($"  ! {name}: albedo missing ({texDir}{(string)m["albedo"]})");
            bool wind = (bool?)m["wind"] ?? false;
            bool terrain = cat == "Ground";
            var mat = SunDunesImporter.EnsureToonMaterial($"{EnvArtRoot}/{cat}/Materials/M_{name}.mat", albedo, emission, wind, terrain);
            if (wind)
                mat.SetFloat("_WindStrength", name.Contains("Palm") ? 0.05f : 0.06f);
            ApplyHsvShadow(mat);
            if (cat == "Background")
                mat.SetColor("_BaseColor", BackgroundTint(name));

            bool sea = ConceptId(name) == "B08";
            var water = sea ? EnsureWaterMaterial("M_CC_B08_Sea.mat", false) : null;
            var foamMat = sea ? EnsureWaterMaterial("M_CC_B08_Foam.mat", true) : null;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var (rc, cc, lv) = SunDunesImporter.DressModel(root, mat, null, cat == "Background");
                if (sea)
                {
                    // The whole tile is water except the foam ribbon; neither casts or receives shadows.
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        bool foam = r.name.IndexOf("Foam", StringComparison.OrdinalIgnoreCase) >= 0;
                        var m2 = foam ? foamMat : water;
                        if (m2 != null)
                            r.sharedMaterials = Enumerable.Repeat(m2, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                }
                if (cat == "Ground" && ConceptId(name) == "T04")
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                string path = $"{EnvPrefabRoot}/{cat}/{name}.prefab";
                SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"  + {cat}/{name}: renderers {rc}, colliders {cc}, LODs {lv}{(wind ? ", wind" : "")}{(emission != null ? ", emission" : "")}{(sea ? ", water" : "")}");
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ monsters

        private sealed class MonsterInfo
        {
            public int Id, Hp;
            public string Korean, Graph, Note, Movement;
            public float Move, Chase, Range, View, HalfAngle, Lose;
            public bool Stompable;
            public Color Burst;
        }

        // Existing behaviours only (Monster.Movement.cs ground / fly; Monster.Attack.cs via SunDunesImporter.Attacks).
        private static readonly Dictionary<string, MonsterInfo> Monsters = new Dictionary<string, MonsterInfo>
        {
            ["M01"] = M(1301, "해안 게", true, 1, 1.2f, 0f, 4f, new Color(0.95f, 0.45f, 0.30f), "옆걸음 배회. attack=melee: 놀람(!!) 예고 후 집게 찍기."),
            ["M02"] = M(1302, "소라게", true, 1, 0.6f, 0f, 3f, new Color(0.96f, 0.75f, 0.70f), "느린 배회(공격 없음). 접촉 시 피격, 밟기 가능. Hide 클립은 연출용(전용 AI 없음)."),
            ["M04"] = M(1303, "조수 해파리", true, 1, 0.9f, 0f, 3f, new Color(0.55f, 0.92f, 0.85f), "movement=fly: 공중 배회(Fly 클립). 기존 비행 AI의 급강하 규칙을 그대로 사용.", "fly"),
            ["M06"] = M(1304, "급강하 갈매기", true, 1, 1.8f, 0f, 7f, new Color(0.95f, 0.97f, 1f), "movement=fly: 넓게 공중 배회하다 기존 비행 AI 규칙으로 예고 후 급강하(Fly 클립).", "fly"),
        };

        private static MonsterInfo M(int id, string korean, bool stompable, int hp, float move, float chase, float range, Color burst, string note, string movement = "ground")
        {
            bool chaser = chase > 0f;
            return new MonsterInfo
            {
                Id = id, Korean = korean, Stompable = stompable, Hp = hp, Move = move, Chase = chase, Range = range, Burst = burst, Note = note, Movement = movement,
                Graph = chaser ? MonsterSetup.ChaserGraphPath : MonsterSetup.PatrolGraphPath,
                View = chaser ? 7f : 0f, HalfAngle = chaser ? 50f : 0f, Lose = chaser ? 10f : 0f,
            };
        }

        private static (string visual, string gameplay) ImportMonster(JObject m, StringBuilder log)
        {
            string name = (string)m["name"];
            string fbx = (string)m["fbx"];
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
            {
                log.AppendLine($"  - {name}: FBX not found ({fbx}), skipped");
                return (null, null);
            }
            var mi = SunDunesImporter.ConfigureModel(fbx, true);
            SunDunesImporter.ConfigureClips(mi);

            string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
            var albedo = SunDunesImporter.ConfigureTexture($"{dir}/{(string)m["albedo"]}", true);
            var emission = m["emission"]?.Type == JTokenType.String ? SunDunesImporter.ConfigureTexture($"{dir}/{(string)m["emission"]}", true) : null;
            var mat = SunDunesImporter.EnsureToonMaterial($"{dir}/M_{name}.mat", albedo, emission, false, false);

            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
            var controller = SunDunesImporter.BuildController($"{dir}/{name}.controller", clips);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            string visualPath = $"{MonsterPrefabRoot}/{name}_Visual.prefab";
            SceneFlowSetup.EnsureFolder(MonsterPrefabRoot);
            var root = new GameObject(name + "_Visual");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                // Visual prefabs have their pivot at the feet: body-centre models (jelly, gull) are lifted by their depth below the pivot.
                float bottom = (float?)m["bottom_m"] ?? 0f;
                if (bottom < 0f)
                    instance.transform.localPosition = new Vector3(0f, -bottom, 0f);
                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                SunDunesImporter.DressModel(root, mat, null, false);
                PrefabUtility.SaveAsPrefabAsset(root, visualPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            string gameplayPath = null;
            if (Monsters.TryGetValue(ConceptId(name), out var info))
            {
                float height = (float?)m["height_m"] ?? 1f;
                var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
                var monsterRoot = MonsterSetup.NewMonsterRoot(name, out var visual);
                var comp = MonsterSetup.FinishMonster(monsterRoot, visual, info.Burst, info.Graph);
                MonsterSetup.AttachAnimatedModel(monsterRoot, visual, comp, visualPrefab, Mathf.Clamp(height, 0.4f, 2.4f));
                gameplayPath = $"{MonsterPrefabRoot}/{name}.prefab";
                SceneFlowSetup.SavePrefab(monsterRoot, gameplayPath);
            }
            log.AppendLine($"  + Monsters/{name}: clips {clips.Count} ({string.Join(", ", clips.Select(c => c.name))}){(gameplayPath != null ? ", gameplay prefab" : "")}");
            return (visualPath, gameplayPath);
        }

        // ------------------------------------------------------------------ prop table

        private sealed class PropInfo { public string Korean, Category, Note; public float Z; }

        // Category / default depth follow Docs/prop-table-schema.md (decorations behind the 4 m play strip: z > 2).
        private static readonly Dictionary<string, PropInfo> Props = new Dictionary<string, PropInfo>
        {
            ["T01"] = P("모래 절벽 코어", "Terrain", 0f, "2x2x4 모래 캡 + 석회암. 피벗 좌상단/깊이 중앙. 테마 킷 groundCore(+변형)."),
            ["T02"] = P("모래 절벽 왼쪽 끝", "Terrain", 0f, "2x2x4 왼쪽 끝 모듈. 테마 킷 groundEndLeft."),
            ["T03"] = P("모래 절벽 오른쪽 끝", "Terrain", 0f, "2x2x4 오른쪽 끝 모듈. 테마 킷 groundEndRight."),
            ["T04"] = P("적층 석회암", "Terrain", 0f, "2x2x4 모래 없는 석회암, 상하좌우 반복. 테마 킷 rockCore(+변형)."),
            ["T05"] = P("산호암 포장 상단", "Terrain", 0f, "2x2x4 T01 접합, 포장면 높이 0. 장식/변형 지형."),
            ["T06"] = P("소도 발판", "Terrain", 0f, "3x1x4 모래 상판 + 석회암 하부. 테마 킷 floatingPlatform."),
            ["T07"] = P("부두 덱", "Terrain", 0f, "3x0.4x4 판자 덱(난간 없음). 테마 킷 timberPlatform."),
            ["T08"] = P("부두 지지대", "Terrain", 0f, "3x4x4 기둥4 + 가로보, 피벗 바닥 중앙. 부두 덱 아래 장식(충돌 없음)."),
            ["G01"] = P("진주 체크포인트", "Gameplay", 0f, "높이 3. 테마 킷 checkpointVisual."),
            ["G02"] = P("조개 골인 아치", "Gameplay", 0f, "2.7x4x1.2, 통로 1.5x3. 테마 킷 goalVisual."),
            ["G03"] = P("조수 압력판", "Gameplay", 0f, "Base/Plate/Emblem. 테마 킷 switchVisual."),
            ["G04"] = P("승강 수문", "Gameplay", 0f, "이동 문 판 1x3x1.5. 테마 킷 gateVisual. 고정 가이드는 TideGateGuide 장식."),
            ["G05"] = P("뗏목 발판", "Gameplay", 0f, "3x0.4x4 좌상단 피벗. 테마 킷 moverVisual/fallerVisual."),
            ["G06"] = P("수납식 산호 가시", "Gameplay", 0f, "Base + Spike_01..03(+0.58 상승). 테마 킷 spikeTrapVisual."),
            ["P01"] = P("야자수", "Vegetation", 6f, "높이 5. 잎 바람."),
            ["P02"] = P("해안 식물", "Vegetation", 2.5f, "큰 잎(바람) + 꽃."),
            ["P03"] = P("가지 산호", "Vegetation", 3f, "장식 산호(가시 없음)."),
            ["P04"] = P("돔 산호", "Vegetation", 3f, "뇌산호 돔."),
            ["P05"] = P("조개 세트", "Prop", 2.2f, "가리비/소라/대합."),
            ["P06"] = P("화물 상자", "Prop", 2.5f, "장식 전용(파괴 불가)."),
            ["P07"] = P("나무통", "Prop", 2.5f, "장식 전용."),
            ["P08"] = P("닻", "Prop", 3f, "닻 + 로프."),
            ["P09"] = P("항로 등불", "Prop", 2.5f, "높이 2.4, 등불 발광."),
            ["P10"] = P("계선주와 로프", "Prop", 2.2f, "부두 장식."),
            ["P11"] = P("해안 깃발", "Prop", 2.5f, "천 바람."),
            ["P12"] = P("해안 바위", "Prop", 4f, "석회암 3 + 모래."),
            ["A01"] = P("조개 궁전", "Landmark", 22f, "18x22x12 소라 지붕 궁전. 근경 랜드마크."),
            ["A02"] = P("부두 정자", "Landmark", 5f, "4x4x4 장식(충돌 없음)."),
            ["B01"] = P("야자수 섬", "Background", 45f, "원경 섬, LOD 3단."),
            ["B02"] = P("해식 기둥 군락", "Background", 35f, "중경 바위 기둥, LOD 3단."),
            ["B03"] = P("해식 아치", "Background", 55f, "관통 아치, LOD 3단."),
            ["B04"] = P("산호 능선", "Background", 80f, "원경 능선, LOD 3단."),
            ["B05"] = P("원경 조개 궁전", "Background", 90f, "A01 단순화, 창 발광."),
            ["B06"] = P("난파선", "Background", 22f, "중경 난파선(TiltRoot)."),
            ["B07"] = P("얕은 바다 모래 바닥", "Background", 2.4f, "400x150 연속 해저 모래. x = 스테이지 길이/2."),
            ["B08"] = P("바다 수면", "Background", 2.4f, "32x32 수면 타일 + 포말 띠. 애니메이션 물 재질."),
        };

        private static PropInfo P(string korean, string category, float z, string note) =>
            new PropInfo { Korean = korean, Category = category, Z = z, Note = note };

        private static string UpdatePropTable(Dictionary<string, string> envPrefabs)
        {
            var rows = new List<(string key, string line)>();
            foreach (var kv in envPrefabs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string id = ConceptId(kv.Key);
                if (!Props.TryGetValue(id, out var info))
                    continue;
                string variant = "";
                foreach (var suffix in new[] { "_V2", "_V3", "_L", "_R" })
                    if (kv.Key.EndsWith(suffix, StringComparison.Ordinal))
                        variant = " (" + suffix.Substring(1) + ")";
                string korean = info.Korean + variant;
                string category = info.Category;
                float z = info.Z;
                if (kv.Key.EndsWith("Guide", StringComparison.Ordinal))
                {
                    korean = "승강 수문 고정 가이드";
                    category = "Prop";
                    z = 0f;
                }
                string line = string.Join(",", kv.Key, SunDunesImporter.Csv(korean), category, kv.Value,
                    SunDunesImporter.F(z), "false", "Implemented", SunDunesImporter.Csv("CORAL COAST. " + info.Note));
                rows.Add((kv.Key, line));
            }
            int replaced = SunDunesImporter.ReplaceCsvRows(PropCatalog.TablePath, rows);
            return $"  PropTable: {rows.Count} CC rows ({replaced} updated, {rows.Count - replaced} added)";
        }

        // ------------------------------------------------------------------ theme kit

        private static string UpdateThemeKit()
        {
            string path = StageThemeKit.PathFor(Theme);
            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(path);
            bool created = kit == null;
            if (created)
            {
                SceneFlowSetup.EnsureFolder(StageThemeKit.Dir);
                kit = ScriptableObject.CreateInstance<StageThemeKit>();
                AssetDatabase.CreateAsset(kit, path);
            }
            GameObject Load(string cat, string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvPrefabRoot}/{cat}/{name}.prefab");
            GameObject Shared(string p) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/GreenMeadow/" + p + ".prefab");
            GameObject[] Some(params GameObject[] g) => g.Where(x => x != null).ToArray();

            kit.groundCore = Load("Ground", "CC_T01_SandCliffCore");
            // T05 (paving) is not a random variant: scattered paving cells on the beach would read as noise. It is a prop.
            kit.groundCoreVariants = Some(Load("Ground", "CC_T01_SandCliffCore_V2"), Load("Ground", "CC_T01_SandCliffCore_V3"));
            kit.iceSurface = null; // no slippery surfaces in world 4
            // Bright tropical day: clear sky, soft sea-haze fog that pushes the far islands back, warm sun on the sand.
            kit.useAtmosphere = true;
            kit.skyColor = new Color(0.42f, 0.72f, 0.95f);
            kit.fogColor = new Color(0.66f, 0.86f, 0.92f);
            kit.fogStart = 25f;
            kit.fogEnd = 150f;
            kit.sunColor = new Color(1f, 0.96f, 0.88f);
            kit.sunIntensity = 1.15f;
            kit.groundEndLeft = Load("Ground", "CC_T02_SandCliffEnd_L");
            kit.groundEndRight = Load("Ground", "CC_T03_SandCliffEnd_R");
            kit.rockCore = Load("Ground", "CC_T04_LimestoneCore");
            kit.rockCoreVariants = Some(Load("Ground", "CC_T04_LimestoneCore_V2"), Load("Ground", "CC_T04_LimestoneCore_V3"));
            kit.floatingPlatform = Load("Ground", "CC_T06_IslandPlatform");
            kit.timberPlatform = Load("Ground", "CC_T07_JettyDeck");
            kit.checkpointVisual = Load("Gimmicks", "CC_G01_PearlCheckpoint");
            kit.goalVisual = Load("Gimmicks", "CC_G02_ShellGoalArch");
            kit.switchVisual = Load("Gimmicks", "CC_G03_TidePlate");
            kit.gateVisual = Load("Gimmicks", "CC_G04_TideGate");
            kit.moverVisual = Load("Gimmicks", "CC_G05_RaftPlatform");
            kit.fallerVisual = Load("Gimmicks", "CC_G05_RaftPlatform");
            kit.spikeTrapVisual = Load("Gimmicks", "CC_G06_CoralSpikeTrap");
            kit.spikeVisual = null;
            // Shared pickups stay recognisable across worlds (referenced, not copied).
            kit.coinVisual = Shared("G03a_Coin");
            kit.gemVisual = Shared("G03b_MagentaGem");
            EditorUtility.SetDirty(kit);

            var slots = new (string n, GameObject g)[]
            {
                ("groundCore", kit.groundCore), ("groundEndLeft", kit.groundEndLeft), ("groundEndRight", kit.groundEndRight), ("rockCore", kit.rockCore),
                ("floatingPlatform", kit.floatingPlatform), ("timberPlatform", kit.timberPlatform), ("checkpointVisual", kit.checkpointVisual),
                ("goalVisual", kit.goalVisual), ("switchVisual", kit.switchVisual), ("gateVisual", kit.gateVisual), ("moverVisual", kit.moverVisual),
                ("spikeTrapVisual", kit.spikeTrapVisual),
            };
            var empty = slots.Where(s => s.g == null).Select(s => s.n).ToList();
            return $"  Theme kit {path}: {(created ? "created" : "updated")}, slots {slots.Length - empty.Count}/{slots.Length}"
                   + $", variants ground {kit.groundCoreVariants.Length} / rock {kit.rockCoreVariants.Length}"
                   + (empty.Count > 0 ? ", empty: " + string.Join(", ", empty) : "");
        }

        // ------------------------------------------------------------------ monster table

        private static string UpdateMonsterTable(Dictionary<string, (string visual, string gameplay)> monsters)
        {
            var rows = new List<(string key, string line)>();
            foreach (var kv in monsters.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (kv.Value.gameplay == null || !Monsters.TryGetValue(ConceptId(kv.Key), out var i))
                    continue;
                string note = $"CORAL COAST {ConceptId(kv.Key)}. {i.Note} 비주얼: {kv.Value.visual}";
                string id = i.Id.ToString(CultureInfo.InvariantCulture);
                string line = string.Join(",",
                    id, "Implemented", SunDunesImporter.Csv(i.Korean), $"monster.{id}.name", kv.Value.gameplay,
                    $"Assets/_Project/Art/Icons/Monsters/icon_monster_{id}.png", i.Graph,
                    i.Hp.ToString(CultureInfo.InvariantCulture), SunDunesImporter.F(i.Move), SunDunesImporter.F(i.Chase), SunDunesImporter.F(i.Range),
                    SunDunesImporter.F(i.View), SunDunesImporter.F(i.HalfAngle), SunDunesImporter.F(i.Lose),
                    "false", "0", "0", SunDunesImporter.AttackColumns(i.Id),
                    i.Stompable ? "true" : "false", "true", "1", i.Movement, SunDunesImporter.AttackKind(i.Id), SunDunesImporter.Csv(note));
                rows.Add((id, line));
            }
            int replaced = SunDunesImporter.ReplaceCsvRows(MonsterTableTool.TablePath, rows);
            return $"  MonsterTable: {rows.Count} CC rows ({replaced} updated, {rows.Count - replaced} added)";
        }
    }
}
