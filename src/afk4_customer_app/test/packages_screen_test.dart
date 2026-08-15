import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/packages/packages_screen.dart';

import 'support/fake_http.dart';

const _branchId = 'b1';
final _now = DateTime(2026, 8, 15, 12);

String _offersJson() => jsonEncode([
      {
        'packageDefinitionId': 'pkg1',
        'name': 'Ночной 5ч',
        'priceMinorUnits': 40000,
        'currencyCode': 'TJS',
        'includedSeconds': 18000,
        'bonusSeconds': 1800,
        'expiresAfterDays': 30,
      },
      {
        'packageDefinitionId': 'pkg2',
        'name': 'Дневной 2ч',
        'priceMinorUnits': 20000,
        'currencyCode': 'TJS',
        'includedSeconds': 7200,
        'bonusSeconds': 0,
        'expiresAfterDays': 0,
      },
    ]);

Map<String, dynamic> _mine({
  String id = 'own1',
  String name = 'Ночной 5ч',
  int remainingIncluded = 9000,
  int remainingBonus = 0,
  String? expiresAtUtc,
}) =>
    {
      'playerPackageId': id,
      'packageDefinitionId': 'pkg1',
      'playerAccountId': 'player1',
      'name': name,
      'purchasedPrice': {'currencyCode': 'TJS', 'minorUnits': 40000},
      'includedSeconds': 18000,
      'bonusSeconds': 1800,
      'remainingIncludedSeconds': remainingIncluded,
      'remainingBonusSeconds': remainingBonus,
      'purchasedAtUtc': '2026-08-01T10:00:00Z',
      'expiresAtUtc': expiresAtUtc ?? '2026-09-01T10:00:00Z',
    };

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: PackagesScreen(api: api, branchId: _branchId, clock: () => _now),
    );

PlayerApiClient clientWith(FakeHttpClient http) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: http);

FakeHttpClient _serve({
  String offers = '[]',
  String mine = '[]',
  (String, int)? purchase,
}) =>
    FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/branches/$_branchId/packages' => (offers, 200),
          '/api/me/packages' => (mine, 200),
          '/api/me/branches/$_branchId/packages/pkg1/purchase' => purchase ?? ('{}', 500),
          _ => ('[]', 200),
        });

void main() {
  testWidgets('витрина показывает цену, часы и бонус', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(offers: _offersJson()))));
    await tester.pumpAndSettle();

    expect(find.text('Ночной 5ч'), findsOneWidget);
    expect(find.textContaining('400,00'), findsOneWidget);
    expect(find.text('5 ч'), findsOneWidget);
    expect(find.textContaining('+30 мин в подарок'), findsOneWidget);
  });

  // Бонуса нет — строки о нём быть не должно: «+0 в подарок» выглядит как насмешка.
  testWidgets('пакет без бонуса о нём молчит', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(offers: _offersJson()))));
    await tester.pumpAndSettle();

    expect(find.textContaining('+0'), findsNothing);
  });

  testWidgets('клуб без пакетов объясняется словами, а не пустотой', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve())));
    await tester.pumpAndSettle();

    expect(find.text('Клуб не продаёт пакеты часов'), findsOneWidget);
  });

  // Деньги списываются молча только у мошенников: сумма и время стоят в вопросе.
  testWidgets('покупка спрашивает подтверждение с суммой и временем', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(offers: _offersJson()))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Купить').first);
    await tester.pumpAndSettle();

    expect(find.textContaining('400,00'), findsWidgets);
    expect(find.textContaining('5 ч 30 мин'), findsOneWidget);
  });

  testWidgets('отказ в подтверждении ничего не покупает', (tester) async {
    var purchaseCalls = 0;
    final http = FakeHttpClient((request) {
      if (request.url.path.endsWith('/purchase')) purchaseCalls++;
      return switch (request.url.path) {
        '/api/me/branches/$_branchId/packages' => (_offersJson(), 200),
        '/api/me/packages' => ('[]', 200),
        _ => ('{}', 200),
      };
    });

    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Купить').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Отмена'));
    await tester.pumpAndSettle();

    expect(purchaseCalls, 0);
  });

  testWidgets('удачная покупка подтверждается и обновляет список', (tester) async {
    var purchased = false;
    final http = FakeHttpClient((request) {
      if (request.url.path.endsWith('/purchase')) {
        purchased = true;
        return (jsonEncode(_mine(remainingIncluded: 18000, remainingBonus: 1800)), 200);
      }
      return switch (request.url.path) {
        '/api/me/branches/$_branchId/packages' => (_offersJson(), 200),
        '/api/me/packages' => (purchased ? jsonEncode([_mine()]) : '[]', 200),
        _ => ('{}', 200),
      };
    });

    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Купить').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Купить').last);
    await tester.pumpAndSettle();

    expect(find.textContaining('куплен'), findsOneWidget);
    expect(find.text('Мои пакеты'), findsOneWidget);
    expect(find.text('Осталось 2 ч 30 мин'), findsOneWidget);
  });

  // Отказ по деньгам обязан быть назван словами: «не получилось» отправляет игрока гадать.
  testWidgets('нехватка денег названа своей причиной', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(
      offers: _offersJson(),
      purchase: ('{"error":"insufficient_funds"}', 409),
    ))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Купить').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Купить').last);
    await tester.pumpAndSettle();

    expect(find.text('На кошельке не хватает денег на этот пакет'), findsOneWidget);
  });

  // Потраченный и просроченный пакеты не исчезают: пропавшая покупка читается как пропажа денег.
  testWidgets('потраченные пакеты остаются в отдельном разделе', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(
      offers: _offersJson(),
      mine: jsonEncode([
        _mine(id: 'spent', name: 'Потраченный', remainingIncluded: 0),
        _mine(id: 'old', name: 'Просроченный', expiresAtUtc: '2026-08-01T10:00:00Z'),
      ]),
    ))));
    await tester.pumpAndSettle();

    expect(find.text('Использованные'), findsOneWidget);
    expect(find.text('Время потрачено'), findsOneWidget);
    expect(find.text('Срок вышел'), findsOneWidget);
    expect(find.text('Мои пакеты'), findsNothing);
  });
}
