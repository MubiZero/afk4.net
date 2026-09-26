import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';
import 'package:afk4_customer_app/reviews/club_reviews_sheet.dart';

import 'support/fake_http.dart';

const _club = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
  rating: 4.5,
  reviewCount: 2,
);

String _reviewsJson() => jsonEncode({
      'rating': 4.5,
      'reviewCount': 2,
      'items': [
        {
          'reviewId': 'r1',
          'authorName': 'Иван',
          'rating': 5,
          'comment': 'Отличный зал',
          'createdAtUtc': '2026-08-11T18:00:00Z',
        },
        {
          'reviewId': 'r2',
          'authorName': 'Пётр',
          'rating': 4,
          'comment': null,
          'createdAtUtc': '2026-08-10T18:00:00Z',
        },
      ],
    });

Widget harness(OrganizationDirectory directory) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(body: ClubReviewsSheet(directory: directory, club: _club)),
    );

OrganizationDirectory _directory(FakeHttpClient http) =>
    OrganizationDirectory(baseUrl: 'https://api', httpClient: http);

void main() {
  testWidgets('показывает, что игроки написали о клубе', (tester) async {
    final http = FakeHttpClient((_) => (_reviewsJson(), 200));
    await tester.pumpWidget(harness(_directory(http)));
    await tester.pumpAndSettle();

    expect(find.text('Иван'), findsOneWidget);
    expect(find.text('Отличный зал'), findsOneWidget);
    expect(find.text('Пётр'), findsOneWidget);
  });

  // Ответ клуба виден под отзывом; скрытый клубом текст не выдаётся за «без текста».
  testWidgets('показывает ответ клуба и говорит, что текст скрыт', (tester) async {
    final http = FakeHttpClient((_) => (
          jsonEncode({
            'rating': 3.0,
            'reviewCount': 2,
            'items': [
              {
                'reviewId': 'r1',
                'authorName': 'Иван',
                'rating': 2,
                'comment': 'Мышь липкая',
                'createdAtUtc': '2026-08-11T18:00:00Z',
                'clubReply': 'Поменяли мышь, приходите.',
                'clubRepliedAtUtc': '2026-08-12T10:00:00Z',
              },
              {
                'reviewId': 'r2',
                'authorName': 'Пётр',
                'rating': 1,
                'comment': null,
                'createdAtUtc': '2026-08-10T18:00:00Z',
                'commentHidden': true,
              },
            ],
          }),
          200
        ));
    await tester.pumpWidget(harness(_directory(http)));
    await tester.pumpAndSettle();

    expect(find.text('Ответ клуба'), findsOneWidget);
    expect(find.text('Поменяли мышь, приходите.'), findsOneWidget);
    expect(find.text('Клуб скрыл текст отзыва — оценка осталась.'), findsOneWidget);
  });

  // Пустой список отзывов — приглашение написать первый, а не сообщение о поломке.
  testWidgets('клуб без отзывов зовёт написать первый', (tester) async {
    final http = FakeHttpClient((_) => ('{"rating":null,"reviewCount":0,"items":[]}', 200));
    await tester.pumpWidget(harness(_directory(http)));
    await tester.pumpAndSettle();

    expect(find.text('Отзывов пока нет — ваш будет первым'), findsOneWidget);
  });

  // «Не смогли спросить» и «отзывов нет» — разные вещи: из первого есть выход, повтор.
  testWidgets('сбой загрузки не выдаётся за отсутствие отзывов', (tester) async {
    var attempt = 0;
    final http = FakeHttpClient((_) => ++attempt == 1 ? ('boom', 500) : (_reviewsJson(), 200));
    await tester.pumpWidget(harness(_directory(http)));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось загрузить отзывы'), findsOneWidget);
    expect(find.text('Отзывов пока нет — ваш будет первым'), findsNothing);

    await tester.tap(find.text('Повторить'));
    await tester.pumpAndSettle();

    expect(find.text('Иван'), findsOneWidget);
  });
}
