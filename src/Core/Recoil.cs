using System;
using System.Collections.Generic;
using System.Linq;

namespace Reticly.Core
{
    /// <summary>A spray pattern: cumulative per-shot offsets (pixels at 1080p, negative Y = up) and fire rate.</summary>
    public sealed class RecoilPattern
    {
        public string Name, Game, Category;
        public int Rpm;
        public int[][] Points;
        public bool HandTuned;
        /// <summary>Made or edited in the pattern editor (saved to patterns.json).</summary>
        public bool Custom;
        /// <summary>Bullets that land dead center before recoil kicks in (first-shot accuracy). 1 = recoil starts right away.</summary>
        public int StableShots = 1;
        public double ShotMs => 60000.0 / Math.Max(1, Rpm);
        public string Key => Game + "|" + Name;
        public string Label => Category + " · " + Name;
    }

    /// <summary>
    /// Recoil-tracking crosshairs: a layer walks along a weapon's spray pattern while the fire key is held and snaps back on
    /// release — the timed-animation technique community recoil crosshairs use. Every recoil crosshair can switch weapon.
    /// Patterns are approximations (hand-tuned for the most popular rifles, generated from fire rate, magazine and recoil
    /// traits for the rest); use the scale setting to match your resolution and field of view.
    /// </summary>
    public static class Recoil
    {
        static readonly string[] BuiltInGames = { "VALORANT", "Counter-Strike 2", "Rust", "Apex Legends" };

        /// <summary>Built-in games first, then games that only exist as custom patterns.</summary>
        public static string[] Games => BuiltInGames.Concat(Patterns.Select(p => p.Game).Where(g => !BuiltInGames.Contains(g)).Distinct()).ToArray();

        public static readonly List<RecoilPattern> BuiltIn = WithStableShots(Build());

        /// <summary>Built-in patterns with custom ones applied (a custom pattern with a built-in key replaces it).</summary>
        public static List<RecoilPattern> Patterns { get; private set; } = new List<RecoilPattern>(BuiltIn);

        public static List<RecoilPattern> CustomPatterns => Patterns.Where(p => p.Custom).ToList();

        static string CustomFile => System.IO.Path.Combine(AppState.DataDir, "patterns.json");

        /// <summary>Loads patterns.json (custom and edited patterns).</summary>
        public static void LoadCustom()
        {
            try
            {
                if (!System.IO.File.Exists(CustomFile)) return;
                SetCustom((J.List(Json.Parse(System.IO.File.ReadAllText(CustomFile)), "patterns") ?? new List<object>()).Select(PatternFromJson).Where(p => p != null), save: false);
            }
            catch { }
        }

        /// <summary>Replaces the custom pattern set (and saves it).</summary>
        public static void SetCustom(IEnumerable<RecoilPattern> custom, bool save = true)
        {
            var list = custom.ToList();
            foreach (var c in list) c.Custom = true;
            var merged = BuiltIn.Select(b => list.FirstOrDefault(c => c.Key == b.Key) ?? b).ToList();
            merged.AddRange(list.Where(c => BuiltIn.All(b => b.Key != c.Key)));
            Patterns = merged;
            if (!save) return;
            try
            {
                System.IO.Directory.CreateDirectory(AppState.DataDir);
                System.IO.File.WriteAllText(CustomFile, Json.Serialize(J.O("version", 1, "patterns", list.Select(p => (object)PatternToJson(p)).ToList()), true));
            }
            catch { }
        }

        /// <summary>Adds or replaces one custom pattern. If its key changed, the old one is removed.</summary>
        public static void SaveCustom(RecoilPattern p, string previousKey = null)
        {
            var list = CustomPatterns.Where(c => c.Key != p.Key && c.Key != previousKey).ToList();
            list.Add(p);
            SetCustom(list);
        }

        public static void DeleteCustom(string key) => SetCustom(CustomPatterns.Where(c => c.Key != key));

        public static RecoilPattern BuiltInFor(string key) => BuiltIn.FirstOrDefault(b => b.Key == key);

        public static Dictionary<string, object> PatternToJson(RecoilPattern p) => J.O("name", p.Name, "game", p.Game, "category", p.Category, "rpm", p.Rpm,
            "stableShots", p.StableShots, "points", p.Points.Select(pt => (object)J.A(pt[0], pt[1])).ToList());

