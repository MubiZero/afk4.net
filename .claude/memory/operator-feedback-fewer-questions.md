---
name: operator-feedback-fewer-questions
description: "During active Operator-redesign dev the user wants fewer questions — don't ask ordering/sequencing, just proceed; everything in the program will be done anyway"
metadata:
  node_type: memory
  type: feedback
---

В активной фазе разработки (эпик переработки Operator App) пользователь явно просит **меньше вопросов, больше работы**.

**Why:** всё запланированное всё равно будет сделано (идёт активная разработка), поэтому уточнять очерёдность кусков/этапов — пустой round-trip.

**How to apply:** не спрашивать «с какого куска начать / в каком порядке» — просто бери следующий по разумной логике (дешёвый фронт → тяжёлый бэкенд) и делай. Спрашивать только реально развилочное: необратимое, противоречие спеки и кода, или решение, меняющее объём. Связано с [[operator-redesign-phase0-decisions]].
