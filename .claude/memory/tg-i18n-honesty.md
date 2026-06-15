---
name: tg-i18n-honesty
description: Legacy tg===ru fake copies fixed — 1027 keys really translated to Tajik + a guard test blocks future silent ru-copies. Committed 99aa918 on branch fix/tg-i18n-real-translations, pushed → PR #63 open (not yet merged) 2026-06-08.
metadata: 
  node_type: memory
  type: project
  originSessionId: 596031e9-fdfa-4f46-a021-b4f79e29bc43
---

**2026-06-08:** killed the legacy `tg === ru` fake copies in `locales/tg.json`.

**The problem:** 1027 of 2365 keys held Russian text copied into the Tajik locale to satisfy the
key-parity test (which only checks *key sets*, not values). The runtime already falls back `tg→ru`
(`packages/i18n/src/I18nProvider.tsx`), so the fakes produced the exact same on-screen result as
*no key at all* — pure lie, zero value, and broke any coverage metric. ~1323 keys were genuinely
translated already (so Tajik was clearly a real target).

**What was done (3 files, committed `99aa918` on branch `fix/tg-i18n-real-translations`):**
- `locales/tg.json` — 894 values changed: all 1027 fakes translated to real Tajik (Cyrillic) via 12
  parallel subagents batched by namespace, anchored on a glossary extracted from the existing real
  translations for consistency. 133 of the 1027 stayed identical to ru because they are genuine
  loanwords Tajik borrows (тариф, бонус, онлайн, логин, категория, артикул…). Blank-line section
  separators in the source JSON were preserved (diff is values-only).
- `packages/i18n/src/messages.test.ts` — new guard test `tg has no silent ru-copies`: any `tg===ru`
  fails unless the key is in `TG_IDENTICAL_TO_RU_ALLOWED` (148 entries: loanwords + brand tokens like
  Starter/MRR + symbols like №). A second test kills stale whitelist entries. Old key-parity test kept.
- `messages.ts` — regenerated via `bun run gen` (in `packages/i18n`).
- Normalized 12 cross-batch term inconsistencies to one form (Касса→Хазина, Журнал→Рӯзнома,
  Кошелёк→Ҳамён, payment Карта→Корт).

Gate: `cd packages/i18n && bun test` → 34/34 green.

**Honest caveats for the native owner to red-pen:** the 1027 translations are model-produced (good but
not native-reviewed); the 148-key whitelist includes borderline loanwords (Филиал, Объект, Система,
Каталог, Оболочка) that a purist might prefer in pure Tajik — the existing corpus itself is split
(Филиал 14 vs Шӯъба 3, Хазина 15 vs Касса 9), so those are genuinely the owner's terminology call.

Related: [[email-identity-parity]], [[copy-voice-terminology]], [[frontends-on-bun-test]].
