using System;
using System.Collections.Generic;
using System.Text;
using GameEngine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenNFS1.Parsers.Track;

namespace OpenNFS1.UI.Screens
{
    class RaceFinishedScreen : BaseUIScreen, IGameScreen
    {
        Race _race;
        Texture2D _background;
        Track _raceTrack;

        int _selectedOption = 0;

        public RaceFinishedScreen(Race race, Track raceTrack)
            : base()
        {
            _race = race;
            _raceTrack = raceTrack;
            _background = ScreenEffects.TakeScreenshot();
            GC.Collect(); //force some memory freeness here.
        }


        #region IDrawableObject Members

        public void Update(GameTime gameTime)
        {
            if (UIController.Back)
            {
                Engine.Instance.Screen = new HomeScreen();
            }

            if (UIController.Up && _selectedOption > 0)
                _selectedOption--;
            else if (UIController.Down && _selectedOption < 1)
                _selectedOption++;

            if (UIController.Ok)
            {
                if (_selectedOption == 1)
                    Engine.Instance.Screen = new HomeScreen();
                else
                {
					if (!GameConfig.SelectedTrackDescription.IsOpenRoad)
                        Engine.Instance.Screen = new DoRaceScreen(_raceTrack);
                    else
                    {
                        GameConfig.SelectedTrackDescription = TrackDescription.GetNextOpenRoadStage(GameConfig.SelectedTrackDescription);
                        if (GameConfig.SelectedTrackDescription == null)
                            Engine.Instance.Screen = new HomeScreen();
                        else
                            Engine.Instance.Screen = new LoadRaceScreen();
                    }
                }
            }
        }

		public override void Draw()
		{
			base.Draw();

            Engine.Instance.SpriteBatch.Draw(_background, new Rectangle(0, 0, GameConfig.ResX, GameConfig.ResY), new Color(255, 255, 255, 100));

			if (GameConfig.SelectedTrackDescription.IsOpenRoad)
                DrawOpenRoadResult();
            else
                DrawCircuitResult();

            Engine.Instance.SpriteBatch.End();
        }

        #endregion

        private void DrawCircuitResult()
        {
			WriteLine(GameConfig.SelectedTrackDescription.Name + "- Race completed", Color.Gray, 20, 30, TitleSize);
            
            TimeSpan total = TimeSpan.Zero;
            for (int i = 0; i < _race.PlayerStats.LapTimes.Count; i++)
            {
				var t = _race.PlayerStats.LapTimes[i];
				WriteLine("Lap " + (i + 1) + ": " + FormatLapTime(t));
                total += t;
            }

			WriteLine("Total time: " + FormatLapTime(total));
			
			WriteLine(" Race again", _selectedOption == 0, 60, 30, SectionSize);
			WriteLine(" Main menu", _selectedOption == 1, 30, 30, SectionSize);
        }

        private void DrawOpenRoadResult()
        {
			WriteLine(GameConfig.SelectedTrackDescription.Name + " - Stage completed", Color.Gray, 20, 30, TitleSize);
            
			WriteLine("Time: " + FormatLapTime(_race.PlayerStats.LapTimes[0]));

            if (TrackDescription.GetNextOpenRoadStage(GameConfig.SelectedTrackDescription) != null)
			{
				WriteLine("Continue to next stage", _selectedOption == 0, 60, 30, SectionSize);
            }

			WriteLine(" Main menu", _selectedOption == 1, 30, 30, SectionSize);
        }
		private static string FormatLapTime(TimeSpan t)
		{
			return string.Format("{0}:{1}.{2}",
				t.Minutes.ToString("00"),
				t.Seconds.ToString("00"),
				t.Milliseconds.ToString("000"));
		}

    }
}
