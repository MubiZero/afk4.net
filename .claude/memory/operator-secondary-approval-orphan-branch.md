---
name: operator-secondary-approval-orphan-branch
description: "Единственная непомерженная ветка проекта: подтверждение денежных действий вторым сотрудником (SecondaryApproval) — в main его нет, решение по судьбе не принято"
metadata: 
  node_type: memory
  type: project
  originSessionId: 0b12af7f-4dcf-4243-beb9-11cda59c4af0
  modified: 2026-08-10T02:51:59.124Z
---

**Ветка `feat/operator-reports-workspace-consolidation` — единственная в репозитории, чьи коммиты
не в main** (проверено 2026-08-10: +30 коммитов, tip 16.07, база отстала от main на 339).

Большая часть её содержимого до main **всё-таки доехала**, но переписанная заново под новые пути
(продукт переименован `AFK4.Operator.App.Web` → `AFK4.OrganizationAdmin.Web`): консолидированные
Отчёты (`reports/ReportsWorkspace.tsx`, Revenue/ShiftCash/Summary) и центр Управления
(`management/ManagementScreen.tsx` + `destinations/`) в main есть.

## Что в main НЕ доехало

Подсистема **`AntiFraud/SecondaryApproval/`** — 9 файлов + 6 тест-файлов. Идея: высокорисковое
денежное действие подтверждается **вторым сотрудником прямо на месте** (ввод его учётных данных,
без токенов), вместо асинхронной очереди одобрений. Обработчики: возврат в POS, комп сессии,
закрытие смены, money-action. Плюс WPF-хост получал защищённый prompt для ввода.

В main вместо этого по-прежнему живёт **очередь одобрений** (`MoneyActionApprovalService`,
`EfMoneyActionExecutor`), и она в июле была отредизайнена (`3f34f341 redesign approvals and audit`).

**Развилка не решена и требует владельца продукта:** очередь и подтверждение-на-месте — два
конкурирующих ответа на один вопрос. Ветку мержить нельзя (пути мертвы, 30 коммитов на старой базе);
если подтверждение вторым сотрудником нужно — это отдельный эпик поверх нынешнего анти-фрода, а
ветка служит источником готового дизайна и тестов.

Связано: [[operator-pos-receipts-panel]] (там же обратное решение — «усыпить анти-фрод» — отменено
ходом main), [[operator-redesign-phase0-decisions]], [[memory-hygiene-verify-status]].
