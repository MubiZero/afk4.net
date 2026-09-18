import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';

import 'package:afk4_customer_app/api/contracts.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/club_card.dart';
import 'package:afk4_customer_app/organization/club_details_sheet.dart';
import 'package:afk4_customer_app/organization/club_map.dart';
import 'package:afk4_customer_app/organization/club_picker_screen.dart';
import 'package:afk4_customer_app/organization/opening_hours.dart';
import 'package:afk4_customer_app/organization/nearby_location.dart';
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
      coverImageUrl: 'https://cdn.example/hall.jpg',
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

class _StubLocation implements NearbyLocation {
  _StubLocation(this.result);

  final NearbyResult result;
  int calls = 0;

  @override
  Future<NearbyResult> current() async {
    calls++;
    return result;
  }
}

Widget harness(
  OrganizationDirectory directory, {
  ValueChanged<Organization>? onSelected,
  List<MyClubDto> myClubs = const [],
  String? selectedOrganizationId,
  NearbyLocation? location,
}) =>
    MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: ClubPickerScreen(
        directory: directory,
        onSelected: onSelected ?? (_) {},
        myClubs: myClubs,
        selectedOrganizationId: selectedOrganizationId,
        location: location ??
            _StubLocation((outcome: NearbyOutcome.unavailable, point: null)),
      ),
    );

/// Счёт игрока в клубе, как его отдаёт `/api/me`.
MyClubDto _myClub({String? organizationId, String name = 'CyberX', int wallet = 12000, int held = 0}) =>
    MyClubDto.fromJson({
      'organizationId': organizationId ?? _cyberx.organizationId,
      'organizationName': name,
      'playerAccountId': 'p1',
      'homeBranchId': 'b1',
      'currencyCode': 'TJS',
      'walletBalanceMinorUnits': wallet,
      'heldMinorUnits': held,
      'debtMinorUnits': 0,
      'visitCount': 3,
    });

