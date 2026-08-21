import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/play/start_session_screen.dart';

import 'support/fake_http.dart';

Map<String, dynamic> _seat({
  required String id,
  required String name,
  bool available = true,
  String? reason,
}) =>
    {
      'seatId': id,
      'deviceId': 'device-$id',
      'seatName': name,
      'zoneName': 'Зал A',
      'isAvailable': available,
      'unavailableReason': reason,
    };

String _tariffsJson() => jsonEncode([
      {
        'tariffId': 't1',
        'tariffVersionId': 'v1',
        'name': 'Дневной',
        'tariffRuleVersionId': 'v1',
        'versionNumber': 1,
        'currencyCode': 'TJS',
        'pricePerMinuteMinorUnits': 25,
        'minimumBillableMinutes': 0,
        'roundingIncrementMinutes': 1,
        'effectiveFromUtc': '2026-01-01T00:00:00Z',
      },
    ]);

String _quoteJson({int amount = 1500}) => jsonEncode({
      'tariffVersionId': 'v1',
      'tariffName': 'Дневной',
      'requestedMinutes': 60,
      'billableMinutes': 60,
      'amountMinorUnits': amount,
      'currencyCode': 'TJS',
    });

FakeHttpClient _serve({String seats = '[]', (String, int)? start, String? tariffs}) =>
    FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/branches/branch-1/seats' => (seats, 200),
          '/api/me/branches/branch-1/tariffs' => (tariffs ?? _tariffsJson(), 200),
          '/api/me/reservations/quote' => (_quoteJson(), 200),
          '/api/me/sessions/start' => start ?? ('{}', 500),
          _ => ('[]', 200),
        });

Widget harness(FakeHttpClient http, {void Function(String?)? onClosed}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () async {
              final result = await Navigator.of(context).push<String>(
                MaterialPageRoute(
                  builder: (_) => StartSessionScreen(
                    api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
                    branchId: 'branch-1',
                  ),
                ),
              );
              onClosed?.call(result);
            },
            child: const Text('открыть'),
          ),
        ),
      ),
    );

Future<void> open(WidgetTester tester) async {
  await tester.tap(find.text('открыть'));
  await tester.pumpAndSettle();
}

