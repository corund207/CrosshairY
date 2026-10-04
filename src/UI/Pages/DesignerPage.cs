using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.Render;
using CrosshairY.UI.Controls;
using CrosshairY.UI.Dialogs;

namespace CrosshairY.UI.Pages
{
    /// <summary>
    /// Designer: toolbar (name, File, Share, Save, undo/redo), canvas with floating Layers panel and tool dock,
    /// and an inspector with collapsible sections for Design and Animate.
    /// </summary>
    public sealed class DesignerPage : Page
    {
        CrosshairEntry editing;
        List<object> layers = Defaults.NewCrosshairLayers();
        int selected;
        bool animateMode;
        int stageIndex;
        bool dirty, building;
        string lastSnapshot;
        readonly Stack<string> undo = new Stack<string>(), redo = new Stack<string>();
        bool livePreview = true;

        readonly Panel toolbar = new Panel { Dock = DockStyle.Top, BackColor = Theme.Chrome };
        readonly Panel canvasHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
        readonly PreviewCanvas canvas = new PreviewCanvas { Dock = DockStyle.Fill, Background = "dark", RulerBottom = 92 };
        readonly LayersPanel layersPanel = new LayersPanel();
        readonly ToolDock dock = new ToolDock();
        readonly Panel inspector = new Panel { Dock = DockStyle.Right, BackColor = Theme.Chrome };
        readonly Panel inspectorHeader = new Panel { Dock = DockStyle.Top, BackColor = Theme.Chrome };
        readonly DarkLabel layerTitle = new DarkLabel("", Theme.H2);
        readonly Segmented modeSwitch = new Segmented(new[] { "Design", "Animate" });
        readonly ScrollHost props = new ScrollHost { Dock = DockStyle.Fill };
        readonly TextField nameBox = new TextField("", "Untitled") { Icon = Glyph.Pencil };
        readonly FlatButton undoBtn = new FlatButton("", Glyph.Undo, ButtonKind.Ghost), redoBtn = new FlatButton("", Glyph.Redo, ButtonKind.Ghost);
        readonly FlatButton liveBtn = new FlatButton("Live", Glyph.Eye, ButtonKind.Ghost);
        static string[] fontNames;

        public override string Title => "Designer";

        public DesignerPage()
        {
            BackColor = Theme.Canvas;
            BuildToolbar();
            canvasHost.Controls.Add(layersPanel);
            canvasHost.Controls.Add(dock);
            canvasHost.Controls.Add(canvas);
            layersPanel.BringToFront();
            dock.BringToFront();
            canvasHost.Layout += (s, e) =>
            {
                layersPanel.Location = new Point(Theme.S(14), Theme.S(14));
                dock.Location = new Point((canvasHost.Width - dock.Width) / 2, canvasHost.Height - dock.Height - Theme.S(16));
            };
            BuildInspector();
            Controls.Add(canvasHost);
            Controls.Add(inspector);
            Controls.Add(toolbar);

            canvas.ShowGrid = App.State.Settings.DesignerGrid;
            canvas.Background = App.State.Settings.DesignerBackground;
            canvas.LayerClicked += i => SelectLayer(i);
            canvas.LayerDragged += (i, dx, dy) =>
            {
                if (!(Layer(i) is Dictionary<string, object> l)) return;
                var pos = J.EnsureObj(l, "position");
                pos["x"] = J.Num(pos, "x") + dx;
                pos["y"] = J.Num(pos, "y") + dy;
                Changed();
                RefreshPositionRows();
            };
            canvas.LayerDragEnded += Commit;

            layersPanel.SelectedChanged += i => SelectLayer(i);
            layersPanel.AddClicked += () => ShowAddMenu(layersPanel, new Point(Theme.S(14), layersPanel.Height));
            layersPanel.VisibilityToggled += i =>
            {
                var l = Layer(i);
                if (l == null) return;
                if (J.Bool(l, "hidden")) l.Remove("hidden"); else l["hidden"] = true;
                Changed(); Commit();
            };
            layersPanel.RightClicked += (i, pt) => LayerMenu(i, layersPanel, pt);
            layersPanel.Renamed += (i, name) =>
            {
                var l = Layer(i);
                if (l == null) return;
                if (string.IsNullOrWhiteSpace(name)) l.Remove("layerName"); else l["layerName"] = name;
                Changed(); Commit(); BuildProps();
            };

            dock.ToolClicked += OnTool;
            dock.ModeChanged += anim => SetMode(anim);
            ResetDocument(null, Defaults.NewCrosshairLayers(), "Untitled");
        }

        Dictionary<string, object> Layer(int i) => i >= 0 && i < layers.Count ? layers[i] as Dictionary<string, object> : null;
        Dictionary<string, object> L => Layer(selected);

        void OnTool(string t)
        {
            switch (t)
            {
                case "select": canvas.Focus(); break;
                case "crosshair": AddLayer(Defaults.ModelLayer()); break;
                case "image": { var l = PickImageLayer(false); if (l != null) AddLayer(l); break; }
                case "shape":
                    {
                        var m = Menus.Create();
                        m.Items.Item("Square", () => AddLayer(Defaults.ShapeLayer("square")));
                        m.Items.Item("Circle", () => AddLayer(Defaults.ShapeLayer("circle")));
                        m.Items.Item("Triangle", () => AddLayer(Defaults.ShapeLayer("triangle")));
                        m.Show(dock, dock.ToolPoint("shape"));
                        break;
                    }
                case "draw": { var l = PickImageLayer(true); if (l != null) AddLayer(l); break; }
                case "text": AddLayer(Defaults.TextLayer("+")); break;
                case "fit": canvas.FitToContent(); break;
                case "bg":
                    {
                        var order = new[] { "dark", "checker", "scene", "light" };
                        canvas.Background = order[(Array.IndexOf(order, canvas.Background) + 1) % order.Length];
                        canvas.Invalidate();
                        App.State.Settings.DesignerBackground = canvas.Background; App.State.MarkSettingsChanged();
                        MainForm.Instance.ShowToast("Canvas: " + char.ToUpper(canvas.Background[0]) + canvas.Background.Substring(1), Glyph.Palette);
                        break;
                    }
                case "grid":
                    canvas.ShowGrid = !canvas.ShowGrid; canvas.Invalidate();
                    App.State.Settings.DesignerGrid = canvas.ShowGrid; App.State.MarkSettingsChanged();
                    break;
            }
        }

        // ------------------------------------------------------------------ layout

        void BuildToolbar()
        {
            toolbar.Height = Theme.S(56);
            var file = new FlatButton("File", Glyph.ChevronDown, ButtonKind.Secondary) { Height = Theme.S(36) };
            file.AutoSizeWidth();
            var share = new FlatButton("Share", Glyph.Upload, ButtonKind.Secondary) { Height = Theme.S(36), Tint = Theme.Purple, TintBorder = Theme.PurpleBorder, TintText = Color.White };
            share.AutoSizeWidth();
            var save = new FlatButton("Save", Glyph.BookmarkFilled, ButtonKind.Ghost) { Height = Theme.S(36) };
            save.AutoSizeWidth();
            var dup = new FlatButton("", Glyph.Duplicate, ButtonKind.Ghost) { Width = Theme.S(36), Height = Theme.S(36) };
            var del = new FlatButton("", Glyph.Delete, ButtonKind.Ghost) { Width = Theme.S(36), Height = Theme.S(36) };
            undoBtn.Size = redoBtn.Size = new Size(Theme.S(36), Theme.S(36));
            liveBtn.Height = Theme.S(36);
            liveBtn.Toggled = livePreview;
            liveBtn.AutoSizeWidth();
            var tips = new ToolTip();
            tips.SetToolTip(undoBtn, "Undo (Ctrl+Z)"); tips.SetToolTip(redoBtn, "Redo (Ctrl+Y)"); tips.SetToolTip(dup, "Duplicate layer (Ctrl+D)");
            tips.SetToolTip(del, "Delete layer"); tips.SetToolTip(liveBtn, "Show this design on screen while editing"); tips.SetToolTip(save, "Save (Ctrl+S)");
            toolbar.Controls.AddRange(new Control[] { nameBox, file, share, save, liveBtn, undoBtn, redoBtn, dup, del });
            toolbar.Layout += (s, e) =>
            {
                int y = (toolbar.Height - Theme.S(36)) / 2, x = Theme.S(18);
                nameBox.SetBounds(x, y, Theme.S(280), Theme.S(36)); x = nameBox.Right + Theme.S(16);
                file.Location = new Point(x, y); x = file.Right + Theme.S(10);
                share.Location = new Point(x, y); x = share.Right + Theme.S(6);
                save.Location = new Point(x, y);
                int r = toolbar.Width - Theme.S(18);
                foreach (var b in new Control[] { del, dup }) { r -= b.Width; b.Location = new Point(r, y); r -= Theme.S(4); }
                r -= Theme.S(14);
                foreach (var b in new Control[] { redoBtn, undoBtn }) { r -= b.Width; b.Location = new Point(r, y); r -= Theme.S(4); }
                r -= Theme.S(14) + liveBtn.Width;
                liveBtn.Location = new Point(r, y);
            };
            toolbar.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(p, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
                    int x1 = undoBtn.Left - Theme.S(10), x2 = dup.Left - Theme.S(10);
                    e.Graphics.DrawLine(p, x1, Theme.S(16), x1, toolbar.Height - Theme.S(16));
                    e.Graphics.DrawLine(p, x2, Theme.S(16), x2, toolbar.Height - Theme.S(16));
                    e.Graphics.DrawLine(p, nameBox.Right + Theme.S(8), Theme.S(16), nameBox.Right + Theme.S(8), toolbar.Height - Theme.S(16));
                }
            };
            nameBox.TextChanged += (s, e) => { if (!building) dirty = true; };
            file.Click += (s, e) => ShowFileMenu(file);
            share.Click += (s, e) =>
            {
                var temp = new CrosshairEntry { Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled" : nameBox.Text.Trim(), Layers = StripRuntimeKeys((List<object>)J.DeepClone(layers)), Source = editing?.Source ?? "designer" };
                MainForm.Instance.ShowShare(temp);
            };
            save.Click += (s, e) => Save(false);
            undoBtn.Click += (s, e) => Undo();
            redoBtn.Click += (s, e) => Redo();
            dup.Click += (s, e) => DuplicateLayer();
            del.Click += (s, e) => DeleteLayer();
            liveBtn.Click += (s, e) => { livePreview = !livePreview; liveBtn.Toggled = livePreview; liveBtn.Invalidate(); UpdateLive(); };
        }

