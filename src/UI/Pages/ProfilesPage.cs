using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.UI.Controls;
using CrosshairY.UI.Dialogs;

namespace CrosshairY.UI.Pages
{
    public sealed class ProfilesPage : Page
    {
        readonly Panel listPanel = new Panel { Dock = DockStyle.Left, BackColor = Theme.Bg };
        readonly StackPanel list = new StackPanel { Spacing = Theme.S(6) };
        readonly ScrollHost editor = new ScrollHost { Dock = DockStyle.Fill };
        string selectedId;

        public override string Title => "Profiles";
        public string Subtitle => "A profile bundles a crosshair, keybinds and position for a game. Link apps to switch automatically.";

        public ProfilesPage()
        {
            listPanel.Width = Theme.S(280);
            listPanel.Padding = new Padding(Theme.S(28), Theme.S(8), Theme.S(12), Theme.S(12));
            var newBtn = new FlatButton("New profile", Glyph.Add, ButtonKind.Primary);
            newBtn.Click += (s, e) =>
            {
                var name = InputDialog.Ask(Main, "New profile", "Profile name (e.g. the game it's for)");
                if (name == null) return;
                var p = App.Profile.Clone();
                p.Name = name;
                p.Processes.Clear();
                App.State.Profiles.Add(p);
                App.State.MarkProfilesChanged();
                selectedId = p.Id;
                Rebuild();
            };
            list.Controls.Add(newBtn);
            listPanel.Controls.Add(list);
            listPanel.Layout += (s, e) => list.SetBounds(listPanel.Padding.Left, listPanel.Padding.Top, listPanel.Width - listPanel.Padding.Horizontal, list.Height);
            editor.Stack.Inner = new Padding(Theme.S(12), Theme.S(8), Theme.S(28), Theme.S(28));
            editor.Stack.Spacing = Theme.S(16);
            Controls.Add(editor);
            Controls.Add(listPanel);
            App.ProfileChanged += () => { if (Visible) Rebuild(); };
        }

        public override void OnActivated()
        {
            if (selectedId == null || App.State.Profiles.All(p => p.Id != selectedId)) selectedId = App.State.Settings.ActiveProfileId;
            Rebuild();
        }

        Profile Sel => App.State.Profiles.FirstOrDefault(p => p.Id == selectedId) ?? App.Profile;

        void Rebuild()
        {
            // profile list
            while (list.Controls.Count > 1) { var c = list.Controls[1]; list.Controls.RemoveAt(1); c.Dispose(); }
            foreach (var p in App.State.Profiles)
            {
                var prof = p;
                bool active = p.Id == App.State.Settings.ActiveProfileId;
                string apps = p.Processes.Count == 0 ? "" : "   ·  " + p.Processes.Count + " app" + (p.Processes.Count == 1 ? "" : "s");
                var b = new FlatButton(p.Name + (active ? "  (active)" : "") + apps, null, ButtonKind.Subtle)
                {
                    Toggled = p.Id == Sel.Id, Height = Theme.S(40), AlignLeft = true, Font = p.Id == Sel.Id ? Theme.BodyBold : Theme.Body
                };
                b.Click += (s, e) => { selectedId = prof.Id; Rebuild(); };
                list.Controls.Add(b);
            }
            BuildEditor();
        }

