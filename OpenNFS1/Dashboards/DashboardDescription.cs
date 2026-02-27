using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace OpenNFS1.Dashboards
{
	class DashboardDescription
	{
		public string Filename;

		// Tachometer
		public Vector2 TachPosition;
		public float TachRpmMultiplier;
		public float TachIdlePosition;
		public float TachNeedleLength;

		// Speedometer
		// SpeedoPosition: screen position in 640x480 space of the needle pivot.
		//   Zero = no speedometer (e.g. cars whose speedo sits outside the viewport).
		// SpeedoMaxKph: speed in kph at which the needle reaches full deflection.
		// SpeedoRpmMultiplier: arc sweep in units of PI (1.0 = 180°, matching tach default).
		// SpeedoIdlePosition: radian offset subtracted at speed=0 (same role as TachIdlePosition).
		// SpeedoNeedleLength: scale of the needle sprite height.
		public Vector2 SpeedoPosition;
		public float   SpeedoMaxKph;
		public float   SpeedoRpmMultiplier;
		public float   SpeedoIdlePosition;
		public float   SpeedoNeedleLength;
		// If true, draws a digital number readout instead of a needle.
		public bool    IsDigitalSpeedo;
		// Font scale multiplier for the digital speedo (applied on top of the base 0.75f scale).
		public float   DigitalFontScale = 1.0f;
		// Colour of the digital speedo digits.
		public Color   DigitalFontColor = new Color(0, 220, 80, 255); // default green
		// If true, uses the italic (visually thinner) variant of the font.
		public bool    DigitalFontItalic = false;

		// ── Rear-view mirror ────────────────────────────────────────────────────
		// Rectangle that covers the black mirror area on the dashboard texture.
		// Defined in 640×480 coordinate space; scaled at runtime.
		// (0,0,0,0) means no mirror for this car.
		public Rectangle MirrorRect;

		// ── 7-Segment LED/LCD display ────────────────────────────────────────────
		// When true, replaces the font-based readout with a proper 7-segment display
		// drawn from primitives.  Always shows 3 digits (000-999) with leading zeros.
		// SpeedoPosition continues to serve as the right-centre anchor of the display.
		public bool    UseSevenSegment = false;
		// Height of each digit character in 640×480 space.
		// Width, thickness and gap are derived proportionally from this value.
		public float   SevenSegmentDigitHeight = 28f;
		// Colour of inactive (ghost / unlit) segments — creates the authentic LED/LCD look.
		// For LED-style (Warrior): a very dim version of the lit colour.
		// For LCD-style (ZR1):     a subtle mid-grey showing through the LCD surface.
		public Color   SevenSegmentDimColor = new Color(0, 40, 18, 70); // default: dim green

		public static List<DashboardDescription> Descriptions = new List<DashboardDescription>();

		static DashboardDescription()
		{
			//ZR1 (Corvette)
			// 7-segment LCD-style display. Digit height is 50% of the Warrior's (28 -> 14).
			// Lit colour is near-black (original dark LCD ink); dim shows the lighter LCD surface.
			Descriptions.Add(new DashboardDescription
			{
				Filename = "czr1dh.fsh",
				TachPosition = new Vector2(277, 398),
				TachRpmMultiplier = 0.54f,
				TachIdlePosition = 1.9f,
				TachNeedleLength = 1.7f,
				SpeedoPosition = new Vector2(334, 371),
				IsDigitalSpeedo = true,
				UseSevenSegment = true,
				SevenSegmentDigitHeight = 12f,                       // 50% of Warrior's 28
				DigitalFontColor = new Color(10, 10, 10, 255),       // lit  = near-black LCD ink
				SevenSegmentDimColor = new Color(140, 135, 120, 55), // dim  = pale parchment LCD off
				DigitalFontScale = 0.6f,
				DigitalFontItalic = true,
				SpeedoMaxKph = 300,
				SpeedoRpmMultiplier = 0.54f,
				SpeedoIdlePosition = 1.9f,
				SpeedoNeedleLength = 1.7f,
				// Mirror: horizontal black strip across the top of the ZR1 dash
				MirrorRect = new Rectangle(545, 78, 130, 44),
			});

			//Diablo
			Descriptions.Add(new DashboardDescription
			{
				Filename = "ldiabldh.fsh",
				TachPosition = new Vector2(362, 333),
				TachRpmMultiplier = 1.25f,
				TachIdlePosition = 2.42f,
				TachNeedleLength = 1.0f,
				SpeedoPosition = new Vector2(283, 332),
				SpeedoMaxKph = 330,
				SpeedoRpmMultiplier = 1.45f,
				SpeedoIdlePosition = 2.42f,
				SpeedoNeedleLength = 1.0f,
				MirrorRect = new Rectangle(542, 75, 130, 45),
			});

			//Viper
			Descriptions.Add(new DashboardDescription
			{
				Filename = "dviperdh.fsh",
				TachPosition = new Vector2(417, 364),
				TachRpmMultiplier = 1f,
				TachIdlePosition = 2.62f,
				TachNeedleLength = 1.26f,
				SpeedoPosition = new Vector2(221, 364),
				SpeedoMaxKph = 300,
				SpeedoRpmMultiplier = 1.55f,
				SpeedoIdlePosition = 2.62f,
				SpeedoNeedleLength = 1.26f,
				MirrorRect = new Rectangle(540, 54, 130, 44),
			});

			//F512
			Descriptions.Add(new DashboardDescription
			{
				Filename = "f512trdh.fsh",
				TachPosition = new Vector2(382, 363),
				TachRpmMultiplier = 1.3f,
				TachIdlePosition = 2.52f,
				TachNeedleLength = 0.9f,
				SpeedoPosition = new Vector2(256, 363),
				SpeedoMaxKph = 320,
				SpeedoRpmMultiplier = 1.55f,
				SpeedoIdlePosition = 2.52f,
				SpeedoNeedleLength = 0.9f,
				MirrorRect = new Rectangle(542, 67, 130, 46),
			});

			//NSX
			Descriptions.Add(new DashboardDescription
			{
				Filename = "ansxdh.fsh",
				TachPosition = new Vector2(288, 362),
				TachRpmMultiplier = 1.3f,
				TachIdlePosition = 2.6f,
				TachNeedleLength = 1.1f,
				SpeedoPosition = new Vector2(358, 362),
				SpeedoMaxKph = 280,
				SpeedoRpmMultiplier = 1.48f,
				SpeedoIdlePosition = 2.6f,
				SpeedoNeedleLength = 1.1f,
				MirrorRect = new Rectangle(542, 53, 130, 44),
			});

			//911
			Descriptions.Add(new DashboardDescription
			{
				Filename = "p911dh.fsh",
				TachPosition = new Vector2(321, 361),
				TachRpmMultiplier = 1.37f,
				TachIdlePosition = 2.61f,
				TachNeedleLength = 1f,
				SpeedoPosition = new Vector2(393, 361),
				SpeedoMaxKph = 280,
				SpeedoRpmMultiplier = 1.6f,
				SpeedoIdlePosition = 2.41f,
				SpeedoNeedleLength = 1f,
				MirrorRect = new Rectangle(545, 90, 130, 45),
			});

			//RX7
			Descriptions.Add(new DashboardDescription
			{
				Filename = "mrx7dh.fsh",
				TachPosition = new Vector2(317, 361),
				TachRpmMultiplier = 1.5f,
				TachIdlePosition = 2.62f,
				TachNeedleLength = 0.95f,
				SpeedoPosition = new Vector2(384, 375),
				SpeedoMaxKph = 260,
				SpeedoRpmMultiplier = 1.6f,
				SpeedoIdlePosition = 2.62f,
				SpeedoNeedleLength = 0.95f,
				MirrorRect = new Rectangle(552, 68, 130, 44),
			});

			//Supra
			Descriptions.Add(new DashboardDescription
			{
				Filename = "tsupradh.fsh",
				TachPosition = new Vector2(324, 354),
				TachRpmMultiplier = 1.28f,
				TachIdlePosition = 2.4f,
				TachNeedleLength = 1f,
				SpeedoPosition = new Vector2(401, 374),
				SpeedoMaxKph = 280,
				SpeedoRpmMultiplier = 1.8f,
				SpeedoIdlePosition = 2.4f,
				SpeedoNeedleLength = 1f,
				MirrorRect = new Rectangle(545, 78, 130, 45),
			});

			//Warrior
			// 7-segment LED-style display using the pre-existing "000" placeholder area on the
			// dashboard texture.  Green lit segments, very dim green ghost for unlit segments.
			// Digit height 28 matches the placeholder dimensions; position unchanged.
			Descriptions.Add(new DashboardDescription
			{
				Filename = "traffcdh.fsh",
				TachPosition = new Vector2(319, 376),
				TachRpmMultiplier = 0.63f,
				TachIdlePosition = 1.8f,
				TachNeedleLength = 1.35f,
				SpeedoPosition = new Vector2(216, 246),
				IsDigitalSpeedo = true,
				UseSevenSegment = true,
				SevenSegmentDigitHeight = 22f,                    // matches the pre-drawn "000" area
				DigitalFontColor = new Color(0, 220, 80, 255),    // bright green LED
				SevenSegmentDimColor = new Color(0, 40, 18, 70),  // dim green ghost segments
				DigitalFontScale = 0.7f,
				SpeedoMaxKph = 220,
				SpeedoRpmMultiplier = 0.63f,
				SpeedoIdlePosition = 1.8f,
				SpeedoNeedleLength = 1.35f,
				// Mirror: the wide black strip across the top of the Warrior's rollbar area
				MirrorRect = new Rectangle(498, 245, 95, 46),
			});
		}
	}
}
