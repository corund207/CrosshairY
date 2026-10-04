using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Reticly.Core;
using Reticly.Input;

namespace Reticly.UI.Controls
{
    /// <summary>Color swatch + hex text; opens an HSV popup picker.</summary>
    public class ColorButton : DarkControl
    {
        Color color = Color.White;
        public event EventHandler ColorChanged;      // live while dragging in the popup
        public event EventHandler ColorCommitted;    // when the popup closes / hex entered

        public ColorButton()
        {
            Height = Theme.S(34);
            Width = Theme.S(130);
            Cursor = Cursors.Hand;
        }

        public Color Color
        {
            get => color;
            set { color = value; Invalidate(); }
        }

        public string Hex => ColorUtil.ToHex(color);

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var popup = new ColorPopup(color);
            popup.ColorChanged += (s, a) => { color = popup.Color; Invalidate(); ColorChanged?.Invoke(this, EventArgs.Empty); };
            popup.FormClosed += (s, a) => ColorCommitted?.Invoke(this, EventArgs.Empty);
            var screenPt = PointToScreen(new Point(Width - popup.Width, Height + Theme.S(4)));
            var wa = Screen.FromControl(this).WorkingArea;
            if (screenPt.Y + popup.Height > wa.Bottom) screenPt.Y = PointToScreen(Point.Empty).Y - popup.Height - Theme.S(4);
            if (screenPt.X < wa.Left) screenPt.X = wa.Left;
            popup.Location = screenPt;
            popup.Show(FindForm());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, hover ? Theme.Surface3 : Theme.Surface2, r, Theme.SF(8));
            Theme.StrokeRound(g, Theme.Border, r, Theme.SF(8));
            var sw = new RectangleF(Theme.SF(6), Theme.SF(6), Height - Theme.SF(12), Height - Theme.SF(12));
            DrawChecker(g, sw);
            Theme.FillRound(g, color, sw, Theme.SF(5));
            Theme.StrokeRound(g, Color.FromArgb(60, 255, 255, 255), sw, Theme.SF(5));
            Theme.DrawText(g, ColorUtil.ToHex(color), Theme.Mono, Theme.Text, new Rectangle((int)(sw.Right + Theme.S(8)), 0, Width - (int)sw.Right - Theme.S(10), Height));
        }

        internal static void DrawChecker(Graphics g, RectangleF r)
        {
            using (var path = Theme.Round(r, Theme.SF(5)))
            {
                var st = g.Save();
                g.SetClip(path);
                g.Clear(Color.FromArgb(200, 200, 200));
                float s = Theme.SF(5);
                using (var b = new SolidBrush(Color.FromArgb(120, 120, 120)))
                    for (float y = r.Y; y < r.Bottom; y += s)
                        for (float x = r.X + ((int)((y - r.Y) / s) % 2) * s; x < r.Right; x += 2 * s)
                            g.FillRectangle(b, x, y, s, s);
                g.Restore(st);
            }
        }
    }

    /// <summary>Borderless HSV color picker popup with hex input and preset swatches.</summary>
    public class ColorPopup : Form
    {
        double h, s, v;
        Color current;
        readonly TextField hexBox;
        Rectangle svRect, hueRect;
        int dragging; // 1 = sv, 2 = hue
        public event EventHandler ColorChanged;
        static readonly List<Color> recent = new List<Color>();
        static readonly string[] Presets =
        {
            "#FFFFFF", "#000000", "#FF0000", "#FF5A90", "#FF00FF", "#A855F7", "#3B82F6", "#00C5FF", "#00FFFF", "#2EE6A6",
            "#00FF00", "#7FFF00", "#DFFF00", "#FFFF00", "#FFB547", "#FF7A00", "#C0C0C0", "#808080"
        };

        public ColorPopup(Color initial)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Surface2;
            KeyPreview = true;
            Size = new Size(Theme.S(264), Theme.S(330));
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            current = Color.FromArgb(255, initial);
            RgbToHsv(current, out h, out s, out v);
            int pad = Theme.S(12);
            svRect = new Rectangle(pad, pad, Width - pad * 2 - Theme.S(30), Theme.S(170));
            hueRect = new Rectangle(svRect.Right + Theme.S(10), pad, Theme.S(20), svRect.Height);
            hexBox = new TextField(ColorUtil.ToHex(current)) { Location = new Point(pad, svRect.Bottom + Theme.S(12)), Width = Width - pad * 2 - Theme.S(44) };
            hexBox.Box.Font = Theme.Mono;
            hexBox.Committed += (sender, e) =>
            {
                var t = hexBox.Text.Trim();
                if (!t.StartsWith("#")) t = "#" + t;
                var c = ColorUtil.Parse(t, Color.Empty);
                if (c != Color.Empty) { SetColor(Color.FromArgb(255, c), true); }
            };
            Controls.Add(hexBox);
            KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        public Color Color => current;

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // CS_DROPSHADOW
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!recent.Contains(current)) { recent.Insert(0, current); if (recent.Count > 9) recent.RemoveAt(9); }
            Close();
        }

        void SetColor(Color c, bool updateHsv)
        {
            current = c;
            if (updateHsv) RgbToHsv(c, out h, out s, out v);
            if (!hexBox.Box.Focused) hexBox.Text = ColorUtil.ToHex(c);
            Invalidate();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        IEnumerable<(Rectangle r, Color c)> Swatches()
        {
            int pad = Theme.S(12), size = Theme.S(20), gap = Theme.S(4);
            int y = svRect.Bottom + Theme.S(56);
            int x = pad;
            int perRow = (Width - pad * 2 + gap) / (size + gap);
            var all = Presets.Select(p => ColorUtil.Parse(p)).Concat(recent.Where(r => !Presets.Contains(ColorUtil.ToHex(r)))).Take(perRow * 3).ToList();
            for (int i = 0; i < all.Count; i++)
            {
                int col = i % perRow, row = i / perRow;
                yield return (new Rectangle(x + col * (size + gap), y + row * (size + gap), size, size), all[i]);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Theme.Surface2);
            using (var p = new Pen(Theme.BorderStrong)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            // SV square
            var hueColor = HsvToRgb(h, 1, 1);
            using (var b = new LinearGradientBrush(svRect, Color.White, hueColor, 0f)) g.FillRectangle(b, svRect);
            using (var b = new LinearGradientBrush(svRect, Color.Transparent, Color.Black, 90f)) g.FillRectangle(b, svRect);
            int sx = svRect.X + (int)(s * svRect.Width), sy = svRect.Y + (int)((1 - v) * svRect.Height);
            using (var p = new Pen(Color.White, 2)) g.DrawEllipse(p, sx - 6, sy - 6, 12, 12);
            using (var p = new Pen(Color.Black, 1)) g.DrawEllipse(p, sx - 7, sy - 7, 14, 14);
            // hue strip
            using (var b = new LinearGradientBrush(hueRect, Color.Red, Color.Red, 90f))
            {
                var blend = new ColorBlend
                {
                    Colors = new[] { Color.Red, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta, Color.Red },
                    Positions = new[] { 0f, 1 / 6f, 2 / 6f, 3 / 6f, 4 / 6f, 5 / 6f, 1f }
                };
                b.InterpolationColors = blend;
                g.FillRectangle(b, hueRect);
            }
            int hy = hueRect.Y + (int)(h / 360 * hueRect.Height);
            using (var p = new Pen(Color.White, 2)) g.DrawRectangle(p, hueRect.X - 2, hy - 3, hueRect.Width + 3, 6);
            // preview swatch
            var prev = new Rectangle(hexBox.Right + Theme.S(8), hexBox.Top, Theme.S(36), hexBox.Height);
            Theme.FillRound(g, current, prev, Theme.SF(6));
            Theme.StrokeRound(g, Theme.Border, prev, Theme.SF(6));
            Theme.DrawText(g, "PRESETS & RECENT", Theme.SmallBold, Theme.TextMute, new Rectangle(Theme.S(12), hexBox.Bottom + Theme.S(4), Width, Theme.S(18)));
            foreach (var (r, c) in Swatches())
            {
                Theme.FillRound(g, c, r, Theme.SF(4));
                Theme.StrokeRound(g, Color.FromArgb(50, 255, 255, 255), r, Theme.SF(4));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (svRect.Contains(e.Location)) { dragging = 1; UpdateDrag(e.Location); }
            else if (Rectangle.Inflate(hueRect, 4, 0).Contains(e.Location)) { dragging = 2; UpdateDrag(e.Location); }
            else
                foreach (var (r, c) in Swatches())
                    if (r.Contains(e.Location)) { SetColor(c, true); break; }
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging != 0) UpdateDrag(e.Location); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = 0; }

        void UpdateDrag(Point p)
        {
            if (dragging == 1)
            {
                s = Math.Max(0, Math.Min(1, (p.X - svRect.X) / (double)svRect.Width));
                v = 1 - Math.Max(0, Math.Min(1, (p.Y - svRect.Y) / (double)svRect.Height));
            }
            else if (dragging == 2)
                h = Math.Max(0, Math.Min(359.9, (p.Y - hueRect.Y) / (double)hueRect.Height * 360));
            SetColor(HsvToRgb(h, s, v), false);
        }

        static void RgbToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            h = 0;
            if (d > 0)
            {
                if (max == r) h = 60 * (((g - b) / d) % 6);
                else if (max == g) h = 60 * ((b - r) / d + 2);
                else h = 60 * ((r - g) / d + 4);
            }
            if (h < 0) h += 360;
            s = max == 0 ? 0 : d / max;
            v = max;
        }

        static Color HsvToRgb(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromArgb(255, (int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }
    }

    /// <summary>Click, then press any key, mouse button, wheel or controller button. Esc cancels, Backspace clears.</summary>
    public class KeybindBox : DarkControl
    {
        string binding = "";
        bool capturing;
        Mods pendingMods;
        string pendingModifierToken;
        public bool AllowMouseButtons = true;
        public event EventHandler BindingChanged;

        public KeybindBox()
        {
            Height = Theme.S(34);
            Width = Theme.S(170);
            Cursor = Cursors.Hand;
        }

        public string Binding
        {
            get => binding;
            set { binding = value ?? ""; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (capturing) return;
            if (e.Button == MouseButtons.Left) StartCapture();
            else if (e.Button == MouseButtons.Right)
            {
                var m = Menus.Create();
                m.Items.Item("Change…", StartCapture);
                m.Items.Item("Clear", () => SetBinding(""), !string.IsNullOrEmpty(binding));
                m.Show(this, e.Location);
            }
        }

        void StartCapture()
        {
            capturing = true;
            pendingMods = Mods.None;
            pendingModifierToken = null;
            Invalidate();
            AppController.I.CaptureHandler = OnCapture;
            if (Pad) AppController.I.Input.ControllerEnabled = true;
            var f = FindForm();
            if (f != null) f.Deactivate += CancelOnDeactivate;
        }

        void CancelOnDeactivate(object sender, EventArgs e) => StopCapture();

        void StopCapture()
        {
            if (!capturing) return;
            capturing = false;
            if (AppController.I.CaptureHandler == (Action<InputEvent>)OnCapture) AppController.I.CaptureHandler = null;
            AppController.I.Input.ControllerEnabled = AppController.I.State.Settings.ControllerSupport;
            var f = FindForm();
            if (f != null) f.Deactivate -= CancelOnDeactivate;
            Invalidate();
        }

        void OnCapture(InputEvent ev)
        {
            if (!capturing) { AppController.I.CaptureHandler = null; return; }
            if (ev.IsModifier)
            {
                var m = TokenMod(ev.Token);
                if (ev.Down) { pendingMods |= m; pendingModifierToken = ev.Token; Invalidate(); }
                else if (pendingModifierToken == ev.Token)
                {
                    // modifier pressed and released on its own: bind the modifier key itself
                    SetBinding(ev.Token);
                    StopCapture();
                }
                return;
            }
            if (!ev.Down) return;
            if (ev.Token == "Esc") { StopCapture(); return; }
            if (ev.Token == "Backspace" || ev.Token == "Delete") { SetBinding(""); StopCapture(); return; }
            if (!AllowMouseButtons && (ev.Token == "LMB" || ev.Token == "RMB")) return;
            if (Pad && !ev.Token.StartsWith("Pad")) return;
            if (!Pad && Caps && ev.Token.StartsWith("Pad")) return;
            var kb = new KeyBinding { Key = ev.Token, Mods = IsMouseOrPad(ev.Token) ? Mods.None : ev.Mods };
            SetBinding(kb.ToString());
            StopCapture();
        }

        static bool IsMouseOrPad(string t) => t == "LMB" || t == "RMB" || t == "MMB" || t.StartsWith("Mouse") || t.StartsWith("Wheel") || t.StartsWith("Pad");

        static Mods TokenMod(string t)
        {
            if (t.Contains("Ctrl")) return Mods.Ctrl;
            if (t.Contains("Shift")) return Mods.Shift;
            if (t.Contains("Alt")) return Mods.Alt;
            if (t.Contains("Win")) return Mods.Win;
            return Mods.None;
        }

        void SetBinding(string b)
        {
            binding = b;
            Invalidate();
            BindingChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (capturing && AppController.I != null && AppController.I.CaptureHandler == (Action<InputEvent>)OnCapture)
                AppController.I.CaptureHandler = null;
            base.Dispose(disposing);
        }

        /// <summary>Table-cell look: device icon followed by keycaps ("Shift + Alt + Z"), or "Unbound".</summary>
        public bool Caps;
        public bool Pad;

        void PaintCaps(Graphics g)
        {
            var r = new RectangleF(0, 0, Width, Height);
            if (capturing || hover) Theme.FillRound(g, capturing ? Theme.AccentDim : Theme.Surface, r, Theme.SF(7));
            if (capturing) Theme.StrokeRound(g, Theme.AccentBorder, r, Theme.SF(7));
            var kb = KeyBinding.Parse(binding);
            string device = Pad || (kb.Key ?? "").StartsWith("Pad") ? Glyph.Gamepad :
                (kb.Key == "LMB" || kb.Key == "RMB" || kb.Key == "MMB" || (kb.Key ?? "").StartsWith("Mouse") || (kb.Key ?? "").StartsWith("Wheel")) ? Glyph.Mouse : Glyph.Keyboard;
            Theme.DrawIcon(g, device, Theme.IconSmall, Theme.TextDim, new Rectangle(Theme.S(8), 0, Theme.S(22), Height));
            int x = Theme.S(40);
            if (capturing)
            {
                string t = pendingMods != Mods.None ? new KeyBinding { Key = "…", Mods = pendingMods }.ToString().Replace("+", " + ") : "Press a key or button…";
                Theme.DrawText(g, t, Theme.BodyMedium, Theme.Accent, new Rectangle(x, 0, Width - x, Height));
                return;
            }
            if (kb.IsEmpty)
            {
                Theme.DrawText(g, "Unbound", Theme.Body, Theme.TextMute, new Rectangle(x, 0, Width - x, Height));
                return;
            }
            var parts = new List<string>();
            if (kb.Mods.HasFlag(Mods.Ctrl)) parts.Add("Ctrl");
            if (kb.Mods.HasFlag(Mods.Shift)) parts.Add("Shift");
            if (kb.Mods.HasFlag(Mods.Alt)) parts.Add("Alt");
            if (kb.Mods.HasFlag(Mods.Win)) parts.Add("Win");
            parts.Add(CapName(kb.Key));
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    Theme.DrawText(g, "+", Theme.Body, Theme.TextDim, new Rectangle(x, 0, Theme.S(16), Height), Theme.Center);
                    x += Theme.S(16);
                }
                int w = Theme.TextWidth(parts[i], Theme.BodyMedium) + Theme.S(14);
                if (x + w > Width) break;
                var cap = new RectangleF(x, (Height - Theme.S(26)) / 2f, w, Theme.S(26));
                Theme.FillRound(g, Theme.Surface2, cap, Theme.SF(4));
                Theme.DrawText(g, parts[i], Theme.BodyMedium, Theme.Text, Rectangle.Round(cap), Theme.Center);
                x += w;
            }
        }

        static string CapName(string k)
        {
            switch (k)
            {
                case "LMB": return "LeftClick";
                case "RMB": return "RightClick";
                case "MMB": return "MiddleClick";
                case "PadLT": return "LT";
                case "PadRT": return "RT";
                case "PadLB": return "LB";
                case "PadRB": return "RB";
                default: return k != null && k.StartsWith("Pad") ? k.Substring(3) : k;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            if (Caps) { PaintCaps(g); return; }
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, capturing ? Theme.AccentDim : hover ? Theme.Surface3 : Theme.Surface2, r, Theme.SF(8));
            Theme.StrokeRound(g, capturing ? Theme.Accent : Theme.Border, r, Theme.SF(8));
            string text;
            Color c;
            if (capturing)
            {
                text = pendingMods != Mods.None ? new KeyBinding { Key = "…", Mods = pendingMods }.ToString().Replace("+", " + ") : "Press a key…";
                c = Theme.Accent;
            }
            else
            {
                text = KeyBinding.Display(binding);
                c = string.IsNullOrEmpty(binding) ? Theme.TextMute : Theme.Text;
            }
            Theme.DrawIcon(g, Glyph.Keyboard, Theme.IconSmall, capturing ? Theme.Accent : Theme.TextDim, new Rectangle(Theme.S(6), 0, Theme.S(22), Height));
            Theme.DrawText(g, text, Theme.BodyBold, c, new Rectangle(Theme.S(30), 0, Width - Theme.S(36), Height));
        }
    }
}
