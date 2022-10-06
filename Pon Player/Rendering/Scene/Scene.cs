using Pon_Player.Misc;

namespace Pon_Player.Rendering
{
    class Scene : IDirect3D
    {
        public int NotesPassed
        {
            get => rndr.NotesPassed;
        }
        public GlobalTime Time
        {
            get => rndr.Time;
            set => rndr.Time = value;
        }

        public virtual D3D11 Renderer
        {
            get { return Context;  }
            set
            {
                if (Renderer != null)
                {
                    Renderer.Rendering -= ContextRendering;
                    Detach();
                }
                Context = value;
                if (Renderer != null)
                {
                    Renderer.Rendering += ContextRendering;
                    Attach();
                }
            }
        }
        D3D11 Context;
        public Render rndr;

        void ContextRendering(object aCtx, DrawEventArgs args) { RenderScene(args); }
        public void RenderScene(DrawEventArgs args)
        {
            rndr.DrawFrame(Renderer.Device, Renderer.RenderTargetView, args);
        }

        protected void Attach()
        {
            if (Renderer == null)
                return;

            rndr = new Render(Renderer.Device);
        }

        protected void Detach()
        {
            rndr.Dispose();
        }

        void IDirect3D.Reset(DrawEventArgs args)
        {
            if (Renderer != null)
                Renderer.Reset(args);
        }

        void IDirect3D.Render(DrawEventArgs args)
        {
            if (Renderer != null)
                Renderer.Render(args);
        }
    }
}
