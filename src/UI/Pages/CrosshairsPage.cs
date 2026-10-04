using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Reticly.Core;
using Reticly.Import;
using Reticly.Input;
using Reticly.UI.Controls;
using Reticly.UI.Dialogs;

namespace Reticly.UI.Pages
{
    /// <summary>Crosshairs section: Discover / Browse / Saved tabs with Share and Import actions.</summary>
    public sealed class CrosshairsPage : Page
    {
        readonly TabBar tabs = new TabBar { Dock = DockStyle.Top };
        readonly Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        readonly DiscoverView discover;
        readonly BrowseView browse;
        readonly SavedView saved;
        Control currentView;

        public override string Title => "Crosshairs";

        public CrosshairsPage()
        {
            tabs.Tabs.Add(new TabBar.Tab { Text = "Discover", Glyph = Glyph.Compass });
            tabs.Tabs.Add(new TabBar.Tab { Text = "Browse", Glyph = Glyph.Search });
            tabs.Tabs.Add(new TabBar.Tab { Text = "Saved", Glyph = Glyph.BookmarkFilled });
            tabs.Actions.Add(new TabBar.Tab { Text = "Share", Glyph = Glyph.Upload });
            tabs.Actions.Add(new TabBar.Tab { Text = "Import", Glyph = Glyph.Download });
            discover = new DiscoverView(this) { Dock = DockStyle.Fill, Visible = false };
            browse = new BrowseView { Dock = DockStyle.Fill, Visible = false };
            saved = new SavedView { Dock = DockStyle.Fill, Visible = false };
            host.Controls.AddRange(new Control[] { discover, browse, saved });
            Controls.Add(host);
            Controls.Add(tabs);
            tabs.SelectedChanged += i => ShowView(i);
            tabs.ActionClicked += a =>
            {
                if (a == 0) Main.ShowShare(saved.SelectedEntry ?? App.ActiveCrosshair);
                else Main.ShowImport();
            };
            tabs.SetSilently(2);
            ShowView(2);
        }

        void ShowView(int i)
        {
            if (currentView != null) currentView.Visible = false;
            currentView = i == 0 ? (Control)discover : i == 1 ? browse : saved;
            currentView.Visible = true;
            if (currentView == discover) discover.Rebuild();
            if (currentView == saved) saved.Rebuild();
            if (currentView != browse) browse.StopPreview();
        }

        public void SelectTab(int i) { tabs.SetSilently(i); ShowView(i); }

        public void ShowSaved(string highlightId)
        {
            SelectTab(2);
            saved.Highlight(highlightId);
        }

        public override void OnActivated()
        {
            if (currentView == saved) saved.Rebuild();
            if (currentView == discover) discover.Rebuild();
        }

        public override void OnDeactivated() => browse.StopPreview();

        // ---------------------------------------------------------------- shared helpers

        internal static void ShowEntryMenu(CrosshairEntry e, Control anchor, Point pt)
        {
            var app = AppController.I;
            var main = MainForm.Instance;
            var m = Menus.Create();
            m.Items.Item("Use", () => app.ApplyCrosshair(e.Id));
            m.Items.Item("Edit in Designer", () => main.EditInDesigner(e));
            m.Items.Sep();
            m.Items.Item("Rename…", () =>
            {
                var n = InputDialog.Ask(main, "Rename crosshair", "Name", e.Name);
                if (n != null) { e.Name = n; e.Updated = DateTime.UtcNow; app.State.MarkLibraryChanged(); }
            });
            m.Items.Item("Duplicate", () =>
            {
                var c = e.Clone();
                c.Name = e.Name + " copy";
                c.Created = c.Updated = DateTime.UtcNow;
                int idx = app.State.Library.IndexOf(e);
                foreach (var x in app.State.Library.Where(x => x.Order > e.Order)) x.Order++;
                c.Order = e.Order + 1;
                app.State.Library.Insert(idx + 1, c);
                app.State.MarkLibraryChanged();
            });
            m.Items.Item(e.Favorite ? "Remove from Favorites" : "Add to Favorites", () => { e.Favorite = !e.Favorite; app.State.MarkLibraryChanged(); });
            var cats = new ToolStripMenuItem("Move to category") { ForeColor = Theme.Text };
            cats.DropDownItems.Item("(None)", () => { e.Folder = ""; app.State.MarkLibraryChanged(); }, true, e.Folder == "");
            foreach (var f in app.State.Folders)
            {
                var fn = f;
                cats.DropDownItems.Item(fn, () => { e.Folder = fn; app.State.MarkLibraryChanged(); }, true, e.Folder == fn);
            }
            cats.DropDownItems.Add(new ToolStripSeparator());
            cats.DropDownItems.Item("New category…", () =>
            {
                var n = InputDialog.Ask(main, "New category", "Category name");
                if (n == null) return;
                if (!app.State.Folders.Contains(n)) app.State.Folders.Add(n);
                e.Folder = n;
                app.State.MarkLibraryChanged();
            });
            m.Items.Add(cats);
            if (Recoil.HasRecoil(e.Layers)) m.Items.Add(WeaponMenu(e));
            m.Items.Sep();
            m.Items.Item("Share…", () => main.ShowShare(e));
            m.Items.Item("Copy share code", () => { try { Clipboard.SetText(CodeImporter.ExportOwn(e.Layers, e.Name)); main.ShowToast("Share code copied", Glyph.Copy); } catch { } });
            m.Items.Item("Assign keybind…", () => AssignKey(e));
            m.Items.Sep();
            m.Items.Item("Delete", () =>
            {
                if (!DarkDialog.Confirm(main, "Delete crosshair", "Delete “" + e.Name + "”? This can't be undone.", "Delete", true)) return;
                bool wasActive = app.Profile.CrosshairId == e.Id;
                app.State.Remove(e.Id);
                if (wasActive) app.PushCrosshair();
            });
            m.Show(anchor, pt);
        }

