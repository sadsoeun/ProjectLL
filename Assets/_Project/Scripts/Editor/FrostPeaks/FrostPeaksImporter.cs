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
    /// Imports the FROST PEAKS (world 3) art described in Docs/art/frostpeaks-contract.md. Same pipeline as the Sun Dunes
    /// importer (whose texture / material / model / LOD / animator helpers it reuses): reads Docs/art/frostpeaks/manifest/*.json,
    /// builds the prefabs under Prefabs/Environment/FrostPeaks/{Ground,Props,Gimmicks,Architecture,Background} and
    /// Prefabs/Monsters/Stage03, then fills the map-tool data: PropTable.csv rows (FP_*), Data/Themes/FrostPeaks.asset and
    /// MonsterTable.csv rows (1201–1207, 2201). Idempotent (prefab GUIDs are kept on re-run).
    /// Menu: Tools/MyGame/Art/Import Frost Peaks.
    /// </summary>
    public static class FrostPeaksImporter
    {
        private const string EnvArtRoot = "Assets/_Project/Art/Environment/FrostPeaks";
        private const string EnvPrefabRoot = "Assets/_Project/Prefabs/Environment/FrostPeaks";
        private const string MonsterPrefabRoot = "Assets/_Project/Prefabs/Monsters/Stage03";
        private const string Theme = "FrostPeaks";

        private static string ManifestDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/art/frostpeaks/manifest"));

        private static readonly string[] Categories = { "Ground", "Props", "Gimmicks", "Architecture", "Background" };

        /// <summary>Background tint (multiplies the albedo). The snow floor right behind the play strip is the darkest so the
        /// strip's white top edge stays readable; distant layers are lighter and get the stage fog on top.</summary>
        internal static Color BackgroundTint(string name) =>
            name.Contains("B07") ? new Color(0.64f, 0.72f, 0.87f)
            : name.Contains("B06") || name.Contains("B08") ? new Color(0.66f, 0.75f, 0.9f)
            : name.Contains("B03") ? new Color(0.7f, 0.8f, 0.94f)
            : new Color(0.74f, 0.8f, 0.93f);

        [MenuItem("Tools/MyGame/Art/Import Frost Peaks")]
        public static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing Frost Peaks.");
            var log = new StringBuilder("[FrostPeaksImporter]\n");
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
            try { log.AppendLine("  " + MonsterTableTool.Apply().Replace("\n", "\n  ")); }
            catch (Exception e) { Debug.LogException(e); log.AppendLine("  ! Apply Monster Table failed: " + e.Message); }
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

        /// <summary>FP_T01_SnowCliffCore_V2 -> T01.</summary>
        private static string ConceptId(string assetName)
        {
            var parts = assetName.Split('_');
            return parts.Length > 1 ? parts[1] : assetName;
        }

        /// <summary>Albedo-derived toon shadow, same rule as the other background / terrain materials.</summary>
        private static void ApplyHsvShadow(Material mat)
        {
            if (!mat.HasProperty("_ShadowFromAlbedo"))
                return;
            mat.SetFloat("_ShadowFromAlbedo", 1f);
            mat.SetFloat("_ShadowHueShift", -0.035f);
            mat.SetFloat("_ShadowSaturation", 1.4f);
            mat.SetFloat("_ShadowValue", 0.66f);
            mat.SetColor("_ShadowColor", new Color(0.93f, 0.94f, 1f));
            EditorUtility.SetDirty(mat);
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
            int texMax = cat == "Background" ? 4096 : 2048; // HQ far layers keep their authored 4K detail
            var albedo = SunDunesImporter.ConfigureTexture(texDir + (string)m["albedo"], true, texMax);
            var emission = m["emission"]?.Type == JTokenType.String ? SunDunesImporter.ConfigureTexture(texDir + (string)m["emission"], true, texMax) : null;
            if (albedo == null)
                log.AppendLine($"  ! {name}: albedo missing ({texDir}{(string)m["albedo"]})");
            bool wind = (bool?)m["wind"] ?? false;
            bool terrain = cat == "Ground";
            var mat = SunDunesImporter.EnsureToonMaterial($"{EnvArtRoot}/{cat}/Materials/M_{name}.mat", albedo, emission, wind, terrain);
            if (wind)
                mat.SetFloat("_WindStrength", name.Contains("Fir") ? 0.03f : 0.06f); // firs: gentle tip sway
            ApplyHsvShadow(mat);
            if (cat == "Background")
                mat.SetColor("_BaseColor", BackgroundTint(name)); // cool blue-grey: the white play strip reads in front of it

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var (rc, cc, lv) = SunDunesImporter.DressModel(root, mat, null, cat == "Background");
                // Repeated terrain below the cap (rock cores) and far solids: no shadow casting cost.
                if (cat == "Ground" && ConceptId(name) == "T04")
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                string path = $"{EnvPrefabRoot}/{cat}/{name}.prefab";
                SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"  + {cat}/{name}: renderers {rc}, colliders {cc}, LODs {lv}{(wind ? ", wind" : "")}{(emission != null ? ", emission" : "")}");
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

        // Movement: Monster.Movement.cs (ground / fly). Attacks: Monster.Attack.cs (SunDunesImporter.Attacks).
        private static readonly Dictionary<string, MonsterInfo> Monsters = new Dictionary<string, MonsterInfo>
        {
            ["M01"] = M(1201, "눈 슬라임", true, 1, 1.0f, 0f, 4f, new Color(0.75f, 0.88f, 1f), "배회. attack=leap: 놀람(!!) 예고 후 플레이어 위치로 포물선 도약(Jump 클립)."),
            ["M02"] = M(1202, "빙정 풍뎅이", false, 1, 1.4f, 0f, 6f, new Color(0.45f, 0.85f, 0.95f), "등의 얼음 결정 때문에 밟기 불가. attack=charge: 놀람(!!)·흙먼지 예고 후 돌진."),
            ["M03"] = M(1203, "서리 박쥐", true, 1, 1.6f, 0f, 6f, new Color(0.45f, 0.55f, 0.80f), "movement=fly: 배치 높이 +2.5 m에서 공중 배회하다 아래 5 m 안에 플레이어가 있으면 예고(!! 아이콘) 후 급강하. Fly 클립 포함.", "fly"),
            ["M04"] = M(1204, "눈 토끼", true, 1, 1.8f, 0f, 6f, new Color(0.95f, 0.97f, 1f), "빠른 배회. attack=leap: 놀람(!!) 예고 후 도약(Jump 클립)."),
            ["M05"] = M(1205, "설원 늑대", true, 1, 1.4f, 3.6f, 7f, new Color(0.60f, 0.68f, 0.85f), "배회 -> 발견 시 추격(ChaserEnemyAI). attack=lunge: 놀람(!!) 예고 후 짧은 돌진 물기."),
            ["M06"] = M(2201, "얼음 골렘", true, 3, 0.7f, 0f, 4f, new Color(0.65f, 0.75f, 0.90f), "중형 정예(hp 3). 느린 배회. attack=slam: 놀람(!!) 예고 후 지면 강타, 양쪽으로 퍼지는 충격파(점프로 회피)."),
            ["M07"] = M(1206, "눈덩이 정령", true, 1, 1.2f, 0f, 5f, new Color(0.90f, 0.95f, 1f), "배회. attack=charge: 놀람(!!) 예고 후 구르기 돌진(Roll 클립)."),
            ["M08"] = M(1207, "빙결 수호병", true, 2, 1.0f, 2.6f, 6f, new Color(0.40f, 0.60f, 0.95f), "배회 -> 발견 시 추격(ChaserEnemyAI), hp 2. attack=melee: 놀람(!!) 예고 후 창 찌르기."),
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
                // Visual prefabs have their pivot at the feet: a body-centre model (M03 bat) is lifted by its depth below the pivot.
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
            ["T01"] = P("설면 절벽 코어", "Terrain", 0f, "2x2x4 눈 캡 + 청회색 암반. 피벗 좌상단/깊이 중앙. 테마 킷 groundCore(+변형)."),
            ["T02"] = P("설면 절벽 왼쪽 끝", "Terrain", 0f, "2x2x4 왼쪽 끝 모듈. 테마 킷 groundEndLeft."),
            ["T03"] = P("설면 절벽 오른쪽 끝", "Terrain", 0f, "2x2x4 오른쪽 끝 모듈. 테마 킷 groundEndRight."),
            ["T04"] = P("빙암 코어", "Terrain", 0f, "2x2x4 눈 없는 암반, 상하좌우 반복. 테마 킷 rockCore(+변형)."),
            ["T05"] = P("얼음 표면 변형", "Terrain", 0f, "2x2x4 T01 접합. 테마 킷 groundCoreVariants. 테마 킷 iceSurface: surface=ice 블록(미끄럼)."),
            ["T06"] = P("공중 얼음 발판", "Terrain", 0f, "3x1x4 눈 상판 + 암반 하부 + 고드름. 테마 킷 floatingPlatform."),
            ["T07"] = P("목재 다리 발판", "Terrain", 0f, "3x0.4x4 덱 + 뒤쪽 난간(Rails). 테마 킷 timberPlatform."),
            ["T08"] = P("빙벽 확장 모듈", "Terrain", 3f, "2x8x4 장식 빙벽. 피벗 좌상단."),
            ["G01"] = P("체크포인트 비콘", "Gameplay", 0f, "높이 3. 테마 킷 checkpointVisual."),
            ["G02"] = P("골인 아치", "Gameplay", 0f, "2.5x3.8x1.2, 통로 1.5x3. 테마 킷 goalVisual."),
            ["G03"] = P("룬 압력판", "Gameplay", 0f, "Plinth/Plate/Emblem. 테마 킷 switchVisual."),
            ["G04"] = P("승강 얼음 문", "Gameplay", 0f, "이동 슬래브 1x3x1.5. 테마 킷 gateVisual."),
            ["G05"] = P("이동 얼음 발판", "Gameplay", 0f, "3x0.4x4 좌상단 피벗. 테마 킷 moverVisual/fallerVisual."),
            ["G06"] = P("수납식 얼음 가시", "Gameplay", 0f, "Plinth + Spike_01..03(+0.58 상승). 테마 킷 spikeTrapVisual."),
            ["P01"] = P("대형 설송", "Vegetation", 6f, "높이 4.5. 가지 끝 약한 바람."),
            ["P02"] = P("어린 설송", "Vegetation", 4f, "높이 2.2."),
            ["P03"] = P("눈바위 군락", "Prop", 4f, "바위 3 + 설관."),
            ["P04"] = P("겨울 관목", "Vegetation", 1.5f, "눈 덮인 마른 가지(바람)."),
            ["P05"] = P("빙정 군락", "Prop", 3f, "결정 3, 약한 발광."),
            ["P06"] = P("등불 기둥", "Prop", 2.5f, "높이 2.7, 등불 발광. 피벗 기둥 바닥."),
            ["P07"] = P("방향 표지판", "Prop", 2f, "화살판 2."),
            ["P08"] = P("보급 상자", "Prop", 2f, "Body/Lid(뒤 경첩)/Lock. 장식 전용."),
            ["P09"] = P("눈꽃 깃발", "Prop", 2f, "기둥형 깃발, 천 바람. 피벗 바닥 중앙."),
            ["P10"] = P("무너진 기둥", "Prop", 4f, "높이 3.2."),
            ["A01"] = P("얼음 성", "Landmark", 20f, "18x22x12 중앙동/탑4/첨탑/성벽. 근경 랜드마크."),
            ["A02"] = P("폐허 아치", "Landmark", 5f, "5x5x1.8 파손 아치 + 파편."),
            ["B01"] = P("독립 설산", "Background", 60f, "원경 입체 메시, LOD 3단."),
            ["B02"] = P("3봉 능선", "Background", 80f, "원경 능선, LOD 3단."),
            ["B03"] = P("빙하 절벽", "Background", 30f, "중경 빙벽, 뒷면/밑면 닫힘."),
            ["B04"] = P("설산 암석 아치", "Background", 50f, "실제 관통 아치(두께 8)."),
            ["B05"] = P("원경 얼음 성", "Background", 90f, "최원경 성, 창 발광."),
            ["B06"] = P("눈 언덕 군락", "Background", 25f, "눈 언덕 + 전나무 3."),
            ["B07"] = P("연속 설원 바닥", "Background", 2.4f, "400x150 연속 설원. x = 스테이지 길이/2 에 배치."),
            ["B08"] = P("전나무 숲 띠", "Background", 18f, "60 m 타일 반복 숲 띠(전나무 92그루), 끝단 이음매 없음."),
        };

        private static PropInfo P(string korean, string category, float z, string note) =>
            new PropInfo { Korean = korean, Category = category, Z = z, Note = note };

        private static string UpdatePropTable(Dictionary<string, string> envPrefabs)
        {
            var rows = new List<(string key, string line)>();
            foreach (var kv in envPrefabs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!Props.TryGetValue(ConceptId(kv.Key), out var info))
                    continue;
                string variant = "";
                foreach (var suffix in new[] { "_V2", "_V3", "_L", "_R" })
                    if (kv.Key.EndsWith(suffix, StringComparison.Ordinal))
                        variant = " (" + suffix.Substring(1) + ")";
                string line = string.Join(",", kv.Key, SunDunesImporter.Csv(info.Korean + variant), info.Category, kv.Value,
                    SunDunesImporter.F(info.Z), "false", "Implemented", SunDunesImporter.Csv("FROST PEAKS. " + info.Note));
                rows.Add((kv.Key, line));
            }
            int replaced = SunDunesImporter.ReplaceCsvRows(PropCatalog.TablePath, rows);
            return $"  PropTable: {rows.Count} FP rows ({replaced} updated, {rows.Count - replaced} added)";
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

            kit.groundCore = Load("Ground", "FP_T01_SnowCliffCore");
            kit.groundCoreVariants = Some(Load("Ground", "FP_T01_SnowCliffCore_V2"), Load("Ground", "FP_T01_SnowCliffCore_V3"));
            kit.iceSurface = Load("Ground", "FP_T05_FrozenSurface"); // only on "ice" blocks (they are slippery)
            // Snowy whites need depth cues: deeper sky, cool distance fog, warm-white sun on the play strip.
            kit.useAtmosphere = true;
            kit.skyColor = new Color(0.36f, 0.55f, 0.82f);
            kit.fogColor = new Color(0.56f, 0.66f, 0.84f);
            kit.fogStart = 18f;
            kit.fogEnd = 120f;
            kit.sunColor = new Color(1f, 0.95f, 0.88f);
            kit.sunIntensity = 1.1f;
            kit.groundEndLeft = Load("Ground", "FP_T02_SnowCliffEnd_L");
            kit.groundEndRight = Load("Ground", "FP_T03_SnowCliffEnd_R");
            kit.rockCore = Load("Ground", "FP_T04_IceRockCore");
            kit.rockCoreVariants = Some(Load("Ground", "FP_T04_IceRockCore_V2"), Load("Ground", "FP_T04_IceRockCore_V3"));
            kit.floatingPlatform = Load("Ground", "FP_T06_IceFloatingPlatform");
            kit.timberPlatform = Load("Ground", "FP_T07_TimberBridgeDeck");
            kit.checkpointVisual = Load("Gimmicks", "FP_G01_CheckpointBeacon");
            kit.goalVisual = Load("Gimmicks", "FP_G02_GoalArch");
            kit.switchVisual = Load("Gimmicks", "FP_G03_RunePlate");
            kit.gateVisual = Load("Gimmicks", "FP_G04_IceGate");
            kit.moverVisual = Load("Gimmicks", "FP_G05_IceMoverPlatform");
            kit.fallerVisual = Load("Gimmicks", "FP_G05_IceMoverPlatform");
            kit.spikeTrapVisual = Load("Gimmicks", "FP_G06_IceSpikeTrap");
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
                string note = $"FROST PEAKS {ConceptId(kv.Key)}. {i.Note} 비주얼: {kv.Value.visual}";
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
            return $"  MonsterTable: {rows.Count} FP rows ({replaced} updated, {rows.Count - replaced} added)";
        }
    }
}
