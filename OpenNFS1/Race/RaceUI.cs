using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameEngine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenNFS1.Vehicles.AI;
using OpenNFS1.Parsers;
using OpenNFS1.Physics;

namespace OpenNFS1.UI
{
    class RaceUI
    {
        Race _race;

        // Height of the black HUD bar at the top, in 640x480 baseline pixels.
        // Increase this value to make the bar taller.
        const int HudBarHeight = 30;

        Rectangle _backgroundRectangle;
        Texture2D _backgroundTexture;
        SpriteFont _font;

        public RaceUI(Race race)
        {
            _race = race;

            Color[] pixel = new Color[1] { Color.Black };
            _backgroundTexture = new Texture2D(Engine.Instance.Device, 1, 1);
            _backgroundTexture.SetData<Color>(pixel);
            _font = Engine.Instance.ContentManager.Load<SpriteFont>("Content\\ArialBlack-Italic");
        }

        public void Render()
        {
            Engine.Instance.SpriteBatch.Begin();

            // Pre-compute scale so all positions stay proportional to the render resolution.
            float sx = GameConfig.ScaleX;
            float sy = GameConfig.ScaleY;

            // Recompute every frame so resolution changes take effect immediately.
            _backgroundRectangle = new Rectangle(0, 0, GameConfig.ResX, (int)(HudBarHeight * sy));

            int secondsTillStart = _race.SecondsTillStart;
            if (secondsTillStart > 0)
            {
                Engine.Instance.SpriteBatch.DrawString(_font, _race.SecondsTillStart.ToString(), new Vector2(300 * sx, 50 * sy), Color.Yellow, 0, Vector2.Zero, 1.5f * sx, SpriteEffects.None, 0);
            }
            else if (secondsTillStart == 0)
            {
                Engine.Instance.SpriteBatch.DrawString(_font, "Go!", new Vector2(300 * sx, 50 * sy), Color.Yellow, 0, Vector2.Zero, 1.5f * sx, SpriteEffects.None, 0);
            }
			
            Engine.Instance.SpriteBatch.Draw(_backgroundTexture, _backgroundRectangle, Color.White);
			
            string msg = String.Format("{0}:{1}.{2}", _race.PlayerStats.CurrentLapTime.Minutes.ToString("00"), _race.PlayerStats.CurrentLapTime.Seconds.ToString("00"), _race.PlayerStats.CurrentLapTime.Milliseconds.ToString("000"));
            Engine.Instance.SpriteBatch.DrawString(_font, msg, new Vector2(15 * sx, 0), Color.GreenYellow, 0, Vector2.Zero, 0.6f * sx, SpriteEffects.None, 0);

            msg = String.Format("{0} kph", Math.Abs((int)_race.Player.Vehicle.Speed));
            Engine.Instance.SpriteBatch.DrawString(_font, msg, new Vector2(270 * sx, -5 * sy), Color.WhiteSmoke, 0, Vector2.Zero, 0.8f * sx, SpriteEffects.None, 0);
            Engine.Instance.SpriteBatch.DrawString(_font, msg, new Vector2(271 * sx, -4 * sy), Color.Red,        0, Vector2.Zero, 0.8f * sx, SpriteEffects.None, 0);

            msg = String.Format("G:{0}", GearToString(((DrivableVehicle)_race.Player.Vehicle).Motor.Gearbox.CurrentGear));
            Engine.Instance.SpriteBatch.DrawString(_font, msg, new Vector2(410 * sx, 0), Color.GreenYellow, 0, Vector2.Zero, 0.6f * sx, SpriteEffects.None, 0);

			if (!GameConfig.SelectedTrackDescription.IsOpenRoad)
            {
				msg = String.Format("L:{0}/{1}", Math.Min(_race.PlayerStats.CurrentLap, _race.NbrLaps), _race.NbrLaps);
                Engine.Instance.SpriteBatch.DrawString(_font, msg, new Vector2(480 * sx, 0), Color.GreenYellow, 0, Vector2.Zero, 0.6f * sx, SpriteEffects.None, 0);
            }

			msg = String.Format("P:{0}/{1}", _race.PlayerStats.Position + 1, _race.Drivers.Count(a => a is RacingAIDriver || a is PlayerDriver));
			Engine.Instance.SpriteBatch.DrawString(_font, msg, new Vector2(550 * sx, 0), Color.GreenYellow, 0, Vector2.Zero, 0.6f * sx, SpriteEffects.None, 0);
            Engine.Instance.SpriteBatch.End();
        }

        private string GearToString(int gear)
        {
            if (gear == -1)
                return "R";
            else if (gear == 0)
                return "N";
            else
                return gear.ToString();
        }
    }
}
