#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Behavior;
using UnityEngine;

namespace MyGame.QA
{
    /// <summary>
    /// QA autoplay driver (test-only, no product code is modified). Drives the real player in Play Mode:
    /// PlayerInputReader is disabled and PlayerMotor.SetInput / RequestJump are pushed from FixedUpdate
    /// (execution order -100, i.e. before PlayerMotor), so input timing is per physics step and independent of
    /// the (possibly very low) Editor frame rate. All timing is game time (Time.time).
    /// InGameController state is read by reflection (private field "session"); Pause/Retry are called by reflection
    /// because the pause menu buttons are UI only.
    /// Entry points (static, called from execute_script runners): Begin, Status, Stop.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class QaAutoPlay : MonoBehaviour
    {
        private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        public static QaAutoPlay Instance { get; private set; }

        // ------------------------------------------------------------------ public entry points

        public static string Begin(string scenario, IDictionary<string, string> args)
        {
            if (!Application.isPlaying)
                return "ERROR: not in Play Mode.";
            if (Instance == null)
            {
                var go = new GameObject("QA_AutoPlay");
                Instance = go.AddComponent<QaAutoPlay>();
            }
            return Instance.StartScenario(scenario, args ?? new Dictionary<string, string>());
        }

        public static string Status()
        {
            if (Instance == null)
                return "STATUS scenario=(none) running=false result=NONE (driver not created; call start first)";
            return Instance.BuildStatus();
        }

        public static string Stop()
        {
            if (Instance == null)
                return "driver not created";
            Instance.Finish("stopped by caller", true);
            return Instance.BuildStatus();
        }

        // ------------------------------------------------------------------ state

        private sealed class Info
        {
            public Monster M;
            public Rigidbody Rb;
            public Collider Box;
            public BehaviorGraphAgent Agent;
            public Transform Visual;
            public Vector3 VisualScale;
            public Renderer[] Eyes;
            public float StartX;
            public float StartY;
            public string Name => M.name;
            public float X => Rb.position.x;

            public bool AlertOn()
            {
                foreach (var e in Eyes)
                {
                    if (e != null && e.sharedMaterial != null && e.sharedMaterial.name.Contains("Alert"))
                        return true;
                }
                return false;
            }
        }

        private sealed class ModeTrace
        {
            public Monster.Mode Last;
            public readonly List<Monster.Mode> Seq = new List<Monster.Mode>();
            public float MaxSpeed;
            public bool AlertInChase;
            public bool AlertOutside;
            private float prevX = float.NaN;

            public ModeTrace(Info c)
            {
                Last = c.M.CurrentMode;
                Seq.Add(Last);
            }

            public void Sample(QaAutoPlay q, Info c)
            {
                var mode = c.M.CurrentMode;
                if (mode != Last)
                {
                    Seq.Add(mode);
                    q.Log($"{c.Name}: mode {Last} -> {mode} x={c.X:F2} dist={Mathf.Abs(q.playerRb.position.x - c.X):F2}");
                    Last = mode;
                }
                float x = c.X;
                if (mode == Monster.Mode.Chase && !float.IsNaN(prevX))
                    MaxSpeed = Mathf.Max(MaxSpeed, Mathf.Abs(x - prevX) / Time.fixedDeltaTime);
                prevX = x;
                bool alert = c.AlertOn();
                if (mode == Monster.Mode.Chase) { if (alert) AlertInChase = true; }
                else if (alert) AlertOutside = true;
            }

            public bool Saw(Monster.Mode m) => Seq.Contains(m);

            /// <summary>True if the recorded modes contain a, then b, then c (not necessarily adjacent).</summary>
            public bool SawOrder(Monster.Mode a, Monster.Mode b, Monster.Mode c)
            {
                int stage = 0;
                foreach (var m in Seq)
                {
                    if (stage == 0 && m == a) stage = 1;
                    else if (stage == 1 && m == b) stage = 2;
                    else if (stage == 2 && m == c) return true;
                }
                return false;
            }
        }

        private readonly StringBuilder log = new StringBuilder();
        private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private readonly List<Info> monsters = new List<Info>();
        private Dictionary<string, string> args = new Dictionary<string, string>();

        private bool running;
        private string scenario = "(none)";
        private string result = "NONE";
        private int failures;
        private int checks;
        private bool waitOk;

        private float moveX;
        private bool jumpHeld;

        private PlayerMotor motor;
        private Rigidbody playerRb;
        private PlayerInputReader reader;
        private InGameController controller;
        private StageRoot stage;
        private bool subscribed;
        private FieldInfo sessionField;
        private MethodInfo pauseMethod;
        private MethodInfo retryMethod;

        private int hitCount;
        private float lastHitTime = -1f;
        private string lastHitNear = "";
        private float lastHitDist = -1f;

        private float savedMaxDt;
        private bool savedRunInBg;
        private bool settingsSaved;

        private StageSession Session => controller != null && sessionField != null ? sessionField.GetValue(controller) as StageSession : null;
        private StageState State => Session != null ? Session.State : StageState.Loading;
        private Vector3 PlayerPos => playerRb.position;

        // ------------------------------------------------------------------ args / log helpers

        private string S(string key, string def = null) => args.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : def;

        private float F(string key, float def) =>
            args.TryGetValue(key, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def;

        private void Log(string message)
        {
            string line = $"[QA] t={Time.time:F2} {message}";
            log.AppendLine(line);
            Debug.Log(line);
        }

        private void Check(string id, bool ok, string detail)
        {
            checks++;
            if (!ok)
                failures++;
            Log($"{(ok ? "PASS" : "FAIL")} {id}: {detail}");
        }

        private string BuildStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"STATUS scenario={scenario} running={running.ToString().ToLowerInvariant()} result={result} checks={checks} failures={failures} " +
                          $"gt={Time.time:F2} state={State} timeScale={Time.timeScale} hits={hitCount}");
            sb.Append(log);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ lifecycle

        private string StartScenario(string name, IDictionary<string, string> a)
        {
            if (running)
                Finish("restarted by new start", true);
            args = new Dictionary<string, string>(a);
            scenario = name;
            result = "RUNNING";
            failures = 0;
            checks = 0;
            hitCount = 0;
            moveX = 0f;
            jumpHeld = false;
            log.Clear();
            stack.Clear();
            monsters.Clear();

            if (!settingsSaved)
            {
                savedMaxDt = Time.maximumDeltaTime;
                savedRunInBg = Application.runInBackground;
                settingsSaved = true;
            }
            Application.runInBackground = true;
            Time.maximumDeltaTime = Mathf.Max(0.02f, F("maxdt", 0.05f)); // low editor FPS => game time runs slower, never coarser

            IEnumerator script = CreateScript(name);
            if (script == null)
            {
                result = "ERROR";
                return $"ERROR: unknown scenario '{name}'. Known: env, reset, patrol, stomp, stomp_hold, side_death, defeat_death_retry, walk_w1test, chase_blocked, chase_wall, chase_kill, chase_stomp, chaser_retry";
            }
            stack.Push(script);
            running = true;
            Log($"scenario '{name}' started (maxdt={Time.maximumDeltaTime:F3}, fixedDt={Time.fixedDeltaTime:F3}, unity={Application.unityVersion})");
            return $"started '{name}'";
        }

        private void Finish(string why, bool forced = false)
        {
            if (!running && !forced)
                return;
            running = false;
            stack.Clear();
            moveX = 0f;
            jumpHeld = false;
            if (motor != null)
                motor.SetInput(0f, false);
            if (reader != null)
                reader.enabled = true;
            if (subscribed && stage != null)
                stage.PlayerHit -= OnHit;
            subscribed = false;
            if (settingsSaved)
            {
                Time.maximumDeltaTime = savedMaxDt;
                Application.runInBackground = savedRunInBg;
                settingsSaved = false;
            }
            if (result == "RUNNING")
                result = failures == 0 && checks > 0 ? "PASS" : (checks == 0 ? "NO_CHECKS" : "FAIL");
            if (forced && why.StartsWith("stopped", StringComparison.Ordinal) && result == "PASS")
                result = "PASS";
            Log($"scenario '{scenario}' finished: {result} ({why}); checks={checks}, failures={failures}");
        }

        private void Abort(string why)
        {
            Check("ABORT", false, why);
            stack.Clear();
        }

        private void OnDestroy()
        {
            if (settingsSaved)
            {
                Time.maximumDeltaTime = savedMaxDt;
                Application.runInBackground = savedRunInBg;
            }
            if (Instance == this)
                Instance = null;
        }

        // ------------------------------------------------------------------ scheduler (one step per FixedUpdate)

        private void FixedUpdate()
        {
            if (!running)
                return;
            try
            {
                EnsureRefs();
                if (!Advance())
                    Finish("script ended");
            }
            catch (Exception ex)
            {
                Check("EXCEPTION", false, ex.ToString());
                Finish("exception");
            }
            if (motor != null)
                motor.SetInput(moveX, jumpHeld);
        }

        /// <summary>Runs the script until its next "yield return null" (= wait one fixed step). False when finished.</summary>
        private bool Advance()
        {
            for (int guard = 0; guard < 1000; guard++)
            {
                if (stack.Count == 0)
                    return false;
                var top = stack.Peek();
                bool more = top.MoveNext();
                if (stack.Count == 0)
                    return false; // Abort() cleared the stack while the script was running
                if (!more)
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator sub)
                {
                    stack.Push(sub);
                    continue;
                }
                return true;
            }
            return true;
        }

