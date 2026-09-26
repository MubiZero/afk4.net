import 'package:flutter/material.dart';

import '../api/contracts.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../shell/app_scaffold.dart';
import '../shell/load_failure.dart';

/// Что клуб присылал этому человеку.
///
/// До этого экрана пуш был единственным способом узнать о событии, и пропущенный пуш —
/// выключенные уведомления, переустановленное приложение, мёртвый токен — прочитать было негде.
/// Поэтому в списке есть и то, что до телефона не доехало: именно в этом и была дыра.
///
/// Прочитанным список помечается при открытии, а не по одному сообщению: его читают целиком.
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key, required this.api, this.onRead});

  final PlayerApiClient api;

  /// Список открыли и прочитали — экрану выше пора убрать значок непрочитанного.
  final VoidCallback? onRead;

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

enum _Load { loading, ready, failed }

class _NotificationsScreenState extends State<NotificationsScreen> {
  _Load _state = _Load.loading;
  bool _offline = false;
  List<PlayerNotificationDto> _items = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _state = _Load.loading);
    try {
      final feed = await widget.api.getNotifications();
      if (!mounted) return;
      setState(() {
        _items = feed.notifications;
        _state = _Load.ready;
      });
      // Отметка ставится после показа, а не вместо него: упавшая отметка не должна прятать
      // список, который уже загрузился.
      try {
        await widget.api.markNotificationsRead();
        widget.onRead?.call();
      } on PlayerApiException {
        // Не отметилось — значит в следующий раз откроется с тем же непрочитанным. Это лучше,
        // чем ошибка поверх прочитанного списка.
      }
    } on PlayerApiException catch (error) {
      if (mounted) {
        setState(() {
          _offline = error.isOffline;
          _state = _Load.failed;
        });
      }
    }
  }

  IconData _icon(String templateKey) => switch (templateKey) {
        'player.balance_topped_up' => Icons.account_balance_wallet_outlined,
        'player.order_ready' => Icons.restaurant_outlined,
        'player.reservation_soon' => Icons.event_outlined,
        'player.session_ending' => Icons.timer_outlined,
        'player.birthday_gift' => Icons.cake_outlined,
        _ => Icons.notifications_outlined,
      };

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Scaffold(
      body: CustomScrollView(
        slivers: [
          appHeader(context, title: l.customerNotificationsTitle),
          if (_state == _Load.loading)
            const SliverFillRemaining(
              hasScrollBody: false,
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_state == _Load.failed)
            SliverFillRemaining(
              hasScrollBody: false,
              child: _offline
                  ? LoadFailure.offline(message: l.customerErrorOffline, onRetry: _load)
                  : LoadFailure(message: l.customerNotificationsError, onRetry: _load),
            )
          else if (_items.isEmpty)
            SliverFillRemaining(
              hasScrollBody: false,
              child: Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Text(
                    l.customerNotificationsEmpty,
                    textAlign: TextAlign.center,
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                ),
              ),
            )
          else
            SliverList.separated(
              itemCount: _items.length,
              separatorBuilder: (_, _) => const Divider(height: 1),
              itemBuilder: (_, index) {
                final item = _items[index];
                return ListTile(
                  leading: Icon(_icon(item.templateKey)),
                  title: Text(
                    item.subject,
                    style: item.isUnread ? const TextStyle(fontWeight: FontWeight.w600) : null,
                  ),
                  subtitle: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(item.body),
                      const SizedBox(height: 4),
                      Text(
                        formatDateTime(l, item.createdAtUtc, locale),
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                );
              },
            ),
        ],
      ),
    );
  }
}
