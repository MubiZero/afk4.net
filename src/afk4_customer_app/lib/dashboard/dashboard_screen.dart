import 'dart:async';

import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../wallet/wallet_panel.dart';
import 'live_session_card.dart';

/// Главный экран: сколько денег в кошельке и что происходит с сессией прямо сейчас.
class DashboardScreen extends StatefulWidget {
  const DashboardScreen({
    super.key,
    required this.api,
    required this.displayName,
    required this.phoneVerified,
    required this.onSignOut,
    required this.features,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final String displayName;
  final bool phoneVerified;
  final VoidCallback onSignOut;

  /// Возможности клуба; null — список не получен. Загружает их оболочка: он нужен ещё и
  /// разделам, а два независимых запроса одного и того же — лишний трафик и рассинхрон.
  final List<String>? features;

  final DateTime Function() clock;

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  /// Как часто перезапрашиваются деньги и сессия. Чаще незачем: секунды таймера дорисовывает
  /// сама карточка, а баланс так часто не меняется.
  static const Duration _refreshEvery = Duration(seconds: 30);

  Timer? _poll;
  PlayerDashboard? _data;
  DateTime? _fetchedAt;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    _refresh();
    _poll = Timer.periodic(_refreshEvery, (_) => _refresh());
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    try {
      final data = await widget.api.getDashboard();
      if (!mounted) return;
      setState(() {
        _data = data;
        _fetchedAt = widget.clock();
        _failed = false;
      });
    } on PlayerApiException {
      if (!mounted) return;
      // Уже показанные цифры не стираются: пропавшая на секунду сеть не повод заменять
      // баланс сообщением об ошибке. Ошибка видна, только пока показывать нечего.
      if (_data == null) setState(() => _failed = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final data = _data;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.displayName),
        actions: [
          // Временно живёт здесь: свой экран профиля с выходом появится шагом позже.
          IconButton(
            onPressed: widget.onSignOut,
            icon: const Icon(Icons.logout),
            tooltip: l.customerProfileSignOut,
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(l.customerDashboardWelcome, style: theme.textTheme.bodyMedium),
            const SizedBox(height: 16),
            if (data == null && _failed)
              Text(l.customerDashboardLoadError, style: TextStyle(color: theme.colorScheme.error))
            else if (data == null)
              Semantics(
                label: l.a11yLoadingDashboard,
                child: const Center(child: Padding(
                  padding: EdgeInsets.symmetric(vertical: 32),
                  child: CircularProgressIndicator(),
                )),
              )
            else ...[
              _BalanceCard(data: data),
              const SizedBox(height: 12),
              WalletPanel(
                api: widget.api,
                phoneVerified: widget.phoneVerified,
                features: widget.features,
              ),
              const SizedBox(height: 12),
              if (data.activeSession != null)
                LiveSessionCard(
                  session: data.activeSession!,
                  fetchedAt: _fetchedAt ?? widget.clock(),
                  clock: widget.clock,
                )
              else
                Card(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 32, horizontal: 16),
                    child: Center(
                      child: Text(
                        l.customerDashboardNoSession,
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                    ),
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _BalanceCard extends StatelessWidget {
  const _BalanceCard({required this.data});

  final PlayerDashboard data;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              l.customerDashboardBalance,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 4),
            Text(
              formatMoney(data.walletBalance.minorUnits, data.walletBalance.currencyCode, locale: locale),
              style: theme.textTheme.headlineMedium,
            ),
            // Долг показывается, только когда он есть: строка «Долг: 0» на главной пугает зря.
            if (data.debtBalance.minorUnits > 0)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  '${l.customerDashboardDebt}: '
                  '${formatMoney(data.debtBalance.minorUnits, data.debtBalance.currencyCode, locale: locale)}',
                  style: TextStyle(color: theme.colorScheme.error),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