        public static RecoilPattern PatternFromJson(object o)
        {
            var pts = (J.List(o, "points") ?? new List<object>()).OfType<List<object>>().Where(x => x.Count >= 2)
                .Select(x => new[] { (int)Math.Round(J.ToNum(x[0])), (int)Math.Round(J.ToNum(x[1])) }).ToArray();
            string name = J.Str(o, "name", "").Trim(), game = J.Str(o, "game", "Custom").Trim();
            if (name.Length == 0 || pts.Length < 2) return null;
            return new RecoilPattern { Name = name, Game = game.Length == 0 ? "Custom" : game, Category = J.Str(o, "category", "Custom"),
                Rpm = (int)Math.Max(30, Math.Min(2000, J.Num(o, "rpm", 600))), StableShots = (int)Math.Max(1, Math.Min(15, J.Num(o, "stableShots", 1))), Points = pts, Custom = true };
        }

        /// <summary>
        /// First-shot accuracy per weapon: how many bullets hit the center before the spray starts climbing.
        /// Defaults come from the weapon type; well-known guns get their own value.
        /// </summary>
        static List<RecoilPattern> WithStableShots(List<RecoilPattern> list)
        {
            var byCategory = new Dictionary<string, int>
            {
                { "Rifle", 2 }, { "Assault Rifle", 2 }, { "SMG", 2 }, { "Heavy", 2 }, { "LMG", 3 },
                { "Pistol", 1 }, { "Sidearm", 1 }, { "Shotgun", 1 }, { "Sniper", 1 }, { "Marksman", 1 }
            };
            var exact = new Dictionary<string, int>
            {
                // VALORANT
                { "VALORANT|Phantom", 3 }, { "VALORANT|Vandal", 2 }, { "VALORANT|Spectre", 3 }, { "VALORANT|Stinger", 2 },
                { "VALORANT|Bulldog", 2 }, { "VALORANT|Ares", 3 }, { "VALORANT|Odin", 3 }, { "VALORANT|Frenzy", 2 },
                // Counter-Strike 2
                { "Counter-Strike 2|AK-47", 2 }, { "Counter-Strike 2|M4A4", 3 }, { "Counter-Strike 2|M4A1-S", 3 },
                { "Counter-Strike 2|Galil AR", 2 }, { "Counter-Strike 2|FAMAS", 2 }, { "Counter-Strike 2|AUG", 3 },
                { "Counter-Strike 2|SG 553", 3 }, { "Counter-Strike 2|MP5-SD", 3 }, { "Counter-Strike 2|MP7", 3 },
                { "Counter-Strike 2|P90", 3 }, { "Counter-Strike 2|PP-Bizon", 3 }, { "Counter-Strike 2|Negev", 2 },
                { "Counter-Strike 2|M249", 2 }, { "Counter-Strike 2|Nova", 1 }, { "Counter-Strike 2|XM1014", 1 },
                { "Counter-Strike 2|MAG-7", 1 }, { "Counter-Strike 2|Sawed-Off", 1 }, { "Counter-Strike 2|Glock-18", 2 },
                { "Counter-Strike 2|Dual Berettas", 2 }, { "Counter-Strike 2|CZ75-Auto", 2 },
                // Rust (most guns kick from the first shot)
                { "Rust|Assault Rifle", 1 }, { "Rust|LR-300", 2 }, { "Rust|MP5A4", 2 }, { "Rust|Thompson", 2 },
                { "Rust|Custom SMG", 2 }, { "Rust|M249", 2 }, { "Rust|HMLMG", 2 },
                // Apex Legends
                { "Apex Legends|R-301", 2 }, { "Apex Legends|Flatline", 1 }, { "Apex Legends|Havoc", 3 },
                { "Apex Legends|R-99", 1 }, { "Apex Legends|Alternator", 2 }, { "Apex Legends|Volt", 2 },
                { "Apex Legends|Spitfire", 3 }, { "Apex Legends|Devotion", 4 }, { "Apex Legends|RE-45", 2 }
            };
            foreach (var p in list)
                p.StableShots = exact.TryGetValue(p.Key, out var n) ? n : byCategory.TryGetValue(p.Category, out var c) ? c : 1;
            return list;
        }

        static int[] P(int x, int y) => new[] { x, y };

