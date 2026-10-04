using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CrosshairY.Input;
using CrosshairY.Overlay;
using Microsoft.Win32;

namespace CrosshairY.Core
{
    /// <summary>Central coordinator: state, overlay, global input, profiles and game detection.</summary>
    public sealed class AppController
    {
        public static AppController I { get; private set; }

        public readonly AppState State = new AppState();
        public OverlayController Overlay { get; private set; }
        public InputHook Input { get; private set; }

        public bool CrosshairVisible { get; private set; } = true;
        public string ForegroundExe { get; private set; } = "";
        public bool InGame { get; private set; }

        /// <summary>When set, the overlay shows these layers instead of the active crosshair (designer live preview).</summary>
        List<object> previewLayers;
        bool aimHeld, aimToggled;
        string manualProfileId;
        System.Windows.Forms.Timer gameTimer, topmostTimer;

        /// <summary>Keybind capture: while set, global input goes here instead of triggering actions.</summary>
        public Action<InputEvent> CaptureHandler;

        public event Action ActiveCrosshairChanged;
        public event Action VisibilityChanged;
        public event Action ProfileChanged;
        public event Action<string> Toast;
        public event Action ReloadPressed;

        public static readonly string[] KnownGames =
        {
            "VALORANT-Win64-Shipping.exe", "cs2.exe", "csgo.exe", "r5apex.exe", "r5apex_dx12.exe", "FortniteClient-Win64-Shipping.exe",
            "RustClient.exe", "cod.exe", "ModernWarfare.exe", "BlackOpsColdWar.exe", "HuntGame.exe", "DayZ_x64.exe",
            "SoTGame.exe", "MCC-Win64-Shipping.exe", "GTA5.exe", "FiveM.exe", "PUBG-Win64-Shipping.exe", "TslGame.exe",
            "Overwatch.exe", "RainbowSix.exe", "RainbowSix_Vulkan.exe", "EscapeFromTarkov.exe", "destiny2.exe",
            "Battlefield2042.exe", "bf2042.exe", "bf1.exe", "bfv.exe", "Minecraft.Windows.exe", "javaw.exe", "RobloxPlayerBeta.exe",
            "TheFinals.exe", "Discovery.exe", "deadlock.exe", "project8.exe", "MarvelRivals_Launcher.exe", "Marvel-Win64-Shipping.exe",
            "XDefiant.exe", "Palworld-Win64-Shipping.exe", "ArmaReforgerSteam.exe", "arma3_x64.exe", "SquadGame.exe",
            "Insurgency.exe", "ReadyOrNot-Win64-Shipping.exe", "Paladins.exe", "Splitgate.exe", "hl2.exe", "left4dead2.exe",
            "Cyberpunk2077.exe", "starfield.exe", "Fallout4.exe", "eldenring.exe", "witcher3.exe", "ShooterGame.exe"
        };

        public static AppController Create()
        {
            I = new AppController();
            return I;
        }

        public void Init()
        {
            State.Load();
            Recoil.LoadCustom();
            Render.ImageCache.DiskCacheDir = Path.Combine(AppState.DataDir, "cache");
            CrosshairVisible = State.Settings.VisibleOnLaunch;
            // keep saved recoil crosshairs in sync with the current weapon defaults (e.g. fire-rate tweaks)
            bool refreshed = false;
            foreach (var e in State.Library) if (Recoil.Refresh(e.Layers)) refreshed = true;
            if (refreshed) State.MarkLibraryChanged();
            manualProfileId = State.Settings.ActiveProfileId;

            Overlay = new OverlayController { FrameRate = State.Settings.FrameRate };
            Overlay.Window.SetCaptureExcluded(!State.Settings.ShowInCapture);
            Render.ImageCache.ImageLoaded += () => { var ctx = uiContext; ctx?.Post(_ => PushCrosshair(), null); };

            uiContext = SynchronizationContext.Current;
            Input = new InputHook(uiContext, OnInput) { ControllerEnabled = State.Settings.ControllerSupport };
            Input.Start();

            gameTimer = new System.Windows.Forms.Timer { Interval = 500 };
            gameTimer.Tick += (s, e) => PollForeground();
            gameTimer.Start();
            topmostTimer = new System.Windows.Forms.Timer { Interval = State.Settings.DisplayMode == "assist" ? 250 : 1500 };
            winEventProc = (hook, evt, hwnd, idObj, idChild, thread, time) =>
            {
                // a window came to the foreground (e.g. a game going fullscreen): put the overlay back on top immediately
                Overlay.Window.EnsureTopmost();
                if (State.Settings.DisplayMode == "assist") burst = 8;
            };
            winEventHook = SetWinEventHook(3, 3, IntPtr.Zero, winEventProc, 0, 0, 0);
            topmostTimer.Tick += (s, e) =>
            {
                topmostTimer.Interval = State.Settings.DisplayMode == "assist" || burst > 0 ? 250 : 1500;
                if (burst > 0) burst--;
                Overlay.Window.EnsureTopmost();
            };
            topmostTimer.Start();

            State.SettingsChanged += () =>
            {
                Input.ControllerEnabled = State.Settings.ControllerSupport;
                Overlay.FrameRate = State.Settings.FrameRate;
                Overlay.Window.SetCaptureExcluded(!State.Settings.ShowInCapture);
                UpdateOverlay();
            };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) => uiContext?.Post(_ => UpdateOverlay(), null);

