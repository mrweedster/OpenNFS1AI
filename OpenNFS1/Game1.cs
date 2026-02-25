using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GameEngine;
using OpenNFS1.UI.Screens;

namespace OpenNFS1
{
	class Game1 : Microsoft.Xna.Framework.Game
	{
		// Static reference so screens can call ApplyResolution() without coupling.
		public static Game1 Instance { get; private set; }

		GraphicsDeviceManager _graphics;
		RenderTarget2D _renderTarget;
		Effect _gammaEffect;

		public Game1()
		{
			Instance = this;
			_graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";

			GameConfig.Load();

			if (GameConfig.FullScreen)
			{
				_graphics.PreferredBackBufferWidth  = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
				_graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
				_graphics.IsFullScreen = true;
			}
			else
			{
				_graphics.PreferredBackBufferWidth  = GameConfig.ResX;
				_graphics.PreferredBackBufferHeight = GameConfig.ResY;
				_graphics.IsFullScreen = false;
			}
			_graphics.PreferredDepthStencilFormat = DepthFormat.Depth24;
			_graphics.PreferMultiSampling = true;
		}

		protected override void Initialize()
		{
			base.Initialize();
		}

		protected override void LoadContent()
		{
			Engine.Create(this, _graphics);

			// Auto-detect gamma from the GPU vendor unless the user has an explicit
			// "gamma" entry in gameconfig.json.  Must be called after Engine.Create()
			// so the graphics device (and adapter info) is fully initialised.
			GameConfig.AutoDetectGamma(GraphicsAdapter.DefaultAdapter.Description);

			_renderTarget = new RenderTarget2D(Engine.Instance.Device, GameConfig.ResX, GameConfig.ResY,
				false,
				_graphics.GraphicsDevice.DisplayMode.Format,
				DepthFormat.Depth24,
				0,
				RenderTargetUsage.DiscardContents);

			_gammaEffect = Content.Load<Effect>("GammaCorrect");

			Engine.Instance.Device.SetRenderTarget(_renderTarget);
			Engine.Instance.ScreenSize = new Vector2(
				GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
				GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height);

			Engine.Instance.Screen = new ResolutionScreen();
		}

		protected override void UnloadContent()
		{
			Engine.Instance.ContentManager.Unload();
		}

		protected override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			Engine.Instance.Device.SetRenderTarget(_renderTarget);

			Color c = new Color(0.15f, 0.15f, 0.15f);
			_graphics.GraphicsDevice.Clear(c);

			base.Draw(gameTime);

			Engine.Instance.Device.SetRenderTarget(null);

			using (SpriteBatch sprite = new SpriteBatch(Engine.Instance.Device))
			{
				// Use window-relative coordinates, not screen coordinates
				Rectangle r = new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
				if (GameConfig.FullScreen)
				{
					// Retain original 4:3 aspect ratio
					int originalWidth = r.Width;
					int w = (int)((4f / 3f) * r.Height);
					r.Width = w;
					r.X = originalWidth / 2 - w / 2;
				}

				// Apply gamma correction shader to counteract the driver's
				// linear→sRGB conversion on NVIDIA/AMD discrete GPUs.
				// On Intel (no sRGB conversion) gamma=1.0 produces pow(x,1)=x — identity.
				_gammaEffect.Parameters["Gamma"].SetValue(GameConfig.Gamma);
				sprite.Begin(SpriteSortMode.Immediate, null, null, null, null, _gammaEffect);
				sprite.Draw(_renderTarget, r, Color.White);
				sprite.End();
			}
		}

		// Called by ResolutionScreen when the user confirms a resolution.
		// Resizes the back buffer and recreates the render target at the new size.
		public void ApplyResolution(int w, int h)
		{
			GameConfig.ResX = w;
			GameConfig.ResY = h;

			_graphics.PreferredBackBufferWidth  = w;
			_graphics.PreferredBackBufferHeight = h;
			_graphics.ApplyChanges();

			// Recreate the render target at the new resolution.
			_renderTarget?.Dispose();
			_renderTarget = new RenderTarget2D(Engine.Instance.Device, w, h,
				false,
				_graphics.GraphicsDevice.DisplayMode.Format,
				DepthFormat.Depth24,
				0,
				RenderTargetUsage.DiscardContents);
		}
	}
}
