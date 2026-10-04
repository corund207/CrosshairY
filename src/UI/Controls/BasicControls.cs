using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CrosshairY.Core;

namespace CrosshairY.UI.Controls
{
    public abstract class DarkControl : Control
    {
        protected bool hover, pressed;

        protected DarkControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
            Font = Theme.Body;
        }

        protected Color ParentBack
        {
            get
            {
                Control p = Parent;
                while (p != null && p.BackColor.A < 255) p = p.Parent;
                return p?.BackColor ?? Theme.Bg;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    public enum ButtonKind { Primary, Secondary, Ghost, Danger, Subtle }

    public class FlatButton : DarkControl
    {
        public ButtonKind Kind = ButtonKind.Secondary;
        public string Glyph;
        public bool Toggled;
        public bool AlignLeft;
        public int Radius = 7;
        public Color Tint = Color.Empty, TintBorder = Color.Empty, TintText = Color.Empty;

        public FlatButton(string text = "", string glyph = null, ButtonKind kind = ButtonKind.Secondary)
        {
            Text = Core.L.T(text);
            Glyph = glyph;
            Kind = kind;
            Cursor = Cursors.Hand;
            Height = Theme.S(34);
            Font = Theme.BodyBold;
            AutoSizeWidth();
        }

        public FlatButton AutoSizeWidth()
        {
            int w = Theme.S(14) * 2;
            if (!string.IsNullOrEmpty(Text)) w += TextRenderer.MeasureText(Text, Font).Width;
            if (!string.IsNullOrEmpty(Glyph)) w += Theme.S(string.IsNullOrEmpty(Text) ? 6 : 24);
            Width = Math.Max(Height, w);
            return this;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            Color bg, fg, border = Color.Empty;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    bg = pressed ? Theme.Blend(Theme.Accent, Color.Black, .15) : hover ? Theme.AccentHover : Theme.Accent;
                    fg = Theme.AccentText; break;
                case ButtonKind.Danger:
                    bg = pressed ? Theme.Blend(Theme.Danger, Color.Black, .2) : hover ? Theme.Blend(Theme.Danger, Color.White, .1) : Theme.Blend(Theme.Surface2, Theme.Danger, .18);
                    fg = hover ? Color.White : Theme.Danger; break;
                case ButtonKind.Ghost:
                    bg = pressed ? Theme.Surface3 : hover ? Theme.Surface2 : Color.Transparent;
                    fg = Toggled ? Theme.Accent : hover ? Theme.Text : Theme.TextDim; break;
                case ButtonKind.Subtle:
                    bg = Toggled ? Theme.AccentDim : pressed ? Theme.Surface3 : hover ? Theme.Surface3 : Theme.Surface2;
                    fg = Toggled ? Theme.Accent : Theme.Text; break;
                default:
                    bg = Toggled ? Theme.AccentDim : pressed ? Theme.Surface3 : hover ? Theme.Surface3 : Theme.Surface2;
                    fg = Toggled ? Theme.Accent : Theme.Text;
                    border = Toggled ? Theme.Accent : Theme.Border; break;
            }
            if (!Tint.IsEmpty)
            {
                bg = hover ? Theme.Blend(Tint, Color.White, .06) : Tint;
                border = TintBorder;
                fg = TintText.IsEmpty ? fg : TintText;
            }
            if (!Enabled) { fg = Theme.TextMute; if (Kind == ButtonKind.Primary) bg = Theme.Surface3; }
            var r = new RectangleF(0, 0, Width, Height);
            if (bg.A > 0) Theme.FillRound(g, bg, r, Theme.SF(Radius));
            if (!border.IsEmpty) Theme.StrokeRound(g, border, r, Theme.SF(Radius));
            bool hasText = !string.IsNullOrEmpty(Text);
            if (!string.IsNullOrEmpty(Glyph))
            {
                if (!hasText) { Theme.DrawIcon(g, Glyph, Theme.Icon, fg, ClientRectangle); return; }
                int tw = TextRenderer.MeasureText(Text, Font).Width;
                int total = tw + Theme.S(22);
                int x = (Width - total) / 2;
                Theme.DrawIcon(g, Glyph, Theme.IconSmall, fg, new Rectangle(x, 0, Theme.S(16), Height));
                Theme.DrawText(g, Text, Font, fg, new Rectangle(x + Theme.S(22), 0, tw + 4, Height));
            }
            else if (AlignLeft)
                Theme.DrawText(g, Text, Font, fg, new Rectangle(Theme.S(14), 0, Width - Theme.S(20), Height));
            else
                Theme.DrawText(g, Text, Font, fg, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    public class ToggleSwitch : DarkControl
    {
        bool isChecked;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Size = new Size(Theme.S(42), Theme.S(24));
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get => isChecked;
            set { if (isChecked == value) return; isChecked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        public void SetSilently(bool v) { isChecked = v; Invalidate(); }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            var track = isChecked ? (hover ? Theme.AccentHover : Theme.Accent) : (hover ? Theme.BorderStrong : Theme.Surface3);
            Theme.FillRound(g, Enabled ? track : Theme.Surface2, r, r.Height / 2);
            float d = r.Height - Theme.SF(6);
            float x = isChecked ? r.Right - d - Theme.SF(3) : r.X + Theme.SF(3);
            using (var b = new SolidBrush(isChecked ? Theme.AccentText : Theme.Text))
                g.FillEllipse(b, x, r.Y + Theme.SF(3), d, d);
        }
    }

    /// <summary>Horizontal slider. ValueChanged fires while dragging; ValueCommitted on release.</summary>
    public class Slider : DarkControl
    {
        double min, max = 100, value, step = 1;
        bool dragging;
        public event EventHandler ValueChanged, ValueCommitted;

        public Slider()
        {
            Height = Theme.S(24);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public double Minimum { get => min; set { min = value; Invalidate(); } }
        public double Maximum { get => max; set { max = value; Invalidate(); } }
        public double Step { get => step; set => step = value <= 0 ? 0.01 : value; }

        public double Value
        {
            get => value;
            set { SetValue(value, true); }
        }

        public void SetSilently(double v) { value = v; Invalidate(); }

        void SetValue(double v, bool raise)
        {
            if (double.IsNaN(v)) return;
            v = Math.Round(v / step) * step;
            v = Math.Round(v, 6);
            if (Math.Abs(v - value) < 1e-9) return;
            value = v;
            Invalidate();
            if (raise) ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        RectangleF Track => new RectangleF(Theme.SF(8), Height / 2f - Theme.SF(2), Width - Theme.SF(16), Theme.SF(4));

        double FromX(int x)
        {
            var t = Track;
            double f = Math.Max(0, Math.Min(1, (x - t.X) / t.Width));
            return min + f * (max - min);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            Capture = true;
            SetValue(Clamp(FromX(e.X)), true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetValue(Clamp(FromX(e.X)), true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            Capture = false;
            ValueCommitted?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!Focused) { base.OnMouseWheel(e); return; }
            SetValue(Clamp(value + Math.Sign(e.Delta) * step), true);
            ValueCommitted?.Invoke(this, EventArgs.Empty);
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double d = e.KeyCode == Keys.Left ? -step : e.KeyCode == Keys.Right ? step : 0;
            if (d != 0) { SetValue(Clamp(value + d * (e.Shift ? 10 : 1)), true); ValueCommitted?.Invoke(this, EventArgs.Empty); }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        double Clamp(double v) => Math.Max(min, Math.Min(max, v));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            var t = Track;
            Theme.FillRound(g, Theme.Surface3, t, t.Height / 2);
            double f = max > min ? (Math.Max(min, Math.Min(max, value)) - min) / (max - min) : 0;
            var filled = new RectangleF(t.X, t.Y, (float)(t.Width * f), t.Height);
            if (filled.Width > 0.5f) Theme.FillRound(g, Enabled ? Theme.Accent : Theme.TextMute, filled, t.Height / 2);
            float d = Theme.SF(dragging || hover ? 16 : 14);
            float cx = t.X + (float)(t.Width * f);
            using (var b = new SolidBrush(Theme.Text)) g.FillEllipse(b, cx - d / 2, Height / 2f - d / 2, d, d);
            if (Focused)
                using (var p = new Pen(Theme.Blend(Theme.Accent, Color.Transparent, .5), Theme.SF(2))) g.DrawEllipse(p, cx - d / 2 - 2, Height / 2f - d / 2 - 2, d + 4, d + 4);
        }
    }

    /// <summary>Dark single-line text input with a rounded border and optional placeholder.</summary>
    public class TextField : DarkControl
    {
        public readonly TextBox Box;
        string placeholder;
        public event EventHandler Committed;

        public TextField(string text = "", string placeholder = null)
        {
            Height = Theme.S(34);
            Box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Well,
                ForeColor = Theme.Text,
                Font = Theme.Body,
                Text = text
            };
            Controls.Add(Box);
            Box.GotFocus += (s, e) => Invalidate();
            Box.LostFocus += (s, e) => { Invalidate(); Committed?.Invoke(this, EventArgs.Empty); };
            Box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Committed?.Invoke(this, EventArgs.Empty); }
            };
            Box.TextChanged += (s, e) => OnTextChanged(e);
            Placeholder = placeholder;
            Cursor = Cursors.IBeam;
        }

        public string Placeholder
        {
            get => placeholder;
            set
            {
                placeholder = value;
                if (Box.IsHandleCreated) Native.SendMessage(Box.Handle, Native.EM_SETCUEBANNER, (IntPtr)1, value ?? "");
                else Box.HandleCreated += (s, e) => Native.SendMessage(Box.Handle, Native.EM_SETCUEBANNER, (IntPtr)1, placeholder ?? "");
            }
        }

        public override string Text { get => Box?.Text ?? ""; set { if (Box != null) Box.Text = value ?? ""; } }

        protected override void OnClick(EventArgs e) { Box.Focus(); base.OnClick(e); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Box == null) return;
            int pad = Theme.S(10);
            int left = Icon != null ? Theme.S(34) : pad;
            Box.Location = new Point(left, (Height - Box.Height) / 2);
            Box.Width = Math.Max(10, Width - left - pad);
        }

        /// <summary>Optional leading glyph (e.g. a pencil on the designer name field).</summary>
        public new string Icon;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, Theme.Well, r, Theme.SF(7));
            Theme.StrokeRound(g, Box.Focused ? Theme.AccentBorder : hover ? Theme.BorderStrong : Theme.Border, r, Theme.SF(7));
            if (Icon != null) Theme.DrawIcon(g, Icon, Theme.IconSmall, Theme.TextDim, new Rectangle(Theme.S(8), 0, Theme.S(20), Height));
        }
    }

    /// <summary>Numeric input with mouse-wheel / arrow stepping and validation.</summary>
    public class NumberBox : TextField
    {
        double value;
        public double Minimum = double.MinValue, Maximum = double.MaxValue, Step = 1;
        public int Decimals = 0;
        public event EventHandler ValueChanged;

        public NumberBox()
        {
            Box.TextAlign = HorizontalAlignment.Center;
            Width = Theme.S(64);
            Committed += (s, e) => Commit();
            Box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                {
                    e.Handled = true;
                    Commit();
                    SetValue(value + (e.KeyCode == Keys.Up ? Step : -Step) * (e.Shift ? 10 : 1), true);
                }
            };
            Box.MouseWheel += (s, e) =>
            {
                if (!Box.Focused) return;
                SetValue(value + Math.Sign(e.Delta) * Step, true);
                if (e is HandledMouseEventArgs h) h.Handled = true;
            };
        }

        public double Value { get => value; set => SetValue(value, false); }

        public void SetValue(double v, bool raise)
        {
            v = Math.Max(Minimum, Math.Min(Maximum, Math.Round(v, Math.Max(0, Decimals))));
            bool changed = Math.Abs(v - value) > 1e-9;
            value = v;
            string s = v.ToString(Decimals > 0 ? "0." + new string('#', Decimals) : "0", CultureInfo.InvariantCulture);
            if (Box.Text != s) Box.Text = s;
            if (changed && raise) ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        void Commit()
        {
            var t = Box.Text.Trim().Replace(',', '.');
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) SetValue(v, true);
            else SetValue(value, false);
        }
    }

