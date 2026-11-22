using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Connects the in-stage tutorial flow (StageTutorialFlow: which step, which phase) to the game: forwards the real
    /// game events (jump, special collect, checkpoint, stomp, perfect dodge), answers the flow's questions about the
    /// player and shows its two views. Explanation phase: gameplay is frozen through InGameController's tutorial hold
    /// (Time.timeScale 0, player control off, pause blocked) and released again on finish, skip, death, retry and destroy.
    /// It changes no damage, camera or reward rule. Created at runtime by InGameController, so stage rebuilds cannot remove it.
    /// </summary>
    public sealed class StageTutorialDirector : MonoBehaviour, ITutorialWorld, ITutorialPresenter
    {
        private InGameController controller;
        private StageRoot stage;
        private PlayerMotor player;
        private InGameView hud;
        private TutorialConfig config;
        private TutorialPromptView prompt;
        private TutorialExplainView explainView;
        private TutorialExplainRunner runner;
        private StageTutorialFlow flow;
        private GameSystems systems;
        private Camera worldCamera;
        private BossController boss;
        private RectTransform hudSlots;
        private const string BossHudPanelPath = "BossHud/Panel";
        private readonly Dictionary<string, Transform> enemies = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> checkpoints = new Dictionary<string, Transform>();
        private readonly Dictionary<int, Transform> specials = new Dictionary<int, Transform>();
        private bool subscribed;

        public string ActiveStepId => flow != null ? flow.ActiveStepId : null;
        public StageTutorialFlow.State Phase => flow != null ? flow.Current : StageTutorialFlow.State.Idle;
        public string ShownText => prompt != null && !prompt.IsIdle ? prompt.MainText : null;

        /// <summary>Steps were marked seen since the stage started. The normal save points (clear, game over) write them;
        /// InGameController also saves on pause -> exit to the world map when this is set.</summary>
        public bool HasUnsavedProgress { get; private set; }

        /// <summary>Adds the director when the stage has tutorial steps; returns null otherwise.</summary>
        public static StageTutorialDirector Attach(InGameController controller, string stageId, StageRoot stage, PlayerMotor player, InGameView hud)
        {
            var config = TutorialConfig.Load();
            var def = config.FindStage(stageId);
            if (def == null || def.steps == null || def.steps.Length == 0 || !GameSystems.Exists)
                return null;
            var go = new GameObject("StageTutorial");
            go.transform.SetParent(controller.transform, false);
            var d = go.AddComponent<StageTutorialDirector>();
            d.Init(controller, stage, player, hud, config, def.steps);
            return d;
        }

        private void Init(InGameController owner, StageRoot root, PlayerMotor motor, InGameView inGameView, TutorialConfig cfg, TutorialStepDef[] defs)
        {
            controller = owner; stage = root; player = motor; hud = inGameView; config = cfg;
            systems = GameSystems.Instance;
            // The stage content lives under several root objects of the stage scene, not under the StageRoot object
            // (StageRoot collects its elements the same way), so search every root of that scene.
            var monsters = new List<Monster>();
            var flags = new List<Checkpoint>();
            var leaves = new List<SpecialCollectible>();
            var bosses = new List<BossController>();
            foreach (var sceneRoot in stage.gameObject.scene.GetRootGameObjects())
            {
                sceneRoot.GetComponentsInChildren(true, monsters);
                foreach (var m in monsters)
                    if (!enemies.ContainsKey(m.name)) enemies[m.name] = m.transform;
                sceneRoot.GetComponentsInChildren(true, flags);
                foreach (var c in flags)
                    if (!string.IsNullOrEmpty(c.Id)) checkpoints[c.Id] = c.transform;
                sceneRoot.GetComponentsInChildren(true, leaves);
                foreach (var s in leaves)
                    specials[s.Slot] = s.transform;
                if (boss == null)
                {
                    sceneRoot.GetComponentsInChildren(true, bosses);
                    if (bosses.Count > 0) boss = bosses[0];
                }
            }
            if (boss == null) boss = FindFirstObjectByType<BossController>();

            hudSlots = hud.transform.Find("SpecialSlots") as RectTransform;
            prompt = TutorialPromptView.Create(hud.transform, config.view, hudSlots, false, config.view.stageFallbackTop);
            explainView = TutorialExplainView.Create(hud.transform, config.explain, false);
            runner = new TutorialExplainRunner(explainView, config.explain, ResolveSpot);
            flow = new StageTutorialFlow(config, defs, this, this);
            Subscribe(true);
        }

        private void OnDestroy()
        {
            Subscribe(false);
            flow = null;
            if (controller != null)
                controller.EndTutorialHold(); // never leave the game frozen (the views may already be gone here)
            if (prompt != null) Destroy(prompt.gameObject);
            if (explainView != null) Destroy(explainView.gameObject);
        }

        private void Subscribe(bool on)
        {
            if (on == subscribed || stage == null || player == null)
                return;
            subscribed = on;
            if (on)
            {
                player.Jumped += OnJumped;
                player.Dodged += OnDodged;
                stage.SpecialCollected += OnSpecial;
                stage.CheckpointActivated += OnCheckpoint;
                stage.EnemyStomped += OnStomped;
                stage.PerfectDodged += OnPerfectDodge;
                stage.PlayerHit += OnPlayerHit;
                stage.GoalReached += OnGoal;
                if (boss != null) boss.Damaged += OnBossDamaged;
            }
            else
            {
                player.Jumped -= OnJumped;
                player.Dodged -= OnDodged;
                stage.SpecialCollected -= OnSpecial;
                stage.CheckpointActivated -= OnCheckpoint;
                stage.EnemyStomped -= OnStomped;
                stage.PerfectDodged -= OnPerfectDodge;
                stage.PlayerHit -= OnPlayerHit;
                stage.GoalReached -= OnGoal;
                if (boss != null) boss.Damaged -= OnBossDamaged;
            }
        }

        /// <summary>Pause-menu Retry: the attempt's checkpoints / collectibles / defeated enemies reset; seen steps stay seen.</summary>
        public void ResetForAttempt()
        {
            if (flow != null) flow.ResetForAttempt();
        }

        // ------------------------------------------------------------------ game events -> flow

        private void OnJumped() => flow.NotifyJumped();
        private void OnDodged() => flow.NotifyDodged();
        private void OnSpecial(int slot) => flow.NotifySpecial(slot);
        private void OnCheckpoint(string id, float x) => flow.NotifyCheckpoint(id);
        private void OnStomped(Monster m) { if (m != null) flow.NotifyStomped(m.name); }
        private void OnPerfectDodge() => flow.NotifyPerfectDodge();
        private void OnGoal() => flow.NotifyGoal();
        private void OnBossDamaged() => flow.NotifyBossDamaged();

        private void OnPlayerHit()
        {
            // Runs after InGameController (subscribed first). Only a hit that actually killed the player is a failed try:
            // a perfect dodge, or a hit ignored during the time stop / grace, leaves the state Playing.
            if (!controller.IsGameplayActive)
                flow.NotifyPlayerKilled();
        }

        private void Update()
        {
            if (flow == null || player == null || !systems.Save.HasSave)
                return;
            flow.Tick();
        }

        // ------------------------------------------------------------------ ITutorialWorld

        private TutorialProgress Progress => systems.Save.HasSave ? systems.Save.Current.Tutorial : null;

        float ITutorialWorld.DeltaTime => Time.deltaTime;
        bool ITutorialWorld.Playing => controller.IsGameplayActive;
        bool ITutorialWorld.Showable => controller.IsGameplayActive && hud.IsHudVisible && !systems.Popups.IsBlocking && !DemoMode.HidesTutorials;
        float ITutorialWorld.PlayerX => player.transform.position.x;
        float ITutorialWorld.PlayerFeetY => player.FeetY;
        bool ITutorialWorld.Grounded => player.IsGrounded;
        bool ITutorialWorld.Steady => player.IsGrounded && !player.IsDodging && !player.IsKnockedBack;
        float ITutorialWorld.MoveInputX => systems.InputMode.Move.x;

        bool ITutorialWorld.RunningFast
        {
            get
            {
                float vx = Mathf.Abs(player.Velocity.x - player.PlatformVelocity.x);
                return player.IsGrounded && systems.InputMode.RunHeld && vx > player.MoveSpeed + 0.4f;
            }
        }

        bool ITutorialWorld.TryGetEnemyX(string id, out float x)
        {
            x = 0f;
            if (string.IsNullOrEmpty(id) || !enemies.TryGetValue(id, out var t) || t == null || !t.gameObject.activeInHierarchy)
                return false;
            x = t.position.x;
            return true;
        }

        private bool BossFighting => boss != null && boss.IsEngaged && boss.Current != BossController.State.Defeat && boss.Current != BossController.State.Done;
        bool ITutorialWorld.BossEngaged => BossFighting;
        bool ITutorialWorld.BossCalm => BossFighting && boss.IsCalmFor(config.bossCalmSeconds)
            && boss.EdgeDistance(player.transform.position.x) >= config.bossMinGap;
        bool ITutorialWorld.BossIntro => BossFighting && boss.IsIntroFor(config.bossIntroDelay);
        bool ITutorialWorld.BossStunned => BossFighting && boss.IsStunOpen && !NowCutIn.IsPlaying;
        int ITutorialWorld.BossPhase => boss != null ? boss.CurrentPhaseIndex : 0;

        /// <summary>The slot was collected in an earlier clear: the leaf is shown as a ghost, no point asking for it.</summary>
        bool ITutorialWorld.SlotOwned(int slot)
        {
            var record = systems.Save.Current.FindStage(controller.StageId);
            return record != null && record.IsCollected(slot);
        }

        bool ITutorialWorld.TryBeginHold() => controller.TryBeginTutorialHold();
        void ITutorialWorld.EndHold() => controller.EndTutorialHold();
        bool ITutorialWorld.IsSeen(string id) => Progress == null || Progress.IsSeen(id);

        void ITutorialWorld.MarkSeen(string id)
        {
            if (Progress != null && Progress.MarkSeen(id))
                HasUnsavedProgress = true;
        }

        /// <summary>"Skip tutorial": every hint counts as seen. Nothing else changes (no clear, collectible or unlock).</summary>
        void ITutorialWorld.SkipAll()
        {
            if (Progress == null)
                return;
            Progress.SkipAll();
            HasUnsavedProgress = true;
        }

        // ------------------------------------------------------------------ ITutorialPresenter

        void ITutorialPresenter.OpenExplain(TutorialStepDef step) => runner.Open(step.explain);
        ExplainResult ITutorialPresenter.PollExplain() => runner.Poll();
        void ITutorialPresenter.CancelExplain() => runner.Cancel();
        void ITutorialPresenter.ShowPrompt(TutorialStepDef step)
        {
            // During a boss fight the HP bar sits where the prompt normally goes (and draws above the HUD): hang the prompt under it.
            var bossPanel = boss != null ? FindHudElement(BossHudPanelPath) : null;
            prompt.SetAnchor(bossPanel != null ? bossPanel : hudSlots);
            prompt.Show(new TutorialPromptContent { textKey = step.textKey, subKey = step.subKey, glyphs = step.glyphs });
        }
        void ITutorialPresenter.ChangePromptText(string textKey) => prompt.ChangeText(textKey, null);
        void ITutorialPresenter.CompletePrompt(float holdSeconds, string successKey) => prompt.Complete(holdSeconds, successKey);
        void ITutorialPresenter.DismissPrompt() => prompt.Dismiss();
        void ITutorialPresenter.HidePrompt() => prompt.HideImmediate();
        bool ITutorialPresenter.PromptIdle => prompt.IsIdle;
        void ITutorialPresenter.SetSuppressed(bool value) => prompt.SetSuppressed(value);

        // ------------------------------------------------------------------ highlight

        /// <summary>HUD child by path; when the first segment is not under the HUD it names a root object (the boss HP bar has its own canvas).</summary>
        private RectTransform FindHudElement(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var found = hud.transform.Find(path) as RectTransform;
            if (found != null)
                return found;
            int slash = path.IndexOf('/');
            var rootObject = GameObject.Find(slash < 0 ? path : path.Substring(0, slash));
            if (rootObject == null)
                return null;
            return (slash < 0 ? rootObject.transform : rootObject.transform.Find(path.Substring(slash + 1))) as RectTransform;
        }

        /// <summary>Hole for a page: its target if it is on screen, else the player, else the screen centre.</summary>
        private SpotShape ResolveSpot(TutorialExplainPage page)
        {
            var overlay = (RectTransform)explainView.transform;
            var settings = config.explain;
            if (page.spot == TutorialSpotKind.HudElement)
            {
                var element = FindHudElement(page.spotId);
                if (TutorialSpot.TryRect(overlay, element, page.radius, settings.rectCornerPixels, out var rect))
                    return rect;
            }
            if (worldCamera == null)
                worldCamera = Camera.main;
            Vector3 playerCentre = player.transform.position + Vector3.up * 0.9f;
            Vector3 offset = new Vector3(page.point.x, page.point.y, 0f);
            Vector3 target = playerCentre;
            switch (page.spot)
            {
                case TutorialSpotKind.Player: target = playerCentre + offset; break;
                case TutorialSpotKind.WorldPoint: target = new Vector3(page.point.x, page.point.y, player.transform.position.z); break;
                case TutorialSpotKind.Enemy:
                    if (!string.IsNullOrEmpty(page.spotId) && enemies.TryGetValue(page.spotId, out var e) && e != null && e.gameObject.activeInHierarchy)
                        target = e.position + offset;
                    break;
                case TutorialSpotKind.Checkpoint:
                    if (!string.IsNullOrEmpty(page.spotId) && checkpoints.TryGetValue(page.spotId, out var c) && c != null)
                        target = c.position + offset;
                    break;
                case TutorialSpotKind.Boss:
                    if (boss != null)
                        target = boss.transform.position + Vector3.up * (boss.BodyHeight * 0.5f) + offset;
                    break;
                case TutorialSpotKind.Special:
                    if (specials.TryGetValue(page.spotSlot, out var s) && s != null && s.gameObject.activeInHierarchy)
                        target = s.position + offset;
                    break;
            }
            float radius = page.spot == TutorialSpotKind.HudElement ? 1.7f : page.radius;
            if (TutorialSpot.TryWorld(overlay, worldCamera, target, radius, settings.minSpotPixels, out var spot))
                return spot;
            if (TutorialSpot.TryWorld(overlay, worldCamera, playerCentre, 1.7f, settings.minSpotPixels, out spot))
                return spot;
            return SpotShape.Circle(Vector2.zero, settings.minSpotPixels * 1.5f);
        }
    }
}
