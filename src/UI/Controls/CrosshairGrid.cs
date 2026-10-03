using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.Render;

namespace CrosshairY.UI.Controls
{
    public static class Thumbnails
    {
        static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();

        static Bitmap Cached(string k, Func<Bitmap> make)
        {
            if (cache.TryGetValue(k, out var b)) return b;
            b = make();
            if (cache.Count > 800)
            {
                foreach (var v in cache.Values) v.Dispose();
                cache.Clear();
            }
            cache[k] = b;
            return b;
        }

        /// <summary>Zoomed-to-fit thumbnail (used for big previews).</summary>
        public static Bitmap Get(string key, List<object> layers, int size) => Cached("fit|" + key + "|" + size, () => Make(layers, size));

        /// <summary>Crosshair at its real on-screen size (2× when tiny, scaled down only if it doesn't fit).</summary>
        public static Bitmap Actual(string key, List<object> layers, int w, int h) => Cached("act|" + key + "|" + w + "x" + h, () => MakeActual(layers, w, h));

        public static Bitmap MakeActual(List<object> layers, int w, int h)
        {
            var result = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
            try
            {
                using (var r = CrosshairRenderer.Render(layers, 1))
                using (var g = Graphics.FromImage(result))
                {
                    g.Clear(Color.Transparent);
                    double radius = Math.Max(1, ContentRadius(r.Bitmap, r.OriginX, r.OriginY));
                    double avail = Math.Min(w, h) / 2.0 - 2;
                    double scale = radius * 2 <= avail * 0.6 && radius < 7 ? 2 : radius > avail ? avail / radius : 1;
                    if (scale >= 1)
                    {
                        int z = (int)scale;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(r.Bitmap, new Rectangle(w / 2 - r.OriginX * z, h / 2 - r.OriginY * z, r.Bitmap.Width * z, r.Bitmap.Height * z));
                    }
                    else
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(r.Bitmap, (float)(w / 2.0 - r.OriginX * scale), (float)(h / 2.0 - r.OriginY * scale), (float)(r.Bitmap.Width * scale), (float)(r.Bitmap.Height * scale));
                    }
                }
            }
            catch { }
            return result;
        }

        public static void Invalidate(string keyPrefix)
        {
            foreach (var k in cache.Keys.Where(x => x.Contains("|" + keyPrefix)).ToList())
            {
                cache[k].Dispose();
                cache.Remove(k);
            }
        }

        public static Bitmap Make(List<object> layers, int size)
        {
            var result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            try
            {
                using (var r = CrosshairRenderer.Render(layers, 1))
                {
                    double radius = Math.Max(1, ContentRadius(r.Bitmap, r.OriginX, r.OriginY));
                    double fit = (size * 0.42) / radius;
                    using (var g = Graphics.FromImage(result))
                    {
                        g.Clear(Color.Transparent);
                        if (fit >= 1)
                        {
                            int z = (int)Math.Max(1, Math.Min(8, Math.Floor(fit)));
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            g.PixelOffsetMode = PixelOffsetMode.Half;
                            g.DrawImage(r.Bitmap, new Rectangle(size / 2 - r.OriginX * z, size / 2 - r.OriginY * z, r.Bitmap.Width * z, r.Bitmap.Height * z));
                        }
                        else
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(r.Bitmap, size / 2f - (float)(r.OriginX * fit), size / 2f - (float)(r.OriginY * fit), (float)(r.Bitmap.Width * fit), (float)(r.Bitmap.Height * fit));
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>Largest distance from the origin to any visible pixel.</summary>
        public static double ContentRadius(Bitmap bmp, int ox, int oy)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = data.Stride / 4;
                var px = new int[stride * bmp.Height];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                double best = 0;
                for (int y = 0; y < bmp.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if (((px[row + x] >> 24) & 0xFF) < 8) continue;
                        double d = Math.Max(Math.Abs(x + 0.5 - ox), Math.Abs(y + 0.5 - oy));
                        if (d > best) best = d;
                    }
                }
                return best;
            }
            finally { bmp.UnlockBits(data); }
        }
    }

    public sealed class TileItem
    {
        public string Id;
        public string Name;
        public List<object> Layers;
        public string CacheKey;
        public bool Favorite;
        public bool Active;
        public bool Saved;        // browse: bookmark state
        public string Badge;      // keybind
        public object Tag;
    }

    public enum TileStyle { Saved, Browse, Plain }

    /// <summary>Wrapping grid of crosshair cards. Height follows content so it can live inside a ScrollHost.</summary>
    public class CrosshairGrid : Control
    {
        List<TileItem> items = new List<TileItem>();
        int hoverIndex = -1, selectedIndex = -1;
        public TileStyle Style = TileStyle.Saved;
        public int TileWidth = Theme.S(204), TileHeight = Theme.S(152), Gap = Theme.S(16);
        public string EmptyText = "Nothing here yet.";
        public event Action<TileItem> ItemClicked, ItemDoubleClicked, StarClicked, BookmarkClicked;
        public event Action<TileItem, Point> ItemRightClicked, MenuClicked;

        public CrosshairGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            Height = Theme.S(200);
        }

        public List<TileItem> Items
        {
            get => items;
            set { items = value ?? new List<TileItem>(); hoverIndex = -1; Relayout(); Invalidate(); }
        }

        public TileItem Selected => selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;

        public void Select(string id) { selectedIndex = items.FindIndex(i => i.Id == id); Invalidate(); }

        public Rectangle BoundsOf(string id) { int i = items.FindIndex(x => x.Id == id); return i < 0 ? Rectangle.Empty : TileRect(i); }

