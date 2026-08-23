import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/branch_choice.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/reservations/reservations_screen.dart';
import 'package:afk4_customer_app/wallet/wallet_screen.dart';

import 'support/fake_http.dart';

final _now = DateTime(2026, 8, 12, 12, 0, 0);

const _rudaki = ClubPlace(
  branchId: 'b-rudaki',
  name: 'На Рудаки',
  city: 'Душанбе',
  address: 'проспект Рудаки, 12',
);

const _somoni = ClubPlace(
  branchId: 'b-somoni',
  name: 'На Сомони',
  city: 'Душанбе',
  address: 'улица Сомони, 40',
);

String _rulesJson({String branchId = 'b-somoni', String mode = 'manual'}) => jsonEncode({
      'branchId': branchId,
      'acceptanceMode': mode,
      'respondWithinMinutes': 15,
      'prepaymentRequired': false,
      'activeReservations': 0,
      'maxActiveReservations': null,
      'holdSeatAfterStartMinutes': 20,
    });

/// Сервер сети с двумя залами. Правила приёма отвечают по любому из них: до открытия счёта
/// их и спрашивают — именно этим маршрутом.
FakeHttpClient _serve({(String, int)? create, (String, int)? topUp}) =>
    FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/branches/b-rudaki/booking-rules' => (_rulesJson(branchId: 'b-rudaki'), 200),
          '/api/me/branches/b-somoni/booking-rules' => (_rulesJson(), 200),
          '/api/me/reservations' =>
            request.method == 'POST' ? (create ?? ('{}', 500)) : ('[]', 200),
          '/api/me/reservations/group' => create ?? ('{}', 500),
          '/api/me/wallet/top-up-intent' => topUp ?? ('{}', 500),
          '/api/me/wallet/top-up-intents' => ('[]', 200),
          _ => ('[]', 200),
        });

const String _reservationJson = '''
{"reservationId":"r1","seatId":null,"seatName":null,
 "startsAtUtc":"2026-08-13T12:00:00Z","endsAtUtc":"2026-08-13T14:00:00Z",
 "state":"pending","note":null}
''';

const String _groupJson = '''
{"reservationGroupId":"g1","seatCount":2,"reservations":[]}
''';

const String _intentJson = '''
{"paymentIntentId":"i1","amountMinorUnits":5000,"currencyCode":"TJS",
 "state":"pending","isExpired":false}
''';

Widget _reservations(
  FakeHttpClient http, {
  required BranchChoice branch,
  bool accountOpen = false,
}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ReservationsScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        phoneVerified: true,
        accountOpen: accountOpen,
        branch: branch,
        clock: () => _now,
      ),
    );

Widget _wallet(FakeHttpClient http, {required BranchChoice branch}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: WalletScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        phoneVerified: true,
        features: const ['online_topup'],
        accountOpen: false,
        branch: branch,
        clock: () => _now,
      ),
    );

final _confirm = find.byWidgetPredicate(
  (widget) => widget is Text && (widget.data == 'OK' || widget.data == 'ОК'),
);

final _submit = find.widgetWithText(FilledButton, 'Забронировать');

Future<void> _openForm(WidgetTester tester) async {
  await tester.tap(find.byType(FloatingActionButton));
  await tester.pumpAndSettle();
}

/// Выбор зала делает лист длиннее, и поля времени уезжают под нижний край. Нажимается поле
/// целиком, а не подпись внутри рамки: сама подпись нажатие не принимает.
Future<void> _pickDateTime(WidgetTester tester, String label) async {
  final field = find.ancestor(of: find.text(label), matching: find.byType(InkWell)).first;
  await tester.ensureVisible(field);
  await tester.pumpAndSettle();
  await tester.tap(field);
  await tester.pumpAndSettle();
  await tester.tap(_confirm);
  await tester.pumpAndSettle();
  await tester.tap(_confirm);
  await tester.pumpAndSettle();
}

Future<void> _fillTimes(WidgetTester tester) async {
  await _pickDateTime(tester, 'Начало');
  await _pickDateTime(tester, 'Конец');
}

/// Выбор с ответом игрока: лист получает зал так же, как его получил бы от оболочки.
BranchChoice _chosen(String branchId) =>
    BranchChoice(halls: const [_rudaki, _somoni], chosenId: branchId);

