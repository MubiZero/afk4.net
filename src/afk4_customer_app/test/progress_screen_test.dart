import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/progress/progress_screen.dart';

import 'support/fake_http.dart';

String _progressJson({
  int level = 3,
  int visitCount = 12,
  int playedMinutes = 1080,
  int? minutesToNextLevel = 720,
  List<Map<String, dynamic>>? achievements,
}) =>
    jsonEncode({
      'level': level,
      'visitCount': visitCount,
      'playedMinutes': playedMinutes,
      'minutesToNextLevel': minutesToNextLevel,
      'achievements': achievements ??
          [
            {
              'code': 'first_visit',
              'progress': 1,
              'target': 1,
              'unlockedAtUtc': '2026-07-01T18:00:00Z',
            },
            {'code': 'veteran', 'progress': 12, 'target': 50, 'unlockedAtUtc': null},
          ],
    });

Widget harness(FakeHttpClient http) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ProgressScreen(
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: http),
      ),
    );

void main() {
  testWidgets('показывает уровень, часы и визиты', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_progressJson(), 200))));
    await tester.pumpAndSettle();

    expect(find.text('Уровень 3'), findsOneWidget);
    expect(find.text('18 ч за ПК · 12 визитов'), findsOneWidget);
    expect(find.text('До следующего уровня — 12 ч'), findsOneWidget);
  });

  // «До следующего уровня 0 ч» звучало бы как подсчёт, который вот-вот сорвётся.
  testWidgets('на последнем уровне сказано, что расти дальше некуда', (tester) async {
    await tester.pumpWidget(harness(
      FakeHttpClient((_) => (_progressJson(minutesToNextLevel: null), 200)),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Максимальный уровень'), findsOneWidget);
    expect(find.textContaining('До следующего уровня'), findsNothing);
  });

  // Прогресс виден и до получения — иначе список выглядит как набор запертых дверей без
  // замочных скважин.
  testWidgets('неполученное достижение показывает, сколько осталось', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (_progressJson(), 200))));
    await tester.pumpAndSettle();

    expect(find.text('Ветеран зала'), findsOneWidget);
    expect(find.text('12 из 50'), findsOneWidget);
    expect(find.byIcon(Icons.check_circle), findsOneWidget); // только первый визит получен
  });

  // Сервер новее приложения — достижение остаётся в списке под своим кодом: пропавшее
  // выглядело бы как потерянное.
  testWidgets('незнакомое достижение не пропадает из списка', (tester) async {
    await tester.pumpWidget(harness(FakeHttpClient((_) => (
          _progressJson(achievements: [
            {'code': 'tournament_winner', 'progress': 0, 'target': 1, 'unlockedAtUtc': null},
          ]),
          200
        ))));
    await tester.pumpAndSettle();

    expect(find.text('tournament_winner'), findsOneWidget);
  });

  testWidgets('сбой загрузки виден и лечится повтором', (tester) async {
    var attempt = 0;
    await tester.pumpWidget(harness(FakeHttpClient(
      (_) => ++attempt == 1 ? ('{"error":"boom"}', 500) : (_progressJson(), 200),
    )));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось загрузить стаж'), findsOneWidget);

    await tester.tap(find.text('Повторить'));
    await tester.pumpAndSettle();

    expect(find.text('Уровень 3'), findsOneWidget);
  });
}
