---
name: operator-wizard-auth-phone-first
description: Durable — порядок резолва staff sign-in + анти-enumeration forgot-password
metadata: 
  node_type: memory
  type: project
  originSessionId: 16d55fb3-a7fa-4e36-af4f-29c4ec7b2325
---

Auth Operator+Wizard унифицирован phone-first (PR #82 в main; UI-детали с тех пор переписаны, правда в коде).

Durable:
- **`/api/auth/staff/sign-in`** резолвит идентификатор в порядке username → email → verified phone (`ResolveOrgUserAsync`).
- **Анти-enumeration**: `/forgot-password*` всегда возвращает 200; реальная отправка гейтится внутри (не раскрывать существование аккаунта).
- Phone-хелперы — в per-app `phoneFormat.ts` (НЕ общий пакет).
