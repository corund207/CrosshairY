---
name: Reticly
description: A free crosshair overlay for Windows, presented as the crosshair's own engineering drawing set.
colors:
  amber: "#f5a524"
  amber-hi: "#ffbb47"
  amber-ink: "#1b1305"
  cyan: "#45c4d6"
  red: "#e5534b"
  plot: "#0e1012"
  ink: "#e9e6df"
  ink-2: "#b3b6b9"
  ink-3: "#7c8389"
  rule: "#343a40"
  rule-strong: "#565d64"
  grid-minor: "rgba(233, 230, 223, 0.035)"
  grid-major: "rgba(233, 230, 223, 0.07)"
typography:
  display:
    fontFamily: "B612, system-ui, sans-serif"
    fontSize: "clamp(38px, 4.1vw, 62px)"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "-0.025em"
  headline:
    fontFamily: "B612, system-ui, sans-serif"
    fontSize: "clamp(30px, 3.6vw, 52px)"
    fontWeight: 700
    lineHeight: 1.05
    letterSpacing: "-0.01em"
  title:
    fontFamily: "B612, system-ui, sans-serif"
    fontSize: "15px"
    fontWeight: 700
    letterSpacing: "0.06em"
  body:
    fontFamily: "B612, system-ui, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.55
    fontFeature: "\"tnum\""
  button:
    fontFamily: "B612, system-ui, sans-serif"
    fontSize: "14px"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "0.08em"
  label:
    fontFamily: "B612 Mono, ui-monospace, monospace"
    fontSize: "11px"
    fontWeight: 400
    letterSpacing: "0.06em"
  data:
    fontFamily: "B612 Mono, ui-monospace, monospace"
    fontSize: "13px"
    fontWeight: 400
    letterSpacing: "0"
rounded:
  none: "0"
  balloon: "50%"
spacing:
  grid-minor: "8px"
  frame-inset: "10px"
  sheet-pad: "22px"
  sheet-gap: "40px"
  grid-major: "40px"
  gutter: "clamp(16px, 3vw, 40px)"
  sheet-max: "1440px"
components:
  button-primary:
    backgroundColor: "{colors.amber}"
    textColor: "{colors.amber-ink}"
    typography: "{typography.button}"
    rounded: "{rounded.none}"
    padding: "0 16px"
    height: "40px"
  button-primary-hover:
    backgroundColor: "{colors.amber-hi}"
    textColor: "{colors.amber-ink}"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.button}"
    rounded: "{rounded.none}"
    padding: "0 16px"
    height: "40px"
  sheet-tab:
    backgroundColor: "{colors.plot}"
    textColor: "{colors.ink-2}"
    rounded: "{rounded.none}"
    padding: "0 14px"
    height: "56px"
  sheet-tab-active:
    textColor: "{colors.ink}"
  title-block-download:
    backgroundColor: "{colors.amber}"
    textColor: "{colors.amber-ink}"
    rounded: "{rounded.none}"
    padding: "18px"
  segment-toggle:
    backgroundColor: "transparent"
    textColor: "{colors.ink-2}"
    rounded: "{rounded.none}"
    padding: "12px 6px"
  segment-toggle-on:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.plot}"
  choice-chip:
    backgroundColor: "transparent"
    textColor: "{colors.ink-2}"
    rounded: "{rounded.none}"
    padding: "8px 10px"
  choice-chip-on:
    textColor: "{colors.amber}"
  balloon:
    backgroundColor: "{colors.plot}"
    textColor: "{colors.amber}"
    rounded: "{rounded.balloon}"
    size: "28px"
---

# Design System: Reticly

## Overview

**Creative North Star: "The Reticle Spec Sheet"**

The website is the crosshair's engineering drawing: a numbered set of dark drawing sheets in which every part is measured, called out and signed. Each section is a sheet with a double drawing frame, zone markers along its border, a sheet tag in its corner, and drafting furniture (title block, parts list, revision table, balloons with leader lines, dimension lines) doing the work that cards and feature grids do elsewhere. The whole page sits on a dark plot ground ruled with a faint 8 px / 40 px CAD grid.

Color is assigned the way a CAD program assigns it, by layer: amber is the reticle and every action, cyan is dimensions and measurement, red is spray paths and centerlines, off-white is ink linework. Depth comes only from line layers overlapping on the flat plot. Density is deliberately uneven: crowded tabular blocks (the title block, the coordinates table, the parts list) sit against charged empty drawing field around the reticle.

