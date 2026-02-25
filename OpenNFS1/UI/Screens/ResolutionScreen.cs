using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using GameEngine;

namespace OpenNFS1.UI.Screens
{
	/// <summary>
	/// Shown once at startup. The player picks a render resolution with the
	/// arrow keys and confirms with Enter. Game1.ApplyResolution() is called
	/// to resize the backbuffer and render target before the splash screen loads.
	/// </summary>
	class ResolutionScreen : BaseUIScreen, IGameScreen
	{
		// Available resolutions (all 4:3 to match the game's aspect ratio).
		private static readonly (int W, int H, string Label)[] Resolutions =
		{
			(640,  480,  "640 x 480"),
			(800,  600,  "800 x 600"),
			(1024, 768,  "1024 x 768"),
			(1280, 1024, "1280 x 1024"),
		};

		private int _selectedIndex = 0;   // default: 640x480

		public void Update(GameTime gameTime)
		{
			if (Engine.Instance.Input.WasPressed(Keys.Up))
				_selectedIndex = Math.Max(0, _selectedIndex - 1);

			if (Engine.Instance.Input.WasPressed(Keys.Down))
				_selectedIndex = Math.Min(Resolutions.Length - 1, _selectedIndex + 1);

			if (Engine.Instance.Input.WasPressed(Keys.Enter))
			{
				var chosen = Resolutions[_selectedIndex];
				Game1.Instance.ApplyResolution(chosen.W, chosen.H);
				Engine.Instance.Screen = new OpenNFS1SplashScreen();
			}
		}

		public override void Draw()
		{
			base.Draw();

			WriteLine("OpenNFS1", Color.Red, 0, 30, TitleSize);
			WriteLine("Select Resolution", TextColor, 60, 30, SectionSize);
			WriteLine("", Color.Red);

			for (int i = 0; i < Resolutions.Length; i++)
			{
				bool selected = (i == _selectedIndex);
				WriteLine(Resolutions[i].Label, selected, 35, 50, TextSize);
			}

			WriteLine("UP / DOWN   choose resolution", Color.DimGray, 80, 30, TextSize * 0.85f);
			WriteLine("ENTER       confirm",           Color.DimGray, 25, 30, TextSize * 0.85f);

			Engine.Instance.SpriteBatch.End();
		}
	}
}
