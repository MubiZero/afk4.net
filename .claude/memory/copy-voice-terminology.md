---
name: copy-voice-terminology
description: Глоссарий продуктового текста + i18n guard-тест + render-локализация floor-map
metadata: 
  node_type: memory
  type: project
  originSessionId: 9758e85d-c3bf-4ec3-a14b-0e42a3eec377
---

Стандарт продуктового текста (2026-06-04, в main). Durable:
- **Глоссарий** (`docs/product/copy-voice-and-terminology.md`): ПК/устройство/место/клуб/филиал/сеть/киберклуб/тенант.
- **Guard-тест** `voice.test.ts`: нет CAPS, нет «компьютер».
- **Единый i18n-источник**: `locales/{ru,en,tg}.json` → `bun run gen` (см. [[tg-i18n-honesty]]).
- **Архитектурный факт**: floor-map data-слой хранит EN-токены, локализуется на render через `*Label`-функции — это НЕ дыра, t()-ить не надо.
