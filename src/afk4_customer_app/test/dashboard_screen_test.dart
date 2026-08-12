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

    await tester.tap(find.text('Забронировать место'));
    await tester.pump();

    expect(opened, isTrue);
    await unmount(tester);
  });

  // Клуб не принимает онлайн-брони — звать некуда, и кнопки быть не должно.
  testWidgets('без онлайн-броней пустое состояние никуда не зовёт', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve((_dashboardJson(), 200)))));
    await tester.pumpAndSettle();

    expect(find.text('Нет активной сессии'), findsOneWidget);
    expect(find.text('Забронировать место'), findsNothing);
    await unmount(tester);
  });

  // Идущая сессия — то, ради чего экран открывают посреди игры.
  testWidgets('идущая сессия стоит выше кошелька', (tester) async {
    await tester.pumpWidget(harness(
      clientWith(_serve((_dashboardJson(session: _openSession()), 200))),
    ));
    await tester.pumpAndSettle();

    final session = tester.getTopLeft(find.text('PC-07')).dy;
    final wallet = tester.getTopLeft(find.text('Баланс кошелька')).dy;
    expect(session, lessThan(wallet));
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

}
