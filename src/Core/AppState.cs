using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CrosshairY.Core
{
    public sealed class CrosshairBind
    {
        public string Key = "";
        public string PadKey = "";
        public string CrosshairId = "";
    }

    /// <summary>A recoil loadout slot: a weapon (or "off") switched to with a key / controller button.</summary>
    public sealed class RecoilSlot
    {
        public string Weapon = "";   // Recoil pattern key ("Game|Name") or "off"
        public string Key = "";
        public string PadKey = "";
        public double Scale = 1;     // multiplies each tracker's own scale while this slot is selected
        public string CrosshairId = ""; // optional: switch to this crosshair when the slot is selected

        public RecoilSlot Copy() => new RecoilSlot { Weapon = Weapon, Key = Key, PadKey = PadKey, Scale = Scale, CrosshairId = CrosshairId };

        public Dictionary<string, object> ToJson() => J.O("weapon", Weapon, "key", Key, "padKey", PadKey, "scale", Scale, "crosshairId", CrosshairId);

        public static RecoilSlot FromJson(object s)
        {
            var r = new RecoilSlot { Weapon = J.Str(s, "weapon", ""), Key = J.Str(s, "key", ""), PadKey = J.Str(s, "padKey", ""),
                Scale = J.Num(s, "scale", 1), CrosshairId = J.Str(s, "crosshairId", "") };
            if (r.Scale <= 0) r.Scale = 1;
            return r;
        }
    }

    public sealed class SavedPosition
    {
        public string Name = "Position";
        public int X, Y;
    }

    /// <summary>A bundle of settings for a game/app: default crosshair, keybinds, and position &amp; size.</summary>
    public sealed class Profile
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "Default";
        public List<string> Processes = new List<string>();
        public string CrosshairId = "";
        public int OffsetX, OffsetY;
        public double Scale = 1;
        public double Opacity = 1;

        // keybinds
        public string ToggleKey = "Shift+Alt+Z";
        public string FireKey = "LMB";
        public string AimKey = "RMB";
        public string AimAction = "none";        // none | hide | show | switch
        public bool AimToggleMode;               // single click toggles instead of hold
        public string AimCrosshairId = "";
        public string ReloadKey = "R";
        public string TogglePad = "", FirePad = "PadRT", AimPad = "PadLT", ReloadPad = "PadX", NextPad = "", PrevPad = "";
        public string PositionKeys = "none";
        public string NextWeaponKey = "", PrevWeaponKey = "", NextWeaponPad = "", PrevWeaponPad = "";     // none | arrows | numpad
        public string NextKey = "";
        public string PrevKey = "";
        public string MoveUpKey = "", MoveDownKey = "", MoveLeftKey = "", MoveRightKey = "", CenterKey = "";
        public string SizeUpKey = "", SizeDownKey = "";
        public string HitKey = "", HitPad = "", KillKey = "", KillPad = "";
        public List<CrosshairBind> CrosshairBinds = new List<CrosshairBind>();
        public List<SavedPosition> Positions = new List<SavedPosition>();
        public List<RecoilSlot> RecoilSlots = new List<RecoilSlot>();

        public Profile Clone()
        {
            var p = (Profile)MemberwiseClone();
            p.Id = Guid.NewGuid().ToString("N");
            p.Processes = new List<string>(Processes);
            p.CrosshairBinds = CrosshairBinds.Select(b => new CrosshairBind { Key = b.Key, PadKey = b.PadKey, CrosshairId = b.CrosshairId }).ToList();
            p.Positions = Positions.Select(x => new SavedPosition { Name = x.Name, X = x.X, Y = x.Y }).ToList();
            p.RecoilSlots = RecoilSlots.Select(x => x.Copy()).ToList();
            return p;
        }

        public Dictionary<string, object> ToJson() => J.O(
            "id", Id, "name", Name, "processes", Processes.Cast<object>().ToList(), "crosshairId", CrosshairId,
            "offsetX", OffsetX, "offsetY", OffsetY, "scale", Scale, "opacity", Opacity,
            "toggleKey", ToggleKey, "fireKey", FireKey, "aimKey", AimKey, "aimAction", AimAction, "aimToggleMode", AimToggleMode,
            "aimCrosshairId", AimCrosshairId, "reloadKey", ReloadKey, "nextKey", NextKey, "prevKey", PrevKey,
            "moveUpKey", MoveUpKey, "moveDownKey", MoveDownKey, "moveLeftKey", MoveLeftKey, "moveRightKey", MoveRightKey,
            "centerKey", CenterKey, "sizeUpKey", SizeUpKey, "sizeDownKey", SizeDownKey,
            "togglePad", TogglePad, "firePad", FirePad, "aimPad", AimPad, "reloadPad", ReloadPad, "nextPad", NextPad, "prevPad", PrevPad, "positionKeys", PositionKeys, "nextWeaponKey", NextWeaponKey, "prevWeaponKey", PrevWeaponKey, "nextWeaponPad", NextWeaponPad, "prevWeaponPad", PrevWeaponPad,
            "crosshairBinds", CrosshairBinds.Select(b => (object)J.O("key", b.Key, "padKey", b.PadKey, "crosshairId", b.CrosshairId)).ToList(),
            "positions", Positions.Select(p => (object)J.O("name", p.Name, "x", p.X, "y", p.Y)).ToList(),
            "hitKey", HitKey, "hitPad", HitPad, "killKey", KillKey, "killPad", KillPad,
            "recoilSlots", RecoilSlots.Select(s => (object)s.ToJson()).ToList());

        public static Profile FromJson(object o)
        {
            var p = new Profile
            {
                Id = J.Str(o, "id") ?? Guid.NewGuid().ToString("N"),
                Name = J.Str(o, "name", "Profile"),
                Processes = J.StrList(o, "processes"),
                CrosshairId = J.Str(o, "crosshairId", ""),
                OffsetX = (int)J.Num(o, "offsetX"),
                OffsetY = (int)J.Num(o, "offsetY"),
                Scale = J.Num(o, "scale", 1),
                Opacity = J.Num(o, "opacity", 1),
                ToggleKey = J.Str(o, "toggleKey", "Shift+Alt+Z"),
                FireKey = J.Str(o, "fireKey", "LMB"),
                AimKey = J.Str(o, "aimKey", "RMB"),
                AimAction = J.Str(o, "aimAction", "none"),
                AimToggleMode = J.Bool(o, "aimToggleMode"),
                AimCrosshairId = J.Str(o, "aimCrosshairId", ""),
                ReloadKey = J.Str(o, "reloadKey", "R"),
                TogglePad = J.Str(o, "togglePad", ""),
                FirePad = J.Str(o, "firePad", "PadRT"),
                AimPad = J.Str(o, "aimPad", "PadLT"),
                ReloadPad = J.Str(o, "reloadPad", "PadX"),
                NextPad = J.Str(o, "nextPad", ""),
                PrevPad = J.Str(o, "prevPad", ""),
                PositionKeys = J.Str(o, "positionKeys", "none"),
                NextWeaponKey = J.Str(o, "nextWeaponKey", ""),
                PrevWeaponKey = J.Str(o, "prevWeaponKey", ""),
                NextWeaponPad = J.Str(o, "nextWeaponPad", ""),
                PrevWeaponPad = J.Str(o, "prevWeaponPad", ""),
                NextKey = J.Str(o, "nextKey", ""),
                PrevKey = J.Str(o, "prevKey", ""),
                MoveUpKey = J.Str(o, "moveUpKey", ""),
                MoveDownKey = J.Str(o, "moveDownKey", ""),
                MoveLeftKey = J.Str(o, "moveLeftKey", ""),
                MoveRightKey = J.Str(o, "moveRightKey", ""),
                CenterKey = J.Str(o, "centerKey", ""),
                SizeUpKey = J.Str(o, "sizeUpKey", ""),
                SizeDownKey = J.Str(o, "sizeDownKey", ""),
                HitKey = J.Str(o, "hitKey", ""),
                HitPad = J.Str(o, "hitPad", ""),
                KillKey = J.Str(o, "killKey", ""),
                KillPad = J.Str(o, "killPad", "")
            };
            if (p.Scale <= 0) p.Scale = 1;
            foreach (var b in J.List(o, "crosshairBinds") ?? new List<object>())
                p.CrosshairBinds.Add(new CrosshairBind { Key = J.Str(b, "key", ""), PadKey = J.Str(b, "padKey", ""), CrosshairId = J.Str(b, "crosshairId", "") });
            foreach (var s in J.List(o, "recoilSlots") ?? new List<object>())
                p.RecoilSlots.Add(RecoilSlot.FromJson(s));
            foreach (var x in J.List(o, "positions") ?? new List<object>())
                p.Positions.Add(new SavedPosition { Name = J.Str(x, "name", "Position"), X = (int)J.Num(x, "x"), Y = (int)J.Num(x, "y") });
            return p;
        }
    }

    public sealed class Settings
    {
        public string Monitor = "";                  // device name, "" = primary
        public bool VisibleOnLaunch = true;
        public bool LaunchOnStartup;
        public bool StartMinimized;
        public bool CloseToTray = true;
        public bool OnlyShowInGame;
        public List<string> GameProcesses = new List<string>();
        public bool ProfileDetection = true;
        public bool ControllerSupport;
        public bool IgnoreDpiScaling = true;
        public bool ShowInCapture = true;
        public bool ShowTrayNotifications;
        public string ActiveProfileId = "";
        public string LibrarySort = "manual";
        public bool FirstRunDone;
        public string DesignerBackground = "dark";
        public bool DesignerGrid = true;
        public int FrameRate = 144;
        public string DisplayMode = "overlay";       // overlay | assist (re-asserts topmost whenever a game takes focus)
        public bool SidebarCollapsed;
        // updates
        public bool AutoUpdate = true;
        public string LastUpdateCheck = "";          // ISO date of the last automatic check
        public string SkippedVersion = "";
        // first-run tour, language
        public bool TourDone;
        public string Language = "en";
        // reactions (hit marker / kill flash)
        public bool HitOnFire;
        public string HitStyle = "x";                // x | ring | brackets
        public string HitColor = "#FFFFFF";
        public string KillColor = "#FF3030";
        public double ReactionSize = 1;
        public int ReactionMs = 220;

        public Dictionary<string, object> ToJson() => J.O(
            "monitor", Monitor, "visibleOnLaunch", VisibleOnLaunch, "launchOnStartup", LaunchOnStartup,
            "startMinimized", StartMinimized, "closeToTray", CloseToTray, "onlyShowInGame", OnlyShowInGame,
            "gameProcesses", GameProcesses.Cast<object>().ToList(), "profileDetection", ProfileDetection,
            "controllerSupport", ControllerSupport, "ignoreDpiScaling", IgnoreDpiScaling, "showInCapture", ShowInCapture,
            "showTrayNotifications", ShowTrayNotifications, "activeProfileId", ActiveProfileId, "librarySort", LibrarySort,
            "firstRunDone", FirstRunDone, "designerBackground", DesignerBackground, "designerGrid", DesignerGrid,
            "frameRate", FrameRate, "displayMode", DisplayMode, "sidebarCollapsed", SidebarCollapsed,
            "autoUpdate", AutoUpdate, "lastUpdateCheck", LastUpdateCheck, "skippedVersion", SkippedVersion,
            "tourDone", TourDone, "language", Language,
            "hitOnFire", HitOnFire, "hitStyle", HitStyle, "hitColor", HitColor, "killColor", KillColor,
            "reactionSize", ReactionSize, "reactionMs", ReactionMs);

        public static Settings FromJson(object o) => new Settings
        {
            Monitor = J.Str(o, "monitor", ""),
            VisibleOnLaunch = J.Bool(o, "visibleOnLaunch", true),
            LaunchOnStartup = J.Bool(o, "launchOnStartup"),
            StartMinimized = J.Bool(o, "startMinimized"),
            CloseToTray = J.Bool(o, "closeToTray", true),
            OnlyShowInGame = J.Bool(o, "onlyShowInGame"),
            GameProcesses = J.StrList(o, "gameProcesses"),
            ProfileDetection = J.Bool(o, "profileDetection", true),
            ControllerSupport = J.Bool(o, "controllerSupport"),
            IgnoreDpiScaling = J.Bool(o, "ignoreDpiScaling", true),
            ShowInCapture = J.Bool(o, "showInCapture", true),
            ShowTrayNotifications = J.Bool(o, "showTrayNotifications", false),
            ActiveProfileId = J.Str(o, "activeProfileId", ""),
            LibrarySort = J.Str(o, "librarySort", "manual"),
            FirstRunDone = J.Bool(o, "firstRunDone"),
            DesignerBackground = J.Str(o, "designerBackground", "dark"),
            DesignerGrid = J.Bool(o, "designerGrid", true),
            FrameRate = (int)J.Num(o, "frameRate", 144),
            DisplayMode = J.Str(o, "displayMode", "overlay"),
            SidebarCollapsed = J.Bool(o, "sidebarCollapsed"),
            AutoUpdate = J.Bool(o, "autoUpdate", true),
            LastUpdateCheck = J.Str(o, "lastUpdateCheck", ""),
            SkippedVersion = J.Str(o, "skippedVersion", ""),
            TourDone = J.Bool(o, "tourDone"),
            Language = J.Str(o, "language", "en"),
            HitOnFire = J.Bool(o, "hitOnFire"),
            HitStyle = J.Str(o, "hitStyle", "x"),
            HitColor = J.Str(o, "hitColor", "#FFFFFF"),
            KillColor = J.Str(o, "killColor", "#FF3030"),
            ReactionSize = Math.Max(0.25, J.Num(o, "reactionSize", 1)),
            ReactionMs = (int)Math.Max(60, J.Num(o, "reactionMs", 220))
        };
    }

    /// <summary>All persisted data plus change notifications.</summary>
    public sealed class AppState
    {
        public static readonly string DataDir = Environment.GetEnvironmentVariable("CROSSHAIRY_DATA") is string d && d.Length > 0
            ? d : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrosshairY");
        static string SettingsFile => Path.Combine(DataDir, "settings.json");
        static string LibraryFile => Path.Combine(DataDir, "library.json");
        static string ProfilesFile => Path.Combine(DataDir, "profiles.json");

        public Settings Settings = new Settings();
        public List<CrosshairEntry> Library = new List<CrosshairEntry>();
        public List<string> Folders = new List<string>();
        public List<Profile> Profiles = new List<Profile>();

        public event Action LibraryChanged;
        public event Action ProfilesChanged;
        public event Action SettingsChanged;

        Timer saveTimer;
        readonly object saveLock = new object();
        bool dirtyLibrary, dirtySettings, dirtyProfiles;

        public Profile ActiveProfile
        {
            get
            {
                var p = Profiles.FirstOrDefault(x => x.Id == Settings.ActiveProfileId);
                if (p == null)
                {
                    if (Profiles.Count == 0) Profiles.Add(new Profile());
                    p = Profiles[0];
                    Settings.ActiveProfileId = p.Id;
                }
                return p;
            }
        }

        public CrosshairEntry Find(string id) => string.IsNullOrEmpty(id) ? null : Library.FirstOrDefault(e => e.Id == id);

        public void Load()
        {
            Directory.CreateDirectory(DataDir);
            try { if (File.Exists(SettingsFile)) Settings = Settings.FromJson(Json.Parse(File.ReadAllText(SettingsFile, Encoding.UTF8))); }
            catch { BackupCorrupt(SettingsFile); }
            try
            {
                if (File.Exists(LibraryFile))
                {
                    var root = Json.Parse(File.ReadAllText(LibraryFile, Encoding.UTF8));
                    Library = (J.List(root, "crosshairs") ?? new List<object>()).Select(CrosshairEntry.FromJson).ToList();
                    Folders = J.StrList(root, "folders");
                }
            }
            catch { BackupCorrupt(LibraryFile); }
            try
            {
                if (File.Exists(ProfilesFile))
                    Profiles = (J.List(Json.Parse(File.ReadAllText(ProfilesFile, Encoding.UTF8)), "profiles") ?? new List<object>()).Select(Profile.FromJson).ToList();
            }
            catch { BackupCorrupt(ProfilesFile); }
            if (Profiles.Count == 0) Profiles.Add(new Profile { Name = "Default" });
            var _ = ActiveProfile;
        }

        static void BackupCorrupt(string file)
        {
            try { File.Copy(file, file + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
        }

        public void MarkLibraryChanged() { dirtyLibrary = true; ScheduleSave(); LibraryChanged?.Invoke(); }
        public void MarkProfilesChanged() { dirtyProfiles = true; ScheduleSave(); ProfilesChanged?.Invoke(); }
        public void MarkSettingsChanged() { dirtySettings = true; ScheduleSave(); SettingsChanged?.Invoke(); }

        void ScheduleSave()
        {
            lock (saveLock)
            {
                if (saveTimer == null) saveTimer = new Timer(_ => SaveNow(), null, 600, Timeout.Infinite);
                else saveTimer.Change(600, Timeout.Infinite);
            }
        }

        public void SaveNow()
        {
            lock (saveLock)
            {
                try
                {
                    Directory.CreateDirectory(DataDir);
                    if (dirtySettings) { WriteAtomic(SettingsFile, Json.Serialize(Settings.ToJson(), true)); dirtySettings = false; }
                    if (dirtyProfiles)
                    {
                        WriteAtomic(ProfilesFile, Json.Serialize(J.O("profiles", Profiles.Select(p => (object)p.ToJson()).ToList()), true));
                        dirtyProfiles = false;
                    }
                    if (dirtyLibrary)
                    {
                        string json = Json.Serialize(J.O("version", 1, "folders", Folders.Cast<object>().ToList(),
                            "crosshairs", Library.Select(e => (object)e.ToJson()).ToList()));
                        WriteAtomic(LibraryFile, json);
                        dirtyLibrary = false;
                    }
                }
                catch { /* disk full / locked: retry on next change */ }
            }
        }

        public void SaveAllNow()
        {
            dirtyLibrary = dirtySettings = dirtyProfiles = true;
            SaveNow();
        }

        static void WriteAtomic(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); return; } catch { }
                File.Delete(path);
            }
            File.Move(tmp, path);
        }

        public CrosshairEntry Add(CrosshairEntry e, bool atTop = true)
        {
            if (atTop)
            {
                foreach (var x in Library) x.Order++;
                e.Order = 0;
                Library.Insert(0, e);
            }
            else
            {
                e.Order = Library.Count == 0 ? 0 : Library.Max(x => x.Order) + 1;
                Library.Add(e);
            }
            MarkLibraryChanged();
            return e;
        }

        public void Remove(string id)
        {
            Library.RemoveAll(e => e.Id == id);
            foreach (var p in Profiles)
            {
                p.CrosshairBinds.RemoveAll(b => b.CrosshairId == id);
                if (p.CrosshairId == id) p.CrosshairId = Library.FirstOrDefault()?.Id ?? "";
                if (p.AimCrosshairId == id) p.AimCrosshairId = "";
            }
            MarkLibraryChanged();
            MarkProfilesChanged();
        }

        /// <summary>Full backup: settings, profiles (keybinds, loadouts), library, categories and custom recoil patterns.</summary>
        public string ExportAll() => Json.Serialize(J.O(
            "app", "CrosshairY", "version", 2, "appVersion", Program.Version, "exported", DateTime.UtcNow.ToString("o"),
            "settings", Settings.ToJson(),
            "folders", Folders.Cast<object>().ToList(),
            "crosshairs", Library.Select(e => (object)e.ToJson()).ToList(),
            "profiles", Profiles.Select(p => (object)p.ToJson()).ToList(),
            "patterns", Recoil.CustomPatterns.Select(p => (object)Recoil.PatternToJson(p)).ToList()), true);

        /// <summary>One profile with the crosshairs it refers to, so it works on another PC.</summary>
        public string ExportProfile(Profile p)
        {
            var ids = new HashSet<string> { p.CrosshairId, p.AimCrosshairId };
            foreach (var b in p.CrosshairBinds) ids.Add(b.CrosshairId);
            foreach (var s in p.RecoilSlots) ids.Add(s.CrosshairId);
            return Json.Serialize(J.O("app", "CrosshairY", "kind", "profile", "version", 2,
                "profiles", J.A(p.ToJson()),
                "crosshairs", Library.Where(e => ids.Contains(e.Id)).Select(e => (object)e.ToJson()).ToList()), true);
        }

        /// <summary>Replaces everything with a backup's contents (settings, library, profiles, custom patterns).</summary>
        public void RestoreAll(string json)
        {
            var root = Json.Parse(json);
            if (J.List(root, "crosshairs") == null && J.List(root, "profiles") == null) throw new InvalidDataException("This file isn't a CrosshairY backup.");
            Library = (J.List(root, "crosshairs") ?? new List<object>()).Select(CrosshairEntry.FromJson).ToList();
            Folders = J.StrList(root, "folders");
            Profiles = (J.List(root, "profiles") ?? new List<object>()).Select(Profile.FromJson).ToList();
            if (Profiles.Count == 0) Profiles.Add(new Profile());
            if (J.Obj(root, "settings") is Dictionary<string, object> s)
            {
                var keep = Settings;
                Settings = Settings.FromJson(s);
                Settings.FirstRunDone = true;
                Settings.Monitor = keep.Monitor;        // monitors are per-PC
            }
            if (J.List(root, "patterns") is List<object> pats) Recoil.SetCustom(pats.Select(Recoil.PatternFromJson).Where(x => x != null));
            var _ = ActiveProfile;
            SaveAllNow();
            MarkLibraryChanged();
            MarkProfilesChanged();
            MarkSettingsChanged();
        }

        /// <summary>Merges a backup or exported profile: adds crosshairs, profiles and custom patterns that aren't here yet.</summary>
        public int ImportAll(string json)
        {
            var root = Json.Parse(json);
            int added = 0;
            var idMap = new Dictionary<string, string>();
            foreach (var c in J.List(root, "crosshairs") ?? new List<object>())
            {
                var e = CrosshairEntry.FromJson(c);
                if (Find(e.Id) != null)
                {
                    // identical design already here: reuse it; otherwise keep both
                    if (Json.Serialize(Find(e.Id).Layers) == Json.Serialize(e.Layers)) { idMap[e.Id] = e.Id; continue; }
                    string old = e.Id;
                    e.Id = Guid.NewGuid().ToString("N");
                    idMap[old] = e.Id;
                }
                Add(e, false);
                added++;
            }
            foreach (var f in J.StrList(root, "folders")) if (!Folders.Contains(f)) Folders.Add(f);
            foreach (var p in J.List(root, "profiles") ?? new List<object>())
            {
                var prof = Profile.FromJson(p);
                if (Profiles.Any(x => x.Id == prof.Id))
                {
                    if (J.Str(root, "kind") != "profile") continue;
                    prof.Id = Guid.NewGuid().ToString("N");   // importing a profile twice makes a copy
                    prof.Name += " (imported)";
                }
                Func<string, string> map = id => id != null && idMap.TryGetValue(id, out var n) ? n : id;
                prof.CrosshairId = map(prof.CrosshairId);
                prof.AimCrosshairId = map(prof.AimCrosshairId);
                foreach (var b in prof.CrosshairBinds) b.CrosshairId = map(b.CrosshairId);
                foreach (var s in prof.RecoilSlots) s.CrosshairId = map(s.CrosshairId);
                Profiles.Add(prof);
            }
            if (J.List(root, "patterns") is List<object> pats)
            {
                var incoming = pats.Select(Recoil.PatternFromJson).Where(x => x != null && Recoil.CustomPatterns.All(c => c.Key != x.Key)).ToList();
                if (incoming.Count > 0) Recoil.SetCustom(Recoil.CustomPatterns.Concat(incoming));
            }
            MarkLibraryChanged();
            MarkProfilesChanged();
            return added;
        }
    }
}
