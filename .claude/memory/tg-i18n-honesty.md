---
name: tg-i18n-honesty
description: Guard-тест против tg===ru фейков + caveat «переводы не native-reviewed»
metadata: 
  node_type: memory
  type: project
  originSessionId: 596031e9-fdfa-4f46-a021-b4f79e29bc43
---

Легаси-фейки `tg===ru` починены (реальный таджикский), **смержено в main `6c60604` (PR #63)**.

Durable:
- **Guard-тест** `tg has no silent ru-copies` (`packages/i18n`): любой `tg===ru` падает, кроме whitelist `TG_IDENTICAL_TO_RU_ALLOWED` (~148 легитимных loanwords). Добавляешь tg-ключ — делай реально таджикский, иначе красный (ловил это в booking-сессии).
- Runtime fallback `tg→ru` для дыр.
- Терминология: Касса→Хазина, Кошелёк→Ҳамён.
- **Caveat**: переводы model-produced, НЕ native-reviewed.
