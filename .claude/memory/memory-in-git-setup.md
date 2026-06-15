---
name: memory-in-git-setup
description: Project memory is version-controlled in the repo via a directory junction; how it works and how to set it up on a new device
metadata: 
  node_type: memory
  type: reference
  originSessionId: 6262e69c-029c-4aaa-9867-0fb5498c555f
---

Память проекта **версионируется в гите**, а не только в `~/.claude`. Сделано 2026-06-15.

**Как устроено:**
- В репо память живёт в `<repo>/.claude/memory/` (трекается гитом).
- `.gitignore` игнорит `.claude/*` и вложенные `**/.claude/`, но **re-include**'ит корневую память: `!/.claude/memory/` + `!/.claude/memory/**` (плюс `!/.claude/` чтобы перебить `**/.claude/`). Если новые файлы памяти «молча не коммитятся» — проверь, что эти строки на месте и идут ПОСЛЕ `**/.claude/`.
- Живая папка Claude `~/.claude/projects/<mangled-repo-path>/memory` — это **directory junction** на `<repo>/.claude/memory`. Поэтому всё, что Claude пишет в память, физически лежит в репо и попадает в гит как обычные файлы.
- На этом устройстве: `<mangled-repo-path>` = `D--afk4-net` (из `D:\afk4.net`).

**Новое устройство (одноразово):** `git clone`, затем (PowerShell, без админа; имя папки проекта = `<диск>--<путь>`, Claude создаёт её при первом запуске в проекте):
```powershell
$live="$HOME\.claude\projects\<mangled>\memory"
Remove-Item -Recurse -Force $live   # если Claude уже создал пустую
New-Item -ItemType Junction -Path $live -Target "<repo>\.claude\memory"
```
Сделай junction ДО активной работы, иначе память раздвоится (часть в репо, часть в пустой живой папке).

**Коммит памяти:** обычными `git add .claude/memory && git commit`. Дрейфа нет — источник один.
