# Next steps

The plan for the next CrosshairY release (1.3). Each item has the design, where it lives in the code and what's left. ✅ = done, 🟡 = done in code but needs something outside it, ⬜ = not started.

| # | Feature | Status |
|---|---|---|
| 1 | Auto-update | ✅ |
| 2 | Code signing | 🟡 build support done; needs a certificate |
| 3 | Spray pattern editor | ✅ |
| 4 | Per-weapon scale in the loadout | ✅ |
| 9 | Hit marker & kill flash | ✅ |
| 10 | Per-weapon crosshairs | ✅ |
| 11 | Community gallery | 🟡 app side done; gallery goes live when `gallery/` is pushed |
| 13 | First-run tour | ✅ |
| 14 | Backup export / import | ✅ |
| 15 | Tray menu | ✅ |
| 16 | Localization | ✅ 4 languages, ~360 strings each |

### Verified

- Logic harness (26 checks): version comparison; a live GitHub check that finds 1.2.0 with its SHA-256; custom patterns that override and restore built-ins and survive a reload; slot scale halving the spray while keeping the base scale; full backup and restore; profile import remapping clashing crosshair ids; all reaction styles drawing and fading; translation lookups with format placeholders intact.
- End-to-end update in a sandbox: the real 1.2.0 asset downloaded, its checksum verified, a tampered checksum refused, the exe swapped and the old one renamed.
- Screenshots: tour steps, Settings (updates, backup), Keybinds (loadout scale and crosshair, hit and kill rows), pattern editor, update dialog, German UI.

### Still to do by hand

- **Signing:** get a certificate (see `docs/SIGNING.md`), then `.\build.ps1 -Sign`.
- **Gallery:** push `gallery/` and `.github/ISSUE_TEMPLATE/gallery.yml`, then create a `gallery` label on GitHub. New submissions are added to `gallery.json` by hand for now.
- **Translations:** a native speaker should review them. Strings built from pieces (e.g. “Imported “name””) and dropdown option lists in the Designer are still English.
- Click-through checks that screenshots can't cover: dragging in the pattern editor, the tray weapon menu, and an update followed by a restart on a release that knows `--updated` (1.3 onward).

---

## 1. Auto-update

**Goal:** people find out about new versions without checking GitHub.

- On launch (at most once a day) query `https://api.github.com/repos/corund207/CrosshairY/releases/latest`. Compare `tag_name` with `Program.Version`.
- If newer, show a toast/banner: **Update to x.y.z**. Clicking it shows the release notes, downloads `CrosshairY.exe` to `%TEMP%`, verifies the asset's SHA-256 against the `digest` GitHub publishes, then swaps the exe and restarts.
- The swap works because a running exe can be renamed: rename `CrosshairY.exe` → `CrosshairY.old.exe`, move the new file in, start it with `--updated`, exit. The new process deletes `*.old.exe`.
- Settings › General: **Check for updates automatically** (on by default) and a **Check now** button. Help shows the current version.
- Code: `src/Core/Updater.cs`, `Settings.AutoUpdate`, `Settings.LastUpdateCheck`.

## 2. Code signing

**Goal:** no SmartScreen / Smart App Control block.

- Code side: `build.ps1 -Sign` signs the exe with `signtool` using a certificate from the store (`$env:CROSSHAIRY_CERT_THUMBPRINT`) or a PFX (`$env:CROSSHAIRY_PFX` + `$env:CROSSHAIRY_PFX_PASSWORD`), timestamped via `http://timestamp.digicert.com`.
- Needs a certificate, which costs money or an application. Options, cheapest first:
  1. **SignPath Foundation**: free code signing for open-source projects (apply with the repo).
  2. **Azure Trusted Signing**: ~$10/month, individual developers supported, instant SmartScreen reputation.
  3. A standard OV certificate (~$200–400/year). SmartScreen reputation builds over time.
- See `docs/SIGNING.md`.

## 3. Spray pattern editor

**Goal:** fix a pattern that feels off, or add a gun or game that isn't built in, without a new release.

