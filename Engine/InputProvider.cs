using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;

namespace GameEngine
{
    public enum Actions
    {
        MoveForwardsPrimary,
        MoveForwardsAlternate,
        MoveBackwardsPrimary,
        MoveBackwardsAlternate,
        StrafeRightPrimary,
        StrafeRightAlternate,
        StrafeLeftPrimary,
        StrafeLeftAlternate,
        RunPrimary,
        RunAlternate,
        CrouchPrimary,
        CrouchAlternate,
        JumpPrimary,
        JumpAlternate
    };

    public enum MouseInputMode
    {
        FPS,
        FreeMouse
    }

    public class InputProvider : GameComponent
    {
        private GamePadState _gamePadState, _previousGamePadState;

        private KeyboardState _keyboardState, _previousKeyboardState;
        
        private const float SENSITIVITY = 1.0f;
        
        private float _perFrameMultiplier;
        

        private JoystickProvider _joystick = new JoystickProvider();

        public InputProvider(Game game)
            : base(game)
        {
            _joystick.Initialize();
            // Button debug output writes to joystick_debug.txt and VS Output window.
            // Enable via "debugLogging": true in gameconfig.json.
            _joystick.DebugButtons = false;
        }

        public GamePadState GamePadState
        {
            get { return _gamePadState; }
        }

        /// <summary>True when an XInput gamepad is connected on slot 1.</summary>
        public bool IsGamePadConnected
        {
            get { return _gamePadState.IsConnected; }
        }

        /// <summary>SDL2 joystick provider for non-XInput controllers (PS5 DualSense, DS4, etc.).</summary>
        public JoystickProvider Joystick
        {
            get { return _joystick; }
        }

        public override void Update(GameTime gameTime)
        {
            float frameTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _perFrameMultiplier = frameTime * SENSITIVITY;
            
            _previousKeyboardState = _keyboardState;
            _previousGamePadState = _gamePadState;
            _keyboardState = Keyboard.GetState();
            _gamePadState = GamePad.GetState(PlayerIndex.One);

            if (_previousKeyboardState == null)
                _previousKeyboardState = _keyboardState;

            _joystick.Update();
            
            base.Update(gameTime);
        }

        private Vector2 GetScreenCenter()
        {
            GameWindow window = Engine.Instance.Game.Window;
            return new Vector2(window.ClientBounds.Width / 2, window.ClientBounds.Height / 2);
        }

        public float MoveForward
        {
            get
            {
                if (_gamePadState.ThumbSticks.Left.Y != 0)
                    return _gamePadState.ThumbSticks.Left.Y * _perFrameMultiplier;
                else if (_keyboardState.IsKeyDown(Keys.W))
                    return 1.0f * _perFrameMultiplier;
                else if (_keyboardState.IsKeyDown(Keys.S))
                    return -1.0f * _perFrameMultiplier;
                else
                    return 0.0f;
            }
        }
        public float Strafe
        {
            get
            {
                if (_gamePadState.ThumbSticks.Left.X != 0)
                    return _gamePadState.ThumbSticks.Left.X * _perFrameMultiplier;
                else if (_keyboardState.IsKeyDown(Keys.A))
                    return -1.0f * _perFrameMultiplier;
                else if (_keyboardState.IsKeyDown(Keys.D))
                    return 1.0f * _perFrameMultiplier;
                else
                    return 0.0f;
            }
        }

        public bool WasPressed(Keys key)
        {
            if (_previousKeyboardState.IsKeyDown(key) && !_keyboardState.IsKeyDown(key))
                return true;

            // PS5 DualSense D-pad and face button mappings for menus.
            // These mirror the keyboard arrow keys so all menu screens get
            // controller support without any per-screen changes.
            var joy = _joystick;
            if (!joy.IsConnected) return false;
            switch (key)
            {
                case Keys.Up:     return joy.WasDPadUp();
                case Keys.Down:   return joy.WasDPadDown();
                case Keys.Left:   return joy.WasDPadLeft();
                case Keys.Right:  return joy.WasDPadRight();
                case Keys.Enter:  return joy.WasPressed(joy.ButtonCross);   // Cross = confirm
                // Keys.Escape is NOT mapped to Circle — see UIController.Pause for toggle logic.
                default:          return false;
            }
        }

        public bool WasPressed(Buttons button)
        {
            return _previousGamePadState.IsButtonDown(button) && !_gamePadState.IsButtonDown(button);
        }

        public bool IsKeyDown(Keys key)
        {
            return _keyboardState.IsKeyDown(key);
        }
    }
}
