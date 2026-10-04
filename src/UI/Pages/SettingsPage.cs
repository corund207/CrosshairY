using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Reticly.Core;
using Reticly.UI.Controls;
using Reticly.UI.Dialogs;

namespace Reticly.UI.Pages
{
    /// <summary>Settings: General / Recording &amp; Streaming tabs, a categories column and icon-tile setting rows.</summary>
    public sealed class SettingsPage : Page
    {
        readonly TabBar tabs = new TabBar { Dock = DockStyle.Top };
        readonly CategoryList cats = new CategoryList();
        readonly ScrollHost scroll = new ScrollHost { Dock = DockStyle.Fill };
        bool selfEdit;
        Stepper sizeStep, xStep, yStep;

        static readonly (string key, string text, string glyph)[] Categories =
        {
            ("all", "All", Glyph.AllApps), ("interface", "Interface", Glyph.Display), ("display", "Display", Glyph.Monitor),
            ("position", "Position & Size", Glyph.Move), ("startup", "Startup", Glyph.Rocket), ("performance", "Performance", Glyph.Lightning),
            ("input", "Input", Glyph.Gamepad), ("reactions", "Hit Markers", Glyph.Target), ("profiles", "Profiles", Glyph.People),
            ("updates", "Updates", Glyph.Refresh), ("actions", "Backup & Data", Glyph.Wrench),
        };

        public override string Title => L.T("Settings");

        public SettingsPage()
        {
            tabs.Tabs.Add(new TabBar.Tab { Text = L.T("General"), Glyph = Glyph.Settings });
            tabs.Tabs.Add(new TabBar.Tab { Text = L.T("Recording / Streaming"), Glyph = Glyph.Video });
            var catPanel = new Panel { Dock = DockStyle.Left, Width = Theme.S(250), BackColor = Theme.Bg };
            var catScroll = new ScrollHost { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            catScroll.Stack.BackColor = Theme.Bg;
            catScroll.Stack.Inner = new Padding(Theme.S(20), Theme.S(14), Theme.S(16), Theme.S(16));
            catScroll.Stack.Controls.Add(cats);
            catPanel.Controls.Add(catScroll);
            catPanel.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, catPanel.Width - 1, 0, catPanel.Width - 1, catPanel.Height); };
            cats.BackColor = Theme.Bg;
            cats.Items.Add(new CategoryList.Item { Text = L.T("Categories"), Header = true });
            foreach (var c in Categories) cats.Items.Add(new CategoryList.Item { Key = c.key, Text = L.T(c.text), Glyph = c.glyph });
            cats.SelectedKey = "all";
            catPanel.Layout += (s, e) => { cats.Width = catPanel.Width - Theme.S(36); cats.Refresh2(); };
            scroll.Stack.Inner = new Padding(Theme.S(34), Theme.S(18), Theme.S(40), Theme.S(40));
            scroll.Stack.Spacing = Theme.S(10);
            Controls.Add(scroll);
            Controls.Add(catPanel);
            Controls.Add(tabs);
            tabs.SelectedChanged += i => { catPanel.Visible = i == 0; Build(); };
            cats.Selected += it => Build();
            App.ProfileChanged += () =>
            {
                if (!Visible || selfEdit) return;
                if (sizeStep != null && !sizeStep.IsDisposed) { sizeStep.Value = App.Profile.Scale; xStep.Value = App.Profile.OffsetX; yStep.Value = App.Profile.OffsetY; }
            };
        }

        public void ShowCategory(string key) { tabs.SetSilently(0); cats.SelectedKey = key; Build(); }

        public override void OnActivated() => Build();

        ToggleSwitch Toggle(bool value, Action<bool> set)
        {
            var t = new ToggleSwitch();
            t.SetSilently(value);
            t.CheckedChanged += (s, e) => { set(t.Checked); App.State.MarkSettingsChanged(); };
            return t;
        }

        void Caption(string glyph, string text)
        {
            scroll.Stack.Controls.Add(new Panel { Height = Theme.S(8), BackColor = Theme.Bg });
            scroll.Stack.Controls.Add(new CaptionLabel(glyph, L.T(text)));
        }

        void EditedProfile()
        {
            selfEdit = true;
            try { App.State.MarkProfilesChanged(); App.UpdateOverlay(); } finally { selfEdit = false; }
        }

