using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Reticly.Core;
using Reticly.UI.Controls;

namespace Reticly.UI.Pages
{
    /// <summary>Randomizer: spin through random and hand-picked crosshairs, optionally applying each spin live.</summary>
    public sealed class RandomizerPage : Page
    {
        readonly PreviewCanvas stage = new PreviewCanvas { ShowGrid = false, ShowRuler = false, ShowCenter = false };
        readonly DarkLabel nameLabel = new DarkLabel("", Theme.H1) { TextAlign = ContentAlignment.MiddleCenter };
        readonly DarkLabel rarityLabel = new DarkLabel("", Theme.Caption, Theme.TextDim) { TextAlign = ContentAlignment.MiddleCenter };
        readonly DarkLabel countLabel = new DarkLabel("", Theme.Body, Theme.TextDim) { TextAlign = ContentAlignment.MiddleCenter };
        readonly FlatButton spin = new FlatButton("Spin", Glyph.Shuffle, ButtonKind.Primary) { Height = Theme.S(46) };
        readonly FlatButton save = new FlatButton("Save", Glyph.BookmarkFilled, ButtonKind.Secondary) { Height = Theme.S(46) };
        readonly FlatButton edit = new FlatButton("Customize", Glyph.Palette, ButtonKind.Secondary) { Height = Theme.S(46) };
        readonly ToggleSwitch live = new ToggleSwitch();
        readonly ToggleSwitch auto = new ToggleSwitch();
        readonly Timer autoTimer = new Timer { Interval = 4000 };
        List<object> current;
        string currentName;
        int spins;
        readonly Random rnd = new Random();
        static readonly string[] RarityNames = { "COMMON", "UNCOMMON", "RARE", "LEGENDARY" };
        static readonly Color[] RarityColors = { Theme.TextDim, Color.FromArgb(70, 190, 120), Color.FromArgb(110, 140, 255), Theme.Accent };

        public override string Title => "Randomizer";

        public RandomizerPage()
        {
            BackColor = Theme.Bg;
            spin.Width = Theme.S(180);
            save.AutoSizeWidth();
            edit.AutoSizeWidth();
            live.SetSilently(true);
            var liveLabel = new DarkLabel("Apply each spin on screen", Theme.Body, Theme.TextDim) { Width = Theme.S(190) };
            var autoLabel = new DarkLabel("Auto-spin every 4 s", Theme.Body, Theme.TextDim) { Width = Theme.S(150) };
            var hint = new WrapLabel("Tip: while this page is open, your Reload key (" + Input.KeyBinding.Display(App.Profile.ReloadKey) + ") spins too — play with the Randomizer running in the background.", Theme.Small, Theme.TextMute) { TextAlign = ContentAlignment.MiddleCenter };
            Controls.AddRange(new Control[] { stage, nameLabel, rarityLabel, countLabel, spin, save, edit, live, liveLabel, auto, autoLabel, hint });
            Layout += (s, e) =>
            {
                int w = Math.Min(Width - Theme.S(80), Theme.S(560));
                int x = (Width - w) / 2, y = Theme.S(36);
                stage.SetBounds(x, y, w, Math.Min(Theme.S(340), Height - Theme.S(380))); y = stage.Bottom + Theme.S(18);
                rarityLabel.SetBounds(x, y, w, Theme.S(20)); y += Theme.S(22);
                nameLabel.SetBounds(x, y, w, Theme.S(36)); y += Theme.S(38);
                countLabel.SetBounds(x, y, w, Theme.S(22)); y += Theme.S(34);
                int bw = spin.Width + save.Width + edit.Width + Theme.S(20);
                int bx = (Width - bw) / 2;
                spin.Location = new Point(bx, y); save.Location = new Point(spin.Right + Theme.S(10), y); edit.Location = new Point(save.Right + Theme.S(10), y);
                y += spin.Height + Theme.S(22);
                int tw = live.Width + liveLabel.Width + auto.Width + autoLabel.Width + Theme.S(48);
                int tx = (Width - tw) / 2;
                live.Location = new Point(tx, y + Theme.S(4)); liveLabel.SetBounds(live.Right + Theme.S(10), y, liveLabel.Width, Theme.S(32));
                auto.Location = new Point(liveLabel.Right + Theme.S(28), y + Theme.S(4)); autoLabel.SetBounds(auto.Right + Theme.S(10), y, autoLabel.Width, Theme.S(32));
                y += Theme.S(44);
                hint.SetBounds(x, y, w, hint.Height);
            };
            stage.Paint += (s, e) => Theme.StrokeRound(e.Graphics, Theme.Border, new RectangleF(0, 0, stage.Width, stage.Height), Theme.SF(12));
            spin.Click += (s, e) => Spin();
            save.Click += (s, e) =>
            {
                if (current == null) return;
                var en = App.State.Add(new CrosshairEntry { Name = currentName, Layers = (List<object>)J.DeepClone(current), Source = "randomizer" });
                MainForm.Instance.ShowToast("Saved “" + en.Name + "”", Glyph.BookmarkFilled);
            };
            edit.Click += (s, e) => { if (current != null) MainForm.Instance.NewInDesigner(current, currentName); };
            live.CheckedChanged += (s, e) => App.SetPreview(live.Checked && Visible ? current : null);
            auto.CheckedChanged += (s, e) => { if (auto.Checked) autoTimer.Start(); else autoTimer.Stop(); };
            autoTimer.Tick += (s, e) => { if (Visible) Spin(); };
            App.ReloadPressed += () => { if (Visible && MainForm.Instance.CurrentPage == this) Spin(); };
        }

