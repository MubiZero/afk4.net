---
name: afk4-sp4-shipped
description: "SP4 epic — fully shipped to main (incl. realtime, the last track). Records what landed and the small genuinely-deferred backlog. Full blow-by-blow is in git history."
metadata:
  node_type: memory
  type: project
  originSessionId: e09930db-5386-415e-96b4-9fc396df0c89
---

The SP4 body of work (the nine 2026-06-01 design specs, now archived under `docs/archive/superpowers/specs/`) is **fully shipped to main**. Process throughout: execute from spec, TDD, logical commits per slice, no auto-merge. Blow-by-blow lives in git history — not re-recorded here.

**LANDED IN main (all verified green at merge):**
- **Frontend consolidation** — root bun workspace + shared `@afk4/money|i18n|formatting` (TS-source, no build step), consumed by both React apps.
- **Localization** — `locales/{ru,en,tg}.json` → generated `@afk4/i18n` `messages.ts`; web switchers; `BranchEntity.PreferredLocale`; WPF substrate (`AFK4.Localization` + `{loc:T}`). Real Tajik strings landed (no longer a ru stopgap).
- **Notifications backbone** — outbox + dispatcher + SMTP (MailKit); all §7 triggers (auth/billing/operational/digests with CSV backbone). Staff onboarding switched to **invite-by-email only** (breaking; inline staff-create removed).
- **Counter-loop** — open-tab/postpaid, live accrued cost, unified checkout (split payment, attach POS to tab), auto-protection (warn/lock).
- **Tier-0** — offline-resilience U1–U5 (billing outbox, configurable grace + offline lease extension, heartbeat escalation, command-result outbox, operator offline mirror) + anti-fraud §5.1–5.7 (money-action guard/caps/approval, comp control+valuation, audit+manager-review screen, daily owner summary, shift-close sign-off).
- **Customer portal + player-auth** — `PlayerCredentialEntity`, opaque PK-routed player tokens, `/api/me/*` middleware + **first rate-limiter**, portal reads/writes, PWA `AFK4.Customer.Web` (installable, offline-read cache). Real PNG PWA icons shipped (PR #71) so it installs on iOS/Android.
- **Realtime consistency (last SP4 track)** — `SessionEntity.Version` concurrency-token + unique partial index on active SeatId + `ExpectedVersion` on mutations → 409 `stale_version`/`seat_occupied` (migration `20260609024305_AddSessionVersionConcurrencyGuard`); `sessionLifecycleChanged` broadcast via `ISessionLifecycleNotifier` after commit; React `useOperatorRealtime` debounces authoritative floor-map reload + dashboard reconcile on push/reconnect (poll demoted to 120s safety net); configurable `HeartbeatIntervalPolicy` (adaptive when commands pending, default 10s).
- **Phone/email staff identity + password reset** — SMS infra, staff phone identity + verification UI, wizard phone login, email-identity parity, FE forgot/reset-password (`ForgotPassword.tsx`). All merged.
- **Customer-shell WebView2 pivot + Unit F (shop/loyalty/news)** — see [[afk4-customer-shell-pivot]]. Multi-tenant dcgate payments (Subsystems A/B/C) — see [[afk4-multitenant-payments-state]].

**GENUINELY-DEFERRED BACKLOG (intentional):**
- **Player OTP** — email/SMS OTP sign-in + self-registration + phone/display-name self-edit; gated on notifications **Stage 6 SMS channel** (`SmsChannel`) + **InAppChannel**/inbox. Seams (enum values, channel-by-name dispatch) already exist.
- **Per-tenant custom PWA install icons** (dynamic per-tenant manifest) — larger than the default-brand PNG fix that shipped.
- **WPF counter-loop player/tariff pickers** — dormant native-shell UX debt (raw GUID inputs). WPF retirement tail: the **legacy gaming-pc WPF setup tool** (`AFK4.GamingPc.Setup` + `.Core` + `.Tests`), the dead `AFK4.Localization.Wpf` (`TExtension`), `installers/gaming-pc/Package.wxs`, `scripts/publish-staging-bootstrapper.ps1` and the `-IncludeLegacyGamingPcPackage`/`-BuildLegacyStagingBootstrapper` build switches were **removed (PR #76, merged 2026-06-12, `782a55b`)**. The 3 modern WebView2 hosts (Operator.App/Player.Shell/SetupWizard) keep a thin WPF host window — current architecture, NOT retirement targets. `AFK4.Localization` (the non-Wpf `{loc:T}` substrate) stays.
- **Realtime within-track deferrals** — WPF operator parallel `sessionLifecycleChanged` wiring (only if WPF stays a surface), SignalR Redis backplane for multi-node, typed dashboard delta-apply.
- **Customer-shell G5 on-device hardware smoke gate** + Phase-2 vault/privacy-wipe — tracked in [[afk4-customer-shell-pivot]].
- Live admin→agent locale push (belongs to a sync track).

Env/build quirks (bun path, WPF Windows bridge, agent-test baseline, Coolify runbook) in [[afk4-env-quirks]]. Brand epic CLOSED — [[afk4-branding-positioning-backlog]]. Deferred time pass — [[afk4-time-handling-audit]].
