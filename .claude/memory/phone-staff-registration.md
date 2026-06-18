---
name: phone-staff-registration
description: payom.tj SMS + OTP-инварианты + phone как global login-id (эпик закрыт)
metadata: 
  node_type: memory
  type: project
  originSessionId: 15e86a91-8567-474d-9b69-4bed613f0ddb
---

Эпик «телефон вместо owner-code» ЗАКРЫТ и в main. **owner-code полностью выпилен** из проекта (миграция `DropOwnerCodes`, каталога `OwnerCodes/` нет) — он больше НЕ fallback (старые заметки про это неверны). Program.cs давно разбит на модули (см. [[monolith-refactor]]).

Durable-инварианты:
- **SMS**: payom.tj — `POST gateway.payom.tj/api/message`, Bearer, senderName «AFK4.NET», лимит 67 кириллица/сегмент → OTP-текст ≤65 симв.
- **Phone = global login-id** (unique verified index).
- **OTP-инфра**: TTL 5 мин / 3 попытки / cooldown 60 с / окно 5 ч.
- Reuse: opaque staff tokens, notification channel-паттерн.
