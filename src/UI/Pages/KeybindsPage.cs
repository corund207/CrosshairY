using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.Input;
using CrosshairY.UI.Controls;
using CrosshairY.UI.Dialogs;

namespace CrosshairY.UI.Pages
{
    /// <summary>"Configure Keybinds": action keybinds and crosshair shortcuts, each with keyboard/mouse and controller columns.</summary>
    public sealed class KeybindsPage : Page
    {
        readonly ScrollHost scroll = new ScrollHost { Dock = DockStyle.Fill };
        bool selfEdit;

        public override string Title => L.T("Keybinds");

        public KeybindsPage()
        {
            scroll.Stack.Inner = new Padding(Theme.S(34), Theme.S(28), Theme.S(34), Theme.S(34));
            scroll.Stack.Spacing = Theme.S(14);
            scroll.BackColor = Theme.Bg;
            Controls.Add(scroll);
            App.ProfileChanged += () => { if (Visible && !selfEdit) Build(); };
        }

        void Notify() { selfEdit = true; try { App.NotifyProfileEdited(); } finally { selfEdit = false; } }

        public override void OnActivated() => Build();

        KeybindBox Cap(string value, bool pad, Action<string> set)
        {
            var kb = new KeybindBox { Caps = true, Pad = pad, Binding = value, Height = Theme.S(40) };
            kb.BindingChanged += (s, e) => { set(kb.Binding); Notify(); };
            return kb;
        }

