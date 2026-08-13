import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/club_card.dart';
import 'package:afk4_customer_app/organization/club_map.dart';
import 'package:afk4_customer_app/organization/club_picker_screen.dart';
import 'package:afk4_customer_app/organization/opening_hours.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';

class _StubDirectory extends OrganizationDirectory {
  _StubDirectory({this.clubs = const [], this.fails = false}) : super(baseUrl: 'https://stub');

  final List<Organization> clubs;
  final bool fails;
  final List<String?> queries = [];

  @override
  Future<List<Organization>> search({String? query}) async {
    queries.add(query);
    if (fails) throw const OrganizationDirectoryException(500);
    return clubs;
  }
}

const _cyberx = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
  places: [
    ClubPlace(
      branchId: 'b1',
      name: 'На Рудаки',
      city: 'Душанбе',
      address: 'пр. Рудаки, 1',
      latitude: 38.5598,
      longitude: 68.7870,
    ),
  ],
  pricePerHourFromMinorUnits: 1500,
  currencyCode: 'TJS',
  seatCount: 40,
  rating: 4.6,
  reviewCount: 12,
);

const _arena = Organization(
  organizationId: '22222222-2222-2222-2222-222222222222',
  slug: 'arena',
  name: 'Arena',
  logoUrl: null,
);

Widget harness(OrganizationDirectory directory, {ValueChanged<Organization>? onSelected}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ClubPickerScreen(directory: directory, onSelected: onSelected ?? (_) {}),
    );

