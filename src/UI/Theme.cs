using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CrosshairY.Core;

namespace CrosshairY.UI
{
    /// <summary>Design tokens: neutral charcoal chrome, a single orange accent, Noto Sans type.</summary>
    public static class Theme
    {
        // surfaces (darkest → lightest)
        public static readonly Color Canvas = Hex("#141414");      // designer canvas
        public static readonly Color Well = Hex("#181818");        // inset columns, search fields, table headers
        public static readonly Color Chrome = Hex("#1C1C1C");      // title bar, sidebar, tab bar, tables
        public static readonly Color Sidebar = Chrome;
        public static readonly Color Bg = Hex("#202020");          // page background
        public static readonly Color Surface = Hex("#252525");     // cards, panels
        public static readonly Color Surface2 = Hex("#2B2B2B");    // controls, dropdowns
        public static readonly Color Surface3 = Hex("#343434");    // hover / selected rows
        public static readonly Color Border = Hex("#2E2E2E");
        public static readonly Color BorderStrong = Hex("#3D3D3D");
        public static readonly Color Text = Hex("#EDEDED");
        public static readonly Color TextDim = Hex("#A3A3A3");
        public static readonly Color TextMute = Hex("#707070");
        public static readonly Color Accent = Hex("#EF9408");
        public static readonly Color AccentHover = Hex("#F7A62A");
        public static readonly Color AccentDim = Hex("#3A2A13");    // selected fill
        public static readonly Color AccentBorder = Hex("#B8700C");
        public static readonly Color AccentText = Hex("#1B1205");
        public static readonly Color Purple = Hex("#4D447B");
        public static readonly Color PurpleBorder = Hex("#6E62B0");
        public static readonly Color Danger = Hex("#E5484D");
        public static readonly Color DangerDim = Hex("#3A1C1E");
        public static readonly Color Success = Hex("#46A758");
        public static readonly Color Warn = Hex("#F5B800");

        public static float Scale = 1f;
        public static int S(double px) => (int)Math.Round(px * Scale);
        public static float SF(double px) => (float)(px * Scale);

        public static Font Body, BodyMedium, BodyBold, Small, SmallBold, Caption, Nav, H1, H2, H3, Logo, Mono, Icon, IconSmall, IconLarge, IconTiny;
        public static string IconFamily = "Segoe MDL2 Assets";
        public static string UiFamily = "Segoe UI";

        public static void Init(float dpiScale)
        {
            Scale = dpiScale;
            var reg = Fonts.Load();
            Body = Fonts.Make(reg, 9.75f, false);
            BodyMedium = Fonts.Make(reg, 9.75f, true, "Medium");
            BodyBold = Fonts.Make(reg, 9.75f, true, "SemiBold");
            Nav = Fonts.Make(reg, 10f, false);
            Small = Fonts.Make(reg, 8.25f, false);
            SmallBold = Fonts.Make(reg, 8.25f, true, "SemiBold");
            Caption = Fonts.Make(reg, 8.75f, true, "SemiBold");
            H1 = Fonts.Make(reg, 14.5f, true, "SemiBold");
            H2 = Fonts.Make(reg, 12f, true, "SemiBold");
            H3 = Fonts.Make(reg, 10.5f, true, "SemiBold");
            Logo = Fonts.Make(reg, 13.5f, true, "Bold");
            UiFamily = Body.FontFamily.Name;
            Mono = new Font("Consolas", 9.5f);
            using (var fonts = new InstalledFontCollection())
                if (fonts.Families.Any(f => f.Name == "Segoe Fluent Icons")) IconFamily = "Segoe Fluent Icons";
            Icon = new Font(IconFamily, 11f);
            IconSmall = new Font(IconFamily, 9f);
            IconTiny = new Font(IconFamily, 7.5f);
            IconLarge = new Font(IconFamily, 16f);
        }

        static Color Hex(string h) => ColorUtil.Parse(h);

        public static Color Blend(Color a, Color b, double t) => ColorUtil.Lerp(a, b, t);

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            if (c.A == 0) return;
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color c, RectangleF r, float radius, float width = 1)
        {
            using (var p = Round(new RectangleF(r.X + width / 2, r.Y + width / 2, r.Width - width, r.Height - width), radius))
            using (var pen = new Pen(c, width)) g.DrawPath(pen, p);
        }

