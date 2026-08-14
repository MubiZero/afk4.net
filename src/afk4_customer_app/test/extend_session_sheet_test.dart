import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/dashboard/extend_session_sheet.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

import 'support/fake_http.dart';

Widget harness(PlayerApiClient api, {void Function(int?)? onClosed}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () async {
              final result = await showModalBottomSheet<int>(
                context: context,
                builder: (_) => ExtendSessionSheet(api: api, sessionId: 's1'),
              );
              onClosed?.call(result);
            },
            child: const Text('открыть'),
          ),
        ),
      ),
    );

FakeHttpClient _serve(int status, {String body = '{}'}) =>
    FakeHttpClient((_) => (body, status));

Future<void> openSheet(WidgetTester tester) async {
  await tester.tap(find.text('открыть'));
  await tester.pumpAndSettle();
}

void main() {
  // Пустой выбор заставлял бы игрока решать с нуля посреди игры. Час — то, что берут чаще
  // всего, и он же стоит на кнопке до первого касания.
  testWidgets('час предвыбран, кнопка называет выбранное время', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(baseUrl: 'https://api', httpClient: _serve(200))));
    await openSheet(tester);

    expect(find.text('Продлить на 1 час'), findsOneWidget);
  });

  testWidgets('выбор другого варианта меняет и кнопку', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(baseUrl: 'https://api', httpClient: _serve(200))));
    await openSheet(tester);

    await tester.tap(find.text('30 минут'));
    await tester.pumpAndSettle();

    expect(find.text('Продлить на 30 минут'), findsOneWidget);
  });

  testWidgets('продление уходит на сервер с выбранными минутами и ключом', (tester) async {
    final http = _serve(200);
    int? closedWith;
    await tester.pumpWidget(harness(
      PlayerApiClient(baseUrl: 'https://api', httpClient: http),
      onClosed: (result) => closedWith = result,
    ));
    await openSheet(tester);

    await tester.tap(find.text('2 часа'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Продлить на 2 часа'));
    await tester.pumpAndSettle();

    expect(http.paths, ['/api/me/sessions/s1/extend']);
    expect(http.bodies.single['additionalMinutes'], 120);
    // Ключ идемпотентности — единственная защита от двойного списания, когда ответ потерялся.
    expect(http.bodies.single['idempotencyKey'], isA<String>());
    expect((http.bodies.single['idempotencyKey'] as String).isNotEmpty, isTrue);
    // Лист возвращает выбранное время: сообщение об успехе показывает главный экран.
    expect(closedWith, 120);
  });

  // Деньги — самая частая причина отказа, и «что-то пошло не так» здесь бесполезно: игрок
  // должен понять, что дело в кошельке, и куда идти.
  testWidgets('нехватка денег объясняется словами про кошелёк', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: _serve(409, body: '{"error":"insufficient_balance"}'),
    )));
    await openSheet(tester);
    await tester.tap(find.text('Продлить на 1 час'));
    await tester.pumpAndSettle();

    expect(find.textContaining('не хватает денег'), findsOneWidget);
    // Лист остаётся открытым: игроку есть что здесь сделать после пополнения.
    expect(find.text('Продлить сессию'), findsOneWidget);
  });

  testWidgets('завершившаяся сессия названа завершившейся, а не общей ошибкой', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: _serve(404, body: '{}'),
    )));
    await openSheet(tester);
    await tester.tap(find.text('Продлить на 1 час'));
    await tester.pumpAndSettle();

    expect(find.text('Сессия уже завершилась'), findsOneWidget);
  });

  testWidgets('прочий отказ сервера не выдаёт себя за успех', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: _serve(500, body: '{}'),
    )));
    await openSheet(tester);
    await tester.tap(find.text('Продлить на 1 час'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Не удалось продлить'), findsOneWidget);
  });
}
