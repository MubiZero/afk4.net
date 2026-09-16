# AFK4 Installers

AFK4 uses WiX-authored MSI packages for MVP Windows client distribution.

- `organization-admin` packages `AFK4.OrganizationAdmin.App`.
- `agent` packages `AFK4.Agent.Service` and `AFK4.SetupWizard`.
- `player-shell` packages `AFK4.Player.Shell` for Agent-pulled installs.
- `gaming-pc` is the retired coordinated Agent + Player Shell package kept
  only for legacy staging fallback work.
- `bundle` is the WiX Burn master installer (`afk4-client-<version>-<channel>.exe`) that
  carries the .NET 10 Desktop Runtime, installs the Microsoft Edge WebView2 Runtime and
  chains the `agent` MSI. It is the single deliverable; the component MSIs are build
  inputs moved to `intermediates/`.

  The WebView2 runtime is a hard prerequisite of every window a human sees — the setup
  wizard, Organization Admin, the player shell — so the bundle carries Microsoft's
  Evergreen bootstrapper (~2 MB, verified at build time by its Microsoft Authenticode
  signature) and runs it first. The bootstrapper pulls the runtime itself, so the first
  install needs internet; the machine needs it anyway to reach the AFK4 cloud. Installing
  a component MSI on its own still requires the runtime up front, and the MSI now says
  where to get it.

Generated MSI files belong under ignored `artifacts/client-packages/`.
Do not commit built installers, signing keys, certificates, or generated update
package request JSON.
