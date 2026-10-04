using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.Import;
using CrosshairY.Render;
using CrosshairY.UI.Controls;

namespace CrosshairY.UI.Dialogs
{
    public class DarkDialog : Form
    {
        protected readonly StackPanel Content = new StackPanel();
        protected readonly HStack Buttons = new HStack { RightAlign = true };
        readonly Label titleLabel;

        public DarkDialog(string title, int width = 520)
        {
            title = L.T(title);
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Theme.S(width), Theme.S(200));
            KeyPreview = true;
            Content.BackColor = Theme.Surface;
            Content.Spacing = Theme.S(12);
            titleLabel = new DarkLabel(title, Theme.H2) { Height = Theme.S(30) };
            Content.Controls.Add(titleLabel);
            Controls.Add(Content);
            Controls.Add(Buttons);
            Content.SizeChanged += (s, e) => Relayout();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Relayout();
        }

        protected void Relayout()
        {
            int pad = Theme.S(22);
            Content.SetBounds(pad, pad - Theme.S(6), ClientSize.Width - pad * 2, Content.Height);
            Buttons.SetBounds(pad, Content.Bottom + Theme.S(18), ClientSize.Width - pad * 2, Theme.S(36));
            int h = Buttons.Bottom + pad;
            if (ClientSize.Height != h) ClientSize = new Size(ClientSize.Width, h);
        }

        protected FlatButton AddButton(string text, ButtonKind kind, Action onClick)
        {
            var b = new FlatButton(text, null, kind);
            b.Click += (s, e) => onClick();
            Buttons.Controls.Add(b);
            return b;
        }

        public static void Info(IWin32Window owner, string title, string message)
        {
            using (var d = new MessageDialog(title, message, false)) d.ShowDialog(owner);
        }

