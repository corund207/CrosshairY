using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CrosshairY.Import;

namespace CrosshairY.Core
{
    public sealed class GalleryItem
    {
        public string Id, Name, Author, Code, Category;
        public List<object> Layers;
    }

    /// <summary>
    /// Community gallery without a server: gallery/gallery.json in the GitHub repo, read from raw.githubusercontent.com and
    /// cached locally for offline use. Submissions are GitHub issues made from a prefilled template.
    /// </summary>
    public static class Gallery
    {
        const string Url = "https://raw.githubusercontent.com/" + Updater.Repo + "/main/gallery/gallery.json";
        const string IssueUrl = "https://github.com/" + Updater.Repo + "/issues/new";

        public static List<GalleryItem> Items { get; private set; }
        public static string Error { get; private set; }
        static string CacheFile => Path.Combine(AppState.DataDir, "cache", "gallery.json");
        static Task<List<GalleryItem>> loading;

        static Gallery()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        /// <summary>Loads the gallery (network first, then the offline cache). Safe to call repeatedly.</summary>
        public static Task<List<GalleryItem>> LoadAsync(bool force = false)
        {
            if (!force && Items != null) return Task.FromResult(Items);
            if (loading != null && !loading.IsCompleted) return loading;
            loading = Task.Run(() =>
            {
                string json = null;
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "CrosshairY/" + Program.Version;
                        wc.Encoding = System.Text.Encoding.UTF8;
                        json = wc.DownloadString(Url + "?t=" + DateTime.UtcNow.Ticks / TimeSpan.TicksPerHour);
                    }
                    try { Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)); File.WriteAllText(CacheFile, json); } catch { }
                    Error = null;
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    try { if (File.Exists(CacheFile)) json = File.ReadAllText(CacheFile); } catch { }
                }
                var list = json == null ? new List<GalleryItem>() : Parse(json);
                Items = list;
                return list;
            });
            return loading;
        }

        public static List<GalleryItem> Parse(string json)
        {
            var list = new List<GalleryItem>();
            foreach (var o in J.List(Json.Parse(json), "crosshairs") ?? new List<object>())
            {
                try
                {
                    string code = J.Str(o, "code");
                    if (string.IsNullOrEmpty(code)) continue;
                    var r = CodeImporter.Import(code);   // CrosshairY codes decode offline
                    list.Add(new GalleryItem
                    {
                        Id = J.Str(o, "id") ?? Guid.NewGuid().ToString("N"),
                        Name = J.Str(o, "name") ?? r.Name,
                        Author = J.Str(o, "author") ?? "",
                        Category = J.Str(o, "category") ?? "",
                        Code = code,
                        Layers = r.Layers
                    });
                }
                catch { /* skip broken entries */ }
            }
            return list;
        }

        /// <summary>Prefilled "submit to gallery" issue for a crosshair (GitHub issue forms accept field values in the URL).</summary>
        public static string SubmitUrl(CrosshairEntry e)
        {
            string code = CodeImporter.ExportOwn(e.Layers, e.Name);
            return IssueUrl + "?template=gallery.yml&labels=gallery&title=" + Uri.EscapeDataString("Gallery: " + e.Name)
                + "&name=" + Uri.EscapeDataString(e.Name) + "&code=" + Uri.EscapeDataString(code);
        }
    }
}
