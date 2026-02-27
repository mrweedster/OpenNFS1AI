using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GameEngine;
using OpenNFS1;
using OpenNFS1.Physics;

namespace OpenNFS1.Dashboards
{
    /// <summary>
    /// Renders the rear-view mirror inset on the in-car dashboard.
    ///
    /// Technique:
    ///   1. Each frame, temporarily swap Engine.Instance.Camera for a backward-looking
    ///      SimpleCamera whose eye position matches the main dashboard camera.
    ///   2. Render the track + vehicles into a small RenderTarget2D sized to the mirror
    ///      rectangle.  The player's own car is NOT rendered (same as the main view).
    ///   3. Restore the original camera and render target.
    ///   4. In Dashboard.Render() (after the dash sprite is drawn) blit the mirror
    ///      texture into the mirror rectangle, adding a subtle dark glass tint.
    ///
    /// The mirror rectangle is defined in 640×480 coordinate space in
    /// DashboardDescription and scaled to screen pixels at runtime.
    /// </summary>
    class RearViewMirror
    {
        // ── configuration ────────────────────────────────────────────────────────
        // Tint applied over the mirror image — simulates dark glass / chromium.
        private static readonly Color MirrorTint = new Color(200, 210, 220, 255);

        // Extra FOV multiplier — convex mirrors have a wider apparent field.
        private const float ConvexFovMultiplier = 1.0f;

        // How far above the car's origin the mirror eye sits (same as main cam).
        private const float EyeHeightOffset = 5f;

        // ── runtime state ────────────────────────────────────────────────────────
        private readonly DrivableVehicle _car;
        private readonly Race            _race;
        private readonly Rectangle       _mirrorRect640;   // in 640×480 space

        private RenderTarget2D _mirrorTarget;
        private SimpleCamera   _mirrorCamera;

        // 1-pixel white texture for the border/vignette.
        private static Texture2D _pixel;

        public RearViewMirror(DrivableVehicle car, Race race, Rectangle mirrorRect640)
        {
            _car          = car;
            _race         = race;
            _mirrorRect640 = mirrorRect640;

            _mirrorCamera = new SimpleCamera();
            _mirrorCamera.FieldOfView    = GameConfig.FOV * ConvexFovMultiplier;
            _mirrorCamera.FarPlaneDistance = GameConfig.DrawDistance;
            _mirrorCamera.NearPlaneDistance = 1.0f;
        }

        // ── public API ───────────────────────────────────────────────────────────

        /// <summary>Call once per frame (before Render) to update the mirror camera.</summary>
        public void Update()
        {
            Vector3 eye = _car.Position + new Vector3(0, EyeHeightOffset, 0);
            // Look directly behind: negate RenderDirection.
            Vector3 behind = eye - _car.RenderDirection * 60f;
            _mirrorCamera.Position  = eye;
            _mirrorCamera.LookAt    = behind;
            _mirrorCamera.UpVector  = _car.Up;
            _mirrorCamera.Update(null);
        }

        /// <summary>
        /// Renders the scene to the mirror render target.
        /// Call this BEFORE the main SpriteBatch.Begin() in DashboardView.Render().
        /// </summary>
        public void RenderScene()
        {
            var device = Engine.Instance.Device;

            EnsureRenderTarget(device);
            EnsurePixel();

            // ── Save state ───────────────────────────────────────────────────────
            var savedCamera   = Engine.Instance.Camera;
            var savedRT       = device.GetRenderTargets();
            var savedViewport = device.Viewport;

            // ── Set up mirror camera and render target ───────────────────────────
            Engine.Instance.Camera = _mirrorCamera;
            device.SetRenderTarget(_mirrorTarget);
            device.Viewport = new Viewport(0, 0, _mirrorTarget.Width, _mirrorTarget.Height);
            device.Clear(Color.Black);

            // 3D render states (same as DoRaceScreen.Draw → Race.Render entry)
            device.BlendState        = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.SamplerStates[0]  = GameConfig.WrapSampler;

            // Render track + opponents (player car excluded so it matches main view)
            _race.Render(renderPlayerVehicle: false);

            // ── Restore ──────────────────────────────────────────────────────────
            Engine.Instance.Camera = savedCamera;
            device.SetRenderTargets(savedRT);
            device.Viewport = savedViewport;
        }

        /// <summary>
        /// Draws the mirror texture into the dashboard rectangle.
        /// Call this inside an active SpriteBatch.Begin() block (in Dashboard.Render).
        /// </summary>
        public void DrawMirror(SpriteBatch spriteBatch)
        {
            if (_mirrorTarget == null) return;

            // Scale the 640×480 rect to current resolution.
            Rectangle dest = new Rectangle(
                (int)(_mirrorRect640.X      * GameConfig.ScaleX),
                (int)(_mirrorRect640.Y      * GameConfig.ScaleY),
                (int)(_mirrorRect640.Width  * GameConfig.ScaleX),
                (int)(_mirrorRect640.Height * GameConfig.ScaleY));

            // Mirror image (horizontally flipped — left becomes right as in a real mirror).
            spriteBatch.Draw(_mirrorTarget, dest, null, MirrorTint,
                0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);

            // Thin dark border to frame the mirror cleanly (1 px each side).
            DrawBorder(spriteBatch, dest, new Color(0, 0, 0, 180), 1);
        }

        // ── Internals ────────────────────────────────────────────────────────────

        private void EnsureRenderTarget(GraphicsDevice device)
        {
            if (_mirrorTarget != null) return;

            // Size the RT to the scaled mirror rectangle so we render at native pixel
            // density; no up/down-sampling artefacts.
            int w = Math.Max(1, (int)(_mirrorRect640.Width  * GameConfig.ScaleX));
            int h = Math.Max(1, (int)(_mirrorRect640.Height * GameConfig.ScaleY));

            _mirrorTarget = new RenderTarget2D(device, w, h,
                false, SurfaceFormat.Color, DepthFormat.Depth24);

            // Update the mirror camera projection to use the correct aspect ratio.
            _mirrorCamera.FieldOfView = GameConfig.FOV * ConvexFovMultiplier;
            _mirrorCamera.Update(null);
        }

        private static void EnsurePixel()
        {
            if (_pixel != null) return;
            _pixel = new Texture2D(Engine.Instance.Device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        private void DrawBorder(SpriteBatch sb, Rectangle r, Color c, int thickness)
        {
            // Top
            sb.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, thickness), c);
            // Bottom
            sb.Draw(_pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), c);
            // Left
            sb.Draw(_pixel, new Rectangle(r.X, r.Y, thickness, r.Height), c);
            // Right
            sb.Draw(_pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), c);
        }
    }
}
