import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/dashboard/dashboard_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/organization.dart';

import 'support/fake_http.dart';

const _club = Organization(organizationId: 'o1', slug: 'cyberx', name: 'CyberX');

final _now = DateTime.utc(2026, 8, 12, 12, 0, 0);

String _dashboardJson({Map<String, dynamic>? session}) => jsonEncode({
      'walletBalance': {'currencyCode': 'TJS', 'minorUnits': 120050},
      'debtBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
      'activeSession': session,
    });

String _pendingJson() => jsonEncode({
      'sessionId': 's1',
      'branchName': 'На Рудаки',
      'seatName': 'PC-07',
      'endedAtUtc': _now.subtract(const Duration(hours: 2)).toIso8601String(),
    });

Map<String, dynamic> _openSession() => {
      'sessionId': 's1',
      'seatId': 'seat-1',
      'seatName': 'PC-07',
      'startedAtUtc': _now.subtract(const Duration(minutes: 30)).toIso8601String(),
      'durationMode': 'open',
      'remainingSeconds': null,
      'accruedCostMinorUnits': 4500,
      'currencyCode': 'TJS',
    };

/// Заглушка сервера главной: свой ответ на приглашение оценить визит и на отправку отзыва.
FakeHttpClient _serve({
  (String, int) pending = ('', 204),
  (String, int) submit = ('{"rating":5,"reviewCount":1,"items":[]}', 200),
  Map<String, dynamic>? session,
}) =>
    FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/dashboard' => (_dashboardJson(session: session), 200),
          '/api/me/features' => ('{"features":["online_topup"]}', 200),
          '/api/me/reviews/pending' => pending,
          '/api/me/reviews' => submit,
          _ => ('[]', 200),
        });

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: DashboardScreen(
        api: api,
        displayName: 'Иван',
        organization: _club,
        phoneVerified: true,
        features: const ['online_topup'],
        clock: () => _now,
      ),
    );

PlayerApiClient clientWith(FakeHttpClient inner) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: inner);

Future<void> unmount(WidgetTester tester) => tester.pumpWidget(const SizedBox.shrink());

void main() {
  testWidgets('свежий неоценённый визит приглашает поставить оценку', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(pending: (_pendingJson(), 200)))));
    await tester.pumpAndSettle();

    expect(find.text('Как прошёл визит?'), findsOneWidget);
    expect(find.text('PC-07 в клубе На Рудаки'), findsOneWidget);
    await unmount(tester);
  });

  // Приглашение оценить визит, которого не было, раздражает сильнее, чем его отсутствие:
  // сервер отвечает пустым 204, и приложение молчит.
  testWidgets('когда оценивать нечего, приглашения нет', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve())));
    await tester.pumpAndSettle();

    expect(find.text('Как прошёл визит?'), findsNothing);
    await unmount(tester);
  });

  // Посреди игры спрашивать об ушедшем вечере не время: экран открывают ради своей сессии.
  testWidgets('во время сессии приглашение не мешается', (tester) async {
    await tester.pumpWidget(harness(clientWith(
      _serve(pending: (_pendingJson(), 200), session: _openSession()),
    )));
    await tester.pumpAndSettle();

    expect(find.text('Как прошёл визит?'), findsNothing);
    await unmount(tester);
  });

  testWidgets('«не сейчас» убирает приглашение сразу', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(pending: (_pendingJson(), 200)))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Не сейчас'));
    await tester.pumpAndSettle();

    expect(find.text('Как прошёл визит?'), findsNothing);
    await unmount(tester);
  });

  testWidgets('оценка и комментарий уходят на сервер', (tester) async {
    final http = _serve(pending: (_pendingJson(), 200));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Оценить'));
    await tester.pumpAndSettle();

    // Четвёртая звезда: считаем слева направо, как их и нажимают.
    await tester.tap(find.byIcon(Icons.star_outline_rounded).at(3));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'Хороший зал');
    await tester.tap(find.text('Отправить отзыв'));
    await tester.pumpAndSettle();

    expect(http.bodies.single, {'sessionId': 's1', 'rating': 4, 'comment': 'Хороший зал'});
    expect(find.text('Спасибо, отзыв отправлен'), findsOneWidget);
    expect(find.text('Как прошёл визит?'), findsNothing);
    await unmount(tester);
  });

  // Без оценки отправлять нечего, и кнопка говорит, чего именно ждёт, вместо того чтобы
  // просто быть серой.
  testWidgets('без оценки кнопка называет недостающий шаг', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(pending: (_pendingJson(), 200)))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Оценить'));
    await tester.pumpAndSettle();

    expect(find.text('Поставьте оценку'), findsOneWidget);
    expect(
      tester.widget<FilledButton>(find.ancestor(
        of: find.text('Поставьте оценку'),
        matching: find.byType(FilledButton),
      )).onPressed,
      isNull,
    );
    await unmount(tester);
  });

  // Про этот вечер уже написано — это не сбой отправки, и звать «повторить» здесь значило бы
  // приглашать к тому, что снова не получится.
  testWidgets('повторный отзыв о том же визите назван своей причиной', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(
      pending: (_pendingJson(), 200),
      submit: ('{"error":"already_reviewed"}', 409),
    ))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Оценить'));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.star_outline_rounded).at(4));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Отправить отзыв'));
    await tester.pumpAndSettle();

    expect(find.text('Об этом визите вы уже написали'), findsOneWidget);
    await unmount(tester);
  });

  testWidgets('сбой отправки не выдаётся за отправленный отзыв', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(
      pending: (_pendingJson(), 200),
      submit: ('{"error":"boom"}', 500),
    ))));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Оценить'));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.star_outline_rounded).at(4));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Отправить отзыв'));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось отправить отзыв'), findsOneWidget);
    expect(find.text('Спасибо, отзыв отправлен'), findsNothing);
    await unmount(tester);
  });
}
