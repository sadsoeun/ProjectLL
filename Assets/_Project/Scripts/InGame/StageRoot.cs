using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>
    /// Entry point of an additive stage scene: spawn point, camera range, fall line, goal event, and the
    /// event hub for the gameplay elements of the stage (coins, specials, checkpoints, hazards).
    /// The InGame scene finds this component after loading the stage and owns the attempt state.
    /// </summary>
    public sealed class StageRoot : MonoBehaviour
    {
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private float cameraMinX = 0f;
        [SerializeField] private float cameraMaxX = 50f;
        [SerializeField] private float cameraMinY = 2f;
        [Tooltip("The player dies when below this Y.")]
        [SerializeField] private float killY = -10f;
        [Tooltip("Left / right end of the course terrain. When set (max > min) the camera keeps its whole view inside this range.")]
        [SerializeField] private float levelMinX;
        [SerializeField] private float levelMaxX;

        private readonly List<IStageElement> elements = new List<IStageElement>();
        private readonly Dictionary<string, Checkpoint> checkpoints = new Dictionary<string, Checkpoint>();
        private bool goalRaised;
        private readonly Dictionary<int, int> channelHolds = new Dictionary<int, int>();

        public Vector3 SpawnPosition => playerSpawnPoint.position;
        public float CameraMinX => cameraMinX;
        public float CameraMaxX => cameraMaxX;
        public float CameraMinY => cameraMinY;
        public float KillY => killY;
        public bool HasLevelEdges => levelMaxX > levelMinX;
        public float LevelMinX => levelMinX;
        public float LevelMaxX => levelMaxX;

        /// <summary>The player (set by InGameController). Used by elements that react to its position.</summary>
        public Transform Player { get; private set; }

        /// <summary>The stage camera (set by InGameController). Null in test setups; boss arenas lock it.</summary>
        public CameraFollow CameraRig { get; private set; }

        public event Action GoalReached;
        /// <summary>A hazard touched the player. The listener decides whether this is a death (state check).</summary>
        public event Action PlayerHit;
        /// <summary>A monster / boss attack met the player mid-dodge (raised by InGameController).</summary>
        public event Action PerfectDodged;
        public event Action CoinCollected;
        public event Action<int> SpecialCollected;
        public event Action<string, float> CheckpointActivated;
        /// <summary>Real stomp damage on a monster (raised by Monster.TakeStomp, not by every bounce). Arg: the monster.</summary>
        public event Action<Monster> EnemyStomped;

        private void Awake()
        {
            Assert.IsNotNull(playerSpawnPoint, "StageRoot.playerSpawnPoint is not assigned.");

            var buffer = new List<IStageElement>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                buffer.Clear();
                root.GetComponentsInChildren(true, buffer);
                elements.AddRange(buffer);
            }
            foreach (var e in elements)
            {
                e.Bind(this);
                if (e is Checkpoint cp)
                {
                    if (checkpoints.ContainsKey(cp.Id))
                        Debug.LogError($"[StageRoot] Duplicate checkpoint id '{cp.Id}'.");
                    checkpoints[cp.Id] = cp;
                }
            }
        }

        public void BindPlayer(Transform player) => Player = player;
        public void BindCamera(CameraFollow cameraFollow) => CameraRig = cameraFollow;

        // ---------------------------------------------------------------- switch channels

        public bool IsChannelRaised(int channel) => channelHolds.TryGetValue(channel, out int n) && n > 0;

        /// <summary>Adds one hold on the channel (a pressed switch). The channel is raised while it has at least one hold.</summary>
        public void AddChannelHold(int channel)
        {
            channelHolds[channel] = (channelHolds.TryGetValue(channel, out int n) ? n : 0) + 1;
        }

        /// <summary>Removes one hold (a momentary switch released).</summary>
        public void RemoveChannelHold(int channel)
        {
            if (!channelHolds.TryGetValue(channel, out int n) || n <= 0)
                return;
            channelHolds[channel] = n - 1;
        }

        public bool TryGetRespawnPosition(string checkpointId, out Vector3 position)
        {
            position = default;
            if (string.IsNullOrEmpty(checkpointId) || !checkpoints.TryGetValue(checkpointId, out var cp))
                return false;
            position = cp.RespawnPosition;
            return true;
        }

        /// <summary>A respawn position forced by an element (running boss fight); false = use the checkpoint / spawn.</summary>
        public bool TryGetRespawnOverride(out Vector3 position)
        {
            foreach (var e in elements)
            {
                if (e is IRespawnOverride o && o.TryGetRespawnPosition(out position))
                    return true;
            }
            position = default;
            return false;
        }

        /// <summary>True when the hit being reported is a monster / boss attack that a dodge can evade (perfect dodge).</summary>
        public bool LastHitDodgeable { get; private set; }

        /// <summary>Reports a hit. <paramref name="dodgeable"/>: monster / boss attacks (true) vs. stage hazards like spikes (false).</summary>
        public void NotifyPlayerHit(bool dodgeable = false)
        {
            LastHitDodgeable = dodgeable;
            PlayerHit?.Invoke();
            LastHitDodgeable = false;
        }
        public void NotifyPerfectDodge() => PerfectDodged?.Invoke();

        /// <summary>The player is dead until the respawn (set by InGameController): a boss ignores every contact meanwhile, so a falling body never lands a hit.</summary>
        public bool PlayerDead { get; private set; }
        public void SetPlayerDead(bool dead) => PlayerDead = dead;

        /// <summary>A boss went down: nothing kills the player any more (the stage clear follows). Cleared on Retry.</summary>
        public bool PlayerDeathBlocked { get; private set; }
        /// <summary>Where a player who falls out of the stage while deaths are blocked is put back.</summary>
        public Vector3 DeathBlockRescuePosition { get; private set; }
        public void BlockPlayerDeath(Vector3 rescuePosition)
        {
            PlayerDeathBlocked = true;
            DeathBlockRescuePosition = rescuePosition;
        }
        public void NotifyCoin() => CoinCollected?.Invoke();
        public void NotifySpecial(int slot) => SpecialCollected?.Invoke(slot);
        public void NotifyCheckpoint(string id, float x) => CheckpointActivated?.Invoke(id, x);
        public void NotifyEnemyStomped(Monster monster) => EnemyStomped?.Invoke(monster);

        /// <summary>After a death: hazards and movers reset; collected items and defeated enemies stay as they are.</summary>
        public void ResetForRespawn()
        {
            foreach (var e in elements)
                e.ResetForRespawn();
        }

        /// <summary>Retry: the whole stage returns to its initial state.</summary>
        public void ResetForAttempt()
        {
            channelHolds.Clear();
            PlayerDeathBlocked = false;
            PlayerDead = false;
            foreach (var e in elements)
                e.ResetForAttempt();
            ResetGoal();
        }

        /// <summary>Called by GoalTrigger. Raised once.</summary>
        public void NotifyGoal()
        {
            if (goalRaised)
                return;
            goalRaised = true;
            GoalReached?.Invoke();
        }

        /// <summary>Stage clear: every monster disappears so none walks through the celebration.</summary>
        public void HideMonstersForClear()
        {
            foreach (var e in elements)
                if (e is Monster monster && monster != null)
                    monster.HideForClear();
        }

        /// <summary>Allows the goal to fire again (stage retry).</summary>
        public void ResetGoal() => goalRaised = false;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(cameraMinX - 50f, killY, 0f), new Vector3(cameraMaxX + 50f, killY, 0f));
        }
    }
}
