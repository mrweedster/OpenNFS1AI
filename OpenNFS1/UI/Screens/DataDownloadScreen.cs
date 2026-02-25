// DataDownloadScreen.cs
// Ionic.Zip (DotNetZip) is no longer available as a dependency.
// Automatic CD data download is disabled in this build.
// The screen immediately redirects to HomeScreen.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using GameEngine;

namespace OpenNFS1.UI.Screens
{
	class DataDownloadScreen : BaseUIScreen, IGameScreen
	{
		public DataDownloadScreen() : base() { }

		public void Update(GameTime gameTime)
		{
			// Automatic download not supported — go straight to home screen.
			Engine.Instance.Screen = new HomeScreen();
		}

		public override void Draw()
		{
			base.Draw();
			WriteLine("Automatic data download is not supported in this build.", TextColor, 0, 30, TextSize);
			WriteLine("Please place NFS1 CD data in the CD_Data folder.", TextColor, 20, 30, TextSize);
			Engine.Instance.SpriteBatch.End();
		}
	}
}
