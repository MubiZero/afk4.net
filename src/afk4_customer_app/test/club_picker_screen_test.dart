import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/organization/club_picker_screen.dart';
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
);
const _arena = Organization(
  organizationId: '22222222-2222-2222-2222-222222222222',
  slug: 'arena',
  name: 'Arena',
  logoUrl: null,
);

Widget harness(OrganizationDirectory directory, {ValueChanged<Organization>? onSelected}) => MaterialApp(
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

  testWidgets('нажатие на клуб отдаёт его наверх', (tester) async {
    Organization? picked;
    await tester.pumpWidget(harness(
      _StubDirectory(clubs: const [_cyberx]),
      onSelected: (club) => picked = club,
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.text('CyberX'));
    await tester.pump();

    expect(picked, _cyberx);
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

  testWidgets('клуб без логотипа получает букву вместо пустого кружка', (tester) async {
    await tester.pumpWidget(harness(_StubDirectory(clubs: const [_arena])));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(CircleAvatar, 'A'), findsOneWidget);
  });
}
