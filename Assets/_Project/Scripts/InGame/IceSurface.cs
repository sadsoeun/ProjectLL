using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Slippery footing (layout blocks with surface "ice", schema v1.5). While the player stands on a collider with this
    /// component, PlayerMotor scales its ground acceleration / deceleration: starting is sluggish and stopping slides.
    /// Jumping and air control are unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceSurface : MonoBehaviour
    {
        [Tooltip("Ground acceleration multiplier while pressing a direction on ice.")]
        [Range(0.02f, 1f)] public float accelerationScale = 0.28f;
        [Tooltip("Ground deceleration multiplier with no input on ice (lower = longer slide).")]
        [Range(0.02f, 1f)] public float decelerationScale = 0.1f;
    }
}
