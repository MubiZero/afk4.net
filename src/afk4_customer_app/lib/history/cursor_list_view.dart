import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import 'cursor_list.dart';

/// Список с подгрузкой: одинаково ведёт себя у визитов и покупок — загрузка, ошибка с
/// повтором, пустота и кнопка «показать ещё».
class CursorListView<T> extends StatelessWidget {
  const CursorListView({
    super.key,
    required this.controller,
    required this.loadingLabel,
    required this.errorText,
    required this.emptyText,
    required this.itemBuilder,
  });

  final CursorListController<T> controller;

  /// Подпись для экранной читалки, пока идёт загрузка.
  final String loadingLabel;
  final String errorText;
  final String emptyText;
  final Widget Function(BuildContext context, T item) itemBuilder;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return AnimatedBuilder(
      animation: controller,
      builder: (context, _) {
        if (controller.status == CursorListStatus.loading) {
          return Semantics(
            label: loadingLabel,
            child: const Center(child: Padding(
              padding: EdgeInsets.all(32),
              child: CircularProgressIndicator(),
            )),
          );
        }

        if (controller.status == CursorListStatus.failed) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(errorText, style: TextStyle(color: theme.colorScheme.error)),
                  const SizedBox(height: 8),
                  TextButton(onPressed: controller.load, child: Text(l.customerCommonRetry)),
                ],
              ),
            ),
          );
        }

        if (controller.items.isEmpty) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: Text(
                emptyText,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ),
          );
        }

        return ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: controller.items.length + (controller.hasMore ? 1 : 0),
          separatorBuilder: (_, _) => const SizedBox(height: 12),
          itemBuilder: (context, index) {
            if (index == controller.items.length) {
              return OutlinedButton(
                onPressed: controller.loadingMore ? null : controller.loadMore,
                child: Text(controller.loadingMore ? l.customerCommonLoading : l.customerCommonLoadMore),
              );
            }
            return itemBuilder(context, controller.items[index]);
          },
        );
      },
    );
  }
}