Motion belongs to the drawing. Dimension lines draw in once and labels fade in after them; the recoil plot fires only while held; everything else is still, and nothing moves under reduced motion. On fine pointers the cursor becomes a full-screen CAD crosshair with a live coordinate readout.

The site borrows two things from the native Windows app, whose own UI is outside this system: the amber accent and the ring-with-four-ticks logo mark, with the final "Y" of the wordmark set in amber.

**Key Characteristics:**
- Dark plot ground with a two-weight drafting grid; 1 px hairline rules everywhere.
- Color by drawing layer: amber reticle/actions, cyan dimensions, red spray and centerlines.
- Every section is a numbered sheet in a double frame with zone markers and a sheet tag.
- B612 for lettering, B612 Mono for every number, code and table cell; tabular figures throughout.
- Square corners; circles only for balloons, view letters and plotted points.
- Flat: no elevation shadows, no glow, no colored gradients.

## Colors

A near-black drafting plot with warm off-white ink, three saturated layer colors, and nothing in between.

### Primary
- **Reticle Amber** (amber): the reticle itself, every download and primary action, balloons and leader lines, active sheet-tab underline and number, selected weapon chip, quantities in the parts list, revision numbers, step counters, text selection, and the focus ring.
- **Lit Amber** (amber-hi): hover state of amber fills and of text links.
- **Amber Ink** (amber-ink): text and icons on amber fills only.

### Secondary
- **Dimension Cyan** (cyan): dimension lines, arrowheads and their figures; the plotted spray path and points; accurate-shot rows in the coordinates table; registration crosses on screenshot frames.

### Tertiary
- **Centerline Red** (red): dash-dot centerlines and axes (pattern 22 5 3 5), plotted hits and their numbers, the currently firing row in the coordinates table.

### Neutral
- **Plot** (plot): the page ground, sticky header ground, sticky table headers, balloon fill, and text on ink-filled toggles.
- **Ink** (ink): headings, strong text, the logo ring, the linework of the reticle outline, the CAD cursor box.
- **Ink 2** (ink-2): body copy, inactive tabs and toggles, table cells, drawing annotations.
- **Ink 3** (ink-3): field keys in the title block, table headers, zone markers, captions and the footer. The lowest text tone; do not go dimmer for text.
- **Rule** (rule): inner frame, cell dividers, list separators.
- **Rule Strong** (rule-strong): outer sheet frame, table and title-block outer borders, ghost-button border, header bottom rule, scrollbar thumb.
- **Grid Minor / Grid Major** (grid-minor, grid-major): the 8 px and 40 px drafting grid on the page ground. Background only.

### Named Rules
**The Layer Rule.** A color means a drawing layer, not a mood. Amber is the reticle and what you can act on; cyan is measured; red is the spray and the centerline. Never use cyan or red for decoration, and never use amber for something that is neither the crosshair nor an action.

**The One Amber Fill Rule.** Solid amber fills are reserved for download actions (header button, title-block cell, closing button). Everything else amber is a hairline, a figure or a 1.5 px balloon stroke.

## Typography

**Display Font:** B612 (with system-ui, sans-serif)
**Body Font:** B612 (with system-ui, sans-serif)
**Label/Mono Font:** B612 Mono (with ui-monospace, monospace)

**Character:** B612 was drawn for aircraft cockpit displays, so it reads as instrument lettering rather than marketing type; its mono sibling carries every figure, so numbers line up like a drawing's annotations. Self-hosted via Fontsource at weights 400 and 700.

### Hierarchy
- **Display** (700, clamp(38px, 4.1vw, 62px), 1): the single sheet-01 headline. Tight negative tracking, balanced wrap.
- **Headline** (700, clamp(30px, 3.6vw, 52px), 1.05): one per sheet, a short declarative sentence ending in a period.
- **Title** (700, 15px, uppercase, 0.06em): callout and note headings; drops to 13px inside numbered lists and figure captions.
- **Body** (400, 16px, 1.55): paragraphs capped at 64ch (lede 44ch, 16-18px). Tabular figures on by default.
- **Button** (700, 14px, uppercase, 0.08em): buttons; sheet tabs use 13px at 0.06em.
- **Label** (B612 Mono 400, 10-12px, uppercase, 0.05-0.08em): title-block keys, table headers, sheet tags, zone markers, plot hint and readout.
- **Data** (B612 Mono 400, 11-15px, no tracking, sentence case): values in the title block, spec strip and tables; SHA-256 and code.

