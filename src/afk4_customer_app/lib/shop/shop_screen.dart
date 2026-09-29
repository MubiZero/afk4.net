import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/contracts.dart';
import '../api/dto_rules.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';
import '../shell/load_failure.dart';
import '../shell/actions.dart';
import '../shell/empty_state.dart';
import '../shell/group_header.dart';
import '../shell/quantity_stepper.dart';
import '../shell/skeleton.dart';
import '../theme/space.dart';
import '../shell/status_badge.dart';
import '../shell/app_scaffold.dart';

/// Заказ еды и напитков за игровое место.
///
/// Открывается только при идущей сессии: сервер и меню-то отдаёт по месту, за которым игрок
/// сидит. Экран живёт отдельным маршрутом, а не вкладкой, ровно поэтому — вкладка, которая
/// половину времени пуста, выглядит сломанной.
class ShopScreen extends StatefulWidget {
  const ShopScreen({
    super.key,
    required this.api,
    this.sessionActive = true,
    this.place,
    this.placeLogoUrl,
  });

  final PlayerApiClient api;

  /// Клуб, в котором игрок сейчас, — строкой над заголовком, как у разделов.
  final String? place;
  final String? placeLogoUrl;

  /// Идёт ли сессия. Меню открыто всегда — цены смотрят и до игры, — но заказ несут за
  /// конкретный ПК, и без сессии нести его некуда. Об этом говорит кнопка, а не отказ
  /// сервера после оформления.
  final bool sessionActive;

  @override
  State<ShopScreen> createState() => _ShopScreenState();
}

class _ShopScreenState extends State<ShopScreen> {
  /// Как часто перечитывается судьба заказа. Оператор принимает и несёт заказ за минуты,
  /// и игрок смотрит на этот экран, пока ждёт.
  static const Duration _pollEvery = Duration(seconds: 5);

  List<ShopCatalogItemDto>? _catalog;
  bool _loadFailed = false;

  /// Сколько чего в корзине. Товары без строки здесь просто не заказаны.
  final Map<String, int> _cart = {};

  /// Ключ попытки: повтор после обрыва обязан прийти с тем же, иначе бар получит два заказа
  /// и спишет деньги дважды. Изменённая корзина — другая попытка со своим ключом.
  final AttemptKey _attempt = AttemptKey();

  /// Слепок корзины, по которому попытка узнаётся: состав и количества, в устойчивом порядке.
  String get _cartSignature {
    final lines = _cart.entries.map((item) => '${item.key}:${item.value}').toList()..sort();
    return lines.join(',');
  }

  ShopOrderDto? _order;

