---
name: Reticly website
surface: site/
world: Apple-style product page (user-pinned, 2026-10-03)
tokens:
  color:
    black: "#000000"
    ink: "#f5f5f7"
    ink-2: "#a1a1a6"
    ink-3: "#6e6e73"
    light: "#f5f5f7"
    light-ink: "#1d1d1f"
    light-ink-2: "#6e6e73"
    tile: "#ffffff"
    tile-dark: "#101012"
    amber: "#f5a524"
    amber-hi: "#ffb94a"
    amber-ink: "#1a1204"
    amber-on-light: "#a35a00"
    cyan: "#45c4d6"
    red: "#ff5a4f"
  type:
    family: "Inter Variable (self-hosted via @fontsource-variable/inter)"
    hero: "700, clamp(64px, 11vw, 148px), -0.045em, line-height 1"
    headline: "700, clamp(36px, 5.4vw, 64px), -0.038em, line-height 1.06"
    tile-title: "700, 26px, -0.03em"
    body: "17px, line-height 1.47, -0.012em"
    nav: "12.5px"
  radius:
    pill: "980px"
    tile: "28px"
    window: "22px"
    image: "14–20px"
  motion:
    ease: "cubic-bezier(0.22, 1, 0.36, 1)"
    reveal: "opacity + 40px rise, 1s"
---

# Reticly website design system

An Apple-style product page, chosen by the user ("make the website be an Apple style website"). It replaces the earlier engineering-drawing world. It borrows Apple's page grammar only: no Apple logos, product imagery, device renders or copy. The product's own amber is the single accent.

## Page grammar

- **Sticky translucent nav**: 48px, `rgba(22,22,23,.86)` with saturate and blur, wordmark left ("Reticl" plus an amber "y"), small grey links centred, small amber pill on the right.
- **Pinned scroll sections** (`.scrolly` > `.sticky`): the section is taller than the viewport, and the sticky child stays in view while scroll progress `--p` (0–1, set by `site.js`) drives the animation.
  - **Hero (210vh)**: giant wordmark, two-line subhead and CTAs; the amber reticle scales, rotates 45° and blooms its arms as you scroll, and the copy lifts away.
  - **Statement (240vh)**: one huge sentence whose words light up as you scroll; key words light in amber.
  - **Recoil (460vh)**: the Vandal's real spray is scrubbed by scroll. Red hits appear, the tracker walks the pattern, and a round counter and four captions advance. At the end the tracker snaps back.
  - **Window (170vh)**: the Designer screenshot rises and scales from 0.8 to 1 in a rounded window with a soft shadow.
- **Alternating grounds**: black for drama (hero, recoil, try it, tour, numbers, final), light `#f5f5f7` for explanation (design, features, gallery, install, footer).
- **Two-tone headlines**: a bold statement followed by a dimmed continuation in the same line (`.headline .dim`). No eyebrows above headings.
- **Bento grid**: 6 columns, tiles with 28px radius, white on light, with one or two dark tiles for contrast. Mixed spans (4×2 hero tile, tall tile, halves) so it never reads as a row of equal cards.
- **Carousel**: horizontally scrolling screenshots with snap points and round prev/next buttons.
- **Big numbers**: count-up figures (91 patterns, 100+ designs, 0 accounts).
- **Install**: three numbered step tiles plus a chevron FAQ (`details`).
- **Final call**: mark, "Get Reticly.", a large amber pill, version and size.
- **Footer**: light grey, three link columns, one legal line.

## Rules

- Amber is the only accent: buttons, the reticle, highlighted words. On light grounds, links use `amber-on-light` for contrast.
- Pill buttons only (980px radius). Primary is amber with dark text; secondary is a text link with a "›".
- Gradient text is used once: the hero wordmark (white to light grey).
- Shadows are soft and offset (windows and screenshots only), never coloured glows.
- Motion: one scroll-driven story per pinned section, plus a reveal-on-view rise elsewhere. Under `prefers-reduced-motion`, pinned sections collapse to static and everything is visible immediately.
- The interactive demo (#try) keeps the data colours: cyan for the pattern and the climb dimension, red for hits, amber for the tracker.

## Breakpoints

- ≤900px: nav links hidden, recoil stacks the plot over the captions, bento drops to 2 columns, steps and numbers stack.
- ≤600px: bento drops to 1 column, and plot labels are enlarged in SVG units to stay at 11px or more.