        int Columns => Math.Max(1, (Width + Gap) / (TileWidth + Gap));

        void Relayout()
        {
            int rows = items.Count == 0 ? 1 : (items.Count + Columns - 1) / Columns;
            int h = items.Count == 0 ? Theme.S(200) : rows * (TileHeight + Gap) - Gap + Theme.S(4);
            if (Height != h) Height = h;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }

        Rectangle TileRect(int i)
        {
            int cols = Columns;
            int tw = Math.Min((Width - (cols - 1) * Gap) / cols, TileWidth + Theme.S(60));
            int col = i % cols, row = i / cols;
            return new Rectangle(col * (tw + Gap), row * (TileHeight + Gap), tw, TileHeight);
        }

        Rectangle StarRect(Rectangle t) => new Rectangle(t.X + Theme.S(8), t.Y + Theme.S(8), Theme.S(28), Theme.S(28));
        Rectangle MenuRect(Rectangle t) => new Rectangle(t.Right - Theme.S(36), t.Y + Theme.S(8), Theme.S(28), Theme.S(28));

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++) if (TileRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = HitTest(e.Location);
            if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverIndex = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = HitTest(e.Location);
            if (i < 0) return;
            selectedIndex = i;
            Invalidate();
            var r = TileRect(i);
            if (e.Button == MouseButtons.Left)
            {
                if (Style == TileStyle.Saved && StarRect(r).Contains(e.Location)) StarClicked?.Invoke(items[i]);
                else if (MenuRect(r).Contains(e.Location))
                {
                    if (Style == TileStyle.Saved) MenuClicked?.Invoke(items[i], new Point(MenuRect(r).Left, MenuRect(r).Bottom));
                    else BookmarkClicked?.Invoke(items[i]);
                }
                else ItemClicked?.Invoke(items[i]);
            }
            else if (e.Button == MouseButtons.Right) ItemRightClicked?.Invoke(items[i], e.Location);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int i = HitTest(e.Location);
            if (i >= 0 && e.Button == MouseButtons.Left && !MenuRect(TileRect(i)).Contains(e.Location) && !StarRect(TileRect(i)).Contains(e.Location))
                ItemDoubleClicked?.Invoke(items[i]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            if (items.Count == 0)
            {
                Theme.DrawText(g, EmptyText, Theme.Body, Theme.TextDim, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                return;
            }
            var mouse = PointToClient(MousePosition);
            for (int i = 0; i < items.Count; i++)
            {
                var r = TileRect(i);
                if (!e.ClipRectangle.IntersectsWith(r)) continue;
                var it = items[i];
                bool hov = i == hoverIndex;
                var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
                Theme.FillRound(g, hov ? Theme.Blend(Theme.Surface, Color.White, .03) : Theme.Surface, rf, Theme.SF(9));
                if (it.Active) Theme.StrokeRound(g, Theme.Accent, rf, Theme.SF(9), Theme.SF(1.5));
                else if (i == selectedIndex) Theme.StrokeRound(g, Theme.BorderStrong, rf, Theme.SF(9));

                // crosshair at actual size
                int pillH = Theme.S(28);
                var art = new Rectangle(r.X + Theme.S(10), r.Y + Theme.S(10), r.Width - Theme.S(20), r.Height - pillH - Theme.S(26));
                var bmp = Thumbnails.Actual(it.CacheKey ?? it.Id, it.Layers, art.Width, art.Height);
                var st = g.Save();
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImageUnscaled(bmp, art.X, art.Y);
                g.Restore(st);
                Theme.Smooth(g);

                // name pill
                int pw = (int)(r.Width * 0.68);
                var pill = new Rectangle(r.X + (r.Width - pw) / 2, r.Bottom - pillH - Theme.S(14), pw, pillH);
                Theme.FillRound(g, Theme.Chrome, pill, Theme.SF(5));
                Theme.DrawText(g, it.Name, Theme.Body, it.Active ? Theme.Accent : Theme.Text, new Rectangle(pill.X + Theme.S(8), pill.Y, pill.Width - Theme.S(16), pill.Height), Theme.Center);

                if (Style == TileStyle.Saved)
                {
                    var sr = StarRect(r);
                    bool starHot = hov && sr.Contains(mouse);
                    Theme.DrawIcon(g, it.Favorite ? Glyph.StarFilled : Glyph.StarFilled, Theme.Icon,
                        it.Favorite ? Theme.Accent : starHot ? Theme.TextDim : Theme.Blend(Theme.Surface, Theme.TextMute, .8), sr);
                    var mr = MenuRect(r);
                    Theme.DrawIcon(g, Glyph.Hamburger, Theme.Icon, hov && mr.Contains(mouse) ? Theme.Text : Theme.TextMute, mr);
                }
                else if (Style == TileStyle.Browse)
                {
                    var mr = MenuRect(r);
                    Theme.DrawIcon(g, it.Saved ? Glyph.BookmarkFilled : Glyph.Bookmark, Theme.Icon, it.Saved ? Theme.Accent : hov && mr.Contains(mouse) ? Theme.Text : Theme.TextMute, mr);
                }
                if (!string.IsNullOrEmpty(it.Badge))
                {
                    int bw = Theme.TextWidth(it.Badge, Theme.SmallBold) + Theme.S(12);
                    var br = new Rectangle(r.X + (r.Width - bw) / 2, r.Y + Theme.S(10), bw, Theme.S(20));
                    Theme.FillRound(g, Theme.Surface2, br, Theme.SF(4));
                    Theme.DrawText(g, it.Badge, Theme.SmallBold, Theme.TextDim, br, Theme.Center);
                }
            }
        }
    }
}
