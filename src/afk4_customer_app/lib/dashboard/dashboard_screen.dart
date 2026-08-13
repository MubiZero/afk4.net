import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../shop/shop_screen.dart';
import '../wallet/wallet_card.dart';
import 'extend_session_sheet.dart';
import 'live_session_card.dart';

/// Главный экран: что происходит с сессией прямо сейчас и сколько денег в кошельке.
class DashboardScreen extends StatefulWidget {
  const DashboardScreen({
    super.key,
    required this.api,
    required this.displayName,
    required this.phoneVerified,
    required this.features,
    this.onOpenReservations,
    this.onPhoneVerified,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final String displayName;
  final bool phoneVerified;
  /// Возможности клуба; null — список не получен. Загружает их оболочка: он нужен ещё и
  /// разделам, а два независимых запроса одного и того же — лишний трафик и рассинхрон.
  final List<String>? features;

  /// Куда вести из пустого состояния «нет сессии». null — клуб не принимает онлайн-брони,
  /// и звать туда некуда.
  final VoidCallback? onOpenReservations;

  /// Номер подтвердили из карточки кошелька — оболочке пора считать игрока подтверждённым.
  final VoidCallback? onPhoneVerified;

  final DateTime Function() clock;

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  /// Как часто перезапрашиваются деньги и сессия. Чаще незачем: секунды таймера дорисовывает
  /// сама карточка, а баланс так часто не меняется.
  static const Duration _refreshEvery = Duration(seconds: 30);

  Timer? _poll;
  PlayerDashboard? _data;
  DateTime? _fetchedAt;
  bool _failed = false;

  /// Последний запрос не дошёл до сервера. Данные на экране остаются, но они с прошлого
  /// удачного ответа — молчать об этом значит показывать баланс, которому нельзя верить.
  bool _stale = false;

  @override
  void initState() {
    super.initState();
    _refresh();
    _poll = Timer.periodic(_refreshEvery, (_) => _refresh());
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    try {
      final data = await widget.api.getDashboard();
      if (!mounted) return;
      setState(() {
        _data = data;
        _fetchedAt = widget.clock();
        _failed = false;
        _stale = false;
      });
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      // Уже показанные цифры не стираются: пропавшая на секунду сеть не повод заменять
      // баланс сообщением об ошибке. Ошибка видна, только пока показывать нечего — а когда
      // есть что показать, полоска сверху честно говорит, что цифры могли устареть.
      setState(() {
        _failed = _data == null;
        _stale = error.statusCode == null;
      });
    }
  }

  /// null в списке возможностей — «считаем включённым», как и везде в приложении: спрятать
  /// заказ из-за сетевого сбоя значит соврать игроку, что клуб его не принимает.
  bool get _shopEnabled => widget.features == null || widget.features!.contains('player_shop');

  Future<void> _openShop() async {
    await Navigator.of(context).push<void>(
      MaterialPageRoute(builder: (_) => ShopScreen(api: widget.api)),
    );
    // Заказ списывает деньги с кошелька — вернувшись, игрок должен увидеть настоящий баланс.
    if (mounted) await _refresh();
  }

  /// Продление сессии. Успех подтверждается тремя способами сразу: короткая вибрация в
  /// момент действия, сообщение с выбранным временем и перечитанный экран — игрок не должен
  /// гадать, списались деньги или нет.
  Future<void> _extend(ActiveSession session) async {
    final l = L.of(context);
    final minutes = await showModalBottomSheet<int>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => ExtendSessionSheet(api: widget.api, sessionId: session.sessionId),
    );
    if (minutes == null || !mounted) return;

    unawaited(HapticFeedback.lightImpact());
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(l.customerSessionExtendDone(extendDurationLabel(l, minutes))),
    ));
    await _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final data = _data;