void main() {
  testWidgets('показывает клубы из каталога', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx, _arena])));
    await tester.pumpAndSettle();

    expect(find.text('CyberX'), findsOneWidget);
    expect(find.text('Arena'), findsOneWidget);
  });

  // «Открыт ли он сейчас» игрок спрашивает до выхода из дома, и семь строк расписания на
  // карточке этот вопрос не закрывают.
  testWidgets('карточка отвечает, открыт ли клуб сейчас', (tester) async {
    const club = Organization(
      organizationId: '44444444-4444-4444-4444-444444444444',
      slug: 'cyberx',
      name: 'CyberX',
      places: [
        ClubPlace(
          branchId: 'b1',
          name: 'На Рудаки',
          city: 'Душанбе',
          workingHours: [
            OpeningDay(dayOfWeek: 1, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 2, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 3, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 4, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 5, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 6, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 7, isClosed: true),
          ],
        ),
      ],
    );

    Widget card(DateTime now) => MaterialApp(
          locale: const Locale('ru'),
          localizationsDelegates: appLocalizationsDelegates,
          supportedLocales: appSupportedLocales,
          home: Scaffold(body: ClubCard(club: club, onTap: () {}, clock: () => now)),
        );

    // 2026-08-12 — среда.
    await tester.pumpWidget(card(DateTime(2026, 8, 12, 14)));
    await tester.pumpAndSettle();
    expect(find.text('Открыто до 23:00'), findsOneWidget);

    await tester.pumpWidget(card(DateTime(2026, 8, 12, 8)));
    await tester.pumpAndSettle();
    expect(find.text('Закрыто, откроется в 10:00'), findsOneWidget);
  });

  // Расписания может не быть — придумывать за клуб часы работы значит отправить игрока
  // к закрытой двери.
  testWidgets('без расписания карточка о часах молчит', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx])));
    await tester.pumpAndSettle();

    expect(find.textContaining('Открыто'), findsNothing);
    expect(find.textContaining('Закрыто'), findsNothing);
  });

  // Клуб выбирают по тому, где он и сколько стоит час: одно название не отвечает ни на один
  // из этих вопросов.
  testWidgets('карточка отвечает, где клуб, сколько стоит час и сколько мест', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx])));
    await tester.pumpAndSettle();

    expect(find.text('Душанбе, пр. Рудаки, 1'), findsOneWidget);
    expect(find.text('от 15,00 с. в час'), findsOneWidget);
    expect(find.text('40 мест'), findsOneWidget);
    expect(find.text('4,6'), findsOneWidget);
  });

  // Клуб, в который ещё никто не сходил, не хуже клуба, который все ругают: ноль звёзд был бы
  // приговором за отсутствие отзывов.
  testWidgets('клуб без отзывов говорит «оценок пока нет», а не показывает ноль', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_arena])));
    await tester.pumpAndSettle();

    expect(find.text('Оценок пока нет'), findsOneWidget);
    expect(find.text('0,0'), findsNothing);
  });

  testWidgets('клуб без тарифов зовёт уточнить цену, а не показывает ноль', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_arena])));
    await tester.pumpAndSettle();

    expect(find.text('Цену уточняйте в клубе'), findsOneWidget);
  });

  testWidgets('нажатие на клуб отдаёт его наверх', (tester) async {
    Organization? picked;
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_cyberx]),
      onSelected: (club) => picked = club,
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(ClubCard));
    await tester.pump();

    expect(picked, _cyberx);
  });

  // До клуба надо доехать, поэтому «какой рядом» — вопрос не менее частый, чем «какие есть».
  testWidgets('карта открывается переключателем и не теряет список', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx])));
    await tester.pumpAndSettle();

    await tester.tap(find.text('На карте'));
    await tester.pump();

    expect(find.byType(ClubMap), findsOneWidget);
    expect(find.byType(ClubCard), findsNothing);

    await tester.tap(find.text('Списком'));
    await tester.pump();

    expect(find.byType(ClubCard), findsOneWidget);
  });

  // Точки на карте должны быть видны при любом разбросе клубов: с двумя городами любой
  // фиксированный масштаб оставлял бы один из них за кадром, и полный каталог выглядел бы
  // как пустая карта.
  testWidgets('на карте видны точки клубов из разных городов', (tester) async {
    const khujand = Organization(
      organizationId: '33333333-3333-3333-3333-333333333333',
      slug: 'nightowl',
      name: 'Night Owl',
      places: [
        ClubPlace(
          branchId: 'b2',
          name: 'Night Owl',
          city: 'Худжанд',
          latitude: 40.2833,
          longitude: 69.6167,
        ),
      ],
    );

    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx, khujand])));
    await tester.pumpAndSettle();

    await tester.tap(find.text('На карте'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.byIcon(Icons.sports_esports), findsNWidgets(2));
  });

  // Клуб без координат остаётся в каталоге: на карте его просто нет, и об этом сказано прямо.
  testWidgets('карта без точек объясняет пустоту, а не показывает пустой глобус', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_arena])));
    await tester.pumpAndSettle();

    await tester.tap(find.text('На карте'));
    await tester.pump();

    expect(find.text('Клубы ещё не отметились на карте'), findsOneWidget);
  });

  // Пустой список и сбой сети выглядят одинаково, если не различать их явно: игрок решит,
  // что клубов нет, и закроет приложение вместо повтора.
  testWidgets('сбой показывает ошибку и кнопку повтора, а не «клуб не найден»', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(fails: true)));
    await tester.pumpAndSettle();

    expect(find.text('Не удалось загрузить список клубов'), findsOneWidget);
    expect(find.text('Повторить'), findsOneWidget);
    expect(find.text('Клуб не найден'), findsNothing);
  });

  testWidgets('пустой каталог говорит «клуб не найден» без кнопки повтора', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [])));
    await tester.pumpAndSettle();

    expect(find.text('Клуб не найден'), findsOneWidget);
    expect(find.text('Повторить'), findsNothing);
  });

  testWidgets('набор текста шлёт один запрос, а не по одному на букву', (tester) async {
    final directory = _StubDirectory(clubs: const [_arena]);
    await tester.pumpWidget(harness(directory));
    await tester.pumpAndSettle();
    expect(directory.queries, hasLength(1)); // стартовая загрузка

    await tester.enterText(find.byType(TextField), 'а');
    await tester.enterText(find.byType(TextField), 'ар');
    await tester.enterText(find.byType(TextField), 'аре');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();

    expect(directory.queries, hasLength(2));
    expect(directory.queries.last, 'аре');
  });

  // Фотографии зала может не быть — тогда на её месте знак клуба, а не серый прямоугольник,
  // который читается как несработавшая загрузка.
  testWidgets('клуб без фото и логотипа получает букву вместо пустой обложки', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_arena])));
    await tester.pumpAndSettle();

    expect(
      find.descendant(of: find.byType(ClubCard), matching: find.text('A')),
      findsOneWidget,
    );
  });
}
