import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../theme/app_theme.dart';

/// Друзья и «кто сейчас в зале».
///
/// Дружба принадлежит человеку, а не клубной карточке: друг остаётся другом в любом клубе сети.
/// Приватность здесь важнее списка — видно только имя и зал, и только после принятой заявки.
class FriendsScreen extends StatefulWidget {
  const FriendsScreen({super.key, required this.api});

  final PlayerApiClient api;

  @override
  State<FriendsScreen> createState() => _FriendsScreenState();
}

class _FriendsScreenState extends State<FriendsScreen> {
  FriendsView? _view;
  bool _failed = false;
  bool _busy = false;
  String? _error;
  final _phone = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _phone.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final view = await widget.api.getFriends();
      if (!mounted) return;
      setState(() {
        _view = view;
        _failed = false;
      });
    } on PlayerApiException {
      if (!mounted) return;
      setState(() => _failed = _view == null);
    }
  }

  /// Общий ход всех действий: список приезжает в ответе, поэтому второй запрос за ним не нужен.
  Future<void> _run(Future<FriendsView> Function() action, {String? toast}) async {
    final l = L.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final view = await action();
      if (!mounted) return;
      unawaited(HapticFeedback.lightImpact());
      setState(() {
        _view = view;
        _busy = false;
      });
      if (toast != null) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(toast)));
      }
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = error.message == 'friend_self' ? l.customerFriendsErrSelf : l.customerFriendsErrGeneric;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = l.customerFriendsErrGeneric;
      });
    }
  }

  Future<void> _invite() async {
    final phone = _phone.text.trim();
    if (phone.isEmpty) return;
    final l = L.of(context);
    await _run(() => widget.api.sendFriendRequest(phone), toast: l.customerFriendsSent);
    if (mounted && _error == null) _phone.clear();
  }

  Future<void> _remove(Friend friend) async {
    final l = L.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l.customerFriendsRemoveTitle(friend.displayName)),
        content: Text(l.customerFriendsRemoveHint),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l.customerFriendsDismiss),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l.customerFriendsRemove),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    await _run(() => widget.api.removeFriend(friend.platformPersonId));
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(l.customerFriendsTitle)),
      body: RefreshIndicator(onRefresh: _load, child: _body(l)),
    );
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final view = _view;

    if (view == null) {
      return _failed
          ? ListView(
              padding: const EdgeInsets.all(24),
              children: [
                Text(l.customerFriendsLoadError, style: TextStyle(color: theme.colorScheme.error)),
              ],
            )
          : const Center(child: CircularProgressIndicator());
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (_error != null) ...[
          Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
          const SizedBox(height: 16),
        ],

        // Пришедшие заявки идут первыми: это единственное, что ждёт ответа человека.
        if (view.incoming.isNotEmpty) ...[
          Text(l.customerFriendsIncoming, style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          for (final request in view.incoming)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: _RequestRow(
                name: request.displayName,
                busy: _busy,
                onAccept: () => _run(() => widget.api.acceptFriendRequest(request.friendRequestId)),
                onDecline: () => _run(() => widget.api.declineFriendRequest(request.friendRequestId)),
              ),
            ),
          const SizedBox(height: 24),
        ],

        Text(l.customerFriendsTitle, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (view.friends.isEmpty)
          Text(
            '${l.customerFriendsNone}\n${l.customerFriendsNoneHint}',
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          )
        else
          for (final friend in view.friends)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: FriendRow(friend: friend, onRemove: _busy ? null : () => _remove(friend)),
            ),

        // Отправленные заявки — внизу и молча: отзывать их незачем, а знать, что ответа ещё
        // нет, человеку стоит.
        if (view.outgoing.isNotEmpty) ...[
          const SizedBox(height: 24),
          Text(l.customerFriendsOutgoing, style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          for (final request in view.outgoing)
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Text(
                request.displayName,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ),
        ],

        const SizedBox(height: 24),
        Text(l.customerFriendsAddTitle, style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        TextField(
          controller: _phone,
          enabled: !_busy,
          keyboardType: TextInputType.phone,
          autofillHints: const [AutofillHints.telephoneNumber],
          decoration: InputDecoration(labelText: l.customerFriendsAddHint),
          onSubmitted: (_) => _invite(),
        ),
        const SizedBox(height: 12),
        SizedBox(
          width: double.infinity,
          height: AppTheme.primaryButtonHeight,
          child: FilledButton(
            onPressed: _busy ? null : _invite,
            child: Text(l.customerFriendsAddCta),
          ),
        ),

        const SizedBox(height: 24),
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          value: view.showsPresence,
          onChanged: _busy ? null : (value) => _run(() => widget.api.setPresenceVisible(value)),
          title: Text(l.customerFriendsVisibility),
          subtitle: view.showsPresence
              ? null
              // Объясняем только выключенное состояние: включённое и так видно по списку.
              : Text(l.customerFriendsVisibilityOffHint),
        ),
      ],
    );
  }
}

/// Строка друга: имя и где он сейчас. Отдельный виджет — ради тестов.
class FriendRow extends StatelessWidget {
  const FriendRow({super.key, required this.friend, this.onRemove});

  final Friend friend;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final presence = friend.presence;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(friend.displayName, style: theme.textTheme.titleSmall),
                const SizedBox(height: 2),
                Text(
                  presence == null
                      ? l.customerFriendsNotInHall
                      : l.customerFriendsInHall(presence.organizationName, presence.branchName),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: presence == null
                        ? theme.colorScheme.onSurfaceVariant
                        : theme.colorScheme.primary,
                  ),
                ),
              ],
            ),
          ),
          if (onRemove != null)
            IconButton(
              onPressed: onRemove,
              tooltip: l.customerFriendsRemove,
              icon: const Icon(Icons.person_remove_outlined),
            ),
        ],
      ),
    );
  }
}

class _RequestRow extends StatelessWidget {
  const _RequestRow({
    required this.name,
    required this.onAccept,
    required this.onDecline,
    this.busy = false,
  });

  final String name;
  final VoidCallback onAccept;
  final VoidCallback onDecline;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(AppTheme.radiusCard),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      child: Row(
        children: [
          Expanded(child: Text(name, style: theme.textTheme.titleSmall)),
          TextButton(onPressed: busy ? null : onDecline, child: Text(l.customerFriendsDecline)),
          FilledButton(onPressed: busy ? null : onAccept, child: Text(l.customerFriendsAccept)),
        ],
      ),
    );
  }
}