        void ShowFileMenu(Control anchor)
        {
            var m = Menus.Create();
            m.Items.Item("New crosshair", () => { if (CanLeave()) OpenNew(); });
            m.Items.Item("Save", () => Save(false));
            m.Items.Item("Save as new", () => Save(true));
            m.Items.Sep();
            m.Items.Item("Add image layer…", () => { var l = PickImageLayer(false); if (l != null) AddLayer(l); });
            m.Items.Item("Add pixel-art layer…", () => { var l = PickImageLayer(true); if (l != null) AddLayer(l); });
            m.Items.Item("Import code…", () => MainForm.Instance.ShowImport());
            m.Items.Sep();
            m.Items.Item("Export PNG…", () => ExportFile("PNG image|*.png", ".png", p => ShareDialog.ExportPng(layers, p, 1)));
            m.Items.Item("Export PNG (4×)…", () => ExportFile("PNG image|*.png", ".png", p => ShareDialog.ExportPng(layers, p, 4)));
            m.Items.Item("Export JSON…", () => ExportFile("Crosshair JSON|*.json", ".json", p => File.WriteAllText(p, Json.Serialize(J.O("name", nameBox.Text, "layers", StripRuntimeKeys((List<object>)J.DeepClone(layers))), true))));
            m.Items.Sep();
            m.Items.Item("Revert changes", Revert, dirty);
            m.Show(anchor, new Point(0, anchor.Height + Theme.S(4)));
        }

        void ExportFile(string filter, string ext, Action<string> write)
        {
            using (var d = new SaveFileDialog { Filter = filter, FileName = ShareDialog.MakeSafe(string.IsNullOrWhiteSpace(nameBox.Text) ? "crosshair" : nameBox.Text) + ext })
                if (d.ShowDialog(Main) == DialogResult.OK)
                {
                    try { write(d.FileName); MainForm.Instance.ShowToast("Exported " + Path.GetFileName(d.FileName), Glyph.Export); }
                    catch (Exception ex) { DarkDialog.Info(Main, "Export failed", ex.Message); }
                }
        }

