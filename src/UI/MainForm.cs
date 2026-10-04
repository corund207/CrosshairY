using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Reticly.Core;
using Reticly.Input;
using Reticly.UI.Controls;
using Reticly.UI.Dialogs;
using Reticly.UI.Pages;

namespace Reticly.UI
{
    public abstract class Page : Panel
    {
        public abstract string Title { get; }
        protected MainForm Main => MainForm.Instance;
        protected AppController App => AppController.I;

        protected Page()
        {
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        public virtual void OnActivated() { }
        public virtual void OnDeactivated() { }
        public virtual bool CanLeave() => true;
    }

    public sealed class MainForm : Form
    {
        readonly TitleBar titleBar;
        readonly Sidebar sidebar;
        readonly Panel content;
        public readonly Toast Toast = new Toast();
        readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
        Page current;
        public readonly NotifyIcon Tray;
        bool reallyClosing, closeHintShown;
        public static MainForm Instance;

        public MainForm()
        {
            Instance = this;
            Text = "Reticly";
            BackColor = Theme.Chrome;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(Theme.S(1060), Theme.S(680));
            Size = new Size(Theme.S(1320), Theme.S(840));
            Icon = AppIcon.Make(64);
            KeyPreview = true;
            DoubleBuffered = true;

            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            titleBar = new TitleBar(this) { Dock = DockStyle.Top };
            sidebar = new Sidebar(this) { Dock = DockStyle.Left };
            Controls.Add(content);
            Controls.Add(sidebar);
            Controls.Add(titleBar);
            content.Controls.Add(Toast);

            Tray = new NotifyIcon { Icon = AppIcon.Make(32), Text = "Reticly", Visible = true };
            Tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
            Tray.ContextMenuStrip = BuildTrayMenu();
            Tray.ContextMenuStrip.Opening += (s, e) => { Tray.ContextMenuStrip = BuildTrayMenu(); };
            Tray.BalloonTipClicked += (s, e) =>
            {
                if (pendingUpdate == null) return;
                var u = pendingUpdate;
                pendingUpdate = null;
                ShowFromTray();
                ShowUpdate(u);
            };

            var app = AppController.I;
            app.Toast += msg =>
            {
                if (app.State.Settings.ShowTrayNotifications && !Visible) try { Tray.ShowBalloonTip(1200, "Reticly", msg, ToolTipIcon.None); } catch { }
            };
            app.VisibilityChanged += () => titleBar.Invalidate(true);
            app.ProfileChanged += () => titleBar.Invalidate(true);
            app.ActiveCrosshairChanged += () => titleBar.Invalidate(true);
            app.State.ProfilesChanged += () => titleBar.Invalidate(true);
            app.State.LibraryChanged += () => titleBar.Invalidate(true);

            Navigate("crosshairs");

            if (!Program.SnapMode)
            {
                // automatic update check (at most about once a day), a few seconds after start
                var t = new Timer { Interval = 4000 };
                t.Tick += (s, e) => { t.Stop(); t.Dispose(); if (Updater.AutoCheckDue(app.State.Settings)) CheckForUpdates(false); };
                t.Start();
            }
        }

        // ---------------- updates ----------------

        UpdateInfo pendingUpdate;
        bool checkingUpdate;

        /// <summary>Asks GitHub for a newer release. manual = from a button (always reports the result).</summary>
        public async void CheckForUpdates(bool manual)
        {
            if (checkingUpdate) return;
            checkingUpdate = true;
            var s = AppController.I.State.Settings;
            try
            {
                var u = await Updater.CheckAsync();
                s.LastUpdateCheck = DateTime.UtcNow.ToString("o");
                AppController.I.State.MarkSettingsChanged();
                if (u == null)
                {
                    if (manual) ShowToast(string.Format(L.T("You're on the latest version ({0})"), Program.Version), Glyph.Check);
                    return;
                }
                if (!manual && u.Version == s.SkippedVersion) return;
                if (Visible && WindowState != FormWindowState.Minimized) ShowUpdate(u);
                else
                {
                    pendingUpdate = u;
                    try { Tray.ShowBalloonTip(6000, L.T("Update available"), string.Format(L.T("Reticly {0} is ready. Click to update."), u.Version), ToolTipIcon.Info); } catch { }
                }
            }
            catch (Exception ex)
            {
                if (manual) DarkDialog.Info(this, L.T("Couldn't check for updates"), ex.Message);
            }
            finally { checkingUpdate = false; }
        }

