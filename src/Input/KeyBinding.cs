using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CrosshairY.Input
{
    [Flags]
    public enum Mods { None = 0, Ctrl = 1, Shift = 2, Alt = 4, Win = 8 }

    /// <summary>
    /// A key or button binding, serialized as text like "F9", "Ctrl+Shift+X", "Mouse4", "RMB", "WheelUp", "PadRT".
    /// A binding without modifiers also fires when modifiers are held (as in Crosshair X).
    /// </summary>
    public struct KeyBinding
    {
        public string Key;   // canonical key token (no modifiers)
        public Mods Mods;

        public bool IsEmpty => string.IsNullOrEmpty(Key);

        public static KeyBinding Parse(string text)
        {
            var kb = new KeyBinding();
            if (string.IsNullOrWhiteSpace(text)) return kb;
            var parts = text.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (text.EndsWith("++")) parts.Add("+");
            foreach (var p in parts)
            {
                switch (p.ToLowerInvariant())
                {
                    case "ctrl": case "control": kb.Mods |= Mods.Ctrl; break;
                    case "shift": kb.Mods |= Mods.Shift; break;
                    case "alt": kb.Mods |= Mods.Alt; break;
                    case "win": kb.Mods |= Mods.Win; break;
                    default: kb.Key = Canon(p); break;
                }
            }
            return kb;
        }

        static string Canon(string p)
        {
            foreach (var n in AllTokens) if (string.Equals(n, p, StringComparison.OrdinalIgnoreCase)) return n;
            return p;
        }

        public override string ToString()
        {
            if (IsEmpty) return "";
            var parts = new List<string>();
            if (Mods.HasFlag(Mods.Ctrl)) parts.Add("Ctrl");
            if (Mods.HasFlag(Mods.Shift)) parts.Add("Shift");
            if (Mods.HasFlag(Mods.Alt)) parts.Add("Alt");
            if (Mods.HasFlag(Mods.Win)) parts.Add("Win");
            parts.Add(Key);
            return string.Join("+", parts);
        }

        public static string Display(string text)
        {
            var kb = Parse(text);
            if (kb.IsEmpty) return "Not set";
            var parts = new List<string>();
            if (kb.Mods.HasFlag(Mods.Ctrl)) parts.Add("Ctrl");
            if (kb.Mods.HasFlag(Mods.Shift)) parts.Add("Shift");
            if (kb.Mods.HasFlag(Mods.Alt)) parts.Add("Alt");
            if (kb.Mods.HasFlag(Mods.Win)) parts.Add("Win");
            parts.Add(Friendly(kb.Key));
            return string.Join(" + ", parts);
        }

        static string Friendly(string k)
        {
            switch (k)
            {
                case "LMB": return "Left Mouse";
                case "RMB": return "Right Mouse";
                case "MMB": return "Middle Mouse";
                case "Mouse4": return "Mouse 4";
                case "Mouse5": return "Mouse 5";
                case "WheelUp": return "Wheel Up";
                case "WheelDown": return "Wheel Down";
                case "PadLT": return "Left Trigger";
                case "PadRT": return "Right Trigger";
                case "PadLB": return "Left Bumper";
                case "PadRB": return "Right Bumper";
                default:
                    if (k.StartsWith("Pad")) return "Pad " + k.Substring(3);
                    return k;
            }
        }

        public bool Matches(string token, Mods held)
        {
            if (IsEmpty || !string.Equals(Key, token, StringComparison.OrdinalIgnoreCase)) return false;
            if (Mods == Mods.None) return true;
            return (held & Mods) == Mods;
        }

        // ---- virtual key <-> token mapping ----

        static readonly Dictionary<int, string> vkNames = BuildVkNames();
        static readonly Dictionary<string, int> nameVks = vkNames.GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<string> AllTokens =>
            vkNames.Values.Concat(new[] { "LMB", "RMB", "MMB", "Mouse4", "Mouse5", "WheelUp", "WheelDown",
                "PadA", "PadB", "PadX", "PadY", "PadLB", "PadRB", "PadLT", "PadRT", "PadBack", "PadStart", "PadLS", "PadRS",
                "PadUp", "PadDown", "PadLeft", "PadRight" });

        static Dictionary<int, string> BuildVkNames()
        {
            var d = new Dictionary<int, string>();
            for (int i = 0; i < 26; i++) d[0x41 + i] = ((char)('A' + i)).ToString();
            for (int i = 0; i < 10; i++) d[0x30 + i] = i.ToString();
            for (int i = 0; i < 24; i++) d[0x70 + i] = "F" + (i + 1);
            for (int i = 0; i < 10; i++) d[0x60 + i] = "Num" + i;
            d[0x6A] = "Num*"; d[0x6B] = "Num+"; d[0x6D] = "Num-"; d[0x6E] = "Num."; d[0x6F] = "Num/";
            d[0x08] = "Backspace"; d[0x09] = "Tab"; d[0x0D] = "Enter"; d[0x13] = "Pause"; d[0x14] = "CapsLock";
            d[0x1B] = "Esc"; d[0x20] = "Space"; d[0x21] = "PageUp"; d[0x22] = "PageDown"; d[0x23] = "End"; d[0x24] = "Home";
            d[0x25] = "Left"; d[0x26] = "Up"; d[0x27] = "Right"; d[0x28] = "Down"; d[0x2C] = "PrintScreen"; d[0x2D] = "Insert"; d[0x2E] = "Delete";
            d[0x90] = "NumLock"; d[0x91] = "ScrollLock";
            d[0xBA] = ";"; d[0xBB] = "="; d[0xBC] = ","; d[0xBD] = "-"; d[0xBE] = "."; d[0xBF] = "/"; d[0xC0] = "`";
            d[0xDB] = "["; d[0xDC] = "\\"; d[0xDD] = "]"; d[0xDE] = "'";
            d[0xA0] = "LShift"; d[0xA1] = "RShift"; d[0xA2] = "LCtrl"; d[0xA3] = "RCtrl"; d[0xA4] = "LAlt"; d[0xA5] = "RAlt";
            d[0x5B] = "LWin"; d[0x5C] = "RWin"; d[0x5D] = "Menu";
            d[0xAD] = "VolumeMute"; d[0xAE] = "VolumeDown"; d[0xAF] = "VolumeUp"; d[0xB0] = "NextTrack"; d[0xB1] = "PrevTrack"; d[0xB3] = "PlayPause";
            return d;
        }

        public static string TokenFromVk(int vk) => vkNames.TryGetValue(vk, out var n) ? n : "VK" + vk.ToString("X2");
        public static int VkFromToken(string token) => nameVks.TryGetValue(token ?? "", out var vk) ? vk : 0;
        public static bool IsModifierVk(int vk) => vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C;
    }
}
