using System;
using System.Collections.Generic;

using System.Text;
using OpenNFS1.Physics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using OpenNFS1.Vehicles;
using OpenNFS1.Parsers.Track;
using Microsoft.Xna.Framework.Graphics;

namespace OpenNFS1
{
    static class GameConfig
    {
		// Resolution of the internal render target.
		// Set at startup by ResolutionScreen before the engine initialises.
		// Everything that uses pixel coordinates is scaled relative to the
		// original 640x480 baseline via ScaleX / ScaleY.
		public static int ResX = 640;
		public static int ResY = 480;

		// Helpers – multiply any hard-coded 640x480 coordinate by these to get
		// the equivalent coordinate in the current resolution.
		public static float ScaleX => ResX / 640f;
		public static float ScaleY => ResY / 480f;

		// constants, shouldnt be here really
		public const float MeshScale = 0.040f;
		public const float TerrainScale = 0.000080f;
		public static readonly float FOV = MathHelper.ToRadians(65);
		public const float MaxSegmentRenderCount = 50;
		public static readonly SamplerState WrapSampler = SamplerState.AnisotropicWrap;

		// Dashboard (in-car) view pitch offset — adjusts how far down the camera looks.
		// Negative = look more downward → horizon rises toward top of screen, more road visible.
		// Positive = look more upward → horizon drops, more sky visible.
		// Default: -3.5f  (roughly 30% more road visible vs the original straight-ahead aim)
		public const float DashboardViewPitchOffset = -3.5f;

		// Set while navigating through menus
        public static VehicleDescription SelectedVehicle;
        public static TrackDescription SelectedTrackDescription;
		public static Track CurrentTrack;
		public static bool ManualGearbox { get; set; }
		public static bool AlternativeTimeOfDay { get; set; }

		// Loaded from config file
		public static bool FullScreen { get; set; }
		public static string CdDataPath { get; set; }
        public static bool Render2dScenery = true, Render3dScenery = true;
        public static bool RenderOnlyPhysicalTrack = true;
        public static int DrawDistance { get; set; }
		public static bool RespectOpenRoadCheckpoints { get; set; }
		public static bool DrawDebugInfo { get; set; }

		/// <summary>
		/// Master switch for all debug file and console output:
		///   - ai_driver_N.txt  (per-AI telemetry logs)
		///   - wheelmodel_debug.txt
		///   - carmesh_debug_N.txt
		///   - joystick_debug.txt
		///   - Debug.WriteLine in asset parsers
		/// Set "debugLogging": true in gameconfig.json to enable.
		/// DrawDebugInfo (on-screen HUD) is a separate flag and unaffected.
		/// </summary>
		public static bool DebugLogging { get; set; }

		

        static GameConfig()
        {
			
        }

		public static void Load()
		{
			JObject o1 = JObject.Parse(File.ReadAllText(@"gameconfig.json"));
			FullScreen = o1.Value<bool>("fullScreen");
			CdDataPath = o1.Value<string>("cdDataPath");
			DrawDistance = o1.Value<int>("drawDistance");
			RespectOpenRoadCheckpoints = o1.Value<bool>("respectOpenRoadCheckpoints");
			DrawDebugInfo = o1.Value<bool>("drawDebugInfo");
			// Optional key — defaults to false if absent so release builds are quiet.
			DebugLogging  = o1.ContainsKey("debugLogging") && o1.Value<bool>("debugLogging");
		}
    }
}