        public void ShowUpdate(UpdateInfo u)
        {
            using (var d = new UpdateDialog(u)) d.ShowDialog(this);
        }

        public void ShowToast(string text, string glyph = null) => Toast.Show(text, glyph);

        /// <summary>Where a UI element is, in this form's client coordinates (used by the tour).</summary>
        public Rectangle TourTarget(string key)
        {
            Rectangle r;
            Control owner;
            if (key == "pill" || key == "profile" || key == "preview") { r = titleBar.RectFor(key); owner = titleBar; }
            else { r = sidebar.RectFor(key); owner = sidebar; }
            if (r.IsEmpty) return r;
            return RectangleToClient(owner.RectangleToScreen(r));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!Program.SnapMode && !AppController.I.State.Settings.TourDone)
            {
                var t = new Timer { Interval = 700 };
                t.Tick += (s, e2) => { t.Stop(); t.Dispose(); if (Visible) Tour.Start(this); };
                t.Start();
            }
        }

        public void ApplySidebarWidth() => sidebar.Width = AppController.I.State.Settings.SidebarCollapsed ? Theme.S(60) : Theme.S(170);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(this);
            // re-run NCCALCSIZE so the caption disappears
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0020 | Native.SWP_NOMOVE | Native.SWP_NOSIZE | 0x0004 | Native.SWP_NOACTIVATE);
        }

        // ---------------- custom chrome (keeps native resize borders, snap and shadow) ----------------

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct NCCALCSIZE_PARAMS { public RECT r0, r1, r2; public IntPtr pos; }
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x83, WM_NCHITTEST = 0x84, HTCLIENT = 1, HTCAPTION = 2, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
            if (m.Msg == Program.ShowMessage) { ShowFromTray(); return; }
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                var p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(m.LParam);
                int top = p.r0.Top;
                base.WndProc(ref m);
                p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(m.LParam);
                p.r0.Top = top + (WindowState == FormWindowState.Maximized ? GetSystemMetrics(33) + GetSystemMetrics(92) : 0);
                Marshal.StructureToPtr(p, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() != HTCLIENT) return;
                var pt = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                if (WindowState != FormWindowState.Maximized && pt.Y < Theme.S(5))
                {
                    m.Result = (IntPtr)(pt.X < Theme.S(10) ? HTTOPLEFT : pt.X > Width - Theme.S(10) ? HTTOPRIGHT : HTTOP);
                    return;
                }
                if (pt.Y < titleBar.Height) m.Result = (IntPtr)HTCAPTION;
                return;
            }
            base.WndProc(ref m);
        }

        internal void ToggleMaximize() => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            titleBar?.Invalidate();
            if (Toast != null && Toast.Visible && Toast.Parent != null)
                Toast.Location = new Point((Toast.Parent.ClientSize.Width - Toast.Width) / 2, Toast.Parent.ClientSize.Height - Toast.Height - Theme.S(28));
        }

        // ---------------- tray ----------------

        ContextMenuStrip BuildTrayMenu()
        {
            var app = AppController.I;
            var m = Menus.Create();
            m.Items.Item(L.T("Open Reticly"), ShowFromTray);
            m.Items.Sep();
            m.Items.Item(app.CrosshairVisible ? L.T("Hide crosshair") : L.T("Show crosshair"), app.ToggleVisible);

            // crosshairs: favorites and recent first would be nice, but library order is what people arrange
            var xh = new ToolStripMenuItem(L.T("Crosshair")) { ForeColor = Theme.Text };
            var lib = app.State.Library.OrderBy(x => x.Order).ToList();
            foreach (var e in lib.Take(25))
            {
                var id = e.Id;
                xh.DropDownItems.Item(e.Name, () => app.ApplyCrosshair(id), true, id == app.Profile.CrosshairId);
            }
            if (lib.Count > 25) xh.DropDownItems.Item(L.T("More…"), () => { ShowFromTray(); Crosshairs.ShowSaved(app.Profile.CrosshairId); });
            if (xh.DropDownItems.Count == 0) xh.Enabled = false;
            m.Items.Add(xh);

            // weapon: the loadout first, then every gun of the current game
            var active = app.ActiveCrosshair;
            if (active != null && Recoil.HasRecoil(active.Layers))
            {
                var wm = new ToolStripMenuItem(L.T("Weapon")) { ForeColor = Theme.Text };
                var cur = Recoil.IsOff(active.Layers) ? null : Recoil.CurrentWeapon(active.Layers);
                foreach (var slot in app.Profile.RecoilSlots.ToList())
                {
                    var sl = slot;
                    var pat = sl.Weapon == "off" ? null : Recoil.Find(sl.Weapon);
                    if (pat == null && sl.Weapon != "off") continue;
                    string label = (pat?.Name ?? L.T("Recoil off")) + (string.IsNullOrEmpty(sl.Key) ? "" : "   (" + KeyBinding.Display(sl.Key) + ")");
                    wm.DropDownItems.Item(label, () => app.SelectRecoilSlot(sl));
                }
                if (wm.DropDownItems.Count > 0) wm.DropDownItems.Add(new ToolStripSeparator());
                string game = cur?.Game ?? Recoil.CurrentWeapon(active.Layers)?.Game ?? Recoil.Games[0];
                foreach (var w in Recoil.ForGame(game))
                {
                    var key = w.Key;
                    wm.DropDownItems.Item(w.Name, () => app.SelectRecoilSlot(new RecoilSlot { Weapon = key }), true, cur != null && cur.Key == key);
                }
                m.Items.Add(wm);
            }

            var prof = new ToolStripMenuItem(L.T("Profile")) { ForeColor = Theme.Text };
            foreach (var p in app.State.Profiles)
            {
                var id = p.Id;
                prof.DropDownItems.Item(p.Name, () => app.ActivateProfile(id), true, id == app.State.Settings.ActiveProfileId);
            }
            m.Items.Add(prof);
            m.Items.Item(L.T("Center crosshair"), app.CenterPosition);
            m.Items.Sep();
            m.Items.Item(pendingUpdate != null ? string.Format(L.T("Update to {0}…"), pendingUpdate.Version) : L.T("Check for updates"), () =>
            {
                if (pendingUpdate != null) { var u = pendingUpdate; pendingUpdate = null; ShowFromTray(); ShowUpdate(u); }
                else CheckForUpdates(true);
            });
            m.Items.Item(L.T("Exit"), ExitApp);
            return m;
        }

        public void ShowFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            Native.SetForegroundWindow(Handle);
        }

        public void ExitApp()
        {
            if (current != null && !current.CanLeave()) return;
            reallyClosing = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyClosing && e.CloseReason == CloseReason.UserClosing && AppController.I.State.Settings.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                if (!closeHintShown)
                {
                    closeHintShown = true;
                    try { Tray.ShowBalloonTip(2500, "Reticly is still running", "Your crosshair stays on screen. Open Reticly from the tray icon.", ToolTipIcon.Info); } catch { }
                }
                return;
            }
            if (!reallyClosing && e.CloseReason == CloseReason.UserClosing && current != null && !current.CanLeave()) { e.Cancel = true; return; }
            Tray.Visible = false;
            base.OnFormClosing(e);
        }

        // ---------------- navigation ----------------

        public Page CurrentPage => current;
        public string CurrentKey { get; private set; }

        public void Navigate(string key)
        {
            if (CurrentKey == key) { current?.OnActivated(); return; }
            if (current != null && !current.CanLeave()) return;
            if (!pages.TryGetValue(key, out var page))
            {
                switch (key)
                {
                    case "crosshairs": page = new CrosshairsPage(); break;
                    case "designer": page = new DesignerPage(); break;
                    case "keybinds": page = new KeybindsPage(); break;
                    case "randomizer": page = new RandomizerPage(); break;
                    case "settings": page = new SettingsPage(); break;
                    case "help": page = new HelpPage(); break;
                    case "profiles": page = new ProfilesPage(); break;
                    default: return;
                }
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                pages[key] = page;
                content.Controls.Add(page);
            }
            current?.OnDeactivated();
            if (current != null) current.Visible = false;
            current = page;
            CurrentKey = key;
            page.Visible = true;
            page.BringToFront();
            Toast.BringToFront();
            page.OnActivated();
            sidebar.Invalidate();
        }

        public CrosshairsPage Crosshairs { get { Navigate("crosshairs"); return pages["crosshairs"] as CrosshairsPage; } }

        public DesignerPage Designer
        {
            get
            {
                if (!pages.TryGetValue("designer", out var p)) { Navigate("designer"); return pages["designer"] as DesignerPage; }
                return (DesignerPage)p;
            }
        }

        public void EditInDesigner(CrosshairEntry e)
        {
            if (current != null && CurrentKey != "designer" && !current.CanLeave()) return;
            if (pages.TryGetValue("designer", out var p) && !p.CanLeave()) return;
            Navigate("designer");
            Designer.Open(e);
        }

        public void NewInDesigner(List<object> layers = null, string name = null)
        {
            if (pages.TryGetValue("designer", out var p) && !p.CanLeave()) return;
            Navigate("designer");
            Designer.OpenNew(layers, name);
        }

        public void ShowImport(string code = null)
        {
            using (var d = new ImportDialog(code))
                if (d.ShowDialog(this) == DialogResult.OK && d.Imported != null)
                {
                    Crosshairs.ShowSaved(d.Imported.Id);
                    ShowToast("Imported “" + d.Imported.Name + "”", Glyph.Download);
                }
        }

        public void ShowShare(CrosshairEntry e)
        {
            if (e == null) { ShowToast("Select a crosshair to share first", Glyph.Info); return; }
            using (var d = new ShareDialog(e)) d.ShowDialog(this);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.I)) { ShowImport(); return true; }
            if (current is DesignerPage dp && dp.HandleShortcut(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ title bar

        sealed class TitleBar : Control
        {
            readonly MainForm form;
            readonly ProfileButton profile = new ProfileButton();
            readonly PreviewButton preview = new PreviewButton();
            readonly VisibilityPill pill = new VisibilityPill();
            int hoverBtn = -1;

            public TitleBar(MainForm f)
            {
                form = f;
                Height = Theme.S(52);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                BackColor = Theme.Chrome;
                Controls.Add(profile);
                Controls.Add(preview);
                Controls.Add(pill);
                preview.Click += (s, e) => form.Crosshairs.ShowSaved(AppController.I.Profile.CrosshairId);
                var tips = new ToolTip();
                tips.SetToolTip(preview, L.T("Current crosshair"));
                pill.MouseEnter += (s, e) => tips.SetToolTip(pill, L.T("Toggle crosshair") + " (" + KeyBinding.Display(AppController.I.Profile.ToggleKey) + ")");
            }

            int BtnW => Theme.S(46);
            Rectangle BtnRect(int i) => new Rectangle(Width - BtnW * (3 - i), 0, BtnW, Height);

            public Rectangle RectFor(string key) => key == "pill" ? pill.Bounds : key == "profile" ? profile.Bounds : key == "preview" ? preview.Bounds : Rectangle.Empty;

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                int y = (Height - Theme.S(36)) / 2;
                int x = Width - BtnW * 3 - Theme.S(14);
                pill.SetBounds(x - Theme.S(60), y, Theme.S(60), Theme.S(36)); x -= Theme.S(60) + Theme.S(10);
                preview.SetBounds(x - Theme.S(36), y, Theme.S(36), Theme.S(36)); x -= Theme.S(36) + Theme.S(24);
                profile.SetBounds(x - Theme.S(150), y, Theme.S(150), Theme.S(36));
            }

            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84, HTTRANSPARENT = -1;
                if (m.Msg == WM_NCHITTEST)
                {
                    var pt = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
                    bool onButtons = pt.X >= Width - BtnW * 3;
                    if (!onButtons && !(form.WindowState != FormWindowState.Maximized && pt.Y < Theme.S(5) && false)) { m.Result = (IntPtr)HTTRANSPARENT; return; }
                }
                base.WndProc(ref m);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int h = -1;
                for (int i = 0; i < 3; i++) if (BtnRect(i).Contains(e.Location)) h = i;
                if (h != hoverBtn) { hoverBtn = h; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverBtn = -1; Invalidate(); }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (BtnRect(0).Contains(e.Location)) form.WindowState = FormWindowState.Minimized;
                else if (BtnRect(1).Contains(e.Location)) form.ToggleMaximize();
                else if (BtnRect(2).Contains(e.Location)) form.Close();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);
                Theme.Smooth(g);
                using (var logo = AppIcon.Mark(Theme.S(26))) g.DrawImage(logo, Theme.S(16), (Height - Theme.S(26)) / 2);
                int x = Theme.S(52);
                // measure in this device context so the accent letter sits exactly where it would in the full word
                var nf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                int full = TextRenderer.MeasureText(g, "Reticly", Theme.Logo, new Size(1000, Height), nf).Width;
                int y1 = TextRenderer.MeasureText(g, "y", Theme.Logo, new Size(1000, Height), nf).Width;
                Theme.DrawText(g, "Reticl", Theme.Logo, Theme.Text, new Rectangle(x, 0, Theme.S(200), Height));
                Theme.DrawText(g, "y", Theme.Logo, Theme.Accent, new Rectangle(x + full - y1, 0, Theme.S(40), Height));
                // divider before window buttons
                using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - BtnW * 3 - Theme.S(4), Theme.S(12), Width - BtnW * 3 - Theme.S(4), Height - Theme.S(12));
                using (var p = new Pen(Theme.Border)) g.DrawLine(p, preview.Left - Theme.S(12), Theme.S(12), preview.Left - Theme.S(12), Height - Theme.S(12));
                string[] glyphs = { Glyph.Minimize, form.WindowState == FormWindowState.Maximized ? Glyph.Restore : Glyph.Maximize, Glyph.ChromeClose };
                for (int i = 0; i < 3; i++)
                {
                    var r = BtnRect(i);
                    if (i == hoverBtn) using (var b = new SolidBrush(i == 2 ? Color.FromArgb(196, 43, 28) : Theme.Surface2)) g.FillRectangle(b, r);
                    Theme.DrawIcon(g, glyphs[i], Theme.IconSmall, i == hoverBtn && i == 2 ? Color.White : Theme.TextDim, r);
                }
                using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }
        }

        /// <summary>Profile quick select: person icon, profile name, chevron.</summary>
        sealed class ProfileButton : DarkControl
        {
            public ProfileButton() { Cursor = Cursors.Hand; }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                var app = AppController.I;
                var m = Menus.Create();
                foreach (var p in app.State.Profiles)
                {
                    var id = p.Id;
                    m.Items.Item(p.Name + (p.Processes.Count > 0 ? "   (" + string.Join(", ", p.Processes.Take(2)) + (p.Processes.Count > 2 ? "…" : "") + ")" : ""),
                        () => app.ActivateProfile(id), true, id == app.State.Settings.ActiveProfileId);
                }
                m.Items.Sep();
                m.Items.Item(L.T("New profile…"), () =>
                {
                    var name = InputDialog.Ask(MainForm.Instance, L.T("New profile"), L.T("Profile name (e.g. the game it's for)"));
                    if (name == null) return;
                    var np = app.Profile.Clone();
                    np.Name = name;
                    np.Processes.Clear();
                    app.State.Profiles.Add(np);
                    app.State.MarkProfilesChanged();
                    app.ActivateProfile(np.Id);
                    MainForm.Instance.Navigate("profiles");
                });
                m.Items.Item(L.T("Manage profiles…"), () => MainForm.Instance.Navigate("profiles"));
                m.Show(this, new Point(0, Height + Theme.S(4)));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                g.Clear(Theme.Chrome);
                var r = new RectangleF(0, 0, Width, Height);
                Theme.FillRound(g, hover ? Theme.Surface3 : Theme.Surface2, r, Theme.SF(7));
                Theme.StrokeRound(g, Theme.BorderStrong, r, Theme.SF(7));
                Theme.DrawIcon(g, Glyph.Person, Theme.Icon, Theme.TextDim, new Rectangle(Theme.S(10), 0, Theme.S(20), Height));
                Theme.DrawText(g, AppController.I.Profile.Name, Theme.BodyMedium, Theme.Text, new Rectangle(Theme.S(38), 0, Width - Theme.S(66), Height));
                Theme.DrawIcon(g, Glyph.ChevronDown, Theme.IconTiny, Theme.TextDim, new Rectangle(Width - Theme.S(26), 0, Theme.S(16), Height));
            }
        }

        /// <summary>Square showing the active crosshair at actual size; outlined in orange while visible.</summary>
        sealed class PreviewButton : DarkControl
        {
            public PreviewButton() { Cursor = Cursors.Hand; }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                g.Clear(Theme.Chrome);
                var app = AppController.I;
                var r = new RectangleF(0, 0, Width, Height);
                Theme.FillRound(g, Color.FromArgb(12, 12, 12), r, Theme.SF(7));
                Theme.StrokeRound(g, app.CrosshairVisible ? Theme.Accent : Theme.BorderStrong, r, Theme.SF(7), app.CrosshairVisible ? Theme.SF(1.5) : 1);
                var e2 = app.ActiveCrosshair;
                if (e2 == null) return;
                var bmp = Thumbnails.Actual(e2.Id + ":" + e2.Updated.Ticks, e2.Layers, Width - 4, Height - 4);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImageUnscaled(bmp, 2, 2);
            }
        }

        /// <summary>The big visibility toggle: orange pill with an eye when visible, dark red with a power icon when hidden.</summary>
        sealed class VisibilityPill : DarkControl
        {
            public VisibilityPill() { Cursor = Cursors.Hand; }

            protected override void OnClick(EventArgs e) { base.OnClick(e); AppController.I.ToggleVisible(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                g.Clear(Theme.Chrome);
                bool on = AppController.I.CrosshairVisible;
                var r = new RectangleF(0, 0, Width, Height);
                var fill = on ? (hover ? Theme.AccentHover : Theme.Accent) : (hover ? Theme.Blend(Theme.DangerDim, Theme.Danger, .2) : Theme.DangerDim);
                Theme.FillRound(g, fill, r, Height / 2f);
                if (!on) Theme.StrokeRound(g, Theme.Blend(Theme.DangerDim, Theme.Danger, .45), r, Height / 2f);
                float d = Height - Theme.SF(8);
                float kx = on ? Width - d - Theme.SF(4) : Theme.SF(4);
                using (var b = new SolidBrush(on ? Color.FromArgb(28, 22, 12) : Color.FromArgb(52, 24, 26))) g.FillEllipse(b, kx, Theme.SF(4), d, d);
                Theme.DrawIcon(g, on ? Glyph.Eye : Glyph.Power, Theme.IconSmall, on ? Theme.Accent : Theme.Danger, Rectangle.Round(new RectangleF(kx, Theme.SF(4), d, d)));
            }
        }

        // ------------------------------------------------------------------ sidebar

        sealed class Sidebar : Control
        {
            readonly MainForm form;
            static readonly (string key, string label, string glyph)[] Top =
            {
                ("crosshairs", "Crosshairs", Glyph.Crosshair),
                ("designer", "Designer", Glyph.Palette),
                ("keybinds", "Keybinds", Glyph.Keyboard),
                ("randomizer", "Randomizer", Glyph.Shuffle),
            };
            static readonly (string key, string label, string glyph)[] Bottom =
            {
                ("profiles", "Profiles", Glyph.People),
                ("settings", "Settings", Glyph.Settings),
                ("help", "Help", Glyph.Help),
            };
            int hover = -100;

            public Sidebar(MainForm f)
            {
                form = f;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                BackColor = Theme.Chrome;
                Width = AppController.I.State.Settings.SidebarCollapsed ? Theme.S(60) : Theme.S(170);
            }

            bool Collapsed => Width < Theme.S(100);

            public Rectangle RectFor(string key)
            {
                for (int i = 0; i < Top.Length; i++) if (Top[i].key == key) return TopRect(i);
                for (int i = 0; i < Bottom.Length; i++) if (Bottom[i].key == key) return BottomRect(i);
                return Rectangle.Empty;
            }
            int RowH => Theme.S(40);
            Rectangle MenuRect => new Rectangle(Theme.S(8), Theme.S(10), Width - Theme.S(16), RowH);
            Rectangle TopRect(int i) => new Rectangle(Theme.S(8), Theme.S(58) + i * (RowH + Theme.S(4)), Width - Theme.S(16), RowH);
            Rectangle BottomRect(int i) => new Rectangle(Theme.S(8), Height - Theme.S(12) - (Bottom.Length - i) * (RowH + Theme.S(4)), Width - Theme.S(16), RowH);

            int HitTest(Point p)
            {
                if (MenuRect.Contains(p)) return -1;
                for (int i = 0; i < Top.Length; i++) if (TopRect(i).Contains(p)) return i;
                for (int i = 0; i < Bottom.Length; i++) if (BottomRect(i).Contains(p)) return 100 + i;
                return -100;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int h = HitTest(e.Location);
                if (h != hover) { hover = h; Invalidate(); }
                Cursor = h != -100 ? Cursors.Hand : Cursors.Default;
            }

            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -100; Invalidate(); }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int h = HitTest(e.Location);
                if (h == -1)
                {
                    var s = AppController.I.State.Settings;
                    s.SidebarCollapsed = !Collapsed;
                    AppController.I.State.MarkSettingsChanged();
                    Width = s.SidebarCollapsed ? Theme.S(60) : Theme.S(170);
                    return;
                }
                if (h >= 0 && h < Top.Length) form.Navigate(Top[h].key);
                else if (h >= 100) form.Navigate(Bottom[h - 100].key);
            }

            void Item(Graphics g, Rectangle r, string glyph, string label, bool active, bool hot)
            {
                if (active) Theme.FillRound(g, Theme.Surface3, r, Theme.SF(8));
                else if (hot) Theme.FillRound(g, Theme.Surface, r, Theme.SF(8));
                var c = active ? Theme.Text : hot ? Theme.Text : Theme.Blend(Theme.Text, Theme.Chrome, .14);
                Theme.DrawIcon(g, glyph, Theme.Icon, c, new Rectangle(r.X + Theme.S(12), r.Y, Theme.S(20), r.Height));
                if (!Collapsed) Theme.DrawText(g, L.T(label), active ? Theme.BodyMedium : Theme.Nav, c, new Rectangle(r.X + Theme.S(46), r.Y, r.Width - Theme.S(50), r.Height));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);
                Theme.Smooth(g);
                Item(g, MenuRect, Glyph.Hamburger, "Menu", false, hover == -1);
                for (int i = 0; i < Top.Length; i++) Item(g, TopRect(i), Top[i].glyph, Top[i].label, form.CurrentKey == Top[i].key, hover == i);
                int divY = BottomRect(0).Y - Theme.S(10);
                using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, divY, Width, divY);
                for (int i = 0; i < Bottom.Length; i++) Item(g, BottomRect(i), Bottom[i].glyph, Bottom[i].label, form.CurrentKey == Bottom[i].key, hover == 100 + i);
                using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
            }
        }
    }

    /// <summary>Procedurally drawn app mark and icon.</summary>
    public static class AppIcon
    {
        /// <summary>Ring with four ticks and a center dot, drawn in the accent color (title bar mark).</summary>
        public static Bitmap Mark(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float c = size / 2f, r = size * 0.36f, th = Math.Max(1.6f, size * 0.1f);
                using (var pen = new Pen(Theme.Text, th)) g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
                using (var b = new SolidBrush(Theme.Chrome))
                {
                    float gap = size * 0.16f;
                    g.FillRectangle(b, c - gap / 2, 0, gap, size * 0.32f);
                    g.FillRectangle(b, c - gap / 2, size * 0.68f, gap, size * 0.32f);
                }
                using (var b = new SolidBrush(Theme.Accent))
                {
                    float d = size * 0.2f;
                    g.FillEllipse(b, c - d / 2, c - d / 2, d, d);
                    g.FillRectangle(b, c - th / 2, size * 0.02f, th, size * 0.22f);
                    g.FillRectangle(b, c - th / 2, size * 0.76f, th, size * 0.22f);
                }
            }
            return bmp;
        }

        public static Bitmap Logo(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                var r = new RectangleF(0, 0, size, size);
                using (var b = new SolidBrush(ColorUtil.Parse("#1C1C1C")))
                using (var path = Theme.Round(r, size * 0.24f)) g.FillPath(b, path);
                using (var mark = Mark((int)(size * 0.8))) g.DrawImage(mark, size * 0.1f, size * 0.1f);
            }
            return bmp;
        }

        public static Icon Make(int size)
        {
            using (var bmp = Logo(size))
                return Icon.FromHandle(bmp.GetHicon());
        }
    }
}
