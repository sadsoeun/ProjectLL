using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace MyGame
{
    public enum InputMode
    {
        /// <summary>Player action map active (movement, jump, world-map confirm).</summary>
        Gameplay,
        /// <summary>Menus. Player map disabled; the UI module reads its own UI actions.</summary>
        UI,
    }

    /// <summary>Device family the player used last; drives on-screen key / button hints.</summary>
    public enum InputDeviceKind { Keyboard, Gamepad }

    /// <summary>Gameplay actions whose bindings can be shown as hints.</summary>
    public enum HintAction
    {
        /// <summary>Stage movement: horizontal parts of Move.</summary>
        MoveHorizontal,
        /// <summary>World map next / previous stage: vertical parts of Move.</summary>
        MoveVertical,
        Jump,
        Run,
        Dodge,
        Confirm,
        Cancel,
    }

    /// <summary>
    /// Central input gate (spec 4.1): switches Player/UI mode and locks all game input during transitions.
    /// The UI action map is consumed by InputSystemUIInputModule on each scene's EventSystem.
    /// Pause/Confirm/Cancel are defined in code because InputSystem_Actions has no such actions.
    /// </summary>
    public sealed class InputModeService : MonoBehaviour
    {
        [Tooltip("Defaults to the project-wide actions (InputSystem_Actions).")]
        [SerializeField] private InputActionAsset actions;

        private InputActionMap playerMap;
        private InputAction move;
        private InputAction jump;
        private InputAction run;
        private InputAction dodge;
        private InputAction pause;
        private InputAction confirm;
        private InputAction cancel;
        private InputAction zoomKeys;
        private int lockCount;

        public InputMode Mode { get; private set; } = InputMode.UI;
        public bool IsLocked => lockCount > 0;

        public void Initialize()
        {
            if (actions == null)
                actions = InputSystem.actions;
            Assert.IsNotNull(actions, "InputModeService has no InputActionAsset (assign InputSystem_Actions).");

            playerMap = actions.FindActionMap("Player", true);
            move = playerMap.FindAction("Move", true);
            jump = playerMap.FindAction("Jump", true);
            run = playerMap.FindAction("Sprint", true); // Ctrl (keyboard), left stick press (gamepad)

            // Dodge has no action in InputSystem_Actions (Shift is free; Ctrl is Sprint).
            dodge = new InputAction("Dodge", InputActionType.Button);
            dodge.AddBinding("<Keyboard>/leftShift");
            dodge.AddBinding("<Keyboard>/rightShift");
            dodge.AddBinding("<Gamepad>/rightTrigger");

            pause = new InputAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            cancel = new InputAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            confirm = new InputAction("Confirm", InputActionType.Button);
            confirm.AddBinding("<Keyboard>/enter");
            confirm.AddBinding("<Keyboard>/numpadEnter");
            confirm.AddBinding("<Keyboard>/space");
            confirm.AddBinding("<Gamepad>/buttonSouth");

            // World map zoom: mouse wheel (one step per notch) and -/= keys or gamepad shoulders (held).
            zoomKeys = new InputAction("ZoomKeys", InputActionType.Value);
            zoomKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/minus").With("Positive", "<Keyboard>/equals");
            zoomKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/numpadMinus").With("Positive", "<Keyboard>/numpadPlus");
            zoomKeys.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/leftShoulder").With("Positive", "<Gamepad>/rightShoulder");

            dodge.Enable();
            pause.Enable();
            zoomKeys.Enable();
            cancel.Enable();
            confirm.Enable();
            Apply();

            ActiveDevice = Keyboard.current == null && Gamepad.current != null ? InputDeviceKind.Gamepad : InputDeviceKind.Keyboard;
            anyButton?.Dispose();
            anyButton = InputSystem.onAnyButtonPress.Call(OnAnyButtonPress);
        }

        // ------------------------------------------------------------------ active device (hints)

        [Tooltip("Gamepad stick deflection that switches the hints to the gamepad.")]
        [SerializeField, Range(0.2f, 0.95f)] private float stickSwitchThreshold = 0.5f;
        private System.IDisposable anyButton;

        /// <summary>Device family used last (keyboard + mouse, or gamepad).</summary>
        public InputDeviceKind ActiveDevice { get; private set; } = InputDeviceKind.Keyboard;
        /// <summary>Raised when <see cref="ActiveDevice"/> changes.</summary>
        public event System.Action<InputDeviceKind> ActiveDeviceChanged;

        private void OnAnyButtonPress(InputControl control)
        {
            var device = control?.device;
            if (device is Gamepad) SetActiveDevice(InputDeviceKind.Gamepad);
            else if (device is Keyboard || device is Mouse) SetActiveDevice(InputDeviceKind.Keyboard);
        }

        private void Update()
        {
            // Sticks are not buttons: switch on a clear deflection.
            var pad = Gamepad.current;
            if (ActiveDevice != InputDeviceKind.Gamepad && pad != null
                && (pad.leftStick.ReadValue().magnitude >= stickSwitchThreshold || pad.rightStick.ReadValue().magnitude >= stickSwitchThreshold))
                SetActiveDevice(InputDeviceKind.Gamepad);
        }

        /// <summary>Also used by editor captures to preview both hint sets.</summary>
        public void SetActiveDevice(InputDeviceKind kind)
        {
            if (kind == ActiveDevice)
                return;
            ActiveDevice = kind;
            ActiveDeviceChanged?.Invoke(kind);
        }

        /// <summary>
        /// Binding paths of the action for the device family, read from the live actions (InputSystem_Actions and the
        /// code-defined Dodge / Confirm), in binding order without duplicates. Composite parts are filtered by axis.
        /// </summary>
        public void GetBindingPaths(HintAction hint, InputDeviceKind kind, List<string> into)
        {
            into.Clear();
            InputAction action;
            string[] parts = null;
            switch (hint)
            {
                case HintAction.MoveHorizontal: action = move; parts = new[] { "left", "right" }; break;
                case HintAction.MoveVertical: action = move; parts = new[] { "up", "down" }; break;
                case HintAction.Jump: action = jump; break;
                case HintAction.Run: action = run; break;
                case HintAction.Dodge: action = dodge; break;
                case HintAction.Cancel: action = cancel; break;
                default: action = confirm; break;
            }
            if (action == null)
                return;
            string prefix = kind == InputDeviceKind.Gamepad ? "<Gamepad>" : "<Keyboard>";
            var bindings = action.bindings;
            // Composite parts are collected per part and emitted set by set (A D, then Left Right), so a hint can
            // group them; a non-composite binding (gamepad stick) is one complete set on its own.
            List<string>[] perPart = parts != null ? new[] { new List<string>(), new List<string>() } : null;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite)
                    continue;
                string path = b.effectivePath;
                if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, System.StringComparison.Ordinal))
                    continue;
                if (b.isPartOfComposite)
                {
                    int part = parts == null ? -1 : System.Array.IndexOf(parts, b.name);
                    if (part >= 0 && !perPart[part].Contains(path))
                        perPart[part].Add(path);
                    continue;
                }
                if (parts != null && kind == InputDeviceKind.Keyboard)
                    continue; // a plain keyboard binding on Move would not be directional
                if (!into.Contains(path))
                    into.Add(path);
            }
            if (perPart == null)
                return;
            if (into.Count > 0)
            {
                // Gamepad: the stick binding is the hint; directional composite parts would only repeat it.
                return;
            }
            int sets = Mathf.Min(perPart[0].Count, perPart[1].Count);
            for (int s = 0; s < sets; s++)
            {
                into.Add(perPart[0][s]);
                into.Add(perPart[1][s]);
            }
        }

        /// <summary>Paths per complete hint set from <see cref="GetBindingPaths"/> (2 for a keyboard direction pair, else 1).</summary>
        public static int PathsPerSet(HintAction hint, InputDeviceKind kind)
            => (hint == HintAction.MoveHorizontal || hint == HintAction.MoveVertical) && kind == InputDeviceKind.Keyboard ? 2 : 1;

        private void OnDestroy()
        {
            anyButton?.Dispose();
            anyButton = null;
            dodge?.Dispose();
            pause?.Dispose();
            cancel?.Dispose();
            confirm?.Dispose();
            zoomKeys?.Dispose();
        }

        public void SetMode(InputMode mode)
        {
            Mode = mode;
            Apply();
        }

        public void PushLock()
        {
            lockCount++;
            Apply();
        }

        public void PopLock()
        {
            Assert.IsTrue(lockCount > 0, "PopLock without matching PushLock.");
            if (lockCount > 0)
                lockCount--;
            Apply();
        }

        private void Apply()
        {
            if (playerMap == null)
                return;
            bool playerOn = !IsLocked && Mode == InputMode.Gameplay;
            if (playerOn && !playerMap.enabled)
                playerMap.Enable();
            else if (!playerOn && playerMap.enabled)
                playerMap.Disable();
        }

        private bool GameplayReady => !IsLocked && Mode == InputMode.Gameplay;

        // DemoMode.ScriptedInput (showcase reel / editor tests only) replaces the device input for the gameplay actions.
        public Vector2 Move => !GameplayReady ? Vector2.zero : DemoMode.ScriptedInput ? new Vector2(DemoMode.MoveX, 0f) : move.ReadValue<Vector2>();
        public bool JumpHeld => GameplayReady && (DemoMode.ScriptedInput ? DemoMode.JumpHeld : jump.IsPressed());
        public bool JumpPressed => GameplayReady && (DemoMode.ScriptedInput ? DemoMode.ConsumeJump() : jump.WasPressedThisFrame());
        public bool RunHeld => GameplayReady && (DemoMode.ScriptedInput ? DemoMode.RunHeld : run.IsPressed());
        public bool DodgePressed => GameplayReady && (DemoMode.ScriptedInput ? DemoMode.ConsumeDodge() : dodge.WasPressedThisFrame());
        public bool ConfirmPressed => GameplayReady && (confirm.WasPressedThisFrame() || DemoMode.ConfirmPressed);
        public bool PausePressed => !IsLocked && pause.WasPressedThisFrame();
        public bool CancelPressed => !IsLocked && cancel.WasPressedThisFrame();
        /// <summary>Cancel held (tutorial skip).</summary>
        public bool CancelHeld => !IsLocked && cancel.IsPressed();

        /// <summary>Wheel notches this frame: +1 per notch toward the player (zoom in), -1 away. 0 outside gameplay.</summary>
        public float ZoomWheelSteps
        {
            get
            {
                if (!GameplayReady)
                    return 0f;
                var mouse = Mouse.current;
                if (mouse == null)
                    return 0f;
                float y = mouse.scroll.y.ReadValue(); // per-frame delta
                // Scroll is reported per notch as 120 (Windows raw) or 1 (normalised) depending on the Input System setting.
                return Mathf.Abs(y) < 0.01f ? 0f : (Mathf.Abs(y) >= 60f ? y / 120f : Mathf.Sign(y));
            }
        }

        /// <summary>Left mouse button pressed this frame (gameplay only; the caller checks for UI under the pointer).</summary>
        public bool PointerClicked => GameplayReady && !DemoMode.ScriptedInput && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        /// <summary>Right mouse button held (world map free look; gameplay only).</summary>
        public bool LookHeld => GameplayReady && !DemoMode.ScriptedInput && Mouse.current != null && Mouse.current.rightButton.isPressed;

        /// <summary>Mouse movement this frame in pixels.</summary>
        public Vector2 LookDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        /// <summary>Mouse position in screen pixels (zero without a mouse).</summary>
        public Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        /// <summary>Held zoom keys: +1 zoom in, -1 zoom out.</summary>
        public float ZoomHeld => GameplayReady ? zoomKeys.ReadValue<float>() : 0f;
    }
}
