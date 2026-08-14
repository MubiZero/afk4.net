import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/club_details_sheet.dart';
import 'package:afk4_customer_app/organization/opening_hours.dart';
import 'package:afk4_customer_app/organization/organization.dart';

const _club = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
  pricePerHourFromMinorUnits: 1500,
  currencyCode: 'TJS',
  places: [
    ClubPlace(
      branchId: 'b1',
      name: 'На Рудаки',
      city: 'Душанбе',
      address: 'пр. Рудаки, 25',
      description: 'Сорок машин и две PlayStation',
      zones: [
        ClubZone(name: 'Основной зал', seatCount: 30, hardwareSummary: 'RTX 4060 · 27" 165 Гц'),
        ClubZone(name: 'VIP', seatCount: 10),
      ],
      workingHours: [
        OpeningDay(dayOfWeek: 1, isClosed: false, openTime: '10:00', closeTime: '23:00'),
        OpeningDay(dayOfWeek: 2, isClosed: false, openTime: '10:00', closeTime: '23:00'),
        OpeningDay(dayOfWeek: 3, isClosed: false, openTime: '10:00', closeTime: '23:00'),
        OpeningDay(dayOfWeek: 4, isClosed: false, openTime: '10:00', closeTime: '23:00'),
        OpeningDay(dayOfWeek: 5, isClosed: false, openTime: '10:00', closeTime: '02:00'),
        OpeningDay(dayOfWeek: 6, isClosed: false, openTime: '10:00', closeTime: '02:00'),
        OpeningDay(dayOfWeek: 7, isClosed: true),
      ],
    ),
  ],
);

Widget harness(Organization club, {VoidCallback? onChoose}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: ClubDetailsSheet(club: club, onChoose: onChoose ?? () {}),
      ),
    );

void main() {
  testWidgets('показывает описание, адрес и цену', (tester) async {
    await tester.pumpWidget(harness(_club));
    await tester.pumpAndSettle();

    expect(find.text('CyberX'), findsOneWidget);
    expect(find.text('Душанбе, пр. Рудаки, 25'), findsOneWidget);
    expect(find.text('Сорок машин и две PlayStation'), findsOneWidget);
    expect(find.text('от 15,00 с. в час'), findsOneWidget);
  });

  // По железу клубы и сравнивают: «сорок мест» ничего не говорит о том, пойдёт ли на них игра.
  testWidgets('показывает залы с железом и числом мест', (tester) async {
    await tester.pumpWidget(harness(_club));
    await tester.pumpAndSettle();

    expect(find.text('Основной зал'), findsOneWidget);
    expect(find.text('30 мест'), findsOneWidget);
    expect(find.text('RTX 4060 · 27" 165 Гц'), findsOneWidget);
    expect(find.text('VIP'), findsOneWidget);
  });

  // Железо указано не у всех залов — и придумывать его за клуб нечем.
  testWidgets('зал без указанного железа показывается без выдуманной строки', (tester) async {
    await tester.pumpWidget(harness(_club));
    await tester.pumpAndSettle();

    expect(find.text('10 мест'), findsOneWidget);
    expect(find.textContaining('RTX'), findsOneWidget); // только у первого зала
  });

  testWidgets('показывает расписание на неделю с выходным', (tester) async {
    await tester.pumpWidget(harness(_club));
    await tester.pumpAndSettle();

    expect(find.text('Понедельник'), findsOneWidget);
    expect(find.text('Воскресенье'), findsOneWidget);
    expect(find.text('выходной'), findsOneWidget);
    expect(find.text('10:00 – 02:00'), findsNWidgets(2));
  });

  // Клуб выбирают отсюда же: возвращаться в список ради одной кнопки незачем.
  testWidgets('клуб выбирается прямо из подробностей', (tester) async {
    var chosen = false;
    await tester.pumpWidget(harness(_club, onChoose: () => chosen = true));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Играть здесь'));
    await tester.pump();

    expect(chosen, isTrue);
  });

  // Пустой клуб не должен ронять экран: заполнять витрину владелец не обязан.
  testWidgets('клуб без описания, залов и расписания открывается без ошибок', (tester) async {
    const bare = Organization(
      organizationId: '22222222-2222-2222-2222-222222222222',
      slug: 'bare',
      name: 'Bare Club',
    );

    await tester.pumpWidget(harness(bare));
    await tester.pumpAndSettle();

    expect(find.text('Bare Club'), findsOneWidget);
    expect(find.text('Играть здесь'), findsOneWidget);
    expect(find.text('Залы'), findsNothing);
  });
}
