using System;
using System.Collections.Generic;

using System.Text;
using System.IO;
using OpenNFS1.Parsers;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using GameEngine;
using OpenNFS1;
using OpenNFS1.Physics;
using System.Diagnostics;
using Microsoft.Xna.Framework.Input;
using OpenNFS1.Vehicles;

namespace OpenNFS1.Dashboards
{
	class Dashboard
	{
        protected DrivableVehicle _car;
        //protected Texture2D _instrumentLine;
        GearboxAnimation _gearBoxAnimation;
        public bool IsVisible { get; set; }
		DashboardDescription _descriptor;
        RearViewMirror _mirror;  // null when MirrorRect is empty
        
        BitmapEntry Dash, GearGate, GearKnob, Wstr, Wl06, Wl14, Wl22, Wl32, Wl45, Wr06, Wr14, Wr22, Wr32, Wr45;
		BitmapEntry Leather1, Leather2, Leather3;

		static Texture2D _tachLineTexture;
		static SpriteFont _digitalFont;
		static SpriteFont _digitalFontItalic;

		public Dashboard(DrivableVehicle car, DashboardDescription descriptor, Race race)
        {
            _car = car;
			_descriptor = descriptor;
            _car.Motor.Gearbox.GearChangeStarted += new EventHandler(Gearbox_GearChangeStarted);
            
            _gearBoxAnimation = new GearboxAnimation();
            
            //_instrumentLine = Engine.Instance.ContentManager.Load<Texture2D>("Content\\SpeedoLine");
			if (_tachLineTexture == null)
			{
				_tachLineTexture = new Texture2D(Engine.Instance.Device, (int)3, 25);
				Color[] pixels = new Color[_tachLineTexture.Width * _tachLineTexture.Height];
				for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.Red;
				_tachLineTexture.SetData<Color>(pixels);
			}
			if (_digitalFont == null)
				_digitalFont = Engine.Instance.ContentManager.Load<SpriteFont>("Content\\ArialBlack");
			if (_digitalFontItalic == null)
				_digitalFontItalic = Engine.Instance.ContentManager.Load<SpriteFont>("Content\\ArialBlack-Italic");

			// Create the rear-view mirror if this dashboard has a defined mirror rectangle
			// and we have a valid Race object to render the scene from.
			if (race != null && descriptor.MirrorRect.Width > 0 && descriptor.MirrorRect.Height > 0)
				_mirror = new RearViewMirror(car, race, descriptor.MirrorRect);

			FshFile fsh = new FshFile(Path.Combine(@"SIMDATA\DASH", descriptor.Filename));
			var bitmaps = fsh.Header;

            Dash = bitmaps.FindByName("dash");
            GearGate = bitmaps.FindByName("gate");
            GearKnob = bitmaps.FindByName("nob1");

            Leather1 = bitmaps.FindByName("lth1");
            Leather2 = bitmaps.FindByName("lth2");
            Leather3 = bitmaps.FindByName("lth3");

			//steering wheel images, from straight to left, then to the right.  Not all cars have all steering angles
            Wstr = bitmaps.FindByName("wstr");
            Wl06 = bitmaps.FindByName("wl06");
            Wl14 = bitmaps.FindByName("wl14");
            Wl22 = bitmaps.FindByName("wl22");
            Wl32 = bitmaps.FindByName("wl32");
            if (Wl32 == null)
                Wl32 = Wl22;
            Wl45 = bitmaps.FindByName("wl45");
            if (Wl45 == null)
                Wl45 = Wl32;
            Wr06 = bitmaps.FindByName("wr06");
            Wr14 = bitmaps.FindByName("wr14");
            Wr22 = bitmaps.FindByName("wr22");
            Wr32 = bitmaps.FindByName("wr32");
            if (Wr32 == null)
                Wr32 = Wr22;
            Wr45 = bitmaps.FindByName("wr45");
            if (Wr45 == null)
                Wr45 = Wr32;
        }


        void Gearbox_GearChangeStarted(object sender, EventArgs e)
        {
            if (IsVisible)
            {
                _gearBoxAnimation.Current = _car.Motor.Gearbox.CurrentGear + 1;
                _gearBoxAnimation.Next = _car.Motor.Gearbox.NextGear + 1;
            }
        }

