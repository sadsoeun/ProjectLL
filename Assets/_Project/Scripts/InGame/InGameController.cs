using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace MyGame
{
    /// <summary>
    /// Orchestrates one stage attempt (spec 5.6): loads the additive stage scene, drives
    /// Ready / Playing / Paused / Dead / Clear / Result / GameOver, and returns to the world map.
    /// </summary>
    public sealed class InGameController : MonoBehaviour
    {
        [SerializeField] private PlayerMotor player;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private InGameView view;
        [Tooltip("Stage played when this scene is started directly in the Editor.")]
        [SerializeField] private string debugStageId = "W1_S1";
        [SerializeField, Min(0f)] private float readyDuration = 1.0f;
        [SerializeField, Min(0f)] private float deadDuration = 1.2f;
        [Tooltip("Killed in mid-air: longest fall (game seconds) before the death pose plays anyway.")]
        [SerializeField, Min(0f)] private float airDeathMaxFall = 1.5f;
        [SerializeField, Min(0f)] private float clearDuration = 2.6f;

        [Header("Camera zoom (mouse wheel, - / = keys, gamepad shoulders)")]
        [Tooltip("Zoom change per wheel notch (< 1 zooms in per notch; the camera clamps to its min / max zoom).")]
        [SerializeField, Range(0.5f, 0.99f)] private float zoomStepFactor = 0.85f;
        [Tooltip("Notches per second while a zoom key is held.")]
        [SerializeField, Min(0f)] private float zoomHeldStepsPerSecond = 4f;

        [Header("Stage clear zoom")]
        [SerializeField] private bool clearZoomEnabled = true;
        [Tooltip("Seconds of zoom + V-sign pose before the clear overlay (title, results) appears.")]
        [SerializeField, Min(0f)] private float clearOverlayDelay = 2.0f;
        [SerializeField] private ClearCameraZoom.Settings clearZoom = new ClearCameraZoom.Settings();

        [Header("Perfect dodge")]
        [Tooltip("World time stop (seconds of real time) after dodging a monster / boss attack.")]
        [SerializeField, Min(0.1f)] private float perfectDodgeTimeStop = TimeStop.DefaultDuration;
        [Tooltip("Attacks are ignored this long (game time) after a perfect dodge, so the player can get clear once time resumes.")]
        [SerializeField, Min(0f)] private float perfectDodgeGrace = 0.5f;

        private GameSystems systems;
        private StageData stageData;
        private StageRoot stageRoot;
        private StageSession session;
        private string[] newlyUnlockedNodeIds = System.Array.Empty<string>();
        /// <summary>This clear mastered the stage for the first time (result fireworks + world map master presentation).</summary>
        private bool newlyMastered;
        private bool firstClear;
        private bool gateHeld;
        private bool initialized;
        private StageTutorialDirector tutorial;

        /// <summary>Stage being played (null until initialised).</summary>
        public string StageId => stageData != null ? stageData.StageId : null;
        /// <summary>True only in the Playing state (not Ready, Paused, Dead, Clear, Result or GameOver).</summary>
        public bool IsGameplayActive => initialized && session != null && session.State == StageState.Playing;
        private bool dodgeRewarded;      // the current dodge already triggered a perfect dodge
        private float attackGraceUntil = -1f;

        private void Awake()
        {
            Assert.IsNotNull(player, "InGameController.player is not assigned.");
            Assert.IsNotNull(cameraFollow, "InGameController.cameraFollow is not assigned.");
            Assert.IsNotNull(view, "InGameController.view is not assigned.");

            // Hold the fade-in until the stage scene is loaded (spec 4.2).
            if (GameSystems.Exists)
            {
                GameSystems.Instance.Loader.AcquireReadyGate();
                gateHeld = true;
            }

            // Player is inactive until the stage is placed.
            player.SetControl(false);
            player.Freeze(true);
        }

        private void OnEnable()
        {
            view.PauseClicked += RequestHudPause;
            view.ResumeClicked += Resume;
            view.RetryClicked += Retry;
            view.SettingsClicked += OpenSettings;
            view.ExitClicked += RequestExit;
            view.ResultContinueClicked += OnResultContinue;
            view.GameOverRetryClicked += OnGameOverRetry;
            view.GameOverContinueClicked += OnGameOverContinue;
            view.GameOverTitleClicked += OnGameOverTitle;
        }

        private void OnDisable()
        {
            view.PauseClicked -= RequestHudPause;
            view.ResumeClicked -= Resume;
            view.RetryClicked -= Retry;
            view.SettingsClicked -= OpenSettings;
            view.ExitClicked -= RequestExit;
            view.ResultContinueClicked -= OnResultContinue;
            view.GameOverRetryClicked -= OnGameOverRetry;
            view.GameOverContinueClicked -= OnGameOverContinue;
            view.GameOverTitleClicked -= OnGameOverTitle;
        }

        private void OnDestroy()
        {
            NowCutIn.CanPlay = null;
            TimeStop.Cancel();
            Time.timeScale = 1f; // always restore on scene exit (spec 8)
            if (stageRoot != null)
                UnsubscribeStage();
            ReleaseGate();
        }

        private void ReleaseGate()
        {
            if (gateHeld && GameSystems.Exists)
                GameSystems.Instance.Loader.ReleaseReadyGate();
            gateHeld = false;
        }

        private void Start()
        {
            systems = GameSystems.Instance;
            StartCoroutine(InitRoutine());
        }

        // ---------------------------------------------------------------- init

        private IEnumerator InitRoutine()
        {
            systems.InputMode.SetMode(InputMode.Gameplay);
            var save = systems.Save;
            if (!save.HasSave)
                save.CreateTemporary(systems.StageDb.StartNodeId); // direct play (spec 4.3)

            // Which stage?
            string stageId = systems.Loader.LastRequest?.StageId;
            if (!systems.StageDb.TryGetStage(stageId, out stageData))
            {
                if (Application.isEditor && systems.StageDb.TryGetStage(debugStageId, out stageData))
                {
                    Debug.Log($"[InGame] No valid StageId ('{stageId}'). Using debug stage '{debugStageId}'.");
                }
                else
                {
                    yield return FailAndReturn($"Unknown StageId '{stageId}'.");
                    yield break;
                }
            }

            // Load the stage scene additively (skip if it is already open, e.g. multi-scene editing).
            string sceneName = stageData.SceneName;
            Scene stageScene = SceneManager.GetSceneByName(sceneName);
            if (!stageScene.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    yield return FailAndReturn($"Stage scene '{sceneName}' is not in Build Settings.");
                    yield break;
                }
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    yield return FailAndReturn($"Could not load stage scene '{sceneName}'.");
                    yield break;
                }
                yield return op;
                stageScene = SceneManager.GetSceneByName(sceneName);
            }

            stageRoot = FindStageRoot(stageScene);
            if (stageRoot == null)
            {
                yield return FailAndReturn($"Stage scene '{sceneName}' has no StageRoot.");
                yield break;
            }
            stageRoot.GoalReached += OnGoalReached;
            stageRoot.PlayerHit += OnPlayerHit;
            stageRoot.CoinCollected += OnCoinCollected;
            stageRoot.SpecialCollected += OnSpecialCollected;
            stageRoot.CheckpointActivated += OnCheckpointActivated;
            player.Jumped += OnPlayerJumped;
            player.Landed += OnPlayerLanded;
            player.Bounced += OnPlayerBounced;
            player.Dodged += OnPlayerDodged;
            DodgeCooldownIndicator.Attach(player); // ring over her head while the dodge is on cooldown
            stageRoot.BindPlayer(player.transform);
            stageRoot.BindCamera(cameraFollow);
            // Tutorial hints (TutorialConfig, keyed by stage id). Subscribes after the handlers above, so it sees their outcome.
            tutorial = StageTutorialDirector.Attach(this, stageData.StageId, stageRoot, player, view);

            // Session / HUD
            // Lives belong to one stage attempt cycle: every entry (and every retry) starts with the full count.
            int lives = save.InitialLives;
            session = new StageSession(lives);
            NowCutIn.CanPlay = () => session != null && session.State == StageState.Playing; // not while dying / respawning / cleared
            save.Current.lives = session.Lives;
            view.SetStageName(stageData.DisplayNameKey);
            view.SetLives(session.Lives);
            view.SetCoins(0);
            view.SetSpecials(session.CopySpecials());
            view.SetTime(0f);

            // Place player and camera
            if (stageRoot.HasLevelEdges)
                cameraFollow.SetViewBounds(stageRoot.LevelMinX, stageRoot.LevelMaxX, stageRoot.CameraMinY);
            else
                cameraFollow.SetBounds(stageRoot.CameraMinX, stageRoot.CameraMaxX, stageRoot.CameraMinY);
            cameraFollow.SetTarget(player.transform);
            PlacePlayerAtSpawn();
            cameraFollow.SnapToTarget();

            systems.Audio.PlayBgm(stageData.Bgm);

            initialized = true;
            ReleaseGate();
            RequireTransition(StageState.Ready);
            yield return ReadyRoutine(waitForFade: true);
        }

        private static StageRoot FindStageRoot(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var root = go.GetComponentInChildren<StageRoot>(true);
                if (root != null)
                    return root;
            }
            return null;
        }

        private IEnumerator FailAndReturn(string message)
        {
            Debug.LogError($"[InGame] {message} Returning to the world map.");
            ReleaseGate();
            // Wait for the running transition to finish before requesting a new one.
            while (systems.Loader.IsBusy)
                yield return null;
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.Failed));
        }

        /// <summary>Places the player at the active checkpoint, or at the spawn point when none is active.</summary>
        private void PlacePlayerAtSpawn()
        {
            Vector3 position = stageRoot.SpawnPosition;
            if (stageRoot.TryGetRespawnOverride(out var overridePosition))
                position = overridePosition; // e.g. inside a running boss arena
            else if (session != null && stageRoot.TryGetRespawnPosition(session.CheckpointId, out var checkpointPosition))
                position = checkpointPosition;
            stageRoot.SetPlayerDead(false);
            player.Freeze(false);
            player.TeleportTo(position);
            PlayerAnim?.ResetState();
        }

        private PlayerAnimator playerAnim;
        private PlayerAnimator PlayerAnim => playerAnim != null ? playerAnim : (playerAnim = player != null ? player.GetComponent<PlayerAnimator>() : null);

        private void UnsubscribeStage()
        {
            stageRoot.GoalReached -= OnGoalReached;
            stageRoot.PlayerHit -= OnPlayerHit;
            stageRoot.CoinCollected -= OnCoinCollected;
            stageRoot.SpecialCollected -= OnSpecialCollected;
            stageRoot.CheckpointActivated -= OnCheckpointActivated;
            player.Jumped -= OnPlayerJumped;
            player.Landed -= OnPlayerLanded;
            player.Bounced -= OnPlayerBounced;
            player.Dodged -= OnPlayerDodged;
        }

        // ---------------------------------------------------------------- stage events

        private void OnPlayerHit()
        {
            if (session == null || session.State != StageState.Playing)
                return;
            if (stageRoot.LastHitDodgeable)
            {
                // Monster / boss attacks: harmless while time is stopped and briefly after a perfect dodge.
                if (TimeStop.IsActive || Time.time < attackGraceUntil)
                    return;
                if (player.IsDodging)
                {
                    if (!dodgeRewarded)
                        PerfectDodge();
                    return;
                }
            }
            Die();
        }

        /// <summary>An attack met the player mid-dodge: no damage, and the world stops (TimeStop) while the player moves on.</summary>
        private void PerfectDodge()
        {
            dodgeRewarded = true;
            Voice.Play(VoiceSlot.PerfectDodge); // before the boss cut-in below, whose "Now!" voice may take over
            attackGraceUntil = Time.time + perfectDodgeGrace; // game time: counts from when the world resumes
            TimeStop.Play(player, perfectDodgeTimeStop); // refused during the boss cut-in; the dodge still counts
            stageRoot.NotifyPerfectDodge(); // a boss goes groggy at once, held open until the first stomp
        }

        private void OnPlayerDodged()
        {
            dodgeRewarded = false;
            Voice.Play(VoiceSlot.Dodge);
        }

        private void OnCoinCollected()
        {
            if (session == null)
                return;
            session.AddCoin();
            view.SetCoins(session.Coins);
            Sfx.Play(SfxId.Coin);
        }

        private void OnSpecialCollected(int slot)
        {
            if (session != null && session.CollectSpecial(slot))
            {
                view.SetSpecials(session.CopySpecials());
                Sfx.Play(SfxId.Special);
            }
        }

        private void OnCheckpointActivated(string id, float x)
        {
            if (session != null && session.TryActivateCheckpoint(id, x))
                Sfx.Play(SfxId.Checkpoint);
        }

        private void OnPlayerJumped() => Voice.PlayOr(VoiceSlot.Jump, SfxId.Jump); // the player's own recording, when they made one
        private void OnPlayerLanded() => Sfx.Play(SfxId.Land);
        private void OnPlayerBounced() => Sfx.Play(SfxId.Stomp);

        // ---------------------------------------------------------------- states

        private IEnumerator ReadyRoutine(bool waitForFade)
        {
            player.SetControl(false);
            view.ShowMessage("ingame.ready");
            if (waitForFade)
            {
                while (systems.Loader.IsBusy)
                    yield return null;
            }
            yield return WaitRealtime(readyDuration);
            view.HideMessage();
            if (session.TryTransition(StageState.Playing))
            {
                player.SetControl(true);
                systems.InputMode.SetMode(InputMode.Gameplay);
            }
        }

        /// <summary>Transition that must be valid by construction; logs an error otherwise (no side effects inside Assert).</summary>
        private void RequireTransition(StageState next)
        {
            var from = session.State;
            bool ok = session.TryTransition(next);
            if (!ok)
                Debug.LogError($"[InGame] Invalid state transition {from} -> {next}.");
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void Update()
        {
            view.SetPauseAvailable(initialized && session != null && session.State == StageState.Playing &&
                systems != null && !systems.InputMode.IsLocked && !systems.Popups.IsBlocking && !systems.Loader.IsBusy && !NowCutIn.IsPlaying && !TimeStop.IsActive && !IsTutorialHold);
            if (!initialized || session == null || systems.Loader.IsBusy)
                return;

            var input = systems.InputMode;
            switch (session.State)
            {
                case StageState.Playing:
                    session.Tick(Time.deltaTime);
                    view.SetTime(session.Elapsed);
                    if (!systems.Popups.IsBlocking)
                        UpdateZoom(input);
                    if (input.PausePressed && !systems.Popups.IsBlocking)
                        Pause();
                    else if (player.transform.position.y < stageRoot.KillY)
                        Die();
                    break;

                case StageState.Paused:
                    if ((input.PausePressed || input.CancelPressed) && !systems.Popups.IsBlocking)
                        Resume();
                    break;
            }
        }

        // Player zoom: same scheme as the world map. The chosen zoom survives respawns and retries (CameraFollow keeps it).
        private void UpdateZoom(InputModeService input)
        {
            float steps = input.ZoomWheelSteps + input.ZoomHeld * zoomHeldStepsPerSecond * Time.unscaledDeltaTime;
            if (steps != 0f)
                cameraFollow.ZoomBy(Mathf.Pow(zoomStepFactor, steps));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Auto-pause when the window loses focus (spec 8). Skipped in the Editor where focus flips constantly.
            if (!hasFocus && !Application.isEditor && initialized && session.State == StageState.Playing)
                Pause();
        }

        // ---------------------------------------------------------------- pause

        private void RequestHudPause()
        {
            if (!initialized || session == null || session.State != StageState.Playing ||
                systems == null || systems.InputMode.IsLocked || systems.Popups.IsBlocking || systems.Loader.IsBusy) return;
            Pause();
        }

        // ---------------------------------------------------------------- tutorial hold

        /// <summary>The tutorial explanation is up: world frozen, player input off, pause blocked. State stays Playing.</summary>
        public bool IsTutorialHold { get; private set; }

        /// <summary>
        /// Freezes gameplay for a tutorial explanation. Refused (false) whenever something else owns the clock or the
        /// screen: not Playing, pause, perfect-dodge time stop, boss cut-in, popup, scene load, scripted demo input.
        /// The stage timer stops with it (session.Tick uses scaled time). Always paired with EndTutorialHold.
        /// </summary>
        public bool TryBeginTutorialHold()
        {
            if (IsTutorialHold)
                return true;
            if (!IsGameplayActive || systems == null || systems.Loader.IsBusy || systems.Popups.IsBlocking || systems.InputMode.IsLocked
                || NowCutIn.IsPlaying || TimeStop.IsActive || DemoMode.HidesTutorials || Time.timeScale <= 0f)
                return false;
            IsTutorialHold = true;
            Time.timeScale = 0f;
            player.SetControl(false);
            return true;
        }

        /// <summary>Releases the hold. Safe to call at any time (death, retry, scene exit, destroy).</summary>
        public void EndTutorialHold()
        {
            if (!IsTutorialHold)
                return;
            IsTutorialHold = false;
            // Only undo what the hold did: another owner (pause, time stop, result) may have taken over meanwhile.
            if (session != null && session.State == StageState.Playing && !TimeStop.IsActive && !NowCutIn.IsPlaying)
            {
                Time.timeScale = 1f;
                if (player != null)
                {
                    player.SetControl(false); // drops a jump / dodge pressed while reading
                    player.SetControl(true);
                }
            }
        }

        private void Pause()
        {
            if (IsTutorialHold)
                return; // the explanation owns Time.timeScale; it closes with Confirm or the skip hold
            if (NowCutIn.IsPlaying)
                return; // the cut-in owns Time.timeScale while it plays
            if (TimeStop.IsActive)
                return; // so does a perfect-dodge time stop
            if (!session.TryTransition(StageState.Paused))
                return;
            Time.timeScale = 0f;
            systems.InputMode.SetMode(InputMode.UI);
            view.ShowPause(true);
        }

        private void Resume()
        {
            if (session == null || session.State != StageState.Paused)
                return;
            if (!session.TryTransition(StageState.Playing))
                return;
            Time.timeScale = 1f;
            systems.InputMode.SetMode(InputMode.Gameplay);
            view.ShowPause(false);
        }

        private void Retry()
        {
            if (session == null || session.State != StageState.Paused || systems.Loader.IsBusy)
                return;
            if (!session.TryTransition(StageState.Ready))
                return;
            Time.timeScale = 1f;
            view.ShowPause(false);
            systems.InputMode.SetMode(InputMode.Gameplay);
            session.ResetForRetry(systems.Save.InitialLives);
            if (tutorial != null) tutorial.ResetForAttempt();
            systems.Save.Current.lives = session.Lives;
            view.SetLives(session.Lives);
            view.SetTime(0f);
            view.SetCoins(0);
            view.SetSpecials(session.CopySpecials());
            stageRoot.ResetForAttempt(); // collectibles, checkpoints, defeated enemies, hazards, goal
            PlacePlayerAtSpawn();
            cameraFollow.SnapToTarget();
            StartCoroutine(ReadyRoutine(waitForFade: false));
        }

        private void OpenSettings()
        {
            if (systems.Popups.IsBlocking)
                return;
            systems.Popups.OpenSettings();
        }

        private void RequestExit()
        {
            if (systems.Popups.IsBlocking || systems.Loader.IsBusy)
                return;
            systems.Popups.OpenConfirm("pause.confirm_exit", ExitToWorldMap);
        }

        private void ExitToWorldMap()
        {
            // Keep the tutorial hints already shown (only when this attempt marked some; nothing else changed mid-stage).
            if (tutorial != null && tutorial.HasUnsavedProgress && !systems.Save.Save())
                Debug.LogWarning("[InGame] Could not save tutorial progress on exit.");
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.StageExit));
        }

        // ---------------------------------------------------------------- dead / game over

        private void Die()
        {
            if (DemoMode.Invincible)
                return; // showcase reel only
            if (stageRoot.PlayerDeathBlocked)
            {
                // The boss is already defeated: the player cannot die any more. Out of the world -> back onto the arena floor.
                if (player.transform.position.y < stageRoot.KillY)
                    player.TeleportTo(stageRoot.DeathBlockRescuePosition);
                return;
            }
            TimeStop.Cancel();
            if (!session.TryTransition(StageState.Dead))
                return;
            stageRoot.SetPlayerDead(true); // a boss ignores the body from here on (cleared when she is placed again)
            Voice.PlayOr(VoiceSlot.Death, SfxId.Death);
            bool hasLives = session.ConsumeLife();
            systems.Save.Current.lives = session.Lives;
            view.SetLives(session.Lives);
            player.SetControl(false);
            bool fellOut = player.transform.position.y < stageRoot.KillY;
            if (player.IsGrounded || fellOut)
            {
                player.Freeze(true);
                if (!fellOut)
                    PlayerAnim?.PlayDeath();
                StartCoroutine(DeadRoutine(hasLives));
            }
            else
            {
                // Killed in mid-air: fall in the hurt pose and lie down only once on the ground (or keep falling into a pit).
                PlayerAnim?.PlayAirDeath();
                StartCoroutine(AirDeathRoutine(hasLives));
            }
        }

        private IEnumerator AirDeathRoutine(bool respawn)
        {
            float waited = 0f;
            while (!player.IsGrounded && waited < airDeathMaxFall && player.transform.position.y >= stageRoot.KillY)
            {
                yield return new WaitForFixedUpdate();
                waited += Time.fixedDeltaTime;
            }
            if (player.IsGrounded)
            {
                player.Freeze(true);
                PlayerAnim?.PlayDeath();
            }
            // still airborne (pit / very long drop): keep falling out of view in the hurt pose
            yield return DeadRoutine(respawn);
        }

        private IEnumerator DeadRoutine(bool respawn)
        {
            view.ShowMessage("ingame.dead");
            yield return WaitRealtime(deadDuration);
            view.HideMessage();

            if (respawn)
            {
                RequireTransition(StageState.Ready);
                stageRoot.ResetForRespawn(); // hazards and movers restart; collected items stay collected
                PlacePlayerAtSpawn();
                cameraFollow.SnapToTarget();
                yield return ReadyRoutine(waitForFade: false);
            }
            else
            {
                RequireTransition(StageState.GameOver);
                Time.timeScale = 0f;
                systems.InputMode.SetMode(InputMode.UI);
                view.ShowGameOver();
            }
        }

        private bool CanLeaveGameOver()
        {
            return session != null && session.State == StageState.GameOver
                && !systems.Loader.IsBusy && !systems.Popups.IsBlocking;
        }

        /// <summary>Every game-over exit refills lives so no destination is a dead end.</summary>
        private void RestoreLivesAfterGameOver()
        {
            systems.Save.ResetLives();
            if (!systems.Save.Save())
            {
                Debug.LogError("[InGame] Failed to save after game over.");
                var service=systems.Save;UiFeedback.SaveFailed("ui.save_progress_failed",service.Save);
            }
        }

        private void OnGameOverRetry()
        {
            if (!CanLeaveGameOver())
                return;
            RestoreLivesAfterGameOver();
            // Reload the stage so every stage object starts from a clean scene.
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.InGame, stageData.StageId, LoadReason.StageEnter));
        }

        private void OnGameOverContinue()
        {
            if (!CanLeaveGameOver())
                return;
            RestoreLivesAfterGameOver();
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.GameOver));
        }

        private void OnGameOverTitle()
        {
            if (!CanLeaveGameOver())
                return;
            RestoreLivesAfterGameOver();
            systems.Loader.Load(SceneId.Title);
        }

        // ---------------------------------------------------------------- clear

        private void OnGoalReached()
        {
            if (session == null || !session.TryTransition(StageState.Clear))
                return;
            TimeStop.Cancel();
            stageRoot.HideMonstersForClear();
            Voice.PlayOr(VoiceSlot.StageClear, SfxId.StageClear); // the player's cheer over the fanfare, when they recorded one
            player.SetControl(false); // physics stays on: she lands if airborne, then walks to the gate centre
            StartCoroutine(ClearRoutine());
        }

        [Header("Stage clear walk-in")]
        [SerializeField, Min(0.5f)] private float walkInTimeout = 2.5f;
        [SerializeField, Min(0.01f)] private float walkInArriveDistance = 0.08f;

        /// <summary>Lands, walks to the gate's celebration point, stops, then starts the clear pose and camera.</summary>
        private IEnumerator ClearWalkIn()
        {
            var goal = FindAnyObjectByType<GoalTrigger>(); // inactive on boss stages: stay where she lands
            bool hasGate = goal != null && goal.isActiveAndEnabled;
            Vector3 target = hasGate ? goal.CelebrationPosition : player.transform.position;
            float t = 0f, stuck = 0f, lastX = player.transform.position.x;
            bool cameraStarted = false;
            while (true)
            {
                t += Time.deltaTime;
                float dx = target.x - player.transform.position.x;
                bool grounded = player.IsGrounded;
                if (grounded && !cameraStarted)
                {
                    cameraStarted = true;
                    BeginClearCamera();
                }
                if (!hasGate)
                    dx = 0f;
                float dir = Mathf.Clamp(dx / 1.2f, -1f, 1f);
                if (Mathf.Abs(dx) > walkInArriveDistance)
                    dir = Mathf.Sign(dx) * Mathf.Max(Mathf.Abs(dir), 0.12f);
                else
                    dir = 0f;
                if (grounded)
                    player.SetAutoMove(dir);
                else
                    player.SetAutoMove(0f);
                float vx = Mathf.Abs(player.Velocity.x);
                if (grounded && Mathf.Abs(dx) <= walkInArriveDistance * 1.5f && vx < 0.6f)
                    break;
                stuck = Mathf.Abs(player.transform.position.x - lastX) < 0.002f && Mathf.Abs(dx) > walkInArriveDistance ? stuck + Time.deltaTime : 0f;
                lastX = player.transform.position.x;
                if (stuck > walkInTimeout || t > walkInTimeout + 4f)
                {
                    var p = player.transform.position;
                    p.x = target.x;
                    player.TeleportTo(p);
                    break;
                }
                yield return null;
            }
            player.ClearAutoMove();
            player.Freeze(true);
            if (!cameraStarted)
                BeginClearCamera();
            PlayerAnim?.PlayClear();
        }

        private void BeginClearCamera()
        {
            if (!clearZoomEnabled)
                return;
            var zoom = cameraFollow.GetComponent<ClearCameraZoom>();
            if (zoom == null)
                zoom = cameraFollow.gameObject.AddComponent<ClearCameraZoom>();
            // the visual root stands on the feet; the player pivot is the capsule centre
            zoom.Begin(PlayerAnim != null && PlayerAnim.VisualRoot != null ? PlayerAnim.VisualRoot : player.transform, clearZoom);
        }

        private IEnumerator ClearRoutine()
        {
            // Record and save immediately (spec 5.6 "Clear: record / save").
            var save = systems.Save;
            var previousRecord=save.Current.FindStage(stageData.StageId);
            firstClear = previousRecord == null || !previousRecord.cleared;
            bool newRecord=firstClear||previousRecord.bestTime<=0||session.Elapsed<previousRecord.bestTime;
            var collectedSlots=session.CopySpecials();int collectedCount=0;
            foreach(bool obtained in collectedSlots)if(obtained)collectedCount++;
            var lockedBefore = StageUnlocks.CaptureLocked(systems.StageDb, save.Current);
            bool masteredBefore = StageMastery.IsMastered(stageData, save.Current);
            save.Current.RecordClear(stageData.StageId, session.Elapsed, collectedSlots, session.Coins);
            newlyUnlockedNodeIds = StageUnlocks.NewlyOpened(systems.StageDb, save.Current, lockedBefore);
            newlyMastered = !masteredBefore && StageMastery.IsMastered(stageData, save.Current);
            save.Current.lives = session.Lives;
            if (systems.StageDb.TryGetNodeByStage(stageData.StageId, out var node))
                save.Current.currentNodeId = node.NodeId;
            if (!save.Save())
            {
                Debug.LogError("[InGame] Failed to save stage clear.");
                UiFeedback.SaveFailed("ui.save_progress_failed",save.Save);
            }
            systems.Ranking.ReportClear(stageData.StageId); // online boards: this stage's best time + the overall score

            yield return ClearWalkIn();

            if (clearZoomEnabled && clearOverlayDelay > 0f)
                yield return WaitRealtime(clearOverlayDelay); // the camera zoom and V-sign pose play before the overlay
            float presentationDuration = view.ShowClearCelebration(clearZoomEnabled ? StageClearCelebration.MinimumDuration : clearDuration);
            yield return WaitRealtime(presentationDuration);

            RequireTransition(StageState.Result);
            Time.timeScale = 0f;
            systems.InputMode.SetMode(InputMode.UI);
            view.ShowResult(session.Elapsed,session.Coins,collectedCount,stageData.CollectibleCount,newRecord,newlyMastered);
        }

        private void OnResultContinue()
        {
            if (systems.Loader.IsBusy || systems.Popups.IsBlocking)
                return;
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, stageData.StageId, LoadReason.StageClear, newlyUnlockedNodeIds, newlyMastered, firstClear));
        }
    }
}
