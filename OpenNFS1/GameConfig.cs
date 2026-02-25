using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

		/// <summary>
		/// Gamma correction applied during the final render-target blit.
		/// Auto-selected at startup from the per-vendor config values below.
		/// Override for a specific machine with a "gamma" key in gameconfig.json.
		/// </summary>
		public static float Gamma { get; set; } = 1.0f;

		/// <summary>True when "gamma" was found in gameconfig.json (explicit per-machine override).</summary>
		public static bool GammaIsExplicit { get; private set; } = false;

		// ── Per-vendor gamma defaults ─────────────────────────────────────────────
		// These are chosen at startup based on the GPU vendor string.
		// Tune them here if a whole vendor class needs a different value.
		// Individual machines can still override with "gamma" in gameconfig.json.
		public static float GammaNvidia { get; set; } = 2.2f;
		public static float GammaAmd    { get; set; } = 1.0f;
		public static float GammaOther  { get; set; } = 1.0f;

		// ── GL_FRAMEBUFFER_SRGB query ────────────────────────────────────────────
		// glIsEnabled lets us ask OpenGL directly whether the driver has enabled
		// automatic linear→sRGB conversion on the backbuffer.  When this is active
		// every pixel written to the screen is brightened by the driver, which is
		// the root cause of the "too bright on NVIDIA" problem.
		// NVIDIA's OpenGL driver enables it by default; AMD's typically does not —
		// so vendor-name heuristics are unreliable.  Querying the GL state directly
		// is the only portable, future-proof approach.
		private const int GL_FRAMEBUFFER_SRGB = 0x8DB9;

		// Windows and Linux use different shared-library names for OpenGL.
		[DllImport("opengl32.dll", EntryPoint = "glIsEnabled", ExactSpelling = true)]
		private static extern byte glIsEnabled_Win(int cap);

		[DllImport("libGL.so.1", EntryPoint = "glIsEnabled", ExactSpelling = true)]
		private static extern byte glIsEnabled_Linux(int cap);

		private static bool IsSRGBFramebufferEnabled()
		{
			try
			{
				if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
						System.Runtime.InteropServices.OSPlatform.Windows))
					return glIsEnabled_Win(GL_FRAMEBUFFER_SRGB) != 0;
				else
					return glIsEnabled_Linux(GL_FRAMEBUFFER_SRGB) != 0;
			}
			catch
			{
				// GL context not available or platform not supported — assume no correction needed.
				return false;
			}
		}

		/// <summary>
		/// Called from Game1.LoadContent() once the GL context is live.
		/// Queries GL_FRAMEBUFFER_SRGB directly — no vendor-name guessing.
		/// If the driver has sRGB conversion active, sets Gamma=2.2 to counteract it.
		/// Skipped when the user has an explicit "gamma" entry in gameconfig.json.
		/// </summary>
		public static void AutoDetectGamma(string adapterDescription)
		{
			if (GammaIsExplicit) return;  // per-machine "gamma" key wins

			string desc = (adapterDescription ?? "").ToUpperInvariant();

			string vendor;
			if (desc.Contains("NVIDIA") || desc.Contains("GEFORCE"))
			{
				Gamma  = GammaNvidia;
				vendor = "NVIDIA";
			}
			else if (desc.Contains("AMD") || desc.Contains("RADEON") || desc.Contains("ATI"))
			{
				Gamma  = GammaAmd;
				vendor = "AMD";
			}
			else
			{
				Gamma  = GammaOther;
				vendor = "Other";
			}

			string logLine = string.Format(
				"[Gamma] Adapter: \"{0}\" → vendor={1} → gamma={2:F1}",
				adapterDescription, vendor, Gamma);
			System.Diagnostics.Debug.WriteLine(logLine);
			try { System.IO.File.AppendAllText("gamma_debug.txt", logLine + "\n"); } catch { }
		}

		

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
			// Per-vendor gamma defaults — override in gameconfig.json to tune a whole GPU class.
			if (o1.ContainsKey("gammaNvidia")) GammaNvidia = o1.Value<float>("gammaNvidia");
			if (o1.ContainsKey("gammaAmd"))    GammaAmd    = o1.Value<float>("gammaAmd");
			if (o1.ContainsKey("gammaOther"))  GammaOther  = o1.Value<float>("gammaOther");
			// Per-machine override — takes priority over all vendor defaults.
			if (o1.ContainsKey("gamma"))
			{
				Gamma = o1.Value<float>("gamma");
				GammaIsExplicit = true;
			}
			// else: vendor default is selected later via AutoDetectGamma() in Game1.LoadContent()
		}
    }
}
