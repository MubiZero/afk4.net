import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/shop/shop_screen.dart';

import 'support/fake_http.dart';

String _catalogJson() => jsonEncode([
      {
        'productId': 'p1',
        'name': 'Кола',
        'sku': 'COLA',
        'price': {'currencyCode': 'TJS', 'minorUnits': 1200},
        'stockOnHand': 40,
      },
      {
        'productId': 'p2',
        'name': 'Бургер',
        'sku': 'BURG',
        'price': {'currencyCode': 'TJS', 'minorUnits': 3500},
        'stockOnHand': 2,
      },
    ]);

Map<String, dynamic> _order({String status = 'placed', int total = 1200}) => {
      'id': 'o1',
      'status': status,
      'total': {'currencyCode': 'TJS', 'minorUnits': total},
      'lines': [
        {
          'productId': 'p1',
          'name': 'Кола',
          'unitPrice': {'currencyCode': 'TJS', 'minorUnits': 1200},
          'quantity': 1,
          'lineTotal': {'currencyCode': 'TJS', 'minorUnits': 1200},
        },
      ],
    };

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ShopScreen(api: api),
    );

PlayerApiClient clientWith(FakeHttpClient http) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: http);

/// Снимает экран: у заказа есть опрос статуса, и оставленный таймер валит тест.
Future<void> unmount(WidgetTester tester) => tester.pumpWidget(const SizedBox.shrink());

FakeHttpClient _serve({
  String catalog = '[]',
  String orders = '[]',
  (String, int)? place,
  (String, int)? cancel,
}) =>
    FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/shop/catalog' => (catalog, 200),
          '/api/me/shop/orders' =>
            request.method == 'POST' ? (place ?? ('{}', 500)) : (orders, 200),
          '/api/me/shop/orders/o1/cancel' => cancel ?? ('{}', 500),
          _ => ('[]', 200),
        });

void main() {
  testWidgets('меню показывает позиции и цены', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(catalog: _catalogJson()))));
    await tester.pumpAndSettle();

    expect(find.text('Кола'), findsOneWidget);
    expect(find.textContaining('12,00'), findsOneWidget);
    await unmount(tester);
  });

  // «Осталось 40 штук» не меняет решение, «осталось 2» — меняет.
  testWidgets('о последних штуках предупреждают, о полном складе молчат', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(catalog: _catalogJson()))));
    await tester.pumpAndSettle();

    expect(find.textContaining('осталось 2 штуки'), findsOneWidget);
    expect(find.textContaining('40'), findsNothing);
    await unmount(tester);
  });

  // Выключенная кнопка без объяснения — тупик: игрок не понимает, чего от него хотят.
  testWidgets('пустая корзина объясняет себя вместо немой кнопки', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(catalog: _catalogJson()))));
    await tester.pumpAndSettle();

    expect(find.text('Выберите, что принести'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('добавление в корзину показывает сумму на кнопке', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(catalog: _catalogJson()))));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add).first);
    await tester.pumpAndSettle();

    expect(find.textContaining('Заказать за'), findsOneWidget);
    expect(find.textContaining('12,00'), findsWidgets);
    await unmount(tester);
  });

  testWidgets('заказ уходит на сервер и сменяется его состоянием', (tester) async {
    final http = _serve(catalog: _catalogJson(), place: (jsonEncode(_order()), 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add).first);
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Заказать за'));
    await tester.pumpAndSettle();

    expect(http.bodies.single['lines'], [
      {'productId': 'p1', 'quantity': 1}
    ]);
    // Как и продление, заказ платный: повтор потерянного ответа не должен списать дважды.
    expect(http.bodies.single['idempotencyKey'], isA<String>());
    expect(find.text('Заказ принят, готовим'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('нехватка денег названа своей причиной', (tester) async {
    final http = _serve(
      catalog: _catalogJson(),
      place: ('{"error":"insufficient_funds"}', 409),
    );
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add).first);
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Заказать за'));
    await tester.pumpAndSettle();

    expect(find.textContaining('не хватает денег'), findsOneWidget);
    // Корзина цела: игрок пополнит кошелёк и повторит, а не соберёт заказ заново.
    expect(find.textContaining('Заказать за'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('кончившийся товар не выдаётся за общий сбой', (tester) async {
    final http = _serve(catalog: _catalogJson(), place: ('{"error":"out_of_stock"}', 409));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add).first);
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Заказать за'));
    await tester.pumpAndSettle();

    expect(find.text('Товар только что закончился'), findsOneWidget);
    await unmount(tester);
  });

  // Заказ мог быть оформлен раньше — с этого телефона или из лаунчера за ПК.
  testWidgets('незакрытый заказ показывается вместо меню при открытии', (tester) async {
    final http = _serve(catalog: _catalogJson(), orders: jsonEncode([_order(status: 'accepted')]));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('Оператор несёт заказ'), findsOneWidget);
    expect(find.text('Кола'), findsNothing);
    await unmount(tester);
  });

  testWidgets('состояние заказа обновляется само, без действий игрока', (tester) async {
    var orderCalls = 0;
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/shop/catalog' => (_catalogJson(), 200),
          '/api/me/shop/orders' => (
              jsonEncode([_order(status: ++orderCalls == 1 ? 'placed' : 'delivered')]),
              200
            ),
          _ => ('[]', 200),
        });

    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    expect(find.text('Заказ принят, готовим'), findsOneWidget);

    await tester.pump(const Duration(seconds: 6));
    await tester.pumpAndSettle();

    expect(find.text('Заказ принесли'), findsOneWidget);
    // Принесённый заказ — не тупик: из него можно заказать ещё.
    expect(find.text('Заказать ещё'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('заказ можно отменить, пока его не принесли', (tester) async {
    final http = _serve(
      catalog: _catalogJson(),
      orders: jsonEncode([_order()]),
      cancel: (jsonEncode(_order(status: 'cancelled')), 200),
    );
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Отменить заказ'));
    await tester.pumpAndSettle();

    expect(find.text('Заказ отменён'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('пустое меню объясняет, что делать, а не молчит', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve())));
    await tester.pumpAndSettle();

    expect(find.text('Сейчас заказывать нечего'), findsOneWidget);
    expect(find.textContaining('Спросите на стойке'), findsOneWidget);
    await unmount(tester);
  });
}
