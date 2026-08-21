import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/dto.dart';
import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/auth/player_session.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/shell/app_shell.dart';

import 'package:afk4_customer_app/organization/organization.dart';

import 'support/fake_http.dart';

const _club = Organization(
  organizationId: 'o1',
  slug: 'cyberx',
  name: 'CyberX',
  logoUrl: null,
  accentColor: null,
);

final _now = DateTime.utc(2026, 8, 12, 12, 0, 0);

final _session = PlayerSession(
  playerAccountId: 'p1',
  organizationId: 'o1',
  displayName: 'Иван',
  phoneVerified: true,
  accessToken: 'access-1',
  accessTokenExpiresAtUtc: _now.add(const Duration(hours: 1)),
  refreshToken: 'refresh-1',
  refreshTokenExpiresAtUtc: _now.add(const Duration(days: 30)),
);

String _dashboard() => jsonEncode({
      'walletBalance': {'currencyCode': 'TJS', 'minorUnits': 120050},
      'heldBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
      'debtBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
      'activeSession': null,
    });

String _visits() => jsonEncode({
      'items': [
        {
          'sessionId': 's1',
          'seatId': 'seat-1',
          'seatName': 'PC-07',
          'startedAtUtc': _now.subtract(const Duration(hours: 2)).toIso8601String(),
          'endedAtUtc': _now.toIso8601String(),
          'timeChargeMinorUnits': 3000,
          'posTotalMinorUnits': 0,
          'grandTotalMinorUnits': 3000,
          'currencyCode': 'TJS',
          'hasReceipt': false,
        }
      ],
      'nextCursor': null,
    });

FakeHttpClient _serve({String features = '{"features":["online_topup","online_booking"]}'}) =>
    FakeHttpClient((request) => switch (request.url.path) {
      '/api/me/dashboard' => (_dashboard(), 200),
      '/api/me/features' => (features, 200),
      '/api/me/reservations' => ('[]', 200),
      '/api/me/visits' => (_visits(), 200),
      '/api/me/purchases' => ('{"items":[],"nextCursor":null}', 200),
      _ => ('[]', 200),
    });

/// «Кто я и где у меня счета». `clubs` пуст — человек в этом клубе ещё ничего не делал.
Me _me({bool hasAccountHere = true}) => Me.fromJson({
      'person': {
        'platformPersonId': 'pp1',
        'phoneNumber': '+992900000000',
        'displayName': 'Иван',
        'preferredLocale': 'ru',
        'phoneVerified': true,
        'pinSet': false,
        'networkBanned': false,
      },
      'clubs': [
        if (hasAccountHere)
          {
            'organizationId': 'o1',
            'organizationName': 'CyberX',
            'playerAccountId': 'p1',
            'homeBranchId': 'b1',
            'currencyCode': 'TJS',
            'walletBalanceMinorUnits': 120050,
            'heldMinorUnits': 0,
            'debtMinorUnits': 0,
            'visitCount': 3,
          },
      ],
    });

Widget harness(FakeHttpClient http, {VoidCallback? onSignOut, Me? me}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: AppShell(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        session: _session,
        organization: _club,
        me: me,
        onSignOut: onSignOut ?? () {},
        onChangeClub: () {},
        onLocaleChanged: (_) {},
        clock: () => _now,
      ),
    );

void main() {
  testWidgets('открывается на главной', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    expect(find.text('Баланс кошелька'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('нижняя панель уводит в кошелёк и обратно', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Кошелёк'));
    await tester.pumpAndSettle();
    expect(find.text('PC-07'), findsOneWidget);

    await tester.tap(find.text('Главная'));
    await tester.pumpAndSettle();
    expect(find.text('Баланс кошелька'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Возврат на уже открытый раздел не должен выглядеть как повторный вход в приложение.
  testWidgets('возврат в кошелёк не перезагружает список заново', (tester) async {
    final http = _serve();
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Кошелёк'));
    await tester.pumpAndSettle();
    final afterFirstVisit = http.paths.where((path) => path == '/api/me/visits').length;

    await tester.tap(find.text('Главная'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Кошелёк'));
    await tester.pumpAndSettle();

    expect(http.paths.where((path) => path == '/api/me/visits').length, afterFirstVisit);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Строка баланса на главной — не подпись, а вход в раздел денег.
  testWidgets('баланс с главной ведёт в кошелёк', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Баланс кошелька'));
    await tester.pumpAndSettle();

    expect(find.text('Визиты'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Разделов у клуба без броней на один меньше, и запомненный номер после этого указывал бы
  // на соседа: раздел ищется по имени.
  testWidgets('без броней кошелёк остаётся кошельком, а не сдвигается', (tester) async {
    await tester.pumpWidget(harness(_serve(features: '{"features":["online_topup"]}')));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Кошелёк'));
    await tester.pumpAndSettle();

    expect(find.text('Визиты'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('все готовые разделы на месте', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    expect(find.text('Профиль'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('клуб без онлайн-броней не показывает раздел броней', (tester) async {
    await tester.pumpWidget(harness(_serve(features: '{"features":["online_topup"]}')));
    await tester.pumpAndSettle();

    expect(find.text('Брони'), findsNothing);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Список возможностей не пришёл — разделы показываются все. Спрятать «Брони» из-за
  // сетевого сбоя значит соврать игроку, что клуб их не принимает.
  testWidgets('неизвестные возможности оставляют разделы на месте', (tester) async {
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => (_dashboard(), 200),
          '/api/me/features' => ('{"error":"boom"}', 500),
          '/api/me/reservations' => ('[]', 200),
          '/api/me/visits' => (_visits(), 200),
          _ => ('[]', 200),
        });
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    expect(find.text('Брони'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Приглашение с главной должно приводить туда, где бронь действительно создают.
  testWidgets('«забронировать» с главной ведёт в раздел броней', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Забронировать'));
    await tester.pumpAndSettle();

    expect(find.text('Броней пока нет'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('раздел броней открывается из панели', (tester) async {
    await tester.pumpWidget(harness(_serve()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Брони'));
    await tester.pumpAndSettle();

    expect(find.text('Броней пока нет'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Клуб не заводит человека — счёт появляется с первым действием. До него показывать нули
  // значило бы пообещать кошелёк, которого нет, а «не удалось загрузить» — соврать про сбой.
  testWidgets('в клубе без счёта главная объясняет пустоту, а не сообщает об ошибке',
      (tester) async {
    await tester.pumpWidget(harness(_serve(), me: _me(hasAccountHere: false)));
    await tester.pumpAndSettle();

    expect(find.text('Здесь вы ещё не играли'), findsOneWidget);
    expect(find.text('Баланс кошелька'), findsNothing);
    expect(find.text('Не удалось загрузить данные. Проверьте соединение.'), findsNothing);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  // Спрашивать сервер о клубе, который игрока ещё не знает, незачем: он на всё ответит
  // отказом, а каждый отказ — это лишняя ошибка в логе и лишняя секунда ожидания.
  testWidgets('в клубе без счёта закрытые разделы не опрашиваются', (tester) async {
    final http = _serve();
    await tester.pumpWidget(harness(http, me: _me(hasAccountHere: false)));
    await tester.pumpAndSettle();

    expect(http.paths, isEmpty);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('свой клуб показывает деньги как раньше', (tester) async {
    await tester.pumpWidget(harness(_serve(), me: _me()));
    await tester.pumpAndSettle();

    expect(find.text('Баланс кошелька'), findsOneWidget);
    expect(find.text('Здесь вы ещё не играли'), findsNothing);
    await tester.pumpWidget(const SizedBox.shrink());
  });
}
