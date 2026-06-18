---
name: monolith-refactor
description: Чертёж раскладки разбитых монолитов (Program.cs, App.tsx) + грабли dotnet format
metadata: 
  node_type: memory
  type: project
  originSessionId: c43c3457-e97c-4b54-a8ba-a4b4343ee9c2
---

Два монолита разбиты на модули (в main, `272c5ab`): `Program.cs` 13303→425 (`Endpoints/`, 36 файлов), Operator `App.tsx` 10469→1235 (15 модулей).

Durable-blueprint (если придётся разбивать ещё):
- Раскладка `Endpoints/`: хелперы → domain-эндпоинты → contracts в global namespace; метод byte-exact по route-маркерам снизу-вверх.
- Раскладка `App.tsx`: типы → хелперы → примитивы → воркспейсы.
- **Грабли**: `dotnet format --include` требует **относительный** путь (абсолютный молча не фильтрует).
