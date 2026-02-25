using System;
using Microsoft.Xna.Framework.Input;

namespace GameEngine
{
    /// <summary>
    /// Wraps MonoGame's built-in Joystick API (SDL2) for non-XInput controllers.
    ///
    /// ── PS5 DualSense USB button layout (SDL2 raw) ─────────────────────────
    ///   0 Cross       1 Circle    2 Square    3 Triangle
    ///   4 L1          5 R1        6 L2(dig.)  7 R2(dig.)
    ///   8 Create      9 Options  10 L3       11 R3
    ///  12 PS         13 Touchpad
    ///  D-pad as buttons: 11=Up  12=Down  13=Left  14=Right  (verify with DebugButtons)
    ///
    /// ── Xbox 360 USB button layout (SDL2 raw) ──────────────────────────────
    ///   0 A           1 B         2 X         3 Y
    ///   4 LB          5 RB        6 Back      7 Start
    ///   8 L3          9 R3
    ///   D-pad: HAT 0 (Up/Down/Left/Right) — NOT discrete buttons
    ///
    /// ── Axis layout (both controllers) ────────────────────────────────────
    ///   Index 0  Left stick X   -1 … +1
    ///   Index 1  Left stick Y   -1 … +1
    ///   Index 2  L2 / LT        -1 (rest) … +1 (full)
    ///   Index 3  R2 / RT        -1 (rest) … +1 (full)
    ///   Index 4  Right stick X  -1 … +1
    ///   Index 5  Right stick Y  -1 … +1
    /// </summary>
    public class JoystickProvider
    {
        // Axis indices
        private const int SteeringAxis = 0;
        private const int BrakeAxis    = 2;
        private const int ThrottleAxis = 3;

        // Dead zones
        private const float StickDeadZone   = 0.12f;
        private const float TriggerDeadZone = 0.05f;

        // ── Button index fields ───────────────────────────────────────────────
        public int ButtonCross    = 0;   // Xbox A  / PS5 Cross
        public int ButtonCircle   = 1;   // Xbox B  / PS5 Circle
        public int ButtonSquare   = 2;   // Xbox X  / PS5 Square
        public int ButtonTriangle = 3;   // Xbox Y  / PS5 Triangle
        public int ButtonL1       = 4;   // Xbox LB / PS5 L1
        public int ButtonR1       = 5;   // Xbox RB / PS5 R1

        // PS5 D-pad discrete button indices (Xbox D-pad uses HAT — see WasDPad*)
        public int ButtonDPadUp    = 11;
        public int ButtonDPadDown  = 12;
        public int ButtonDPadLeft  = 13;
        public int ButtonDPadRight = 14;

        // ── State ─────────────────────────────────────────────────────────────
        // We deep-copy the button/hat arrays on every Update() to avoid the
        // struct reference aliasing bug: JoystickState.Buttons is a reference
        // type inside a struct, so _prev = _state shares the same array pointer.
        private int           _index      = -1;
        private JoystickState _rawState;

        private ButtonState[] _currentButtons  = Array.Empty<ButtonState>();
        private ButtonState[] _previousButtons = Array.Empty<ButtonState>();

        private JoystickHat _currentHat;
        private JoystickHat _previousHat;

        public bool IsConnected { get; private set; }

        /// <summary>Print pressed button indices every frame for controller mapping.</summary>
        public bool DebugButtons = false;

        // ── Initialisation ────────────────────────────────────────────────────