            PushCrosshair();
            UpdateOverlay();
        }

        SynchronizationContext uiContext;
        int burst;
        WinEventDelegate winEventProc;
        IntPtr winEventHook;
        delegate void WinEventDelegate(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventDelegate proc, uint pid, uint tid, uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool UnhookWinEvent(IntPtr hook);

        // ---------------- force borderless (for games stuck in windowed mode) ----------------

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        struct RECT { public int L, T, R, B; }
        readonly Dictionary<IntPtr, (long style, long ex, Rectangle rect)> borderlessRestore = new Dictionary<IntPtr, (long, long, Rectangle)>();

        public bool IsBorderless(IntPtr hwnd) => borderlessRestore.ContainsKey(hwnd);

        /// <summary>Removes a window's frame and stretches it over the crosshair monitor so the overlay can sit on top.</summary>
        public bool MakeBorderless(IntPtr hwnd)
        {
            if (!IsWindow(hwnd)) return false;
            long style = GetWindowLongPtr(hwnd, -16).ToInt64(), ex = GetWindowLongPtr(hwnd, -20).ToInt64();
            GetWindowRect(hwnd, out var r);
            if (!borderlessRestore.ContainsKey(hwnd)) borderlessRestore[hwnd] = (style, ex, Rectangle.FromLTRB(r.L, r.T, r.R, r.B));
            const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_SYSMENU = 0x00080000, WS_MAXIMIZEBOX = 0x10000, WS_MINIMIZEBOX = 0x20000;
            const long WS_EX_DLGMODALFRAME = 0x1, WS_EX_CLIENTEDGE = 0x200, WS_EX_STATICEDGE = 0x20000, WS_EX_WINDOWEDGE = 0x100;
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MAXIMIZEBOX | WS_MINIMIZEBOX);
            ex &= ~(WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE | WS_EX_WINDOWEDGE);
            SetWindowLongPtr(hwnd, -16, new IntPtr(style));
            SetWindowLongPtr(hwnd, -20, new IntPtr(ex));
            var b = TargetScreen.Bounds;
            Native.SetWindowPos(hwnd, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, 0x0020 | 0x0004 | Native.SWP_NOOWNERZORDER);
            Overlay.Window.EnsureTopmost();
            return true;
        }

        public void RestoreWindow(IntPtr hwnd)
        {
            if (!borderlessRestore.TryGetValue(hwnd, out var o)) return;
            borderlessRestore.Remove(hwnd);
            if (!IsWindow(hwnd)) return;
            SetWindowLongPtr(hwnd, -16, new IntPtr(o.style));
            SetWindowLongPtr(hwnd, -20, new IntPtr(o.ex));
            Native.SetWindowPos(hwnd, IntPtr.Zero, o.rect.X, o.rect.Y, o.rect.Width, o.rect.Height, 0x0020 | 0x0004 | Native.SWP_NOOWNERZORDER);
        }

