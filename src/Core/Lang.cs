using System;
using System.Collections.Generic;
using System.Linq;

namespace Reticly.Core
{
    /// <summary>
    /// UI translations. Strings are looked up by their English text; anything without a translation stays English, so a
    /// language can be partial. The language is chosen in Settings and applies on the next start.
    /// </summary>
    public static partial class L
    {
        public sealed class Language
        {
            public string Code, Name;
            public Dictionary<string, string> Strings;
        }

        static Dictionary<string, string> active;
        static HashSet<string> activeValues;   // already-translated text passes through unchanged (controls translate too)

        public static string Current { get; private set; } = "en";

        public static Language[] Languages => new[]
        {
            new Language { Code = "en", Name = "English", Strings = new Dictionary<string, string>() },
            new Language { Code = "es", Name = "Español", Strings = Spanish() },
            new Language { Code = "de", Name = "Deutsch", Strings = German() },
            new Language { Code = "fr", Name = "Français", Strings = French() },
            new Language { Code = "pt", Name = "Português (Brasil)", Strings = Portuguese() },
        };

        public static void Use(string code)
        {
            var lang = Languages.FirstOrDefault(l => l.Code == code);
            Current = lang?.Code ?? "en";
            active = lang == null || lang.Code == "en" ? null : lang.Strings;
            activeValues = active == null ? null : new HashSet<string>(active.Values);
        }

        /// <summary>Translates an English UI string (returns it unchanged if there is no translation).</summary>
        public static string T(string english)
        {
            if (active == null || english == null || activeValues.Contains(english)) return english;
            return active.TryGetValue(english, out var t) && !string.IsNullOrEmpty(t) ? t : english;
        }

        /// <summary>Upper-cased translation, for section captions.</summary>
        public static string Upper(string english) => T(english).ToUpperInvariant();
    }
}
