import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/dashboard/dashboard_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

import 'support/fake_http.dart';

final _now = DateTime.utc(2026, 8, 12, 12, 0, 0);

String _dashboardJson({
  int wallet = 120050,
  int debt = 0,
  Map<String, dynamic>? session,
}) =>
    jsonEncode({
      'walletBalance': {'currencyCode': 'TJS', 'minorUnits': wallet},
      'debtBalance': {'currencyCode': 'TJS', 'minorUnits': debt},
      'activeSession': session,
    });

Map<String, dynamic> _openSession() => {
      'sessionId': 's1',
      'seatId': 'seat-1',
      'seatName': 'PC-07',
      'startedAtUtc': _now.subtract(const Duration(minutes: 30)).toIso8601String(),
      'durationMode': 'open',
      'remainingSeconds': null,
      'accruedCostMinorUnits': 4500,
      'currencyCode': 'TJS',
    };

Map<String, dynamic> _fixedSession({required int remainingSeconds}) => {
      'sessionId': 's1',
      'seatId': 'seat-1',
      'seatName': 'PC-07',
      'startedAtUtc': _now.subtract(const Duration(minutes: 30)).toIso8601String(),
      'durationMode': 'fixed',
      'remainingSeconds': remainingSeconds,
      'accruedCostMinorUnits': null,
      'currencyCode': 'TJS',
    };

Widget harness(
  PlayerApiClient api, {
  bool phoneVerified = true,
  List<String>? features = const ['online_topup'],
  VoidCallback? onOpenReservations,
}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: DashboardScreen(
        api: api,
        displayName: 'Иван',
        phoneVerified: phoneVerified,
        features: features,
        onOpenReservations: onOpenReservations,
        clock: () => _now,
      ),
    );

/// Заглушка сервера главной: заданный ответ на сам экран плюс пустые ответы на попутные
/// запросы возможностей и заявок на пополнение.
FakeHttpClient _serve(
  (String, int) dashboard, {
  String features = '{"features":["online_topup"]}',
  String intents = '[]',
}) =>
    FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => dashboard,
          '/api/me/features' => (features, 200),
          _ => (intents, 200),
        });

PlayerApiClient clientWith(FakeHttpClient inner) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

/// Снимает экран до конца теста: у главной есть опрос раз в 30 секунд, и оставленный
/// таймер валит тест как «pending timer».
Future<void> unmount(WidgetTester tester) => tester.pumpWidget(const SizedBox.shrink());

