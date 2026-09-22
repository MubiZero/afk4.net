import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/profile/profile_screen.dart';

import 'package:afk4_customer_app/theme/brand_mark.dart';

import 'support/fake_http.dart';

String _profileJson({
  String name = 'Иван',
  String? phone = '+992900000000',
  String? locale,
  bool marketing = false,
  bool phoneVerified = true,
}) =>
    jsonEncode({
      'playerAccountId': 'p1',
      'displayName': name,
      'phoneNumber': phone,
      'phoneVerified': phoneVerified,
      'preferredLocale': locale,
      'marketingOptIn': marketing,
    });

Widget harness(
  FakeHttpClient http, {
  VoidCallback? onSignOut,
  VoidCallback? onChangeClub,
  ValueChanged<Locale>? onLocaleChanged,
  Locale locale = const Locale('ru'),
}) =>
    MaterialApp(
      locale: locale,
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ProfileScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        onSignOut: onSignOut ?? () {},
        onChangeClub: onChangeClub ?? () {},
        onLocaleChanged: onLocaleChanged ?? (_) {},
      ),
    );

void main() {
  // От подтверждённости зависят пополнение и брони — профиль обязан показывать состояние,
  // а не только сам номер.
  testWidgets('неподтверждённый номер назван неподтверждённым и зовёт подтвердить', (tester) async {
    await tester.pumpWidget(
        harness(FakeHttpClient((_) => (_profileJson(phoneVerified: false), 200))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Номер не подтверждён'), findsOneWidget);

    await tester.tap(find.text('Подтвердить номер'));
    await tester.pumpAndSettle();

    expect(find.text('Подтверждение номера'), findsOneWidget);
  });

  // Смена номера — та же процедура: код уходит на новый номер и доказывает владение им.
  testWidgets('подтверждённый номер можно сменить отсюда же', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Изменить номер'));
    await tester.pumpAndSettle();

    expect(find.text('Подтверждение номера'), findsOneWidget);
    expect(find.text('+992900000000'), findsWidgets);
  });

  testWidgets('показывает имя и телефон игрока', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    expect(find.text('Иван'), findsOneWidget);
    expect(find.text('+992900000000'), findsOneWidget);
  });

  // Веб оставлял вечный скелет: непонятно, грузится или сломалось.
  testWidgets('сбой загрузки виден и лечится повтором', (tester) async {
    var attempt = 0;
    final http = FakeHttpClient((_) =>
        ++attempt == 1 ? ('{"error":"boom"}', 500) : (_profileJson(), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    expect(find.text('Не удалось загрузить профиль.'), findsOneWidget);

    await tester.tap(find.text('Повторить'));
    await tester.pumpAndSettle();

    expect(find.text('Иван'), findsOneWidget);
  });

  // Веб предлагал только русский и английский — в Таджикистане это теряет часть аудитории.
  testWidgets('таджикский предлагается наравне с остальными языками', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    expect(find.text('Таджикский'), findsOneWidget);
    expect(find.text('Русский'), findsOneWidget);
    expect(find.text('English'), findsOneWidget);
  });

  testWidgets('выбор языка применяется сразу и уходит на сервер', (tester) async {
    Locale? applied;
    final http = FakeHttpClient((_) => (_profileJson(locale: 'tg'), 200));
    await tester.pumpWidget(harness(http, onLocaleChanged: (locale) => applied = locale));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Таджикский'));
    await tester.pumpAndSettle();

    expect(applied, const Locale('tg'));
    expect(http.bodies.single, {'preferredLocale': 'tg'});
    expect(find.text('Сохранено'), findsOneWidget);
  });

  // Язык — настройка интерфейса: заминка в сети не повод показывать игроку чужой язык.
  testWidgets('язык применяется даже когда сервер не ответил', (tester) async {
    Locale? applied;
    final http = FakeHttpClient((request) =>
        request.method == 'GET' ? (_profileJson(), 200) : ('{"error":"boom"}', 500));
    await tester.pumpWidget(harness(http, onLocaleChanged: (locale) => applied = locale));
    await tester.pumpAndSettle();

    await tester.tap(find.text('English'));
    await tester.pumpAndSettle();

    expect(applied, const Locale('en'));
    expect(find.text('Не удалось сохранить'), findsOneWidget);
  });

  testWidgets('согласие на рассылку шлёт только изменённое поле', (tester) async {
    final http = FakeHttpClient((request) =>
        (_profileJson(marketing: request.method != 'GET'), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(Switch));
    await tester.pumpAndSettle();

    expect(http.bodies.single, {'marketingOptIn': true});
  });

  testWidgets('выход и смена клуба зовут свои обработчики', (tester) async {
    var signedOut = false;
    var changedClub = false;
    await tester.pumpWidget(harness(
      FakeHttpClient((_) => (_profileJson(), 200)),
      onSignOut: () => signedOut = true,
      onChangeClub: () => changedClub = true,
    ));
    await tester.pumpAndSettle();

    // Выходы стоят в самом низу профиля: на невысоком экране до них надо доскроллить, и с
    // запасом — прокрутка «до видимости» оставляет кнопку под прилипшей шапкой.
    await tester.drag(find.byType(CustomScrollView), const Offset(0, -400));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Выйти'));
    await tester.tap(find.text('Сменить клуб'));
    await tester.pump();

    expect(signedOut, isTrue);
    expect(changedClub, isTrue);
  });

  // Приложение носит цвет и знак клуба — игрок пришёл к нему. Наш знак стоит в самом низу
  // настроек: подпись платформы, которая никому не мешает и никого не путает.
  testWidgets('знак платформы стоит в конце профиля', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_profileJson(), 200))));
    await tester.pumpAndSettle();

    await tester.drag(find.byType(CustomScrollView), const Offset(0, -400));
    await tester.pumpAndSettle();

    expect(find.byType(BrandMark), findsOneWidget);
    expect(find.text('Работает на AFK4.NET'), findsOneWidget);
  });

  // Удаления учётной записи не было ни на сервере, ни здесь, хотя App Store требует его от любого
  // приложения с регистрацией.
  testWidgets('удаление спрашивается и уводит из приложения', (tester) async {
    var signedOut = false;
    late final FakeHttpClient http;
    http = FakeHttpClient((request) =>
        request.method == 'DELETE' ? ('', 204) : (_profileJson(), 200));
    await tester.pumpWidget(harness(http, onSignOut: () => signedOut = true));
    await tester.pumpAndSettle();

    // Кнопка стоит в самом низу настроек — до неё надо доскроллить, как доскроллит человек.
    await tester.ensureVisible(find.text('Удалить учётную запись'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Удалить учётную запись'));
    await tester.pumpAndSettle();
    expect(find.text('Удалить учётную запись?'), findsOneWidget);
    // Пока не подтвердили — ничего не ушло.
    expect(http.requests.any((r) => r.method == 'DELETE'), isFalse);

    await tester.tap(find.widgetWithText(TextButton, 'Удалить'));
    await tester.pumpAndSettle();

    expect(http.requests.any((r) => r.method == 'DELETE'), isTrue);
    expect(signedOut, isTrue);
  });

  testWidgets('отказ от подтверждения ничего не удаляет', (tester) async {
    final http = FakeHttpClient((_) => (_profileJson(), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    // Кнопка стоит в самом низу настроек — до неё надо доскроллить, как доскроллит человек.
    await tester.ensureVisible(find.text('Удалить учётную запись'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Удалить учётную запись'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Отмена'));
    await tester.pumpAndSettle();

    expect(http.requests.any((r) => r.method == 'DELETE'), isFalse);
  });

  // Три причины отказа — три разных следующих шага человека. Одна надпись «не удалось» не помогла
  // бы ни в одном из трёх случаев.
  testWidgets('отказ называет причину, а не «что-то пошло не так»', (tester) async {
    for (final (code, expected) in [
      ('remaining_balance', 'На кошельке остались деньги. Заберите их на стойке клуба — и возвращайтесь сюда.'),
      ('outstanding_debt', 'За вами числится долг. Погасите его — с кошелька или на стойке клуба.'),
      ('active_session', 'Сейчас идёт ваша сессия. Завершите её и попробуйте снова.'),
    ]) {
      final http = FakeHttpClient((request) => request.method == 'DELETE'
          ? (jsonEncode({'error': code}), 409)
          : (_profileJson(), 200));
      await tester.pumpWidget(harness(http));
      await tester.pumpAndSettle();

      // Кнопка стоит в самом низу настроек — до неё надо доскроллить, как доскроллит человек.
    await tester.ensureVisible(find.text('Удалить учётную запись'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Удалить учётную запись'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Удалить'));
      await tester.pumpAndSettle();

      expect(find.text(expected), findsOneWidget, reason: code);
      await tester.pumpWidget(const SizedBox.shrink());
      await tester.pumpAndSettle();
    }
  });


  // Профиль перечитывает себя после подтверждения номера и при возврате на вкладку. Раньше он
  // на это время подменял содержимое спиннером: человек видел свои данные секунду назад, а
  // теперь видит пустой экран — это читается как сбой, хотя всё в порядке.
  testWidgets('перечитывание не подменяет уже показанный профиль ожиданием', (tester) async {
    var calls = 0;
    final http = FakeHttpClient((_) {
      calls++;
      return (_profileJson(name: calls == 1 ? 'Иван' : 'Иван Петров'), 200);
    });
    await tester.pumpWidget(_Reopenable(http: http));
    await tester.pumpAndSettle();
    expect(find.text('Иван'), findsOneWidget);

    // Вкладку закрыли и открыли заново — экран идёт за свежими данными.
    await tester.tap(find.text('Закрыть/открыть'));
    await tester.pump();
    await tester.tap(find.text('Закрыть/открыть'));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('Иван'), findsOneWidget);

    await tester.pumpAndSettle();
    expect(find.text('Иван Петров'), findsOneWidget);
  });

}

/// Родитель, который закрывает и снова открывает вкладку профиля: именно так экран получает
/// повторную загрузку (didUpdateWidget по accountOpen).
class _Reopenable extends StatefulWidget {
  const _Reopenable({required this.http});

  final FakeHttpClient http;

  @override
  State<_Reopenable> createState() => _ReopenableState();
}

class _ReopenableState extends State<_Reopenable> {
  bool _open = true;

  @override
  Widget build(BuildContext context) => MaterialApp(
        locale: const Locale('ru'),
        localizationsDelegates: appLocalizationsDelegates,
        supportedLocales: appSupportedLocales,
        home: Scaffold(
          body: Column(
            children: [
              TextButton(
                onPressed: () => setState(() => _open = !_open),
                child: const Text('Закрыть/открыть'),
              ),
              Expanded(
                child: ProfileScreen(
                  api: PlayerApiClient(baseUrl: 'https://api', httpClient: widget.http),
                  accountOpen: _open,
                  onSignOut: () {},
                  onChangeClub: () {},
                  onLocaleChanged: (_) {},
                ),
              ),
            ],
          ),
        ),
      );
}
