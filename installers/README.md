# AFK4 Installers

AFK4 uses WiX-authored MSI packages for MVP Windows client distribution.

- `organization-admin` packages `AFK4.OrganizationAdmin.App`.
- `agent` packages `AFK4.Agent.Service` and `AFK4.SetupWizard`.
- `player-shell` packages `AFK4.Player.Shell` for Agent-pulled installs.
- `gaming-pc` is the retired coordinated Agent + Player Shell package kept
  only for legacy staging fallback work.
- `bundle` is the WiX Burn master installer (`afk4-client-<version>-<channel>.exe`) that
  chains the setup payload and the `agent` MSI. It is the
  single deliverable; the component MSIs are build inputs moved to `intermediates/`.

  The installer carries everything a club PC needs; nothing is downloaded and no one is
  told to install something. Every program (agent service, setup wizard, player shell,
  Organization Admin) is Native AOT (one file of machine code, Windows 7 to 11), so no .NET
  and no browser runtime (WebView2) is chained or checked: windows are Win32 + Miniblink
  (`AFK4.WebHost`, one DLL beside each exe). VC++ Redistributable is not needed (the
  BAFunctions DLL is linked with /MT).

Generated MSI files belong under ignored `artifacts/client-packages/`.
Do not commit built installers, signing keys, certificates, or generated update
package request JSON.
