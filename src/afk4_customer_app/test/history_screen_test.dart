import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/history/history_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

import 'support/fake_http.dart';

final _now = DateTime.utc(2026, 8, 12, 12, 0, 0);

Map<String, dynamic> _visit({
  String id = 's1',
  String seat = 'PC-07',
  bool hasReceipt = true,
  int total = 4500,
}) =>
    {
      'sessionId': id,
      'seatId': 'seat-1',
      'seatName': seat,
      'startedAtUtc': _now.subtract(const Duration(hours: 3)).toIso8601String(),
      'endedAtUtc': _now.subtract(const Duration(minutes: 30)).toIso8601String(),
      'timeChargeMinorUnits': total,
      'posTotalMinorUnits': 0,
      'grandTotalMinorUnits': total,
      'currencyCode': 'TJS',
      'hasReceipt': hasReceipt,
    };

Map<String, dynamic> _purchase({String id = 'p1', int total = 1500}) => {
      'posSaleId': id,
      'createdAtUtc': _now.subtract(const Duration(days: 1)).toIso8601String(),
      'totalMinorUnits': total,
      'currencyCode': 'TJS',
      'lines': [
        {'productName': 'Кола', 'quantity': 2, 'unitPriceMinorUnits': 750, 'lineTotalMinorUnits': total}
      ],
    };

String _page(List<Map<String, dynamic>> items, {String? next}) =>
    jsonEncode({'items': items, 'nextCursor': next});

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: HistoryScreen(api: api, clock: () => _now),
    );

PlayerApiClient clientWith(FakeHttpClient inner) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

FakeHttpClient _serve({String? visits, String? purchases, String? receipt}) =>
    FakeHttpClient((request) {
      final path = request.url.path;
      if (path.endsWith('/receipt')) return (receipt ?? '{"error":"missing"}', receipt == null ? 404 : 200);
      if (path == '/api/me/visits') return (visits ?? _page([]), 200);
      return (purchases ?? _page([]), 200);
    });

void main() {
  testWidgets('визиты показывают место, длительность и сумму', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(visits: _page([_visit()])))));
    await tester.pumpAndSettle();

    expect(find.text('PC-07'), findsOneWidget);
    expect(find.textContaining('45,00'), findsOneWidget);
    expect(find.textContaining('2 ч 30 мин'), findsOneWidget);
  });

  testWidgets('пустая история говорит об этом словами', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve())));
    await tester.pumpAndSettle();

    expect(find.text('Пока нет визитов'), findsOneWidget);
  });

  testWidgets('ошибка загрузки предлагает повторить, и повтор срабатывает', (tester) async {
    var attempt = 0;
    final http = FakeHttpClient((request) {
      if (request.url.path != '/api/me/visits') return (_page([]), 200);
      return ++attempt == 1 ? ('{"error":"boom"}', 500) : (_page([_visit()]), 200);
    });
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    expect(find.text('Не удалось загрузить историю.'), findsOneWidget);

    await tester.tap(find.text('Повторить'));
    await tester.pumpAndSettle();

    expect(find.text('PC-07'), findsOneWidget);
  });

  testWidgets('«показать ещё» дозагружает следующую страницу', (tester) async {
    final http = FakeHttpClient((request) {
      if (request.url.path != '/api/me/visits') return (_page([]), 200);
      return request.url.queryParameters['cursor'] == null
          ? (_page([_visit(seat: 'PC-01')], next: 'c2'), 200)
          : (_page([_visit(id: 's2', seat: 'PC-02')]), 200);
    });
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    expect(find.text('PC-02'), findsNothing);

    await tester.tap(find.text('Показать ещё'));
    await tester.pumpAndSettle();

    expect(find.text('PC-01'), findsOneWidget);
    expect(find.text('PC-02'), findsOneWidget);
    expect(find.text('Показать ещё'), findsNothing);
  });

  // Кнопка чека обещает то, чего может не быть, — поэтому её нет там, где чека нет.
  testWidgets('у визита без чека кнопки чека нет', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(visits: _page([_visit(hasReceipt: false)])))));
    await tester.pumpAndSettle();

    expect(find.text('Чек →'), findsNothing);
  });

  testWidgets('вкладка покупок показывает состав и сумму', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(purchases: _page([_purchase()])))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Покупки'));
    await tester.pumpAndSettle();

    expect(find.text('Кола × 2'), findsOneWidget);
    expect(find.textContaining('15,00'), findsOneWidget);
  });

  testWidgets('чек открывается из визита и показывает итог', (tester) async {
    final receipt = jsonEncode({
      'receiptNumber': 'Ч-000123',
      'createdAtUtc': _now.toIso8601String(),
      'sessionId': 's1',
      'seatName': 'PC-07',
      'startedAtUtc': _now.subtract(const Duration(hours: 2)).toIso8601String(),
      'endedAtUtc': _now.toIso8601String(),
      'timeChargeMinorUnits': 3000,
      'posLines': [
        {'productName': 'Кола', 'quantity': 1, 'unitPriceMinorUnits': 1500, 'lineTotalMinorUnits': 1500}
      ],
      'posTotalMinorUnits': 1500,
      'grandTotalMinorUnits': 4500,
      'currencyCode': 'TJS',
    });
    await tester.pumpWidget(harness(clientWith(_serve(visits: _page([_visit()]), receipt: receipt))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Чек →'));
    await tester.pumpAndSettle();

    expect(find.text('Ч-000123'), findsOneWidget);
    expect(find.text('Кола × 1'), findsOneWidget);
    expect(find.textContaining('45,00'), findsOneWidget);
  });

  // «Чека нет» и «не смогли загрузить» — разные новости: первое окончательно, второе нет.
  testWidgets('пропавший чек говорит «не найден», а не «ошибка»', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(visits: _page([_visit()])))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Чек →'));
    await tester.pumpAndSettle();

    expect(find.text('Чек не найден'), findsOneWidget);
  });

  testWidgets('сбой загрузки чека говорит именно об ошибке', (tester) async {
    final failing = FakeHttpClient((request) => request.url.path.endsWith('/receipt')
        ? ('{"error":"boom"}', 500)
        : (_page([_visit()]), 200));
    await tester.pumpWidget(harness(clientWith(failing)));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Чек →'));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось загрузить чек.'), findsOneWidget);
  });
}