        public void Initialize()
        {
            IsConnected = false;
            _index      = -1;
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    if (Joystick.GetCapabilities(i).IsConnected)
                    {
                        _index      = i;
                        IsConnected = true;
                        return;
                    }
                }
                catch { }
            }
        }

        // ── Per-frame update ──────────────────────────────────────────────────

        public void Update()
        {
            if (_index < 0) return;

            // Snapshot PREVIOUS arrays before reading new state
            _previousButtons = _currentButtons;
            _previousHat     = _currentHat;

            try
            {
                _rawState   = Joystick.GetState(_index);
                IsConnected = _rawState.IsConnected;
            }
            catch
            {
                IsConnected = false;
            }

            if (!IsConnected) { _index = -1; return; }

            // Deep-copy buttons so current and previous never share a reference
            var rawBtns = _rawState.Buttons;
            if (rawBtns != null)
            {
                _currentButtons = new ButtonState[rawBtns.Length];
                Array.Copy(rawBtns, _currentButtons, rawBtns.Length);
            }
            else
            {
                _currentButtons = Array.Empty<ButtonState>();
            }

            // Hat 0 — Xbox 360 D-pad
            var hats = _rawState.Hats;
            _currentHat = (hats != null && hats.Length > 0) ? hats[0] : new JoystickHat();

            if (DebugButtons) PrintDebugButtons();
        }

        public void PrintDebugButtons()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _currentButtons.Length; i++)
            {
                if (_currentButtons[i] == ButtonState.Pressed)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(i);
                }
            }
            if (_currentHat.Up    == ButtonState.Pressed) { if (sb.Length > 0) sb.Append(", "); sb.Append("Hat-Up"); }
            if (_currentHat.Down  == ButtonState.Pressed) { if (sb.Length > 0) sb.Append(", "); sb.Append("Hat-Down"); }
            if (_currentHat.Left  == ButtonState.Pressed) { if (sb.Length > 0) sb.Append(", "); sb.Append("Hat-Left"); }
            if (_currentHat.Right == ButtonState.Pressed) { if (sb.Length > 0) sb.Append(", "); sb.Append("Hat-Right"); }

            string msg = sb.Length > 0
                ? "[Joystick] Pressed: " + sb
                : "[Joystick] No buttons pressed";

            System.Console.WriteLine(msg);
            System.Diagnostics.Debug.WriteLine(msg);
            try { System.IO.File.AppendAllText("joystick_debug.txt", msg + "\n"); } catch { }
        }

        // ── Axis accessors ────────────────────────────────────────────────────

        public float GetSteering()
        {
            if (!IsConnected) return 0f;
            return ApplyDeadZone(SafeAxis(SteeringAxis), StickDeadZone);
        }

        public float GetThrottle()
        {
            if (!IsConnected) return 0f;
            return ApplyDeadZone((SafeAxis(ThrottleAxis) + 1f) / 2f, TriggerDeadZone);
        }

        public float GetBrake()
        {
            if (!IsConnected) return 0f;
            return ApplyDeadZone((SafeAxis(BrakeAxis) + 1f) / 2f, TriggerDeadZone);
        }

        // ── Button accessors ──────────────────────────────────────────────────

        /// <summary>True while the button is held this frame.</summary>
        public bool IsButtonDown(int index)
        {
            if (!IsConnected || index < 0 || index >= _currentButtons.Length) return false;
            return _currentButtons[index] == ButtonState.Pressed;
        }

        /// <summary>True on the single frame the button transitions Released to Pressed.</summary>
        public bool WasPressed(int index)
        {
            if (!IsConnected || index < 0) return false;
            if (index >= _currentButtons.Length || index >= _previousButtons.Length) return false;
            return _previousButtons[index] == ButtonState.Released
                && _currentButtons[index]  == ButtonState.Pressed;
        }

        // ── D-pad — checks BOTH hat (Xbox) and discrete buttons (PS5) ────────

        public bool WasDPadUp()
            => WasHat(_previousHat.Up,    _currentHat.Up)    || WasPressed(ButtonDPadUp);

        public bool WasDPadDown()
            => WasHat(_previousHat.Down,  _currentHat.Down)  || WasPressed(ButtonDPadDown);

        public bool WasDPadLeft()
            => WasHat(_previousHat.Left,  _currentHat.Left)  || WasPressed(ButtonDPadLeft);

        public bool WasDPadRight()
            => WasHat(_previousHat.Right, _currentHat.Right) || WasPressed(ButtonDPadRight);

        private static bool WasHat(ButtonState prev, ButtonState cur)
            => prev == ButtonState.Released && cur == ButtonState.Pressed;

        // ── Helpers ───────────────────────────────────────────────────────────

        private float SafeAxis(int index)
        {
            var axes = _rawState.Axes;
            if (axes == null || axes.Length <= index) return 0f;
            return Math.Max(-1f, axes[index] / 32768f);
        }

        private static float ApplyDeadZone(float value, float dz)
        {
            float abs = Math.Abs(value);
            if (abs < dz) return 0f;
            float sign = value < 0f ? -1f : 1f;
            return sign * (abs - dz) / (1f - dz);
        }
    }
}
