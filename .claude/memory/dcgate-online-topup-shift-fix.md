---
name: dcgate-online-topup-shift-fix
description: Durable — shared-host тест-изоляция (573s→27s) + online-topup кредитуется с shift=null
metadata: 
  node_type: memory
  type: project
  originSessionId: 29fa6666-3b5c-4c0e-8acc-314811021a2e
---

Money-баг (online top-up webhook требовал open shift → 503, деньги не кредитовались) ПОЧИНЕН и в main, e2e на staging.

Durable-уроки:
- **Online-topup кредитуется с `ShiftId=null` / `CreatedByStaffUserId=Guid.Empty`** — cash-репорты считают только entries с shift (поэтому онлайн-пополнение в кассу не попадает, и это правильно).
- **Shared-host тест-изоляция** (как ускорили сьют 573s→~27s, ~20×): один host на сборку + per-test InMemory DB через AsyncLocal + per-test 10.x IP для rate-limiter + hosted services убраны из shared host + ServerGC + глушение логов.
