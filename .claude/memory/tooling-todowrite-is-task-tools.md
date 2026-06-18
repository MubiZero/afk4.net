---
name: tooling-todowrite-is-task-tools
description: This environment has no TodoWrite — superpowers skills that say TodoWrite mean the Task tools (TaskCreate/TaskUpdate/TaskList/TaskGet)
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 6330d5e0-89b2-4fcd-a21d-6966f36a69d9
---

`TodoWrite` does **not** exist in this Claude Code environment. The superpowers skills (`subagent-driven-development`, `using-superpowers`, `executing-plans`, `writing-plans`) say "create a TodoWrite todo per item" because they're written for vanilla Claude Code. Here the task tracker is the **Task tool family**: `TaskCreate` (status starts `pending`), `TaskUpdate` (`in_progress`/`completed`/`deleted`, plus `addBlockedBy`/`addBlocks` for deps), `TaskList`, `TaskGet`. These are deferred tools — load schemas first via `ToolSearch` query `select:TaskCreate,TaskUpdate,TaskList,TaskGet`.

**Why:** I reached for `TodoWrite`, found it missing, and started tracking progress in plain prose instead — the user stopped me and pointed out the Task tools are the right substitute.

**How to apply:** Whenever a skill or instinct says "TodoWrite", silently substitute the Task tools — don't fall back to prose-only tracking. Don't bother editing the plugin cache files (`plugins/cache/.../superpowers/...`) to fix the wording: they get overwritten on every plugin update, so this memory is the durable fix.
