import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../auth/player_session.dart';
import '../dashboard/dashboard_screen.dart';
import '../history/history_screen.dart';
import '../l10n/app_localizations.dart';
import '../profile/profile_screen.dart';
import '../reservations/reservations_screen.dart';

/// Оболочка вошедшего игрока: разделы внизу, содержимое сверху.
///
/// Раздел «Брони» появляется, только если клуб принимает онлайн-брони: вкладка, ведущая в
/// невозможное действие, хуже её отсутствия.
class AppShell extends StatefulWidget {
  const AppShell({
    super.key,
    required this.api,
    required this.session,
    required this.onSignOut,
    required this.onChangeClub,
    required this.onLocaleChanged,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final PlayerSession session;
  final VoidCallback onSignOut;
  final VoidCallback onChangeClub;
  final ValueChanged<Locale> onLocaleChanged;
  final DateTime Function() clock;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _section = 0;

  /// null — список возможностей не получен. Тогда разделы показываются все: спрятать
  /// «Брони» из-за сетевого сбоя значит соврать игроку, что клуб их не принимает. Это
  /// удобство интерфейса, а не защита — сервер всё равно проверяет право на действие.
  List<String>? _features;

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

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final booking = _enabled('online_booking');

    final sections = <(Widget screen, NavigationDestination tab)>[
      (
        DashboardScreen(
          api: widget.api,
          displayName: widget.session.displayName,
          phoneVerified: widget.session.phoneVerified,
          features: _features,
          clock: widget.clock,
        ),
        NavigationDestination(
          icon: const Icon(Icons.home_outlined),
          selectedIcon: const Icon(Icons.home),
          label: l.customerNavDashboard,
        ),
      ),
      (
        HistoryScreen(api: widget.api, clock: widget.clock),
        NavigationDestination(
          icon: const Icon(Icons.history_outlined),
          selectedIcon: const Icon(Icons.history),
          label: l.customerNavHistory,
        ),
      ),
      if (booking)
        (
          ReservationsScreen(
            api: widget.api,
            phoneVerified: widget.session.phoneVerified,
            clock: widget.clock,
          ),
          NavigationDestination(
            icon: const Icon(Icons.event_outlined),
            selectedIcon: const Icon(Icons.event),
            label: l.customerNavReservations,
          ),
        ),
      (
        ProfileScreen(
          api: widget.api,
          onSignOut: widget.onSignOut,
          onChangeClub: widget.onChangeClub,
          onLocaleChanged: widget.onLocaleChanged,
        ),
        NavigationDestination(
          icon: const Icon(Icons.person_outline),
          selectedIcon: const Icon(Icons.person),
          label: l.customerNavProfile,
        ),
      ),
    ];

    return Scaffold(
      // Разделы держатся живыми: вернувшись на главную, игрок видит её сразу, а не заново
      // загружающийся экран.
      body: IndexedStack(
        // Список разделов может укоротиться, когда придёт ответ про возможности, — тогда
        // выбранный индекс упирается в последний доступный, а не в пустоту.
        index: _section.clamp(0, sections.length - 1),
        children: [for (final (screen, _) in sections) screen],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _section.clamp(0, sections.length - 1),
        onDestinationSelected: (index) => setState(() => _section = index),
        destinations: [for (final (_, tab) in sections) tab],
      ),
    );
  }
}
