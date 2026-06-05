---
name: Working style — bias to action
description: User prefers minimal clarifying questions; execute the plan, only pause at real blockers
type: feedback
originSessionId: 11b65f43-4e3f-43c9-954c-c8d5ee25e470
---
Ask less, do more. When an implementation plan exists, execute it task-by-task without asking permission between tasks. Make reasonable judgment calls on small deviations (version pins, equivalent packages, target-framework adjustments) and report them after the fact in the commit message or end-of-chunk summary.

**Why:** user stated explicitly "спрашивай поменьше, больше дела" after I paused between Phase 0 end and Phase 1 start asking whether to continue — they already approved the plan, re-confirming every phase transition wastes their time.

**How to apply:**
- Inside an approved plan: proceed through tasks and phases without check-in prompts.
- Still pause for: real blockers (failing build I can't fix, ambiguous spec, destructive action, test failures after 2 attempts).
- Still pause for: scope changes that weren't in the original plan, touching shared state, running commands that affect the user's machine beyond the repo.
- Report deviations in the summary, not as a question beforehand — unless the deviation is large enough to change the plan shape.
