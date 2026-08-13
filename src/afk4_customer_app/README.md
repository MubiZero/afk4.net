# afk4_customer_app

Клиентское приложение AFK4 на Flutter: вход, главная, история, брони, профиль.
Первый заход переезда на Flutter — спека:
`docs/superpowers/specs/2026-08-11-customer-app-flutter-design.md`.

## Проверка

```bash
flutter pub get                                   # он же генерирует классы локализации из .arb
flutter analyze
flutter test                                      # модульные и виджет-тесты
flutter test integration_test -d flutter-tester   # сквозной сценарий
```

Сквозной сценарий (`integration_test/`) поднимает приложение целиком и подменяет только
сервер. В обычный `flutter test` он не попадает, а без `-d flutter-tester` инструмент ищет
подключённое устройство — поэтому в CI это отдельный шаг.

Строки берутся из общего каталога `locales/*.json`: править `lib/l10n/*.arb` руками нельзя,
их перегенерирует `cd packages/i18n && bun run gen`.
