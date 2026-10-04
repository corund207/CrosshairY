using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Reticly.Core;

namespace Reticly.UI
{
    /// <summary>
    /// First-run tour: coach marks over the real window. Each step takes a snapshot of the form, dims it, cuts out the
    /// highlighted element and shows a bubble next to it. Shown once; replay from Settings › Interface or Help.
    /// </summary>
    public sealed class Tour : Control
    {
        sealed class Step { public string Page, Target, Title, Body; }

        static readonly Step[] Steps =
        {
            new Step { Page = "crosshairs", Target = "crosshairs", Title = "Browse & save crosshairs",
                Body = "Browse has 100+ designs, including 46 recoil-tracking crosshairs. Click one to try it on your screen, and bookmark it to keep it in Saved. Import codes from Crosshair X, VALORANT and CS2 with Ctrl+I." },
            new Step { Page = "crosshairs", Target = "pill", Title = "Show or hide your crosshair",
                Body = "This switch turns the on-screen crosshair on and off. In game, use the Global Toggle key (Shift + Alt + Z by default)." },
            new Step { Page = "designer", Target = "designer", Title = "Make your own",
                Body = "The Designer has layers, shapes, text, images, animations and recoil tracking. Any layer can follow a weapon's spray." },
            new Step { Page = "keybinds", Target = "keybinds", Title = "Keybinds & recoil loadout",
                Body = "Bind keys to show, hide and switch crosshairs. Set up a recoil loadout so your weapon keys (1, 2, 3…) also switch the spray pattern — and even the crosshair." },
            new Step { Page = "keybinds", Target = "profile", Title = "A profile for every game",
                Body = "Each profile keeps its own crosshair, keybinds, loadout and position, and can switch automatically when its game is focused." },
            new Step { Page = "settings", Target = "settings", Title = "Settings",
                Body = "Hit markers, updates, language, backups, fullscreen help and more live here. That's it — have fun!" },
        };

        readonly MainForm form;
        Bitmap shot;
        int index = -1;
        Rectangle target, bubble, nextBtn, skipBtn;
        int hot = 0;   // 1 = next, 2 = skip
        static Tour current;

        public static void Start(MainForm form, int step = 0)
        {
            current?.Finish(false);
            var t = new Tour(form);
            current = t;
            form.Controls.Add(t);
            t.Bounds = form.ClientRectangle;
            t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            t.Go(Math.Max(0, Math.Min(Steps.Length - 1, step)));
        }

        Tour(MainForm f)
        {
            form = f;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Default;
            TabStop = true;
        }

        void Go(int i)
        {
            if (i >= Steps.Length) { Finish(true); return; }
            index = i;
            var s = Steps[i];
            Visible = false;
            form.Navigate(s.Page);
            form.Refresh();
            Application.DoEvents();
            shot?.Dispose();
            shot = Capture(form);
            target = form.TourTarget(s.Target);
            target.Inflate(Theme.S(4), Theme.S(4));
            LayoutBubble();
            Bounds = form.ClientRectangle;
            Visible = true;
            BringToFront();
            Focus();
            Invalidate();
        }

        void Finish(bool completed)
        {
            var st = AppController.I.State;
            if (!st.Settings.TourDone) { st.Settings.TourDone = true; st.MarkSettingsChanged(); }
            if (current == this) current = null;
            Parent?.Controls.Remove(this);
            shot?.Dispose();
            shot = null;
            if (completed) form.Navigate("crosshairs");
            Dispose();
        }