void main() {
  testWidgets('показывает клубы из каталога', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx, _arena])));
    await tester.pumpAndSettle();

    expect(find.text('CyberX'), findsOneWidget);
    // Вторая карточка лежит ниже строки фильтров — список ленивый, до неё надо долистать.
    await tester.drag(find.byType(ClubCard).first, const Offset(0, -400));
    await tester.pumpAndSettle();
    expect(find.text('Arena'), findsOneWidget);
  });

  // Сеть — не первый её зал. Адрес, часы и описание принадлежат конкретному залу, и выданные
  // за весь клуб они зовут игрока по адресу одного зала к часам другого.
  testWidgets('карточка сети говорит про залы, а не про адрес и часы первого из них', (tester) async {
    const network = Organization(
      organizationId: '55555555-5555-5555-5555-555555555555',
      slug: 'cyberx',
      name: 'CyberX',
      places: [
        ClubPlace(
          branchId: 'b1',
          name: 'На Рудаки',
          city: 'Душанбе',
          address: 'пр. Рудаки, 1',
          description: 'Зал с новыми видеокартами',
          workingHours: [
            OpeningDay(dayOfWeek: 1, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 2, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 3, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 4, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 5, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 6, isClosed: false, openTime: '10:00', closeTime: '23:00'),
            OpeningDay(dayOfWeek: 7, isClosed: false, openTime: '10:00', closeTime: '23:00'),
          ],
        ),
        ClubPlace(
          branchId: 'b2',
          name: 'В Худжанде',
          city: 'Худжанд',
          address: 'ул. Ленина, 5',
        ),
      ],
    );

    await tester.pumpWidget(MaterialApp(
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      home: Scaffold(
        body: ClubCard(club: network, onTap: () {}, clock: () => DateTime(2026, 8, 12, 14)),
      ),
    ));
    await tester.pumpAndSettle();

    expect(find.text('2 зала · Душанбе, Худжанд'), findsOneWidget);
    expect(find.text('Душанбе, пр. Рудаки, 1'), findsNothing);
    expect(find.text('Открыто до 23:00'), findsNothing);
    expect(find.text('Зал с новыми видеокартами'), findsNothing);
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

  // «40 мест» отвечает не на тот вопрос: сорок мест бывает и в забитом зале. Игрок едет
  // туда, где есть куда сесть.
  group('свободные места на карточке', () {
    const week = [
      OpeningDay(dayOfWeek: 1, isClosed: false, openTime: '10:00', closeTime: '23:00'),
      OpeningDay(dayOfWeek: 2, isClosed: false, openTime: '10:00', closeTime: '23:00'),
      OpeningDay(dayOfWeek: 3, isClosed: false, openTime: '10:00', closeTime: '23:00'),
      OpeningDay(dayOfWeek: 4, isClosed: false, openTime: '10:00', closeTime: '23:00'),
      OpeningDay(dayOfWeek: 5, isClosed: false, openTime: '10:00', closeTime: '23:00'),
      OpeningDay(dayOfWeek: 6, isClosed: false, openTime: '10:00', closeTime: '23:00'),
      OpeningDay(dayOfWeek: 7, isClosed: false, openTime: '10:00', closeTime: '23:00'),
    ];

    Organization clubWith({required int seats, required int free}) => Organization(
          organizationId: '55555555-5555-5555-5555-555555555555',
          slug: 'cyberx',
          name: 'CyberX',
          seatCount: seats,
          places: [
            ClubPlace(
              branchId: 'b1',
              name: 'На Рудаки',
              city: 'Душанбе',
              workingHours: week,
              seatCount: seats,
              freeSeatCount: free,
            ),
          ],
        );

    Widget card(Organization club, DateTime now) => MaterialApp(
          locale: const Locale('ru'),
          localizationsDelegates: appLocalizationsDelegates,
          supportedLocales: appSupportedLocales,
          home: Scaffold(body: ClubCard(club: club, onTap: () {}, clock: () => now)),
        );

    testWidgets('у открытого клуба видно, сколько мест свободно', (tester) async {
      await tester.pumpWidget(card(clubWith(seats: 40, free: 12), DateTime(2026, 8, 12, 14)));
      await tester.pumpAndSettle();

      expect(find.text('Свободно 12 из 40'), findsOneWidget);
      expect(find.text('40 мест'), findsNothing);
    });

    // Забитый клуб не должен выглядеть так же, как клуб с одним свободным местом: игрок
    // поедет и упрётся в очередь.
    testWidgets('забитый клуб говорит об этом словами', (tester) async {
      await tester.pumpWidget(card(clubWith(seats: 40, free: 0), DateTime(2026, 8, 12, 14)));
      await tester.pumpAndSettle();

      expect(find.text('Свободных мест нет'), findsOneWidget);
    });

    // Ночью свободны все места — потому что клуб закрыт. «Свободно 40 из 40» позвало бы
    // игрока к запертой двери.
    testWidgets('у закрытого клуба карточка говорит про места, как раньше', (tester) async {
      await tester.pumpWidget(card(clubWith(seats: 40, free: 40), DateTime(2026, 8, 12, 5)));
      await tester.pumpAndSettle();

      expect(find.text('40 мест'), findsOneWidget);
      expect(find.textContaining('Свободно'), findsNothing);
    });

    // Сервер про занятость промолчал (старая сборка на той стороне) — «мест нет» здесь было бы
    // выдуманным отказом, а не правдой.
    testWidgets('без ответа про занятость карточка показывает всего мест', (tester) async {
      const club = Organization(
        organizationId: '66666666-6666-6666-6666-666666666666',
        slug: 'cyberx',
        name: 'CyberX',
        seatCount: 40,
        places: [
          ClubPlace(
            branchId: 'b1',
            name: 'На Рудаки',
            city: 'Душанбе',
            workingHours: week,
            seatCount: 40,
          ),
        ],
      );

      await tester.pumpWidget(card(club, DateTime(2026, 8, 12, 14)));
      await tester.pumpAndSettle();

      expect(find.text('40 мест'), findsOneWidget);
      expect(find.textContaining('Свободн'), findsNothing);
    });

    // Клуб, который не сказал даже, сколько у него мест, остаётся как был.
    testWidgets('без чисел о местах карточка молчит', (tester) async {
      await tester.pumpWidget(card(clubWith(seats: 0, free: 0), DateTime(2026, 8, 12, 14)));
      await tester.pumpAndSettle();

      expect(find.textContaining('мест'), findsNothing);
      expect(find.textContaining('Свободно'), findsNothing);
    });
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

  // Нажатие по фото открывает клуб так же, как по любому другому месту карточки: лента
  // фотографий забирает себе только движение вбок.
  testWidgets('нажатие по фото зала тоже открывает клуб', (tester) async {
    Organization? picked;
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_cyberx]),
      onSelected: (club) => picked = club,
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.descendant(of: find.byType(ClubCard), matching: find.byType(PageView)));
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

  // «Подробнее» — для того, кто уже присматривается: сравнивает железо и смотрит расписание.
  testWidgets('«подробнее» открывает подробности клуба', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx])));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Подробнее'));
    await tester.pumpAndSettle();

    expect(find.byType(ClubDetailsSheet), findsOneWidget);
  });

  // Из подробностей клуб выбирается сразу: возвращаться в список ради одной кнопки незачем.
  testWidgets('выбор из подробностей отдаёт клуб наверх', (tester) async {
    Organization? picked;
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_cyberx]),
      onSelected: (club) => picked = club,
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Подробнее'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Играть здесь'));
    await tester.pumpAndSettle();

    expect(picked, _cyberx);
    expect(find.byType(ClubDetailsSheet), findsNothing);
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

  // Одно фото отвечает «как тут выглядит», остальные — «а что ещё»: их листают, и о том,
  // что они есть, говорят точки-счётчик.
  testWidgets('несколько фото зала листаются в карточке', (tester) async {
    const club = Organization(
      organizationId: '55555555-5555-5555-5555-555555555555',
      slug: 'cyberx',
      name: 'CyberX',
      places: [
        ClubPlace(
          branchId: 'b1',
          name: 'На Рудаки',
          city: 'Душанбе',
          photoUrls: [
            'https://cdn.example/hall.jpg',
            'https://cdn.example/vip.jpg',
            'https://cdn.example/bar.jpg',
          ],
        ),
      ],
    );

    await tester.pumpWidget(harness(_StubDirectory(clubs: const [club])));
    await tester.pumpAndSettle();

    final gallery = find.descendant(of: find.byType(ClubCard), matching: find.byType(PageView));
    expect(gallery, findsOneWidget);
    expect(tester.widget<PageView>(gallery).controller!.hasClients, isTrue);

    // Тестовый экран шире телефонного, и порог перелистывания — половина его ширины:
    // короткий сдвиг вернулся бы на первое фото, ничего не доказав.
    await tester.drag(gallery, const Offset(-600, 0));
    await tester.pumpAndSettle();

    expect(tester.widget<PageView>(gallery).controller!.page?.round(), 1);
  });

  // Одно фото листать нечего — и точек-счётчика быть не должно.
  testWidgets('одно фото не притворяется галереей', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx])));
    await tester.pumpAndSettle();

    expect(find.descendant(of: find.byType(ClubCard), matching: find.byType(PageView)), findsOneWidget);
    expect(tester.widget<PageView>(find.byType(PageView)).childrenDelegate.estimatedChildCount, 1);
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

  // Аккаунт один на всю сеть, а деньги у каждого клуба свои: список своих клубов с их
  // остатками — единственное место, где это видно целиком.
  testWidgets('свои клубы идут первыми и со своими деньгами', (tester) async {
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_cyberx, _arena]),
      myClubs: [_myClub(held: 4500)],
    ));
    await tester.pumpAndSettle();

    expect(find.text('Ваши клубы'), findsOneWidget);
    expect(find.text('Все клубы'), findsOneWidget);
    expect(find.textContaining('120,00 с.'), findsOneWidget);
    expect(find.textContaining('Придержано под брони'), findsOneWidget);
  });

  testWidgets('без своих клубов витрина выглядит как прежде', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx, _arena])));
    await tester.pumpAndSettle();

    expect(find.text('Ваши клубы'), findsNothing);
    expect(find.text('Все клубы'), findsNothing);
  });

  // Открытый сейчас клуб помечен, но остаётся нажимаемым: это дорога назад для того, кто
  // передумал переходить.
  testWidgets('текущий клуб помечен и возвращает в себя', (tester) async {
    Organization? chosen;
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_cyberx, _arena]),
      myClubs: [_myClub()],
      selectedOrganizationId: _cyberx.organizationId,
      onSelected: (club) => chosen = club,
    ));
    await tester.pumpAndSettle();

    expect(find.text('Вы здесь'), findsOneWidget);
    expect(find.text('Перейти'), findsNothing);

    await tester.tap(find.text('Вы здесь'));
    await tester.pumpAndSettle();

    expect(chosen?.organizationId, _cyberx.organizationId);
  });

  // Клуб, снявшийся с витрины, строкой не показывается: она вела бы в никуда.
  testWidgets('свой клуб без карточки в каталоге не показывается строкой', (tester) async {
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_arena]),
      myClubs: [_myClub()],
    ));
    await tester.pumpAndSettle();

    expect(find.text('Ваши клубы'), findsNothing);
  });

  /// Первый экран приложения спрашивал «в каком клубе вы играете» и ничем не помогал ответить:
  /// список шёл вперемешку по всей стране. Город — то, что человек про себя знает точно.
  testWidgets('города сужают витрину, и карта слушается того же выбора', (tester) async {
    const inKhujand = Organization(
      organizationId: '99999999-9999-9999-9999-999999999999',
      slug: 'arena',
      name: 'Арена',
      places: [
        ClubPlace(branchId: 'b9', name: 'Центр', city: 'Худжанд', address: 'ул. Ленина, 5'),
      ],
    );
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx, inKhujand])));
    await tester.pumpAndSettle();

    // До фильтра CyberX первый в списке; вторая карточка может лежать за краем экрана, и
    // проверять её там нечестно — список ленивый.
    expect(find.text('CyberX'), findsOneWidget);

    await tester.tap(find.widgetWithText(ChoiceChip, 'Худжанд'));
    await tester.pumpAndSettle();

    expect(find.text('CyberX'), findsNothing);
    expect(find.text('Арена'), findsOneWidget);
  });

  // Один город на всю витрину — выбирать не из чего, и строка чипов только занимала бы место.
  testWidgets('в одном городе фильтра городов нет', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_cyberx])));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(ChoiceChip, 'Душанбе'), findsNothing);
  });

  /// Клубы рядом — по нажатию, а не при открытии экрана: доступ к местоположению спрашивают
  /// тогда, когда человек сам о нём попросил.
  testWidgets('местоположение спрашивают только по нажатию и раскладывают витрину по близости',
      (tester) async {
    const nearby = Organization(
      organizationId: '77777777-7777-7777-7777-777777777777',
      slug: 'nearby',
      name: 'Соседний',
      places: [
        ClubPlace(
          branchId: 'bn',
          name: 'Рядом',
          city: 'Душанбе',
          address: 'ул. Сомони, 2',
          latitude: 38.5600,
          longitude: 68.7872,
        ),
      ],
    );
    const far = Organization(
      organizationId: '88888888-8888-8888-8888-888888888888',
      slug: 'far',
      name: 'Дальний',
      places: [
        ClubPlace(
          branchId: 'bf',
          name: 'Далеко',
          city: 'Душанбе',
          address: 'ул. Дальняя, 90',
          latitude: 38.6400,
          longitude: 68.9000,
        ),
      ],
    );
    final location = _StubLocation((
      outcome: NearbyOutcome.located,
      point: const LatLng(38.5598, 68.7870),
    ));

    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [far, nearby]),
      location: location,
    ));
    await tester.pumpAndSettle();

    // Экран открылся — местоположение не спрашивали.
    expect(location.calls, 0);
    expect(tester.widget<ClubCard>(find.byType(ClubCard).first).club.name, 'Дальний');

    await tester.tap(find.widgetWithText(ChoiceChip, 'Рядом со мной'));
    await tester.pumpAndSettle();

    expect(location.calls, 1);
    expect(tester.widget<ClubCard>(find.byType(ClubCard).first).club.name, 'Соседний');
    expect(find.textContaining('км отсюда'), findsWidgets);
  });

  /// Отказ в доступе — это ответ игрока, а не сбой: говорим об этом один раз и возвращаемся
  /// к выбору города, а не показываем пустую витрину.
  testWidgets('отказ в доступе к местоположению объясняется и не ломает витрину', (tester) async {
    final location = _StubLocation((outcome: NearbyOutcome.denied, point: null));
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_cyberx]),
      location: location,
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(ChoiceChip, 'Рядом со мной'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Без доступа к местоположению'), findsOneWidget);
    expect(find.text('CyberX'), findsOneWidget);
  });

  // Ни у кого нет координат — считать близость нечем, и предлагать это незачем.
  testWidgets('без координат у клубов «рядом со мной» не предлагают', (tester) async {
    const noPoint = Organization(
      organizationId: '66666666-6666-6666-6666-666666666666',
      slug: 'nomap',
      name: 'Без карты',
      places: [ClubPlace(branchId: 'b0', name: 'Зал', city: 'Душанбе', address: 'ул. Без карты')],
    );
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [noPoint])));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(ChoiceChip, 'Рядом со мной'), findsNothing);
  });
}
