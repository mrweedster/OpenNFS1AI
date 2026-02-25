
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GameEngine;
using OpenNFS1.Parsers.Track;
using OpenNFS1.Tracks;
using OpenNFS1.Vehicles;


namespace OpenNFS1.Physics
{

	class DrivableVehicle : Vehicle
	{
		const float CarFrictionOnRoad = 14;
		const float AirFrictionPerSpeed = 0.07f;
		const float MaxAirFriction = AirFrictionPerSpeed * 100.0f;
		// for reflecting away from walls
		public float MaxRotationPerSec = 5f;
		public float RearSlipFactorSpeed = 1.6f;
		public float RearSlipFactorMax = 0.5f;
		public float FrontSlipMultiplier = 0.0000017f;

		public VehicleDescription Descriptor { get; private set; }

		public float BrakePower = 70;
		public float HandbrakePower = 40;  // kept for reference; runtime drag uses HandbrakePeakPower

		// ── Realistic handbrake constants ────────────────────────────────────────
		// Minimum speed (km/h) before rear yaw initiates. Below this the rear just skids.
		public float HandbrakeMinSpeed       = 20f;
		// How fast slip builds per second (scaled by steering magnitude and speed ratio).
		public float HandbrakeSlipBuildRate  = 2.5f;
		// Speed at which build rate is 1× — above this it scales proportionally faster.
		public float MaxYawReferenceSpeed    = 120f;
		// Peak drag when rear wheels first lock. Fades over HandbrakeMaxLockTime.
		public float HandbrakePeakPower      = 50f;
		// After this many seconds locked the drag fades to 60% of peak (tyres polish).
		public float HandbrakeMaxLockTime    = 2.0f;
		// Baseline decay rate when slip is small.
		public float RearSlipDecayBase       = 0.8f;
		// Additional decay per unit of slip magnitude (larger slide = slower recovery).
		public float RearSlipDecayMomentumScale = 2.0f;

		// ── Handbrake runtime state ──────────────────────────────────────────────
		float _handbrakeHeldTime = 0f;   // seconds rear wheels have been locked this press
		int   _lastSteeringSign  = 1;    // last non-zero steering direction, for straight-line slides

		public Motor Motor { get; private set; }
		public Vector3 RenderDirection;
		public Spring BodyPitch { get; private set; }
		public Spring BodyRoll { get; private set; }

		private float _rotateAfterCollision;
		public float RotateCarAfterCollision
		{
			set
			{
				_rotateAfterCollision = value;
				_rotationChange = 0;
			}
		}
		public VehicleWheel[] Wheels { get; private set; }

		float _frontSlipFactor, _rearSlipFactor;		
		Vector3 _force;	
		VehicleAudioProvider _audioProvider;
		int _nbrWheelsOffRoad;
		float _traction = 520;
		
		// inputs
		public float ThrottlePedalInput, BrakePedalInput;
		public bool GearUpInput, GearDownInput, HandbrakeInput;
		public bool AutoDrift;  //used for ai racers

		public DrivableVehicle(VehicleDescription desc, bool isPlayer = false)
			: base(desc.ModelFile)
		{
			Descriptor = desc;
			float offset = VehicleWheel.Width / 2 - 0.1f;
			Wheels = new VehicleWheel[4];
			Wheels[0] = new VehicleWheel(this, _model.LeftFrontWheelPos,  _model.FrontWheelSize, _model.LeftFrontWheelTexture,  offset);
			Wheels[1] = new VehicleWheel(this, _model.RightFrontWheelPos, _model.FrontWheelSize, _model.RightFrontWheelTexture, -offset);
			Wheels[2] = new VehicleWheel(this, _model.LeftRearWheelPos,   _model.RearWheelSize,  _model.LeftRearWheelTexture,   offset);
			Wheels[3] = new VehicleWheel(this, _model.RightRearWheelPos,  _model.RearWheelSize,  _model.RightRearWheelTexture,  -offset);

			List<float> power = new List<float>(new float[] { 0.2f, 0.3f, 0.4f, 0.7f, 0.8f, 1.0f, 0.8f, 0.8f, 0.8f, 0.3f });
			List<float> ratios = new List<float>(new float[] { 3.827f, 2.360f, 1.685f, 1.312f, 1.000f, 0.793f });

			// Player car respects GameConfig.ManualGearbox; AI always gets AutoGearbox
			// since it never sets GearUpInput/GearDownInput.
			BaseGearbox gearbox = BaseGearbox.Create(isPlayer && GameConfig.ManualGearbox, ratios, 0.2f);
			Motor = new Motor(power, Descriptor.Horsepower, Descriptor.Redline, gearbox);
			Motor.Gearbox.GearChangeStarted += new EventHandler(Gearbox_GearChanged);
			_traction = (Motor.GetPowerAtRpmForGear(Motor.RedlineRpm, 2) * 30) - 30;

			BodyPitch = new Spring(1200, 1.5f, 200, 0, 1.4f);
			BodyRoll = new Spring(1200, 1.5f, 180, 0, 3);

			_audioProvider = new VehicleAudioProvider(this);

			// Initialize RenderDirection so the first frame's wheel matrix is valid.
			// It's normally set in Update(), but Render() can be called before the first Update().
			RenderDirection = Direction;
		}


