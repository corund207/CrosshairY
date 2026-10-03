using System.Collections.Generic;

namespace CrosshairY.Core
{
    /// <summary>
    /// The recoil-tracking collection. Every design has one or more tracker layers that follow the spray while Fire is
    /// held; the weapon can be switched afterwards (picker, Designer, loadout keybinds) and each tracker keeps its own
    /// scale, trail delay, direction and effects.
    /// </summary>
    public static partial class Presets
    {
        static RecoilPattern trackWeapon;

        static readonly string[] Top = { "top" }, Bottom = { "bottom" }, Sides = { "left", "right" }, Vert = { "top", "bottom" };

        static IEnumerable<Preset> RecoilCollection(RecoilPattern weapon)
        {
            trackWeapon = weapon;
            return new List<Preset>
            {
                // ---------------- plus: the top arm climbs ----------------
                RP("Recoil Plus Dot", M(5, 2, 6, "#00FF66", visible: TSt), Dot("#00FF66", 2, "square"), TopArm(5, 2, 6, "#00FF66").Track()),
                RP("Hairline Recoil", M(9, 1, 5, "#FFFFFF", oo: .6, visible: TSt), TopArm(9, 1, 5, "#FFFFFF", oo: .6).Track()),
                RP("Tiny Recoil Plus", M(3, 1, 3, "#00FFFF", visible: TSt), TopArm(3, 1, 3, "#00FFFF").Track()),
                RP("Big Recoil Plus", M(10, 2, 10, "#FF5A90", visible: TSt), TopArm(10, 2, 10, "#FF5A90").Track()),
                RP("Chunky Recoil Plus", M(7, 4, 8, "#FFFF00", visible: TSt), TopArm(7, 4, 8, "#FFFF00").Track()),
                RP("Bloom Recoil Plus", M(5, 2, 6, "#00FFFF", visible: TSt).Anim("left", .12, line: J.O("offset", 12)).Named("Blooming arms"),
                    TopArm(5, 2, 6, "#00FFFF").Track()),
                RP("Heat Plus", M(5, 2, 6, "#00FF66", visible: TSt), TopArm(5, 2, 6, "#00FF66").Track(fxEnd: J.O("line", J.O("color", "#FF2020")))),
                RP("Flash Plus", M(5, 2, 6, "#FFFFFF", visible: TSt).Anim("left", .06, line: J.O("color", "#FF3030")).Named("Flashing arms"),
                    TopArm(5, 2, 6, "#FFFFFF").Track(fx: J.O("line", J.O("color", "#FF3030")))),
                RP("Stretch Plus", M(5, 2, 6, "#A855F7", visible: TSt), TopArm(5, 2, 6, "#A855F7").Track(fxEnd: J.O("line", J.O("length", 11)))),

                // ---------------- T, bars and rails ----------------
                RP("Tactical T", M(6, 2, 5, "#00FF66", dot: 2, visible: TSt), M(4, 2, 0, "#00FF66", visible: Sides).At(0, -9).Named("Recoil bar").Track()),
                RP("Horizon", M(6, 2, 10, "#FFFFFF", visible: Sides), Dot("#FFFFFF", 2, "square"), M(5, 2, 0, "#00FFFF", visible: Sides).Named("Recoil bar").Track()),
                RP("Goalposts", M(4, 2, 0, "#FFFFFF", visible: Vert).At(-10, 0), M(4, 2, 0, "#FFFFFF", visible: Vert).At(10, 0), Dot("#FF3B3B", 4).Track()),
                RP("Onetap Mini", M(10, 2, 10, "#FFFFFF", visible: Sides), M(3, 2, 0, "#FFFFFF", visible: Vert).At(-6, 0), M(3, 2, 0, "#FFFFFF", visible: Vert).At(6, 0),
                    Dot("#00FFFF", 3).Track()),
                RP("Rangefinder Recoil", Dot("#FF3B3B", 2, "square"), M(4, 1, 8, "#FF3B3B", visible: Bottom), M(2, 1, 16, "#FF3B3B", visible: Bottom),
                    M(3, 1, 0, "#FF3B3B", visible: Sides).Named("Recoil bar").Track()),

                // ---------------- dots ----------------
                RP("Micro Dot Recoil", Dot("#FFFFFF", 1, "square", opacity: .5).Named("Origin"), Dot("#FFFFFF", 2, "square").Track()),
                RP("Comet Trail", M(4, 1, 8, "#FF5A90", oo: .6),
                    Dot("#FF5A90", 2, opacity: .25).Named("Trail 3").Track(lag: 3), Dot("#FF5A90", 2, opacity: .4).Named("Trail 2").Track(lag: 2),
                    Dot("#FF5A90", 3, opacity: .65).Named("Trail 1").Track(lag: 1), Dot("#FF5A90", 4).Track()),
                RP("Heat Dot", M(5, 1, 8, "#FFFFFF", oo: .5, opacity: .6), Dot("#00FF66", 5).Track(fxEnd: J.O("dot", J.O("color", "#FF2020")))),
                RP("Swell Dot", Dot("#FF3B3B", 3, opacity: .35, ot: 0, oo: 0).With("dot", "blur", .6).Named("Origin glow"),
                    Dot("#FF3B3B", 3).Track(fxEnd: J.O("dot", J.O("diameter", 8)))),
                RP("Diamond Tracker", M(5, 1, 6, "#FFFF00", oo: .6), Dot("#FFFF00", 6, "diamond").Track()),
                RP("Ring Tracker", Dot("#00FFFF", 2, "square"), Ring("#00FFFF", 10).Track()),
                RP("Scope Recoil", Ring("#FF2020", 30, ot: 0, oo: 0), M(12, 1, 6, "#FF2020", ot: 0, oo: 0), Dot("#FF2020", 3).Track()),
                RP("Neon Tracker", M(5, 2, 7, "#00FFFF", ot: 0, oo: 0, opacity: .35),
                    Dot("#00FFFF", 9, opacity: .55, ot: 0, oo: 0).With("dot", "blur", 2).Named("Glow").Track(), Dot("#E0FFFF", 3, ot: 0, oo: 0).Track()),
                RP("Ghost Origin", Dot("#FFFFFF", 4, opacity: .3, ot: 0, oo: 0).Named("Origin"), Dot("#A855F7", 4).Track(fx: J.O("dot", J.O("color", "#D8B4FE")))),

                // ---------------- frames and brackets ----------------
                RP("Box Tracker", Dot("#00FF66", 2, "square"), Frame("#00FF66", 12, 12).Track()),
                RP("Rounded Box Tracker", Dot("#00FFFF", 2, "square"), Frame("#00FFFF", 14, 14, radius: 4).Track()),
                RP("Circle Frame", Dot("#FFFFFF", 2, "square"), Frame("#FFFFFF", 16, 16, shape: "circle").Track()),
                RP("Corner Lock", Corners("#00FFFF", 9, 4, false), Dot("#00FFFF", 3).Track()),
                RP("Drifting Brackets", Dot("#FFFFFF", 2, "square"), Corners("#FFFF00", 8, 3, true)),

                // ---------------- chevrons and arrows ----------------
                RP("Double Chevron", Dot("#7FFF00", 2, "square"), Chevron("#7FFF00", .45).Named("Echo chevron").Track(lag: 1), Chevron("#7FFF00").Track()),
                RP("Chevron Heat", Dot("#FFFFFF", 2, "square"), Chevron("#FFFF00").Track(fxEnd: J.O("line", J.O("color", "#FF2020")))),
                RP("Arrow Tracker", Ring("#FFFFFF", 16, opacity: .6), Arrow("#FF3B3B", 8, 7).Track()),

                // ---------------- pull guides and mirrored pairs ----------------
                RP("Pull Guide", M(5, 2, 6, "#00FF66", oo: .6), Dot("#00FFFF", 4).Named("Pull guide").Track(invert: true)),
                RP("Spray + Pull", Dot("#FFFFFF", 2, "square"), Dot("#FF3B3B", 4).Named("Spray").Track(), Dot("#00FFFF", 4).Named("Pull guide").Track(invert: true)),
                RP("Wings", Dot("#FFFFFF", 2, "square"), M(4, 2, 0, "#FF7A00", visible: Vert).At(-7, 0).Named("Left wing").Track(mirror: true),
                    M(4, 2, 0, "#FF7A00", visible: Vert).At(7, 0).Named("Right wing").Track()),
                RP("Twin Dots", M(4, 1, 8, "#FFFFFF", oo: .5, opacity: .5), Dot("#FF5A90", 3).At(-4, 0).Named("Left dot").Track(mirror: true),
                    Dot("#FF5A90", 3).At(4, 0).Named("Right dot").Track()),

                // ---------------- animated frames ----------------
                RP("Breathing Recoil", Ring("#00FFFF", 12).Anim("autoplay", 1.2, press: "press", loop: true, direction: "alternate", easing: "easeInOutSine",
                    dot: J.O("diameter", 18, "opacity", .3)).Named("Breathing ring"), Dot("#00FFFF", 3).Track()),
                RP("Spinner Recoil", M(10, 2, 18, "#A855F7", shape: "arc").With("line", "armCount", 3)
                    .Anim("autoplay", 2, press: "press", loop: true, easing: "linear", line: J.O("rotation", 360)).Named("Spinning arcs"), Dot("#A855F7", 3).Track()),
                RP("RGB Recoil", M(6, 2, 6, "#FF0000").Anim("autoplay", .6, press: "press", loop: true, easing: "linear", line: J.O("color", "#FFFF00"), stages: RgbStages())
                    .Named("RGB arms"), Dot("#FFFFFF", 3).Track()),
                RP("Aim Focus Recoil", M(5, 2, 14, "#FFFFFF").Anim("right", .15, line: J.O("offset", 4, "color", "#00FF66"), easing: "easeOutCubic").Named("Aim arms"),
                    Dot("#00FF66", 3).Track()),
                RP("Fade Frame", M(6, 2, 6, "#FFFFFF").Anim("left", .1, line: J.O("opacity", .2), outline: J.O("opacity", .2)).Named("Fading arms"), Dot("#FF3B3B", 4).Track()),
                RP("Kick Ring", Ring("#FFFF00", 14).Anim("left", .08, dot: J.O("diameter", 20)).Named("Kick ring"), Dot("#FFFF00", 3).Track()),
                RP("Text Brackets", Label("[      ]", 18, "#FFFFFF"), Dot("#00FF66", 3).Track()),
            };
        }

