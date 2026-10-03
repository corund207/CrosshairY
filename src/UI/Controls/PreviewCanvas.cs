using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.Render;

namespace CrosshairY.UI.Controls
{
    /// <summary>
    /// Designer canvas: pixel-exact zoomable preview with pan, pixel grid, layer selection/dragging and
    /// live animation preview (hold left/right mouse on empty canvas to test fire/aim animations).
    /// </summary>
    public class PreviewCanvas : Control
    {
        List<object> layers = new List<object>();
        readonly Animator animator = new Animator();
        RenderResult frame;
        bool frameDirty = true;
        readonly Timer timer = new Timer { Interval = 15 };
        int zoom = 6;
        PointF pan;
        Point lastMouse;
        bool panning, dragLayer, firing, aiming;
        double dragAccX, dragAccY;
        public int SelectedLayer = -1;
        public string Background = "dark";
        public bool ShowGrid = true;
        public bool ShowCenter = true;
        public event Action<int> LayerClicked;
        public event Action<int, int, int> LayerDragged;   // index, dx, dy (design pixels)
        public event Action LayerDragEnded;
        public event Action ZoomChanged;
        static Bitmap sceneBitmap;

        public PreviewCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Theme.Bg;
            timer.Tick += (s, e) => { frameDirty = true; Invalidate(); };
            TabStop = true;
        }

        public int Zoom
        {
            get => zoom;
            set { zoom = Math.Max(1, Math.Min(24, value)); Invalidate(); ZoomChanged?.Invoke(); }
        }

        public void SetLayers(List<object> l, bool reloadAnimations = true)
        {
            layers = l ?? new List<object>();
            if (reloadAnimations) animator.Load(layers);
            frameDirty = true;
            Invalidate();
            UpdateTimer(false);
        }

        public void PlayAutoplay() { animator.Reset(); UpdateTimer(true); }

        bool pendingFit;

        /// <summary>Fits as soon as the canvas has a real size (safe to call before the control is shown).</summary>
        public void RequestFit()
        {
            if (IsHandleCreated && Width > 20 && Height > 20) FitToContent();
            else pendingFit = true;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (pendingFit && IsHandleCreated && Width > 20 && Height > 20) { pendingFit = false; FitToContent(); }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (pendingFit && Width > 20 && Height > 20) { pendingFit = false; FitToContent(); }
        }

        public void FitToContent()
        {
            double r = 1;
            foreach (var l in layers.OfType<Dictionary<string, object>>()) r = Math.Max(r, CrosshairRenderer.ComputeMaxBound(l));
            int z = (int)Math.Floor(Math.Min(Width, Height) * 0.42 / r);
            pan = PointF.Empty;
            Zoom = Math.Max(1, Math.Min(16, z));
        }

        void UpdateTimer(bool force)
        {
            bool need = force || animator.HasTimelines || (frame != null && frame.TimeDependent);
            if (need && !timer.Enabled) timer.Start();
            if (!need && timer.Enabled) timer.Stop();
        }

        PointF Center => new PointF(Width / 2f + pan.X, Height / 2f + pan.Y);

        PointF DesignToScreen(double x, double y) => new PointF((float)(Center.X + x * zoom), (float)(Center.Y + y * zoom));

        PointF ScreenToDesign(Point p) => new PointF((p.X - Center.X) / zoom, (p.Y - Center.Y) / zoom);