        public override void OnActivated()
        {
            if (current == null) Spin();
            else App.SetPreview(live.Checked ? current : null);
        }

        public override void OnDeactivated()
        {
            autoTimer.Stop();
            auto.SetSilently(false);
            App.SetPreview(null);
        }

        void Spin()
        {
            int rarity;
            if (rnd.Next(5) == 0)
            {
                // hand-picked designs from the built-in collection
                var all = Presets.All();
                var p = all[rnd.Next(all.Count)];
                current = (List<object>)J.DeepClone(p.Layers);
                currentName = p.Name;
                rarity = p.Category == "Animated" || p.Category == "Recoil" ? 3 : p.Category == "Fancy" ? 2 : 1;
            }
            else
            {
                current = RandomCrosshair.Make();
                currentName = RandomCrosshair.Name();
                rarity = RandomCrosshair.LastRarity;
            }
            spins++;
            nameLabel.Text = currentName;
            rarityLabel.Text = RarityNames[rarity];
            rarityLabel.ForeColor = RarityColors[rarity];
            countLabel.Text = "Spin #" + spins;
            stage.SetLayers(current);
            stage.FitToContent();
            if (live.Checked) App.SetPreview(current);
        }
    }

    /// <summary>Help: setup tips, troubleshooting and how the import/recoil features work.</summary>
    public sealed class HelpPage : Page
    {
        readonly ScrollHost scroll = new ScrollHost { Dock = DockStyle.Fill };

        public override string Title => "Help";

        public HelpPage()
        {
            scroll.Stack.Inner = new Padding(Theme.S(34), Theme.S(28), Theme.S(34), Theme.S(34));
            scroll.Stack.Spacing = Theme.S(14);
            Controls.Add(scroll);
            var st = scroll.Stack;
            st.Controls.Add(new DarkLabel(L.T("Help"), Theme.H1) { Height = Theme.S(34) });
            var tour = new FlatButton(L.T("Take the tour"), Glyph.Compass, ButtonKind.Primary);
            tour.AutoSizeWidth();
            tour.Click += (s, e) => Tour.Start(MainForm.Instance);
            var updates = new FlatButton(L.T("Check for updates"), Glyph.Refresh, ButtonKind.Secondary);
            updates.AutoSizeWidth();
            updates.Click += (s, e) => MainForm.Instance.CheckForUpdates(true);
            st.Controls.Add(new HStack(tour, updates) { Height = Theme.S(38) });
            Topic(Glyph.Monitor, "My crosshair doesn't show in a game",
                "Overlays can't draw over true exclusive fullscreen. Switch the game to Borderless or Windowed Fullscreen, or use Settings › Display › Force Borderless Fullscreen. Many DX9/DX12 games in “fullscreen” still work thanks to Windows fullscreen optimizations; try Fullscreen Assist Mode if the crosshair disappears when the game takes focus.");
            Topic(Glyph.Keyboard, "Keybinds don't work in a game",
                "If a game runs as administrator, Windows won't let normal apps see its keyboard input. Run Reticly as administrator too. Single-key binds also work while Ctrl/Shift/Alt are held.");
            Topic(Glyph.Download, "Importing crosshair codes",
                "Press Import (Ctrl+I) and paste: a Crosshair X share code or link (e.g. xe4lbu6zh8 or crosshairx.gg/s/xe4lbu6zh8), a VALORANT profile code (0;P;…), a CS2/CS:GO code (CSGO-… or CS…), a Reticly code (CXY1-…) or crosshair JSON. Crosshair X codes are downloaded from the same public share service crosshairx.gg uses.");
            Topic(Glyph.Recoil, "Recoil tracking crosshairs",
                "Recoil crosshairs are timed animations: while you hold Fire, a layer walks along a weapon's spray pattern and snaps back when you let go. Browse › Recoil Tracking has ready-made ones, and the Designer's Animate tab can apply a pattern to any layer. Patterns are approximate; tune the scale for your resolution and FOV. Reticly never reads game memory.");
            Topic(Glyph.Target, "Hit markers",
                "An overlay can't see what happens inside a game, so hit markers and kill flashes play when you press their keys (Keybinds page) or on every shot (Settings › Hit Markers).");
            Topic(Glyph.Edit, "Custom spray patterns",
                "Keybinds › Recoil loadout › Edit patterns (or the Designer's Recoil Pattern section) opens the pattern editor. Drag the dots, test the spray at its real fire rate, and save. Saving under a built-in weapon's name replaces its pattern; you can reset it any time.");
            Topic(Glyph.Crosshair, "Centering",
                "The crosshair is drawn on the exact center pixel of the selected monitor in physical pixels. If a game's own center differs by a pixel, nudge it in Settings › Position & Size or set Position Keybinds.");
            Topic(Glyph.Info, "About",
                "Reticly " + Program.Version + " — a native Windows crosshair overlay. It renders Crosshair X designs pixel-for-pixel and is an independent project, not affiliated with CenterPoint Gaming. Noto Sans is used under the SIL Open Font License.");
        }

        void Topic(string glyph, string title, string body)
        {
            var card = new SettingsCard();
            var row = new SettingRow(glyph, title, null, null) { Height = Theme.S(56) };
            card.Add(row);
            var text = new WrapLabel(L.T(body), Theme.Body, Theme.TextDim) { BackColor = Theme.Surface, Margin = new Padding(Theme.S(68), 0, Theme.S(20), Theme.S(18)) };
            card.Rows.Controls.Add(text);
            scroll.Stack.Controls.Add(card);
        }
    }
}
