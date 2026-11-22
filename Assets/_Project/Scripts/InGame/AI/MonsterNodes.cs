using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace MyGame
{
    // Behavior graph nodes for monsters. Each node is one phase of a pattern and finishes when the phase is over,
    // so a graph reads top to bottom (e.g. Patrol -> Chase -> Reset -> Wait -> repeat).
    // The nodes decide; Monster moves, looks and handles physics. Speeds and senses come from the monster table.

    /// <summary>Walks back and forth inside the patrol zone forever (blind patrollers).</summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Patrol Zone",
        description: "Walks back and forth inside the patrol zone, turning at its ends, walls and ledges. Never ends.",
        story: "[Self] patrols its zone",
        category: "Action/MyGame/Monster",
        id: "3f6a9d2e7c1b4e0a8d5f2c9b6e1a4d70")]
    public partial class PatrolZoneAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        [CreateProperty] private float pauseTimer;
        private Monster monster;

        protected override Status OnStart()
        {
            if (!MonsterNodeUtil.TryGetMonster(this, Self, out monster))
                return Status.Failure;
            MonsterNodeUtil.BeginPatrol(monster, ref pauseTimer);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            MonsterNodeUtil.StepPatrol(monster, ref pauseTimer);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (monster != null)
                monster.Stop();
        }
    }

    /// <summary>Walks back and forth inside the patrol zone. Succeeds as soon as the target is spotted.</summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Patrol Until Target Spotted",
        description: "Walks back and forth inside the patrol zone (turning at its ends, walls and ledges). " +
                     "Succeeds when the target enters the view cone with a clear sight line.",
        story: "[Self] patrols until it spots [Target]",
        category: "Action/MyGame/Monster",
        id: "5b0f3c1e9a7d4e2f8c6b1a0d3e5f7a91")]
    public partial class PatrolUntilTargetSpottedAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        [CreateProperty] private float pauseTimer;
        private Monster monster;

        protected override Status OnStart()
        {
            if (!MonsterNodeUtil.TryGetMonster(this, Self, out monster))
                return Status.Failure;
            MonsterNodeUtil.BeginPatrol(monster, ref pauseTimer);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (monster.CanSpot(Target.Value))
            {
                monster.Stop();
                return Status.Success;
            }
            MonsterNodeUtil.StepPatrol(monster, ref pauseTimer);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (monster != null)
                monster.Stop();
        }
    }

    /// <summary>Runs toward the target while it stays visible. Succeeds when the sight line is lost.</summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Chase While Visible",
        description: "Runs toward the target and stops at walls and ledges. Succeeds when the sight line has been " +
                     "blocked (or the target is out of range) for longer than the monster's lose-sight grace time.",
        story: "[Self] chases [Target] while it can see it",
        category: "Action/MyGame/Monster",
        id: "c27e4a8d1f3b4c6a9e0d2b5f7a1c3e48")]
    public partial class ChaseWhileVisibleAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        [CreateProperty] private float unseenTime;
        private Monster monster;

        protected override Status OnStart()
        {
            if (!MonsterNodeUtil.TryGetMonster(this, Self, out monster))
                return Status.Failure;
            monster.SetMode(Monster.Mode.Chase);
            unseenTime = 0f;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            GameObject target = Target.Value;
            if (monster.CanTrack(target))
            {
                unseenTime = 0f;
            }
            else
            {
                unseenTime += Time.deltaTime;
                if (unseenTime > monster.LoseSightGrace)
                    return Status.Success;
                monster.Stop(); // hesitate while the sight line is briefly blocked
                return Status.Running;
            }

            float dx = target.transform.position.x - monster.transform.position.x;
            if (Mathf.Abs(dx) <= monster.ChaseStopDistance)
            {
                monster.Stop();
                return Status.Running;
            }
            monster.Walk(dx > 0f ? 1 : -1, monster.ChaseSpeed);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (monster != null)
                monster.Stop();
        }
    }

    /// <summary>Clears the chase: stops and returns to the patrol mode. Succeeds at once.</summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Reset Chaser Pattern",
        description: "Stops the monster and puts it back into the patrol mode. The next patrol walks back into its zone.",
        story: "[Self] resets its AI pattern",
        category: "Action/MyGame/Monster",
        id: "8e4d2a6c0b1f4a3e9d7c5b2a1f0e6d34")]
    public partial class ResetChaserPatternAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        protected override Status OnStart()
        {
            if (!MonsterNodeUtil.TryGetMonster(this, Self, out var monster))
                return Status.Failure;
            monster.Stop();
            monster.SetMode(Monster.Mode.Reset);
            return Status.Success;
        }
    }

    internal static class MonsterNodeUtil
    {
        public static bool TryGetMonster(Node node, BlackboardVariable<GameObject> self, out Monster monster)
        {
            monster = null;
            if (self?.Value != null && self.Value.TryGetComponent(out monster))
                return true;
            Debug.LogError($"[{node.GetType().Name}] Self has no Monster component.", self?.Value);
            return false;
        }

        public static void BeginPatrol(Monster monster, ref float pauseTimer)
        {
            monster.SetMode(Monster.Mode.Patrol);
            pauseTimer = 0f;
            // Coming back from a chase outside the zone: head home first.
            float x = monster.transform.position.x;
            if (x > monster.PatrolMaxX)
                monster.Face(-1);
            else if (x < monster.PatrolMinX)
                monster.Face(1);
        }

        /// <summary>One frame of ping-pong patrol: walk, and turn (with a pause) at zone ends, walls and ledges.</summary>
        public static void StepPatrol(Monster monster, ref float pauseTimer)
        {
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
                monster.Stop();
                return;
            }

            float x = monster.transform.position.x;
            bool atEnd = monster.Facing > 0 ? x >= monster.PatrolMaxX : x <= monster.PatrolMinX;
            if (atEnd || monster.BlockedAhead)
            {
                monster.Stop();
                monster.Face(-monster.Facing);
                pauseTimer = monster.TurnPause;
                return;
            }
            monster.Walk(monster.Facing, monster.MoveSpeed);
        }
    }
}
