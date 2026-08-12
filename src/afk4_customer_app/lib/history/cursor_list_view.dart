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

        // Потянуть вниз обновляет список: жест, выученный на главной, должен работать и
        // здесь. Пустой список тоже тянется — иначе обновить его нечем.
        return RefreshIndicator(
          onRefresh: controller.load,
          child: controller.items.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    Padding(
                      padding: const EdgeInsets.all(32),
                      child: Text(
                        emptyText,
                        textAlign: TextAlign.center,
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                    ),
                  ],
                )
              : _items(context, l),
        );
      },
    );
  }

  /// Следующая страница подтягивается сама у края списка: лишнее касание «показать ещё»
  /// на телефоне ничего не решает. Кнопка остаётся только как способ повторить сорвавшуюся
  /// подгрузку — молча зациклить запрос на ошибке было бы хуже.
  Widget _items(BuildContext context, L l) {
    final controller = this.controller;
    final showFooter = controller.hasMore;

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      physics: const AlwaysScrollableScrollPhysics(),
      itemCount: controller.items.length + (showFooter ? 1 : 0),
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        if (index == controller.items.length) {
          if (controller.moreFailed) {
            return OutlinedButton(
              onPressed: controller.loadMore,
              child: Text(l.customerCommonLoadMoreError),
            );
          }
          if (!controller.loadingMore) {
            // Запрос ставится в очередь после текущего кадра: дёргать его прямо во время
            // построения списка нельзя.
            WidgetsBinding.instance.addPostFrameCallback((_) => controller.loadMore());
          }
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: CircularProgressIndicator(),
            ),
          );
        }
        return itemBuilder(context, controller.items[index]);
      },
    );
  }
}
