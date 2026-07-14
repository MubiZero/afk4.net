# Operator App QA Hardening Design

**Status:** approved for implementation
**Date:** 2026-07-13

## Goal

Make the browser-preview Operator App trustworthy for daily UI QA while fixing
the audited high-priority CTA and color-contrast defects. Production backend
contracts, money rules, and native SignalR behaviour remain unchanged.

## Scope

1. The enabled session-start confirmation button must retain the primary action
   treatment inside the confirmation footer.
2. The audited text and control colors must meet WCAG AA contrast in the dark
   and light themes.
3. Browser-preview mock state must be internally consistent: a successful
   session start updates the later floor-map read, and Settings readiness uses
   the same mock seat and staff sources as the rest of the preview.
4. Browser-preview must not attempt a real SignalR connection. It reports a
   deterministic connected state through a preview-only realtime client.
5. Each behaviour receives a regression test before its implementation.

## Non-goals

- Do not change ASP.NET Core endpoints, DTOs, idempotency, ledger behaviour,
  authentication, or native WebView2 bridge contracts.
- Do not make the preview a full backend emulator for every workspace mutation.
- Do not claim that browser-preview replaces the Windows Agent/Shell smoke
  suite.
- Do not include React test-warning cleanup or bundle splitting in this change.

## Design

### Primary session CTA

`MapSidePanel` already marks the launch action as `cta-primary`. The generic
`.critical-confirmation-actions button` selector has greater specificity and
currently replaces its primary colors. Add an explicit selector for the
enabled primary CTA in that footer, including hover and focus-visible states.
The disabled rule remains visually muted and keeps the HTML `disabled`
attribute authoritative.

### Accessible tokens and component colors

Keep semantic colors, but replace presentation uses that fall below 4.5:1:

- POS SKU text must use a dark-theme text value that is AA-compliant on a card.
- Client list secondary text and inactive badges must use AA-compliant colors.
- Light-theme map controls (command-search placeholder, active rail label,
  avatar, shift label, seat labels, open-tab label, success chips, and primary
  CTA) must meet 4.5:1 for their rendered foreground/background pairs.

The shared token tests remain the floor; component-level tests or an automated
axe scan protect the selectors whose background is component-specific.

### Stateful preview data

`devMockBackend` owns a preview state object created from the existing fixtures.
It exposes only the mutations needed by this slice: start session changes the
target seat to an active session with the requested billing metadata, and
subsequent `GET /floor-map` reads return that modified state. Settings endpoints
derive layout seats and staff counts from the same state, avoiding contradictory
readiness values.

State is memory-local to one browser preview session. Reloading Vite restores
the fixed seed, keeping demos deterministic.

### Preview realtime

Inject or select a no-network preview realtime implementation when
`runtime === 'browser-dev'` and preview mock mode is active. It resolves
`start()` immediately and reports `connected`; it does not fabricate device or
session events. The existing production SignalR construction and reconnect
behaviour remain untouched.

### Verification

Use test-first cycles for each behavioural change. Run the focused Bun tests,
the full Operator Web test suite, production build, and Playwright + axe on
the local preview at 1440×960 in dark and light themes. The Windows validation
runbook remains the release gate for WebView2, Agent, Shell, installers, and
real device enforcement.

## Acceptance Criteria

- An enabled `Старт · открытый счёт` control is visibly primary; the same
  action is visually muted only when disabled.
- Automated axe reports no WCAG 2 A/AA violations on sign-in, floor map in both
  themes, POS, and Clients using the audited preview fixtures.
- After a preview start-session command, the next floor-map fetch renders the
  seat as active and the UI confirmation is visible.
- Settings readiness reports the seeded preview seats and staff rather than
  zero values when the map/session data exists.
- Browser-preview produces no real `/hubs/devices` request and displays the
  connected realtime state.
- Full Operator Web tests and production build pass.