        /// <summary>"Recoil weapon ▸ Game ▸ Category · Weapon" submenu for recoil crosshairs.</summary>
        internal static ToolStripMenuItem WeaponMenu(CrosshairEntry e)
        {
            var app = AppController.I;
            var cur = Recoil.CurrentWeapon(e.Layers);
            var root = new ToolStripMenuItem("Recoil weapon" + (cur != null ? "  (" + cur.Name + ")" : "")) { ForeColor = Theme.Text };
            foreach (var game in Recoil.Games)
            {
                var gm = new ToolStripMenuItem(game) { ForeColor = Theme.Text, Checked = cur?.Game == game };
                string lastCat = null;
                foreach (var p in Recoil.ForGame(game))
                {
                    if (lastCat != null && p.Category != lastCat) gm.DropDownItems.Add(new ToolStripSeparator());
                    lastCat = p.Category;
                    var pat = p;
                    gm.DropDownItems.Item(p.Label + (p.HandTuned ? "  ★" : ""), () =>
                    {
                        Recoil.SetWeapon(e.Layers, pat);
                        e.Updated = DateTime.UtcNow;
                        app.State.MarkLibraryChanged();
                        if (app.Profile.CrosshairId == e.Id || app.Profile.AimCrosshairId == e.Id) app.PushCrosshair();
                        MainForm.Instance.ShowToast("“" + e.Name + "” now tracks the " + pat.Name, Glyph.Recoil);
                    }, true, cur?.Key == p.Key);
                }
                root.DropDownItems.Add(gm);
            }
            return root;
        }

        internal static void AssignKey(CrosshairEntry e)
        {
            var app = AppController.I;
            var p = app.Profile;
            var existing = p.CrosshairBinds.FirstOrDefault(b => b.CrosshairId == e.Id);
            using (var d = new KeyAssignDialog("Keybind for “" + e.Name + "”", existing?.Key ?? ""))
            {
                if (d.ShowDialog(MainForm.Instance) != DialogResult.OK) return;
                string pad = existing?.PadKey ?? "";
                p.CrosshairBinds.RemoveAll(b => b.CrosshairId == e.Id);
                if (!string.IsNullOrEmpty(d.Binding) || pad.Length > 0) p.CrosshairBinds.Add(new CrosshairBind { Key = d.Binding, PadKey = pad, CrosshairId = e.Id });
                app.NotifyProfileEdited();
                app.State.MarkLibraryChanged();
            }
        }

        /// <summary>Search field with an icon (and optional SEARCH button) on the well color.</summary>
        internal sealed class SearchBox : DarkControl
        {
            public readonly TextBox Box = new TextBox();
            readonly FlatButton button;
            public event EventHandler Search;

            public SearchBox(string placeholder, bool withButton)
            {
                Height = Theme.S(46);
                Box.BorderStyle = BorderStyle.None;
                Box.BackColor = Theme.Well;
                Box.ForeColor = Theme.Text;
                Box.Font = Theme.Nav;
                Controls.Add(Box);
                Box.HandleCreated += (s, e) => Native.SendMessage(Box.Handle, Native.EM_SETCUEBANNER, (IntPtr)1, placeholder);
                Box.TextChanged += (s, e) => Search?.Invoke(this, EventArgs.Empty);
                Box.GotFocus += (s, e) => Invalidate();
                Box.LostFocus += (s, e) => Invalidate();
                if (withButton)
                {
                    button = new FlatButton("SEARCH", null, ButtonKind.Secondary) { Height = Theme.S(34) };
                    button.Font = Theme.SmallBold;
                    button.AutoSizeWidth();
                    button.Click += (s, e) => Search?.Invoke(this, EventArgs.Empty);
                    Controls.Add(button);
                }
                Cursor = Cursors.IBeam;
            }

            public override string Text { get => Box?.Text ?? ""; set { if (Box != null) Box.Text = value; } }

            protected override void OnClick(EventArgs e) { base.OnClick(e); Box.Focus(); }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                if (Box == null) return;
                int right = button != null ? button.Width + Theme.S(14) : Theme.S(14);
                Box.SetBounds(Theme.S(48), (Height - Box.Height) / 2, Width - Theme.S(48) - right, Box.Height);
                if (button != null) button.Location = new Point(Width - button.Width - Theme.S(6), (Height - button.Height) / 2);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                g.Clear(ParentBack);
                var r = new RectangleF(0, 0, Width, Height);
                Theme.FillRound(g, Theme.Well, r, Theme.SF(9));
                Theme.StrokeRound(g, Box.Focused ? Theme.AccentBorder : Theme.Border, r, Theme.SF(9));
                Theme.DrawIcon(g, Glyph.Search, Theme.Icon, Theme.TextDim, new Rectangle(Theme.S(16), 0, Theme.S(22), Height));
            }
        }

        // ---------------------------------------------------------------- Saved

        sealed class SavedView : Panel
        {
            readonly SearchBox search = new SearchBox("Search saved crosshairs…", false);
            readonly Dropdown sort = new Dropdown { Width = Theme.S(150) };
            readonly CategoryList cats = new CategoryList { AddText = "New Category" };
            readonly Panel catPanel = new Panel { Dock = DockStyle.Left, BackColor = Theme.Well };
            readonly ScrollHost scroll = new ScrollHost { Dock = DockStyle.Fill };
            readonly CrosshairGrid grid = new CrosshairGrid { Style = TileStyle.Saved, EmptyText = "No crosshairs here yet.\nBrowse designs, import a code, or build one in the Designer." };
            readonly Label countLabel = new Label { ForeColor = Theme.TextDim, Font = Theme.Body, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Bottom, BackColor = Theme.Well };
            bool dirty = true, syncing;
            static readonly string[] SortKeys = { "manual", "lastUsed", "name", "newest" };
            AppController App => AppController.I;

            public CrosshairEntry SelectedEntry => grid.Selected == null ? null : App.State.Find(grid.Selected.Id);

