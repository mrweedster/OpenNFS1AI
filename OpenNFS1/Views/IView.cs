using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;

namespace OpenNFS1.Views
{
    interface IView
    {
        bool Selectable { get; }
        bool ShouldRenderPlayer { get; }
        void Update(GameTime gameTime);
        /// <summary>
        /// Called BEFORE the main 3D scene renders to the backbuffer.
        /// Use for off-screen render-target work (e.g. mirror RT).
        /// Switching RTs inside Render() discards the backbuffer, so this
        /// hook must fire first.  Default implementation: do nothing.
        /// </summary>
        void PreRender();
        void Render();
        void Activate();
        void Deactivate();
    }
}