        public static void DashedRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), radius))
            using (var pen = new Pen(c, 1) { DashPattern = new[] { 4f, 3f } }) g.DrawPath(pen, p);
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public const TextFormatFlags Left = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        public const TextFormatFlags Center = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        public const TextFormatFlags Right = TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public static void DrawText(Graphics g, string text, Font f, Color c, Rectangle r, TextFormatFlags flags = Left)
        {
            TextRenderer.DrawText(g, text, f, r, c, flags);
        }

        public static int TextWidth(string text, Font f) => TextRenderer.MeasureText(text ?? "", f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;

        public static void DrawIcon(Graphics g, string glyph, Font f, Color c, Rectangle r)
        {
            if (glyph != null && glyph.Length > 0 && glyph[0] == '\u0001')
            {
                // custom-drawn bookmark ribbon (the icon font has no matching glyph)
                float h = f.Size * 1.25f * g.DpiY / 72f, w = h * 0.72f;
                float x = r.X + (r.Width - w) / 2f, y = r.Y + (r.Height - h) / 2f;
                var pts = new[] { new PointF(x, y), new PointF(x + w, y), new PointF(x + w, y + h), new PointF(x + w / 2, y + h * 0.7f), new PointF(x, y + h) };
                var old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (glyph == Glyph.BookmarkFilled) using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
                else using (var p = new Pen(c, Math.Max(1.2f, h / 11f)) { LineJoin = LineJoin.Round }) g.DrawPolygon(p, pts);
                g.SmoothingMode = old;
                return;
            }
            TextRenderer.DrawText(g, glyph, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        public static void DarkTitleBar(Form f)
        {
            try
            {
                int on = 1;
                Native.DwmSetWindowAttribute(f.Handle, 20, ref on, 4);     // immersive dark mode
                int border = ColorTranslator.ToWin32(Hex("#2E2E2E"));
                Native.DwmSetWindowAttribute(f.Handle, 34, ref border, 4); // border color (Win11)
                int caption = ColorTranslator.ToWin32(Chrome);
                Native.DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);
            }
            catch { }
        }

        public static void DarkScrollbars(Control c)
        {
            try { Native.SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { }
        }
    }

    /// <summary>Loads the bundled Noto Sans files (SIL Open Font License) for both GDI+ and GDI text.</summary>
    public static class Fonts
    {
        static PrivateFontCollection pfc;

        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

        public static PrivateFontCollection Load()
        {
            if (pfc != null) return pfc;
            pfc = new PrivateFontCollection();
            var asm = typeof(Fonts).Assembly;
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    byte[] data;
                    using (var s = asm.GetManifestResourceStream(name))
                    using (var ms = new MemoryStream()) { s.CopyTo(ms); data = ms.ToArray(); }
                    IntPtr mem = Marshal.AllocCoTaskMem(data.Length);   // intentionally kept alive for the process lifetime
                    Marshal.Copy(data, 0, mem, data.Length);
                    pfc.AddMemoryFont(mem, data.Length);
                    uint count = 0;
                    AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref count);
                }
                catch { }
            }
            return pfc;
        }

        /// <summary>Creates a font from the bundled family (weight variants are separate families in static Noto builds).</summary>
        public static Font Make(PrivateFontCollection reg, float size, bool heavy, string weight = null)
        {
            try
            {
                FontFamily fam = null;
                if (weight != null) fam = reg.Families.FirstOrDefault(f => f.Name.Equals("Noto Sans " + weight, StringComparison.OrdinalIgnoreCase));
                if (fam == null) fam = reg.Families.FirstOrDefault(f => f.Name.Equals("Noto Sans", StringComparison.OrdinalIgnoreCase));
                if (fam != null)
                {
                    var style = FontStyle.Regular;
                    if (weight == "Bold" && fam.Name == "Noto Sans") style = FontStyle.Bold;
                    else if (heavy && fam.Name == "Noto Sans" && weight != "Medium") style = FontStyle.Bold;
                    if (!fam.IsStyleAvailable(style)) style = FontStyle.Regular;
                    return new Font(fam, size, style);
                }
            }
            catch { }
            return new Font(heavy ? "Segoe UI Semibold" : "Segoe UI", size);
        }
    }

    /// <summary>Segoe Fluent / MDL2 glyphs.</summary>
    public static class Glyph
    {
        public const string Crosshair = "", Target = "", Edit = "", Brush = "", Globe = "",
            People = "", Person = "", Keyboard = "", Display = "", Settings = "", Add = "", Delete = "",
            Copy = "", Share = "", Download = "", Upload = "", Star = "", StarFilled = "",
            Eye = "", EyeOff = "", Up = "", Down = "", Left = "", Right = "",
            ArrowUp = "", ArrowDown = "", ArrowLeft = "", ArrowRight = "",
            Search = "", Save = "", Undo = "", Redo = "", Play = "", Folder = "",
            Image = "", Font = "", Shape = "", Layers = "", Check = "", Close = "",
            More = "", Refresh = "", ZoomIn = "", ZoomOut = "", Fit = "", Grid = "",
            Link = "", Game = "", Pin = "", Center = "", Import = "", Export = "",
            Info = "", Duplicate = "", Lightning = "", Mouse = "", Sparkle = "", Home = "",
            Heart = "", Palette = "", Rename = "", Dot = "", Circle = "", Move = "",
            Hamburger = "", Bookmark = "bm", BookmarkFilled = "bmf", Compass = "", Shuffle = "",
            Help = "", Power = "", Minimize = "", Maximize = "", Restore = "", ChromeClose = "",
            Filter = "", AllApps = "", Rocket = "", Plug = "", Wrench = "", Gamepad = "",
            Monitor = "", Video = "", Resize = "", Trophy = "", Reset = "", Pencil = "",
            Cursor = "", ChevronDown = "", ChevronUp = "", Swap = "", Square = "", Text = "",
            Fire = "", Aim = "", Reload = "", Hand = "", Tag = "", Clock = "", Recoil = "",
            Code = "", News = "";
    }
}
