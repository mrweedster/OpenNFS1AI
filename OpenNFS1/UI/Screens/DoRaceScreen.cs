using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using GameEngine;
using OpenNFS1.Parsers.Track;
using OpenNFS1.Physics;
using OpenNFS1.UI;
using OpenNFS1.UI.Screens;
using OpenNFS1.Vehicles;
using OpenNFS1.Vehicles.AI;


namespace OpenNFS1
{
	class DoRaceScreen : IGameScreen
	{
		Track _track;
		DrivableVehicle _car;
		bool _circlePrevDown;  // tracks previous frame Circle state for edge detection
		Race _race;
		RaceUI _raceUI;
		PlayerUI _playerUI;
		Viewport _raceViewport, _uiViewport;
		PlayerDriver _playerDriver;

		public DoRaceScreen(Track track)
		{
			_track = track;
			_car = new DrivableVehicle(GameConfig.SelectedVehicle, isPlayer: true);

			_playerDriver = new PlayerDriver(_car);
			_car.AudioEnabled = true;

			_race = new Race(_track.IsOpenRoad ? 1 : 3, _track, _playerDriver);

			// Pick opponents only from the same class as the player's vehicle (including the player's own car).
			var classPool = VehicleDescription.SameClass(GameConfig.SelectedVehicle);
			for (int i = 0; i < 10; i++)
			{
				var opponent = classPool[Engine.Instance.Random.Next(classPool.Count)];
				_race.AddDriver(new RacingAIDriver(opponent));
			}
				//_race.AddDriver(new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "Viper")));
				//_race.AddDriver(new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "Viper")));
				//_race.AddDriver(new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "Viper")));
				//_race.AddDriver(new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "Viper")));
				_playerUI = new PlayerUI(_car);
			/*
			d = new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "911"));
			_aiDrivers.Add(d);
			_track.AddDriver(d);
			d = new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "Viper"));
			_aiDrivers.Add(d);
			_track.AddDriver(d);
			d = new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "Diablo"));
			_aiDrivers.Add(d);
			_track.AddDriver(d);
			d = new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "F512"));
			_aiDrivers.Add(d);
			_track.AddDriver(d);
			d = new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "ZR1"));
			_aiDrivers.Add(d);
			_track.AddDriver(d);
			d = new AIDriver(VehicleDescription.Descriptions.Find(a => a.Name == "NSX"));
			_aiDrivers.Add(d);
			_track.AddDriver(d);
			*/
						
			_raceUI = new RaceUI(_race);
			_race.StartCountdown();

			// Both viewports span the full 640x480 render target.
		// The 3D scene fills the whole target; dashboard/HUD sprites are then drawn
		// on top.  Previously _raceViewport was 640x400, leaving the bottom 80 rows
		// permanently black because the 3D scene never wrote there and no dashboard
		// sprite covers that region either.
		_raceViewport = new Viewport(0, 0, GameConfig.ResX, GameConfig.ResY);
		_uiViewport   = new Viewport(0, 0, GameConfig.ResX, GameConfig.ResY);
		}

		#region IDrawableObject Members

		public void Update(GameTime gameTime)
		{
			Engine.Instance.Device.Viewport = _raceViewport;

			_race.Update();
			TyreSmokeParticleSystem.Instance.Update();
			
			_playerUI.Update(gameTime);

			if (_race.Finished)
			{
				_car.AudioEnabled = false;
				Engine.Instance.Screen = new RaceFinishedScreen(_race, _track);
				return;
			}

			// Pause toggle: Triangle/Y — detected via raw IsButtonDown edge to avoid
			// JoystickState struct aliasing issues with WasPressed.
			bool triangleDown = Engine.Instance.Input.Joystick.IsConnected
				&& Engine.Instance.Input.Joystick.IsButtonDown(Engine.Instance.Input.Joystick.ButtonTriangle);
			bool triangleJustPressed = triangleDown && !_circlePrevDown;
			_circlePrevDown = triangleDown;

			if (UIController.Pause || triangleJustPressed || Engine.Instance.Input.WasPressed(Buttons.Y))
			{
				Pause();
				return;
			}

			if (Engine.Instance.Input.WasPressed(Keys.R))
			{
				_car.Reset();
			}

			// K: toggle car-to-car collision detection on/off at runtime.
			/*if (Engine.Instance.Input.WasPressed(Keys.K))
			{
				GameConfig.VehicleCollisions = !GameConfig.VehicleCollisions;
				GameConsole.WriteLine("Car collisions: " + (GameConfig.VehicleCollisions ? "ON" : "OFF"));
			}
			*/

			TyreSmokeParticleSystem.Instance.SetCamera(Engine.Instance.Camera);
			Engine.Instance.Camera.Update(gameTime);
		}

		public void Pause()
		{
			_car.AudioEnabled = false;
			Engine.Instance.Screen = new RacePausedScreen(this);
		}

		public void Resume()
		{
			_car.AudioEnabled = true;
		}

		public void Draw()
		{
			Engine.Instance.Device.Viewport = _raceViewport;

			_race.Render(_playerUI.ShouldRenderCar);
			TyreSmokeParticleSystem.Instance.Render();

			Engine.Instance.Device.Viewport = _uiViewport;
			_raceUI.Render();
			_playerUI.Render();

			Engine.Instance.Device.Viewport = _raceViewport;
		}

		#endregion
	}
}
