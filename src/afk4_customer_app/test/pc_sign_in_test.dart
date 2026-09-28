import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/auth/player_session.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/play/pc_sign_in_screen.dart';

import 'support/fake_http.dart';

PlayerApiClient _api(FakeHttpClient http) {
  final api = PlayerApiClient(baseUrl: 'https://api', httpClient: http);
  api.updateSession(PlayerSession(
    displayName: 'Азиз',
    phoneVerified: true,
    accessToken: 'token',
    accessTokenExpiresAtUtc: DateTime.utc(2030),
    refreshToken: 'refresh',
    refreshTokenExpiresAtUtc: DateTime.utc(2030),
  ));
  return api;
}

Widget _harness(PlayerApiClient api, PcSignInLink link) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: PcSignInScreen(api: api, initialLink: link, enableCamera: false),
    );

void main() {
  group('parsePcSignInLink', () {
    test('читает код и клуб из QR на мониторе', () {
      final link = parsePcSignInLink('https://afk4.net/s/482913?o=11111111-1111-1111-1111-111111111111');
      expect(link?.code, '482913');
      expect(link?.organizationId, '11111111-1111-1111-1111-111111111111');
    });

    test('годится ссылка без клуба и голый код, набранный руками', () {
      expect(parsePcSignInLink('https://afk4.net/s/482913')?.organizationId, isNull);
      expect(parsePcSignInLink(' 482913 ')?.code, '482913');
    });

    // Чужой QR не должен превращаться в заявку на вход.
    test('чужие QR и неполные коды не принимает', () {
      expect(parsePcSignInLink('https://evil.example/s/482913'), isNull);
      expect(parsePcSignInLink('https://afk4.net/s/48291'), isNull);
      expect(parsePcSignInLink('WIFI:S:club;T:WPA;P:secret;;'), isNull);
    });
  });

  testWidgets('ПК забрал заявку — «Вы вошли на ПК 07», клуб — из QR', (tester) async {
    final http = FakeHttpClient((_) => (
          jsonEncode({
            'claimId': 'c1',
            'status': 'redeemed',
            'expiresAtUtc': '2030-01-01T00:00:00Z',
            'seatLabel': 'ПК 07',
          }),
          200
        ));
    await tester.pumpWidget(_harness(_api(http), const PcSignInLink('482913', organizationId: 'org-2')));
    await tester.pumpAndSettle();

    expect(find.text('Вы вошли на ПК 07'), findsOneWidget);
    expect(http.requests.single.headers['X-AFK4-Organization'], 'org-2');
    expect(http.bodies.single['seatingCode'], '482913');
  });

  testWidgets('неверный код — говорит, что сделать, и даёт попробовать ещё раз', (tester) async {
    final http = FakeHttpClient((_) => (jsonEncode({'error': 'seating_code_invalid'}), 400));
    await tester.pumpWidget(_harness(_api(http), const PcSignInLink('482913')));
    await tester.pumpAndSettle();

    expect(find.textContaining('Код не подошёл'), findsOneWidget);
    expect(find.text('Попробовать ещё раз'), findsOneWidget);
  });
}
