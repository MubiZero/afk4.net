import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';

/// Часы пакета для показа. Секунды — единица сервера, человек считает часами, и «5 ч»
/// читается там, где «18000 с» не значит ничего. Неполный час показывается с минутами,
/// потому что пакет на 90 минут — обычное дело.
///
/// Единицы берутся из тех же строк, что и длительность визита в истории: два разных «мин»
/// в одном приложении — это два места, где перевод разъедется.
String formatPackageDuration(L l, int seconds) {
  final totalMinutes = seconds ~/ 60;
  final hours = totalMinutes ~/ 60;
  final minutes = totalMinutes % 60;
  if (hours == 0) return l.customerHistoryDurationMinutes('$minutes');
  if (minutes == 0) return l.customerPackagesHours('$hours');
  return l.customerHistoryDurationHoursMinutes('$hours', '$minutes');
}

/// Пакеты часов: предоплата вперёд, час дешевле поминутного тарифа.
///
/// Покупка идёт с кошелька и не ждёт открытой смены — предоплачивают как раз до того, как
/// дошли до клуба. Свои пакеты показываются здесь же: купить и не знать, сколько осталось,
/// значит купить вслепую.
class PackagesScreen extends StatefulWidget {
  const PackagesScreen({
    super.key,
    required this.api,
    required this.branchId,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final String branchId;
  final DateTime Function() clock;

  @override
  State<PackagesScreen> createState() => _PackagesScreenState();
}

class _PackagesScreenState extends State<PackagesScreen> {
  List<PackageOption>? _offers;
  List<PlayerPackage>? _mine;
  bool _failed = false;
  String? _buyingId;
  String? _error;

  /// Кошелёк списался — вернуть это главной, чтобы баланс там не остался прежним.
  bool _walletChanged = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final offers = await widget.api.getPackages(widget.branchId);
      final mine = await widget.api.getMyPackages();
      if (!mounted) return;
      setState(() {
        _offers = offers;
        _mine = mine;
        _failed = false;
      });
    } on PlayerApiException {
      if (!mounted) return;
      setState(() => _failed = _offers == null);
    }
  }

  Future<void> _buy(PackageOption offer) async {
    final l = L.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => _ConfirmDialog(offer: offer),
    );
    if (confirmed != true || !mounted) return;

    setState(() {
      _buyingId = offer.packageDefinitionId;
      _error = null;
    });

    try {
      await widget.api.purchasePackage(
        branchId: widget.branchId,
        packageDefinitionId: offer.packageDefinitionId,
        idempotencyKey: newIdempotencyKey(),
      );
      if (!mounted) return;
      unawaited(HapticFeedback.lightImpact());
      setState(() {
        _buyingId = null;
        _walletChanged = true;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l.customerPackagesBought(offer.name))),
      );
      await _load();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _buyingId = null;
        _error = switch (error.message) {
          'insufficient_funds' => l.customerPackagesErrFunds,
          _ => l.customerPackagesErrGeneric,
        };
      });
    } catch (_) {
      // Любой другой сбой обязан вернуть кнопку в рабочее состояние: висящее «Покупаем…»
      // читается как зависшее списание денег.
      if (!mounted) return;
      setState(() {
        _buyingId = null;
        _error = l.customerPackagesErrGeneric;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.of(context).pop(_walletChanged);
      },
      child: Scaffold(
        appBar: AppBar(title: Text(l.customerPackagesTitle)),
        body: RefreshIndicator(onRefresh: _load, child: _body(l)),
      ),
    );
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final offers = _offers;
    final mine = _mine;

    if (offers == null || mine == null) {
      return _failed
          ? ListView(
              padding: const EdgeInsets.all(24),
              children: [
                Text(l.customerPackagesLoadError,
                    style: TextStyle(color: theme.colorScheme.error)),
              ],
            )
          : const Center(child: CircularProgressIndicator());
    }

    final locale = Localizations.localeOf(context).languageCode;
    final now = widget.clock();
    final active = mine.where((package) => package.isUsable(now)).toList();
    final spent = mine.where((package) => !package.isUsable(now)).toList();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (_error != null) ...[
          Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
          const SizedBox(height: 16),
        ],
        if (active.isNotEmpty) ...[
          Text(l.customerPackagesMine, style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          for (final package in active)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: _MinePackageCard(package: package, locale: locale, clock: widget.clock),
            ),
          const SizedBox(height: 24),
        ],
        Text(l.customerPackagesOffers, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (offers.isEmpty)
          Text(
            l.customerPackagesNone,
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          )
        else
          for (final offer in offers)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: _OfferCard(
                offer: offer,
                locale: locale,
                busy: _buyingId == offer.packageDefinitionId,
                anyBusy: _buyingId != null,
                onBuy: () => _buy(offer),
              ),
            ),
        if (spent.isNotEmpty) ...[
          const SizedBox(height: 24),
          Text(l.customerPackagesSpent, style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          for (final package in spent)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: _MinePackageCard(package: package, locale: locale, clock: widget.clock),
            ),
        ],
      ],
    );
  }
}

