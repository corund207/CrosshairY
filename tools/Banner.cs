// Renders docs/images/banner.png for the README using the app's mark and bundled Noto Sans.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;

static class Banner
{
    static void Main(string[] args)
    {
        string fonts = args[0], output = args[1];
        var pfc = new PrivateFontCollection();
        foreach (var f in Directory.GetFiles(fonts, "*.ttf")) pfc.AddFontFile(f);
        var bold = pfc.Families.First(f => f.Name == "Noto Sans");
        var regular = pfc.Families.First(f => f.Name == "Noto Sans");
        int w = 1280, h = 360;
        var bg = Color.FromArgb(0x14, 0x14, 0x14);
        var orange = Color.FromArgb(0xEF, 0x94, 0x08);
        var text = Color.FromArgb(0xED, 0xED, 0xED);
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(bg);
            // faint pixel grid, like the designer canvas
            using (var p = new Pen(Color.FromArgb(14, 255, 255, 255)))
            {
                for (int x = 0; x < w; x += 16) g.DrawLine(p, x, 0, x, h);
                for (int y = 0; y < h; y += 16) g.DrawLine(p, 0, y, w, y);
            }
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(-200, -260, 900, 880);
                using (var b = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(46, orange), SurroundColors = new[] { Color.FromArgb(0, orange) } })
                    g.FillPath(b, glow);
            }
            // mark
            float cx = 220, cy = h / 2f, r = 74, th = 16;
            // ring with gaps at top and bottom (clipped, so the background shows through)
            var state = g.Save();
            g.SetClip(new RectangleF(cx - 15, cy - r - 20, 30, 56), CombineMode.Exclude);
            g.SetClip(new RectangleF(cx - 15, cy + r - 36, 30, 56), CombineMode.Exclude);
            using (var pen = new Pen(text, th)) g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
            g.Restore(state);
            using (var b = new SolidBrush(orange))
            {
                g.FillEllipse(b, cx - 17, cy - 17, 34, 34);
                g.FillRectangle(b, cx - th / 2, cy - r - 12, th, 42);
                g.FillRectangle(b, cx - th / 2, cy + r - 30, th, 42);
            }
            // wordmark
            using (var f = new Font(bold, 78, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var tb = new SolidBrush(text))
            using (var ob = new SolidBrush(orange))
            {
                var fmt = StringFormat.GenericTypographic;
                float x = 340, y = 92;
                g.DrawString("Crosshair", f, tb, x, y, fmt);
                float wWord = g.MeasureString("Crosshair", f, 2000, fmt).Width;
                g.DrawString("Y", f, ob, x + wWord, y, fmt);
            }
            using (var f = new Font(regular, 28, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.FromArgb(0xA3, 0xA3, 0xA3)))
                g.DrawString("Custom crosshair overlay for any PC game", f, b, 344, 196, StringFormat.GenericTypographic);
            // feature chips
            string[] chips = { "Native Windows", "Crosshair X codes", "Recoil tracking", "MIT licensed" };
            using (var f = new Font(regular, 19, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                float x = 344, y = 254;
                foreach (var c in chips)
                {
                    float cw = g.MeasureString(c, f, 1000, StringFormat.GenericTypographic).Width + 32;
                    using (var path = Round(new RectangleF(x, y, cw, 40), 20))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(0x25, 0x25, 0x25))) g.FillPath(b, path);
                        using (var p = new Pen(Color.FromArgb(0x3D, 0x3D, 0x3D))) g.DrawPath(p, path);
                    }
                    using (var b = new SolidBrush(text)) g.DrawString(c, f, b, x + 16, y + 9, StringFormat.GenericTypographic);
                    x += cw + 12;
                }
            }
            bmp.Save(output, ImageFormat.Png);
        }
    }

    static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
