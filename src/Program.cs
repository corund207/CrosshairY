using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Reticly.Core;
using Reticly.UI;

[assembly: System.Reflection.AssemblyTitle("Reticly")]
[assembly: System.Reflection.AssemblyProduct("Reticly")]
[assembly: System.Reflection.AssemblyDescription("Custom crosshair overlay for any PC game")]
[assembly: System.Reflection.AssemblyVersion("1.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.0.0")]

namespace Reticly
{
    public static class Program
    {
        public const string Version = "1.3.0";
        public static int ShowMessage;
        /// <summary>True while rendering --snap screenshots (no update checks, tour, etc.).</summary>
        public static bool SnapMode;
        static Mutex mutex;

        [STAThread]
        static void Main(string[] args)
        {
            // Per-monitor DPI awareness so the overlay always works in physical pixels.
            try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); }
            catch { try { Native.SetProcessDPIAware(); } catch { } }

            ShowMessage = Native.RegisterWindowMessage("Reticly.ShowWindow.7f3a");
            bool snap = args.Length >= 3 && args[0] == "--snap";   // developer aid: render pages to PNG files
            SnapMode = snap;
            mutex = new Mutex(true, snap ? "Reticly.Snap" : "Reticly.SingleInstance.7f3a", out bool first);
            if (!first && args.Contains("--updated"))
            {
                // started by the updater: wait for the old version to finish exiting
                try { first = mutex.WaitOne(15000); } catch (AbandonedMutexException) { first = true; }
            }
            if (!first)
            {
                Native.PostMessage(Native.HWND_BROADCAST, ShowMessage, IntPtr.Zero, IntPtr.Zero);
                return;
            }
            if (!snap) Updater.CleanupOld();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportError(e.ExceptionObject as Exception);

            float scale = 1f;
            try
            {
                using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            }
            catch { }
            Theme.Init(Math.Max(1f, scale));

            var app = AppController.Create();
            // a SynchronizationContext must exist before Init so input events marshal to the UI thread
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            app.Init();
            L.Use(app.State.Settings.Language);
            if (!app.State.Settings.FirstRunDone)
            {
                SeedLibrary(app.State);
                app.State.Settings.FirstRunDone = true;
                app.State.MarkSettingsChanged();
                app.PushCrosshair();
            }

            bool startup = args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));
            bool minimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase)) || (startup && app.State.Settings.StartMinimized);
            string code = snap ? null : args.FirstOrDefault(a => !a.StartsWith("--"));

            var form = new MainForm();
            if (snap)
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new System.Drawing.Point(-30000, -30000);
                form.ShowInTaskbar = false;
                if (args.Length > 3 && int.TryParse(args[3], out int snapH)) form.Height = snapH;   // optional: taller capture
                System.IO.Directory.CreateDirectory(args[2]);
                form.Shown += (s, e) => form.BeginInvoke(new Action(() =>
                {
                    foreach (var key in args[1].Split(','))
                    {
                        string[] parts = key.Split(':');
                        Form target = form;
                        if (parts[0] == "tour") Tour.Start(form, parts.Length > 1 ? int.Parse(parts[1]) : 0);
                        else if (parts[0] == "patterns" || parts[0] == "update")
                        {
                            // dialogs: shown off-screen and captured on their own
                            target = parts[0] == "patterns"
                                ? UI.Dialogs.PatternEditorDialog.CreateForSnap(Recoil.Find(parts.Length > 1 ? parts[1].Replace('_', ' ').Replace('~', '|') : "VALORANT|Vandal"))
                                : new UI.Dialogs.UpdateDialog(new UpdateInfo { Version = "9.9.9", Sha256 = "x", Notes = "## What's new\n\n- **Auto-update**: this dialog\n- Spray pattern editor\n- Hit markers" });
                            target.StartPosition = FormStartPosition.Manual;
                            target.Location = new System.Drawing.Point(-30000, -30000);
                            target.Show(form);
                        }
                        else form.Navigate(parts[0]);
                        if (parts.Length > 1 && parts[0] == "designer") form.Designer.SelectTabForSnap(parts[1]);
                        if (parts.Length > 1 && parts[0] == "crosshairs") form.Crosshairs.SelectTab(int.Parse(parts[1]));
                        for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(15); }
                        if (parts.Length > 1 && parts[parts.Length - 1] == "end")   // e.g. keybinds:end scrolls the page to the bottom
                        {
                            Func<Control, IEnumerable<Control>> all = null;
                            all = c => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(all(x)));
                            foreach (var sh in all(form).OfType<UI.Controls.ScrollHost>().Where(x => x.Visible))
                                sh.AutoScrollPosition = new System.Drawing.Point(0, 100000);
                            for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(15); }
                        }
                        using (var bmp = new System.Drawing.Bitmap(target.Width, target.Height))
                        {
                            // PrintWindow captures the real DWM rendering (custom title bar, floating panels)
                            using (var g = System.Drawing.Graphics.FromImage(bmp))
                            {
                                var hdc = g.GetHdc();
                                bool ok = PrintWindow(target.Handle, hdc, 2);
                                g.ReleaseHdc(hdc);
                                if (!ok) target.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, target.Width, target.Height));
                            }
                            bmp.Save(System.IO.Path.Combine(args[2], key.Replace(':', '_').Replace('|', '~') + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                        if (target != form) target.Dispose();
                    }
                    form.ExitApp();
                }));
            }
            if (minimized)
            {
                form.WindowState = FormWindowState.Minimized;
                form.ShowInTaskbar = false;
                form.Load += (s, e) => { form.Hide(); form.WindowState = FormWindowState.Normal; form.ShowInTaskbar = true; };
            }
            if (!string.IsNullOrEmpty(code))
                form.Shown += (s, e) => form.BeginInvoke(new Action(() => form.ShowImport(code)));
            Application.ApplicationExit += (s, e) => app.Shutdown();
            Application.Run(form);
            GC.KeepAlive(mutex);
        }

        /// <summary>First-run library: a handful of good starting crosshairs.</summary>
        public static void SeedLibrary(AppState state)
        {
            var picks = new[] { "Classic Green", "Cross + Dot", "Dot Medium", "T With Dot", "Valorant Style", "Ring + Dot", "Bloom on Fire", "RGB Cycle" };
            var all = Presets.All();
            foreach (var name in picks)
            {
                var p = all.FirstOrDefault(x => x.Name == name);
                if (p == null) continue;
                state.Add(new CrosshairEntry { Name = p.Name, Layers = (System.Collections.Generic.List<object>)J.DeepClone(p.Layers), Source = "preset" }, atTop: false);
            }
            var prof = state.ActiveProfile;
            if (string.IsNullOrEmpty(prof.CrosshairId) || state.Find(prof.CrosshairId) == null)
                prof.CrosshairId = state.Library.OrderBy(e => e.Order).FirstOrDefault()?.Id ?? "";
            state.MarkProfilesChanged();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        static int errorCount;

        static void ReportError(Exception ex)
        {
            if (ex == null) return;
            try
            {
                System.IO.Directory.CreateDirectory(AppState.DataDir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppState.DataDir, "error.log"), DateTime.Now.ToString("o") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            if (Interlocked.Increment(ref errorCount) <= 3)
                MessageBox.Show("Something went wrong:\n\n" + ex.Message + "\n\nDetails were written to error.log in the Reticly data folder.", "Reticly", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
