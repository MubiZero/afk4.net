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
}) =>
    {
      'reservationId': id,
      'seatId': 'seat-1',
      'seatName': seat,
      'startsAtUtc': _now.add(const Duration(days: 1)).toUtc().toIso8601String(),
      'endsAtUtc': _now.add(const Duration(days: 1, hours: 2)).toUtc().toIso8601String(),
      'state': state,
      'note': null,
    };

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
    expect(find.textContaining('подтвердите номер телефона'), findsOneWidget);
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

  testWidgets('незнакомое состояние показывается как есть, а не выдумывается', (tester) async {
    await tester.pumpWidget(harness(_serve(jsonEncode([_reservation(state: 'no_show')]))));
    await tester.pumpAndSettle();

    expect(find.text('no_show'), findsOneWidget);
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
}
