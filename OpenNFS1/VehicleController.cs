using System;
using GameEngine;
using Microsoft.Xna.Framework.Input;

namespace OpenNFS1
{
    static class VehicleController
    {
        // ── Dead zones (XInput path) ────────────────────────────────────────────
        // The WinMM/joystick path applies its own dead zones inside JoystickProvider.
        private const float SteeringDeadZone = 0.12f;
        private const float TriggerDeadZone  = 0.05f;

        public static bool ForceBrake { get; set; }

        // ── Throttle ────────────────────────────────────────────────────────────
        public static float Acceleration
        {
            get
            {
                if (ForceBrake) return 0f;
                if (Engine.Instance.Input.IsKeyDown(Keys.Up)) return 1.0f;

                // XInput (Xbox controller)
                if (Engine.Instance.Input.IsGamePadConnected)
                    return NormaliseAxis(Engine.Instance.Input.GamePadState.Triggers.Right, TriggerDeadZone);

                // WinMM fallback (PS5 DualSense, DS4, generic HID gamepads)
                return Engine.Instance.Input.Joystick.GetThrottle();
            }
        }

        // ── Brake ───────────────────────────────────────────────────────────────
        public static float Brake
        {
            get
            {
                if (ForceBrake) return 1.0f;
                if (Engine.Instance.Input.IsKeyDown(Keys.Down)) return 1.0f;

                if (Engine.Instance.Input.IsGamePadConnected)
                    return NormaliseAxis(Engine.Instance.Input.GamePadState.Triggers.Left, TriggerDeadZone);

                return Engine.Instance.Input.Joystick.GetBrake();
            }
        }

        // ── Steering ────────────────────────────────────────────────────────────
        public static float Turn
        {
            get
            {
                if (Engine.Instance.Input.IsKeyDown(Keys.Left))  return -1f;
                if (Engine.Instance.Input.IsKeyDown(Keys.Right)) return  1f;

                float raw;
                if (Engine.Instance.Input.IsGamePadConnected)
                {
                    raw = NormaliseAxis(Engine.Instance.Input.GamePadState.ThumbSticks.Left.X, SteeringDeadZone);
                }
                else
                {
                    // JoystickProvider already applies its own dead zone
                    raw = Engine.Instance.Input.Joystick.GetSteering();
                }

                // Response curve: 60 % linear + 40 % cubic → more precision near centre
                return 0.6f * raw + 0.4f * raw * raw * raw;
            }
        }

        // ── Change camera view ──────────────────────────────────────────────────
        public static bool ChangeView
        {
            get
            {
                if (Engine.Instance.Input.WasPressed(Keys.C)) return true;
                // Xbox: Right Shoulder
                if (Engine.Instance.Input.WasPressed(Buttons.RightShoulder)) return true;
                // PS5: R1 (bit 5)
                if (Engine.Instance.Input.Joystick.WasPressed(Engine.Instance.Input.Joystick.ButtonR1)) return true;
                return false;
            }
        }

        // ── Gear up ─────────────────────────────────────────────────────────────
        public static bool GearUp
        {
            get
            {
                if (Engine.Instance.Input.WasPressed(Keys.A))    return true;
                // Xbox: X button
                if (Engine.Instance.Input.WasPressed(Buttons.X)) return true;
                // Joystick: Square/X (index 2)
                if (Engine.Instance.Input.Joystick.WasPressed(2)) return true;
                return false;
            }
        }

        // ── Gear down ───────────────────────────────────────────────────────────
        public static bool GearDown
        {
            get
            {
                if (Engine.Instance.Input.WasPressed(Keys.Z))    return true;
                // Xbox: A button
                if (Engine.Instance.Input.WasPressed(Buttons.A)) return true;
                // Joystick: Cross/A (index 0)
                if (Engine.Instance.Input.Joystick.WasPressed(0)) return true;
                return false;
            }
        }

        // ── Handbrake ───────────────────────────────────────────────────────────
        public static bool Handbrake
        {
            get
            {
                if (Engine.Instance.Input.IsKeyDown(Keys.Space)) return true;
                // Xbox: B button
                if (Engine.Instance.Input.GamePadState.IsButtonDown(Buttons.B)) return true;
                // Joystick: Circle/B (index 1)
                if (Engine.Instance.Input.Joystick.IsButtonDown(1)) return true;
                return false;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Remove dead zone and re-map the remaining travel to [-1, 1] or [0, 1].
        /// Used for the XInput path only; the WinMM path normalises inside JoystickProvider.
        /// </summary>
        private static float NormaliseAxis(float value, float deadZone)
        {
            float abs = Math.Abs(value);
            if (abs < deadZone) return 0f;
            float sign = value < 0f ? -1f : 1f;
            return sign * Math.Min(1f, (abs - deadZone) / (1f - deadZone));
        }
    }
}
