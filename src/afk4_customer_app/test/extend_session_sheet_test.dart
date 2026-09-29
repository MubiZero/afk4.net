import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/dashboard/extend_session_sheet.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';

import 'support/fake_http.dart';

Widget harness(PlayerApiClient api, {void Function(int?)? onClosed, VoidCallback? onTopUp}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () async {
              final result = await showModalBottomSheet<int>(
                context: context,
                builder: (_) => ExtendSessionSheet(api: api, sessionId: 's1', onTopUp: onTopUp),
              );
              onClosed?.call(result);
            },
            child: const Text('открыть'),
          ),
        ),
      ),
    );

Map<String, dynamic> _money(int minor) => {'currencyCode': 'TJS', 'minorUnits': minor};

Map<String, dynamic> _option(int minutes, int amount, {bool affordable = true}) => {
      'minutes': minutes,
      'billableMinutes': minutes,
      'endsAtUtc': DateTime.utc(2026, 9, 28, 18).add(Duration(minutes: minutes)).toIso8601String(),
      'amount': _money(amount),
      'balanceAfter': _money(5000 - amount),
      'affordable': affordable,
    };

String _offers({String? unavailableReason, List<Map<String, dynamic>>? options}) => jsonEncode({
      'sessionId': 's1',
      'balance': _money(5000),
      'options': options ?? [_option(30, 1500), _option(60, 3000), _option(120, 6000, affordable: false)],
      'unavailableReason': unavailableReason,
    });

/// Цены — с сервера; POST продления отвечает кодом [status] и телом [body].
FakeHttpClient _serve({int status = 200, String body = '{}', String? offers}) => FakeHttpClient((request) =>
    request.method == 'GET' ? (offers ?? _offers(), 200) : (body, status));

Future<void> openSheet(WidgetTester tester) async {
  await tester.tap(find.text('открыть'));
  await tester.pumpAndSettle();
}

void main() {
  // Пустой выбор заставлял бы игрока решать с нуля посреди игры. Час — то, что берут чаще
  // всего, и он же стоит на кнопке до первого касания.
  testWidgets('час предвыбран, кнопка называет выбранное время', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(baseUrl: 'https://api', httpClient: _serve())));
    await openSheet(tester);

    expect(find.text('Продлить на 1 час'), findsOneWidget);
  });

  // Раньше лист умножал цену часа на минуты и писал «примерно»: минимум и шаг тарифа знает
  // только сервер. Теперь сумма и остаток — его.
  testWidgets('сумма и остаток — с сервера, для выбранного варианта', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(baseUrl: 'https://api', httpClient: _serve())));
    await openSheet(tester);

    expect(find.textContaining('Спишется 30'), findsOneWidget);
    expect(find.textContaining('останется 20'), findsOneWidget);

    await tester.tap(find.text('30 минут'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Спишется 15'), findsOneWidget);
    expect(find.text('Продлить на 30 минут'), findsOneWidget);
  });

  testWidgets('на что не хватает денег — не выбрать', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(baseUrl: 'https://api', httpClient: _serve())));
    await openSheet(tester);

    await tester.tap(find.text('2 часа'));
    await tester.pumpAndSettle();

    expect(find.text('Продлить на 1 час'), findsOneWidget);
  });

  testWidgets('продление уходит на сервер с выбранными минутами и ключом', (tester) async {
    final http = _serve();
    int? closedWith;
    await tester.pumpWidget(harness(
      PlayerApiClient(baseUrl: 'https://api', httpClient: http),
      onClosed: (result) => closedWith = result,
    ));
    await openSheet(tester);

    await tester.tap(find.text('30 минут'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Продлить на 30 минут'));
    await tester.pumpAndSettle();

    expect(http.paths, ['/api/me/sessions/s1/extend-offers', '/api/me/sessions/s1/extend']);
    expect(http.bodies.single['additionalMinutes'], 30);
    // Ключ идемпотентности — единственная защита от двойного списания, когда ответ потерялся.
    expect((http.bodies.single['idempotencyKey'] as String).isNotEmpty, isTrue);
    // Лист возвращает выбранное время: сообщение об успехе показывает главный экран.
    expect(closedWith, 30);
  });

  testWidgets('сессию по пакету лист не продлевает деньгами, а объясняет почему', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: _serve(offers: _offers(unavailableReason: 'package_session', options: [])),
    )));
    await openSheet(tester);

    expect(find.textContaining('продлевают новым стартом'), findsOneWidget);
    expect(find.textContaining('Продлить на'), findsNothing);
  });

  // Деньги — самая частая причина отказа, и «что-то пошло не так» здесь бесполезно: игрок
  // должен понять, что дело в кошельке, и куда идти.
  testWidgets('нехватка денег объясняется словами про кошелёк', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: _serve(status: 409, body: '{"error":"insufficient_balance"}'),
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
      httpClient: _serve(status: 404),
    )));
    await openSheet(tester);
    await tester.tap(find.text('Продлить на 1 час'));
    await tester.pumpAndSettle();

    expect(find.text('Сессия уже завершилась'), findsOneWidget);
  });

  /// 409 бывает не только про деньги: тариф кончился, столько времени взять нельзя. Раньше
  /// любой конфликт объявлялся нехваткой денег, и человек шёл пополнять кошелёк зря.
  testWidgets('конфликт не про деньги не выдаётся за нехватку денег', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: _serve(status: 409, body: '{"error":"invalid_tariff"}'),
    )));
    await openSheet(tester);
    await tester.tap(find.text('Продлить на 1 час'));
    await tester.pumpAndSettle();

    expect(find.text('Этот тариф больше не действует — выберите другой'), findsOneWidget);
    expect(find.textContaining('не хватает денег'), findsNothing);
  });

  testWidgets('прочий отказ сервера не выдаёт себя за успех', (tester) async {
    await tester.pumpWidget(harness(PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: _serve(status: 500),
    )));
    await openSheet(tester);
    await tester.tap(find.text('Продлить на 1 час'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Не удалось продлить'), findsOneWidget);
  });

  // Раньше лист говорил «пополните на главной» — человеку, уже стоявшему на главной. Теперь
  // пополнение открывается прямо из листа.
  testWidgets('не хватает на продление — лист ведёт к пополнению', (tester) async {
    var toppedUp = false;
    final http = _serve(offers: _offers(options: [_option(60, 9000, affordable: false)]));
    await tester.pumpWidget(harness(PlayerApiClient(baseUrl: 'https://api', httpClient: http), onTopUp: () => toppedUp = true));
    await openSheet(tester);

    await tester.tap(find.text('Пополнить баланс'));
    await tester.pumpAndSettle();

    expect(toppedUp, isTrue);
    expect(find.byType(ExtendSessionSheet), findsNothing);
  });
}
