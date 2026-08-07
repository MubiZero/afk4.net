# Wave B — Billing Dunning and Flexible Pricing

**Status:** approved design, 2026-08-07
**Epic:** platform admin gaps, wave B (money). Wave A (platform staff directory,
mandatory TOTP, support mode) is closed — see
`2026-08-04-platform-access-and-support-mode-design.md`.

## 1. Problem

Platform billing issues invoices and marks them overdue, and that is where the
money cycle stops.

- An overdue invoice sends one email. The subscription stays `active` forever.
- `past_due` is only ever reached through a manual `PATCH`.
- `OrganizationSubscriptionEntity.PaymentGraceUntilUtc` is written by a platform
  dialog and read by nothing. It looks like a lever; it is connected to nothing.
- Organization suspension is a separate manual status with no link to money.
- Paying an invoice restores nothing, because nothing degrades.
- `EfInvoiceNotifier` documents in a comment that dunning stages 2+ are deferred.

Pricing has one lever: overwrite `AmountMinorUnits` by hand. A plan change
silently overwrites it again, and nothing records that the price was negotiated.

Two smaller defects sit in the same files: the default currency is `RUB` and the
seeded plans are priced in rubles, while the product sells in Tajikistan; and
`EfInvoiceNotifier` formats money with a hardcoded `/ 100m`.

## 2. Scope

In scope: the money cycle between the platform and a club — carrying non-payment
to a human decision, and making price flexible without hand-editing amounts.

Out of scope: a payment provider (there is none, and none is planned — payment is
confirmed by hand); money inside a club (players, POS, wallets — a separate,
frozen contour); taxes and legal documents.

## 3. States and automation

No new status names. The existing ones carry the cycle.

| Transition | Trigger | Actor |
| --- | --- | --- |
| invoice `issued` → `overdue` | `DueAtUtc` passed | automatic (exists) |
| subscription `active` → `past_due` | organization has an overdue balance | automatic (new) |
| subscription `past_due` → `active` | balance settled | automatic (new) |
| organization → `suspended` | platform decision | **manual, unchanged** |
| organization `suspended` → active | platform decision | **manual, unchanged** |

Suspension stays a human decision: a club that stops selling sessions because a
scheduler ticked at the wrong moment is a worse outcome than a few extra days of
unpaid service. The platform panel makes the decision cheap, not automatic.

**Grace becomes real.** While `PaymentGraceUntilUtc` is in the future for a
subscription:

- no dunning notice is sent for that organization's invoices;
- the subscription is not moved to `past_due` (and an existing `past_due` is not
  reverted — grace suppresses new transitions, it does not settle debt);
- the organization does not appear in the panel's "needs a decision" queue; it
  appears in the debt section marked "grace until …".

When grace expires, the next scheduler tick resumes the ladder from the stage the
invoice's age warrants — stages are computed from `DueAtUtc`, not from the moment
grace ended, so an invoice that spent two weeks under grace resumes at the stage
matching its real age rather than replaying the ladder from the beginning.

**Debt is a balance, not a single invoice.** An organization's outstanding balance
is the signed sum of its `issued` and `overdue` invoices. This is what credit
notes (§5) require: a credited club must stop receiving demands for money it does
not owe. An organization is *in arrears* when it has at least one `overdue`
invoice **and** a strictly positive balance.

**MRR counts `active` + `past_due`.** `EfBillingMetricsService` currently counts
`active` only. Without this change, the first automatic `past_due` transition
would silently drop a paying customer out of revenue reporting — a metric that
degrades the moment the feature starts working is worse than no feature.

## 4. Dunning ladder

The existing hourly `InvoiceGenerationHostedService` carries the ladder; no new
scheduler.

The pre-due reminder and the overdue ladder are tracked separately, because they
are different events and because the overdue stage numbering is already live in
production idempotency keys.

*Pre-due reminder*, once per invoice, 3 days before `DueAtUtc`, template
`invoice.due_soon` (new), idempotency key `invoice-due-soon:{invoiceId:N}`,
recorded on the invoice as `DueSoonNotifiedAtUtc DateTimeOffset?`.

*Overdue ladder*, offsets from `DueAtUtc`, template `invoice.overdue` (exists):

| Stage | Offset |
| --- | --- |
| 1 | 0 (due date passed) |
| 2 | +3 days |
| 3 | +7 days |
| 4 | +14 days |

Stage 1 keeps its current meaning and its current idempotency key
(`invoice-overdue:{invoiceId:N}:1`), which is already asserted by an existing
test; the new stages extend that numbering rather than renumbering it.

Offsets live in `BillingOptions` with these defaults:
`DueSoonReminderBefore = 3.00:00:00` and
`DunningOffsetsAfterDue = [0, 3, 7, 14]` days.

`InvoiceEntity` gains `DunningStage int` (0 = nothing sent, then 1–4 as above) and
`LastDunningAtUtc DateTimeOffset?`. The stage column serves two purposes: it
prevents re-sending after a restart, and it lets the panel show which rung a club
is on without reading a mail log.

Only the highest due stage fires per tick: an invoice that is 10 days overdue when
the scheduler first sees it sends stage 3 once, not stages 1–3 in a burst.

One new template, three locales (`ru`, `en`, `tg`). The Tajik text must be real
Tajik — the repository's parity guard rejects `tg` identical to `ru`.
`invoice.overdue` gains a `daysOverdue` token; per-stage templates are not
introduced.

## 5. Flexible pricing

