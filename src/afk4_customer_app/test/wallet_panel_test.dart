import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/wallet/wallet_panel.dart';

import 'support/fake_http.dart';

Map<String, dynamic> _intent({String state = 'pending', bool expired = false, int amount = 5000}) => {
      'paymentIntentId': 'i1',
      'amountMinorUnits': amount,
      'currencyCode': 'TJS',
      'state': state,
      'purpose': 'wallet_topup',
      'method': 'cash',
      'createdAtUtc': '2026-08-12T10:00:00Z',
      'fulfilledAtUtc': null,
      'isExpired': expired,
    };

String _intentListJson({String state = 'pending', bool expired = false}) =>
    jsonEncode([_intent(state: state, expired: expired)]);

Widget harness(
  PlayerApiClient api, {
  bool phoneVerified = true,
  List<String>? features = const ['online_topup'],
}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: WalletPanel(api: api, phoneVerified: phoneVerified, features: features),
      ),
    );

PlayerApiClient clientWith(FakeHttpClient inner) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

void main() {
  testWidgets('заявка уходит на сервер в минорных единицах', (tester) async {
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? (jsonEncode(_intent()), 200) : ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), '12,50');
    await tester.tap(find.text('Запросить'));
    await tester.pumpAndSettle();

    expect(http.bodies.single['amountMinorUnits'], 1250);
    expect(http.bodies.single['currencyCode'], 'TJS');
  });

  // Ввод вроде `1e400` превращается в бесконечность, а та уезжает на сервер как null.
  // Потолок отсекает это до запроса.
  testWidgets('несуразная сумма не уходит на сервер', (tester) async {
    for (final input in ['0', '-5', 'abc', '1e400', '9999999999']) {
      final http = FakeHttpClient((_) => ('[]', 200));
      await tester.pumpWidget(harness(clientWith(http)));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), input);
      await tester.tap(find.text('Запросить'));
      await tester.pumpAndSettle();

      expect(http.bodies, isEmpty, reason: 'ввод «$input» не должен создавать заявку');
      expect(find.text('Введите сумму больше нуля'), findsOneWidget);
      await tester.pumpWidget(const SizedBox.shrink());
    }
  });

  testWidgets('неподтверждённый телефон объясняет, почему пополнить нельзя', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200))), phoneVerified: false));
    await tester.pumpAndSettle();

    expect(find.byType(TextField), findsNothing);
    expect(find.textContaining('подтвердите номер телефона'), findsOneWidget);
  });

  testWidgets('выключенное клубу пополнение прячет форму целиком', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200))), features: const []));
    await tester.pumpAndSettle();

    expect(find.byType(TextField), findsNothing);
    expect(find.textContaining('подтвердите номер телефона'), findsNothing);
  });

  // Список возможностей не загрузился — пополнение считается включённым. Спрятать кнопку
  // из-за сетевого сбоя значит соврать игроку, что возможности нет; запись всё равно
  // проверяет сервер.
  testWidgets('неизвестный список возможностей оставляет пополнение доступным', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200))), features: null));
    await tester.pumpAndSettle();

    expect(find.byType(TextField), findsOneWidget);
  });

  testWidgets('заявки показываются суммой и состоянием', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => (_intentListJson(state: 'fulfilled'), 200)))));
    await tester.pumpAndSettle();

    expect(find.textContaining('50,00'), findsOneWidget);
    expect(find.text('Зачислено'), findsOneWidget);
  });

  testWidgets('истёкшая заявка помечается истёкшей, а не ожидающей', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => (_intentListJson(expired: true), 200)))));
    await tester.pumpAndSettle();

    expect(find.text('Истекло'), findsOneWidget);
  });

  testWidgets('недоступный список заявок не ломает саму панель', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('{"error":"boom"}', 500)))));
    await tester.pumpAndSettle();

    expect(find.byType(TextField), findsOneWidget);
  });
}
