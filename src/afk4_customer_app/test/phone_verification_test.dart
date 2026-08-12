import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/phone/phone_verification_sheet.dart';

import 'support/fake_http.dart';

String _started() => jsonEncode({'expiresInSeconds': 300, 'resendAfterSeconds': 60});

Widget harness(FakeHttpClient http, {ValueChanged<bool?>? onClosed}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () async {
                final result = await showModalBottomSheet<bool>(
                  context: context,
                  isScrollControlled: true,
                  builder: (_) => PhoneVerificationSheet(
                    api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
                    initialPhone: '+992937380070',
                  ),
                );
                onClosed?.call(result);
              },
              child: const Text('открыть'),
            ),
          ),
        ),
      ),
    );

Future<void> open(WidgetTester tester) async {
  await tester.tap(find.text('открыть'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('код запрашивается на номер и подтверждается', (tester) async {
    bool? result;
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/phone/start-verification' => (_started(), 200),
          _ => (jsonEncode({'phone': '+992937380070'}), 200),
        });
    await tester.pumpWidget(harness(http, onClosed: (value) => result = value));
    await open(tester);

    await tester.tap(find.text('Прислать код'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Код отправлен'), findsOneWidget);

    await tester.enterText(find.byType(TextField), '123456');
    await tester.tap(find.text('Подтвердить'));
    await tester.pumpAndSettle();

    expect(http.bodies.last, {'code': '123456'});
    expect(result, isTrue);
  });

  // Номер из профиля подставлен заранее: чаще всего подтверждают именно его.
  testWidgets('номер из профиля не нужно набирать заново', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_started(), 200))));
    await open(tester);

    expect(find.text('+992937380070'), findsOneWidget);
  });

  // «Не удалось» здесь бесполезно: «подождите» и «проверьте номер» требуют разных действий.
  testWidgets('причина отказа называется словами', (tester) async {
    for (final (status, message) in [
      (400, 'Проверьте номер телефона'),
      (429, 'Код уже отправлен — подождите немного'),
      (502, 'SMS не отправилась. Попробуйте позже'),
    ]) {
      await tester.pumpWidget(
          harness(FakeHttpClient((_) => ('{"error":"nope"}', status))));
      await open(tester);

      await tester.tap(find.text('Прислать код'));
      await tester.pumpAndSettle();

      expect(find.text(message), findsOneWidget, reason: 'код состояния $status');
      await tester.pumpWidget(const SizedBox.shrink());
    }
  });

  testWidgets('неверный код называется неверным, а не общей ошибкой', (tester) async {
    final http = FakeHttpClient((request) =>
        request.url.path == '/api/me/phone/start-verification'
            ? (_started(), 200)
            : ('{"error":"invalid_code"}', 400));
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.tap(find.text('Прислать код'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '000000');
    await tester.tap(find.text('Подтвердить'));
    await tester.pumpAndSettle();

    expect(find.text('Неверный код'), findsOneWidget);
  });

  // Устаревший код нельзя починить повторным вводом — только новым кодом.
  testWidgets('устаревший код возвращает к запросу нового', (tester) async {
    final http = FakeHttpClient((request) =>
        request.url.path == '/api/me/phone/start-verification'
            ? (_started(), 200)
            : ('{"error":"code_expired"}', 410));
    await tester.pumpWidget(harness(http));
    await open(tester);

    await tester.tap(find.text('Прислать код'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '000000');
    await tester.tap(find.text('Подтвердить'));
    await tester.pumpAndSettle();

    expect(find.text('Код устарел — запросите новый'), findsOneWidget);
    expect(find.text('Прислать код'), findsOneWidget);
  });

  // Молча заблокированная кнопка выглядит как поломка.
  testWidgets('повторная отправка ждёт с видимым отсчётом', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_started(), 200))));
    await open(tester);

    await tester.tap(find.text('Прислать код'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Прислать заново можно через'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });
}
