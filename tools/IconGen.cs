// Generates build/app.ico (16-256 px PNG frames) for the executable. Run by build.ps1.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class IconGen
{
    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "app.ico";
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (var bmp = Draw(s))
            using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
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

    static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            var dark = Color.FromArgb(0x1C, 0x1C, 0x1C);
            using (var b = new SolidBrush(dark))
            using (var path = Round(new RectangleF(0, 0, size, size), size * 0.24f)) g.FillPath(b, path);
            float c = size / 2f, r = size * 0.3f, th = Math.Max(1.4f, size * 0.085f);
            using (var pen = new Pen(Color.FromArgb(0xED, 0xED, 0xED), th)) g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
            float gap = size * 0.14f;
            using (var b = new SolidBrush(dark))
            {
                g.FillRectangle(b, c - gap / 2, size * 0.08f, gap, size * 0.28f);
                g.FillRectangle(b, c - gap / 2, size * 0.64f, gap, size * 0.28f);
            }
            using (var b = new SolidBrush(Color.FromArgb(0xEF, 0x94, 0x08)))
            {
                float d = size * 0.17f;
                g.FillEllipse(b, c - d / 2, c - d / 2, d, d);
                g.FillRectangle(b, c - th / 2, size * 0.1f, th, size * 0.2f);
                g.FillRectangle(b, c - th / 2, size * 0.7f, th, size * 0.2f);
            }
        }
        return bmp;
    }
}
