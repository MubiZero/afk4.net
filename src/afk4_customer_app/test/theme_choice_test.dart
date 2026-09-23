import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/app.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';
import 'package:afk4_customer_app/profile/profile_screen.dart';
import 'package:afk4_customer_app/theme/theme_preference_store.dart';

import 'support/fake_http.dart';
import 'support/real_fonts.dart';

class _StubDirectory extends OrganizationDirectory {
  _StubDirectory() : super(baseUrl: 'https://stub');

  @override
  Future<List<Organization>> search({String? query}) async => const [
        Organization(
          organizationId: '11111111-1111-1111-1111-111111111111',
          slug: 'cyberx',
          name: 'CyberX',
        ),
      ];
}

const _session = '''
{"playerAccountId":"p1","organizationId":"11111111-1111-1111-1111-111111111111",
 "displayName":"Иван","phoneVerified":true,
 "accessToken":"access-1","accessTokenExpiresAtUtc":"2026-08-11T12:00:00Z",
 "refreshToken":"refresh-1","refreshTokenExpiresAtUtc":"2026-09-11T12:00:00Z"}''';

const _me = '''
{"person":{"platformPersonId":"pp1","phoneNumber":"+992900000000","displayName":"Иван",
 "preferredLocale":null,"phoneVerified":true,"pinSet":false,"networkBanned":false},
 "clubs":[{"organizationId":"11111111-1111-1111-1111-111111111111","organizationName":"CyberX",
 "playerAccountId":"p1","homeBranchId":"b1","currencyCode":"TJS",
 "walletBalanceMinorUnits":120050,"heldMinorUnits":0,"debtMinorUnits":0,"visitCount":3}]}''';

const _profile = '''
{"playerAccountId":"p1","displayName":"Иван","phoneNumber":"+992900000000",
 "phoneVerified":true,"preferredLocale":null,"marketingOptIn":false}''';

const _dashboard = '''
{"walletBalance":{"currencyCode":"TJS","minorUnits":120050},
 "heldBalance":{"currencyCode":"TJS","minorUnits":0},
 "debtBalance":{"currencyCode":"TJS","minorUnits":0},
 "activeSession":null}''';

Widget _app() => CustomerApp(
      locale: const Locale('ru'),
      directory: _StubDirectory(),
      api: PlayerApiClient(
        baseUrl: 'https://api',
        httpClient: FakeHttpClient((request) => switch (request.url.path) {
              '/api/me' => (_me, 200),
              '/api/me/dashboard' => (_dashboard, 200),
              '/api/me/profile' => (_profile, 200),
              '/api/me/features' => ('{"features":[]}', 200),
              '/api/public/register/start' =>
                ('{"expiresInSeconds":300,"resendAfterSeconds":60}', 200),
              final path when path.startsWith('/api/public/') => (_session, 200),
              _ => ('[]', 200),
            }),
      ),
    );

Brightness _shown(WidgetTester tester) =>
    Theme.of(tester.element(find.byType(Scaffold).first)).brightness;

