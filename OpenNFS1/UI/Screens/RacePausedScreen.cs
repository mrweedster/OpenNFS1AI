using System;
using System.Collections.Generic;
using System.Text;
using GameEngine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenNFS1.Physics;

namespace OpenNFS1.UI.Screens
{
    class RacePausedScreen : BaseUIScreen, IGameScreen
    {
        DoRaceScreen _currentRace;
        int _selectedOption = 0;
        bool _firstFrame = true;
        bool _circlePrevDown = true;  // start true so the opening press is consumed

        public RacePausedScreen(DoRaceScreen currentRace)
            : base()
        {
            _currentRace = currentRace;
        }

        #region IDrawableObject Members

        public void Update(GameTime gameTime)
        {
			//Engine.Instance.Device.Viewport = FullViewport;

            // Skip input on the very first frame — prevents the Circle press that
            // opened the pause menu from also immediately dismissing it.
            if (_firstFrame) { _firstFrame = false; return; }

            if (UIController.Up && _selectedOption > 0)
                _selectedOption--;
            else if (UIController.Down && _selectedOption < 1)
                _selectedOption++;

            // Dismiss on keyboard Escape (UIController.Back) or a fresh Circle press.
            // Circle is edge-detected manually to avoid WasPressed struct aliasing issues.
            bool circleDown = Engine.Instance.Input.Joystick.IsConnected
                && Engine.Instance.Input.Joystick.IsButtonDown(Engine.Instance.Input.Joystick.ButtonCircle);
            bool circleJustPressed = circleDown && !_circlePrevDown;
            _circlePrevDown = circleDown;

            if (UIController.Back || circleJustPressed)
            {
                Engine.Instance.Screen = _currentRace;
                _currentRace.Resume();
                return;
            }

            if (UIController.Ok)
            {
                if (_selectedOption == 0)
                {
                    Engine.Instance.Screen = _currentRace;
					_currentRace.Resume();
                }
                else
                {
                    Engine.Instance.Screen = new HomeScreen();
                }
            }
        }

		public override void Draw()
		{
			base.Draw();
			
			WriteLine("Race paused", Color.White, 20, 30, TitleSize);
			WriteLine("Continue", _selectedOption == 0, 60, 30, SectionSize);
			WriteLine("Main menu", _selectedOption == 1, 40, 30, SectionSize);

            Engine.Instance.SpriteBatch.End();
        }

        #endregion
    }
}
