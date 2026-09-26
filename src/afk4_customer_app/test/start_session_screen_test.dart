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

Widget harness(FakeHttpClient http, {void Function(String?)? onClosed, bool? pinSet}) => MaterialApp(
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
                    pinSet: pinSet,
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

  // Список мест перестал быть выбором: машину называет её собственный монитор. Но «есть ли
  // вообще куда сесть» — вопрос живой, и ответ на него остаётся.
  testWidgets('экран говорит, сколько мест свободно', (tester) async {
    final http = _serve(
      seats: jsonEncode([
        _seat(id: 's1', name: 'PC-01'),
        _seat(id: 's2', name: 'PC-02', available: false, reason: 'session'),
        _seat(id: 's3', name: 'PC-03', available: false, reason: 'offline'),
      ]),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.textContaining('Свободных мест: 1'), findsOneWidget);
    // Выбирать место больше нечем: клик по «PC-01» ничего не значит и его на экране нет.
    expect(find.text('PC-01'), findsNothing);
  });

  testWidgets('цена времени спрашивается у сервера и стоит на кнопке', (tester) async {
    final http = _serve(seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]));
    await tester.pumpWidget(harness(http));
    await open(tester);
    // Цену сервер считает сразу, а кнопка ждёт код: без него начинать нечего.
    await tester.enterText(find.byType(TextField), '482913');
    await tester.pumpAndSettle();

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

  // Код с монитора — доказательство, что человек стоит перед этой машиной. Раньше здесь уходил
  // идентификатор устройства, выбранный из списка, и занять ПК можно было не приходя в клуб.
  testWidgets('старт уходит с кодом, тарифом и временем', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01'), _seat(id: 's2', name: 'PC-02')]),
      start: ('{"session":{"seatId":"s2"}}', 200),
    );
    String? closedWith;
    await tester.pumpWidget(harness(http, onClosed: (result) => closedWith = result));
    await open(tester);

    await tester.enterText(find.byType(TextField), '482913');
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    final started = http.bodies.firstWhere((body) => body.containsKey('seatingCode'));
    expect(started['seatingCode'], '482913');
    expect(started['tariffRuleVersionId'], 'v1');
    expect(started['durationMinutes'], 60);
    // Платное действие — ключ идемпотентности защищает от двойного списания.
    expect(started['idempotencyKey'], isA<String>());
    // Имя места подтверждает, что человек не ошибся монитором.
    expect(closedWith, 'PC-02');
  });

  // Код не подошёл — это не общий сбой: он истёк, набран с чужого экрана или с опечаткой, и
  // человеку надо сказать, куда смотреть.
  testWidgets('не подошедший код объясняется своими словами', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      start: ('{"error":"seating_code_invalid"}', 400),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.enterText(find.byType(TextField), '000000');
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Код не подошёл'), findsOneWidget);
  });

  /// ПК показывает код, но к платформе не привязан — сам игрок этого не исправит. Общее
  /// «попробуйте ещё раз» отправляло его набирать те же цифры до бесконечности.
  testWidgets('непривязанный ПК зовёт администратора, а не повтор', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      start: ('{"error":"device_not_assigned"}', 404),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.enterText(find.byType(TextField), '482913');
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    expect(find.textContaining('позовите администратора'), findsOneWidget);
    expect(find.textContaining('Попробуйте ещё раз'), findsNothing);
  });

  testWidgets('нехватка денег названа своей причиной', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      start: ('{"error":"insufficient_balance"}', 409),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.enterText(find.byType(TextField), '482913');
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    expect(find.textContaining('не хватает денег'), findsOneWidget);
  });

  // Клуб увёл ПК на обслуживание: это не «место заняли», и сказать надо именно это.
  testWidgets('ПК на обслуживании назван своей причиной', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      start: ('{"error":"device_in_maintenance"}', 409),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.enterText(find.byType(TextField), '482913');
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Начать за'));
    await tester.pumpAndSettle();

    expect(find.textContaining('на обслуживании'), findsOneWidget);
    expect(find.textContaining('только что заняли'), findsNothing);
  });

  // Место могли занять за те секунды, что игрок выбирал: об этом надо сказать прямо.
  testWidgets('занятое за секунду до старта место просит выбрать другое', (tester) async {
    final http = _serve(
      seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]),
      start: ('{"error":"seat_busy"}', 409),
    );
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.enterText(find.byType(TextField), '482913');
    await tester.pumpAndSettle();
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

    // Сначала кнопка ждёт код: без машины начинать нечего, каким бы ни был тариф.
    expect(find.widgetWithText(FilledButton, 'Код с экрана ПК'), findsOneWidget);

    await tester.enterText(find.byType(TextField), '482913');
    await tester.pumpAndSettle();
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

  /// Про ПИН человек узнавал, только дойдя до ПК: экран лаунчера просит номер и код, которого
  /// нет, — и обещание «начните игру без оператора» кончалось дорогой к стойке.
  testWidgets('без ПИН-кода экран предупреждает и предлагает задать его здесь', (tester) async {
    final http = _serve(seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]));
    await tester.pumpWidget(harness(http, pinSet: false));
    await open(tester);

    expect(find.textContaining('ПИН-код'), findsWidgets);

    await tester.tap(find.widgetWithText(FilledButton, 'Задать ПИН-код'));
    await tester.pumpAndSettle();

    expect(find.text('ПИН-код для посадки за ПК'), findsOneWidget);
  });

  // Профиль ещё не прочитан: пугать предупреждением о том, чего мы не знаем, хуже молчания.
  testWidgets('пока про ПИН неизвестно, экран о нём молчит', (tester) async {
    final http = _serve(seats: jsonEncode([_seat(id: 's1', name: 'PC-01')]));
    await tester.pumpWidget(harness(http));
    await open(tester);

    expect(find.widgetWithText(FilledButton, 'Задать ПИН-код'), findsNothing);
  });
}
