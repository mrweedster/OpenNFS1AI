using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using GameEngine;
using OpenNFS1.Parsers.Track;
using OpenNFS1.Physics;
using OpenNFS1.Tracks;

namespace OpenNFS1.Vehicles.AI
{
	enum CornerType  { Straight, Gentle, Tight }
	enum RacingPhase { Straight, Approach, Apex, Exit }

	abstract class AIDriver : IDriver
	{
		public int     VirtualLane  { get; set; }
		public Vehicle Vehicle      { get; protected set; }
		public bool    AtEndOfTrack { get; set; }

		public const int MaxVirtualLanes = 4;
		public const int MinVirtualLane  = 1;
		public const int MaxVirtualLane  = 3;

		// ── Steering lookahead ───────────────────────────────────────────────
		protected const float SpeedPerLookaheadNode = 12f;
		protected const int   MinLookahead           = 2;
		protected const int   MaxLookahead           = 15;

		// ── Corner classification (degrees) ─────────────────────────────────
		protected const float CurvStraightMax = 1.5f;
		protected const float CurvGentleMax   = 5.0f;
		protected const float CurvApexVerge   = 7.0f;

		// ── Speed control ────────────────────────────────────────────────────
		protected const int   BrakeLookaheadNodes = 60;
		protected const float PeakCurvFullSpeed   = 2.0f;
		protected const float PeakCurvMinSpeed    = 8.0f;
		protected const float MinCornerSpeed      = 55f;
		protected const float MaxTargetSpeed      = 220f;
		protected const float BrakeGain           = 1.5f;
		protected const float ThrottleMax         = 0.95f;
		protected const float BrakeMax            = 0.65f;
		protected const float MinMovingSpeed      = 15f;
		protected const float ResumeControlSpeed  = 50f;

		// ── Jump detection ───────────────────────────────────────────────────
		protected const float JumpSlopeThreshold = 60f;
		protected const int   JumpLandingSkip    = 8;
		protected const int   JumpScanWindow     = 20;
		protected const float PostJumpCornerMin  = 2.0f;

		// ── Verge avoidance ──────────────────────────────────────────────────
		protected const float VergeMarginFraction = 0.15f;
		protected const float VergeCorrectionGain = 0.8f;

		// ── Racing line ──────────────────────────────────────────────────────
		protected const int   RacingLineScanNodes  = 30;
		protected const int   PhaseBlendNodes      = 7;
		protected const float ApproachOutsideBias  = 0.20f;
		protected const float ExitOutsideBias      = 0.18f;
		protected const float ApexInsideBias       = 0.22f;

		// ── Pre-jump positioning ─────────────────────────────────────────────
		protected const float JumpBiasMax      = 0.15f;
		protected const float JumpBiasDeltaRef = 8.0f;

		// ════════════════════════════════════════════════════════════════════
		//  Helpers
		// ════════════════════════════════════════════════════════════════════

		protected static TrackNode NodeAhead(TrackNode start, int count)
		{
			var n = start;
			for (int i = 0; i < count; i++) { if (n.Next == null) break; n = n.Next; }
			return n;
		}

		protected static float NormaliseDelta(float d)
		{
			while (d >  180f) d -= 360f;
			while (d < -180f) d += 360f;
			return d;
		}

		protected static float PeakNodeCurvature(TrackNode start, int scanCount, bool skipJumps = true)
		{
			float peak = 0f;
			var n = start;
			int skipLeft = 0;
			for (int i = 0; i < scanCount; i++)
			{
				if (n.Next == null) break;
				if (skipJumps)
				{
					if (Math.Abs(n.Slope) > JumpSlopeThreshold) skipLeft = JumpLandingSkip;
					if (skipLeft > 0) { skipLeft--; n = n.Next; continue; }
				}
				float delta = Math.Abs(NormaliseDelta(n.Next.Orientation - n.Orientation));
				peak = Math.Max(peak, delta);
				n = n.Next;
			}
			return peak;
		}

		protected static float SignedDelta(TrackNode n)
		{
			if (n.Next == null) return 0f;
			return NormaliseDelta(n.Next.Orientation - n.Orientation);
		}

		protected static float TargetSpeedForCurvature(float peak, float cornerSpeedMult = 1f)
		{
			float minSpeed = MinCornerSpeed * cornerSpeedMult;
			float t = MathHelper.Clamp(
				(peak - PeakCurvFullSpeed) / (PeakCurvMinSpeed - PeakCurvFullSpeed),
				0f, 1f);
			return MathHelper.Lerp(MaxTargetSpeed, minSpeed, t);
		}