        // ---------------- builders ----------------

        static Preset RP(string name, params object[] parts)
        {
            var layers = new List<object>();
            foreach (var p in parts)
            {
                if (p is Dictionary<string, object> d) layers.Add(d);
                else if (p is IEnumerable<Dictionary<string, object>> many) layers.AddRange(many);
            }
            Defaults.NormalizeLayers(layers);
            return new Preset { Category = "Recoil", Name = name, Layers = layers };
        }

        /// <summary>Makes this layer a recoil tracker for the collection's default weapon.</summary>
        static Dictionary<string, object> Track(this Dictionary<string, object> l, double scale = 1, Dictionary<string, object> fx = null,
            Dictionary<string, object> fxEnd = null, bool invert = false, bool mirror = false, int lag = 0)
        {
            var fo = Defaults.FiringOptions();
            if (fx != null) fo["recoilFx"] = fx;
            if (fxEnd != null) fo["recoilFxEnd"] = fxEnd;
            if (invert) fo["recoilInvert"] = true;
            if (mirror) fo["recoilMirror"] = true;
            if (lag > 0) fo["recoilLag"] = (double)lag;
            l["firingOptions"] = fo;
            if (J.Str(l, "layerName") == null) l["layerName"] = "Recoil tracker";
            Recoil.Apply(l, trackWeapon, scale);
            return l;
        }

