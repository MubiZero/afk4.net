import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/dto.dart';
import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/auth/player_session.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/push/push_messages.dart';
import 'package:afk4_customer_app/push/push_notification.dart';
import 'package:afk4_customer_app/shell/app_shell.dart';
import 'package:afk4_customer_app/shell/push_note.dart';

import 'support/fake_http.dart';

const _club = Organization(
  organizationId: 'o1',
  slug: 'cyberx',
  name: 'CyberX',
  logoUrl: null,
  accentColor: null,
);

final _now = DateTime.utc(2026, 9, 3, 20, 0, 0);

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

/// Подменные уведомления: тест сам решает, что и когда «пришло».
class _FakePushMessages implements PushMessages {
  _FakePushMessages({this.initial});

  final PushNotification? initial;
  final StreamController<PushNotification> opened = StreamController<PushNotification>.broadcast();
  final StreamController<PushNotification> foreground = StreamController<PushNotification>.broadcast();

  @override
  Future<PushNotification?> initialMessage() async => initial;

  @override
  Stream<PushNotification> get onOpenedApp => opened.stream;

  @override
  Stream<PushNotification> get onForegroundMessage => foreground.stream;

  Future<void> dispose() async {
    await opened.close();
    await foreground.close();
  }
}

FakeHttpClient _serve() => FakeHttpClient((request) => switch (request.url.path) {
      '/api/me/dashboard' => (
          jsonEncode({
            'walletBalance': {'currencyCode': 'TJS', 'minorUnits': 120050},
            'heldBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
            'debtBalance': {'currencyCode': 'TJS', 'minorUnits': 0},
            'activeSession': null,
          }),
          200
        ),
      '/api/me/features' => ('{"features":["online_topup","online_booking","player_shop"]}', 200),
      '/api/me/reservations' => ('[]', 200),
      '/api/me/shop/catalog' => ('{"categories":[]}', 200),
      '/api/me/shop/orders' => ('{"items":[],"nextCursor":null}', 200),
      _ => ('{"items":[],"nextCursor":null}', 200),
    });

Me _me() => Me.fromJson({
      'person': {
        'platformPersonId': 'pp1',
        'phoneNumber': '+992900000000',
        'displayName': 'Иван',
        'preferredLocale': 'ru',
        'phoneVerified': true,
        'pinSet': false,
        'networkBanned': false,
        'networkBanReason': null,
      },
      'clubs': [
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

Widget _harness(FakeHttpClient http, _FakePushMessages messages) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: AppShell(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        session: _session,
        organization: _club,
        me: _me(),
        pushMessages: messages,
        onSignOut: () {},
        onChangeClub: () {},
        onLocaleChanged: (_) {},
        clock: () => _now,
      ),
    );

void main() {
  testWidgets('нажатие на уведомление о брони открывает раздел броней', (tester) async {
    final messages = _FakePushMessages();
    addTearDown(messages.dispose);

    await tester.pumpWidget(_harness(_serve(), messages));
    await tester.pumpAndSettle();

    messages.opened.add(const PushNotification(
      title: 'Скоро бронь',
      body: 'Через час вас ждут за PC-07.',
      template: 'player.reservation_soon',
    ));
    await tester.pumpAndSettle();

    final bar = tester.widget<NavigationBar>(find.byType(NavigationBar));
    expect(bar.selectedIndex, 1, reason: 'второй раздел — «Брони»');
  });

  testWidgets('уведомление о пополнении открывает кошелёк', (tester) async {
    final messages = _FakePushMessages();
    addTearDown(messages.dispose);

    await tester.pumpWidget(_harness(_serve(), messages));
    await tester.pumpAndSettle();

    messages.opened.add(const PushNotification(
      body: 'Кошелёк пополнен на 50 с.',
      template: 'player.balance_topped_up',
    ));
    await tester.pumpAndSettle();

    expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 2);
  });

  // Запуск с закрытого приложения — тот же путь, но уведомление приходит один раз и до
  // первого кадра. Потерять его здесь значит открыть главную вместо обещанного экрана.
  testWidgets('запуск нажатием на уведомление ведёт туда же', (tester) async {
    final messages = _FakePushMessages(
      initial: const PushNotification(body: 'Скоро бронь', template: 'player.reservation_soon'),
    );
    addTearDown(messages.dispose);

    await tester.pumpWidget(_harness(_serve(), messages));
    await tester.pumpAndSettle();

    expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 1);
  });

  testWidgets('уведомление в открытом приложении показывается сообщением с переходом', (tester) async {
    final messages = _FakePushMessages();
    addTearDown(messages.dispose);

    await tester.pumpWidget(_harness(_serve(), messages));
    await tester.pumpAndSettle();

    messages.foreground.add(const PushNotification(
      body: 'Кошелёк пополнен на 50 с.',
      template: 'player.balance_topped_up',
    ));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Кошелёк пополнен на 50 с.'), findsOneWidget);
    // Игрока не выдёргивают с экрана: раздел прежний, пока он сам не нажмёт.
    expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 0);

    await tester.tap(find.text('Открыть'));
    await tester.pumpAndSettle();

    expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 2);
  });

  testWidgets('незнакомое событие не двигает игрока никуда', (tester) async {
    final messages = _FakePushMessages();
    addTearDown(messages.dispose);

    await tester.pumpWidget(_harness(_serve(), messages));
    await tester.pumpAndSettle();

    messages.opened.add(const PushNotification(body: 'Что-то новое', template: 'staff.invite'));
    await tester.pumpAndSettle();

    expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 0);
  });

  // Сообщение без текста показывать нечем: пустая полоса внизу экрана — это шум, а не новость.
  testWidgets('уведомление без текста ничего не показывает', (tester) async {
    final messages = _FakePushMessages();
    addTearDown(messages.dispose);

    await tester.pumpWidget(_harness(_serve(), messages));
    await tester.pumpAndSettle();

    messages.foreground.add(const PushNotification(template: 'player.balance_topped_up'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.byType(PushNote), findsNothing);
  });
}
