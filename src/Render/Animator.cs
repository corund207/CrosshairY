using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using CrosshairY.Core;

namespace CrosshairY.Render
{
    /// <summary>
    /// Plays Crosshair X firing/aim/autoplay animations. Each layer's firingOptions (plus optional stages and
    /// triggerTimelines) becomes a chain of keyframes; the animator interpolates the layer JSON between them.
    /// </summary>
    public sealed class Animator
    {
        static readonly HashSet<string> MetaKeys = new HashSet<string>
        {
            "duration", "startDelay", "easing", "mouseButton", "pressType", "releaseBehavior", "direction", "loop",
            "version", "stages", "triggerTimelines", "tShapeWhenFiring", "firingOffset", "bloomDirection", "finalOpacity",
            "trigger", "name", "id", "label"
        };

        readonly Stopwatch clock = Stopwatch.StartNew();
        List<object> baseLayers = new List<object>();
        readonly List<Timeline> timelines = new List<Timeline>();
        bool fireDown, aimDown;

        public long Now => clock.ElapsedMilliseconds;
        public bool HasTimelines => timelines.Count > 0;
        public bool HasTrigger(string trig) => timelines.Any(t => t.Trigger == trig);

        public void Load(List<object> layers)
        {
            baseLayers = layers ?? new List<object>();
            timelines.Clear();
            for (int i = 0; i < baseLayers.Count; i++)
            {
                if (!(baseLayers[i] is Dictionary<string, object> layer)) continue;
                var fo = J.Obj(layer, "firingOptions");
                if (fo == null) continue;
                var main = Timeline.Build(i, layer, fo, null);
                if (main != null) timelines.Add(main);
                if (J.Obj(fo, "triggerTimelines") is Dictionary<string, object> tts)
                    foreach (var kv in tts)
                        if (kv.Value is Dictionary<string, object> tfo)
                        {
                            var t = Timeline.Build(i, layer, tfo, kv.Key);
                            if (t != null) timelines.Add(t);
                        }
            }
            long now = Now;
            foreach (var t in timelines)
            {
                if (t.Trigger == "autoplay") t.StartForward(now, fromStart: true);
                // keep the state consistent if a key is already held when switching crosshairs
                if ((t.Trigger == "left" && fireDown) || (t.Trigger == "right" && aimDown)) t.Press(now);
            }
        }

        /// <summary>Feeds trigger input. trigger is "left" (fire) or "right" (aim).</summary>
        public void Input(string trigger, bool down)
        {
            if (trigger == "left") { if (fireDown == down) return; fireDown = down; }
            if (trigger == "right") { if (aimDown == down) return; aimDown = down; }
            long now = Now;
            foreach (var t in timelines)
            {
                if (t.Trigger != trigger) continue;
                if (down) t.Press(now); else t.Release(now);
            }
        }

        /// <summary>The "Reload" keybind: resets paused or finished animations to their start.</summary>
        public void Reset()
        {
            long now = Now;
            foreach (var t in timelines)
            {
                t.Stop(0);
                if (t.Trigger == "autoplay") t.StartForward(now, true);
            }
        }

        /// <summary>Returns the layer list for this instant. <paramref name="animating"/> is true while more frames are needed.</summary>
        public List<object> Evaluate(out bool animating)
        {
            animating = false;
            if (timelines.Count == 0) return baseLayers;
            long now = Now;
            List<object> result = null;
            foreach (var t in timelines)
            {
                t.Advance(now);
                if (t.IsRunning) animating = true;
                if (t.Position <= 0 && !t.IsRunning) continue;
                if (result == null) result = new List<object>(baseLayers);
                var current = result[t.LayerIndex] as Dictionary<string, object>;
                var evaluated = t.Evaluate();
                // a secondary timeline only replaces the layer if it is actually playing
                result[t.LayerIndex] = evaluated ?? current;
            }
            return result ?? baseLayers;
        }

        // ------------------------------------------------------------------

        sealed class Timeline
        {
            public int LayerIndex;
            public string Trigger;       // "left", "right", "autoplay", "none"
            public string PressType;     // "press", "hold"
            public string ReleaseMode;       // "reset", "reverse", "pause"
            public bool Loop, Alternate;
            readonly List<Dictionary<string, object>> keys = new List<Dictionary<string, object>>();
            readonly List<double> durs = new List<double>();
            readonly List<string> eases = new List<string>();
            double total;

            public double Position;      // ms along the timeline
            int velocity;                // +1, -1, 0
            long lastTick;
            bool held;
            bool pingPongReturning;      // non-looping alternate: playing back after reaching the end
            int cycleCount;

            public bool IsRunning => velocity != 0;

