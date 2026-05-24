# AFK4 Post-Onboarding Roadmap

Status: Roadmap (not an implementation plan)
Date: 2026-05-24
Owner: AFK4 platform

## What This Document Is

This is a **roadmap**, not an implementation plan. Each section below names
an initiative that lives outside the
[2026-05-24 club self-service onboarding plan](./2026-05-24-afk4-club-self-service-onboarding.md)
but is still on the long-term path to a real SaaS product. The shape is
intentionally light — a paragraph or two each on what it is, why it matters,
what it depends on, and a rough architecture sketch — so we can pick any item
up later and expand it into its own implementation plan file without
re-thinking the basics.

Order below reflects approximate dependency order, not commit-priority. Pick
based on business pressure when the time comes.

---

## 1. Production environment separation

**What.** A second Coolify project (`AFK4 → production`) running on a separate
domain, with its own Postgres, its own MinIO/update bucket, its own session
signing key, and its own Cloudflare uptime monitors. Currently staging serves
both purposes, which conflates Mubi's test data with anything pilot clubs
would touch.

**Why.** Pilot clubs eventually need a "prod" badge on the URL even if the
backend is identical, and Mubi's experimental Coolify redeploys should not
take down a live club. Also: when SignPath signs MSIs, the signed update
manifest needs to live on a domain that won't churn.

**Depends on.** A final production domain decision. The current best
candidate is `afk4.net` (which Mubi does not yet own); fallback is
`afk4.mubi.dev` apex. The whole separation hinges on this — doing it on
`afk4.mubi.dev` and then re-doing on `afk4.net` is a real ops migration.

**Sketch.** Mirror the existing `coolify-staging-deploy.yml` workflow as
`coolify-prod-deploy.yml` with `workflow_dispatch`-only triggering (no
auto-deploy on push). Mirror `deploy/coolify/staging.env.template` as
`prod.env.template` and the Postgres fallback compose. Add prod-side
hostnames to the Cloudflare uptime worker
(`deploy/cloudflare/staging-uptime-monitor/worker.js` — note: rename worker
namespace at this point). Generate and store a separate session-signing
key in Coolify secrets, commit the matching public key as
`deploy/coolify/prod-session-signing-public.pem`. Document prod-specific
deltas as `docs/operations/coolify-prod-deploy.md` (don't fork the staging
doc — diff against it).

---

## 2. SignPath signing rollout