        void BuildInspector()
        {
            inspector.Width = Theme.S(340);
            inspectorHeader.Height = Theme.S(60);
            modeSwitch.Width = Theme.S(156);
            modeSwitch.Height = Theme.S(32);
            inspectorHeader.Controls.Add(layerTitle);
            inspectorHeader.Controls.Add(modeSwitch);
            inspectorHeader.Layout += (s, e) =>
            {
                modeSwitch.Location = new Point(inspectorHeader.Width - modeSwitch.Width - Theme.S(16), (inspectorHeader.Height - modeSwitch.Height) / 2);
                layerTitle.SetBounds(Theme.S(18), 0, modeSwitch.Left - Theme.S(26), inspectorHeader.Height);
            };
            inspectorHeader.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, inspectorHeader.Height - 1, inspectorHeader.Width, inspectorHeader.Height - 1); };
            modeSwitch.SelectedChanged += (s, e) => SetMode(modeSwitch.SelectedIndex == 1);
            props.BackColor = Theme.Chrome;
            props.Stack.BackColor = Theme.Chrome;
            props.Stack.Spacing = 0;
            inspector.Controls.Add(props);
            inspector.Controls.Add(inspectorHeader);
            inspector.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, 0, inspector.Height); };
        }

        void SetMode(bool anim)
        {
            animateMode = anim;
            modeSwitch.SetSilently(anim ? 1 : 0);
            dock.Animate = anim;
            dock.Invalidate();
            stageIndex = 0;
            BuildProps();
        }

        // ------------------------------------------------------------------ document

        public void Open(CrosshairEntry e) => ResetDocument(e, (List<object>)J.DeepClone(e.Layers), e.Name);

        public void OpenNew(List<object> l = null, string name = null)
        {
            ResetDocument(null, l != null ? (List<object>)J.DeepClone(l) : Defaults.NewCrosshairLayers(), name ?? "Untitled");
            if (l != null) dirty = true;
        }

        void ResetDocument(CrosshairEntry e, List<object> l, string name)
        {
            building = true;
            editing = e;
            layers = l;
            Defaults.NormalizeLayers(layers);
            if (layers.Count == 0) layers.Add(Defaults.ModelLayer());
            nameBox.Text = name;
            building = false;
            undo.Clear(); redo.Clear();
            lastSnapshot = Json.Serialize(layers);
            dirty = false;
            selected = layers.Count - 1;
            stageIndex = 0;
            canvas.SetLayers(layers);
            SelectLayer(selected, force: true);
            canvas.RequestFit();
            UpdateLive();
            UpdateUndoButtons();
        }

        public override void OnActivated() => UpdateLive();
        public override void OnDeactivated() => App.SetPreview(null);

        void UpdateLive()
        {
            if (!Visible) return;
            App.SetPreview(livePreview ? layers : null);
        }

        public override bool CanLeave()
        {
            if (!dirty) return true;
            using (var d = new SaveChangesDialog(string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled" : nameBox.Text))
            {
                var r = d.ShowDialog(Main);
                if (r == DialogResult.Yes) return Save(false);
                if (r == DialogResult.No) { dirty = false; App.SetPreview(null); return true; }
                return false;
            }
        }

        bool Save(bool asNew)
        {
            string name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled" : nameBox.Text.Trim();
            var clean = StripRuntimeKeys((List<object>)J.DeepClone(layers));
            if (editing == null || asNew)
            {
                var e = new CrosshairEntry { Name = asNew && editing != null && name == editing.Name ? name + " copy" : name, Layers = clean, Source = "designer" };
                if (editing != null) e.Folder = editing.Folder;
                App.State.Add(e);
                editing = e;
                App.ApplyCrosshair(e.Id);
            }
            else
            {
                var e = App.State.Find(editing.Id);
                if (e == null) { e = editing; App.State.Add(e); }
                e.Name = name;
                e.Layers = clean;
                e.Updated = DateTime.UtcNow;
                editing = e;
                App.State.MarkLibraryChanged();
                if (App.Profile.CrosshairId == e.Id || App.Profile.AimCrosshairId == e.Id) App.PushCrosshair();
                else App.ApplyCrosshair(e.Id);
            }
            dirty = false;
            MainForm.Instance.ShowToast("Saved “" + name + "”", Glyph.BookmarkFilled);
            return true;
        }

        static List<object> StripRuntimeKeys(List<object> l)
        {
            foreach (var d in l.OfType<Dictionary<string, object>>())
            {
                var line = J.Obj(d, "line");
                line?.Remove("_vis"); line?.Remove("_visArms");
            }
            return l;
        }

        void Revert()
        {
            if (!dirty) return;
            if (!DarkDialog.Confirm(Main, "Discard changes", "Throw away all unsaved changes to this crosshair?", "Discard", true)) return;
            if (editing != null) Open(App.State.Find(editing.Id) ?? editing);
            else OpenNew();
        }

        void Changed()
        {
            if (building) return;
            dirty = true;
            canvas.SelectedLayer = selected;
            canvas.SetLayers(layers);
            layersPanel.SetItems(layers, selected);
            UpdateLive();
        }

        void Commit()
        {
            string now = Json.Serialize(layers);
            if (now == lastSnapshot) return;
            undo.Push(lastSnapshot);
            if (undo.Count > 200) { var keep = undo.Take(150).Reverse().ToList(); undo.Clear(); foreach (var k in keep) undo.Push(k); }
            redo.Clear();
            lastSnapshot = now;
            UpdateUndoButtons();
        }

        void UpdateUndoButtons()
        {
            undoBtn.Enabled = undo.Count > 0;
            redoBtn.Enabled = redo.Count > 0;
        }

        void Undo()
        {
            Commit();
            if (undo.Count == 0) return;
            redo.Push(lastSnapshot);
            Restore(undo.Pop());
        }

        void Redo()
        {
            if (redo.Count == 0) return;
            undo.Push(lastSnapshot);
            Restore(redo.Pop());
        }

        void Restore(string snapshot)
        {
            layers = (List<object>)Json.Parse(snapshot);
            lastSnapshot = snapshot;
            selected = Math.Min(selected, layers.Count - 1);
            dirty = true;
            canvas.SetLayers(layers);
            SelectLayer(selected, force: true);
            UpdateLive();
            UpdateUndoButtons();
        }

        public bool HandleShortcut(Keys k)
        {
            if (k == (Keys.Control | Keys.Z)) { Undo(); return true; }
            if (k == (Keys.Control | Keys.Y) || k == (Keys.Control | Keys.Shift | Keys.Z)) { Redo(); return true; }
            if (k == (Keys.Control | Keys.S)) { Save(false); return true; }
            if (k == (Keys.Control | Keys.D)) { DuplicateLayer(); return true; }
            if (k == Keys.Delete && canvas.Focused) { DeleteLayer(); return true; }
            return false;
        }

        internal void SelectTabForSnap(string t) => SetMode(t.Equals("animate", StringComparison.OrdinalIgnoreCase));

        // ------------------------------------------------------------------ layers

        void SelectLayer(int i, bool force = false)
        {
            if (layers.Count == 0) return;
            i = Math.Max(0, Math.Min(layers.Count - 1, i));
            bool changed = i != selected || force;
            selected = i;
            canvas.SelectedLayer = i;
            canvas.Invalidate();
            layersPanel.SetItems(layers, selected);
            if (!changed) return;
            stageIndex = 0;
            BuildProps();
        }

        void ShowAddMenu(Control anchor, Point pt)
        {
            var m = Menus.Create();
            m.Items.Item("Crosshair (lines + dot)", () => AddLayer(Defaults.ModelLayer()));
            m.Items.Item("Square", () => AddLayer(Defaults.ShapeLayer("square")));
            m.Items.Item("Circle", () => AddLayer(Defaults.ShapeLayer("circle")));
            m.Items.Item("Triangle", () => AddLayer(Defaults.ShapeLayer("triangle")));
            m.Items.Item("Text", () => AddLayer(Defaults.TextLayer("+")));
            m.Items.Sep();
            m.Items.Item("Image from file…", () => { var l = PickImageLayer(false); if (l != null) AddLayer(l); });
            m.Items.Item("Pixel art from file…", () => { var l = PickImageLayer(true); if (l != null) AddLayer(l); });
            m.Items.Sep();
            var rec = new ToolStripMenuItem("Recoil tracker") { ForeColor = Theme.Text };
            foreach (var p in Recoil.Patterns)
            {
                var pat = p;
                rec.DropDownItems.Item(pat.Name + "  ·  " + pat.Game, () =>
                {
                    var made = Recoil.MakeCrosshair(pat);
                    AddLayer((Dictionary<string, object>)made[1]);
                    SetMode(true);
                });
            }
            m.Items.Add(rec);
            m.Show(anchor, pt);
        }

        void LayerMenu(int i, Control anchor, Point pt)
        {
            SelectLayer(i);
            var m = Menus.Create();
            m.Items.Item("Rename…", () =>
            {
                var name = InputDialog.Ask(Main, "Rename layer", "Layer name (leave empty for automatic)", J.Str(L, "layerName", ""));
                if (name == null) return;
                if (string.IsNullOrWhiteSpace(name)) L.Remove("layerName"); else L["layerName"] = name;
                Changed(); Commit(); BuildProps();
            });
            m.Items.Item(J.Bool(L, "hidden") ? "Show" : "Hide", () => { if (J.Bool(L, "hidden")) L.Remove("hidden"); else L["hidden"] = true; Changed(); Commit(); });
            m.Items.Item("Duplicate", DuplicateLayer);
            m.Items.Item("Move up", () => MoveLayer(1), i < layers.Count - 1);
            m.Items.Item("Move down", () => MoveLayer(-1), i > 0);
            m.Items.Sep();
            m.Items.Item("Delete", DeleteLayer, layers.Count > 1);
            m.Show(anchor, pt);
        }

        Dictionary<string, object> PickImageLayer(bool drawing)
        {
            var img = PickImage(out int w, out int h, out string name);
            if (img == null) return null;
            return drawing ? Defaults.DrawLayer(img, w, h) : Defaults.ImageLayer(img, w, h, name);
        }

        string PickImage(out int w, out int h, out string name)
        {
            w = h = 0; name = null;
            using (var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*", Title = "Choose an image" })
            {
                if (d.ShowDialog(Main) != DialogResult.OK) return null;
                try
                {
                    var bytes = File.ReadAllBytes(d.FileName);
                    if (bytes.Length > 8 * 1024 * 1024) { DarkDialog.Info(Main, "Image too large", "Please choose an image under 8 MB."); return null; }
                    var decoded = ImageCache.Decode(bytes);
                    if (decoded.Failed) throw new Exception(decoded.Error);
                    w = decoded.Width; h = decoded.Height;
                    double max = Math.Max(w, h);
                    if (max > 128) { w = (int)Math.Round(w * 128 / max); h = (int)Math.Round(h * 128 / max); }
                    name = Path.GetFileNameWithoutExtension(d.FileName);
                    return ImageCache.ToDataUrl(bytes, ImageCache.MimeFromPath(d.FileName));
                }
                catch (Exception ex)
                {
                    DarkDialog.Info(Main, "Couldn't load image", ex.Message);
                    return null;
                }
            }
        }

        void AddLayer(Dictionary<string, object> l)
        {
            int at = Math.Min(layers.Count, selected + 1);
            layers.Insert(at, l);
            Defaults.NormalizeLayers(layers);
            Changed();
            Commit();
            SelectLayer(at, force: true);
        }

        void MoveLayer(int dir)
        {
            int j = selected + dir;
            if (j < 0 || j >= layers.Count) return;
            var l = layers[selected];
            layers.RemoveAt(selected);
            layers.Insert(j, l);
            selected = j;
            Changed();
            Commit();
            SelectLayer(j, force: true);
        }

        void DuplicateLayer()
        {
            if (L == null) return;
            var copy = J.CloneObj(L);
            copy.Remove("layerName");
            layers.Insert(selected + 1, copy);
            Changed();
            Commit();
            SelectLayer(selected + 1, force: true);
        }

        void DeleteLayer()
        {
            if (layers.Count <= 1) { MainForm.Instance.ShowToast("A crosshair needs at least one layer", Glyph.Info); return; }
            layers.RemoveAt(selected);
            Changed();
            Commit();
            SelectLayer(Math.Min(selected, layers.Count - 1), force: true);
        }

        // ------------------------------------------------------------------ inspector

        StackPanel cur;
        readonly List<Action> positionRefreshers = new List<Action>();

        void BuildProps()
        {
            if (building) return;
            layerTitle.Text = L == null ? "" : Defaults.LayerName(L, selected);
            props.SuspendLayout();
            var st = props.Stack;
            st.SuspendLayout();
            foreach (Control c in st.Controls.Cast<Control>().ToList()) { st.Controls.Remove(c); c.Dispose(); }
            positionRefreshers.Clear();
            cur = null;
            var l = L;
            if (l != null)
            {
                if (animateMode) BuildAnimate(l);
                else
                    switch (Defaults.LayerType(l))
                    {
                        case "image":
                        case "draw": BuildImage(l); break;
                        case "shape": BuildShape(l); break;
                        case "text": BuildText(l); break;
                        default: BuildModel(l); break;
                    }
            }
            st.ResumeLayout(true);
            props.ResumeLayout(true);
            props.AutoScrollPosition = Point.Empty;
        }

        void RefreshPositionRows() { foreach (var r in positionRefreshers) r(); }

        void Section(string t)
        {
            var sec = new InspectorSection(t);
            props.Stack.Controls.Add(sec);
            cur = sec.Body;
        }

        void Note(string t) => cur.Controls.Add(new WrapLabel(t, Theme.Small, Theme.TextDim));

        Row R(string label, Control editor, int width = 0, bool fill = true)
        {
            var row = new Row(label, editor, null, width, fill && width == 0) { FillLabel = 100, BackColor = Theme.Chrome };
            cur.Controls.Add(row);
            return row;
        }

        SliderInput Num(string label, Func<Dictionary<string, object>> target, string key, double min, double max, double step, int dec, double def = 0,
            double hardMin = double.NaN, double hardMax = double.NaN, Action after = null)
        {
            var si = new SliderInput(min, max, step, dec, double.IsNaN(hardMin) ? min : hardMin, double.IsNaN(hardMax) ? max : hardMax);
            si.Value = J.Num(target(), key, def);
            si.ValueChanged += (s, e) => { target()[key] = si.Value; Changed(); after?.Invoke(); };
            si.ValueCommitted += (s, e) => Commit();
            R(label, si);
            return si;
        }

        ColorButton Col(string label, Func<Dictionary<string, object>> target, string key, string def = "#FFFFFF")
        {
            var cb = new ColorButton { Color = ColorUtil.Parse(J.Str(target(), key, def)) };
            cb.ColorChanged += (s, e) => { target()[key] = cb.Hex; Changed(); };
            cb.ColorCommitted += (s, e) => Commit();
            R(label, cb);
            return cb;
        }

        ToggleSwitch Tog(string label, Func<Dictionary<string, object>> target, string key, bool def = false, Action after = null)
        {
            var t = new ToggleSwitch();
            t.SetSilently(J.Bool(target(), key, def));
            t.CheckedChanged += (s, e) => { target()[key] = t.Checked; Changed(); Commit(); after?.Invoke(); };
            R(label, t, t.Width, false);
            return t;
        }

        Segmented Seg(string label, Func<Dictionary<string, object>> target, string key, string[] values, string[] labels, string def, Action after = null)
        {
            var seg = new Segmented(labels);
            string curv = J.Str(target(), key, def) ?? def;
            seg.SetSilently(Math.Max(0, Array.IndexOf(values, curv)));
            seg.SelectedChanged += (s, e) =>
            {
                string v = values[seg.SelectedIndex];
                target()[key] = v.Length > 0 && char.IsDigit(v[0]) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? (object)d : v;
                Changed(); Commit(); after?.Invoke();
            };
            R(label, seg);
            return seg;
        }

        Dropdown Drop(string label, string[] labels, int index, Action<int> onChange)
        {
            var d = new Dropdown();
            d.Items.AddRange(labels);
            d.SelectedIndex = Math.Max(0, Math.Min(labels.Length - 1, index));
            d.SelectedIndexChanged += (s, e) => onChange(d.SelectedIndex);
            R(label, d);
            return d;
        }

        TextField Txt(string label, Func<Dictionary<string, object>> target, string key, string def = "")
        {
            var tf = new TextField(J.Str(target(), key, def));
            tf.TextChanged += (s, e) => { target()[key] = tf.Text; Changed(); };
            tf.Committed += (s, e) => Commit();
            R(label, tf);
            return tf;
        }

        FieldBox Field(string glyph, Func<Dictionary<string, object>> target, string key, double def, double step, int dec, double min = -100000, double max = 100000)
        {
            var fb = new FieldBox(glyph) { Step = step, Decimals = dec, Minimum = min, Maximum = max };
            fb.Value = J.Num(target(), key, def);
            fb.ValueChanged += (s, e) => { target()[key] = fb.Value; Changed(); };
            fb.ValueCommitted += (s, e) => Commit();
            return fb;
        }

        void PositionSection(bool withScale)
        {
            Section("Position");
            Func<Dictionary<string, object>> pos = () => J.EnsureObj(L, "position");
            var x = Field(Glyph.Swap, pos, "x", 0, 1, 1);
            var y = Field(Glyph.Resize, pos, "y", 0, 1, 1);
            cur.Controls.Add(new Columns(new FieldGroup("X", x), new FieldGroup("Y", y)));
            positionRefreshers.Add(() => { x.Value = J.Num(pos(), "x"); y.Value = J.Num(pos(), "y"); });
            if (withScale)
            {
                Func<Dictionary<string, object>> layer = () => J.EnsureObj(L, "layer");
                cur.Controls.Add(new FieldGroup("Scale", Field(Glyph.Fit, layer, "size", 1, 0.05, 2, 0.01, 50)));
            }
            var center = new FlatButton("Center layer", Glyph.Center, ButtonKind.Secondary);
            center.Click += (s, e) => { var p = pos(); p["x"] = 0.0; p["y"] = 0.0; Changed(); Commit(); RefreshPositionRows(); };
            cur.Controls.Add(new HStack(center));
        }

        // --- crosshair (model) layer ---

        static readonly string[] ShapeValues = { "rectangle", "triangle", "chevronIn", "chevronOut", "arc", "tShape", "image", "text" };
        static readonly string[] ShapeLabels = { "Rectangle", "Triangle", "Chevron (in)", "Chevron (out)", "Arc", "T-Shape", "Image", "Text" };

        void BuildModel(Dictionary<string, object> l)
        {
            Func<Dictionary<string, object>> line = () => J.EnsureObj(L, "line");
            var ln = line();
            string shape = J.Str(ln, "shape", "rectangle");
            Section("Lines");
            Drop("Shape", ShapeLabels, Array.IndexOf(ShapeValues, shape), i => { ApplyShapeChange(line(), ShapeValues[i]); Changed(); Commit(); BuildProps(); });
            int arms = J.Has(ln, "armCount") ? (int)J.Num(ln, "armCount", 4) : 4;
            Drop("Arms", new[] { "3", "4 (classic)", "5", "6", "7", "8" }, arms - 3, i =>
            {
                int n = i + 3;
                var lc = line();
                if (n == 4) { lc.Remove("armCount"); lc.Remove("visibleArms"); }
                else { lc["armCount"] = (double)n; lc.Remove("visibleArms"); }
                Changed(); Commit(); BuildProps();
            });
            BuildVisibility(line, arms);
            if (shape != "text")
            {
                Num("Length", line, "length", 0, 60, 1, 1, 0, 0, 2000);
                Num("Thickness", line, "thickness", 0, 30, 1, 1, 0, 0, 500);
            }
            Num("Gap", line, "offset", 0, 60, 1, 1, 0, 0, 2000);
            if (shape == "chevronIn" || shape == "chevronOut")
            {
                Num("Angle", line, "angle", 10, 170, 1, 0, 60);
                Num("Roundness", line, "roundness", 0, 1, 0.05, 2);
            }
            if (shape == "arc") Seg("Caps", line, "capStyle", new[] { "flat", "angled" }, new[] { "Flat", "Angled" }, "flat");
            if (shape == "tShape")
            {
                Num("Cap length", line, "capLength", 0, 40, 1, 1, 0, 0, 500);
                Num("Cap width", line, "capThickness", 0, 20, 1, 1, 0, 0, 500);
            }
            if (shape == "image")
            {
                var choose = new FlatButton("Choose image…", Glyph.Image);
                choose.Click += (s, e) =>
                {
                    var img = PickImage(out int w, out int h, out _);
                    if (img == null) return;
                    var lc = line();
                    lc["image"] = img;
                    lc["aspectRatio"] = w > 0 ? (double)h / w : 1;
                    Changed(); Commit();
                };
                R("Image", choose, choose.Width, false);
                Tog("Flip", line, "flipImage");
            }
            if (shape == "text")
            {
                Txt("Text", line, "text", "|");
                FontRow(line);
                Num("Size", line, "fontSize", 4, 80, 1, 0, 10, 1, 500);
                Seg("Weight", line, "fontWeight", new[] { "400", "700" }, new[] { "Regular", "Bold" }, "700");
                Num("Spacing", line, "letterSpacing", -5, 20, 0.5, 1);
            }
            Num("Opacity", line, "opacity", 0, 1, 0.01, 2, 1);
            if (shape != "image") Col("Color", line, "color", "#00FF66");
            Num("Rotation", line, "rotation", -180, 180, 1, 1, 0, -360, 360);
            Num("Blur", line, "blur", 0, 10, 0.1, 1, 0, 0, 50);

            Func<Dictionary<string, object>> dot = () => J.EnsureObj(L, "dot");
            string dshape = J.Str(dot(), "shape", "");
            var dotShapes = new[] { "", "square", "circle", "diamond", "ring" };
            Section("Center Dot");
            Drop("Shape", new[] { "None", "Square", "Circle", "Diamond", "Ring" }, Math.Max(0, Array.IndexOf(dotShapes, dshape)), i =>
            {
                var d = dot();
                d["shape"] = dotShapes[i];
                if (i > 0 && J.Num(d, "opacity") == 0) d["opacity"] = 1.0;
                if (i > 0 && J.Num(d, "diameter") == 0) d["diameter"] = 2.0;
                if (i == 4 && !J.Has(d, "thickness")) d["thickness"] = 1.0;
                Changed(); Commit(); BuildProps();
            });
            if (dshape != "")
            {
                Num("Size", dot, "diameter", 0, 40, 1, 1, 0, 0, 2000);
                if (dshape == "ring") Num("Ring width", dot, "thickness", 0.5, 10, 0.5, 1, 1, 0.5, 200);
                Num("Opacity", dot, "opacity", 0, 1, 0.01, 2, 1);
                Col("Color", dot, "color");
                Num("Blur", dot, "blur", 0, 10, 0.1, 1, 0, 0, 50);
            }

            Func<Dictionary<string, object>> o = () => J.EnsureObj(L, "outline");
            Section("Outline");
            Num("Thickness", o, "thickness", 0, 10, 1, 1, 0, 0, 200);
            Num("Opacity", o, "opacity", 0, 1, 0.01, 2);
            Col("Color", o, "color", "#000000");
            Num("Blur", o, "blur", 0, 10, 0.1, 1, 0, 0, 50);

            PositionSection(true);
        }

        void FontRow(Func<Dictionary<string, object>> target)
        {
            if (fontNames == null)
                using (var fc = new InstalledFontCollection()) fontNames = fc.Families.Select(f => f.Name).ToArray();
            string curf = J.Str(target(), "fontFamily", "Segoe UI");
            var names = fontNames.Contains(curf) ? fontNames : new[] { curf }.Concat(fontNames).ToArray();
            Drop("Font", names, Array.IndexOf(names, curf), i => { target()["fontFamily"] = names[i]; Changed(); Commit(); });
        }

        void ApplyShapeChange(Dictionary<string, object> line, string shape)
        {
            line["shape"] = shape;
            double th = J.Num(line, "thickness"), len = J.Num(line, "length");
            switch (shape)
            {
                case "tShape":
                    if (!(J.Num(line, "capLength") > 0 && J.Num(line, "capThickness") > 0)) { line["capLength"] = Math.Max(6, th * 3); line["capThickness"] = Math.Max(1, th); }
                    break;
                case "chevronIn":
                case "chevronOut":
                    if (!J.Has(line, "angle")) line["angle"] = 60.0;
                    if (!J.Has(line, "roundness")) line["roundness"] = 0.0;
                    break;
                case "image":
                    if (len == 0) line["length"] = 16.0;
                    if (th == 0) line["thickness"] = 16.0;
                    if (!J.Has(line, "image")) line["image"] = "";
                    break;
                case "text":
                    if (!J.Has(line, "text")) line["text"] = "|";
                    if (!J.Has(line, "fontSize")) line["fontSize"] = 10.0;
                    if (!J.Has(line, "fontFamily")) line["fontFamily"] = "Segoe UI";
                    if (!J.Has(line, "fontWeight")) line["fontWeight"] = 700.0;
                    if (!J.Has(line, "letterSpacing")) line["letterSpacing"] = 0.0;
                    break;
            }
            if ((shape == "rectangle" || shape == "triangle" || shape == "arc") && (len == 0 || th == 0))
            {
                if (len == 0) line["length"] = 6.0;
                if (th == 0) line["thickness"] = 2.0;
            }
        }

        void BuildVisibility(Func<Dictionary<string, object>> line, int arms)
        {
            MultiToggle mt;
            if (arms == 4)
            {
                var dirs = new[] { "top", "right", "bottom", "left" };
                var vis0 = J.StrList(line(), "visible");
                mt = new MultiToggle(new[] { "T", "R", "B", "L" }, dirs.Select(d => vis0.Contains(d)).ToArray());
                mt.Toggled += i => { line()["visible"] = dirs.Where((d, k) => mt.Checked[k]).Cast<object>().ToList(); Changed(); Commit(); };
            }
            else
            {
                var list = J.List(line(), "visibleArms");
                var state = Enumerable.Range(0, arms).Select(k => list == null || list.Any(x => (int)J.ToNum(x, -1) == k)).ToArray();
                mt = new MultiToggle(Enumerable.Range(1, arms).Select(k => k.ToString()).ToArray(), state);
                mt.Toggled += i =>
                {
                    var on = Enumerable.Range(0, arms).Where(k => mt.Checked[k]).ToList();
                    if (on.Count == arms) line().Remove("visibleArms");
                    else line()["visibleArms"] = on.Select(x => (object)(double)x).ToList();
                    Changed(); Commit();
                };
            }
            R("Visible", mt);
        }

        // --- image / drawing ---

        void BuildImage(Dictionary<string, object> l)
        {
            Func<Dictionary<string, object>> t = () => L;
            bool isDraw = Defaults.LayerType(l) == "draw";
            Section(isDraw ? "Drawing" : "Image");
            var choose = new FlatButton(isDraw ? "Replace drawing…" : "Replace image…", Glyph.Image);
            choose.Click += (s, e) =>
            {
                var img = PickImage(out int w, out int h, out string nm);
                if (img == null) return;
                L["image"] = img; L["width"] = (double)w; L["height"] = (double)h; L["aspectRatio"] = w > 0 ? (double)h / w : 1;
                if (!isDraw && nm != null) L["name"] = nm;
                Changed(); Commit(); BuildProps();
            };
            cur.Controls.Add(new HStack(choose));
            var img0 = ImageCache.Get(J.Str(l, "image"));
            if (img0 != null && img0.Failed) Note("⚠ " + img0.Error);
            SliderInput wIn = null, hIn = null;
            wIn = Num("Width", t, "width", 1, 256, 1, 0, 32, 1, 4000, () =>
            {
                if (J.Bool(L, "lockAspectRatio", true) && J.Num(L, "aspectRatio") > 0) { L["height"] = Math.Round(J.Num(L, "width") * J.Num(L, "aspectRatio")); hIn.Value = J.Num(L, "height"); }
            });
            hIn = Num("Height", t, "height", 1, 256, 1, 0, 32, 1, 4000, () =>
            {
                if (J.Bool(L, "lockAspectRatio", true) && J.Num(L, "aspectRatio") > 0) { L["width"] = Math.Round(J.Num(L, "height") / J.Num(L, "aspectRatio")); wIn.Value = J.Num(L, "width"); }
            });
            if (!isDraw) Tog("Lock ratio", t, "lockAspectRatio", true);
            Num("Opacity", t, "opacity", 0, 1, 0.01, 2, 1);
            if (!isDraw)
            {
                Num("Rotation", t, "rotation", -180, 180, 1, 0, 0, -360, 360);
                var px = new ToggleSwitch();
                px.SetSilently(J.Str(l, "name") == "Drawing");
                px.CheckedChanged += (s, e) => { L["name"] = px.Checked ? "Drawing" : "Image"; Changed(); Commit(); };
                R("Pixelated", px, px.Width, false);
            }
            PositionSection(true);
        }

        // --- shape ---

        void BuildShape(Dictionary<string, object> l)
        {
            Func<Dictionary<string, object>> t = () => L;
            Section("Shape");
            Seg("Type", t, "shape", new[] { "square", "circle", "triangle" }, new[] { "Square", "Circle", "Triangle" }, "square", () => BuildProps());
            Num("Width", t, "width", 0, 200, 1, 1, 10, 0, 4000);
            Num("Height", t, "height", 0, 200, 1, 1, 10, 0, 4000);
            if (J.Str(l, "shape") == "square") Num("Corners", t, "cornerRadius", 0, 50, 0.5, 1);
            Num("Opacity", t, "opacity", 0, 1, 0.01, 2, 1);
            Col("Color", t, "color");
            Num("Rotation", t, "rotation", -180, 180, 1, 0, 0, -360, 360);
            Num("Blur", t, "blur", 0, 10, 0.1, 1, 0, 0, 50);
            Func<Dictionary<string, object>> anchor = () => J.EnsureObj(L, "anchor");
            Seg("Anchor X", anchor, "x", new[] { "left", "center", "right" }, new[] { "Left", "Center", "Right" }, "center");
            Seg("Anchor Y", anchor, "y", new[] { "top", "center", "bottom" }, new[] { "Top", "Center", "Bottom" }, "center");
            Func<Dictionary<string, object>> o = () => J.EnsureObj(L, "outline");
            Section("Outline");
            Num("Thickness", o, "thickness", 0, 10, 1, 1, 0, 0, 200);
            Num("Opacity", o, "opacity", 0, 1, 0.01, 2);
            Col("Color", o, "color", "#000000");
            Num("Blur", o, "blur", 0, 10, 0.1, 1, 0, 0, 50);
            PositionSection(false);
        }

        // --- text ---

        void BuildText(Dictionary<string, object> l)
        {
            Func<Dictionary<string, object>> t = () => L;
            Section("Text");
            Txt("Text", t, "text", "Text");
            FontRow(t);
            Num("Size", t, "fontSize", 4, 120, 1, 0, 12, 1, 1000);
            Seg("Weight", t, "fontWeight", new[] { "400", "700" }, new[] { "Regular", "Bold" }, "400");
            Num("Spacing", t, "letterSpacing", -5, 30, 0.5, 1);
            Num("Opacity", t, "opacity", 0, 1, 0.01, 2, 1);
            Col("Color", t, "color");
            Num("Rotation", t, "rotation", -180, 180, 1, 0, 0, -360, 360);
            Num("Blur", t, "blur", 0, 10, 0.1, 1, 0, 0, 50);
            Section("Layout");
            Seg("Layout", t, "layout", new[] { "straight", "circle" }, new[] { "Straight", "Circle" }, "straight", () => BuildProps());
            if (J.Str(l, "layout") == "circle")
            {
                Num("Radius", t, "radius", 4, 200, 1, 0, 30, 1, 2000);
                Tog("Repeat", t, "repeatToFill");
                Num("Padding", t, "repeatPadding", 0, 50, 1, 0);
            }
            Func<Dictionary<string, object>> o = () => J.EnsureObj(L, "outline");
            Section("Outline");
            Num("Thickness", o, "thickness", 0, 10, 1, 1, 0, 0, 200);
            Num("Opacity", o, "opacity", 0, 1, 0.01, 2);
            Col("Color", o, "color", "#000000");
            PositionSection(false);
        }

        // --- animate ---

        static readonly string[] EaseValues = Easing.Names;
        static readonly HashSet<string> TimingKeys = new HashSet<string> { "duration", "startDelay", "easing", "mouseButton", "pressType", "releaseBehavior", "direction", "loop", "version", "stages", "triggerTimelines", "tShapeWhenFiring", "firingOffset", "bloomDirection", "finalOpacity", "recoilPattern",
            "recoilScale", "recoilRpm", "recoilStable", "recoilFx", "recoilFxEnd", "recoilInvert", "recoilMirror", "recoilLag", "recoilOff", "recoilFactor" };

        Dictionary<string, object> Fo() => J.EnsureObj(L, "firingOptions");

        Dictionary<string, object> StageObj()
        {
            var fo = Fo();
            if (stageIndex == 0) return fo;
            var list = J.List(fo, "stages");
            if (list == null || stageIndex - 1 >= list.Count) return fo;
            if (!(list[stageIndex - 1] is Dictionary<string, object> d)) { d = J.O(); list[stageIndex - 1] = d; }
            return d;
        }

        Func<Dictionary<string, object>> V2(Func<Dictionary<string, object>> t) => () => { Fo()["version"] = 2.0; return t(); };

        Dictionary<string, object> StageBasis(Dictionary<string, object> l)
        {
            var baseLayer = J.CloneObj(l);
            baseLayer.Remove("firingOptions");
            var fo = J.ObjOrEmpty(l, "firingOptions");
            var result = baseLayer;
            if (stageIndex >= 1) result = J.Merge(result, fo, TimingKeys);
            var stages = J.List(fo, "stages") ?? new List<object>();
            for (int i = 0; i < stageIndex - 1 && i < stages.Count; i++)
                if (stages[i] is Dictionary<string, object> s) result = J.Merge(result, s, TimingKeys);
            return result;
        }

        static bool HasOverrides(Dictionary<string, object> fo)
        {
            foreach (var kv in fo)
            {
                if (kv.Value == null) continue;
                if (kv.Key == "stages") { if ((kv.Value as List<object>)?.Count > 0) return true; continue; }
                if (TimingKeys.Contains(kv.Key)) continue;
                if (kv.Value is Dictionary<string, object> d) { if (d.Values.Any(v => v != null)) return true; }
                else return true;
            }
            return false;
        }

        static void ClearOverrides(Dictionary<string, object> fo)
        {
            foreach (var k in new[] { "dot", "line", "outline", "position", "layer" }) fo[k] = J.O();
            foreach (var k in new[] { "opacity", "width", "height", "rotation", "color", "fontSize", "letterSpacing", "stages", "recoilPattern" }) fo.Remove(k);
            fo["firingOffset"] = 0.0;
            fo["tShapeWhenFiring"] = false;
        }

        void ConvertLegacy(Dictionary<string, object> l)
        {
            var fo = J.EnsureObj(l, "firingOptions");
            var line = J.ObjOrEmpty(l, "line");
            var foLine = J.EnsureObj(fo, "line");
            double fof = J.Num(fo, "firingOffset");
            if (fof != 0 && !J.Has(foLine, "offset"))
                foLine["offset"] = Math.Max(0, J.Num(line, "offset") + (J.Str(fo, "bloomDirection") == "inward" ? -1 : 1) * fof * 2);
            if (J.Bool(fo, "tShapeWhenFiring") && !J.Has(foLine, "visible"))
                foLine["visible"] = J.StrList(line, "visible").Where(v => v != "top").Cast<object>().ToList();
            double fin = J.Num(fo, "finalOpacity", 1);
            if (fin != 1)
                foreach (var k in new[] { "line", "dot", "outline" })
                {
                    var o = J.EnsureObj(fo, k);
                    if (!J.Has(o, "opacity")) o["opacity"] = J.Num(J.Obj(l, k), "opacity") * fin;
                }
            fo["firingOffset"] = 0.0;
            fo["tShapeWhenFiring"] = false;
            fo["finalOpacity"] = 1.0;
            if (!J.Has(fo, "pressType")) fo["pressType"] = "hold";
            if (!J.Has(fo, "releaseBehavior")) fo["releaseBehavior"] = "reverse";
            fo["version"] = 2.0;
        }

        void BuildAnimate(Dictionary<string, object> l)
        {
            var fo = J.EnsureObj(l, "firingOptions");
            bool isModel = Defaults.LayerType(l) == "model";
            if (isModel && J.Num(fo, "version", 1) < 2 && (J.Num(fo, "firingOffset") != 0 || J.Bool(fo, "tShapeWhenFiring") || J.Num(fo, "finalOpacity", 1) != 1))
            {
                Section("Classic animation");
                Note("This design uses a classic firing animation (bloom " + J.Num(fo, "firingOffset") + " px" + (J.Bool(fo, "tShapeWhenFiring") ? ", T-shape when firing" : "") + "). It plays as-is; convert it to edit it with the full tools.");
                var conv = new FlatButton("Convert to editable animation", Glyph.Refresh, ButtonKind.Secondary);
                conv.Click += (s, e) => { ConvertLegacy(l); Changed(); Commit(); BuildProps(); };
                cur.Controls.Add(new HStack(conv));
                return;
            }
            var stages = J.List(fo, "stages") ?? new List<object>();
            stageIndex = Math.Max(0, Math.Min(stageIndex, stages.Count));

            Section("Trigger");
            var trig = new Segmented(new[] { "Off", "Fire", "Aim", "Auto" });
            string mb = J.Str(fo, "mouseButton", "left");
            bool hasAny = HasOverrides(fo);
            trig.SetSilently(!hasAny && mb != "autoplay" ? 0 : mb == "right" ? 2 : mb == "autoplay" ? 3 : 1);
            trig.SelectedChanged += (s, e) =>
            {
                var f = Fo();
                f["version"] = 2.0;
                switch (trig.SelectedIndex)
                {
                    case 0: ClearOverrides(f); f["mouseButton"] = "left"; break;
                    case 1: f["mouseButton"] = "left"; break;
                    case 2: f["mouseButton"] = "right"; break;
                    case 3: f["mouseButton"] = "autoplay"; if (!J.Has(f, "loop")) f["loop"] = true; break;
                }
                if (trig.SelectedIndex > 0 && J.Num(f, "duration") == 0) f["duration"] = 0.2;
                Changed(); Commit(); BuildProps();
            };
            R("Plays on", trig);
            if (trig.SelectedIndex == 0)
            {
                Note("Pick what starts the animation: the Fire key, the Aim key (Keybinds page) or Autoplay. Then choose what changes, or apply a recoil pattern below.");
                RecoilSection();
                return;
            }
            Func<Dictionary<string, object>> f0 = Fo;
            if (mb != "autoplay")
            {
                Seg("Press", V2(f0), "pressType", new[] { "press", "hold" }, new[] { "Single", "Hold" }, "press", () => BuildProps());
                if (J.Str(fo, "pressType", "press") == "hold")
                    Seg("Release", V2(f0), "releaseBehavior", new[] { "reset", "reverse", "pause" }, new[] { "Reset", "Reverse", "Pause" }, "reset");
            }
            Seg("Direction", V2(f0), "direction", new[] { "normal", "alternate" }, new[] { "Normal", "Alternate" }, "normal");
            Tog("Loop", V2(f0), "loop");

            Section("Timing");
            var stageNames = new List<string> { "Stage 1" };
            for (int i = 0; i < stages.Count; i++) stageNames.Add("Stage " + (i + 2));
            var stageDrop = new Dropdown { Width = Theme.S(104) };
            stageDrop.Items.AddRange(stageNames.ToArray());
            stageDrop.SelectedIndex = stageIndex;
            stageDrop.SelectedIndexChanged += (s, e) => { stageIndex = stageDrop.SelectedIndex; BuildProps(); };
            var addStage = new FlatButton("", Glyph.Add, ButtonKind.Secondary) { Width = Theme.S(34) };
            addStage.Click += (s, e) =>
            {
                var f = Fo();
                var list = J.List(f, "stages") ?? new List<object>();
                list.Add(J.O("duration", J.Num(f, "duration", 0.2), "easing", J.Str(f, "easing", "linear"), "dot", J.O(), "line", J.O(), "outline", J.O(), "position", J.O(), "layer", J.O()));
                f["stages"] = list;
                f["version"] = 2.0;
                stageIndex = list.Count;
                Changed(); Commit(); BuildProps();
            };
            var removeStage = new FlatButton("", Glyph.Delete, ButtonKind.Ghost) { Width = Theme.S(34), Enabled = stageIndex > 0 };
            removeStage.Click += (s, e) =>
            {
                var list = J.List(Fo(), "stages");
                if (list == null || stageIndex == 0) return;
                list.RemoveAt(stageIndex - 1);
                stageIndex--;
                Changed(); Commit(); BuildProps();
            };
            R("Stage", new HStack(stageDrop, addStage, removeStage) { Spacing = Theme.S(6) });
            if (J.Str(fo, "recoilPattern") is string rp) Note((stages.Count + 1) + " stages from the " + rp + " recoil pattern.");
            Func<Dictionary<string, object>> st = StageObj;
            Num("Duration", V2(st), "duration", 0, 3, 0.01, 2, 0, 0, 60);
            Num("Delay", V2(st), "startDelay", 0, 3, 0.01, 2, 0, 0, 60);
            string ease = J.Str(st(), "easing", "linear");
            Drop("Easing", EaseValues.Select(PrettyEase).ToArray(), Math.Max(0, Array.IndexOf(EaseValues, ease)), i => { st()["easing"] = EaseValues[i]; Fo()["version"] = 2.0; Changed(); Commit(); });

            Section(stageIndex == 0 ? "Animate to" : "Then animate to");
            Note("Switch on what changes. Values are the end of this stage.");
            var basis = StageBasis(l);
            switch (Defaults.LayerType(l))
            {
                case "model":
                    OverrideNum(st, "line", "length", "Length", 0, 60, 1, 1, basis);
                    OverrideNum(st, "line", "thickness", "Thickness", 0, 30, 1, 1, basis);
                    OverrideNum(st, "line", "offset", "Gap", 0, 60, 1, 1, basis);
                    OverrideNum(st, "line", "opacity", "Line opacity", 0, 1, 0.01, 2, basis);
                    OverrideColor(st, "line", "color", "Line color", basis);
                    OverrideNum(st, "line", "rotation", "Rotation", -360, 360, 1, 0, basis);
                    OverrideVisible(st, basis);
                    OverrideNum(st, "dot", "diameter", "Dot size", 0, 40, 1, 1, basis);
                    OverrideNum(st, "dot", "opacity", "Dot opacity", 0, 1, 0.01, 2, basis);
                    OverrideColor(st, "dot", "color", "Dot color", basis);
                    OverrideNum(st, "outline", "thickness", "Outline", 0, 10, 1, 1, basis);
                    OverrideNum(st, "outline", "opacity", "Outline op.", 0, 1, 0.01, 2, basis);
                    OverrideColor(st, "outline", "color", "Outline color", basis);
                    OverrideNum(st, "position", "x", "Position X", -200, 200, 1, 0, basis);
                    OverrideNum(st, "position", "y", "Position Y", -200, 200, 1, 0, basis);
                    OverrideNum(st, "layer", "size", "Scale", 0.1, 5, 0.05, 2, basis);
                    break;
                case "image":
                case "draw":
                    OverrideNum(st, null, "opacity", "Opacity", 0, 1, 0.01, 2, basis);
                    OverrideNum(st, null, "width", "Width", 1, 256, 1, 0, basis);
                    OverrideNum(st, null, "height", "Height", 1, 256, 1, 0, basis);
                    OverrideNum(st, null, "rotation", "Rotation", -360, 360, 1, 0, basis);
                    OverrideNum(st, "position", "x", "Position X", -200, 200, 1, 0, basis);
                    OverrideNum(st, "position", "y", "Position Y", -200, 200, 1, 0, basis);
                    break;
                case "shape":
                    OverrideNum(st, null, "width", "Width", 0, 200, 1, 1, basis);
                    OverrideNum(st, null, "height", "Height", 0, 200, 1, 1, basis);
                    OverrideNum(st, null, "opacity", "Opacity", 0, 1, 0.01, 2, basis);
                    OverrideColor(st, null, "color", "Color", basis);
                    OverrideNum(st, null, "rotation", "Rotation", -360, 360, 1, 0, basis);
                    OverrideNum(st, "position", "x", "Position X", -200, 200, 1, 0, basis);
                    OverrideNum(st, "position", "y", "Position Y", -200, 200, 1, 0, basis);
                    break;
                case "text":
                    OverrideNum(st, null, "fontSize", "Size", 4, 120, 1, 0, basis);
                    OverrideNum(st, null, "opacity", "Opacity", 0, 1, 0.01, 2, basis);
                    OverrideColor(st, null, "color", "Color", basis);
                    OverrideNum(st, null, "rotation", "Rotation", -360, 360, 1, 0, basis);
                    OverrideNum(st, "position", "x", "Position X", -200, 200, 1, 0, basis);
                    OverrideNum(st, "position", "y", "Position Y", -200, 200, 1, 0, basis);
                    break;
            }
            RecoilSection();
        }

        /// <summary>Recoil tracking: writes a weapon spray pattern into this layer's fire animation.</summary>
        void RecoilSection()
        {
            Section("Recoil Pattern");
            var current = Recoil.Find(J.Str(Fo(), "recoilPattern"));
            bool isTracker = current != null;
            Note(isTracker
                ? "This layer follows the selected weapon's spray while you hold Fire. Pick any gun — the switch applies straight away. Patterns are approximate; adjust Scale for your resolution and FOV."
                : "Make this layer follow a weapon's spray while you hold Fire, then snap back on release. Patterns are approximate; adjust Scale for your resolution and FOV.");
            string game = current?.Game ?? Recoil.Games[0];
            var gameDrop = Drop("Game", Recoil.Games, Array.IndexOf(Recoil.Games, game), i => { });
            var weapons = Recoil.ForGame(game);
            var weaponDrop = Drop("Weapon", weapons.Select(p => p.Label + (p.HandTuned ? "  ★" : "")).ToArray(), Math.Max(0, weapons.FindIndex(p => p.Key == current?.Key)), i => { });
            var scale = new FieldBox(Glyph.Fit) { Step = 0.05, Decimals = 2, Minimum = 0.1, Maximum = 10 };
            scale.Value = isTracker ? J.Num(Fo(), "recoilScale", 1) : 1;
            var rpm = new FieldBox(Glyph.Clock) { Step = 10, Decimals = 0, Minimum = 30, Maximum = 2000 };
            Func<RecoilPattern> sel = () => weapons[Math.Max(0, weaponDrop.SelectedIndex)];
            rpm.Value = isTracker && J.Num(Fo(), "recoilRpm") > 0 ? J.Num(Fo(), "recoilRpm") : sel().Rpm;
            var stable = new FieldBox(Glyph.Target) { Step = 1, Decimals = 0, Minimum = 1, Maximum = 15 };
            stable.Value = isTracker && J.Num(Fo(), "recoilStable") >= 1 ? J.Num(Fo(), "recoilStable") : sel().StableShots;
            gameDrop.SelectedIndexChanged += (s, e) =>
            {
                weapons = Recoil.ForGame(Recoil.Games[gameDrop.SelectedIndex]);
                weaponDrop.Items.Clear();
                weaponDrop.Items.AddRange(weapons.Select(p => p.Label + (p.HandTuned ? "  ★" : "")).ToArray());
                weaponDrop.SelectedIndex = 0;
            };
            weaponDrop.SelectedIndexChanged += (s, e) =>
            {
                if (weaponDrop.SelectedIndex < 0) return;
                rpm.Value = sel().Rpm;
                stable.Value = sel().StableShots;
                if (!isTracker) return;
                // switching weapon on an existing recoil crosshair re-targets every recoil layer immediately
                foreach (var layer in layers.OfType<Dictionary<string, object>>())
                {
                    var fo = J.Obj(layer, "firingOptions");
                    if (J.Str(fo, "recoilPattern") == null) continue;
                    Recoil.Apply(layer, sel(), layer == L ? scale.Value : J.Num(fo, "recoilScale", 1));
                }
                Changed(); Commit();
                MainForm.Instance.ShowToast("Weapon: " + sel().Name + " (" + sel().Game + ")", Glyph.Recoil);
            };
            cur.Controls.Add(new Columns(new FieldGroup("Scale", scale), new FieldGroup("Fire rate (RPM)", rpm)));
            var lag = new FieldBox(Glyph.Layers) { Step = 1, Decimals = 0, Minimum = 0, Maximum = 10 };
            lag.Value = isTracker ? J.Num(Fo(), "recoilLag") : 0;
            cur.Controls.Add(new Columns(new FieldGroup("Accurate first shots", stable), new FieldGroup("Trail delay (shots)", lag)));
            var modeDrop = Drop("Direction", new[] { "Follow the spray", "Pull guide (inverted)", "Mirror sideways" },
                isTracker ? (J.Bool(Fo(), "recoilInvert") ? 1 : J.Bool(Fo(), "recoilMirror") ? 2 : 0) : 0, i => { });
            var apply = new FlatButton(isTracker ? "Re-apply with these settings" : "Apply recoil pattern", Glyph.Recoil, ButtonKind.Primary);
            apply.Click += (s, e) =>
            {
                var f = Fo();
                f["recoilInvert"] = modeDrop.SelectedIndex == 1;
                f["recoilMirror"] = modeDrop.SelectedIndex == 2;
                f["recoilLag"] = lag.Value;
                Recoil.Apply(L, sel(), scale.Value, rpm.Value, (int)stable.Value);
                Changed(); Commit();
                MainForm.Instance.ShowToast("Recoil pattern applied — hold left mouse on the canvas to test", Glyph.Recoil);
                BuildProps();
            };
            var editPattern = new FlatButton(isTracker ? Core.L.T("Edit pattern…") : Core.L.T("New pattern…"), Glyph.Edit, ButtonKind.Secondary);
            editPattern.Click += (s, e) =>
            {
                string before = isTracker ? current.Key : null;
                var made = PatternEditorDialog.ShowFor(Main, isTracker ? current : null, newPattern: !isTracker);
                if (made == null) return;
                // re-point this design's trackers (the editor already updated saved crosshairs)
                foreach (var layer in layers.OfType<Dictionary<string, object>>())
                {
                    var fo = J.Obj(layer, "firingOptions");
                    string k = J.Str(fo, "recoilPattern");
                    if (layer == L && !isTracker) Recoil.Apply(layer, made, scale.Value);
                    else if (k != null && (k == before || k == made.Key)) Recoil.Apply(layer, made, J.Num(fo, "recoilScale", 1));
                }
                Changed(); Commit();
                BuildProps();
            };
            cur.Controls.Add(new HStack(apply, editPattern));
            Note("Accurate first shots = bullets that land dead center before the recoil kicks in (each gun has its own default). Trail delay makes this layer follow a few shots behind (for trails). Pull guide moves opposite to the spray — the way to drag your mouse. ★ = hand-tuned pattern. Tip: bind Next / Previous Weapon on the Keybinds page to switch guns in game.");
        }

        static string PrettyEase(string e)
        {
            if (e == "linear") return "Linear";
            var s = e.Replace("ease", "");
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) { if (char.IsUpper(c) && sb.Length > 0) sb.Append(' '); sb.Append(c); }
            return sb.ToString();
        }

        Dictionary<string, object> OverrideTarget(Func<Dictionary<string, object>> st, string group) => group == null ? st() : J.EnsureObj(st(), group);

        void OverrideNum(Func<Dictionary<string, object>> st, string group, string key, string label, double min, double max, double step, int dec, Dictionary<string, object> basis)
        {
            var target = OverrideTarget(st, group);
            bool on = J.Has(target, key);
            double baseVal = J.Num(group == null ? basis : J.Obj(basis, group), key, key == "size" ? 1 : 0);
            var si = new SliderInput(min, max, step, dec, -10000, 10000) { Enabled = on };
            si.Value = on ? J.Num(target, key) : baseVal;
            si.ValueChanged += (s, e) => { OverrideTarget(st, group)[key] = si.Value; Fo()["version"] = 2.0; Changed(); };
            si.ValueCommitted += (s, e) => Commit();
            AddOverrideRow(label, si, on, isOn =>
            {
                var t = OverrideTarget(st, group);
                if (isOn) t[key] = si.Value; else t.Remove(key);
                si.Enabled = isOn;
                Fo()["version"] = 2.0;
                Changed(); Commit();
            });
        }

        void OverrideColor(Func<Dictionary<string, object>> st, string group, string key, string label, Dictionary<string, object> basis)
        {
            var target = OverrideTarget(st, group);
            bool on = !string.IsNullOrEmpty(J.Str(target, key));
            var cb = new ColorButton { Enabled = on, Color = ColorUtil.Parse(on ? J.Str(target, key) : J.Str(group == null ? basis : J.Obj(basis, group), key, "#FFFFFF")) };
            cb.ColorChanged += (s, e) => { OverrideTarget(st, group)[key] = cb.Hex; Fo()["version"] = 2.0; Changed(); };
            cb.ColorCommitted += (s, e) => Commit();
            AddOverrideRow(label, cb, on, isOn =>
            {
                var t = OverrideTarget(st, group);
                if (isOn) t[key] = cb.Hex; else t.Remove(key);
                cb.Enabled = isOn;
                Changed(); Commit();
            });
        }

        void OverrideVisible(Func<Dictionary<string, object>> st, Dictionary<string, object> basis)
        {
            var bline = J.ObjOrEmpty(basis, "line");
            if (J.Has(bline, "armCount") && (int)J.Num(bline, "armCount", 4) != 4) return;
            var target = OverrideTarget(st, "line");
            bool on = J.List(target, "visible") != null;
            var current = on ? J.StrList(target, "visible") : J.StrList(bline, "visible");
            var dirs = new[] { "top", "right", "bottom", "left" };
            var mt = new MultiToggle(new[] { "T", "R", "B", "L" }, dirs.Select(d => current.Contains(d)).ToArray()) { Enabled = on };
            mt.Toggled += i =>
            {
                OverrideTarget(st, "line")["visible"] = dirs.Where((d, k) => mt.Checked[k]).Cast<object>().ToList();
                Fo()["version"] = 2.0;
                Changed(); Commit();
            };
            AddOverrideRow("Visible", mt, on, isOn =>
            {
                var t = OverrideTarget(st, "line");
                var basisVis = J.StrList(bline, "visible");
                if (isOn) t["visible"] = basisVis.Cast<object>().ToList(); else t.Remove("visible");
                for (int k = 0; k < 4; k++) mt.Checked[k] = basisVis.Contains(dirs[k]);
                mt.Enabled = isOn;
                mt.Invalidate();
                Fo()["version"] = 2.0;
                Changed(); Commit();
            });
        }

        void AddOverrideRow(string label, Control editor, bool on, Action<bool> toggled)
        {
            var row = new OverrideRow(label, editor, on);
            row.Toggle.CheckedChanged += (s, e) => toggled(row.Toggle.Checked);
            cur.Controls.Add(row);
        }

        sealed class OverrideRow : Panel
        {
            public readonly ToggleSwitch Toggle = new ToggleSwitch();
            readonly Control editor;
            readonly string label;

            public OverrideRow(string label, Control editor, bool on)
            {
                this.label = label;
                this.editor = editor;
                Toggle.SetSilently(on);
                Toggle.Size = new Size(Theme.S(32), Theme.S(18));
                Height = Theme.S(36);
                BackColor = Theme.Chrome;
                Controls.Add(Toggle);
                Controls.Add(editor);
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                Toggle.CheckedChanged += (s, e) => Invalidate();
            }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                Toggle.Location = new Point(0, (Height - Toggle.Height) / 2);
                int left = Theme.S(124);
                editor.SetBounds(left, (Height - editor.Height) / 2, Width - left, editor.Height);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                Theme.DrawText(e.Graphics, label, Theme.Small, Toggle.Checked ? Theme.Text : Theme.TextDim, new Rectangle(Theme.S(40), 0, Theme.S(84), Height));
            }
        }
    }

    /// <summary>Floating "Layers" panel over the canvas: thumbnail rows, visibility, rename, dashed "New Layer".</summary>
    public sealed class LayersPanel : Control
    {
        List<object> layers = new List<object>();
        int selected, hover = -1, scroll;
        bool collapsed;
        public event Action<int> SelectedChanged, VisibilityToggled;
        public event Action<int, Point> RightClicked;
        public event Action<int, string> Renamed;
        public event Action AddClicked;
        const int MaxRows = 7;

        public LayersPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Chrome;
            Width = Theme.S(236);
            Fit();
        }

        int HeaderH => Theme.S(48);
        int RowH => Theme.S(46);
        int VisibleRows => Math.Min(MaxRows, layers.Count);

        void Fit()
        {
            int h = HeaderH + (collapsed ? 0 : VisibleRows * (RowH + Theme.S(4)) + Theme.S(56));
            if (Height != h) Height = h;
            UpdateRegion();
        }

        void UpdateRegion()
        {
            using (var p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.SF(10))) Region = new Region(p);
        }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); UpdateRegion(); }

        public void SetItems(List<object> l, int sel)
        {
            layers = l;
            selected = sel;
            int row = layers.Count - 1 - selected;
            if (row < scroll) scroll = row;
            if (row >= scroll + MaxRows) scroll = row - MaxRows + 1;
            scroll = Math.Max(0, Math.Min(scroll, Math.Max(0, layers.Count - MaxRows)));
            Fit();
            Invalidate();
        }

        Rectangle RowRect(int visualRow) => new Rectangle(Theme.S(10), HeaderH + (visualRow - scroll) * (RowH + Theme.S(4)), Width - Theme.S(20), RowH);
        Rectangle AddRect => new Rectangle(Theme.S(10), HeaderH + VisibleRows * (RowH + Theme.S(4)) + Theme.S(4), Width - Theme.S(20), Theme.S(40));

        int IndexAt(Point p)
        {
            for (int row = scroll; row < Math.Min(layers.Count, scroll + MaxRows); row++)
                if (RowRect(row).Contains(p)) return layers.Count - 1 - row;
            return -1;
        }

        Rectangle EyeRect(Rectangle r) => new Rectangle(r.Right - Theme.S(32), r.Y, Theme.S(28), r.Height);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = collapsed ? -1 : IndexAt(e.Location);
            if (!collapsed && AddRect.Contains(e.Location)) i = -2;
            if (i != hover) { hover = i; Invalidate(); }
            Cursor = i != -1 || e.Y < HeaderH ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Y < HeaderH) { collapsed = !collapsed; Fit(); Invalidate(); return; }
            if (AddRect.Contains(e.Location)) { AddClicked?.Invoke(); return; }
            int i = IndexAt(e.Location);
            if (i < 0) return;
            if (e.Button == MouseButtons.Right) { RightClicked?.Invoke(i, e.Location); return; }
            if (EyeRect(RowRect(layers.Count - 1 - i)).Contains(e.Location)) { VisibilityToggled?.Invoke(i); return; }
            SelectedChanged?.Invoke(i);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int i = IndexAt(e.Location);
            if (i < 0) return;
            var name = InputDialog.Ask(FindForm(), "Rename layer", "Layer name (leave empty for automatic)", J.Str(layers[i], "layerName", ""));
            if (name != null) Renamed?.Invoke(i, name);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll = Math.Max(0, Math.Min(Math.Max(0, layers.Count - MaxRows), scroll - Math.Sign(e.Delta)));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Chrome);
            Theme.Smooth(g);
            Theme.StrokeRound(g, Theme.BorderStrong, new RectangleF(0, 0, Width, Height), Theme.SF(10));
            Theme.DrawIcon(g, Glyph.Layers, Theme.Icon, Theme.TextDim, new Rectangle(Theme.S(16), 0, Theme.S(20), HeaderH));
            Theme.DrawText(g, "Layers", Theme.H3, Theme.Text, new Rectangle(Theme.S(44), 0, Theme.S(80), HeaderH));
            int lw = Theme.TextWidth("Layers", Theme.H3);
            var chip = new RectangleF(Theme.S(52) + lw, (HeaderH - Theme.S(22)) / 2f, Theme.S(22), Theme.S(22));
            Theme.FillRound(g, Theme.Surface3, chip, Theme.SF(11));
            Theme.DrawText(g, layers.Count.ToString(), Theme.SmallBold, Theme.Text, Rectangle.Round(chip), Theme.Center);
            Theme.DrawIcon(g, collapsed ? Glyph.ChevronDown : Glyph.ChevronUp, Theme.IconTiny, Theme.TextDim, new Rectangle(Width - Theme.S(34), 0, Theme.S(20), HeaderH));
            if (collapsed) return;
            for (int row = scroll; row < Math.Min(layers.Count, scroll + MaxRows); row++)
            {
                int i = layers.Count - 1 - row;
                var r = RowRect(row);
                var l = layers[i] as Dictionary<string, object>;
                bool sel = i == selected, hidden = J.Bool(l, "hidden");
                if (sel) Theme.FillRound(g, Theme.Surface3, r, Theme.SF(7));
                else if (i == hover) Theme.FillRound(g, Theme.Surface, r, Theme.SF(7));
                var thumb = new Rectangle(r.X + Theme.S(8), r.Y + (r.Height - Theme.S(30)) / 2, Theme.S(30), Theme.S(30));
                Theme.FillRound(g, Theme.Well, thumb, Theme.SF(5));
                var bmp = Thumbnails.Get("layer:" + Json.Serialize(l).GetHashCode(), new List<object> { l }, Theme.S(28));
                var stt = g.Save();
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImageUnscaled(bmp, thumb.X + 1, thumb.Y + 1);
                g.Restore(stt);
                Theme.Smooth(g);
                var fg = hidden ? Theme.TextMute : sel ? Theme.Text : Theme.Blend(Theme.Text, Theme.Chrome, .1);
                Theme.DrawText(g, Defaults.LayerName(l, i), sel ? Theme.BodyMedium : Theme.Body, fg, new Rectangle(thumb.Right + Theme.S(12), r.Y, r.Width - Theme.S(90), r.Height));
                if (hidden || i == hover) Theme.DrawIcon(g, hidden ? Glyph.EyeOff : Glyph.Eye, Theme.IconSmall, hidden ? Theme.TextMute : Theme.TextDim, EyeRect(r));
            }
            var a = AddRect;
            if (hover == -2) Theme.FillRound(g, Theme.Surface, a, Theme.SF(7));
            Theme.DashedRound(g, Theme.BorderStrong, a, Theme.SF(7));
            string t = "New Layer";
            int tw = Theme.TextWidth(t, Theme.Body) + Theme.S(24);
            int tx = a.X + (a.Width - tw) / 2;
            Theme.DrawIcon(g, Glyph.Add, Theme.IconTiny, Theme.TextDim, new Rectangle(tx, a.Y, Theme.S(16), a.Height));
            Theme.DrawText(g, t, Theme.Body, Theme.TextDim, new Rectangle(tx + Theme.S(24), a.Y, tw, a.Height));
        }
    }

    /// <summary>Floating bottom dock: layer tools plus the Design / Animate switch.</summary>
    public sealed class ToolDock : Control
    {
        static readonly (string key, string glyph, string tip)[] Tools =
        {
            ("select", Glyph.Move, "Select / move (drag layers on the canvas)"),
            ("crosshair", Glyph.Crosshair, "Add crosshair layer"),
            ("image", Glyph.Image, "Add image layer"),
            ("shape", Glyph.Square, "Add shape layer"),
            ("draw", Glyph.Pencil, "Add pixel-art layer from an image"),
            ("text", Glyph.Text, "Add text layer"),
            ("fit", Glyph.Fit, "Fit to view"),
            ("grid", Glyph.Grid, "Toggle pixel grid"),
            ("bg", Glyph.Palette, "Cycle canvas background"),
        };
        int hover = -1;
        public bool Animate;
        public event Action<string> ToolClicked;
        public event Action<bool> ModeChanged;
        readonly ToolTip tip = new ToolTip();

        public ToolDock()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = Theme.S(52);
            Width = Tools.Length * Theme.S(44) + Theme.S(28) + Theme.S(200);
            BackColor = Theme.Chrome;
            using (var p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.SF(12))) Region = new Region(p);
        }

        Rectangle ToolRect(int i) => new Rectangle(Theme.S(8) + i * Theme.S(44), Theme.S(6), Theme.S(40), Height - Theme.S(12));
        Rectangle ModeRect(int i) => new Rectangle(Width - Theme.S(196) + i * Theme.S(94), Theme.S(7), Theme.S(90), Height - Theme.S(14));

        public Point ToolPoint(string key) { int i = Array.FindIndex(Tools, t => t.key == key); var r = ToolRect(Math.Max(0, i)); return new Point(r.X, -Theme.S(110)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1;
            for (int i = 0; i < Tools.Length; i++) if (ToolRect(i).Contains(e.Location)) h = i;
            for (int i = 0; i < 2; i++) if (ModeRect(i).Contains(e.Location)) h = 100 + i;
            if (h != hover)
            {
                hover = h;
                Invalidate();
                tip.SetToolTip(this, h >= 0 && h < Tools.Length ? Tools[h].tip : "");
            }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < Tools.Length; i++) if (ToolRect(i).Contains(e.Location)) { ToolClicked?.Invoke(Tools[i].key); return; }
            for (int i = 0; i < 2; i++) if (ModeRect(i).Contains(e.Location)) { Animate = i == 1; Invalidate(); ModeChanged?.Invoke(Animate); return; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Chrome);
            Theme.Smooth(g);
            Theme.StrokeRound(g, Theme.BorderStrong, new RectangleF(0, 0, Width, Height), Theme.SF(12));
            for (int i = 0; i < Tools.Length; i++)
            {
                var r = ToolRect(i);
                if (i == hover) Theme.FillRound(g, Theme.Surface3, r, Theme.SF(7));
                else if (i == 0) Theme.FillRound(g, Theme.Surface2, r, Theme.SF(7));
                Theme.DrawIcon(g, Tools[i].glyph, Theme.Icon, i == hover || i == 0 ? Theme.Text : Theme.TextDim, r);
                if (i == 5)
                    using (var p = new Pen(Theme.Border)) g.DrawLine(p, r.Right + Theme.S(2), Theme.S(14), r.Right + Theme.S(2), Height - Theme.S(14));
            }
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - Theme.S(204), Theme.S(12), Width - Theme.S(204), Height - Theme.S(12));
            for (int i = 0; i < 2; i++)
            {
                var r = ModeRect(i);
                bool on = (i == 1) == Animate;
                if (on) { Theme.FillRound(g, Theme.AccentDim, r, Theme.SF(7)); Theme.StrokeRound(g, Theme.AccentBorder, r, Theme.SF(7)); }
                else if (hover == 100 + i) Theme.FillRound(g, Theme.Surface3, r, Theme.SF(7));
                Theme.DrawText(g, i == 0 ? "Design" : "Animate", Theme.BodyMedium, on ? Theme.Text : Theme.TextDim, r, Theme.Center);
            }
        }
    }

    public sealed class SaveChangesDialog : DarkDialog
    {
        public SaveChangesDialog(string name) : base("Save changes?", 460)
        {
            Content.Controls.Add(new WrapLabel("“" + name + "” has unsaved changes.", Theme.Body, Theme.TextDim));
            AddButton("Cancel", ButtonKind.Ghost, () => { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Don't save", ButtonKind.Secondary, () => { DialogResult = DialogResult.No; Close(); });
            AddButton("Save", ButtonKind.Primary, () => { DialogResult = DialogResult.Yes; Close(); });
        }
    }
}