class _ConfirmDialog extends StatelessWidget {
  const _ConfirmDialog({required this.offer});

  final PackageOption offer;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return AlertDialog(
      title: Text(offer.name),
      // Сумма и время в вопросе, а не только в карточке: подтверждают то, что видят в
      // подтверждении.
      content: Text(l.customerPackagesConfirm(
        formatMoney(offer.priceMinorUnits, offer.currencyCode, locale: locale),
        formatPackageDuration(l, offer.totalSeconds),
      )),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: Text(l.customerPackagesCancel),
        ),
        FilledButton(
          onPressed: () => Navigator.of(context).pop(true),
          child: Text(l.customerPackagesBuy),
        ),
      ],
    );
  }
}

class _OfferCard extends StatelessWidget {
  const _OfferCard({
    required this.offer,
    required this.locale,
    required this.busy,
    required this.anyBusy,
    required this.onBuy,
  });

  final PackageOption offer;
  final String locale;
  final bool busy;
  final bool anyBusy;
  final VoidCallback onBuy;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: theme.colorScheme.outline),
        color: theme.colorScheme.surface,
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(offer.name, style: theme.textTheme.titleMedium)),
              Text(
                formatMoney(offer.priceMinorUnits, offer.currencyCode, locale: locale),
                style: theme.textTheme.titleMedium,
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            formatPackageDuration(l, offer.includedSeconds),
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          // Бонусные часы — причина покупать пакет, а не платить поминутно; молчать о них
          // значит прятать выгоду.
          if (offer.bonusSeconds > 0)
            Text(
              l.customerPackagesBonus(formatPackageDuration(l, offer.bonusSeconds)),
              style: theme.textTheme.bodyMedium?.copyWith(color: AppTheme.emerald),
            ),
          if (offer.expiresAfterDays > 0)
            Text(
              l.customerPackagesExpiresIn(offer.expiresAfterDays),
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: anyBusy ? null : onBuy,
              child: Text(busy ? l.customerPackagesBuying : l.customerPackagesBuy),
            ),
          ),
        ],
      ),
    );
  }
}

class _MinePackageCard extends StatelessWidget {
  const _MinePackageCard({
    required this.package,
    required this.locale,
    required this.clock,
  });

  final PlayerPackage package;
  final String locale;
  final DateTime Function() clock;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final now = clock();
    final usable = package.isUsable(now);

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(
          color: usable ? AppTheme.emerald.withValues(alpha: 0.5) : theme.colorScheme.outline,
        ),
        color: theme.colorScheme.surface,
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(package.name, style: theme.textTheme.titleSmall),
          const SizedBox(height: 4),
          Text(
            usable
                ? l.customerPackagesLeft(formatPackageDuration(l, package.remainingSeconds))
                : package.isSpent
                    ? l.customerPackagesUsedUp
                    : l.customerPackagesExpired,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: usable ? AppTheme.emerald : theme.colorScheme.onSurfaceVariant,
            ),
          ),
          if (package.expiresAtUtc != null && usable)
            Text(
              l.customerPackagesValidUntil(
                  formatDateTime(l, package.expiresAtUtc!, locale, now: now)),
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
        ],
      ),
    );
  }
}