		protected static float GetLateralOffset(Vehicle vehicle)
		{
			var node = vehicle.CurrentNode;
			float rad = MathHelper.ToRadians(node.Orientation);
			var trackRight = new Vector3((float)Math.Cos(rad), 0f, -(float)Math.Sin(rad));
			return Vector3.Dot(vehicle.Position - node.Position, trackRight);
		}

		protected static CornerType ClassifyCorner(float peakDelta)
		{
			if (peakDelta < CurvStraightMax) return CornerType.Straight;
			if (peakDelta < CurvGentleMax)  return CornerType.Gentle;
			return CornerType.Tight;
		}

		protected static TrackNode FindPostJumpCorner(TrackNode start)
		{
			var n = start;
			for (int i = 0; i < JumpScanWindow; i++)
			{
				if (n.Next == null) return null;
				if (Math.Abs(n.Slope) > JumpSlopeThreshold)
				{
					var land = NodeAhead(n, JumpLandingSkip);
					for (int j = 0; j < 25; j++)
					{
						if (land.Next == null) return null;
						if (Math.Abs(SignedDelta(land)) >= PostJumpCornerMin) return land;
						land = land.Next;
					}
					return null;
				}
				n = n.Next;
			}
			return null;
		}

		protected static float RightVergeMargin(TrackNode node) =>
			node.DistanceToRightVerge * VergeMarginFraction;
		protected static float LeftVergeMargin(TrackNode node) =>
			node.DistanceToLeftVerge  * VergeMarginFraction;

		// Racing-line fraction given phase
		protected static float RacingLineFraction(float baseFraction, CornerType ct,
			float signedDelta, RacingPhase phase, float phaseBlend, bool allowVergeApex)
		{
			if (ct == CornerType.Straight || ct == CornerType.Gentle) return baseFraction;
			float apexSide = signedDelta > 0f ? 1f : -1f;
			float apexBias = allowVergeApex ? ApexInsideBias + 0.05f : ApexInsideBias;
			float target;
			switch (phase)
			{
				case RacingPhase.Approach: target = baseFraction - apexSide * ApproachOutsideBias; break;
				case RacingPhase.Apex:     target = baseFraction + apexSide * apexBias; break;
				case RacingPhase.Exit:     target = baseFraction - apexSide * ExitOutsideBias; break;
				default:                   target = baseFraction; break;
			}
			return MathHelper.Clamp(MathHelper.Lerp(baseFraction, target, phaseBlend), 0.05f, 0.95f);
		}

		// ════════════════════════════════════════════════════════════════════
		//  GetNextTarget / FollowTrack (base — overridden in RacingAIDriver)
		// ════════════════════════════════════════════════════════════════════

		public virtual Vector3 GetNextTarget()
		{
			float peak = PeakNodeCurvature(Vehicle.CurrentNode, MaxLookahead);
			int cap = (int)MathHelper.Clamp(MaxLookahead - peak * 1.5f, MinLookahead, MaxLookahead);
			int look = (int)MathHelper.Clamp(Vehicle.Speed / SpeedPerLookaheadNode, MinLookahead, cap);
			var targetNode = NodeAhead(Vehicle.CurrentNode, look);
			int lane = MathHelper.Clamp(VirtualLane, MinVirtualLane, MaxVirtualLane);
			float frac = 0.35f + (lane - MinVirtualLane) * 0.15f;
			return Vector3.Lerp(targetNode.GetLeftVerge2(), targetNode.GetRightVerge2(), frac);
		}

		protected virtual void FollowTrack(bool atApexOfTightCorner = false)
		{
			var target = GetNextTarget();
			if (GameConfig.DrawDebugInfo)
				Engine.Instance.GraphicsUtils.AddCube(Matrix.CreateTranslation(target), Color.Yellow);

			float angle = Utility.GetSignedAngleBetweenVectors(
				Vehicle.Direction, target - Vehicle.Position, true);
			float speedFrac = MathHelper.Clamp(Vehicle.Speed / MaxTargetSpeed, 0f, 1f);
			float gain      = MathHelper.Lerp(4.5f, 3.0f, speedFrac);
			float steering  = -angle * gain;

			if (!atApexOfTightCorner)
			{
				var   node      = Vehicle.CurrentNode;
				float lateral   = GetLateralOffset(Vehicle);
				float rMargin   = RightVergeMargin(node);
				float lMargin   = LeftVergeMargin(node);
				float rLimit    =  node.DistanceToRightVerge - rMargin;
				float lLimit    = -(node.DistanceToLeftVerge  - lMargin);

				if (lateral > rLimit && steering > 0f)
					steering = Math.Min(steering, -((lateral - rLimit) / rMargin) * VergeCorrectionGain);
				else if (lateral < lLimit && steering < 0f)
					steering = Math.Max(steering,  ((lLimit - lateral) / lMargin) * VergeCorrectionGain);
			}

			Vehicle.SteeringInput = MathHelper.Clamp(steering, -1f, 1f);
		}

