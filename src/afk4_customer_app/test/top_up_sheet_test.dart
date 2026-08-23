import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/wallet/top_up_sheet.dart';

import 'support/fake_http.dart';

/// Пополнение с телефона: игрок платит в приложении банка, а не идёт к стойке.
///
/// Бэкенд умел это давно — создавал заказ в банке, отдавал ссылку и опрашивал статус, — но
/// в приложении двери к нему не было: заявка всегда уходила как «зачислите на стойке».

String _intent({String? deepLink, String? payUrl}) => jsonEncode({
      'paymentIntentId': 'i1',
      'amountMinorUnits': 10000,
      'currencyCode': 'TJS',
      'state': 'pending',
      'purpose': 'wallet_topup',
      'method': deepLink == null && payUrl == null ? 'counter' : 'eskhata',
      'createdAtUtc': '2026-08-24T10:00:00Z',
      'isExpired': false,
      'payUrl': payUrl,
      'deepLink': deepLink,
    });

String _methods({required bool online}) => jsonEncode({'counter': true, 'online': online});

class _Opener {
  final List<Uri> opened = [];
  bool succeed = true;

  Future<bool> call(Uri uri) async {
    opened.add(uri);
    return succeed;
  }
}

Widget _harness(PlayerApiClient api, {Future<bool> Function(Uri)? open}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: TopUpSheet(
          api: api,
          currencyCode: 'TJS',
          intents: const [],
          openLink: open,
        ),
      ),
    );

PlayerApiClient _client(FakeHttpClient http) =>
    PlayerApiClient(baseUrl: 'https://api', httpClient: http);

Future<void> _enterAmount(WidgetTester tester, String amount) async {
  await tester.enterText(find.byType(TextField).first, amount);
  await tester.pump();
}

void main() {
  testWidgets('клуб без онлайн-оплаты предлагает только стойку', (tester) async {
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/wallet/top-up-methods' => (_methods(online: false), 200),
          _ => (_intent(), 200),
        });
    await tester.pumpWidget(_harness(_client(http)));
    await tester.pumpAndSettle();

    expect(find.text('Оплатить онлайн'), findsNothing);
    expect(find.text('Внести на стойке'), findsOneWidget);
  });

  testWidgets('онлайн-оплата уводит в приложение банка', (tester) async {
    final opener = _Opener();
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/wallet/top-up-methods' => (_methods(online: true), 200),
          '/api/me/wallet/top-up-intent' => (_intent(deepLink: 'eskhata://pay/abc'), 200),
          _ => ('{"payment":"pending"}', 200),
        });
    await tester.pumpWidget(_harness(_client(http), open: opener.call));
    await tester.pumpAndSettle();

    await _enterAmount(tester, '100');
    await tester.tap(find.text('Оплатить онлайн'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(opener.opened.single.toString(), 'eskhata://pay/abc');
    expect(http.bodies.last['method'], 'eskhata');
    // Пока банк молчит, лист остаётся на экране и говорит, чего он ждёт.
    expect(find.textContaining('банк'), findsWidgets);
  });

  /// Приложения банка на телефоне может не быть — тогда платят в браузере, а не упираются
  /// в тишину.
  testWidgets('без приложения банка открывается страница оплаты', (tester) async {
    final opener = _Opener()..succeed = false;
    final http = FakeHttpClient((request) => switch (request.url.path) {
          '/api/me/wallet/top-up-methods' => (_methods(online: true), 200),
          '/api/me/wallet/top-up-intent' =>
            (_intent(deepLink: 'eskhata://pay/abc', payUrl: 'https://bank.test/invoices/abc'), 200),
          _ => ('{"payment":"pending"}', 200),
        });
    await tester.pumpWidget(_harness(_client(http), open: opener.call));
    await tester.pumpAndSettle();

    await _enterAmount(tester, '100');
    await tester.tap(find.text('Оплатить онлайн'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(opener.opened.map((uri) => uri.toString()),
        ['eskhata://pay/abc', 'https://bank.test/invoices/abc']);
  });

  testWidgets('оплаченная заявка закрывает лист', (tester) async {
    var polls = 0;
    final http = FakeHttpClient((request) {
      if (request.url.path == '/api/me/wallet/top-up-methods') return (_methods(online: true), 200);
      if (request.url.path == '/api/me/wallet/top-up-intent') {
        return (_intent(deepLink: 'eskhata://pay/abc'), 200);
      }
      polls++;
      return ('{"payment":"${polls >= 2 ? 'paid' : 'pending'}"}', 200);
    });
    await tester.pumpWidget(_harness(_client(http), open: (_) async => true));
    await tester.pumpAndSettle();

    await _enterAmount(tester, '100');
    await tester.tap(find.text('Оплатить онлайн'));
    await tester.pump();
    await tester.pump(const Duration(seconds: 4));
    await tester.pump(const Duration(seconds: 4));
    await tester.pumpAndSettle();

    expect(polls, greaterThanOrEqualTo(2));
    expect(find.byType(TopUpSheet), findsNothing);
  });
}
