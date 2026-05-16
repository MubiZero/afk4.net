# AFK4 Installers

AFK4 uses WiX-authored MSI packages for MVP Windows client distribution.

- `operator-app` packages `AFK4.Operator.App`.
- `gaming-pc` packages `AFK4.Agent.Service` and `AFK4.Player.Shell` together.

Generated MSI files belong under ignored `artifacts/client-packages/`.
Do not commit built installers, signing keys, certificates, or generated update
package request JSON.
