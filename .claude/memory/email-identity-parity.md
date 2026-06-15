---
name: email-identity-parity
description: "DONE & over-delivered — email is a co-equal alternative to phone for staff login/register/reset across all three frontends. Epic CLOSED (PRs #57/#58/#59 on main)."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b5c87d3-256e-49bb-bed9-1f02dbef9069
---

**Status: COMPLETE & over-delivered (confirmed by user + git 2026-06-08). Epic closed.**

Goal (user's rule #33): wherever phone is an identity/auth channel, email must be an equal
alternative — never a deferred afterthought. Staff-only, all 3 frontends, runtime `Email.ToLower()`
match (no NormalizedEmail migration), email-on-file is enough to log in (the invite proves control).

**What shipped (all merged to main):**
- **M1+M2 — PR #57** (`5f6479f`): backend login-by-email (`PasswordHashingStaffCredentialService`
  resolves username-then-email; fixed a real collision bug where an email equal to another user's
  username signed in the wrong user) + Platform.Web channel-aware `ForgotPassword.tsx`/`ResetPassword.tsx`
  (email-token OR SMS-OTP), login relabel «Логин или email», forgot link.
- **M3 — PR #58** (`6cb8d4f`): Operator.App.Web reset screens + email login; plus an **i18n engine
  upgrade** in `@afk4/i18n` (`t(key, values?)` via `intl-messageformat` — ICU interpolation +
  per-locale plurals) and a full migration of ~1500 hardcoded RU Operator strings into the ICU
  catalog (`pluralRu` deleted). tg = real Tajik, no ru-copies.
- **PR #59 `feature/email-coequal-channel`** — the over-delivery: SetupWizard.Web reset screen
  (`ForgotPasswordScreen.tsx`) + remaining channel-parity work. All three frontends now have the
  reset screen and email login.

Reset screens live in: `AFK4.Platform.Web/src/components/ForgotPassword.tsx` +
`AFK4.Operator.App.Web/src/ForgotPassword.tsx` + `AFK4.SetupWizard.Web/src/ForgotPasswordScreen.tsx`.

**Leftover RESOLVED 2026-06-08 → see [[tg-i18n-honesty]]:** the legacy `tg === ru` fake copies
(turned out to be 1027 keys, incl. `op.*`) are now real Tajik + guarded against regression. Related:
[[phone-staff-registration]], [[email-server-available]], [[copy-voice-terminology]], [[tg-i18n-honesty]].