        private IEnumerator Until(Func<bool> cond, float timeoutGameSeconds)
        {
            float end = Time.time + timeoutGameSeconds;
            waitOk = false;
            while (Time.time < end)
            {
                if (cond())
                {
                    waitOk = true;
                    yield break;
                }
                yield return null;
            }
            waitOk = cond();
        }

        private IEnumerator Secs(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        // ------------------------------------------------------------------ scene references

        private void EnsureRefs()
        {
            if (controller == null)
                controller = FindFirstObjectByType<InGameController>();
            if (controller != null && sessionField == null)
            {
                var t = typeof(InGameController);
                sessionField = t.GetField("session", NonPublic);
                pauseMethod = t.GetMethod("Pause", NonPublic);
                retryMethod = t.GetMethod("Retry", NonPublic);
            }
            if (motor == null)
            {
                motor = FindFirstObjectByType<PlayerMotor>();
                playerRb = motor != null ? motor.GetComponent<Rigidbody>() : null;
            }
            if (reader == null && motor != null)
                reader = motor.GetComponent<PlayerInputReader>();
            if (reader != null && reader.enabled)
                reader.enabled = false; // the driver is the only input source
            if (stage == null)
                stage = FindFirstObjectByType<StageRoot>();
            if (stage != null && !subscribed)
            {
                stage.PlayerHit += OnHit;
                subscribed = true;
            }
        }

        private void Scan()
        {
            if (monsters.Count > 0)
                return;
            var found = FindObjectsByType<Monster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in found)
            {
                var visual = m.transform.Find("Visual");
                var eyes = new List<Renderer>();
                foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.StartsWith("Eye", StringComparison.Ordinal))
                        eyes.Add(r);
                }
                monsters.Add(new Info
                {
                    M = m,
                    Rb = m.GetComponent<Rigidbody>(),
                    Box = m.GetComponent<Collider>(),
                    Agent = m.GetComponent<BehaviorGraphAgent>(),
                    Visual = visual,
                    VisualScale = visual != null ? visual.localScale : Vector3.one,
                    Eyes = eyes.ToArray(),
                    StartX = m.PatrolMinX,
                    StartY = m.GetComponent<Rigidbody>().position.y,
                });
            }
            monsters.Sort((a, b) => a.StartX.CompareTo(b.StartX));
        }

        private void OnHit()
        {
            hitCount++;
            lastHitTime = Time.time;
            Info best = null;
            float bd = float.MaxValue;
            if (playerRb != null)
            {
                foreach (var i in monsters)
                {
                    if (i.M.IsDefeated)
                        continue;
                    float d = Vector2.Distance(i.Rb.position, playerRb.position);
                    if (d < bd) { bd = d; best = i; }
                }
            }
            lastHitNear = best != null ? best.Name : "";
            lastHitDist = best != null ? bd : -1f;
            Log($"event PlayerHit #{hitCount} state={State} playerPos={PlayerPos.x:F2},{PlayerPos.y:F2} nearest={lastHitNear} dist={lastHitDist:F2}");
        }

        // ------------------------------------------------------------------ small helpers

        private float FeetOffset => motor.FeetY - motor.transform.position.y;

        private void PlaceFeet(float x, float feetY)
        {
            motor.TeleportTo(new Vector3(x, feetY - FeetOffset + 0.02f, 0f));
        }

        private static float Track(float fromX, float targetX)
        {
            float d = targetX - fromX;
            return Mathf.Abs(d) < 0.1f ? 0f : Mathf.Clamp(d * 3f, -1f, 1f);
        }

        private Info Pick(string key, Func<Info, bool> pred, int skip = 0)
        {
            string v = S(key);
            if (!string.IsNullOrEmpty(v))
            {
                if (v.StartsWith("idx:", StringComparison.Ordinal) && int.TryParse(v.Substring(4), out int idx) && idx >= 0 && idx < monsters.Count)
                    return monsters[idx];
                foreach (var i in monsters)
                {
                    if (i.Name == v)
                        return i;
                }
                return null;
            }
            foreach (var i in monsters)
            {
                if (pred(i) && skip-- <= 0)
                    return i;
            }
            return null;
        }

        private Info NearestOther(Info m)
        {
            Info best = null;
            float bd = float.MaxValue;
            foreach (var i in monsters)
            {
                if (i == m || i.M.IsDefeated)
                    continue;
                float d = Mathf.Abs(i.X - m.X);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        private bool InvokeRetry(string tag)
        {
            if (State != StageState.Playing)
            {
                Check($"{tag}.pre", false, $"Retry needs Playing, state={State}");
                return false;
            }
            if (pauseMethod == null || retryMethod == null)
            {
                Check($"{tag}.reflection", false, "InGameController.Pause/Retry not found by reflection");
                return false;
            }
            pauseMethod.Invoke(controller, null);
            Check($"{tag}.paused", State == StageState.Paused, $"state={State}");
            retryMethod.Invoke(controller, null);
            Check($"{tag}.ready", State == StageState.Ready, $"state after Retry={State}");
            Check($"{tag}.timeScale", Mathf.Approximately(Time.timeScale, 1f), $"timeScale={Time.timeScale}");
            return State == StageState.Ready;
        }

        private IEnumerator WaitPlaying(float timeout = 60f)
        {
            yield return Until(() =>
            {
                EnsureRefs();
                return controller != null && Session != null && stage != null && motor != null && State == StageState.Playing;
            }, timeout);
            if (!waitOk)
            {
                Abort($"state never became Playing (state={State}, controller={(controller != null)}, stage={(stage != null)}, motor={(motor != null)})");
                yield break;
            }
            Scan();
        }

        private void CheckFresh(string tag, Info i, float posTol, bool checkFacing)
        {
            var m = i.M;
            Check($"{tag}.{i.Name}.notDefeated", !m.IsDefeated, $"defeated={m.IsDefeated}");
            Check($"{tag}.{i.Name}.visual", i.Visual == null || i.Visual.gameObject.activeSelf, $"visualActive={(i.Visual != null && i.Visual.gameObject.activeSelf)}");
            Check($"{tag}.{i.Name}.hitBox", i.Box.enabled, $"hitBoxEnabled={i.Box.enabled}");
            Check($"{tag}.{i.Name}.agent", i.Agent.enabled && i.Agent.Graph != null, $"agentEnabled={i.Agent.enabled}");
            Check($"{tag}.{i.Name}.hp", m.CurrentHp >= 1, $"hp={m.CurrentHp}");
            Check($"{tag}.{i.Name}.mode", m.CurrentMode == Monster.Mode.Patrol, $"mode={m.CurrentMode}");
            Check($"{tag}.{i.Name}.pos", Mathf.Abs(i.X - i.StartX) <= posTol, $"x={i.X:F2} start={i.StartX:F2} tol={posTol}");
            if (checkFacing)
                Check($"{tag}.{i.Name}.facing", m.Facing == 1, $"facing={m.Facing}");
            if (i.Visual != null)
                Check($"{tag}.{i.Name}.scale", (i.Visual.localScale - i.VisualScale).magnitude < 0.01f, $"scale={i.Visual.localScale} expected={i.VisualScale}");
            Check($"{tag}.{i.Name}.eyes", !i.AlertOn(), $"alertEyes={i.AlertOn()}");
        }

        private void CheckStaysDefeated(string tag, Info i)
        {
            Check($"{tag}.{i.Name}.defeated", i.M.IsDefeated, $"defeated={i.M.IsDefeated}");
            Check($"{tag}.{i.Name}.visualHidden", i.Visual == null || !i.Visual.gameObject.activeSelf, $"visualActive={(i.Visual != null && i.Visual.gameObject.activeSelf)}");
            Check($"{tag}.{i.Name}.hitBoxOff", !i.Box.enabled, $"hitBoxEnabled={i.Box.enabled}");
            Check($"{tag}.{i.Name}.agentOff", !i.Agent.enabled, $"agentEnabled={i.Agent.enabled}");
        }

        // ------------------------------------------------------------------ scenarios

        private IEnumerator CreateScript(string name)
        {
            switch (name)
            {
                case "env": return Env();
                case "reset": return ResetScenario();
                case "patrol": return Patrol();
                case "stomp": return StompScenario(false);
                case "stomp_hold": return StompScenario(true);
                case "side_death": return SideDeath();
                case "defeat_death_retry": return DefeatDeathRetry();
                case "walk_w1test": return WalkW1Test();
                case "chase_blocked": return ChaseBlocked();
                case "chase_wall": return ChaseWall();
                case "chase_kill": return ChaseKill();
                case "chase_stomp": return ChaseStomp();
                case "chaser_retry": return ChaserRetry();
                default: return null;
            }
        }

        // ---- env: wiring check (requirement 1) and bookkeeping

        private IEnumerator Env()
        {
            yield return WaitPlaying();
            Log($"env: unity={Application.unityVersion} fixedDt={Time.fixedDeltaTime} maxDt={Time.maximumDeltaTime} timeScale={Time.timeScale} lives={Session.Lives} coins={Session.Coins} checkpoint='{Session.CheckpointId}'");
            for (int i = 0; i < SceneCount(); i++)
                Log($"env: scene[{i}] {UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name}");
            Check("ENV.inputReader", reader != null && !reader.enabled, $"PlayerInputReader found={(reader != null)} disabled={(reader != null && !reader.enabled)} (SetInput/RequestJump are the injection API)");
            Check("ENV.monstersFound", monsters.Count > 0, $"{monsters.Count} monster(s)");
            yield return Secs(1.0f); // let Monster.BindTarget push the player into the blackboard
            foreach (var i in monsters)
            {
                var m = i.M;
                string graphName = i.Agent != null && i.Agent.Graph != null ? i.Agent.Graph.name : "(null)";
                Log($"env: {i.Name} id={m.MonsterId} pos=({i.X:F2},{i.StartY:F2}) zone=[{m.PatrolMinX:F2},{m.PatrolMaxX:F2}] speed={m.MoveSpeed} chase={m.ChaseSpeed} hp={m.CurrentHp} mode={m.CurrentMode} graph={graphName}");
                Check($"ENV.{i.Name}.agent", i.Agent != null && i.Agent.enabled && i.Agent.Graph != null, $"agent={(i.Agent != null)} enabled={(i.Agent != null && i.Agent.enabled)} graph={graphName}");
                string expect = m.MonsterId == 1001 ? "PatrolEnemyAI" : m.MonsterId == 1002 ? "ChaserEnemyAI" : null;
                Check($"ENV.{i.Name}.graphName", expect != null && graphName.Contains(expect), $"graph='{graphName}' expected contains '{expect}'");
                bool bound = false;
                string boundName = "(none)";
                if (i.Agent.GetVariable<GameObject>(Monster.TargetVariable, out var v))
                {
                    bound = v.Value == motor.gameObject;
                    boundName = v.Value != null ? v.Value.name : "(null)";
                }
                Check($"ENV.{i.Name}.targetBound", bound, $"blackboard Target={boundName}");
                bool selfOk = i.Agent.GetVariable<GameObject>("Self", out var sv) && sv.Value == m.gameObject;
                Check($"ENV.{i.Name}.selfBound", selfOk, "blackboard Self is the monster");
            }
            var gaps = new HashSet<string>();
            foreach (var i in monsters)
                gaps.Add(i.Name);
            Check("ENV.uniqueNames", gaps.Count == monsters.Count, "monster names are unique (needed for monster=<name> args)");
        }

        private static int SceneCount() => UnityEngine.SceneManagement.SceneManager.sceneCount;

        // ---- reset: Retry back to a clean attempt between scenarios

        private IEnumerator ResetScenario()
        {
            yield return WaitPlaying();
            int initialLives = Session.Lives;
            if (!InvokeRetry("RESET"))
                yield break;
            foreach (var i in monsters)
                CheckFresh("RESET", i, 0.1f, true);
            Check("RESET.coins", Session.Coins == 0, $"coins={Session.Coins}");
            Check("RESET.checkpoint", string.IsNullOrEmpty(Session.CheckpointId), $"checkpoint='{Session.CheckpointId}'");
            Check("RESET.playerAtSpawn", Mathf.Abs(PlayerPos.x - stage.SpawnPosition.x) < 0.5f, $"playerX={PlayerPos.x:F2} spawnX={stage.SpawnPosition.x:F2}");
            yield return Until(() => State == StageState.Playing, 20f);
            Check("RESET.playing", waitOk, $"state={State}");
            Log($"reset: lives before={initialLives} after={Session.Lives}");
        }

        // ---- patrol: patrollers patrol (requirement 3a)

        private IEnumerator Patrol()
        {
            float dur = F("dur", 16f);
            int id = (int)F("id", 1001);
            yield return WaitPlaying();
            PlaceFeet(stage.SpawnPosition.x, motor.FeetY); // keep the player where it spawned (no input)
            var list = new List<Info>();
            foreach (var i in monsters)
            {
                if (i.M.MonsterId == id)
                    list.Add(i);
            }
            Check("PATROL.found", list.Count > 0, $"{list.Count} monster(s) with id {id}");
            int n = list.Count;
            var min = new float[n]; var max = new float[n]; var path = new float[n]; var prev = new float[n];
            var lastDir = new int[n]; var reversals = new int[n]; var yDrift = new float[n];
            for (int k = 0; k < n; k++) { min[k] = max[k] = prev[k] = list[k].X; }
            int h0 = hitCount;
            float end = Time.time + dur;
            float t0 = Time.time;
            while (Time.time < end && hitCount == h0)
            {
                for (int k = 0; k < n; k++)
                {
                    float x = list[k].X;
                    min[k] = Mathf.Min(min[k], x);
                    max[k] = Mathf.Max(max[k], x);
                    float dx = x - prev[k];
                    path[k] += Mathf.Abs(dx);
                    int dir = Mathf.Abs(dx) < 1e-4f ? 0 : (dx > 0f ? 1 : -1);
                    if (dir != 0)
                    {
                        if (lastDir[k] != 0 && dir != lastDir[k]) reversals[k]++;
                        lastDir[k] = dir;
                    }
                    prev[k] = x;
                    yDrift[k] = Mathf.Max(yDrift[k], Mathf.Abs(list[k].Rb.position.y - list[k].StartY));
                }
                yield return null;
            }
            float elapsed = Time.time - t0;
            Check("PATROL.playerSurvived", hitCount == h0, $"hits during run={hitCount - h0}");
            for (int k = 0; k < n; k++)
            {
                var i = list[k];
                var m = i.M;
                float lo = m.PatrolMinX, hi = m.PatrolMaxX, speed = m.MoveSpeed;
                Log($"patrol {i.Name}: x=[{min[k]:F2},{max[k]:F2}] zone=[{lo:F2},{hi:F2}] path={path[k]:F2} over {elapsed:F1}s reversals={reversals[k]} yDrift={yDrift[k]:F3}");
                Check($"PATROL.{i.Name}.minX", min[k] >= lo - 0.7f, $"minX={min[k]:F2} zoneMin={lo:F2}");
                Check($"PATROL.{i.Name}.maxX", max[k] <= hi + 0.7f, $"maxX={max[k]:F2} zoneMax={hi:F2}");
                Check($"PATROL.{i.Name}.coversZone", min[k] <= lo + 0.7f && max[k] >= hi - 0.7f, $"visited [{min[k]:F2},{max[k]:F2}] of [{lo:F2},{hi:F2}] (a wall/ledge turn would show here)");
                Check($"PATROL.{i.Name}.reverses", reversals[k] >= 2, $"reversals={reversals[k]} (needs >= 2)");
                Check($"PATROL.{i.Name}.yStable", yDrift[k] < 0.02f, $"max y drift={yDrift[k]:F3}");
                if (id == 1001)
                {
                    float avg = path[k] / Mathf.Max(0.01f, elapsed);
                    Check($"PATROL.{i.Name}.speed", avg > speed * 0.85f && avg < speed * 1.05f, $"avg speed={avg:F2} table moveSpeed={speed:F2} (patroller turns without pause)");
                }
            }
        }

        // ---- stomp (requirement 3b: stomp defeats, hp 1)

        private IEnumerator StompScenario(bool hold)
        {
            yield return WaitPlaying();
            var m = Pick("monster", i => i.M.MonsterId == 1001);
            if (m == null) { Abort("no target monster (arg monster=<name>|idx:N)"); yield break; }
            Log($"stomp target {m.Name} id={m.M.MonsterId} x={m.X:F2} hold={hold}");
            yield return DoStomp(m, hold ? "STOMPHOLD" : "STOMP", hold, true);
            // The other monsters must be unaffected.
            foreach (var i in monsters)
            {
                if (i != m && i.M.IsDefeated)
                    Check($"STOMP.{i.Name}.collateral", false, "another monster got defeated too");
            }
        }

        private IEnumerator DoStomp(Info m, string tag, bool hold, bool teleport)
        {
            int h0 = hitCount;
            int lives0 = Session.Lives;
            int coins0 = Session.Coins;
            if (teleport)
                PlaceFeet(m.X, m.StartY + 3f);
            jumpHeld = hold;
            moveX = 0f;
            float end = Time.time + 4f;
            while (!m.M.IsDefeated && hitCount == h0 && Time.time < end)
            {
                moveX = Track(PlayerPos.x, m.X);
                yield return null;
            }
            bool defeated = m.M.IsDefeated;
            Check($"{tag}.defeated", defeated && hitCount == h0, $"defeated={defeated} newHits={hitCount - h0} feetY={motor.FeetY:F2} monster=({m.X:F2},{m.StartY:F2}) mode={m.M.CurrentMode}");
            if (!defeated)
            {
                moveX = 0f;
                jumpHeld = false;
                yield break;
            }

            float feet0 = motor.FeetY;
            float peak = feet0;
            float vmax = motor.Velocity.y;
            var other = NearestOther(m);
            float away = other != null ? (PlayerPos.x >= other.X ? 1f : -1f) : 1f;
            moveX = away;
            float t1 = Time.time + 1.3f;
            while (Time.time < t1 && hitCount == h0)
            {
                peak = Mathf.Max(peak, motor.FeetY);
                vmax = Mathf.Max(vmax, motor.Velocity.y);
                yield return null;
            }
            float rise = peak - feet0;
            Check($"{tag}.bounce", rise >= (hold ? 2.2f : 1.2f), $"rise={rise:F2} m (jump held={hold}: expect ~{(hold ? 3.2f : 1.9f):F1}), peak vy={vmax:F1}");
            moveX = 0f;
            jumpHeld = false;
            yield return Until(() => motor.IsGrounded, 3f);
            Check($"{tag}.stillPlaying", State == StageState.Playing && hitCount == h0, $"state={State} newHits={hitCount - h0}");
            Check($"{tag}.lives", Session.Lives == lives0, $"lives {lives0} -> {Session.Lives}");
            Check($"{tag}.coins", Session.Coins == coins0 + 0, $"coins {coins0} -> {Session.Coins} (rewardCoins is 0 in the table)");
            yield return Secs(0.6f);
            CheckStaysDefeated(tag, m);
            float x1 = m.X;
            yield return Secs(0.5f);
            Check($"{tag}.{m.Name}.frozen", Mathf.Abs(m.X - x1) < 0.001f, $"x {x1:F3} -> {m.X:F3}");
            Check($"{tag}.{m.Name}.mode", m.M.CurrentMode == Monster.Mode.Patrol && !m.AlertOn(), $"mode={m.M.CurrentMode} alertEyes={m.AlertOn()}");
        }

        // ---- side contact kills, respawn state (requirement 3c, 3e)

        private IEnumerator SideDeath()
        {
            yield return WaitPlaying();
            var m = Pick("monster", i => i.M.MonsterId == 1001);
            if (m == null) { Abort("no target monster"); yield break; }
            yield return DoSideDeath(m, "SIDE");
            yield return AwaitRespawn("SIDE", null);
        }

        private IEnumerator DoSideDeath(Info m, string tag)
        {
            int h0 = hitCount;
            int lives0 = Session.Lives;
            if (lives0 < 2)
            {
                Abort($"lives={lives0}: a death would be GameOver; run 'reset' first");
                yield break;
            }
            PlaceFeet(m.M.PatrolMaxX - 0.3f, m.StartY + 0.05f);
            moveX = 0f;
            jumpHeld = false;
            yield return Secs(0.5f); // land
            Check($"{tag}.standing", motor.IsGrounded, $"grounded={motor.IsGrounded} feetY={motor.FeetY:F2} (monster floor y={m.StartY:F2})");
            moveX = -1f;
            float end = Time.time + 8f;
            while (hitCount == h0 && Time.time < end)
                yield return null;
            moveX = 0f;
            Check($"{tag}.hit", hitCount > h0, $"PlayerHit events={hitCount - h0}; player x={PlayerPos.x:F2}");
            if (hitCount == h0)
                yield break;
            Check($"{tag}.state", State == StageState.Dead, $"state={State}");
            Check($"{tag}.lives", Session.Lives == lives0 - 1, $"lives {lives0} -> {Session.Lives}");
            Check($"{tag}.byMonster", lastHitDist >= 0f && lastHitDist < 1.3f, $"nearest monster at hit = {lastHitNear} dist {lastHitDist:F2}");
            int defeatedNow = 0;
            foreach (var i in monsters) { if (i.M.IsDefeated) defeatedNow++; }
            Log($"{tag}: monsters defeated at death = {defeatedNow}");
        }

        /// <summary>After a death: wait for Ready (respawn), check monsters at that instant, then for Playing.</summary>
        private IEnumerator AwaitRespawn(string tag, ICollection<Info> expectDefeated)
        {
            yield return Until(() => State == StageState.Ready, 15f);
            if (!waitOk)
            {
                Check($"{tag}.respawnReady", false, $"state never became Ready (state={State})");
                yield break;
            }
            Check($"{tag}.respawnReady", true, "Dead -> Ready");
            Vector3 expect = stage.SpawnPosition;
            if (stage.TryGetRespawnPosition(Session.CheckpointId, out var cp))
                expect = cp;
            Check($"{tag}.respawnPos", Mathf.Abs(PlayerPos.x - expect.x) < 1.0f, $"playerX={PlayerPos.x:F2} expected~{expect.x:F2} (checkpoint='{Session.CheckpointId}')");
            foreach (var i in monsters)
            {
                if (expectDefeated != null && expectDefeated.Contains(i))
                    CheckStaysDefeated(tag + ".respawn", i);
                else
                    CheckFresh(tag + ".respawn", i, 0.15f, true);
            }
            yield return Until(() => State == StageState.Playing, 15f);
            Check($"{tag}.playingAgain", waitOk, $"state={State}");
        }

        // ---- defeat, die, defeated stays defeated, Retry restores (requirement 3c-3e)

        private IEnumerator DefeatDeathRetry()
        {
            yield return WaitPlaying();
            var a = Pick("monster", i => i.M.MonsterId == 1001);
            var b = Pick("killer", i => i.M.MonsterId == 1001 && i != a);
            if (a == null || b == null || a == b) { Abort("need two different patrollers (args monster=, killer=)"); yield break; }
            int livesStart = Session.Lives;
            Log($"A (stomped)={a.Name} B (killer)={b.Name} livesStart={livesStart}");

            yield return DoStomp(a, "DDR.stomp", false, true);
            if (!a.M.IsDefeated) yield break;

            yield return DoSideDeath(b, "DDR.death");
            var defeated = new List<Info> { a };
            yield return AwaitRespawn("DDR", defeated);

            // After the respawn: A stays defeated and does not move; B patrols again.
            float ax = a.X;
            float bPath = 0f, bPrev = b.X;
            float end = Time.time + 2.5f;
            int h0 = hitCount;
            while (Time.time < end)
            {
                bPath += Mathf.Abs(b.X - bPrev);
                bPrev = b.X;
                yield return null;
            }
            CheckStaysDefeated("DDR.afterRespawn", a);
            Check("DDR.A.static", Mathf.Abs(a.X - ax) < 0.001f, $"A x {ax:F3} -> {a.X:F3}");
            Check("DDR.B.patrols", bPath >= 1.0f, $"B path over 2.5s = {bPath:F2}");
            Check("DDR.noHitWhileIdle", hitCount == h0, $"hits={hitCount - h0}");

            // Retry: everything returns, including the defeated monster.
            if (!InvokeRetry("DDR.retry"))
                yield break;
            foreach (var i in monsters)
                CheckFresh("DDR.retry", i, 0.1f, true);
            Check("DDR.retry.lives", Session.Lives == livesStart, $"lives={Session.Lives} expected initial {livesStart}");
            Check("DDR.retry.coins", Session.Coins == 0, $"coins={Session.Coins}");
            var ps = a.M.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null)
                Check("DDR.retry.burstCleared", ps.particleCount == 0 && !ps.isPlaying, $"particles={ps.particleCount} playing={ps.isPlaying}");
            yield return Until(() => State == StageState.Playing, 20f);
            Check("DDR.retry.playing", waitOk, $"state={State}");

            float aPath = 0f, aPrev = a.X;
            end = Time.time + 2.5f;
            while (Time.time < end)
            {
                aPath += Mathf.Abs(a.X - aPrev);
                aPrev = a.X;
                yield return null;
            }
            Check("DDR.retry.A.patrols", aPath >= 1.0f, $"A path after Retry over 2.5s = {aPath:F2} (graph restarted?)");

            // And it can be stomped a second time.
            yield return DoStomp(a, "DDR.stomp2", false, true);
        }

        // ---- natural run on W1_TEST: jump over the spikes, run into the patrollers, respawn at the checkpoint

        private IEnumerator WalkW1Test()
        {
            yield return WaitPlaying();
            float jumpX = F("jumpx", 6.6f);
            int h0 = hitCount;
            int lives0 = Session.Lives;
            if (lives0 < 2) { Abort("lives < 2; run reset first"); yield break; }
            PlaceFeet(stage.SpawnPosition.x, motor.FeetY);
            yield return Secs(0.4f);
            moveX = 1f;
            float end = Time.time + 3f;
            while (PlayerPos.x < jumpX && Time.time < end && hitCount == h0)
                yield return null;
            Log($"walk: jumping at x={PlayerPos.x:F2}");
            jumpHeld = true;
            motor.RequestJump();
            yield return Secs(0.8f);
            jumpHeld = false;
            Check("WALK.spikesCleared", hitCount == h0 && PlayerPos.x > 10.2f, $"x={PlayerPos.x:F2} hits={hitCount - h0}");
            end = Time.time + 12f;
            while (hitCount == h0 && Time.time < end)
                yield return null;
            moveX = 0f;
            Check("WALK.hitByMonster", hitCount > h0 && lastHitDist >= 0f && lastHitDist < 1.3f, $"hits={hitCount - h0} nearest={lastHitNear} dist={lastHitDist:F2} playerX={PlayerPos.x:F2}");
            Check("WALK.dead", State == StageState.Dead && Session.Lives == lives0 - 1, $"state={State} lives {lives0}->{Session.Lives}");
            Check("WALK.checkpointKept", Session.CheckpointId == "T_A_CP1", $"checkpoint='{Session.CheckpointId}' (expected T_A_CP1 at x=14, passed before the patrollers)");
            yield return AwaitRespawn("WALK", null);
        }

        // ---- chaser (requirement 3: spot, chase, reset when sight is blocked, patrol again)

        private Info Chaser() => Pick("monster", i => i.M.MonsterId == 1002);

        private Collider FindWall()
        {
            var go = GameObject.Find(S("wall", "T_C_wall2"));
            return go != null ? go.GetComponent<Collider>() : null;
        }

        private IEnumerator ChaseBlocked()
        {
            yield return WaitPlaying();
            var c = Chaser();
            var wall = FindWall();
            if (c == null || wall == null) { Abort("need chaser (1002) and wall (arg wall=<block name>)"); yield break; }
            float px = F("px", wall.bounds.min.x - 1.5f);
            PlaceFeet(px, c.StartY);
            moveX = 0f;
            jumpHeld = false;
            yield return Secs(0.5f);
            Log($"blocked: player x={PlayerPos.x:F2}, chaser zone=[{c.M.PatrolMinX:F2},{c.M.PatrolMaxX:F2}], wall x=[{wall.bounds.min.x:F2},{wall.bounds.max.x:F2}] top={wall.bounds.max.y:F2}");
            var trace = new ModeTrace(c);
            int h0 = hitCount;
            int eligible = 0, spotted = 0;
            float end = Time.time + F("dur", 14f);
            while (Time.time < end && hitCount == h0)
            {
                trace.Sample(this, c);
                float dx = PlayerPos.x - c.X;
                bool facingPlayer = (dx >= 0f && c.M.Facing > 0) || (dx < 0f && c.M.Facing < 0);
                if (Mathf.Abs(dx) < 7.5f && facingPlayer && c.M.CurrentMode == Monster.Mode.Patrol)
                {
                    eligible++;
                    if (c.M.CanSpot(motor.gameObject))
                        spotted++;
                }
                yield return null;
            }
            Check("BLOCKED.meaningful", eligible >= 20, $"steps where the player was in range (<7.5), in front of the chaser and only the wall in between: {eligible}");
            Check("BLOCKED.neverSpotted", spotted == 0 && !trace.Saw(Monster.Mode.Chase), $"CanSpot true in {spotted} steps; modes seen: {string.Join(">", trace.Seq)}");
            Check("BLOCKED.alive", hitCount == h0, $"hits={hitCount - h0}");
            Check("BLOCKED.alertEyes", !trace.AlertOutside, $"alert eyes outside chase={trace.AlertOutside}");
        }

        private IEnumerator ChaseWall()
        {
            yield return WaitPlaying();
            var c = Chaser();
            var wall = FindWall();
            if (c == null || wall == null) { Abort("need chaser (1002) and wall"); yield break; }
            float lose = F("lose", 11f);
            var b = wall.bounds;
            PlaceFeet(b.center.x, b.max.y + 1.0f); // stand on top of the wall, in plain view of the chaser
            moveX = 0f;
            jumpHeld = false;
            yield return Secs(0.6f);
            Log($"wall: player standing at ({PlayerPos.x:F2},{motor.FeetY:F2}) wall top={b.max.y:F2} grounded={motor.IsGrounded}");
            Check("WALL.onTop", motor.IsGrounded && motor.FeetY > b.max.y - 0.1f, $"grounded={motor.IsGrounded} feetY={motor.FeetY:F2} wallTop={b.max.y:F2}");
            var trace = new ModeTrace(c);
            int h0 = hitCount;

            // 1) spot + chase
            float end = Time.time + 30f;
            while (Time.time < end && hitCount == h0 && !trace.Saw(Monster.Mode.Chase)) { trace.Sample(this, c); yield return null; }
            Check("WALL.chaseStarted", trace.Saw(Monster.Mode.Chase), $"modes={string.Join(">", trace.Seq)}");
            if (!trace.Saw(Monster.Mode.Chase)) yield break;

            // 2) chase ends because the sight line is blocked (not because of distance)
            float lastDist = 0f; bool lastCanTrack = true;
            end = Time.time + 15f;
            while (Time.time < end && hitCount == h0 && c.M.CurrentMode == Monster.Mode.Chase)
            {
                trace.Sample(this, c);
                lastDist = Mathf.Abs(PlayerPos.x - c.X);
                lastCanTrack = c.M.CanTrack(motor.gameObject);
                yield return null;
            }
            trace.Sample(this, c);
            Check("WALL.resetReached", trace.Saw(Monster.Mode.Reset), $"modes={string.Join(">", trace.Seq)}");
            Check("WALL.blockedNotRange", lastDist < lose - 1f, $"horizontal distance at the end of the chase={lastDist:F2} (loseDistance {lose}); the chase must end by the wall, not by range");
            Check("WALL.noContact", hitCount == h0, $"hits={hitCount - h0}");
            Check("WALL.chaseSpeed", trace.MaxSpeed > 3.3f && trace.MaxSpeed < 3.7f, $"max chase speed={trace.MaxSpeed:F2} (table 3.5)");
            Check("WALL.alertEyes", trace.AlertInChase && !trace.AlertOutside, $"alert eyes during chase={trace.AlertInChase}, outside chase={trace.AlertOutside}");
            Log($"wall: chase ended at chaser x={c.X:F2}, last CanTrack={lastCanTrack}");

            // 3) patrol again and walk back into the zone
            float resetStart = Time.time;
            yield return Until(() => c.M.CurrentMode == Monster.Mode.Patrol, 4f);
            Check("WALL.patrolAgain", waitOk, $"mode={c.M.CurrentMode} {Time.time - resetStart:F2}s after Reset (Wait 0.8s expected)");
            float x0 = c.X;
            yield return Secs(2.0f);
            trace.Sample(this, c);
            Check("WALL.walksHome", Mathf.Abs(c.X - x0) > 0.5f, $"x {x0:F2} -> {c.X:F2} in 2s");

            // 4) the pattern repeats
            end = Time.time + 40f;
            int chases = 0;
            var last = c.M.CurrentMode;
            while (Time.time < end && hitCount == h0 && chases < 1)
            {
                trace.Sample(this, c);
                if (c.M.CurrentMode == Monster.Mode.Chase && last != Monster.Mode.Chase) chases++;
                last = c.M.CurrentMode;
                yield return null;
            }
            Check("WALL.repeats", chases >= 1, $"second chase observed={chases >= 1}; sequence={string.Join(">", trace.Seq)}");
            Check("WALL.order", trace.SawOrder(Monster.Mode.Patrol, Monster.Mode.Chase, Monster.Mode.Reset), "Patrol>Chase>Reset seen");
        }

        private IEnumerator ChaseKill()
        {
            yield return WaitPlaying();
            var c = Chaser();
            if (c == null) { Abort("no chaser"); yield break; }
            if (Session.Lives < 2) { Abort("lives < 2; run reset first"); yield break; }
            int h0 = hitCount;
            int lives0 = Session.Lives;
            PlaceFeet(c.M.PatrolMaxX + F("dx", 3f), c.StartY);
            moveX = 0f;
            jumpHeld = false;
            var trace = new ModeTrace(c);
            float end = Time.time + 25f;
            while (Time.time < end && hitCount == h0) { trace.Sample(this, c); yield return null; }
            trace.Sample(this, c);
            Check("CKILL.chaseBeforeHit", trace.Saw(Monster.Mode.Chase), $"modes={string.Join(">", trace.Seq)}");
            Check("CKILL.chaseSpeed", trace.MaxSpeed > 3.3f && trace.MaxSpeed < 3.7f, $"max chase speed={trace.MaxSpeed:F2} (table 3.5)");
            Check("CKILL.alertEyes", trace.AlertInChase, "eyes switched to the alert material during the chase");
            Check("CKILL.hit", hitCount > h0 && State == StageState.Dead && Session.Lives == lives0 - 1, $"hits={hitCount - h0} state={State} lives {lives0}->{Session.Lives}");
            Check("CKILL.byChaser", lastHitDist >= 0f && lastHitDist < 1.3f && lastHitNear == c.Name, $"nearest={lastHitNear} dist={lastHitDist:F2}");
            yield return AwaitRespawn("CKILL", null);
            // The chaser must be a working patroller again after the respawn.
            var trace2 = new ModeTrace(c);
            float path = 0f, prev = c.X;
            end = Time.time + 4f;
            while (Time.time < end)
            {
                trace2.Sample(this, c);
                path += Mathf.Abs(c.X - prev);
                prev = c.X;
                yield return null;
            }
            Check("CKILL.patrolsAfterRespawn", path >= 3f && !trace2.Saw(Monster.Mode.Chase), $"path over 4s={path:F2}, modes={string.Join(">", trace2.Seq)}");
        }

        private IEnumerator ChaseStomp()
        {
            yield return WaitPlaying();
            var c = Chaser();
            if (c == null) { Abort("no chaser"); yield break; }
            int h0 = hitCount;
            PlaceFeet(c.M.PatrolMaxX + F("dx", 3f), c.StartY);
            moveX = 0f;
            jumpHeld = false;
            yield return Until(() => c.M.CurrentMode == Monster.Mode.Chase && Mathf.Abs(c.X - PlayerPos.x) < 4.5f || hitCount > h0, 25f);
            Check("CSTOMP.chasing", c.M.CurrentMode == Monster.Mode.Chase && hitCount == h0, $"mode={c.M.CurrentMode} hits={hitCount - h0}");
            if (c.M.CurrentMode != Monster.Mode.Chase || hitCount > h0) yield break;
            yield return DoStomp(c, "CSTOMP", false, true);
        }

        private IEnumerator ChaserRetry()
        {
            yield return WaitPlaying();
            var c = Chaser();
            if (c == null) { Abort("no chaser"); yield break; }
            yield return DoStomp(c, "CRETRY.stomp", false, true);
            if (!c.M.IsDefeated) yield break;
            if (!InvokeRetry("CRETRY.retry"))
                yield break;
            foreach (var i in monsters)
                CheckFresh("CRETRY.retry", i, 0.1f, true);
            yield return Until(() => State == StageState.Playing, 20f);
            Check("CRETRY.playing", waitOk, $"state={State}");
            if (!waitOk) yield break;
            // After Retry the target must be bound again and the chaser must chase.
            int h0 = hitCount;
            var trace = new ModeTrace(c);
            PlaceFeet(c.M.PatrolMaxX + F("dx", 3f), c.StartY);
            moveX = 0f;
            float end = Time.time + 25f;
            while (Time.time < end && hitCount == h0 && !trace.Saw(Monster.Mode.Chase)) { trace.Sample(this, c); yield return null; }
            Check("CRETRY.chasesAgain", trace.Saw(Monster.Mode.Chase), $"modes={string.Join(">", trace.Seq)} (the graph restarted and the blackboard Target is still bound)");
            moveX = 0f;
        }
    }
}
#endif