        // sway: "lr" = climb then left/right swing, "s" = S-curve, "jitter" = small random wobble, "kick" = single kicks (semi-auto / snipers)
        static RecoilPattern Gen(string game, string cat, string name, int rpm, int shots, double climb, int climbShots, double sway, string swayType, int seed)
        {
            // trait values are relative; scale them into the same pixel range as the hand-tuned patterns
            climb *= 2.4;
            sway *= 1.9;
            var pts = new List<int[]>();
            var rnd = new Random(seed);
            double x = 0, y = 0;
            int n = Math.Min(shots, 40);
            for (int i = 0; i < n; i++)
            {
                pts.Add(P((int)Math.Round(x), (int)Math.Round(y)));
                double t = i < climbShots ? 1 : 0.12 + 0.08 * rnd.NextDouble();
                double ramp = Math.Min(1, 0.45 + i * 0.22);           // first shots climb a little less
                y -= climb * t * ramp;
                double after = Math.Max(0, i + 1 - climbShots);
                double span = Math.Max(1, n - climbShots);
                switch (swayType)
                {
                    case "lr": x = i + 1 <= climbShots ? x + (rnd.NextDouble() - 0.5) * 0.6 : -sway * Math.Sin(Math.PI * 1.5 * after / span); break;
                    case "s": x = sway * Math.Sin(2 * Math.PI * (i + 1) / Math.Max(6, n / 1.6)); break;
                    case "kick": x += (rnd.NextDouble() - 0.5) * sway; break;
                    default: x = Math.Max(-sway, Math.Min(sway, x + (rnd.NextDouble() - 0.5) * sway * 0.9)); break;
                }
            }
            return new RecoilPattern { Game = game, Category = cat, Name = name, Rpm = rpm, Points = pts.ToArray() };
        }

