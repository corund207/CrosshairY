using System;
using System.Collections.Generic;
using System.Linq;

namespace CrosshairY.Core
{
    public sealed class Preset
    {
        public string Name, Category;
        public List<object> Layers;
    }

    /// <summary>Built-in crosshair designs, authored in the Crosshair X layer format.</summary>
    public static class Presets
    {
        public static readonly string[] Categories = { "All", "Classic", "Dot", "Circle", "Chevron", "T-Style", "Fancy", "Animated", "Recoil" };

        static Dictionary<string, object> M(double len, double thick, double gap, string color, double ot = 1, double oo = 1,
            double dot = 0, string dotShape = "square", string[] visible = null, string shape = "rectangle", double opacity = 1,
            double rotation = 0, string dotColor = null, double dotOpacity = 1, string outlineColor = "#000000")
        {
            var l = Defaults.ModelLayer();
            l["line"] = J.O("length", len, "thickness", thick, "offset", gap, "opacity", len > 0 ? opacity : 0, "color", color,
                "visible", visible ?? new[] { "top", "right", "bottom", "left" }, "shape", shape, "blur", 0, "rotation", rotation);
            l["dot"] = J.O("diameter", dot, "opacity", dot > 0 ? dotOpacity : 0, "color", dotColor ?? color, "shape", dot > 0 ? dotShape : "square", "blur", 0);
            l["outline"] = J.O("thickness", ot, "opacity", oo, "color", outlineColor, "blur", 0);
            return l;
        }

        static Dictionary<string, object> With(this Dictionary<string, object> l, string group, params object[] kv)
        {
            var g = group == null ? l : J.EnsureObj(l, group);
            for (int i = 0; i + 1 < kv.Length; i += 2) g[(string)kv[i]] = kv[i + 1] is int n ? (double)n : kv[i + 1];
            return l;
        }

        static Dictionary<string, object> Anim(this Dictionary<string, object> l, string trigger, double duration, string press = "hold",
            string release = "reverse", bool loop = false, string direction = "normal", string easing = "easeOutQuad", Dictionary<string, object> line = null,
            Dictionary<string, object> dot = null, Dictionary<string, object> outline = null, List<object> stages = null, Dictionary<string, object> layer = null)
        {
            var fo = Defaults.FiringOptions();
            fo["mouseButton"] = trigger; fo["duration"] = duration; fo["pressType"] = press; fo["releaseBehavior"] = release;
            fo["loop"] = loop; fo["direction"] = direction; fo["easing"] = easing;
            if (line != null) fo["line"] = line;
            if (dot != null) fo["dot"] = dot;
            if (outline != null) fo["outline"] = outline;
            if (layer != null) fo["layer"] = layer;
            if (stages != null) fo["stages"] = stages;
            l["firingOptions"] = fo;
            return l;
        }

        static Preset P(string cat, string name, params Dictionary<string, object>[] layers) =>
            new Preset { Category = cat, Name = name, Layers = layers.Cast<object>().ToList() };

        static readonly string[] TSt = { "right", "bottom", "left" };