        void Build()
        {
            var st = scroll.Stack;
            st.SuspendLayout();
            foreach (Control c in st.Controls.Cast<Control>().ToList()) { st.Controls.Remove(c); c.Dispose(); }
            if (tabs.SelectedIndex == 1) BuildStreaming();
            else
            {
                string k = cats.SelectedKey ?? "all";
                bool all = k == "all";
                if (all || k == "interface") BuildInterface();
                if (all || k == "display") BuildDisplay();
                if (all || k == "position") BuildPosition();
                if (all || k == "startup") BuildStartup();
                if (all || k == "performance") BuildPerformance();
                if (all || k == "input") BuildInput();
                if (all || k == "reactions") BuildReactions();
                if (all || k == "profiles") BuildProfiles();
                if (all || k == "updates") BuildUpdates();
                if (all || k == "actions") BuildActions();
            }
            st.ResumeLayout(true);
            scroll.AutoScrollPosition = Point.Empty;
        }

        void BuildInterface()
        {
            var s = App.State.Settings;
            Caption(Glyph.Display, "Interface");
            var card = new SettingsCard();
            var close = new Dropdown { Width = Theme.S(220) };
            close.Items.AddRange(new object[] { L.T("Minimize to tray"), L.T("Exit Reticly") });
            close.SelectedIndex = s.CloseToTray ? 0 : 1;
            close.SelectedIndexChanged += (o, e) => { s.CloseToTray = close.SelectedIndex == 0; App.State.MarkSettingsChanged(); };
            card.Add(new SettingRow(Glyph.ChromeClose, "Close Button", "What happens when you close the window", close));
            card.Add(new SettingRow(Glyph.News, "Notifications", "Show a notification when toggling or switching crosshairs and profiles", Toggle(s.ShowTrayNotifications, v => s.ShowTrayNotifications = v)));
            card.Add(new SettingRow(Glyph.Hamburger, "Compact Sidebar", "Show only icons in the navigation sidebar", Toggle(s.SidebarCollapsed, v => { s.SidebarCollapsed = v; MainForm.Instance.ApplySidebarWidth(); })));
            var langs = L.Languages;
            var lang = new Dropdown { Width = Theme.S(220) };
            foreach (var l in langs) lang.Items.Add(l.Name);
            lang.SelectedIndex = Math.Max(0, Array.FindIndex(langs, l => l.Code == s.Language));
            lang.SelectedIndexChanged += (o, e) =>
            {
                string code = langs[lang.SelectedIndex].Code;
                if (code == s.Language) return;
                s.Language = code;
                App.State.MarkSettingsChanged();
                L.Use(code);
                if (DarkDialog.Confirm(Main, L.T("Language"), L.T("Restart Reticly now to switch the language?"), L.T("Restart")))
                {
                    App.State.SaveNow();
                    try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--updated") { UseShellExecute = false }); } catch { }
                    MainForm.Instance.ExitApp();
                }
            };
            card.Add(new SettingRow(Glyph.Globe, "Language", "Translations are community-made; anything untranslated stays in English", lang));
            var tour = new FlatButton(L.T("Take the tour"), Glyph.Compass, ButtonKind.Secondary);
            tour.AutoSizeWidth();
            tour.Click += (o, e) => Tour.Start(MainForm.Instance);
            card.Add(new SettingRow(Glyph.Compass, "Tour", "A quick walk through the main parts of Reticly", tour));
            scroll.Stack.Controls.Add(card);
        }

