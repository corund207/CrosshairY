// Gallery submission tool, used by .github/workflows/gallery.yml (built with tools/gallery.ps1).
//   GalleryTool validate <issue-body.txt> <result.json> <preview.png>
//   GalleryTool add <result.json> <issue-number> <github-login> <gallery.json> <previews-dir>
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Reticly.Core;
using Reticly.Import;
using Reticly.Render;

namespace Reticly { static class Program { public const string Version = "gallery-tool"; public static bool SnapMode; } }

static class GalleryTool
{
    static readonly string[] Categories = { "Classic", "Dot", "Circle", "Chevron", "T-Style", "Fancy", "Animated", "Recoil" };

    static int Main(string[] a)
    {
        try
        {
            if (a.Length >= 4 && a[0] == "validate") return Validate(a[1], a[2], a[3]);
            if (a.Length >= 6 && a[0] == "add") return Add(a[1], a[2], a[3], a[4], a[5]);
            Console.Error.WriteLine("usage: validate <body> <result.json> <preview.png> | add <result.json> <issue> <login> <gallery.json> <previews-dir>");
            return 2;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>GitHub issue forms render as "### Label\n\nvalue" sections; "_No response_" means empty.</summary>
    static Dictionary<string, string> ParseForm(string body)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = Regex.Split(body.Replace("\r\n", "\n"), @"^###\s+", RegexOptions.Multiline);
        foreach (var p in parts.Skip(1))
        {
            int nl = p.IndexOf('\n');
            string key = (nl < 0 ? p : p.Substring(0, nl)).Trim();
            string val = nl < 0 ? "" : p.Substring(nl + 1);
            val = Regex.Replace(val, @"^```\w*\s*$", "", RegexOptions.Multiline).Trim();
            if (val == "_No response_") val = "";
            d[key] = val;
        }
        return d;
    }

    static int Validate(string bodyFile, string resultFile, string previewFile)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var form = ParseForm(File.ReadAllText(bodyFile));
        string name = Get(form, "Name").Replace("\n", " ").Trim();
        string code = Regex.Replace(Get(form, "Reticly code"), @"\s+", "");
        string author = Get(form, "Credit as").Replace("\n", " ").Trim();
        string category = Get(form, "Category").Trim();

        if (name.Length == 0) errors.Add("The **Name** field is empty.");
        else if (name.Length > 40) errors.Add("The name is longer than 40 characters.");
        if (author.Length > 40) author = author.Substring(0, 40);
        if (code.Length == 0) errors.Add("The **Reticly code** field is empty. In Reticly use Share › Share Code and paste it.");
        else if (code.Length > 200000) errors.Add("The code is too large (over 200 KB).");
        var cat = Categories.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));

        List<object> layers = null;
        string kind = null;
        if (code.Length > 0 && code.Length <= 200000)
        {
            try
            {
                var r = CodeImporter.Import(code);
                layers = r.Layers;
                kind = r.Kind;
                if (name.Length == 0) name = r.Name;
            }
            catch (Exception ex) { errors.Add("The code couldn't be read: " + ex.Message); }
        }

        var types = new Dictionary<string, int>();
        bool animated = false, recoil = false;
        if (layers != null)
        {
            if (layers.Count == 0) errors.Add("The design has no layers.");
            if (layers.Count > 40) errors.Add("The design has more than 40 layers.");
            foreach (var l in layers.OfType<Dictionary<string, object>>())
            {
                string t = Defaults.LayerType(l);
                types[t] = types.TryGetValue(t, out var n) ? n + 1 : 1;
                var fo = J.Obj(l, "firingOptions");
                if (J.Str(fo, "recoilPattern") != null) recoil = true;
                if (fo != null && (J.Num(fo, "duration") > 0 || (J.List(fo, "stages")?.Count ?? 0) > 0)) animated = true;
                if (t == "image" || t == "draw")
                {
                    string src = J.Str(l, "image") ?? "";
                    if (!src.StartsWith("data:")) errors.Add("Image layers must be embedded (no links to external images).");
                    else warnings.Add("Contains an image layer: check the preview before approving.");
                }
            }
            if (cat == null) cat = recoil ? "Recoil" : animated ? "Animated" : "Classic";
            try { RenderPreview(layers, previewFile); }
            catch (Exception ex) { errors.Add("The design couldn't be drawn: " + ex.Message); }
        }

        // re-export as a Reticly code so every gallery entry decodes offline
        string normalized = layers != null && errors.Count == 0 ? CodeImporter.ExportOwn(layers, name) : null;
        var result = J.O("ok", errors.Count == 0, "name", name, "author", author, "category", cat ?? "Classic", "code", normalized,
            "kind", kind, "layers", layers?.Count ?? 0, "types", string.Join(", ", types.Select(kv => kv.Value + "× " + kv.Key)),
            "animated", animated, "recoil", recoil,
            "errors", errors.Distinct().Cast<object>().ToList(), "warnings", warnings.Distinct().Cast<object>().ToList());
        File.WriteAllText(resultFile, Json.Serialize(result, true), new UTF8Encoding(false));
        Console.WriteLine(errors.Count == 0 ? "valid: " + name : "invalid: " + string.Join(" / ", errors));
        return 0;
    }

    static int Add(string resultFile, string issue, string login, string galleryFile, string previewsDir)
    {
        var r = Json.Parse(File.ReadAllText(resultFile));
        if (!J.Bool(r, "ok")) { Console.Error.WriteLine("submission is not valid"); return 3; }
        var root = (Dictionary<string, object>)Json.Parse(File.ReadAllText(galleryFile));
        var list = J.List(root, "crosshairs") ?? new List<object>();
        string code = J.Str(r, "code");
        // the same design under another name is still a duplicate: compare the layers, not the code
        string design = Json.Serialize(CodeImporter.Import(code).Layers);
        foreach (var e in list)
        {
            try { if (Json.Serialize(CodeImporter.Import(J.Str(e, "code")).Layers) == design) { Console.WriteLine("duplicate of " + J.Str(e, "id")); return 4; } }
            catch { }
        }
        string name = J.Str(r, "name");
        string slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "design";
        if (slug.Length > 30) slug = slug.Substring(0, 30).Trim('-');
        string id = slug + "-" + issue;
        string author = J.Str(r, "author");
        list.Add(J.O("id", id, "name", name, "author", string.IsNullOrWhiteSpace(author) ? login : author, "category", J.Str(r, "category"),
            "added", DateTime.UtcNow.ToString("yyyy-MM-dd"), "issue", int.Parse(issue), "code", code));
        root["crosshairs"] = list;
        File.WriteAllText(galleryFile, Json.Serialize(root, true) + "\n", new UTF8Encoding(false));
        Directory.CreateDirectory(previewsDir);
        Console.WriteLine(id);
        return 0;
    }

    static string Get(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) ? v : "";

    /// <summary>Crisp 4× preview on a dark and a light background, side by side.</summary>
    static void RenderPreview(List<object> layers, string path)
    {
        using (var r = CrosshairRenderer.Render(layers, 1))
        {
            int half = Math.Max(12, Math.Min(90, (int)Math.Ceiling(Thumbnails.ContentRadius(r.Bitmap, r.OriginX, r.OriginY)) + 6));
            int size = half * 2, z = Math.Max(2, Math.Min(8, 240 / size)), cell = size * z;
            using (var bmp = new Bitmap(cell * 2 + 12, cell))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(24, 24, 24));
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                var bgs = new[] { Color.FromArgb(30, 30, 30), Color.FromArgb(150, 160, 150) };
                for (int i = 0; i < 2; i++)
                {
                    var dst = new Rectangle(i * (cell + 12), 0, cell, cell);
                    using (var b = new SolidBrush(bgs[i])) g.FillRectangle(b, dst);
                    g.DrawImage(r.Bitmap, dst, new Rectangle(r.OriginX - half, r.OriginY - half, size, size), GraphicsUnit.Pixel);
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}

/// <summary>Content radius of a rendered crosshair (same idea as the app's thumbnail helper).</summary>
static class Thumbnails
{
    public static double ContentRadius(Bitmap b, int ox, int oy)
    {
        double max = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (b.GetPixel(x, y).A > 8) max = Math.Max(max, Math.Max(Math.Abs(x - ox), Math.Abs(y - oy)));
        return max;
    }
}