**Term discount.** On the subscription: `DiscountPercent int?` **exclusive-or**
`DiscountAmountMinorUnits long?`, plus `DiscountUntilUtc DateTimeOffset?` and
`DiscountReason string?`. Setting both discount forms is a validation error. One
active discount per subscription is enough; a discount expires on its own, so
nobody has to remember to remove it. A plan change no longer erases the negotiated
price, because the discount lives beside the plan price rather than replacing it.

**Invoices show the discount.** `InvoiceEntity` gains
`GrossAmountMinorUnits long` and `DiscountMinorUnits long` (0 when none);
`AmountMinorUnits` remains the payable total. Full line items are a large model to
build for one line, and are not introduced. `DiscountMinorUnits` never exceeds
`GrossAmountMinorUnits` — the payable total floors at zero.

**One-off charges and credit notes.** Two new `InvoiceKindNames` values: `one_off`
(setup, hardware, extra service) and `credit`. A credit note carries a **negative**
`AmountMinorUnits`, so the balance is arithmetic rather than a special case in
every query. A negative amount is valid only for `credit`, enforced at the service
boundary; every other kind must be strictly positive. Credit notes are never
"paid" — they are issued and counted in the balance.

Both are created by the platform through
`POST /api/platform/organizations/{organizationId:guid}/invoices` with an explicit
kind, amount, description and due date, behind `ManageInvoices`, audited, and
idempotent through the existing `Idempotency-Key` mechanism used by mark-paid.

**Annual plans.** The `yearly` interval already works in `BillingPeriod.Advance`
and in MRR normalization; only the catalog entries are missing. Three seeded
annual plans priced at ten months of the monthly plan (two months free). No new
mechanics.

## 6. Currency

`BillingOptions.DefaultCurrencyCode`, the entity defaults on
`SubscriptionPlanEntity`, `OrganizationSubscriptionEntity` and `InvoiceEntity`, the
metrics fallback, and the seeded plan prices move to **TJS**.

Seeded monthly prices: Starter 290 000, Growth 790 000, Scale 1 990 000 minor units
(2 900 / 7 900 / 19 900 somoni). Annual: 2 900 000, 7 900 000, 19 900 000.

`EfInvoiceNotifier`'s `/ 100m` is replaced by a small server-side helper that
formats a minor-unit amount for a currency code. The frontend keeps using
`@afk4/money`; the helper exists because the backend renders email bodies and has
no access to that package.

Existing data: production is not deployed. On staging, a migration rewrites the
currency code `RUB` → `TJS` on plans, subscriptions and invoices **without
converting amounts** — 2 900 rubles becomes 2 900 somoni. This is acceptable for a
test environment and must be stated in the runbook so nobody reads it as a
conversion. The plan seeder only adds known plan codes that are missing; it never
touches a code that already exists (custom plans included) — the currency rebase
for existing rows is the migration's job, not the seeder's.

## 7. Platform panel

The Billing screen gains a **Debt** section: club, outstanding amount, days
overdue, dunning stage, grace marker. Row actions: mark paid, grant grace,
suspend, add a support note. The club passport gains the same debt block.

This is where the manual suspension button lives — with the whole picture in view.

The panel must surface the mismatch the manual model allows: an organization that
is still `suspended` after its balance reached zero is shown with an explicit
"debt settled, still suspended" marker, because nothing un-suspends it
automatically.

## 8. Club-side banner

A banner across the top of the club admin when the organization is in arrears:
invoice number, days overdue, amount owed. It renders only for staff holding
`ViewSubscription` — a cashier has no business seeing platform invoices.

Data comes from a new compact endpoint
`GET /api/organizations/{organizationId:guid}/billing/status` under
`ViewSubscription`, returning the arrears flag, balance, oldest overdue invoice
number, days overdue and grace-until. The existing invoice-list endpoint is not
reused: the banner renders on every screen and must not pull a full invoice list
on each load.

## 9. Testing

- Ladder: stage selection at each offset boundary; only the highest due stage
  fires; no repeat after a restart; grace suppresses sending; expiry resumes at
  the age-appropriate stage.
- Transitions: `active` → `past_due` on arrears, back on settlement, no transition
  under grace, suspension untouched throughout.
- Balance: credit note offsets an overdue invoice and stops the ladder; negative
  amount rejected for every kind but `credit`.
- Discount: percent and fixed forms; mutual exclusion rejected; expiry stops
  applying; plan change preserves the discount; total floors at zero.
- Metrics: `past_due` counted in MRR; annual normalized to monthly.
- Currency: migration leaves amounts untouched; email renders through the helper.
- Panel and banner: arrears rendering, permission gate on the banner,
  "settled, still suspended" marker.
- Postgres-backed tests for the scheduler's concurrent paths, per the repository's
  Postgres CI job.

## 10. Slices

Two plans, mirroring wave A.

**Plan 1 — backend**

1. Currency and money formatting.
2. Dunning engine: stage columns, ladder, automatic `past_due` both ways, live
   grace, balance, MRR correction.
3. Flexible pricing: discount, invoice gross/discount fields, one-off and credit
   invoices, annual plans.

**Plan 2 — interface**

4. Debt section in the platform panel and the passport block.
5. Club-side banner and the billing-status endpoint.

Slice 1 is separated deliberately: it touches the same files as the rest, and
mixing a currency rename into new logic guarantees an unreadable review diff.

## 11. Decisions worth remembering

- Grace suppresses **everything**, including the `past_due` transition. A grace
  that still degrades the subscription is a half-measure, and half-present levers
  are what erode trust in an admin panel.
- A credit note is a negative amount, not a separate "in favour of the client"
  flag. Arithmetic instead of special cases in every aggregate.
- MRR was corrected as part of this wave, not after it. A feature that breaks a
  metric the moment it activates is not shippable.
- Suspension stays manual by explicit product decision, not by omission.
