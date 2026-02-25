using System;
using System.IO;
using System.Runtime.InteropServices;
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

			// On discrete GPUs (NVIDIA/AMD) the OpenGL driver marks the backbuffer as
			// sRGB-capable and applies a linear→sRGB gamma curve when we blit the render
			// target to screen, making mid-tones appear too bright/washed out.
			// Integrated Intel GPUs typically don't do this so they look correct by default.
			// SDL_SetWindowBrightness(1.0f) resets the window's gamma ramp to neutral
			// (identity), neutralising any driver-level gamma boost without affecting
			// other applications or the system display settings.
			try { SDL_SetWindowBrightness(Window.Handle, 1.0f); }
			catch { /* non-fatal: some platforms don't support gamma ramps */ }
		}

		[DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern int SDL_SetWindowBrightness(IntPtr window, float brightness);

		protected override void LoadContent()
		{
			Engine.Create(this, _graphics);

			_renderTarget = new RenderTarget2D(Engine.Instance.Device, GameConfig.ResX, GameConfig.ResY,
				false,
				_graphics.GraphicsDevice.DisplayMode.Format,
				DepthFormat.Depth24,
				0,
				RenderTargetUsage.DiscardContents);

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
				sprite.Begin();
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
