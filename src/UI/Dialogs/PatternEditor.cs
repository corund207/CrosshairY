using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Reticly.Core;
using Reticly.UI.Controls;

namespace Reticly.UI.Dialogs
{
    /// <summary>
    /// Spray pattern editor: one dot per bullet (pixels at 1080p, up = negative Y). Drag to move (Shift moves every later
    /// bullet too), click empty space to add the next bullet, right-click to remove. Saved patterns go to patterns.json;
    /// saving under a built-in weapon's name replaces that pattern.
    /// </summary>
    public sealed class PatternEditorDialog : DarkDialog
    {
        readonly PatternCanvas canvas = new PatternCanvas();
        readonly TextField nameBox, gameBox, categoryBox;
        readonly FieldBox rpmBox, stableBox;
        readonly Dropdown patternDrop;
        readonly WrapLabel status;
        readonly FlatButton deleteBtn, resetBtn;
        List<RecoilPattern> customs;
        string editingKey;   // key of the custom pattern being edited (null = new)
        public RecoilPattern Saved;

        public static RecoilPattern ShowFor(IWin32Window owner, RecoilPattern start, bool newPattern = false)
        {
            using (var d = new PatternEditorDialog(start, newPattern))
                return d.ShowDialog(owner) == DialogResult.OK ? d.Saved : null;
        }

        /// <summary>For --snap screenshots only.</summary>
        internal static Form CreateForSnap(RecoilPattern start) => new PatternEditorDialog(start, false);

        PatternEditorDialog(RecoilPattern start, bool newPattern) : base(L.T("Spray pattern editor"), 900)
        {
            Content.Controls.Add(new WrapLabel(L.T("One dot per bullet. Drag a dot to move it (hold Shift to move every later bullet too), click empty space to add the next bullet, right-click a dot to remove it. Units are pixels at 1080p; up is up."), Theme.Small, Theme.TextDim));

            patternDrop = new Dropdown { Width = Theme.S(300) };
            var startFrom = new FlatButton(L.T("Start from a built-in…"), Glyph.Copy, ButtonKind.Secondary) { Height = Theme.S(36) };
            startFrom.AutoSizeWidth();
            startFrom.Click += (s, e) => ShowStartMenu(startFrom);
            var top = new HStack(new DarkLabel(L.T("Pattern"), Theme.BodyMedium) { Width = Theme.S(70), Height = Theme.S(36) }, patternDrop, startFrom) { Height = Theme.S(36) };
            Content.Controls.Add(top);

            // canvas on the left, fields on the right
            var body = new Panel { Height = Theme.S(440), BackColor = Theme.Surface };
            canvas.Changed += UpdateStatus;
            body.Controls.Add(canvas);
            var side = new StackPanel { BackColor = Theme.Surface, Spacing = Theme.S(10) };
            nameBox = new TextField("", L.T("Weapon name"));
            gameBox = new TextField("", L.T("Game"));
            categoryBox = new TextField("", L.T("Category (e.g. Rifle)"));
            rpmBox = new FieldBox(Glyph.Clock) { Step = 10, Decimals = 0, Minimum = 30, Maximum = 2000 };
            stableBox = new FieldBox(Glyph.Target) { Step = 1, Decimals = 0, Minimum = 1, Maximum = 15 };
            stableBox.ValueCommitted += (s, e) => { canvas.Stable = (int)stableBox.Value; canvas.Invalidate(); };
            side.Controls.Add(new FieldGroup(L.T("Name"), nameBox));
            side.Controls.Add(new FieldGroup(L.T("Game"), gameBox));
            side.Controls.Add(new FieldGroup(L.T("Category"), categoryBox));
            side.Controls.Add(new Columns(new FieldGroup(L.T("Fire rate (RPM)"), rpmBox), new FieldGroup(L.T("Accurate shots"), stableBox)));
            var test = new FlatButton(L.T("Test spray"), Glyph.Fire, ButtonKind.Secondary);
            test.Click += (s, e) => canvas.Play((int)rpmBox.Value, (int)stableBox.Value);
            var fit = new FlatButton(L.T("Fit"), Glyph.Fit, ButtonKind.Ghost);
            fit.Click += (s, e) => canvas.FitView();
            var undo = new FlatButton(L.T("Remove last"), Glyph.Undo, ButtonKind.Ghost);
            undo.Click += (s, e) => canvas.RemoveLast();
            side.Controls.Add(new HStack(test, fit));
            side.Controls.Add(new HStack(undo));
            status = new WrapLabel("", Theme.Small, Theme.TextMute);
            side.Controls.Add(status);
            body.Controls.Add(side);
            body.Layout += (s, e) =>
            {
                int sw = Theme.S(300);
                canvas.SetBounds(0, 0, body.Width - sw - Theme.S(18), body.Height);
                side.SetBounds(body.Width - sw, 0, sw, body.Height);
            };
            Content.Controls.Add(body);

            deleteBtn = AddButton(L.T("Delete"), ButtonKind.Danger, DeleteCurrent);
            resetBtn = AddButton(L.T("Reset to built-in"), ButtonKind.Ghost, ResetCurrent);
            AddButton(L.T("Cancel"), ButtonKind.Ghost, () => { DialogResult = DialogResult.Cancel; Close(); });
            AddButton(L.T("Save pattern"), ButtonKind.Primary, Save);

            customs = Recoil.CustomPatterns;
            FillDrop();
            patternDrop.SelectedIndexChanged += (s, e) =>
            {
                int i = patternDrop.SelectedIndex;
                if (i <= 0) LoadPattern(null, false);
                else LoadPattern(customs[i - 1], true);
            };
            if (newPattern || start == null) { patternDrop.SelectedIndex = customs.Count > 0 && !newPattern && start == null ? 1 : 0; if (patternDrop.SelectedIndex == 0) LoadPattern(start, false); }
            else
            {
                int ci = customs.FindIndex(c => c.Key == start.Key);
                if (ci >= 0) patternDrop.SelectedIndex = ci + 1;
                else { patternDrop.SelectedIndex = 0; LoadPattern(start, false, keepName: true); }
            }
        }

