import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../auth/player_session.dart';
import '../dashboard/dashboard_screen.dart';
import '../l10n/app_localizations.dart';
import '../organization/organization.dart';
import '../profile/profile_screen.dart';
import '../reservations/reservations_screen.dart';
import '../wallet/wallet_screen.dart';

/// Разделы приложения. Порядок — это заявление о том, чем игрок пользуется чаще: сначала то,
/// что происходит сейчас, потом деньги, в конце настройки.
enum AppSection { home, reservations, wallet, profile }

/// Оболочка вошедшего игрока: разделы внизу, содержимое сверху.
///
/// Раздел «Брони» появляется, только если клуб принимает онлайн-брони: вкладка, ведущая в
/// невозможное действие, хуже её отсутствия.
class AppShell extends StatefulWidget {
  const AppShell({
    super.key,
    required this.api,
    required this.session,
    required this.organization,
    required this.onSignOut,
    required this.onChangeClub,
    required this.onLocaleChanged,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final PlayerSession session;

  /// Клуб, в который игрок вошёл: его знак и цвет носит приложение, пока игрок здесь.
  final Organization organization;

  final VoidCallback onSignOut;
  final VoidCallback onChangeClub;
  final ValueChanged<Locale> onLocaleChanged;
  final DateTime Function() clock;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  AppSection _section = AppSection.home;

  /// null — список возможностей не получен. Тогда разделы показываются все: спрятать
  /// «Брони» из-за сетевого сбоя значит соврать игроку, что клуб их не принимает. Это
  /// удобство интерфейса, а не защита — сервер всё равно проверяет право на действие.
  List<String>? _features;

  /// Номер подтвердили прямо сейчас. Сессия в памяти этого ещё не знает, а входить заново
  /// ради открывшихся возможностей — плохая цена за подтверждение.
  bool _phoneVerifiedNow = false;

  @override
  void initState() {
    super.initState();
    _loadFeatures();
  }

  Future<void> _loadFeatures() async {
    try {
      final features = await widget.api.getFeatures();
      if (mounted) setState(() => _features = features);
    } on PlayerApiException {
      // Остаётся null — «считаем включённым».
    }
  }

  bool _enabled(String feature) => _features == null || _features!.contains(feature);

  bool get _phoneVerified => _phoneVerifiedNow || widget.session.phoneVerified;

  void _open(AppSection section) => setState(() => _section = section);

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final booking = _enabled('online_booking');

    final sections = <(AppSection, Widget screen, NavigationDestination tab)>[
      (
        AppSection.home,
        DashboardScreen(
          api: widget.api,
          displayName: widget.session.displayName,
          organization: widget.organization,
          phoneVerified: _phoneVerified,
          features: _features,
          onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
          onOpenReservations: booking ? () => _open(AppSection.reservations) : null,
          onOpenWallet: () => _open(AppSection.wallet),
          clock: widget.clock,
        ),
        NavigationDestination(
          icon: const Icon(Icons.home_outlined),
          selectedIcon: const Icon(Icons.home),
          label: l.customerNavDashboard,
        ),
      ),
      if (booking)
        (
          AppSection.reservations,
          ReservationsScreen(
            api: widget.api,
            phoneVerified: _phoneVerified,
            onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
            clock: widget.clock,
          ),
          NavigationDestination(
            icon: const Icon(Icons.event_outlined),
            selectedIcon: const Icon(Icons.event),
            label: l.customerNavReservations,
          ),
        ),
      (
        AppSection.wallet,
        WalletScreen(
          api: widget.api,
          phoneVerified: _phoneVerified,
          features: _features,
          onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
          clock: widget.clock,
        ),
        NavigationDestination(
          icon: const Icon(Icons.account_balance_wallet_outlined),
          selectedIcon: const Icon(Icons.account_balance_wallet),
          label: l.customerNavWallet,
        ),
      ),
      (
        AppSection.profile,
        ProfileScreen(
          api: widget.api,
          onSignOut: widget.onSignOut,
          onChangeClub: widget.onChangeClub,
          onLocaleChanged: widget.onLocaleChanged,
          onPhoneVerified: () => setState(() => _phoneVerifiedNow = true),
        ),
        NavigationDestination(
          icon: const Icon(Icons.person_outline),
          selectedIcon: const Icon(Icons.person),
          label: l.customerNavProfile,
        ),
      ),
    ];

    // Раздел ищется по имени, а не по номеру: список укорачивается, когда клуб не принимает
    // брони, и запомненный номер после этого указывал бы на соседа. Пропавший раздел
    // возвращает игрока на главную.
    final index = sections.indexWhere((section) => section.$1 == _section);
    final selected = index < 0 ? 0 : index;

    return Scaffold(
      // Разделы держатся живыми: вернувшись на главную, игрок видит её сразу, а не заново
      // загружающийся экран.
      body: IndexedStack(
        index: selected,
        children: [for (final (_, screen, _) in sections) screen],
      ),
      bottomNavigationBar: DecoratedBox(
        // Волосяная линия сверху: без неё панель сливается с прокрученным под неё списком, и
        // непонятно, где кончается содержимое и начинается навигация.
        decoration: BoxDecoration(
          border: Border(top: BorderSide(color: Theme.of(context).colorScheme.outline)),
        ),
        child: NavigationBar(
          selectedIndex: selected,
          onDestinationSelected: (position) => _open(sections[position].$1),
          destinations: [for (final (_, _, tab) in sections) tab],
        ),
      ),
    );
  }
}
