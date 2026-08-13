import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/reservations/reservations_screen.dart';

import 'support/fake_http.dart';

final _now = DateTime(2026, 8, 12, 12, 0, 0);

String _profileJson({String? branchId = 'branch-1'}) => jsonEncode({
      'playerAccountId': 'p1',
      'displayName': 'Иван',
      'phoneNumber': '+992900000000',
      'phoneVerified': true,
      'preferredLocale': null,
      'marketingOptIn': false,
      'homeBranchId': branchId,
      'homeBranchName': 'CyberX на Рудаки',
    });

String _tariffsJson(int count) => jsonEncode([
      for (var index = 1; index <= count; index++)
        {
          'tariffId': 't$index',
          'tariffVersionId': 'v$index',
          'name': index == 1 ? 'Дневной' : 'Ночной',
          'tariffRuleVersionId': 'v$index',
          'versionNumber': 1,
          'currencyCode': 'TJS',
          'pricePerMinuteMinorUnits': 25,
          'minimumBillableMinutes': 0,
          'roundingIncrementMinutes': 1,
          'effectiveFromUtc': '2026-01-01T00:00:00Z',
        },
    ]);

String _quoteJson({int requested = 60, int billable = 60, int amount = 1500}) => jsonEncode({
      'tariffVersionId': 'v1',
      'tariffName': 'Дневной',
      'requestedMinutes': requested,
      'billableMinutes': billable,
      'amountMinorUnits': amount,
      'currencyCode': 'TJS',
    });

/// Сервер раздела броней: список, профиль с филиалом, прайс и расчёт.
FakeHttpClient _serve({
  String profile = '',
  String tariffs = '[]',
  (String, int)? quote,
  (String, int)? create,
}) =>
    FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/profile' => (profile.isEmpty ? _profileJson() : profile, 200),
          '/api/me/branches/branch-1/tariffs' => (tariffs, 200),
          '/api/me/reservations/quote' => quote ?? (_quoteJson(), 200),
          '/api/me/reservations' =>
            request.method == 'POST' ? (create ?? ('{}', 500)) : ('[]', 200),
          _ => ('[]', 200),
        });

Widget harness(FakeHttpClient http) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ReservationsScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        phoneVerified: true,
        clock: () => _now,
      ),
    );

final _confirm = find.byWidgetPredicate(
  (widget) => widget is Text && (widget.data == 'OK' || widget.data == 'ОК'),
);

Future<void> openForm(WidgetTester tester) async {
  await tester.tap(find.byType(FloatingActionButton));
  await tester.pumpAndSettle();
}

Future<void> pickDateTime(WidgetTester tester, String fieldLabel) async {
  await tester.tap(find.text(fieldLabel));
  await tester.pumpAndSettle();
  await tester.tap(_confirm);
  await tester.pumpAndSettle();
  await tester.tap(_confirm);
  await tester.pumpAndSettle();
}

Future<void> fillTimes(WidgetTester tester) async {
  await pickDateTime(tester, 'Начало');
  await pickDateTime(tester, 'Конец');
}

void main() {
  testWidgets('тарифы клуба предлагаются на выбор', (tester) async {
    await tester.pumpWidget(harness(_serve(tariffs: _tariffsJson(2))));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.text('Дневной'), findsOneWidget);
    expect(find.text('Ночной'), findsOneWidget);
  });

  // Клуб без прайса в системе — обычная жизнь маленького клуба, а не поломка: бронь остаётся
  // возможной, просто считают её на стойке.
  testWidgets('без тарифов бронь не блокируется, а объясняется', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.textContaining('посчитают на стойке'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Забронировать'), findsOneWidget);
  });

  testWidgets('единственный тариф выбран сразу — выбирать не из чего', (tester) async {
    final http = _serve(tariffs: _tariffsJson(1));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);

    expect(find.textContaining('К оплате'), findsOneWidget);
    expect(find.textContaining('15,00'), findsOneWidget);
  });

  testWidgets('цену считает сервер, а не приложение', (tester) async {
    final http = _serve(tariffs: _tariffsJson(1));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);

    expect(http.paths, contains('/api/me/reservations/quote'));
    final quote = http.bodies.firstWhere((body) => body.containsKey('tariffVersionId'));
    expect(quote['tariffVersionId'], 'v1');
  });

  // Час брони на тарифе с двухчасовым минимумом стоит два часа. Узнавать это из чека — худший
  // момент, поэтому минимум объясняется до подтверждения.
  testWidgets('минимум тарифа объясняется до подтверждения', (tester) async {
    final http = _serve(
      tariffs: _tariffsJson(1),
      quote: (_quoteJson(requested: 60, billable: 120, amount: 3000), 200),
    );
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);

    expect(find.textContaining('минимум 120 минут'), findsOneWidget);
    expect(find.textContaining('30,00'), findsOneWidget);
  });

  testWidgets('снятый с публикации тариф просит выбрать другой', (tester) async {
    final http = _serve(
      tariffs: _tariffsJson(1),
      quote: ('{"error":"invalid_tariff"}', 404),
    );
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);

    expect(find.textContaining('больше не действует'), findsOneWidget);
    expect(find.textContaining('К оплате'), findsNothing);
  });

  testWidgets('выбранный тариф уходит вместе с бронью', (tester) async {
    final http = _serve(
      tariffs: _tariffsJson(2),
      create: (
        jsonEncode({
          'reservationId': 'r1',
          'seatId': null,
          'seatName': null,
          'startsAtUtc': _now.add(const Duration(days: 1)).toUtc().toIso8601String(),
          'endsAtUtc': _now.add(const Duration(days: 1, hours: 1)).toUtc().toIso8601String(),
          'state': 'confirmed',
          'note': null,
          'tariffVersionId': 'v2',
          'tariffName': 'Ночной',
          'estimatedCostMinorUnits': 1500,
          'currencyCode': 'TJS',
        }),
        200
      ),
    );
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);

    await tester.tap(find.text('Ночной'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Забронировать'));
    await tester.pumpAndSettle();

    final created = http.bodies.lastWhere((body) => body.containsKey('startsAtUtc'));
    expect(created['tariffVersionId'], 'v2');
  });

  // Без филиала спрашивать прайс не по чему — это должно проходить молча, а не ошибкой.
  testWidgets('аккаунт без филиала не ломает форму', (tester) async {
    await tester.pumpWidget(harness(_serve(profile: _profileJson(branchId: null))));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.widgetWithText(FilledButton, 'Забронировать'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