            public static Timeline Build(int index, Dictionary<string, object> layer, Dictionary<string, object> fo, string triggerKey)
            {
                var stages = (J.List(fo, "stages") ?? new List<object>()).OfType<Dictionary<string, object>>().ToList();
                var baseLayer = J.CloneObj(layer);
                baseLayer.Remove("firingOptions");
                double version = J.Num(fo, "version", 1);
                var target = J.Merge(baseLayer, fo, MetaKeys);
                bool isModel = Defaults.LayerType(layer) == "model";
                if (isModel && version < 2) ApplyLegacy(baseLayer, target, fo);

                bool changes = !TreeEquals(baseLayer, target) || stages.Count > 0;
                if (!changes) return null;

                string trig = (J.Str(fo, "mouseButton") ?? J.Str(fo, "trigger") ?? triggerKey ?? "left").ToLowerInvariant();
                if (trig == "fire" || trig == "lmb" || trig == "mouse1") trig = "left";
                if (trig == "aim" || trig == "ads" || trig == "rmb" || trig == "mouse2") trig = "right";
                if (trig == "none" || trig == "off") return null;
                if (trig != "left" && trig != "right" && trig != "autoplay") trig = triggerKey == null ? "left" : trig;

                var t = new Timeline
                {
                    LayerIndex = index,
                    Trigger = trig,
                    PressType = J.Str(fo, "pressType") ?? (version < 2 ? "hold" : "press"),
                    ReleaseMode = J.Str(fo, "releaseBehavior") ?? (version < 2 ? "reverse" : "reset"),
                    Loop = J.Bool(fo, "loop"),
                    Alternate = J.Str(fo, "direction") == "alternate"
                };
                double baseDur = Math.Max(0, J.Num(fo, "duration")) * 1000;
                string baseEase = J.Str(fo, "easing", "linear");
                t.keys.Add(baseLayer);
                double delay = Math.Max(0, J.Num(fo, "startDelay")) * 1000;
                if (delay > 0) t.AddSegment(baseLayer, delay, "linear");
                t.AddSegment(target, baseDur, baseEase);
                var prev = target;
                foreach (var s in stages)
                {
                    double sd = Math.Max(0, J.Num(s, "startDelay")) * 1000;
                    if (sd > 0) t.AddSegment(prev, sd, "linear");
                    var next = J.Merge(prev, s, MetaKeys);
                    t.AddSegment(next, (J.NumOpt(s, "duration") ?? J.Num(fo, "duration")) * 1000, J.Str(s, "easing") ?? baseEase);
                    prev = next;
                }
                return t;
            }

            static void ApplyLegacy(Dictionary<string, object> baseLayer, Dictionary<string, object> target, Dictionary<string, object> fo)
            {
                var tline = J.EnsureObj(target, "line");
                var bline = J.ObjOrEmpty(baseLayer, "line");
                double fof = J.Num(fo, "firingOffset");
                if (fof != 0 && !J.Has(J.Obj(fo, "line"), "offset"))
                {
                    double sign = J.Str(fo, "bloomDirection") == "inward" ? -1 : 1;
                    tline["offset"] = Math.Max(0, J.Num(bline, "offset") + sign * fof * 2);
                }
                if (J.Bool(fo, "tShapeWhenFiring") && !J.Has(J.Obj(fo, "line"), "visible"))
                {
                    var vis = J.StrList(bline, "visible").Where(v => v != "top").Cast<object>().ToList();
                    tline["visible"] = vis;
                }
                double fin = J.Num(fo, "finalOpacity", 1);
                if (fin != 1)
                    foreach (var k in new[] { "line", "dot", "outline" })
                    {
                        var o = J.EnsureObj(target, k);
                        o["opacity"] = J.Num(o, "opacity") * fin;
                    }
            }

            void AddSegment(Dictionary<string, object> to, double ms, string ease)
            {
                keys.Add(to);
                durs.Add(Math.Max(0, ms));
                eases.Add(ease ?? "linear");
                total += Math.Max(0, ms);
            }

            public void StartForward(long now, bool fromStart)
            {
                if (fromStart) { Position = 0; cycleCount = 0; }
                pingPongReturning = false;
                velocity = 1;
                lastTick = now;
                if (total <= 0) { Position = 0; velocity = 1; }
            }

            public void Stop(double pos) { velocity = 0; Position = pos; pingPongReturning = false; }

            public void Press(long now)
            {
                held = true;
                if (PressType == "hold")
                {
                    // continue from wherever we are (supports "pause" and mid-reverse presses)
                    StartForward(now, fromStart: Position <= 0 || Position >= total && !Loop);
                    if (Position >= total && total > 0 && !Loop) velocity = 0;
                }
                else
                {
                    if (Loop && velocity != 0) { Stop(0); return; }   // single press toggles looping animations
                    StartForward(now, fromStart: true);
                }
            }

