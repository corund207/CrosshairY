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
    /// <summary>Top tab strip: icon + label tabs with an orange underline on the active tab, plus right-aligned actions.</summary>
    public class TabBar : DarkControl
    {
        public sealed class Tab { public string Text, Glyph; }
        public readonly List<Tab> Tabs = new List<Tab>();
        public readonly List<Tab> Actions = new List<Tab>();
        int selected, hoverTab = -1, hoverAction = -1;
        public event Action<int> SelectedChanged;
        public event Action<int> ActionClicked;

        public TabBar()
        {
            Height = Theme.S(42);
            BackColor = Theme.Chrome;
            Font = Theme.Nav;
        }

        public int SelectedIndex
        {
            get => selected;
            set { if (selected == value) return; selected = value; Invalidate(); SelectedChanged?.Invoke(value); }
        }

        public void SetSilently(int i) { selected = i; Invalidate(); }

        Rectangle[] TabRects()
        {
            var r = new Rectangle[Tabs.Count];
            int x = 0;
            for (int i = 0; i < Tabs.Count; i++)
            {
                int w = Theme.TextWidth(Core.L.T(Tabs[i].Text), Font) + Theme.S(Tabs[i].Glyph != null ? 62 : 40);
                r[i] = new Rectangle(x, 0, w, Height);
                x += w;
            }
            return r;
        }

        Rectangle[] ActionRects()
        {
            var r = new Rectangle[Actions.Count];
            int x = Width - Theme.S(16);
            for (int i = Actions.Count - 1; i >= 0; i--)
            {
                int w = Theme.TextWidth(Actions[i].Text, Font) + Theme.S(Actions[i].Glyph != null ? 50 : 28);
                x -= w;
                r[i] = new Rectangle(x, 0, w, Height);
            }
            return r;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int t = Array.FindIndex(TabRects(), r => r.Contains(e.Location)), a = Array.FindIndex(ActionRects(), r => r.Contains(e.Location));
            if (t != hoverTab || a != hoverAction) { hoverTab = t; hoverAction = a; Invalidate(); }
            Cursor = t >= 0 || a >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverTab = hoverAction = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int t = Array.FindIndex(TabRects(), r => r.Contains(e.Location));
            if (t >= 0) { SelectedIndex = t; return; }
            int a = Array.FindIndex(ActionRects(), r => r.Contains(e.Location));
            if (a >= 0) ActionClicked?.Invoke(a);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            var tr = TabRects();
            for (int i = 0; i < Tabs.Count; i++)
            {
                var r = tr[i];
                bool on = i == selected;
                if (on) using (var b = new SolidBrush(Theme.Blend(Theme.Chrome, Theme.Accent, .05))) g.FillRectangle(b, r);
                var c = on ? Theme.Accent : i == hoverTab ? Theme.Text : Theme.Blend(Theme.Text, Theme.Chrome, .12);
                int x = r.X + Theme.S(20);
                if (Tabs[i].Glyph != null)
                {
                    Theme.DrawIcon(g, Tabs[i].Glyph, Theme.Icon, c, new Rectangle(x, 0, Theme.S(18), Height));
                    x += Theme.S(26);
                }
                Theme.DrawText(g, Core.L.T(Tabs[i].Text), Font, c, new Rectangle(x, 0, r.Right - x, Height));
                if (on) using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, r.X, Height - Theme.S(2), r.Width, Theme.S(2));
            }
            var ar = ActionRects();
            for (int i = 0; i < Actions.Count; i++)
            {
                var r = ar[i];
                var c = i == hoverAction ? Theme.Accent : Theme.Text;
                int x = r.X + Theme.S(12);
                if (Actions[i].Glyph != null)
                {
                    Theme.DrawIcon(g, Actions[i].Glyph, Theme.Icon, c, new Rectangle(x, 0, Theme.S(18), Height));
                    x += Theme.S(24);
                }
                Theme.DrawText(g, Actions[i].Text, Font, c, new Rectangle(x, 0, r.Right - x, Height));
            }
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>Left "CATEGORIES"/"COLLECTIONS" column: selectable rows with icon, label and count; optional dashed add row.</summary>
    public class CategoryList : DarkControl
    {
        public sealed class Item { public string Text, Glyph, Count, Key; public bool Header; }
        public readonly List<Item> Items = new List<Item>();
        public string AddText;
        int selected, hover = -1;
        public event Action<Item> Selected;
        public event Action AddClicked;
        public event Action<Item, Point> ItemRightClicked;

        public CategoryList()
        {
            BackColor = Theme.Well;
            Font = Theme.Nav;
        }

        public string SelectedKey
        {
            get => selected >= 0 && selected < Items.Count ? Items[selected].Key : null;
            set { int i = Items.FindIndex(x => x.Key == value && !x.Header); if (i >= 0) { selected = i; Invalidate(); } }
        }

        int RowH => Theme.S(38);

        public void Refresh2() { int h = Layout2().Last().Bottom + Theme.S(12); if (Height != h) Height = h; Invalidate(); }

        List<Rectangle> Layout2()
        {
            var list = new List<Rectangle>();
            int y = Theme.S(4);
            foreach (var it in Items)
            {
                int h = it.Header ? Theme.S(42) : RowH;
                list.Add(new Rectangle(0, y, Width, h));
                y += h + (it.Header ? 0 : Theme.S(4));
            }
            list.Add(AddText == null ? new Rectangle(0, y, Width, 0) : new Rectangle(0, y + Theme.S(2), Width, RowH));
            return list;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

        int HitTest(Point p)
        {
            var l = Layout2();
            for (int i = 0; i < l.Count; i++) if (l[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h >= 0 && h < Items.Count && Items[h].Header) h = -1;
            if (h != hover) { hover = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int h = HitTest(e.Location);
            if (h < 0) return;
            if (h == Items.Count) { if (AddText != null && e.Button == MouseButtons.Left) AddClicked?.Invoke(); return; }
            if (Items[h].Header) return;
            if (e.Button == MouseButtons.Right) { ItemRightClicked?.Invoke(Items[h], e.Location); return; }
            selected = h;
            Invalidate();
            Selected?.Invoke(Items[h]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            var l = Layout2();
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                var r = l[i];
                if (it.Header)
                {
                    Theme.DrawText(g, Core.L.Upper(it.Text), Theme.Caption, Theme.TextMute, new Rectangle(r.X + Theme.S(6), r.Y + Theme.S(14), r.Width, Theme.S(22)));
                    continue;
                }
                bool on = i == selected;
                var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
                if (on)
                {
                    Theme.FillRound(g, Theme.AccentDim, rf, Theme.SF(7));
                    Theme.StrokeRound(g, Theme.AccentBorder, rf, Theme.SF(7));
                }
                else if (i == hover) Theme.FillRound(g, Theme.Surface, rf, Theme.SF(7));
                int x = r.X + Theme.S(14);
                if (it.Glyph != null)
                {
                    Theme.DrawIcon(g, it.Glyph, Theme.Icon, on ? Theme.Accent : Theme.TextDim, new Rectangle(x, r.Y, Theme.S(20), r.Height));
                    x += Theme.S(30);
                }
                int countW = string.IsNullOrEmpty(it.Count) ? 0 : Theme.S(36);
                Theme.DrawText(g, Core.L.T(it.Text), on ? Theme.BodyBold : Font, on ? Theme.Text : Theme.Blend(Theme.Text, Theme.Well, .1), new Rectangle(x, r.Y, r.Right - x - countW - Theme.S(8), r.Height));
                if (countW > 0) Theme.DrawText(g, it.Count, Theme.Small, Theme.TextDim, new Rectangle(r.Right - countW - Theme.S(10), r.Y, countW, r.Height), Theme.Right);
            }
            if (AddText != null)
            {
                var r = l[Items.Count];
                var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
                if (hover == Items.Count) Theme.FillRound(g, Theme.Surface, rf, Theme.SF(7));
                Theme.DashedRound(g, Theme.BorderStrong, rf, Theme.SF(7));
                Theme.DrawIcon(g, Glyph.Add, Theme.IconSmall, Theme.TextDim, new Rectangle(r.X + Theme.S(14), r.Y, Theme.S(20), r.Height));
                Theme.DrawText(g, AddText, Font, Theme.TextDim, new Rectangle(r.X + Theme.S(44), r.Y, r.Width - Theme.S(50), r.Height));
            }
        }
    }

    /// <summary>Settings row: icon tile, title, description and a right-aligned control. Sub-rows hang off a guide line.</summary>
    public class SettingRow : Panel
    {
        readonly string glyph, title, desc;
        public readonly Control Editor;
        public bool Sub;
        public int EditorWidth;

        public SettingRow(string glyph, string title, string desc, Control editor, int editorWidth = 0, bool sub = false)
        {
            this.glyph = glyph; this.title = Core.L.T(title); this.desc = Core.L.T(desc);
            Editor = editor; EditorWidth = editorWidth; Sub = sub;
            BackColor = Theme.Surface;
            Height = Theme.S(sub ? 58 : 72);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            if (editor != null)
            {
                Controls.Add(editor);
                editor.SizeChanged += (s, e) => { int h = Math.Max(Theme.S(sub ? 58 : 72), editor.Height + Theme.S(24)); if (Height != h) Height = h; else PerformLayout(); };
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (Editor == null) return;
            int w = EditorWidth > 0 ? EditorWidth : Editor.Width;
            Editor.SetBounds(Width - w - Theme.S(16), (Height - Editor.Height) / 2, w, Editor.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            int x = Theme.S(16);
            if (Sub)
            {
                using (var p = new Pen(Theme.BorderStrong, Theme.SF(2))) g.DrawLine(p, Theme.S(34), 0, Theme.S(34), Height);
                x = Theme.S(58);
            }
            else if (glyph != null)
            {
                var tile = new RectangleF(x, (Height - Theme.S(36)) / 2f, Theme.S(36), Theme.S(36));
                Theme.FillRound(g, Theme.Surface3, tile, Theme.SF(7));
                Theme.DrawIcon(g, glyph, Theme.Icon, Theme.TextDim, Rectangle.Round(tile));
                x += Theme.S(52);
            }
            int right = Editor != null ? Editor.Left - Theme.S(16) : Width - Theme.S(16);
            var tf = Sub ? Theme.Body : Theme.H3;
            if (desc == null)
                Theme.DrawText(g, title, tf, Theme.Text, new Rectangle(x, 0, right - x, Height));
            else
            {
                Theme.DrawText(g, title, tf, Theme.Text, new Rectangle(x, Height / 2 - Theme.S(21), right - x, Theme.S(22)));
                Theme.DrawText(g, desc, Theme.Small, Theme.TextDim, new Rectangle(x, Height / 2 + Theme.S(2), right - x, Theme.S(18)));
            }
        }
    }

    /// <summary>Rounded card that stacks SettingRows with hairline dividers.</summary>
    public class SettingsCard : Panel
    {
        public readonly StackPanel Rows = new StackPanel { Spacing = 0 };

        public SettingsCard()
        {
            BackColor = Theme.Bg;
            Rows.BackColor = Theme.Surface;
            Controls.Add(Rows);
            Padding = new Padding(1);
            Rows.SizeChanged += (s, e) => { int h = Rows.Height + 2; if (Height != h) Height = h; };
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public SettingsCard Add(Control row)
        {
            if (Rows.Controls.Count > 0 && !(row is SettingRow sr && sr.Sub)) Rows.Controls.Add(new Divider { BackColor = Theme.Border, Margin = new Padding(Theme.S(16), 0, Theme.S(16), 0) });
            Rows.Controls.Add(row);
            return this;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Rows.SetBounds(1, 1, Width - 2, Rows.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Control p = Parent;
            while (p != null && p.BackColor.A < 255) p = p.Parent;
            g.Clear(p?.BackColor ?? Theme.Bg);
            Theme.Smooth(g);
            Theme.FillRound(g, Theme.Surface, new RectangleF(0, 0, Width, Height), Theme.SF(10));
            Theme.StrokeRound(g, Theme.Border, new RectangleF(0, 0, Width, Height), Theme.SF(10));
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }

    /// <summary>Section caption with icon, e.g. "▣ DISPLAY".</summary>
    public class CaptionLabel : Control
    {
        readonly string glyph;
        public CaptionLabel(string glyph, string text)
        {
            this.glyph = glyph;
            Text = Core.L.T(text);
            Height = Theme.S(34);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Control p = Parent;
            while (p != null && p.BackColor.A < 255) p = p.Parent;
            e.Graphics.Clear(p?.BackColor ?? Theme.Bg);
            int x = 0;
            if (glyph != null) { Theme.DrawIcon(e.Graphics, glyph, Theme.IconSmall, Theme.TextDim, new Rectangle(0, 0, Theme.S(18), Height)); x = Theme.S(26); }
            Theme.DrawText(e.Graphics, Text.ToUpperInvariant(), Theme.Caption, Theme.Text, new Rectangle(x, 0, Width - x, Height));
        }
    }

    /// <summary>[−] value [+] [reset] stepper, like Crosshair X position &amp; size controls.</summary>
    public class Stepper : DarkControl
    {
        readonly FlatButton minus, plus, reset;
        public readonly NumberBox Box = new NumberBox();
        public double DefaultValue;
        public event EventHandler ValueChanged;

        public Stepper(string minusGlyph, string plusGlyph, double step, int decimals, double min, double max, double def)
        {
            Height = Theme.S(38);
            DefaultValue = def;
            minus = new FlatButton("", minusGlyph) { Width = Theme.S(38), Height = Theme.S(38) };
            plus = new FlatButton("", plusGlyph) { Width = Theme.S(38), Height = Theme.S(38) };
            reset = new FlatButton("", Glyph.Reset, ButtonKind.Ghost) { Width = Theme.S(38), Height = Theme.S(38) };
            Box.Step = step; Box.Decimals = decimals; Box.Minimum = min; Box.Maximum = max;
            Box.Height = Theme.S(38);
            Controls.AddRange(new Control[] { minus, Box, plus, reset });
            minus.Click += (s, e) => { Box.SetValue(Box.Value - step, true); };
            plus.Click += (s, e) => { Box.SetValue(Box.Value + step, true); };
            reset.Click += (s, e) => { Box.SetValue(DefaultValue, true); };
            Box.ValueChanged += (s, e) => { UpdateReset(); ValueChanged?.Invoke(this, EventArgs.Empty); };
            Width = Theme.S(38 * 3 + 70 + 24);
        }

        public double Value { get => Box.Value; set { Box.Value = value; UpdateReset(); } }

        void UpdateReset()
        {
            bool changed = Math.Abs(Box.Value - DefaultValue) > 1e-9;
            reset.Kind = changed ? ButtonKind.Subtle : ButtonKind.Ghost;
            reset.Toggled = changed;
            reset.Enabled = changed;
            reset.Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (minus == null) return;
            int g = Theme.S(8), x = 0;
            minus.Location = new Point(x, 0); x += minus.Width + g;
            Box.SetBounds(x, 0, Theme.S(70), Height); x += Box.Width + g;
            plus.Location = new Point(x, 0); x += plus.Width + g;
            reset.Location = new Point(x, 0);
        }

        protected override void OnPaint(PaintEventArgs e) => e.Graphics.Clear(ParentBack);
    }

    /// <summary>Collapsible inspector section: chevron + uppercase title; body stacks fields.</summary>
    public class InspectorSection : Panel
    {
        public readonly StackPanel Body = new StackPanel { Spacing = Theme.S(10) };
        readonly string title;
        bool collapsed;
        static readonly HashSet<string> collapsedTitles = new HashSet<string>();

        public InspectorSection(string title)
        {
            this.title = Core.L.T(title);
            collapsed = collapsedTitles.Contains(title);
            BackColor = Theme.Chrome;
            Body.BackColor = Theme.Chrome;
            Body.Visible = !collapsed;
            Controls.Add(Body);
            Body.SizeChanged += (s, e) => Fit();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Default;
            Fit();
        }

        int HeaderH => Theme.S(46);

        void Fit()
        {
            int h = HeaderH + (collapsed ? 0 : Body.Height + Theme.S(16));
            if (Height != h) Height = h;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Body.SetBounds(Theme.S(18), HeaderH, Width - Theme.S(36), Body.Height);
            Fit();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Y > HeaderH) return;
            collapsed = !collapsed;
            if (collapsed) collapsedTitles.Add(title); else collapsedTitles.Remove(title);
            Body.Visible = !collapsed;
            Fit();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Cursor = e.Y <= HeaderH ? Cursors.Hand : Cursors.Default; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.DrawIcon(g, collapsed ? Glyph.Right : Glyph.Down, Theme.IconTiny, Theme.TextDim, new Rectangle(Theme.S(18), 0, Theme.S(14), HeaderH));
            Theme.DrawText(g, title.ToUpperInvariant(), Theme.Caption, Theme.Text, new Rectangle(Theme.S(42), 0, Width - Theme.S(50), HeaderH));
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>Label-above-control field used in the designer inspector.</summary>
    public class FieldGroup : Panel
    {
        readonly string label;
        public readonly Control Editor;

        public FieldGroup(string label, Control editor)
        {
            this.label = Core.L.T(label);
            Editor = editor;
            BackColor = Theme.Chrome;
            Controls.Add(editor);
            Height = Theme.S(22) + editor.Height;
            editor.SizeChanged += (s, e) => { Height = Theme.S(22) + editor.Height; };
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Editor.SetBounds(0, Theme.S(22), Width, Editor.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Theme.DrawText(e.Graphics, label, Theme.Small, Theme.TextDim, new Rectangle(0, 0, Width, Theme.S(18)));
        }
    }

    /// <summary>Lays two (or more) controls side by side with equal widths.</summary>
    public class Columns : Panel
    {
        public Columns(params Control[] cols)
        {
            BackColor = Color.Transparent;
            foreach (var c in cols) Controls.Add(c);
            Height = cols.Length == 0 ? 0 : cols.Max(c => c.Height);
            foreach (var c in cols) c.SizeChanged += (s, e) => { int h = Controls.Cast<Control>().Max(x => x.Height); if (Height != h) Height = h; };
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var list = Controls.Cast<Control>().ToList();
            if (list.Count == 0) return;
            int gap = Theme.S(10);
            int w = (Width - gap * (list.Count - 1)) / list.Count;
            for (int i = 0; i < list.Count; i++) list[i].SetBounds(i * (w + gap), 0, w, list[i].Height);
        }
    }

    /// <summary>Numeric input box with an icon prefix (drag the icon to scrub), right-aligned value.</summary>
    public class FieldBox : DarkControl
    {
        readonly TextBox box = new TextBox();
        readonly string glyph;
        double value;
        public double Minimum = -100000, Maximum = 100000, Step = 1;
        public int Decimals;
        bool scrubbing;
        int scrubX;
        double scrubStart;
        public event EventHandler ValueChanged, ValueCommitted;

        public FieldBox(string glyph)
        {
            this.glyph = glyph;
            Height = Theme.S(38);
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.Well;
            box.ForeColor = Theme.Text;
            box.Font = Theme.Body;
            box.TextAlign = HorizontalAlignment.Right;
            Controls.Add(box);
            box.LostFocus += (s, e) => Commit();
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); }
                else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                {
                    e.Handled = true;
                    SetValue(value + (e.KeyCode == Keys.Up ? Step : -Step) * (e.Shift ? 10 : 1), true);
                    ValueCommitted?.Invoke(this, EventArgs.Empty);
                }
            };
            box.MouseWheel += (s, e) =>
            {
                if (!box.Focused) return;
                SetValue(value + Math.Sign(e.Delta) * Step, true);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
                if (e is HandledMouseEventArgs h) h.Handled = true;
            };
            box.GotFocus += (s, e) => Invalidate();
            Cursor = Cursors.SizeWE;
        }

        public double Value { get => value; set => SetValue(value, false); }

        public void SetValue(double v, bool raise)
        {
            v = Math.Max(Minimum, Math.Min(Maximum, Math.Round(v, Math.Max(0, Decimals))));
            bool changed = Math.Abs(v - value) > 1e-9;
            value = v;
            string s = v.ToString(Decimals > 0 ? "0." + new string('0', Math.Min(Decimals, 2)) + new string('#', Math.Max(0, Decimals - 2)) : "0", CultureInfo.InvariantCulture);
            if (box.Text != s) box.Text = s;
            if (changed && raise) ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        void Commit()
        {
            if (double.TryParse(box.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) SetValue(v, true);
            else SetValue(value, false);
            ValueCommitted?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (box == null) return;
            int left = glyph != null ? Theme.S(40) : Theme.S(12);
            box.SetBounds(left, (Height - box.Height) / 2, Width - left - Theme.S(12), box.Height);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            scrubbing = true; scrubX = e.X; scrubStart = value; Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!scrubbing) return;
            int dx = e.X - scrubX;
            SetValue(scrubStart + Math.Round(dx / (double)Theme.S(4)) * Step, true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!scrubbing) return;
            scrubbing = false; Capture = false;
            ValueCommitted?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(ParentBack);
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, Theme.Well, r, Theme.SF(7));
            Theme.StrokeRound(g, box.Focused ? Theme.AccentBorder : hover ? Theme.BorderStrong : Theme.Border, r, Theme.SF(7));
            if (glyph != null) Theme.DrawIcon(g, glyph, Theme.IconSmall, Theme.TextDim, new Rectangle(Theme.S(8), 0, Theme.S(24), Height));
        }
    }

    /// <summary>In-app toast shown at the bottom center of a host control.</summary>
    public class Toast : Control
    {
        readonly Timer timer = new Timer();
        string glyph = Glyph.Check;

        public Toast()
        {
            Visible = false;
            Height = Theme.S(46);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            timer.Tick += (s, e) => { timer.Stop(); Visible = false; };
        }

        public void Show(string text, string icon = null, int ms = 2200)
        {
            text = Core.L.T(text);
            Text = text;
            glyph = icon ?? Glyph.Check;
            Width = Theme.TextWidth(text, Theme.BodyBold) + Theme.S(74);
            if (Parent != null) Location = new Point((Parent.ClientSize.Width - Width) / 2, Parent.ClientSize.Height - Height - Theme.S(28));
            Visible = true;
            BringToFront();
            Invalidate();
            timer.Stop(); timer.Interval = ms; timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            var r = new RectangleF(0, 0, Width, Height);
            Theme.FillRound(g, Theme.Surface3, r, Height / 2f);
            Theme.StrokeRound(g, Theme.AccentBorder, r, Height / 2f);
            Theme.DrawIcon(g, glyph, Theme.Icon, Theme.Accent, new Rectangle(Theme.S(18), 0, Theme.S(22), Height));
            Theme.DrawText(g, Text, Theme.BodyBold, Theme.Text, new Rectangle(Theme.S(48), 0, Width - Theme.S(60), Height));
        }
    }
}
