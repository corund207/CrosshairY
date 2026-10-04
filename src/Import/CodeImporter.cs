using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Reticly.Core;

namespace Reticly.Import
{
    public sealed class ImportResult
    {
        public List<object> Layers;
        public string Name;
        public string Source;      // "cx:<id>", "valorant", "cs2", "reticly", "json"
        public string Kind;        // human readable
        public string Note;        // extra info (approximations etc.)
    }

    /// <summary>
    /// Imports crosshairs from:
    ///  • Crosshair X share codes (e.g. "xe4lbu6zh8") and share links (crosshairx.gg/s/xe4lbu6zh8)
    ///  • Reticly share codes ("CXY1-...", fully offline)
    ///  • VALORANT crosshair profile codes ("0;P;c;5;h;0;...")
    ///  • Counter-Strike 2 / CS:GO share codes ("CSGO-xxxxx-..." and the new "CS..." format)
    ///  • Raw Crosshair X layer JSON
    /// </summary>
    public static class CodeImporter
    {
        // Public endpoint used by Crosshair X share pages (crosshairx.gg/s/<code>) to fetch a shared crosshair.
        public const string CxFetchUrl = "https://8yy0fp6ycd.execute-api.us-east-1.amazonaws.com/Prod/fetchsharedcrosshair?linkId=";
        public const string OwnPrefix = "CXY1-";

        static readonly Regex CxLink = new Regex(@"crosshairx\.(?:gg|com)/s/([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase);
        static readonly Regex CxCode = new Regex(@"^[A-Za-z0-9]{6,16}$");
        const string CsDict = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";
        static readonly Regex CsLegacy = new Regex("^CSGO(-?[" + CsDict + "]{5}){5}$");
        static readonly Regex Cs2New = new Regex("^CS[" + CsDict + "]{44}$");

        public static string Detect(string input)
        {
            var s = (input ?? "").Trim();
            if (s.Length == 0) return null;
            if (s.StartsWith(OwnPrefix, StringComparison.OrdinalIgnoreCase)) return "reticly";
            if (CxLink.IsMatch(s)) return "cx";
            if (CsLegacy.IsMatch(s) || Cs2New.IsMatch(s)) return "cs2";
            if (Regex.IsMatch(s, @"^\d+;") && s.Contains(";")) return "valorant";
            if (s.StartsWith("[") || s.StartsWith("{")) return "json";
            if (CxCode.IsMatch(s)) return "cx";
            return null;
        }

        public static string DescribeKind(string kind)
        {
            switch (kind)
            {
                case "cx": return "Crosshair X share code";
                case "reticly": return "Reticly code";
                case "cs2": return "Counter-Strike crosshair code";
                case "valorant": return "VALORANT crosshair code";
                case "json": return "Crosshair JSON";
                default: return "Unknown";
            }
        }

        /// <summary>Imports a code. May perform a network request for Crosshair X codes — call off the UI thread.</summary>
        public static ImportResult Import(string input)
        {
            var s = (input ?? "").Trim();
            switch (Detect(s))
            {
                case "reticly": return ImportOwn(s);
                case "cx": return ImportCrosshairX(s);
                case "cs2": return CsCrosshair.Import(s);
                case "valorant": return ValorantCrosshair.Import(s);
                case "json": return ImportJson(s);
                default: throw new FormatException("That doesn't look like a crosshair code. Paste a Crosshair X code or link, a VALORANT code, a CS2 code, or a Reticly code.");
            }
        }

        // ---------------- Crosshair X ----------------

        public static string ExtractCxId(string s)
        {
            var m = CxLink.Match(s);
            if (m.Success) return m.Groups[1].Value;
            return s.Trim();
        }

        public static ImportResult ImportCrosshairX(string input)
        {
            string id = ExtractCxId(input);
            string json;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 Reticly";
                    json = wc.DownloadString(CxFetchUrl + Uri.EscapeDataString(id));
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse r && (int)r.StatusCode == 404)
                    throw new Exception("Crosshair X code “" + id + "” was not found.");
                throw new Exception("Couldn't reach the Crosshair X share service: " + ex.Message);
            }
            var root = Json.Parse(json);
            string b64 = J.Str(root, "crosshairModel") ?? J.Str(J.Obj(root, "data"), "crosshairModel");
            if (string.IsNullOrEmpty(b64))
            {
                string msg = J.Str(root, "message") ?? J.Str(root, "error");
                throw new Exception("Crosshair X code “" + id + "” was not found" + (msg != null ? " (" + msg + ")" : "") + ".");
            }
            string modelJson = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            var layers = ParseLayers(Json.Parse(modelJson));
            return new ImportResult { Layers = layers, Name = "Crosshair X " + id, Source = "cx:" + id, Kind = DescribeKind("cx") };
        }

