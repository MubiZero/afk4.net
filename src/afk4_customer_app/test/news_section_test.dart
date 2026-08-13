import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/news/news_section.dart';

import 'support/fake_http.dart';

Map<String, dynamic> _item({required String id, required String title, String body = ''}) => {
      'id': id,
      'branchId': null,
      'title': title,
      'body': body,
      'imageUrl': null,
      'isPublished': true,
      'publishAtUtc': '2026-08-10T09:00:00Z',
      'expiresAtUtc': null,
      'createdAtUtc': '2026-08-10T09:00:00Z',
      'updatedAtUtc': '2026-08-10T09:00:00Z',
    };

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(body: NewsSection(api: api)),
    );

PlayerApiClient clientWith(String body, {int status = 200}) => PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: FakeHttpClient((_) => (body, status)),
    );

void main() {
  testWidgets('новости клуба показываются с заголовком и текстом', (tester) async {
    await tester.pumpWidget(harness(clientWith(jsonEncode([
      _item(id: 'n1', title: 'Ночь скидок', body: 'С 22:00 до 8:00 час дешевле'),
    ]))));
    await tester.pumpAndSettle();

    expect(find.text('Новости клуба'), findsOneWidget);
    expect(find.text('Ночь скидок'), findsOneWidget);
    expect(find.text('С 22:00 до 8:00 час дешевле'), findsOneWidget);
  });

  // Главный экран — про сессию и деньги. Лента новостей его вытеснила бы.
  testWidgets('на главной показывается не больше трёх новостей', (tester) async {
    await tester.pumpWidget(harness(clientWith(jsonEncode([
      for (var index = 1; index <= 5; index++) _item(id: 'n$index', title: 'Новость $index'),
    ]))));
    await tester.pumpAndSettle();

    expect(find.text('Новость 1'), findsOneWidget);
    expect(find.text('Новость 3'), findsOneWidget);
    expect(find.text('Новость 4'), findsNothing);
  });

  // Пустая рамка «новостей нет» — рассказ об отсутствии на самом дорогом экране.
  testWidgets('без новостей блок исчезает целиком', (tester) async {
    await tester.pumpWidget(harness(clientWith('[]')));
    await tester.pumpAndSettle();

    expect(find.text('Новости клуба'), findsNothing);
  });

  testWidgets('сбой новостей не ломает экран и не показывает ошибку', (tester) async {
    await tester.pumpWidget(harness(clientWith('{"error":"boom"}', status: 500)));
    await tester.pumpAndSettle();

    expect(find.text('Новости клуба'), findsNothing);
    expect(tester.takeException(), isNull);
  });
}
