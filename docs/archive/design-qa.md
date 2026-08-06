# Operator cash shift and system footer design QA

> Archived snapshot from 2026-07-15. Local `/tmp` and `~/.codex/generated_images`
> paths referenced below no longer exist; this file does not reflect the
> current state of the Operator App and is kept only for the deliberate
> design differences it records.

## Source and state

- Selected source: `/home/fedya/.codex/generated_images/019f5acc-9310-70a0-9fb4-610db9dfb1b0/exec-62f0089f-18fa-4dbb-89a9-015b12d1a884.png`
- Implemented capture: `/tmp/afk4-cash-shift-dark-1920x945-fixed.png`
- Side-by-side comparison: `/tmp/afk4-shift-qa-comparison-final.png`
- Viewport: 1920 x 945, dark theme, authenticated preview operator, open shift, cash shift tab.
- Responsive evidence: `/tmp/afk4-cash-shift-1440x900.png`, `/tmp/afk4-cash-shift-1280x900.png`, `/tmp/afk4-cash-shift-1100x900.png`, and `/tmp/afk4-cash-shift-light-1280-clean.png`.

## Comparison passes

### Pass 1

- P1 typography: the expected-cash value inherited the small `Money` primitive size instead of the 28 px reconciliation hierarchy. Fixed with an explicit inherited size and color in the reconciliation band.
- P1 layout: the desktop register stopped before the bottom of the workspace and left a large dead region. Fixed by letting the main grid and ledger consume the available height.
- P1 responsiveness: stacked cards could shrink and visually overlap below 1180 px. Fixed by disabling flex shrink for the status and reconciliation bands and allowing the stacked grid to grow naturally.
- P2 readability: movement rows and top command controls were smaller than the selected source. Raised the ledger body to 15 px and the shift command controls to 13 px with medium control height.

### Pass 2

- Layout: selected hierarchy is preserved: shift status, dominant reconciliation, compact revenue summary, primary cash-movement ledger, and past-shifts rail.
- Typography: monetary hierarchy, row copy, headers, and command labels are readable at the target viewport; no microtype is used for operational content.
- Color: semantic green/red states and neutral dark surfaces use the existing AFK4 tokens with sufficient visual separation.
- Icons: all visible controls use the existing Lucide icon family; no placeholder or custom-drawn icons were introduced.
- Behavior: shift commands, export menu, selectable past shifts, loading/error/empty states, keyboard semantics, and permission gates remain functional.
- Responsiveness: 1440 and 1280 retain the two-column terminal; the narrow layout stacks without overlap or horizontal overflow.
- Footer: the selected source's quiet status pattern is carried over with authoritative operator context on the left and system health on the right.

## Deliberate product differences from the concept

- The real command set keeps cash-in, cash-out, close-shift, X-report, and export available instead of replacing them with one illustrative CTA.
- The existing Cash section tabs remain because they are live navigation, not decorative content.
- The past-shift list uses current backend history data and an inline selected-shift detail rather than fabricated rows.

No open P0, P1, or P2 findings remain for the selected state.

## Authoritative system footer QA

### Source and captures

- Supplied reference: `/mnt/c/Users/Mukhamedov.M/Documents/ShareX/Screenshots/2026-07/Photos_urJPsWlDuq.png`.
- Dark 1920 capture: `/tmp/afk4-system-footer-dark-1920.png`.
- Dark 1280 capture: `/tmp/afk4-system-footer-dark-1280.png`.
- Light 1280 capture: `/tmp/afk4-system-footer-light-1280.png`.
- Same-height reference comparison: `/tmp/afk4-footer-comparison-row.png`.

### Blocking comparison

- P1 data fidelity: the initial browser-preview fixture omitted `roleNames` and `appVersion`, producing unavailable values despite complete production contracts. Fixed by mirroring the authenticated role contract, adding `dev` build identity, and preserving live-dev backend roles.
- P2 composition: permanent fields follow the supplied order and grouping: operator, role, club; Online, server health, version, time. The former cash summary is absent.
- P2 typography: labels stay quiet while authoritative values use the stronger weight visible in the supplied footer; semantic green is limited to healthy connection/server states.
- P2 layout: the row is exactly 32 px at 1920 and 1280, `white-space: nowrap` is effective, and measured `scrollWidth` equals `clientWidth` at both widths.
- P2 themes: dividers, text hierarchy, status tones, and right alignment remain readable in dark and light themes.
- Runtime: the Chrome/Chromium pass reported zero console, page, and failed-request errors after authenticated preview load.

No open P0, P1, or P2 findings remain for the authoritative system footer.

final result: passed