        static List<RecoilPattern> Build()
        {
            const string V = "VALORANT", C = "Counter-Strike 2", R = "Rust", A = "Apex Legends";
            var list = new List<RecoilPattern>
            {
                // ---------------- VALORANT ----------------
                Gen(V, "Sidearm", "Classic", 400, 12, 3.2, 3, 3, "kick", 11),
                Gen(V, "Sidearm", "Shorty", 200, 2, 6, 1, 2, "kick", 12),
                Gen(V, "Sidearm", "Frenzy", 600, 13, 4.2, 6, 8, "jitter", 13),
                Gen(V, "Sidearm", "Ghost", 400, 15, 3, 4, 3, "kick", 14),
                Gen(V, "Sidearm", "Sheriff", 240, 6, 9, 3, 4, "kick", 15),
                Gen(V, "SMG", "Stinger", 960, 20, 3.4, 8, 10, "jitter", 16),
                Gen(V, "SMG", "Spectre", 800, 30, 2.8, 9, 7, "jitter", 17),
                Gen(V, "Shotgun", "Bucky", 66, 5, 7, 1, 2, "kick", 18),
                Gen(V, "Shotgun", "Judge", 210, 7, 6, 2, 4, "kick", 19),
                Gen(V, "Rifle", "Bulldog", 590, 24, 4.2, 8, 6, "jitter", 20),
                Gen(V, "Rifle", "Guardian", 283, 12, 5, 4, 2, "kick", 21),
                new RecoilPattern { Name = "Phantom", Game = V, Category = "Rifle", Rpm = 600, HandTuned = true, Points = new[] {
                    P(0,0),P(0,-4),P(0,-10),P(0,-17),P(0,-25),P(0,-33),P(0,-41),P(0,-48),P(0,-54),P(0,-58),
                    P(-2,-61),P(-4,-63),P(-2,-64),P(1,-65),P(4,-66),P(2,-67),P(-1,-67),P(-4,-68),P(-2,-68),P(1,-69),
                    P(3,-69),P(1,-70),P(-1,-70),P(-3,-71),P(-1,-71),P(1,-72),P(2,-72),P(0,-72),P(-1,-73),P(0,-73) } },
                new RecoilPattern { Name = "Vandal", Game = V, Category = "Rifle", Rpm = 585, HandTuned = true, Points = new[] {
                    P(0,0),P(0,-5),P(0,-12),P(0,-20),P(0,-29),P(0,-38),P(0,-47),P(0,-55),P(0,-62),P(0,-67),
                    P(-3,-70),P(-6,-72),P(-3,-73),P(2,-74),P(6,-75),P(3,-76),P(-2,-77),P(-6,-78),P(-3,-78),P(2,-79),
                    P(5,-80),P(2,-80),P(-2,-81),P(-4,-81),P(-1,-82) } },
                Gen(V, "Sniper", "Marshal", 91, 5, 10, 1, 3, "kick", 22),
                Gen(V, "Sniper", "Outlaw", 165, 2, 12, 1, 3, "kick", 23),
                Gen(V, "Sniper", "Operator", 37, 5, 14, 1, 3, "kick", 24),
                Gen(V, "Heavy", "Ares", 780, 40, 2.6, 10, 12, "s", 25),
                Gen(V, "Heavy", "Odin", 720, 40, 2.4, 10, 14, "s", 26),

                // ---------------- Counter-Strike 2 ----------------
                Gen(C, "Pistol", "Glock-18", 400, 20, 3.6, 6, 6, "jitter", 31),
                Gen(C, "Pistol", "USP-S", 352, 12, 4.8, 5, 4, "kick", 32),
                Gen(C, "Pistol", "P2000", 352, 13, 4.6, 5, 4, "kick", 33),
                Gen(C, "Pistol", "Dual Berettas", 500, 30, 3.8, 8, 7, "jitter", 34),
                Gen(C, "Pistol", "P250", 400, 13, 5.2, 5, 5, "kick", 35),
                Gen(C, "Pistol", "Five-SeveN", 400, 20, 4.2, 6, 5, "kick", 36),
                Gen(C, "Pistol", "Tec-9", 500, 18, 5, 6, 8, "jitter", 37),
                Gen(C, "Pistol", "CZ75-Auto", 600, 12, 5.4, 8, 9, "jitter", 38),
                Gen(C, "Pistol", "Desert Eagle", 267, 7, 11, 4, 5, "kick", 39),
                Gen(C, "Pistol", "R8 Revolver", 120, 8, 9, 3, 4, "kick", 40),
                Gen(C, "SMG", "MAC-10", 800, 30, 2.8, 10, 12, "lr", 41),
                Gen(C, "SMG", "MP9", 857, 30, 2.8, 10, 10, "lr", 42),
                Gen(C, "SMG", "MP7", 800, 30, 2.6, 10, 9, "lr", 43),
                Gen(C, "SMG", "MP5-SD", 750, 30, 2.4, 10, 8, "lr", 44),
                Gen(C, "SMG", "UMP-45", 666, 25, 3.2, 9, 10, "lr", 45),
                Gen(C, "SMG", "P90", 857, 40, 2.2, 12, 10, "s", 46),
                Gen(C, "SMG", "PP-Bizon", 750, 40, 2.2, 12, 9, "s", 47),
                new RecoilPattern { Name = "AK-47", Game = C, Category = "Rifle", Rpm = 600, HandTuned = true, Points = new[] {
                    P(0,0),P(0,-4),P(0,-10),P(-1,-18),P(-1,-28),P(0,-40),P(1,-52),P(2,-63),P(3,-73),P(4,-80),
                    P(-6,-84),P(-14,-86),P(-22,-87),P(-28,-88),P(-30,-90),P(-24,-91),P(-14,-92),P(-2,-92),P(10,-92),P(20,-93),
                    P(28,-94),P(32,-95),P(30,-96),P(22,-96),P(12,-97),P(2,-98),P(-6,-98),P(-10,-99),P(-8,-100),P(-4,-100) } },
                new RecoilPattern { Name = "M4A4", Game = C, Category = "Rifle", Rpm = 666, HandTuned = true, Points = new[] {
                    P(0,0),P(0,-3),P(0,-8),P(0,-15),P(-1,-23),P(-1,-32),P(0,-41),P(1,-49),P(2,-56),P(4,-62),
                    P(1,-66),P(-4,-69),P(-9,-71),P(-12,-73),P(-10,-74),P(-5,-75),P(1,-76),P(7,-77),P(11,-78),P(12,-79),
                    P(9,-80),P(4,-80),P(-1,-81),P(-5,-81),P(-6,-82),P(-4,-82),P(-1,-83),P(2,-83),P(3,-84),P(2,-84) } },
                Gen(C, "Rifle", "M4A1-S", 600, 20, 3.4, 9, 9, "lr", 48),
                Gen(C, "Rifle", "Galil AR", 666, 35, 3.6, 10, 16, "lr", 49),
                Gen(C, "Rifle", "FAMAS", 666, 25, 3.4, 9, 12, "lr", 50),
                Gen(C, "Rifle", "AUG", 600, 30, 3.4, 10, 14, "lr", 51),
                Gen(C, "Rifle", "SG 553", 545, 30, 3.8, 10, 16, "lr", 52),
                Gen(C, "Sniper", "SSG 08", 48, 10, 10, 1, 2, "kick", 53),
                Gen(C, "Sniper", "AWP", 41, 5, 14, 1, 2, "kick", 54),
                Gen(C, "Sniper", "G3SG1", 240, 20, 4, 5, 4, "kick", 55),
                Gen(C, "Sniper", "SCAR-20", 240, 20, 4, 5, 4, "kick", 56),
                Gen(C, "Heavy", "Negev", 800, 40, 3.2, 8, 10, "s", 57),
                Gen(C, "Heavy", "M249", 750, 40, 3, 10, 12, "s", 58),
                Gen(C, "Heavy", "Nova", 68, 8, 9, 2, 3, "kick", 59),
                Gen(C, "Heavy", "XM1014", 171, 7, 7, 3, 4, "kick", 60),
                Gen(C, "Heavy", "MAG-7", 71, 5, 9, 2, 3, "kick", 61),
                Gen(C, "Heavy", "Sawed-Off", 71, 7, 9, 2, 3, "kick", 62),

                // ---------------- Rust ----------------
                new RecoilPattern { Name = "Assault Rifle", Game = R, Category = "Rifle", Rpm = 450, HandTuned = true, Points = new[] {
                    P(0,0),P(-4,-14),P(-2,-28),P(3,-41),P(8,-52),P(12,-61),P(14,-69),P(13,-76),P(10,-82),P(5,-87),
                    P(-1,-91),P(-7,-94),P(-12,-97),P(-16,-99),P(-18,-101),P(-18,-103),P(-15,-105),P(-10,-106),P(-4,-107),P(2,-108),
                    P(8,-109),P(13,-110),P(16,-111),P(17,-112),P(15,-113),P(11,-114),P(6,-114),P(0,-115),P(-5,-115),P(-9,-116) } },
                Gen(R, "Rifle", "LR-300", 500, 30, 3.1, 10, 10, "s", 71),
                Gen(R, "Rifle", "Semi-Automatic Rifle", 343, 16, 5, 6, 5, "kick", 72),
                Gen(R, "Rifle", "M39 Rifle", 300, 20, 5.4, 6, 5, "kick", 73),
                Gen(R, "Rifle", "Bolt Action Rifle", 35, 4, 12, 1, 3, "kick", 74),
                Gen(R, "Rifle", "L96 Rifle", 25, 5, 14, 1, 3, "kick", 75),
                Gen(R, "SMG", "MP5A4", 600, 30, 3.2, 10, 8, "s", 76),
                Gen(R, "SMG", "Thompson", 462, 20, 3.4, 8, 7, "s", 77),
                Gen(R, "SMG", "Custom SMG", 600, 24, 3.2, 9, 8, "s", 78),
                Gen(R, "Heavy", "M249", 500, 40, 3.6, 12, 12, "s", 79),
                Gen(R, "Heavy", "HMLMG", 480, 40, 3.8, 12, 13, "s", 80),
                Gen(R, "Pistol", "Python Revolver", 400, 6, 8, 3, 4, "kick", 81),
                Gen(R, "Pistol", "M92 Pistol", 400, 15, 5, 5, 4, "kick", 82),
                Gen(R, "Pistol", "Semi-Automatic Pistol", 400, 10, 4.6, 5, 4, "kick", 83),
                Gen(R, "Pistol", "Revolver", 343, 8, 6, 3, 4, "kick", 84),

                // ---------------- Apex Legends ----------------
                new RecoilPattern { Name = "R-301", Game = A, Category = "Assault Rifle", Rpm = 816, HandTuned = true, Points = new[] {
                    P(0,0),P(0,-4),P(1,-9),P(1,-14),P(2,-19),P(1,-24),P(-1,-28),P(-3,-31),P(-4,-34),P(-3,-37),
                    P(-1,-39),P(2,-41),P(4,-43),P(5,-45),P(4,-47),P(2,-48),P(0,-49),P(-2,-50) } },
                Gen(A, "Assault Rifle", "Flatline", 600, 20, 3.6, 8, 12, "lr", 91),
                Gen(A, "Assault Rifle", "Havoc", 672, 25, 3.8, 10, 10, "s", 92),
                Gen(A, "Assault Rifle", "Hemlok", 930, 18, 3, 6, 6, "jitter", 93),
                Gen(A, "Assault Rifle", "Nemesis", 720, 20, 3, 8, 8, "jitter", 94),
                Gen(A, "SMG", "R-99", 1080, 20, 2.6, 8, 10, "jitter", 95),
                Gen(A, "SMG", "Alternator", 600, 19, 2.6, 8, 6, "s", 96),
                Gen(A, "SMG", "Prowler", 840, 20, 2.8, 8, 9, "jitter", 97),
                Gen(A, "SMG", "Volt", 720, 19, 2.6, 8, 8, "lr", 98),
                Gen(A, "SMG", "C.A.R.", 930, 18, 2.8, 8, 10, "jitter", 99),
                Gen(A, "LMG", "Spitfire", 540, 35, 3.2, 12, 12, "s", 100),
                Gen(A, "LMG", "Devotion", 900, 36, 2.8, 12, 14, "s", 101),
                Gen(A, "LMG", "L-STAR", 600, 30, 3, 10, 10, "s", 102),
                Gen(A, "LMG", "Rampage", 300, 28, 4.2, 10, 8, "s", 103),
                Gen(A, "Marksman", "G7 Scout", 240, 15, 5, 5, 4, "kick", 104),
                Gen(A, "Marksman", "Triple Take", 77, 6, 9, 2, 3, "kick", 105),
                Gen(A, "Marksman", "30-30 Repeater", 100, 6, 9, 2, 3, "kick", 106),
                Gen(A, "Marksman", "Bocek Bow", 90, 5, 6, 1, 2, "kick", 107),
                Gen(A, "Sniper", "Longbow DMR", 78, 6, 11, 1, 3, "kick", 108),
                Gen(A, "Sniper", "Kraber", 30, 4, 16, 1, 3, "kick", 109),
                Gen(A, "Sniper", "Sentinel", 40, 4, 14, 1, 3, "kick", 110),
                Gen(A, "Pistol", "P2020", 420, 14, 4.2, 5, 4, "kick", 111),
                Gen(A, "Pistol", "RE-45", 780, 15, 3.6, 6, 8, "jitter", 112),
                Gen(A, "Pistol", "Wingman", 156, 6, 10, 3, 4, "kick", 113),
            };
            return list;
        }

