---
name: afk4-client-demo-runbook
description: Как собрать MSI и гонять клиенты против staging; грабли pwsh7/upgrade
metadata:
  node_type: memory
  type: project
  originSessionId: c10f2726-84a8-451c-aaf0-dce6b2b1a599
---

Клиенты задеплоены в main (демо-ветки удалены). Один agent-MSI покрывает все роли (gaming_pc→Player.Shell, manager_workstation→Operator.App auto-install+launch).

Durable runbook/грабли:
- **Сборка MSI — `pwsh7`, НЕ `powershell.exe` (PS 5.1)**: PS5.1 падает на stderr от bun/vite.
- **Upgrade-over-enrolled VM НЕ переоткрывает визард** (`WIX_UPGRADE_DETECTED`) → тестировать визард на **чистой** VM.
- Инсталлятор = WiX Burn bundle, несёт .NET 10 (см. [[afk4-productionize-installer-epic]]).
- Staging-фикстуры под демо: org/owner/tariff заведены.