		bool _audioEnabled;
		public bool AudioEnabled
		{
			get { return _audioEnabled; }
			set
			{
				if (value)
					_audioProvider.Initialize();
				else
					_audioProvider.StopAll();
				_audioEnabled = value;
			}
		}

		private void UpdateWheels()
		{
			//front wheels
			for (int i = 0; i < 2; i++)
				Wheels[i].Rotation -= Speed * Engine.Instance.FrameTime;

			//back wheels
			if (!HandbrakeInput)
			{
				for (int i = 2; i < 4; i++)
				{
					Wheels[i].Rotation -= Engine.Instance.FrameTime * (Motor.WheelsSpinning ? 50 : Speed);
				}
			}
			// Rear wheels locked by handbrake — mark skidding when fast enough.
			else if (_isOnGround && Speed > HandbrakeMinSpeed)
			{
				Wheels[2].IsSkidding = Wheels[3].IsSkidding = true;
				_audioProvider.PlaySkid(true);
			}

			Wheels[0].Steer(_steeringWheel);
			Wheels[1].Steer(_steeringWheel);

			if (_isOnGround && BrakePedalInput > 0.5f && Math.Abs(Speed) > 10)
			{
				if (Speed < 80)
				{
					Wheels[0].IsSkidding = Wheels[1].IsSkidding = Wheels[2].IsSkidding = Wheels[3].IsSkidding = true;
				}
				_audioProvider.PlaySkid(true);
			}
			else if (_isOnGround && Math.Abs(_rearSlipFactor) > 0 && !HandbrakeInput)
			{
				Wheels[2].IsSkidding = Wheels[3].IsSkidding = true;
				_audioProvider.PlaySkid(true);
			}
			else if (_isOnGround && Math.Abs(_steeringWheel) > 0.25f && _frontSlipFactor > 0.43f)
			{
				_audioProvider.PlaySkid(true);
			}
			else if (!HandbrakeInput || Speed <= HandbrakeMinSpeed)
			{
				_audioProvider.PlaySkid(false);
			}

			foreach (VehicleWheel wheel in Wheels)
				wheel.Update();
		}

		

		private void UpdateDrag()
		{
			float elapsedSeconds = Engine.Instance.FrameTime;

			float airFriction = AirFrictionPerSpeed * Math.Abs(Speed);
			if (airFriction > MaxAirFriction)
				airFriction = MaxAirFriction;
			// Don't use ground friction if we are not on the ground.
			float groundFriction = CarFrictionOnRoad;
			if (_isOnGround == false)
				groundFriction = 0;

			_force *= 1.0f - (groundFriction + airFriction) * 0.06f * elapsedSeconds;
			Speed *= 1.0f - (groundFriction + airFriction) * 0.0015f * elapsedSeconds;

			if (_isOnGround)
			{
				float drag = BrakePower * BrakePedalInput;

				// Handbrake: time-dependent drag curve.
				// Peaks at HandbrakePeakPower on first application, fades 40% over HandbrakeMaxLockTime
				// as locked rubber heats and polishes (loses kinetic friction).
				if (HandbrakeInput)
				{
					_handbrakeHeldTime = Math.Min(_handbrakeHeldTime + elapsedSeconds, HandbrakeMaxLockTime);
					float fadeFraction = _handbrakeHeldTime / HandbrakeMaxLockTime;
					drag += HandbrakePeakPower * (1f - fadeFraction * 0.4f);
				}
				else
				{
					// Release: reset quickly so the next press feels fresh.
					_handbrakeHeldTime = Math.Max(0f, _handbrakeHeldTime - elapsedSeconds * 3f);
				}
				
				// as we're turning, add drag
				drag += Math.Abs(_steeringWheel) * 10f;
				if (Math.Abs(Speed) > 30)
				{
					drag += _nbrWheelsOffRoad * 5f;
				}

				drag += Motor.CurrentFriction;

				if (Math.Abs(Speed) < 1 || drag < 0)
					drag = 0;

				if (Speed > 0)
				{
					Speed -= drag * elapsedSeconds;
					if (Speed < 0) Speed = 0; //avoid braking so hard we go backwards
				}
				else if (Speed < 0)
				{
					Speed += drag * elapsedSeconds;
				}
			}
		}