void main() {
  group('зал в сети из нескольких залов', () {
    testWidgets('спрашивается перед первой бронью', (tester) async {
      await tester.pumpWidget(_reservations(
        _serve(),
        branch: const BranchChoice(halls: [_rudaki, _somoni]),
      ));
      await tester.pumpAndSettle();
      await _openForm(tester);

      expect(find.text('В какой зал вы придёте?'), findsOneWidget);
      expect(find.text('На Рудаки'), findsOneWidget);
      expect(find.text('На Сомони'), findsOneWidget);
    });

    // Кнопка гаснет, а не отвечает отказом после нажатия: сервер на бронь без зала отвечает
    // тем самым 409, из-за которого этот выбор и появился.
    testWidgets('до ответа игрока бронировать нечем', (tester) async {
      await tester.pumpWidget(_reservations(
        _serve(),
        branch: const BranchChoice(halls: [_rudaki, _somoni]),
      ));
      await tester.pumpAndSettle();
      await _openForm(tester);

      expect(tester.widget<FilledButton>(_submit).onPressed, isNull);
    });

    // Правила приёма — то, ради чего их теперь можно спросить до счёта: новичок узнаёт про
    // предоплату и ручной приём заранее, а не из отказа по первой брони.
    testWidgets('названный зал сразу отвечает правилами приёма', (tester) async {
      final http = _serve();
      await tester.pumpWidget(_reservations(
        http,
        branch: const BranchChoice(halls: [_rudaki, _somoni]),
      ));
      await tester.pumpAndSettle();
      await _openForm(tester);
      await tester.tap(find.text('На Сомони'));
      await tester.pumpAndSettle();

      expect(http.paths, contains('/api/me/branches/b-somoni/booking-rules'));
      expect(find.textContaining('Заявку смотрит администратор'), findsOneWidget);
      expect(tester.widget<FilledButton>(_submit).onPressed, isNotNull);
    });

    testWidgets('бронь уезжает с названным залом', (tester) async {
      final http = _serve(create: (_reservationJson, 200));
      await tester.pumpWidget(_reservations(http, branch: _chosen('b-somoni')));
      await tester.pumpAndSettle();
      await _openForm(tester);
      await _fillTimes(tester);
      await tester.tap(_submit);
      await tester.pumpAndSettle();

      expect(http.paths, contains('/api/me/reservations'));
      expect(http.bodies.last['branchId'], 'b-somoni');
    });

    testWidgets('бронь на компанию уезжает с названным залом', (tester) async {
      final http = _serve(create: (_groupJson, 200));
      await tester.pumpWidget(_reservations(http, branch: _chosen('b-rudaki')));
      await tester.pumpAndSettle();
      await _openForm(tester);
      await _fillTimes(tester);
      await tester.tap(find.byTooltip('Больше мест'));
      await tester.pumpAndSettle();
      await tester.tap(_submit);
      await tester.pumpAndSettle();

      expect(http.paths, contains('/api/me/reservations/group'));
      expect(http.bodies.last['branchId'], 'b-rudaki');
    });

    // Зал сняли с карты, пока игрок заполнял форму. Общее «не удалось» не подсказывает, что
    // делать, а выход здесь есть — назвать соседний зал.
    testWidgets('исчезнувший зал назван своей причиной', (tester) async {
      final http = _serve(create: ('{"error":"branch_not_found"}', 409));
      await tester.pumpWidget(_reservations(http, branch: _chosen('b-somoni')));
      await tester.pumpAndSettle();
      await _openForm(tester);
      await _fillTimes(tester);
      await tester.tap(_submit);
      await tester.pumpAndSettle();

      expect(find.textContaining('Такого зала в сети больше нет'), findsOneWidget);
    });

    testWidgets('первое пополнение уезжает с названным залом', (tester) async {
      final http = _serve(topUp: (_intentJson, 200));
      await tester.pumpWidget(_wallet(http, branch: _chosen('b-somoni')));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(FilledButton, 'Пополнить'));
      await tester.pumpAndSettle();
      expect(find.text('В какой зал вы придёте?'), findsOneWidget);

      await tester.enterText(find.byType(TextField).first, '50');
      await tester.tap(find.widgetWithText(FilledButton, 'Внести на стойке'));
      await tester.pumpAndSettle();

      expect(http.paths, contains('/api/me/wallet/top-up-intent'));
      expect(http.bodies.last['branchId'], 'b-somoni');
    });
  });

  group('зал, который выбирать не за что', () {
    // Один зал — он и есть ответ. Спрашивать «в какой из одного» бессмысленно, но назвать
    // его серверу всё равно надо: счёт открывается именно в нём.
    testWidgets('единственный зал уезжает сам, без вопроса', (tester) async {
      final http = _serve(create: (_reservationJson, 200));
      await tester.pumpWidget(_reservations(
        http,
        branch: const BranchChoice(halls: [_rudaki]),
      ));
      await tester.pumpAndSettle();
      await _openForm(tester);

      expect(find.text('В какой зал вы придёте?'), findsNothing);
      expect(http.paths, contains('/api/me/branches/b-rudaki/booking-rules'));

      await _fillTimes(tester);
      await tester.tap(_submit);
      await tester.pumpAndSettle();

      expect(http.bodies.last['branchId'], 'b-rudaki');
    });

    // У игрока со счётом зал уже записан, и присланный его не переписывает: спрашивать —
    // значит обещать выбор, которого нет.
    testWidgets('игрока со счётом ни о чём не спрашивают', (tester) async {
      final http = FakeHttpClient((request) => switch (request.url.path) {
            '/api/me/profile' => (
                jsonEncode({
                  'displayName': 'Иван',
                  'phoneNumber': '+992900000000',
                  'phoneVerified': true,
                  'preferredLocale': null,
                  'marketingOptIn': false,
                  'homeBranchId': 'b-rudaki',
                }),
                200,
              ),
            '/api/me/reservations' =>
              request.method == 'POST' ? (_reservationJson, 200) : ('[]', 200),
            _ => ('[]', 200),
          });
      await tester.pumpWidget(_reservations(
        http,
        accountOpen: true,
        branch: const BranchChoice(halls: [_rudaki, _somoni]),
      ));
      await tester.pumpAndSettle();
      await _openForm(tester);

      expect(find.text('В какой зал вы придёте?'), findsNothing);

      await _fillTimes(tester);
      await tester.tap(_submit);
      await tester.pumpAndSettle();

      expect(http.bodies.last.containsKey('branchId'), isFalse);
    });
  });
}