		protected void ApplySpeedControl(DrivableVehicle vehicle, float cornerSpeedMult,
		                                  bool floorIn, out bool floorOut)
		{
			float speed = Math.Abs(vehicle.Speed);
			floorOut = floorIn;
			if (speed < MinMovingSpeed)           floorOut = true;
			if (floorOut && speed >= ResumeControlSpeed) floorOut = false;

			if (floorOut)
			{
				vehicle.ThrottlePedalInput = ThrottleMax;
				vehicle.BrakePedalInput    = 0f;
				return;
			}

			float peakFar  = PeakNodeCurvature(vehicle.CurrentNode, BrakeLookaheadNodes, skipJumps: true);
			float peakNear = PeakNodeCurvature(vehicle.CurrentNode, 10, skipJumps: false);
			float peak     = Math.Max(peakFar, peakNear);
			float target   = TargetSpeedForCurvature(peak, cornerSpeedMult);

			if (speed <= target)
			{
				vehicle.ThrottlePedalInput = ThrottleMax;
				vehicle.BrakePedalInput    = 0f;
			}
			else
			{
				float overshoot = (speed - target) / Math.Max(target, 1f);
				vehicle.ThrottlePedalInput = 0f;
				vehicle.BrakePedalInput    = MathHelper.Clamp(overshoot * BrakeGain, 0f, BrakeMax);
			}
		}

		public abstract void Update(List<IDriver> otherDrivers);
	}

	// ════════════════════════════════════════════════════════════════════════
	//  RacingAIDriver
	// ════════════════════════════════════════════════════════════════════════
	class RacingAIDriver : AIDriver
	{
		// Personality
		public float AggressionFactor      { get; private set; }
		public float CornerSpeedMultiplier { get; private set; }
		public float LaneChangeProbability { get; private set; }

		// Racing phase
		private RacingPhase _phase             = RacingPhase.Straight;
		private float       _phaseBlend        = 0f;
		private float       _signedCornerDelta = 0f;
		private bool        _apexAllowVerge    = false;
		private float       _racingLineFraction = 0.5f;

		// Speed floor
		private bool _speedFloorActive = false;

		// Overtaking
		private float _laneChangeCooldown = 0f;
		private float _blockedTimer       = 0f;
		private const float LaneChangeCooldownTime = 3.0f;
		private const float BlockedTriggerTime     = 1.5f;
		private const float OvertakeTriggerDist    = 1.5f;
		private const float BlockedSpeedReduction  = 0.80f;

		private float _firstLaneChangeAllowed;
		private DrivableVehicle _vehicle;

		// Debug log
		private float      _logTimer   = 0f;
		private const float LogInterval = 1.0f;
		private string     _logFile;
		private static int _instanceCount = 0;
		private int        _instanceId;

		public RacingAIDriver(VehicleDescription vehicleDescriptor)
		{
			_vehicle               = new DrivableVehicle(vehicleDescriptor);
			_vehicle.SteeringSpeed = 6;
			_vehicle.AutoDrift     = false;
			Vehicle                = _vehicle;

			_firstLaneChangeAllowed = Engine.Instance.Random.Next(5, 40);
			_instanceId             = ++_instanceCount;
			_logFile                = "ai_driver_" + _instanceId + ".txt";
			if (GameConfig.DebugLogging)
				System.IO.File.WriteAllText(_logFile, "AI Driver " + _instanceId + " log\n");

			var rng = Engine.Instance.Random;
			AggressionFactor      = 0.7f  + (float)rng.NextDouble() * 0.6f;
			CornerSpeedMultiplier = 0.85f + (float)rng.NextDouble() * 0.30f;
			LaneChangeProbability = 0.5f  + (float)rng.NextDouble() * 0.5f;

			VirtualLane = rng.Next(MinVirtualLane, MaxVirtualLane + 1);
			_racingLineFraction = 0.35f + (VirtualLane - MinVirtualLane) * 0.15f;
		}

