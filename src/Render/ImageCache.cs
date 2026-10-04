using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Reticly.Render
{
    /// <summary>A decoded image, possibly animated (GIF).</summary>
    public sealed class CachedImage
    {
        public Bitmap[] Frames;
        public int[] Delays;      // milliseconds per frame
        public int TotalMs;
        public bool Failed;
        public string Error;
        public bool IsAnimated => Frames != null && Frames.Length > 1 && TotalMs > 0;
        public int Width => Frames != null && Frames.Length > 0 ? Frames[0].Width : 0;
        public int Height => Frames != null && Frames.Length > 0 ? Frames[0].Height : 0;

        public Bitmap FrameAt(long ms)
        {
            if (Frames == null || Frames.Length == 0) return null;
            if (!IsAnimated) return Frames[0];
            long t = ms % TotalMs;
            for (int i = 0; i < Frames.Length; i++)
            {
                if (t < Delays[i]) return Frames[i];
                t -= Delays[i];
            }
            return Frames[Frames.Length - 1];
        }
    }

    /// <summary>
    /// Loads images referenced by layers: data: URLs (custom images / drawings) and http(s) URLs
    /// (Crosshair X stores large custom images on S3). Remote files are cached on disk.
    /// </summary>
    public static class ImageCache
    {
        static readonly ConcurrentDictionary<string, CachedImage> cache = new ConcurrentDictionary<string, CachedImage>();
        static readonly ConcurrentDictionary<string, bool> pending = new ConcurrentDictionary<string, bool>();
        public static string DiskCacheDir;

        /// <summary>Raised (on a worker thread) when an asynchronously downloaded image becomes available.</summary>
        public static event Action ImageLoaded;

        public static CachedImage Get(string href)
        {
            if (string.IsNullOrEmpty(href)) return null;
            string key = KeyOf(href);
            if (cache.TryGetValue(key, out var img)) return img;

            if (href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                img = LoadDataUrl(href);
                cache[key] = img;
                return img;
            }
            if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string file = DiskPath(key);
                if (file != null && File.Exists(file))
                {
                    try
                    {
                        img = Decode(File.ReadAllBytes(file));
                        cache[key] = img;
                        return img;
                    }
                    catch { }
                }
                if (pending.TryAdd(key, true))
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            byte[] data = Download(href);
                            var decoded = Decode(data);
                            if (file != null) { try { File.WriteAllBytes(file, data); } catch { } }
                            cache[key] = decoded;
                        }
                        catch (Exception ex)
                        {
                            cache[key] = new CachedImage { Failed = true, Error = ex.Message };
                        }
                        finally
                        {
                            pending.TryRemove(key, out bool _);
                            ImageLoaded?.Invoke();
                        }
                    });
                }
                return null;
            }
            if (File.Exists(href))
            {
                try { img = Decode(File.ReadAllBytes(href)); }
                catch (Exception ex) { img = new CachedImage { Failed = true, Error = ex.Message }; }
                cache[key] = img;
                return img;
            }
            return null;
        }

        public static byte[] Download(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "Reticly/1.0";
                return wc.DownloadData(url);
            }
        }

        static string DiskPath(string key)
        {
            if (string.IsNullOrEmpty(DiskCacheDir)) return null;
            try { Directory.CreateDirectory(DiskCacheDir); } catch { return null; }
            return Path.Combine(DiskCacheDir, key + ".img");
        }

        static string KeyOf(string href)
        {
            if (href.Length < 64) return Hash(href);
            return Hash(href);
        }

        static string Hash(string s)
        {
            using (var sha = SHA1.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder();
                foreach (var x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        static CachedImage LoadDataUrl(string href)
        {
            try
            {
                int comma = href.IndexOf(',');
                if (comma < 0) throw new FormatException("Malformed data URL");
                string meta = href.Substring(5, comma - 5);
                string payload = href.Substring(comma + 1);
                if (meta.IndexOf("svg", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new CachedImage { Failed = true, Error = "SVG images are not supported" };
                byte[] data = meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                    ? Convert.FromBase64String(payload.Trim())
                    : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
                return Decode(data);
            }
            catch (Exception ex)
            {
                return new CachedImage { Failed = true, Error = ex.Message };
            }
        }

        public static CachedImage Decode(byte[] data)
        {
            if (data.Length > 5 && data[0] == '<')
                return new CachedImage { Failed = true, Error = "SVG images are not supported" };
            using (var ms = new MemoryStream(data))
            using (var src = Image.FromStream(ms))
            {
                var dims = src.FrameDimensionsList;
                int count = 1;
                FrameDimension dim = null;
                if (dims.Length > 0)
                {
                    dim = new FrameDimension(dims[0]);
                    try { count = src.GetFrameCount(dim); } catch { count = 1; }
                }
                var frames = new List<Bitmap>();
                var delays = new List<int>();
                int[] gifDelays = null;
                if (count > 1)
                {
                    try
                    {
                        var prop = src.GetPropertyItem(0x5100);
                        gifDelays = new int[count];
                        for (int i = 0; i < count; i++)
                        {
                            int d = BitConverter.ToInt32(prop.Value, i * 4) * 10;
                            gifDelays[i] = d <= 10 ? 100 : d;
                        }
                    }
                    catch { gifDelays = null; }
                }
                for (int i = 0; i < Math.Min(count, 600); i++)
                {
                    if (count > 1) src.SelectActiveFrame(dim, i);
                    var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
                    }
                    frames.Add(bmp);
                    delays.Add(gifDelays != null ? gifDelays[i] : 100);
                }
                int total = 0;
                foreach (var d in delays) total += d;
                return new CachedImage { Frames = frames.ToArray(), Delays = delays.ToArray(), TotalMs = frames.Count > 1 ? total : 0 };
            }
        }

        public static string ToDataUrl(byte[] data, string mime) => "data:" + mime + ";base64," + Convert.ToBase64String(data);

        public static string MimeFromPath(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".webp": return "image/webp";
                case ".svg": return "image/svg+xml";
                default: return "application/octet-stream";
            }
        }
    }
}
