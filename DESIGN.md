---
name: AFK4.NET public site
description: Linear-style dark product site for PC club owners; the real Panel is the hero, emerald is the brand, amber means urgency only.
colors:
  canvas: "#121212"
  surface-elevated: "#1e1e1e"
  surface-sunken: "#161616"
  surface-accent-soft: "#143830"
  border-soft: "#2a2a2a"
  border-default: "#3a3a3a"
  border-strong: "#555555"
  border-control: "#6e6e6e"
  border-accent: "#2f6b58"
  hairline: "rgba(255, 255, 255, 0.08)"
  ink: "rgba(255, 255, 255, 0.92)"
  ink-2: "rgba(255, 255, 255, 0.70)"
  ink-3: "rgba(255, 255, 255, 0.55)"
  accent: "#2cc592"
  accent-hover: "#3dd9a3"
  accent-text: "#4dd6a6"
  accent-soft: "#143830"
  accent-on-soft: "#c2f0df"
  accent-glow: "rgba(44, 197, 146, 0.30)"
  text-on-accent: "#0a1a14"
  focus-outline: "#4dd6a6"
  warning: "#fbbf24"
  warning-text: "#fcd34d"
  warning-soft-bg: "#2a1f0a"
  warning-soft-border: "#5a3f18"
  danger-text: "#fca5a5"
typography:
  display:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Roboto, system-ui, sans-serif"
    fontSize: "clamp(2.4rem, 6.2vw, 5rem)"
    fontWeight: 650
    lineHeight: 1.04
    letterSpacing: "-0.04em"
  headline:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Roboto, system-ui, sans-serif"
    fontSize: "clamp(1.8rem, 3.8vw, 3rem)"
    fontWeight: 650
    lineHeight: 1.1
    letterSpacing: "-0.035em"
  title:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Roboto, system-ui, sans-serif"
    fontSize: "1.15rem"
    fontWeight: 650
    lineHeight: 1.3
    letterSpacing: "-0.01em"
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Roboto, system-ui, sans-serif"
    fontSize: "17px"
    fontWeight: 400
    lineHeight: 1.6
  lead:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Roboto, system-ui, sans-serif"
    fontSize: "clamp(1.05rem, 1.6vw, 1.25rem)"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Roboto, system-ui, sans-serif"
    fontSize: "0.97rem"
    fontWeight: 600
    lineHeight: 1.2
rounded:
  xs: "4px"
  sm: "6px"
  md: "8px"
  lg: "12px"
  pill: "999px"
spacing:
  "1": "4px"
  "2": "8px"
  "3": "12px"
  "4": "16px"
  "5": "20px"
  "6": "24px"
  "8": "32px"
  "12": "48px"
  "16": "64px"
  "24": "96px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.text-on-accent}"
    rounded: "{rounded.md}"
    height: "48px"
    padding: "0 24px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-ink:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.canvas}"
    rounded: "{rounded.md}"
    height: "48px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    height: "48px"
    padding: "0 24px"
  chip-have:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent-on-soft}"
    rounded: "{rounded.pill}"
  chip-planned:
    textColor: "{colors.ink-3}"
    rounded: "{rounded.pill}"
  card:
    backgroundColor: "rgba(255, 255, 255, 0.02)"
    rounded: "{rounded.lg}"
    padding: "24px"
  window:
    backgroundColor: "{colors.surface-elevated}"
    rounded: "{rounded.md}"
  input:
    backgroundColor: "{colors.surface-sunken}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    height: "48px"
    padding: "12px 16px"
  tab:
    textColor: "{colors.ink-2}"
    rounded: "{rounded.md}"
    height: "44px"
    padding: "0 20px"
---

# Design System: AFK4.NET public site

## Overview

**Creative North Star: "The Operator's Console, Lit From Behind"**

The site is the dark theme of the product's own design book (`packages/tokens/tokens.css`, copied to `dist/tokens.css`, `data-theme="dark"`), not a separate brand skin. A visitor who later opens the Panel sees the same canvas, the same emerald and the same radii. Layout follows Linear: big quiet headlines with a grey continuation of the sentence, thin hairlines instead of boxed cards, and the product as the hero. The first thing on screen is a real Panel screenshot in perspective, fading out at the bottom, over a hairline grid and an emerald glow that follows the pointer.

Everything shows a real thing a club owner knows: seats with timers, a receipt, a Telegram chat, a phone with the player app. Product frames are drawn as Windows 11 windows (icon and title left, minimise / maximise / close right, 8 px corners), because that is the club market's OS, not macOS.

Density is generous: 96 px between blocks, 17 px body. Motion is decorative and switchable. Savedata, 2g or reduced-motion visitors get the lite mode, where glow, perspective, reveal, tab pill and loops are turned off and the meaning stays intact.

**Key Characteristics:**
- Dark neutral canvas, one emerald accent, amber reserved for urgency.
- System font stack only, no web fonts, tight negative tracking on large sizes.
- Hairline (8% white) borders and 2% white fills rather than solid cards.
- Glow is the only decoration: emerald, radial, low opacity.
- Status chips carry honesty: filled for shipped, dashed for planned.