        /// <summary>Weapons of a game, ordered by category then name as listed.</summary>
        public static List<RecoilPattern> ForGame(string game) => Patterns.Where(p => p.Game == game).ToList();

        /// <summary>Finds by "Game|Name" key, or by name alone (older saves stored just the weapon name).</summary>
        public static RecoilPattern Find(string keyOrName)
        {
            if (string.IsNullOrEmpty(keyOrName)) return null;
            return Patterns.FirstOrDefault(p => p.Key == keyOrName) ?? Patterns.FirstOrDefault(p => p.Name == keyOrName);
        }

        /// <summary>Writes the pattern into a layer's fire animation (hold to spray, release resets).</summary>
        public static void Apply(Dictionary<string, object> layer, RecoilPattern pattern, double scale = 1, double rpm = 0, int stableShots = 0, double factor = double.NaN)
        {
            // per-layer tracker style, kept whenever the weapon, scale or fire rate changes:
            //   recoilFx      property overrides that kick in on the first recoil shot (e.g. { dot: { diameter: 6 } })
            //   recoilFxEnd   values reached on the last bullet, blended across the spray (e.g. a green → red heat ramp)
            //   recoilInvert  move opposite to the spray — shows where to pull the mouse
            //   recoilMirror  flip the sideways sway (for symmetric pairs)
            //   recoilLag     follow the spray this many shots behind (trails)
            var old = J.Obj(layer, "firingOptions");
            var fx = J.Obj(old, "recoilFx");
            var fxEnd = J.Obj(old, "recoilFxEnd");
            bool invert = J.Bool(old, "recoilInvert"), mirror = J.Bool(old, "recoilMirror");
            int lag = Math.Max(0, (int)J.Num(old, "recoilLag"));
            // recoilFactor: temporary multiplier from a loadout slot (the crosshair's own scale stays in recoilScale)
            if (double.IsNaN(factor)) factor = J.Num(old, "recoilFactor", 1);
            if (factor <= 0) factor = 1;

            double useRpm = rpm > 0 ? rpm : pattern.Rpm;
            double shot = 60000.0 / useRpm;
            // keep the layer's resting position when re-applying
            var basePos = J.ObjOrEmpty(layer, "position");
            double bx = J.Num(basePos, "x"), by = J.Num(basePos, "y");
            var pts = pattern.Points;
            double sy = scale * factor * (invert ? -1 : 1), sx = sy * (mirror ? -1 : 1);
            Func<int, Dictionary<string, object>> pos = i => J.O("x", Math.Round(bx + pts[i][0] * sx), "y", Math.Round(by + pts[i][1] * sy));
            var fo = Defaults.FiringOptions();
            fo["mouseButton"] = "left";
            fo["pressType"] = "hold";
            fo["releaseBehavior"] = "reset";
            fo["loop"] = false;
            fo["easing"] = "linear";
            fo["duration"] = Math.Round(shot / 1000, 4);
            // first-shot accuracy: stay centered while the first N bullets fire, then follow the spray
            int stable = stableShots >= 1 ? stableShots : pattern.StableShots;
            fo["startDelay"] = Math.Round((Math.Max(0, stable - 1) + lag) * shot / 1000, 4);
            fo["position"] = pos(Math.Min(1, pts.Length - 1));
            var stages = new List<object>();
            for (int i = 2; i < pts.Length; i++)
                stages.Add(J.O("duration", Math.Round(shot / 1000, 4), "easing", "linear", "position", pos(i)));
            fo["stages"] = stages;
            if (fx != null || fxEnd != null)
            {
                // stages inherit from the previous one, so a plain effect only needs the first shot; a ramp sets every bullet
                int n = pts.Length;
                for (int k = 1; k < n; k++)
                {
                    if (fxEnd == null && k > 1) break;
                    var target = k == 1 ? fo : (Dictionary<string, object>)stages[k - 2];
                    // ramps peak by the 10th bullet (a typical spray), then hold
                    BlendFx(target, layer, fx, fxEnd, n > 2 ? Math.Min(1, (k - 1) / (double)Math.Min(n - 2, 9)) : 1);
                }
            }
            // remembered so the weapon can be switched later with the same settings
            fo["recoilPattern"] = pattern.Key;
            fo["recoilScale"] = scale;
            fo["recoilRpm"] = rpm > 0 && Math.Abs(rpm - pattern.Rpm) > 0.5 ? rpm : 0;
            fo["recoilStable"] = stableShots >= 1 && stableShots != pattern.StableShots ? stableShots : 0;
            if (fx != null) fo["recoilFx"] = J.CloneObj(fx);
            if (fxEnd != null) fo["recoilFxEnd"] = J.CloneObj(fxEnd);
            if (invert) fo["recoilInvert"] = true;
            if (mirror) fo["recoilMirror"] = true;
            if (lag > 0) fo["recoilLag"] = (double)lag;
            if (Math.Abs(factor - 1) > 1e-6) fo["recoilFactor"] = factor;
            layer["firingOptions"] = fo;
        }

