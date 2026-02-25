using System;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using GameEngine;

namespace OpenNFS1
{
    static class WheelModel
    {
        static VertexPositionTexture[] _cylinderVertexArray;
        static VertexBuffer            _cylinderVertexBuffer;
        static Texture2D               _rubberTexture;
        static AlphaTestEffect         _effect;

        static int           _totalRenderCalls = 0;
        static bool          _debugFlushed     = false;
        static StringBuilder _log              = new StringBuilder();

        static WheelModel() { CreateGeometry(); }

        public static void BeginBatch(AlphaTestEffect vehicleEffect)
        {
            _effect            = vehicleEffect;
            _effect.View       = Engine.Instance.Camera.View;
            _effect.Projection = Engine.Instance.Camera.Projection;

            if (_cylinderVertexBuffer == null)
            {
                _cylinderVertexBuffer = new VertexBuffer(Engine.Instance.Device,
                    typeof(VertexPositionTexture), _cylinderVertexArray.Length, BufferUsage.WriteOnly);
                _cylinderVertexBuffer.SetData(_cylinderVertexArray);
            }
            if (_rubberTexture == null)
            {
                _rubberTexture = new Texture2D(Engine.Instance.Device, 1, 1);
                _rubberTexture.SetData(new Color[] { new Color(30, 30, 30, 255) });
            }
        }

        public static void Render(Matrix world, Texture2D hubTexture)
        {
            _totalRenderCalls++;
            bool doLog = GameConfig.DebugLogging && !_debugFlushed && _totalRenderCalls <= 16;

            GraphicsDevice device = Engine.Instance.Device;

            if (doLog)
            {
                float scaleX = new Vector3(world.M11, world.M12, world.M13).Length();
                float scaleY = new Vector3(world.M21, world.M22, world.M23).Length();
                float scaleZ = new Vector3(world.M31, world.M32, world.M33).Length();

                // Sample CENTRE and CORNERS of hub texture — corners may be opaque dark
                // causing rectangular artifacts outside the circular tyre region
                string hubInfo = "NULL";
                if (hubTexture != null)
                {
                    try
                    {
                        int w = hubTexture.Width, h = hubTexture.Height;
                        Color[] px = new Color[1];
                        hubTexture.GetData(0, new Rectangle(w/2,  h/2,  1, 1), px, 0, 1); Color ctr = px[0];
                        hubTexture.GetData(0, new Rectangle(0,    0,    1, 1), px, 0, 1); Color tl  = px[0];
                        hubTexture.GetData(0, new Rectangle(w-1,  0,    1, 1), px, 0, 1); Color tr  = px[0];
                        hubTexture.GetData(0, new Rectangle(0,    h-1,  1, 1), px, 0, 1); Color bl  = px[0];
                        hubTexture.GetData(0, new Rectangle(w-1,  h-1,  1, 1), px, 0, 1); Color br  = px[0];
                        hubInfo = string.Format(
                            "{0}x{1} ctr=({2},{3},{4},{5}) TL=({6},{7},{8},{9}) TR=({10},{11},{12},{13}) BL=({14},{15},{16},{17}) BR=({18},{19},{20},{21})",
                            w, h,
                            ctr.R, ctr.G, ctr.B, ctr.A,
                            tl.R,  tl.G,  tl.B,  tl.A,
                            tr.R,  tr.G,  tr.B,  tr.A,
                            bl.R,  bl.G,  bl.B,  bl.A,
                            br.R,  br.G,  br.B,  br.A);
                    }
                    catch { hubInfo = hubTexture.Width + "x" + hubTexture.Height + " (corner read failed)"; }
                }

                _log.AppendLine(string.Format(
                    "Render #{0}: pos=({1:F2},{2:F2},{3:F2}) scale=({4:F3},{5:F3},{6:F3})",
                    _totalRenderCalls, world.M41, world.M42, world.M43, scaleX, scaleY, scaleZ));
                _log.AppendLine("  hubTex=" + hubInfo);

                // Sample multiple screen positions — centre is depth-rejected but rim
                // corners (at ±2.1 world units from hub centre) may escape depth rejection
                // and show the rectangular corner artifact
                SamplePoint(device, world, Vector3.Zero,             "ctr  BEFORE");
                SamplePoint(device, world, new Vector3(0,  2.1f, 0), "rimR BEFORE");
                SamplePoint(device, world, new Vector3(0, -2.1f, 0), "rimL BEFORE");
                SamplePoint(device, world, new Vector3(0, 0,  2.1f), "rimT BEFORE");
                SamplePoint(device, world, new Vector3(0, 0, -2.1f), "rimB BEFORE");
            }

            device.RasterizerState   = RasterizerState.CullNone;
            device.BlendState        = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;

            device.SetVertexBuffer(_cylinderVertexBuffer);
            _effect.World = world;
            _effect.CurrentTechnique.Passes[0].Apply();

            // Cylinder tyre tread
            device.Textures[0] = _rubberTexture;
            device.DrawPrimitives(PrimitiveType.TriangleList, 0, 64);

            // Hub cap faces — texture corners are fully transparent so no rectangle artifact
            Texture2D hub = hubTexture ?? _rubberTexture;
            device.Textures[0] = hub;
            device.DrawPrimitives(PrimitiveType.TriangleList, 192, 4);

            if (doLog)
            {
                SamplePoint(device, world, Vector3.Zero,             "ctr  AFTER ");
                SamplePoint(device, world, new Vector3(0,  2.1f, 0), "rimR AFTER ");
                SamplePoint(device, world, new Vector3(0, -2.1f, 0), "rimL AFTER ");
                SamplePoint(device, world, new Vector3(0, 0,  2.1f), "rimT AFTER ");
                SamplePoint(device, world, new Vector3(0, 0, -2.1f), "rimB AFTER ");
                _log.AppendLine();

                if (_totalRenderCalls == 16)
                {
                    _debugFlushed = true;
                    try { File.WriteAllText("wheelmodel_debug.txt", _log.ToString()); }
                    catch { }
                }
            }
        }

