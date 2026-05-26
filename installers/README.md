# AFK4 Installers

AFK4 uses WiX-authored MSI packages for MVP Windows client distribution.

- `operator-app` packages `AFK4.Operator.App`.
- `agent` packages `AFK4.Agent.Service` and `AFK4.SetupWizard`.
- `player-shell` packages `AFK4.Player.Shell` for Agent-pulled installs.
- `gaming-pc` is the retired coordinated Agent + Player Shell package kept
  only for legacy staging fallback work.

Generated MSI files belong under ignored `artifacts/client-packages/`.
Do not commit built installers, signing keys, certificates, or generated update
package request JSON.