        void FillDrop()
        {
            patternDrop.Items.Clear();
            patternDrop.Items.Add(L.T("Unsaved pattern"));
            foreach (var c in customs) patternDrop.Items.Add(c.Game + "  ·  " + c.Name + (Recoil.BuiltInFor(c.Key) != null ? "  (" + L.T("edited") + ")" : ""));
        }

        /// <summary>Loads a pattern into the editor. existing = editing a saved custom pattern.</summary>
        void LoadPattern(RecoilPattern p, bool existing, bool keepName = false)
        {
            editingKey = existing ? p.Key : null;
            if (p == null)
            {
                nameBox.Text = ""; gameBox.Text = "Custom"; categoryBox.Text = "Rifle";
                rpmBox.Value = 600; stableBox.Value = 1;
                canvas.Points = new List<Point> { Point.Empty };
            }
            else
            {
                // editing a built-in keeps its name, so saving replaces it; "start from" copies are renamed by the user
                nameBox.Text = existing || keepName ? p.Name : p.Name + " (custom)";
                gameBox.Text = existing || keepName ? p.Game : "Custom";
                categoryBox.Text = p.Category;
                rpmBox.Value = p.Rpm;
                stableBox.Value = p.StableShots;
                canvas.Points = p.Points.Select(pt => new Point(pt[0], pt[1])).ToList();
            }
            canvas.Stable = (int)stableBox.Value;
            canvas.FitView();
            deleteBtn.Visible = existing;
            resetBtn.Visible = existing && Recoil.BuiltInFor(p.Key) != null;
            UpdateStatus();
        }

        void ShowStartMenu(Control anchor)
        {
            var m = Menus.Create();
            foreach (var game in Recoil.Games)
            {
                var gm = new ToolStripMenuItem(game) { ForeColor = Theme.Text };
                foreach (var w in Recoil.ForGame(game))
                {
                    var pat = w;
                    var item = new ToolStripMenuItem(w.Label + (w.HandTuned ? "  ★" : "")) { ForeColor = Theme.Text };
                    item.DropDownItems.Item(L.T("Copy as a new pattern"), () => { patternDrop.SelectedIndex = 0; LoadPattern(pat, false); });
                    item.DropDownItems.Item(L.T("Edit this weapon's pattern"), () => { patternDrop.SelectedIndex = 0; LoadPattern(pat, false, keepName: true); });
                    gm.DropDownItems.Add(item);
                }
                m.Items.Add(gm);
            }
            m.Show(anchor, new Point(0, anchor.Height + 2));
        }

