---
name: afk4-api-client-decomposition
description: Монорепо сохранён осознанно + паттерн domain-sub-client для жирных API-клиентов
metadata:
  node_type: memory
  type: project
  originSessionId: 2bc799dd-22b4-4c03-b6e4-1ce7a2de74c8
---

Кампания декомпозиции god-клиентов ЗАКРЫТА (в main). Durable-решения:
- **Монорепо сохранён** (НЕ полирепо) — осознанно.
- **Паттерн**: god-client → shared transport + per-domain клиенты через фасад `client.<domain>.<method>`; потребители типизируются через `Pick<DomainApi, …>`.
- **WPF ViewModels — off-limits** (не трогать).
- ~40 рёбер у screen-orchestrator — норма, НЕ пилить ради цифр (YAGNI).
