import 'package:flutter/material.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../theme/app_theme.dart';

/// Новости и акции клуба на главной.
///
/// Загружаются один раз при открытии экрана, а не по таймеру: клуб пишет их раз в неделю,
/// и опрашивать сервер каждые полминуты ради них незачем.
///
/// Пустого состояния у блока нет намеренно: когда новостей нет, он исчезает целиком. Пустая
/// рамка с надписью «новостей нет» занимала бы главный экран рассказом об отсутствии.
class NewsSection extends StatefulWidget {
  const NewsSection({super.key, required this.api, this.maxItems = 3});

  final PlayerApiClient api;

  /// Сколько новостей показывать на главной. Лента здесь не нужна: главный экран — про
  /// сессию и деньги, а не про чтение.
  final int maxItems;

  @override
  State<NewsSection> createState() => _NewsSectionState();
}

class _NewsSectionState extends State<NewsSection> {
  List<NewsItem> _items = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final items = await widget.api.getNews();
      if (mounted) setState(() => _items = items);
    } on PlayerApiException {
      // Новости — не действие: без них экран полностью рабочий, и ошибку показывать незачем.
    }
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    if (_items.isEmpty) return const SizedBox.shrink();

    final shown = _items.take(widget.maxItems).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l.customerNewsTitle, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        for (final item in shown)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: _NewsCard(item: item),
          ),
      ],
    );
  }
}

class _NewsCard extends StatelessWidget {
  const _NewsCard({required this.item});

  final NewsItem item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Container(
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(AppTheme.radiusControl),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (item.imageUrl != null)
            // Картинка не должна ломать экран, если ссылка мертва или сеть отвалилась:
            // тогда карточка живёт одним текстом.
            Image.network(
              item.imageUrl!,
              height: 140,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => const SizedBox.shrink(),
            ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.title, style: theme.textTheme.titleMedium),
                if (item.body.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(item.body, style: theme.textTheme.bodyMedium),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
