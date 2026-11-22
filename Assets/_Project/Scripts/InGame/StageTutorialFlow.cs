using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    public enum ExplainResult { Running, Done, Skipped }

    /// <summary>What the tutorial flow needs to know about the game (implemented by StageTutorialDirector; faked in tests).</summary>
    public interface ITutorialWorld
    {
        /// <summary>Scaled frame time: 0 while the game is frozen, so gaps and practice timers only run during play.</summary>
        float DeltaTime { get; }
        bool Playing { get; }
        /// <summary>Playing, HUD visible, no popup, no scripted demo input.</summary>
        bool Showable { get; }
        float PlayerX { get; }
        float PlayerFeetY { get; }
        bool Grounded { get; }
        /// <summary>Grounded and in control (not dodging, not knocked back): an explanation may open.</summary>
        bool Steady { get; }
        float MoveInputX { get; }
        /// <summary>Run held, grounded, faster than walk speed.</summary>
        bool RunningFast { get; }
        bool TryGetEnemyX(string id, out float x);
        /// <summary>A boss fight is running (engaged, not defeated).</summary>
        bool BossEngaged { get; }
        /// <summary>The boss walks between attacks with time to spare and the player is clear of its body: safe to stop and read.</summary>
        bool BossCalm { get; }
        /// <summary>The boss is in its entrance roar (invulnerable, before the first attack) and has been for the intro delay.</summary>
        bool BossIntro { get; }
        /// <summary>The boss is groggy with its weak point open (and no cut-in is playing).</summary>
        bool BossStunned { get; }
        int BossPhase { get; }
        /// <summary>The special slot was already collected in an earlier clear.</summary>
        bool SlotOwned(int slot);
        /// <summary>Freeze gameplay for an explanation; false = not now (something else owns the clock / screen).</summary>
        bool TryBeginHold();
        void EndHold();
        bool IsSeen(string id);
        void MarkSeen(string id);
        void SkipAll();
    }

    /// <summary>What the flow shows (implemented by StageTutorialDirector with the two views; faked in tests).</summary>
    public interface ITutorialPresenter
    {
        void OpenExplain(TutorialStepDef step);
        /// <summary>Runs the pages (input, highlight). Done / Skipped only once the overlay has faded out.</summary>
        ExplainResult PollExplain();
        void CancelExplain();
        void ShowPrompt(TutorialStepDef step);
        void ChangePromptText(string textKey);
        void CompletePrompt(float holdSeconds, string successKey);
        void DismissPrompt();
        void HidePrompt();
        bool PromptIdle { get; }
        void SetSuppressed(bool value);
    }

    /// <summary>
    /// Phase machine of the in-stage tutorial (no Unity objects, so it is testable with fakes).
    /// One step at a time: Explain (game frozen, highlight + bubble, advanced by the player) -> Practice (game runs, small
    /// prompt until the real action) -> success beat. Pacing: an explanation only opens while the player stands steady,
    /// not within minGapSeconds of a completion (unless the step is urgent), and never on top of its practice enemy.
    /// </summary>
    public sealed class StageTutorialFlow
    {
        public enum State { Idle, Explaining, Practice }

        private readonly TutorialConfig config;
        private readonly TutorialStepDef[] steps;
        private readonly ITutorialWorld world;
        private readonly ITutorialPresenter presenter;

        private readonly HashSet<string> explained = new HashSet<string>(); // this stage entry (not shown again after a retry)
        private readonly HashSet<string> activatedCheckpoints = new HashSet<string>();
        private readonly HashSet<int> collectedSlots = new HashSet<int>();
        private readonly HashSet<string> stompedEnemies = new HashSet<string>();

        private int active = -1;
        private float gapLeft, shownTime, startX, runTime, clock, pendingDodgeAt = -1f;
        private bool jumpedSinceShown, helperShown, bossDamaged;
        private int failures;

        public State Current { get; private set; } = State.Idle;
        public string ActiveStepId => active >= 0 ? steps[active].id : null;
        public float GapLeft => gapLeft;

        public StageTutorialFlow(TutorialConfig config, TutorialStepDef[] steps, ITutorialWorld world, ITutorialPresenter presenter)
        {
            this.config = config; this.steps = steps; this.world = world; this.presenter = presenter;
        }

        // ------------------------------------------------------------------ events (only flag state; Tick decides)

        public void NotifyJumped() => jumpedSinceShown = true;
        public void NotifySpecial(int slot) => collectedSlots.Add(slot);
        public void NotifyCheckpoint(string id) => activatedCheckpoints.Add(id);
        public void NotifyStomped(string enemyId) { if (!string.IsNullOrEmpty(enemyId)) stompedEnemies.Add(enemyId); }
        /// <summary>A real head stomp took boss HP.</summary>
        public void NotifyBossDamaged() => bossDamaged = true;

        public void NotifyDodged()
        {
            if (Current == State.Practice && steps[active].goal == TutorialGoal.PerfectDodge && NearTarget(steps[active]))
                pendingDodgeAt = clock;
        }

        public void NotifyPerfectDodge()
        {
            pendingDodgeAt = -1f;
            if (Current == State.Practice && steps[active].goal == TutorialGoal.PerfectDodge)
                Complete();
        }

        /// <summary>The player was actually killed (not a dodged or ignored hit): one failed try of the dodge practice.</summary>
        public void NotifyPlayerKilled()
        {
            if (Current == State.Practice && steps[active].goal == TutorialGoal.PerfectDodge)
            {
                failures++;
                pendingDodgeAt = -1f; // one attempt = one failure (the dodge timeout must not count it again)
            }
        }

        /// <summary>Goal reached: the hint on screen counts as shown.</summary>
        public void NotifyGoal() => Close(true);

        /// <summary>Pause-menu Retry: the attempt's checkpoints / collectibles / defeated enemies reset; seen and explained steps stay.</summary>
        public void ResetForAttempt()
        {
            activatedCheckpoints.Clear();
            collectedSlots.Clear();
            stompedEnemies.Clear();
            gapLeft = 0f;
            Close(false);
        }

        /// <summary>Scene exit / destroy: release everything.</summary>
        public void Shutdown() => Close(false);

        // ------------------------------------------------------------------ tick

        public void Tick()
        {
            if (!world.Playing)
            {
                // Dead, paused, cleared, result: an explanation cannot stay up; a practice prompt is kept (hidden) for the respawn.
                if (Current == State.Explaining)
                    AbortExplain();
                presenter.SetSuppressed(true);
                return;
            }
            presenter.SetSuppressed(!world.Showable);
            float dt = world.DeltaTime;
            clock += dt;
            if (gapLeft > 0f) gapLeft -= dt;
            float x = world.PlayerX;

            switch (Current)
            {
                case State.Explaining:
                {
                    var result = presenter.PollExplain();
                    if (result == ExplainResult.Running)
                        return;
                    world.EndHold();
                    if (result == ExplainResult.Skipped)
                    {
                        world.SkipAll();
                        presenter.HidePrompt();
                        active = -1; Current = State.Idle;
                        return;
                    }
                    var s = steps[active];
                    explained.Add(s.id);
                    if (s.goal == TutorialGoal.ExplainOnly)
                    {
                        world.MarkSeen(s.id);
                        gapLeft = config.minGapSeconds;
                        active = -1; Current = State.Idle;
                    }
                    else
                        BeginPractice(x);
                    return;
                }
                case State.Practice:
                    UpdatePractice(x, dt);
                    if (Current == State.Practice && shownTime >= config.minVisibleSeconds)
                        TryPreempt(x);
                    return;
                default:
                    TryStart(x);
                    return;
            }
        }

        private void AbortExplain()
        {
            presenter.CancelExplain();
            world.EndHold();
            active = -1; Current = State.Idle;
        }

        private void UpdatePractice(float x, float dt)
        {
            var s = steps[active];
            shownTime += dt;
            if (world.IsSeen(s.id))
            {
                Close(false); // marked seen elsewhere (dev cheat, skip): close quietly
                return;
            }
            if (x > s.endX)
            {
                // Passed by without the action: the hint just ends. No success beat, no gap.
                world.MarkSeen(s.id);
                presenter.DismissPrompt();
                active = -1; Current = State.Idle;
                return;
            }
            switch (s.goal)
            {
                case TutorialGoal.IntroTimer:
                    if (shownTime >= s.goalValue || Mathf.Abs(world.MoveInputX) > 0.5f) Complete();
                    break;
                case TutorialGoal.ShowFor:
                    if (shownTime >= s.goalValue) Complete();
                    break;
                case TutorialGoal.MoveDistance:
                    if (Mathf.Abs(x - startX) >= s.goalValue) Complete();
                    break;
                case TutorialGoal.RunFor:
                    runTime = world.RunningFast ? runTime + dt : 0f;
                    if (runTime >= s.goalValue) Complete();
                    break;
                case TutorialGoal.LandInZone:
                    if (jumpedSinceShown && world.Grounded && x >= s.zoneMinX && x <= s.zoneMaxX && world.PlayerFeetY >= s.zoneMinFeetY)
                        Complete();
                    break;
                case TutorialGoal.CollectSpecial:
                    if (collectedSlots.Contains(s.specialSlot)) Complete();
                    break;
                case TutorialGoal.StompEnemy:
                    if (stompedEnemies.Contains(s.targetId)) Complete();
                    break;
                case TutorialGoal.BossStunned:
                    if (world.BossStunned) Complete(); // only ticked while the player is alive: the attack was survived
                    break;
                case TutorialGoal.BossDamaged:
                    if (bossDamaged) Complete();
                    break;
                case TutorialGoal.PerfectDodge:
                    if (pendingDodgeAt >= 0f && clock - pendingDodgeAt > config.dodgeFailWindow)
                    {
                        pendingDodgeAt = -1f;
                        failures++;
                    }
                    // The practice enemy was stomped instead: dodging it is no longer possible.
                    bool targetGone = !string.IsNullOrEmpty(s.targetId) && stompedEnemies.Contains(s.targetId);
                    if (!helperShown && !string.IsNullOrEmpty(s.helperKey)
                        && ((s.helperAfterFailures > 0 && failures >= s.helperAfterFailures) || targetGone))
                    {
                        helperShown = true;
                        presenter.ChangePromptText(s.helperKey);
                    }
                    break;
            }
        }

        /// <summary>Starts the first unseen step whose start condition holds.</summary>
        private void TryStart(float x)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                var s = steps[i];
                if (world.IsSeen(s.id) || !Prerequisite(i))
                    continue;
                if (x > s.endX)
                {
                    world.MarkSeen(s.id); // passed without it: skipping only ends the hint
                    continue;
                }
                if (!Startable(s, x))
                    continue;
                if (AlreadySatisfied(s))
                {
                    world.MarkSeen(s.id); // done out of order (e.g. leaf collected first): no need to ask for it
                    continue;
                }

                // Pacing: wait out the gap after the previous completion (and its success line), unless the step is urgent.
                bool urgent = s.ignoreGap || (s.urgentX > 0f && x >= s.urgentX);
                if (!urgent && (gapLeft > 0f || !presenter.PromptIdle))
                    return;

                bool needsExplain = s.HasExplain && !explained.Contains(s.id);
                if (!needsExplain)
                {
                    if (s.goal == TutorialGoal.ExplainOnly)
                    {
                        world.MarkSeen(s.id); // already explained this entry (retry)
                        continue;
                    }
                    active = i;
                    presenter.HidePrompt();
                    BeginPractice(x);
                    return;
                }
                if (!world.Showable || !world.Steady)
                    return; // mid-air, dodging, knocked back or HUD hidden: wait
                if (IsEnemyGoal(s) && world.TryGetEnemyX(s.targetId, out float ex) && Mathf.Abs(ex - x) < config.minEnemyDistance)
                    return; // too close to the practice enemy to stop and read
                if (!world.TryBeginHold())
                    return;
                active = i;
                Current = State.Explaining;
                presenter.HidePrompt(); // a success line still fading must not sit under the overlay
                presenter.OpenExplain(s);
                return;
            }
        }

        /// <summary>A later step whose start condition holds replaces an unfinished practice prompt (one hint at a time).</summary>
        private void TryPreempt(float x)
        {
            for (int i = active + 1; i < steps.Length; i++)
            {
                var s = steps[i];
                if (world.IsSeen(s.id) || !Prerequisite(i) || !Startable(s, x) || s.start == TutorialStart.StageStart || AlreadySatisfied(s))
                    continue;
                world.MarkSeen(steps[active].id);
                presenter.DismissPrompt();
                active = -1; Current = State.Idle;
                return;
            }
        }

        private bool Prerequisite(int i)
        {
            string after = steps[i].afterStep;
            if (string.IsNullOrEmpty(after) || world.IsSeen(after))
                return true;
            // The prerequisite is not part of this stage: do not block.
            for (int k = 0; k < steps.Length; k++)
                if (steps[k].id == after) return false;
            return true;
        }

        private bool Startable(TutorialStepDef s, float x)
        {
            switch (s.start)
            {
                case TutorialStart.StageStart: return true;
                case TutorialStart.CheckpointActivated: return activatedCheckpoints.Contains(s.targetId);
                case TutorialStart.BossCalm: return world.BossEngaged && world.BossCalm && world.BossPhase >= s.bossPhase;
                case TutorialStart.BossStunned: return world.BossEngaged && world.BossStunned;
                // At the entrance; the calm fallback covers a roar spent entirely in the air.
                case TutorialStart.BossIntro: return world.BossEngaged && (world.BossIntro || world.BossCalm);
                default: return x >= s.startX;
            }
        }

        private static bool IsEnemyGoal(TutorialStepDef s)
            => (s.goal == TutorialGoal.StompEnemy || s.goal == TutorialGoal.PerfectDodge) && !string.IsNullOrEmpty(s.targetId);

        private bool AlreadySatisfied(TutorialStepDef s)
        {
            switch (s.goal)
            {
                case TutorialGoal.CollectSpecial: return collectedSlots.Contains(s.specialSlot) || world.SlotOwned(s.specialSlot);
                case TutorialGoal.StompEnemy: return !string.IsNullOrEmpty(s.targetId) && stompedEnemies.Contains(s.targetId);
                default: return false;
            }
        }

        private bool NearTarget(TutorialStepDef s)
        {
            if (string.IsNullOrEmpty(s.targetId) || !world.TryGetEnemyX(s.targetId, out float ex))
                return true;
            return Mathf.Abs(ex - world.PlayerX) <= config.dodgeTryRadius;
        }

        private void BeginPractice(float x)
        {
            Current = State.Practice;
            shownTime = 0f; runTime = 0f; startX = x; failures = 0; pendingDodgeAt = -1f;
            jumpedSinceShown = false; helperShown = false; bossDamaged = false;
            presenter.ShowPrompt(steps[active]);
        }

        /// <summary>The real action happened: success beat on the prompt, then a quiet gap before the next step.</summary>
        private void Complete()
        {
            if (Current != State.Practice)
                return;
            world.MarkSeen(steps[active].id);
            presenter.CompletePrompt(config.successHoldSeconds, config.successKey);
            gapLeft = config.minGapSeconds;
            active = -1; Current = State.Idle;
        }

        private void Close(bool markSeen)
        {
            if (Current == State.Explaining)
            {
                presenter.CancelExplain();
                world.EndHold();
            }
            else if (Current == State.Practice && markSeen)
                world.MarkSeen(steps[active].id);
            active = -1; Current = State.Idle;
            pendingDodgeAt = -1f;
            presenter.HidePrompt();
        }
    }
}
