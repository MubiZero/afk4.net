---
name: memory-in-git-setup
description: Память проекта версионируется в гите через симлинк/junction на <repo>/.claude/memory
metadata: 
  node_type: memory
  type: reference
  originSessionId: 6262e69c-029c-4aaa-9867-0fb5498c555f
---

Память проекта **версионируется в гите**: живёт в `<repo>/.claude/memory/` (git-tracked). Живая папка Claude `~/.claude/projects/<mangled>/memory` — симлинк/junction на неё, поэтому всё, что Claude пишет, физически лежит в репо и коммитится обычным `git add .claude/memory`.

**`.gitignore`**: игнор `.claude/*` + `**/.claude/`, но re-include корневой памяти — `!/.claude/` + `!/.claude/memory/` + `!/.claude/memory/**` ПОСЛЕ `**/.claude/`. Если новый файл «молча не коммитится» — проверь эти строки.

**Новое устройство (одноразово, ДО активной работы — иначе раздвоение):** `git clone`, затем заменить пустую живую папку ссылкой на репо-память.
- **WSL/Linux** (репо `/home/<u>/projects/afk4.net`, mangled `-home-<u>-projects-afk4-net`): `rm -rf $live && ln -s <repo>/.claude/memory $live`.
- **Windows** (репо `D:\afk4.net`, mangled `D--afk4-net`): PowerShell `New-Item -ItemType Junction -Path $live -Target <repo>\.claude\memory`.

Работа идёт с обеих машин (Windows native + WSL); junction настроен на каждой.