        static void SamplePoint(GraphicsDevice device, Matrix world, Vector3 localOffset, string label)
        {
            try
            {
                Vector3 worldPos = Vector3.Transform(localOffset, world);
                Vector3 sp = device.Viewport.Project(worldPos,
                    Engine.Instance.Camera.Projection, Engine.Instance.Camera.View, Matrix.Identity);
                int sx = (int)sp.X, sy = (int)sp.Y;
                bool on = sp.Z > 0 && sp.Z < 1 && sx >= 0 && sx < device.Viewport.Width
                                               && sy >= 0 && sy < device.Viewport.Height;
                if (!on) { _log.AppendLine("  " + label + " offScreen"); return; }
                Color[] px = new Color[1];
                device.GetBackBufferData(new Rectangle(sx, sy, 1, 1), px, 0, 1);
                _log.AppendLine(string.Format("  {0} ({1},{2}) px=({3},{4},{5},{6})",
                    label, sx, sy, px[0].R, px[0].G, px[0].B, px[0].A));
            }
            catch (Exception ex) { _log.AppendLine("  " + label + " ex: " + ex.Message); }
        }

        static void FlushLog()
        {
            try { File.WriteAllText("wheelmodel_debug.txt", _log.ToString()); }
            catch { }
        }

        private static void CreateGeometry()
        {
            const int   numSides    = 32;
            const float radius      = 0.51f;
            float       angleChange = (float)Math.PI / (numSides / 2);
            var         bottom      = new Vector3(0, -.5f, 0);
            var         top         = new Vector3(0,  .5f, 0);

            // Cylinder body + hub cap faces — 192 + 12 = 204 verts
            // Hub cap corners are fully transparent (0,0,0,0) so they don't produce
            // rectangle artefacts; only the circular hub artwork in the centre shows.
            _cylinderVertexArray = new VertexPositionTexture[numSides * 6 + 12];

            for (int k = 0; k < numSides; k++)
            {
                float   a    = k * angleChange;
                Vector3 cur  = new Vector3((float)Math.Cos(a),               0, (float)Math.Sin(a));
                Vector3 next = new Vector3((float)Math.Cos(a + angleChange), 0, (float)Math.Sin(a + angleChange));
                _cylinderVertexArray[k * 6 + 0] = new VertexPositionTexture(top    + cur  * radius, Vector2.Zero);
                _cylinderVertexArray[k * 6 + 1] = new VertexPositionTexture(bottom + cur  * radius, Vector2.Zero);
                _cylinderVertexArray[k * 6 + 2] = new VertexPositionTexture(bottom + next * radius, Vector2.Zero);
                _cylinderVertexArray[k * 6 + 3] = new VertexPositionTexture(bottom + next * radius, Vector2.Zero);
                _cylinderVertexArray[k * 6 + 4] = new VertexPositionTexture(top    + next * radius, Vector2.Zero);
                _cylinderVertexArray[k * 6 + 5] = new VertexPositionTexture(top    + cur  * radius, Vector2.Zero);
            }

            int   hub = numSides * 6; // = 192
            const float y = 0.505f;
            // Outward hub face
            _cylinderVertexArray[hub + 0]  = new VertexPositionTexture(new Vector3(-0.5f,  y,  0.5f), new Vector2(0, 0));
            _cylinderVertexArray[hub + 1]  = new VertexPositionTexture(new Vector3(-0.5f,  y, -0.5f), new Vector2(0, 1));
            _cylinderVertexArray[hub + 2]  = new VertexPositionTexture(new Vector3( 0.5f,  y, -0.5f), new Vector2(1, 1));
            _cylinderVertexArray[hub + 3]  = new VertexPositionTexture(new Vector3(-0.5f,  y,  0.5f), new Vector2(0, 0));
            _cylinderVertexArray[hub + 4]  = new VertexPositionTexture(new Vector3( 0.5f,  y, -0.5f), new Vector2(1, 1));
            _cylinderVertexArray[hub + 5]  = new VertexPositionTexture(new Vector3( 0.5f,  y,  0.5f), new Vector2(1, 0));
            // Inward hub face
            _cylinderVertexArray[hub + 6]  = new VertexPositionTexture(new Vector3(-0.5f, -y,  0.5f), new Vector2(0, 1));
            _cylinderVertexArray[hub + 7]  = new VertexPositionTexture(new Vector3( 0.5f, -y, -0.5f), new Vector2(1, 0));
            _cylinderVertexArray[hub + 8]  = new VertexPositionTexture(new Vector3(-0.5f, -y, -0.5f), new Vector2(0, 0));
            _cylinderVertexArray[hub + 9]  = new VertexPositionTexture(new Vector3(-0.5f, -y,  0.5f), new Vector2(0, 1));
            _cylinderVertexArray[hub + 10] = new VertexPositionTexture(new Vector3( 0.5f, -y,  0.5f), new Vector2(1, 1));
            _cylinderVertexArray[hub + 11] = new VertexPositionTexture(new Vector3( 0.5f, -y, -0.5f), new Vector2(1, 0));
        }
    }
}
