import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/loyalty/loyalty_screen.dart';

import 'support/fake_http.dart';

String _loyaltyJson({
  bool topUp = true,
  int topUpBp = 500,
  bool shop = false,
  int shopBp = 0,
  bool session = false,
  int sessionBp = 0,
  int total = 4500,
  List<Map<String, dynamic>> recent = const [],
}) =>
    jsonEncode({
      'topUpEnabled': topUp,
      'topUpPercentBasisPoints': topUpBp,
      'shopEnabled': shop,
      'shopPercentBasisPoints': shopBp,
      'sessionEnabled': session,
      'sessionPercentBasisPoints': sessionBp,
      'totalEarned': {'currencyCode': 'TJS', 'minorUnits': total},
      'recent': recent,
    });

Map<String, dynamic> _entry({String reason = 'cashback:topup', int amount = 2500}) => {
      'amountMinorUnits': amount,
      'currencyCode': 'TJS',
      'reason': reason,
      'createdAtUtc': '2026-08-12T10:00:00Z',
    };

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: LoyaltyScreen(api: api),
    );

PlayerApiClient clientWith(String body, {int status = 200}) => PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: FakeHttpClient((_) => (body, status)),
    );

void main() {
  test('базисные пункты превращаются в проценты без лишней дроби', () {
    expect(formatBasisPoints(500, locale: 'ru'), '5%');
    expect(formatBasisPoints(250, locale: 'ru'), '2,5%');
    expect(formatBasisPoints(250, locale: 'en'), '2.5%');
  });

  testWidgets('накопленное показывается суммой, а не количеством баллов', (tester) async {
    await tester.pumpWidget(harness(clientWith(_loyaltyJson())));
    await tester.pumpAndSettle();

    expect(find.text('Накоплено кешбэком'), findsOneWidget);
    expect(find.textContaining('45,00'), findsOneWidget);
  });

  // Правила — единственный ответ на вопрос «а сколько мне вернётся». Показываются только
  // включённые: строка «0% с заказов» ничего не объясняет.
  testWidgets('показываются только включённые правила начисления', (tester) async {
    await tester.pumpWidget(harness(clientWith(_loyaltyJson(shop: true, shopBp: 300))));
    await tester.pumpAndSettle();

    expect(find.text('5% с пополнения кошелька'), findsOneWidget);
    expect(find.text('3% с заказов в баре'), findsOneWidget);
    expect(find.textContaining('с оплаченного времени'), findsNothing);
  });

  // Главное о кешбэке: это не баллы, а деньги на кошельке.
  testWidgets('сказано, что кешбэк можно тратить', (tester) async {
    await tester.pumpWidget(harness(clientWith(_loyaltyJson())));
    await tester.pumpAndSettle();

    expect(find.textContaining('обычные деньги на кошельке'), findsOneWidget);
  });

  testWidgets('начисления подписаны источником, а не служебной строкой', (tester) async {
    await tester.pumpWidget(harness(clientWith(_loyaltyJson(recent: [
      _entry(),
      _entry(reason: 'cashback:shop:8f14e45f-ceea-467a-9575-28f6b3f0d0dd', amount: 700),
    ]))));
    await tester.pumpAndSettle();

    expect(find.text('Пополнение'), findsOneWidget);
    expect(find.text('Заказ в баре'), findsOneWidget);
    expect(find.textContaining('cashback:'), findsNothing);
    expect(find.textContaining('+'), findsWidgets);
  });

  testWidgets('пустая история объясняет, откуда возьмётся кешбэк', (tester) async {
    await tester.pumpWidget(harness(clientWith(_loyaltyJson())));
    await tester.pumpAndSettle();

    expect(find.text('Кешбэк ещё не начислялся'), findsOneWidget);
    expect(find.textContaining('после первого пополнения'), findsOneWidget);
  });

  // Клуб мог включить лояльность и оставить нули — обещать процент в этом случае нельзя.
  testWidgets('клуб без начислений честно об этом говорит', (tester) async {
    await tester.pumpWidget(harness(clientWith(_loyaltyJson(topUp: false, topUpBp: 0))));
    await tester.pumpAndSettle();

    expect(find.text('Клуб пока не начисляет кешбэк'), findsOneWidget);
    expect(find.textContaining('с пополнения кошелька'), findsNothing);
  });

  testWidgets('сбой загрузки не притворяется нулевым кешбэком', (tester) async {
    await tester.pumpWidget(harness(clientWith('{"error":"boom"}', status: 500)));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось загрузить кешбэк'), findsOneWidget);
    expect(find.textContaining('0,00'), findsNothing);
  });
}