        public static List<Preset> All()
        {
            var list = new List<Preset>
            {
                // Classic
                P("Classic", "Classic Green", M(6, 2, 6, "#00FF66")),
                P("Classic", "Classic Cyan", M(5, 2, 4, "#00FFFF")),
                P("Classic", "Tight Cross", M(4, 2, 2, "#00FF00", dot: 0)),
                P("Classic", "Closed Cross", M(6, 2, 0, "#FFFFFF")),
                P("Classic", "Thin Long", M(10, 1, 6, "#00FF66")),
                P("Classic", "Hairline", M(8, 1, 4, "#FF00FF", ot: 0, oo: 0)),
                P("Classic", "Chunky", M(7, 4, 8, "#FFFF00", ot: 1)),
                P("Classic", "Classic Red", M(5, 2, 5, "#FF2D2D")),
                P("Classic", "Pink Pro", M(4, 2, 4, "#FF5A90", ot: 0, oo: 0)),
                P("Classic", "Yellow Pro", M(3, 2, 2, "#FFFF00", ot: 1, oo: .5)),
                P("Classic", "Cross + Dot", M(5, 2, 8, "#00FFFF", dot: 2)),
                P("Classic", "Diagonal X", M(6, 2, 6, "#FFFFFF", rotation: 45)),
                P("Classic", "Plus Dot Wide", M(4, 2, 12, "#7FFF00", dot: 2)),

                // Dot
                P("Dot", "Dot Small", M(0, 0, 0, "#00FF66", dot: 2)),
                P("Dot", "Dot Medium", M(0, 0, 0, "#00FFFF", dot: 4)),
                P("Dot", "Round Dot", M(0, 0, 0, "#FF00FF", dot: 4, dotShape: "circle")),
                P("Dot", "Red Dot Sight", M(0, 0, 0, "#FF2020", ot: 0, oo: 0, dot: 5, dotShape: "circle").With("dot", "blur", .6)),
                P("Dot", "Diamond Dot", M(0, 0, 0, "#FFFF00", dot: 6, dotShape: "diamond")),
                P("Dot", "Tiny White", M(0, 0, 0, "#FFFFFF", dot: 1)),

                // Circle
                P("Circle", "Ring", M(0, 0, 0, "#00FF66", dot: 14, dotShape: "ring").With("dot", "thickness", 1)),
                P("Circle", "Ring + Dot", M(0, 0, 0, "#00FFFF", dot: 16, dotShape: "ring").With("dot", "thickness", 1), M(0, 0, 0, "#00FFFF", dot: 2)),
                P("Circle", "Shotgun Circle", M(0, 0, 0, "#FFFFFF", dot: 40, dotShape: "ring", ot: 1, oo: .6).With("dot", "thickness", 2), M(0, 0, 0, "#FFFFFF", dot: 2, dotShape: "circle")),
                P("Circle", "Arc Brackets", M(8, 2, 16, "#00FF66", shape: "arc").With("line", "capStyle", "flat")),
                P("Circle", "Arc Trio", M(10, 2, 18, "#FF5A90", shape: "arc").With("line", "armCount", 3), M(0, 0, 0, "#FF5A90", dot: 2)),
                P("Circle", "Scope Ring", M(0, 0, 0, "#FF2020", dot: 30, dotShape: "ring", ot: 0, oo: 0).With("dot", "thickness", 1), M(12, 1, 6, "#FF2020", ot: 0, oo: 0)),

                // Chevron
                P("Chevron", "Chevron Up", M(6, 2, 0, "#00FF66", shape: "chevronOut", visible: new[] { "bottom" }).With("line", "angle", 90)),
                P("Chevron", "Arrows In", M(6, 2, 10, "#00FFFF", shape: "chevronIn").With("line", "angle", 90)),
                P("Chevron", "Arrows Out", M(5, 2, 6, "#FFFF00", shape: "chevronOut").With("line", "angle", 100)),
                P("Chevron", "Soft Chevron", M(7, 2, 0, "#FF5A90", shape: "chevronOut", visible: new[] { "bottom" }).With("line", "angle", 80, "roundness", .6), M(0, 0, 0, "#FF5A90", dot: 2)),
                P("Chevron", "Triangle Points", M(6, 4, 6, "#FFFFFF", shape: "triangle")),
                P("Chevron", "Three Wedges", M(6, 4, 8, "#00FF66", shape: "triangle").With("line", "armCount", 3)),

                // T-style
                P("T-Style", "T Classic", M(6, 2, 6, "#00FF66", visible: TSt)),
                P("T-Style", "T With Dot", M(5, 2, 6, "#00FFFF", dot: 2, visible: TSt)),
                P("T-Style", "Capped Cross", M(6, 2, 6, "#FFFFFF", shape: "tShape").With("line", "capLength", 6, "capThickness", 2)),
                P("T-Style", "Rangefinder", M(0, 0, 0, "#FF2020", dot: 2), M(4, 1, 8, "#FF2020", visible: new[] { "bottom" }), M(2, 1, 16, "#FF2020", visible: new[] { "bottom" })),

                // Fancy
                P("Fancy", "Double Cross", M(4, 2, 4, "#00FF66"), M(3, 2, 18, "#00FF66", opacity: .5, oo: .5)),
                P("Fancy", "Valorant Style", M(6, 2, 6, "#FFFFFF", oo: .5, opacity: .8), M(2, 2, 20, "#FFFFFF", oo: .5, opacity: .35)),
                P("Fancy", "Neon", M(6, 2, 6, "#00FFFF", ot: 0, oo: 0).With("line", "blur", 2), M(6, 2, 6, "#E0FFFF", ot: 0, oo: 0)),
                P("Fancy", "Star Burst", M(6, 2, 6, "#FFFF00"), M(4, 2, 6, "#FF7A00", rotation: 45)),
                P("Fancy", "Hex Cross", M(6, 2, 8, "#A855F7").With("line", "armCount", 6), M(0, 0, 0, "#A855F7", dot: 2, dotShape: "circle")),
                P("Fancy", "Box Frame", ShapeOutline(20, "#00FF66"), M(0, 0, 0, "#00FF66", dot: 2)),
                P("Fancy", "Corner Brackets", Bracket(-9, -9, false, false), Bracket(9, -9, true, false), Bracket(-9, 9, false, true), Bracket(9, 9, true, true), M(0, 0, 0, "#00FFFF", dot: 2)),

                // Animated
                P("Animated", "Bloom on Fire", M(5, 2, 6, "#00FF66").Anim("left", .12, line: J.O("offset", 16))),
                P("Animated", "Recoil Kick", M(5, 2, 6, "#00FFFF").Anim("left", .08, press: "press", direction: "alternate", line: J.O("offset", 12), layer: null)
                    .With("firingOptions", "position", J.O("y", -3))),
                P("Animated", "Hit Flash", M(5, 2, 6, "#FFFFFF").Anim("left", .1, press: "press", direction: "alternate", line: J.O("color", "#FF2020"), dot: J.O())),
                P("Animated", "Pulse Dot", M(0, 0, 0, "#FF5A90", dot: 4, dotShape: "circle").Anim("autoplay", .8, press: "press", loop: true, direction: "alternate", easing: "easeInOutSine", dot: J.O("diameter", 8, "opacity", .4))),
                P("Animated", "RGB Cycle", M(6, 2, 6, "#FF0000").Anim("autoplay", .6, press: "press", loop: true, easing: "linear", line: J.O("color", "#FFFF00"),
                    stages: J.A(J.O("duration", .6, "easing", "linear", "line", J.O("color", "#00FF00")), J.O("duration", .6, "easing", "linear", "line", J.O("color", "#00FFFF")),
                                J.O("duration", .6, "easing", "linear", "line", J.O("color", "#0000FF")), J.O("duration", .6, "easing", "linear", "line", J.O("color", "#FF00FF")),
                                J.O("duration", .6, "easing", "linear", "line", J.O("color", "#FF0000"))))),
                P("Animated", "Spinner", M(5, 2, 8, "#00FF66").Anim("autoplay", 2, press: "press", loop: true, easing: "linear", line: J.O("rotation", 360))),
                P("Animated", "Aim Tighten", M(5, 2, 14, "#FFFFFF").Anim("right", .15, line: J.O("offset", 4, "color", "#00FF66"), easing: "easeOutCubic")),
                P("Animated", "Aim Dot Only", M(5, 2, 6, "#00FFFF", dot: 2).Anim("right", .1, line: J.O("opacity", 0))),
                P("Animated", "Breathing Ring", M(0, 0, 0, "#00FFFF", dot: 12, dotShape: "ring").With("dot", "thickness", 1)
                    .Anim("autoplay", 1.2, press: "press", loop: true, direction: "alternate", easing: "easeInOutSine", dot: J.O("diameter", 18, "opacity", .3)), M(0, 0, 0, "#00FFFF", dot: 2)),
            };
            var defaultWeapon = Recoil.Find("VALORANT|Vandal");
            list.Add(new Preset { Category = "Recoil", Name = "Recoil Plus", Layers = Recoil.MakeCrosshair(defaultWeapon, "#FF3B3B") });
            list.Add(new Preset { Category = "Recoil", Name = "Recoil Dot", Layers = Recoil.MakeDotCrosshair(defaultWeapon, "#00FFFF") });
            list.Add(new Preset { Category = "Recoil", Name = "Onetap Bars", Layers = Recoil.MakeOnetapBars(defaultWeapon) });
            list.Add(new Preset { Category = "Recoil", Name = "Recoil Chevron", Layers = Recoil.MakeChevronCrosshair(defaultWeapon, "#7FFF00") });
            return list;
        }

        static Dictionary<string, object> ShapeOutline(double size, string color)
        {
            var s = Defaults.ShapeLayer("square");
            s["width"] = size; s["height"] = size; s["opacity"] = 0.0;
            s["outline"] = J.O("thickness", 1, "opacity", 1, "color", color, "blur", 0);
            return s;
        }

        static Dictionary<string, object> Bracket(int x, int y, bool flipX, bool flipY)
        {
            // an L-shaped bracket made from a two-arm crosshair layer positioned at a corner
            var vis = new List<string> { flipY ? "top" : "bottom", flipX ? "left" : "right" };
            var l = M(5, 2, 0, "#00FFFF", visible: vis.ToArray());
            l["position"] = J.O("x", x, "y", y);
            return l;
        }
    }
}