            public void Release(long now)
            {
                held = false;
                if (PressType != "hold") return;
                switch (ReleaseMode)
                {
                    case "pause": velocity = 0; break;
                    case "reverse": velocity = -1; lastTick = now; pingPongReturning = false; break;
                    default: Stop(0); break;
                }
            }

            public void Advance(long now)
            {
                if (velocity == 0) { lastTick = now; return; }
                double dt = Math.Max(0, now - lastTick);
                lastTick = now;
                if (total <= 0)
                {
                    // zero-length animation: jump straight to the end (or back to the start)
                    Position = velocity > 0 ? 1 : 0;
                    velocity = 0;
                    return;
                }
                Position += velocity * dt;
                if (velocity > 0 && Position >= total)
                {
                    if (Loop)
                    {
                        cycleCount++;
                        if (Alternate) { Position = total - (Position - total); velocity = -1; pingPongReturning = true; }
                        else Position = Position % total;
                    }
                    else if (Alternate && !pingPongReturning && PressType != "hold")
                    {
                        Position = total - (Position - total);
                        velocity = -1;
                        pingPongReturning = true;
                    }
                    else { Position = total; velocity = 0; }
                }
                else if (velocity < 0 && Position <= 0)
                {
                    Position = 0;
                    if (Loop && pingPongReturning && (held || PressType != "hold" || Trigger == "autoplay"))
                    {
                        velocity = 1;
                        pingPongReturning = false;
                    }
                    else { velocity = 0; pingPongReturning = false; }
                }
                if (Position < 0) Position = 0;
            }

            public Dictionary<string, object> Evaluate()
            {
                if (keys.Count < 2) return null;
                if (total <= 0) return Position > 0 ? keys[keys.Count - 1] : null;
                double p = Math.Max(0, Math.Min(total, Position));
                double acc = 0;
                for (int i = 0; i < durs.Count; i++)
                {
                    double d = durs[i];
                    if (p <= acc + d || i == durs.Count - 1)
                    {
                        double local = d <= 0 ? 1 : (p - acc) / d;
                        local = Math.Max(0, Math.Min(1, local));
                        double e = Easing.Apply(eases[i], local);
                        return Lerp(keys[i], keys[i + 1], e);
                    }
                    acc += d;
                }
                return keys[keys.Count - 1];
            }
        }

        // ---------------- interpolation ----------------

        static bool TreeEquals(object a, object b) => Json.Serialize(a) == Json.Serialize(b);

        public static Dictionary<string, object> Lerp(Dictionary<string, object> a, Dictionary<string, object> b, double t)
        {
            var r = new Dictionary<string, object>();
            foreach (var kv in a)
            {
                b.TryGetValue(kv.Key, out var bv);
                r[kv.Key] = LerpValue(kv.Key, kv.Value, b.ContainsKey(kv.Key) ? bv : kv.Value, t, a, b);
            }
            foreach (var kv in b)
                if (!r.ContainsKey(kv.Key)) r[kv.Key] = t >= 0.5 ? kv.Value : null;
            if (a.ContainsKey("visible") || b.ContainsKey("visible")) LerpVisibility(a, b, t, r, "visible", "_vis", Defaults.AllDirections);
            if (a.ContainsKey("visibleArms") || b.ContainsKey("visibleArms"))
            {
                int arms = (int)Math.Max(J.Num(a, "armCount", 4), J.Num(b, "armCount", 4));
                LerpVisibility(a, b, t, r, "visibleArms", "_visArms", Enumerable.Range(0, Math.Max(arms, 8)).Select(x => x.ToString()).ToArray());
            }
            return r;
        }

        static object LerpValue(string key, object av, object bv, double t, Dictionary<string, object> pa, Dictionary<string, object> pb)
        {
            if (av is Dictionary<string, object> ad && bv is Dictionary<string, object> bd) return Lerp(ad, bd, t);
            if (IsNum(av) && IsNum(bv))
            {
                double x = J.ToNum(av), y = J.ToNum(bv);
                return x + (y - x) * t;
            }
            if (av is string sa && bv is string sb && sa != sb && ColorUtil.LooksLikeColor(sa) && ColorUtil.LooksLikeColor(sb))
            {
                var c = ColorUtil.Lerp(ColorUtil.Parse(sa), ColorUtil.Parse(sb), t);
                return ColorUtil.ToHex(c, true);
            }
            if (key == "visible" || key == "visibleArms") return av; // handled separately
            return t >= 0.5 ? bv : av;
        }

        static bool IsNum(object v) => v is double || v is int || v is long || v is float || v is decimal;

