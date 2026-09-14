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
    // Деньги на кошельке есть, поэтому карточка предлагает закрыть долг отсюда, а не отправляет
    // к стойке. К стойке она отправляет только тогда, когда платить нечем — см. тест ниже.
    expect(find.text('Можно закрыть деньгами с кошелька или на стойке клуба.'), findsOneWidget);
  });

  testWidgets('заявка уходит на сервер в минорных единицах', (tester) async {
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? (jsonEncode(_intent()), 200) : ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await openTopUp(tester);
    await tester.enterText(find.byType(TextField), '12,50');
    await tester.tap(find.text('Внести на стойке'));
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
    await tester.tap(find.text('Внести на стойке'));
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
    await tester.tap(find.text('Внести на стойке'));
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
      await tester.tap(find.text('Внести на стойке'));
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

  // Отказаться от заявки было нельзя: она висела ожидающей сутки и всё это время отвечала «да»
  // на вопрос «я же пополнял».
  testWidgets('ожидающую заявку можно отменить', (tester) async {
    final methods = <String>[];
    var cancelled = false;
    final http = FakeHttpClient((request) {
      methods.add('${request.method} ${request.url.path}');
      if (request.method == 'DELETE') {
        cancelled = true;
        return (jsonEncode(_intent(state: 'cancelled')), 200);
      }
      return (cancelled ? _intentListJson(state: 'cancelled') : _intentListJson(), 200);
    });
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();
    expect(find.text('Отменить заявку'), findsOneWidget);

    await tester.tap(find.text('Отменить заявку'));
    await tester.pumpAndSettle();

    expect(methods, contains('DELETE /api/me/wallet/top-up-intents/i1'));
    expect(find.text('Отменить заявку'), findsNothing);
  });

  // Отменённая на стойке заявка ещё сутки висела у игрока как ожидающая: ждущей считалась любая,
  // кроме исполненной.
  testWidgets('отменённая заявка не показывается как ожидающая', (tester) async {
    final http = FakeHttpClient((_) => (_intentListJson(state: 'cancelled'), 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('Отменить заявку'), findsNothing);
  });

  testWidgets('исполненная заявка тоже не ожидающая', (tester) async {
    final http = FakeHttpClient((_) => (_intentListJson(state: 'fulfilled'), 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('Отменить заявку'), findsNothing);
  });

  // Долг игрок только видел, а надпись отправляла его на стойку — при том, что деньги лежали на
  // его же кошельке.
  testWidgets('долг гасится с кошелька, когда денег хватает', (tester) async {
    late final FakeHttpClient http;
    http = FakeHttpClient((request) {
      if (request.method == 'POST' && request.url.path.endsWith('/debt-payment')) {
        return (
          jsonEncode({
            'walletBalance': {'currencyCode': 'TJS', 'minorUnits': 7000},
            'heldBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
            'debtBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
          }),
          200
        );
      }
      return ('[]', 200);
    });
    await tester.pumpWidget(harness(clientWith(http), wallet: 10000, debt: 3000));
    await tester.pumpAndSettle();

    expect(find.text('Погасить 30,00 с.'), findsOneWidget);
    await tester.tap(find.text('Погасить 30,00 с.'));
    await tester.pumpAndSettle();

    expect(http.paths, contains('/api/me/wallet/debt-payment'));
    final sent = http.bodies.last;
    expect((sent['amount'] as Map<String, dynamic>)['minorUnits'], 3000);
    expect(sent['idempotencyKey'], isA<String>());
  });

  // Долг больше остатка — закрывается то, что есть. Иначе долг в две тысячи при тысяче на
  // кошельке нельзя было бы тронуть вовсе.
  testWidgets('когда денег меньше долга, предлагается закрыть остаток', (tester) async {
    final http = FakeHttpClient((_) => ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http), wallet: 1000, debt: 3000));
    await tester.pumpAndSettle();

    expect(find.text('Погасить 10,00 с.'), findsOneWidget);
  });

  // Пустой кошелёк: платить нечем, и обещать кнопкой нечего — остаётся стойка.
  testWidgets('без денег на кошельке кнопки гашения нет', (tester) async {
    final http = FakeHttpClient((_) => ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http), wallet: 0, debt: 3000));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(OutlinedButton, 'Погасить 0,00 с.'), findsNothing);
    expect(find.text('Погасить долг можно на стойке клуба'), findsOneWidget);
  });

  testWidgets('без долга кнопки гашения нет', (tester) async {
    final http = FakeHttpClient((_) => ('[]', 200));
    await tester.pumpWidget(harness(clientWith(http), wallet: 10000, debt: 0));
    await tester.pumpAndSettle();

    expect(find.byType(OutlinedButton), findsNothing);
    expect(find.textContaining('Погасить'), findsNothing);
  });
}
