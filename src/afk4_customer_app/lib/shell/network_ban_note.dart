import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';

/// «Платформа закрыла вход в сеть» — состояние, в котором человек всё видит, но ничего не
/// начинает.
///
/// Полоса стоит над всеми разделами, а не в одном: запрет действует везде, и объяснять его
/// на экране брони, промолчав на экране кошелька, значило бы оставить человека гадать, что
/// сломалось. Причина показывается его словами: запрет, о котором нельзя узнать, за что, —
/// это повод идти спорить к стойке, которая его не ставила.
class NetworkBanNote extends StatelessWidget {
  const NetworkBanNote({super.key, this.reason});

  final String? reason;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final reason = this.reason?.trim();

    return Material(
      color: theme.colorScheme.errorContainer,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(Icons.block, size: 18, color: theme.colorScheme.onErrorContainer),
              const SizedBox(width: 8),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      l.customerBanTitle,
                      style: theme.textTheme.titleSmall
                          ?.copyWith(color: theme.colorScheme.onErrorContainer),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      l.customerBanHint,
                      style: theme.textTheme.bodySmall
                          ?.copyWith(color: theme.colorScheme.onErrorContainer),
                    ),
                    if (reason != null && reason.isNotEmpty) ...[
                      const SizedBox(height: 4),
                      Text(
                        l.customerBanReason(reason),
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: theme.colorScheme.onErrorContainer),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