### Named Rules
**The Figures Are Mono Rule.** Any number a reader might compare (versions, sizes, hashes, coordinates, item numbers, step counters, zone markers) is set in B612 Mono. Prose numbers inside sentences stay in B612 with tabular figures.

**The Uppercase Is Drafting Lettering Rule.** Uppercase with positive tracking belongs to labels, tabs, buttons and callout titles, the lettering of a drawing. Headlines and body are never uppercase.

## Layout

The page is a vertical stack of sheets, each up to 1440px wide, centered, separated by 40px (24px between header and sheet 01; 24px on phones), with a fluid side gutter of clamp(16px, 3vw, 40px). The header strip is sticky at 56px tall; anchor scrolling is padded 64px so sheets land below it, and keys 1-6 jump to sheets.

Each sheet has 22px padding (14px on phones), an inner frame inset 10px (6px on phones), and an inner body padded clamp(18px, 3vw, 40px). Headings inside sheets reserve 220px on the right so the corner sheet tag never collides; below 1100px that reserve becomes 34px of top padding instead.

Interiors are asymmetric two-column grids rather than equal columns: about 1.75 : 1 on the arrangement sheet (drawing left, title and title block right), 1.25 : 0.75 on the recoil sheet (square plot left, controls and coordinates right), a 6-column grid on the detail sheet where view A spans 4 x 2. Column gaps run clamp(20px, 3vw, 44-48px). Tabular regions (title block, spec strip, parts list, gallery) butt their cells together with shared hairlines and zero gap.

Breakpoints: at 1100px every interior collapses to one column and the drawing's text annotations hide in favor of the visible numbered list; at 720px zone markers hide, tab labels drop to numbers only, the title block goes to two columns, and on sheet 01 the title block (download) moves above the drawing, which is cropped to the reticle at 150% width so its figures stay readable. A short-desktop query (min-width 1101px and max-height 860px) tightens sheet 01 so the download stays in the first viewport.

## Elevation & Depth

The system is flat. There are no elevation shadows, no glow and no colored gradients. Depth comes only from line layers overlapping on the plot: the faint grid under the sheet, the outer and inner frames, the dashed overlay-window boundary, the ink outline around the amber reticle, cyan dimensions and red centerlines crossing it. Sticky elements (header, table heads) separate from content with a solid plot fill and a hairline, never a shadow.

`linear-gradient` appears in the build only as a technique to draw 1px lines (the drafting grid and the registration crosses); that is native to the world and is not a gradient fill.

### Named Rules
**The Line Layers Rule.** To bring something forward, put a line around it or a layer over it. If a surface seems to need a shadow, it needs a frame.

## Shapes

Corners are square everywhere: sheets, frames, buttons, chips, toggles, table cells, images. Borders are 1px hairlines in two weights (rule inside, rule-strong outside); balloon and letter rings are 1.5px. Circles are reserved for drawing symbols: numbered amber balloons (28-36px), lettered view rings in ink (30px), leader-line terminal dots, plotted shot points and the recoil tracker. Dashes carry meaning: dash-dot 22 5 3 5 for centerlines, 6 6 for the overlay window boundary, 4 4 for the spray path, a dashed rule-strong border for a gallery preview that has not been published.

Sheets carry a drawing frame: outer rule-strong border, inner rule border inset 10px, zone numbers 1-6 with ticks along the top (stopping short of the sheet tag) and zone letters A-D down the left. Screenshot frames carry 14px cyan registration crosses at the top-left and bottom-right corners.

## Components

### Buttons
Square, lettered, and short; they read as controls on an instrument.
- **Shape:** square corners (0), 40px tall, 16px side padding, optional 16px stroked SVG icon with a 10px gap.
- **Primary:** amber fill and border with amber-ink lettering; used only for downloading.
- **Hover / Focus:** primary lightens to amber-hi; ghost border shifts from rule-strong to ink. Focus everywhere is a 2px amber outline offset 3px. No transitions.
- **Ghost:** transparent with a rule-strong hairline and ink lettering, for secondary links such as GitHub submissions.

### Title Block
The sheet-01 signature: a crowded drafting title block, four columns of hairline-divided cells, each a mono key (ink-3, 10px, uppercase) over a value (ink, 13px). Cells for title, drawn by, license, rev, date, size, platform and the full SHA-256. Its largest cell is the full-width amber download, 18px padding, big uppercase B612 (clamp(20px, 2vw, 26px)) with a mono sub-line and a 34px stroked download arrow. Release values are live from GitHub.

