# Reservation Task 6 Retry Classification Report

Status: DONE

Base commit: `e67bec37 fix(operator-booking): harden reservation start retries`

## Fix

- Added an explicit reservation-start outcome classifier.
- Transport failures, HTTP 408, 425, 429, and every 5xx response are ambiguous: the immutable request snapshot remains owned by the unresolved attempt, the modal/form remain locked, and retry replays the exact body and idempotency key.
- Deterministic domain 4xx outcomes clear the snapshot and rotate the key. The existing HTTP 409 path still refreshes authoritative reservation state, then submits the current version with a fresh key.
- No backend or contract changes were required.

## TDD Evidence

RED:

- `isReservationStartOutcomeAmbiguous` did not exist.
- The existing catch branch classified every `PlatformApiError`, including HTTP 500, as deterministic and unlocked the attempt.

GREEN:

- Classifier regression covers transport, 408/425/429/500/502/503/504 as ambiguous and 400/401/403/404/409/422 as deterministic.
- HTTP 500 integration proves locked close/form state and exact same-key/body retry through authoritative success.
- Existing HTTP 409 refresh/current-version/key-rotation regression remains green.

## Verification

- Focused booking/Map/form suite: 52 pass, 0 fail, 279 expects across 5 files.
- Full i18n suite: 35 pass, 0 fail, 754 expects across 4 files.
- Full Operator phase excluding `App.test.tsx`: 650 pass, 0 fail, 1623 expects across 101 files.
- Full Operator `App.test.tsx` phase: 84 pass, 0 fail, 631 expects across 1 file.
- Operator production build: exit 0 (`tsc -b && vite build`).
- Existing build and test warnings are unchanged from the prior review-fix report.

No push was performed.
