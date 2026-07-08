---
name: feedback-no-opus-delegation
description: При делегировании работы субагентам/воркфлоу не использовать модель opus — предпочитать sonnet/haiku
metadata: 
  node_type: memory
  type: feedback
  originSessionId: ad27d0e8-5cc7-4758-adef-517f7f5ae728
---

Не использовать `opus` вообще при спавне субагентов (Agent/Workflow `model:` параметр) — по умолчанию
предпочитать `sonnet` или `haiku`.

**Why:** явное указание пользователя (2026-07-08, во время работы над [[operator-ui-kit-epic]]) — «не юзай
вообще opus, желательно haiku и sonnet».

**How to apply:** при любом `Agent`/`Workflow` вызове с `model:` — выбирать между sonnet/haiku по сложности
задачи (haiku для механических/узких, sonnet для более содержательных), никогда opus. Если не уверен —
sonnet как дефолт. Если модель не задана явно — наследуется модель сессии, это ок (условие касается именно
явного выбора модели для агентов).
