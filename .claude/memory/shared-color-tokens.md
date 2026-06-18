---
name: shared-color-tokens
description: Палитры Operator+Wizard сведены в общий CSS-пакет @afk4/tokens
metadata: 
  node_type: memory
  type: project
  originSessionId: 7ccf17c2-c4ca-4e42-bc98-f122d3732198
---

Палитры Operator+Wizard унифицированы в `@afk4/tokens` (`packages/tokens/tokens.css`) — единый CSS-источник цвета/шкал (смержено, operator-тема в main, см. [[operator-theme-and-preview]]).

Durable:
- Подключать `import '@afk4/tokens/tokens.css'` **перед** `./styles.css`.
- Оба фронта по умолчанию dark + no-flash inline-скрипт.
- Guard-тесты: used-vars-defined + WCAG-контраст.
- Вне scope: Player.Shell.Web (следующий кандидат), Platform/Customer (на Tailwind).