        void Build()
        {
            var st = scroll.Stack;
            st.SuspendLayout();
            foreach (Control c in st.Controls.Cast<Control>().ToList()) { st.Controls.Remove(c); c.Dispose(); }
            var p = App.Profile;

            st.Controls.Add(new DarkLabel(L.T("Configure Keybinds"), Theme.H1) { Height = Theme.S(34) });
            st.Controls.Add(new WrapLabel(string.Format(L.T("Toggle the crosshair, react to aiming and firing, or switch crosshairs with any key, mouse button or controller button. Keybinds belong to the active profile ({0}). Click a binding, then press the input; Esc cancels, Backspace clears."), p.Name), Theme.Nav, Theme.TextDim));
            st.Controls.Add(new Panel { Height = Theme.S(6), BackColor = Theme.Bg });

            st.Controls.Add(new CaptionLabel(Glyph.Keyboard, "Action keybinds"));
            var actions = new KeyTable("Keybind");

            var onoff = new Segmented(new[] { "OFF", "ON" }) { Width = Theme.S(110), Height = Theme.S(36) };
            onoff.SetSilently(App.CrosshairVisible ? 1 : 0);
            onoff.SelectedChanged += (s, e) => App.SetVisible(onoff.SelectedIndex == 1);
            actions.AddRow(Glyph.EyeOff, "Global Toggle", onoff, Cap(p.ToggleKey, false, v => p.ToggleKey = v), Cap(p.TogglePad, true, v => p.TogglePad = v));

            var aimActions = new[] { ("none", "Nothing"), ("hide", "Hide Crosshair"), ("show", "Show Crosshair"), ("switch", "Show Saved Crosshair") };
            var aimDrop = new Dropdown { Width = Theme.S(220) };
            foreach (var a in aimActions) aimDrop.Items.Add(a.Item2 + (a.Item1 == "none" ? "" : p.AimToggleMode ? " (Toggle)" : " (Hold)"));
            aimDrop.SelectedIndex = Math.Max(0, Array.FindIndex(aimActions, a => a.Item1 == p.AimAction));
            aimDrop.SelectedIndexChanged += (s, e) => { p.AimAction = aimActions[aimDrop.SelectedIndex].Item1; Notify(); Build(); };
            var aimExtra = new HStack(aimDrop) { Width = Theme.S(320) };
            if (p.AimAction == "switch")
            {
                var target = App.State.Find(p.AimCrosshairId);
                var pick = new FlatButton(target == null ? "Pick…" : "", target == null ? Glyph.Hand : null, ButtonKind.Secondary) { Width = Theme.S(target == null ? 80 : 40), Height = Theme.S(36) };
                if (target != null) pick.Paint += (s, e) =>
                {
                    var bmp = Thumbnails.Actual(target.Id + ":" + target.Updated.Ticks, target.Layers, pick.Width - 6, pick.Height - 6);
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    e.Graphics.DrawImageUnscaled(bmp, 3, 3);
                };
                pick.Click += (s, e) =>
                {
                    var id = CrosshairPickerDialog.Pick(Main, "Crosshair while aiming", p.AimCrosshairId);
                    if (id == null) return;
                    p.AimCrosshairId = id;
                    Notify();
                    Build();
                };
                new ToolTip().SetToolTip(pick, target == null ? "Choose the crosshair to show while aiming" : target.Name);
                aimExtra.Controls.Add(pick);
            }
            var mode = new FlatButton(p.AimToggleMode ? "Toggle" : "Hold", null, ButtonKind.Secondary) { Height = Theme.S(36) };
            mode.AutoSizeWidth();
            mode.Click += (s, e) => { p.AimToggleMode = !p.AimToggleMode; Notify(); Build(); };
            new ToolTip().SetToolTip(mode, "Hold the aim key, or press once to toggle");
            if (p.AimAction != "none") aimExtra.Controls.Add(mode);
            actions.AddRow(Glyph.Aim, "Aim", aimExtra, Cap(p.AimKey, false, v => p.AimKey = v), Cap(p.AimPad, true, v => p.AimPad = v));
            actions.AddRow(Glyph.Fire, "Fire", null, Cap(p.FireKey, false, v => p.FireKey = v), Cap(p.FirePad, true, v => p.FirePad = v));
            actions.AddRow(Glyph.Reload, "Reload", null, Cap(p.ReloadKey, false, v => p.ReloadKey = v), Cap(p.ReloadPad, true, v => p.ReloadPad = v));
            actions.AddRow(Glyph.Right, "Next Crosshair", null, Cap(p.NextKey, false, v => p.NextKey = v), Cap(p.NextPad, true, v => p.NextPad = v));
            actions.AddRow(Glyph.Left, "Previous Crosshair", null, Cap(p.PrevKey, false, v => p.PrevKey = v), Cap(p.PrevPad, true, v => p.PrevPad = v));
            actions.AddRow(Glyph.Recoil, "Next Weapon (recoil)", null, Cap(p.NextWeaponKey, false, v => p.NextWeaponKey = v), Cap(p.NextWeaponPad, true, v => p.NextWeaponPad = v));
            actions.AddRow(Glyph.Recoil, "Previous Weapon (recoil)", null, Cap(p.PrevWeaponKey, false, v => p.PrevWeaponKey = v), Cap(p.PrevWeaponPad, true, v => p.PrevWeaponPad = v));
            var reactSettings = new FlatButton(L.T("Style…"), Glyph.Settings, ButtonKind.Ghost) { Height = Theme.S(36) };
            reactSettings.AutoSizeWidth();
            reactSettings.Click += (s, e) => { Main.Navigate("settings"); (Main.CurrentPage as SettingsPage)?.ShowCategory("reactions"); };
            actions.AddRow(Glyph.Target, "Hit Marker", reactSettings, Cap(p.HitKey, false, v => p.HitKey = v), Cap(p.HitPad, true, v => p.HitPad = v));
            actions.AddRow(Glyph.Fire, "Kill Flash", null, Cap(p.KillKey, false, v => p.KillKey = v), Cap(p.KillPad, true, v => p.KillPad = v));
            st.Controls.Add(actions);
            if (!App.State.Settings.ControllerSupport)
            {
                var note = new WrapLabel("Controller bindings are active when Controller support is on (Settings › Input).", Theme.Small, Theme.TextMute);
                st.Controls.Add(note);
            }

            // ---------------- recoil loadout ----------------
            st.Controls.Add(new Panel { Height = Theme.S(10), BackColor = Theme.Bg });
            st.Controls.Add(new CaptionLabel(Glyph.Recoil, "Recoil loadout"));
            st.Controls.Add(new WrapLabel(L.T("Pick the guns you use and give each one a key. Bind them to the same keys as your in-game weapon slots (1, 2, 3…) and the recoil tracker switches pattern when you swap guns. Each gun can have its own scale and its own crosshair. Next / Previous Weapon cycle through this list."), Theme.Small, Theme.TextDim));
            var loadout = new KeyTable("Weapon");
            var tips = new ToolTip();
            foreach (var sl in p.RecoilSlots.ToList())
            {
                var slot = sl;
                var pat = slot.Weapon == "off" ? null : Recoil.Find(slot.Weapon);
                string label = slot.Weapon == "off" ? L.T("Recoil off (knife / utility)") : pat != null ? pat.Name + "  ·  " + pat.Game : L.T("Unknown weapon");
                // per-weapon crosshair
                var xh = App.State.Find(slot.CrosshairId);
                var pick = new FlatButton(xh == null ? "" : "", xh == null ? Glyph.Crosshair : null, xh == null ? ButtonKind.Ghost : ButtonKind.Secondary) { Width = Theme.S(40), Height = Theme.S(36) };
                if (xh != null) pick.Paint += (s, e) =>
                {
                    var bmp = Thumbnails.Actual(xh.Id + ":" + xh.Updated.Ticks, xh.Layers, pick.Width - 6, pick.Height - 6);
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    e.Graphics.DrawImageUnscaled(bmp, 3, 3);
                };
                tips.SetToolTip(pick, xh == null ? L.T("Crosshair for this weapon: keep the current one (click to choose)") : L.T("Crosshair for this weapon:") + " " + xh.Name);
                pick.Click += (s, e) =>
                {
                    var m = Menus.Create();
                    m.Items.Item(L.T("Choose crosshair…"), () =>
                    {
                        var id = CrosshairPickerDialog.Pick(Main, L.T("Crosshair for") + " " + label, slot.CrosshairId);
                        if (string.IsNullOrEmpty(id)) return;
                        slot.CrosshairId = id;
                        Notify();
                        Build();
                    });
                    m.Items.Item(L.T("Keep the current crosshair"), () => { slot.CrosshairId = ""; Notify(); Build(); }, xh != null);
                    m.Show(pick, new Point(0, pick.Height + 2));
                };
                // per-weapon scale
                var scale = new FieldBox(Glyph.Fit) { Step = 0.05, Decimals = 2, Minimum = 0.2, Maximum = 5, Width = Theme.S(92), Height = Theme.S(36), Enabled = pat != null };
                scale.Value = slot.Scale;
                tips.SetToolTip(scale, L.T("Recoil scale for this weapon (multiplies the crosshair's own scale)"));
                scale.ValueCommitted += (s, e) =>
                {
                    slot.Scale = scale.Value;
                    Notify();
                    // re-apply straight away if this gun is the one in use
                    var act = App.ActiveCrosshair;
                    if (act != null && pat != null && !Recoil.IsOff(act.Layers) && Recoil.CurrentWeapon(act.Layers)?.Key == pat.Key) App.SelectRecoilSlot(slot);
                };
                var change = new FlatButton("", Glyph.Swap, ButtonKind.Secondary) { Width = Theme.S(40), Height = Theme.S(36) };
                tips.SetToolTip(change, L.T("Change weapon"));
                change.Click += (s, e) => ShowWeaponMenu(change, w => { slot.Weapon = w; Notify(); Build(); });
                var remove = new FlatButton("", Glyph.Delete, ButtonKind.Ghost) { Width = Theme.S(36), Height = Theme.S(36) };
                remove.Click += (s, e) => { p.RecoilSlots.Remove(slot); Notify(); Build(); };
                var extra = new HStack(pick, scale, change, remove) { Width = pick.Width + scale.Width + change.Width + remove.Width + Theme.S(24), Height = Theme.S(36) };
                loadout.AddRow(slot.Weapon == "off" ? Glyph.Close : Glyph.Recoil, label, extra,
                    Cap(slot.Key, false, v => slot.Key = v), Cap(slot.PadKey, true, v => slot.PadKey = v));
            }
            var addSlot = new FlatButton("Add weapon", Glyph.Add, ButtonKind.Ghost) { Height = Theme.S(40) };
            addSlot.AutoSizeWidth();
            addSlot.Click += (s, e) => ShowWeaponMenu(addSlot, w =>
            {
                p.RecoilSlots.Add(new RecoilSlot { Weapon = w, Key = DefaultSlotKey(p) });
                Notify();
                Build();
            });
            var quick = new FlatButton("Quick setup", Glyph.Lightning, ButtonKind.Ghost) { Height = Theme.S(40) };
            quick.AutoSizeWidth();
            quick.Click += (s, e) => ShowQuickSetup(quick, p);
            var patterns = new FlatButton(L.T("Edit patterns…"), Glyph.Edit, ButtonKind.Ghost) { Height = Theme.S(40) };
            patterns.AutoSizeWidth();
            patterns.Click += (s, e) => { PatternEditorDialog.ShowFor(Main, null); Build(); };
            var footer = new HStack(addSlot, quick, patterns) { Height = Theme.S(40), Width = addSlot.Width + quick.Width + patterns.Width + Theme.S(16) };
            loadout.AddFooter(footer);
            st.Controls.Add(loadout);

            st.Controls.Add(new Panel { Height = Theme.S(10), BackColor = Theme.Bg });
            st.Controls.Add(new CaptionLabel(Glyph.Crosshair, "Crosshair shortcuts"));
            var shortcuts = new KeyTable("Crosshairs");
            foreach (var b in p.CrosshairBinds.ToList())
            {
                var bind = b;
                var e = App.State.Find(b.CrosshairId);
                if (e == null) continue;
                var remove = new FlatButton("", Glyph.Delete, ButtonKind.Ghost) { Width = Theme.S(36), Height = Theme.S(36) };
                remove.Click += (s, a) => { p.CrosshairBinds.Remove(bind); Notify(); Build(); };
                shortcuts.AddRow(null, e.Name, remove, Cap(b.Key, false, v => bind.Key = v), Cap(b.PadKey, true, v => bind.PadKey = v), e);
            }
            var add = new FlatButton("Add crosshair shortcut", Glyph.Add, ButtonKind.Ghost) { Height = Theme.S(40) };
            add.AutoSizeWidth();
            add.Click += (s, e) =>
            {
                var id = CrosshairPickerDialog.Pick(Main, "Choose a crosshair", null);
                if (string.IsNullOrEmpty(id)) return;
                if (!p.CrosshairBinds.Any(x => x.CrosshairId == id)) p.CrosshairBinds.Add(new CrosshairBind { CrosshairId = id });
                Notify();
                Build();
            };
            shortcuts.AddFooter(add);
            st.Controls.Add(shortcuts);

            st.ResumeLayout(true);
            scroll.PerformLayout();
        }

