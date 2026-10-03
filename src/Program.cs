using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CrosshairY.Core;
using CrosshairY.UI;

[assembly: System.Reflection.AssemblyTitle("CrosshairY")]
[assembly: System.Reflection.AssemblyProduct("CrosshairY")]
[assembly: System.Reflection.AssemblyDescription("Custom crosshair overlay for any PC game")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.0.0")]

namespace CrosshairY
{
    public static class Program
    {
        public const string Version = "1.2.0";
        public static int ShowMessage;
        static Mutex mutex;

        [STAThread]
        static void Main(string[] args)
        {
            // Per-monitor DPI awareness so the overlay always works in physical pixels.
            try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); }
            catch { try { Native.SetProcessDPIAware(); } catch { } }

            ShowMessage = Native.RegisterWindowMessage("CrosshairY.ShowWindow.7f3a");
            bool snap = args.Length >= 3 && args[0] == "--snap";   // developer aid: render pages to PNG files
            mutex = new Mutex(true, snap ? "CrosshairY.Snap" : "CrosshairY.SingleInstance.7f3a", out bool first);
            if (!first)
            {
                Native.PostMessage(Native.HWND_BROADCAST, ShowMessage, IntPtr.Zero, IntPtr.Zero);
                return;
            }

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
                        form.Navigate(parts[0]);
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
                        using (var bmp = new System.Drawing.Bitmap(form.Width, form.Height))
                        {
                            // PrintWindow captures the real DWM rendering (custom title bar, floating panels)
                            using (var g = System.Drawing.Graphics.FromImage(bmp))
                            {
                                var hdc = g.GetHdc();
                                bool ok = PrintWindow(form.Handle, hdc, 2);
                                g.ReleaseHdc(hdc);
                                if (!ok) form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                            }
                            bmp.Save(System.IO.Path.Combine(args[2], key.Replace(':', '_') + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
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
                MessageBox.Show("Something went wrong:\n\n" + ex.Message + "\n\nDetails were written to error.log in the CrosshairY data folder.", "CrosshairY", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
