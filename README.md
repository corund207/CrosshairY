<p align="center">
  <img src="docs/images/banner.png" alt="CrosshairY — custom crosshair overlay for any PC game" width="100%">
</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-1c1c1c?style=flat-square&labelColor=141414">
  <img alt="Runtime" src="https://img.shields.io/badge/.NET%20Framework-4.8-ef9408?style=flat-square&labelColor=141414">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-ef9408?style=flat-square&labelColor=141414">
</p>

**CrosshairY** is a native Windows crosshair overlay. It draws a pixel-perfect crosshair on top of any game, ships a full layered designer with animations, and imports crosshair codes from **Crosshair X**, **VALORANT** and **Counter-Strike 2**. It runs on the .NET Framework that already comes with Windows 10 and 11 — one small `.exe`, nothing to install.

<p align="center">
  <img src="docs/images/saved.png" alt="Saved crosshairs" width="49%">
  <img src="docs/images/designer.png" alt="Designer" width="49%">
</p>

## Features

- **Overlay** — transparent, click-through, always-on-top window that never steals focus. Drawn on the exact center pixel of your monitor in physical pixels; static crosshairs use no CPU, animations redraw at up to 240 FPS.
- **Crosshair X compatible** — renders the Crosshair X design format pixel-for-pixel (lines in every shape, dots, outlines, images, GIFs, shapes, text, drawings and multi-stage animations).
- **Import anything** — Crosshair X share codes and links, VALORANT profile codes, CS2/CS:GO share codes, CrosshairY codes and raw JSON.
- **Designer** — layers panel, tool dock, zoomable pixel canvas with drag-to-move, collapsible inspector, undo/redo and live on-screen preview.
- **Animations** — fire, aim or autoplay triggers; single press or hold; reset / reverse / pause on release; stages, easing, loop and alternate.
- **Recoil tracking** — crosshairs that follow a weapon's spray while you hold fire, with a weapon picker covering every gun in VALORANT, CS2, Rust and Apex Legends (see below).
- **Library** — Discover, Browse (50+ built-in designs) and Saved tabs, categories, favorites, search and sorting.
- **Profiles** — per-game crosshair, keybinds, position and size, switched automatically when a linked game is focused.
- **Keybinds** — global toggle, aim and fire keys, hide/show/swap while aiming, reload, next/previous crosshair or weapon, position nudging and crosshair shortcuts — keyboard, mouse or controller (XInput).
- **Display** — monitor selection, offsets and saved positions, size and opacity, only-show-in-game, hide from recordings, Fullscreen Assist mode and a Force Borderless tool.
- **Randomizer**, PNG/JSON export, backup and restore, tray mode and launch on startup.

<p align="center">
  <img src="docs/images/browse.png" alt="Browse" width="49%">
  <img src="docs/images/keybinds.png" alt="Keybinds" width="49%">
</p>

## Importing crosshair codes

Press **Import** (or `Ctrl+I`) and paste any of these:

| Code | Example |
| --- | --- |
| Crosshair X share code or link | `xe4lbu6zh8` · `crosshairx.gg/s/xe4lbu6zh8` |
| VALORANT crosshair profile | `0;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0` |
| CS2 / CS:GO share code | `CSGO-xxxxx-xxxxx-xxxxx-xxxxx-xxxxx` or `CS…` |
| CrosshairY code | `CXY1-…` |
| Crosshair JSON | `[{"type":"model", …}]` |

Crosshair X codes are short IDs; CrosshairY fetches the design from the same public share service that crosshairx.gg share pages use. CrosshairY shares its own `CXY1-` codes, which contain the whole design and work offline.

## Recoil tracking

<img src="docs/images/recoil.png" alt="Recoil pattern settings" width="100%">

Recoil crosshairs are timed animations: while you hold **Fire**, a tracker (the top arm of a plus, a center dot or a chevron) walks along a weapon's spray pattern, then snaps back when you let go. Every recoil crosshair has a **weapon picker** — Game › Category › Weapon — in the Browse preview bar, the Saved card menu and the Designer, and you can bind **Next / Previous Weapon** to switch guns in game (the weapon name flashes under the crosshair).

- **91 weapons**: VALORANT (18), Counter-Strike 2 (34), Rust (15) and Apex Legends (24).
- **Accurate first shots** per weapon — the tracker stays centered for the bullets that land dead center before recoil kicks in.
- Fire rate, scale and accurate shots can be tuned per crosshair.

> Patterns are approximations, not data extracted from the games. The most popular rifles (★) are hand-tuned; the rest are generated from each gun's fire rate, magazine and recoil traits. Adjust **Scale** for your resolution and field of view. CrosshairY never reads or modifies game memory.

## Fullscreen

CrosshairY is a regular overlay window, so it shows over **borderless** and **windowed** games, and over most **DX9 / DX12 "fullscreen"** games thanks to Windows fullscreen optimizations. **Fullscreen Assist** mode re-raises the crosshair the instant a game takes focus. Games running in true exclusive fullscreen can't be drawn over by any overlay window — switch them to borderless, or use **Settings › Display › Force Borderless Fullscreen**.

## Build from source

Requirements: Windows 10/11 and PowerShell. No Visual Studio or .NET SDK needed.

```powershell
git clone https://github.com/corund207/CrosshairY.git
cd CrosshairY
.\build.ps1          # add -Run to launch after building
```

The first build downloads the Roslyn C# compiler (`Microsoft.Net.Compilers.Toolset`) from NuGet into `.tools/`. The app is written to `bin\CrosshairY.exe`.

## Usage notes

- The default toggle key is **Shift + Alt + Z**; change it on the **Keybinds** page.
- Closing the window keeps CrosshairY in the system tray; right-click the tray icon to exit.
- If a game runs as administrator, run CrosshairY as administrator too so its keybinds work in that game.
- Your crosshairs, profiles and settings live in `%APPDATA%\CrosshairY`.

## Project layout

```
src/
  Core/      settings, profiles, library, presets, recoil patterns, JSON
  Render/    crosshair renderer, animation engine, blur, image cache
  Import/    Crosshair X / VALORANT / CS2 / CrosshairY code importers
  Overlay/   layered overlay window and render thread
  Input/     global keyboard, mouse and controller input
  UI/        main window, pages, dialogs and custom-drawn controls
  Assets/    bundled Noto Sans font
tools/       icon and banner generators
build.ps1    build script
```

## Credits

- Typeface: [Noto Sans](https://github.com/notofonts/latin-greek-cyrillic), licensed under the SIL Open Font License 1.1 (`src/Assets/Fonts/OFL.txt`).
- Icons: Segoe Fluent Icons / Segoe MDL2 Assets, provided by Windows.

CrosshairY is an independent project. It is not affiliated with, endorsed by or sponsored by CenterPoint Gaming, Riot Games, Valve, Facepunch Studios or Electronic Arts. Crosshair X, VALORANT, Counter-Strike, Rust and Apex Legends are trademarks of their respective owners.

## License

[MIT](LICENSE) © 2026 Jonah
