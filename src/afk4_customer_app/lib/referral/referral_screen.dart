import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';

/// «Приведи друга».
///
/// Платит клуб и он же назначает суммы, поэтому условия приходят с сервера, а не зашиты сюда.
/// Деньги приходят не за код, а за первое настоящее пополнение друга — экран говорит об этом
/// прямо, иначе игрок ждёт бонус сразу после того, как назовёт код.
class ReferralScreen extends StatefulWidget {
  const ReferralScreen({super.key, required this.api});

  final PlayerApiClient api;

  @override
  State<ReferralScreen> createState() => _ReferralScreenState();
}

class _ReferralScreenState extends State<ReferralScreen> {
  PlayerReferral? _data;
  bool _failed = false;
  final TextEditingController _codeController = TextEditingController();
  bool _claiming = false;
  String? _claimError;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final data = await widget.api.getReferral();
      if (!mounted) return;
      setState(() {
        _data = data;
        _failed = false;
      });
    } on PlayerApiException {
      if (mounted) setState(() => _failed = _data == null);
    }
  }

  Future<void> _copyCode(String code) async {
    final l = L.of(context);
    await Clipboard.setData(ClipboardData(text: code));
    if (!mounted) return;
    unawaited(HapticFeedback.lightImpact());
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(l.customerReferralCopied)),
    );
  }

  Future<void> _claim() async {
    final l = L.of(context);
    final code = _codeController.text.trim();
    if (code.isEmpty) return;

    setState(() {
      _claiming = true;
      _claimError = null;
    });

    try {
      final name = await widget.api.claimReferralCode(code);
      if (!mounted) return;
      unawaited(HapticFeedback.lightImpact());
      setState(() => _claiming = false);
      _codeController.clear();
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text(name == null || name.isEmpty
            ? l.customerReferralClaimedPlain
            : l.customerReferralClaimed(name)),
      ));
      await _load();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _claiming = false;
        // Каждая причина названа своим словом: выход из «это ваш код» и из «окно закрылось»
        // разный, и общая «не получилось» отправляет игрока гадать.
        _claimError = switch (error.message) {
          'referral_own_code' => l.customerReferralErrOwnCode,
          'referral_already_claimed' => l.customerReferralErrAlready,
          'referral_window_closed' => l.customerReferralErrWindow,
          'referral_unknown_code' => l.customerReferralErrUnknown,
          'referral_disabled' => l.customerReferralOff,
          _ => l.customerReferralErrGeneric,
        };
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _claiming = false;
        _claimError = l.customerReferralErrGeneric;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l.customerReferralTitle)),
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
                Text(l.customerReferralLoadError, style: TextStyle(color: theme.colorScheme.error)),
              ],
            )
          : const Center(child: CircularProgressIndicator());
    }

    if (!data.enabled) {
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text(
            l.customerReferralOff,
            style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ],
      );
    }

    final locale = Localizations.localeOf(context).languageCode;
    String money(int minor) => formatMoney(minor, data.currencyCode, locale: locale);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _CodeCard(code: data.code ?? '', onCopy: () => _copyCode(data.code ?? '')),
        const SizedBox(height: 20),
        Text(l.customerReferralHow, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        _Step(number: '1', text: l.customerReferralStepShare),
        _Step(
          number: '2',
          text: data.minimumTopUpMinorUnits > 0
              ? l.customerReferralStepTopUp(money(data.minimumTopUpMinorUnits))
              : l.customerReferralStepTopUpAny,
        ),
        _Step(
          number: '3',
          text: l.customerReferralStepReward(
            money(data.referrerBonusMinorUnits),
            money(data.inviteeBonusMinorUnits),
          ),
        ),
        const SizedBox(height: 24),
        Text(l.customerReferralMine, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        _StatsRow(
          invited: data.invitedCount,
          rewarded: data.rewardedCount,
          earned: money(data.earnedMinorUnits),
        ),
        const SizedBox(height: 24),
        if (data.hasClaimedCode)
          Text(
            l.customerReferralAlreadyInvited,
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          )
        else if (data.canClaimCode) ...[
          Text(l.customerReferralHaveCode, style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          TextField(
            controller: _codeController,
            textCapitalization: TextCapitalization.characters,
            decoration: InputDecoration(
              labelText: l.customerReferralEnterCode,
              errorText: _claimError,
            ),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _claiming ? null : _claim,
            child: Text(_claiming ? l.customerReferralClaiming : l.customerReferralClaim),
          ),
        ] else
          Text(
            l.customerReferralWindowClosedNote,
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
      ],
    );
  }
}

class _CodeCard extends StatelessWidget {
  const _CodeCard({required this.code, required this.onCopy});

  final String code;
  final VoidCallback onCopy;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: AppTheme.emerald.withValues(alpha: 0.5)),
        color: theme.colorScheme.surface,
      ),
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            l.customerReferralYourCode,
            style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 10),
          Text(
            code,
            // Разрядка: код переписывают от руки и называют голосом, и слипшиеся знаки
            // читаются с ошибкой.
            style: theme.textTheme.displaySmall?.copyWith(letterSpacing: 4),
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: onCopy,
            icon: const Icon(Icons.copy_outlined),
            label: Text(l.customerReferralCopy),
          ),
        ],
      ),
    );
  }
}

class _Step extends StatelessWidget {
  const _Step({required this.number, required this.text});

  final String number;
  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            radius: 12,
            backgroundColor: AppTheme.emerald.withValues(alpha: 0.18),
            child: Text(number, style: theme.textTheme.labelMedium),
          ),
          const SizedBox(width: 10),
          Expanded(child: Text(text, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.invited, required this.rewarded, required this.earned});

  final int invited;
  final int rewarded;
  final String earned;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    Widget cell(String value, String label) => Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(value, style: theme.textTheme.titleLarge),
              Text(
                label,
                style:
                    theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
          ),
        );

    return Row(
      children: [
        cell('$invited', l.customerReferralInvited),
        // «Пришло» и «дошло до пополнения» — разные числа, и второе объясняет, почему денег
        // меньше, чем друзей.
        cell('$rewarded', l.customerReferralRewarded),
        cell(earned, l.customerReferralEarned),
      ],
    );
  }
}
