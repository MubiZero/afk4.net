import 'dart:async';

import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../wallet/wallet_card.dart';
import 'live_session_card.dart';

/// Главный экран: что происходит с сессией прямо сейчас и сколько денег в кошельке.
class DashboardScreen extends StatefulWidget {
  const DashboardScreen({
    super.key,
    required this.api,
    required this.displayName,
    required this.phoneVerified,
    required this.features,
    this.onOpenReservations,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final String displayName;
  final bool phoneVerified;
  /// Возможности клуба; null — список не получен. Загружает их оболочка: он нужен ещё и
  /// разделам, а два независимых запроса одного и того же — лишний трафик и рассинхрон.
  final List<String>? features;

  /// Куда вести из пустого состояния «нет сессии». null — клуб не принимает онлайн-брони,
  /// и звать туда некуда.
  final VoidCallback? onOpenReservations;

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
      appBar: AppBar(title: Text(widget.displayName)),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
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
              // Идущая сессия — то, ради чего экран открывают посреди игры, поэтому она
              // впереди денег. Когда сессии нет, впереди кошелёк: пустая карточка наверху
              // сообщала бы только об отсутствии.
              if (data.activeSession != null) ...[
                LiveSessionCard(
                  session: data.activeSession!,
                  fetchedAt: _fetchedAt ?? widget.clock(),
                  clock: widget.clock,
                ),
                const SizedBox(height: 12),
                _wallet(data),
              ] else ...[
                _wallet(data),
                const SizedBox(height: 12),
                _NoSessionCard(onBook: widget.onOpenReservations),
              ],
            ],
          ],
        ),
      ),
    );
  }

  Widget _wallet(PlayerDashboard data) => WalletCard(
        api: widget.api,
        walletBalance: data.walletBalance,
        debtBalance: data.debtBalance,
        phoneVerified: widget.phoneVerified,
        features: widget.features,
      );
}

/// Пустое состояние с выходом: раньше здесь была серая надпись и никакого следующего шага.
class _NoSessionCard extends StatelessWidget {
  const _NoSessionCard({required this.onBook});

  final VoidCallback? onBook;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 32, horizontal: 16),
        child: Column(
          children: [
            Text(
              l.customerDashboardNoSession,
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            if (onBook != null) ...[
              const SizedBox(height: 12),
              OutlinedButton(onPressed: onBook, child: Text(l.customerDashboardBookSeat)),
            ],
          ],
        ),
      ),
    );
  }
}