    return Scaffold(
      // Приветствие мелким шрифтом над именем: экран открывает свой человек, а не пользователь
      // системы. Имя остаётся заголовком — по нему видно, в чей аккаунт вошли.
      appBar: AppBar(
        toolbarHeight: 76,
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              l.customerDashboardWelcome,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            Text(widget.displayName, style: theme.textTheme.headlineSmall),
          ],
        ),
      ),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (_stale && data != null) ...[
              const _StaleBanner(),
              const SizedBox(height: 12),
            ],
            if (data == null && _failed)
              Text(l.customerDashboardLoadError, style: TextStyle(color: theme.colorScheme.error))
            else if (data == null)
              Semantics(
                label: l.a11yLoadingDashboard,
                child: const Center(child: Padding(
                  padding: EdgeInsets.symmetric(vertical: 32),
                  child: CircularProgressIndicator(),
                )),
              )
            else ...[
              // Идущая сессия — то, ради чего экран открывают посреди игры, поэтому она
              // впереди денег. Когда сессии нет, впереди кошелёк: пустая карточка наверху
              // сообщала бы только об отсутствии.
              if (data.activeSession != null) ...[
                LiveSessionCard(
                  session: data.activeSession!,
                  fetchedAt: _fetchedAt ?? widget.clock(),
                  onExtend: () => _extend(data.activeSession!),
                  clock: widget.clock,
                ),
                // Заказ к месту живёт рядом с сессией и только при ней: меню сервер отдаёт
                // по месту, за которым игрок сидит, а вне сессии заказывать некуда.
                if (_shopEnabled) ...[
                  const SizedBox(height: 12),
                  _ShopEntry(onOpen: _openShop),
                ],
                const SizedBox(height: 12),
                _wallet(data),
              ] else ...[
                _wallet(data),
                const SizedBox(height: 12),
                _NoSessionCard(onBook: widget.onOpenReservations),
              ],
            ],
          ],
        ),
      ),
    );
  }

  Widget _wallet(PlayerDashboard data) => WalletCard(
        api: widget.api,
        walletBalance: data.walletBalance,
        debtBalance: data.debtBalance,
        phoneVerified: widget.phoneVerified,
        features: widget.features,
        onPhoneVerified: widget.onPhoneVerified,
      );
}

/// Полоска «данные могли устареть». Не пугает ошибкой — просто снимает доверие с цифр,
/// пока связь не вернулась.
class _StaleBanner extends StatelessWidget {
  const _StaleBanner();

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        children: [
          Icon(Icons.cloud_off_outlined, size: 18, color: theme.colorScheme.onSurfaceVariant),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              l.customerOfflineStale,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ),
        ],
      ),
    );
  }
}

/// Вход в заказ к месту. Строкой, а не кнопкой: заказ — не главное действие экрана, но и
/// прятать его в разделы нельзя, иначе о нём никто не узнает.
class _ShopEntry extends StatelessWidget {
  const _ShopEntry({required this.onOpen});

  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Material(
      color: theme.colorScheme.surfaceContainerHighest,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onOpen,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Icon(Icons.local_cafe_outlined, color: theme.colorScheme.primary),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(l.customerShopOpen, style: theme.textTheme.titleMedium),
                    Text(
                      l.customerShopOpenHint,
                      style: theme.textTheme.bodySmall
                          ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                    ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right, color: theme.colorScheme.onSurfaceVariant),
            ],
          ),
        ),
      ),
    );
  }
}

/// Пустое состояние с выходом: раньше здесь была серая надпись и никакого следующего шага.
class _NoSessionCard extends StatelessWidget {
  const _NoSessionCard({required this.onBook});

  final VoidCallback? onBook;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 36, horizontal: 16),
        child: Column(
          children: [
            Container(
              width: 56,
              height: 56,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: theme.colorScheme.primary.withValues(alpha: 0.12),
              ),
              child: Icon(Icons.sports_esports_outlined,
                  color: theme.colorScheme.primary, size: 28),
            ),
            const SizedBox(height: 14),
            Text(
              l.customerDashboardNoSession,
              style: theme.textTheme.titleMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            if (onBook != null) ...[
              const SizedBox(height: 12),
              OutlinedButton(onPressed: onBook, child: Text(l.customerDashboardBookSeat)),
            ],
          ],
        ),
      ),
    );
  }
}