		public void Update(GameTime gameTime)
		{
			_gearBoxAnimation.Update(gameTime);
			_mirror?.Update();
			if (Engine.Instance.Input.IsKeyDown(Keys.I))
				_descriptor.SpeedoPosition.Y += Engine.Instance.FrameTime * 20;
			if (Engine.Instance.Input.IsKeyDown(Keys.K))
				_descriptor.SpeedoPosition.Y -= Engine.Instance.FrameTime * 20;
			if (Engine.Instance.Input.IsKeyDown(Keys.J))
				_descriptor.SpeedoPosition.X -= Engine.Instance.FrameTime * 20;
			if (Engine.Instance.Input.IsKeyDown(Keys.L))
				_descriptor.SpeedoPosition.X += Engine.Instance.FrameTime * 20;
			if (Engine.Instance.Input.IsKeyDown(Keys.O))
				_descriptor.TachNeedleLength -= Engine.Instance.FrameTime;

			if (OpenNFS1.GameConfig.DebugLogging)
			    Debug.WriteLine(_descriptor.Filename
				+ "  Tach=(" + (int)_descriptor.TachPosition.X + "," + (int)_descriptor.TachPosition.Y + ")"
				+ "  Speedo=(" + (int)_descriptor.SpeedoPosition.X + "," + (int)_descriptor.SpeedoPosition.Y + ")");
		}

        /// <summary>
        /// Renders the rear-view mirror scene to its render target.
        /// Must be called BEFORE the SpriteBatch.Begin() in DashboardView.Render().
        /// </summary>
        public void RenderMirrorScene()
        {
            _mirror?.RenderScene();
        }

        public void Render()
        {
            // Scale all sprites from the original 640x480 coordinate space.
            Vector2 spriteScale = new Vector2(GameConfig.ScaleX, GameConfig.ScaleY);

            Engine.Instance.SpriteBatch.Draw(Dash.Texture, Dash.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);

            // Draw the mirror texture into the dashboard's black mirror rectangle,
            // AFTER the dashboard background so it sits on top of the black area.
            _mirror?.DrawMirror(Engine.Instance.SpriteBatch);

            if (_gearBoxAnimation.IsAnimating)
            {
                RenderGearstick(spriteScale);
            }

			Color color = new Color(165, 0, 0, 255);
			float rpmFactor = _car.Motor.Rpm / _car.Motor.RedlineRpm;
			// TachPosition is stored in 640x480 space – scale to current resolution.
			Vector2 revCounterPosition = _descriptor.TachPosition * spriteScale;
			float rotation = (float)(rpmFactor * Math.PI * _descriptor.TachRpmMultiplier) - _descriptor.TachIdlePosition;

			RenderSteeringWheel(spriteScale);

			// Tachometer needle (original red 3x25, pivot bottom-centre at (1.5, 25)).
			Engine.Instance.SpriteBatch.Draw(_tachLineTexture, revCounterPosition, null, color, rotation, new Vector2(1.5f, 25), new Vector2(0.8f * GameConfig.ScaleX, _descriptor.TachNeedleLength * GameConfig.ScaleY), SpriteEffects.None, 0);

			// Speedometer — digital readout or needle depending on the car.
			if (_descriptor.SpeedoPosition != Vector2.Zero)
			{
				Vector2 speedoPosition = _descriptor.SpeedoPosition * spriteScale;
				if (_descriptor.IsDigitalSpeedo)
				{
					int speed = Math.Abs((int)_car.Speed);

					if (_descriptor.UseSevenSegment)
					{
						// ── 7-segment LED/LCD display ─────────────────────────────────────
						// SpeedoPosition is the right-centre anchor in 640×480 space.
						// SevenSegmentDisplay works in 640×480 coordinates and scales internally.
						SevenSegmentDisplay.Draw(
							Engine.Instance.SpriteBatch,
							speed,
							_descriptor.SpeedoPosition,          // 640×480 right-centre anchor
							_descriptor.SevenSegmentDigitHeight,
							_descriptor.DigitalFontColor,
							_descriptor.SevenSegmentDimColor);
					}
					else
					{
						// ── ArialBlack font fallback ──────────────────────────────────────
						SpriteFont font      = _descriptor.DigitalFontItalic ? _digitalFontItalic : _digitalFont;
						float      fontScale = 0.75f * _descriptor.DigitalFontScale * GameConfig.ScaleX;
						Color      fontColor = _descriptor.DigitalFontColor;
						string speedText = speed.ToString();
						Vector2 textSize = font.MeasureString(speedText) * fontScale;
						// Right-align: right edge of text sits exactly at speedoPosition.X
						Vector2 drawPos  = new Vector2(speedoPosition.X - textSize.X,
						                               speedoPosition.Y - textSize.Y / 2);
						// Shadow (30% opacity of the font colour, offset 1px)
						Color shadow = new Color(fontColor.R / 6, fontColor.G / 6, fontColor.B / 6, 160);
						Engine.Instance.SpriteBatch.DrawString(font, speedText,
							drawPos + new Vector2(1, 1), shadow, 0, Vector2.Zero, fontScale, SpriteEffects.None, 0);
						Engine.Instance.SpriteBatch.DrawString(font, speedText,
							drawPos, fontColor, 0, Vector2.Zero, fontScale, SpriteEffects.None, 0);
					}
				}
				else
				{
					float speedFactor    = Math.Min(1f, Math.Abs(_car.Speed) / _descriptor.SpeedoMaxKph);
					float speedoRotation = (float)(speedFactor * Math.PI * _descriptor.SpeedoRpmMultiplier) - _descriptor.SpeedoIdlePosition;
					Engine.Instance.SpriteBatch.Draw(_tachLineTexture, speedoPosition, null, color, speedoRotation, new Vector2(1.5f, 25), new Vector2(0.8f * GameConfig.ScaleX, _descriptor.SpeedoNeedleLength * GameConfig.ScaleY), SpriteEffects.None, 0);
				}
			}
        }

