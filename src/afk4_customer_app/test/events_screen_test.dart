import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/events/events_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

import 'support/fake_http.dart';

const _branchId = 'b1';
final _now = DateTime(2026, 8, 26, 12);

Map<String, dynamic> _event({
  String id = 't1',
  String title = 'Ночь Counter-Strike',
  int fee = 2000,
  int capacity = 10,
  int registered = 3,
  bool isRegistered = false,
  String state = 'published',
  String cancelReason = '',
}) =>
    {
      'tournamentId': id,
      'branchId': _branchId,
      'branchName': 'На Рудаки',
      'title': title,
      'description': 'Пять на пять, свои команды',
      'discipline': 'Counter-Strike',
      'startsAtUtc': '2026-08-28T14:00:00Z',
      'entryFee': {'currencyCode': 'TJS', 'minorUnits': fee},
      'capacity': capacity,
      'registeredCount': registered,
      'isRegistered': isRegistered,
      'state': state,
      'cancelReason': cancelReason,
    };

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: EventsScreen(api: api, branchId: _branchId, clock: () => _now),
    );

PlayerApiClient clientWith(FakeHttpClient http) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: http);

FakeHttpClient _serve({
  List<Map<String, dynamic>> events = const [],
  (String, int)? register,
  (String, int)? cancel,
}) =>
    FakeHttpClient((request) {
      final path = request.url.path;
      if (path == '/api/me/branches/$_branchId/tournaments') {
        return (jsonEncode(events), 200);
      }
      if (path == '/api/me/tournaments/t1/registration') {
        return request.method == 'DELETE'
            ? (cancel ?? (jsonEncode(_event(isRegistered: false)), 200))
            : (register ?? (jsonEncode(_event(isRegistered: true, registered: 4)), 200));
      }
      return ('[]', 200);
    });

void main() {
  testWidgets('событие показывает, когда, во что играют и сколько стоит', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(events: [_event()]))));
    await tester.pumpAndSettle();

    expect(find.text('Ночь Counter-Strike'), findsOneWidget);
    expect(find.text('Counter-Strike'), findsOneWidget);
    expect(find.textContaining('20,00'), findsOneWidget);
    expect(find.text('осталось 7 мест'), findsOneWidget);
  });

  // Бесплатный вечер — обычный случай, и «Взнос 0,00 с.» выглядел бы как ошибка.
  testWidgets('бесплатное событие так и называется', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(events: [_event(fee: 0)]))));
    await tester.pumpAndSettle();

    expect(find.text('Бесплатно'), findsOneWidget);
    expect(find.textContaining('Взнос'), findsNothing);
  });

  // Потолка нет — «осталось N мест» было бы выдумкой.
  testWidgets('событие без ограничения мест показывает число участников', (tester) async {
    await tester.pumpWidget(
      harness(clientWith(_serve(events: [_event(capacity: 0, registered: 12)]))));
    await tester.pumpAndSettle();

    expect(find.text('12 участников'), findsOneWidget);
    expect(find.textContaining('осталось'), findsNothing);
  });

  testWidgets('пустое расписание объясняется словами, а не пустотой', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve())));
    await tester.pumpAndSettle();

    expect(find.text('Клуб пока ничего не запланировал'), findsOneWidget);
  });

  // Деньги списываются молча только у мошенников: сумма и судьба возврата стоят в вопросе.
  testWidgets('запись спрашивает подтверждение с суммой и обещанием возврата', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(events: [_event()]))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Записаться').first);
    await tester.pumpAndSettle();

    expect(find.textContaining('20,00'), findsWidgets);
    expect(find.textContaining('деньги вернутся'), findsOneWidget);
  });

  testWidgets('отказ в подтверждении никого не записывает', (tester) async {
    final http = _serve(events: [_event()]);
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Записаться').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Отмена'));
    await tester.pumpAndSettle();

    expect(http.paths.where((path) => path.endsWith('/registration')), isEmpty);
  });

  testWidgets('после записи экран говорит, что вы записаны', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(events: [_event()]))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Записаться').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Записаться').last);
    await tester.pumpAndSettle();

    expect(find.textContaining('Записали на'), findsOneWidget);
  });

  testWidgets('записанный игрок видит кнопку снять запись', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(events: [_event(isRegistered: true)]))));
    await tester.pumpAndSettle();

    expect(find.text('Вы записаны'), findsOneWidget);
    expect(find.text('Не пойду'), findsOneWidget);
    expect(find.text('Записаться'), findsNothing);
  });

  // Забитое событие не должно выглядеть так же, как то, куда ещё можно попасть.
  testWidgets('без мест кнопка не зовёт нажимать', (tester) async {
    await tester.pumpWidget(
      harness(clientWith(_serve(events: [_event(capacity: 4, registered: 4)]))));
    await tester.pumpAndSettle();

    expect(find.text('Мест больше нет'), findsWidgets);
    final button = tester.widget<FilledButton>(find.byType(FilledButton));
    expect(button.onPressed, isNull);
  });

  // Молча убрать отменённое событие значит оставить человека собираться на вечер, которого нет.
  testWidgets('отменённое клубом событие говорит об этом и называет причину', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(events: [
      _event(state: 'cancelled', isRegistered: false, cancelReason: 'Свет отключили')
    ]))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Клуб отменил событие'), findsOneWidget);
    expect(find.textContaining('Свет отключили'), findsOneWidget);
    expect(find.text('Записаться'), findsNothing);
  });

  testWidgets('нехватка денег объясняется, а не выглядит общим сбоем', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(
      events: [_event()],
      register: ('{"error":"insufficient_funds"}', 409),
    ))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Записаться').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Записаться').last);
    await tester.pumpAndSettle();

    expect(find.text('На кошельке не хватает денег на взнос'), findsOneWidget);
  });

  testWidgets('снятие спрашивает подтверждение и обещает вернуть взнос', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(events: [_event(isRegistered: true)]))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Не пойду'));
    await tester.pumpAndSettle();

    expect(find.text('Снять запись?'), findsOneWidget);
    expect(find.textContaining('вернётся на кошелёк'), findsOneWidget);
  });
}
