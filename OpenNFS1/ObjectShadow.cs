using System;
using GameEngine;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace OpenNFS1
{
    class ObjectShadow
    {
        static BasicEffect           _effect;
        static VertexBuffer          _vertexBuffer;
        static VertexPositionColor[] _verts = new VertexPositionColor[4];

        static void EnsureInit()
        {
            if (_effect != null) return;
            _effect = new BasicEffect(Engine.Instance.Device)
            {
                TextureEnabled     = false,
                VertexColorEnabled = true,
            };
            var shadowColor = new Color(10, 10, 10, 150);
            for (int i = 0; i < 4; i++)
                _verts[i] = new VertexPositionColor(Vector3.Zero, shadowColor);
            _vertexBuffer = new VertexBuffer(Engine.Instance.Device,
                typeof(VertexPositionColor), 4, BufferUsage.WriteOnly);
        }

        public static void Render(Vector3[] points, bool ignoreDepthBuffer)
        {
            EnsureInit();
            for (int i = 0; i < 4; i++)
                _verts[i].Position = points[i];
            _vertexBuffer.SetData(_verts);

            var device = Engine.Instance.Device;
            _effect.World      = Matrix.Identity;
            _effect.View       = Engine.Instance.Camera.View;
            _effect.Projection = Engine.Instance.Camera.Projection;

            device.DepthStencilState = ignoreDepthBuffer ? DepthStencilState.None : DepthStencilState.Default;
            device.BlendState        = BlendState.AlphaBlend;
            device.RasterizerState   = RasterizerState.CullCounterClockwise;

            device.SetVertexBuffer(_vertexBuffer);
            _effect.CurrentTechnique.Passes[0].Apply();
            device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            device.BlendState        = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState   = RasterizerState.CullNone;
        }
    }
}
