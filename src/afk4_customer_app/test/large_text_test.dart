import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/api/contracts.dart';
import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/app.dart' show maxTextScale;
import 'package:afk4_customer_app/auth/player_session.dart';
import 'package:afk4_customer_app/auth/sign_in_screen.dart';
import 'package:afk4_customer_app/dashboard/extend_session_sheet.dart';
import 'package:afk4_customer_app/events/events_screen.dart';
import 'package:afk4_customer_app/friends/friends_screen.dart';
import 'package:afk4_customer_app/history/receipt_screen.dart';
import 'package:afk4_customer_app/l10n/localization_setup.dart';
import 'package:afk4_customer_app/loyalty/loyalty_screen.dart';
import 'package:afk4_customer_app/notifications/notifications_screen.dart';
import 'package:afk4_customer_app/organization/club_details_sheet.dart';
import 'package:afk4_customer_app/organization/club_picker_screen.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';
import 'package:afk4_customer_app/packages/packages_screen.dart';
import 'package:afk4_customer_app/phone/phone_verification_sheet.dart';
import 'package:afk4_customer_app/play/pc_sign_in_screen.dart';
import 'package:afk4_customer_app/play/start_session_screen.dart';
import 'package:afk4_customer_app/profile/pin_sheet.dart';
import 'package:afk4_customer_app/progress/progress_screen.dart';
import 'package:afk4_customer_app/referral/referral_screen.dart';
import 'package:afk4_customer_app/reservations/new_reservation_sheet.dart';
import 'package:afk4_customer_app/reviews/club_reviews_sheet.dart';
import 'package:afk4_customer_app/reviews/review_sheet.dart';
import 'package:afk4_customer_app/shell/app_shell.dart';
import 'package:afk4_customer_app/shop/shop_screen.dart';
import 'package:afk4_customer_app/theme/app_theme.dart';
import 'package:afk4_customer_app/wallet/top_up_sheet.dart';

import 'support/fake_http.dart';
import 'support/real_fonts.dart';

// Крупный шрифт системы — до двух раз (решение владельца 29.09). Каждый экран и каждый лист
// приложения открывается на узком телефоне с двукратным шрифтом и пролистывается до конца:
// переполнение строки Flutter сообщает ошибкой сам, и тест падает на первом же. Меряется
// настоящим Roboto — тестовый шрифт вдвое шире и переполнял бы то, что на телефоне помещается.

final _now = DateTime.utc(2026, 9, 29, 12);
String _iso(Duration d) => _now.add(d).toIso8601String();
Map<String, dynamic> _money(int minor) => {'currencyCode': 'TJS', 'minorUnits': minor};

const _club = Organization(
  organizationId: 'o1',
  slug: 'cyberx',
  name: 'CyberX Рудаки — самый длинный зал сети',
  pricePerHourFromMinorUnits: 1500,
  currencyCode: 'TJS',
  seatCount: 40,
  rating: 4.7,
  reviewCount: 128,
  places: [
    ClubPlace(
      branchId: 'b1',
      name: 'Рудаки',
      city: 'Душанбе',
      address: 'пр. Рудаки, 45',
      seatCount: 40,
      freeSeatCount: 12,
      latitude: 38.5598,
      longitude: 68.7870,
    ),
    ClubPlace(branchId: 'b2', name: 'Сомони', city: 'Худжанд', address: 'ул. Ленина, 12', seatCount: 24),
  ],
);

Map<String, dynamic> _session() => {
      'sessionId': 's1',
      'seatId': 'seat-7',
      'seatName': 'ПК-07',
      'startedAtUtc': _iso(const Duration(minutes: -48)),
      'durationMode': 'fixed',
      'remainingSeconds': 4260,
      'accruedCostMinorUnits': null,
      'currencyCode': 'TJS',
      'tariffName': 'Вечерний',
      'pricePerHourMinorUnits': 1500,
      'zoneName': 'VIP',
    };

