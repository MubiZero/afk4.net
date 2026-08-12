import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../auth/player_session.dart';
import '../dashboard/dashboard_screen.dart';
import '../history/history_screen.dart';
import '../l10n/app_localizations.dart';

/// Оболочка вошедшего игрока: разделы внизу, содержимое сверху.
///
/// Разделы добавляются по мере готовности — «Брони» и «Профиль» появятся следующими шагами.
/// Пустая вкладка-заглушка хуже её отсутствия: она обещает то, чего нет.
class AppShell extends StatefulWidget {
  const AppShell({
    super.key,
    required this.api,
    required this.session,
    required this.onSignOut,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final PlayerSession session;
  final VoidCallback onSignOut;
  final DateTime Function() clock;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _section = 0;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return Scaffold(
      // Разделы держатся живыми: вернувшись на главную, игрок видит её сразу, а не заново
      // загружающийся экран.
      body: IndexedStack(
        index: _section,
        children: [
          DashboardScreen(
            api: widget.api,
            displayName: widget.session.displayName,
            phoneVerified: widget.session.phoneVerified,
            onSignOut: widget.onSignOut,
            clock: widget.clock,
          ),
          HistoryScreen(api: widget.api, clock: widget.clock),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _section,
        onDestinationSelected: (index) => setState(() => _section = index),
        destinations: [
          NavigationDestination(
            icon: const Icon(Icons.home_outlined),
            selectedIcon: const Icon(Icons.home),
            label: l.customerNavDashboard,
          ),
          NavigationDestination(
            icon: const Icon(Icons.history_outlined),
            selectedIcon: const Icon(Icons.history),
            label: l.customerNavHistory,
          ),
        ],
      ),
    );
  }
}
