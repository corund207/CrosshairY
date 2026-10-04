using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Reticly.Core;

namespace Reticly.UI.Controls
{
    /// <summary>Edits a list of executable names (e.g. "cs2.exe") with add-from-running-apps and browse.</summary>
    public class ProcessListEditor : StackPanel
    {
        readonly List<string> list;
        readonly Action changed;
        readonly StackPanel rows = new StackPanel { Spacing = Theme.S(6) };

        public ProcessListEditor(List<string> target, Action onChanged)
        {
            list = target;
            changed = onChanged;
            Spacing = Theme.S(10);
            var add = new FlatButton("Add running app", Glyph.Add, ButtonKind.Secondary);
            var browse = new FlatButton("Browse .exe…", Glyph.Folder, ButtonKind.Secondary);
            var manual = new TextField("", "or type e.g. cs2.exe and press Enter") { Width = Theme.S(260) };
            add.Click += (s, e) => ShowRunning(add);
            browse.Click += (s, e) =>
            {
                using (var d = new OpenFileDialog { Filter = "Programs|*.exe", Title = "Choose a game executable" })
                    if (d.ShowDialog(FindForm()) == DialogResult.OK) Add(Path.GetFileName(d.FileName));
            };
            manual.Box.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                var t = manual.Text.Trim();
                if (t.Length == 0) return;
                if (!t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) t += ".exe";
                Add(t);
                manual.Text = "";
            };
            Controls.Add(new HStack(add, browse, manual));
            Controls.Add(rows);
            Rebuild();
        }

        void Add(string exe)
        {
            if (string.IsNullOrWhiteSpace(exe)) return;
            if (list.Any(x => string.Equals(x, exe, StringComparison.OrdinalIgnoreCase))) return;
            list.Add(exe);
            Rebuild();
            changed();
        }

        public void Rebuild()
        {
            rows.SuspendLayout();
            foreach (Control c in rows.Controls.Cast<Control>().ToList()) { rows.Controls.Remove(c); c.Dispose(); }
            if (list.Count == 0)
                rows.Controls.Add(new DarkLabel("No apps added yet.", Theme.Small, Theme.TextMute) { Height = Theme.S(22) });
            foreach (var exe in list.ToList())
            {
                var name = exe;
                var remove = new FlatButton("", Glyph.Close, ButtonKind.Ghost) { Width = Theme.S(30), Height = Theme.S(30) };
                remove.Click += (s, e) => { list.Remove(name); Rebuild(); changed(); };
                var row = new Row("▸  " + name, remove) { Height = Theme.S(32) };
                rows.Controls.Add(row);
            }
            rows.ResumeLayout(true);
            PerformLayout();
        }

        void ShowRunning(Control anchor)
        {
            var m = Menus.Create();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<(string exe, string title)>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;
                    if (p.Id == Process.GetCurrentProcess().Id) continue;
                    string exe = AppController.ExeNameOf((uint)p.Id);
                    if (string.IsNullOrEmpty(exe) || !seen.Add(exe)) continue;
                    items.Add((exe, p.MainWindowTitle));
                }
                catch { }
                finally { p.Dispose(); }
            }
            foreach (var (exe, title) in items.OrderBy(i => i.exe, StringComparer.OrdinalIgnoreCase))
            {
                string t = title.Length > 40 ? title.Substring(0, 40) + "…" : title;
                m.Items.Item(exe + "   —   " + t, () => Add(exe));
            }
            if (items.Count == 0) m.Items.Item("No windows found", null, false);
            m.Show(anchor, new Point(0, anchor.Height + 2));
        }
    }
}