        /// <summary>Game › Category · Weapon menu, plus "Recoil off".</summary>
        void ShowWeaponMenu(Control anchor, Action<string> picked)
        {
            var m = Menus.Create();
            foreach (var game in Recoil.Games)
            {
                var gm = new ToolStripMenuItem(game) { ForeColor = Theme.Text };
                string lastCat = null;
                foreach (var w in Recoil.ForGame(game))
                {
                    if (lastCat != null && w.Category != lastCat) gm.DropDownItems.Add(new ToolStripSeparator());
                    lastCat = w.Category;
                    var key = w.Key;
                    gm.DropDownItems.Item(w.Label + (w.HandTuned ? "  ★" : ""), () => picked(key));
                }
                m.Items.Add(gm);
            }
            m.Items.Sep();
            m.Items.Item(L.T("New custom pattern…"), () =>
            {
                var made = PatternEditorDialog.ShowFor(Main, null, newPattern: true);
                if (made != null) picked(made.Key);
            });
            m.Items.Item(L.T("Recoil off (knife / utility)"), () => picked("off"));
            m.Show(anchor, new Point(0, anchor.Height + 2));
        }

        /// <summary>Suggests the next free number key (1–9) for a new loadout slot.</summary>
        static string DefaultSlotKey(Profile p)
        {
            for (int i = 1; i <= 9; i++)
                if (!p.RecoilSlots.Any(s => s.Key == i.ToString())) return i.ToString();
            return "";
        }