		// ── Racing line target ───────────────────────────────────────────────
		public override Vector3 GetNextTarget()
		{
			float peak = PeakNodeCurvature(Vehicle.CurrentNode, MaxLookahead);
			int cap  = (int)MathHelper.Clamp(MaxLookahead - peak * 1.5f, MinLookahead, MaxLookahead);
			int look = (int)MathHelper.Clamp(Vehicle.Speed / SpeedPerLookaheadNode, MinLookahead, cap);
			var targetNode = NodeAhead(Vehicle.CurrentNode, look);

			float useFraction = _racingLineFraction;

			// Pre-jump corner bias
			var postJump = FindPostJumpCorner(Vehicle.CurrentNode);
			if (postJump != null)
			{
				float jd   = Math.Abs(SignedDelta(postJump));
				float bias = MathHelper.Clamp(jd / JumpBiasDeltaRef, 0f, 1f) * JumpBiasMax;
				float side = SignedDelta(postJump) > 0f ? 1f : -1f;
				useFraction = MathHelper.Clamp(useFraction + side * bias * 0.5f, 0.05f, 0.95f);
			}

			return Vector3.Lerp(targetNode.GetLeftVerge2(), targetNode.GetRightVerge2(), useFraction);
		}

		// ── Racing phase ─────────────────────────────────────────────────────
		private void UpdateRacingPhase(float dt)
		{
			int   lane    = MathHelper.Clamp(VirtualLane, MinVirtualLane, MaxVirtualLane);
			float baseFrac = 0.35f + (lane - MinVirtualLane) * 0.15f;
			var   current  = _vehicle.CurrentNode;
			float peakAhead = PeakNodeCurvature(current, RacingLineScanNodes, skipJumps: true);
			var   ct        = ClassifyCorner(peakAhead);

			if (ct == CornerType.Straight || ct == CornerType.Gentle)
			{
				_phase             = RacingPhase.Straight;
				_phaseBlend        = 0f;
				_apexAllowVerge    = false;
				_racingLineFraction = MathHelper.Lerp(_racingLineFraction, baseFrac, dt * 2.5f);
				return;
			}

			// Find dominant signed delta
			var n = current; _signedCornerDelta = 0f;
			for (int i = 0; i < RacingLineScanNodes; i++)
			{
				if (n.Next == null) break;
				float d = SignedDelta(n);
				if (Math.Abs(d) > Math.Abs(_signedCornerDelta)) _signedCornerDelta = d;
				n = n.Next;
			}
			_apexAllowVerge = peakAhead > CurvApexVerge;

			float peakNear = PeakNodeCurvature(current, PhaseBlendNodes,     skipJumps: true);
			float peakMid  = PeakNodeCurvature(current, PhaseBlendNodes * 2, skipJumps: true);

			RacingPhase newPhase;
			if      (peakNear >= peakAhead * 0.7f)  newPhase = RacingPhase.Apex;
			else if (peakMid  >= peakAhead * 0.5f)  newPhase = RacingPhase.Approach;
			else                                      newPhase = RacingPhase.Exit;

			if (newPhase != _phase) { _phase = newPhase; _phaseBlend = 0f; }
			_phaseBlend = Math.Min(_phaseBlend + dt * (1f / (PhaseBlendNodes * 0.05f)), 1f);

			float target = RacingLineFraction(baseFrac, ct, _signedCornerDelta,
			                                  _phase, _phaseBlend, _apexAllowVerge);
			_racingLineFraction = MathHelper.Lerp(_racingLineFraction, target, dt * 3.5f);
		}