        public void Shutdown()
        {
            try { gameTimer?.Stop(); topmostTimer?.Stop(); if (winEventHook != IntPtr.Zero) UnhookWinEvent(winEventHook); } catch { }
            foreach (var h in borderlessRestore.Keys.ToList()) { try { RestoreWindow(h); } catch { } }
            try { Input?.Dispose(); } catch { }
            try { Overlay?.Dispose(); } catch { }
            State.SaveNow();
        }

        // ---------------- crosshair selection ----------------

        public Profile Profile => State.ActiveProfile;
        public CrosshairEntry ActiveCrosshair => State.Find(Profile.CrosshairId);

        public void ApplyCrosshair(string id, bool notify = false)
        {
            var e = State.Find(id);
            if (e == null) return;
            Profile.CrosshairId = id;
            e.LastUsed = DateTime.UtcNow;
            State.MarkLibraryChanged();
            State.MarkProfilesChanged();
            PushCrosshair();
            ActiveCrosshairChanged?.Invoke();
            if (notify) Toast?.Invoke("Crosshair: " + e.Name);
        }

        public void SetPreview(List<object> layers)
        {
            previewLayers = layers;
            PushCrosshair();
        }

        public bool IsPreviewing => previewLayers != null;

        /// <summary>Re-sends the effective crosshair (after edits to the active entry, preview changes, aim switching...).</summary>
        public void PushCrosshair()
        {
            List<object> layers = previewLayers;
            if (layers == null)
            {
                var p = Profile;
                CrosshairEntry e = null;
                if (p.AimAction == "switch" && AimActive) e = State.Find(p.AimCrosshairId);
                if (e == null) e = State.Find(p.CrosshairId);
                layers = e?.Layers;
            }
            if (layers != null && overlayLabel != null && DateTime.UtcNow < overlayLabelUntil)
            {
                // brief on-screen label (e.g. the weapon you just switched to), drawn under the crosshair
                layers = new List<object>(layers);
                var t = Defaults.TextLayer(overlayLabel);
                t["fontSize"] = 13.0;
                t["position"] = J.O("x", 0, "y", 46);
                t["outline"] = J.O("thickness", 2, "opacity", 1, "color", "#000000", "blur", 0);
                t["firingOptions"] = J.O();
                layers.Add(t);
            }
            Overlay?.SetCrosshair(layers);
        }

        string overlayLabel;
        DateTime overlayLabelUntil;
        System.Windows.Forms.Timer labelTimer;

        /// <summary>Shows a short label on the overlay for ~1.2 s (visible in game).</summary>
        public void ShowOverlayLabel(string text)
        {
            overlayLabel = text;
            overlayLabelUntil = DateTime.UtcNow.AddMilliseconds(1200);
            PushCrosshair();
            if (labelTimer == null)
            {
                labelTimer = new System.Windows.Forms.Timer { Interval = 1250 };
                labelTimer.Tick += (s, e) => { labelTimer.Stop(); overlayLabel = null; PushCrosshair(); };
            }
            labelTimer.Stop();
            labelTimer.Start();
        }

        public void CycleCrosshair(int dir)
        {
            var list = State.Library.OrderBy(x => x.Order).ToList();
            if (list.Count == 0) return;
            int i = list.FindIndex(x => x.Id == Profile.CrosshairId);
            i = ((i < 0 ? 0 : i + dir) % list.Count + list.Count) % list.Count;
            ApplyCrosshair(list[i].Id, true);
        }

        /// <summary>Switches the active recoil crosshair to a loadout slot's weapon ("off" disables tracking).</summary>
        public void SelectRecoilSlot(RecoilSlot slot)
        {
            // per-weapon crosshair: switch design first, then point its tracker(s) at the slot's gun
            bool switched = false;
            if (!string.IsNullOrEmpty(slot.CrosshairId) && slot.CrosshairId != Profile.CrosshairId && State.Find(slot.CrosshairId) != null)
            {
                ApplyCrosshair(slot.CrosshairId);
                switched = true;
            }
            var e = ActiveCrosshair;
            var pattern = slot.Weapon == "off" ? null : Recoil.Find(slot.Weapon);
            if (e == null || !Recoil.HasRecoil(e.Layers)) { ShowOverlayLabel(switched ? pattern?.Name ?? e?.Name ?? "" : "No recoil crosshair"); return; }
            if (pattern == null && slot.Weapon != "off") return;
            Recoil.SetWeapon(e.Layers, pattern, slot.Scale > 0 ? slot.Scale : 1);
            e.Updated = DateTime.UtcNow;
            State.MarkLibraryChanged();
            PushCrosshair();
            if (pattern != null) WeaponChanged?.Invoke(pattern);
            ShowOverlayLabel(pattern == null ? "Recoil off" : pattern.Name);
        }

