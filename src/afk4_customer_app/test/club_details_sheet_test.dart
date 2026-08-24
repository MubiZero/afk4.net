import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/club_details_sheet.dart';
import 'package:afk4_customer_app/organization/opening_hours.dart';
import 'package:afk4_customer_app/organization/organization.dart';

const _rudaki = ClubPlace(
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
);

/// Второй зал сети: другой город, другой адрес, другие часы и другие зоны. Всё, чем он
/// отличается от первого, — это ровно то, что раньше подменялось данными первого.
const _sino = ClubPlace(
  branchId: 'b2',
  name: 'На Сино',
  city: 'Худжанд',
  address: 'ул. Сино, 4',
  description: 'Ночной зал у вокзала',
  zones: [ClubZone(name: 'Буткемп', seatCount: 12, hardwareSummary: 'RTX 4070 · 240 Гц')],
  workingHours: [
    OpeningDay(dayOfWeek: 1, isClosed: false, openTime: '12:00', closeTime: '06:00'),
    OpeningDay(dayOfWeek: 2, isClosed: false, openTime: '12:00', closeTime: '06:00'),
    OpeningDay(dayOfWeek: 3, isClosed: false, openTime: '12:00', closeTime: '06:00'),
    OpeningDay(dayOfWeek: 4, isClosed: false, openTime: '12:00', closeTime: '06:00'),
    OpeningDay(dayOfWeek: 5, isClosed: false, openTime: '12:00', closeTime: '06:00'),
    OpeningDay(dayOfWeek: 6, isClosed: false, openTime: '12:00', closeTime: '06:00'),
    OpeningDay(dayOfWeek: 7, isClosed: false, openTime: '12:00', closeTime: '06:00'),
  ],
);

/// Зал, про который клуб не рассказал ничего, кроме адреса.
const _bareHall = ClubPlace(branchId: 'b3', name: 'На Айни', city: 'Душанбе', address: 'ул. Айни, 7');

const _club = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
  pricePerHourFromMinorUnits: 1500,
  currencyCode: 'TJS',
  places: [_rudaki],
);

const _network = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
  pricePerHourFromMinorUnits: 1500,
  currencyCode: 'TJS',
  places: [_rudaki, _sino],
);

/// Понедельник, 11:00: Рудаки уже открылся, Сино откроется только в полдень. Так видно, что
/// часы читаются у каждого зала свои, а не у первого за всех.
final _mondayMorning = DateTime(2026, 8, 24, 11);