        public void RenderGearstick(Vector2 spriteScale)
        {
			Vector2 gatePos = GearGate.GetDisplayAt();
			// Use scaled texture dimensions to find the visual centre of the gate sprite.
			Vector2 gateCenter = gatePos + new Vector2(GearGate.Texture.Width * GameConfig.ScaleX, GearGate.Texture.Height * GameConfig.ScaleY) / 2;
			Engine.Instance.SpriteBatch.Draw(GearGate.Texture, gatePos, null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);

			var gearAnim = _gearBoxAnimation.CurrentPosition * spriteScale;

			// leather gearstick boot
			if (Leather1 != null)
			{	
				Vector2 offset = new Vector2(Leather1.Texture.Width * GameConfig.ScaleX, Leather1.Texture.Height * GameConfig.ScaleY) / 2;
				Engine.Instance.SpriteBatch.Draw(Leather1.Texture, gateCenter - offset + gearAnim * 0.1f, null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
				offset = new Vector2(Leather2.Texture.Width * GameConfig.ScaleX, Leather2.Texture.Height * GameConfig.ScaleY) / 2;
				Engine.Instance.SpriteBatch.Draw(Leather2.Texture, gateCenter - offset + gearAnim * 0.4f, null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
				offset = new Vector2(Leather3.Texture.Width * GameConfig.ScaleX, Leather3.Texture.Height * GameConfig.ScaleY) / 2;
				Engine.Instance.SpriteBatch.Draw(Leather3.Texture, gateCenter - offset + gearAnim * 0.7f, null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
			}

			Vector2 knobOffset = new Vector2(GearKnob.Texture.Width * GameConfig.ScaleX, GearKnob.Texture.Height * GameConfig.ScaleY) / 2;	
			Engine.Instance.SpriteBatch.Draw(GearKnob.Texture, gateCenter - knobOffset + gearAnim, null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
        }

        public void RenderSteeringWheel(Vector2 spriteScale)
        {
            float steeringFactor = _car._steeringWheel / Vehicle.MaxSteeringLock;

            if (steeringFactor < -0.8f)
                Engine.Instance.SpriteBatch.Draw(Wl45.Texture, Wl45.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor < -0.64f)
                Engine.Instance.SpriteBatch.Draw(Wl32.Texture, Wl32.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor < -0.48f)
                Engine.Instance.SpriteBatch.Draw(Wl22.Texture, Wl22.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor < -0.32f)
                Engine.Instance.SpriteBatch.Draw(Wl14.Texture, Wl14.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor < -0.16f)
                Engine.Instance.SpriteBatch.Draw(Wl06.Texture, Wl06.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);

            else if (steeringFactor > 0.8f)
                Engine.Instance.SpriteBatch.Draw(Wr45.Texture, Wr45.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor > 0.64f)
                Engine.Instance.SpriteBatch.Draw(Wr32.Texture, Wr32.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor > 0.48f)
                Engine.Instance.SpriteBatch.Draw(Wr22.Texture, Wr22.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor > 0.32f)
                Engine.Instance.SpriteBatch.Draw(Wr14.Texture, Wr14.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else if (steeringFactor > 0.16f)
                Engine.Instance.SpriteBatch.Draw(Wr06.Texture, Wr06.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
            else
                Engine.Instance.SpriteBatch.Draw(Wstr.Texture, Wstr.GetDisplayAt(), null, Color.White, 0f, Vector2.Zero, spriteScale, SpriteEffects.None, 0f);
        }
	}
}