            public SavedView()
            {
                BackColor = Theme.Bg;
                var top = new Panel { Dock = DockStyle.Top, Height = Theme.S(76), BackColor = Theme.Chrome };
                var sortLabel = new DarkLabel("Sort:", Theme.Nav, Theme.TextDim) { Width = Theme.S(44) };
                top.Controls.AddRange(new Control[] { search, sortLabel, sort });
                top.Layout += (s, e) =>
                {
                    int pad = Theme.S(24);
                    sort.Location = new Point(top.Width - pad - sort.Width, (top.Height - sort.Height) / 2);
                    sortLabel.SetBounds(sort.Left - Theme.S(50), 0, Theme.S(44), top.Height);
                    search.SetBounds(pad, (top.Height - search.Height) / 2, sortLabel.Left - pad - Theme.S(16), search.Height);
                };
                top.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };

                catPanel.Width = Theme.S(238);
                catPanel.Padding = new Padding(Theme.S(18), Theme.S(6), Theme.S(18), 0);
                var catScroll = new ScrollHost { Dock = DockStyle.Fill, BackColor = Theme.Well };
                catScroll.Stack.BackColor = Theme.Well;
                catScroll.Stack.Controls.Add(cats);
                countLabel.Height = Theme.S(52);
                countLabel.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, countLabel.Width, 0); };
                catPanel.Controls.Add(catScroll);
                catPanel.Controls.Add(countLabel);
                catPanel.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, catPanel.Width - 1, 0, catPanel.Width - 1, catPanel.Height); };

                scroll.Stack.Inner = new Padding(Theme.S(28), Theme.S(28), Theme.S(28), Theme.S(28));
                scroll.Stack.Controls.Add(grid);
                scroll.BackColor = Theme.Bg;
                Controls.Add(scroll);
                Controls.Add(catPanel);
                Controls.Add(top);

                sort.Items.AddRange(new object[] { "Manual", "Recently used", "Name", "Newest" });
                syncing = true;
                sort.SelectedIndex = Math.Max(0, Array.IndexOf(SortKeys, App.State.Settings.LibrarySort));
                syncing = false;
                sort.SelectedIndexChanged += (s, e) =>
                {
                    if (syncing) return;
                    App.State.Settings.LibrarySort = SortKeys[Math.Max(0, sort.SelectedIndex)];
                    App.State.MarkSettingsChanged();
                    Rebuild();
                };
                search.Search += (s, e) => Rebuild();
                cats.Selected += it => Rebuild();
                cats.AddClicked += () =>
                {
                    var n = InputDialog.Ask(MainForm.Instance, "New category", "Category name (e.g. a game)");
                    if (n == null || App.State.Folders.Contains(n)) return;
                    App.State.Folders.Add(n);
                    App.State.MarkLibraryChanged();
                    cats.SelectedKey = "f:" + n;
                    Rebuild();
                };
                cats.ItemRightClicked += (it, pt) =>
                {
                    if (it.Key == null || !it.Key.StartsWith("f:")) return;
                    string name = it.Key.Substring(2);
                    var m = Menus.Create();
                    m.Items.Item("Rename…", () =>
                    {
                        var n = InputDialog.Ask(MainForm.Instance, "Rename category", "Name", name);
                        if (n == null || n == name) return;
                        int i = App.State.Folders.IndexOf(name);
                        if (i >= 0) App.State.Folders[i] = n;
                        foreach (var e in App.State.Library.Where(x => x.Folder == name)) e.Folder = n;
                        App.State.MarkLibraryChanged();
                    });
                    m.Items.Item("Delete category", () =>
                    {
                        App.State.Folders.Remove(name);
                        foreach (var e in App.State.Library.Where(x => x.Folder == name)) e.Folder = "";
                        App.State.MarkLibraryChanged();
                    });
                    m.Show(cats, pt);
                };

                grid.ItemClicked += it => App.ApplyCrosshair(it.Id);
                grid.ItemDoubleClicked += it => { var e = App.State.Find(it.Id); if (e != null) MainForm.Instance.EditInDesigner(e); };
                grid.StarClicked += it => { var e = App.State.Find(it.Id); if (e == null) return; e.Favorite = !e.Favorite; App.State.MarkLibraryChanged(); };
                grid.MenuClicked += (it, pt) => { var e = App.State.Find(it.Id); if (e != null) ShowEntryMenu(e, grid, pt); };
                grid.ItemRightClicked += (it, pt) => { var e = App.State.Find(it.Id); if (e != null) ShowEntryMenu(e, grid, pt); };

                App.State.LibraryChanged += () => { if (Visible) Rebuild(); else dirty = true; };
                App.ActiveCrosshairChanged += () => { if (Visible) Rebuild(); else dirty = true; };
                App.ProfileChanged += () => { if (Visible) Rebuild(); else dirty = true; };
            }

            protected override void OnVisibleChanged(EventArgs e)
            {
                base.OnVisibleChanged(e);
                if (Visible && dirty) Rebuild();
            }

            static bool IsAnimated(CrosshairEntry e) => e.Layers.OfType<Dictionary<string, object>>().Any(l =>
            {
                var fo = J.Obj(l, "firingOptions");
                if (fo == null) return false;
                bool hasStages = (J.List(fo, "stages")?.Count ?? 0) > 0;
                bool over = new[] { "line", "dot", "outline", "position", "layer" }.Any(k => J.Obj(fo, k)?.Any(kv => kv.Value != null) ?? false);
                return hasStages || over || J.Num(fo, "firingOffset") != 0 || J.Bool(fo, "tShapeWhenFiring");
            });

            static bool IsImported(CrosshairEntry e) => e.Source.StartsWith("cx:") || e.Source == "valorant" || e.Source == "cs2" || e.Source == "reticly" || e.Source == "json";

            void BuildCategories()
            {
                string sel = cats.SelectedKey ?? "all";
                cats.Items.Clear();
                var lib = App.State.Library;
                cats.Items.Add(new CategoryList.Item { Text = "Categories", Header = true });
                cats.Items.Add(new CategoryList.Item { Key = "all", Text = "All", Glyph = Glyph.Crosshair, Count = lib.Count.ToString() });
                cats.Items.Add(new CategoryList.Item { Key = "fav", Text = "Favorites", Glyph = Glyph.StarFilled, Count = lib.Count(e => e.Favorite).ToString() });
                cats.Items.Add(new CategoryList.Item { Key = "anim", Text = "Animated", Glyph = Glyph.Lightning, Count = lib.Count(IsAnimated).ToString() });
                cats.Items.Add(new CategoryList.Item { Key = "imp", Text = "Imported", Glyph = Glyph.Download, Count = lib.Count(IsImported).ToString() });
                foreach (var f in App.State.Folders)
                    cats.Items.Add(new CategoryList.Item { Key = "f:" + f, Text = f, Glyph = Glyph.Folder, Count = lib.Count(e => e.Folder == f).ToString() });
                cats.SelectedKey = sel;
                if (cats.SelectedKey == null) cats.SelectedKey = "all";
                cats.Width = catPanel.Width - Theme.S(36);
                cats.Refresh2();
            }

            public void Rebuild()
            {
                dirty = false;
                BuildCategories();
                IEnumerable<CrosshairEntry> q = App.State.Library;
                string term = search.Text.Trim();
                if (term.Length > 0) q = q.Where(e => e.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || e.Folder.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                string key = cats.SelectedKey ?? "all";
                if (key == "fav") q = q.Where(e => e.Favorite);
                else if (key == "anim") q = q.Where(IsAnimated);
                else if (key == "imp") q = q.Where(IsImported);
                else if (key.StartsWith("f:")) q = q.Where(e => e.Folder == key.Substring(2));
                switch (App.State.Settings.LibrarySort)
                {
                    case "lastUsed": q = q.OrderByDescending(e => e.LastUsed).ThenByDescending(e => e.Updated); break;
                    case "name": q = q.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase); break;
                    case "newest": q = q.OrderByDescending(e => e.Created); break;
                    default: q = q.OrderBy(e => e.Order); break;
                }
                var binds = App.Profile.CrosshairBinds;
                var list = q.ToList();
                grid.Items = list.Select(e => new TileItem
                {
                    Id = e.Id, Name = e.Name, Layers = e.Layers, CacheKey = e.Id + ":" + e.Updated.Ticks, Favorite = e.Favorite,
                    Active = e.Id == App.Profile.CrosshairId,
                    Badge = binds.FirstOrDefault(b => b.CrosshairId == e.Id && !string.IsNullOrEmpty(b.Key)) is CrosshairBind b ? KeyBinding.Display(b.Key) : null
                }).ToList();
                countLabel.Text = App.State.Library.Count + " crosshair" + (App.State.Library.Count == 1 ? "" : "s");
                scroll.PerformLayout();
            }

            public void Highlight(string id)
            {
                cats.SelectedKey = "all";
                search.Text = "";
                Rebuild();
                grid.Select(id);
                var r = grid.BoundsOf(id);
                if (!r.IsEmpty) scroll.AutoScrollPosition = new Point(0, Math.Max(0, r.Y - Theme.S(20)));
            }
        }

        // ---------------------------------------------------------------- Browse

        sealed class BrowseView : Panel
        {
            readonly SearchBox search = new SearchBox("Search designs (e.g. dot, recoil, chevron)…", true);
            readonly CategoryList cats = new CategoryList();
            readonly ScrollHost scroll = new ScrollHost { Dock = DockStyle.Fill };
            readonly CrosshairGrid grid = new CrosshairGrid { Style = TileStyle.Browse };
            readonly Panel previewBar = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(64), BackColor = Theme.Chrome, Visible = false };
            readonly DarkLabel previewLabel = new DarkLabel("", Theme.BodyMedium);
            readonly List<Preset> presets = Presets.All();
            Preset previewing;
            AppController App => AppController.I;

            static readonly string[] TopPicks = { "Classic Green", "Cross + Dot", "Valorant Style", "Dot Medium", "T With Dot", "Ring + Dot", "Neon", "Bloom on Fire", "Arrows In", "Recoil Plus", "Onetap Bars", "Hairline" };

            public BrowseView()
            {
                BackColor = Theme.Bg;
                var top = new Panel { Dock = DockStyle.Top, Height = Theme.S(76), BackColor = Theme.Chrome };
                top.Controls.Add(search);
                top.Layout += (s, e) => search.SetBounds(Theme.S(24), (top.Height - search.Height) / 2, top.Width - Theme.S(48), search.Height);
                top.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };

                var catPanel = new Panel { Dock = DockStyle.Left, Width = Theme.S(238), BackColor = Theme.Well };
                var catScroll = new ScrollHost { Dock = DockStyle.Fill, BackColor = Theme.Well };
                catScroll.Stack.BackColor = Theme.Well;
                catScroll.Stack.Inner = new Padding(Theme.S(18), Theme.S(6), Theme.S(18), Theme.S(18));
                catScroll.Stack.Controls.Add(cats);
                catPanel.Controls.Add(catScroll);
                catPanel.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, catPanel.Width - 1, 0, catPanel.Width - 1, catPanel.Height); };

                cats.Items.Add(new CategoryList.Item { Text = "Collections", Header = true });
                cats.Items.Add(new CategoryList.Item { Key = "top", Text = "Top Picks", Glyph = Glyph.Trophy });
                cats.Items.Add(new CategoryList.Item { Key = "All", Text = "All Designs", Glyph = Glyph.AllApps, Count = presets.Count.ToString() });
                string[] glyphs = { null, Glyph.Add, Glyph.Dot, Glyph.Circle, Glyph.Up, Glyph.Move, Glyph.Sparkle, Glyph.Lightning, Glyph.Recoil };
                for (int i = 1; i < Presets.Categories.Length; i++)
                {
                    string c = Presets.Categories[i];
                    cats.Items.Add(new CategoryList.Item { Key = c, Text = c == "Recoil" ? "Recoil Tracking" : c, Glyph = glyphs[Math.Min(i, glyphs.Length - 1)], Count = presets.Count(p => p.Category == c).ToString() });
                }
                cats.SelectedKey = "top";
                catPanel.Layout += (s, e) => { cats.Width = catPanel.Width - Theme.S(36); cats.Refresh2(); };

                var use = new FlatButton("Save & Use", Glyph.Check, ButtonKind.Primary);
                var save = new FlatButton("Save", Glyph.BookmarkFilled);
                var edit = new FlatButton("Customize", Glyph.Palette);
                var stop = new FlatButton("", Glyph.Close, ButtonKind.Ghost);
                weaponBtn.Click += (s, e) => ShowWeaponPicker();
                previewBar.Controls.AddRange(new Control[] { previewLabel, weaponBtn, use, save, edit, stop });
                previewBar.Layout += (s, e) =>
                {
                    int x = previewBar.Width - Theme.S(24);
                    foreach (var b in new Control[] { stop, use, save, edit, weaponBtn }) { if (!b.Visible) continue; x -= b.Width; b.Location = new Point(x, (previewBar.Height - b.Height) / 2); x -= Theme.S(10); }
                    previewLabel.SetBounds(Theme.S(28), 0, Math.Max(10, x - Theme.S(28)), previewBar.Height);
                };
                previewBar.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, previewBar.Width, 0); };
                use.Click += (s, e) => { var en = SavePreset(previewing); if (en != null) { App.ApplyCrosshair(en.Id); MainForm.Instance.ShowToast("Now using “" + en.Name + "”"); } StopPreview(); };
                save.Click += (s, e) => { var en = SavePreset(previewing); if (en != null) MainForm.Instance.ShowToast("Saved “" + en.Name + "”", Glyph.BookmarkFilled); Rebuild(); };
                edit.Click += (s, e) => { var p = previewing; var l = previewLayers; StopPreview(); if (p != null) MainForm.Instance.NewInDesigner(l ?? p.Layers, p.Name); };
                stop.Click += (s, e) => StopPreview();

                scroll.Stack.Inner = new Padding(Theme.S(28), Theme.S(28), Theme.S(28), Theme.S(28));
                scroll.Stack.Controls.Add(grid);
                Controls.Add(scroll);
                Controls.Add(previewBar);
                Controls.Add(catPanel);
                Controls.Add(top);

                search.Search += (s, e) => Rebuild();
                cats.Selected += it => Rebuild();
                grid.ItemClicked += it => StartPreview((Preset)it.Tag);
                grid.ItemDoubleClicked += it => { var en = SavePreset((Preset)it.Tag); if (en != null) App.ApplyCrosshair(en.Id); StopPreview(); };
                grid.BookmarkClicked += it =>
                {
                    var p = (Preset)it.Tag;
                    var existing = App.State.Library.FirstOrDefault(x => x.Source == "preset" && x.Name == p.Name);
                    if (existing != null) { App.State.Remove(existing.Id); MainForm.Instance.ShowToast("Removed from Saved", Glyph.Bookmark); }
                    else { SavePreset(p); MainForm.Instance.ShowToast("Saved “" + p.Name + "”", Glyph.BookmarkFilled); }
                    Rebuild();
                };
                grid.ItemRightClicked += (it, pt) =>
                {
                    var p = (Preset)it.Tag;
                    var m = Menus.Create();
                    m.Items.Item("Preview on screen", () => StartPreview(p));
                    m.Items.Item("Save & Use", () => { var en = SavePreset(p); if (en != null) App.ApplyCrosshair(en.Id); });
                    m.Items.Item("Save", () => { SavePreset(p); Rebuild(); });
                    m.Items.Item("Customize in Designer", () => MainForm.Instance.NewInDesigner(p.Layers, p.Name));
                    m.Show(grid, pt);
                };
                Rebuild();
            }

            CrosshairEntry SavePreset(Preset p)
            {
                if (p == null) return null;
                // recoil styles keep the weapon picked in the preview bar
                var layersToSave = p == previewing && previewLayers != null ? previewLayers : p.Layers;
                var w = Recoil.CurrentWeapon(layersToSave);
                string name = w != null ? p.Name + " (" + w.Name + ")" : p.Name;
                var existing = App.State.Library.FirstOrDefault(x => x.Source == "preset" && x.Name == name);
                if (existing != null) return existing;
                return App.State.Add(new CrosshairEntry { Name = name, Layers = (List<object>)J.DeepClone(layersToSave), Source = "preset" });
            }

            List<object> previewLayers;
            readonly FlatButton weaponBtn = new FlatButton("Weapon", Glyph.Recoil, ButtonKind.Secondary) { Visible = false };

            void UpdateWeaponButton()
            {
                var w = previewLayers == null ? null : Recoil.CurrentWeapon(previewLayers);
                weaponBtn.Visible = w != null;
                if (w != null) { weaponBtn.Text = w.Game + "  ·  " + w.Name; weaponBtn.AutoSizeWidth(); }
                previewBar.PerformLayout();
            }

            void ShowWeaponPicker()
            {
                if (previewLayers == null) return;
                var cur = Recoil.CurrentWeapon(previewLayers);
                var m = Menus.Create();
                foreach (var game in Recoil.Games)
                {
                    var gm = new ToolStripMenuItem(game) { ForeColor = Theme.Text, Checked = cur?.Game == game };
                    string lastCat = null;
                    foreach (var p in Recoil.ForGame(game))
                    {
                        if (lastCat != null && p.Category != lastCat) gm.DropDownItems.Add(new ToolStripSeparator());
                        lastCat = p.Category;
                        var pat = p;
                        gm.DropDownItems.Item(p.Label + (p.HandTuned ? "  ★" : ""), () =>
                        {
                            Recoil.SetWeapon(previewLayers, pat);
                            App.SetPreview(previewLayers);
                            UpdateWeaponButton();
                        }, true, cur?.Key == p.Key);
                    }
                    m.Items.Add(gm);
                }
                m.Show(weaponBtn, new Point(0, -Theme.S(4) - 4 * Theme.S(26)));
            }

            void StartPreview(Preset p)
            {
                previewing = p;
                previewLayers = (List<object>)J.DeepClone(p.Layers);
                App.SetPreview(previewLayers);
                UpdateWeaponButton();
                previewLabel.Text = "Previewing “" + p.Name + "” on your screen" + (p.Category == "Animated" || p.Category == "Recoil" ? "  —  hold fire / aim to see the animation" : "");
                previewBar.Visible = true;
                grid.Select("preset:" + p.Name);
            }

            public void StopPreview()
            {
                if (previewing == null) return;
                previewing = null;
                previewBar.Visible = false;
                App.SetPreview(null);
            }

            void Rebuild()
            {
                string key = cats.SelectedKey ?? "top";
                string term = search.Text.Trim();
                IEnumerable<Preset> q = presets;
                if (term.Length > 0) q = q.Where(p => p.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || p.Category.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                else if (key == "top") q = TopPicks.Select(n => presets.FirstOrDefault(p => p.Name == n)).Where(p => p != null);
                else if (key != "All") q = q.Where(p => p.Category == key);
                var savedNames = new HashSet<string>(App.State.Library.Where(x => x.Source == "preset").Select(x => x.Name));
                grid.Items = q.Select(p => new TileItem { Id = "preset:" + p.Name, Name = p.Name, Layers = p.Layers, CacheKey = "preset:" + p.Name, Tag = p, Saved = savedNames.Contains(p.Name) }).ToList();
                grid.EmptyText = "No designs match “" + term + "”.";
                scroll.PerformLayout();
            }
        }

        // ---------------------------------------------------------------- Discover

        sealed class DiscoverView : Panel
        {
            readonly CrosshairsPage owner;
            readonly ScrollHost scroll = new ScrollHost { Dock = DockStyle.Fill };
            AppController App => AppController.I;
            int jumpIndex;

            public DiscoverView(CrosshairsPage owner)
            {
                this.owner = owner;
                BackColor = Theme.Bg;
                scroll.Stack.Inner = new Padding(Theme.S(28), Theme.S(28), Theme.S(28), Theme.S(28));
                scroll.Stack.Spacing = Theme.S(24);
                Controls.Add(scroll);
            }

            public void Rebuild()
            {
                var st = scroll.Stack;
                st.SuspendLayout();
                foreach (Control c in st.Controls.Cast<Control>().ToList()) { st.Controls.Remove(c); c.Dispose(); }

                var presets = Presets.All();
                var lib = App.State.Library;
                var hero = new HeroPanel(new[]
                {
                    (Glyph.Crosshair, lib.Count.ToString(), "Saved crosshairs", Theme.Accent),
                    (Glyph.AllApps, presets.Count.ToString(), "Built-in designs", Color.FromArgb(150, 130, 255)),
                    (Glyph.People, App.State.Profiles.Count.ToString(), "Profiles", Color.FromArgb(70, 190, 120)),
                });
                st.Controls.Add(hero);

                var actions = new Columns(
                    QuickButton("Browse", Glyph.Search, true, () => owner.SelectTab(1)),
                    QuickButton("Designer", Glyph.Palette, false, () => MainForm.Instance.NewInDesigner()),
                    QuickButton("Share", Glyph.Upload, false, () => MainForm.Instance.ShowShare(App.ActiveCrosshair)),
                    QuickButton("Surprise Me", Glyph.Shuffle, false, () => MainForm.Instance.Navigate("randomizer")));
                st.Controls.Add(actions);

                // Today's pick + jump back in
                var pick = presets[(DateTime.Today.DayOfYear * 7 + DateTime.Today.Year) % presets.Count];
                var recent = lib.Where(e => e.LastUsed > DateTime.MinValue).OrderByDescending(e => e.LastUsed).Take(5).ToList();
                if (recent.Count == 0) recent = lib.OrderBy(e => e.Order).Take(5).ToList();
                var pickCard = new FeatureCard("Today's Pick", Glyph.StarFilled, pick.Name, pick.Layers, "pick:" + pick.Name);
                pickCard.AddAction(Glyph.Eye, "Preview", () => { App.SetPreview(pick.Layers); MainForm.Instance.ShowToast("Previewing — open Browse to keep it", Glyph.Eye); });
                pickCard.AddAction(Glyph.BookmarkFilled, "Save & Use", () =>
                {
                    App.SetPreview(null);
                    var en = lib.FirstOrDefault(x => x.Source == "preset" && x.Name == pick.Name) ?? App.State.Add(new CrosshairEntry { Name = pick.Name, Layers = (List<object>)J.DeepClone(pick.Layers), Source = "preset" });
                    App.ApplyCrosshair(en.Id);
                    MainForm.Instance.ShowToast("Now using “" + en.Name + "”");
                });
                FeatureCard jumpCard;
                if (recent.Count > 0)
                {
                    jumpIndex = Math.Min(jumpIndex, recent.Count - 1);
                    var cur = recent[jumpIndex];
                    jumpCard = new FeatureCard("Jump Back In", Glyph.Clock, cur.Name, cur.Layers, cur.Id + ":" + cur.Updated.Ticks);
                    if (recent.Count > 1)
                    {
                        jumpCard.AddAction(Glyph.Left, "", () => { jumpIndex = (jumpIndex + recent.Count - 1) % recent.Count; Rebuild(); });
                        jumpCard.AddAction(Glyph.Right, "", () => { jumpIndex = (jumpIndex + 1) % recent.Count; Rebuild(); });
                    }
                    jumpCard.AddAction(Glyph.Check, "Use", () => { App.ApplyCrosshair(cur.Id); MainForm.Instance.ShowToast("Now using “" + cur.Name + "”"); });
                }
                else jumpCard = new FeatureCard("Jump Back In", Glyph.Clock, "Nothing yet", new List<object>(), "empty");
                st.Controls.Add(new Columns(pickCard, jumpCard));

                st.Controls.Add(new CaptionLabel(Glyph.Lightning, "Featured animated & recoil designs"));
                var featured = new CrosshairGrid { Style = TileStyle.Browse };
                var savedNames = new HashSet<string>(lib.Where(x => x.Source == "preset").Select(x => x.Name));
                featured.Items = presets.Where(p => p.Category == "Animated" || p.Category == "Recoil").Take(8)
                    .Select(p => new TileItem { Id = "preset:" + p.Name, Name = p.Name, Layers = p.Layers, CacheKey = "preset:" + p.Name, Tag = p, Saved = savedNames.Contains(p.Name) }).ToList();
                featured.ItemClicked += it => { owner.SelectTab(1); };
                featured.BookmarkClicked += it =>
                {
                    var p = (Preset)it.Tag;
                    if (!App.State.Library.Any(x => x.Source == "preset" && x.Name == p.Name))
                        App.State.Add(new CrosshairEntry { Name = p.Name, Layers = (List<object>)J.DeepClone(p.Layers), Source = "preset" });
                    MainForm.Instance.ShowToast("Saved “" + p.Name + "”", Glyph.BookmarkFilled);
                    Rebuild();
                };
                st.Controls.Add(featured);
                BuildGallery(st);
                st.ResumeLayout(true);
            }

            /// <summary>Community gallery: designs shared through GitHub (gallery/gallery.json in the repo).</summary>
            void BuildGallery(StackPanel st)
            {
                st.Controls.Add(new CaptionLabel(Glyph.People, L.T("Community gallery")));
                var share = new FlatButton(L.T("Share yours"), Glyph.Upload, ButtonKind.Secondary);
                share.AutoSizeWidth();
                share.Click += (s, e) =>
                {
                    var cur = App.ActiveCrosshair;
                    if (cur == null) { MainForm.Instance.ShowToast(L.T("Select a crosshair to share first"), Glyph.Info); return; }
                    if (!DarkDialog.Confirm(MainForm.Instance, L.T("Share to the community gallery"),
                        string.Format(L.T("This opens a GitHub issue with the code for “{0}” filled in (you need a free GitHub account). Once it's reviewed, it appears here for everyone."), cur.Name), L.T("Open GitHub"))) return;
                    try { System.Diagnostics.Process.Start(Gallery.SubmitUrl(cur)); } catch { }
                };
                var refresh = new FlatButton("", Glyph.Refresh, ButtonKind.Ghost) { Width = Theme.S(38), Height = Theme.S(38) };
                refresh.Click += async (s, e) => { await Gallery.LoadAsync(force: true); if (!IsDisposed) Rebuild(); };
                st.Controls.Add(new HStack(share, refresh) { Height = Theme.S(38) });

                if (Gallery.Items == null)
                {
                    st.Controls.Add(new WrapLabel(L.T("Loading the gallery…"), Theme.Small, Theme.TextDim));
                    Gallery.LoadAsync().ContinueWith(_ => { try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Rebuild)); } catch { } });
                    return;
                }
                if (Gallery.Items.Count == 0)
                {
                    st.Controls.Add(new WrapLabel(Gallery.Error != null ? L.T("Couldn't load the gallery. Check your connection and press refresh.") : L.T("No community designs yet — be the first to share one!"), Theme.Small, Theme.TextDim));
                    return;
                }
                var lib = App.State.Library;
                var grid = new CrosshairGrid { Style = TileStyle.Browse };
                grid.Items = Gallery.Items.Select(g => new TileItem
                {
                    Id = "gallery:" + g.Id, Name = g.Name + (string.IsNullOrEmpty(g.Author) ? "" : "  ·  " + g.Author), Layers = g.Layers,
                    CacheKey = "gallery:" + g.Id, Tag = g, Saved = lib.Any(x => x.Source == "gallery:" + g.Id)
                }).ToList();
                grid.ItemClicked += it =>
                {
                    var g = (GalleryItem)it.Tag;
                    App.SetPreview(g.Layers);
                    MainForm.Instance.ShowToast(string.Format(L.T("Previewing “{0}” — bookmark it to keep it"), g.Name), Glyph.Eye);
                };
                grid.BookmarkClicked += it =>
                {
                    var g = (GalleryItem)it.Tag;
                    App.SetPreview(null);
                    var en = lib.FirstOrDefault(x => x.Source == "gallery:" + g.Id)
                        ?? App.State.Add(new CrosshairEntry { Name = g.Name, Layers = (List<object>)J.DeepClone(g.Layers), Source = "gallery:" + g.Id });
                    App.ApplyCrosshair(en.Id);
                    MainForm.Instance.ShowToast(string.Format(L.T("Saved and using “{0}”"), g.Name), Glyph.BookmarkFilled);
                    Rebuild();
                };
                st.Controls.Add(grid);
            }

            Control QuickButton(string text, string glyph, bool tinted, Action click)
            {
                var b = new FlatButton(text, glyph, tinted ? ButtonKind.Subtle : ButtonKind.Secondary) { Height = Theme.S(56), Radius = 10, Toggled = false };
                if (tinted) { b.Tint = Color.FromArgb(45, 45, 60); b.TintBorder = Color.FromArgb(78, 80, 140); b.TintText = Color.FromArgb(150, 165, 255); }
                b.Font = Theme.Nav;
                b.Click += (s, e) => click();
                return b;
            }

            /// <summary>Stats banner (mirrors the community banner layout with library stats).</summary>
            sealed class HeroPanel : Control
            {
                readonly (string glyph, string value, string label, Color color)[] stats;

                public HeroPanel((string, string, string, Color)[] s)
                {
                    stats = s;
                    Height = Theme.S(220);
                    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                }

                protected override void OnPaint(PaintEventArgs e)
                {
                    var g = e.Graphics;
                    g.Clear(Theme.Bg);
                    Theme.Smooth(g);
                    var r = new RectangleF(0, 0, Width, Height);
                    using (var path = Theme.Round(r, Theme.SF(14)))
                    using (var b = new LinearGradientBrush(r, Color.FromArgb(30, 30, 30), Color.FromArgb(22, 22, 26), 120f)) g.FillPath(b, path);
                    Theme.StrokeRound(g, Theme.Border, r, Theme.SF(14));
                    string title = "YOUR LIBRARY";
                    int tw = Theme.TextWidth(title, Theme.H3) + Theme.S(30);
                    int tx = (Width - tw) / 2;
                    Theme.DrawIcon(g, Glyph.Settings, Theme.Icon, Color.FromArgb(120, 140, 255), new Rectangle(tx, Theme.S(36), Theme.S(22), Theme.S(28)));
                    Theme.DrawText(g, title, Theme.H3, Theme.Text, new Rectangle(tx + Theme.S(30), Theme.S(36), tw, Theme.S(28)));
                    int cw = Theme.S(220), gap = Theme.S(14);
                    int total = stats.Length * cw + (stats.Length - 1) * gap;
                    int x = (Width - total) / 2;
                    foreach (var s in stats)
                    {
                        var c = new RectangleF(x, Theme.S(86), cw, Theme.S(92));
                        Theme.FillRound(g, Color.FromArgb(36, 36, 36), c, Theme.SF(10));
                        Theme.StrokeRound(g, Theme.Border, c, Theme.SF(10));
                        var tile = new RectangleF(c.X + Theme.S(18), c.Y + (c.Height - Theme.S(44)) / 2, Theme.S(44), Theme.S(44));
                        Theme.FillRound(g, Theme.Blend(Color.FromArgb(36, 36, 36), s.color, .22), tile, Theme.SF(8));
                        Theme.DrawIcon(g, s.glyph, Theme.Icon, s.color, Rectangle.Round(tile));
                        Theme.DrawText(g, s.value, Theme.H1, Theme.Text, new Rectangle((int)tile.Right + Theme.S(14), (int)c.Y + Theme.S(18), cw, Theme.S(30)));
                        Theme.DrawText(g, s.label, Theme.Body, Theme.TextDim, new Rectangle((int)tile.Right + Theme.S(14), (int)c.Y + Theme.S(50), cw, Theme.S(22)));
                        x += cw + gap;
                    }
                }
            }

            /// <summary>"Today's Pick" / "Jump Back In" card with a label chip, large preview, name and actions.</summary>
            sealed class FeatureCard : Panel
            {
                readonly string chip, chipGlyph, name, key;
                readonly List<object> layers;
                readonly HStack actions = new HStack { RightAlign = true };

                public FeatureCard(string chip, string chipGlyph, string name, List<object> layers, string key)
                {
                    this.chip = chip; this.chipGlyph = chipGlyph; this.name = name; this.layers = layers; this.key = key;
                    Height = Theme.S(250);
                    BackColor = Theme.Bg;
                    Controls.Add(actions);
                    SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                }

                public void AddAction(string glyph, string text, Action click)
                {
                    var b = new FlatButton(text, glyph, ButtonKind.Secondary) { Height = Theme.S(34) };
                    b.AutoSizeWidth();
                    b.Click += (s, e) => click();
                    actions.Controls.Add(b);
                }

                protected override void OnLayout(LayoutEventArgs levent)
                {
                    base.OnLayout(levent);
                    actions.SetBounds(Width / 2, Height - Theme.S(54), Width / 2 - Theme.S(18), Theme.S(36));
                }

                protected override void OnPaint(PaintEventArgs e)
                {
                    var g = e.Graphics;
                    g.Clear(BackColor);
                    Theme.Smooth(g);
                    var r = new RectangleF(0, 0, Width, Height);
                    Theme.FillRound(g, Theme.Surface, r, Theme.SF(12));
                    Theme.StrokeRound(g, Theme.Border, r, Theme.SF(12));
                    int cw = Theme.TextWidth(chip.ToUpperInvariant(), Theme.Caption) + Theme.S(40);
                    var chipR = new RectangleF(Theme.S(16), Theme.S(16), cw, Theme.S(30));
                    Theme.FillRound(g, Theme.Chrome, chipR, Theme.SF(6));
                    Theme.DrawIcon(g, chipGlyph, Theme.IconSmall, Theme.Accent, new Rectangle((int)chipR.X + Theme.S(8), (int)chipR.Y, Theme.S(18), (int)chipR.Height));
                    Theme.DrawText(g, chip.ToUpperInvariant(), Theme.Caption, Theme.Text, new Rectangle((int)chipR.X + Theme.S(30), (int)chipR.Y, cw, (int)chipR.Height));
                    var art = new Rectangle(Theme.S(16), Theme.S(54), Width - Theme.S(32), Height - Theme.S(124));
                    if (layers.Count > 0)
                    {
                        var bmp = Thumbnails.Actual(key, layers, art.Width, art.Height);
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImageUnscaled(bmp, art.X, art.Y);
                        Theme.Smooth(g);
                    }
                    Theme.DrawText(g, name, Theme.H2, Theme.Text, new Rectangle(Theme.S(20), Height - Theme.S(56), Width / 2, Theme.S(40)));
                }
            }
        }
    }

    public sealed class KeyAssignDialog : DarkDialog
    {
        readonly KeybindBox box = new KeybindBox { Width = Theme.S(240) };
        public string Binding => box.Binding;

        public KeyAssignDialog(string title, string current) : base(title, 440)
        {
            box.Binding = current;
            Content.Controls.Add(new WrapLabel("Click the box and press a key or mouse button. The binding belongs to the current profile (" + AppController.I.Profile.Name + ").", Theme.Small, Theme.TextDim));
            Content.Controls.Add(new Row("Key", box, null, Theme.S(240)));
            AddButton("Cancel", ButtonKind.Ghost, () => { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Clear", ButtonKind.Secondary, () => { box.Binding = ""; });
            AddButton("Save", ButtonKind.Primary, () => { DialogResult = DialogResult.OK; Close(); });
        }
    }
}