Object? _route(String path, {required bool session}) => switch (path) {
      '/api/me/dashboard' => {
          'walletBalance': _money(45000),
          'heldBalance': _money(3000),
          'debtBalance': _money(1200),
          'activeSession': session ? _session() : null,
        },
      '/api/me/features' => {
          'features': ['online_topup', 'online_booking', 'player_shop', 'loyalty']
        },
      '/api/me/profile' => {
          'playerAccountId': 'p1',
          'displayName': 'Фаррух Рахмонов',
          'phoneNumber': '+992 90 123-45-67',
          'phoneVerified': true,
          'preferredLocale': 'ru',
          'marketingOptIn': true,
          'homeBranchId': 'b1',
          'homeBranchName': 'CyberX Рудаки',
        },
      '/api/me/notifications' => {
          'notifications': [
            {
              'id': 'n1',
              'templateKey': 'session_ending',
              'subject': 'Сессия заканчивается',
              'body': 'Через десять минут время на ПК-07 закончится. Продлите, если хотите остаться.',
              'createdAtUtc': _iso(const Duration(minutes: -5)),
              'isUnread': true,
            },
          ],
          'unreadCount': 1,
        },
      '/api/me/news' => [
          {
            'id': 'n1',
            'title': 'Ночь скидок по пятницам и субботам',
            'body': 'С 22:00 до 8:00 час дешевле на треть',
            'imageUrl': null,
            'publishedAtUtc': _iso(const Duration(days: -1)),
          },
        ],
      '/api/me/referral' => {
          'enabled': true,
          'code': 'FARR42',
          'referrerBonusMinorUnits': 2000,
          'inviteeBonusMinorUnits': 1000,
          'minimumTopUpMinorUnits': 5000,
          'currencyCode': 'TJS',
          'invitedCount': 2,
          'rewardedCount': 1,
          'earnedMinorUnits': 2000,
          'hasClaimedCode': false,
          'canClaimCode': true,
        },
      '/api/me/loyalty' => {
          'topUpEnabled': true,
          'topUpPercentBasisPoints': 500,
          'shopEnabled': true,
          'shopPercentBasisPoints': 300,
          'sessionEnabled': true,
          'sessionPercentBasisPoints': 200,
          'totalEarned': _money(4500),
          'recent': [
            {'amountMinorUnits': 2500, 'currencyCode': 'TJS', 'reason': 'cashback:topup', 'createdAtUtc': _iso(const Duration(days: -2))},
          ],
        },
      '/api/me/achievements' => {
          'level': 3,
          'visitCount': 12,
          'playedMinutes': 2400,
          'minutesToNextLevel': 600,
          'achievements': [
            {'code': 'first_visit', 'progress': 1, 'target': 1, 'unlockedAtUtc': _iso(const Duration(days: -30))},
            {'code': 'veteran', 'progress': 12, 'target': 50, 'unlockedAtUtc': null},
          ],
        },
      '/api/me/branches/b1/packages' => [
          {
            'packageDefinitionId': 'pkg1',
            'name': 'Ночной пакет на пять часов с бонусом',
            'priceMinorUnits': 40000,
            'currencyCode': 'TJS',
            'includedSeconds': 18000,
            'bonusSeconds': 1800,
            'expiresAfterDays': 30,
          },
        ],
      '/api/me/packages' => [],
      '/api/me/branches/b1/tournaments' => [
          {
            'tournamentId': 't1',
            'branchId': 'b1',
            'branchName': 'CyberX Рудаки',
            'title': 'Кубок CyberX по Counter-Strike 2',
            'description': 'Пять на пять, свои команды',
            'discipline': 'Counter-Strike 2',
            'startsAtUtc': _iso(const Duration(days: 4)),
            'entryFee': _money(5000),
            'capacity': 16,
            'registeredCount': 9,
            'isRegistered': true,
            'state': 'open',
            'cancelReason': '',
          },
        ],
      '/api/me/branches/b1/seats' => [
          for (var i = 1; i <= 4; i++)
            {'seatId': 'seat-$i', 'seatName': 'ПК-0$i', 'zoneName': 'Зал', 'isAvailable': i.isEven},
        ],
      '/api/me/branches/b1/tariffs' => [
          for (final (id, name) in [('tv1', 'Стандарт'), ('tv2', 'VIP с мягким креслом')])
            {
              'tariffId': 't$id',
              'tariffVersionId': id,
              'name': name,
              'tariffRuleVersionId': 'r$id',
              'versionNumber': 1,
              'currencyCode': 'TJS',
              'pricePerMinuteMinorUnits': 25,
              'minimumBillableMinutes': 30,
              'roundingIncrementMinutes': 5,
              'effectiveFromUtc': '2026-01-01T00:00:00Z',
              'appliesNow': true,
            },
        ],
      '/api/me/reservations/quote' => {
          'tariffVersionId': 'tv1',
          'tariffName': 'Стандарт',
          'requestedMinutes': 60,
          'billableMinutes': 60,
          'amountMinorUnits': 1500,
          'currencyCode': 'TJS',
        },
      '/api/me/reservations' => [
          {
            'reservationId': 'r1',
            'seatId': 'seat-1',
            'seatName': 'ПК-12',
            'startsAtUtc': _iso(const Duration(days: 1)),
            'endsAtUtc': _iso(const Duration(days: 1, hours: 2)),
            'state': 'pending',
            'note': null,
            'reservationGroupId': null,
            'estimatedCostMinorUnits': 3000,
            'currencyCode': 'TJS',
            'respondByUtc': _iso(const Duration(minutes: 14)),
          },
        ],
      '/api/me/wallet/top-up-intents' => [],
      '/api/me/wallet/top-up-methods' => {'counter': true, 'online': true},
      '/api/me/sessions/s1/extend-offers' => {
          'sessionId': 's1',
          'balance': _money(45000),
          'options': [
            for (final minutes in [30, 60, 120, 180])
              {
                'minutes': minutes,
                'billableMinutes': minutes,
                'endsAtUtc': _iso(Duration(minutes: 71 + minutes)),
                'amount': _money(minutes * 25),
                'balanceAfter': _money(45000 - minutes * 25),
                'affordable': true,
              },
          ],
          'unavailableReason': null,
        },
      '/api/me/shop/catalog' => [
          for (final (id, name, price, stock) in [
            ('p1', 'Кола 0,5', 1200, 40),
            ('p2', 'Бургер с говядиной и сыром чеддер', 3500, 2),
          ])
            {'productId': id, 'name': name, 'sku': id, 'price': _money(price), 'stockOnHand': stock},
        ],
      '/api/me/shop/orders' => [],
      '/api/me/visits' => {
          'items': [
            {
              'sessionId': 'v1',
              'seatId': 'seat-1',
              'seatName': 'ПК-01',
              'startedAtUtc': _iso(const Duration(days: -1, hours: -3)),
              'endedAtUtc': _iso(const Duration(days: -1)),
              'timeChargeMinorUnits': 4500,
              'posTotalMinorUnits': 1200,
              'grandTotalMinorUnits': 5700,
              'currencyCode': 'TJS',
              'hasReceipt': true,
            },
          ],
          'nextCursor': null,
        },
      '/api/me/visits/v1/receipt' => {
          'receiptNumber': 'Ч-000123',
          'createdAtUtc': _iso(const Duration(days: -1)),
          'sessionId': 'v1',
          'seatName': 'ПК-01',
          'startedAtUtc': _iso(const Duration(days: -1, hours: -3)),
          'endedAtUtc': _iso(const Duration(days: -1)),
          'timeChargeMinorUnits': 4500,
          'posLines': [
            {'productName': 'Бургер с говядиной', 'quantity': 1, 'unitPriceMinorUnits': 1200, 'lineTotalMinorUnits': 1200},
          ],
          'posTotalMinorUnits': 1200,
          'grandTotalMinorUnits': 5700,
          'currencyCode': 'TJS',
        },
      '/api/me/purchases' || '/api/me/wallet/ledger' => {'items': [], 'nextCursor': null},
      '/api/me/friends' => {
          'friends': [
            {
              'platformPersonId': 'f1',
              'displayName': 'Азиз Каримов',
              'presence': {'organizationName': 'CyberX', 'branchName': 'Рудаки'},
            },
          ],
          'incoming': [
            {'friendRequestId': 'q1', 'platformPersonId': 'f3', 'displayName': 'Бехруз Назаров', 'createdAtUtc': _iso(const Duration(hours: -2))},
          ],
          'outgoing': [],
          'showsPresence': true,
        },
      _ => null,
    };

