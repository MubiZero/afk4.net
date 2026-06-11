# Productionize the client installer (shared runtime + prod URL + signing)

Status: design, approved for planning — pick up in a new session
Date: 2026-06-11
Branch: main (client-demo work already merged; this epic starts fresh)

## Context — what already shipped

The client install story is functionally done and on `main`:

- A **single agent MSI** (`afk4-agent-<ver>-internal.msi`) covers every device role.
  The wizard asks the role and installs the matching app from a bundled payload:
  `gaming_pc` → Player Shell (supervised by the agent service), `manager_workstation`
  → Operator App (auto-launched by the wizard via `explorer.exe`, with desktop +
  Start-Menu shortcuts and the brand icon). See
  `2026-06-10-setup-wizard-shell-provisioning-design.md` for the provisioning design.
- The standalone operator/player-shell MSIs are build inputs only — bundled inside the
  agent MSI payload and moved to `artifacts/client-packages/intermediates/`.
- Verified end-to-end on a clean VM against staging.

Three things still stand between "works in the demo" and "hand it to a paying club":
size, the staging-pinned URL, and the SmartScreen warning. This epic closes them.

## Problem

### 1. Bloat — every component is self-contained

All components publish **self-contained win-x64**, so each MSI embeds its own full copy
of the .NET runtime (~40–55 MB). The agent MSI now carries three of them (agent +
player-shell + operator payloads) and weighs **~160 MB**. Most of that is duplicated
runtime. An earlier attempt to slim this with a WiX Burn master installer that
**downloaded** the runtime was reverted to self-contained (commit "revert to
self-contained; drop Burn master installers + runtime download") because the *download*
made install fragile (needs internet at install time, bootstrapper `.exe` SmartScreen).

### 2. Staging URL is build-pinned

The wizard's first discovery/enroll call goes to `SetupWizardDefaults.PlatformBaseUrl`,
hardcoded to `https://afk4.staging.mubi.dev`. (After enroll, the agent uses the
`ApiBaseUrl` the platform returns — but the *initial* wizard call is pinned.) There is
no prod build today; a real client install would talk to staging.

### 3. Unsigned — SmartScreen friction

All MSIs are unsigned, so Windows SmartScreen shows "Windows protected your PC → More
info → Run anyway" on first install. Acceptable for an internal demo, alarming for a
club owner. **No code-signing certificate is available yet** — this workstream is
specced but blocked on procuring a cert.

## Goals

- Cut the agent MSI from ~160 MB to ~70–90 MB by shipping **one** shared .NET runtime
  and making the apps framework-dependent.
- The shared runtime is **bundled in the installer** (carried, not downloaded) so
  install stays offline-reliable — this is the key difference from the reverted attempt.
- Produce **prod** builds whose wizard talks to the production platform, selected by a
  build parameter/channel, without touching the staging build.
- Sign the installer (when a cert exists) so installs are SmartScreen-clean.

Non-goals: changing enrollment/provisioning behavior, the role model, or the runtime
*download* path. We carry the runtime, never fetch it.

## Design

### Workstream A — framework-dependent apps + bundled .NET Desktop Runtime

**Runtime choice.** The WPF hosts (`AFK4.Operator.App`, `AFK4.Player.Shell`,
`AFK4.SetupWizard`, all `net10.0-windows`, WPF) need the **.NET 10 Desktop Runtime**
(`Microsoft.WindowsDesktop.App`). The agent worker (`AFK4.Agent.Service`, `net10.0`,
no Kestrel — just HttpClient + SignalR client) needs only the base runtime, which the
Desktop Runtime is a superset of. So **one Desktop Runtime install covers all four
components.** (WebView2 is already a separate prerequisite the MSIs check via
`WEBVIEW2_RUNTIME_*` — we add the Desktop Runtime as a second prerequisite, reusing that
pattern.)

**Publish change.** Switch the four components from `--self-contained true` to
`--no-self-contained` (framework-dependent) in `build-client-packages.ps1`. Each app
drops to a few MB (app dlls + WebView2 native + WebAssets); WebAssets still dominate.

**Critical ordering constraint.** The wizard and the agent service are themselves .NET
apps. Framework-dependent, they cannot run until the runtime is installed. So the
runtime MUST be installed **before**:
- the agent service starts (`AgentServiceCompletionAction` / service auto-start), and
- the wizard auto-launches (the agent MSI's `LaunchSetupWizard` custom action, fired
  `After=InstallFinalize`).

A single MSI cannot guarantee a runtime install precedes its own custom action cleanly.
The right tool is a **WiX Burn bundle** (`.exe`) that chains:
1. .NET 10 Desktop Runtime (skip if already present — Burn `DetectCondition` on the
   runtime's registry/ARP entry, same idea as the existing WebView2 check),
2. WebView2 Evergreen runtime (today the MSI just *blocks* if missing — the bundle can
   install it instead),
3. the agent MSI (which still bundles the role-app payloads and launches the wizard).

The Desktop Runtime installer (~55 MB) is embedded in the bundle (Burn `Payload`),
not downloaded. Net: one runtime in the bundle vs three in today's MSIs.

**Open decisions for A:**
- Bundle UI: silent/managed Burn UI vs the stock bootstrapper UI. Stock is simplest.
- Whether to also install WebView2 from the bundle or keep the current block-and-instruct
  behavior. Installing it is friendlier; adds ~150 MB if we embed the Evergreen
  *standalone* installer — prefer the small **bootstrapper** (online) for WebView2 only,
  or detect-and-block as today. Decide at build time.
- The bundle is an `.exe`, which makes **signing (Workstream C) more important** — an
  unsigned `.exe` bootstrapper triggers worse SmartScreen than an unsigned `.msi`.

### Workstream B — prod URL by build channel

- Make `SetupWizardDefaults.PlatformBaseUrl` resolve from a build-injected value instead
  of a hardcoded staging constant. Options: a `-d`/MSBuild constant baked at publish, or
  a config file next to the wizard exe. Prefer a build constant keyed off the existing
  `-Channel` param: `internal`/`beta` → staging, `stable` → prod
  (`https://afk4.<prod-domain>`). Confirm the prod platform domain before implementing.
- Verify nothing else hardcodes staging on the *initial* (pre-enroll) path: wizard
  discovery/enroll, and any default in the web frontends' `apiBase`/`operatorConfig`
  fallbacks. Post-enroll URLs already come from the platform's enroll response.
- Keep the staging build untouched and default; prod is opt-in via `-Channel stable`.

### Workstream C — code signing (blocked on cert)

- Acquire an OV/EV code-signing certificate (EV avoids SmartScreen reputation
  warm-up). **This is the blocker — no cert today.**
- Sign, in order: each component exe/dll as needed, the role-app MSIs, the agent MSI,
  and finally the Burn bundle `.exe` (Burn requires signing the engine + bundle via
  `insignia`). Add a signing step to `build-client-packages.ps1` gated on a cert path
  param (no cert → skip, current behavior).
- Timestamp signatures so they survive cert expiry.

## Risks / trade-offs

- **Reintroduces the Burn bootstrapper** the team removed once. Mitigation: we *carry*
  the runtime (the reverted version *downloaded* it — that was the fragile part).
- **Runtime version drift**: framework-dependent apps need the exact major runtime.
  Pin to .NET 10 Desktop Runtime; the bundle guarantees presence. A machine-wide runtime
  update is a non-issue (roll-forward within major).
- **Offline installs**: bundling the runtime keeps installs offline-capable; only
  WebView2 (if we choose the online bootstrapper) would need network — keep the
  detect-and-block fallback for fully offline sites.
- **Self-contained is bulletproof**: we are trading simplicity for size. Keep
  self-contained as the demo build until the bundle is proven on a clean VM.

## Phased implementation outline (detailed TDD plan to be authored at kickoff)

1. **Spike A0** — publish one component (operator) framework-dependent, hand-install the
   .NET 10 Desktop Runtime on a clean VM, confirm it runs. Measure size delta.
2. **Phase A1** — flip all four components to framework-dependent in the build script;
   keep emitting the same MSIs (still installable on a VM that has the runtime).
3. **Phase A2** — author the Burn bundle (`installers/bundle/Bundle.wxs`): chain Desktop
   Runtime (embedded, detect-skip) → agent MSI. Build script emits
   `afk4-client-<ver>-<channel>.exe` as the new deliverable; agent MSI moves to
   intermediates alongside the role MSIs. Verify on a clean VM (no runtime preinstalled).
4. **Phase B** — channel-driven prod URL; add `-Channel stable` prod build; verify the
   wizard hits prod. (Independent of A — can land first.)
5. **Phase C** — signing step, gated on cert availability. Blocked until a cert exists;
   wire the build hook now so it's a one-line enable later.

Suggested order: **B → A → C** (B is small and unblocks real prod installs; A is the big
size win; C waits on the cert).

## References

- Prior installer-slimming design + the revert rationale:
  `2026-06-10-setup-wizard-shell-provisioning-design.md` (installer slimming section).
- Wizard launch gate: `installers/agent/Package.wxs` `LaunchSetupWizard` custom action
  (`NOT Installed AND NOT WIX_UPGRADE_DETECTED`).
- Build entrypoint: `scripts/build-client-packages.ps1` (build with **pwsh7**, not
  Windows PowerShell 5.1 — PS5.1 aborts on bun/vite stderr).
- Runtime config writer: `src/AFK4.SetupWizard.Core/EnvironmentBootstrapWriter.cs`
  (+ `AgentBootstrapValues`).
- Architecture source of truth: `2026-05-12-afk4-platform-architecture-design.md`.
