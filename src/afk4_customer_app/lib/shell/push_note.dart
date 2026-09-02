import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';

/// Уведомление, пришедшее, пока игрок в приложении.
///
/// Система в этом случае не показывает ничего сама, а выдёргивать человека с экрана, где он
/// что-то делает, нельзя: полоса появляется над разделами, ждёт несколько секунд и уходит.
/// Переход предлагается, только когда есть куда вести, — кнопка в никуда хуже её отсутствия.
class PushNote extends StatelessWidget {
  const PushNote({super.key, required this.text, this.onOpen, required this.onDismiss});

  final String text;
  final VoidCallback? onOpen;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Material(
      color: theme.colorScheme.secondaryContainer,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 10, 8, 10),
          child: Row(
            children: [
              Icon(Icons.notifications_active_outlined,
                  size: 18, color: theme.colorScheme.onSecondaryContainer),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  text,
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(color: theme.colorScheme.onSecondaryContainer),
                ),
              ),
              if (onOpen != null)
                TextButton(onPressed: onOpen, child: Text(L.of(context).customerPushOpen)),
              IconButton(
                onPressed: onDismiss,
                icon: const Icon(Icons.close, size: 18),
                color: theme.colorScheme.onSecondaryContainer,
                tooltip: MaterialLocalizations.of(context).closeButtonTooltip,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