  /// Закрытые заказы — принесённые и отменённые, новые сверху.
  List<ShopOrderDto> _pastOrders = const [];
  Timer? _poll;
  bool _placing = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final catalog = await widget.api.getShopCatalog();
      // Заказ мог быть оформлен раньше — с этого же телефона или из лаунчера за ПК. Показать
      // меню поверх незакрытого заказа значит потерять его из виду.
      final orders = await widget.api.getShopOrders();
      if (!mounted) return;
      setState(() {
        _catalog = catalog;
        _loadFailed = false;
        _order = orders.where((order) => order.isOpen).firstOrNull;
        // Закрытые заказы больше не выбрасываются. Сервер и раньше отдавал их все, а приложение
        // оставляло один незакрытый — и на вопрос «принесли мне вчерашний заказ или отменили?»
        // ответить было негде: в покупках видна только продажа, без судьбы самого заказа.
        _pastOrders = orders.where((order) => !order.isOpen).toList()
          ..sort((a, b) => b.placedAtUtc.compareTo(a.placedAtUtc));
      });
      if (_order != null) _startPolling();
    } on PlayerApiException {
      if (mounted) setState(() => _loadFailed = _catalog == null);
    }
  }

  void _startPolling() {
    _poll?.cancel();
    _poll = Timer.periodic(_pollEvery, (_) => _refreshOrder());
  }

  Future<void> _refreshOrder() async {
    final current = _order;
    if (current == null) return;
    try {
      final orders = await widget.api.getShopOrders();
      if (!mounted) return;
      final mine = orders.where((order) => order.id == current.id).firstOrNull;
      if (mine == null) return;
      setState(() => _order = mine);
      if (!mine.isOpen) _poll?.cancel();
    } on PlayerApiException {
      // Статус — справка. Пропавшая на секунду сеть не повод заменять заказ ошибкой.
    }
  }

  int get _cartTotalMinorUnits {
    final catalog = _catalog ?? const <ShopCatalogItemDto>[];
    var total = 0;
    for (final product in catalog) {
      total += product.price.minorUnits * (_cart[product.productId] ?? 0);
    }
    return total;
  }

  String get _currencyCode =>
      _catalog?.firstOrNull?.price.currencyCode ?? _order?.total.currencyCode ?? 'TJS';

  void _changeQuantity(ShopCatalogItemDto product, int delta) {
    final next = (_cart[product.productId] ?? 0) + delta;
    setState(() {
      if (next <= 0) {
        _cart.remove(product.productId);
      } else {
        _cart[product.productId] = next;
      }
      _error = null;
    });
    unawaited(HapticFeedback.selectionClick());
  }

  Future<void> _place() async {
    final l = L.of(context);
    setState(() {
      _placing = true;
      _error = null;
    });

    try {
      final order = await widget.api.placeShopOrder(
        quantitiesByProductId: Map.of(_cart),
        idempotencyKey: _attempt.forSubject(_cartSignature),
      );
      _attempt.done();
      if (!mounted) return;
      unawaited(HapticFeedback.lightImpact());
      setState(() {
        _order = order;
        _cart.clear();
        _placing = false;
      });
      _startPolling();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _placing = false;
        _error = switch (error.message) {
          _ when error.isOffline => l.customerErrorOffline,
          'insufficient_funds' => l.customerShopErrFunds,
          'out_of_stock' => l.customerShopErrStock,
          'product_unavailable' => l.customerShopErrUnavailable,
          // Оба кода про одно: сессии нет, нести заказ некуда.
          'placement_context_invalid' || 'no_active_session' => l.customerShopErrNoSession,
          'FeatureDisabled' => l.customerErrorFeatureOff,
          _ => l.customerShopErrGeneric,
        };
      });
    } catch (_) {
      // См. лист продления: любой другой сбой тоже обязан вернуть кнопку в рабочее состояние,
      // иначе «Оформляем…» висит вечно и выглядит как зависшее списание.
      if (!mounted) return;
      setState(() {
        _placing = false;
        _error = l.customerShopErrGeneric;
      });
    }
  }

  Future<void> _cancel() async {
    final l = L.of(context);
    final current = _order;
    if (current == null) return;
    try {
      final cancelled = await widget.api.cancelShopOrder(current.id);
      if (!mounted) return;
      setState(() => _order = cancelled);
      _poll?.cancel();
    } on PlayerApiException catch (error) {
      if (!mounted) return;
      // Заказ уже приняли на кухне — это не сбой отмены, а её невозможность: «не удалось
      // отменить, попробуйте ещё раз» звало бы жать кнопку, которая не сработает никогда.
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text(switch (error.message) {
          'invalid_transition' => l.customerShopCancelErrStarted,
          _ => l.customerShopCancelError,
        }),
      ));
      // Состояние заказа на экране устарело — потому отмена и не прошла.
      await _refreshOrder();
    }
  }

  /// Возврат к меню после закрытого заказа: экран не должен упираться в «принесли» без
  /// следующего шага.
  void _backToMenu() {
    setState(() => _order = null);
    _load();
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);

    return Scaffold(
      appBar: nestedAppBar(context, title: l.customerActionsOrder, place: widget.place, placeLogoUrl: widget.placeLogoUrl),
      body: _body(l),
      bottomNavigationBar: _order == null && (_catalog?.isNotEmpty ?? false) ? _checkout(l) : null,
    );
  }

  Widget _pastOrders_(L l) {
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: Space.s6),
        GroupHeader(l.customerShopPastTitle),
        for (final order in _pastOrders)
          Padding(
            padding: const EdgeInsets.only(bottom: Space.s2),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(formatMoney(order.total.minorUnits, order.total.currencyCode, locale: locale)),
                      Text(
                        formatDateTime(l, order.placedAtUtc, locale),
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
                StatusBadge(
                  label: _pastStatus(l, order.status),
                  tone: order.status == 'delivered' ? StatusTone.positive : StatusTone.neutral,
                ),
              ],
            ),
          ),
      ],
    );
  }

  String _pastStatus(L l, String status) => switch (status) {
        'delivered' => l.customerShopStatusDelivered,
        _ => l.customerShopStatusCancelled,
      };

  Widget _body(L l) {
    final order = _order;
    if (order != null) {
      return SingleChildScrollView(
        padding: const EdgeInsets.all(Space.s4),
        child: _OrderCard(order: order, onCancel: _cancel, onBackToMenu: _backToMenu),
      );
    }

    final catalog = _catalog;
    if (catalog == null) {
      return _loadFailed
          ? LoadFailure(message: l.customerShopLoadError, onRetry: _load)
          : ListSkeleton(label: l.customerCommonLoading);
    }

    if (catalog.isEmpty) {
      return EmptyState(
        icon: Icons.local_cafe_outlined,
        title: l.customerShopEmpty,
        hint: l.customerShopEmptyHint,
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(Space.s4),
      // Последняя строка — прошлые заказы, когда они есть: список меню и история живут в одном
      // прокручиваемом полотне, чтобы за меню не пряталась вторая прокрутка.
      itemCount: catalog.length + (_pastOrders.isEmpty ? 0 : 1),
      separatorBuilder: (_, _) => const SizedBox(height: Space.s2),
      itemBuilder: (_, index) {
        if (index == catalog.length) return _pastOrders_(l);
        final product = catalog[index];
        return _ProductTile(
          product: product,
          quantity: _cart[product.productId] ?? 0,
          onChange: (delta) => _changeQuantity(product, delta),
        );
      },
    );
  }

  Widget _checkout(L l) {
    final locale = Localizations.localeOf(context).languageCode;
    final total = _cartTotalMinorUnits;
    final empty = total == 0;

    return PinnedActions(
      child: ActionStack(
        error: _error,
        // Кнопка не выключается молча: над ней сказано, чего не хватает — сессии или товаров в
        // корзине. Раньше это было написано в самой выключенной кнопке, серым по серому.
        hint: _placing
            ? null
            : !widget.sessionActive
                ? l.customerShopErrNoSession
                : empty
                    ? l.customerShopCartEmpty
                    : null,
        primary: AppAction(
          _placing
              ? l.customerShopPlacing
              : empty
                  ? l.customerShopPlaceAction
                  : l.customerShopPlace(formatMoney(total, _currencyCode, locale: locale)),
          _placing || empty || !widget.sessionActive ? null : _place,
        ),
      ),
    );
  }
}