PlayerApiClient _api({bool session = false}) => PlayerApiClient(
      baseUrl: 'https://api',
      httpClient: FakeHttpClient((request) {
        final hit = _route(request.url.path, session: session);
        if (hit == null && request.url.path == '/api/me/reviews/pending') return ('', 204);
        return (jsonEncode(hit ?? []), 200);
      }),
      session: PlayerSession(
        playerAccountId: 'p1',
        organizationId: 'o1',
        displayName: 'Фаррух',
        phoneVerified: true,
        accessToken: 'a',
        accessTokenExpiresAtUtc: _now.add(const Duration(days: 1)),
        refreshToken: 'r',
        refreshTokenExpiresAtUtc: _now.add(const Duration(days: 30)),
      ),
    );

final _me = MeDto.fromJson({
  'person': {
    'platformPersonId': 'pp1',
    'phoneNumber': '+992 90 123-45-67',
    'displayName': 'Фаррух Рахмонов',
    'preferredLocale': 'ru',
    'phoneVerified': true,
    'pinSet': false,
    'networkBanned': false,
  },
  'clubs': [
    {
      'organizationId': 'o1',
      'organizationName': 'CyberX',
      'playerAccountId': 'p1',
      'homeBranchId': 'b1',
      'currencyCode': 'TJS',
      'walletBalanceMinorUnits': 45000,
      'heldMinorUnits': 3000,
      'debtMinorUnits': 0,
      'visitCount': 12,
    },
  ],
});

