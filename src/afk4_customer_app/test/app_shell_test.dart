import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/auth/player_session.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/shell/app_shell.dart';

import 'support/fake_http.dart';

final _now = DateTime.utc(2026, 8, 12, 12, 0, 0);

final _session = PlayerSession(
  playerAccountId: 'p1',
  organizationId: 'o1',
  displayName: 'Иван',
  phoneVerified: true,
  accessToken: 'access-1',
  accessTokenExpiresAtUtc: _now.add(const Duration(hours: 1)),
  refreshToken: 'refresh-1',
  refreshTokenExpiresAtUtc: _now.add(const Duration(days: 30)),
);

String _dashboard() => jsonEncode({
      'walletBalance': {'currencyCode': 'TJS', 'minorUnits': 120050},
      'debtBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
      'activeSession': null,
    });

String _visits() => jsonEncode({
      'items': [
        {
          'sessionId': 's1',
          'seatId': 'seat-1',
          'seatName': 'PC-07',
          'startedAtUtc': _now.subtract(const Duration(hours: 2)).toIso8601String(),
          'endedAtUtc': _now.toIso8601String(),
          'timeChargeMinorUnits': 3000,
          'posTotalMinorUnits': 0,
          'grandTotalMinorUnits': 3000,
          'currencyCode': 'TJS',
          'hasReceipt': false,
        }
      ],
      'nextCursor': null,
    });

FakeHttpClient _serve({String features = '{"features":["online_topup","online_booking"]}'}) =>
    FakeHttpClient((request) => switch (request.url.path) {
      '/api/me/dashboard' => (_dashboard(), 200),
      '/api/me/features' => (features, 200),
      '/api/me/reservations' => ('[]', 200),
      '/api/me/visits' => (_visits(), 200),
      '/api/me/purchases' => ('{"items":[],"nextCursor":null}', 200),
      _ => ('[]', 200),
    });

Widget harness(FakeHttpClient http, {VoidCallback? onSignOut}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: AppShell(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        session: _session,
        onSignOut: onSignOut ?? () {},
        onChangeClub: () {},
        onLocaleChanged: (_) {},
        clock: () => _now,
      ),
    );

void main() {
  testWidgets('открывается на главной', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    expect(find.text('Баланс кошелька'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('нижняя панель уводит в историю и обратно', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('История'));
    await tester.pumpAndSettle();
    expect(find.text('PC-07'), findsOneWidget);

    await tester.tap(find.text('Главная'));
    await tester.pumpAndSettle();
    expect(find.text('Баланс кошелька'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Возврат на уже открытый раздел не должен выглядеть как повторный вход в приложение.
  testWidgets('возврат в историю не перезагружает список заново', (tester) async {
    final http = _serve();
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.text('История'));
    await tester.pumpAndSettle();
    final afterFirstVisit = http.paths.where((path) => path == '/api/me/visits').length;

    await tester.tap(find.text('Главная'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('История'));
    await tester.pumpAndSettle();

    expect(http.paths.where((path) => path == '/api/me/visits').length, afterFirstVisit);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('все готовые разделы на месте', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    expect(find.text('Профиль'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('клуб без онлайн-броней не показывает раздел броней', (tester) async {
    await tester.pumpWidget(harness(_serve(features: '{"features":["online_topup"]}')));
    await tester.pumpAndSettle();

    expect(find.text('Брони'), findsNothing);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Список возможностей не пришёл — разделы показываются все. Спрятать «Брони» из-за
  // сетевого сбоя значит соврать игроку, что клуб их не принимает.
  testWidgets('неизвестные возможности оставляют разделы на месте', (tester) async {
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => (_dashboard(), 200),
          '/api/me/features' => ('{"error":"boom"}', 500),
          '/api/me/reservations' => ('[]', 200),
          '/api/me/visits' => (_visits(), 200),
          _ => ('[]', 200),
        });
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    expect(find.text('Брони'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Приглашение с главной должно приводить туда, где бронь действительно создают.
  testWidgets('«забронировать место» с главной ведёт в раздел броней', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Забронировать место'));
    await tester.pumpAndSettle();

    expect(find.text('Броней пока нет'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('раздел броней открывается из панели', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Брони'));
    await tester.pumpAndSettle();

    expect(find.text('Броней пока нет'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });
}
