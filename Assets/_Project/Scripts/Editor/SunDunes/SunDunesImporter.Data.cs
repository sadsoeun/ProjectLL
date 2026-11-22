using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>Monsters and map-tool data (prop table, theme kit, monster table) for the Sun Dunes import.</summary>
    public static partial class SunDunesImporter
    {
        // ------------------------------------------------------------------ catalogue data

        private sealed class PropInfo
        {
            public string Korean, Category, Note;
            public float Z;
            public bool Collision;
        }

        // Keyed by concept ID (G01, P05, B03 ...). Category / default depth follow Docs/prop-table-schema.md.
        private static readonly Dictionary<string, PropInfo> Props = new Dictionary<string, PropInfo>
        {
            ["G01"] = P("모래 바닥 기본", "Terrain", 0f, "2x2x4 모래 상판 + 사암 측면. 피벗 좌상단/깊이 중앙. 테마 킷 groundCore."),
            ["G02"] = P("모래 반폭 바닥", "Terrain", 0f, "1x2x4. G01 접합면 공유."),
            ["G03"] = P("모래 오목 L 코너", "Terrain", 0f, "2x2x4. 상판 L자(1x1 사분면 제외)."),
            ["G04"] = P("모래 볼록 코너", "Terrain", 0f, "2x2x4 끝 모듈. L=왼쪽 끝, R=오른쪽 끝. 테마 킷 groundEndLeft/Right."),
            ["G05"] = P("모래 경사 발판", "Terrain", 0f, "폭 2, 높이차 1. COL_ 경사 충돌체 포함."),
            ["G06"] = P("사암 3단 계단", "Terrain", 0f, "2x2x4, 3단 총 1 m. COL_ 경사 충돌체 포함."),
            ["G07"] = P("사암 공중 발판", "Terrain", 0f, "Thin 3x0.4x4(테마 킷 timberPlatform) / Floating 3x1x4(테마 킷 floatingPlatform)."),
            ["G08"] = P("붕괴 발판", "Terrain", 0f, "3x0.4x4. 4조각 개별 피벗(낙하 기믹용)."),
            ["G09"] = P("사암 절벽 연장", "Terrain", 0f, "2x2x4 상하좌우 반복. 테마 킷 rockCore."),
            ["G10"] = P("오아시스 수반", "Terrain", 0f, "4x0.6x3. 테두리/물 분리(물은 애니메이션 물 셰이더)."),
            ["P01"] = P("야자수", "Vegetation", 6f, "높이 3.5. 잎 바람 흔들림."),
            ["P02"] = P("선인장", "Vegetation", 3f, "Tall(기둥형) / Round(구형)."),
            ["P03"] = P("사암 바위", "Prop", 4f, "실루엣 A/B/C."),
            ["P04"] = P("유적 기둥", "Prop", 5f, "Intact(정상) / Broken(파손)."),
            ["P05"] = P("태양 문", "Landmark", 4f, "3.5x3.2x1. 기둥2/아치/문양(발광). 테마 킷 goalVisual."),
            ["P06"] = P("계단 신전", "Landmark", 12f, "8x7x8. 신전/문/문양 분리."),
            ["P07"] = P("밧줄 다리", "Prop", 0f, "4 m. 판자/기둥4/로프2 분리. COL_ 통행면.", true),
            ["P08"] = P("토기", "Prop", 2.5f, "본체 + 파괴편 4."),
            ["P09"] = P("태양 깃발", "Prop", 3f, "봉/천 분리(천 바람). 피벗 봉 상단."),
            ["P10"] = P("석제 화로", "Prop", 3f, "받침/그릇 + FX_Fire 지점."),
            ["P11"] = P("태양 압력판", "Gameplay", 0f, "base/plate(이동 0.08)/emblem. 맵 도구 switches 요소의 테마 비주얼(SpikeTrap/FloorSwitch 구현됨)."),
            ["P12"] = P("상승 가시 함정", "Gameplay", 0f, "base/spikes3. 맵 도구 spikeTraps 요소의 테마 비주얼(가시가 로컬 Y +0.58 상승)."),
            ["P13"] = P("태양 문 (슬라이딩 게이트)", "Gameplay", 0f, "1x3x1.5. 맵 도구 gates 요소의 테마 비주얼. 바닥 중앙 피벗, COL_Gate."),
            ["B01"] = P("메사 군락", "Background", 45f, "원경 입체 메시, LOD 3단."),
            ["B02"] = P("암주 세트", "Background", 60f, "원경 암주 3개, LOD 3단."),
            ["B03"] = P("입체 사구", "Background", 30f, "중경 사구, 뒷면/밑면 닫힘."),
            ["B04"] = P("바위 아치", "Background", 50f, "원경 아치(실제 두께)."),
            ["B05"] = P("원경 신전", "Background", 90f, "최원경 신전, LOD 3단."),
            ["B06"] = P("원경 오아시스", "Background", 35f, "야자수/바위/물 조합."),
        };

        private static PropInfo P(string korean, string category, float z, string note, bool collision = false) =>
            new PropInfo { Korean = korean, Category = category, Z = z, Note = note, Collision = collision };

        private sealed class MonsterInfo
        {
            public int Id, Hp;
            public string Korean, Graph;
            public float Move, Chase, Range, View, HalfAngle, Lose;
            public bool Stompable;
            public Color Burst;
            public string Note;
            public string Movement = "ground"; // ground / fly / burrow (MonsterTable column movement)
        }

        // Burrow (1104 / 1105) and fly (1106) are Monster.Movement.cs (column movement); attacks are Monster.Attack.cs
        // (column attack, values in Attacks below). Decisions still come from the patrol / chaser graphs.
        private static readonly Dictionary<string, MonsterInfo> Monsters = new Dictionary<string, MonsterInfo>
        {
            ["M01"] = M(1101, "선인장 워커", false, 1, 1.0f, 0f, 5f, new Color(0.45f, 0.70f, 0.30f), "가시 몸이라 밟기 불가. attack=shoot: 사거리 안에서 놀람(!!) 예고 후 씨앗 투사체 1발."),
            ["M02"] = M(1102, "태양 풍뎅이", true, 1, 1.4f, 0f, 6f, new Color(0.25f, 0.45f, 0.75f), "attack=charge: 놀람(!!)·흙먼지 예고 후 벽/낭떠러지까지 구르기 돌진. Roll 클립 포함."),
            ["M03"] = M(1103, "사구 전갈", true, 1, 1.2f, 3.0f, 6f, new Color(0.75f, 0.35f, 0.20f), "배회 -> 발견 시 추격(ChaserEnemyAI). attack=melee: 놀람(!!) 예고 후 앞쪽 꼬리 찌르기."),
            ["M04"] = M(1104, "모래 슬라임", true, 1, 0.8f, 0f, 4f, new Color(0.95f, 0.80f, 0.35f), "movement=burrow: 플레이어가 emergeDistance 안에 들면 0.8초 모래 더미로 예고한 뒤 솟아올라 순찰한다(밟기 가능). Burrow 클립 포함.", "burrow"),
            ["M05"] = M(1105, "모래벌레", false, 2, 0.6f, 0f, 3f, new Color(0.80f, 0.60f, 0.40f), "movement=burrow: 예고 후 수직으로 솟아 1.2초 머물고 가라앉아 2초 쉬며 반복한다. 이빨 때문에 밟기 불가(접촉 사망).", "burrow"),
            ["M06"] = M(1106, "황혼 박쥐", true, 1, 1.6f, 0f, 6f, new Color(0.45f, 0.35f, 0.60f), "movement=fly: 배치 높이 +2.5 m에서 공중 배회하다 아래 5 m 안에 플레이어가 있으면 0.5초 예고(!! 아이콘) 후 급강하한다. Fly 클립 포함.", "fly"),
            ["M07"] = M(1107, "붕대 수호자", true, 2, 1.0f, 2.5f, 5f, new Color(0.85f, 0.80f, 0.65f), "배회 -> 발견 시 추격(ChaserEnemyAI). attack=melee: 놀람(!!) 예고 후 근접 펀치."),
            ["M08"] = M(2101, "사암 골렘", true, 3, 0.7f, 0f, 4f, new Color(0.85f, 0.55f, 0.30f), "중형 정예(hp 3). 느린 배회. attack=slam: 놀람(!!) 예고 후 지면 강타, 양쪽으로 퍼지는 충격파(점프로 회피)."),
        };

        /// <summary>MonsterTable attack columns per id (both stages): attack kind, attackRange, attackInterval. Damage is always 1.</summary>
        internal static readonly Dictionary<int, (string kind, float range, float interval)> Attacks = new Dictionary<int, (string, float, float)>
        {
            [1101] = ("shoot", 6f, 3.5f), [1102] = ("charge", 5f, 3.5f), [1103] = ("melee", 1.6f, 2.5f), [1107] = ("melee", 1.5f, 2.5f),
            [2101] = ("slam", 3f, 4f),
            [1201] = ("leap", 4f, 3f), [1202] = ("charge", 5f, 3.5f), [1204] = ("leap", 4.5f, 2.8f), [1205] = ("lunge", 3f, 2.5f),
            [2201] = ("slam", 3f, 4f), [1206] = ("charge", 5f, 3.5f), [1207] = ("melee", 2f, 2.5f),
            [1301] = ("melee", 1.5f, 2.5f),
            [1402] = ("melee", 1.5f, 2.5f), [1405] = ("charge", 5f, 3.5f), [1406] = ("charge", 5f, 3.5f), [2401] = ("slam", 3f, 4f), [1407] = ("melee", 2f, 2.5f),
        };

        /// <summary>canAttack,attackRange,attackInterval,attackDamage cells (already comma-joined).</summary>
        internal static string AttackColumns(int id) =>
            Attacks.TryGetValue(id, out var a) ? $"true,{F(a.range)},{F(a.interval)},1" : "false,0,0,0";

        internal static string AttackKind(int id) => Attacks.TryGetValue(id, out var a) ? a.kind : "none";

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

        private static string ConceptId(string assetName)
        {
            // SD_G04_Ground_OuterCorner_L -> G04
            var parts = assetName.Split('_');
            return parts.Length > 1 ? parts[1] : assetName;
        }

        // ------------------------------------------------------------------ monsters

        private static readonly string[] LoopClips = { "Idle", "Move", "Fly", "Roll" };

        private static (string visual, string gameplay) ImportMonster(JObject m, StringBuilder log)
        {
            string name = (string)m["name"];
            string fbx = (string)m["fbx"];
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
            {
                log.AppendLine($"  - {name}: FBX not found ({fbx}), skipped");
                return (null, null);
            }
            var mi = ConfigureModel(fbx, true);
            ConfigureClips(mi);

            string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
            var albedo = ConfigureTexture($"{dir}/{(string)m["albedo"]}", true);
            var emission = m["emission"]?.Type == JTokenType.String ? ConfigureTexture($"{dir}/{(string)m["emission"]}", true) : null;
            var mat = EnsureToonMaterial($"{dir}/M_{name}.mat", albedo, emission, false, false);

            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
            var controller = BuildController($"{dir}/{name}.controller", clips);

            // Visual prefab: model + Animator (faces +Z, pivot at the feet), like the Stage01 *_Visual prefabs.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            string visualPath = $"{MonsterPrefabRoot}/{name}_Visual.prefab";
            SceneFlowSetup.EnsureFolder(MonsterPrefabRoot);
            var root = new GameObject(name + "_Visual");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                DressModel(root, mat, null, false);
                PrefabUtility.SaveAsPrefabAsset(root, visualPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // Gameplay prefab: the shared Monster component + Behavior graph, with the visual attached.
            string id = ConceptId(name);
            string gameplayPath = null;
            if (Monsters.TryGetValue(id, out var info))
            {
                float height = (float?)m["height_m"] ?? 1f;
                var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
                var monsterRoot = MonsterSetup.NewMonsterRoot(name, out var visual);
                var comp = MonsterSetup.FinishMonster(monsterRoot, visual, info.Burst, info.Graph);
                MonsterSetup.AttachAnimatedModel(monsterRoot, visual, comp, visualPrefab, Mathf.Clamp(height, 0.4f, 2.2f));
                if (info.Movement == "burrow")
                    EditorWire.Set(comp, "emergeFxPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabRoot + "/FX/SD_FX03_BurrowEmerge.prefab"));
                gameplayPath = $"{MonsterPrefabRoot}/{name}.prefab";
                SceneFlowSetup.SavePrefab(monsterRoot, gameplayPath);
            }
            log.AppendLine($"  + {name}: clips {clips.Count} ({string.Join(", ", clips.Select(c => c.name))}){(gameplayPath != null ? ", gameplay prefab" : "")}");
            return (visualPath, gameplayPath);
        }

        private static string ClipBaseName(string take)
        {
            int bar = take.LastIndexOf('|');
            return bar >= 0 ? take.Substring(bar + 1) : take;
        }

        internal static void ConfigureClips(ModelImporter mi)
        {
            var source = mi.clipAnimations != null && mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            bool dirty = false;
            var clips = source.Select(c =>
            {
                string baseName = ClipBaseName(c.takeName);
                bool loop = LoopClips.Contains(baseName);
                if (c.name != baseName || c.loopTime != loop || c.lockRootPositionXZ != true)
                    dirty = true;
                c.name = baseName;
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootPositionXZ = true;
                c.lockRootHeightY = true;
                c.lockRootRotation = true;
                return c;
            }).ToArray();
            if (!dirty && mi.clipAnimations != null && mi.clipAnimations.Length == clips.Length)
                return;
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }

        /// <summary>Idle ⇄ Move by Speed; Attack / Hit return to Idle; Defeat is final. Other clips are loose states.</summary>
        internal static AnimatorController BuildController(string path, List<AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Defeat", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n);

            var states = new Dictionary<string, AnimatorState>();
            AnimatorState State(string n, Vector2 pos)
            {
                var s = sm.AddState(n, pos);
                s.motion = Clip(n);
                states[n] = s;
                return s;
            }
            var idle = State("Idle", new Vector2(300, 0));
            sm.defaultState = idle;
            var move = State("Move", new Vector2(300, 120));
            var t = idle.AddTransition(move);
            t.hasExitTime = false; t.duration = 0.1f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t = move.AddTransition(idle);
            t.hasExitTime = false; t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            foreach (var (n, y) in new[] { ("Attack", 240), ("Hit", 360) })
            {
                var s = State(n, new Vector2(600, y));
                var any = sm.AddAnyStateTransition(s);
                any.hasExitTime = false; any.duration = 0.05f; any.canTransitionToSelf = false;
                any.AddCondition(AnimatorConditionMode.If, 0f, n);
                var back = s.AddTransition(idle);
                back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.1f;
            }
            var defeat = State("Defeat", new Vector2(600, 480));
            var toDefeat = sm.AddAnyStateTransition(defeat);
            toDefeat.hasExitTime = false; toDefeat.duration = 0.05f; toDefeat.canTransitionToSelf = false;
            toDefeat.AddCondition(AnimatorConditionMode.If, 0f, "Defeat");

            int extra = 0;
            foreach (var c in clips.Where(c => !states.ContainsKey(c.name)))
                State(c.name, new Vector2(0, 120 * extra++)); // Anticipate, Roll, Burrow, Fly ...
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ------------------------------------------------------------------ prop table

        internal static string Csv(string s) => s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        internal static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string UpdatePropTable(Dictionary<string, string> envPrefabs)
        {
            string path = PropCatalog.TablePath;
            var rows = new List<(string key, string line)>();
            foreach (var kv in envPrefabs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string id = ConceptId(kv.Key);
                if (!Props.TryGetValue(id, out var info))
                    continue; // FX and unknown ids are not map-tool props
                string variant = kv.Key.Length > 0 ? VariantLabel(kv.Key) : "";
                string line = string.Join(",", kv.Key, Csv(info.Korean + variant), info.Category, kv.Value, F(info.Z),
                    info.Collision ? "true" : "false", "Implemented", Csv("SUN DUNES. " + info.Note));
                rows.Add((kv.Key, line));
            }
            int replaced = ReplaceCsvRows(path, rows);
            return $"  PropTable: {rows.Count} SD rows ({replaced} updated, {rows.Count - replaced} added)";
        }

        private static string VariantLabel(string name)
        {
            string[] known = { "_Tall", "_Round", "_Intact", "_Broken", "_Thin", "_Floating", "_L", "_R", "_A", "_B", "_C" };
            foreach (var k in known)
                if (name.EndsWith(k, StringComparison.Ordinal))
                    return " (" + k.Substring(1) + ")";
            return "";
        }

        /// <summary>Replaces rows whose first field matches, appends the rest. Keeps the header, BOM and LF endings.</summary>
        internal static int ReplaceCsvRows(string assetPath, List<(string key, string line)> rows)
        {
            string full = Path.GetFullPath(assetPath);
            var lines = File.ReadAllText(full, Encoding.UTF8).TrimStart('﻿').Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
            int replaced = 0;
            foreach (var (key, line) in rows)
            {
                int index = lines.FindIndex(1, l => l.StartsWith(key + ",", StringComparison.Ordinal));
                if (index >= 0)
                {
                    lines[index] = line;
                    replaced++;
                }
                else
                {
                    lines.Add(line);
                }
            }
            File.WriteAllText(full, string.Join("\n", lines) + "\n", new UTF8Encoding(true));
            AssetDatabase.ImportAsset(assetPath);
            return replaced;
        }

        // ------------------------------------------------------------------ theme kit

        private static string UpdateThemeKit(Dictionary<string, string> envPrefabs)
        {
            string path = StageThemeKit.PathFor("SunDunes");
            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(path);
            bool created = kit == null;
            if (created)
            {
                SceneFlowSetup.EnsureFolder(StageThemeKit.Dir);
                kit = ScriptableObject.CreateInstance<StageThemeKit>();
                AssetDatabase.CreateAsset(kit, path);
            }
            GameObject Load(string name) => envPrefabs.TryGetValue(name, out var p) ? AssetDatabase.LoadAssetAtPath<GameObject>(p) : null;
            GameObject Shared(string p) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/GreenMeadow/" + p + ".prefab");
            GameObject LoadOrDisk(string name, string category) => Load(name) != null ? Load(name)
                : AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvPrefabRoot}/{category}/{name}.prefab"); // imported in an earlier run

            // The v2 sandstone terrain (SD_G11..G15, ArtSource/SunDunes/_build_scripts/ground/build_sd_terrain_v2.py) wins when its
            // prefabs exist: a re-import must not put the stages back on the v1 ground.
            GameObject V2(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvPrefabRoot}/Ground/{name}.prefab");
            GameObject V2Or(string v2, string v1) => V2(v2) != null ? V2(v2) : Load(v1);
            kit.groundCore = V2Or("SD_G11_Sandstone_Core", "SD_G01_Ground_Straight");
            kit.groundEndLeft = V2Or("SD_G12_Sandstone_End_L", "SD_G04_Ground_OuterCorner_L");
            kit.groundEndRight = V2Or("SD_G12_Sandstone_End_R", "SD_G04_Ground_OuterCorner_R");
            kit.rockCore = V2Or("SD_G13_Sandstone_Rock", "SD_G09_Cliff_Extension");
            kit.floatingPlatform = V2Or("SD_G14_Sandstone_Floating", "SD_G07_Platform_Floating");
            kit.timberPlatform = V2Or("SD_G15_PalmTimber_Platform", "SD_G07_Platform_Thin");
            kit.goalVisual = Load("SD_P05_SunArch");
            // Gimmick visuals (schema v1.4). SD_P13_SunGate is modelled separately: until its prefab exists the slot stays empty
            // (primitive gate) and the next Run() picks it up. spikeVisual stays empty (no static spike asset in the pack).
            kit.fallerVisual = LoadOrDisk("SD_G08_Platform_Breakaway", "Ground");
            kit.moverVisual = V2("SD_G15_PalmTimber_Platform") != null ? V2("SD_G15_PalmTimber_Platform") : LoadOrDisk("SD_G07_Platform_Thin", "Ground");
            kit.spikeTrapVisual = LoadOrDisk("SD_P12_SpikeTrap", "Props");
            kit.switchVisual = LoadOrDisk("SD_P11_SunSwitch", "Props");
            kit.gateVisual = LoadOrDisk("SD_P13_SunGate", "Props");
            // No desert-specific checkpoint / coin / gem in the concept pack: the shared gameplay pickups are referenced
            // (not copied), so they stay recognisable across stages.
            kit.checkpointVisual = Shared("G01_Checkpoint");
            kit.coinVisual = Shared("G03a_Coin");
            kit.gemVisual = Shared("G03b_MagentaGem");
            kit.groundCoreVariants = new[] { V2("SD_G11_Sandstone_Core_V2"), V2("SD_G11_Sandstone_Core_V3") }.Where(g => g != null).ToArray();
            kit.rockCoreVariants = new[] { V2("SD_G13_Sandstone_Rock_V2"), V2("SD_G13_Sandstone_Rock_V3") }.Where(g => g != null).ToArray();
            EditorUtility.SetDirty(kit);
            int filled = new[] { kit.groundCore, kit.groundEndLeft, kit.groundEndRight, kit.rockCore, kit.floatingPlatform, kit.timberPlatform, kit.goalVisual }
                .Count(g => g != null);
            return $"  Theme kit {path}: {(created ? "created" : "updated")}, desert slots filled {filled}/7, gimmick slots {new[] { kit.fallerVisual, kit.moverVisual, kit.spikeTrapVisual, kit.switchVisual, kit.gateVisual }.Count(g => g != null)}/5 (gate empty = SD_P13_SunGate not modelled yet)";
        }

        // ------------------------------------------------------------------ monster table

        private static string UpdateMonsterTable(Dictionary<string, (string visual, string gameplay, JObject m)> monsters)
        {
            const string path = "Assets/_Project/Data/Tables/MonsterTable.csv";
            var rows = new List<(string key, string line)>();
            foreach (var kv in monsters.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (kv.Value.gameplay == null || !Monsters.TryGetValue(ConceptId(kv.Key), out var i))
                    continue;
                string note = $"SUN DUNES {ConceptId(kv.Key)}. {i.Note} 비주얼: {kv.Value.visual}";
                string line = string.Join(",",
                    i.Id.ToString(CultureInfo.InvariantCulture), "Implemented", Csv(i.Korean), $"monster.{i.Id}.name", kv.Value.gameplay,
                    $"Assets/_Project/Art/Icons/Monsters/icon_monster_{i.Id}.png", i.Graph,
                    i.Hp.ToString(CultureInfo.InvariantCulture), F(i.Move), F(i.Chase), F(i.Range), F(i.View), F(i.HalfAngle), F(i.Lose),
                    "false", "0", "0", SunDunesImporter.AttackColumns(i.Id),
                    i.Stompable ? "true" : "false", "true", "1", i.Movement, SunDunesImporter.AttackKind(i.Id), Csv(note));
                rows.Add((i.Id.ToString(CultureInfo.InvariantCulture), line));
            }
            int replaced = ReplaceCsvRows(path, rows);
            return $"  MonsterTable: {rows.Count} SD rows ({replaced} updated, {rows.Count - replaced} added)";
        }
    }
}