		// ── Overtaking ───────────────────────────────────────────────────────
		private void HandleOvertaking(List<IDriver> otherDrivers, float dt)
		{
			_laneChangeCooldown = Math.Max(0f, _laneChangeCooldown - dt);

			float myPos  = _vehicle.TrackPosition;
			float mySpd  = _vehicle.Speed;
			var   myNode = _vehicle.CurrentNode;
			float trigDist = OvertakeTriggerDist * AggressionFactor;
			bool  blocked  = false;

			foreach (var other in otherDrivers)
			{
				if (other == this || other is PlayerDriver) continue;
				float dist = other.Vehicle.TrackPosition - myPos;
				if (dist < 0f || dist > trigDist) continue;
				if (mySpd < other.Vehicle.Speed) continue;

				blocked = true;
				if (_laneChangeCooldown > 0f) continue;
				if ((float)Engine.Instance.Random.NextDouble() > LaneChangeProbability) continue;
				if (myNode.Number < _firstLaneChangeAllowed) continue;

				// Choose lane direction
				float peakAhead = PeakNodeCurvature(myNode, 15, skipJumps: true);
				float signedD   = 0f;
				if (peakAhead > CurvStraightMax)
				{
					var nn = myNode;
					for (int i = 0; i < 15; i++)
					{
						if (nn.Next == null) break;
						float d = SignedDelta(nn);
						if (Math.Abs(d) > Math.Abs(signedD)) signedD = d;
						nn = nn.Next;
					}
				}

				int desired;
				if (peakAhead > CurvStraightMax && Math.Abs(signedD) > CurvStraightMax)
					desired = signedD > 0f
						? Math.Max(MinVirtualLane, VirtualLane - 1)
						: Math.Min(MaxVirtualLane, VirtualLane + 1);
				else
					desired = myNode.DistanceToRightVerge > myNode.DistanceToLeftVerge
						? Math.Min(MaxVirtualLane, VirtualLane + 1)
						: Math.Max(MinVirtualLane, VirtualLane - 1);

				if (desired == VirtualLane) continue;

				bool occupied = false;
				foreach (var check in otherDrivers)
				{
					if (check == this) continue;
					if (check is AIDriver ai && ai.VirtualLane == desired
					    && Math.Abs(check.Vehicle.TrackPosition - myPos) < 2.5f)
					{ occupied = true; break; }
				}
				if (occupied) continue;

				VirtualLane         = desired;
				_laneChangeCooldown = LaneChangeCooldownTime;
				_blockedTimer       = 0f;
				blocked             = false;
				break;
			}

			if (blocked)
			{
				_blockedTimer += dt;
				if (_blockedTimer > BlockedTriggerTime)
				{
					_vehicle.Speed *= BlockedSpeedReduction;
					_blockedTimer   = 0f;
				}
			}
			else
				_blockedTimer = Math.Max(0f, _blockedTimer - dt);
		}

		// ── Main update ──────────────────────────────────────────────────────
		public override void Update(List<IDriver> otherDrivers)
		{
			var   node = _vehicle.CurrentNode;
			float dt   = Engine.Instance.FrameTime;

			if (node.Next == null || node.Next.Next == null)
			{
				_vehicle.ThrottlePedalInput = 0f;
				_vehicle.BrakePedalInput    = 1f;
				AtEndOfTrack = true;
				Log("END_OF_TRACK node=" + node.Number + " speed=" + _vehicle.Speed.ToString("F1"));
				return;
			}

			UpdateRacingPhase(dt);

			bool atApex = _phase == RacingPhase.Apex && _apexAllowVerge;
			FollowTrack(atApexOfTightCorner: atApex);

			ApplySpeedControl(_vehicle, CornerSpeedMultiplier, _speedFloorActive, out _speedFloorActive);

			// Corner exit: blend throttle back in as track straightens
			if (_phase == RacingPhase.Exit && _vehicle.BrakePedalInput == 0f)
			{
				float nearPeak = PeakNodeCurvature(node, 8, skipJumps: false);
				if (nearPeak < PeakCurvMinSpeed * 0.6f)
					_vehicle.ThrottlePedalInput = MathHelper.Lerp(
						_vehicle.ThrottlePedalInput, ThrottleMax, dt * 3f);
			}

			HandleOvertaking(otherDrivers, dt);
			_vehicle.Update();

			_logTimer += dt;
			if (_logTimer >= LogInterval)
			{
				_logTimer = 0f;
				float lat    = GetLateralOffset(_vehicle);
				float rLim   =  node.DistanceToRightVerge - RightVergeMargin(node);
				float lLim   = -(node.DistanceToLeftVerge  - LeftVergeMargin(node));
				string verge = lat > rLim ? "ON_RIGHT_VERGE" : lat < lLim ? "ON_LEFT_VERGE" : "on_road";
				float peak   = PeakNodeCurvature(node, BrakeLookaheadNodes, skipJumps: true);
				Log(string.Format(
					"node={0,4} spd={1,5:F1} thr={2:F2} brk={3:F2} steer={4,5:F2} " +
					"peak={5,4:F1} tgt={6,5:F1} lat={7,5:F1} lane={8} phase={9} frac={10:F2} {11}",
					node.Number, _vehicle.Speed,
					_vehicle.ThrottlePedalInput, _vehicle.BrakePedalInput,
					_vehicle.SteeringInput, peak, TargetSpeedForCurvature(peak, CornerSpeedMultiplier),
					lat, VirtualLane, _phase, _racingLineFraction, verge));
			}
		}

		private void Log(string msg)
		{
			if (!GameConfig.DebugLogging) return;
			try { System.IO.File.AppendAllText(_logFile, msg + "\n"); }
			catch { }
		}

		public void Render() { }
	}
}
