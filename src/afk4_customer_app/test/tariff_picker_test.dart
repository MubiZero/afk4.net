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

/// Тариф с расписанием: утренний, по будням.
String _scheduledTariffJson({
  int? from = 8 * 60,
  int? to = 16 * 60,
  int daysMask = 0,
}) =>
    jsonEncode([
      {
        'tariffId': 't1',
        'tariffVersionId': 'v1',
        'name': 'Утренний',
        'tariffRuleVersionId': 'v1',
        'versionNumber': 1,
        'currencyCode': 'TJS',
        'pricePerMinuteMinorUnits': 25,
        'minimumBillableMinutes': 0,
        'roundingIncrementMinutes': 1,
        'effectiveFromUtc': '2026-01-01T00:00:00Z',
        'appliesOnDaysMask': daysMask,
        'appliesFromMinuteOfDay': from,
        'appliesToMinuteOfDay': to,
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

  // Заморозка денег объясняется до брони: молча уменьшенный баланс читается как списание.
  testWidgets('сказано, что сумма удерживается и вернётся при отмене', (tester) async {
    await tester.pumpWidget(harness(_serve(tariffs: _tariffsJson(1))));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);

    expect(find.textContaining('удерживается на кошельке'), findsOneWidget);
  });

  // Отказ по деньгам и занятое время — разные новости: из первого выход в пополнение,
  // из второго — в другое время.
  testWidgets('нехватка денег на бронь названа своей причиной', (tester) async {
    final http = _serve(
      tariffs: _tariffsJson(1),
      create: ('{"error":"insufficient_funds"}', 400),
    );
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);
    await tester.tap(find.widgetWithText(FilledButton, 'Забронировать'));
    await tester.pumpAndSettle();

    expect(find.textContaining('не хватает денег'), findsOneWidget);
  });

  // Без филиала спрашивать прайс не по чему — это должно проходить молча, а не ошибкой.
  testWidgets('аккаунт без филиала не ломает форму', (tester) async {
    await tester.pumpWidget(harness(_serve(profile: _profileJson(branchId: null))));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.widgetWithText(FilledButton, 'Забронировать'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
  // Клуб с «Утренним» и «Вечерним» различает их временем, и игрок должен видеть его до отказа,
  // а не после.
  testWidgets('часы тарифа видно прямо в списке', (tester) async {
    await tester.pumpWidget(harness(_serve(tariffs: _scheduledTariffJson())));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.text('Утренний · 08:00–16:00'), findsOneWidget);
  });

  testWidgets('у тарифа по будням в подписи стоят дни', (tester) async {
    await tester.pumpWidget(harness(_serve(
      tariffs: _scheduledTariffJson(daysMask: 0x1F),
    )));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.text('Утренний · Пн Вт Ср Чт Пт 08:00–16:00'), findsOneWidget);
  });

  // Обычный круглосуточный тариф подписи не получает: приписка к каждому только зашумила бы
  // список.
  testWidgets('тариф без расписания остаётся просто названием', (tester) async {
    await tester.pumpWidget(harness(_serve(
      tariffs: _scheduledTariffJson(from: null, to: null),
    )));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.text('Утренний'), findsOneWidget);
  });

  // Выход отсюда — другой тариф или другое время, и общая «не удалось» не подсказывает ни того,
  // ни другого.
  testWidgets('отказ по часам тарифа назван своей причиной', (tester) async {
    await tester.pumpWidget(harness(_serve(
      tariffs: _scheduledTariffJson(),
      create: ('{"error":"tariff_outside_its_hours"}', 400),
    )));
    await tester.pumpAndSettle();
    await openForm(tester);
    await fillTimes(tester);
    await tester.tap(find.widgetWithText(FilledButton, 'Забронировать'));
    await tester.pumpAndSettle();

    expect(find.textContaining('не действует в выбранное время'), findsOneWidget);
  });
}
