using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GameEngine;

namespace OpenNFS1.Dashboards
{
    /// <summary>
    /// Renders a 3-digit 7-segment LED/LCD display in 640x480 coordinate space,
    /// scaled to the current resolution via GameConfig.ScaleX/Y.
    ///
    /// Segment layout (classic 7-segment):
    ///   aaa
    ///  f   b
    ///  f   b
    ///   ggg
    ///  e   c
    ///  e   c
    ///   ddd
    ///
    /// SpeedoPosition is treated as the RIGHT-CENTRE anchor of the full 3-digit group,
    /// matching the existing right-aligned text layout so no position values need changing.
    /// </summary>
    static class SevenSegmentDisplay
    {
        // Segment bitmasks for digits 0-9.
        // Bit order: a=0, b=1, c=2, d=3, e=4, f=5, g=6
        private static readonly byte[] DigitSegments = new byte[]
        {
            0b0111111, // 0 — a b c d e f
            0b0000110, // 1 — b c
            0b1011011, // 2 — a b d e g
            0b1001111, // 3 — a b c d g
            0b1100110, // 4 — b c f g
            0b1101101, // 5 — a c d f g
            0b1111101, // 6 — a c d e f g
            0b0000111, // 7 — a b c
            0b1111111, // 8 — all
            0b1101111, // 9 — a b c d f g
        };

        // 1×1 white pixel — reused for every segment rectangle draw call.
        private static Texture2D _pixel;

        /// <summary>Must be called once (lazily) before any Draw call.</summary>
        private static void EnsurePixel()
        {
            if (_pixel != null) return;
            _pixel = new Texture2D(Engine.Instance.Device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        // -----------------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Draws a 3-digit 7-segment display.
        /// </summary>
        /// <param name="spriteBatch">Active SpriteBatch (already Begin'd).</param>
        /// <param name="value">Speed value (clamped 0-999).</param>
        /// <param name="rightCentre640">
        ///   Anchor in 640×480 space: the right edge / vertical centre of the display group.
        ///   This matches the existing SpeedoPosition semantic so nothing needs moving.
        /// </param>
        /// <param name="digitHeight640">Height of each digit character in 640×480 units.</param>
        /// <param name="litColor">Colour of active (lit) segments.</param>
        /// <param name="dimColor">Colour of inactive (ghost) segments — gives the LED/LCD look.</param>
        public static void Draw(
            SpriteBatch spriteBatch,
            int         value,
            Vector2     rightCentre640,
            float       digitHeight640,
            Color       litColor,
            Color       dimColor)
        {
            EnsurePixel();

            // Proportional geometry (all in 640×480 space).
            float h   = digitHeight640;
            float t   = h * 0.13f;          // segment thickness
            float w   = h * 0.55f;          // character cell width (outer)
            float gap = h * 0.1f;          // horizontal gap between digit cells

            float totalWidth = 3 * w + 2 * gap;

            // Top-left corner of the whole 3-digit block (640×480 space).
            float startX = rightCentre640.X - totalWidth;
            float startY = rightCentre640.Y - h / 2f;

            // Decompose value into three digits, always showing leading zeros.
            value = Math.Max(0, Math.Min(999, value));
            int hundreds = value / 100;
            int tens     = (value % 100) / 10;
            int units    = value % 10;

            DrawDigit(spriteBatch, hundreds, new Vector2(startX,           startY), w, h, t, litColor, dimColor);
            DrawDigit(spriteBatch, tens,     new Vector2(startX + w + gap, startY), w, h, t, litColor, dimColor);
            DrawDigit(spriteBatch, units,    new Vector2(startX + 2*(w + gap), startY), w, h, t, litColor, dimColor);
        }

        // -----------------------------------------------------------------------
        //  Internals
        // -----------------------------------------------------------------------

        /// <summary>
        /// Draws one 7-segment digit character cell whose top-left is <paramref name="origin640"/>
        /// (all coordinates still in 640×480 space; scaled to screen inside DrawRect).
        /// </summary>
        private static void DrawDigit(
            SpriteBatch spriteBatch,
            int         digit,
            Vector2     origin640,
            float       w, float h, float t,
            Color       litColor,
            Color       dimColor)
        {
            byte segs = DigitSegments[digit % 10];

            // Inner horizontal bar length (space between the two vertical rails).
            float inner = w - 2 * t;
            // Half-height: where the middle row falls.
            float mid = h / 2f;

            // Helpers for lit/dim colour selection.
            bool a = (segs & (1 << 0)) != 0;
            bool b = (segs & (1 << 1)) != 0;
            bool c = (segs & (1 << 2)) != 0;
            bool d = (segs & (1 << 3)) != 0;
            bool e = (segs & (1 << 4)) != 0;
            bool f = (segs & (1 << 5)) != 0;
            bool g = (segs & (1 << 6)) != 0;

            float ox = origin640.X;
            float oy = origin640.Y;

            // ── Horizontal bars ─────────────────────────────────────────────────
            // a — top
            DrawRect(spriteBatch, ox + t, oy,            inner, t, a ? litColor : dimColor);
            // g — middle
            DrawRect(spriteBatch, ox + t, oy + mid - t/2f, inner, t, g ? litColor : dimColor);
            // d — bottom
            DrawRect(spriteBatch, ox + t, oy + h - t,    inner, t, d ? litColor : dimColor);

            // ── Vertical bars (top half) ─────────────────────────────────────────
            float vLen = mid - t * 1.5f;   // vertical segment length (top or bottom half)

            // f — top-left
            DrawRect(spriteBatch, ox,     oy + t,   t, vLen, f ? litColor : dimColor);
            // b — top-right
            DrawRect(spriteBatch, ox + w - t, oy + t,   t, vLen, b ? litColor : dimColor);

            // ── Vertical bars (bottom half) ──────────────────────────────────────
            float bottomVStart = oy + mid + t / 2f;

            // e — bottom-left
            DrawRect(spriteBatch, ox,     bottomVStart, t, vLen, e ? litColor : dimColor);
            // c — bottom-right
            DrawRect(spriteBatch, ox + w - t, bottomVStart, t, vLen, c ? litColor : dimColor);
        }

        /// <summary>
        /// Draws a filled rectangle defined in 640×480 space, scaled to screen pixels.
        /// </summary>
        private static void DrawRect(SpriteBatch sb, float x640, float y640, float w640, float h640, Color color)
        {
            int sx = (int)(x640 * GameConfig.ScaleX);
            int sy = (int)(y640 * GameConfig.ScaleY);
            int sw = Math.Max(1, (int)(w640 * GameConfig.ScaleX));
            int sh = Math.Max(1, (int)(h640 * GameConfig.ScaleY));
            sb.Draw(_pixel, new Rectangle(sx, sy, sw, sh), color);
        }
    }
}