		private void UpdateEngineForce()
		{
			_previousSpeed = Speed;

			float newAccelerationForce = 0.0f;

			Motor.Throttle = ThrottlePedalInput;
			newAccelerationForce += Motor.CurrentPowerOutput * 0.4f;

			if (Motor.Gearbox.GearEngaged && Motor.Gearbox.CurrentGear > 0)
			{
				float tractionFactor = Math.Min(1, (_traction + Speed) / newAccelerationForce);
				Motor.WheelsSpinning = tractionFactor < 1 || (Motor.Rpm > 0.7f && Speed < 10 && Motor.Throttle > 0);
				if (Motor.WheelsSpinning)
				{
					_audioProvider.PlaySkid(true);
					Wheels[2].IsSkidding = Wheels[3].IsSkidding = true;
				}
				else if (!_isOnGround)
				{
					Motor.WheelsSpinning = true;
				}
			}
			
			GearboxAction action = GearboxAction.None;
			if (GearUpInput) action = GearboxAction.GearUp;
			else if (GearDownInput) action = GearboxAction.GearDown;
			Motor.Update(Speed, action);

			if (Motor.AtRedline && !Motor.WheelsSpinning)
			{
				_force *= 0.2f;
			}

			if (Motor.Throttle == 0 && Math.Abs(Speed) < 1)
			{
				Speed = 0;
			}

			if (_isOnGround)
				_force += Direction * newAccelerationForce * Engine.Instance.FrameTime * 2.5f;
		}

		public override void Update()
		{
			if (CurrentNode.Next == null) return;
			float elapsedSeconds = Engine.Instance.FrameTime;

			UpdateRearSlip();
			UpdateEngineForce();
			UpdateDrag();

			Vector3 speedChangeVector = _force / Descriptor.Mass;
			if (_isOnGround && speedChangeVector.Length() > 0)
			{
				float speedApplyFactor = Vector3.Dot(Vector3.Normalize(speedChangeVector), Direction);
				if (speedApplyFactor > 1)
					speedApplyFactor = 1;
				Speed += speedChangeVector.Length() * speedApplyFactor;
			}
			
			UpdateWheels();
			CheckForCollisions();

			// Calculate pitch depending on the force
			float speedChange = Speed - _previousSpeed;
			BodyPitch.ChangePosition(speedChange * 0.6f);
			BodyRoll.ChangePosition(_steeringWheel * -0.05f * Math.Min(1, Math.Abs(Speed) / 30));

			// Handbrake weight transfer: nose dips forward, body rolls to outside of slide.
			if (HandbrakeInput && _isOnGround)
			{
				BodyPitch.ChangePosition(Speed * 0.003f * elapsedSeconds);
				BodyRoll.ChangePosition(_rearSlipFactor * 0.08f);
			}

			BodyPitch.Simulate(Engine.Instance.FrameTime * 2.5f);
			BodyRoll.Simulate(Engine.Instance.FrameTime * 2.5f);

			_audioProvider.UpdateEngine();

			base.Update();
		}

