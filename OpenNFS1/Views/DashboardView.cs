using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GameEngine;
using OpenNFS1.Dashboards;
using OpenNFS1.Physics;
using OpenNFS1.Vehicles;
using OpenNFS1.Views;

namespace OpenNFS1
{
    class DashboardView : IView
    {
        DrivableVehicle _car;
        SimpleCamera _camera;
        private Dashboard _dashboard;

        public DashboardView(DrivableVehicle car, Race race)
        {
            _car = car;
            _camera = new SimpleCamera();
            _camera.FieldOfView = GameConfig.FOV;
            _camera.FarPlaneDistance = GameConfig.DrawDistance;

            var dashfile = Path.GetFileNameWithoutExtension(car.Descriptor.ModelFile) + "dh.fsh";
            var dashDescription = DashboardDescription.Descriptions.Find(a => a.Filename == dashfile);
            _dashboard = new Dashboard(car, dashDescription, race);
        }

        #region IView Members

        public bool Selectable { get { return true; } }

        public bool ShouldRenderPlayer { get { return false; } }

        public void Update(GameTime gameTime)
        {
            _camera.Position = _car.Position + new Vector3(0, 5, 0);
            _camera.LookAt   = _camera.Position + _car.RenderDirection * 60f
                             + new Vector3(0, _car.BodyPitch.Position + GameConfig.DashboardViewPitchOffset, 0);
            _camera.UpVector = _car.Up;
            _dashboard.Update(gameTime);
        }

        /// <summary>
        /// Render the mirror scene into its off-screen RT.
        /// MUST be called before the main backbuffer render — switching to a
        /// RenderTarget2D after the backbuffer has been drawn discards all backbuffer
        /// content, leaving only black behind.
        /// </summary>
        public void PreRender()
        {
            _dashboard.RenderMirrorScene();
        }

        public void Render()
        {
            // Mirror RT was already filled in PreRender(). Here we just do the 2D overlay.
            var device = Engine.Instance.Device;
            device.BlendState        = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState   = RasterizerState.CullCounterClockwise;

            Engine.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                null,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise);

            _dashboard.Render();

            Engine.Instance.SpriteBatch.End();
        }

        public void Activate()
        {
            Engine.Instance.Camera = _camera;
            _dashboard.IsVisible   = true;
            _camera.Position = _car.Position + new Vector3(0, 5, 0);
            _camera.LookAt   = _camera.Position + _car.RenderDirection * 60f
                             + new Vector3(0, GameConfig.DashboardViewPitchOffset, 0);
            _camera.UpVector = _car.Up;
            _camera.Update(null);
        }

        public void Deactivate()
        {
            _dashboard.IsVisible = false;
        }

        #endregion
    }
}