        int HitLayer(Point p)
        {
            var d = ScreenToDesign(p);
            int best = -1;
            double bestArea = double.MaxValue;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (!(layers[i] is Dictionary<string, object> l) || J.Bool(l, "hidden")) continue;
                var b = CrosshairRenderer.LayerBounds(l);
                b.Inflate(1.5f, 1.5f);
                if (!b.Contains(d)) continue;
                double area = b.Width * b.Height;
                if (area < bestArea) { best = i; bestArea = area; }
            }
            return best;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            lastMouse = e.Location;
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Space)))
            {
                panning = true;
                Cursor = Cursors.SizeAll;
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                int hit = HitLayer(e.Location);
                if (hit >= 0 && !ModifierKeys.HasFlag(Keys.Shift))
                {
                    if (hit != SelectedLayer) LayerClicked?.Invoke(hit);
                    dragLayer = true;
                    dragAccX = dragAccY = 0;
                    Cursor = Cursors.SizeAll;
                    return;
                }
                firing = true;
                animator.Input("left", true);
                UpdateTimer(true);
            }
            else if (e.Button == MouseButtons.Right)
            {
                aiming = true;
                animator.Input("right", true);
                UpdateTimer(true);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int dx = e.X - lastMouse.X, dy = e.Y - lastMouse.Y;
            if (panning)
            {
                pan = new PointF(pan.X + dx, pan.Y + dy);
                lastMouse = e.Location;
                Invalidate();
            }
            else if (dragLayer && SelectedLayer >= 0)
            {
                dragAccX += dx / (double)zoom;
                dragAccY += dy / (double)zoom;
                int mx = (int)Math.Truncate(dragAccX), my = (int)Math.Truncate(dragAccY);
                if (mx != 0 || my != 0)
                {
                    dragAccX -= mx; dragAccY -= my;
                    LayerDragged?.Invoke(SelectedLayer, mx, my);
                }
                lastMouse = e.Location;
            }
            else if (e.Button == MouseButtons.None)
                Cursor = HitLayer(e.Location) >= 0 ? Cursors.Hand : Cursors.Cross;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (panning) { panning = false; Cursor = Cursors.Cross; }
            if (dragLayer) { dragLayer = false; Cursor = Cursors.Hand; LayerDragEnded?.Invoke(); }
            if (firing && e.Button == MouseButtons.Left) { firing = false; animator.Input("left", false); UpdateTimer(true); }
            if (aiming && e.Button == MouseButtons.Right) { aiming = false; animator.Input("right", false); UpdateTimer(true); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var before = ScreenToDesign(e.Location);
            int nz = e.Delta > 0 ? (zoom < 4 ? zoom + 1 : (int)Math.Ceiling(zoom * 1.25)) : (zoom <= 4 ? zoom - 1 : (int)Math.Floor(zoom / 1.25));
            nz = Math.Max(1, Math.Min(24, nz));
            if (nz == zoom) return;
            zoom = nz;
            // keep the point under the cursor fixed
            var after = DesignToScreen(before.X, before.Y);
            pan = new PointF(pan.X + (e.X - after.X), pan.Y + (e.Y - after.Y));
            Invalidate();
            ZoomChanged?.Invoke();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (SelectedLayer < 0) return;
            int step = e.Shift ? 10 : 1;
            int dx = e.KeyCode == Keys.Left ? -step : e.KeyCode == Keys.Right ? step : 0;
            int dy = e.KeyCode == Keys.Up ? -step : e.KeyCode == Keys.Down ? step : 0;
            if (dx != 0 || dy != 0) { LayerDragged?.Invoke(SelectedLayer, dx, dy); LayerDragEnded?.Invoke(); }
        }

        protected override bool IsInputKey(Keys k) => k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            PaintBackground(g);

            List<object> frameLayers = animator.Evaluate(out bool animating);
            if (frameDirty || animating || frame == null)
            {
                frame?.Dispose();
                try { frame = CrosshairRenderer.Render(frameLayers, 1, Environment.TickCount); } catch { frame = null; }
                frameDirty = false;
            }
            if (!animating && !(frame?.TimeDependent ?? false) && !firing && !aiming) UpdateTimer(false);

            var c = Center;
            if (ShowCenter)
            {
                using (var p = new Pen(Color.FromArgb(40, 255, 255, 255)) { DashStyle = DashStyle.Dash })
                {
                    float cx = (float)Math.Round(c.X), cy = (float)Math.Round(c.Y);
                    g.DrawLine(p, cx, 0, cx, Height);
                    g.DrawLine(p, 0, cy, Width, cy);
                }
            }
            if (frame != null)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                float x = c.X - frame.OriginX * zoom, y = c.Y - frame.OriginY * zoom;
                g.DrawImage(frame.Bitmap, (float)Math.Round(x), (float)Math.Round(y), frame.Bitmap.Width * zoom, frame.Bitmap.Height * zoom);
            }
            if (ShowGrid && zoom >= 6) DrawPixelGrid(g, c);

            if (SelectedLayer >= 0 && SelectedLayer < layers.Count && layers[SelectedLayer] is Dictionary<string, object> sel)
            {
                var b = CrosshairRenderer.LayerBounds(sel);
                var tl = DesignToScreen(b.X, b.Y);
                var rect = new RectangleF(tl.X - 2, tl.Y - 2, b.Width * zoom + 4, b.Height * zoom + 4);
                g.SmoothingMode = SmoothingMode.None;
                using (var p = new Pen(Theme.Blend(Theme.Accent, Color.Transparent, .25), 1) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(p, rect.X, rect.Y, rect.Width, rect.Height);
            }
            if (ShowRuler)
            {
                // scale bar: 10 design pixels at the current zoom
                g.SmoothingMode = SmoothingMode.None;
                int len = 10 * zoom, x0 = Theme.S(18), y0 = Height - Theme.S(RulerBottom);
                using (var p = new Pen(Theme.TextDim))
                {
                    g.DrawLine(p, x0, y0, x0 + len, y0);
                    g.DrawLine(p, x0, y0 - Theme.S(4), x0, y0 + Theme.S(4));
                    g.DrawLine(p, x0 + len, y0 - Theme.S(4), x0 + len, y0 + Theme.S(4));
                }
                Theme.DrawText(g, "10px", Theme.Small, Theme.TextDim, new Rectangle(x0, y0 - Theme.S(24), Theme.S(60), Theme.S(18)));
            }
            if (animator.HasTimelines && ShowHint)
                Theme.DrawText(g, "Hold left or right mouse on empty space to preview animations", Theme.Small, Theme.TextDim,
                    new Rectangle(Theme.S(12), Theme.S(10), Width - Theme.S(24), Theme.S(22)), Theme.Center);
        }

        public bool ShowRuler = true;
        public int RulerBottom = 26;
        public bool ShowHint = true;

        void DrawPixelGrid(Graphics g, PointF c)
        {
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.None;
            using (var p = new Pen(Color.FromArgb(zoom >= 12 ? 26 : 16, 255, 255, 255)))
            {
                float startX = (float)(c.X - Math.Ceiling(c.X / zoom) * zoom);
                for (float x = (float)Math.Round(startX); x < Width; x += zoom) g.DrawLine(p, x, 0, x, Height);
                float startY = (float)(c.Y - Math.Ceiling(c.Y / zoom) * zoom);
                for (float y = (float)Math.Round(startY); y < Height; y += zoom) g.DrawLine(p, 0, y, Width, y);
            }
        }

        void PaintBackground(Graphics g)
        {
            switch (Background)
            {
                case "light": g.Clear(Color.FromArgb(214, 219, 226)); break;
                case "checker":
                    {
                        g.Clear(Color.FromArgb(60, 64, 72));
                        int s = Theme.S(12);
                        using (var b = new SolidBrush(Color.FromArgb(44, 48, 56)))
                            for (int y = 0; y < Height; y += s)
                                for (int x = ((y / s) % 2) * s; x < Width; x += s * 2)
                                    g.FillRectangle(b, x, y, s, s);
                        break;
                    }
                case "scene":
                    {
                        if (sceneBitmap == null) sceneBitmap = BuildScene();
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(sceneBitmap, new Rectangle(0, 0, Width, Height));
                        break;
                    }
                default: g.Clear(Theme.Canvas); break;
            }
        }

        /// <summary>A procedural "game-like" backdrop (sky, haze, terrain, structures) for judging contrast.</summary>
        static Bitmap BuildScene()
        {
            var bmp = new Bitmap(800, 500);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var sky = new LinearGradientBrush(new Rectangle(0, 0, 800, 300), Color.FromArgb(120, 168, 214), Color.FromArgb(222, 214, 196), 90f))
                    g.FillRectangle(sky, 0, 0, 800, 300);
                var rnd = new Random(7);
                using (var far = new SolidBrush(Color.FromArgb(132, 146, 150)))
                {
                    var pts = new List<PointF> { new PointF(0, 300) };
                    for (int x = 0; x <= 800; x += 40) pts.Add(new PointF(x, 230 + (float)(rnd.NextDouble() * 50)));
                    pts.Add(new PointF(800, 300));
                    g.FillPolygon(far, pts.ToArray());
                }
                using (var ground = new LinearGradientBrush(new Rectangle(0, 290, 800, 210), Color.FromArgb(150, 128, 92), Color.FromArgb(78, 66, 48), 90f))
                    g.FillRectangle(ground, 0, 290, 800, 210);
                Color[] walls = { Color.FromArgb(176, 160, 132), Color.FromArgb(96, 92, 88), Color.FromArgb(205, 196, 180), Color.FromArgb(60, 64, 70) };
                for (int i = 0; i < 9; i++)
                {
                    int w = 50 + rnd.Next(120), h = 60 + rnd.Next(160), x = rnd.Next(760), y = 300 - h + rnd.Next(30);
                    using (var b = new SolidBrush(walls[i % walls.Length])) g.FillRectangle(b, x, y, w, h);
                    using (var b = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillRectangle(b, x + w - 10, y, 10, h);
                }
                using (var b = new SolidBrush(Color.FromArgb(40, 30, 24))) g.FillEllipse(b, 330, 360, 160, 50);
                using (var b = new SolidBrush(Color.FromArgb(70, 90, 60))) for (int i = 0; i < 30; i++) g.FillEllipse(b, rnd.Next(800), 320 + rnd.Next(170), 20 + rnd.Next(40), 10 + rnd.Next(14));
            }
            return bmp;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); frame?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