void main() {
  testWidgets('показывает баланс кошелька', (tester) async {
    final http = _serve((_dashboardJson(), 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('Баланс кошелька'), findsOneWidget);
    expect(find.textContaining('200,50'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('долг показывается только когда он есть', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve((_dashboardJson(), 200)))));
    await tester.pumpAndSettle();
    expect(find.textContaining('Долг'), findsNothing);
    await unmount(tester);

    await tester.pumpWidget(
      harness(clientWith(_serve((_dashboardJson(debt: 5000), 200)))),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('Долг'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('без сессии показывается «нет активной сессии»', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve((_dashboardJson(), 200)))));
    await tester.pumpAndSettle();

    expect(find.text('Нет активной сессии'), findsOneWidget);
    await unmount(tester);
  });

  // Пустое состояние без следующего шага — тупик: игрок узнал, что сессии нет, и всё.
  testWidgets('без сессии предлагается забронировать место', (tester) async {
    var opened = false;
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(), 200))),
      onOpenReservations: () => opened = true,
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Забронировать'));
    await tester.pump();

    expect(opened, isTrue);
    await unmount(tester);
  });

  // Игрок, открывший приложение в клубе, хочет играть сейчас, а не бронировать на завтра —
  // поэтому посадка стоит главным действием пустого состояния.
  testWidgets('без сессии предлагается сесть за свободный ПК', (tester) async {
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => (_dashboardJson(), 200),
          '/api/me/profile' => (
              jsonEncode({
                'playerAccountId': 'p1',
                'displayName': 'Иван',
                'phoneNumber': '+992900000000',
                'phoneVerified': true,
                'preferredLocale': null,
                'marketingOptIn': false,
                'homeBranchId': 'branch-1',
                'homeBranchName': 'CyberX',
              }),
              200
            ),
          '/api/me/features' => ('{"features":[]}', 200),
          _ => ('[]', 200),
        });

    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('Сесть за ПК'), findsOneWidget);
    // Заодно шапка подписана клубом: у сети их несколько, и до этого узнать, в какой ты
    // вошёл, можно было только через профиль.
    expect(find.text('CyberX'), findsOneWidget);
    await unmount(tester);
  });

  // Филиал неизвестен — предлагать посадку нечем: и места, и тарифы у клуба свои.
  testWidgets('без филиала посадка не предлагается', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve((_dashboardJson(), 200)))));
    await tester.pumpAndSettle();

    expect(find.text('Сесть за ПК'), findsNothing);
    await unmount(tester);
  });

  // Клуб не принимает онлайн-брони — звать некуда, и кнопки быть не должно.
  testWidgets('без онлайн-броней пустое состояние никуда не зовёт', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve((_dashboardJson(), 200)))));
    await tester.pumpAndSettle();

    expect(find.text('Нет активной сессии'), findsOneWidget);
    expect(find.text('Забронировать'), findsNothing);
    await unmount(tester);
  });

  // Деньги — первый вопрос вошедшего, и строка баланса стоит на одном месте независимо от
  // того, идёт сессия или нет: блок, который переезжает вверх-вниз по состоянию, приходится
  // каждый раз искать глазами заново.
  testWidgets('баланс стоит первым, сессия сразу под ним', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _openSession()), 200))),
    ));
    await tester.pumpAndSettle();

    final session = tester.getTopLeft(find.text('PC-07')).dy;
    final wallet = tester.getTopLeft(find.text('Баланс кошелька')).dy;
    expect(wallet, lessThan(session));
    await unmount(tester);
  });

  testWidgets('открытая сессия показывает место, часы и набежавшую стоимость', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _openSession()), 200))),
    ));
    await tester.pumpAndSettle();

    expect(find.text('PC-07'), findsOneWidget);
    expect(find.text('00:30:00'), findsOneWidget);
    expect(find.textContaining('45,00'), findsOneWidget);
    await unmount(tester);
  });

  // Оплаченная сессия заканчивалась без единого сигнала: игрока выбрасывало из-за
  // компьютера, а последнее, что он видел, — спокойные чёрные цифры.
  testWidgets('запас времени не тревожит раньше времени', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _fixedSession(remainingSeconds: 3600)), 200))),
    ));
    await tester.pumpAndSettle();

    expect(find.text('01:00:00'), findsOneWidget);
    expect(find.text('Время заканчивается'), findsNothing);
    await unmount(tester);
  });

  testWidgets('последние минуты оплаченной сессии предупреждают', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _fixedSession(remainingSeconds: 240)), 200))),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Время заканчивается'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('кончившееся время названо кончившимся, а не нулём на часах', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _fixedSession(remainingSeconds: 0)), 200))),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Время вышло'), findsOneWidget);
    await unmount(tester);
  });

  // Показывать баланс, которому нельзя верить, и молчать об этом — хуже, чем сказать.
  testWidgets('пропавшая связь честно помечает цифры устаревшими', (tester) async {
    var calls = 0;
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' when ++calls == 1 => (_dashboardJson(), 200),
          // Заглушка обрывает соединение так же, как это делает пропавшая сеть.
          '/api/me/dashboard' => throw Exception('offline'),
          '/api/me/features' => ('{"features":[]}', 200),
          _ => ('[]', 200),
        });
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    expect(find.text('Нет связи — данные могут устареть'), findsNothing);

    await tester.drag(find.byType(CustomScrollView), const Offset(0, 300));
    await tester.pumpAndSettle();

    expect(find.textContaining('200,50'), findsOneWidget);
    expect(find.text('Нет связи — данные могут устареть'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('сетевая ошибка показывается, только пока показывать нечего', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(FakeHttpClient((_) => ('{"error":"boom"}', 500))),
    ));
    await tester.pumpAndSettle();

    expect(find.textContaining('Не удалось загрузить'), findsOneWidget);
    await unmount(tester);
  });

  // Главное правило опроса: упавший повторный запрос не стирает уже показанные цифры.
  // Иначе пропавшая на секунду сеть подменяет баланс сообщением об ошибке.
  testWidgets('упавшее обновление не стирает уже показанный баланс', (tester) async {
    var dashboardCalls = 0;
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => ++dashboardCalls == 1
              ? (_dashboardJson(), 200)
              : ('{"error":"boom"}', 500),
          '/api/me/features' => ('{"features":[]}', 200),
          _ => ('[]', 200),
        });

    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    expect(find.textContaining('200,50'), findsOneWidget);

    await tester.pump(const Duration(seconds: 31));
    await tester.pumpAndSettle();

    expect(dashboardCalls, greaterThan(1));
    expect(find.textContaining('200,50'), findsOneWidget);
    expect(find.textContaining('Не удалось загрузить'), findsNothing);
    await unmount(tester);
  });

  // Продлевают ровно в тот момент, когда смотрят на убегающие цифры. Если за временем надо
  // идти к стойке, сессия чаще всего просто заканчивается.
  testWidgets('у оплаченной сессии время можно продлить прямо из карточки', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _fixedSession(remainingSeconds: 600)), 200))),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Продлить'), findsOneWidget);
    await unmount(tester);
  });

  // У открытой сессии нет оплаченного остатка: продлевать нечего, она идёт, пока игрок сидит.
  testWidgets('у открытой сессии продления нет', (tester) async {
    await tester.pumpWidget(
      harness(clientWith(_serve((_dashboardJson(session: _openSession()), 200)))),
    );
    await tester.pumpAndSettle();

    expect(find.text('Продлить'), findsNothing);
    await unmount(tester);
  });

  testWidgets('после продления экран говорит о результате и перечитывает себя', (tester) async {
    var dashboardCalls = 0;
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => (
              _dashboardJson(session: _fixedSession(remainingSeconds: ++dashboardCalls == 1 ? 600 : 4200)),
              200
            ),
          '/api/me/features' => ('{"features":["online_topup"]}', 200),
          '/api/me/sessions/s1/extend' => ('{}', 200),
          _ => ('[]', 200),
        });

    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Продлить'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Продлить на 1 час'));
    await tester.pumpAndSettle();

    expect(find.text('Сессия продлена на 1 час'), findsOneWidget);
    // Экран перечитан: остаток на часах уже новый, а не тот, с которым игрок нажимал кнопку.
    expect(http.paths.where((path) => path == '/api/me/dashboard').length, greaterThan(1));
    expect(find.text('01:10:00'), findsOneWidget);
    await unmount(tester);
  });

  // Заказ к месту имеет смысл только при идущей сессии — сервер и меню отдаёт по месту.
  // Меню открыто всегда: цены смотрят и до игры, а плитка, появляющаяся только при сессии,
  // выглядит как пропавшая.
  testWidgets('заказ еды предлагается и до, и во время сессии', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _fixedSession(remainingSeconds: 600)), 200))),
      features: const ['player_shop'],
    ));
    await tester.pumpAndSettle();
    expect(find.text('Заказать еду'), findsOneWidget);
    await unmount(tester);

    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(), 200))),
      features: const ['player_shop'],
    ));
    await tester.pumpAndSettle();
    expect(find.text('Заказать еду'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('без магазина в тарифе клуба заказа нет', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _fixedSession(remainingSeconds: 600)), 200))),
      features: const ['online_topup'],
    ));
    await tester.pumpAndSettle();

    expect(find.text('Заказать еду'), findsNothing);
    await unmount(tester);
  });

  testWidgets('заказ открывается с главной и возвращает обновлённый баланс', (tester) async {
    var dashboardCalls = 0;
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => (
              _dashboardJson(
                wallet: ++dashboardCalls == 1 ? 120050 : 108050,
                session: _fixedSession(remainingSeconds: 600),
              ),
              200
            ),
          '/api/me/features' => ('{"features":["player_shop"]}', 200),
          _ => ('[]', 200),
        });

    await tester.pumpWidget(harness(clientWith(http), features: const ['player_shop']));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Заказать еду'));
    await tester.pumpAndSettle();
    expect(find.text('Заказ к месту'), findsOneWidget);

    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    // Заказ списывает деньги — вернувшись, игрок должен видеть настоящий баланс, а не тот,
    // с которым уходил.
    expect(find.textContaining('080,50'), findsOneWidget);
    await unmount(tester);
  });
}
