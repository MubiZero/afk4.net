import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/wallet/wallet_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

import 'support/fake_http.dart';

final _now = DateTime.utc(2026, 8, 12, 12, 0, 0);

Map<String, dynamic> _visit({
  String id = 's1',
  String seat = 'PC-07',
  bool hasReceipt = true,
  int total = 4500,
}) => {
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
    {
      'productName': 'Кола',
      'quantity': 2,
      'unitPriceMinorUnits': 750,
      'lineTotalMinorUnits': total,
    },
  ],
};

String _page(List<Map<String, dynamic>> items, {String? next}) =>
    jsonEncode({'items': items, 'nextCursor': next});

Widget harness(PlayerApiClient api) => MaterialApp(
  locale: const Locale('ru'),
  localizationsDelegates: appLocalizationsDelegates,
  supportedLocales: appSupportedLocales,
  home: WalletScreen(
    api: api,
    phoneVerified: true,
    features: const ['online_topup'],
    clock: () => _now,
  ),
);

PlayerApiClient clientWith(FakeHttpClient inner) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

String _dashboard({int wallet = 120050, int held = 0, int debt = 0}) => jsonEncode({
  'walletBalance': {'currencyCode': 'TJS', 'minorUnits': wallet},
  'heldBalance': {'currencyCode': 'TJS', 'minorUnits': held},
  'debtBalance': {'currencyCode': 'TJS', 'minorUnits': debt},
  'activeSession': null,
});

FakeHttpClient _serve({String? visits, String? purchases, String? receipt, String? dashboard}) =>
    FakeHttpClient((request) {
      final path = request.url.path;
      if (path.endsWith('/receipt')) {
        return (receipt ?? '{"error":"missing"}', receipt == null ? 404 : 200);
      }
      if (path == '/api/me/dashboard') return (dashboard ?? _dashboard(), 200);
      if (path == '/api/me/visits') return (visits ?? _page([]), 200);
      if (path == '/api/me/purchases') return (purchases ?? _page([]), 200);
      return ('[]', 200);
    });

void main() {
  // Деньги и их движение — один вопрос игрока, поэтому и раздел один: остаток сверху,
  // визиты и покупки под ним.
  testWidgets('остаток стоит над списками трат', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(visits: _page([_visit()])))));
    await tester.pumpAndSettle();

    expect(find.text('Баланс кошелька'), findsOneWidget);
    expect(find.textContaining('200,50'), findsOneWidget);
    expect(
      tester.getTopLeft(find.textContaining('200,50')).dy,
      lessThan(tester.getTopLeft(find.text('PC-07')).dy),
    );
  });

  testWidgets('пополнение доступно прямо из раздела', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve())));
    await tester.pumpAndSettle();

    expect(find.text('Пополнить'), findsOneWidget);
  });

  // Сбой сети на балансе не должен уносить с собой списки: у них своя загрузка и свои
  // сообщения об ошибке.
  testWidgets('не загрузившийся остаток не ломает списки', (tester) async {
    final http = FakeHttpClient(
      (request) => switch (request.url.path) {
        '/api/me/dashboard' => ('{"error":"boom"}', 500),
        '/api/me/visits' => (_page([_visit()]), 200),
        _ => ('[]', 200),
      },
    );
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('PC-07'), findsOneWidget);
  });

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
      if (request.url.path == '/api/me/dashboard') return (_dashboard(), 200);
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

  // Лишнее касание «показать ещё» на телефоне ничего не решает: следующая страница
  // подтягивается сама у края списка.
  testWidgets('следующая страница подтягивается сама', (tester) async {
    final http = FakeHttpClient((request) {
      if (request.url.path == '/api/me/dashboard') return (_dashboard(), 200);
      if (request.url.path != '/api/me/visits') return (_page([]), 200);
      return request.url.queryParameters['cursor'] == null
          ? (_page([_visit(seat: 'PC-01')], next: 'c2'), 200)
          : (_page([_visit(id: 's2', seat: 'PC-02')]), 200);
    });
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    await tester.pumpAndSettle();

    expect(find.text('PC-01'), findsOneWidget);
    expect(find.text('PC-02'), findsOneWidget);
  });

  // Молча зациклить запрос на ошибке хуже, чем показать кнопку: игрок сам решает, повторять
  // ли, и видит, что список неполон.
  testWidgets('сорвавшаяся подгрузка предлагает повтор, а не крутится вечно', (tester) async {
    var attempt = 0;
    final http = FakeHttpClient((request) {
      if (request.url.path == '/api/me/dashboard') return (_dashboard(), 200);
      if (request.url.path != '/api/me/visits') return (_page([]), 200);
      if (request.url.queryParameters['cursor'] == null) {
        return (_page([_visit(seat: 'PC-01')], next: 'c2'), 200);
      }
      return ++attempt == 1
          ? ('{"error":"boom"}', 500)
          : (_page([_visit(id: 's2', seat: 'PC-02')]), 200);
    });
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('PC-01'), findsOneWidget);
    expect(find.textContaining('Не удалось подгрузить'), findsOneWidget);

    await tester.tap(find.textContaining('Не удалось подгрузить'));
    await tester.pumpAndSettle();

    expect(find.text('PC-02'), findsOneWidget);
  });

  // Жест, выученный на главной, должен работать и в списках.
  testWidgets('список обновляется потягиванием вниз', (tester) async {
    final http = _serve(visits: _page([_visit(seat: 'PC-01')]));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    final before = http.paths.where((path) => path == '/api/me/visits').length;

    await tester.fling(find.byType(RefreshIndicator).first, const Offset(0, 300), 1000);
    await tester.pumpAndSettle();

    expect(http.paths.where((path) => path == '/api/me/visits').length, greaterThan(before));
  });

  // Кнопка чека обещает то, чего может не быть, — поэтому её нет там, где чека нет.
  testWidgets('у визита без чека кнопки чека нет', (tester) async {
    await tester.pumpWidget(
      harness(clientWith(_serve(visits: _page([_visit(hasReceipt: false)])))),
    );
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
        {
          'productName': 'Кола',
          'quantity': 1,
          'unitPriceMinorUnits': 1500,
          'lineTotalMinorUnits': 1500,
        },
      ],
      'posTotalMinorUnits': 1500,
      'grandTotalMinorUnits': 4500,
      'currencyCode': 'TJS',
    });
    await tester.pumpWidget(
      harness(clientWith(_serve(visits: _page([_visit()]), receipt: receipt))),
    );
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
    final failing = FakeHttpClient(
      (request) => switch (request.url.path) {
        final path when path.endsWith('/receipt') => ('{"error":"boom"}', 500),
        '/api/me/dashboard' => (_dashboard(), 200),
        _ => (_page([_visit()]), 200),
      },
    );
    await tester.pumpWidget(harness(clientWith(failing)));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Чек →'));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось загрузить чек.'), findsOneWidget);
  });
}
