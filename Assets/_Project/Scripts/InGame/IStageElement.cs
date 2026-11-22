using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Anything placed in a stage scene that reacts to the attempt lifecycle (collectibles, hazards, movers).
    /// StageRoot discovers all elements of its scene on Awake and binds itself to them.
    /// </summary>
    public interface IStageElement
    {
        void Bind(StageRoot stage);

        /// <summary>After a death: hazards and movers return to their start state; collected items stay collected.</summary>
        void ResetForRespawn();

        /// <summary>Pause-menu Retry: everything returns to the initial state (collectibles, checkpoints, defeated enemies).</summary>
        void ResetForAttempt();
    }

    /// <summary>
    /// A stage element that can take over the respawn position after a death (a running boss fight respawns the player
    /// inside the arena instead of at the checkpoint). Queried after ResetForRespawn.
    /// </summary>
    public interface IRespawnOverride
    {
        bool TryGetRespawnPosition(out Vector3 position);
    }

    /// <summary>
    /// A kinematic platform that carries the player. PlayerMotor reads <see cref="Velocity"/> (the movement of the current
    /// physics step) of the platform it stands on and works in that frame. Implemented by Mover and the boss pillars.
    /// </summary>
    public interface IPlatformMotion
    {
        Vector3 Velocity { get; }
    }
}
