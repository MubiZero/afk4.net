import 'package:flutter/material.dart';

import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import 'phone_verification_sheet.dart';
import '../shell/app_sheet.dart';
import '../shell/actions.dart';
import '../theme/space.dart';

/// Действие закрыто, пока номер не подтверждён, — и здесь же способ его подтвердить.
///
/// Гейт без кнопки был тупиком: он отправлял к администратору клуба, у которого возможности
/// подтвердить чужой номер тоже нет. Один вид на все такие места: деньги, брони, и всё, что
/// появится дальше.
class VerifyPhoneGate extends StatelessWidget {
  const VerifyPhoneGate({
    super.key,
    required this.api,
    required this.explanation,
    this.onVerified,
  });

  final PlayerApiClient api;

  /// Почему действие закрыто — словами того раздела, в котором гейт стоит.
  final String explanation;

  final VoidCallback? onVerified;

  Future<void> _verify(BuildContext context) async {
    final l = L.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await showAppSheet<bool>(
      context,
      (_) => PhoneVerificationSheet(api: api),
    );
    if (confirmed != true) return;

    messenger.showSnackBar(SnackBar(content: Text(l.customerPhoneDone)));
    onVerified?.call();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          explanation,
          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: Space.s3),
        SecondaryButton(action: AppAction(l.customerWalletGateAction, () => _verify(context))),
      ],
    );
  }
}
