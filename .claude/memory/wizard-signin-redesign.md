---
name: wizard-signin-redesign
description: owner-code удалён из проекта целиком; device-approval — опция филиала
metadata: 
  node_type: memory
  type: project
  originSessionId: 7f9fa301-0ccd-443e-b70e-46a02bb23dca
---

Редизайн sign-in визарда (phone-first) давно в main; UI много раз перекрыт — текущая правда в коде.

Durable-факты (важно, отменяют старые заметки про «owner-code fallback»):
- **owner-code выпилен из ВСЕГО проекта** — эпик `chore/remove-owner-code`, миграция `DropOwnerCodes` (drop `owner_codes` + колонка `devices.EnrolledViaOwnerCodeId`); unauth install-эндпоинтов нет.
- **Device-approval НЕ обязателен по умолчанию**: `BranchEntity.RequireManualDeviceApproval=false` → enroll сразу `approved`; ручное одобрение — опция филиала.
- Зона по умолчанию — «Общий зал».
