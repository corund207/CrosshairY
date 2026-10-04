# Product

<!-- impeccable:product-schema 1 -->

## Platform

web (the website). The product itself is a native Windows desktop app.

## Stack

Website: Astro (static output) in `site/`, deployed to Vercel. The user asked for a framework and named Astro or Next; Astro was chosen because the site is static content with a little client-side fetching. The app is C# / WinForms, .NET Framework 4.8, built with `build.ps1`.

## Users

A small circle of friends and people the maintainer (Jonah, GitHub `corund207`) sends the link to: PC gamers who want a custom on-screen crosshair, mainly for VALORANT, Counter-Strike 2, Rust and Apex Legends. They arrive from a shared link, want to understand quickly what it is, trust that it's safe, and download it. The site also serves as the project homepage when applying for code-signing programs.

## Product Purpose

CrosshairY draws a custom crosshair over any game: a transparent, click-through, always-on-top overlay on the exact center pixel. It's free, open source (MIT) and made by one person. Success: a visitor downloads the exe, gets past the unsigned-app warning without worry, and has a crosshair on screen within a minute.

## Positioning

A free, open-source crosshair overlay with recoil-tracking crosshairs: a tracker follows each weapon's spray pattern while you hold fire. It has 91 weapon patterns across four games, a per-gun loadout bound to your weapon keys, and a spray pattern editor. It's a native Windows app with no installer and no account, and it never reads or touches game memory.

## Operating Context

- Download is a single `CrosshairY.exe` from GitHub Releases (latest: `https://github.com/corund207/CrosshairY/releases/latest`). No installer; needs Windows 10/11 (.NET Framework 4.8 is built in).
- The exe isn't code-signed: SmartScreen shows "Windows protected your PC" → **More info › Run anyway**. PCs with Smart App Control on may block it outright; the alternative is building from source (`.\build.ps1`).
- Works over borderless/windowed games and most "fullscreen" games; true exclusive fullscreen can't be drawn over by any overlay (Force Borderless helps).
- From 1.3.0 the app updates itself from GitHub Releases.
- Community gallery: `gallery/gallery.json` in the repo, with preview images in `gallery/previews/`; submissions are GitHub issues, reviewed by a workflow and approved by the maintainer.

## Capabilities and Constraints

- Overlay: click-through, topmost, physical-pixel centering, a still crosshair idles at near-zero CPU (the render thread sleeps until something changes), per-monitor, offsets, opacity, hide from recordings, only-in-game, Fullscreen Assist, Force Borderless.
- Designer: layers (lines, dots, shapes, text, images, drawings), multi-stage fire/aim/autoplay animations, undo/redo, live preview.
- Library: 100+ built-in designs including 46 recoil crosshairs; Saved, categories, favorites; randomizer; community gallery.
- Recoil: 91 weapons (VALORANT 18, CS2 34, Rust 15, Apex 24); per-weapon accurate first shots; loadout keys with per-gun scale and crosshair; spray pattern editor for custom or corrected patterns. Patterns are approximations.
- Imports VALORANT and CS2 crosshair codes, CrosshairY codes and JSON.
- Hit marker / kill flash reactions (key-triggered; an overlay can't detect real hits).
- Profiles per game with automatic switching; keyboard, mouse and XInput controller keybinds; tray menu; backup/restore; English, Spanish, German, French and Portuguese (BR).
- Screenshots in `docs/images/` (banner, saved, browse, discover, designer, recoil, keybinds, loadout, settings, randomizer).

## Brand Commitments

- Name **CrosshairY**, written with the final **Y** in the accent orange (`#F59E0B`-family amber, as in the app's logo). The app mark is a ring with four ticks and a center dot.
- The app's UI is dark, with amber accents and Noto Sans.
- **Do not mention Crosshair X** (or any competitor) anywhere on the site.
- Honest copy: say the patterns are approximate, say the exe is unsigned and how to run it, and never imply anti-cheat approval.

## Evidence on Hand

- Real screenshots: `docs/images/*.png`; banner `docs/images/banner.png`.
- Releases 1.0.0, 1.1.0, 1.2.0 on GitHub with SHA-256 per release; MIT license; public source.
- Community gallery entries with previews.
- No testimonials, user counts, reviews, press or endorsements exist. Don't invent any.

## Product Principles

1. Show the real thing: screenshots and actual crosshairs, not mockups.
2. Be straight about trust: open source, unsigned (and how to run it), no memory access, approximate patterns.
3. Get from link to crosshair-on-screen in under a minute.
4. Small and personal: one maintainer, made for friends; no growth-hacking.
