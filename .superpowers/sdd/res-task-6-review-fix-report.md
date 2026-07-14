# Reservation Task 6 Review-Fix Report

Status: DONE

Base commit: `5fcb723c feat(operator-booking): start confirmed reservations`

## Review Changes

- Locked the booking drawer close action while reservation commands are pending.
- Locked every reservation-start modal exit while the command outcome is pending or transport-ambiguous: header X, Escape, backdrop, and footer Cancel.
- Added an immutable start-attempt snapshot. An ambiguous retry replays the exact request body and idempotency key. The form remains frozen until recovery or explicit New attempt. A new attempt and every determined API failure rotate the key before corrected parameters can be submitted.
- Added authoritative reconciliation coverage: 409 refreshes the reservation and the next request uses the current version plus a fresh key; a refreshed `startedSessionId` recovers success without a second start command.
- Removed duplicate `op.booking.start.earlyWarning` entries from all three source locales, retained the canonical `{time}` message, regenerated messages, and added duplicate/interpolation catalog guards.
- Tariff and package reference-data failures now expose localized known errors or the stable localized fallback, never arbitrary raw exception messages.
- Expanded high-risk coverage for linked-client locking, wallet/package/postpaid/comp request projection and validity, Map guest/package start payloads, pending-close locking, same-key ambiguous retry, explicit edited attempt, 409 version conflict, and linked-session recovery.

## TDD Evidence

Observed RED before implementation:

- Booking drawer close remained enabled while `busy`.
- Source locale duplicate guard found 2668 keys but only 2667 unique keys.
- Tariff loader rendered the raw `internal secret` exception.
- After ambiguous start failure, duration controls and modal close routes remained enabled.
- `buildReservationStartRequest` did not exist for auditable billing-mode request projection.

Final verification:

- Focused booking/Map/form suite: 50 pass, 0 fail, 257 expects across 5 files.
- Full i18n suite: 35 pass, 0 fail, 754 expects across 4 files.
- Full Operator phase excluding `App.test.tsx`: 648 pass, 0 fail, 1601 expects across 101 files.
- Full Operator `App.test.tsx` phase: 84 pass, 0 fail, 631 expects across 1 file.
- Operator production build: exit 0 (`tsc -b && vite build`).
- `git diff --check`: exit 0.

## Existing Warnings

- Production build retains the existing SignalR PURE-annotation placement and large-chunk warnings.
- Full tests retain existing React `act(...)`, duplicate fixture-key, and browser-test `ECONNREFUSED` console warnings; both test phases pass.

No push was performed.
