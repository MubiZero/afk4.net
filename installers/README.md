# AFK4 Installers

AFK4 uses WiX-authored MSI packages for MVP Windows client distribution.

- `organization-admin` packages `AFK4.OrganizationAdmin.App`.
- `agent` packages `AFK4.Agent.Service` and `AFK4.SetupWizard`.
- `player-shell` packages `AFK4.Player.Shell` for Agent-pulled installs.
- `gaming-pc` is the retired coordinated Agent + Player Shell package kept
  only for legacy staging fallback work.
- `bundle` is the WiX Burn master installer (`afk4-client-<version>-<channel>.exe`) that
  carries Microsoft's offline WebView2 installer and chains the `agent` MSI. It is the
  single deliverable; the component MSIs are build inputs moved to `intermediates/`.

  The installer carries everything a club PC needs; nothing is downloaded and no one is
  told to install something. Every program (agent service, setup wizard, player shell,
  Organization Admin) is published self-contained for win-x64, so no .NET is chained or
  checked. WebView2 is a hard prerequisite of every window a human sees, so the bundle
  embeds the Evergreen Standalone Installer (x64, ~210 MB, verified at build time by its
  Microsoft Authenticode signature) and runs it silently only when the runtime is absent.
  VC++ Redistributable is not needed (WPF self-contained ships its native libraries; the
  BAFunctions DLL is linked with /MT). Installing a component MSI on its own still needs
  WebView2 up front, and the MSI says where to get it.

Generated MSI files belong under ignored `artifacts/client-packages/`.
Do not commit built installers, signing keys, certificates, or generated update
package request JSON.