        void BuildEditor()
        {
            var st = editor.Stack;
            st.SuspendLayout();
            foreach (Control c in st.Controls.Cast<Control>().ToList()) { st.Controls.Remove(c); c.Dispose(); }
            var p = Sel;
            bool active = p.Id == App.State.Settings.ActiveProfileId;

            var info = new Card(p.Name, active ? "Active profile" : "Not active");
            var name = new TextField(p.Name);
            name.Committed += (s, e) =>
            {
                var t = name.Text.Trim();
                if (t.Length == 0 || t == p.Name) return;
                p.Name = t;
                App.NotifyProfileEdited();
            };
            info.Body.Controls.Add(new Row("Name", name, null, 0, true));
            var actions = new HStack();
            if (!active)
            {
                var activate = new FlatButton("Set active", Glyph.Check, ButtonKind.Primary);
                activate.Click += (s, e) => { App.ActivateProfile(p.Id); Rebuild(); };
                actions.Controls.Add(activate);
            }
            var dup = new FlatButton("Duplicate", Glyph.Duplicate);
            dup.Click += (s, e) =>
            {
                var c = p.Clone();
                c.Name = p.Name + " copy";
                c.Processes.Clear();
                App.State.Profiles.Add(c);
                App.State.MarkProfilesChanged();
                selectedId = c.Id;
                Rebuild();
            };
            actions.Controls.Add(dup);
            var del = new FlatButton("Delete", Glyph.Delete, ButtonKind.Danger) { Enabled = App.State.Profiles.Count > 1 };
            del.Click += (s, e) =>
            {
                if (!DarkDialog.Confirm(Main, "Delete profile", "Delete profile “" + p.Name + "”?", "Delete", true)) return;
                App.State.Profiles.Remove(p);
                if (active) App.ActivateProfile(App.State.Profiles[0].Id);
                App.State.MarkProfilesChanged();
                selectedId = App.State.Settings.ActiveProfileId;
                Rebuild();
            };
            actions.Controls.Add(del);
            info.Body.Controls.Add(actions);
            st.Controls.Add(info);

            var xh = new Card("Crosshair", "Loaded when this profile becomes active");
            var current = App.State.Find(p.CrosshairId);
            var pic = new PictureBox { Size = new Size(Theme.S(96), Theme.S(96)), BackColor = Theme.Surface2, SizeMode = PictureBoxSizeMode.CenterImage };
            if (current != null) pic.Image = Thumbnails.Make(current.Layers, Theme.S(96));
            var change = new FlatButton("Choose crosshair…", Glyph.Crosshair);
            change.Click += (s, e) =>
            {
                var id = CrosshairPickerDialog.Pick(Main, "Crosshair for " + p.Name, p.CrosshairId);
                if (id == null) return;
                p.CrosshairId = id;
                App.NotifyProfileEdited();
                BuildEditor();
            };
            var xhRow = new Panel { Height = Theme.S(100), BackColor = Theme.Surface };
            var xhName = new DarkLabel(current?.Name ?? "None selected", Theme.BodyBold);
            xhRow.Controls.AddRange(new Control[] { pic, xhName, change });
            xhRow.Layout += (s, e) =>
            {
                pic.Location = new Point(0, 2);
                xhName.SetBounds(pic.Right + Theme.S(16), Theme.S(18), xhRow.Width - pic.Width - Theme.S(16), Theme.S(24));
                change.Location = new Point(pic.Right + Theme.S(16), Theme.S(50));
            };
            xh.Body.Controls.Add(xhRow);
            st.Controls.Add(xh);

            var apps = new Card("Linked apps", "When one of these is the focused window, CrosshairY switches to this profile");
            apps.Body.Controls.Add(new ProcessListEditor(p.Processes, () => App.NotifyProfileEdited()));
            var det = new ToggleSwitch();
            det.SetSilently(App.State.Settings.ProfileDetection);
            det.CheckedChanged += (s, e) => { App.State.Settings.ProfileDetection = det.Checked; App.State.MarkSettingsChanged(); };
            apps.Body.Controls.Add(new Row("Profile detection", det, "Automatically switch profiles based on the focused app (global setting)"));
            st.Controls.Add(apps);

            var pos = new Card("Position & size", "Fine-tune where the crosshair sits for this game");
            var ox = new SliderInput(-200, 200, 1, 0, -5000, 5000) { Value = p.OffsetX };
            var oy = new SliderInput(-200, 200, 1, 0, -5000, 5000) { Value = p.OffsetY };
            var sc = new SliderInput(0.25, 5, 0.05, 2, 0.1, 10) { Value = p.Scale };
            var op = new SliderInput(0, 1, 0.01, 2) { Value = p.Opacity };
            ox.ValueChanged += (s, e) => { p.OffsetX = (int)ox.Value; Push(p); };
            oy.ValueChanged += (s, e) => { p.OffsetY = (int)oy.Value; Push(p); };
            sc.ValueChanged += (s, e) => { p.Scale = sc.Value; Push(p); };
            op.ValueChanged += (s, e) => { p.Opacity = op.Value; Push(p); };
            pos.Body.Controls.Add(new Row("Offset X", ox, null, 0, true));
            pos.Body.Controls.Add(new Row("Offset Y", oy, null, 0, true));
            pos.Body.Controls.Add(new Row("Size", sc, null, 0, true));
            pos.Body.Controls.Add(new Row("Opacity", op, null, 0, true));
            st.Controls.Add(pos);

            st.ResumeLayout(true);
        }

        void Push(Profile p)
        {
            App.State.MarkProfilesChanged();
            if (p.Id == App.State.Settings.ActiveProfileId) App.UpdateOverlay();
        }
    }
}
