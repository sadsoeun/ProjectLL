#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.Dev
{
    /// <summary>
    /// Editor-only showcase reel: drives the real game (title, world map, every stage and boss) with scripted input and
    /// records a 1920x1080 / 30 fps MP4 with MediaEncoder. Frames are only captured while a shot is live, so scene loads
    /// never appear in the video. Started from Tools/MyGame/Demo/Record Showcase Reel (ShowcaseReelMenu), which also backs
    /// up and restores the save + settings files around the run.
    /// </summary>
    public sealed partial class ShowcaseDirector : MonoBehaviour
    {
        public const int Width = 1920, Height = 1080, Fps = 30;
        public static string OutputDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/deliverables"));
        public static ShowcaseDirector Instance { get; private set; }
        public static bool Finished;
        /// <summary>Short test run: two stages, one boss, the UI shots.</summary>
        public static bool Quick;

        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // ------------------------------------------------------------------ capture state
        private MediaEncoder encoder;
        private RenderTexture screenRt, outRt;
        private Texture2D frameTex;
        private NativeArray<float> audioBuf;
        private int channels = 2;
        private bool recording;
        private int savedVSync, savedTargetFps;
        private int framesRecorded;
        private AudioSource music;
        private AudioClip bgmMain, bgmBoss;
        private readonly List<string> log = new List<string>();
        private int stillIndex;

        // ------------------------------------------------------------------ overlay
        private Font font;
        private RectTransform badge, lower, card;
        private Text badgeText, lowerTitle, lowerSub, cardTitle, cardSub, cardBody;
        private Image flash;
        private float flashAlpha;

        private static readonly string[] Worlds = { "W1", "W2", "W3", "W4", "W5" };

        public static void Launch(AudioClip main, AudioClip boss)
        {
            Finished = false;
            var go = new GameObject("ShowcaseDirector");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<ShowcaseDirector>();
            d.bgmMain = main;
            d.bgmBoss = boss;
        }

        private void Awake() => Instance = this;

        private IEnumerator Start()
        {
            Time.captureFramerate = Fps;
            // Real time must match video time: unscaled-time effects (time stop, cut-ins, UI) run on real seconds.
            savedVSync = QualitySettings.vSyncCount;
            savedTargetFps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Fps;
            Application.runInBackground = true;
            DemoMode.Invincible = true;
            DemoMode.ScriptedInput = true;
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(Path.Combine(OutputDir, Quick ? "stills_quick" : "stills"));
            BuildOverlay();
            BuildMusic();

            while (!GameSystems.Exists) yield return null;
            yield return WaitIdle();
            yield return null;

            Log($"screen {Screen.width}x{Screen.height}, audio {AudioSettings.outputSampleRate} Hz");
            BeginEncoder();
            if (Trailer)
            {
                yield return Story ? StorySequence() : TrailerSequence();
                EndEncoder();
                Finish();
                yield break;
            }

            // ---------------------------------------------------- 1. intro card + title + world map
            PlayMusic(bgmMain);
            ShowCard("Leaf & Leap", "3D 횡스크롤 플랫포머 · 1차 마일스톤 시연 영상",
                "① 전체 스테이지 25개   ② 보스 5종 · 몬스터 AI 패턴   ③ UI 포함 1차 폴리싱   ④ 프로젝트 소개 문서(AI 작성)");
            yield return Rec(3.6f);
            HideCard();
            Layout(-40, false, 150);
            SetBadge("③ UI 1차 폴리싱 · 타이틀");
            SetLower("타이틀 화면", "로고 → 언어 선택 → 타이틀 → 월드맵 → 스테이지 흐름 구현");
            Flash();
            yield return Rec(3.2f, still: "title");

            GameSystems.Instance.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.Continue));
            yield return WaitIdle();
            yield return WaitUnscaled(0.6f);
            Layout(-40, true, -118);
            SetBadge("③ UI 1차 폴리싱 · 월드맵");
            SetLower("월드맵", "5개 월드 · 일반 스테이지 20 + 보스 스테이지 5 = 총 25개");
            Flash();
            yield return Rec(4.6f, still: "worldmap");

            // ---------------------------------------------------- 2. 20 stages (with monster AI)
            ShowCard("① 전체 스테이지 제작 · 25개", "② 몬스터 AI 패턴",
                "초원 · 사막 · 설원 · 산호 해안 · 용암 요새 — 월드마다 일반 4 + 보스 1");
            yield return Rec(2.0f);
            HideCard();
            int shot = 0;
            foreach (string w in Worlds)
            {
                for (int s = 1; s <= 4; s++)
                {
                    string id = $"{w}_S{s}";
                    if (Quick && id != "W1_S1" && id != "W3_S2" && id != "W5_S3") continue;
                    yield return LoadStage(id);
                    yield return StageShot(id, ++shot);
                }
            }

            // ---------------------------------------------------- 3. bosses
            ShowCard("② 보스 몬스터 5종", "공격 패턴 → 빈틈(기절) → 머리 밟기",
                "돌진 · 내려찍기 충격파 · 낙하물 · 용암 분출 — 퍼펙트 회피 시 시간 정지");
            PlayMusic(bgmBoss);
            yield return Rec(2.0f);
            HideCard();
            var signature = new (string id, BossAttackKind kind, string label)[]
            {
                ("W1_B", BossAttackKind.Charge, "돌진 → 퍼펙트 회피(시간 정지)"),
                ("W2_B", BossAttackKind.Slam, "내려찍기 · 모래 충격파"),
                ("W3_B", BossAttackKind.Rain, "고드름 낙하"),
                ("W4_B", BossAttackKind.Charge, "껍데기 돌진"),
                ("W5_B", BossAttackKind.Eruption, "기 모으기 → 땅 찍기 → 용암 순차 분출 · 분출이 끝난 칸으로 걸어 들어가 회피"),
            };
            foreach (var b in signature)
            {
                if (Quick && b.id != "W5_B" && b.id != "W1_B") continue;
                yield return LoadStage(b.id);
                yield return BossShot(b.id, b.kind, b.label);
            }

            // ---------------------------------------------------- 4. UI polish: pause + clear/result
            PlayMusic(bgmMain);
            yield return LoadStage("W1_S2");
            yield return PauseShot();
            yield return LoadStage("W1_S1");
            yield return ClearShot();

            // ---------------------------------------------------- 5. document + end card
            ShowCard("④ 프로젝트 소개 문서 (AI 작성 초안)", "Leaf_and_Leap_프로젝트_소개.docx / .pdf",
                "게임 개요 · 핵심 조작 · 월드/스테이지 구성 · 몬스터·보스 패턴 · UI 흐름 · 제작 파이프라인(Unity 6.3 URP · Blender) · 다음 단계");
            yield return Rec(4.0f);
            ShowCard("Leaf & Leap", "1차 마일스톤 — 스테이지 25 · 보스 5 · 몬스터 AI · UI 폴리싱 · 소개 문서",
                "Unity 6.3 LTS · URP · 플레이어 캐릭터: 잎새 탐험가");
            yield return Rec(3.0f);

            EndEncoder();
            Finish();
        }


        // ------------------------------------------------------------------ shots

        private IEnumerator LoadStage(string stageId)
        {
            Pause(true);
            yield return WaitIdle();
            GameSystems.Instance.Loader.LoadWithLoading(new LoadRequest(SceneId.InGame, stageId, LoadReason.StageEnter));
            yield return null;
            yield return WaitIdle();
            float t = 0f;
            while (StageState() != "Playing" && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            if (StageState() != "Playing") Log($"{stageId}: stage never reached Playing ({StageState()})");
        }

        private IEnumerator StageShot(string stageId, int index)
        {
            var player = FindAnyObjectByType<PlayerMotor>();
            var root = FindAnyObjectByType<StageRoot>();
            if (player == null || root == null) { Log($"{stageId}: no player/stage"); yield break; }

            // Pick a monster around the middle of the stage to show its AI, else the spawn.
            Monster target = null;
            float bestScore = float.MaxValue;
            float midX = root.HasLevelEdges ? (root.LevelMinX + root.LevelMaxX) * 0.5f : root.SpawnPosition.x + 60f;
            foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                if (m.IsDefeated) continue;
                float score = Mathf.Abs(m.transform.position.x - midX) * 0.4f - (m.MonsterId >= 2000 ? 30f : 0f)
                              + Hash(stageId, m.name) * 25f;
                if (score < bestScore) { bestScore = score; target = m; }
            }

            Vector3 start = root.SpawnPosition;
            if (target != null && TryFindGround(target.transform.position.x - 6.5f, target.transform.position.y, out var g))
            {
                start = g;
            }
            else
            {
                // No monsters (W1 teaches movement only): start from the checkpoint nearest the middle of the stage.
                float best = float.MaxValue;
                foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                {
                    float d = Mathf.Abs(cp.transform.position.x - midX);
                    if (d < best) { best = d; start = cp.RespawnPosition; }
                }
            }
            Log($"{stageId}: start {start} target {(target != null ? target.MonsterId.ToString() : "-")}");
            player.TeleportTo(start + Vector3.up * 0.1f);
            SnapCamera();
            yield return Settle(4);

            string monsterName = target != null ? Loc($"monster.{target.MonsterId}.name") : null;
            LayoutGameplay();
            SetBadge($"① 스테이지 {index}/20 · {WorldName(stageId)}");
            SetLower(StageTitle(stageId), monsterName != null ? $"몬스터 AI · {monsterName}" : "플랫폼 · 장애물 · 수집품");
            Flash();
            DemoMode.MoveX = 0.75f;
            DemoMode.RunHeld = false;
            yield return Live(2.7f, player, root, target, still: $"stage_{stageId}");
            DemoMode.MoveX = 0f;
            DemoMode.ReleaseJump();
        }

        private IEnumerator BossShot(string stageId, BossAttackKind kind, string label)
        {
            var player = FindAnyObjectByType<PlayerMotor>();
            var root = FindAnyObjectByType<StageRoot>();
            var boss = FindAnyObjectByType<BossController>();
            if (player == null || boss == null) { Log($"{stageId}: no boss"); yield break; }
            // Stand a third of the way into the arena so the camera frames the player and the boss together.
            float px = Mathf.Lerp(boss.ArenaMinX, boss.ArenaMaxX, 0.3f);
            if (!TryFindGround(px, boss.transform.position.y + 2f, out var p))
                p = new Vector3(px, boss.transform.position.y + 0.5f, 0f);
            player.TeleportTo(p + Vector3.up * 0.1f);
            SnapCamera();

            // Intro (roar) runs unrecorded; the shot starts when the boss begins walking.
            float t = 0f;
            while (boss.Current != BossController.State.Walk && t < 12f) { t += Time.unscaledDeltaTime; yield return null; }
            Log($"{stageId}: boss {boss.Data.bossId} engaged={boss.IsEngaged} state={boss.Current} after {t:F1}s, player x {player.transform.position.x:F1}, boss x {boss.transform.position.x:F1}, arena {boss.ArenaMinX:F1}..{boss.ArenaMaxX:F1}");

            string bossName = Loc($"boss.{boss.Data.bossId}.name");
            LayoutGameplay();
            SetBadge($"② 보스 · {WorldName(stageId)}");
            SetLower(bossName, label);
            Flash();
            Pause(false);
            recording = true;
            yield return Hold(0.4f);
            typeof(BossController).GetMethod("BeginAttack", Any)?.Invoke(boss, new object[] { kind });

            bool dodged = false, stillTaken = false, stomped = false;
            eruptStep = 0; eruptInner = null;
            int hits = 0;
            float shotClock = 0f;
            System.Action onHit = () => { hits++; if (Quick && hits % 3 == 1) Log($"  hit #{hits} at t={shotClock:F2} x={player.transform.position.x:F2} cells={DescribeCells()}"); };
            if (root != null) root.PlayerHit += onHit;
            var states = new List<string>();
            t = 0f;
            int hpBefore = boss.Hp;
            while (t < 9f)
            {
                t += 1f / Fps;
                shotClock = t;
                string s = boss.Current.ToString();
                if (Quick && Mathf.Repeat(t, 0.5f) < 1f / Fps) Log($"  t={t:F1} {s} ts={Time.timeScale} stop={TimeStop.IsActive} udt={Time.unscaledDeltaTime:F3} dt={Time.deltaTime:F3} bx={boss.transform.position.x:F1} px={player.transform.position.x:F1}");
                if (states.Count == 0 || states[states.Count - 1] != s) states.Add(s);
                if (!stillTaken && t > (kind == BossAttackKind.Eruption ? 5.7f : 1.2f)) { stillTaken = true; SaveStill($"boss_{stageId}"); }
                float dx = boss.transform.position.x - player.transform.position.x;
                if (kind == BossAttackKind.Charge && !dodged && boss.Current == BossController.State.Charge && Mathf.Abs(dx) < 4.2f)
                {
                    dodged = true;
                    DemoMode.MoveX = Mathf.Sign(dx);
                    player.RequestDodge();
                    SetLower(bossName, "퍼펙트 회피 → 시간 정지 (흑백 연출)");
                }
                if (kind == BossAttackKind.Eruption && boss.Current == BossController.State.Eruption) EruptionFootwork(player, boss);
                if (boss.Current == BossController.State.Stunned)
                {
                    DemoMode.MoveX = 0f;
                    yield return Hold(0.6f);
                    // A stomp does not land while the "Now!" cut-in or the time stop is still running.
                    for (float c = 0f; c < 5f && (NowCutIn.IsPlaying || TimeStop.IsActive); c += 1f / Fps) yield return null;
                    yield return Hold(0.25f);
                    SetLower(bossName, "빈틈이 생긴 보스의 머리를 밟아 공격");
                    var bp = boss.transform.position;
                    player.TeleportTo(new Vector3(bp.x - 0.4f, boss.TopY + 2.2f, 0f));
                    float w = 0f;
                    while (boss.Hp == hpBefore && w < 2f) { w += 1f / Fps; yield return null; }
                    stomped = boss.Hp < hpBefore;
                    yield return Hold(1.3f);
                    break;
                }
                GuardFall(player, root);
                yield return null;
            }
            if (root != null) root.PlayerHit -= onHit;
            Log($"{stageId}: states {string.Join(">", states)} dodge={dodged} stomp={stomped} hits={hits} hp {hpBefore}->{boss.Hp} shot {t:F1}s");
            DemoMode.MoveX = 0f;
            recording = false;
        }

        private IEnumerator PauseShot()
        {
            var ctrl = FindAnyObjectByType<InGameController>();
            var player = FindAnyObjectByType<PlayerMotor>();
            var root = FindAnyObjectByType<StageRoot>();
            LayoutGameplay();
            SetBadge("③ UI 1차 폴리싱 · HUD / 일시정지");
            SetLower("HUD · 일시정지 메뉴", "목숨 · 코인 · 수집품 · 시간 표시, 계속/재시작/설정/나가기");
            Flash();
            DemoMode.MoveX = 0.75f;
            yield return Live(1.6f, player, root, null, still: "hud");
            DemoMode.MoveX = 0f;
            ctrl.GetType().GetMethod("Pause", Any)?.Invoke(ctrl, null);
            yield return Rec(2.4f, still: "pause");
            ctrl.GetType().GetMethod("Resume", Any)?.Invoke(ctrl, null);
        }

        private IEnumerator ClearShot()
        {
            var player = FindAnyObjectByType<PlayerMotor>();
            var root = FindAnyObjectByType<StageRoot>();
            var goal = FindAnyObjectByType<GoalTrigger>();
            if (goal != null && TryFindGround(goal.transform.position.x - 5f, goal.transform.position.y + 1f, out var p))
                player.TeleportTo(p + Vector3.up * 0.1f);
            SnapCamera();
            yield return Settle(4);
            LayoutGameplay();
            SetBadge("③ UI 1차 폴리싱 · 클리어 연출 / 결과");
            SetLower("스테이지 클리어", "클리어 연출 → 결과 화면(시간 · 코인 · 수집품 · 신기록)");
            Flash();
            DemoMode.MoveX = 0.8f;
            Pause(false);
            recording = true;
            float t = 0f;

            while (StageState() != "Result" && t < 9f)
            {
                t += 1f / Fps;
                if (StageState() != "Playing") DemoMode.MoveX = 0f;
                yield return null;
            }
            SaveStill("clear");
            HideLower();
            yield return Hold(2.0f);
            SaveStill("result");
            yield return Hold(0.6f);
            recording = false;
        }

        /// <summary>Records a gameplay shot with scripted run + jump, stomping toward the target monster.</summary>
        private IEnumerator Live(float seconds, PlayerMotor player, StageRoot root, Monster target, string still)
        {
            Pause(false);
            recording = true;
            float t = 0f, jumpRelease = -1f, nextHop = 1.1f;
            bool stillTaken = false;
            while (t < seconds)
            {
                t += 1f / Fps;
                if (!stillTaken && t >= seconds * 0.55f && still != null) { stillTaken = true; SaveStill(still); }
                if (player != null)
                {
                    bool wantJump = false;
                    if (target != null && !target.IsDefeated)
                    {
                        float dx = target.transform.position.x - player.transform.position.x;
                        if (dx > 0.5f && dx < 2.8f && player.IsGrounded) wantJump = true;
                    }
                    else if (t >= nextHop && player.IsGrounded) { wantJump = true; nextHop = t + 1.2f; }
                    if (player.IsGrounded && BlockedAhead(player)) wantJump = true;
                    if (wantJump && jumpRelease < 0f) { DemoMode.PressJump(); jumpRelease = t + 0.32f; }
                    if (jumpRelease >= 0f && t >= jumpRelease) { DemoMode.ReleaseJump(); jumpRelease = -1f; }
                    GuardFall(player, root);
                }
                yield return null;
            }
            DemoMode.ReleaseJump();
            recording = false;
        }

        private Vector3 lastSafe;
        private void GuardFall(PlayerMotor player, StageRoot root)
        {
            if (player == null || root == null) return;
            if (player.IsGrounded) lastSafe = player.transform.position;
            if (player.transform.position.y < root.KillY + 3f && lastSafe != Vector3.zero)
            {
                player.TeleportTo(lastSafe + Vector3.up * 0.5f);
                SnapCamera();
            }
        }

        // ------------------------------------------------------------------ eruption footwork

        private int eruptStep;          // 0 = plan, 1 = go to the gap, 2 = wait for the inner cell, 3 = walk in, 4 = done
        private BossEruption eruptInner;
        private float eruptGapX;

        /// <summary>The eruption gimmick as designed: wait on the boundary between two cells and, as soon as the inner cell
        /// (closer to the boss) has erupted, walk toward the boss through cells that already erupted.</summary>
        private void EruptionFootwork(PlayerMotor player, BossController boss)
        {
            float x = player.transform.position.x;
            float bx = boss.transform.position.x;
            float dir = Mathf.Sign(bx - x);
            switch (eruptStep)
            {
                case 0:
                {
                    var cells = new List<BossEruption>(FindObjectsByType<BossEruption>(FindObjectsSortMode.None));
                    cells.RemoveAll(c => Mathf.Sign(c.transform.position.x - bx) != -dir); // the side the player is on
                    if (cells.Count < 2) { DemoMode.MoveX = 0f; return; }
                    cells.Sort((a, b) => Mathf.Abs(a.transform.position.x - bx).CompareTo(Mathf.Abs(b.transform.position.x - bx)));
                    int k = 0;
                    while (k < cells.Count - 2 && Mathf.Abs(cells[k + 1].transform.position.x - bx) < Mathf.Abs(x - bx)) k++;
                    eruptInner = cells[k];
                    eruptGapX = (cells[k].transform.position.x + cells[k + 1].transform.position.x) * 0.5f;
                    eruptStep = 1;
                    Log($"  eruption plan: gap x {eruptGapX:F1} (inner cell {k} at {cells[k].transform.position.x:F1})");
                    break;
                }
                case 1:
                {
                    // Wait just outside the inner cell (the player is 1 m wide, wider than the 0.4 m gap between cells).
                    float target = eruptGapX - dir * 0.45f;
                    if (Mathf.Abs(target - x) > 0.15f) DemoMode.MoveX = Mathf.Sign(target - x) * 0.3f;
                    else { DemoMode.MoveX = 0f; eruptStep = 2; }
                    break;
                }
                case 2:
                {
                    DemoMode.MoveX = 0f;
                    var t = typeof(BossEruption);
                    int phase = eruptInner == null ? 4 : (int)t.GetField("phase", Any).GetValue(eruptInner);
                    float remaining = 99f;
                    if (eruptInner != null && (phase == 2 || phase == 3))
                    {
                        float timer = (float)t.GetField("timer", Any).GetValue(eruptInner);
                        float active = (float)t.GetField("active", Any).GetValue(eruptInner);
                        float rise = (float)t.GetField("riseSeconds", Any).GetValue(eruptInner);
                        remaining = phase == 2 ? rise - timer + active : active - timer;
                    }
                    // Leave so the body reaches the inner cell's edge just as its column drops (1 m wide body, 0.4 m gap).
                    float toEdge = Mathf.Max(0f, (eruptGapX - dir * 0.3f - x) * dir);
                    float lead = 0.08f + toEdge / player.RunSpeed;
                    if (phase >= 4 || remaining <= lead) { eruptStep = 3; Log($"  eruption: step in from x {x:F2} (lead {lead:F2}s)"); }
                    break;
                }
                case 3:
                    // Burst across the boundary before the outer cell rises, then walk on through cells that already erupted.
                    DemoMode.MoveX = dir;
                    DemoMode.RunHeld = true;
                    if ((x - eruptGapX) * dir > 0.8f) { DemoMode.RunHeld = false; eruptStep = 5; }
                    break;
                case 5:
                    if (Mathf.Abs(bx - x) < boss.Data.bodyWidth * 0.5f + 1.6f) { DemoMode.MoveX = 0f; eruptStep = 4; }
                    else DemoMode.MoveX = dir * 0.6f;
                    break;
                default:
                    DemoMode.MoveX = 0f;
                    break;
            }
        }

        // ------------------------------------------------------------------ helpers
        private static string DescribeCells()
        {
            var sb = new System.Text.StringBuilder();
            var t = typeof(BossEruption);
            foreach (var e in FindObjectsByType<BossEruption>(FindObjectsSortMode.None))
            {
                var box = t.GetField("hitBox", Any).GetValue(e) as BoxCollider;
                if (box != null && box.enabled) sb.Append($"[{e.transform.position.x:F1} p{(int)t.GetField("phase", Any).GetValue(e)}]");
            }
            return sb.ToString();
        }


        private static bool BlockedAhead(PlayerMotor player)
        {
            var o = player.transform.position + Vector3.up * 0.6f;
            return Physics.Raycast(o, Vector3.right, 1.1f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static bool TryFindGround(float x, float y, out Vector3 point)
        {
            for (int i = 0; i < 8; i++)
            {
                float px = x - i * 1.0f;
                if (Physics.Raycast(new Vector3(px, y + 6f, 0f), Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
                {
                    point = new Vector3(px, hit.point.y + 0.05f, 0f);
                    return true;
                }
            }
            point = default;
            return false;
        }

        private static float Hash(string a, string b) => ((a + b).GetHashCode() & 0xffff) / 65535f;

        private static void SnapCamera() => FindAnyObjectByType<CameraFollow>()?.SnapToTarget();

        private static string StageState()
        {
            var ctrl = FindAnyObjectByType<InGameController>();
            if (ctrl == null) return "";
            var session = typeof(InGameController).GetField("session", Any)?.GetValue(ctrl);
            return session?.GetType().GetProperty("State")?.GetValue(session)?.ToString() ?? "";
        }

        private static string Loc(string key)
        {
            string v = GameSystems.Instance.Localization.Get(key);
            return string.IsNullOrEmpty(v) || v == key ? null : v;
        }

        private static string StageTitle(string id)
        {
            if (GameSystems.Instance.StageDb.TryGetStage(id, out var data))
                return Loc(data.DisplayNameKey) ?? id;
            return id;
        }

        private static string WorldName(string id)
        {
            switch (id.Substring(0, 2))
            {
                case "W1": return "W1 초원";
                case "W2": return "W2 사막";
                case "W3": return "W3 설원";
                case "W4": return "W4 산호 해안";
                default: return "W5 용암 요새";
            }
        }

        private IEnumerator WaitIdle()
        {
            while (GameSystems.Instance.Loader.IsBusy) yield return null;
        }

        private static IEnumerator WaitUnscaled(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator Settle(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>Records for the given (video) seconds.</summary>
        private IEnumerator Rec(float seconds, string still = null)
        {
            Pause(false);
            recording = true;
            int frames = Mathf.RoundToInt(seconds * Fps);
            for (int i = 0; i < frames; i++)
            {
                if (still != null && i == frames / 2) SaveStill(still);
                yield return null;
            }
            recording = false;
        }

        /// <summary>Keeps the current recording state for the given video seconds.</summary>
        private static IEnumerator Hold(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds * Fps);
            for (int i = 0; i < frames; i++) yield return null;
        }

        private void Log(string s)
        {
            log.Add(s);
            Debug.Log("[Showcase] " + s);
        }

        // ------------------------------------------------------------------ overlay

        private void BuildOverlay()
        {
            font = FontProvider.Get("ko") ?? FontProvider.GetBuiltin();
            var canvasGo = new GameObject("ShowcaseOverlay", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            canvas.enabled = !Trailer; // trailer footage is clean: no captions, no flash
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            scaler.matchWidthOrHeight = 1f;
            var rootRt = (RectTransform)canvasGo.transform;

            badge = Panel(rootRt, "Badge", new Vector2(0, 1), new Vector2(48, -40), new Vector2(760, 64), new Color(0.06f, 0.07f, 0.1f, 0.72f));
            Accent(badge, new Color(1f, 0.74f, 0.25f));
            badgeText = Label(badge, 30, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(28, 0), Color.white);

            lower = Panel(rootRt, "Lower", new Vector2(0, 0), new Vector2(48, 150), new Vector2(980, 132), new Color(0.06f, 0.07f, 0.1f, 0.72f));
            Accent(lower, new Color(0.45f, 0.85f, 1f));
            lowerTitle = Label(lower, 44, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(28, -14), Color.white);
            lowerSub = Label(lower, 28, FontStyle.Normal, TextAnchor.LowerLeft, new Vector2(28, 16), new Color(0.85f, 0.9f, 0.95f));

            card = Panel(rootRt, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height), new Color(0.04f, 0.05f, 0.08f, 0.88f));
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one; card.sizeDelta = Vector2.zero; card.anchoredPosition = Vector2.zero;
            cardTitle = Label(card, 92, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, 110), Color.white);
            cardSub = Label(card, 40, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0, 0), new Color(1f, 0.8f, 0.4f));
            cardBody = Label(card, 30, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0, -110), new Color(0.82f, 0.86f, 0.92f));

            var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(rootRt, false);
            var frt = (RectTransform)flashGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.sizeDelta = Vector2.zero;
            flash = flashGo.GetComponent<Image>();
            flash.color = new Color(0, 0, 0, 0);
            flash.raycastTarget = false;

            badge.gameObject.SetActive(false);
            lower.gameObject.SetActive(false);
            card.gameObject.SetActive(false);
        }

        private static RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static void Accent(RectTransform parent, Color color)
        {
            var go = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(8, 0);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private Text Label(RectTransform parent, int size, FontStyle style, TextAnchor anchor, Vector2 offset, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Mathf.Max(0, offset.x), 0);
            rt.offsetMax = new Vector2(-24, 0);
            rt.anchoredPosition += new Vector2(0, offset.y);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            return t;
        }

        /// <summary>Places the badge (top-left) and the caption panel (top-left under the badge, or bottom-left).</summary>
        private void Layout(float badgeY, bool lowerTop, float lowerY)
        {
            badge.anchoredPosition = new Vector2(48, badgeY);
            var a = lowerTop ? new Vector2(0, 1) : new Vector2(0, 0);
            lower.anchorMin = lower.anchorMax = a;
            lower.pivot = a;
            lower.anchoredPosition = new Vector2(48, lowerY);
        }
        private void LayoutGameplay() => Layout(-128, true, -206);

        private void SetBadge(string s) { badge.gameObject.SetActive(true); badgeText.text = s; FitWidth(badge, badgeText, 760); }
        private void SetLower(string title, string sub)
        {
            lower.gameObject.SetActive(true);
            lowerTitle.text = title;
            lowerSub.text = sub;
            float w = Mathf.Max(lowerTitle.preferredWidth, lowerSub.preferredWidth) + 80f;
            lower.sizeDelta = new Vector2(Mathf.Clamp(w, 520f, 1500f), lower.sizeDelta.y);
        }
        private void HideLower() => lower.gameObject.SetActive(false);
        private static void FitWidth(RectTransform panel, Text text, float min) =>
            panel.sizeDelta = new Vector2(Mathf.Max(min * 0.5f, text.preferredWidth + 70f), panel.sizeDelta.y);

        private void ShowCard(string title, string sub, string body)
        {
            card.gameObject.SetActive(true);
            badge.gameObject.SetActive(false);
            lower.gameObject.SetActive(false);
            cardTitle.text = title; cardSub.text = sub; cardBody.text = body;
            Flash();
        }
        private void HideCard() => card.gameObject.SetActive(false);
        private void Flash() => flashAlpha = 1f;

        private void Update()
        {
            if (flash == null) return;
            flash.color = new Color(0, 0, 0, flashAlpha);
            if (recording) flashAlpha = Mathf.Max(0f, flashAlpha - 1f / 7f);
            // keep the game's own BGM silent; the reel mixes one continuous music track
            var audio = GameSystems.Exists ? GameSystems.Instance.Audio : null;
            if (audio != null)
            {
                var src = typeof(AudioService).GetField("bgmSource", Any)?.GetValue(audio) as AudioSource;
                if (src != null)
                    foreach (var s in src.GetComponentsInChildren<AudioSource>(true)) s.mute = true; // includes the crossfade source
            }
        }

        // ------------------------------------------------------------------ music

        private void BuildMusic()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = 0.55f;
            music.ignoreListenerPause = true;
            music.spatialBlend = 0f;
        }

        private void PlayMusic(AudioClip clip)
        {
            if (clip == null || music.clip == clip) return;
            music.clip = clip;
            music.Play();
            if (!recording) music.Pause();
        }

        private void Pause(bool paused)
        {
            if (paused) { recording = false; if (music.isPlaying) music.Pause(); }
            else if (music.clip != null && !music.isPlaying) music.UnPause();
        }

        // ------------------------------------------------------------------ encoder

        private void BeginEncoder()
        {
            var mode = AudioSettings.GetConfiguration().speakerMode;
            channels = mode == AudioSpeakerMode.Mono ? 1 : mode == AudioSpeakerMode.Quad ? 4 : mode == AudioSpeakerMode.Surround ? 5
                : mode == AudioSpeakerMode.Mode5point1 ? 6 : mode == AudioSpeakerMode.Mode7point1 ? 8 : 2;
            var video = new VideoTrackAttributes
            {
                frameRate = new MediaRational(Fps),
                width = Width,
                height = Height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High,
            };
            var audio = new AudioTrackAttributes
            {
                sampleRate = new MediaRational(AudioSettings.outputSampleRate),
                channelCount = (ushort)channels,
                language = "ko",
            };
            string path = Path.Combine(OutputDir, Quick ? "showcase_quicktest.mp4" : "LeafAndLeap_Showcase.mp4");
            if (Trailer)
            {
                Directory.CreateDirectory(TrailerDir);
                path = Path.Combine(TrailerDir, Story ? "story" + StorySuffix + "_raw.mp4" : Quick ? "trailer_quicktest.mp4" : "trailer_raw.mp4");
            }
            if (File.Exists(path)) File.Delete(path);
            if (Trailer)
            {
                // Trailer footage is a master for editing: the default "High" bitrate smears busy scrolling scenes.
                var h264 = new H264EncoderAttributes { gopSize = 30, numConsecutiveBFrames = 0, profile = VideoEncodingProfile.H264High };
                var master = new VideoTrackEncoderAttributes(h264)
                {
                    frameRate = new MediaRational(Fps), width = Width, height = Height, includeAlpha = false, targetBitRate = 45000000,
                };
                encoder = new MediaEncoder(path, master, audio);
            }
            else
                encoder = new MediaEncoder(path, video, audio);
            screenRt = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            outRt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            frameTex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            AudioRenderer.Start();
            StartCoroutine(CaptureLoop());
            Log("encoder -> " + path);
        }

        private IEnumerator CaptureLoop()
        {
            var eof = new WaitForEndOfFrame();
            while (encoder != null)
            {
                yield return eof;
                if (encoder == null) break;
                int n = AudioRenderer.GetSampleCountForCaptureFrame();
                int len = n * channels;
                if (!audioBuf.IsCreated || audioBuf.Length != len)
                {
                    if (audioBuf.IsCreated) audioBuf.Dispose();
                    audioBuf = new NativeArray<float>(Mathf.Max(len, 1), Allocator.Persistent);
                }
                if (len > 0) AudioRenderer.Render(audioBuf);
                if (!recording) continue;

                GrabFrame();
                encoder.AddFrame(frameTex);
                if (len > 0) encoder.AddSamples(audioBuf);
                framesRecorded++;
            }
        }

        private void GrabFrame()
        {
            if (screenRt.width != Screen.width || screenRt.height != Screen.height)
            {
                screenRt.Release();
                screenRt = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(screenRt);
            if (SystemInfo.graphicsUVStartsAtTop)
                Graphics.Blit(screenRt, outRt, new Vector2(1f, -1f), new Vector2(0f, 1f));
            else
                Graphics.Blit(screenRt, outRt);
            var prev = RenderTexture.active;
            RenderTexture.active = outRt;
            frameTex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            frameTex.Apply(false);
            RenderTexture.active = prev;
        }

        private void SaveStill(string name) { if (!Trailer) StartCoroutine(StillRoutine(name)); }

        private IEnumerator StillRoutine(string name)
        {
            yield return new WaitForEndOfFrame();
            if (frameTex == null) yield break;
            if (!recording) GrabFrame();
            // frameTex holds the last captured frame (the capture loop runs before this at end of frame)
            File.WriteAllBytes(Path.Combine(OutputDir, Quick ? "stills_quick" : "stills", $"{++stillIndex:00}_{name}.png"), frameTex.EncodeToPNG());
        }

        private void EndEncoder()
        {
            var e = encoder;
            encoder = null;
            e?.Dispose();
            AudioRenderer.Stop();
            if (audioBuf.IsCreated) audioBuf.Dispose();
            Log($"encoded {framesRecorded} frames ({framesRecorded / (float)Fps:F1} s)");
        }

        private void Finish()
        {
            QualitySettings.vSyncCount = savedVSync;
            Application.targetFrameRate = savedTargetFps;
            File.WriteAllLines(Trailer ? Path.Combine(TrailerDir, Story ? "story" + StorySuffix + "_log.txt" : Quick ? "trailer_log_quick.txt" : "trailer_log.txt") : Path.Combine(OutputDir, "showcase_log.txt"), log);
            Time.captureFramerate = 0;
            DemoMode.Reset();
            Finished = true;
            EditorApplication.isPlaying = false;
        }

        private void OnDestroy()
        {
            if (encoder != null) EndEncoder();
            if (Instance == this) Instance = null;
            Time.captureFramerate = 0;
        }
    }
}
#endif