**What.** Apply to SignPath Foundation now that the AGPL-3.0 LICENSE is on
`main` (landed in PR #45). Once approved, configure the GitHub repository
settings, flip the `signing_provider` workflow input to `signpath`, and ship
signed MSIs.

**Why.** Unsigned MSIs trigger SmartScreen "Unverified publisher" on every
install — acceptable for pilot 1-2 (decision recorded in the onboarding
plan), unacceptable at scale.

**Depends on.** SignPath Foundation approval (timeline: SignPath-side, weeks
not days). PR #45 is fully merged; the wiring is in
`scripts/sign-client-packages.ps1` and `.github/workflows/client-packages.yml`.

**Sketch.** Add repo secret `AFK4_SIGNPATH_API_TOKEN`. Add repo variables
`AFK4_SIGNPATH_ORGANIZATION_ID`, `AFK4_SIGNPATH_PROJECT_SLUG`,
`AFK4_SIGNPATH_SIGNING_POLICY_SLUG`. Trigger the client-packages workflow
with `signing_provider=signpath` for a release branch, verify the resulting
MSI is signed end-to-end on a clean Windows VM (no SmartScreen warning).
Update `docs/operations/client-packaging.md` to make signpath the default
documented path. No code changes are expected — the scaffolding is in place.

---

## 3. Self-service signup from the landing page

**What.** Public "Try AFK4" CTA on the landing site (Slice 4 of the
onboarding plan) leads to a sign-up form that creates a pending tenant
without Mubi intervention. Mubi reviews + approves in
`/admin/tenants/pending` (or sets auto-approve once anti-abuse is good
enough).

**Why.** Without self-service, every prospect is a manual onboarding by
Mubi, which caps growth at the rate Mubi can hand-hold. With it, the
sales funnel becomes a real funnel.

**Depends on.** Slice 4 (landing page) shipped. Slice 2 customer dashboard
shipped (`/admin/tenants/pending` queue needs a UI). Some form of
anti-abuse — at minimum email verification + CAPTCHA, ideally a brief
phone/manual screening for pilot phase.

**Sketch.** New `POST /api/public/signup` endpoint, unauth, rate-limited
hard. Body: organization name, owner email, owner display name, branch
city + name, password. Creates `Organization` with
`SubscriptionStatus=pending_approval`, `Branch`, `StaffUser` with
`branch_owner` role, sends email verification (requires an email-sending
backend — postmark, mailgun, or amazon ses). Email verify endpoint marks
the staff user verified. Tenant stays `pending_approval` until Mubi flips
it to `trial` in `/admin/tenants/{orgId}/approve`. Auto-approve flag can
go on the tenant or globally.

A separate concern that comes with self-signup: bot/spam mitigation. At
pilot scale, a Cloudflare Turnstile widget on the form + email-verify
gate is sufficient. At real scale, consider Stripe Identity or similar.

---

## 4. Payments + billing UI

**What.** Real money flowing into the system: tenant subscription billing,
optional usage-based add-ons (extra branches, extra devices beyond plan
limits), invoice generation, payment method storage, dunning for failed
payments.

**Why.** Until this exists, every paid tenant is invoiced manually by Mubi,
which (a) caps scale, (b) creates accounting/tax friction, (c) means no
involuntary churn — clubs that stop paying still take backend resources
because suspending them requires Mubi to notice.

**Depends on.** A payment provider decision. Real options for AFK4's pilot
geography (Central Asia / Russia):
- **Stripe** — best DX globally, doesn't work for RU/TJ residents trying
  to pay.
- **ЮKassa / Тинькофф Касса** — RU-domestic, works for RU clubs, doesn't
  serve EU/US.
- **CloudPayments** — RU + CIS, similar tradeoffs.
- **Local TJ acquirers** (Eskhata acquiring, etc.) — works for Dushanbe
  pilots, doesn't scale across borders.
- **Multi-provider** — possible but doubles maintenance.

This is genuinely a business decision, not a technical one. Pick the
provider based on where the first 10 paying clubs will be.

**Sketch.** New `Billing` module under `src/AFK4.Platform.Api/Billing/`.
Entities: `BillingAccount` (per tenant), `PaymentMethod`, `Invoice`,
`InvoiceLine`, `SubscriptionPlan` (extends current `PlanCode`). Webhook
endpoint per provider. Idempotent invoice generation on plan period
boundaries (cron job or Hangfire). Failed-payment retry schedule with
escalating suspend warnings → `SubscriptionStatus=past_due` → eventual
`suspended` after grace period.

UI in customer dashboard `/club/billing`: current plan, invoice history,
payment method (add/remove/set primary), upgrade/downgrade flow with
prorated changes, usage charts (devices vs limit, sessions vs limit),
download invoice PDF. UI in Mubi admin `/admin/tenants/:orgId/billing`:
read-only view + manual override actions (issue refund, comp a month,
write off, change plan as-admin).

---

## 5. Audit log UI

**What.** A searchable, filterable view of the `audit_records` table in the
Mubi admin (currently the data is written but no UI surfaces it). Also a
tenant-scoped read-only view in the customer dashboard so owners can see
who did what in their own club.

**Why.** When something goes wrong, "what happened" is currently a Postgres
query Mubi runs by hand. For support, security review, and disputes
(owner: "I didn't change that limit, who did?"), this needs to be a UI.

**Depends on.** Nothing structurally — backend audit writes already exist
across every slice. This is pure SPA work + maybe a search index
optimization for large `audit_records` tables.

**Sketch.** `/admin/audit` — date range picker, actor filter (platform
admin / staff / system), action filter, target filter, free-text search.
Server-side pagination, `audit_records` indices on `(CreatedAtUtc)`,
`(ActorStaffUserId, CreatedAtUtc)`, `(ActorPlatformAdminUserId,
CreatedAtUtc)` (latter already exists from Slice B). Export to CSV for
the inevitable "send us the audit trail" support request.

`/club/branches/:branchId/audit` — same widget, scoped to the branch's
own organization. Read-only.

---

## 6. Password reset flow

**What.** `/auth/forgot-password` → enter email → emailed magic link →
`/auth/reset-password?token=...` → set new password. Plus a "rotate my
password" affordance inside the dashboard.

**Why.** No password reset is a real product gap — if a club owner forgets
their password they have to contact Mubi to get it reset via the same
DB-delete-and-rebootstrap recipe we used for the staging admin
([recipe](../../home/fedya/.claude/projects/-mnt-d-projects-afk4-net/memory/project_staging_admin.md))
. That doesn't scale and is embarrassing.

**Depends on.** Same email-sending backend as self-service signup
(#3) — it's worth building both at once.

**Sketch.** New `password_reset_tokens` table: `TokenHash`,
`StaffUserId` (or `PlatformAdminUserId`), `ExpiresAtUtc` (15-minute
window), `UsedAtUtc`. `POST /api/auth/forgot-password` always returns
200 regardless of whether the email exists (anti-enumeration). Emails
go to whatever sender is wired for #3. `POST /api/auth/reset-password`
with token + new password, single-use, invalidates all other sessions
for that user. Add audit events. Same flow mirrors for platform-admin
accounts at `/api/platform/auth/forgot-password`. Lockout policy:
already-used token rejected; expired token rejected; brute force on
the lookup endpoint rate-limited.

---

## 7. Mobile companion app

**What.** A native mobile app (iOS + Android via React Native or .NET MAUI)
for the club owner. Read-mostly. Push notifications on critical events:
device down, large cash transaction, day-end summary, support ticket
reply.

**Why.** Owners are not at the club 24/7. Today, if a Gaming PC dies at
3am the owner finds out at 9am from the operator. With push, the owner
knows immediately and can call to coordinate. Also: "watching my club"
on the phone is a real engagement hook.

**Depends on.** A SignalR / push gateway. Stable customer-dashboard API
shape (#3 / #5 ish — the same data the dashboard serves). A push
provider — Firebase Cloud Messaging (free tier covers pilot), Apple
Push Notification service (free for paid Apple Developer accounts —
$99/yr).

**Sketch.** React Native preferred over MAUI (faster dev for small team,
more devs available later). Auth: scan a QR code from the customer
dashboard's `/club/account/devices` page → exchanges for an app-bound
refresh token. App polls or subscribes via SignalR for:
- Branch / device status overview
- Active sessions live count
- Today's revenue + transaction count
- Recent alerts (device down, manual intervention needed)

Push topics per branch, owner subscribes to their own. Notification
preferences UI per topic + quiet hours.

This is the smallest item on this roadmap if React Native; the largest
if MAUI or native two-platform.

---

## 8. Multi-tenant white-label

**What.** Allow franchise / reseller scenarios where a partner sells AFK4
to clubs under their own brand. Custom domain per partner (e.g.
`computers.partner-co.com`), custom logo, custom email sender, optional
revenue share.

**Why.** Unblocks a specific business model where AFK4 grows through
partners (e.g. equipment resellers in a region) instead of direct
sales. Whether to pursue this is a business decision, not a technical
one — putting it on the roadmap means "ready if the business calls
for it", not "definitely shipping".

**Depends on.** Self-service signup (#3) and billing (#4) are
prerequisites — without them, partners can't onboard their own clubs
or get paid. Production env separation (#1) is required because
partners can't share Mubi's staging URL.

**Sketch.** New entity `Partner` above `Organization` in the hierarchy.
Partner owns N organizations. Custom domain CNAME flow: partner adds
a CNAME `app.partner-co.com → ingress.afk4.net`, AFK4 detects the
custom Host header, serves a re-branded SPA bundle. Branding stored
per Partner: logo SVG, primary color, font family, email-from address.
Billing: optionally Partner pays Mubi a flat platform fee + clubs pay
Partner (Partner controls retail price), or Mubi takes a revenue share
through the standard billing engine with Partner-specific split rules.

This item could also become its own SaaS product later — a "white-label
SaaS engine" with AFK4 as the first vertical. Out of scope here.

---

## 9. Final prod domain acquisition + cutover

**What.** Acquire the final production domain (likely `afk4.net`), update
all hard-coded references, DNS, Coolify hosts, Cloudflare monitors,
landing site canonical URLs, MSI download URLs, email templates, etc.

**Why.** The current `*.mubi.dev` subdomains are fine for staging but a
real product needs its own apex. Doing this once everything is built
is much cheaper than doing it twice.

**Depends on.** Production env separation (#1) — both can happen in the
same cutover. SignPath rollout (#2) — re-signing MSIs with the new
download URL embedded is a meaningful operation. Landing site (Slice
4 of onboarding plan) — content/screenshots/legal docs reference the
domain.

**Sketch.** Single "cutover day" runbook: acquire domain, configure DNS,
add Coolify hostnames in parallel, smoke-test parallel `mubi.dev`
and `afk4.net` for a week, flip canonical / OG / sitemap URLs, redirect
`*.mubi.dev` → `*.afk4.net`, retire old hostnames after a 30-day
grace period. The Cloudflare uptime worker grows two monitor sets
during the parallel period, drops the old set after.

Email templates and the MSI download flow are the trickiest pieces —
old MSIs in the wild will keep checking the old update URL, so the
old update-channel host stays live until the install base has fully
rolled over (Agent tracks its own check URL, and a single forced
update can switch all live agents to the new host).

---

## Cross-Cutting Concerns

These don't get their own numbered section because they aren't
single initiatives — they're things every section above should think
about.

- **Localization beyond RU/EN.** First two pilot regions are Tajikistan
  and Russia. Tajik (Cyrillic and Arabic scripts) and Uzbek are realistic
  next languages. The SPA + landing site should never hard-code English
  strings; everything goes through an i18n layer from day one.
- **Tax + legal per jurisdiction.** TJ, RU, KZ all have different VAT,
  invoicing, data-residency requirements. Billing (#4) and self-service
  signup (#3) both touch this. Worth a separate legal review checkpoint
  before shipping #3 or #4.
- **Observability beyond `/api/health`.** Today: a 5-min cron from
  Cloudflare. Eventually: real metrics (Prometheus + Grafana on the
  Coolify host), distributed tracing (OpenTelemetry), structured logs
  with a queryable store (Loki / Elastic). Not urgent until either
  pilot count exceeds ~5 or we add the billing module.
- **Backups + DR drill cadence.** The Postgres backup runbook
  ([`docs/operations/postgres-backup-restore.md`](../../operations/postgres-backup-restore.md))
  exists, but the actual DR drill ("restore staging from yesterday's
  backup, verify clubs can sign in") has not been run. Add to the
  routine after production env separation (#1) so we drill against a
  non-staging DB.
- **Security review checkpoint.** Before payments (#4) and self-service
  signup (#3) ship, run an external security review (pentest +
  authentication/authorization audit). PR-time review is not enough
  once real money flows through.

## Related

- [[2026-05-24-afk4-club-self-service-onboarding]] — the implementation
  plan this roadmap dangles off of.
- [[2026-05-23-saas-control-plane-tenant-onboarding]] — the preceding
  shipped plan (Slices 1-6 + hardening A-E).
- [[project-uptime-monitor]] — operational memory of the existing
  Cloudflare uptime worker; #1 prod-separation has to extend it.
- [[project-staging-admin]] — operational memory of the staging admin
  bootstrap recipe; #6 password reset replaces the DB-delete recipe
  for normal users.
- [[project-repo-visibility]] — AGPL-3.0 license + repo visibility
  state; #2 SignPath depends on this being on `main`.