        void UpdateStatus()
        {
            if (status == null) return;
            int n = canvas.Points.Count;
            double secs = n * 60.0 / Math.Max(1, rpmBox.Value);
            var last = canvas.Points.LastOrDefault();
            status.Text = string.Format(L.T("{0} bullets · {1:0.0} s spray · ends at {2}, {3} px"), n, secs, last.X, last.Y);
            string key = (gameBox.Text.Trim().Length == 0 ? "Custom" : gameBox.Text.Trim()) + "|" + nameBox.Text.Trim();
            if (editingKey == null && Recoil.BuiltInFor(key) != null) status.Text += "\n" + L.T("Saving replaces the built-in pattern for this weapon (you can reset it later).");
        }

        RecoilPattern Build() => new RecoilPattern
        {
            Name = nameBox.Text.Trim(),
            Game = gameBox.Text.Trim().Length == 0 ? "Custom" : gameBox.Text.Trim(),
            Category = categoryBox.Text.Trim().Length == 0 ? "Custom" : categoryBox.Text.Trim(),
            Rpm = (int)rpmBox.Value,
            StableShots = (int)stableBox.Value,
            Points = canvas.Points.Select(p => new[] { p.X, p.Y }).ToArray(),
            Custom = true
        };

        void Save()
        {
            var p = Build();
            if (p.Name.Length == 0) { Info(this, L.T("Name needed"), L.T("Give the pattern a weapon name.")); return; }
            if (p.Name.Contains("|") || p.Game.Contains("|")) { Info(this, L.T("Invalid name"), L.T("Names can't contain “|”.")); return; }
            if (p.Points.Length < 2) { Info(this, L.T("Add bullets"), L.T("A pattern needs at least two bullets.")); return; }
            if (p.Key != editingKey && customs.Any(c => c.Key == p.Key)
                && !Confirm(this, L.T("Replace pattern"), string.Format(L.T("You already have a custom pattern called “{0}”. Replace it?"), p.Name), L.T("Replace"))) return;
            string old = editingKey;
            Recoil.SaveCustom(p, old);
            Retarget(old ?? p.Key, p);
            Saved = Recoil.Find(p.Key);
            DialogResult = DialogResult.OK;
            Close();
        }

        void DeleteCurrent()
        {
            if (editingKey == null) return;
            var builtIn = Recoil.BuiltInFor(editingKey);
            if (!Confirm(this, L.T("Delete pattern"), builtIn != null ? L.T("Delete your edit and go back to the built-in pattern?") : L.T("Delete this pattern? Crosshairs using it keep their current spray until you pick another weapon."), L.T("Delete"), true)) return;
            string key = editingKey;
            Recoil.DeleteCustom(key);
            if (builtIn != null) Retarget(key, builtIn);
            customs = Recoil.CustomPatterns;
            FillDrop();
            patternDrop.SelectedIndex = 0;
        }

