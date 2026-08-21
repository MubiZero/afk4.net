import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/app_localizations.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/reservations/reservations_screen.dart';

import 'support/fake_http.dart';

final _now = DateTime(2026, 8, 12, 12, 0, 0);

Map<String, dynamic> _reservation({
  String id = 'r1',
  String? seat = 'PC-07',
  String state = 'confirmed',
  String? groupId,
  int? costMinorUnits,
  Duration? respondIn,
}) =>
    {
      'reservationId': id,
      'seatId': 'seat-1',
      'seatName': seat,
      'startsAtUtc': _now.add(const Duration(days: 1)).toUtc().toIso8601String(),
      'endsAtUtc': _now.add(const Duration(days: 1, hours: 2)).toUtc().toIso8601String(),
      'state': state,
      'note': null,
      'reservationGroupId': groupId,
      'estimatedCostMinorUnits': costMinorUnits,
      'currencyCode': costMinorUnits == null ? null : 'TJS',
      'respondByUtc':
          respondIn == null ? null : _now.add(respondIn).toUtc().toIso8601String(),
    };

/// Компания: несколько мест с общим идентификатором группы, как их отдаёт сервер.
String _companyJson({int seats = 3, String state = 'confirmed', int perSeat = 1500}) => jsonEncode([
      for (var index = 0; index < seats; index++)
        _reservation(
          id: 'g$index',
          seat: null,
          state: state,
          groupId: 'group-1',
          costMinorUnits: perSeat,
        ),
    ]);

Widget harness(FakeHttpClient http, {bool phoneVerified = true}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ReservationsScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        phoneVerified: phoneVerified,
        clock: () => _now,
      ),
    );

FakeHttpClient _serve(String list, {(String, int)? onWrite}) =>
    FakeHttpClient((request) => request.method == 'GET' ? (list, 200) : (onWrite ?? (list, 200)));

/// Кнопка подтверждения системного диалога. Пишется по-разному в зависимости от языка
/// («ОК» кириллицей на русском), поэтому ищется по обоим написаниям.
final confirmButton = find.byWidgetPredicate(
  (widget) => widget is Text && (widget.data == 'OK' || widget.data == 'ОК'),
);

/// Открывает лист новой брони: форма живёт там, а раздел начинается со списка.
Future<void> openForm(WidgetTester tester) async {
  await tester.tap(find.byType(FloatingActionButton));
  await tester.pumpAndSettle();
}

/// Кнопка подтверждения внутри листа — у неё то же слово, что и у кнопки открытия.
final submitButton = find.widgetWithText(FilledButton, 'Забронировать');

