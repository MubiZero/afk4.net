---
name: setup-wizard-shell-provisioning
description: Hard-won root-cause провижининга (env→SCM-cache→401) + code-map
metadata:
  node_type: memory
  type: project
  originSessionId: provisioning
---

Визард ставит на enroll нужный app роли (gaming_pc→Player.Shell, manager_workstation→Operator.App), агент поднимает БЕЗ ребута — DONE & verified на чистой VM.

**Hard-won root-cause (durable-золото):** визард писал device-credential в MACHINE ENV → SCM кэширует env на boot → сервис не видел credential → 401 → краш host (ребут маскировал баг). **Фикс**: читать creds из `%ProgramData%\AFK4\Agent\bootstrap.json` свежими на каждый старт. Также `BackgroundServiceExceptionBehavior=Ignore`; лог `%ProgramData%\AFK4\logs\agent.log`.

Code-map: web `FinishedScreen.tsx` `ShellStatusRow`, bridge op `wizard:provisionShell`, i18n `setup.wizard.finished.shell.*`. Инсталлятор-форма — см. [[afk4-productionize-installer-epic]].