        // ---------------- JSON ----------------

        public static List<object> ParseLayers(object parsed)
        {
            List<object> layers;
            if (parsed is List<object> l) layers = l;
            else if (parsed is Dictionary<string, object> d)
            {
                if (J.List(d, "layers") is List<object> ll) layers = ll;
                else if (J.Str(d, "crosshairModel") is string b64)
                    layers = ParseLayers(Json.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64))));
                else layers = new List<object> { d };
            }
            else throw new FormatException("Crosshair data must be a JSON array of layers.");
            layers = layers.Where(x => x is Dictionary<string, object>).ToList();
            if (layers.Count == 0) throw new FormatException("The crosshair has no layers.");
            Defaults.NormalizeLayers(layers);
            return layers;
        }

        static ImportResult ImportJson(string s)
        {
            var parsed = Json.Parse(s);
            string name = J.Str(parsed, "name") ?? "Imported crosshair";
            return new ImportResult { Layers = ParseLayers(parsed), Name = name, Source = "json", Kind = DescribeKind("json") };
        }

        // ---------------- Reticly own codes ----------------

        public static string ExportOwn(List<object> layers, string name = null)
        {
            var payload = name == null ? (object)layers : J.O("name", name, "layers", layers);
            byte[] raw = Encoding.UTF8.GetBytes(Json.Serialize(payload));
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length);
                return OwnPrefix + Convert.ToBase64String(ms.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }

        static ImportResult ImportOwn(string s)
        {
            string b = Regex.Replace(s.Substring(OwnPrefix.Length), @"\s", "").Replace('-', '+').Replace('_', '/');
            while (b.Length % 4 != 0) b += "=";
            byte[] data = Convert.FromBase64String(b);
            string json;
            using (var ms = new MemoryStream(data))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var sr = new StreamReader(ds, Encoding.UTF8)) json = sr.ReadToEnd();
            var parsed = Json.Parse(json);
            return new ImportResult
            {
                Layers = ParseLayers(parsed),
                Name = J.Str(parsed, "name") ?? "Shared crosshair",
                Source = "reticly",
                Kind = DescribeKind("reticly")
            };
        }

        internal static BigInteger ParseBase57(string chars)
        {
            BigInteger big = BigInteger.Zero;
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                int idx = CsDict.IndexOf(chars[i]);
                if (idx < 0) throw new FormatException("Invalid character in share code");
                big = big * CsDict.Length + idx;
            }
            return big;
        }
    }

    /// <summary>VALORANT crosshair profile code → Crosshair X layers.</summary>
    public static class ValorantCrosshair
    {
        static readonly string[] Colors = { "#FFFFFF", "#00FF00", "#7FFF00", "#DFFF00", "#FFFF00", "#00FFFF", "#FF00FF", "#FF0000" };

        public static ImportResult Import(string code)
        {
            var parts = code.Trim().Split(';');
            var primary = new Dictionary<string, string>();
            string section = "";
            for (int i = 1; i < parts.Length; i++)
            {
                string k = parts[i];
                if (k == "P" || k == "A" || k == "S") { section = k; continue; }
                if (i + 1 >= parts.Length) break;
                string v = parts[++i];
                if (section == "P" || section == "") primary[k] = v;
            }

            Func<string, double, double> num = (k, def) =>
                primary.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : def;
            Func<string, bool, bool> flag = (k, def) => primary.TryGetValue(k, out var v) ? v != "0" : def;

            int ci = (int)num("c", 0);
            string color = ci >= 0 && ci < Colors.Length ? Colors[ci] : "#FFFFFF";
            if ((ci == 8 || ci >= Colors.Length) && primary.TryGetValue("u", out var hex) && Regex.IsMatch(hex, "^[0-9A-Fa-f]{6,8}$"))
                color = "#" + hex.Substring(0, 6).ToUpperInvariant();

            bool outlines = flag("h", true);
            double outlineT = num("t", 1), outlineO = num("o", 0.5);
            bool dotOn = flag("d", false);
            double dotT = num("z", 2), dotA = num("a", 1);

            var layers = new List<object>();
            // outer lines (drawn underneath)
            AddLines(layers, "1", num, flag, color, outlines, outlineT, outlineO, true, 2, 2, 10, 0.35);
            // inner lines + center dot
            AddLines(layers, "0", num, flag, color, outlines, outlineT, outlineO, true, 6, 2, 3, 0.8);
            if (dotOn)
            {
                var dotLayer = Defaults.ModelLayer();
                var line = J.Obj(dotLayer, "line");
                line["opacity"] = 0.0;
                line["visible"] = new List<object>();
                dotLayer["dot"] = J.O("diameter", dotT, "opacity", dotA, "color", color, "shape", "square", "blur", 0);
                dotLayer["outline"] = J.O("thickness", outlines ? outlineT : 0, "opacity", outlines ? outlineO : 0, "color", "#000000", "blur", 0);
                layers.Add(dotLayer);
            }
            if (layers.Count == 0)
            {
                var empty = Defaults.ModelLayer();
                J.Obj(empty, "line")["opacity"] = 0.0;
                layers.Add(empty);
            }
            Defaults.NormalizeLayers(layers);
            return new ImportResult
            {
                Layers = layers,
                Name = "VALORANT crosshair",
                Source = "valorant",
                Kind = CodeImporter.DescribeKind("valorant"),
                Note = "Firing error is recreated as a short bloom animation on the fire key."
            };
        }

        static void AddLines(List<object> layers, string p, Func<string, double, double> num, Func<string, bool, bool> flag,
            string color, bool outlines, double ot, double oo, bool _, double defLen, double defThick, double defOff, double defAlpha)
        {
            if (!flag(p + "b", true)) return;
            double len = num(p + "l", defLen), thick = num(p + "t", defThick), off = num(p + "o", defOff), alpha = num(p + "a", defAlpha);
            bool separateVertical = flag(p + "g", false);
            double vlen = num(p + "v", len);
            bool firingError = flag(p + "f", true);
            if (len <= 0 || thick <= 0 || alpha <= 0) return;

            Action<double, string[]> add = (l, dirs) =>
            {
                var layer = Defaults.ModelLayer();
                layer["line"] = J.O("length", l, "thickness", thick, "offset", off * 2, "opacity", alpha, "color", color,
                    "visible", dirs, "shape", "rectangle", "blur", 0, "rotation", 0);
                layer["dot"] = J.O("diameter", 0, "opacity", 0, "color", color, "shape", "square", "blur", 0);
                layer["outline"] = J.O("thickness", outlines ? ot : 0, "opacity", outlines ? oo : 0, "color", "#000000", "blur", 0);
                var fo = Defaults.FiringOptions();
                if (firingError)
                {
                    fo["line"] = J.O("offset", off * 2 + 8);
                    fo["duration"] = 0.12;
                    fo["pressType"] = "hold";
                    fo["releaseBehavior"] = "reverse";
                    fo["easing"] = "easeOutQuad";
                }
                layer["firingOptions"] = fo;
                layers.Add(layer);
            };
            if (separateVertical && vlen != len)
            {
                add(len, new[] { "left", "right" });
                if (vlen > 0) add(vlen, new[] { "top", "bottom" });
            }
            else add(len, new[] { "top", "right", "bottom", "left" });
        }
    }

    /// <summary>Counter-Strike 2 / CS:GO crosshair share codes → Crosshair X layers.</summary>
    public static class CsCrosshair
    {
        static readonly string[] CsgoColors = { "#FA3232", "#32FA32", "#FAFA32", "#3232FA", "#32FAFA" };

        public static ImportResult Import(string code)
        {
            code = code.Trim();
            byte[] bytes;
            bool newFormat = code.StartsWith("CS") && !code.StartsWith("CSGO");
            if (newFormat) bytes = ToBytes(code.Substring(2), 32);
            else bytes = ToBytes(code.Replace("CSGO", "").Replace("-", ""), 18);
            int sum = 0;
            for (int i = 1; i < bytes.Length; i++) sum += bytes[i];
            if (bytes[0] != (sum & 0xFF)) throw new FormatException("Invalid Counter-Strike crosshair code (checksum mismatch).");

            var c = new Cs();
            string note = null;
            if (newFormat)
            {
                if (bytes[1] != 1) throw new FormatException("Unsupported CS2 crosshair code version.");
                int bits = bytes[18] | (bytes[19] << 8) | (bytes[20] << 16) | (bytes[21] << 24);
                c.Style = bytes[4] & 0xF; c.Dot = (bytes[4] & 0x40) != 0; c.TStyle = (bytes[4] & 0x80) != 0;
                c.OutlineMode = bytes[14];
                c.R = bytes[5]; c.G = bytes[6]; c.B = bytes[7]; c.A = bytes[8];
                c.OR = bytes[9]; c.OG = bytes[10]; c.OB = bytes[11]; c.OA = bytes[12];
                c.Gap = (sbyte)bytes[15]; c.Length = bytes[16]; c.Thickness = bytes[13];
                c.SplitDistance = bits & 0x7F;
            }
            else
            {
                switch (bytes[1])
                {
                    case 1:
                        {
                            // CS:GO era (world units) – converted to pixels at 1080p.
                            double gap = (sbyte)bytes[2] / 10.0, outline = bytes[3] / 2.0;
                            int colorIdx = bytes[10] & 7;
                            bool outlineOn = (bytes[10] & 8) == 8;
                            double thickness = bytes[12] / 10.0, length = bytes[14] / 10.0;
                            bool dot = ((bytes[13] >> 4) & 1) == 1, alphaOn = ((bytes[13] >> 4) & 4) == 4, tStyle = ((bytes[13] >> 4) & 8) == 8;
                            c.Style = (bytes[13] & 0xF) >> 1;
                            c.R = bytes[4]; c.G = bytes[5]; c.B = bytes[6]; c.A = alphaOn ? bytes[7] : 255;
                            if (colorIdx < 5)
                            {
                                var col = ColorUtil.Parse(CsgoColors[colorIdx]);
                                c.R = col.R; c.G = col.G; c.B = col.B;
                            }
                            c.Dot = dot; c.TStyle = tStyle;
                            c.Thickness = Math.Max(1, Math.Round(thickness * 2));
                            c.Length = Math.Max(0, Math.Round(length * 2));
                            c.Gap = Math.Round((gap + 5) * 2) / 2; // treated as per-side gap below
                            c.OutlineMode = outlineOn ? 1 : 0;
                            c.OutlineThickness = Math.Max(1, Math.Round(outline));
                            c.OR = c.OG = c.OB = 0; c.OA = 255;
                            c.LegacyUnits = true;
                            note = "Converted from CS:GO units at 1080p; fine-tune size in the designer if needed.";
                            break;
                        }
                    case 3:
                    case 4:
                        {
                            int bits = bytes[10] | (bytes[11] << 8) | (bytes[12] << 16) | (bytes[13] << 24);
                            c.Style = bytes[2] & 0xF; c.Dot = (bytes[2] & 0x40) != 0; c.TStyle = (bytes[2] & 0x80) != 0;
                            c.R = bytes[3]; c.G = bytes[4]; c.B = bytes[5]; c.A = bytes[6];
                            c.Gap = bytes[7]; c.Length = bytes[8];
                            c.Thickness = (bits >> 23) & 0x1F;
                            c.SplitDistance = bits & 0x7F;
                            c.OutlineMode = bytes[1] == 3 ? ((bytes[2] & 0x20) != 0 ? 1 : 0) : (bytes[13] >> 4) & 3;
                            c.OR = c.OG = c.OB = 0; c.OA = 255;
                            break;
                        }
                    default: throw new FormatException("Unsupported Counter-Strike crosshair code version.");
                }
            }
            return new ImportResult { Layers = c.ToLayers(), Name = "CS2 crosshair", Source = "cs2", Kind = CodeImporter.DescribeKind("cs2"), Note = note };
        }

        static byte[] ToBytes(string chars, int len)
        {
            var big = CodeImporter.ParseBase57(chars);
            var le = big.ToByteArray(); // little endian, may include sign byte
            var res = new byte[len];
            for (int i = 0; i < len && i < le.Length; i++) res[len - 1 - i] = le[i];
            return res;
        }

        sealed class Cs
        {
            public int Style, OutlineMode;
            public bool Dot, TStyle, LegacyUnits;
            public int R, G, B, A = 255, OR, OG, OB, OA = 255;
            public double Gap, Length, Thickness, OutlineThickness = 1;
            public int SplitDistance;

            public List<object> ToLayers()
            {
                string color = $"#{R:X2}{G:X2}{B:X2}";
                string ocolor = $"#{OR:X2}{OG:X2}{OB:X2}";
                double alpha = A / 255.0;
                double ot = OutlineMode == 0 ? 0 : OutlineThickness;
                double oo = OutlineMode == 0 ? 0 : OA / 255.0 * (OutlineMode == 2 ? 0.5 : 1);
                double thick = Math.Max(1, Thickness);
                double gapTotal = LegacyUnits ? Math.Max(0, Gap * 2) : Math.Max(0, Gap * 2);
                var layers = new List<object>();
                var layer = Defaults.ModelLayer();
                bool dotOnly = Style == 6;
                bool circle = Style == 1 || Style == 3;
                bool square = Style == 8;
                var dirs = TStyle ? new[] { "right", "bottom", "left" } : new[] { "top", "right", "bottom", "left" };
                layer["line"] = J.O("length", dotOnly || circle || square ? 0 : Length, "thickness", thick, "offset", gapTotal,
                    "opacity", dotOnly || circle || square ? 0 : alpha, "color", color, "visible", dirs, "shape", "rectangle", "blur", 0, "rotation", 0);
                layer["dot"] = J.O("diameter", Dot || dotOnly ? thick : 0, "opacity", Dot || dotOnly ? alpha : 0, "color", color, "shape", "square", "blur", 0);
                layer["outline"] = J.O("thickness", ot, "opacity", oo, "color", ocolor, "blur", 0);
                layers.Add(layer);
                if (circle)
                {
                    var ring = Defaults.ModelLayer();
                    J.Obj(ring, "line")["opacity"] = 0.0;
                    ring["dot"] = J.O("diameter", Math.Max(4, gapTotal + Length), "opacity", alpha, "color", color, "shape", "ring", "blur", 0, "thickness", thick);
                    ring["outline"] = J.O("thickness", ot, "opacity", oo, "color", ocolor, "blur", 0);
                    layers.Add(ring);
                }
                if (square)
                {
                    var sq = Defaults.ShapeLayer("square");
                    double s = Math.Max(4, gapTotal + Length);
                    sq["width"] = s; sq["height"] = s; sq["opacity"] = 0.0;
                    sq["outline"] = J.O("thickness", thick, "opacity", alpha, "color", color, "blur", 0);
                    layers.Add(sq);
                }
                Defaults.NormalizeLayers(layers);
                return layers;
            }
        }
    }
}