        /// <summary>One-click loadouts that match each game's weapon-slot keys.</summary>
        void ShowQuickSetup(Control anchor, Profile p)
        {
            var m = Menus.Create();
            void Set(params (string weapon, string key)[] slots)
            {
                if (p.RecoilSlots.Count > 0 && !DarkDialog.Confirm(Main, L.T("Replace loadout"), L.T("Replace your current recoil loadout?"), L.T("Replace"))) return;
                p.RecoilSlots = slots.Select(s => new RecoilSlot { Weapon = s.weapon, Key = s.key }).ToList();
                Notify();
                Build();
            }
            m.Items.Item("VALORANT — Vandal (1) · Sheriff (2) · off (3)", () => Set(("VALORANT|Vandal", "1"), ("VALORANT|Sheriff", "2"), ("off", "3")));
            m.Items.Item("VALORANT — Phantom (1) · Ghost (2) · off (3)", () => Set(("VALORANT|Phantom", "1"), ("VALORANT|Ghost", "2"), ("off", "3")));
            m.Items.Item("CS2 — AK-47 (1) · Glock-18 (2) · off (3)", () => Set(("Counter-Strike 2|AK-47", "1"), ("Counter-Strike 2|Glock-18", "2"), ("off", "3")));
            m.Items.Item("CS2 — M4A4 (1) · USP-S (2) · off (3)", () => Set(("Counter-Strike 2|M4A4", "1"), ("Counter-Strike 2|USP-S", "2"), ("off", "3")));
            m.Items.Item("Rust — Assault Rifle (1) · Thompson (2) · Python (3)", () => Set(("Rust|Assault Rifle", "1"), ("Rust|Thompson", "2"), ("Rust|Python Revolver", "3")));
            m.Items.Item("Apex — R-301 (1) · R-99 (2)", () => Set(("Apex Legends|R-301", "1"), ("Apex Legends|R-99", "2")));
            m.Show(anchor, new Point(0, anchor.Height + 2));
        }