void main() {
  // Клуб не завёл цены — начать игру из приложения нельзя. Раньше блок тарифов просто исчезал,
  // а кнопка оставалась серой: экран выглядел сломанным, хотя дело в настройках клуба.
  testWidgets('без тарифов экран объясняет, почему играть нельзя', (tester) async {
    final http = _serve(seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]), tariffs: '[]');
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.text('Клуб пока не назначил цены'), findsOneWidget);
    expect(find.textContaining('администратор посадит вас за ПК'), findsOneWidget);
    // Кнопки старта нет вовсе: серая кнопка без объяснения — та же поломка, что и была.
    expect(find.byType(FilledButton), findsNothing);
  });

  testWidgets('свободные и занятые места видны вместе, с причиной занятости', (tester) async {
    final http = _serve(
      seats: jsonEncode([
        _seat(id: 's1', name: 'PC-01'),
        _seat(id: 's2', name: 'PC-02', available: false, reason: 'session'),
        _seat(id: 's3', name: 'PC-03', available: false, reason: 'offline'),
      ]),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.text('PC-01'), findsOneWidget);
    expect(find.text('PC-02'), findsOneWidget);
    expect(find.text('Занято'), findsOneWidget);
    expect(find.text('ПК выключен'), findsOneWidget);
  });

  testWidgets('цена времени спрашивается у сервера и стоит на кнопке', (tester) async {
    final http = _serve(seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]));
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(http.paths, contains('/api/me/reservations/quote'));
    expect(find.textContaining('Начать за'), findsOneWidget);
    expect(find.textContaining('15,00'), findsOneWidget);
  });

  // Играют сию секунду — значит и тариф нужен действующий сию секунду. Тариф вне своих часов не
  // прячется: пропавший из списка «Утренний» читается как сбой. Но и выбрать его нельзя, иначе
  // человек жмёт «Начать» и получает отказ сервера, так и не поняв, при чём тут утро.
  testWidgets('тариф вне своих часов назван, но не выбирается', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      tariffs: jsonEncode([
        {
          'tariffId': 't1', 'tariffVersionId': 'v1', 'name': 'Утренний',
          'tariffRuleVersionId': 'v1', 'versionNumber': 1, 'currencyCode': 'TJS',
          'pricePerMinuteMinorUnits': 25, 'minimumBillableMinutes': 0,
          'roundingIncrementMinutes': 1, 'effectiveFromUtc': '2026-01-01T00:00:00Z',
          'appliesOnDaysMask': 0, 'appliesFromMinuteOfDay': 480, 'appliesToMinuteOfDay': 960,
          'appliesNow': false,
        },
      ]),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.textContaining('Утренний'), findsOneWidget);
    expect(find.textContaining('08:00–16:00'), findsOneWidget);
    expect(find.textContaining('Сейчас недоступен'), findsOneWidget);

    // На экране есть и другие чипы (места, длительность) — берём именно тарифный.
    final chip = tester.widget<ChoiceChip>(find.ancestor(
      of: find.textContaining('Утренний'),
      matching: find.byType(ChoiceChip),
    ));
    expect(chip.onSelected, isNull);
  });

  testWidgets('старт уходит с выбранным ПК, тарифом и временем', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01'), _seat(id: 's2', name: 'PC-02')]),
      start: ('{}', 200),
    );
    String? closedWith;
    await tester.pumpWidget(harness(http, onClosed: (result) => closedWith = result));
    await open(tester);

    await tester.tap(find.text('PC-02'));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    final started = http.bodies.firstWhere((body) => body.containsKey('deviceId'));
    expect(started['deviceId'], 'device-s2');
    expect(started['tariffRuleVersionId'], 'v1');
    expect(started['durationMinutes'], 60);
    // Платное действие — ключ идемпотентности защищает от двойного списания.
    expect(started['idempotencyKey'], isA<String>());
    // Экран возвращает имя места: игроку, стоящему посреди зала, нужно знать, куда садиться.
    expect(closedWith, 'PC-02');
  });

  testWidgets('нехватка денег названа своей причиной', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      start: ('{"error":"insufficient_balance"}', 409),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    expect(find.textContaining('не хватает денег'), findsOneWidget);
  });

  // Место могли занять за те секунды, что игрок выбирал: об этом надо сказать прямо.
  testWidgets('занятое за секунду до старта место просит выбрать другое', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      start: ('{"error":"seat_busy"}', 409),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    expect(find.textContaining('только что заняли'), findsOneWidget);
  });

  testWidgets('когда мест нет, экран говорит что делать, а не молчит', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01', available: false, reason: 'session')]),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.text('Свободных мест нет'), findsOneWidget);
    expect(find.textContaining('стойке'), findsOneWidget);
    // Кнопки старта нет: нажимать её было бы не по чему.
    expect(find.textContaining('Начать за'), findsNothing);
  });

  // Выключенная кнопка без объяснения читается как поломка приложения.
  testWidgets('пока тариф не выбран, кнопка говорит чего ждёт', (tester) async {
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/branches/branch-1/seats' =>
            (jsonEncode([_seat(id: 's1', name: 'PC-01')]), 200),
          // Два тарифа — предвыбирать нечего, игрок должен выбрать сам.
          '/api/me/branches/branch-1/tariffs' => (
              jsonEncode([
                ...jsonDecode(_tariffsJson()) as List,
                {
                  'tariffId': 't2',
                  'tariffVersionId': 'v2',
                  'name': 'Ночной',
                  'tariffRuleVersionId': 'v2',
                  'versionNumber': 1,
                  'currencyCode': 'TJS',
                  'pricePerMinuteMinorUnits': 15,
                  'minimumBillableMinutes': 0,
                  'roundingIncrementMinutes': 1,
                  'effectiveFromUtc': '2026-01-01T00:00:00Z',
                },
              ]),
              200
            ),
          _ => ('[]', 200),
        });
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.text('Выберите тариф'), findsOneWidget);

    await tester.tap(find.text('Ночной'));
    await tester.pumpAndSettle();

    expect(find.text('Выберите тариф'), findsNothing);
  });

  testWidgets('сбой загрузки мест не выдаётся за пустой зал', (tester) async {
    final http = FakeHttpClient((_) => ('{"error":"boom"}', 500));
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.text('Не удалось загрузить места'), findsOneWidget);
    expect(find.text('Свободных мест нет'), findsNothing);
  });
}