        /// <summary>Index of the loadout slot matching the active crosshair's current weapon, or -1.</summary>
        int CurrentSlotIndex(Profile p, CrosshairEntry e)
        {
            if (Recoil.IsOff(e.Layers)) return p.RecoilSlots.FindIndex(s => s.Weapon == "off");
            var w = Recoil.CurrentWeapon(e.Layers);
            return w == null ? -1 : p.RecoilSlots.FindIndex(s => s.Weapon == w.Key);
        }

        /// <summary>Next/previous gun: through the profile's recoil loadout if one is set up, otherwise through the game's weapons.</summary>
        public void CycleWeapon(int dir)
        {
            var e = ActiveCrosshair;
            if (e == null || !Recoil.HasRecoil(e.Layers)) { Toast?.Invoke("The active crosshair has no recoil pattern"); ShowOverlayLabel("No recoil crosshair"); return; }
            var prof = Profile;
            var slots = prof.RecoilSlots.Where(s => s.Weapon == "off" || Recoil.Find(s.Weapon) != null).ToList();
            if (slots.Count > 0)
            {
                int i = CurrentSlotIndex(prof, e);
                int cur = i < 0 ? -1 : slots.IndexOf(prof.RecoilSlots[i]);
                SelectRecoilSlot(slots[((cur + dir) % slots.Count + slots.Count) % slots.Count]);
                return;
            }
            var next = Recoil.Cycle(Recoil.CurrentWeapon(e.Layers), dir);
            Recoil.SetWeapon(e.Layers, next);
            e.Updated = DateTime.UtcNow;
            State.MarkLibraryChanged();
            PushCrosshair();
            WeaponChanged?.Invoke(next);
            ShowOverlayLabel(next.Name);
            Toast?.Invoke("Weapon: " + next.Name);
        }

        public event Action<RecoilPattern> WeaponChanged;

        /// <summary>Plays the hit marker (or kill flash) around the crosshair.</summary>
        public void React(bool kill)
        {
            var s = State.Settings;
            Overlay?.React(Reactions.Build(s, kill), kill ? (int)(s.ReactionMs * 1.6) : s.ReactionMs);
        }

        // ---------------- visibility & placement ----------------

        bool AimActive => Profile.AimToggleMode ? aimToggled : aimHeld;

        public void ToggleVisible() => SetVisible(!CrosshairVisible);

        public void SetVisible(bool v)
        {
            CrosshairVisible = v;
            UpdateOverlay();
            VisibilityChanged?.Invoke();
            if (State.Settings.ShowTrayNotifications) Toast?.Invoke(v ? "Crosshair shown" : "Crosshair hidden");
        }

        public bool EffectiveVisible
        {
            get
            {
                var p = Profile;
                bool show = CrosshairVisible;
                if (p.AimAction == "hide" && AimActive) show = false;
                if (p.AimAction == "show") show = show && AimActive;
                if (State.Settings.OnlyShowInGame && !InGame && previewLayers == null) show = false;
                return show;
            }
        }

        public Screen TargetScreen
        {
            get
            {
                var name = State.Settings.Monitor;
                return Screen.AllScreens.FirstOrDefault(s => s.DeviceName == name) ?? Screen.PrimaryScreen;
            }
        }

        public double DpiScaleFor(Screen s)
        {
            if (State.Settings.IgnoreDpiScaling) return 1;
            try
            {
                var c = new Native.POINT(s.Bounds.Left + s.Bounds.Width / 2, s.Bounds.Top + s.Bounds.Height / 2);
                var mon = Native.MonitorFromPoint(c, 2);
                if (Native.GetDpiForMonitor(mon, 0, out uint dx, out _) == 0) return dx / 96.0;
            }
            catch { }
            return 1;
        }

