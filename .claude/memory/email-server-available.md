---
name: email-server-available
description: "User has a configured SMTP server. SMTP transport + password-reset backend are now implemented; only the FE forgot/reset screen and per-env SMTP config remain."
metadata:
  node_type: project
  type: project
  originSessionId: cbdb204f-0a90-4a09-8ed4-e2ed070352f3
---

The user has an already-configured email/SMTP server for the AFK4 platform — use it rather than provisioning a new provider.

**Now wired in code (as of 2026-06-04, SP4 notifications):** `AFK4.Platform.Api/Notifications/` has `MailKitSmtpTransport`, `SmtpEmailChannel`, `NotificationDispatcher`/`NotificationService` over a notification outbox; staff/owner self-service password reset backend is done (`EfStaffPasswordResetService`, `PasswordResetTokenEntity`, migration `AddPasswordResetTokens`). Contact-field + preference migrations added staff/owner email.

**Remaining gaps:**
- **FE forgot/reset-password screen is still a placeholder** (`ReservedAuthPage` in `Platform.Web/src/App.tsx` for routes `/auth/forgot-password` + `/auth/reset-password`) — the backend is ready, the UI form isn't built yet.
- **Per-environment SMTP config** still needs the user's real connection details wired in (`NotificationOptions`). Ask for them when enabling email in a given env.

**How to apply:** when finishing password reset, build the FE form against the existing backend and fill SMTP config from the user's server. See [[platform-web-redesign]] and [[ux-audit-roadmap]].
