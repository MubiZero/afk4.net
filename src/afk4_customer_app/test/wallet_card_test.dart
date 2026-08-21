import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/dto.dart';
import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/wallet/wallet_card.dart';

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
  String currencyCode = 'TJS',
  int wallet = 120050,
  int held = 0,
  int debt = 0,
}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: WalletCard(
          api: api,
          walletBalance: Money(currencyCode: currencyCode, minorUnits: wallet),
          heldBalance: Money(currencyCode: currencyCode, minorUnits: held),
          debtBalance: Money(currencyCode: currencyCode, minorUnits: debt),
          phoneVerified: phoneVerified,
          features: features,
        ),
      ),
    );

PlayerApiClient clientWith(FakeHttpClient inner) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

/// Открывает лист пополнения — форма живёт там, а не на главной.
Future<void> openTopUp(WidgetTester tester) async {
  await tester.tap(find.text('Пополнить'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('баланс виден сразу, а форма ввода не занимает экран', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200)))));
    await tester.pumpAndSettle();

    expect(find.textContaining('200,50'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
    expect(find.text('Пополнить'), findsOneWidget);
  });

  testWidgets('долг показывается только когда он есть и говорит, где его закрыть', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200)))));
    await tester.pumpAndSettle();
    expect(find.textContaining('Долг'), findsNothing);

    await tester.pumpWidget(
      harness(clientWith(FakeHttpClient((_) => ('[]', 200))), debt: 5000),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('Долг'), findsOneWidget);
    // Пополнение кошелька долг не закрывает — гасят его на кассе, и обещать иное нельзя.
    expect(find.text('Погасить долг можно на стойке клуба'), findsOneWidget);
  });

  testWidgets('заявка уходит на сервер в минорных единицах', (tester) async {
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? (jsonEncode(_intent()), 200) : ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await openTopUp(tester);
    await tester.enterText(find.byType(TextField), '12,50');
    await tester.tap(find.text('Запросить'));
    await tester.pumpAndSettle();

    expect(http.bodies.single['amountMinorUnits'], 1250);
    expect(find.text('Заявка на пополнение отправлена'), findsOneWidget);
  });

  // Валюта заявки бралась из константы «TJS», и клуб на других деньгах получал бы просьбу
  // зачислить чужую валюту.
  testWidgets('заявка идёт в валюте кошелька, а не в зашитой', (tester) async {
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? (jsonEncode(_intent()), 200) : ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http), currencyCode: 'USD'));
    await tester.pumpAndSettle();

    await openTopUp(tester);
    await tester.enterText(find.byType(TextField), '10');
    await tester.tap(find.text('Запросить'));
    await tester.pumpAndSettle();

    expect(http.bodies.single['currencyCode'], 'USD');
  });

  // Набирать «100» пальцем — лишнее трение там, где хватает одного касания.
  testWidgets('быстрая сумма подставляется одним касанием', (tester) async {
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? (jsonEncode(_intent()), 200) : ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await openTopUp(tester);
    await tester.tap(find.widgetWithText(ActionChip, '100,00 с.'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Запросить'));
    await tester.pumpAndSettle();

    expect(http.bodies.single['amountMinorUnits'], 10000);
  });

  // Ввод вроде `1e400` превращается в бесконечность, а та уезжает на сервер как null.
  // Потолок отсекает это до запроса.
  testWidgets('несуразная сумма не уходит на сервер', (tester) async {
    for (final input in ['0', '-5', 'abc', '1e400', '9999999999']) {
      final http = FakeHttpClient((_) => ('[]', 200));
      await tester.pumpWidget(harness(clientWith(http)));
      await tester.pumpAndSettle();

      await openTopUp(tester);
      await tester.enterText(find.byType(TextField), input);
      await tester.tap(find.text('Запросить'));
      await tester.pumpAndSettle();

      expect(http.bodies, isEmpty, reason: 'ввод «$input» не должен создавать заявку');
      expect(find.text('Введите сумму больше нуля'), findsOneWidget);
      await tester.pumpWidget(const SizedBox.shrink());
    }
  });

  // Гейт раньше отправлял к администратору клуба, у которого возможности подтвердить номер
  // тоже не было: дверь была заперта с обеих сторон.
  testWidgets('неподтверждённый телефон объясняет запрет и даёт выход', (tester) async {
    await tester.pumpWidget(
        harness(clientWith(FakeHttpClient((_) => ('[]', 200))), phoneVerified: false));
    await tester.pumpAndSettle();

    expect(find.text('Пополнить'), findsNothing);
    expect(find.textContaining('подтвердите свой номер телефона'), findsOneWidget);

    await tester.tap(find.text('Подтвердить номер'));
    await tester.pumpAndSettle();

    expect(find.text('Подтверждение номера'), findsOneWidget);
  });

  testWidgets('выключенное клубу пополнение прячет кнопку целиком', (tester) async {
    await tester.pumpWidget(
        harness(clientWith(FakeHttpClient((_) => ('[]', 200))), features: const []));
    await tester.pumpAndSettle();

    expect(find.text('Пополнить'), findsNothing);
    expect(find.textContaining('подтвердите номер телефона'), findsNothing);
  });

  // Список возможностей не загрузился — пополнение считается включённым. Спрятать кнопку
  // из-за сетевого сбоя значит соврать игроку, что возможности нет; запись всё равно
  // проверяет сервер.
  testWidgets('неизвестный список возможностей оставляет пополнение доступным', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200))), features: null));
    await tester.pumpAndSettle();

    expect(find.text('Пополнить'), findsOneWidget);
  });

  // Иначе игрок отправляет вторую заявку, забыв про первую.
  testWidgets('ожидающая заявка видна на самой карточке', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => (_intentListJson(), 200)))));
    await tester.pumpAndSettle();

    expect(find.textContaining('50,00'), findsOneWidget);
    expect(find.textContaining('ждёт зачисления'), findsOneWidget);
  });

  testWidgets('зачисленная заявка карточку не занимает', (tester) async {
    await tester.pumpWidget(harness(
        clientWith(FakeHttpClient((_) => (_intentListJson(state: 'fulfilled'), 200)))));
    await tester.pumpAndSettle();

    expect(find.textContaining('ждёт зачисления'), findsNothing);
  });

  testWidgets('заявки перечислены в листе суммой и состоянием', (tester) async {
    await tester.pumpWidget(
        harness(clientWith(FakeHttpClient((_) => (_intentListJson(expired: true), 200)))));
    await tester.pumpAndSettle();

    await openTopUp(tester);

    expect(find.text('Ваши заявки'), findsOneWidget);
    expect(find.text('Истекло'), findsOneWidget);
  });

  testWidgets('недоступный список заявок не ломает саму карточку', (tester) async {
    await tester
        .pumpWidget(harness(clientWith(FakeHttpClient((_) => ('{"error":"boom"}', 500)))));
    await tester.pumpAndSettle();

    expect(find.text('Пополнить'), findsOneWidget);
  });

  // Третье число кошелька: остаток уже без него, и без строки игрок видит только то, что
  // денег стало меньше, — а почему, не видит.
  testWidgets('придержанное под брони показывается отдельной строкой', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200))), held: 5000));
    await tester.pumpAndSettle();

    expect(find.textContaining('Придержано под брони'), findsOneWidget);
    expect(find.textContaining('50,00 с.'), findsOneWidget);
    expect(find.text('Вернётся на кошелёк, если бронь отменят.'), findsOneWidget);
  });

  // Строка «придержано 0» на главной — шум: она отвечает на вопрос, которого никто не задал.
  testWidgets('нулевое придержанное не показывается вовсе', (tester) async {
    await tester.pumpWidget(harness(clientWith(FakeHttpClient((_) => ('[]', 200)))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Придержано под брони'), findsNothing);
  });
}