        void BuildDisplay()
        {
            var s = App.State.Settings;
            Caption(Glyph.Monitor, "Display");
            var card = new SettingsCard();
            var mode = new Dropdown { Width = Theme.S(250) };
            mode.Items.AddRange(new object[] { "Simple Overlay Mode", "Fullscreen Assist Mode" });
            mode.SelectedIndex = s.DisplayMode == "assist" ? 1 : 0;
            mode.SelectedIndexChanged += (o, e) => { s.DisplayMode = mode.SelectedIndex == 1 ? "assist" : "overlay"; App.State.MarkSettingsChanged(); };
            var info = new FlatButton("", Glyph.Info, ButtonKind.Secondary) { Width = Theme.S(38), Height = Theme.S(38) };
            info.Click += (o, e) => { using (var d = new DisplayModeDialog()) d.ShowDialog(Main); };
            var modeRow = new HStack(mode, info) { Width = Theme.S(250 + 8 + 38), Height = Theme.S(38) };
            card.Add(new SettingRow(Glyph.Monitor, "Display Mode", "Select how your crosshair is displayed in-game", modeRow));
            var screens = Screen.AllScreens;
            var monitor = new Dropdown { Width = Theme.S(250) };
            foreach (var sc in screens) monitor.Items.Add((sc.Primary ? "Primary  ·  " : "") + sc.Bounds.Width + "×" + sc.Bounds.Height + "  (" + sc.DeviceName.Replace("\\\\.\\", "") + ")");
            monitor.SelectedIndex = Math.Max(0, Array.FindIndex(screens, sc => sc.DeviceName == App.TargetScreen.DeviceName));
            monitor.SelectedIndexChanged += (o, e) => { var sc = screens[monitor.SelectedIndex]; s.Monitor = sc.Primary ? "" : sc.DeviceName; App.State.MarkSettingsChanged(); App.UpdateOverlay(); };
            card.Add(new SettingRow(null, "Select Display", "Choose which monitor shows your crosshair", monitor, 0, true));
            card.Add(new SettingRow(Glyph.Game, "Show Only In-Game", "Only display the crosshair while a game is focused", Toggle(s.OnlyShowInGame, v => { s.OnlyShowInGame = v; App.UpdateOverlay(); })));
            var games = new FlatButton(s.GameProcesses.Count + " game" + (s.GameProcesses.Count == 1 ? "" : "s") + "…", Glyph.Edit, ButtonKind.Secondary);
            games.AutoSizeWidth();
            games.Click += (o, e) => { using (var d = new GameListDialog()) d.ShowDialog(Main); Build(); };
            card.Add(new SettingRow(null, "Games", "Apps linked to profiles count as games too", games, 0, true));
            card.Add(new SettingRow(Glyph.Resize, "Ignore Display Scaling", "Keep the crosshair the same pixel size at 125% / 150% / 200% Windows scaling", Toggle(s.IgnoreDpiScaling, v => s.IgnoreDpiScaling = v)));
            var borderless = new FlatButton("Choose window…", Glyph.Display, ButtonKind.Secondary);
            borderless.AutoSizeWidth();
            borderless.Click += (o, e) => ShowBorderlessMenu(borderless);
            card.Add(new SettingRow(Glyph.Fit, "Force Borderless Fullscreen", "Stretch a windowed game over the whole display so the crosshair can sit on top", borderless));
            scroll.Stack.Controls.Add(card);
        }