## Colors

Near-black neutrals with translucent white ink, one emerald, one amber.

### Primary
- **Club Emerald** (`accent`, #2cc592): brand, primary buttons (`btn-lamp`), progress bar, tab auto-play bar, step numerals, list markers, selected seat ring. Hover is the lighter `accent-hover`; text-sized emerald (links, figures) uses `accent-text` for contrast. Its soft fill `accent-soft` / `accent-on-soft` holds selected tiles, "have" chips and the command-palette highlight.
- **Emerald Glow** (`accent-glow`, 30% emerald): hero and closing radial light, hero-shot halo, dialog halo, pointer spotlight (14% emerald in the hero).

### Secondary
- **Urgency Amber** (`warning`, #fbbf24, with `warning-text` and the soft background/border pair): only things that are burning: a seat with under five minutes left (pulsing ring), the Telegram alert message, the offline state line. Never decoration, never a button.

### Neutral
- **Canvas Black** (`canvas`, #121212) page; **Elevated** (#1e1e1e) windows, dialogs, detail panel; **Sunken** (#161616) receipt, inputs, language rows.
- **Ink ladder** (92 / 70 / 55 % white): headings and body, secondary copy, notes and muted continuation.
- **Hairline** (8% white) is the default divider; `border-default` / `border-strong` outline windows and dashed "planned" shapes; `border-control` (#6e6e6e) outlines interactive controls.

### Named Rules
**The Amber Means Burning Rule.** Amber appears only for time under five minutes, a missing link, or the offline state. If nothing is on fire, the screen has no amber.
**The One Emerald Rule.** Emerald is brand and action. It fills at most one button per view; everything else is outline, ink or soft fill.

## Typography

**Display / Body Font:** Segoe UI Variable Text, Segoe UI, Roboto, system-ui (no web fonts; Tajik Cyrillic ғ ӣ қ ӯ ҳ ҷ must render).
**Mono:** not used on the site.

**Character:** the product's own system face, set large, bold-ish (650) and tightly tracked, so it reads as quiet confidence rather than marketing.

### Hierarchy
- **Display** (650, clamp 2.4-5rem, 1.04, -0.04em): hero h1, centred, max 18ch, balanced wrap.
- **Headline** (650, clamp 1.8-3rem, 1.1, -0.035em): `block-title`; the second half of the sentence goes in `muted` at weight 500. Page titles on inner pages clamp 2.2-4rem; closing title 2-3.6rem.
- **Title** (650, 1.15rem; 1.4-1.9rem inside scenes, 1.3rem in panels, 1.5rem in dialog): h3 and panel headings.
- **Body** (400, 17px, 1.6; 16px on phones): paragraphs capped at 66ch; `lead` 1.05-1.25rem in ink-2, 54ch.
- **Label** (600, 0.97rem buttons and tabs, 0.78rem chips, 0.9rem field labels, 0.8rem window title): sentence case, no uppercase, no extra tracking.
- **Figures** use `font-variant-numeric: tabular-nums` (seat timers, receipt, balance).

### Named Rules
**The Grey Continuation Rule.** Section headlines state the point in ink and continue in ink-3; no second typeface, no colour for emphasis.

## Layout

Single column, centred container `wrap` 1180 px max with 20 px side padding (16 px on phones); text pages use `narrow` 780 px. Blocks are separated by 96 px top padding (64 px on phones). Spacing scale is the token 4-px base (4, 8, 12, 16) extended locally with 20, 24, 32, 48, 64, 96. Two-column compositions (scene, guest-screen split) are 5fr / 7fr with a 48 px gap; the "Всё, что внутри" atlas is 7fr tiles (3 columns) / 5fr sticky detail panel; footer is 1.6fr brand + four link columns; inner pages use a 3fr/8fr doc + sticky toc.

Breakpoints: 1000 px (all grids collapse to one column, footer to two, toc becomes grid), 900 px (secondary nav items hide), 700 px (phone: full-width buttons, stacked tabs, dialog becomes bottom sheet, 2-column atlas), 360 px (tightest header). Sticky header 64 px with blurred translucent canvas; `scroll-padding-top` 96 px.

Sections in order: hero, tabbed scenes inside a window, guest screen split, three small cards (languages, game-cache diagram, player-app phone), atlas, steps, closing contact block, adult footer. Inner pages (download, code-signing, privacy) reuse `page-hero`, `card`, `panel`, `timeline` and `doc` + `toc`.

## Elevation & Depth

Hybrid: tonal layering first (canvas, elevated, sunken), shadows second, light third. Surfaces are flat translucent white fills on hairlines; only windows, dialogs and the palette lift.

### Shadow Vocabulary
- **Elevated** (`--shadow-elevated`, `0 1px 0 rgba(0,0,0,.35), 0 18px 40px rgba(0,0,0,.5)`): product windows, guest PC screen, detail panel, dialogs.
- **Hero halo** (`0 0 0 1px rgba(255,255,255,.04), 0 40px 120px accent-glow`): hero screenshot.
- **Dialog halo** (elevated + `0 0 120px accent-glow`): command palette and request dialog.
- **Press glow** (`--shadow-press`, `0 6px 14px accent-glow`): primary button hover.
- **Focus ring** (`0 0 0 3px accent-ring`) on inputs; outside inputs `:focus-visible` is a 3 px `focus-outline` outline with 3 px offset.

### Named Rules
**The Glow Is Light Rule.** Emerald glow is soft, radial and low-contrast, a light source behind the product. No hard offset shadows, no glow around text.
**The Lite Rule.** Every decorative effect (glow motion, perspective, reveal, spotlight, loops, pulsing) must have an `html.lite` off state.

## Shapes

Gently rounded, never sharp, never pill-shaped except status chips. 12 px (`lg`) for cards, dialogs, tiles, the palette and the detail panel; 8 px (`md`) for buttons, inputs, tabs, seats, rows and the product windows (overridden from 12 to 8 on purpose, Windows 11); 6 px (`sm`) for focus outline corners; 999 px only for chips; phone mock 36 px. "Planned" things use dashed borders (1.5 px) of `border-strong`; shipped things are solid. Windows' title-bar buttons are 46 px wide and flush with the bar edge (38 px on phones).

## Components

### Buttons
- **Shape:** 8 px, min height 48 px (40 px `btn-small`, 56 px `btn-big`), 600 weight, 24 px side padding, 1 px border.
- **Primary** (`btn-lamp`): emerald fill, dark text; hover lightens and adds press glow. **Ink** (`btn-ink`): near-white fill on dark, hover pure white. **Default / quiet:** transparent with `border-control` (quiet: hairline), hover 6% white fill.
- **Active:** scale 0.97. Disabled: 55% opacity. Phones: full width.

### Chips
- **"Есть в программе"** (`chip-have`): emerald-soft fill, filled dot, pill.
- **"В разработке"** (`chip-planned`): transparent, ink-3 text, dashed 1.5 px border, hollow dot. Never swap the two treatments.

### Cards / Containers
- **Card:** 12 px, 1 px hairline, 2% white fill, 24 px padding, no shadow, grid of three. **Panel / callout / open-line / timeline item:** same recipe with 3% white fill where emphasis is needed.
- **Window:** elevated surface, 1 px `border-default`, 8 px corners, 36 px title bar with accent icon, ink-3 title, drawn minimise / maximise / close.

### Inputs / Fields
- Sunken fill, 1 px `border-default`, 8 px, min height 48 px, 12 x 16 padding; labels 0.9rem 600 ink-2 above. Focus: emerald border plus 3 px emerald ring. Status text: emerald on success, `danger-text` on error. A honeypot field is off-screen.

### Navigation
- Sticky blurred header, logo 130 px (100 / 84 px smaller), links ink-2 turning ink on hover with a 6% white pill, current page weight 600; language switch with left hairline. Footer: brand + four columns of ink-3 links, bottom row with legal text.

### Tabs with sliding pill
- 44 px tabs, hairline border; a pill slides under the active tab (380 ms) and a 2 px emerald bar fills over 7 s as auto-play progress, paused offscreen. On phones tabs stack and the pill is replaced by a plain selected fill. One scene per tab inside a window.

### Seat map and scene widgets
- Seat tile: 8 px, emerald-soft fill with `border-accent` for active, dashed for free, sunken for offline, amber pulsing for under five minutes; selected gets a 2 px emerald ring. Receipt (dotted rows), chat bubbles (14 / 14 / 14 / 4 px corners), language rows (active row inverted to ink on canvas), cache diagram (nodes, dropping dot, fan lines), phone mock (7 px bezel, 36 px radius).

### Atlas
- 112 px tiles with icon and name; selected tile gets accent-soft fill and emerald border; planned tiles dashed; detail panel sticky at 88 px with chip aligned right.

### Dialogs
- Command palette (Ctrl K) and request dialog: native `dialog`, 12 px, elevated with emerald halo, 55-60% black blurred backdrop, 260-320 ms rise. Request dialog opens once a week after 15 s, closes by cross, backdrop and Esc; on phones it is a bottom sheet with 12 px top corners.

## Do's and Don'ts

### Do:
- **Do** take colour, radius, shadow and motion values from the product tokens and reference them by name.
- **Do** show the real Panel and guest screens as the proof; draw frames as Windows 11 windows.
- **Do** mark every capability with the filled or dashed chip.
- **Do** keep the 44 px minimum target (tabs 44, buttons 48+) and the 3 px visible focus outline.
- **Do** give every motion effect a lite fallback and keep the page readable without JavaScript.

### Don't:
- **Don't** use amber for anything that is not urgent.
- **Don't** use macOS traffic lights or web fonts.
- **Don't** put solid heavy boxes around content; hairline and 2% white fill instead.
- **Don't** invent clients, reviews, numbers or prices; the programme is not yet in any club.
- **Don't** return to the old generic "dead and dry" SaaS template look.