        static Dictionary<string, object> Named(this Dictionary<string, object> l, string name) { l["layerName"] = name; return l; }

        static Dictionary<string, object> At(this Dictionary<string, object> l, double x, double y) { l["position"] = J.O("x", x, "y", y); return l; }

        static Dictionary<string, object> TopArm(double len, double th, double gap, string color, double oo = 1) =>
            M(len, th, gap, color, oo: oo, visible: Top).Named("Recoil tracker (top arm)");

        static Dictionary<string, object> Dot(string color, double size, string shape = "circle", double opacity = 1, double ot = 1, double oo = 1) =>
            M(0, 0, 0, color, ot, oo, dot: size, dotShape: shape, dotOpacity: opacity);

        static Dictionary<string, object> Ring(string color, double size, double thickness = 1, double opacity = 1, double ot = 1, double oo = 1) =>
            Dot(color, size, "ring", opacity, ot, oo).With("dot", "thickness", thickness);

        static Dictionary<string, object> Chevron(string color, double opacity = 1) =>
            M(5, 2, 0, color, visible: Bottom, shape: "chevronOut", opacity: opacity, oo: opacity).With("line", "angle", 90);

        /// <summary>Hollow square / circle drawn by a shape layer's outline.</summary>
        static Dictionary<string, object> Frame(string color, double w, double h, double radius = 0, string shape = "square")
        {
            var s = Defaults.ShapeLayer(shape);
            s["width"] = w; s["height"] = h; s["color"] = color; s["opacity"] = 0.0; s["cornerRadius"] = radius;
            s["outline"] = J.O("thickness", 1.0, "opacity", 1.0, "color", color, "blur", 0.0);
            return s;
        }

        /// <summary>Solid upward triangle with a black outline.</summary>
        static Dictionary<string, object> Arrow(string color, double w, double h)
        {
            var s = Defaults.ShapeLayer("triangle");
            s["width"] = w; s["height"] = h; s["color"] = color; s["opacity"] = 1.0;
            s["outline"] = J.O("thickness", 1.0, "opacity", 1.0, "color", "#000000", "blur", 0.0);
            return s;
        }

        static Dictionary<string, object> Label(string text, double size, string color)
        {
            var t = Defaults.TextLayer(text);
            t["fontSize"] = size; t["color"] = color;
            return t;
        }

        /// <summary>Four L-shaped corner brackets; with track they all follow the spray as one frame.</summary>
        static IEnumerable<Dictionary<string, object>> Corners(string color, double d, double len, bool track)
        {
            foreach (var (x, y) in new[] { (-d, -d), (d, -d), (-d, d), (d, d) })
            {
                var vis = new[] { y > 0 ? "top" : "bottom", x > 0 ? "left" : "right" };
                var l = M(len, 2, 0, color, visible: vis).At(x, y).Named("Bracket");
                yield return track ? l.Track() : l;
            }
        }

        static List<object> RgbStages() => J.A(
            J.O("duration", .6, "easing", "linear", "line", J.O("color", "#00FF00")), J.O("duration", .6, "easing", "linear", "line", J.O("color", "#00FFFF")),
            J.O("duration", .6, "easing", "linear", "line", J.O("color", "#0000FF")), J.O("duration", .6, "easing", "linear", "line", J.O("color", "#FF00FF")),
            J.O("duration", .6, "easing", "linear", "line", J.O("color", "#FF0000")));
    }
}