        /// <summary>Rounded table: header row (label | keyboard icon | gamepad icon) and binding rows.</summary>
        sealed class KeyTable : Panel
        {
            readonly string header;
            readonly List<(string glyph, string label, Control extra, Control key, Control pad, CrosshairEntry entry)> rows = new List<(string, string, Control, Control, Control, CrosshairEntry)>();
            Control footer;
            int HeaderH => Theme.S(54);
            int RowH => Theme.S(64);

            public KeyTable(string header)
            {
                this.header = header;
                BackColor = Theme.Chrome;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                Fit();
            }

            public void AddRow(string glyph, string label, Control extra, Control key, Control pad, CrosshairEntry entry = null)
            {
                rows.Add((glyph, label, extra, key, pad, entry));
                if (extra != null) Controls.Add(extra);
                Controls.Add(key);
                Controls.Add(pad);
                Fit();
            }

            public void AddFooter(Control c) { footer = c; Controls.Add(c); Fit(); }

            void Fit() { Height = HeaderH + rows.Count * RowH + (footer != null ? Theme.S(58) : 0) + 2; }

            int Col1 => (int)(Width * 0.52);
            int Col2 => (int)(Width * 0.76);

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                for (int i = 0; i < rows.Count; i++)
                {
                    int y = HeaderH + i * RowH;
                    var r = rows[i];
                    if (r.extra != null)
                    {
                        int w = r.extra.Width;
                        r.extra.SetBounds(Col1 - w - Theme.S(18), y + (RowH - r.extra.Height) / 2, w, r.extra.Height);
                    }
                    r.key.SetBounds(Col1 + Theme.S(14), y + (RowH - r.key.Height) / 2, Col2 - Col1 - Theme.S(28), r.key.Height);
                    r.pad.SetBounds(Col2 + Theme.S(14), y + (RowH - r.pad.Height) / 2, Width - Col2 - Theme.S(28), r.pad.Height);
                }
                if (footer != null) footer.Location = new Point(Theme.S(14), HeaderH + rows.Count * RowH + Theme.S(9));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.Bg);
                Theme.Smooth(g);
                var r = new RectangleF(0, 0, Width, Height);
                Theme.FillRound(g, Theme.Chrome, r, Theme.SF(10));
                using (var path = Theme.Round(r, Theme.SF(10)))
                {
                    var stt = g.Save();
                    g.SetClip(path);
                    using (var b = new SolidBrush(Theme.Well)) g.FillRectangle(b, 0, 0, Width, HeaderH);
                    g.Restore(stt);
                }
                Theme.StrokeRound(g, Theme.Border, r, Theme.SF(10));
                using (var p = new Pen(Theme.Border))
                {
                    g.DrawLine(p, 0, HeaderH, Width, HeaderH);
                    g.DrawLine(p, Col1, 0, Col1, HeaderH + rows.Count * RowH);
                    g.DrawLine(p, Col2, 0, Col2, HeaderH + rows.Count * RowH);
                    for (int i = 1; i <= rows.Count; i++) if (i < rows.Count || footer != null) g.DrawLine(p, 0, HeaderH + i * RowH, Width, HeaderH + i * RowH);
                }
                Theme.DrawText(g, L.Upper(header), Theme.Caption, Theme.Text, new Rectangle(Theme.S(28), 0, Col1, HeaderH));
                Theme.DrawIcon(g, Glyph.Monitor, Theme.Icon, Theme.TextDim, new Rectangle(Col1, 0, Col2 - Col1, HeaderH));
                Theme.DrawIcon(g, Glyph.Gamepad, Theme.Icon, Theme.TextDim, new Rectangle(Col2, 0, Width - Col2, HeaderH));
                for (int i = 0; i < rows.Count; i++)
                {
                    int y = HeaderH + i * RowH;
                    var row = rows[i];
                    int x = Theme.S(26);
                    if (row.entry != null)
                    {
                        var thumb = new Rectangle(x, y + (RowH - Theme.S(44)) / 2, Theme.S(44), Theme.S(44));
                        Theme.FillRound(g, Color.FromArgb(12, 12, 12), thumb, Theme.SF(6));
                        var bmp = Thumbnails.Actual(row.entry.Id + ":" + row.entry.Updated.Ticks, row.entry.Layers, thumb.Width - 4, thumb.Height - 4);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                        g.DrawImageUnscaled(bmp, thumb.X + 2, thumb.Y + 2);
                        Theme.Smooth(g);
                        x = thumb.Right + Theme.S(14);
                    }
                    else if (row.glyph != null)
                    {
                        Theme.DrawIcon(g, row.glyph, Theme.Icon, Theme.TextDim, new Rectangle(x, y, Theme.S(22), RowH));
                        x += Theme.S(36);
                    }
                    int right = row.extra != null ? row.extra.Left - Theme.S(10) : Col1 - Theme.S(10);
                    Theme.DrawText(g, L.T(row.label), Theme.H3, Theme.Text, new Rectangle(x, y, right - x, RowH));
                }
            }
        }
    }
}
