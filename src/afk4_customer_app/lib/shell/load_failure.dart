import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';

/// Экран не загрузился — и у человека есть выход.
///
/// Один вид на всё приложение: причина словами и кнопка «Повторить». Экран, который упирается
/// в голую строку ошибки, — тупик: игрок видит, что сломалось, и не видит, что с этим делать,
/// хотя чаще всего достаточно повторить. Жест «потянуть вниз» этого не заменяет: он невидим,
/// и в упавшем экране под ним обычно нечего тянуть.
class LoadFailure extends StatelessWidget {
  const LoadFailure({super.key, required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(color: theme.colorScheme.error),
            ),
            const SizedBox(height: 12),
            OutlinedButton(
              onPressed: onRetry,
              child: Text(L.of(context).customerCommonRetry),
            ),
          ],
        ),
      ),
    );
  }
}
