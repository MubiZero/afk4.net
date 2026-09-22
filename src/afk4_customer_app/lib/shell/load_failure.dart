import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';

/// Экран не загрузился — и у человека есть выход.
///
/// Один вид на всё приложение: причина словами и кнопка «Повторить». Экран, который упирается
/// в голую строку ошибки, — тупик: игрок видит, что сломалось, и не видит, что с этим делать,
/// хотя чаще всего достаточно повторить. Жест «потянуть вниз» этого не заменяет: он невидим,
/// и в упавшем экране под ним обычно нечего тянуть.
/// Отдельно названа потеря связи: «не удалось загрузить» человек читает как поломку клуба и
/// идёт звонить, а причина — его собственный интернет в подвале. Текст про связь он проверит
/// сам за секунду.
class LoadFailure extends StatelessWidget {
  const LoadFailure({super.key, required this.message, required this.onRetry, this.isOffline = false});

  /// Отказ из-за связи: `PlayerApiException.isOffline` у пойманной ошибки.
  factory LoadFailure.offline({Key? key, required String message, required VoidCallback onRetry}) =>
      LoadFailure(key: key, message: message, onRetry: onRetry, isOffline: true);

  final String message;
  final VoidCallback onRetry;
  final bool isOffline;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              isOffline ? Icons.wifi_off_rounded : Icons.error_outline_rounded,
              color: theme.colorScheme.error,
              size: 32,
            ),
            const SizedBox(height: 8),
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
