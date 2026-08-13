import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../loyalty/loyalty_screen.dart';
import '../money/money.dart';
import '../news/news_section.dart';
import '../play/start_session_screen.dart';
import '../shell/app_scaffold.dart';
import '../shell/pressable.dart';
import '../shell/skeleton.dart';
import '../shop/shop_screen.dart';
import '../theme/app_theme.dart';
import 'extend_session_sheet.dart';
import 'live_session_card.dart';
import 'quick_actions.dart';

/// Главный экран: что происходит с сессией прямо сейчас и сколько денег в кошельке.
///
/// Порядок блоков отвечает на вопросы в том порядке, в каком их задают, открыв приложение в
/// зале: сколько у меня денег, что с моей сессией, что я могу сделать, что нового в клубе.
class DashboardScreen extends StatefulWidget {
  const DashboardScreen({
    super.key,
    required this.api,
    required this.displayName,
    required this.phoneVerified,
    required this.features,
    this.onOpenReservations,
    this.onOpenWallet,
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

  /// Переход в раздел денег: строка баланса — не украшение, по ней приходят за пополнением.
  final VoidCallback? onOpenWallet;

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

  /// Филиал игрока — из профиля. Нужен, чтобы предложить сесть за свободный ПК: и места, и
  /// тарифы у клуба свои. null — профиль ещё не прочитан или филиала у аккаунта нет.
  String? _branchId;

  /// Название клуба игрока — им подписана шапка: сеть бывает из нескольких заведений, и
  /// узнать, в какое ты вошёл, можно было только через профиль.
  String? _branchName;

  /// Последний запрос не дошёл до сервера. Данные на экране остаются, но они с прошлого
  /// удачного ответа — молчать об этом значит показывать баланс, которому нельзя верить.
  bool _stale = false;

  @override
  void initState() {
    super.initState();
    _refresh();
    _loadBranch();
    _poll = Timer.periodic(_refreshEvery, (_) => _refresh());
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _loadBranch() async {
    try {
      final profile = await widget.api.getProfile();
      if (!mounted) return;
      setState(() {
        _branchId = profile.homeBranchId;
        _branchName = profile.homeBranchName;
      });
    } on PlayerApiException {
      // Не узнали филиал — просто не предлагаем сесть самому. Всё остальное на экране работает.
    }
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

  bool get _loyaltyEnabled => widget.features == null || widget.features!.contains('loyalty');

  Future<void> _startSession() async {
    final l = L.of(context);
    final branchId = _branchId;
    if (branchId == null) return;

    final seatName = await Navigator.of(context).push<String>(
      MaterialPageRoute(builder: (_) => StartSessionScreen(api: widget.api, branchId: branchId)),
    );
    if (seatName == null || !mounted) return;

    // Куда садиться — единственное, что игроку сейчас нужно знать: он стоит посреди зала.
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(l.customerPlayStarted(seatName))),
    );
    await _refresh();
  }

  void _openLoyalty() {
    Navigator.of(context).push<void>(
      MaterialPageRoute(builder: (_) => LoyaltyScreen(api: widget.api)),
    );
  }

  Future<void> _openShop() async {
    await Navigator.of(context).push<void>(
      MaterialPageRoute(
        builder: (_) => ShopScreen(
          api: widget.api,
          sessionActive: _data?.activeSession != null,
        ),
      ),
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

  List<QuickAction> _actions(L l, PlayerDashboard data) => [
        if (widget.onOpenReservations != null)
          QuickAction(
            icon: Icons.event_outlined,
            label: l.customerActionsBook,
            onOpen: widget.onOpenReservations!,
          ),
        // Меню открыто всегда: цены смотрят и до игры, а плитка, появляющаяся только при
        // сессии, выглядит как пропавшая. Что заказ несут за ПК, объясняет сам экран.
        if (_shopEnabled)
          QuickAction(
            icon: Icons.local_cafe_outlined,
            label: l.customerActionsOrder,
            onOpen: _openShop,
          ),
        if (_loyaltyEnabled)
          QuickAction(
            icon: Icons.savings_outlined,
            label: l.customerLoyaltyTitle,
            onOpen: _openLoyalty,
          ),
      ];

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final data = _data;

    return AppScaffold(
      // Приветствие над именем: экран открывает свой человек, а не пользователь системы.
      // При прокрутке приветствие уходит первым — из двух строк оно менее важная.
      // Приветствия здесь больше нет: из трёх строк подряд оно единственное ничего не
      // сообщало, а вместе они читались как сжатый в комок заголовок.
      place: _branchName,
      title: widget.displayName,
      onRefresh: _refresh,
      slivers: [
        SliverPadding(
          padding: sectionPadding,
          sliver: SliverList.list(
            children: [
              if (_stale && data != null) ...[
                const _StaleBanner(),
                const SizedBox(height: 12),
              ],
              if (data == null && _failed)
                Text(l.customerDashboardLoadError, style: TextStyle(color: theme.colorScheme.error))
              else if (data == null)
                Semantics(label: l.a11yLoadingDashboard, child: const _DashboardSkeleton())
              else ...[
                _BalanceStrip(
                  balance: data.walletBalance,
                  debt: data.debtBalance,
                  onOpen: widget.onOpenWallet,
                ),
                const SizedBox(height: 12),
                // Идущая сессия — то, ради чего экран открывают посреди игры. Когда её нет,
                // на её месте стоит приглашение сесть: пустое место сообщало бы только об
                // отсутствии.
                if (data.activeSession != null)
                  LiveSessionCard(
                    session: data.activeSession!,
                    fetchedAt: _fetchedAt ?? widget.clock(),
                    onExtend: () => _extend(data.activeSession!),
                    clock: widget.clock,
                  )
                else
                  _StartPlayingCard(
                    // Сесть можно, только когда известен филиал: места у клуба свои.
                    onPlay: _branchId == null ? null : _startSession,
                  ),
                const SizedBox(height: 16),
                QuickActions(actions: _actions(l, data)),
                // Новости внизу: акция клуба важна, но не важнее идущей сессии и денег.
                const SizedBox(height: 24),
                NewsSection(api: widget.api),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

/// Скелет главной: те же блоки, что появятся после ответа, той же высоты.
class _DashboardSkeleton extends StatelessWidget {
  const _DashboardSkeleton();

  @override
  Widget build(BuildContext context) => const Column(
        children: [
          SkeletonBox(height: 72),
          SizedBox(height: 12),
          SkeletonBox(height: 168, radius: 24),
          SizedBox(height: 16),
          Row(
            children: [
              Expanded(child: SkeletonBox(height: 88)),
              SizedBox(width: 12),
              Expanded(child: SkeletonBox(height: 88)),
            ],
          ),
        ],
      );
}

/// Строка баланса. Деньги стоят первыми и ведут в свой раздел: «сколько у меня» — первый
/// вопрос вошедшего, и ответ на него не должен требовать прокрутки.
class _BalanceStrip extends StatelessWidget {
  const _BalanceStrip({required this.balance, required this.debt, required this.onOpen});

  final Money balance;
  final Money debt;
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Pressable(
      onPressed: onOpen,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppTheme.radiusControl),
          border: Border.all(color: theme.colorScheme.outline),
          color: theme.colorScheme.surfaceContainerHighest,
        ),
        child: Row(
          children: [
            Icon(Icons.account_balance_wallet_outlined,
                size: 20, color: theme.colorScheme.primary),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    l.customerDashboardBalance,
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                  Text(
                    formatMoney(balance.minorUnits, balance.currencyCode, locale: locale),
                    style: theme.textTheme.titleLarge,
                  ),
                  // Долг показывается, только когда он есть: строка «Долг: 0» на главной
                  // пугает зря.
                  if (debt.minorUnits > 0)
                    Text(
                      '${l.customerDashboardDebt}: '
                      '${formatMoney(debt.minorUnits, debt.currencyCode, locale: locale)}',
                      style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
                    ),
                ],
              ),
            ),
            if (onOpen != null)
              Icon(Icons.chevron_right, color: theme.colorScheme.onSurfaceVariant),
          ],
        ),
      ),
    );
  }
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

/// Пустое состояние с выходом: раньше здесь была серая надпись и никакого следующего шага.
///
/// Действие здесь ровно одно. Бронь живёт плиткой ниже: два призыва подряд заставляют
/// выбирать вместо того, чтобы делать, а игрок в зале пришёл играть сейчас.
class _StartPlayingCard extends StatelessWidget {
  const _StartPlayingCard({required this.onPlay});

  /// Сесть за свободный ПК прямо сейчас. Это главное действие пустого состояния: игрок,
  /// открывший приложение в клубе, хочет играть, а не бронировать на завтра.
  final VoidCallback? onPlay;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 32, horizontal: 20),
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
              child:
                  Icon(Icons.sports_esports_outlined, color: theme.colorScheme.primary, size: 28),
            ),
            const SizedBox(height: 14),
            Text(
              l.customerDashboardNoSession,
              style:
                  theme.textTheme.titleMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            if (onPlay != null) ...[
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: onPlay,
                  icon: const Icon(Icons.play_arrow_rounded, size: 22),
                  label: Text(l.customerPlayStart),
                ),
              ),
              const SizedBox(height: 6),
              Text(
                l.customerPlayStartHint,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodySmall
                    ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