class _Directory extends OrganizationDirectory {
  _Directory() : super(baseUrl: 'https://stub');

  @override
  Future<List<Organization>> search({String? query}) async => const [_club, _club];
}

/// Экран на узком телефоне с шрифтом, увеличенным системой вдвое, — через тот же потолок, что
/// ставит корень приложения.
Widget _phone(Widget home) => MaterialApp(
      theme: AppTheme.dark(),
      locale: const Locale('ru'),
      localizationsDelegates: appLocalizationsDelegates,
      supportedLocales: appSupportedLocales,
      builder: (context, child) => MediaQuery.withClampedTextScaling(
        maxScaleFactor: maxTextScale,
        child: child ?? const SizedBox.shrink(),
      ),
      home: home,
    );

/// Лист, открытый поверх пустого экрана.
Widget _sheet(Widget Function(BuildContext) sheet) => _phone(
      Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: TextButton(
              onPressed: () => showModalBottomSheet<void>(
                context: context,
                isScrollControlled: true,
                useSafeArea: true,
                builder: sheet,
              ),
              child: const Text('открыть'),
            ),
          ),
        ),
      ),
    );

AppShell _shell({bool session = false}) => AppShell(
      api: _api(session: session),
      session: _api().session!,
      organization: _club,
      me: _me,
      onSignOut: () {},
      onChangeClub: () {},
      onLocaleChanged: (_) {},
      onThemeModeChanged: (_) {},
      clock: () => _now,
    );

Future<void> _prepare(WidgetTester tester) async {
  await tester.runAsync(loadRealFonts);
  tester.view.physicalSize = const Size(360 * 3, 780 * 3);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  tester.platformDispatcher.textScaleFactorTestValue = 2.0;
  addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
}

/// Пролистать экран до конца — ленивые списки строят строки только у края.
Future<void> _scrollThrough(WidgetTester tester) async {
  for (var step = 0; step < 8; step++) {
    await tester.dragFrom(const Offset(180, 560), const Offset(0, -420));
    await tester.pumpAndSettle();
  }
}

Future<void> _unmount(WidgetTester tester) async {
  await tester.pumpWidget(const SizedBox.shrink());
  await tester.pump(const Duration(seconds: 1));
}