        public void UpdateOverlay()
        {
            if (Overlay == null) return;
            var p = Profile;
            var scr = TargetScreen;
            Overlay.SetPlacement(scr.Bounds, p.OffsetX, p.OffsetY, Math.Max(0.1, p.Scale) * DpiScaleFor(scr));
            Overlay.SetVisible(EffectiveVisible, Math.Max(0, Math.Min(1, p.Opacity)));
        }

        public void Nudge(int dx, int dy)
        {
            Profile.OffsetX += dx;
            Profile.OffsetY += dy;
            State.MarkProfilesChanged();
            UpdateOverlay();
            ProfileChanged?.Invoke();
        }

        public void CenterPosition()
        {
            Profile.OffsetX = 0;
            Profile.OffsetY = 0;
            State.MarkProfilesChanged();
            UpdateOverlay();
            ProfileChanged?.Invoke();
        }

        public void ChangeScale(double delta)
        {
            Profile.Scale = Math.Round(Math.Max(0.25, Math.Min(8, Profile.Scale + delta)), 2);
            State.MarkProfilesChanged();
            UpdateOverlay();
            ProfileChanged?.Invoke();
        }

        // ---------------- profiles ----------------

        public void ActivateProfile(string id, bool manual = true)
        {
            if (State.Profiles.All(p => p.Id != id)) return;
            if (manual) manualProfileId = id;
            if (State.Settings.ActiveProfileId == id) return;
            State.Settings.ActiveProfileId = id;
            aimHeld = aimToggled = false;
            State.MarkSettingsChanged();
            PushCrosshair();
            UpdateOverlay();
            ProfileChanged?.Invoke();
            ActiveCrosshairChanged?.Invoke();
        }

        public void NotifyProfileEdited()
        {
            State.MarkProfilesChanged();
            PushCrosshair();
            UpdateOverlay();
            ProfileChanged?.Invoke();
        }

