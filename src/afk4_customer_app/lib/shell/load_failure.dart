import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../theme/space.dart';

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

  /// Текст — обычным цветом, а не красным абзацем: красный здесь кричал о поломке там, где
  /// человеку нужно спокойно прочитать причину. Красный остаётся только у значка сбоя сервера;
  /// пропавшая связь — не авария, и её значок нейтральный.
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: Space.s6, vertical: Space.s8),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              isOffline ? Icons.wifi_off_rounded : Icons.error_outline_rounded,
              color: isOffline ? theme.colorScheme.onSurfaceVariant : theme.colorScheme.error,
              size: 40,
            ),
            const SizedBox(height: Space.s3),
            Text(message, textAlign: TextAlign.center, style: theme.textTheme.bodyLarge),
            const SizedBox(height: Space.s4),
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