        public static bool Confirm(IWin32Window owner, string title, string message, string ok = "OK", bool danger = false)
        {
            using (var d = new MessageDialog(title, message, true, ok, danger)) return d.ShowDialog(owner) == DialogResult.OK;
        }
    }

    public class MessageDialog : DarkDialog
    {
        public MessageDialog(string title, string message, bool confirm, string ok = "OK", bool danger = false) : base(title, 460)
        {
            Content.Controls.Add(new WrapLabel(message, Theme.Body, Theme.TextDim));
            if (confirm) AddButton("Cancel", ButtonKind.Ghost, () => { DialogResult = DialogResult.Cancel; Close(); });
            var okb = AddButton(ok, danger ? ButtonKind.Danger : ButtonKind.Primary, () => { DialogResult = DialogResult.OK; Close(); });
            AcceptButton = null;
            Shown += (s, e) => okb.Focus();
        }
    }

    public class InputDialog : DarkDialog
    {
        readonly TextField field;
        public string Value => field.Text.Trim();

        public InputDialog(string title, string label, string initial = "") : base(title, 440)
        {
            Content.Controls.Add(new DarkLabel(label, Theme.Small, Theme.TextDim) { Height = Theme.S(18) });
            field = new TextField(initial);
            Content.Controls.Add(field);
            field.Committed += (s, e) => { };
            field.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); } };
            AddButton("Cancel", ButtonKind.Ghost, () => { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("OK", ButtonKind.Primary, () => { DialogResult = DialogResult.OK; Close(); });
            Shown += (s, e) => { field.Box.Focus(); field.Box.SelectAll(); };
        }

        public static string Ask(IWin32Window owner, string title, string label, string initial = "")
        {
            using (var d = new InputDialog(title, label, initial))
                return d.ShowDialog(owner) == DialogResult.OK && d.Value.Length > 0 ? d.Value : null;
        }
    }

    /// <summary>Paste a Crosshair X code/link, VALORANT code, CS2 code or CrosshairY code; preview, then save.</summary>
    public class ImportDialog : DarkDialog
    {
        readonly TextField codeBox;
        readonly WrapLabel status;
        readonly PictureBox preview;
        readonly FlatButton importBtn, saveBtn, applyBtn;
        ImportResult result;
        readonly TextField nameBox;
        public CrosshairEntry Imported;
        int requestId;

        public ImportDialog(string initialCode = null) : base("Import crosshair", 560)
        {
            Content.Controls.Add(new WrapLabel("Paste any of these and press Import:\n•  Crosshair X share code or link (e.g. xe4lbu6zh8 or crosshairx.gg/s/xe4lbu6zh8)\n•  VALORANT crosshair code (0;P;c;5;…)\n•  CS2 / CS:GO crosshair code (CSGO-… or CS…)\n•  CrosshairY code (CXY1-…) or crosshair JSON", Theme.Small, Theme.TextDim));
            codeBox = new TextField(initialCode ?? "", "Paste a code or link…");
            codeBox.Box.Font = Theme.Mono;
            importBtn = new FlatButton("Import", Glyph.Download, ButtonKind.Primary);
            importBtn.Click += (s, e) => DoImport();
            var row = new Panel { Height = Theme.S(34), BackColor = Color.Transparent };
            row.Controls.Add(codeBox);
            row.Controls.Add(importBtn);
            row.Layout += (s, e) =>
            {
                importBtn.Location = new Point(row.Width - importBtn.Width, 0);
                codeBox.SetBounds(0, 0, row.Width - importBtn.Width - Theme.S(8), Theme.S(34));
            };
            Content.Controls.Add(row);
            codeBox.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DoImport(); } };

            var previewHost = new Panel { Height = Theme.S(190), BackColor = Theme.Surface };
            preview = new PictureBox { Size = new Size(Theme.S(180), Theme.S(180)), BackColor = Theme.Surface2, SizeMode = PictureBoxSizeMode.CenterImage };
            nameBox = new TextField("", "Name") { Visible = false };
            status = new WrapLabel("", Theme.Body, Theme.TextDim);
            previewHost.Controls.Add(preview);
            previewHost.Controls.Add(nameBox);
            previewHost.Controls.Add(status);
            previewHost.Layout += (s, e) =>
            {
                preview.Location = new Point(0, Theme.S(4));
                int x = preview.Right + Theme.S(16);
                nameBox.SetBounds(x, Theme.S(4), previewHost.Width - x, Theme.S(34));
                status.SetBounds(x, nameBox.Visible ? nameBox.Bottom + Theme.S(10) : Theme.S(4), previewHost.Width - x, status.Height);
            };
            Content.Controls.Add(previewHost);

            AddButton("Close", ButtonKind.Ghost, () => Close());
            applyBtn = AddButton("Save & Apply", ButtonKind.Secondary, () => Save(true));
            saveBtn = AddButton("Save to My Crosshairs", ButtonKind.Primary, () => Save(false));
            applyBtn.Enabled = saveBtn.Enabled = false;
            Shown += (s, e) =>
            {
                codeBox.Box.Focus();
                if (string.IsNullOrEmpty(codeBox.Text))
                {
                    try
                    {
                        var clip = Clipboard.GetText()?.Trim();
                        if (!string.IsNullOrEmpty(clip) && clip.Length < 200000 && CodeImporter.Detect(clip) != null && CodeImporter.Detect(clip) != "cx")
                            codeBox.Text = clip;
                        else if (!string.IsNullOrEmpty(clip) && clip.Contains("crosshairx.")) codeBox.Text = clip;
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(initialCode)) DoImport();
            };
        }

        async void DoImport()
        {
            string code = codeBox.Text.Trim();
            if (code.Length == 0) return;
            string kind = CodeImporter.Detect(code);
            if (kind == null)
            {
                SetStatus("That doesn't look like a crosshair code.", true);
                return;
            }
            int id = ++requestId;
            importBtn.Enabled = false;
            SetStatus(kind == "cx" ? "Fetching Crosshair X code…" : "Importing…", false);
            try
            {
                var r = await Task.Run(() => CodeImporter.Import(code));
                if (id != requestId || IsDisposed) return;
                result = r;
                preview.Image?.Dispose();
                preview.Image = Thumbnails.Make(r.Layers, preview.Width);
                nameBox.Visible = true;
                nameBox.Text = r.Name;
                SetStatus("✔  " + r.Kind + " — " + r.Layers.Count + " layer" + (r.Layers.Count == 1 ? "" : "s") + (r.Note != null ? "\n" + r.Note : ""), false);
                applyBtn.Enabled = saveBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                if (id != requestId || IsDisposed) return;
                SetStatus(ex.Message, true);
                applyBtn.Enabled = saveBtn.Enabled = false;
            }
            finally
            {
                if (!IsDisposed) importBtn.Enabled = true;
            }
        }

        void SetStatus(string text, bool error)
        {
            status.ForeColor = error ? Theme.Danger : Theme.TextDim;
            status.Text = text;
            status.Parent?.PerformLayout();
        }

        void Save(bool apply)
        {
            if (result == null) return;
            var entry = new CrosshairEntry
            {
                Name = string.IsNullOrWhiteSpace(nameBox.Text) ? result.Name : nameBox.Text.Trim(),
                Layers = result.Layers,
                Source = result.Source
            };
            AppController.I.State.Add(entry);
            if (apply) AppController.I.ApplyCrosshair(entry.Id);
            Imported = entry;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Share/export: CrosshairY code, JSON, PNG.</summary>
    public class ShareDialog : DarkDialog
    {
        public ShareDialog(CrosshairEntry entry) : base("Choose a share method", 620)
        {
            string code = CodeImporter.ExportOwn(entry.Layers, entry.Name);
            var status = new WrapLabel("Hover over a method to see what it does.", Theme.Body, Theme.TextDim);
            var codeRow = new Panel { Height = Theme.S(38), BackColor = Color.Transparent, Visible = false };
            var codeBox = new TextField(code);
            codeBox.Box.Font = Theme.Mono;
            codeBox.Box.ReadOnly = true;
            codeRow.Controls.Add(codeBox);
            codeRow.Layout += (s, e) => codeBox.SetBounds(0, 0, codeRow.Width, Theme.S(38));

            var tiles = new Columns(
                new ShareTile(Glyph.Code, "Share Code", "Copies a CrosshairY code that contains the whole design. Anyone can paste it into Import — it works offline and never expires.", status, () =>
                {
                    SafeClipboard(code);
                    codeRow.Visible = true;
                    status.Text = "✔ Share code copied to your clipboard.";
                }),
                new ShareTile(Glyph.Copy, "Copy JSON", "Copies the raw Crosshair X layer data (JSON) to paste into tools or other apps.", status, () =>
                {
                    SafeClipboard(Json.Serialize(entry.Layers, true));
                    status.Text = "✔ JSON copied to your clipboard.";
                }),
                new ShareTile(Glyph.Image, "Image", "Saves a transparent PNG of the crosshair (choose 1× or crisp 4×).", status, () =>
                {
                    var m = Menus.Create();
                    m.Items.Item("PNG (actual size)", () => SaveFile(entry.Name, "PNG image|*.png", ".png", path => ExportPng(entry.Layers, path, 1)));
                    m.Items.Item("PNG (4× crisp)", () => SaveFile(entry.Name + "@4x", "PNG image|*.png", ".png", path => ExportPng(entry.Layers, path, 4)));
                    m.Show(Cursor.Position);
                }),
                new ShareTile(Glyph.Export, "Export", "Saves a .json file you can back up or send; restore it from Settings › Actions or Import.", status, () =>
                    SaveFile(entry.Name, "Crosshair JSON|*.json", ".json", path => File.WriteAllText(path, Json.Serialize(J.O("name", entry.Name, "layers", entry.Layers), true)))));
            tiles.Height = Theme.S(170);
            Content.Controls.Add(tiles);
            Content.Controls.Add(codeRow);
            Content.Controls.Add(status);
            if (entry.Source.StartsWith("cx:"))
                Content.Controls.Add(new WrapLabel("Imported from Crosshair X code “" + entry.Source.Substring(3) + "” — that original code still works in Crosshair X.", Theme.Small, Theme.TextMute));
            AddButton("Back", ButtonKind.Secondary, () => Close());
        }

        /// <summary>Large square option tile with an icon and caption.</summary>
        sealed class ShareTile : DarkControl
        {
            readonly string glyph, title, desc;
            readonly Label status;
            readonly Action click;

            public ShareTile(string glyph, string title, string desc, Label status, Action click)
            {
                this.glyph = glyph; this.title = title; this.desc = desc; this.status = status; this.click = click;
                Height = Theme.S(170);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (!status.Text.StartsWith("✔")) status.Text = desc; }
            protected override void OnClick(EventArgs e) { base.OnClick(e); click(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                g.Clear(ParentBack);
                var r = new RectangleF(0, 0, Width, Height);
                Theme.FillRound(g, hover ? Theme.Surface3 : Theme.Surface2, r, Theme.SF(8));
                Theme.StrokeRound(g, hover ? Theme.AccentBorder : Theme.BorderStrong, r, Theme.SF(8));
                Theme.DrawIcon(g, glyph, Theme.IconLarge, hover ? Theme.Accent : Theme.Text, new Rectangle(0, Theme.S(28), Width, Theme.S(60)));
                Theme.DrawText(g, title, Theme.BodyMedium, Theme.Text, new Rectangle(0, Theme.S(104), Width, Theme.S(40)), Theme.Center);
            }
        }

        static void SafeClipboard(string text)
        {
            try { Clipboard.SetText(text); } catch { }
        }

        void SaveFile(string name, string filter, string ext, Action<string> write)
        {
            using (var d = new SaveFileDialog { Filter = filter, FileName = MakeSafe(name) + ext })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    try { write(d.FileName); }
                    catch (Exception ex) { Info(this, "Export failed", ex.Message); }
                }
        }

        public static string MakeSafe(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Length == 0 ? "crosshair" : s;
        }

        /// <summary>Exports a tightly cropped PNG; scale &gt; 1 enlarges with nearest-neighbour to keep pixels crisp.</summary>
        public static void ExportPng(List<object> layers, string path, int scale)
        {
            using (var r = CrosshairRenderer.Render(layers, 1))
            {
                double rad = Math.Ceiling(Thumbnails.ContentRadius(r.Bitmap, r.OriginX, r.OriginY)) + 1;
                int half = (int)rad;
                int size = half * 2;
                using (var crop = new Bitmap(size * scale, size * scale, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(crop))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(r.Bitmap, new Rectangle(0, 0, size * scale, size * scale), new Rectangle(r.OriginX - half, r.OriginY - half, size, size), GraphicsUnit.Pixel);
                    crop.Save(path, ImageFormat.Png);
                }
            }
        }
    }

    /// <summary>Pick a crosshair from the library.</summary>
    public class CrosshairPickerDialog : DarkDialog
    {
        public string SelectedId;

        public CrosshairPickerDialog(string title, string currentId, bool allowNone = false) : base(title, 760)
        {
            var host = new ScrollHost { Height = Theme.S(440), BackColor = Theme.Surface };
            host.Stack.BackColor = Theme.Surface;
            var grid = new CrosshairGrid { BackColor = Theme.Surface, Style = TileStyle.Plain, TileWidth = Theme.S(160), TileHeight = Theme.S(130), EmptyText = "Your library is empty." };
            grid.Items = AppController.I.State.Library.OrderBy(e => e.Order).Select(e => new TileItem
            {
                Id = e.Id, Name = e.Name, Layers = e.Layers, CacheKey = e.Id + e.Updated.Ticks, Active = e.Id == currentId
            }).ToList();
            grid.ItemClicked += it => { SelectedId = it.Id; };
            grid.ItemDoubleClicked += it => { SelectedId = it.Id; DialogResult = DialogResult.OK; Close(); };
            host.Stack.Controls.Add(grid);
            Content.Controls.Add(host);
            AddButton("Cancel", ButtonKind.Ghost, () => { DialogResult = DialogResult.Cancel; Close(); });
            if (allowNone) AddButton("None", ButtonKind.Secondary, () => { SelectedId = ""; DialogResult = DialogResult.OK; Close(); });
            AddButton("Select", ButtonKind.Primary, () => { if (SelectedId != null) { DialogResult = DialogResult.OK; Close(); } });
        }

        public static string Pick(IWin32Window owner, string title, string currentId, bool allowNone = false)
        {
            using (var d = new CrosshairPickerDialog(title, currentId, allowNone))
                return d.ShowDialog(owner) == DialogResult.OK ? d.SelectedId : null;
        }
    }
}
