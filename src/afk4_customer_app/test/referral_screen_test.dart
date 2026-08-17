import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/referral/referral_screen.dart';

import 'support/fake_http.dart';

String _referralJson({
  bool enabled = true,
  String? code = 'K7M4QP',
  int referrerBonus = 5000,
  int inviteeBonus = 3000,
  int minimumTopUp = 10000,
  int invited = 0,
  int rewarded = 0,
  int earned = 0,
  bool hasClaimedCode = false,
  bool canClaimCode = true,
}) =>
    jsonEncode({
      'enabled': enabled,
      'code': code,
      'referrerBonusMinorUnits': referrerBonus,
      'inviteeBonusMinorUnits': inviteeBonus,
      'minimumTopUpMinorUnits': minimumTopUp,
      'currencyCode': 'TJS',
      'invitedCount': invited,
      'rewardedCount': rewarded,
      'earnedMinorUnits': earned,
      'hasClaimedCode': hasClaimedCode,
      'canClaimCode': canClaimCode,
    });

Widget harness(FakeHttpClient http) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ReferralScreen(api: PlayerApiClient(baseUrl: 'https://api', httpClient: http)),
    );

FakeHttpClient _serve(String referral, {(String, int)? claim}) =>
    FakeHttpClient((request) => request.method == 'GET' ? (referral, 200) : (claim ?? ('{}', 500)));

/// Ввод чужого кода стоит внизу экрана: сначала свой код и условия, и до поля надо
/// доскроллить — ровно как настоящему игроку.
Future<void> claimCode(WidgetTester tester, String code) async {
  // Скроллится именно список экрана: у самого поля ввода тоже есть свой Scrollable, и
  // «первый попавшийся» их путает.
  await tester.scrollUntilVisible(
    find.byType(TextField),
    200,
    scrollable: find.descendant(of: find.byType(ListView), matching: find.byType(Scrollable)).first,
  );
  await tester.enterText(find.byType(TextField), code);
  await tester.pumpAndSettle();
  await tester.tap(find.text('Применить код'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('экран показывает код и условия клуба', (tester) async {
    await tester.pumpWidget(harness(_serve(_referralJson())));
    await tester.pumpAndSettle();

    expect(find.text('K7M4QP'), findsOneWidget);
    // Суммы приходят с сервера: их назначает клуб, и у каждого они свои.
    expect(find.textContaining('50,00'), findsOneWidget);
    expect(find.textContaining('100,00'), findsOneWidget);
  });

  // Игрок ждёт бонус сразу после ввода кода, если ему не сказать иначе.
  testWidgets('экран говорит, что платят за пополнение друга, а не за код', (tester) async {
    await tester.pumpWidget(harness(_serve(_referralJson())));
    await tester.pumpAndSettle();

    expect(find.textContaining('пополняет кошелёк'), findsOneWidget);
  });

  testWidgets('клуб без программы честно говорит об этом вместо кода', (tester) async {
    await tester.pumpWidget(harness(_serve(_referralJson(enabled: false, code: null))));
    await tester.pumpAndSettle();

    expect(find.text('Клуб пока не платит за приглашения'), findsOneWidget);
    expect(find.text('Ваш код'), findsNothing);
  });

  testWidgets('код кладётся в буфер обмена', (tester) async {
    final copied = <String>[];
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      (call) async {
        if (call.method == 'Clipboard.setData') {
          copied.add((call.arguments as Map)['text'] as String);
        }
        return null;
      },
    );

    await tester.pumpWidget(harness(_serve(_referralJson())));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Скопировать'));
    await tester.pumpAndSettle();

    expect(copied, ['K7M4QP']);
    expect(find.text('Код скопирован'), findsOneWidget);
  });

  // «Пришло друзей» и «дошло до пополнения» — разные числа, и второе объясняет, почему денег
  // меньше, чем друзей.
  testWidgets('статистика различает пришедших и дошедших до пополнения', (tester) async {
    await tester.pumpWidget(harness(_serve(
      // Числа заведомо не из ряда 1-3: им же подписаны кружки шагов «как это работает».
      _referralJson(invited: 7, rewarded: 4, earned: 5000),
    )));
    await tester.pumpAndSettle();

    expect(find.text('7'), findsOneWidget);
    expect(find.text('4'), findsOneWidget);
    expect(find.text('пришло друзей'), findsOneWidget);
    expect(find.text('дошло до пополнения'), findsOneWidget);
  });

  testWidgets('чужой код уходит на сервер и подтверждается именем', (tester) async {
    final http = _serve(
      _referralJson(),
      claim: ('{"referrerDisplayName":"Фарух"}', 200),
    );
    await tester.pumpWidget(harness(http));
    await tester.pumpAndSettle();

    await claimCode(tester, 'ABC234');

    expect(http.bodies.single['code'], 'ABC234');
    expect(find.textContaining('Фарух'), findsOneWidget);
  });

  // Каждая причина отказа названа своим словом: выход из «это ваш код» и из «окно закрылось»
  // разный, и общая «не получилось» отправляет игрока гадать.
  testWidgets('свой собственный код назван своей причиной', (tester) async {
    await tester.pumpWidget(harness(_serve(
      _referralJson(),
      claim: ('{"error":"referral_own_code"}', 409),
    )));
    await tester.pumpAndSettle();

    await claimCode(tester, 'K7M4QP');

    expect(find.text('Это ваш собственный код'), findsOneWidget);
  });

  testWidgets('закрытое окно названо своей причиной', (tester) async {
    await tester.pumpWidget(harness(_serve(
      _referralJson(),
      claim: ('{"error":"referral_window_closed"}', 409),
    )));
    await tester.pumpAndSettle();

    await claimCode(tester, 'ABC234');

    expect(find.textContaining('время ввода кода уже прошло'), findsOneWidget);
  });

  testWidgets('пришедший по приглашению второй раз код не вводит', (tester) async {
    await tester.pumpWidget(harness(_serve(
      _referralJson(hasClaimedCode: true, canClaimCode: false),
    )));
    await tester.pumpAndSettle();

    expect(find.text('Вы пришли по приглашению друга'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
  });

  testWidgets('когда окно закрылось, поле ввода уступает объяснению', (tester) async {
    await tester.pumpWidget(harness(_serve(
      _referralJson(hasClaimedCode: false, canClaimCode: false),
    )));
    await tester.pumpAndSettle();

    expect(find.byType(TextField), findsNothing);
    expect(find.textContaining('в первые дни'), findsOneWidget);
  });
}
