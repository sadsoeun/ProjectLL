#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MyGame.Dev
{
    /// <summary>
    /// Story-order trailer footage (Tools/MyGame/Demo/Record Story Trailer Footage): title -> first visit (world map and
    /// stage tutorial, on a fresh in-memory save) -> voice slot unlock + voice studio -> stages and bosses -> the last
    /// boss falling and the clear. The opening and ending films are cut in from their source files in the edit.
    /// Writes raw/story_raw.mp4 with story_marks.json / story_takes.json. Nothing is written to the save file.
    /// </summary>
    public sealed partial class ShowcaseDirector
    {
        /// <summary>Shoot the story-order takes instead of the voice trailer takes.</summary>
        public static bool Story;
        /// <summary>"" = the whole shoot; "tutorial" or "boss" = a pick-up shoot of that part only (own output files).</summary>
        public static string StoryPart = "";
        private static string StorySuffix => string.IsNullOrEmpty(StoryPart) ? "" : "_" + StoryPart;

        // (stage, start x; NaN = next to a monster near the middle of the stage)
        private static readonly (string id, float x)[] StoryRuns =
        {
            ("W1_S3", float.NaN), ("W2_S2", 44f), ("W3_S2", float.NaN), ("W4_S2", 156f), ("W5_S2", 67f),
        };

        private IEnumerator StorySequence()
        {
            var systems = GameSystems.Instance;
            systems.Settings.Data.masterVolume = 1f;
            systems.Settings.Data.sfxVolume = 0.8f;
            systems.Settings.NotifyChanged();
            var service = systems.Save;
            var userSave = service.Current;

            bool all = StoryPart == "", doTutorial = all || StoryPart == "tutorial", doBoss = all || StoryPart == "boss";

            // ---------------------------------------------------- title
            if (all)
            {
                Mark("title");
                yield return Rec(6f);
            }

            // ---------------------------------------------------- first visit: world map lesson, then the stage tutorial
            if (doTutorial)
            {
                service.CreateTemporary(systems.StageDb.StartNodeId);
                DemoMode.ShowTutorials = true;
                systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.Continue));
                yield return null;
                yield return WaitIdle();
                yield return MapLessonTake();
                if (!systems.Loader.IsBusy && StageState() == "")
                {
                    Log("map lesson: the stage was not entered from the map; loading it directly");
                    systems.Loader.LoadWithLoading(new LoadRequest(SceneId.InGame, "W1_S1", LoadReason.StageEnter));
                }
                yield return null;
                yield return WaitIdle();
                for (float w = 0f; StageState() != "Playing" && w < 20f; w += Time.unscaledDeltaTime) yield return null;
                yield return TutorialTake();
                DemoMode.ShowTutorials = false;
            }

            // ---------------------------------------------------- voice slots open on the world map, then the studio
            if (userSave != null) SetSave(service, userSave);
            else if (service.Current == null) service.CreateTemporary(systems.StageDb.StartNodeId);
            var save = service.Current;
            foreach (string w in Worlds)
                foreach (string s in new[] { "S1", "S2", "S3", "S4", "B" })
                {
                    string id = w + "_" + s;
                    if (!systems.StageDb.TryGetStage(id, out var data)) continue;
                    var rec = save.stages.Find(r => r != null && r.stageId == id);
                    if (rec == null) { rec = new StageRecord { stageId = id }; save.stages.Add(rec); }
                    var owned = new bool[data.CollectibleCount];
                    for (int i = 0; i < owned.Length; i++) owned[i] = true;
                    rec.collectibles = owned;
                }
            save.Tutorial.SkipAll();
            var voice = save.Voice;
            for (int i = 0; i < voice.announced.Length; i++) voice.announced[i] = !all || i >= 2; // the first two slots open on camera
            SetVoices(true);
            Log($"story: leaves {save.TotalLeaves()}, lives {save.lives}");

            if (all)
            {
                Pause(true);
                systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.Continue));
                yield return null;
                yield return WaitIdle();
                Mark("voice_unlock");
                Pause(false);
                recording = true;
                for (int n = 0; n < 2; n++)
                {
                    VoiceSlotUnlockNotice notice = null;
                    for (float w = 0f; notice == null && w < 4f; w += 1f / Fps)
                    {
                        notice = FindAnyObjectByType<VoiceSlotUnlockNotice>();
                        if (notice == null) yield return null;
                    }
                    if (notice == null) { Log("voice unlock: no notice appeared"); break; }
                    yield return Hold(3.8f);
                    notice.Dismiss();
                    // The next notice reuses the same object in the same frame, so wait on the slot's flag instead.
                    for (float w = 0f; !voice.announced[n] && w < 3f; w += 1f / Fps) yield return null;
                    yield return Hold(0.35f);
                }
                yield return Hold(1.2f);
                recording = false;
                for (int i = 0; i < voice.announced.Length; i++) voice.announced[i] = true;
                yield return WaitUnscaled(0.5f);
                yield return StudioShots();

                // ------------------------------------------------ one stage per world
                foreach (var r in StoryRuns)
                {
                    yield return LoadStage(r.id);
                    yield return RunTake("run_" + r.id, r.x, 6f, RunMode.Stomp, hopEvery: 0f, fromSpawn: false);
                    yield return RunTake("run_" + r.id + "_b", float.NaN, 6f, RunMode.Stomp, hopEvery: 1.4f, fromSpawn: false, checkpoint: true, run: true);
                    yield return LoadStage(r.id);
                    yield return RunTake("run_" + r.id + "_c", float.NaN, 6f, RunMode.Stomp, hopEvery: 1.6f, fromSpawn: true);
                }
                foreach (string id in new[] { "W2_S1", "W3_S2" })
                {
                    yield return LoadStage(id);
                    yield return RunTake("dodge_" + id, float.NaN, 4.5f, RunMode.Dodge, hopEvery: 0f, fromSpawn: false);
                }
            }

            if (doBoss)
            {
                // ------------------------------------------------ bosses of worlds 1-4, played for real: the attack is avoided,
                // the stunned boss is reached on foot and stomped. A take with a hit or without a stomp is shot again.
                save.Voice.disabled[(int)VoiceSlot.Now] = true; // the boss cut-in keeps its built-in sound
                var bosses = new (string id, BossAttackKind kind)[]
                {
                    ("W1_B", BossAttackKind.Charge), ("W2_B", BossAttackKind.Slam), ("W3_B", BossAttackKind.Rain), ("W4_B", BossAttackKind.Charge),
                };
                foreach (var b in bosses)
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        yield return LoadStage(b.id);
                        yield return BossFight("boss_" + b.id + "_" + attempt, b.kind, false);
                        if (fightClean) break;
                    }

                // ------------------------------------------------ the last boss: its signature attack, then the final blow and the clear
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    yield return LoadStage("W5_B");
                    yield return BossFight("final_a_" + attempt, BossAttackKind.Eruption, false);
                    if (!fightClean) continue;
                    var boss = FindAnyObjectByType<BossController>();
                    if (boss == null) continue;
                    // Off camera: one hit left.
                    typeof(BossController).GetField("hp", Any).SetValue(boss, 1);
                    object hud = typeof(BossController).GetField("hud", Any)?.GetValue(boss);
                    hud?.GetType().GetMethod("SetHp", Any)?.Invoke(hud, new object[] { 1 });
                    DemoMode.MoveX = 0f;
                    for (float w = 0f; boss.Current != BossController.State.Walk && w < 12f; w += Time.unscaledDeltaTime) yield return null;
                    yield return BossFight("final_b_" + attempt, BossAttackKind.Slam, true, 8.5f);
                    if (fightClean) break;
                }
            }
            WriteMarks();
        }

        // ------------------------------------------------------------------ boss fight, played for real

        private bool fightClean;

        /// <summary>
        /// One round of a boss fight the way a player plays it: the attack is avoided by its own counter (dodge through the
        /// charge, jump the shockwave, step out of the falling shadows, walk into erupted cells), then the stunned boss is
        /// reached on foot (over the staircase pillars where the fight has them) and stomped. Nothing is teleported.
        /// <paramref name="toClear"/>: the stomp is the last one; the take runs on through the defeat to the result screen.
        /// </summary>
        private IEnumerator BossFight(string name, BossAttackKind kind, bool toClear, float startGap = 0f)
        {
            fightClean = false;
            var player = FindAnyObjectByType<PlayerMotor>();
            var root = FindAnyObjectByType<StageRoot>();
            var boss = FindAnyObjectByType<BossController>();
            if (player == null || boss == null || root == null) { Log($"{name}: no boss"); yield break; }
            float half = boss.Data.bodyWidth * 0.5f, floorY = boss.transform.position.y;
            if (boss.Current == BossController.State.Dormant || boss.Current == BossController.State.Intro)
            {
                // Walk-in is not part of the take: stand in the arena (off camera) and let the entrance roar pass.
                float px = Mathf.Lerp(boss.ArenaMinX, boss.ArenaMaxX, kind == BossAttackKind.Slam || kind == BossAttackKind.Rain ? 0.42f : 0.3f);
                if (!TryFindGround(px, floorY + 2f, out var p0)) p0 = new Vector3(px, floorY + 0.5f, 0f);
                player.TeleportTo(p0 + Vector3.up * 0.1f);
                SnapCamera();
            }
            float t = 0f;
            while (boss.Current != BossController.State.Walk && t < 12f) { t += Time.unscaledDeltaTime; yield return null; }
            if (startGap > 0f)
            {
                // Between two takes (off camera): start this round from a set distance, on the side with the room for it.
                float bx = boss.transform.position.x;
                float side = bx - half - startGap > boss.ArenaMinX + 1f ? -1f : 1f;
                float px = bx + side * (half + startGap);
                if (!TryFindGround(px, floorY + 2f, out var p1)) p1 = new Vector3(px, floorY + 0.5f, 0f);
                player.TeleportTo(p1 + Vector3.up * 0.1f);
                SnapCamera();
                yield return Settle(8);
            }
            // The real rules apply during the take; a hit is counted (and ends the take as unusable) instead of killing.
            int realHits = 0;
            System.Action onHit = () =>
            {
                if (root.LastHitDodgeable && (TimeStop.IsActive || NowCutIn.IsPlaying || player.IsDodging)) return;
                realHits++;
            };
            root.PlayerHit += onHit;
            lastSafe = Vector3.zero;
            eruptStep = 0; eruptInner = null;

            Mark(name);
            float takeStart = framesRecorded / (float)Fps;
            Pause(false);
            recording = true;
            yield return Hold(0.4f);
            typeof(BossController).GetMethod("BeginAttack", Any)?.Invoke(boss, new object[] { kind });
            var rainField = typeof(BossController).GetField("rainX", Any);

            bool dodged = false;
            float jumpRelease = -1f, rainGoal = float.NaN;
            var states = new System.Collections.Generic.List<string>();
            t = 0f;
            while (t < 16f && boss.Current != BossController.State.Stunned)
            {
                t += 1f / Fps;
                string s = boss.Current.ToString();
                if (states.Count == 0 || states[states.Count - 1] != s) states.Add(s);
                Vector3 p = player.transform.position;
                float dx = boss.transform.position.x - p.x;
                bool grounded = player.IsGrounded;
                switch (kind)
                {
                    case BossAttackKind.Charge:
                        DemoMode.MoveX = 0f;
                        if (!dodged && boss.Current == BossController.State.Charge && Mathf.Abs(dx) < 4.2f)
                        {
                            dodged = true;
                            DemoMode.MoveX = Mathf.Sign(dx);
                            player.RequestDodge();
                        }
                        break;
                    case BossAttackKind.Slam:
                        DemoMode.MoveX = 0f;
                        foreach (var wave in FindObjectsByType<BossHazard>(FindObjectsSortMode.None))
                        {
                            float gap = (p.x - wave.transform.position.x) * wave.Direction; // > 0: the wave is coming
                            if (wave.Direction != 0 && gap > 0f && gap < 2.0f && grounded && jumpRelease < 0f)
                            {
                                DemoMode.PressJump();
                                jumpRelease = t + 0.5f;
                            }
                        }
                        break;
                    case BossAttackKind.Rain:
                        if (boss.Current == BossController.State.Rain && float.IsNaN(rainGoal) && rainField?.GetValue(boss) is System.Collections.Generic.List<float> drops && drops.Count > 0)
                        {
                            // Step out of the shadows, toward the boss (the stun that follows is short).
                            float side = Mathf.Sign(p.x - boss.transform.position.x);
                            float near = boss.transform.position.x + side * (half + 1.2f), far = side < 0f ? boss.ArenaMinX + 1.2f : boss.ArenaMaxX - 1.2f;
                            float best = float.MinValue;
                            for (float c = Mathf.Min(near, far); c <= Mathf.Max(near, far); c += 0.2f)
                            {
                                float clear = 99f;
                                foreach (float d in drops) clear = Mathf.Min(clear, Mathf.Abs(d - c));
                                float score = Mathf.Min(clear, 1.8f) * 3f - Mathf.Abs(c - p.x) * 0.25f - Mathf.Abs(c - near) * 0.1f;
                                if (score > best) { best = score; rainGoal = c; }
                            }
                            Log($"  rain: drops {string.Join(",", drops.ConvertAll(d => d.ToString("F1")))} -> stand at {rainGoal:F1}");
                        }
                        DemoMode.MoveX = !float.IsNaN(rainGoal) && Mathf.Abs(rainGoal - p.x) > 0.12f ? Mathf.Sign(rainGoal - p.x) * (Mathf.Abs(rainGoal - p.x) > 0.6f ? 1f : 0.4f) : 0f;
                        break;
                    case BossAttackKind.Eruption:
                        if (boss.Current == BossController.State.Eruption) EruptionFootwork(player, boss);
                        else DemoMode.MoveX = 0f;
                        break;
                }
                if (jumpRelease >= 0f && t >= jumpRelease) { DemoMode.ReleaseJump(); jumpRelease = -1f; }
                GuardFall(player, root);
                yield return null;
            }
            DemoMode.MoveX = 0f;
            DemoMode.RunHeld = false;
            DemoMode.ReleaseJump();

            // ---- the opening: on foot to the boss (pillar by pillar where there are pillars), jump, stomp
            int hpBefore = boss.Hp;
            var tops = new System.Collections.Generic.Dictionary<BossPillar, float>();
            float w2 = 0f, sinceStomp = -1f;
            jumpRelease = -1f;
            string route = "";
            while (w2 < 12f)
            {
                w2 += 1f / Fps;
                if (boss.Hp < hpBefore && sinceStomp < 0f) sinceStomp = 0f;
                if (sinceStomp >= 0f)
                {
                    // Stomped: stay over the head for another bounce while the stun lasts, then the take ends.
                    sinceStomp += 1f / Fps;
                    float over = boss.transform.position.x - player.transform.position.x;
                    DemoMode.MoveX = !toClear && !player.IsGrounded && Mathf.Abs(over) > 0.25f ? Mathf.Sign(over) * 0.6f : 0f;
                    if (sinceStomp >= 1.5f) break;
                    yield return null;
                    continue;
                }
                if (boss.Current != BossController.State.Stunned) break; // the opening closed
                if (NowCutIn.IsPlaying || TimeStop.IsActive) { DemoMode.MoveX = 0f; yield return null; continue; }

                Vector3 p = player.transform.position;
                bool grounded = player.IsGrounded;
                float feet = player.FeetY;
                // Next step up: the farthest pillar that is still above the feet, else the boss.
                BossPillar step = null;
                foreach (var pillar in FindObjectsByType<BossPillar>(FindObjectsSortMode.None))
                {
                    if (pillar.IsSinking || pillar.CurrentTopY <= feet + 0.4f) continue;
                    if (step == null || Mathf.Abs(pillar.transform.position.x - boss.transform.position.x) > Mathf.Abs(step.transform.position.x - boss.transform.position.x)) step = pillar;
                }
                bool usesPillars = boss.Data.pillarsOnStun;
                float tx, tHalf, jumpDist;
                bool ready = true;
                if (step != null)
                {
                    tx = step.transform.position.x; tHalf = boss.Data.pillarWidth * 0.5f; jumpDist = 1.8f;
                    float top = step.CurrentTopY;
                    ready = tops.TryGetValue(step, out float last) && Mathf.Abs(top - last) < 0.0005f; // fully risen
                    tops[step] = top;
                }
                else
                {
                    tx = boss.transform.position.x; tHalf = half;
                    // From the floor the edge of the body must be crossed near the top of the jump.
                    jumpDist = usesPillars ? 1.8f : player.MoveSpeed * 0.34f;
                    if (usesPillars && feet - floorY < boss.Data.minStompFeetHeight - 0.3f) ready = false; // wait for the staircase
                }
                float toward = Mathf.Sign(tx - p.x), edge = Mathf.Abs(tx - p.x) - tHalf;
                string label = step != null ? "pillar" : "boss";
                if (grounded)
                {
                    if (edge > jumpDist + 0.1f) DemoMode.MoveX = toward;
                    else if (feet - floorY < 0.3f && edge < jumpDist - 0.9f) DemoMode.MoveX = -toward; // too close to clear it: step back
                    else if (!ready) DemoMode.MoveX = 0f;
                    else if (jumpRelease < 0f)
                    {
                        DemoMode.MoveX = toward;
                        DemoMode.PressJump();
                        jumpRelease = w2 + 0.5f;
                        route += $"{label}@{p.x:F1}/{feet - floorY:F1} ";
                    }
                }
                else DemoMode.MoveX = Mathf.Abs(tx - p.x) > 0.2f ? toward : 0f;
                if (jumpRelease >= 0f && w2 >= jumpRelease) { DemoMode.ReleaseJump(); jumpRelease = -1f; }
                yield return null;
            }
            DemoMode.MoveX = 0f;
            DemoMode.ReleaseJump();
            bool stomped = boss.Hp < hpBefore;
            fightClean = stomped && realHits == 0;
            if (toClear && boss.Hp <= 0)
            {
                float c = 0f;
                while (StageState() != "Result" && c < 30f) { c += 1f / Fps; yield return null; }
                yield return Hold(4f);
                fightClean &= StageState() == "Result";
            }
            recording = false;
            root.PlayerHit -= onHit;
            float length = framesRecorded / (float)Fps - takeStart;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            takes.Add("{\"name\":\"" + name + "\",\"start\":" + takeStart.ToString("F3", inv) + ",\"length\":" + length.ToString("F3", inv) + ",\"bad\":" + (fightClean ? "-1.000" : "0.000") + "}");
            Log($"{name}: states {string.Join(">", states)} dodge={dodged} stomp={stomped} realHits={realHits} hp {hpBefore}->{boss.Hp} topY {boss.TopY - floorY:F1} route [{route.Trim()}] length {length:F1}s clean={fightClean}");
        }

        private static void SetSave(SaveService service, SaveData data)
        {
            typeof(SaveService).GetProperty("Current").SetValue(service, data);
            typeof(SaveService).GetProperty("IsTemporary").SetValue(service, true); // the shoot never writes the save file
        }

        /// <summary>World map, first visit: the "enter the stage" explanation, its prompt, then Confirm enters the stage.</summary>
        private IEnumerator MapLessonTake()
        {
            var systems = GameSystems.Instance;
            Mark("map_lesson");
            Pause(false);
            recording = true;
            WorldMapTutorialDirector lesson = null;
            float t = 0f, explainFor = 0f, promptFor = 0f;
            bool wasExplaining = false;
            while (t < 16f && !systems.Loader.IsBusy)
            {
                t += 1f / Fps;
                if (lesson == null) lesson = FindAnyObjectByType<WorldMapTutorialDirector>();
                if (lesson != null && lesson.IsExplaining)
                {
                    wasExplaining = true;
                    promptFor = 0f;
                    explainFor += 1f / Fps;
                    if (explainFor >= 3.2f) { DemoMode.PressConfirm(); explainFor = 0f; }
                }
                else if (wasExplaining || t > 3f)
                {
                    explainFor = 0f;
                    promptFor += 1f / Fps;
                    if (promptFor >= 2.4f) { DemoMode.PressConfirm(); promptFor = 0f; }
                }
                yield return null;
            }
            Log($"map lesson: {t:F1}s, explained {wasExplaining}, entering {systems.Loader.IsBusy}");
            recording = false;
        }

        /// <summary>W1_S1 from the spawn with the real tutorial: explanations are read and confirmed, each practice is done.</summary>
        private IEnumerator TutorialTake()
        {
            var player = FindAnyObjectByType<PlayerMotor>();
            var root = FindAnyObjectByType<StageRoot>();
            var lesson = FindAnyObjectByType<StageTutorialDirector>();
            if (player == null || root == null) { Log("tutorial: no player/stage"); yield break; }
            if (lesson == null) Log("tutorial: no tutorial director in this stage");
            var progress = GameSystems.Instance.Save.Current.Tutorial;
            lastSafe = Vector3.zero;
            Mark("tutorial");
            Pause(false);
            recording = true;
            float t = 0f, explainFor = 0f, jumpRelease = -1f, doneFor = 0f, takeStart = framesRecorded / (float)Fps;
            float stuckFor = 0f, lastX = player.transform.position.x;
            string lastStep = null;
            bool jumpedForStep = false;
            while (t < 80f)
            {
                t += 1f / Fps;
                var phase = lesson != null ? lesson.Phase : StageTutorialFlow.State.Idle;
                string step = lesson != null ? lesson.ActiveStepId : null;
                if (step != lastStep)
                {
                    if (step != null) Log($"  tutorial step {step} ({phase}) at t={t:F1} x={player.transform.position.x:F1}");
                    lastStep = step;
                    jumpedForStep = false;
                }
                if (phase == StageTutorialFlow.State.Explaining)
                {
                    // Frozen: read the page, then Confirm.
                    DemoMode.MoveX = 0f;
                    explainFor += 1f / Fps;
                    if (explainFor >= 3.0f) { DemoMode.PressConfirm(); explainFor = 0f; }
                    yield return null;
                    continue;
                }
                explainFor = 0f;

                Vector3 p = player.transform.position;
                bool grounded = player.IsGrounded, wantJump = false, longJump = false;
                if (progress.IsSeen("w1s1_stomp"))
                {
                    // Stomped: land, stand, and the take ends a beat later.
                    DemoMode.MoveX = 0f;
                    DemoMode.RunHeld = false;
                    DemoMode.ReleaseJump();
                    jumpRelease = -1f;
                    doneFor += 1f / Fps;
                    if (doneFor >= 2.0f) break;
                    yield return null;
                    continue;
                }
                if (step == "w1s1_stomp")
                {
                    // The stomp lesson: walk up to the enemy, a short hop from a set distance, and steer onto its head
                    // (a running jump clears it and lands in the pit behind).
                    Monster prey = null;
                    foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
                    {
                        if (m.IsDefeated || Mathf.Abs(m.transform.position.y - p.y) > 3f) continue;
                        if (prey == null || Mathf.Abs(m.transform.position.x - p.x) < Mathf.Abs(prey.transform.position.x - p.x)) prey = m;
                    }
                    if (prey != null && Mathf.Abs(prey.transform.position.x - p.x) < 10f)
                    {
                        float ex = prey.transform.position.x - p.x, away = Mathf.Abs(ex), dir = Mathf.Sign(ex);
                        DemoMode.RunHeld = false;
                        if (!grounded) DemoMode.MoveX = away > 0.15f ? dir * Mathf.Clamp(away / 0.8f, 0.3f, 1f) : 0f;
                        else if (away > 2.1f) DemoMode.MoveX = dir * 0.8f;
                        else if (away < 1.1f) DemoMode.MoveX = -dir * 0.8f; // too close for a hop: make room first
                        else if (jumpRelease < 0f)
                        {
                            DemoMode.MoveX = dir * 0.6f;
                            DemoMode.PressJump();
                            jumpRelease = t + 0.22f;
                        }
                        if (jumpRelease >= 0f && t >= jumpRelease) { DemoMode.ReleaseJump(); jumpRelease = -1f; }
                        GuardFall(player, root);
                        yield return null;
                        continue;
                    }
                }
                DemoMode.MoveX = 1f;
                DemoMode.RunHeld = step == "w1s1_run" || (jumpRelease >= 0f && DemoMode.RunHeld);
                foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
                {
                    if (m.IsDefeated) continue;
                    float dx = m.transform.position.x - p.x;
                    if (Mathf.Abs(m.transform.position.y - p.y) <= 2.5f && dx > 0.5f && dx < 2.6f && grounded) wantJump = true;
                }
                foreach (var leaf in FindObjectsByType<SpecialCollectible>(FindObjectsSortMode.None))
                {
                    float dx = leaf.transform.position.x - p.x, dy = leaf.transform.position.y - p.y;
                    if (grounded && dx > 0.5f && dx < 1.5f && dy > 1.2f && dy < 4.5f) { wantJump = true; longJump = true; }
                }
                if (grounded && GapAhead(player))
                {
                    float land = LandingAhead(p);
                    if (land < 0f) DemoMode.MoveX = GroundBelow(p, 1.5f, 0.6f, 5f) ? 1f : 0f; // a drop to lower ground (not a pit): walk off it
                    else { wantJump = true; longJump = land > 3f; if (longJump) DemoMode.RunHeld = true; }
                }
                else if (grounded)
                {
                    if (BlockedAhead(player)) wantJump = true;
                    // The jump lesson asks for a real jump that lands in its zone.
                    if (step == "w1s1_jump" && phase == StageTutorialFlow.State.Practice && !jumpedForStep && p.x >= 19f) { wantJump = true; jumpedForStep = true; }
                }
                // A low step does not reach the forward ray: standing still against something means "jump it".
                stuckFor = grounded && DemoMode.MoveX > 0.5f && Mathf.Abs(p.x - lastX) < 0.01f ? stuckFor + 1f / Fps : 0f;
                lastX = p.x;
                if (stuckFor > 0.3f) { wantJump = true; longJump = true; stuckFor = 0f; }
                if (wantJump && jumpRelease < 0f) { DemoMode.PressJump(); jumpRelease = t + (longJump ? 0.5f : 0.36f); }
                if (jumpRelease >= 0f && t >= jumpRelease) { DemoMode.ReleaseJump(); jumpRelease = -1f; }
                GuardFall(player, root);

                // Ends a beat after the stomp lesson (or once the course is past it).
                if (p.x > 50f) doneFor += 1f / Fps;
                if (doneFor >= 2.6f) break;
                yield return null;
            }
            DemoMode.MoveX = 0f;
            DemoMode.RunHeld = false;
            DemoMode.ReleaseJump();
            recording = false;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            takes.Add("{\"name\":\"tutorial\",\"start\":" + takeStart.ToString("F3", inv) + ",\"length\":" + t.ToString("F3", inv) + ",\"bad\":-1.000}");
            Log($"tutorial: {t:F1}s, seen {string.Join(",", progress.seen)}");
        }
    }
}
#endif