		public override void HandleExtraSteeringPhysics()
		{
			float maxRot = MaxRotationPerSec * Engine.Instance.FrameTime;

			// Handle car rotation after collision
			if (_rotateAfterCollision != 0)
			{
				_audioProvider.PlaySkid(true);

				if (_rotateAfterCollision > maxRot)
				{
					_rotationChange += maxRot;
					_rotateAfterCollision -= maxRot;
				}
				else if (_rotateAfterCollision < -maxRot)
				{
					_rotationChange -= maxRot;
					_rotateAfterCollision += maxRot;
				}
				else
				{
					_rotationChange += _rotateAfterCollision;
					RotateCarAfterCollision = 0;
				}
			}
			else
			{
				_frontSlipFactor = 0;
				if (_rotationChange != 0)
				{
					_frontSlipFactor = Math.Min(0.91f, Descriptor.Mass * Math.Abs(Speed) * FrontSlipMultiplier);
					_rotationChange *= 1 - _frontSlipFactor;
				}
			}
		}

		
		void UpdateRearSlip()
		{
			float frameTime = Engine.Instance.FrameTime;

			// Track last non-zero steering direction for straight-line slide momentum.
			if (SteeringInput > 0.05f)       _lastSteeringSign =  1;
			else if (SteeringInput < -0.05f) _lastSteeringSign = -1;

			if (_isOnGround)
			{
				// ── AutoDrift path (AI) ──────────────────────────────────────────────
				if (AutoDrift && Speed > 100)
				{
					if (SteeringInput < 0)
					{
						float prev = _rearSlipFactor;
						_rearSlipFactor = Math.Min(RearSlipFactorMax, _rearSlipFactor + RearSlipFactorSpeed * frameTime);
						if (prev < 0 && _rearSlipFactor > 0) _rearSlipFactor = 0;
					}
					else if (SteeringInput > 0)
					{
						float prev = _rearSlipFactor;
						_rearSlipFactor = Math.Max(-RearSlipFactorMax, _rearSlipFactor - RearSlipFactorSpeed * frameTime);
						if (prev > 0 && _rearSlipFactor < 0) _rearSlipFactor = 0;
					}
				}

				// ── Handbrake path (player) ───────────────────────────────────────────
				else if (HandbrakeInput && Speed > HandbrakeMinSpeed)
				{
					// Build rate scales with steering magnitude and entry speed.
					// At low speed or no steering the slip builds slowly; at high speed
					// with full lock it snaps quickly toward RearSlipFactorMax.
					float speedScale  = Speed / MaxYawReferenceSpeed;
					float steerMag    = Math.Abs(SteeringInput);
					float buildRate   = HandbrakeSlipBuildRate * steerMag * speedScale;

					// Target slip direction follows current steering, or last known direction
					// for straight-line entries (keeps existing slide from flipping).
					int   slipSign    = SteeringInput < -0.05f ?  1
					                  : SteeringInput >  0.05f ? -1
					                  : _lastSteeringSign;
					float targetSlip  = slipSign * RearSlipFactorMax;

					// MoveTowards: step _rearSlipFactor toward target by buildRate*dt,
					// never overshooting.
					if (steerMag > 0.05f || Math.Abs(_rearSlipFactor) > 0.02f)
					{
						float step  = buildRate * frameTime;
						float delta = targetSlip - _rearSlipFactor;
						if (Math.Abs(delta) <= step)
							_rearSlipFactor = targetSlip;
						else
							_rearSlipFactor += Math.Sign(delta) * step;
					}

					// Straight-line handbrake: add a small wobble from existing momentum.
					// Without steering the rear still has a mild tendency to yaw based on
					// any slip already accumulated, so the driver must countersteer to hold.
					if (steerMag < 0.05f && Math.Abs(_rearSlipFactor) > 0.02f)
					{
						_rearSlipFactor += Math.Sign(_rearSlipFactor) * 0.3f * speedScale * frameTime;
						_rearSlipFactor  = MathHelper.Clamp(_rearSlipFactor, -RearSlipFactorMax, RearSlipFactorMax);
					}
				}

				// ── Slip continuation while steering (post-handbrake or mid-slide) ─────
				else if (!HandbrakeInput && SteeringInput != 0 && _rearSlipFactor != 0 && Speed > 10)
				{
					if (SteeringInput < 0)
					{
						float prev = _rearSlipFactor;
						_rearSlipFactor = Math.Min(RearSlipFactorMax, _rearSlipFactor + RearSlipFactorSpeed * frameTime);
						if (prev < 0 && _rearSlipFactor > 0) _rearSlipFactor = 0;
					}
					else if (SteeringInput > 0)
					{
						float prev = _rearSlipFactor;
						_rearSlipFactor = Math.Max(-RearSlipFactorMax, _rearSlipFactor - RearSlipFactorSpeed * frameTime);
						if (prev > 0 && _rearSlipFactor < 0) _rearSlipFactor = 0;
					}
				}

				// ── Decay ──────────────────────────────────────────────────────────────
				// Momentum-weighted: large slip angles take longer to recover (correct physics).
				// Handbrake held = no decay (tyre stays unlocked); released = decay active.
				if (!HandbrakeInput || Speed <= HandbrakeMinSpeed)
				{
					if (Math.Abs(_rearSlipFactor) > 0.03f)
					{
						float decayRate = RearSlipDecayBase + Math.Abs(_rearSlipFactor) * RearSlipDecayMomentumScale;
						_rearSlipFactor += (_rearSlipFactor > 0 ? -1 : 1) * decayRate * frameTime;
					}
					else
					{
						_rearSlipFactor = 0;
					}
				}
			}

			RenderDirection = Vector3.TransformNormal(Direction, Matrix.CreateFromAxisAngle(Up, _rearSlipFactor));
		}

