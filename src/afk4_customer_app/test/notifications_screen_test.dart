import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/notifications/notifications_screen.dart';
import 'package:afk4_customer_app/push/push_notification.dart';

import 'support/fake_http.dart';

String _feedJson({int unread = 1, List<Map<String, dynamic>>? items}) => jsonEncode({
      'notifications': items ??
          [
            {
              'notificationId': 'n1',
              'templateKey': 'player.order_ready',
              'subject': 'Заказ готов',
              'body': 'Кола ждёт вас на PC-01.',
              'branchId': null,
              'createdAtUtc': '2026-09-14T10:00:00Z',
              'isUnread': true,
            },
          ],
      'unreadCount': unread,
    });

Widget harness(FakeHttpClient http, {VoidCallback? onRead}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: NotificationsScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
        onRead: onRead,
      ),
    );

/// Открывает NotificationsScreen как обычный экран — поверх стартовой страницы,
/// нажатием которую можно проверить, что список закрылся тем же переходом, что и живой пуш.
Widget _pushedHarness(FakeHttpClient http, {ValueChanged<PushDestination>? onOpenDestination}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: FilledButton(
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => NotificationsScreen(
                    api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
                    onOpenDestination: onOpenDestination,
                  ),
                ),
              ),
              child: const Text('открыть уведомления'),
            ),
          ),
        ),
      ),
    );

void main() {
  // Пуш был единственным способом узнать о событии: пропущенный прочитать было негде.
  testWidgets('список показывает присланное и помечает прочитанным при открытии', (tester) async {
    var read = false;
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? ('', 204) : (_feedJson(), 200));
    await tester.pumpWidget(harness(http, onRead: () => read = true));
    await tester.pumpAndSettle();

    expect(find.text('Заказ готов'), findsOneWidget);
    expect(find.text('Кола ждёт вас на PC-01.'), findsOneWidget);
    expect(http.paths, contains('/api/me/notifications/read'));
    expect(read, isTrue);
  });

  // Отметка ставится после показа: упавшая отметка не должна прятать уже загруженный список.
  testWidgets('неудачная отметка не отбирает список', (tester) async {
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? ('{}', 500) : (_feedJson(), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    expect(find.text('Заказ готов'), findsOneWidget);
    expect(find.text('Не удалось загрузить уведомления.'), findsNothing);
  });

  testWidgets('пустой список объясняет, что здесь будет', (tester) async {
    final http = FakeHttpClient((request) =>
        request.method == 'POST' ? ('', 204) : (_feedJson(unread: 0, items: const []), 200));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    expect(find.textContaining('Пока ничего не приходило'), findsOneWidget);
  });

  // Сбой загрузки и пустой список — разные ответы: показать первое как второе значит сказать
  // «вам ничего не присылали», ничего не зная.
  testWidgets('сбой загрузки не выдаётся за пустой список', (tester) async {
    final http = FakeHttpClient((_) => ('{}', 500));
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось загрузить уведомления.'), findsOneWidget);
    expect(find.textContaining('Пока ничего не приходило'), findsNothing);
  });

  // Раньше строка истории никуда не вела, хотя тот же пуш на телефоне открывает магазин с
  // заказом — нажатие обязано быть тем же переходом, а не тупиком.
  testWidgets('нажатие на строку ведёт туда же, куда вёл бы тот же пуш', (tester) async {
    PushDestination? opened;
    final http =
        FakeHttpClient((request) => request.method == 'POST' ? ('', 204) : (_feedJson(), 200));
    await tester.pumpWidget(_pushedHarness(http, onOpenDestination: (d) => opened = d));
    await tester.tap(find.text('открыть уведомления'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Заказ готов'));
    await tester.pumpAndSettle();

    // player.order_ready ведёт в магазин — то же самое, что pushDestinationFor отвечает пушу.
    expect(opened, PushDestination.shop);
    // Переход закрывает список, как и уход по живому пуше — экран не остаётся висеть поверх.
    expect(find.text('открыть уведомления'), findsOneWidget);
  });

  // Событие без раздела (`pushDestinationFor` вернул null) нажимать некуда — молчаливый переход
  // наугад хуже отсутствия перехода вовсе.
  testWidgets('событие без раздела не нажимается', (tester) async {
    var opened = false;
    final http = FakeHttpClient((request) => request.method == 'POST'
        ? ('', 204)
        : (
            _feedJson(items: [
              {
                'notificationId': 'n2',
                'templateKey': 'staff.invite',
                'subject': 'Служебное сообщение',
                'body': 'Это не про игрока.',
                'branchId': null,
                'createdAtUtc': '2026-09-14T10:00:00Z',
                'isUnread': true,
              },
            ]),
            200
          ));
    await tester.pumpWidget(_pushedHarness(http, onOpenDestination: (_) => opened = true));
    await tester.tap(find.text('открыть уведомления'));
    await tester.pumpAndSettle();

    final tile = tester.widget<ListTile>(find.byType(ListTile));
    expect(tile.onTap, isNull);

    await tester.tap(find.text('Служебное сообщение'));
    await tester.pumpAndSettle();
    expect(opened, isFalse);
  });
}