        void ResetCurrent()
        {
            if (editingKey == null || Recoil.BuiltInFor(editingKey) == null) return;
            string key = editingKey;
            Recoil.DeleteCustom(key);
            Retarget(key, Recoil.BuiltInFor(key));
            Saved = Recoil.Find(key);
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Points crosshairs and loadout slots that used oldKey at the new pattern.</summary>
        static void Retarget(string oldKey, RecoilPattern p)
        {
            var app = AppController.I;
            bool changed = false;
            foreach (var e in app.State.Library)
            {
                var cur = Recoil.CurrentWeapon(e.Layers);
                bool uses = e.Layers.OfType<Dictionary<string, object>>().Any(l => J.Str(J.Obj(l, "firingOptions"), "recoilPattern") == oldKey);
                if (!uses || Recoil.IsOff(e.Layers)) continue;
                var fo = e.Layers.OfType<Dictionary<string, object>>().Select(l => J.Obj(l, "firingOptions")).First(f => J.Str(f, "recoilPattern") == oldKey);
                Recoil.SetWeapon(e.Layers, p, J.Num(fo, "recoilFactor", 1));
                e.Updated = DateTime.UtcNow;
                changed = true;
            }
            foreach (var prof in app.State.Profiles)
                foreach (var s in prof.RecoilSlots) if (s.Weapon == oldKey) { s.Weapon = p.Key; app.State.MarkProfilesChanged(); }
            if (changed) { app.State.MarkLibraryChanged(); app.PushCrosshair(); }
        }

        // ------------------------------------------------------------------ canvas

        sealed class PatternCanvas : Control
        {
            public List<Point> Points = new List<Point> { Point.Empty };
            public int Stable = 1;
            public event Action Changed;
            float zoom = 4;           // screen px per pattern px
            PointF origin;            // screen position of (0, 0)
            int drag = -1, hover = -1;
            Point dragStart;
            List<Point> dragOriginal;
            readonly Timer playTimer = new Timer { Interval = 15 };
            DateTime playStart;
            int playRpm, playStable;
            bool playing;

            public PatternCanvas()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                playTimer.Tick += (s, e) => Invalidate();
                Cursor = Cursors.Cross;
            }

            protected override void Dispose(bool disposing) { if (disposing) playTimer.Dispose(); base.Dispose(disposing); }

            public void FitView()
            {
                if (Width <= 0 || Height <= 0) { origin = new PointF(Width / 2f, Height * 0.85f); return; }
                int minX = Math.Min(0, Points.Min(p => p.X)), maxX = Math.Max(0, Points.Max(p => p.X));
                int minY = Math.Min(0, Points.Min(p => p.Y)), maxY = Math.Max(0, Points.Max(p => p.Y));
                float spanX = Math.Max(40, maxX - minX + 20), spanY = Math.Max(60, maxY - minY + 20);
                zoom = Math.Max(1.2f, Math.Min(10f, Math.Min((Width - 40) / spanX, (Height - 40) / spanY)));
                origin = new PointF(Width / 2f - (minX + maxX) / 2f * zoom, Height / 2f - (minY + maxY) / 2f * zoom);
                Invalidate();
            }

            protected override void OnResize(EventArgs e) { base.OnResize(e); FitView(); }

            PointF ToScreen(Point p) => new PointF(origin.X + p.X * zoom, origin.Y + p.Y * zoom);
            Point ToPattern(Point s) => new Point((int)Math.Round((s.X - origin.X) / zoom), (int)Math.Round((s.Y - origin.Y) / zoom));

            int HitTest(Point s)
            {
                float r = Math.Max(6, zoom * 1.2f) + 2;
                for (int i = Points.Count - 1; i >= 0; i--)
                {
                    var p = ToScreen(Points[i]);
                    if ((p.X - s.X) * (p.X - s.X) + (p.Y - s.Y) * (p.Y - s.Y) <= r * r) return i;
                }
                return -1;
            }

            public void RemoveLast()
            {
                if (Points.Count <= 1) return;
                Points.RemoveAt(Points.Count - 1);
                Invalidate();
                Changed?.Invoke();
            }

            public void Play(int rpm, int stable)
            {
                playRpm = Math.Max(30, rpm);
                playStable = Math.Max(1, stable);
                playStart = DateTime.UtcNow;
                playing = true;
                playTimer.Start();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
                int hit = HitTest(e.Location);
                if (e.Button == MouseButtons.Right)
                {
                    if (hit > 0) { Points.RemoveAt(hit); Invalidate(); Changed?.Invoke(); }
                    return;
                }
                if (e.Button != MouseButtons.Left) return;
                if (hit > 0) { drag = hit; dragStart = e.Location; dragOriginal = new List<Point>(Points); return; }
                if (hit == 0) return;   // the first bullet always lands on the crosshair
                Points.Add(ToPattern(e.Location));
                drag = Points.Count - 1;
                dragStart = e.Location;
                dragOriginal = new List<Point>(Points);
                Invalidate();
                Changed?.Invoke();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (drag > 0 && e.Button == MouseButtons.Left)
                {
                    var now = ToPattern(e.Location);
                    var was = ToPattern(dragStart);
                    int dx = now.X - was.X, dy = now.Y - was.Y;
                    bool chain = (ModifierKeys & Keys.Shift) != 0;
                    for (int i = 0; i < Points.Count; i++)
                    {
                        if (i == drag || (chain && i > drag)) Points[i] = new Point(dragOriginal[i].X + dx, dragOriginal[i].Y + dy);
                        else Points[i] = dragOriginal[i];
                    }
                    Invalidate();
                    Changed?.Invoke();
                    return;
                }
                int h = HitTest(e.Location);
                if (h != hover) { hover = h; Cursor = h > 0 ? Cursors.SizeAll : Cursors.Cross; Invalidate(); }
            }

            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = -1; dragOriginal = null; }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                float old = zoom;
                zoom = Math.Max(0.8f, Math.Min(16f, zoom * (e.Delta > 0 ? 1.15f : 1 / 1.15f)));
                // zoom around the cursor
                origin = new PointF(e.X - (e.X - origin.X) * zoom / old, e.Y - (e.Y - origin.Y) * zoom / old);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.FromArgb(18, 18, 18));
                Theme.Smooth(g);
                // grid every 10 px (pattern units), stronger every 50
                using (var minor = new Pen(Color.FromArgb(30, 255, 255, 255)))
                using (var major = new Pen(Color.FromArgb(58, 255, 255, 255)))
                {
                    float step = 10 * zoom;
                    if (step >= 6)
                    {
                        int i0 = (int)Math.Floor(-origin.X / step), i1 = (int)Math.Ceiling((Width - origin.X) / step);
                        for (int i = i0; i <= i1; i++) { float x = origin.X + i * step; g.DrawLine(i % 5 == 0 ? major : minor, x, 0, x, Height); }
                        int j0 = (int)Math.Floor(-origin.Y / step), j1 = (int)Math.Ceiling((Height - origin.Y) / step);
                        for (int j = j0; j <= j1; j++) { float y = origin.Y + j * step; g.DrawLine(j % 5 == 0 ? major : minor, 0, y, Width, y); }
                    }
                }
                using (var axis = new Pen(Color.FromArgb(90, Theme.Accent))) { g.DrawLine(axis, origin.X, 0, origin.X, Height); g.DrawLine(axis, 0, origin.Y, Width, origin.Y); }