class _ProductTile extends StatelessWidget {
  const _ProductTile({required this.product, required this.quantity, required this.onChange});

  final ShopCatalogItemDto product;
  final int quantity;
  final ValueChanged<int> onChange;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    // Предупреждение об остатке — только когда его правда мало. «Осталось 40 штук» ничего
    // не решает, а «осталась 1» меняет заказ.
    final scarce = product.stockOnHand > 0 && product.stockOnHand <= 3;

    return Container(
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(AppTheme.radiusControl),
        border: Border.all(color: theme.colorScheme.outline),
      ),
      padding: const EdgeInsets.fromLTRB(Space.s4, Space.s2, Space.s2, Space.s2),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(product.name, style: theme.textTheme.titleMedium),
                const SizedBox(height: Space.s1),
                Text(
                  formatMoney(product.price.minorUnits, product.price.currencyCode, locale: locale),
                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.primary),
                ),
                if (scarce)
                  Text(
                    l.customerShopLastItems(product.stockOnHand),
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
              ],
            ),
          ),
          QuantityStepper(
            value: quantity,
            collapsed: true,
            onChanged: (next) => onChange(next - quantity),
            decreaseLabel: l.customerShopRemoveOne(product.name),
            increaseLabel: l.customerShopAddOne(product.name),
          ),
        ],
      ),
    );
  }
}

/// Оформленный заказ: что несут, на сколько и где он сейчас.
class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order, required this.onCancel, required this.onBackToMenu});

  final ShopOrderDto order;
  final VoidCallback onCancel;
  final VoidCallback onBackToMenu;

  String _status(L l) => switch (order.status) {
        'placed' => l.customerShopStatusPlaced,
        'accepted' => l.customerShopStatusAccepted,
        'delivered' => l.customerShopStatusDelivered,
        _ => l.customerShopStatusCancelled,
      };

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          decoration: BoxDecoration(
            color: theme.colorScheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(AppTheme.radiusCard),
            border: Border.all(color: theme.colorScheme.outline),
          ),
          padding: const EdgeInsets.all(Space.s5),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(l.customerShopOrderTitle, style: theme.textTheme.titleMedium),
              const SizedBox(height: Space.s2),
              Text(
                _status(l),
                style: theme.textTheme.titleLarge?.copyWith(color: theme.colorScheme.primary),
              ),
              const SizedBox(height: Space.s4),
              for (final line in order.lines)
                Padding(
                  padding: const EdgeInsets.only(bottom: Space.s2),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(child: Text('${line.name} × ${line.quantity}')),
                      Text(formatMoney(line.lineTotal.minorUnits, line.lineTotal.currencyCode,
                          locale: locale)),
                    ],
                  ),
                ),
              const Divider(height: 24),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(l.customerShopTotal, style: theme.textTheme.titleMedium),
                  Text(
                    formatMoney(order.total.minorUnits, order.total.currencyCode, locale: locale),
                    style: theme.textTheme.titleMedium,
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: Space.s4),
        // Отмена разрушает заказ, но не насовсем — можно заказать заново: красная обводка, не
        // заливка.
        if (order.isCancellable)
          SecondaryButton(action: AppAction(l.customerShopCancel, onCancel, danger: true))
        else
          PrimaryButton(action: AppAction(l.customerShopNewOrder, onBackToMenu)),
      ],
    );
  }
}
