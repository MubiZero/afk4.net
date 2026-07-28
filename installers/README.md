# AFK4 Installers

AFK4 uses WiX-authored MSI packages for MVP Windows client distribution.

- `organization-admin` packages `AFK4.OrganizationAdmin.App`.
- `agent` packages `AFK4.Agent.Service` and `AFK4.SetupWizard`.
- `player-shell` packages `AFK4.Player.Shell` for Agent-pulled installs.
- `gaming-pc` is the retired coordinated Agent + Player Shell package kept
  only for legacy staging fallback work.
- `bundle` is the WiX Burn master installer (`afk4-client-<version>-<channel>.exe`) that
  carries the .NET 10 Desktop Runtime and chains the `agent` MSI. It is the single
  deliverable; the component MSIs are build inputs moved to `intermediates/`.

Generated MSI files belong under ignored `artifacts/client-packages/`.
Do not commit built installers, signing keys, certificates, or generated update
package request JSON.
