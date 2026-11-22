#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MyGame.Dev
{
    /// <summary>
    /// Trailer footage mode of the showcase recorder (Tools/MyGame/Demo/Record Trailer Footage): the same scripted play,
    /// but with no caption overlay and no music, the player's own voice recordings switched on, and a time marker per
    /// take (trailer_marks.json) so the takes can be cut out of the single raw file afterwards.
    /// </summary>
    public sealed partial class ShowcaseDirector
    {
        /// <summary>Record clean trailer takes instead of the milestone reel.</summary>
        public static bool Trailer;
        public static string TrailerDir => Path.Combine(OutputDir, "trailer", "raw");

        private readonly List<string> marks = new List<string>();
        private readonly List<string> takes = new List<string>();

        private enum RunMode { Stomp, Dodge, Die }

        // (stage, start x; NaN = next to a monster near the middle of the stage)
        private static readonly (string id, float x)[] TrailerRuns =
        {
            ("W1_S1", float.NaN), ("W1_S2", 78f), ("W1_S3", float.NaN), ("W1_S4", float.NaN),
            ("W2_S1", float.NaN), ("W2_S2", 44f), ("W2_S3", float.NaN), ("W2_S4", float.NaN),
            ("W3_S1", float.NaN), ("W3_S2", float.NaN), ("W3_S3", float.NaN), ("W3_S4", float.NaN),
            ("W4_S1", float.NaN), ("W4_S2", 156f), ("W4_S3", float.NaN), ("W4_S4", float.NaN),
            ("W5_S1", float.NaN), ("W5_S2", 67f), ("W5_S3", float.NaN), ("W5_S4", float.NaN),
        };

        private void Mark(string name)
        {
            marks.Add($"{{\"name\":\"{name}\",\"start\":{(framesRecorded / (float)Fps).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}}}");
            Log($"mark {name} @ {framesRecorded / (float)Fps:F2}s");
        }

        private void WriteMarks()
        {
            Directory.CreateDirectory(TrailerDir);
            marks.Add($"{{\"name\":\"_end\",\"start\":{(framesRecorded / (float)Fps).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}}}");
            File.WriteAllText(Path.Combine(TrailerDir, Story ? "story" + StorySuffix + "_marks.json" : Quick ? "trailer_marks_quick.json" : "trailer_marks.json"), "[\n" + string.Join(",\n", marks) + "\n]\n");
            File.WriteAllText(Path.Combine(TrailerDir, Story ? "story" + StorySuffix + "_takes.json" : Quick ? "trailer_takes_quick.json" : "trailer_takes.json"), "[\n" + string.Join(",\n", takes) + "\n]\n");
        }

        private IEnumerator TrailerSequence()
        {
            var systems = GameSystems.Instance;
            // The capture reads the mixed output, so the master volume must be up (the settings file is restored afterwards).
            systems.Settings.Data.masterVolume = 1f;
            systems.Settings.Data.sfxVolume = 0.8f;
            systems.Settings.NotifyChanged();
            // Open every voice slot for the shoot (in memory; the save file is restored afterwards).
            var save = systems.Save.Current;
            foreach (string id in new[] { "W3_S1", "W3_S2", "W3_S3", "W3_S4", "W3_B", "W4_S1" })
            {
                var rec = save.stages.Find(r => r != null && r.stageId == id);
                if (rec == null) { rec = new StageRecord { stageId = id }; save.stages.Add(rec); }
                rec.collectibles = new[] { true, true, true };
            }
            var voice = save.Voice;
            for (int i = 0; i < voice.announced.Length; i++) voice.announced[i] = true;
            SetVoices(true);
            Log($"trailer: leaves {save.TotalLeaves()}, lives {save.lives}");

            if (Quick)
            {
                // Pick-up shoot: only the act-1 takes (built-in sounds, no voice), saved beside the main shoot.
                SetVoices(false);
                foreach (string id in new[] { "W1_S1", "W1_S2", "W1_S3", "W1_S4" })
                {
                    yield return LoadStage(id);
                    yield return RunTake("quiet_" + id + "_c", float.NaN, 7f, RunMode.Stomp, hopEvery: 1.5f, fromSpawn: true);
                    yield return RunTake("quiet_" + id + "_m", float.NaN, 6f, RunMode.Stomp, hopEvery: 0f, fromSpawn: false);
                    yield return RunTake("quiet_" + id + "_k", float.NaN, 6f, RunMode.Stomp, hopEvery: 1.5f, fromSpawn: false, checkpoint: true);
                }
                WriteMarks();
                yield break;
            }

            // ---------------------------------------------------- title + world map + voice studio
            Mark("title");
            yield return Rec(3.5f);

            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.Continue));
            yield return WaitIdle();
            yield return WaitUnscaled(0.8f);
            Mark("worldmap");
            yield return Rec(5f);
            yield return StudioShots();

            // ---------------------------------------------------- the same run without and with the player's voice
            yield return LoadStage("W1_S1");
            SetVoices(false);
            yield return RunTake("jump_silent", float.NaN, 5.5f, RunMode.Stomp, hopEvery: 1.1f, fromSpawn: true);
            // Act 1 of the trailer plays before the voice is recorded: the same kind of takes with the built-in sounds.
            foreach (var q in new (string id, float x, bool cp)[] { ("W1_S2", 78f, false), ("W1_S3", float.NaN, false), ("W1_S4", float.NaN, true), ("W1_S2", float.NaN, true), ("W1_S1", 40f, false) })
            {
                yield return LoadStage(q.id);
                yield return RunTake("quiet_" + q.id + (q.cp ? "_b" : ""), q.x, 5f, RunMode.Stomp, hopEvery: q.cp ? 1.4f : 0f, fromSpawn: false, checkpoint: q.cp, run: q.cp);
            }
            yield return LoadStage("W1_S1");
            Mark("quiet_clear");
            yield return ClearShot();
            yield return LoadStage("W1_S1");
            SetVoices(true);
            yield return RunTake("jump_voiced", float.NaN, 7f, RunMode.Stomp, hopEvery: 1.1f, fromSpawn: true);
            yield return LoadStage("W1_S1");
            yield return RunTake("jump_voiced_b", float.NaN, 7f, RunMode.Stomp, hopEvery: 0.9f, fromSpawn: true);

            // ---------------------------------------------------- stages: one take next to a monster, one longer run
            foreach (var r in TrailerRuns)
            {
                if (Quick && r.id != "W1_S2" && r.id != "W3_S2") continue;
                yield return LoadStage(r.id);
                yield return RunTake("run_" + r.id, r.x, 6f, RunMode.Stomp, hopEvery: 0f, fromSpawn: false);
                yield return RunTake("run_" + r.id + "_b", float.NaN, 6f, RunMode.Stomp, hopEvery: 1.4f, fromSpawn: false, checkpoint: true, run: true);
                yield return LoadStage(r.id);
                yield return RunTake("run_" + r.id + "_c", float.NaN, 6f, RunMode.Stomp, hopEvery: 1.6f, fromSpawn: true);
            }

            // ---------------------------------------------------- dodge voice + perfect dodge (time stop)
            foreach (string id in new[] { "W1_S3", "W2_S1", "W3_S2", "W4_S1" })
            {
                if (Quick && id != "W2_S1") continue;
                yield return LoadStage(id);
                yield return RunTake("dodge_" + id, float.NaN, 4.5f, RunMode.Dodge, hopEvery: 0f, fromSpawn: false);
            }

            // ---------------------------------------------------- bosses (perfect dodge, "Now!" cut-in, stomp)
            var bosses = new (string id, BossAttackKind kind)[]
            {
                ("W1_B", BossAttackKind.Charge), ("W2_B", BossAttackKind.Slam), ("W3_B", BossAttackKind.Rain),
                ("W4_B", BossAttackKind.Charge), ("W5_B", BossAttackKind.Eruption), ("W1_B", BossAttackKind.Charge),
            };
            save.Voice.disabled[(int)VoiceSlot.Now] = true; // the boss cut-in keeps its built-in sound in the trailer
            int bossIndex = 0;
            foreach (var b in bosses)
            {
                bossIndex++;
                if (Quick && bossIndex != 1) continue;
                yield return LoadStage(b.id);
                Mark($"boss_{b.id}_{bossIndex}");
                yield return BossShot(b.id, b.kind, "");
            }

            // ---------------------------------------------------- clear
            yield return LoadStage("W1_S1");
            Mark("clear");
            yield return ClearShot();

            // ---------------------------------------------------- deaths (the scream), last: they cost lives
            foreach (string id in new[] { "W1_S3", "W2_S1", "W3_S2" })
            {
                if (Quick && id != "W1_S3") continue;
                yield return LoadStage(id);
                DemoMode.Invincible = false;
                yield return RunTake("death_" + id, float.NaN, 6f, RunMode.Die, hopEvery: 0f, fromSpawn: false);
                DemoMode.Invincible = true;
                yield return WaitUnscaled(1.5f);
            }
            WriteMarks();
        }

        private static void SetVoices(bool on)
        {
            var voice = GameSystems.Instance.Save.Current.Voice;
            for (int i = 0; i < voice.disabled.Length; i++) voice.disabled[i] = !on;
        }

        // ------------------------------------------------------------------ voice studio

        private IEnumerator StudioShots()
        {
            GameSystems.Instance.Popups.OpenVoiceStudio();
            yield return WaitUnscaled(0.3f);
            var popup = FindAnyObjectByType<VoiceStudioPopup>();
            if (popup == null) { Log("studio: popup did not open"); yield break; }
            var t = typeof(VoiceStudioPopup);
            System.Action<string, object[]> call = (m, a) => t.GetMethod(m, Any)?.Invoke(popup, a);

            call("SelectSlot", new object[] { 0 });
            Mark("studio_open");
            yield return Rec(3f);

            // A recording, staged so the shoot needs no microphone and never overwrites a take: the real 3-2-1 countdown,
            // then the player's existing jump take is played while its level is pushed into the live waveform.
            {
                var modeField = t.GetField("mode", Any);
                var wave = t.GetField("wave", Any).GetValue(popup) as VoiceWaveGraphic;
                var config = VoiceStudioConfig.Load();
                var clip = VoiceLibrary.Get(VoiceSlot.Jump, 0);
                System.Action<string> status = key =>
                {
                    t.GetMethod("SetStatus", Any).Invoke(popup, new object[] { key, false, new object[0] });
                    call("Refresh", null);
                };
                if (wave != null && clip != null)
                {
                    Mark("studio_record");
                    Pause(false);
                    recording = true;
                    yield return Hold(0.8f);
                    modeField.SetValue(popup, System.Enum.Parse(modeField.FieldType, "Countdown"));
                    t.GetField("countdown", Any).SetValue(popup, config.countdownSeconds);
                    wave.SetSamples(null);
                    status("voice.msg.get_ready");
                    float guard = 0f;
                    while ((float)t.GetField("countdown", Any).GetValue(popup) > 0.1f && guard < 6f) { guard += 1f / Fps; yield return null; }
                    popup.enabled = false; // stops the popup's own tick, so the microphone is never opened
                    modeField.SetValue(popup, System.Enum.Parse(modeField.FieldType, "Recording"));
                    status("voice.msg.recording");
                    var samples = new float[clip.samples * clip.channels];
                    clip.GetData(samples, 0);
                    float limit = Mathf.Max(config.Find(VoiceSlot.Jump).maxSeconds, clip.length);
                    Voice.Preview(clip);
                    for (float c = 0f; c < limit; c += 1f / Fps)
                    {
                        int from = Mathf.Clamp((int)(c / clip.length * samples.Length), 0, samples.Length), to = Mathf.Clamp((int)((c + 1f / Fps) / clip.length * samples.Length), 0, samples.Length);
                        float peak = 0f;
                        for (int i = from; i < to; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
                        wave.Push(c / limit, peak);
                        yield return null;
                    }
                    modeField.SetValue(popup, System.Enum.Parse(modeField.FieldType, "Idle"));
                    call("ShowWave", null);
                    status("voice.msg.saved");
                    yield return Hold(1.4f);
                    recording = false;
                    popup.enabled = true;
                }
                else Log("studio: no jump take to stage the recording with");
            }

            string[] names = { "jump", "dodge", "perfect", "death", "now" };
            for (int slot = 0; slot < names.Length; slot++)
            {
                call("SelectSlot", new object[] { slot });
                yield return Settle(3);
                Mark("studio_listen_" + names[slot]);
                Pause(false);
                recording = true;
                yield return Hold(0.5f);
                call("OnListenPressed", null);
                yield return Hold(slot >= 2 ? 2.4f : 1.6f);
                recording = false;
            }
            call("Close", null);
            yield return WaitUnscaled(0.6f);
        }

        // ------------------------------------------------------------------ gameplay takes

        private IEnumerator RunTake(string name, float startX, float seconds, RunMode mode, float hopEvery, bool fromSpawn,
            bool checkpoint = false, bool run = false)
        {
            var player = FindAnyObjectByType<PlayerMotor>();
            var root = FindAnyObjectByType<StageRoot>();
            if (player == null || root == null) { Log($"{name}: no player/stage"); yield break; }

            Vector3 start = root.SpawnPosition;
            float midX = root.HasLevelEdges ? (root.LevelMinX + root.LevelMaxX) * 0.5f : root.SpawnPosition.x + 60f;
            if (!fromSpawn)
            {
                Monster target = null;
                float bestScore = float.MaxValue;
                foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
                {
                    if (m.IsDefeated) continue;
                    float score = Mathf.Abs(m.transform.position.x - midX) * 0.4f + Hash(name, m.name) * 25f;
                    if (mode != RunMode.Stomp && m.MonsterId >= 2000) score += 200f; // plain walkers for dodge / death takes
                    if (score < bestScore) { bestScore = score; target = m; }
                }
                bool placed = false;
                if (!float.IsNaN(startX))
                    placed = TryFindGround(startX, root.SpawnPosition.y + 12f, out start);
                else if (!checkpoint && target != null)
                    placed = TryFindGround(target.transform.position.x - (mode == RunMode.Stomp ? 6.5f : 5f), target.transform.position.y, out start);
                if (!placed)
                {
                    float best = float.MaxValue;
                    foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                    {
                        float d = Mathf.Abs(cp.transform.position.x - midX);
                        if (d < best) { best = d; start = cp.RespawnPosition; }
                    }
                }
                player.TeleportTo(start + Vector3.up * 0.1f);
                SnapCamera();
            }
            lastSafe = Vector3.zero;
            yield return Settle(6);
            Log($"{name}: start {player.transform.position}");

            Mark(name);
            DemoMode.MoveX = mode == RunMode.Die ? 0.8f : 1f;
            DemoMode.RunHeld = run;
            Pause(false);
            recording = true;
            float t = 0f, jumpRelease = -1f, nextHop = hopEvery > 0f ? 0.7f : float.MaxValue, deadAt = -1f;
            float bad = -1f, stalled = 0f, lastGroundY = player.transform.position.y, lastGroundT = 0f, takeStart = framesRecorded / (float)Fps;
            bool dodged = false;
            while (t < seconds)
            {
                t += 1f / Fps;
                if (mode == RunMode.Die)
                {
                    if (deadAt < 0f && StageState() == "Dead") { deadAt = t; DemoMode.MoveX = 0f; }
                    if (deadAt >= 0f && t > deadAt + 2.2f) break;
                    if (deadAt < 0f && t > 12f) break; // nothing bit
                    seconds = t + 1f;
                    yield return null;
                    continue;
                }
                bool wantJump = false;
                bool grounded = player.IsGrounded;
                Vector3 p = player.transform.position;
                foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
                {
                    if (m.IsDefeated) continue;
                    float dx = m.transform.position.x - p.x;
                    if (Mathf.Abs(m.transform.position.y - p.y) > 2.5f) continue;
                    if (mode == RunMode.Dodge)
                    {
                        if (!dodged && dx > 0.2f && dx < 1.5f) { dodged = true; player.RequestDodge(); }
                    }
                    else if (dx > 0.5f && dx < 2.6f && grounded) wantJump = true;
                }
                // A take is only usable up to the first fall or stall ("bad"); the edit picks its cuts from the clean part.
                if (grounded) { lastGroundY = p.y; lastGroundT = t; }
                if (bad < 0f && (p.y < root.KillY + 3f || (!grounded && p.y < lastGroundY - 4f))) bad = lastGroundT;
                bool longJump = false;
                if (grounded && GapAhead(player))
                {
                    float land = LandingAhead(p);
                    if (land < 0f)
                    {
                        // Nothing to land on (a moving platform that is away, or a pit too wide): wait at the edge.
                        DemoMode.MoveX = 0f;
                        stalled += 1f / Fps;
                        if (bad < 0f && stalled > 0.4f) bad = t - 0.4f;
                    }
                    else
                    {
                        DemoMode.MoveX = 1f;
                        wantJump = true;
                        longJump = land > 3f;
                        if (longJump) DemoMode.RunHeld = true;
                    }
                }
                else if (grounded)
                {
                    DemoMode.MoveX = 1f;
                    if (jumpRelease < 0f) DemoMode.RunHeld = run;
                    bool clear = GroundBelow(p, 1.5f) && GroundBelow(p, 2.5f) && GroundBelow(p, 3.5f) && GroundBelow(p, 4.5f);
                    if (t >= nextHop && clear) { wantJump = true; nextHop = t + hopEvery; } // never hop into a pit
                    if (BlockedAhead(player)) wantJump = true;
                }
                if (wantJump && jumpRelease < 0f) { DemoMode.PressJump(); jumpRelease = t + (longJump ? 0.5f : 0.36f); }
                if (jumpRelease >= 0f && t >= jumpRelease) { DemoMode.ReleaseJump(); jumpRelease = -1f; }
                GuardFall(player, root);
                yield return null;
            }
            DemoMode.MoveX = 0f;
            DemoMode.RunHeld = false;
            DemoMode.ReleaseJump();
            recording = false;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            takes.Add("{\"name\":\"" + name + "\",\"start\":" + takeStart.ToString("F3", inv) + ",\"length\":" + t.ToString("F3", inv) + ",\"bad\":" + bad.ToString("F3", inv) + "}");
            Log($"{name}: {t:F1}s, clean until {(bad < 0f ? "the end" : bad.ToString("F1") + "s")}");
        }

        private static bool GroundBelow(Vector3 p, float dx, float up = 0.6f, float depth = 2.6f) =>
            Physics.Raycast(p + new Vector3(dx, up, 0f), Vector3.down, up + depth, ~0, QueryTriggerInteraction.Ignore);

        /// <summary>Distance to ground that can be landed on beyond a gap (two samples, so not a sliver), or -1.</summary>
        private static float LandingAhead(Vector3 p)
        {
            for (float d = 1.6f; d <= 5.2f; d += 0.4f)
                if (GroundBelow(p, d, 2.5f, 2.2f) && GroundBelow(p, d + 0.5f, 2.5f, 2.2f)) return d;
            return -1f;
        }

        private static bool GapAhead(PlayerMotor player)
        {
            var o = player.transform.position + new Vector3(0.9f, 0.6f, 0f);
            return !Physics.Raycast(o, Vector3.down, 3.2f, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
#endif