    /// <summary>Slider plus numeric box. Typing a number outside the slider range is allowed (within hard limits).</summary>
    public class SliderInput : DarkControl
    {
        public readonly Slider Slider = new Slider();
        public readonly NumberBox Number = new NumberBox();
        bool syncing;
        public event EventHandler ValueChanged, ValueCommitted;

        public SliderInput(double min, double max, double step, int decimals, double hardMin = double.NaN, double hardMax = double.NaN)
        {
            Height = Theme.S(34);
            Slider.Minimum = min; Slider.Maximum = max; Slider.Step = step;
            Number.Step = step; Number.Decimals = decimals;
            Number.Minimum = double.IsNaN(hardMin) ? min : hardMin;
            Number.Maximum = double.IsNaN(hardMax) ? max : hardMax;
            Controls.Add(Slider);
            Controls.Add(Number);
            Slider.ValueChanged += (s, e) =>
            {
                if (syncing) return;
                syncing = true; Number.Value = Slider.Value; syncing = false;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            Slider.ValueCommitted += (s, e) => ValueCommitted?.Invoke(this, EventArgs.Empty);
            Number.ValueChanged += (s, e) =>
            {
                if (syncing) return;
                syncing = true; Slider.SetSilently(Number.Value); syncing = false;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
            };
        }

        public double Value
        {
            get => Number.Value;
            set { syncing = true; Number.Value = value; Slider.SetSilently(value); syncing = false; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int nw = Theme.S(64);
            Number.SetBounds(Width - nw, 0, nw, Height);
            Slider.SetBounds(0, (Height - Slider.Height) / 2, Width - nw - Theme.S(8), Slider.Height);
        }

        protected override void OnPaint(PaintEventArgs e) => e.Graphics.Clear(ParentBack);
    }

    /// <summary>Pill-style segmented selector.</summary>
    public class Segmented : DarkControl
    {
        string[] items;
        string[] glyphs;
        int selected;
        int hoverIndex = -1;
        public event EventHandler SelectedChanged;

        public Segmented(string[] items, string[] glyphs = null)
        {
            this.items = items;
            this.glyphs = glyphs;
            Height = Theme.S(34);
            Cursor = Cursors.Hand;
            Font = Theme.SmallBold;
        }

        public string[] Items { get => items; set { items = value; Invalidate(); } }

        public int SelectedIndex
        {
            get => selected;
            set { if (selected == value) return; selected = value; Invalidate(); SelectedChanged?.Invoke(this, EventArgs.Empty); }
        }

        public void SetSilently(int i) { selected = i; Invalidate(); }

        /// <summary>Cells sized to their labels (so long labels don't truncate), stretched to fill the control.</summary>
        RectangleF[] Cells()
        {
            var cells = new RectangleF[items.Length];
            if (items.Length == 0) return cells;
            var widths = items.Select((t, i) => (float)(string.IsNullOrEmpty(t) ? Theme.S(28) : TextRenderer.MeasureText(t, Font, Size.Empty, TextFormatFlags.NoPadding).Width) + Theme.SF(18)).ToArray();
            float total = widths.Sum();
            float extra = Math.Max(0, Width - total);
            float scale = total > Width ? Width / total : 1;
            float x = 0;
            for (int i = 0; i < items.Length; i++)
            {
                float w = widths[i] * scale + extra / items.Length;
                cells[i] = new RectangleF(x, 0, w, Height);
                x += w;
            }
            return cells;
        }

        public int PreferredWidth => items == null ? 0 : (int)items.Sum(t => TextRenderer.MeasureText(t ?? "", Font, Size.Empty, TextFormatFlags.NoPadding).Width + Theme.SF(18)) + Theme.S(6);

        int IndexAt(int x)
        {
            var c = Cells();
            for (int i = 0; i < c.Length; i++) if (x < c[i].Right) return i;
            return c.Length - 1;
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = IndexAt(e.X); if (i != hoverIndex) { hoverIndex = i; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); int i = IndexAt(e.X); if (i >= 0) SelectedIndex = i; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, Theme.Surface2, r, Theme.SF(8));
            Theme.StrokeRound(g, Theme.Border, r, Theme.SF(8));
            if (items == null || items.Length == 0) return;
            var cells = Cells();
            for (int i = 0; i < items.Length; i++)
            {
                var cell = new RectangleF(cells[i].X + Theme.SF(3), Theme.SF(3), cells[i].Width - Theme.SF(6), Height - Theme.SF(6));
                if (i == selected) Theme.FillRound(g, Theme.AccentDim, cell, Theme.SF(6));
                else if (i == hoverIndex) Theme.FillRound(g, Theme.Surface3, cell, Theme.SF(6));
                var color = i == selected ? Theme.Accent : Theme.TextDim;
                var rc = Rectangle.Round(cell);
                if (glyphs != null && i < glyphs.Length && !string.IsNullOrEmpty(glyphs[i]) && string.IsNullOrEmpty(items[i]))
                    Theme.DrawIcon(g, glyphs[i], Theme.Icon, color, rc);
                else
                    Theme.DrawText(g, items[i], Font, color, rc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Multi-select pill group (e.g. which crosshair arms are visible). Cells share the width evenly.</summary>
    public class MultiToggle : DarkControl
    {
        readonly string[] items;
        public readonly bool[] Checked;
        int hoverIndex = -1;
        public event Action<int> Toggled;

        public MultiToggle(string[] items, bool[] state)
        {
            this.items = items;
            Checked = state;
            Height = Theme.S(34);
            Cursor = Cursors.Hand;
            Font = Theme.SmallBold;
        }

        int IndexAt(int x) => Math.Max(0, Math.Min(items.Length - 1, x * items.Length / Math.Max(1, Width)));

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = IndexAt(e.X); if (i != hoverIndex) { hoverIndex = i; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled) return;
            int i = IndexAt(e.X);
            Checked[i] = !Checked[i];
            Invalidate();
            Toggled?.Invoke(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, Theme.Surface2, r, Theme.SF(8));
            Theme.StrokeRound(g, Theme.Border, r, Theme.SF(8));
            float w = (float)Width / items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var cell = new RectangleF(i * w + Theme.SF(3), Theme.SF(3), w - Theme.SF(6), Height - Theme.SF(6));
                if (Checked[i]) Theme.FillRound(g, Enabled ? Theme.AccentDim : Theme.Surface3, cell, Theme.SF(6));
                else if (i == hoverIndex && Enabled) Theme.FillRound(g, Theme.Surface3, cell, Theme.SF(6));
                var color = !Enabled ? Theme.TextMute : Checked[i] ? Theme.Accent : Theme.TextDim;
                Theme.DrawText(g, items[i], Font, color, Rectangle.Round(cell), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Owner-drawn dark dropdown.</summary>
    public class Dropdown : ComboBox
    {
        public Dropdown()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            BackColor = Theme.Surface2;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            ItemHeight = Theme.S(26);
            MaxDropDownItems = 14;
            IntegralHeight = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkScrollbars(this);
        }

        bool hovering;
        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        /// <summary>Paints over the system's light flat border and arrow so the closed box matches the theme.</summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F /* WM_PAINT */ || Width <= 0) return;
            using (var g = Graphics.FromHwnd(Handle))
            {
                var border = Focused || DroppedDown ? Theme.Accent : hovering ? Theme.BorderStrong : Theme.Border;
                int bw = SystemInformation.VerticalScrollBarWidth;
                var arrow = new Rectangle(Width - bw - 2, 1, bw + 1, Height - 2);
                using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, arrow);
                using (var p = new Pen(BackColor, 2)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
                using (var p = new Pen(border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                Theme.DrawIcon(g, Glyph.Down, Theme.IconSmall, Enabled ? Theme.TextDim : Theme.TextMute, arrow);
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var b = new SolidBrush(sel ? Theme.Surface3 : Theme.Surface2)) g.FillRectangle(b, e.Bounds);
            if (e.Index < 0) return;
            string text = GetItemText(Items[e.Index]);
            var r = new Rectangle(e.Bounds.X + Theme.S(6), e.Bounds.Y, e.Bounds.Width - Theme.S(8), e.Bounds.Height);
            TextRenderer.DrawText(g, text, Font, r, sel ? Theme.Accent : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>A plain label that paints on its parent's background.</summary>
    public class DarkLabel : Label
    {
        public DarkLabel(string text, Font font = null, Color? color = null)
        {
            Text = Core.L.T(text);
            Font = font ?? Theme.Body;
            ForeColor = color ?? Theme.Text;
            BackColor = Color.Transparent;
            AutoSize = false;
            UseMnemonic = false;
            TextAlign = ContentAlignment.MiddleLeft;
        }
    }

    /// <summary>Wrapping description text that auto-sizes its height to its width.</summary>
    public class WrapLabel : Label
    {
        public WrapLabel(string text, Font font = null, Color? color = null)
        {
            Text = Core.L.T(text);
            Font = font ?? Theme.Small;
            ForeColor = color ?? Theme.TextDim;
            BackColor = Color.Transparent;
            AutoSize = false;
            UseMnemonic = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Fit();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Fit();
        }

        void Fit()
        {
            if (Width <= 0) return;
            var sz = TextRenderer.MeasureText(Text, Font, new Size(Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int h = Math.Max(Theme.S(16), sz.Height + 2);
            if (Height != h) Height = h;
        }
    }

    /// <summary>Lays out children top-to-bottom at full width; its own height follows the content.</summary>
    public class StackPanel : Panel
    {
        public int Spacing = Theme.S(10);
        public Padding Inner = new Padding(0);
        bool inLayout;

        public StackPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (inLayout) return;
            inLayout = true;
            try
            {
                int y = Inner.Top;
                int w = Width - Inner.Horizontal;
                foreach (Control c in Controls)
                {
                    if (!c.Visible) continue;
                    var m = c.Margin;
                    c.SetBounds(Inner.Left + m.Left, y + m.Top, Math.Max(10, w - m.Horizontal), c.Height);
                    y += c.Height + m.Vertical + Spacing;
                }
                int h = Math.Max(0, y - Spacing + Inner.Bottom);
                if (Height != h) Height = h;
            }
            finally { inLayout = false; }
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            e.Control.SizeChanged += (s, a) => PerformLayout();
            e.Control.VisibleChanged += (s, a) => PerformLayout();
        }
    }

    /// <summary>Scrollable host with dark scrollbars that keeps a StackPanel at full width.</summary>
    public class ScrollHost : Panel
    {
        public readonly StackPanel Stack = new StackPanel();

        public ScrollHost()
        {
            // vertical-only scrolling: the stack always matches the client width
            HorizontalScroll.Maximum = 0;
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;
            AutoScroll = true;
            BackColor = Theme.Bg;
            Controls.Add(Stack);
            Stack.Location = new Point(0, 0);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkScrollbars(this);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int w = ClientSize.Width;
            if (Stack.Width != w) Stack.Width = w;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
        }

        protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location; // don't jump on focus
    }

    /// <summary>A rounded "card" container with an optional title.</summary>
    public class Card : Panel
    {
        public string Title;
        public string Subtitle;
        public readonly StackPanel Body = new StackPanel();
        public Color Fill = Theme.Surface;

        public Card(string title = null, string subtitle = null)
        {
            Title = Core.L.T(title);
            Subtitle = Core.L.T(subtitle);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Surface;
            Padding = new Padding(Theme.S(18), Theme.S(title == null ? 16 : subtitle == null ? 50 : 66), Theme.S(18), Theme.S(18));
            Body.BackColor = Theme.Surface;
            Controls.Add(Body);
            Body.SizeChanged += (s, e) => FitHeight();
        }

        void FitHeight()
        {
            int h = Padding.Top + Body.Height + Padding.Bottom;
            if (Height != h) Height = h;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Body.SetBounds(Padding.Left, Padding.Top, Width - Padding.Horizontal, Body.Height);
            FitHeight();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            Color parent = Parent?.BackColor ?? Theme.Bg;
            if (parent.A < 255) parent = Theme.Bg;
            g.Clear(parent);
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, Fill, r, Theme.SF(12));
            Theme.StrokeRound(g, Theme.Border, r, Theme.SF(12));
            if (Title != null)
            {
                Theme.DrawText(g, Title, Theme.H3, Theme.Text, new Rectangle(Theme.S(18), Theme.S(14), Width - Theme.S(36), Theme.S(24)));
                if (Subtitle != null)
                    Theme.DrawText(g, Subtitle, Theme.Small, Theme.TextDim, new Rectangle(Theme.S(18), Theme.S(38), Width - Theme.S(36), Theme.S(18)));
            }
        }
    }

    /// <summary>A labeled row: title (+ optional description) on the left, editor on the right.</summary>
    public class Row : Panel
    {
        public readonly Control Editor;
        readonly string title, desc;
        public int EditorWidth;
        public bool EditorFill;

        public Row(string title, Control editor, string description = null, int editorWidth = 0, bool fill = false)
        {
            this.title = Core.L.T(title);
            desc = Core.L.T(description);
            Editor = editor;
            EditorFill = fill;
            EditorWidth = editorWidth;
            BackColor = Color.Transparent;
            Height = Math.Max(editor?.Height ?? 0, Theme.S(description == null ? 34 : 44));
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            if (editor != null)
            {
                Controls.Add(editor);
                editor.SizeChanged += (s, e) => { int h = Math.Max(editor.Height, Theme.S(desc == null ? 34 : 44)); if (Height != h) Height = h; else PerformLayout(); };
            }
        }

        public int FillLabel = 150;

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Editor == null) return;
            int w;
            if (EditorFill) w = Width - Theme.S(FillLabel);
            else w = EditorWidth > 0 ? EditorWidth : Editor.Width;
            w = Math.Max(10, Math.Min(w, Width));
            Editor.SetBounds(Width - w, (Height - Editor.Height) / 2, w, Editor.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color parent = Parent?.BackColor ?? Theme.Surface;
            Control p = Parent;
            while (p != null && p.BackColor.A < 255) p = p.Parent;
            g.Clear(p?.BackColor ?? Theme.Surface);
            int labelW = Width - (Editor == null ? 0 : (Editor.Width + Theme.S(12)));
            if (desc == null)
                Theme.DrawText(g, title, Theme.Body, Theme.Text, new Rectangle(0, 0, labelW, Height));
            else
            {
                Theme.DrawText(g, title, Theme.Body, Theme.Text, new Rectangle(0, Height / 2 - Theme.S(19), labelW, Theme.S(20)));
                Theme.DrawText(g, desc, Theme.Small, Theme.TextDim, new Rectangle(0, Height / 2 + Theme.S(1), labelW, Theme.S(18)));
            }
        }
    }

    /// <summary>A horizontal row of controls laid out left-to-right (or right-aligned).</summary>
    public class HStack : Panel
    {
        public int Spacing = Theme.S(8);
        public bool RightAlign;

        public HStack(params Control[] items)
        {
            BackColor = Color.Transparent;
            Height = Theme.S(34);
            foreach (var c in items) Controls.Add(c);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var list = Controls.Cast<Control>().Where(c => c.Visible).ToList();
            if (RightAlign)
            {
                int x = Width;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var c = list[i];
                    x -= c.Width;
                    c.Location = new Point(x, (Height - c.Height) / 2);
                    x -= Spacing;
                }
            }
            else
            {
                int x = 0;
                foreach (var c in list)
                {
                    c.Location = new Point(x, (Height - c.Height) / 2);
                    x += c.Width + Spacing;
                }
            }
        }
    }

    public class Divider : Control
    {
        public Divider() { Height = Theme.S(1); BackColor = Theme.Border; }
    }

    public class SectionTitle : Control
    {
        public SectionTitle(string text)
        {
            Text = text;
            Height = Theme.S(26);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Control p = Parent;
            while (p != null && p.BackColor.A < 255) p = p.Parent;
            e.Graphics.Clear(p?.BackColor ?? Theme.Surface);
            Theme.DrawText(e.Graphics, Text.ToUpperInvariant(), Theme.SmallBold, Theme.TextMute, ClientRectangle, TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }

    public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? (e.Item.Selected ? Theme.Accent : Theme.Text) : Theme.TextMute;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextDim;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(e.ImageRectangle.X - 2, e.ImageRectangle.Y - 2, e.ImageRectangle.Width + 4, e.ImageRectangle.Height + 4);
            Theme.DrawIcon(g, Glyph.Check, Theme.IconSmall, Theme.Accent, r);
        }

        sealed class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Theme.Surface2;
            public override Color ImageMarginGradientBegin => Theme.Surface2;
            public override Color ImageMarginGradientMiddle => Theme.Surface2;
            public override Color ImageMarginGradientEnd => Theme.Surface2;
            public override Color MenuBorder => Theme.BorderStrong;
            public override Color MenuItemBorder => Theme.Surface3;
            public override Color MenuItemSelected => Theme.Surface3;
            public override Color MenuItemSelectedGradientBegin => Theme.Surface3;
            public override Color MenuItemSelectedGradientEnd => Theme.Surface3;
            public override Color MenuItemPressedGradientBegin => Theme.Surface3;
            public override Color MenuItemPressedGradientEnd => Theme.Surface3;
            public override Color SeparatorDark => Theme.Border;
            public override Color SeparatorLight => Theme.Border;
            public override Color CheckBackground => Theme.AccentDim;
            public override Color CheckSelectedBackground => Theme.AccentDim;
            public override Color CheckPressedBackground => Theme.AccentDim;
        }
    }

    public static class Menus
    {
        public static ContextMenuStrip Create()
        {
            var m = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), Font = Theme.Body, ShowImageMargin = true, BackColor = Theme.Surface2, ForeColor = Theme.Text };
            return m;
        }

        public static ToolStripMenuItem Item(this ToolStripItemCollection items, string text, Action onClick, bool enabled = true, bool isChecked = false)
        {
            var it = new ToolStripMenuItem(Core.L.T(text)) { Enabled = enabled, Checked = isChecked, ForeColor = Theme.Text };
            if (onClick != null) it.Click += (s, e) => onClick();
            items.Add(it);
            return it;
        }

        public static void Sep(this ToolStripItemCollection items) => items.Add(new ToolStripSeparator());
    }
}