        static void LerpVisibility(Dictionary<string, object> a, Dictionary<string, object> b, double t, Dictionary<string, object> r,
            string key, string alphaKey, string[] universe)
        {
            var av = Alphas(a, key, alphaKey, universe);
            var bv = Alphas(b, key, alphaKey, universe);
            var outVis = new Dictionary<string, object>();
            var union = new List<object>();
            foreach (var u in universe)
            {
                double x = av.TryGetValue(u, out var xa) ? xa : 0, y = bv.TryGetValue(u, out var ya) ? ya : 0;
                double v = x + (y - x) * t;
                outVis[u] = v;
                if (v > 0) union.Add(key == "visibleArms" ? (object)double.Parse(u) : u);
            }
            r[alphaKey] = outVis;
            if (key == "visible" || J.List(a, key) != null || J.List(b, key) != null) r[key] = union;
        }

        static Dictionary<string, double> Alphas(Dictionary<string, object> o, string key, string alphaKey, string[] universe)
        {
            var d = new Dictionary<string, double>();
            if (J.Obj(o, alphaKey) is Dictionary<string, object> existing)
            {
                foreach (var kv in existing) d[kv.Key] = J.ToNum(kv.Value);
                return d;
            }
            var list = J.List(o, key);
            if (list == null)
            {
                foreach (var u in universe) d[u] = key == "visibleArms" ? 1 : 0;
                return d;
            }
            foreach (var x in list)
            {
                string k = x is string s ? s : ((int)J.ToNum(x)).ToString();
                d[k] = 1;
            }
            return d;
        }
    }

    /// <summary>anime.js-compatible easing curves.</summary>
    public static class Easing
    {
        public static readonly string[] Names =
        {
            "linear", "easeInSine", "easeOutSine", "easeInOutSine", "easeInQuad", "easeOutQuad", "easeInOutQuad",
            "easeInCubic", "easeOutCubic", "easeInOutCubic", "easeInQuart", "easeOutQuart", "easeInOutQuart",
            "easeInExpo", "easeOutExpo", "easeInOutExpo", "easeInCirc", "easeOutCirc", "easeInOutCirc",
            "easeInBack", "easeOutBack", "easeInOutBack", "easeInBounce", "easeOutBounce", "easeInOutBounce",
            "easeInElastic", "easeOutElastic", "easeInOutElastic"
        };

        public static double Apply(string name, double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            string n = (name ?? "linear").ToLowerInvariant().Replace(" ", "").Replace("-", "");
            int paren = n.IndexOf('(');
            if (paren >= 0) n = n.Substring(0, paren);
            if (n == "linear" || n.Length == 0) return t;

            Func<double, double> fin;
            if (n.Contains("sine")) fin = x => 1 - Math.Cos(x * Math.PI / 2);
            else if (n.Contains("cubic")) fin = x => x * x * x;
            else if (n.Contains("quart")) fin = x => x * x * x * x;
            else if (n.Contains("quint")) fin = x => x * x * x * x * x;
            else if (n.Contains("expo")) fin = x => x == 0 ? 0 : Math.Pow(2, 10 * x - 10);
            else if (n.Contains("circ")) fin = x => 1 - Math.Sqrt(1 - x * x);
            else if (n.Contains("back")) fin = x => x * x * (2.70158 * x - 1.70158);
            else if (n.Contains("bounce")) fin = x => 1 - BounceOut(1 - x);
            else if (n.Contains("elastic")) fin = x => x == 0 || x == 1 ? x : -Math.Pow(2, 10 * x - 10) * Math.Sin((x * 10 - 10.75) * (2 * Math.PI / 3));
            else fin = x => x * x; // quad and plain "easeIn/easeOut"

            bool inOut = n.Contains("inout");
            bool outIn = n.Contains("outin");
            bool isIn = !inOut && !outIn && (n.StartsWith("easein") || n.StartsWith("in"));
            bool isOut = !inOut && !outIn && (n.StartsWith("easeout") || n.StartsWith("out"));
            if (n.Contains("bounce") && !inOut && !isIn) return BounceOut(t);
            if (isIn) return fin(t);
            if (isOut || (!inOut && !outIn)) return 1 - fin(1 - t);
            if (inOut) return t < 0.5 ? fin(t * 2) / 2 : 1 - fin(-2 * t + 2) / 2;
            return t < 0.5 ? (1 - fin(1 - 2 * t)) / 2 : (fin(2 * t - 1) + 1) / 2;
        }

        static double BounceOut(double x)
        {
            const double n1 = 7.5625, d1 = 2.75;
            if (x < 1 / d1) return n1 * x * x;
            if (x < 2 / d1) { x -= 1.5 / d1; return n1 * x * x + 0.75; }
            if (x < 2.5 / d1) { x -= 2.25 / d1; return n1 * x * x + 0.9375; }
            x -= 2.625 / d1;
            return n1 * x * x + 0.984375;
        }
    }
}
