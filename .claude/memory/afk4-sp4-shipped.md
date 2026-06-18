---
name: afk4-sp4-shipped
description: SP4 смержена; genuinely-deferred бэклог (что реально осталось)
metadata:
  node_type: memory
  type: project
  originSessionId: e09930db-5386-415e-96b4-9fc396df0c89
---

Волна SP4 полностью в main (realtime, PNG-иконки, реальный таджикский, phone/email identity, reset-password). История — в гите.

**Genuinely-deferred бэклог** (живое, что реально не сделано):
- Player OTP — гейт на Stage 6 (SMS/InApp).
- Per-tenant PWA icons.
- WPF counter-loop pickers (мелкий UI-долг).
- SignalR Redis backplane (для multi-instance realtime).
- G5 on-device hardware-smoke (см. [[afk4-customer-shell-pivot]]).

3 WebView2-хоста сохраняют тонкое нативное WPF-окно — это НЕ цель ретайра. `AFK4.Localization` остаётся.
