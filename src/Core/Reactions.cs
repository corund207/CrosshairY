using System;
using System.Collections.Generic;

namespace CrosshairY.Core
{
    /// <summary>
    /// Hit marker / kill flash graphics. Built as ordinary layers for a progress value t (0 → 1) so the overlay can draw
    /// them on top of the crosshair and fade them out without touching the crosshair's own animations.
    /// </summary>
    public static class Reactions
    {
        public static readonly string[] Styles = { "x", "ring", "brackets" };
        public static readonly string[] StyleNames = { "Hit marker (X)", "Ring pulse", "Corner brackets" };

        public static Func<double, List<object>> Build(Settings s, bool kill)
        {
            string style = Array.IndexOf(Styles, s.HitStyle) >= 0 ? s.HitStyle : "x";
            string color = kill ? s.KillColor : s.HitColor;
            double size = Math.Max(0.25, s.ReactionSize) * (kill ? 1.35 : 1);
            return t =>
            {
                double fade = 1 - t * t;                  // stays solid, then drops off
                double pop = 1 - Math.Pow(1 - t, 3);      // quick outward pop
                var layers = new List<object>();
                switch (style)
                {
                    case "ring":
                        layers.Add(Ring(color, (10 + 16 * pop) * size, kill ? 2 : 1.5, fade));
                        break;
                    case "brackets":
                        double d = (9 + 4 * pop) * size, len = 4 * size;
                        foreach (var (x, y) in new[] { (-d, -d), (d, -d), (-d, d), (d, d) })
                            layers.Add(Line(color, len, 2, 0, new[] { y > 0 ? "top" : "bottom", x > 0 ? "left" : "right" }, 0, fade, x, y));
                        break;
                    default:
                        layers.Add(Line(color, (kill ? 7 : 5) * size, 2, (6 + 3 * pop) * size, new[] { "top", "right", "bottom", "left" }, 45, fade, 0, 0));
                        break;
                }
                // a kill also sends out a ring
                if (kill && style != "ring") layers.Add(Ring(color, (14 + 22 * pop) * size, 1.5, fade * 0.8));
                return layers;
            };
        }

        static Dictionary<string, object> Line(string color, double len, double th, double gap, string[] vis, double rotation, double opacity, double x, double y)
        {
            var l = Defaults.ModelLayer();
            l["line"] = J.O("length", Math.Round(len), "thickness", th, "offset", Math.Round(gap), "opacity", opacity, "color", color,
                "visible", vis, "shape", "rectangle", "blur", 0, "rotation", rotation);
            l["dot"] = J.O("diameter", 0, "opacity", 0, "color", color, "shape", "square", "blur", 0);
            l["outline"] = J.O("thickness", 1, "opacity", opacity, "color", "#000000", "blur", 0);
            l["position"] = J.O("x", Math.Round(x), "y", Math.Round(y));
            l["firingOptions"] = J.O();
            return l;
        }

        static Dictionary<string, object> Ring(string color, double diameter, double th, double opacity)
        {
            var l = Defaults.ModelLayer();
            l["line"] = J.O("length", 0, "thickness", 0, "offset", 0, "opacity", 0, "color", color, "visible", new[] { "top" }, "shape", "rectangle", "blur", 0, "rotation", 0);
            l["dot"] = J.O("diameter", Math.Round(diameter), "opacity", opacity, "color", color, "shape", "ring", "thickness", th, "blur", 0);
            l["outline"] = J.O("thickness", 1, "opacity", opacity * 0.8, "color", "#000000", "blur", 0);
            l["firingOptions"] = J.O();
            return l;
        }
    }
}