- Dialog with a zoomable grid: one dot per bullet, connected in order. Drag a dot to move it; click empty space to add the next bullet; right-click to remove. Shift-drag moves every later bullet too.
- Fields: name, game (free text, defaults to "Custom"), fire rate (RPM), accurate first shots. **Start from** copies any built-in pattern to edit.
- **Test** plays the spray at the real fire rate.
- Custom patterns are saved to `%APPDATA%\CrosshairY\patterns.json`. They appear in every weapon picker under their game, and are included in backups.
- Code: `Recoil.Custom`, `Recoil.SaveCustom`, `src/UI/Dialogs/PatternEditor.cs`. Opened from Designer › Recoil Pattern and Keybinds › Recoil loadout.

## 4. Per-weapon scale in the loadout

**Goal:** the Vandal feels right but the Sheriff needs 0.8×.

- `RecoilSlot.Scale` (default 1.0) multiplies each tracker's own scale while that slot is selected. The crosshair's base scale is untouched (`recoilFactor` in `firingOptions`).
- Keybinds › Recoil loadout gets a scale box per row.

## 9. Hit marker & kill flash

**Goal:** a quick on-screen reaction around the crosshair.

- An overlay can't see hits, so reactions are triggered by input:
  - **Hit marker**: on every shot (Fire key), or on its own keybind.
  - **Kill flash**: on its own keybind (e.g. a mouse side button).
- Drawn on a separate reaction layer on the overlay's render thread, so it fades out smoothly and never restarts the crosshair's own animations.
- Settings › Reactions: style (X / ring / brackets), colors, size, duration.
- Code: `OverlayController.React`, `Reactions.Build`, profile keys `HitKey`/`HitPad`/`KillKey`/`KillPad`.

## 10. Per-weapon crosshairs

**Goal:** a dot for the Operator, a plus for rifles.

- `RecoilSlot.CrosshairId`: selecting the slot switches to that crosshair first, then applies the weapon (if the crosshair has a recoil tracker).
- The loadout row shows the crosshair and a picker. A "Recoil off" slot can still switch crosshairs, e.g. knife → small dot.

## 11. Community gallery

**Goal:** share designs and find other people's.

- No server: the gallery is `gallery/gallery.json` in this repo, served from `raw.githubusercontent.com`.
- **Discover › Community** loads it and shows tiles with Save / Use.
- **Share yours** opens a prefilled GitHub issue (template `.github/ISSUE_TEMPLATE/gallery.yml`) with the crosshair code. Accepted submissions are appended to `gallery.json`.
- Later, if it grows: a GitHub Action that turns labeled issues into gallery entries automatically.

## 13. First-run tour

- Five coach marks over the real UI: Browse & Saved, the visibility switch, Designer, Keybinds & recoil loadout, the profile menu.
- Shown once (`Settings.TourDone`). Help › **Take the tour** replays it. Skippable at every step.
- Code: `src/UI/Tour.cs`.

## 14. Backup export / import

- Settings › Backup: **Export backup** writes one `.crosshairy` file (settings, profiles with keybinds and loadouts, library, custom patterns). **Import backup** restores it (replace everything) or merges the library and profiles.
- Profiles page: export / import a single profile.

## 15. Tray menu

- Show / hide crosshair, crosshairs (library, ✓ on the active one), weapon (loadout first, then the game's guns), profiles, open CrosshairY, check for updates, exit.

## 16. Localization

- `src/Core/Lang.cs`: `L.T("English text")` looks up the active language. Missing strings fall back to English, so translation can be partial.
- Languages: English, Español, Deutsch, Français, Português (Brasil).
- Covered first: navigation, page titles, settings, keybinds, tray, tour, updater, dialogs. Settings › General › **Language** (applies after restart).
- Adding a language means adding one dictionary. Contributors can send translations as a PR.

---

## Later (not in 1.3)

- More games: Overwatch 2, Rainbow Six Siege, PUBG, Call of Duty, Escape from Tarkov, The Finals, Marvel Rivals.
- Calibration helper (resolution + FOV + sensitivity → recoil scale).
- Spray preview ghost while holding a key.
- Burst / semi-auto reset modes.
- Animated GIF/APNG image layers.
- GitHub Actions build + test suite.
