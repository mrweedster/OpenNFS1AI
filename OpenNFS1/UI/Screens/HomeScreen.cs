using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using GameEngine;
using OpenNFS1.Parsers;
using OpenNFS1.Parsers.Audio;
using OpenNFS1.Vehicles;

namespace OpenNFS1.UI.Screens
{
	class VehicleUIControl
	{
		public BitmapEntry Bitmap { get; private set; }
		public VehicleDescription Descriptor { get; private set; }

		public VehicleUIControl(VehicleDescription desc)
		{
			Descriptor = desc;
			QfsFile qfs = new QfsFile(@"Frontend\Art\Control\" + desc.UIImageFile);
			Bitmap = qfs.Content.Header.Bitmaps.Find(a => a.Id == "0000");
		}
	}

	class TrackUIControl
	{
		public BitmapEntry Bitmap { get; private set; }
		public TrackDescription Descriptor { get; private set; }

		public TrackUIControl(TrackDescription desc)
		{
			Descriptor = desc;
			QfsFile qfs = new QfsFile(@"Frontend\Art\Control\" + desc.ImageFile);
			Bitmap = qfs.Content.Header.Bitmaps.Find(a => a.Id == "0000");
		}
	}

	enum SelectedControlType
	{
		Vehicle,
		Track
	}

	class HomeScreen : IGameScreen
	{
		BitmapEntry _background, _vehicleSelection, _trackSelection, _raceButtonSelection;

		const int VehicleSelected = 0;
		const int TrackSelected = 1;
		const int RaceButtonSelected = 2;

		List<VehicleUIControl> _vehicles = new List<VehicleUIControl>();
		List<TrackUIControl> _track = new List<TrackUIControl>();
		int _currentVehicle = 2;
		int _currentTrack = 4;
		int _selectedControl = RaceButtonSelected;

		public HomeScreen()
			: base()
		{
			QfsFile qfs = new QfsFile(@"FRONTEND\ART\control\central.qfs");
			_background = qfs.Content.Header.FindByName("bgnd");
			_vehicleSelection = qfs.Content.Header.FindByName("Tlb1");
			_trackSelection = qfs.Content.Header.FindByName("Brb4");
			_raceButtonSelection = qfs.Content.Header.FindByName("Ra1l");

			foreach (var vehicle in VehicleDescription.Descriptions)
			{
				_vehicles.Add(new VehicleUIControl(vehicle));
			}

			foreach (var track in TrackDescription.Descriptions)
			{
				if (!track.HideFromMenu)
				{
					_track.Add(new TrackUIControl(track));
				}
			}

			if (GameConfig.SelectedTrackDescription != null)
				_currentTrack = _track.FindIndex(a => a.Descriptor == GameConfig.SelectedTrackDescription);
			if (GameConfig.SelectedVehicle != null)
				_currentVehicle = _vehicles.FindIndex(a => a.Descriptor == GameConfig.SelectedVehicle);

			if (_currentTrack == -1) _currentTrack = 0;
		}

		public void Update(GameTime gameTime)
		{
			switch (_selectedControl)
			{
				case VehicleSelected:
					if (Engine.Instance.Input.WasPressed(Keys.Left))
						_currentVehicle--; if (_currentVehicle < 0) _currentVehicle = _vehicles.Count-1;
					else if (Engine.Instance.Input.WasPressed(Keys.Right))
						_currentVehicle++; _currentVehicle %= _vehicles.Count;
					break;

				case TrackSelected:
					if (Engine.Instance.Input.WasPressed(Keys.Left))
						_currentTrack--; if (_currentTrack < 0) _currentTrack = _track.Count - 1;
					else if (Engine.Instance.Input.WasPressed(Keys.Right))
						_currentTrack = (_currentTrack + 1) % _track.Count;
					break;
			}

			if (Engine.Instance.Input.WasPressed(Keys.Down))
			{
				_selectedControl++; _selectedControl %= 3;
			}
			else if (Engine.Instance.Input.WasPressed(Keys.Up))
			{
				_selectedControl--; if (_selectedControl < 0) _selectedControl = 2;
			}
			else if (Engine.Instance.Input.WasPressed(Keys.Enter) && _selectedControl == RaceButtonSelected)
			{
				GameConfig.SelectedVehicle = _vehicles[_currentVehicle].Descriptor;
				GameConfig.SelectedTrackDescription = _track[_currentTrack].Descriptor;
				Engine.Instance.Screen = new RaceOptionsScreen();
			}
			else if (Engine.Instance.Input.WasPressed(Keys.Escape))
			{
				Engine.Instance.Game.Exit();
			}
		}

		public void Draw()
		{
			var sb    = Engine.Instance.SpriteBatch;
			var scale = new Vector2(GameConfig.ScaleX, GameConfig.ScaleY);
			var screenRect = new Rectangle(0, 0, GameConfig.ResX, GameConfig.ResY);

			sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

			// Background stretched to fill the full resolution.
			sb.Draw(_background.Texture, screenRect, Color.White);

			// Sprites: position comes from GetDisplayAt() (already scaled), size scaled too.
			DrawScaled(sb, _vehicles[_currentVehicle].Bitmap.Texture,
			           _vehicles[_currentVehicle].Bitmap.GetDisplayAt(), scale);
			DrawScaled(sb, _track[_currentTrack].Bitmap.Texture,
			           _track[_currentTrack].Bitmap.GetDisplayAt(), scale);

			switch (_selectedControl)
			{
				case VehicleSelected:
					DrawScaled(sb, _vehicleSelection.Texture, _vehicleSelection.GetDisplayAt(), scale);
					break;
				case TrackSelected:
					DrawScaled(sb, _trackSelection.Texture, _trackSelection.GetDisplayAt(), scale);
					break;
				case RaceButtonSelected:
					DrawScaled(sb, _raceButtonSelection.Texture, _raceButtonSelection.GetDisplayAt(), scale);
					break;
			}
			sb.End();
		}

		// Helper: draw a sprite at a pre-scaled position with a uniform scale vector.
		static void DrawScaled(SpriteBatch sb, Texture2D tex, Vector2 pos, Vector2 scale)
		{
			sb.Draw(tex, pos, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}
	}
}
