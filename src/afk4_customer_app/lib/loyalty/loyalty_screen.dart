import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';

/// Проценты приходят в базисных пунктах: 500 — это 5%. Дробную часть показываем, только
/// когда она есть, иначе «5,0%» выглядит как ошибка расчёта.
String formatBasisPoints(int basisPoints, {required String locale}) {
  final percent = basisPoints / 100;
  final text = percent == percent.roundToDouble()
      ? percent.round().toString()
      : percent.toStringAsFixed(1);
  return '${locale == 'en' ? text : text.replaceAll('.', ',')}%';
}

/// Кешбэк: сколько уже накоплено, за что начисляют и что с ним делать.
///
/// Экран существует не ради красоты: кешбэк начислялся и раньше, но игрок его не видел, а
/// невидимая лояльность никого не удерживает.
class LoyaltyScreen extends StatefulWidget {
  const LoyaltyScreen({super.key, required this.api});

  final PlayerApiClient api;

  @override
  State<LoyaltyScreen> createState() => _LoyaltyScreenState();
}

class _LoyaltyScreenState extends State<LoyaltyScreen> {
  PlayerLoyalty? _data;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final data = await widget.api.getLoyalty();
      if (mounted) {
        setState(() {
          _data = data;
          _failed = false;
        });
      }
    } on PlayerApiException {
      if (mounted) setState(() => _failed = _data == null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(l.customerLoyaltyTitle)),
      body: RefreshIndicator(onRefresh: _load, child: _body(l)),
    );
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final data = _data;

    if (data == null) {
      return _failed
          ? ListView(
              padding: const EdgeInsets.all(24),
              children: [
                Text(l.customerLoyaltyLoadError,
                    style: TextStyle(color: theme.colorScheme.error)),
              ],
            )
          : const Center(child: CircularProgressIndicator());
    }

    final locale = Localizations.localeOf(context).languageCode;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _EarnedCard(total: data.totalEarned),
        const SizedBox(height: 16),
        if (data.isOff)
          Text(
            l.customerLoyaltyOff,
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          )
        else ...[
          Text(l.customerLoyaltyRules, style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          if (data.topUpEnabled && data.topUpPercentBasisPoints > 0)
            _Rule(
              icon: Icons.account_balance_wallet_outlined,
              text: l.customerLoyaltyRuleTopUp(
                  formatBasisPoints(data.topUpPercentBasisPoints, locale: locale)),
            ),
          if (data.shopEnabled && data.shopPercentBasisPoints > 0)
            _Rule(
              icon: Icons.local_cafe_outlined,
              text: l.customerLoyaltyRuleShop(
                  formatBasisPoints(data.shopPercentBasisPoints, locale: locale)),
            ),
          if (data.sessionEnabled && data.sessionPercentBasisPoints > 0)
            _Rule(
              icon: Icons.sports_esports_outlined,
              text: l.customerLoyaltyRuleSession(
                  formatBasisPoints(data.sessionPercentBasisPoints, locale: locale)),
            ),
          const SizedBox(height: 8),
          // Главное о кешбэке: это не баллы, а деньги. Без этой строки игрок копит непонятно что.
          Text(
            l.customerLoyaltySpendNote,
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ],
        const SizedBox(height: 24),
        Text(l.customerLoyaltyHistory, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (data.recent.isEmpty)
          _EmptyHistory()
        else
          for (final entry in data.recent)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: _EntryRow(entry: entry),
            ),
      ],
    );
  }
}

class _EarnedCard extends StatelessWidget {
  const _EarnedCard({required this.total});

  final Money total;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final dark = theme.brightness == Brightness.dark;

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: theme.colorScheme.outline),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: dark
              ? [const Color(0xFF17322A), const Color(0xFF121B19)]
              : [const Color(0xFFE7F6EF), Colors.white],
        ),
        boxShadow: dark ? AppTheme.accentGlow(AppTheme.emerald.withValues(alpha: 0.28)) : null,
      ),
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            l.customerLoyaltyEarned,
            style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 10),
          Text(
            formatMoney(total.minorUnits, total.currencyCode, locale: locale),
            style: theme.textTheme.displaySmall,
          ),
        ],
      ),
    );
  }
}

class _Rule extends StatelessWidget {
  const _Rule({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          Icon(icon, size: 20, color: theme.colorScheme.primary),
          const SizedBox(width: 10),
          Expanded(child: Text(text, style: theme.textTheme.bodyLarge)),
        ],
      ),
    );
  }
}

class _EntryRow extends StatelessWidget {
  const _EntryRow({required this.entry});

  final CashbackEntry entry;

  String _source(L l) => switch (entry.source) {
        'topup' => l.customerLoyaltySourceTopUp,
        'shop' => l.customerLoyaltySourceShop,
        'session' => l.customerLoyaltySourceSession,
        // Незнакомый источник — не повод показывать служебную строку: смысл начисления
        // всё равно один, кешбэк.
        _ => l.customerLoyaltyTitle,
      };

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_source(l), style: theme.textTheme.bodyLarge),
              Text(
                formatDateTime(l, entry.createdAtUtc, locale),
                style:
                    theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
          ),
        ),
        Text(
          '+${formatMoney(entry.amount.minorUnits, entry.amount.currencyCode, locale: locale)}',
          style: theme.textTheme.titleMedium?.copyWith(color: theme.colorScheme.primary),
        ),
      ],
    );
  }
}

class _EmptyHistory extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l.customerLoyaltyEmpty, style: theme.textTheme.bodyLarge),
        Text(
          l.customerLoyaltyEmptyHint,
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
      ],
    );
  }
}
