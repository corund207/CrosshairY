using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.Render;

namespace CrosshairY.Overlay
{
    /// <summary>Per-pixel-alpha, click-through, always-on-top window that never takes focus.</summary>
    public sealed class OverlayWindow : Form
    {
        IntPtr screenDC, memDC, dib, dibBits, oldObj;
        int dibW, dibH;
        readonly object gdiLock = new object();

        public OverlayWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "CrosshairY Overlay";
            Size = new Size(1, 1);
            Location = new Point(-10000, -10000);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, HTTRANSPARENT = -1, WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
            if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)HTTRANSPARENT; return; }
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        public void EnsureTopmost()
        {
            if (!IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
        }

        public void SetCaptureExcluded(bool excluded)
        {
            if (!IsHandleCreated) return;
            try { Native.SetWindowDisplayAffinity(Handle, excluded ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE); } catch { }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        static extern void CopyMemory(IntPtr dest, IntPtr src, IntPtr count);

        /// <summary>Pushes a premultiplied ARGB frame to the screen. Safe to call from the render thread.</summary>
        public void Present(Bitmap bmp, int x, int y, byte alpha)
        {
            if (!IsHandleCreated || bmp == null) return;
            lock (gdiLock)
            {
                int w = bmp.Width, h = bmp.Height;
                EnsureDib(w, h);
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    for (int row = 0; row < h; row++)
                        CopyMemory(dibBits + row * w * 4, data.Scan0 + row * data.Stride, (IntPtr)(w * 4));
                }
                finally { bmp.UnlockBits(data); }

                var dst = new Native.POINT(x, y);
                var size = new Native.SIZE(w, h);
                var src = new Native.POINT(0, 0);
                var blend = new Native.BLENDFUNCTION { BlendOp = Native.AC_SRC_OVER, SourceConstantAlpha = alpha, AlphaFormat = Native.AC_SRC_ALPHA };
                Native.UpdateLayeredWindow(Handle, screenDC, ref dst, ref size, memDC, ref src, 0, ref blend, Native.ULW_ALPHA);
            }
        }

        /// <summary>Hides the crosshair instantly without hiding the window (no flicker, no focus changes).</summary>
        public void PresentAlphaOnly(byte alpha, int x, int y)
        {
            if (!IsHandleCreated || memDC == IntPtr.Zero) return;
            lock (gdiLock)
            {
                var dst = new Native.POINT(x, y);
                var size = new Native.SIZE(dibW, dibH);
                var src = new Native.POINT(0, 0);
                var blend = new Native.BLENDFUNCTION { BlendOp = Native.AC_SRC_OVER, SourceConstantAlpha = alpha, AlphaFormat = Native.AC_SRC_ALPHA };
                Native.UpdateLayeredWindow(Handle, screenDC, ref dst, ref size, memDC, ref src, 0, ref blend, Native.ULW_ALPHA);
            }
        }

        void EnsureDib(int w, int h)
        {
            if (dib != IntPtr.Zero && w == dibW && h == dibH) return;
            FreeDib();
            screenDC = Native.GetDC(IntPtr.Zero);
            memDC = Native.CreateCompatibleDC(screenDC);
            var bmi = new BITMAPINFOHEADER { biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            dib = CreateDIBSection(screenDC, ref bmi, 0, out dibBits, IntPtr.Zero, 0);
            oldObj = Native.SelectObject(memDC, dib);
            dibW = w; dibH = h;
        }

        void FreeDib()
        {
            if (memDC != IntPtr.Zero)
            {
                if (oldObj != IntPtr.Zero) Native.SelectObject(memDC, oldObj);
                Native.DeleteDC(memDC);
                memDC = IntPtr.Zero;
            }
            if (dib != IntPtr.Zero) { Native.DeleteObject(dib); dib = IntPtr.Zero; }
            if (screenDC != IntPtr.Zero) { Native.ReleaseDC(IntPtr.Zero, screenDC); screenDC = IntPtr.Zero; }
        }

        protected override void Dispose(bool disposing)
        {
            lock (gdiLock) FreeDib();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Owns the overlay window and a dedicated render thread that evaluates animations and redraws at high
    /// frame rates only while something is moving; static crosshairs cost no CPU.
    /// </summary>
    public sealed class OverlayController : IDisposable
    {
        readonly OverlayWindow window;
        readonly Thread thread;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly ConcurrentQueue<Action<Animator>> animatorOps = new ConcurrentQueue<Action<Animator>>();
        readonly object stateLock = new object();
        volatile bool running = true;

        // shared state (guarded by stateLock)
        List<object> layers;
        bool layersChanged = true;
        Rectangle monitor = Screen.PrimaryScreen.Bounds;
        int offsetX, offsetY;
        double scale = 1;
        bool visible = true;
        double opacity = 1;
        bool placementChanged = true;
        // reaction (hit marker / kill flash) drawn on top of the crosshair for a short time
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        Func<double, List<object>> reactBuild;
        long reactStart;
        int reactMs = 1;
        bool reactChanged;

        public OverlayWindow Window => window;
        /// <summary>Target redraw rate while animating.</summary>
        public int FrameRate = 144;
        public event Action<bool> AnimatingChanged;

        public OverlayController()
        {
            window = new OverlayWindow();
            window.Show();
            thread = new Thread(Loop) { IsBackground = true, Name = "OverlayRender", Priority = ThreadPriority.AboveNormal };
            thread.Start();
        }

        public void SetCrosshair(List<object> l)
        {
            lock (stateLock) { layers = l == null ? null : (List<object>)J.DeepClone(l); layersChanged = true; }
            wake.Set();
        }

        public void SetPlacement(Rectangle monitorBounds, int offX, int offY, double sc)
        {
            lock (stateLock)
            {
                if (monitor == monitorBounds && offsetX == offX && offsetY == offY && Math.Abs(scale - sc) < 1e-9) return;
                bool scaleChanged = Math.Abs(scale - sc) > 1e-9;
                monitor = monitorBounds; offsetX = offX; offsetY = offY; scale = sc;
                placementChanged = true;
                if (scaleChanged) layersChanged = true;
            }
            wake.Set();
        }

        public void SetVisible(bool v, double op)
        {
            lock (stateLock)
            {
                if (visible == v && Math.Abs(opacity - op) < 1e-9) return;
                visible = v; opacity = op; placementChanged = true;
            }
            wake.Set();
        }

        public void TriggerInput(string trigger, bool down)
        {
            animatorOps.Enqueue(a => a.Input(trigger, down));
            wake.Set();
        }

        /// <summary>Plays a short reaction: build(t) returns the extra layers for progress t (0 → 1) over ms milliseconds.</summary>
        public void React(Func<double, List<object>> build, int ms)
        {
            lock (stateLock) { reactBuild = build; reactStart = clock.ElapsedMilliseconds; reactMs = Math.Max(1, ms); reactChanged = true; }
            wake.Set();
        }

        public void ResetAnimations()
        {
            animatorOps.Enqueue(a => a.Reset());
            wake.Set();
        }

        void Loop()
        {
            Native.timeBeginPeriod(1);
            var animator = new Animator();
            int fixedHalf = 0, reactHalf = 0;
            bool lastReacting = false;
            RenderResult last = null;
            bool lastAnimating = false;
            bool timeDependent = false;
            int lastX = int.MinValue, lastY = int.MinValue;
            byte lastAlpha = 255;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                while (running)
                {
                    List<object> curLayers; bool lc, pc, vis; Rectangle mon; int ox, oy; double sc, op;
                    Func<double, List<object>> rb; long rs; int rms; bool rc;
                    lock (stateLock)
                    {
                        curLayers = layers; lc = layersChanged; pc = placementChanged; vis = visible;
                        mon = monitor; ox = offsetX; oy = offsetY; sc = scale; op = opacity;
                        layersChanged = false; placementChanged = false;
                        rb = reactBuild; rs = reactStart; rms = reactMs; rc = reactChanged; reactChanged = false;
                    }
                    if (lc)
                    {
                        animator.Load(curLayers);
                        fixedHalf = curLayers == null ? 0 : CrosshairRenderer.ComputeCanvasHalf(curLayers, sc);
                    }
                    while (animatorOps.TryDequeue(out var op2)) op2(animator);
                    if ((rc || lc) && rb != null)
                    {
                        try { reactHalf = Math.Max(CrosshairRenderer.ComputeCanvasHalf(rb(0), sc), CrosshairRenderer.ComputeCanvasHalf(rb(1), sc)); }
                        catch { reactHalf = 0; }
                    }
                    double rt = rb == null ? 2 : (clock.ElapsedMilliseconds - rs) / (double)rms;
                    bool reacting = rb != null && rt < 1 && curLayers != null;
                    int half = reacting ? Math.Max(fixedHalf, reactHalf) : fixedHalf;

                    bool animating = false;
                    byte alpha = (byte)Math.Round(Math.Max(0, Math.Min(1, op)) * 255);
                    if (!vis || curLayers == null) alpha = 0;
                    List<object> frameLayers = curLayers == null ? null : animator.Evaluate(out animating);
                    if (reacting)
                    {
                        frameLayers = new List<object>(frameLayers);
                        try { frameLayers.AddRange(rb(Math.Max(0, rt))); } catch { }
                        animating = true;
                    }
                    bool needRender = lc || ((animating || timeDependent || animating != lastAnimating) && alpha > 0) || (last == null && curLayers != null)
                        || (reacting != lastReacting && alpha > 0);
                    lastReacting = reacting;
                    if (curLayers != null && needRender && (alpha > 0 || lc))
                    {
                        RenderResult r;
                        try { r = CrosshairRenderer.Render(frameLayers, sc, sw.ElapsedMilliseconds, half); }
                        catch { r = null; }
                        if (r != null)
                        {
                            last?.Dispose();
                            last = r;
                            timeDependent = r.TimeDependent;
                            int x = mon.Left + mon.Width / 2 + ox - r.OriginX;
                            int y = mon.Top + mon.Height / 2 + oy - r.OriginY;
                            try { window.Present(r.Bitmap, x, y, alpha); } catch { }
                            lastX = x; lastY = y; lastAlpha = alpha;
                        }
                    }
                    else if (last != null && (pc || alpha != lastAlpha))
                    {
                        int x = mon.Left + mon.Width / 2 + ox - last.OriginX;
                        int y = mon.Top + mon.Height / 2 + oy - last.OriginY;
                        try { window.PresentAlphaOnly(alpha, x, y); } catch { }
                        lastX = x; lastY = y; lastAlpha = alpha;
                    }
                    else if (curLayers == null && lastAlpha != 0 && last != null)
                    {
                        try { window.PresentAlphaOnly(0, lastX, lastY); } catch { }
                        lastAlpha = 0;
                    }

                    if (animating != lastAnimating)
                    {
                        lastAnimating = animating;
                        try { AnimatingChanged?.Invoke(animating); } catch { }
                    }
                    if ((animating || timeDependent) && alpha > 0) wake.WaitOne(Math.Max(2, 1000 / Math.Max(30, FrameRate)));
                    else if (animating) wake.WaitOne(16);
                    else wake.WaitOne(1000);
                }
            }
            finally
            {
                last?.Dispose();
                Native.timeEndPeriod(1);
            }
        }

        public void Dispose()
        {
            running = false;
            wake.Set();
            thread.Join(500);
            if (window.InvokeRequired) window.BeginInvoke(new Action(window.Close));
            else window.Close();
        }
    }
}
