# afk4_customer_app

Приложение игрока AFK4 на Flutter (Android и веб; iOS не собирается): клубы и брони,
кошелёк и пакеты, магазин, события и новости клуба, друзья, отзывы, кешбэк и приглашения,
вход на ПК по QR. Разделы — каталоги в `lib/`. Исходная спека первого захода:
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