        void PollForeground()
        {
            string exe = "";
            try
            {
                var hwnd = Native.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    Native.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid != 0 && pid != (uint)Process.GetCurrentProcess().Id) exe = ExeNameOf(pid);
                    else if (pid == (uint)Process.GetCurrentProcess().Id) exe = ForegroundExe; // keep last external app
                }
            }
            catch { }
            if (exe == ForegroundExe && lastPollDone) return;
            lastPollDone = true;
            ForegroundExe = exe;
            bool inGame = !string.IsNullOrEmpty(exe) &&
                (State.Settings.GameProcesses.Any(g => Same(g, exe)) || State.Profiles.Any(p => p.Processes.Any(g => Same(g, exe))));
            if (inGame != InGame) { InGame = inGame; UpdateOverlay(); }

            if (State.Settings.ProfileDetection && !string.IsNullOrEmpty(exe))
            {
                var linked = State.Profiles.FirstOrDefault(p => p.Processes.Any(g => Same(g, exe)));
                if (linked != null && linked.Id != State.Settings.ActiveProfileId)
                {
                    ActivateProfile(linked.Id, manual: false);
                    Toast?.Invoke("Profile: " + linked.Name);
                }
                else if (linked == null && manualProfileId != null && manualProfileId != State.Settings.ActiveProfileId
                         && State.Profiles.Any(p => p.Id == manualProfileId))
                {
                    var current = State.ActiveProfile;
                    if (current.Processes.Count > 0) ActivateProfile(manualProfileId, manual: true);
                }
            }
        }

        bool lastPollDone;

        static bool Same(string a, string b) =>
            string.Equals(Path.GetFileNameWithoutExtension(a ?? ""), Path.GetFileNameWithoutExtension(b ?? ""), StringComparison.OrdinalIgnoreCase);

        public static string ExeNameOf(uint pid)
        {
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (Native.QueryFullProcessImageName(h, 0, sb, ref size)) return Path.GetFileName(sb.ToString());
                }
                finally { Native.CloseHandle(h); }
            }
            try { return Process.GetProcessById((int)pid).ProcessName + ".exe"; } catch { return ""; }
        }

        // ---------------- input ----------------

        void OnInput(InputEvent ev)
        {
            if (CaptureHandler != null)
            {
                CaptureHandler(ev);
                return;
            }
            var p = Profile;
            bool isDown = ev.Down;

            bool pad = State.Settings.ControllerSupport;
            // fire / aim are hold-style triggers, matched without modifiers
            if (Matches(p.FireKey, ev, true) || (pad && Matches(p.FirePad, ev, true)))
            {
                Overlay.TriggerInput("left", isDown);
                if (isDown && State.Settings.HitOnFire) React(false);
            }
            if (Matches(p.AimKey, ev, true) || (pad && Matches(p.AimPad, ev, true)))
            {
                Overlay.TriggerInput("right", isDown);
                bool before = AimActive;
                aimHeld = isDown;
                if (isDown && p.AimToggleMode) aimToggled = !aimToggled;
                if (before != AimActive)
                {
                    if (p.AimAction == "switch") PushCrosshair();
                    UpdateOverlay();
                }
            }

            if (!isDown) return;
            if (Matches(p.ToggleKey, ev) || (pad && Matches(p.TogglePad, ev))) ToggleVisible();
            if (Matches(p.ReloadKey, ev) || (pad && Matches(p.ReloadPad, ev))) { Overlay.ResetAnimations(); ReloadPressed?.Invoke(); }
            if (Matches(p.NextKey, ev) || (pad && Matches(p.NextPad, ev))) CycleCrosshair(1);
            if (Matches(p.PrevKey, ev) || (pad && Matches(p.PrevPad, ev))) CycleCrosshair(-1);
            if (Matches(p.NextWeaponKey, ev) || (pad && Matches(p.NextWeaponPad, ev))) CycleWeapon(1);
            if (Matches(p.PrevWeaponKey, ev) || (pad && Matches(p.PrevWeaponPad, ev))) CycleWeapon(-1);
            string pk = p.PositionKeys;
            bool presetMods = (ev.Mods & (Mods.Alt | Mods.Shift)) == (Mods.Alt | Mods.Shift);
            string up = pk == "arrows" ? "Up" : pk == "numpad" ? "Num8" : null, down = pk == "arrows" ? "Down" : pk == "numpad" ? "Num2" : null,
                   left = pk == "arrows" ? "Left" : pk == "numpad" ? "Num4" : null, right = pk == "arrows" ? "Right" : pk == "numpad" ? "Num6" : null;
            if (Matches(p.MoveUpKey, ev) || (presetMods && ev.Token == up)) Nudge(0, -1);
            if (Matches(p.MoveDownKey, ev) || (presetMods && ev.Token == down)) Nudge(0, 1);
            if (Matches(p.MoveLeftKey, ev) || (presetMods && ev.Token == left)) Nudge(-1, 0);
            if (Matches(p.MoveRightKey, ev) || (presetMods && ev.Token == right)) Nudge(1, 0);
            if (Matches(p.CenterKey, ev)) CenterPosition();
            if (Matches(p.SizeUpKey, ev)) ChangeScale(0.25);
            if (Matches(p.SizeDownKey, ev)) ChangeScale(-0.25);
            if (Matches(p.HitKey, ev) || (pad && Matches(p.HitPad, ev))) React(false);
            if (Matches(p.KillKey, ev) || (pad && Matches(p.KillPad, ev))) React(true);
            foreach (var slot in p.RecoilSlots.ToList())
                if (Matches(slot.Key, ev) || (pad && Matches(slot.PadKey, ev))) { SelectRecoilSlot(slot); break; }
            foreach (var b in p.CrosshairBinds.ToList())
                if ((Matches(b.Key, ev) || (pad && Matches(b.PadKey, ev))) && State.Find(b.CrosshairId) != null) { ApplyCrosshair(b.CrosshairId, true); break; }
        }

        static bool Matches(string bind, InputEvent ev, bool ignoreMods = false)
        {
            if (string.IsNullOrEmpty(bind)) return false;
            var kb = KeyBinding.Parse(bind);
            if (ignoreMods) return string.Equals(kb.Key, ev.Token, StringComparison.OrdinalIgnoreCase);
            return kb.Matches(ev.Token, ev.Mods);
        }

        // ---------------- startup ----------------

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetLaunchOnStartup(bool enable)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enable) k.SetValue("CrosshairY", "\"" + Application.ExecutablePath + "\" --startup");
                    else k.DeleteValue("CrosshairY", false);
                }
            }
            catch { }
        }
    }
}
