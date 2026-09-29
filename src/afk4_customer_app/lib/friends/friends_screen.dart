import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/contracts.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../theme/app_theme.dart';
import '../shell/load_failure.dart';
import '../shell/group_header.dart';
import '../shell/skeleton.dart';
import '../shell/actions.dart';
import '../shell/empty_state.dart';
import '../theme/space.dart';
import '../shell/app_scaffold.dart';

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
  FriendsDto? _view;
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
  Future<void> _run(Future<FriendsDto> Function() action, {String? toast}) async {
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
        _error = switch (error.message) {
          _ when error.isOffline => l.customerErrorOffline,
          'friend_self' => l.customerFriendsErrSelf,
          // Заявку отозвали или на неё уже ответили с другого экрана — повтор ничего не изменит.
          'friend_request_unknown' => l.customerFriendsErrRequestGone,
          _ => l.customerFriendsErrGeneric,
        };
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

  Future<void> _remove(FriendDto friend) async {
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
      appBar: nestedAppBar(context, title: l.customerFriendsTitle),
      body: RefreshIndicator(onRefresh: _load, child: _body(l)),
    );
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final view = _view;

    if (view == null) {
      return _failed
          ? LoadFailure(message: l.customerFriendsLoadError, onRetry: _load)
          : ListSkeleton(label: l.customerCommonLoading);
    }

    return ListView(
      padding: const EdgeInsets.all(Space.s4),
      children: [
        if (_error != null) ...[
          Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
          const SizedBox(height: Space.s4),
        ],

        // Пришедшие заявки идут первыми: это единственное, что ждёт ответа человека.
        if (view.incoming.isNotEmpty) ...[
          GroupHeader(l.customerFriendsIncoming),
          for (final request in view.incoming)
            Padding(
              padding: const EdgeInsets.only(bottom: Space.s2),
              child: _RequestRow(
                name: request.displayName,
                busy: _busy,
                onAccept: () => _run(() => widget.api.acceptFriendRequest(request.friendRequestId)),
                onDecline: () => _run(() => widget.api.declineFriendRequest(request.friendRequestId)),
              ),
            ),
          const SizedBox(height: Space.s6),
        ],

        GroupHeader(l.customerFriendsTitle),
        if (view.friends.isEmpty)
          EmptyState(
            icon: Icons.people_outline,
            title: l.customerFriendsNone,
            hint: l.customerFriendsNoneHint,
          )
        else
          for (final friend in view.friends)
            Padding(
              padding: const EdgeInsets.only(bottom: Space.s2),
              child: FriendRow(friend: friend, onRemove: _busy ? null : () => _remove(friend)),
            ),

        // Отправленные заявки — внизу и молча: отзывать их незачем, а знать, что ответа ещё
        // нет, человеку стоит.
        if (view.outgoing.isNotEmpty) ...[
          const SizedBox(height: Space.s6),
          GroupHeader(l.customerFriendsOutgoing),
          for (final request in view.outgoing)
            Padding(
              padding: const EdgeInsets.only(bottom: Space.s1),
              child: Text(
                request.displayName,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ),
        ],

        const SizedBox(height: Space.s6),
        GroupHeader(l.customerFriendsAddTitle),
        TextField(
          controller: _phone,
          enabled: !_busy,
          keyboardType: TextInputType.phone,
          autofillHints: const [AutofillHints.telephoneNumber],
          decoration: InputDecoration(labelText: l.customerFriendsAddHint),
          onSubmitted: (_) => _invite(),
        ),
        const SizedBox(height: Space.s3),
        PrimaryButton(action: AppAction(l.customerFriendsAddCta, _busy ? null : _invite)),

        const SizedBox(height: Space.s6),
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

  final FriendDto friend;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final presence = friend.presence;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: Space.s4, vertical: Space.s3),
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
                const SizedBox(height: Space.s1),
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
      padding: const EdgeInsets.symmetric(horizontal: Space.s4, vertical: Space.s2),
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