        /// <summary>Writes effect values into an animation target: fx (or the layer's own value) blended toward fxEnd by t.</summary>
        static void BlendFx(Dictionary<string, object> target, Dictionary<string, object> layer, Dictionary<string, object> fx, Dictionary<string, object> fxEnd, double t)
        {
            var keys = new HashSet<string>();
            if (fx != null) keys.UnionWith(fx.Keys);
            if (fxEnd != null) keys.UnionWith(fxEnd.Keys);
            foreach (var k in keys)
            {
                object a = J.Get(fx, k), b = J.Get(fxEnd, k);
                if (a is Dictionary<string, object> || b is Dictionary<string, object>)
                {
                    BlendFx(J.EnsureObj(target, k), J.ObjOrEmpty(layer, k), a as Dictionary<string, object>, b as Dictionary<string, object>, t);
                    continue;
                }
                object start = a ?? J.Get(layer, k);
                target[k] = b == null || start == null ? (start ?? b) : Mix(start, b, t);
            }
        }

        static object Mix(object a, object b, double t)
        {
            if (a is string sa && b is string sb && ColorUtil.LooksLikeColor(sa) && ColorUtil.LooksLikeColor(sb))
                return ColorUtil.ToHex(ColorUtil.Lerp(ColorUtil.Parse(sa), ColorUtil.Parse(sb), t), true);
            if (!(a is string) && !(b is string) && !(a is bool) && !(b is bool))
            {
                double x = J.ToNum(a), y = J.ToNum(b);
                return Math.Round(x + (y - x) * t, 3);
            }
            return t >= 0.5 ? b : a;
        }