Widget harness(Organization club, {VoidCallback? onChoose, DateTime? now}) => MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: ClubDetailsSheet(
          club: club,
          onChoose: onChoose ?? () {},
          clock: () => now ?? _mondayMorning,
        ),
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
  testWidgets('показывает зоны с железом и числом мест', (tester) async {
    await tester.pumpWidget(harness(_club));
    await tester.pumpAndSettle();

    expect(find.text('Зоны'), findsOneWidget);
    expect(find.text('Основной зал'), findsOneWidget);
    expect(find.text('30 мест'), findsOneWidget);
    expect(find.text('RTX 4060 · 27" 165 Гц'), findsOneWidget);
    expect(find.text('VIP'), findsOneWidget);
  });

  // Железо указано не у всех зон — и придумывать его за клуб нечем.
  testWidgets('зона без указанного железа показывается без выдуманной строки', (tester) async {
    await tester.pumpWidget(harness(_club));
    await tester.pumpAndSettle();

    expect(find.text('10 мест'), findsOneWidget);
    expect(find.textContaining('RTX'), findsOneWidget); // только у первой зоны
  });

  // Игрок открывает подробности, чтобы выбрать зону: «30 мест» в забитом зале и «30 мест»
  // в пустом — одна и та же строка про разные вечера.
  testWidgets('у открытого зала зоны показывают свободные места', (tester) async {
    const hall = ClubPlace(
      branchId: 'b1',
      name: 'На Рудаки',
      city: 'Душанбе',
      zones: [
        ClubZone(name: 'Основной зал', seatCount: 30, freeSeatCount: 7),
        ClubZone(name: 'VIP', seatCount: 10, freeSeatCount: 0),
      ],
      workingHours: [
        OpeningDay(dayOfWeek: 1, isClosed: false, openTime: '10:00', closeTime: '23:00'),
      ],
    );
    const club = Organization(
      organizationId: '33333333-3333-3333-3333-333333333333',
      slug: 'cyberx',
      name: 'CyberX',
      places: [hall],
    );

    await tester.pumpWidget(harness(club));
    await tester.pumpAndSettle();

    expect(find.text('Свободно 7 из 30'), findsOneWidget);
    expect(find.text('Свободных мест нет'), findsOneWidget);
  });

  // Ночью свободны все места — потому что зал закрыт.
  testWidgets('у закрытого зала зоны показывают места, как раньше', (tester) async {
    const hall = ClubPlace(
      branchId: 'b1',
      name: 'На Рудаки',
      city: 'Душанбе',
      zones: [ClubZone(name: 'Основной зал', seatCount: 30, freeSeatCount: 30)],
      workingHours: [
        OpeningDay(dayOfWeek: 1, isClosed: false, openTime: '12:00', closeTime: '23:00'),
      ],
    );
    const club = Organization(
      organizationId: '33333333-3333-3333-3333-333333333333',
      slug: 'cyberx',
      name: 'CyberX',
      places: [hall],
    );

    await tester.pumpWidget(harness(club, now: DateTime(2026, 8, 24, 9)));
    await tester.pumpAndSettle();

    expect(find.text('30 мест'), findsOneWidget);
    expect(find.textContaining('Свободно'), findsNothing);
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

  // Единственный зал сети и есть клуб: списка с одной строкой над ним быть не должно.
  testWidgets('сеть из одного зала обходится без списка залов', (tester) async {
    await tester.pumpWidget(harness(_club));
    await tester.pumpAndSettle();

    expect(find.text('Залы сети'), findsNothing);
    expect(find.text('На Рудаки'), findsNothing);
  });

  // Раньше лист показывал адрес первого зала так, будто он у сети один: человек читал
  // «Душанбе», а ехал в зал, который на самом деле в Худжанде.
  testWidgets('сеть из нескольких залов называет их число и города', (tester) async {
    await tester.pumpWidget(harness(_network));
    await tester.pumpAndSettle();

    expect(find.text('2 зала · Душанбе, Худжанд'), findsOneWidget);
    // Описание первого зала больше не выдаётся за описание всей сети.
    expect(find.text('Сорок машин и две PlayStation'), findsNothing);
  });

  testWidgets('каждый зал сети назван по имени и со своим адресом', (tester) async {
    await tester.pumpWidget(harness(_network));
    await tester.pumpAndSettle();

    expect(find.text('Залы сети'), findsOneWidget);
    expect(find.text('На Рудаки'), findsOneWidget);
    expect(find.text('Душанбе, пр. Рудаки, 25'), findsOneWidget);
    expect(find.text('На Сино'), findsOneWidget);
    expect(find.text('Худжанд, ул. Сино, 4'), findsOneWidget);
  });

  // Один и тот же час, два разных ответа: часы принадлежат залу, а не сети.
  testWidgets('открыт ли зал сейчас — считается по его собственным часам', (tester) async {
    await tester.pumpWidget(harness(_network));
    await tester.pumpAndSettle();

    expect(find.text('Открыто до 23:00'), findsOneWidget);
    expect(find.text('Закрыто, откроется в 12:00'), findsOneWidget);
  });

  testWidgets('зоны и расписание раскрываются у того зала, который открыли', (tester) async {
    await tester.pumpWidget(harness(_network));
    await tester.pumpAndSettle();

    expect(find.text('Основной зал'), findsNothing);
    expect(find.text('Буткемп'), findsNothing);

    await tester.tap(find.text('На Сино'));
    await tester.pumpAndSettle();

    expect(find.text('Буткемп'), findsOneWidget);
    expect(find.text('12 мест'), findsOneWidget);
    expect(find.text('Ночной зал у вокзала'), findsOneWidget);
    // Зоны и часы соседнего зала остались у соседнего зала.
    expect(find.text('Основной зал'), findsNothing);
    expect(find.text('10:00 – 23:00'), findsNothing);
  });

  // Витрину владелец заполняет не всю: пустое место читается как поломка, а не как «клуб
  // ещё не рассказал».
  testWidgets('зал без зон и часов показывает состояние, а не пустоту', (tester) async {
    const network = Organization(
      organizationId: '33333333-3333-3333-3333-333333333333',
      slug: 'cyberx',
      name: 'CyberX',
      places: [_rudaki, _bareHall],
    );

    await tester.pumpWidget(harness(network));
    await tester.pumpAndSettle();

    await tester.tap(find.text('На Айни'));
    await tester.pumpAndSettle();

    expect(find.text('Зоны клуб не описал'), findsOneWidget);
    expect(find.text('Часы работы клуб не указал'), findsOneWidget);
  });

  testWidgets('единственный зал без зон и часов тоже объясняет пустоту', (tester) async {
    const club = Organization(
      organizationId: '44444444-4444-4444-4444-444444444444',
      slug: 'bare',
      name: 'Bare Club',
      places: [_bareHall],
    );

    await tester.pumpWidget(harness(club));
    await tester.pumpAndSettle();

    expect(find.text('Душанбе, ул. Айни, 7'), findsOneWidget);
    expect(find.text('Зоны клуб не описал'), findsOneWidget);
    expect(find.text('Часы работы клуб не указал'), findsOneWidget);
  });

  // Пустой клуб не должен ронять экран: заполнять витрину владелец не обязан.
  testWidgets('клуб без залов открывается и говорит об этом прямо', (tester) async {
    const bare = Organization(
      organizationId: '22222222-2222-2222-2222-222222222222',
      slug: 'bare',
      name: 'Bare Club',
    );

    await tester.pumpWidget(harness(bare));
    await tester.pumpAndSettle();

    expect(find.text('Bare Club'), findsOneWidget);
    expect(find.text('Играть здесь'), findsOneWidget);
    expect(find.text('Клуб не указал, где он находится'), findsOneWidget);
    expect(find.text('Залы сети'), findsNothing);
    expect(find.text('Зоны'), findsNothing);
  });
}