void main() {
  final screens = <String, Widget Function()>{
    'главная без сессии': () => _phone(_shell()),
    'главная с сессией': () => _phone(_shell(session: true)),
    'витрина клубов': () => _phone(ClubPickerScreen(directory: _Directory(), onSelected: (_) {})),
    'вход': () => _phone(SignInScreen(
          organization: _club,
          api: _api(),
          onSignedIn: () {},
          onChangeClub: () {},
          onLocaleChanged: (_) {},
        )),
    'сесть за ПК': () => _phone(PcSignInScreen(api: _api(), enableCamera: false, branchId: 'b1')),
    'шаг тарифа': () => _phone(StartSessionScreen(api: _api(), branchId: 'b1', seatingCode: '482913', pinSet: false)),
    'бар': () => _phone(ShopScreen(api: _api(), place: _club.name)),
    'пакеты': () => _phone(PackagesScreen(api: _api(), branchId: 'b1', clock: () => _now, place: _club.name)),
    'события': () => _phone(EventsScreen(api: _api(), branchId: 'b1', clock: () => _now, place: _club.name)),
    'кешбэк': () => _phone(LoyaltyScreen(api: _api(), place: _club.name)),
    'приглашения': () => _phone(ReferralScreen(api: _api(), place: _club.name)),
    'друзья': () => _phone(FriendsScreen(api: _api())),
    'уведомления': () => _phone(NotificationsScreen(api: _api())),
    'стаж': () => _phone(ProgressScreen(api: _api())),
    'чек': () => _phone(ReceiptScreen(api: _api(), sessionId: 'v1', clock: () => _now)),
  };

  final sheets = <String, Widget Function(BuildContext)>{
    'пополнение': (_) => TopUpSheet(api: _api(), currencyCode: 'TJS', intents: const []),
    'продление': (_) => ExtendSessionSheet(api: _api(session: true), sessionId: 's1', onTopUp: () {}),
    'новая бронь': (_) => NewReservationSheet(api: _api(), clock: () => _now),
    'ПИН-код': (_) => PinSheet(api: _api(), pinSet: false),
    'подтверждение номера': (_) => PhoneVerificationSheet(api: _api()),
    'оценка визита': (_) => ReviewSheet(
          api: _api(),
          visit: PendingClubReviewDto.fromJson({
            'sessionId': 's1',
            'branchName': 'CyberX Рудаки',
            'seatName': 'ПК-07',
            'endedAtUtc': _iso(const Duration(hours: -2)),
          }),
        ),
    'подробности клуба': (_) => ClubDetailsSheet(club: _club, onChoose: () {}),
    'отзывы клуба': (_) => ClubReviewsSheet(directory: _Directory(), club: _club),
  };

  group('шрифт ×2 на узком телефоне', () {
    for (final MapEntry(key: name, value: build) in screens.entries) {
      testWidgets('экран «$name» не переполняется', (tester) async {
        await _prepare(tester);
        await tester.pumpWidget(build());
        await tester.pumpAndSettle();
        await _scrollThrough(tester);
        await _unmount(tester);
      });
    }

    // Разделы оболочки: каждую вкладку внизу — тоже до конца.
    for (final (index, name) in [(1, 'Брони'), (2, 'Баланс'), (3, 'Профиль')]) {
      testWidgets('раздел «$name» не переполняется', (tester) async {
        await _prepare(tester);
        await tester.pumpWidget(_phone(_shell()));
        await tester.pumpAndSettle();
        await tester.tap(find.byType(NavigationDestination).at(index));
        await tester.pumpAndSettle();
        await _scrollThrough(tester);
        await _unmount(tester);
      });
    }

    for (final MapEntry(key: name, value: build) in sheets.entries) {
      testWidgets('лист «$name» не переполняется', (tester) async {
        await _prepare(tester);
        await tester.pumpWidget(_sheet(build));
        await tester.tap(find.text('открыть'));
        await tester.pumpAndSettle();
        await _scrollThrough(tester);
        await _unmount(tester);
      });
    }
  });
}
