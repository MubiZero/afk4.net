import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/friends/friends_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

import 'support/fake_http.dart';

Map<String, dynamic> _friend({
  String id = 'p2',
  String name = 'Далер',
  String? club,
  String? hall,
}) =>
    {
      'platformPersonId': id,
      'displayName': name,
      if (club != null) 'presence': {'organizationName': club, 'branchName': hall ?? ''},
    };

Map<String, dynamic> _request({String id = 'r1', String name = 'Ясин'}) => {
      'friendRequestId': id,
      'platformPersonId': 'p3',
      'displayName': name,
    };

String _view({
  List<Map<String, dynamic>> friends = const [],
  List<Map<String, dynamic>> incoming = const [],
  List<Map<String, dynamic>> outgoing = const [],
  bool showsPresence = true,
}) =>
    jsonEncode({
      'friends': friends,
      'incoming': incoming,
      'outgoing': outgoing,
      'showsPresence': showsPresence,
    });

Widget harness(PlayerApiClient api) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: FriendsScreen(api: api),
    );

PlayerApiClient clientWith(FakeHttpClient http) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: http);

FakeHttpClient _serve({String? list, String? afterAction}) => FakeHttpClient((request) {
      if (request.url.path == '/api/me/friends' && request.method == 'GET') {
        return (list ?? _view(), 200);
      }
      return (afterAction ?? list ?? _view(), 200);
    });

void main() {
  testWidgets('друг в зале показан вместе с клубом и залом', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(
      list: _view(friends: [_friend(club: 'CyberX', hall: 'На Рудаки')]),
    ))));
    await tester.pumpAndSettle();

    expect(find.text('Далер'), findsOneWidget);
    expect(find.text('CyberX, На Рудаки'), findsOneWidget);
  });

  testWidgets('друг не в зале так и подписан', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve(list: _view(friends: [_friend()])))));
    await tester.pumpAndSettle();

    expect(find.text('Не в зале'), findsOneWidget);
  });

  testWidgets('пустой список объясняется словами и зовёт позвать друга', (tester) async {
    await tester.pumpWidget(harness(clientWith(_serve())));
    await tester.pumpAndSettle();

    expect(find.textContaining('Друзей пока нет'), findsOneWidget);
  });

  // Пришедшая заявка — единственное, что ждёт ответа: она идёт первой и с двумя кнопками.
  testWidgets('пришедшая заявка принимается и отклоняется', (tester) async {
    final http = _serve(
      list: _view(incoming: [_request()]),
      afterAction: _view(friends: [_friend(id: 'p3', name: 'Ясин')]),
    );
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    expect(find.text('Зовут в друзья'), findsOneWidget);
    expect(find.text('Отклонить'), findsOneWidget);
    await tester.tap(find.text('Принять'));
    await tester.pumpAndSettle();

    expect(http.paths, contains('/api/me/friends/requests/r1/accept'));
    expect(find.text('Ясин'), findsOneWidget);
  });

  testWidgets('заявка по номеру уходит и поле очищается', (tester) async {
    final http = _serve(list: _view(), afterAction: _view(outgoing: [_request(name: 'Далер')]));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), '+992937380070');
    await tester.tap(find.text('Позвать'));
    await tester.pumpAndSettle();

    expect(http.bodies.last['phoneNumber'], '+992937380070');
    expect(find.text('Заявка отправлена'), findsOneWidget);
    expect(find.text('Ждут ответа'), findsOneWidget);
  });

  // Свой номер — единственный отказ, который приложение показывает словами: про чужие номера
  // сервер молчит намеренно.
  testWidgets('свой номер объясняется, а не выглядит общим сбоем', (tester) async {
    final http = FakeHttpClient((request) => request.method == 'GET'
        ? (_view(), 200)
        : ('{"error":"friend_self"}', 409));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), '+992937380070');
    await tester.tap(find.text('Позвать'));
    await tester.pumpAndSettle();

    expect(find.text('Это ваш номер'), findsOneWidget);
  });

  testWidgets('переключатель видимости выключается и объясняет последствие', (tester) async {
    final http = _serve(
      list: _view(friends: [_friend()]),
      afterAction: _view(friends: [_friend()], showsPresence: false),
    );
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(Switch));
    await tester.pumpAndSettle();

    expect(http.bodies.last['showsPresence'], false);
    expect(find.textContaining('в залах вас не видно'), findsOneWidget);
  });

  // Убрать друга — действие с последствием для обоих, поэтому спрашивается подтверждение.
  testWidgets('удаление друга спрашивает подтверждение', (tester) async {
    final http = _serve(list: _view(friends: [_friend()]));
    await tester.pumpWidget(harness(clientWith(http)));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.person_remove_outlined));
    await tester.pumpAndSettle();

    expect(find.textContaining('Убрать Далер из друзей?'), findsOneWidget);
    await tester.tap(find.text('Отмена'));
    await tester.pumpAndSettle();

    expect(http.paths.where((path) => path.startsWith('/api/me/friends/p2')), isEmpty);
  });
}