### Navigation
The header is the sheet's border strip: logo mark and wordmark, then sheet tabs as hairline-separated cells the full height of the strip, each a mono number (ink-3) and an uppercase label (ink-2). Hover lifts the label to ink; the current sheet gets ink lettering, an amber number and a 2px amber underline drawn inside the bottom edge. Below 720px tabs show numbers only and the download button drops its label.

### Sheet Tag
A two-cell mono label in each sheet's top-right corner, inside the inner frame ("Sheet 03 of 06", plus scale or sheet title), bordered left and bottom with rule hairlines. It is drawing furniture that numbers the sheet, not a label placed over the headline.

### Callout Balloons
Numbered amber rings (plot fill, 1.5px amber stroke, bold amber numeral) joined to the drawing by 1px amber leader lines ending in a 3px dot. On wide screens the balloons carry short notes in the drawing; below 1100px the same callouts render as a list with ring numbers, title and one sentence each, divided by rule hairlines.

### Dimension Lines
Cyan 1px extension and dimension lines with filled cyan arrowheads and cyan mono figures. On load each line draws in once (900ms, cubic-bezier(.16, 1, .3, 1), staggered), labels fade in after (500ms ease-out). Disabled under reduced motion and before JS.

### Toggles and Chips
- **Segmented toggle (game):** a four-cell row inside a rule-strong border, cells divided by rule hairlines, 12px uppercase bold lettering in ink-2; the pressed cell inverts to an ink fill with plot lettering.
- **Choice chip (weapon):** transparent, rule hairline, 13px ink-2; hover raises border to ink-3 and text to ink; selected switches border and text to amber. A small amber diamond marks hand-tuned patterns.

### Tables
Parts list, revision table and coordinates table share one grammar: rule-strong outer border, rule hairline cells, mono uppercase headers (10px, ink-3, 0.06-0.08em), ink-2 cells. Item numbers and dates in mono; quantities and revision numbers in amber mono; part names in bold ink. Coordinates are right-aligned mono with a sticky plot-filled header; accurate rows are cyan and the firing row inverts to a red fill.

### Recoil Plot (signature)
A square, rule-strong framed plot with a crosshair cursor. Holding mouse, finger or space fires the selected weapon's real spray: a dashed cyan path, cyan points with ink-3 numbers, red hits with red numbers, a red dash-dot axis, a cyan dimension and an ink-2 scale bar. A mono readout sits top-left; a centered mono uppercase hint (amber "Hold") sits at the bottom and disappears while firing, moving below the frame on phones.

### Detail Views
Screenshot figures in a rule-strong frame with 8px padding, a rule border on the image itself and cyan registration crosses at two corners; captions lead with a 30px ink letter ring (A-F), then a title and one sentence.

### CAD Cursor
On fine-pointer devices a fixed full-screen crosshair (1px lines in ink at 22% opacity), a 10px ink box at the pointer and a mono coordinate readout in a plot-filled rule box. It dims over interactive elements, which keep a pointer cursor.

## Do's and Don'ts

### Do:
- **Do** build every new section as a numbered sheet: double frame (rule-strong outer, rule inner inset 10px), zone markers, and a sheet tag in the top-right corner.
- **Do** assign color by layer: amber for the reticle and actions, cyan for dimensions and measurements, red for spray paths, hits and centerlines.
- **Do** set every comparable figure in B612 Mono, and keep labels uppercase with 0.05-0.08em tracking.
- **Do** present facts as drafting furniture (title block, parts list, revision table, balloons, dimension lines) with hairline cells and zero gap.
- **Do** leave charged empty field around the drawing and crowd the tabular blocks.
- **Do** keep motion to dimension lines drawing in once and labels fading in, and remove it under reduced motion.
- **Do** show the 2px amber outline offset 3px on every focusable element.

### Don't:
- **Don't** add drop shadows, glow, blur or colored gradient fills; separate layers with lines and plot-filled grounds.
- **Don't** round corners; circles are only for balloons, view letters, leader terminals and plotted points.
- **Don't** fill anything solid amber except a download action.
- **Don't** use cyan or red as decorative accents outside their measurement and spray roles.
- **Don't** place small uppercase labels above headlines; the sheet tag in the frame corner is the only sheet label.
- **Don't** set text dimmer than ink-3 on the plot.
- **Don't** introduce colors outside this palette for grounds or fills.