/// Проходит оба системных диалога, соглашаясь с предложенными значениями.
Future<void> pickDateTime(WidgetTester tester, String fieldLabel) async {
  await tester.tap(find.text(fieldLabel));
  await tester.pumpAndSettle();
  await tester.tap(confirmButton);
  await tester.pumpAndSettle();
  await tester.tap(confirmButton);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('бронь показывает место, время и состояние', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation()]))));
    await tester.pumpAndSettle();

    expect(find.text('PC-07'), findsOneWidget);
    expect(find.text('Подтверждена'), findsOneWidget);
  });

  testWidgets('бронь без назначенного места говорит об этом', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation(seat: null)]))));
    await tester.pumpAndSettle();

    expect(find.text('Без места'), findsOneWidget);
  });

  testWidgets('пустой список говорит «броней пока нет»', (tester) async {
    await tester.pumpWidget(harness(_serve('[]')));
    await tester.pumpAndSettle();

    expect(find.text('Броней пока нет'), findsOneWidget);
  });

  // «Броней нет» и «мы их не увидели» — разные новости: на первой игрок спокойно уйдёт
  // мимо собственной брони.
  testWidgets('сбой загрузки не выдаётся за отсутствие броней', (tester) async {
    var attempt = 0;
    final http = FakeHttpClient((_) =>
        ++attempt == 1 ? ('{"error":"boom"}', 500) : (jsonEncode([_reservation()]), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    expect(find.text('Броней пока нет'), findsNothing);
    expect(find.text('Не удалось загрузить брони.'), findsOneWidget);

    await tester.tap(find.text('Повторить'));
    await tester.pumpAndSettle();

    expect(find.text('PC-07'), findsOneWidget);
  });

  testWidgets('список броней обновляется потягиванием вниз', (tester) async {
    final http = _serve(jsonEncode([_reservation()]));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();
    final before = http.requests.where((r) => r.method == 'GET').length;

    await tester.fling(find.byType(RefreshIndicator), const Offset(0, 300), 1000);
    await tester.pumpAndSettle();

    expect(http.requests.where((r) => r.method == 'GET').length, greaterThan(before));
  });

  // Бронь на завтра важнее знать днём недели, чем датой: «12 авг.» заставляет считать.
  testWidgets('время брони называет соседний день словом', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation()]))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Завтра'), findsOneWidget);
  });

  testWidgets('неподтверждённый телефон объясняет, почему бронировать нельзя', (tester) async {
    await tester.pumpWidget(harness(_serve('[]'), phoneVerified: false));
    await tester.pumpAndSettle();

    expect(find.text('Забронировать'), findsNothing);
    expect(find.textContaining('подтвердите свой номер телефона'), findsOneWidget);

    await tester.tap(find.text('Подтвердить номер'));
    await tester.pumpAndSettle();

    expect(find.text('Подтверждение номера'), findsOneWidget);
  });

  // Раздел открывают, чтобы посмотреть свои брони: развёрнутая форма занимала первый экран
  // у всех, включая тех, кто бронировать не собирался.
  testWidgets('раздел начинается со списка, а форма ждёт за кнопкой', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation()]))));
    await tester.pumpAndSettle();

    expect(find.text('PC-07'), findsOneWidget);
    expect(find.text('Начало'), findsNothing);

    await openForm(tester);
    expect(find.text('Новая бронь'), findsOneWidget);
    expect(find.text('Начало'), findsOneWidget);
  });

  // В киберклубе выбор машины — половина смысла брони, и молчать о том, что место назначает
  // клуб, значит оставить игрока в неведении.
  testWidgets('форма говорит, кто назначает место', (tester) async {
    await tester.pumpWidget(harness(_serve('[]')));
    await tester.pumpAndSettle();
    await openForm(tester);

    expect(find.textContaining('Место назначит администратор клуба'), findsOneWidget);
  });

  // Ошибка относится к полям времени и должна жить рядом с ними: снекбар внизу экрана
  // исчезал вместе с объяснением, что именно чинить.
  testWidgets('без выбранного времени бронь не уходит на сервер', (tester) async {
    final http = _serve('[]');
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await openForm(tester);
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(http.bodies, isEmpty);
    expect(find.text('Укажите начало и конец'), findsOneWidget);
    expect(find.byType(SnackBar), findsNothing);
  });

  testWidgets('выбранное время уходит на сервер в UTC', (tester) async {
    final http = _serve('[]', onWrite: (jsonEncode(_reservation()), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await openForm(tester);
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(http.bodies, hasLength(1));
    expect(http.bodies.single['startsAtUtc'], endsWith('Z'));
    expect(http.bodies.single['endsAtUtc'], endsWith('Z'));
    expect(find.text('Бронь создана'), findsOneWidget);
  });

  // В киберклуб ходят компанией, и это ровно один шаг в форме — счётчик мест.
  testWidgets('несколько мест уходят одной групповой бронью', (tester) async {
    final http = _serve('[]',
        onWrite: (jsonEncode({
          'reservationGroupId': 'group-1',
          'reservations': [_reservation(id: 'g0', seat: null, groupId: 'group-1')],
          'totalEstimatedCostMinorUnits': 4500,
          'currencyCode': 'TJS',
        }), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await openForm(tester);
    // Время выбирается первым: счётчик мест стоит ниже, и после него поля уезжают за край
    // невысокого экрана — как и у настоящего игрока, до них надо прокрутить.
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(find.byTooltip('Больше мест'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Больше мест'));
    await tester.pumpAndSettle();
    expect(find.text('3'), findsOneWidget);
    expect(find.text('Бронь на компанию'), findsOneWidget);

    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    final writes = http.requests.where((r) => r.method == 'POST').toList();
    expect(writes.single.url.path, '/api/me/reservations/group');
    expect(http.bodies.single['seatCount'], 3);
  });

  // Одно место — обычная бронь: групповой маршрут для одиночки был бы лишней сущностью.
  testWidgets('одно место уходит обычной бронью, а не группой', (tester) async {
    final http = _serve('[]', onWrite: (jsonEncode(_reservation()), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await openForm(tester);
    expect(find.text('Бронь на одного'), findsOneWidget);
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    final writes = http.requests.where((r) => r.method == 'POST').toList();
    expect(writes.single.url.path, '/api/me/reservations');
  });

  // Границу видно кнопкой, а не отказом после нажатия.
  testWidgets('меньше одного места не выбрать', (tester) async {
    await tester.pumpWidget(harness(_serve('[]')));
    await tester.pumpAndSettle();

    await openForm(tester);

    // Проверяется исход, а не форма дерева виджетов: нажатие на «минус» на одном месте не
    // должно ни уменьшать счёт, ни ронять экран.
    await tester.tap(find.byTooltip('Меньше мест'), warnIfMissed: false);
    await tester.pumpAndSettle();

    expect(find.text('1'), findsOneWidget);
    expect(find.text('0'), findsNothing);
    expect(find.text('Бронь на одного'), findsOneWidget);
  });

  // Денег не хватило на всю компанию — говорим именно это, а не «пополните кошелёк»:
  // выход отсюда ещё и в том, чтобы взять меньше мест.
  testWidgets('нехватка денег на компанию названа своей причиной', (tester) async {
    await tester.pumpWidget(harness(
        _serve('[]', onWrite: ('{"error":"insufficient_funds"}', 409))));
    await tester.pumpAndSettle();

    await openForm(tester);
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(find.byTooltip('Больше мест'));
    await tester.pumpAndSettle();
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(find.textContaining('на всю компанию'), findsOneWidget);
  });

  // Зал полон — это не «время занято»: время свободно, кончились машины. Выход отсюда другой,
  // и назвать его надо словами.
  testWidgets('заполненный зал объясняется машинами, а не занятым временем', (tester) async {
    await tester.pumpWidget(harness(
        _serve('[]', onWrite: ('{"error":"no_seats_available"}', 409))));
    await tester.pumpAndSettle();

    await openForm(tester);
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(find.textContaining('свободных машин'), findsOneWidget);
    expect(find.text('Это время уже занято'), findsNothing);
  });

  // Компании добавляется выход, которого у одиночной брони нет: взять меньше мест.
  testWidgets('компании, которая не влезла, предлагают взять меньше мест', (tester) async {
    await tester.pumpWidget(harness(
        _serve('[]', onWrite: ('{"error":"no_seats_available"}', 409))));
    await tester.pumpAndSettle();

    await openForm(tester);
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(find.byTooltip('Больше мест'));
    await tester.pumpAndSettle();
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(find.textContaining('меньше мест'), findsOneWidget);
  });

  // 409 — не сбой, а «время уже занято». Общая «не удалось создать» тут врёт про причину.
  testWidgets('занятое время объясняется занятостью, а не общей ошибкой', (tester) async {
    await tester.pumpWidget(harness(_serve('[]', onWrite: ('{"error":"taken"}', 409))));
    await tester.pumpAndSettle();

    await openForm(tester);
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(find.text('Это время уже занято'), findsOneWidget);
    expect(find.text('Не удалось создать бронь'), findsNothing);
  });

  testWidgets('отмена спрашивает подтверждение и без него ничего не шлёт', (tester) async {
    final http = _serve(jsonEncode([_reservation()]));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Отменить'));
    await tester.pumpAndSettle();
    expect(find.text('Отменить бронь?'), findsOneWidget);

    await tester.tap(find.text('Оставить'));
    await tester.pumpAndSettle();

    expect(http.requests.where((r) => r.method == 'DELETE'), isEmpty);
  });

  // Пара «Отменить» / «Назад» читалась как два способа закрыть диалог: игрок, выходивший из
  // него, с равной вероятностью отменял свою бронь.
  testWidgets('обе кнопки диалога называют своё действие целиком', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation()]))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Отменить'));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(TextButton, 'Оставить'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Отменить бронь'), findsOneWidget);
    expect(find.widgetWithText(TextButton, '← Назад'), findsNothing);
  });

  // При двух бронях безымянный вопрос «Отменить бронь?» не говорит, какую именно.
  testWidgets('диалог называет отменяемую бронь', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation()]))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Отменить'));
    await tester.pumpAndSettle();

    expect(find.descendant(of: find.byType(AlertDialog), matching: find.textContaining('PC-07')),
        findsOneWidget);
  });

  testWidgets('подтверждённая отмена уходит на сервер', (tester) async {
    final http = _serve(jsonEncode([_reservation()]),
        onWrite: (jsonEncode(_reservation(state: 'cancelled')), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Отменить'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Отменить бронь'));
    await tester.pumpAndSettle();

    expect(http.requests.where((r) => r.method == 'DELETE'), hasLength(1));
    expect(find.text('Бронь отменена'), findsOneWidget);
  });

  testWidgets('у отменённой брони отменять нечего', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation(state: 'cancelled')]))));
    await tester.pumpAndSettle();

    expect(find.text('Отменена'), findsOneWidget);
    expect(find.text('Отменить'), findsNothing);
  });

  // Четыре брони с общим идентификатором — это одна компания, а не четыре одинаковые карточки:
  // из четырёх строк подряд игрок не поймёт, четыре у него брони или одна на четверых.
  testWidgets('компания показывается одной карточкой с числом мест', (tester) async {
    await tester.pumpWidget(harness(_serve(_companyJson(seats: 3))));
    await tester.pumpAndSettle();

    expect(find.text('3 места'), findsOneWidget);
    expect(find.text('Без места'), findsNothing);
    // Сумма по всей компании: 3 × 15,00.
    expect(find.textContaining('45,00'), findsOneWidget);
  });

  testWidgets('отменённые места компании не идут в счёт', (tester) async {
    final list = jsonEncode([
      _reservation(id: 'g0', seat: null, groupId: 'group-1', costMinorUnits: 1500),
      _reservation(id: 'g1', seat: null, groupId: 'group-1', costMinorUnits: 1500),
      _reservation(
          id: 'g2', seat: null, state: 'cancelled', groupId: 'group-1', costMinorUnits: 1500),
    ]);
    await tester.pumpWidget(harness(_serve(list)));
    await tester.pumpAndSettle();

    expect(find.text('2 места'), findsOneWidget);
    expect(find.textContaining('30,00'), findsOneWidget);
  });

  // Отмена компании — одно действие с одним исходом: четыре отдельных запроса это четыре
  // шанса оборваться на полпути и оставить часть денег замороженной.
  testWidgets('компания отменяется одним запросом к группе', (tester) async {
    final http = _serve(_companyJson(seats: 3));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Отменить всю компанию'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Отменить бронь'));
    await tester.pumpAndSettle();

    final deletes = http.requests.where((r) => r.method == 'DELETE').toList();
    expect(deletes, hasLength(1));
    expect(deletes.single.url.path, '/api/me/reservations/group/group-1');
  });

  testWidgets('одиночная бронь по-прежнему отменяется по себе', (tester) async {
    final http = _serve(jsonEncode([_reservation()]));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Отменить'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Отменить бронь'));
    await tester.pumpAndSettle();

    final deletes = http.requests.where((r) => r.method == 'DELETE').toList();
    expect(deletes.single.url.path, '/api/me/reservations/r1');
  });

  testWidgets('незнакомое состояние показывается как есть, а не выдумывается', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation(state: 'teleported')]))));
    await tester.pumpAndSettle();

    expect(find.text('teleported'), findsOneWidget);
  });

  // Отказ клуба без причины — это исчезнувшая бронь: человек видит, что её нет, и не понимает
  // почему. Причина и судьба денег важнее самого факта отказа.
  testWidgets('отказ клуба показывает причину и судьбу денег', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([
      _reservation(state: 'rejected')
        ..['rejectReasonCode'] = 'maintenance'
        ..['rejectReasonNote'] = 'Меняем проводку в зале'
    ]))));
    await tester.pumpAndSettle();

    expect(find.text('Клуб отказал'), findsOneWidget);
    expect(find.text('Зал закрыт на техработы'), findsOneWidget);
    expect(find.text('Меняем проводку в зале'), findsOneWidget);
    expect(find.text('Деньги вернулись целиком.'), findsOneWidget);
  });

  // «Не приехал» перестал быть незнакомым состоянием: у него своя подпись. Показывать человеку
  // сырое no_show значит требовать от него читать по-английски внутренности сервера.
  testWidgets('неявка называется словами, а не кодом состояния', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation(state: 'no_show')]))));
    await tester.pumpAndSettle();

    expect(find.text('Вы не приехали'), findsOneWidget);
    expect(find.text('no_show'), findsNothing);
  });

  group('проверка времени до отправки', () {
    late L l;

    Future<void> load(WidgetTester tester) async {
      await tester.pumpWidget(MaterialApp(
        locale: const Locale('ru'),
        localizationsDelegates: appLocalizationsDelegates,
        supportedLocales: appSupportedLocales,
        home: Builder(builder: (context) {
          l = L.of(context);
          return const SizedBox.shrink();
        }),
      ));
    }

    testWidgets('начало в прошлом отклоняется с понятной причиной', (tester) async {
      await load(tester);
      final problem = reservationTimeProblem(
        l,
        _now.subtract(const Duration(hours: 1)),
        _now.add(const Duration(hours: 1)),
        now: _now,
      );

      expect(problem, 'Начало должно быть в будущем');
    });

    testWidgets('конец раньше начала отклоняется с понятной причиной', (tester) async {
      await load(tester);
      final problem = reservationTimeProblem(
        l,
        _now.add(const Duration(hours: 3)),
        _now.add(const Duration(hours: 1)),
        now: _now,
      );

      expect(problem, 'Конец должен быть позже начала');
    });

    testWidgets('корректный промежуток проходит', (tester) async {
      await load(tester);
      final problem = reservationTimeProblem(
        l,
        _now.add(const Duration(hours: 1)),
        _now.add(const Duration(hours: 3)),
        now: _now,
      );

      expect(problem, isNull);
    });
  });

  // Заявка, которую смотрит администратор, больше не висит без срока: клуб обязан ответить,
  // а молчание снимает её и возвращает деньги. Игрок должен видеть и срок, и остаток времени.
  testWidgets('у заявки в ожидании идёт обратный отсчёт ответа клуба', (tester) async {
    final list = jsonEncode([
      _reservation(state: 'pending', respondIn: const Duration(minutes: 12)),
    ]);
    await tester.pumpWidget(harness(_serve(list)));
    await tester.pumpAndSettle();

    expect(find.textContaining('Клуб ответит до'), findsOneWidget);
    expect(find.textContaining('осталось 12 минут'), findsOneWidget);
  });

  // Срок вышел — это не отказ и не потеря денег, и молчать об этом хуже всего.
  testWidgets('истёкший срок ответа объясняет, что будет дальше', (tester) async {
    final list = jsonEncode([
      _reservation(state: 'pending', respondIn: const Duration(minutes: -1)),
    ]);
    await tester.pumpWidget(harness(_serve(list)));
    await tester.pumpAndSettle();

    expect(find.text('Клуб не ответил вовремя — заявка снимется, деньги вернутся.'),
        findsOneWidget);
  });

  // У подтверждённой брони отвечать больше не на что: отсчёт там был бы обещанием из воздуха.
  testWidgets('подтверждённая бронь отсчёта не показывает', (tester) async {
    final list = jsonEncode([
      _reservation(state: 'confirmed', respondIn: const Duration(minutes: 12)),
    ]);
    await tester.pumpWidget(harness(_serve(list)));
    await tester.pumpAndSettle();

    expect(find.textContaining('Клуб ответит до'), findsNothing);
  });

  // Счёта в клубе ещё нет — значит и броней нет. Это пустой раздел, а не сбой загрузки.
  testWidgets('клуб без счёта показывает пустой раздел, а не ошибку', (tester) async {
    await tester.pumpWidget(
        harness(FakeHttpClient((_) => ('{"error":"club_not_selected"}', 409))));
    await tester.pumpAndSettle();

    expect(find.text('Броней пока нет'), findsOneWidget);
    expect(find.text('Не удалось загрузить брони.'), findsNothing);
  });
}
