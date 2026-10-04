using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Reticly.Core;

namespace Reticly.Input
{
    public struct InputEvent
    {
        public string Token;   // "A", "F9", "LMB", "Mouse4", "WheelUp", "PadRT", ...
        public bool Down;
        public Mods Mods;      // modifiers held at the time of the event
        public bool IsModifier;
    }

    /// <summary>
    /// Global input via low-level keyboard/mouse hooks running on a dedicated thread (so a busy UI never
    /// delays or drops input), plus XInput controller polling. Events are posted to the UI thread.
    /// Input is only observed, never blocked.
    /// </summary>
    public sealed class InputHook : IDisposable
    {
        readonly SynchronizationContext ui;
        readonly Action<InputEvent> handler;
        Thread thread;
        uint threadId;
        IntPtr kbHook, msHook;
        Native.HookProc kbProc, msProc;   // keep delegates alive
        readonly HashSet<string> down = new HashSet<string>();
        readonly object downLock = new object();
        Timer padTimer;
        ushort lastButtons;
        bool lastLT, lastRT;
        int padIndex = -1;
        bool useXInput14 = true;
        public bool ControllerEnabled;

        public InputHook(SynchronizationContext uiContext, Action<InputEvent> onEvent)
        {
            ui = uiContext;
            handler = onEvent;
        }

        public void Start()
        {
            thread = new Thread(Run) { IsBackground = true, Name = "InputHook", Priority = ThreadPriority.AboveNormal };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            padTimer = new Timer(_ => PollPad(), null, 500, 8);
        }

        void Run()
        {
            threadId = Native.GetCurrentThreadId();
            kbProc = KeyboardProc;
            msProc = MouseProc;
            IntPtr mod = Native.GetModuleHandle(null);
            kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, kbProc, mod, 0);
            msHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, msProc, mod, 0);
            while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) { }
            if (kbHook != IntPtr.Zero) Native.UnhookWindowsHookEx(kbHook);
            if (msHook != IntPtr.Zero) Native.UnhookWindowsHookEx(msHook);
        }

        static Mods CurrentMods()
        {
            Mods m = Mods.None;
            if ((Native.GetAsyncKeyState(0x11) & 0x8000) != 0) m |= Mods.Ctrl;
            if ((Native.GetAsyncKeyState(0x10) & 0x8000) != 0) m |= Mods.Shift;
            if ((Native.GetAsyncKeyState(0x12) & 0x8000) != 0) m |= Mods.Alt;
            if ((Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0) m |= Mods.Win;
            return m;
        }

        void Emit(string token, bool isDown, bool isModifier = false, bool momentary = false)
        {
            if (!momentary)
            {
                lock (downLock)
                {
                    if (isDown) { if (!down.Add(token)) return; }   // ignore auto-repeat
                    else if (!down.Remove(token)) return;
                }
            }
            var ev = new InputEvent { Token = token, Down = isDown, Mods = CurrentMods(), IsModifier = isModifier };
            ui.Post(_ => { try { handler(ev); } catch { } }, null);
        }

        IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                int msg = wParam.ToInt32();
                bool isDown = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                bool isUp = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
                if (isDown || isUp)
                {
                    int vk = (int)k.vkCode;
                    Emit(KeyBinding.TokenFromVk(vk), isDown, KeyBinding.IsModifierVk(vk));
                }
            }
            return Native.CallNextHookEx(kbHook, nCode, wParam, lParam);
        }

        IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                switch (msg)
                {
                    case Native.WM_LBUTTONDOWN: Emit("LMB", true); break;
                    case Native.WM_LBUTTONUP: Emit("LMB", false); break;
                    case Native.WM_RBUTTONDOWN: Emit("RMB", true); break;
                    case Native.WM_RBUTTONUP: Emit("RMB", false); break;
                    case Native.WM_MBUTTONDOWN: Emit("MMB", true); break;
                    case Native.WM_MBUTTONUP: Emit("MMB", false); break;
                    case Native.WM_XBUTTONDOWN:
                    case Native.WM_XBUTTONUP:
                        {
                            var m = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                            int x = (int)(m.mouseData >> 16) & 0xFFFF;
                            Emit(x == 1 ? "Mouse4" : "Mouse5", msg == Native.WM_XBUTTONDOWN);
                            break;
                        }
                    case Native.WM_MOUSEWHEEL:
                        {
                            var m = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                            short delta = (short)((m.mouseData >> 16) & 0xFFFF);
                            string t = delta > 0 ? "WheelUp" : "WheelDown";
                            Emit(t, true, momentary: true);
                            Emit(t, false, momentary: true);
                            break;
                        }
                }
            }
            return Native.CallNextHookEx(msHook, nCode, wParam, lParam);
        }

        static readonly (ushort mask, string name)[] PadButtons =
        {
            (0x1000, "PadA"), (0x2000, "PadB"), (0x4000, "PadX"), (0x8000, "PadY"), (0x0100, "PadLB"), (0x0200, "PadRB"),
            (0x0020, "PadBack"), (0x0010, "PadStart"), (0x0040, "PadLS"), (0x0080, "PadRS"),
            (0x0001, "PadUp"), (0x0002, "PadDown"), (0x0004, "PadLeft"), (0x0008, "PadRight")
        };

        void PollPad()
        {
            if (!ControllerEnabled) return;
            try
            {
                Native.XINPUT_STATE st = default;
                bool got = false;
                int start = padIndex >= 0 ? padIndex : 0;
                for (int n = 0; n < 4 && !got; n++)
                {
                    int i = (start + n) % 4;
                    int r;
                    try { r = useXInput14 ? Native.XInputGetState14(i, out st) : Native.XInputGetState910(i, out st); }
                    catch (DllNotFoundException) { useXInput14 = false; r = Native.XInputGetState910(i, out st); }
                    if (r == 0) { got = true; padIndex = i; }
                }
                if (!got) { padIndex = -1; return; }
                ushort b = st.Gamepad.wButtons;
                foreach (var (mask, name) in PadButtons)
                {
                    bool now = (b & mask) != 0, before = (lastButtons & mask) != 0;
                    if (now != before) Emit(name, now);
                }
                lastButtons = b;
                bool lt = st.Gamepad.bLeftTrigger > 40, rt = st.Gamepad.bRightTrigger > 40;
                if (lt != lastLT) Emit("PadLT", lt);
                if (rt != lastRT) Emit("PadRT", rt);
                lastLT = lt; lastRT = rt;
            }
            catch { ControllerEnabled = false; }
        }

        public bool IsDown(string token)
        {
            lock (downLock) return down.Contains(token);
        }

        public void Dispose()
        {
            padTimer?.Dispose();
            if (threadId != 0) Native.PostThreadMessage(threadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
