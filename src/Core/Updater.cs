using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reticly.Core
{
    public sealed class UpdateInfo
    {
        public string Version, Notes, PageUrl, DownloadUrl, Sha256;
        public long Size;
    }

    /// <summary>
    /// Checks GitHub Releases for a newer Reticly, downloads the exe, verifies its SHA-256 against the digest GitHub
    /// publishes, swaps it in (a running exe can be renamed) and restarts.
    /// </summary>
    public static class Updater
    {
        public const string Repo = "corund207/Reticly";
        const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
        public const string ReleasesPage = "https://github.com/" + Repo + "/releases";

        static Updater()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        static WebClient Client()
        {
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "Reticly/" + Program.Version;
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        /// <summary>Returns the latest release if it is newer than this build, otherwise null. Throws on network errors.</summary>
        public static Task<UpdateInfo> CheckAsync() => Task.Run(() =>
        {
            string json;
            using (var wc = Client()) json = wc.DownloadString(LatestApi);
            var root = Json.Parse(json);
            string tag = (J.Str(root, "tag_name") ?? "").TrimStart('v', 'V');
            var asset = (J.List(root, "assets") ?? new System.Collections.Generic.List<object>())
                .FirstOrDefault(a => string.Equals(J.Str(a, "name"), "Reticly.exe", StringComparison.OrdinalIgnoreCase) || string.Equals(J.Str(a, "name"), "CrosshairY.exe", StringComparison.OrdinalIgnoreCase));   // releases before the rename used the old name
            if (!IsNewer(tag, Program.Version) || asset == null) return null;
            string digest = J.Str(asset, "digest") ?? "";
            return new UpdateInfo
            {
                Version = tag,
                Notes = J.Str(root, "body") ?? "",
                PageUrl = J.Str(root, "html_url") ?? ReleasesPage,
                DownloadUrl = J.Str(asset, "browser_download_url"),
                Sha256 = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest.Substring(7).ToLowerInvariant() : null,
                Size = (long)J.Num(asset, "size")
            };
        });

        public static bool IsNewer(string candidate, string current)
        {
            Version a, b;
            if (!System.Version.TryParse(Normalize(candidate), out a) || !System.Version.TryParse(Normalize(current), out b)) return false;
            return a > b;
        }

        static string Normalize(string v)
        {
            v = (v ?? "").Trim().TrimStart('v', 'V');
            int dash = v.IndexOfAny(new[] { '-', '+' });
            if (dash >= 0) v = v.Substring(0, dash);
            return v.Count(c => c == '.') == 0 ? v + ".0" : v;
        }

        /// <summary>Downloads the new exe to %TEMP% and verifies it. Returns the file path.</summary>
        public static Task<string> DownloadAsync(UpdateInfo u, Action<double> progress) => Task.Run(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), "Reticly-" + u.Version + ".exe");
            var done = new System.Threading.ManualResetEventSlim();
            Exception error = null;
            using (var wc = Client())
            {
                wc.DownloadProgressChanged += (s, e) => progress?.Invoke(e.TotalBytesToReceive > 0 ? e.BytesReceived / (double)e.TotalBytesToReceive : 0);
                wc.DownloadFileCompleted += (s, e) => { error = e.Error; done.Set(); };
                wc.DownloadFileAsync(new Uri(u.DownloadUrl), path);
                done.Wait();
            }
            if (error != null) throw error;
            if (u.Sha256 != null)
            {
                string actual;
                using (var sha = SHA256.Create()) using (var f = File.OpenRead(path))
                    actual = BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
                if (actual != u.Sha256)
                {
                    try { File.Delete(path); } catch { }
                    throw new InvalidDataException("The download didn't match the release checksum. Nothing was changed.");
                }
            }
            return path;
        });

        /// <summary>Swaps the running exe for the downloaded one and starts it. The caller exits the app afterwards.</summary>
        public static void Install(string newExe)
        {
            string cur = Application.ExecutablePath;
            string old = Path.Combine(Path.GetDirectoryName(cur), Path.GetFileNameWithoutExtension(cur) + ".old.exe");
            try { if (File.Exists(old)) File.Delete(old); } catch { }
            File.Move(cur, old);
            try { File.Copy(newExe, cur, true); }
            catch { File.Move(old, cur); throw; }   // put the working version back
            try { File.Delete(newExe); } catch { }
            Process.Start(new ProcessStartInfo(cur, "--updated") { UseShellExecute = false });
        }

        /// <summary>Removes the previous version left behind by an update.</summary>
        public static void CleanupOld()
        {
            try
            {
                string cur = Application.ExecutablePath;
                string old = Path.Combine(Path.GetDirectoryName(cur), Path.GetFileNameWithoutExtension(cur) + ".old.exe");
                if (File.Exists(old)) File.Delete(old);
            }
            catch { }
        }

        /// <summary>True when the automatic once-a-day check is due.</summary>
        public static bool AutoCheckDue(Settings s) =>
            s.AutoUpdate && (!DateTime.TryParse(s.LastUpdateCheck, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last) || (DateTime.UtcNow - last).TotalHours >= 20);
    }
}