Future<void> _signIn(WidgetTester tester) async {
  await tester.tap(find.text('CyberX'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byType(TextField).first, '+992900000000');
  await tester.tap(find.byType(FilledButton));
  await tester.pumpAndSettle();
  await tester.enterText(find.byType(TextField).last, '4321');
  await tester.tap(find.byType(FilledButton));
  await tester.pumpAndSettle();
}

/// Выбор оформления стоит в профиле, ниже языка: до него надо долистать. Вариант ставится в
/// середину списка — у верхнего края его закрывает прилипшая шапка.
Future<void> _choose(WidgetTester tester, String option) async {
  await tester.tap(find.text('Профиль'));
  await tester.pumpAndSettle();
  final list = find.descendant(of: find.byType(ProfileScreen), matching: find.byType(Scrollable));
  await tester.scrollUntilVisible(find.text(option), 200, scrollable: list);
  await Scrollable.ensureVisible(tester.element(find.text(option)), alignment: 0.5);
  await tester.pumpAndSettle();
  await tester.tap(find.text(option));
  await tester.pumpAndSettle();
}

Future<void> _unmount(WidgetTester tester) => tester.pumpWidget(const SizedBox.shrink());

/// Контраст текста по WCAG AA, кроме словесного знака: надпись, которая сама логотип, от
/// требования к контрасту освобождена (WCAG 1.4.3), и цвета у неё — бренда, а не темы.
class _ContrastExceptLogotype extends MinimumTextContrastGuideline {
  const _ContrastExceptLogotype();

  @override
  bool shouldSkipNode(SemanticsData data) => data.label == brandName || super.shouldSkipNode(data);
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    FlutterSecureStorage.setMockInitialValues({});
  });

  // Приложение живёт в компьютерном клубе ночью. Светлая системная тема телефона — не повод
  // слепить человека в тёмном зале, пока он сам не попросил.
  testWidgets('без выбора игрока приложение тёмное, даже если телефон светлый', (tester) async {
    tester.platformDispatcher.platformBrightnessTestValue = Brightness.light;
    addTearDown(tester.platformDispatcher.clearPlatformBrightnessTestValue);

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();

    expect(_shown(tester), Brightness.dark);
  });

  testWidgets('светлая тема из профиля применяется сразу и переживает перезапуск', (tester) async {
    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();
    await _signIn(tester);
    expect(_shown(tester), Brightness.dark);

    await _choose(tester, 'Светлая');
    expect(_shown(tester), Brightness.light);
    expect(await const ThemePreferenceStore().read(), ThemeMode.light);

    await _unmount(tester);
    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();
    expect(find.text('Иван'), findsOneWidget);
    expect(_shown(tester), Brightness.light);
    await _unmount(tester);
  });

  testWidgets('«Как в системе» следует телефону и меняется вместе с ним', (tester) async {
    tester.platformDispatcher.platformBrightnessTestValue = Brightness.light;
    addTearDown(tester.platformDispatcher.clearPlatformBrightnessTestValue);

    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();
    await _signIn(tester);
    await _choose(tester, 'Как в системе');
    expect(_shown(tester), Brightness.light);

    tester.platformDispatcher.platformBrightnessTestValue = Brightness.dark;
    await tester.pumpAndSettle();
    expect(_shown(tester), Brightness.dark);
    expect(await const ThemePreferenceStore().read(), ThemeMode.system);
    await _unmount(tester);
  });

  // Выбор показан как выбор: игрок видит, что сейчас включено, а не угадывает по экрану.
  testWidgets('в профиле отмечено текущее оформление', (tester) async {
    SharedPreferences.setMockInitialValues({'afk4.player.theme': 'light'});
    await tester.pumpWidget(_app());
    await tester.pumpAndSettle();
    await _signIn(tester);
    await tester.tap(find.text('Профиль'));
    await tester.pumpAndSettle();
    final list = find.descendant(of: find.byType(ProfileScreen), matching: find.byType(Scrollable));
    await tester.scrollUntilVisible(find.text('Светлая'), 200, scrollable: list);

    final choice = tester.widget<SegmentedButton<ThemeMode>>(find.byType(SegmentedButton<ThemeMode>));
    expect(choice.selected, {ThemeMode.light});
    await _unmount(tester);
  });

  test('испорченное значение забывается, а не роняет запуск', () async {
    SharedPreferences.setMockInitialValues({'afk4.player.theme': 'sepia'});
    expect(await const ThemePreferenceStore().read(), isNull);
    final prefs = await SharedPreferences.getInstance();
    expect(prefs.getString('afk4.player.theme'), isNull);
  });

  // Светлая тема была написана и ни разу не показана: её акцент на белом листе давал 3,4:1,
  // а слово в знаке бренда было светлым на светлом. Проверка идёт по настоящим экранам и
  // настоящим пикселям, в обеих темах. Шрифт настоящий: тестовый не рисует кириллицу, и
  // русская подпись для проверки была бы пустым местом. Загруженный шрифт остаётся до конца
  // файла, поэтому эта проверка стоит в нём последней.
  //
  // Витрина и вход проходятся, но не меряются. Их подзаголовки растянуты на всю ширину и
  // стоят на свете зала, а guideline берёт самый частый светлый и самый частый тёмный пиксель
  // в прямоугольнике текста. На градиенте под короткой строкой оба оказываются фоном, и он
  // врёт в обе стороны, в тёмной теме тоже. Цвета этих подписей проверяет theme_test по
  // значениям.
  for (final mode in ['light', 'dark']) {
    testWidgets('в оформлении $mode текст на главных экранах читается', (tester) async {
      await tester.runAsync(loadRealFonts);
      final semantics = tester.ensureSemantics();
      SharedPreferences.setMockInitialValues({'afk4.player.theme': mode});
      const guideline = _ContrastExceptLogotype();

      await tester.pumpWidget(_app());
      await tester.pumpAndSettle();
      
      await tester.tap(find.text('CyberX'));
      await tester.pumpAndSettle();
      
      await tester.enterText(find.byType(TextField).first, '+992900000000');
      await tester.tap(find.byType(FilledButton));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField).last, '4321');
      await tester.tap(find.byType(FilledButton));
      await tester.pumpAndSettle();
      await expectLater(tester, meetsGuideline(guideline), reason: 'главная');

      await tester.tap(find.text('Кошелёк'));
      await tester.pumpAndSettle();
      await expectLater(tester, meetsGuideline(guideline), reason: 'кошелёк');

      await tester.tap(find.text('Профиль'));
      await tester.pumpAndSettle();
      await expectLater(tester, meetsGuideline(guideline), reason: 'профиль');
      final list = find.descendant(of: find.byType(ProfileScreen), matching: find.byType(Scrollable));
      await tester.drag(list, const Offset(0, -2000));
      await tester.pumpAndSettle();
      await expectLater(tester, meetsGuideline(guideline), reason: 'низ профиля');

      await _unmount(tester);
      semantics.dispose();
    });
  }
}
