# Agent Installer Enrollment Runbook

Status: current Setup Wizard MSI runbook after Slice 3.4 VM evidence
Last updated: 2026-05-26

## Purpose

This runbook describes the current MVP bootstrap flow for a Windows endpoint.
The default onboarding artifact is now the WiX-built `AFK4 Agent` MSI. It
installs the Agent Service and WPF Setup Wizard; Player Shell and Organization Admin
are intentionally not bundled in this MSI and are pulled by the Agent from the
update channel according to the enrolled device role.

## Preconditions

- The PC runs Windows 10/11.
- The branch exists in the AFK4 backend.
- A branch staff account with enrollment access can sign in through the Setup
  Wizard by phone or email/login and password.
- Agent Service and Setup Wizard binaries come from an approved AFK4
  distribution source.
- Production secrets and signing keys are not stored in the repository.

## Enrollment Flow

1. Install `afk4-agent-<version>-<channel>.msi`.
2. The MSI installs:
   - `AFK4.Agent.Service`;
   - `AFK4.SetupWizard.exe`;
   - update helper scripts;
   - a Start Menu shortcut for the wizard;
   - a per-machine first-run pending marker;
   - a HKLM `RunOnce` entry so the wizard opens on the next admin login if
     immediate launch is not possible.
3. For an interactive install, the MSI attempts to launch
   `AFK4.SetupWizard.exe` after installation. For silent/headless deployment,
   the operator can launch the wizard from the Start Menu or wait for
   `RunOnce`.
4. Sign in in the Setup Wizard with the authorized branch staff account.
5. Choose the target branch and a free floor-map seat. If the seat is missing,
   create it from inside the wizard.
6. Choose the role: `gaming_pc` or `manager_workstation`.
7. The wizard calls `POST /api/install/enroll`.
8. The backend issues:
   - device id;
   - organization id;
   - branch id;
   - device credential secret.
9. Store the device credential and local device key material in per-machine
   storage.
10. Write Agent bootstrap configuration:
   - `Agent:PlatformBaseUrl`;
   - `Agent:OrganizationId`;
   - `Agent:BranchId`;
   - `Agent:DeviceId`;
   - `Agent:DeviceRole`;
   - `Agent:UpdateChannel`;
   - update helper install/rollback/restart commands;
   - Player Shell executable path for `gaming_pc` supervision;
   - lease and update verification public keys.
11. After writing bootstrap configuration, the wizard switches
    `AFK4.Agent.Service` to automatic startup and starts it. Then verify
    heartbeat succeeds and the device appears in the customer dashboard/device
    workflow. Successful completion also clears the first-run marker and HKLM
    `RunOnce` entry so later Agent MSI upgrades do not reopen the wizard.
12. On its update loop the Agent requests role-compatible update packages:
    `gaming_pc` devices install `player-shell`, and `manager_workstation`
    devices install `organization-admin`. Organization Admin installation checks for the
    Microsoft Edge WebView2 Runtime first and runs the Evergreen bootstrapper
    before the MSI when the runtime is missing.
13. After installing Agent Service, Player Shell, or Organization Admin components,
    the update helper schedules an Agent Service restart so new machine
    environment values, component versions, and Shell executable paths are
    loaded by the running service.

## Silent Install By Code (gaming PCs, a whole hall at once)

For a hall installed from a deployment script — no wizard window, no staff
sign-in on each PC (plan P5f-2):

1. In the AFK4.net Panel: **Сеть → Установка → Весь зал разом**. Pick the
   branch, the lifetime (a day, three days or a week) and how many new PCs the
   code may install (1–200), then **Выдать код**. The code is shown once, with
   the ready command; the list keeps only its expiry and the count installed.
   Issuing needs the `organization.devices.install` right in that branch
   (owner, technician).
2. Run the client installer elevated (a deployment tool, an admin console):

   ```
   afk4-client-<version>-<channel>.exe /quiet AFK4_INSTALL_CODE=XXXX-XXXX-XXXX-XXXX [AFK4_SEAT=PC-07]
   ```

   Variable names are upper case. `msiexec /i afk4-agent-….msi /qn
   AFK4_INSTALL_CODE=…` works too, but the bundle also installs the .NET
   runtime and WebView2 the PC needs.
3. The agent MSI hands the code to `AFK4.SetupWizard.exe --install-code … --seat …`
   without a window. The wizard enrolls the PC as a `gaming_pc` by
   `POST /api/install/code/enroll`, writes the Agent bootstrap, installs the
   Player Shell (waiting out a busy Windows Installer, exit 1618), sets up the
   kiosk account and starts the Agent — the same steps the wizard window runs.
4. Seat: `AFK4_SEAT` names it; without it the seat is looked up by the PC's
   Windows name. A seat that is not found, not unique in the branch or held by
   another PC is not a failure — the PC enrolls without a seat and is assigned
   under **Залы и ПК**. With manual approval on in the branch, new PCs wait in
   **Новые ПК** as usual.
5. Restart the PC: the kiosk autologon starts the player screen.

The installer returns before the wizard finishes; the outcome is in
`%ProgramData%\AFK4\logs\setup-wizard.log` and in the wizard's exit code:
`0` installed, `1` no code on the command line, `2` not elevated, `3` refused by
the platform (code unknown, expired, revoked or used up, or the plan's device
limit — retrying will not help), `4` platform unreachable after about eight
minutes of retries, `5` enrolled but the configuration, shell or agent did not
come up, `6` works but without the kiosk. If the silent run fails, the HKLM
`RunOnce` entry stays and the wizard window opens at the next admin logon.

Code rules the platform enforces:

- stored as a SHA-256 hash; the value itself is never in the audit journal, the
  MSI log (`Hidden` property, `HideTarget` action) or the Burn log (`Hidden`
  variable);
- one refusal for unknown, expired, revoked and used-up codes, so a guesser
  learns nothing; 80 random bits in Crockford base32 are not guessable, and the
  door is rate-limited per address anyway;
- reinstalling the same PC (same device key) spends nothing and works even with
  a used-up code;
- the code equals the right to install gaming PCs in its branch and nothing
  wider: no workstation role, no other branch.

The legacy PC enrollment code path and coordinated `afk4-gaming-pc` MSI are
retired from the default onboarding/publishing flow. Use them only as explicit
staging recovery fallbacks for old test devices.

## Safety Rules

- Never ship a hardcoded device credential in installer files.
- Setup Wizard staff tokens must remain scoped to authenticated `install/auth/*`
  endpoints and must not be persisted as device credentials.
- A device credential belongs to exactly one device id.
- If enrollment is repeated for replacement hardware, revoke old credentials
  through the backend.
- The Player Shell is not trusted for enrollment, billing, authorization, or
  update decisions.
- The Agent must remain installed and manageable after a failed Player Shell
  update.

## Manual Recovery

If enrollment or startup fails:

1. Stop the Agent Service.
2. Inspect local Agent logs and backend device command/status records.
3. Remove only the local AFK4 configuration and credential for this device if a
   clean re-enrollment is required.
4. Revoke or reset the staff credential if it may have leaked.
5. Re-run `AFK4.SetupWizard.exe` from the Start Menu and re-enroll the device.
6. Revoke stale credentials for the previous enrollment.

Do not unlock a gaming PC manually as a substitute for a valid backend-approved
session command.