        void ShowBorderlessMenu(Control anchor)
        {
            var m = Menus.Create();
            int count = 0;
            foreach (var p in Process.GetProcesses().OrderBy(x => x.ProcessName))
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(p.MainWindowTitle) || p.Id == Process.GetCurrentProcess().Id) continue;
                    var h = p.MainWindowHandle;
                    string title = p.MainWindowTitle.Length > 44 ? p.MainWindowTitle.Substring(0, 44) + "…" : p.MainWindowTitle;
                    bool on = App.IsBorderless(h);
                    m.Items.Item((on ? "Restore  " : "") + title + "   (" + p.ProcessName + ")", () =>
                    {
                        if (App.IsBorderless(h)) { App.RestoreWindow(h); MainForm.Instance.ShowToast("Window restored", Glyph.Display); }
                        else if (App.MakeBorderless(h)) MainForm.Instance.ShowToast("Made borderless: " + title, Glyph.Fit);
                    }, true, on);
                    count++;
                }
                catch { }
                finally { p.Dispose(); }
            }
            if (count == 0) m.Items.Item("No windows found", null, false);
            m.Show(anchor, new Point(0, anchor.Height + 2));
        }

        void BuildPosition()
        {
            var p = App.Profile;
            Caption(Glyph.Move, "Position & Size");
            var card = new SettingsCard();
            sizeStep = new Stepper(Glyph.Minimize, Glyph.Add, 0.25, 2, 0.25, 8, 1) { Value = p.Scale };
            sizeStep.ValueChanged += (o, e) => { p.Scale = sizeStep.Value; EditedProfile(); };
            card.Add(new SettingRow(Glyph.Resize, "Size", "Adjust the overall size of your crosshair", sizeStep));
            xStep = new Stepper(Glyph.ArrowLeft, Glyph.ArrowRight, 1, 0, -5000, 5000, 0) { Value = p.OffsetX };
            xStep.ValueChanged += (o, e) => { p.OffsetX = (int)xStep.Value; EditedProfile(); };
            card.Add(new SettingRow(Glyph.Swap, "Position X", "Horizontal offset from center", xStep));
            yStep = new Stepper(Glyph.ArrowUp, Glyph.ArrowDown, 1, 0, -5000, 5000, 0) { Value = p.OffsetY };
            yStep.ValueChanged += (o, e) => { p.OffsetY = (int)yStep.Value; EditedProfile(); };
            card.Add(new SettingRow(Glyph.Resize, "Position Y", "Vertical offset from center (negative moves up)", yStep));
            var keys = new Dropdown { Width = Theme.S(200) };
            keys.Items.AddRange(new object[] { "Off", "Arrow Keys", "Numpad 8/4/6/2" });
            keys.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "none", "arrows", "numpad" }, p.PositionKeys));
            keys.SelectedIndexChanged += (o, e) => { p.PositionKeys = new[] { "none", "arrows", "numpad" }[keys.SelectedIndex]; EditedProfile(); };
            card.Add(new SettingRow(Glyph.Keyboard, "Position Keybinds", "Hold Alt + Shift and press the keys to nudge the crosshair 1 px", keys));
            var op = new SliderInput(0, 1, 0.01, 2) { Value = p.Opacity, Width = Theme.S(260) };
            op.ValueChanged += (o, e) => { p.Opacity = op.Value; EditedProfile(); };
            card.Add(new SettingRow(Glyph.Eye, "Opacity", "Fade the whole crosshair", op, Theme.S(260)));

            var saved = new Dropdown { Width = Theme.S(200) };
            foreach (var sp in p.Positions) saved.Items.Add(sp.Name + "  (" + sp.X + ", " + sp.Y + ")");
            if (p.Positions.Count == 0) { saved.Items.Add("No saved positions"); saved.Enabled = false; }
            saved.SelectedIndex = 0;
            var applyPos = new FlatButton("", Glyph.Check, ButtonKind.Secondary) { Width = Theme.S(38), Height = Theme.S(38), Enabled = p.Positions.Count > 0 };
            applyPos.Click += (o, e) =>
            {
                var sp = p.Positions[saved.SelectedIndex];
                p.OffsetX = sp.X; p.OffsetY = sp.Y; EditedProfile();
                xStep.Value = sp.X; yStep.Value = sp.Y;
                MainForm.Instance.ShowToast("Moved to “" + sp.Name + "”", Glyph.Move);
            };
            var delPos = new FlatButton("", Glyph.Delete, ButtonKind.Secondary) { Width = Theme.S(38), Height = Theme.S(38), Enabled = p.Positions.Count > 0 };
            delPos.Click += (o, e) => { p.Positions.RemoveAt(saved.SelectedIndex); EditedProfile(); Build(); };
            card.Add(new SettingRow(Glyph.BookmarkFilled, "Saved Positions", p.Positions.Count == 0 ? "No saved positions yet" : p.Positions.Count + " saved", new HStack(saved, applyPos, delPos) { Width = Theme.S(200 + 38 * 2 + 16), Height = Theme.S(38) }));
            var nameBox = new TextField("", "Position name") { Width = Theme.S(200) };
            var savePos = new FlatButton("", Glyph.BookmarkFilled, ButtonKind.Secondary) { Width = Theme.S(38), Height = Theme.S(38) };
            Action save = () =>
            {
                string n = string.IsNullOrWhiteSpace(nameBox.Text) ? "Position " + (p.Positions.Count + 1) : nameBox.Text.Trim();
                p.Positions.Add(new SavedPosition { Name = n, X = p.OffsetX, Y = p.OffsetY });
                EditedProfile();
                MainForm.Instance.ShowToast("Saved position “" + n + "”", Glyph.BookmarkFilled);
                Build();
            };
            savePos.Click += (o, e) => save();
            nameBox.Box.KeyDown += (o, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; save(); } };
            card.Add(new SettingRow(null, "Save Position", "Save the current position with a name", new HStack(nameBox, savePos) { Width = Theme.S(200 + 38 + 8), Height = Theme.S(38) }, 0, true));
            scroll.Stack.Controls.Add(card);
        }

        void BuildStartup()
        {
            var s = App.State.Settings;
            Caption(Glyph.Rocket, "Startup");
            var card = new SettingsCard();
            card.Add(new SettingRow(Glyph.Rocket, "Launch on Startup", "Start Reticly when you sign in to Windows", Toggle(s.LaunchOnStartup, v => { s.LaunchOnStartup = v; AppController.SetLaunchOnStartup(v); })));
            card.Add(new SettingRow(Glyph.Minimize, "Start Minimized", "Open straight to the system tray", Toggle(s.StartMinimized, v => s.StartMinimized = v)));
            card.Add(new SettingRow(Glyph.Eye, "Crosshair Visible on Launch", "Otherwise it starts hidden until you press Global Toggle", Toggle(s.VisibleOnLaunch, v => s.VisibleOnLaunch = v)));
            scroll.Stack.Controls.Add(card);
        }

        void BuildPerformance()
        {
            var s = App.State.Settings;
            Caption(Glyph.Lightning, "Performance");
            var card = new SettingsCard();
            var fps = new Dropdown { Width = Theme.S(200) };
            int[] rates = { 60, 120, 144, 165, 240 };
            foreach (var r in rates) fps.Items.Add(r + " FPS");
            fps.SelectedIndex = Math.Max(0, Array.IndexOf(rates, s.FrameRate));
            if (Array.IndexOf(rates, s.FrameRate) < 0) fps.SelectedIndex = 2;
            fps.SelectedIndexChanged += (o, e) => { s.FrameRate = rates[fps.SelectedIndex]; App.State.MarkSettingsChanged(); };
            card.Add(new SettingRow(Glyph.Lightning, "Animation Frame Rate", "How smoothly animated crosshairs redraw. Static crosshairs use no CPU at all", fps));
            scroll.Stack.Controls.Add(card);
        }

        void BuildInput()
        {
            var s = App.State.Settings;
            Caption(Glyph.Gamepad, "Input");
            var card = new SettingsCard();
            card.Add(new SettingRow(Glyph.Gamepad, "Controller Support", "Use Xbox / XInput controllers for aim, fire and keybinds", Toggle(s.ControllerSupport, v => s.ControllerSupport = v)));
            var kb = new FlatButton("Open Keybinds", Glyph.Keyboard, ButtonKind.Secondary);
            kb.AutoSizeWidth();
            kb.Click += (o, e) => MainForm.Instance.Navigate("keybinds");
            card.Add(new SettingRow(Glyph.Keyboard, "Keybinds", "Global toggle, aim and fire keys, crosshair shortcuts", kb));
            scroll.Stack.Controls.Add(card);
        }

        void BuildReactions()
        {
            var s = App.State.Settings;
            Caption(Glyph.Target, "Hit Markers");
            var card = new SettingsCard();
            card.Add(new SettingRow(Glyph.Fire, "Hit Marker on Every Shot", "Flash a hit marker each time you press Fire. Or bind Hit Marker / Kill Flash keys on the Keybinds page", Toggle(s.HitOnFire, v => s.HitOnFire = v)));
            var style = new Dropdown { Width = Theme.S(220) };
            foreach (var n in Reactions.StyleNames) style.Items.Add(L.T(n));
            style.SelectedIndex = Math.Max(0, Array.IndexOf(Reactions.Styles, s.HitStyle));
            style.SelectedIndexChanged += (o, e) => { s.HitStyle = Reactions.Styles[style.SelectedIndex]; App.State.MarkSettingsChanged(); App.React(false); };
            card.Add(new SettingRow(Glyph.Shape, "Style", "How the reaction looks around your crosshair", style));
            var hit = new ColorButton { Color = ColorUtil.Parse(s.HitColor, Color.White) };
            hit.ColorCommitted += (o, e) => { s.HitColor = hit.Hex; App.State.MarkSettingsChanged(); App.React(false); };
            card.Add(new SettingRow(Glyph.Brush, "Hit Color", "Color of the hit marker", hit));
            var kill = new ColorButton { Color = ColorUtil.Parse(s.KillColor, Color.Red) };
            kill.ColorCommitted += (o, e) => { s.KillColor = kill.Hex; App.State.MarkSettingsChanged(); App.React(true); };
            card.Add(new SettingRow(Glyph.Brush, "Kill Color", "Color of the kill flash (bigger, with a ring)", kill));
            var size = new Stepper(Glyph.Minimize, Glyph.Add, 0.25, 2, 0.5, 3, 1) { Value = s.ReactionSize };
            size.ValueChanged += (o, e) => { s.ReactionSize = size.Value; App.State.MarkSettingsChanged(); App.React(false); };
            card.Add(new SettingRow(Glyph.Resize, "Size", "Scale of the hit marker and kill flash", size));
            var dur = new Dropdown { Width = Theme.S(200) };
            int[] durations = { 120, 180, 220, 300, 400, 600 };
            foreach (var d in durations) dur.Items.Add(d + " ms");
            dur.SelectedIndex = Math.Max(0, Array.IndexOf(durations, s.ReactionMs));
            if (Array.IndexOf(durations, s.ReactionMs) < 0) dur.SelectedIndex = 2;
            dur.SelectedIndexChanged += (o, e) => { s.ReactionMs = durations[dur.SelectedIndex]; App.State.MarkSettingsChanged(); App.React(false); };
            card.Add(new SettingRow(Glyph.Clock, "Duration", "How long the hit marker stays on screen (the kill flash lasts a bit longer)", dur));
            var testHit = new FlatButton(L.T("Hit"), Glyph.Target, ButtonKind.Secondary);
            testHit.AutoSizeWidth();
            testHit.Click += (o, e) => App.React(false);
            var testKill = new FlatButton(L.T("Kill"), Glyph.Fire, ButtonKind.Secondary);
            testKill.AutoSizeWidth();
            testKill.Click += (o, e) => App.React(true);
            card.Add(new SettingRow(Glyph.Eye, "Preview", "Plays on your on-screen crosshair", new HStack(testHit, testKill) { Width = testHit.Width + testKill.Width + Theme.S(8), Height = Theme.S(38) }));
            scroll.Stack.Controls.Add(card);
            scroll.Stack.Controls.Add(new WrapLabel(L.T("An overlay can't see what happens in the game, so reactions play when you press their keys — not on real hits."), Theme.Small, Theme.TextDim));
        }

        void BuildUpdates()
        {
            var s = App.State.Settings;
            Caption(Glyph.Refresh, "Updates");
            var card = new SettingsCard();
            card.Add(new SettingRow(Glyph.Refresh, "Check for Updates Automatically", "Look for a new version on GitHub about once a day", Toggle(s.AutoUpdate, v => s.AutoUpdate = v)));
            var check = new FlatButton(L.T("Check now"), Glyph.Refresh, ButtonKind.Secondary);
            check.AutoSizeWidth();
            check.Click += (o, e) => MainForm.Instance.CheckForUpdates(true);
            string last = DateTime.TryParse(s.LastUpdateCheck, null, System.Globalization.DateTimeStyles.RoundtripKind, out var when)
                ? L.T("Last checked") + " " + when.ToLocalTime().ToString("g") : L.T("Not checked yet");
            card.Add(new SettingRow(Glyph.Info, L.T("Version") + " " + Program.Version, last, check));
            var notes = new FlatButton(L.T("Release notes"), Glyph.News, ButtonKind.Secondary);
            notes.AutoSizeWidth();
            notes.Click += (o, e) => { try { Process.Start(Updater.ReleasesPage); } catch { } };
            card.Add(new SettingRow(Glyph.News, "What's New", "See every release on GitHub", notes));
            scroll.Stack.Controls.Add(card);
        }

        void BuildProfiles()
        {
            var s = App.State.Settings;
            Caption(Glyph.People, "Profiles");
            var card = new SettingsCard();
            card.Add(new SettingRow(Glyph.People, "Profile Detection", "Switch profiles automatically when a linked game is focused", Toggle(s.ProfileDetection, v => s.ProfileDetection = v)));
            var manage = new FlatButton("Manage profiles", Glyph.People, ButtonKind.Secondary);
            manage.AutoSizeWidth();
            manage.Click += (o, e) => MainForm.Instance.Navigate("profiles");
            card.Add(new SettingRow(Glyph.Person, "Profiles", App.State.Profiles.Count + " profile" + (App.State.Profiles.Count == 1 ? "" : "s") + "  ·  active: " + App.Profile.Name, manage));
            scroll.Stack.Controls.Add(card);
        }

        void BuildActions()
        {
            Caption(Glyph.Wrench, "Backup & Data");
            var card = new SettingsCard();
            var export = new FlatButton(L.T("Back up…"), Glyph.Upload, ButtonKind.Secondary);
            export.AutoSizeWidth();
            export.Click += (o, e) =>
            {
                using (var d = new SaveFileDialog { Filter = L.T("Reticly backup") + "|*.reticly;*.json", FileName = "Reticly-backup-" + DateTime.Now.ToString("yyyy-MM-dd") + ".reticly" })
                    if (d.ShowDialog(Main) == DialogResult.OK)
                    {
                        try { File.WriteAllText(d.FileName, App.State.ExportAll()); MainForm.Instance.ShowToast("Backup saved", Glyph.Upload); }
                        catch (Exception ex) { DarkDialog.Info(Main, "Backup failed", ex.Message); }
                    }
            };
            card.Add(new SettingRow(Glyph.Upload, "Back Up", "Save crosshairs, profiles, keybinds, loadouts, settings and custom patterns to one file", export));
            var import = new FlatButton(L.T("Restore…"), Glyph.Download, ButtonKind.Secondary);
            import.AutoSizeWidth();
            import.Click += (o, e) =>
            {
                using (var d = new OpenFileDialog { Filter = L.T("Reticly backup or crosshair JSON") + "|*.reticly;*.crosshairy;*.json|" + L.T("All files") + "|*.*" })
                {
                    if (d.ShowDialog(Main) != DialogResult.OK) return;
                    try
                    {
                        string json = File.ReadAllText(d.FileName);
                        var root = Json.Parse(json);
                        if (J.List(root, "crosshairs") != null || J.List(root, "profiles") != null)
                        {
                            bool replace = J.Str(root, "kind") != "profile" && J.Obj(root, "settings") != null
                                && DarkDialog.Confirm(Main, L.T("Restore backup"), L.T("Replace everything with this backup? Choose Cancel to merge it into what you have instead."), L.T("Replace everything"), true);
                            if (replace)
                            {
                                App.State.RestoreAll(json);
                                App.ActivateProfile(App.State.ActiveProfile.Id);
                                App.PushCrosshair();
                                App.UpdateOverlay();
                                MainForm.Instance.ShowToast(L.T("Backup restored"), Glyph.Download);
                            }
                            else MainForm.Instance.ShowToast(string.Format(L.T("{0} crosshairs added"), App.State.ImportAll(json)), Glyph.Download);
                            Build();
                        }
                        else
                        {
                            var layers = Import.CodeImporter.ParseLayers(root);
                            App.State.Add(new CrosshairEntry { Name = J.Str(root, "name") ?? Path.GetFileNameWithoutExtension(d.FileName), Layers = layers, Source = "json" });
                            MainForm.Instance.ShowToast("Crosshair imported", Glyph.Download);
                        }
                    }
                    catch (Exception ex) { DarkDialog.Info(Main, "Couldn't import", ex.Message); }
                }
            };
            card.Add(new SettingRow(Glyph.Download, "Restore", "Restore a backup (replace or merge), an exported profile, or add a crosshair JSON file", import));
            var open = new FlatButton("Open folder", Glyph.Folder, ButtonKind.Secondary);
            open.AutoSizeWidth();
            open.Click += (o, e) => { try { Process.Start("explorer.exe", "\"" + AppState.DataDir + "\""); } catch { } };
            card.Add(new SettingRow(Glyph.Folder, "Data Folder", AppState.DataDir, open));
            var reset = new FlatButton("Reset…", Glyph.Reset, ButtonKind.Danger);
            reset.AutoSizeWidth();
            reset.Click += (o, e) =>
            {
                if (!DarkDialog.Confirm(Main, "Reset Reticly", "Delete all crosshairs, profiles and settings and start fresh? Back up first if you want to keep anything.", "Reset everything", true)) return;
                App.State.Library.Clear();
                App.State.Folders.Clear();
                App.State.Profiles.Clear();
                App.State.Profiles.Add(new Profile());
                App.State.Settings = new Settings();
                Program.SeedLibrary(App.State);
                App.State.SaveAllNow();
                App.State.MarkLibraryChanged();
                App.State.MarkSettingsChanged();
                App.ActivateProfile(App.State.Profiles[0].Id);
                App.PushCrosshair();
                Build();
            };
            card.Add(new SettingRow(Glyph.Reset, "Factory Reset", "Remove everything and restore the defaults", reset));
            scroll.Stack.Controls.Add(card);
        }

        void BuildStreaming()
        {
            var s = App.State.Settings;
            Caption(Glyph.Video, "Recording / Streaming");
            var card = new SettingsCard();
            card.Add(new SettingRow(Glyph.Video, "Show Crosshair in Recordings", "Turn off to hide the crosshair from OBS, Discord, Game Bar captures and screenshots (Windows 10 2004+)", Toggle(s.ShowInCapture, v => s.ShowInCapture = v)));
            scroll.Stack.Controls.Add(card);
            scroll.Stack.Controls.Add(new WrapLabel("When hidden from capture, you still see the crosshair on your monitor while viewers don't. Some capture tools using “game capture” never see overlays regardless of this setting.", Theme.Small, Theme.TextDim));
        }
    }

    /// <summary>Display mode comparison (what each mode covers).</summary>
    public sealed class DisplayModeDialog : DarkDialog
    {
        public DisplayModeDialog() : base("Display Mode Comparison", 560)
        {
            Content.Controls.Add(new CompareTable());
            Content.Controls.Add(new WrapLabel("* Works in “fullscreen” games that use Windows fullscreen optimizations (most DX9 / DX12 titles, and DX11 games running flip-model). True exclusive fullscreen cannot be drawn over by any overlay window — set the game to Borderless or use Force Borderless Fullscreen.\n** Assist mode re-raises the crosshair the instant a game takes focus and keeps it pinned on top.", Theme.Small, Theme.TextDim));
            AddButton("Got it", ButtonKind.Primary, () => Close());
        }

        sealed class CompareTable : Control
        {
            public CompareTable()
            {
                Height = Theme.S(200);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.Surface);
                Theme.Smooth(g);
                int c1 = (int)(Width * 0.36), c2 = (int)(Width * 0.68), rowH = Theme.S(46);
                Theme.DrawText(g, "SIMPLE OVERLAY", Theme.Caption, Theme.TextDim, new Rectangle(c1, 0, c2 - c1, rowH), Theme.Center);
                Theme.DrawText(g, "FULLSCREEN ASSIST", Theme.Caption, Theme.TextDim, new Rectangle(c2, 0, Width - c2, rowH), Theme.Center);
                var rows = new[] { ("Fullscreen", "partial", "partial2"), ("Borderless", "yes", "yes"), ("Windowed", "yes", "yes") };
                for (int i = 0; i < rows.Length; i++)
                {
                    int y = rowH * (i + 1);
                    using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, y, Width, y);
                    Theme.DrawText(g, rows[i].Item1, Theme.BodyMedium, Theme.Text, new Rectangle(Theme.S(8), y, c1, rowH));
                    Mark(g, rows[i].Item2, new Rectangle(c1, y, c2 - c1, rowH));
                    Mark(g, rows[i].Item3, new Rectangle(c2, y, Width - c2, rowH));
                }
            }

            static void Mark(Graphics g, string v, Rectangle r)
            {
                if (v == "yes") Theme.DrawIcon(g, Glyph.Check, Theme.Icon, Theme.Success, r);
                else
                {
                    Theme.DrawText(g, v == "partial" ? "— *" : "✓ * **", Theme.BodyBold, Theme.Accent, r, Theme.Center);
                }
            }
        }
    }

    /// <summary>Edit the "games" list used by Show Only In-Game.</summary>
    public sealed class GameListDialog : DarkDialog
    {
        public GameListDialog() : base("Games", 620)
        {
            var s = AppController.I.State.Settings;
            Content.Controls.Add(new WrapLabel("Reticly treats these apps as games for “Show Only In-Game”.", Theme.Small, Theme.TextDim));
            var host = new ScrollHost { Height = Theme.S(320), BackColor = Theme.Surface };
            host.Stack.BackColor = Theme.Surface;
            var editor = new ProcessListEditor(s.GameProcesses, () => AppController.I.State.MarkSettingsChanged());
            host.Stack.Controls.Add(editor);
            Content.Controls.Add(host);
            var common = new FlatButton("Add popular games", Glyph.Game, ButtonKind.Secondary);
            common.AutoSizeWidth();
            common.Click += (o, e) =>
            {
                foreach (var g in AppController.KnownGames)
                    if (!s.GameProcesses.Any(x => string.Equals(x, g, StringComparison.OrdinalIgnoreCase))) s.GameProcesses.Add(g);
                AppController.I.State.MarkSettingsChanged();
                editor.Rebuild();
            };
            var clear = new FlatButton("Clear", Glyph.Delete, ButtonKind.Ghost);
            clear.AutoSizeWidth();
            clear.Click += (o, e) => { s.GameProcesses.Clear(); AppController.I.State.MarkSettingsChanged(); editor.Rebuild(); };
            Content.Controls.Add(new HStack(common, clear));
            AddButton("Done", ButtonKind.Primary, () => Close());
        }
    }
}