                // spray path
                if (Points.Count > 1)
                    using (var pen = new Pen(Color.FromArgb(170, Theme.Accent), Math.Max(1.5f, zoom * 0.35f)) { LineJoin = LineJoin.Round })
                        g.DrawLines(pen, Points.Select(ToScreen).ToArray());
                float r = Math.Max(4, zoom * 0.9f);
                using (var font = new Font(Theme.Small.FontFamily, Math.Max(6.5f, Theme.Small.Size * 0.85f)))
                    for (int i = 0; i < Points.Count; i++)
                    {
                        var p = ToScreen(Points[i]);
                        var c = i < Stable ? Color.FromArgb(0, 230, 160) : i == hover || i == drag ? Theme.AccentHover : Color.White;
                        using (var b = new SolidBrush(c)) g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
                        using (var o = new Pen(Color.Black)) g.DrawEllipse(o, p.X - r, p.Y - r, r * 2, r * 2);
                        if (i % 5 == 4 || i == Points.Count - 1)
                            TextRenderer.DrawText(g, (i + 1).ToString(), font, new Point((int)(p.X + r + 3), (int)(p.Y - r - 2)), Theme.TextDim, TextFormatFlags.NoPadding);
                    }

                // test playback: a red tracker walking the spray at the real fire rate (after the accurate shots)
                if (playing)
                {
                    double shot = 60000.0 / playRpm;
                    double t = (DateTime.UtcNow - playStart).TotalMilliseconds - (playStable - 1) * shot;
                    double idx = Math.Max(0, t / shot);
                    if (idx >= Points.Count + 3) { playing = false; playTimer.Stop(); }
                    else
                    {
                        int a = Math.Min(Points.Count - 1, (int)Math.Floor(idx)), b2 = Math.Min(Points.Count - 1, a + 1);
                        double f = Math.Min(1, idx - Math.Floor(idx));
                        var pa = ToScreen(Points[a]); var pb = ToScreen(Points[b2]);
                        float x = (float)(pa.X + (pb.X - pa.X) * f), y = (float)(pa.Y + (pb.Y - pa.Y) * f);
                        float tr = r + 3;
                        using (var b = new SolidBrush(Color.FromArgb(255, 60, 60))) g.FillEllipse(b, x - tr, y - tr, tr * 2, tr * 2);
                        using (var o = new Pen(Color.Black, 1.5f)) g.DrawEllipse(o, x - tr, y - tr, tr * 2, tr * 2);
                    }
                }
                using (var border = new Pen(Theme.Border)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
                Theme.DrawText(g, L.T("Green = accurate shots · scroll to zoom"), Theme.Small, Theme.TextMute, new Rectangle(Theme.S(10), Height - Theme.S(26), Width - Theme.S(20), Theme.S(20)));
            }
        }
    }
}
