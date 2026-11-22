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
    /// Imports the EMBER FORTRESS (world 5) art described in Docs/art/emberfortress-contract.md. Same pipeline as the Coral Coast
    /// importer (Sun Dunes texture / material / model / LOD / animator helpers): reads Docs/art/emberfortress/manifest/*.json,
    /// builds the prefabs under Prefabs/Environment/EmberFortress/{Ground,Props,Gimmicks,Architecture,Background} and
    /// Prefabs/Monsters/Stage05, then fills the map-tool data: PropTable.csv rows (EF_*), Data/Themes/EmberFortress.asset and
    /// MonsterTable.csv rows (1401–1407, 2401). The lava sea (B08) is an emissive, shadowless toon surface.
    /// Idempotent (prefab GUIDs are kept on re-run). Menu: Tools/MyGame/Art/Import Ember Fortress.
    /// </summary>
    public static class EmberFortressImporter
    {
        private const string EnvArtRoot = "Assets/_Project/Art/Environment/EmberFortress";
        private const string EnvPrefabRoot = "Assets/_Project/Prefabs/Environment/EmberFortress";
        private const string MonsterPrefabRoot = "Assets/_Project/Prefabs/Monsters/Stage05";
        private const string Theme = "EmberFortress";

        private static string ManifestDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/art/emberfortress/manifest"));

        private static readonly string[] Categories = { "Ground", "Props", "Gimmicks", "Architecture", "Background" };

        /// <summary>Background tint (multiplies the albedo): far layers sink into the dusky ash haze so the lit play strip reads first.</summary>
        private static Color BackgroundTint(string name) =>
            name.Contains("B07") || name.Contains("B08") ? Color.white
            : name.Contains("B06") ? new Color(0.9f, 0.88f, 0.9f)
            : name.Contains("B05") || name.Contains("B01") ? new Color(0.82f, 0.78f, 0.84f)
            : new Color(0.86f, 0.83f, 0.88f);

        [MenuItem("Tools/MyGame/Art/Import Ember Fortress")]
        public static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing Ember Fortress.");
            var log = new StringBuilder("[EmberFortressImporter]\n");
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
            try
            {
                foreach (var kv in EmberFortressAmbient.BuildFx())
                {
                    envPrefabs[kv.Key] = kv.Value;
                    log.AppendLine($"  + FX/{kv.Key}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                log.AppendLine("  ! ambient FX: " + e.Message);
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

        /// <summary>EF_T01_CooledBasaltCore_V2 -> T01.</summary>
        private static string ConceptId(string assetName)
        {
            var parts = assetName.Split('_');
            return parts.Length > 1 ? parts[1] : assetName;
        }

        /// <summary>Albedo-derived toon shadow (basalt / iron: slight hue shift toward violet like the key art).</summary>
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
                mat.SetFloat("_WindStrength", name.Contains("Shrub") ? 0.03f : 0.06f);
            ApplyHsvShadow(mat);
            if (cat == "Background")
                mat.SetColor("_BaseColor", BackgroundTint(name));

            bool lava = ConceptId(name) == "B08" || ConceptId(name) == "T08";
            // The lava sea uses the animated world-space lava shader (flowing veins, drifting crust plates) instead of the
            // static painted tile; the painted toon material is kept only as the fallback.
            var lavaFlow = ConceptId(name) == "B08" ? EmberFortressAmbient.EnsureLavaMaterial() : null;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var (rc, cc, lv) = SunDunesImporter.DressModel(root, mat, null, cat == "Background");
                if (lava)
                    // Glowing lava never casts or receives shadows (it is the light source of the scene).
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                        if (lavaFlow == null)
                            continue;
                        // The shader draws its own crust plates; the modelled crust strips would only z-fight with it.
                        if (r.name.IndexOf("Crust", StringComparison.OrdinalIgnoreCase) >= 0)
                            r.gameObject.SetActive(false);
                        else
                            r.sharedMaterials = Enumerable.Repeat(lavaFlow, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                    }
                if (cat == "Ground" && ConceptId(name) == "T04")
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                string path = $"{EnvPrefabRoot}/{cat}/{name}.prefab";
                SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"  + {cat}/{name}: renderers {rc}, colliders {cc}, LODs {lv}{(wind ? ", wind" : "")}{(emission != null ? ", emission" : "")}{(lava ? ", lava" : "")}");
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
            ["M01"] = M(1401, "잿불 슬라임", true, 1, 0.8f, 0f, 3f, new Color(1f, 0.55f, 0.2f), "느린 배회(공격 없음). 접촉 시 피격, 밟기 가능. Jump 클립은 연출용(전용 AI 없음)."),
            ["M02"] = M(1402, "용암 풍뎅이", true, 1, 1.3f, 0f, 4f, new Color(1f, 0.45f, 0.15f), "배회. attack=melee: 놀람(!!) 예고 후 뿔 찌르기."),
            ["M03"] = M(1403, "잿불 박쥐", true, 1, 1.4f, 0f, 5f, new Color(0.75f, 0.7f, 0.8f), "movement=fly: 공중 배회(Fly 클립). 기존 비행 AI의 급강하 규칙을 그대로 사용.", "fly"),
            ["M06"] = M(1404, "불씨 정령", true, 1, 1f, 0f, 3f, new Color(1f, 0.75f, 0.3f), "movement=fly: 부유 배회(Fly 클립). 암편 공전은 애니메이션. 기존 비행 AI 규칙 사용.", "fly"),
            ["M04"] = M(1405, "잿빛 도롱뇽", true, 1, 1.2f, 0f, 5f, new Color(0.95f, 0.5f, 0.2f), "배회. attack=charge: 놀람(!!) 예고(입 발광) 후 낮은 돌진."),
            ["M05"] = M(1406, "흑요석 아르마딜로", false, 1, 1.0f, 0f, 5f, new Color(0.55f, 0.4f, 0.75f), "분절 껍질 때문에 밟기 불가. attack=charge: 놀람(!!) 예고 후 몸을 말아 구르기 돌진(Roll 클립)."),
            ["M07"] = M(2401, "현무암 골렘", true, 3, 0.7f, 0f, 4f, new Color(0.5f, 0.45f, 0.5f), "중형 정예(hp 3). 느린 배회. attack=slam: 놀람(!!) 예고 후 지면 강타, 양쪽으로 퍼지는 충격파(점프로 회피)."),
            ["M08"] = M(1407, "요새 수호병", true, 2, 1.0f, 2.6f, 6f, new Color(0.7f, 0.35f, 0.25f), "배회 -> 발견 시 추격(ChaserEnemyAI), hp 2. attack=melee: 놀람(!!) 예고 후 철퇴 내려치기."),
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
            ["T01"] = P("식은 현무암 코어", "Terrain", 0f, "2x2x4 식은 상판 + 현무암. 피벗 좌상단/깊이 중앙. 테마 킷 groundCore(+변형)."),
            ["T02"] = P("현무암 왼쪽 끝", "Terrain", 0f, "2x2x4 왼쪽 끝 모듈. 테마 킷 groundEndLeft."),
            ["T03"] = P("현무암 오른쪽 끝", "Terrain", 0f, "2x2x4 오른쪽 끝 모듈. 테마 킷 groundEndRight."),
            ["T04"] = P("적층 현무암", "Terrain", 0f, "2x2x4 상하좌우 반복, 발광 없음. 테마 킷 rockCore(+변형)."),
            ["T05"] = P("요새 포장", "Terrain", 0f, "2x2x4 T01 접합, 포장면 높이 0, 청동 띠. 장식/변형 지형."),
            ["T06"] = P("현무암 발판", "Terrain", 0f, "3x1x4 평탄 상판. 테마 킷 floatingPlatform."),
            ["T07"] = P("철제 다리 덱", "Terrain", 0f, "3x0.4x4 리벳 철판 덱. 테마 킷 timberPlatform."),
            ["T08"] = P("용암 수로", "Terrain", 0f, "2x2x4 좌상단 피벗, 용암면 Y -0.3 발광. 장식 전용(피해 없음)."),
            ["G01"] = P("잿불 체크포인트", "Gameplay", 0f, "높이 3, 황금 수정. 테마 킷 checkpointVisual."),
            ["G02"] = P("현무암 골인 아치", "Gameplay", 0f, "2.7x4x1.2, 통로 1.5x3. 테마 킷 goalVisual."),
            ["G03"] = P("용광로 압력판", "Gameplay", 0f, "Base/Plate/Emblem. 테마 킷 switchVisual."),
            ["G04"] = P("철제 격자문", "Gameplay", 0f, "이동 격자 1x3x1.5. 테마 킷 gateVisual. 고정 가이드는 PortcullisGuide 장식."),
            ["G05"] = P("주조장 승강 발판", "Gameplay", 0f, "3x0.4x4 좌상단 피벗. 테마 킷 moverVisual/fallerVisual."),
            ["G06"] = P("수납식 흑요석 가시", "Gameplay", 0f, "Base + Spike_01..03(+0.58 상승). 테마 킷 spikeTrapVisual."),
            ["P01"] = P("흑요석 결정", "Prop", 3f, "결정 3종 조합, 은은한 발광."),
            ["P02"] = P("식은 용암 바위", "Prop", 4f, "바위 3, 발광 없음."),
            ["P03"] = P("현무암 기둥 군락", "Prop", 4f, "6각 기둥 5."),
            ["P04"] = P("재빛 관목", "Vegetation", 2.5f, "붉은 마른 가지(약한 바람)."),
            ["P05"] = P("화로", "Prop", 2.5f, "불꽃 메시 발광."),
            ["P06"] = P("잿불 등불", "Prop", 2.5f, "높이 2.5, 등불 발광."),
            ["P07"] = P("모루", "Prop", 2.5f, "장식 전용."),
            ["P08"] = P("광석 상자", "Prop", 2.5f, "장식 전용(파괴 불가)."),
            ["P09"] = P("체인 기둥", "Prop", 2.2f, "기둥 + 체인."),
            ["P10"] = P("요새 깃발", "Prop", 2.5f, "천 바람."),
            ["P11"] = P("부서진 석주", "Prop", 3f, "높이 3, 파편 분리."),
            ["P12"] = P("화염 분출구", "Prop", 3f, "장식 전용(불꽃·피해 없음)."),
            ["A01"] = P("용암 요새", "Landmark", 22f, "20x22x14 중앙동 + 탑4 + 청동뿔. 근경 랜드마크."),
            ["A02"] = P("대장간 쉼터", "Landmark", 5f, "5x4.5x4 장식(충돌 없음), 화덕 발광."),
            ["B01"] = P("화산체", "Background", 70f, "원경 화산, 분화구 용암띠, LOD 3단."),
            ["B02"] = P("현무암 능선", "Background", 60f, "원경 능선, LOD 3단."),
            ["B03"] = P("흑요석 암주 군락", "Background", 35f, "중경 암주, LOD 3단."),
            ["B04"] = P("현무암 아치", "Background", 45f, "관통 아치, LOD 3단."),
            ["B05"] = P("원경 요새", "Background", 90f, "A01 단순화, 창 발광."),
            ["B06"] = P("용암 폭포 절벽", "Background", 30f, "흐름 메시 발광, LOD 3단."),
            ["B07"] = P("현무암 바닥", "Background", 2.4f, "400x150 연속 현무암 평지. x = 스테이지 길이/2."),
            ["B08"] = P("용암 수면", "Background", 2.4f, "32x32 용암 타일. MyGame/Ambient/Lava 애니메이션 셰이더(흐르는 용암맥·떠다니는 식은 껍질)."),
            ["FX03"] = P("용암 기포", "Background", -6f, "FX03 용암 수면 반복: 32x16 m 범위에서 부풀었다 터지는 발광 기포. 피벗 = 용암면(y -1.8). 장식 전용."),
            ["FX10"] = P("떠오르는 불티", "Background", 8f, "용암에서 천천히 떠오르는 작은 불티(32x20 m 범위). 피벗 = 용암면(y -1.8). 장식 전용."),
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
                    korean = "철제 격자문 고정 가이드";
                    category = "Prop";
                    z = 0f;
                }
                string line = string.Join(",", kv.Key, SunDunesImporter.Csv(korean), category, kv.Value,
                    SunDunesImporter.F(z), "false", "Implemented", SunDunesImporter.Csv("EMBER FORTRESS. " + info.Note));
                rows.Add((kv.Key, line));
            }
            int replaced = SunDunesImporter.ReplaceCsvRows(PropCatalog.TablePath, rows);
            return $"  PropTable: {rows.Count} EF rows ({replaced} updated, {rows.Count - replaced} added)";
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

            kit.groundCore = Load("Ground", "EF_T01_CooledBasaltCore");
            // T05 (fortress paving) is not a random variant: scattered paving cells on raw basalt would read as noise. It is a prop.
            kit.groundCoreVariants = Some(Load("Ground", "EF_T01_CooledBasaltCore_V2"), Load("Ground", "EF_T01_CooledBasaltCore_V3"));
            kit.iceSurface = null; // no slippery surfaces in world 5
            // Volcanic dusk: smoky sky, warm ember haze, low warm sun.
            kit.useAtmosphere = true;
            // Warm ember haze (matches the dusk sky horizon) that starts further out, so mid-ground rocks keep their shape.
            kit.skyColor = new Color(0.40f, 0.25f, 0.27f);
            kit.fogColor = new Color(0.50f, 0.33f, 0.32f);
            kit.fogStart = 32f;
            kit.fogEnd = 175f;
            kit.skyMaterial = EmberFortressAmbient.EnsureSkyMaterial();
            kit.sunColor = new Color(1f, 0.82f, 0.66f);
            kit.sunIntensity = 1.1f;
            kit.groundEndLeft = Load("Ground", "EF_T02_BasaltEnd_L");
            kit.groundEndRight = Load("Ground", "EF_T03_BasaltEnd_R");
            kit.rockCore = Load("Ground", "EF_T04_StackedBasaltCore");
            kit.rockCoreVariants = Some(Load("Ground", "EF_T04_StackedBasaltCore_V2"), Load("Ground", "EF_T04_StackedBasaltCore_V3"));
            kit.floatingPlatform = Load("Ground", "EF_T06_BasaltPlatform");
            kit.timberPlatform = Load("Ground", "EF_T07_IronBridgeDeck");
            kit.checkpointVisual = Load("Gimmicks", "EF_G01_EmberCheckpoint");
            kit.goalVisual = Load("Gimmicks", "EF_G02_BasaltGoalArch");
            kit.switchVisual = Load("Gimmicks", "EF_G03_FurnacePlate");
            kit.gateVisual = Load("Gimmicks", "EF_G04_IronPortcullis");
            kit.moverVisual = Load("Gimmicks", "EF_G05_FoundryLift");
            kit.fallerVisual = Load("Gimmicks", "EF_G05_FoundryLift");
            kit.spikeTrapVisual = Load("Gimmicks", "EF_G06_ObsidianSpikeTrap");
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
                string note = $"EMBER FORTRESS {ConceptId(kv.Key)}. {i.Note} 비주얼: {kv.Value.visual}";
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
            return $"  MonsterTable: {rows.Count} EF rows ({replaced} updated, {rows.Count - replaced} added)";
        }
    }
}
