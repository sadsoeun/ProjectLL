using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>Reads gameplay input from InputModeService and forwards it to the motor (input/movement separation).</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private PlayerMotor motor;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
        }

        private void Update()
        {
            if (!GameSystems.Exists)
                return;
            var input = GameSystems.Instance.InputMode;
            motor.SetInput(input.Move.x, input.JumpHeld, input.RunHeld);
            if (input.JumpPressed)
                motor.RequestJump();
            if (input.DodgePressed)
                motor.RequestDodge();
        }
    }
}