        public static bool HasRecoil(List<object> layers) => layers != null && layers.OfType<Dictionary<string, object>>().Any(l => J.Str(J.Obj(l, "firingOptions"), "recoilPattern") != null);

        /// <summary>True when recoil tracking was switched off with a "Recoil off" loadout slot.</summary>
        public static bool IsOff(List<object> layers) => layers != null && layers.OfType<Dictionary<string, object>>().Any(l => J.Bool(J.Obj(l, "firingOptions"), "recoilOff"));

        /// <summary>The weapon currently used by a crosshair's recoil layer(s).</summary>
        public static RecoilPattern CurrentWeapon(List<object> layers)
        {
            if (layers == null) return null;
            foreach (var l in layers.OfType<Dictionary<string, object>>())
            {
                var p = Find(J.Str(J.Obj(l, "firingOptions"), "recoilPattern"));
                if (p != null) return p;
            }
            return null;
        }

        /// <summary>Switches every recoil layer of a crosshair to another weapon, keeping each layer's scale. Null turns tracking off.</summary>
        public static bool SetWeapon(List<object> layers, RecoilPattern pattern, double factor = 1)
        {
            bool any = false;
            foreach (var l in layers.OfType<Dictionary<string, object>>())
            {
                var fo = J.Obj(l, "firingOptions");
                if (J.Str(fo, "recoilPattern") == null) continue;
                if (pattern == null)
                {
                    // keep the pattern so it can be re-enabled, but stop the animation from triggering
                    fo["mouseButton"] = "none";
                    fo["recoilOff"] = true;
                    any = true;
                    continue;
                }
                double scale = J.Num(fo, "recoilScale", 1);
                Apply(l, pattern, scale <= 0 ? 1 : scale, 0, 0, factor);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Re-applies recoil layers that use the weapon's default fire rate, so tuning a weapon's default updates crosshairs
        /// that were saved earlier. Layers with a custom fire rate are left alone. Returns true if anything changed.
        /// </summary>
        public static bool Refresh(List<object> layers)
        {
            bool changed = false;
            foreach (var l in layers.OfType<Dictionary<string, object>>())
            {
                var fo = J.Obj(l, "firingOptions");
                var p = Find(J.Str(fo, "recoilPattern"));
                if (p == null || J.Num(fo, "recoilRpm") > 0 || J.Bool(fo, "recoilOff")) continue;
                string before = Json.Serialize(fo);
                Apply(l, p, J.Num(fo, "recoilScale", 1) <= 0 ? 1 : J.Num(fo, "recoilScale", 1), 0, (int)J.Num(fo, "recoilStable"));
                if (Json.Serialize(J.Obj(l, "firingOptions")) != before) changed = true;
            }
            return changed;
        }

        /// <summary>Next / previous weapon within the same game.</summary>
        public static RecoilPattern Cycle(RecoilPattern current, int dir)
        {
            var list = ForGame(current?.Game ?? Games[0]);
            int i = current == null ? -1 : list.FindIndex(p => p.Key == current.Key);
            i = ((i + dir) % list.Count + list.Count) % list.Count;
            return list[i];
        }

        static Dictionary<string, object> Model(string name, string color, double len, double th, double gap, string[] vis, double dot, string dotShape, double x = 0)
        {
            var l = Defaults.ModelLayer();
            l["line"] = J.O("length", len, "thickness", th, "offset", gap, "opacity", len > 0 ? 1 : 0, "color", color,
                "visible", vis, "shape", "rectangle", "blur", 0, "rotation", 0);
            l["dot"] = J.O("diameter", dot, "opacity", dot > 0 ? 1 : 0, "color", color, "shape", dot > 0 ? dotShape : "square", "blur", 0);
            l["outline"] = J.O("thickness", 1, "opacity", 1, "color", "#000000", "blur", 0);
            l["position"] = J.O("x", x, "y", 0);
            l["layerName"] = name;
            return l;
        }

        /// <summary>Plus whose left, right and bottom arms stay put while the top arm climbs along the spray.</summary>
        public static List<object> MakeCrosshair(RecoilPattern pattern, string color = "#FF3B3B", double scale = 1)
        {
            var fixedArms = Model("Crosshair", color, 5, 2, 6, new[] { "right", "bottom", "left" }, 0, "square");
            var tracker = Model("Recoil tracker (top arm)", color, 5, 2, 6, new[] { "top" }, 0, "square");
            Apply(tracker, pattern, scale);
            var layers = new List<object> { fixedArms, tracker };
            Defaults.NormalizeLayers(layers);
            return layers;
        }

        /// <summary>Static plus with a center dot that follows the spray.</summary>
        public static List<object> MakeDotCrosshair(RecoilPattern pattern, string color = "#00FFFF", double scale = 1)
        {
            var fixedArms = Model("Crosshair", color, 5, 2, 8, new[] { "top", "right", "bottom", "left" }, 0, "square");
            var tracker = Model("Recoil dot", color, 0, 0, 0, new[] { "top" }, 4, "circle");
            Apply(tracker, pattern, scale);
            var layers = new List<object> { fixedArms, tracker };
            Defaults.NormalizeLayers(layers);
            return layers;
        }

        /// <summary>"——| • |——": long side lines ending in vertical bars, with the center dot as the tracker.</summary>
        public static List<object> MakeOnetapBars(RecoilPattern pattern, double scale = 1)
        {
            var layers = new List<object>
            {
                Model("Side lines", "#FFFFFF", 22, 2, 16, new[] { "left", "right" }, 0, "square"),
                Model("Left bar", "#FFFFFF", 5, 2, 0, new[] { "top", "bottom" }, 0, "square", -8),
                Model("Right bar", "#FFFFFF", 5, 2, 0, new[] { "top", "bottom" }, 0, "square", 8),
                Model("Recoil dot", "#FFFFFF", 0, 0, 0, new[] { "top" }, 4, "circle"),
            };
            Apply((Dictionary<string, object>)layers[3], pattern, scale);
            Defaults.NormalizeLayers(layers);
            return layers;
        }

        /// <summary>Chevron that rides the spray above a static dot.</summary>
        public static List<object> MakeChevronCrosshair(RecoilPattern pattern, string color = "#7FFF00", double scale = 1)
        {
            var dot = Model("Center dot", color, 0, 0, 0, new[] { "top" }, 2, "square");
            var tracker = Model("Recoil chevron", color, 5, 2, 0, new[] { "bottom" }, 0, "square");
            J.Obj(tracker, "line")["shape"] = "chevronOut";
            J.Obj(tracker, "line")["angle"] = 90.0;
            Apply(tracker, pattern, scale);
            var layers = new List<object> { dot, tracker };
            Defaults.NormalizeLayers(layers);
            return layers;
        }
    }
}
