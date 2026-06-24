---
name: afk4-auto-merge-authorized
description: Пользователь разрешил мержить слайс-PR самому после зелёного CI (PR
metadata: 
  node_type: memory
  type: feedback
  originSessionId: aaa3baee-4740-48ff-a81e-404c51c1bd00
---

Пользователь (2026-06-24) разрешил **мержить PR самостоятельно** — для PR #109 (Касса S0) и **всех последующих** PR, без чек-ина на сам мерж.

**Why:** durable authorization; снимает паузу «финально мержит пользователь». Полный цикл слайса теперь автономен: спроектировать → реализовать (subagent-driven + ревью) → push+PR → дождаться зелёного CI → смержить.

**How to apply:** на слайс-PR дождаться **зелёного CI** (`gh pr checks <N> --watch`; не мержить поверх красного — см. [[feedback_working_style]] и принцип #39), затем смержить **merge-commit** (`gh pr merge <N> --merge`, паттерн проекта), подчистить ветку (локально `git branch -d` + remote `--delete`), обновить память. Необратимое/деструктивное вне мержа — по-прежнему подтверждать.
