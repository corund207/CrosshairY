using System;
using System.Collections.Generic;

namespace CrosshairY.Core
{
    /// <summary>Generates random but sensible crosshairs for the Randomizer and "Surprise me".</summary>
    public static class RandomCrosshair
    {
        static readonly Random rnd = new Random();
        static readonly string[] Palette = { "#00FF66", "#00FFFF", "#FF00FF", "#FFFF00", "#FF2D2D", "#FFFFFF", "#FF5A90", "#7FFF00", "#A855F7", "#FF7A00", "#3B82F6", "#EF9408" };
        static readonly string[] Adjectives = { "Neon", "Cobalt", "Solar", "Hyper", "Crimson", "Frost", "Toxic", "Shadow", "Ember", "Pixel", "Vortex", "Ion", "Lunar", "Static", "Prism" };
        static readonly string[] Nouns = { "Strike", "Sight", "Pulse", "Edge", "Halo", "Spark", "Fang", "Arrow", "Point", "Grid", "Bolt", "Wisp", "Core", "Mark", "Blade" };

        public static string Name() => Adjectives[rnd.Next(Adjectives.Length)] + " " + Nouns[rnd.Next(Nouns.Length)];

        /// <summary>Rarity tier for the randomizer: 0 common … 3 legendary.</summary>
        public static int LastRarity;

        public static List<object> Make()
        {
            string color = Palette[rnd.Next(Palette.Length)];
            var layers = new List<object>();
            var l = Defaults.ModelLayer();
            string[] shapes = { "rectangle", "rectangle", "rectangle", "triangle", "chevronIn", "chevronOut", "arc", "tShape" };
            string shape = shapes[rnd.Next(shapes.Length)];
            double thick = new[] { 1, 2, 2, 2, 3, 4 }[rnd.Next(6)];
            double len = rnd.Next(2, 12), gap = rnd.Next(0, 16);
            var line = J.O("length", len, "thickness", thick, "offset", gap, "opacity", 1, "color", color, "visible", new[] { "top", "right", "bottom", "left" },
                "shape", shape, "blur", 0, "rotation", rnd.Next(4) == 0 ? 45 : 0);
            int rarity = 0;
            if (rnd.Next(5) == 0) { line["armCount"] = (double)new[] { 3, 5, 6, 8 }[rnd.Next(4)]; rarity++; }
            else if (rnd.Next(5) == 0) line["visible"] = J.A("right", "bottom", "left");
            if (shape == "tShape") { line["capLength"] = (double)rnd.Next(4, 10); line["capThickness"] = thick; }
            if (shape == "chevronIn" || shape == "chevronOut") { line["angle"] = (double)rnd.Next(50, 120); line["roundness"] = rnd.Next(3) == 0 ? .5 : 0; }
            if (shape == "arc") { line["offset"] = (double)rnd.Next(10, 26); line["length"] = (double)rnd.Next(6, 16); rarity++; }
            l["line"] = line;
            bool dot = rnd.Next(2) == 0;
            l["dot"] = J.O("diameter", dot ? rnd.Next(1, 5) : 0, "opacity", dot ? 1 : 0, "color", rnd.Next(4) == 0 ? Palette[rnd.Next(Palette.Length)] : color,
                "shape", new[] { "square", "circle", "diamond" }[rnd.Next(3)], "blur", 0);
            l["outline"] = J.O("thickness", 1, "opacity", rnd.Next(4) == 0 ? 0 : new[] { .5, 1 }[rnd.Next(2)], "color", "#000000", "blur", 0);
            layers.Add(l);
            if (rnd.Next(4) == 0)
            {
                var ring = Defaults.ModelLayer();
                J.Obj(ring, "line")["opacity"] = 0.0;
                ring["dot"] = J.O("diameter", rnd.Next(12, 40), "opacity", .8, "color", color, "shape", "ring", "blur", 0, "thickness", 1);
                layers.Add(ring);
                rarity++;
            }
            if (rnd.Next(6) == 0)
            {
                // animated: bloom or pulse
                var fo = Defaults.FiringOptions();
                if (rnd.Next(2) == 0) { fo["mouseButton"] = "left"; fo["pressType"] = "hold"; fo["releaseBehavior"] = "reverse"; fo["duration"] = 0.12; fo["line"] = J.O("offset", gap + 10); }
                else { fo["mouseButton"] = "autoplay"; fo["loop"] = true; fo["direction"] = "alternate"; fo["duration"] = 0.8; fo["easing"] = "easeInOutSine"; fo["line"] = J.O("opacity", 0.35); }
                l["firingOptions"] = fo;
                rarity++;
            }
            LastRarity = Math.Min(3, rarity);
            Defaults.NormalizeLayers(layers);
            return layers;
        }
    }
}