        void LayoutBubble()
        {
            int w = Theme.S(340);
            var s = Steps[index];
            int textH = TextRenderer.MeasureText(L.T(s.Body), Theme.Body, new Size(w - Theme.S(36), 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            int h = Theme.S(18 + 26 + 10) + textH + Theme.S(20 + 34 + 18);
            var area = form.ClientRectangle;
            // to the right of sidebar items, below title-bar items
            Point p = target.X < Theme.S(220)
                ? new Point(target.Right + Theme.S(18), target.Y - Theme.S(8))
                : new Point(target.Right - w, target.Bottom + Theme.S(16));
            p.X = Math.Max(Theme.S(12), Math.Min(area.Width - w - Theme.S(12), p.X));
            p.Y = Math.Max(Theme.S(12), Math.Min(area.Height - h - Theme.S(12), p.Y));
            bubble = new Rectangle(p, new Size(w, h));
            int by = bubble.Bottom - Theme.S(18 + 34);
            string next = L.T(index == Steps.Length - 1 ? "Done" : "Next");
            int nw = Theme.TextWidth(next, Theme.BodyMedium) + Theme.S(36);
            nextBtn = new Rectangle(bubble.Right - Theme.S(18) - nw, by, nw, Theme.S(34));
            int sw = Theme.TextWidth(L.T("Skip tour"), Theme.Body) + Theme.S(24);
            skipBtn = new Rectangle(nextBtn.X - Theme.S(8) - sw, by, sw, Theme.S(34));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (shot != null) g.DrawImageUnscaled(shot, 0, 0); else g.Clear(Theme.Bg);
            Theme.Smooth(g);
            // dim everything except the target
            using (var region = new Region(ClientRectangle))
            using (var hole = Theme.Round(target, Theme.SF(10)))
            {
                region.Exclude(hole);
                using (var b = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillRegion(b, region);
            }
            Theme.StrokeRound(g, Theme.Accent, target, Theme.SF(10), Theme.SF(2));

            var s = Steps[index];
            using (var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillRectangle(shadow, bubble.X + 3, bubble.Y + 5, bubble.Width, bubble.Height);
            Theme.FillRound(g, Theme.Surface, bubble, Theme.SF(10));
            Theme.StrokeRound(g, Theme.AccentBorder, bubble, Theme.SF(10));
            int x = bubble.X + Theme.S(18), y = bubble.Y + Theme.S(16), w = bubble.Width - Theme.S(36);
            Theme.DrawText(g, (index + 1) + " / " + Steps.Length, Theme.Caption, Theme.Accent, new Rectangle(x, y, w, Theme.S(16)), Theme.Right);
            Theme.DrawText(g, L.T(s.Title), Theme.H3, Theme.Text, new Rectangle(x, y, w - Theme.S(50), Theme.S(26)));
            y += Theme.S(26 + 10);
            TextRenderer.DrawText(g, L.T(s.Body), Theme.Body, new Rectangle(x, y, w, nextBtn.Y - y - Theme.S(10)), Theme.TextDim, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            Theme.FillRound(g, hot == 1 ? Theme.AccentHover : Theme.Accent, nextBtn, Theme.SF(7));
            Theme.DrawText(g, L.T(index == Steps.Length - 1 ? "Done" : "Next"), Theme.BodyMedium, Color.FromArgb(24, 18, 8), nextBtn, Theme.Center);
            if (hot == 2) Theme.FillRound(g, Theme.Surface3, skipBtn, Theme.SF(7));
            Theme.DrawText(g, L.T("Skip tour"), Theme.Body, Theme.TextDim, skipBtn, Theme.Center);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = nextBtn.Contains(e.Location) ? 1 : skipBtn.Contains(e.Location) ? 2 : 0;
            Cursor = h != 0 ? Cursors.Hand : Cursors.Default;
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (nextBtn.Contains(e.Location)) Go(index + 1);
            else if (skipBtn.Contains(e.Location)) Finish(false);
        }

        protected override bool IsInputKey(Keys k) => k == Keys.Right || k == Keys.Left || k == Keys.Enter || k == Keys.Escape || base.IsInputKey(k);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Finish(false);
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Right || e.KeyCode == Keys.Space) Go(index + 1);
            else if (e.KeyCode == Keys.Left && index > 0) Go(index - 1);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // the snapshot no longer matches the window: retake it for the current step
            if (index >= 0 && Visible && shot != null && (shot.Width < Width || shot.Height < Height)) BeginInvoke(new Action(() => { if (!IsDisposed) Go(index); }));
        }

        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        /// <summary>Snapshot of the form's client area (PrintWindow captures the custom chrome too).</summary>
        static Bitmap Capture(Form f)
        {
            var bmp = new Bitmap(Math.Max(1, f.ClientSize.Width), Math.Max(1, f.ClientSize.Height));
            using (var full = new Bitmap(Math.Max(1, f.Width), Math.Max(1, f.Height)))
            {
                using (var g = Graphics.FromImage(full))
                {
                    var hdc = g.GetHdc();
                    bool ok = PrintWindow(f.Handle, hdc, 2);
                    g.ReleaseHdc(hdc);
                    if (!ok) f.DrawToBitmap(full, new Rectangle(0, 0, f.Width, f.Height));
                }
                var origin = f.PointToScreen(Point.Empty);
                int ox = origin.X - f.Bounds.X, oy = origin.Y - f.Bounds.Y;
                using (var g = Graphics.FromImage(bmp)) g.DrawImage(full, new Rectangle(0, 0, bmp.Width, bmp.Height), new Rectangle(ox, oy, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
            }
            return bmp;
        }
    }
}