		void CheckForCollisions()
		{
			_nbrWheelsOffRoad = VehicleFenceCollision.GetWheelsOutsideRoadVerge(this);

			if (Speed > 3 && _nbrWheelsOffRoad > 0)
			{
				_audioProvider.PlayOffRoad(true);
				Wheels[2].IsSkidding = Wheels[3].IsSkidding = true;
			}
			else
			{
				_audioProvider.PlayOffRoad(false);
			}
			VehicleFenceCollision.Handle(this);
		}

		public void RenderShadow(bool isPlayer)
		{
			// Shadow
			Vector3[] points = new Vector3[4];
			float y = -Wheels[0].Size / 2 + 0.4f;
			// B: negative xoffset pulls each shadow edge inward from its axle point
			// by 0.75 units (half the old wheel Width of 1.5), matching the narrower
			// wheel geometry. Positive xoffset would expand outward (old behaviour ~0.1).
			float xoffset = -0.75f;
			points[0] = Wheels[0].GetOffsetPosition(new Vector3(-xoffset, y, -2));
			points[1] = Wheels[1].GetOffsetPosition(new Vector3(xoffset, y, -2));
			points[2] = Wheels[2].GetOffsetPosition(new Vector3(-xoffset, y, 3.5f));
			points[3] = Wheels[3].GetOffsetPosition(new Vector3(xoffset, y, 3.5f));

			if (!_isOnGround)
			{
				points[0].Y = _currentHeightOfTrack;
				points[1].Y = _currentHeightOfTrack;
				points[2].Y = _currentHeightOfTrack;
				points[3].Y = _currentHeightOfTrack;
			}
			ObjectShadow.Render(points, isPlayer);
		}

		public override void Render()
		{
			_effect.View = Engine.Instance.Camera.View;
			_effect.Projection = Engine.Instance.Camera.Projection;

			// Render wheels BEFORE the car body.
			// When a front wheel turns into the wheel arch, it must write depth values
			// first so that the car body's side polygons fail the depth test and cannot
			// overdraw the wheel face. Rendering body first caused the body to win the
			// depth test and produce black polygon overlaps at the wheel/bodywork junction.
			WheelModel.BeginBatch(_effect);
			foreach (VehicleWheel wheel in Wheels)
			{
				wheel.Render();
			}

			// Car body second — its side polygons are now depth-rejected wherever the
			// wheel already wrote depth, so the wheel remains fully visible when turning.
			_effect.World = GetRenderMatrix();
			_effect.CurrentTechnique.Passes[0].Apply();
			_model.Render(_effect, BrakePedalInput > 0);
		}

		public override Matrix GetRenderMatrix()
		{
			Matrix orientation = Matrix.Identity;
			orientation.Right = Vector3.Cross(RenderDirection, Up);
			orientation.Up = Up;
			orientation.Forward = RenderDirection;

			return Matrix.CreateRotationX(BodyPitch.Position / 60) *
					Matrix.CreateRotationZ(-BodyRoll.Position * 0.21f) *
					orientation *
					Matrix.CreateTranslation(Position);
		}

		protected void Gearbox_GearChanged(object sender, EventArgs e)
		{
			_audioProvider.ChangeGear();
		}

		public override void OnGroundHit()
		{
			_audioProvider.HitGround();
		}
	}
}
