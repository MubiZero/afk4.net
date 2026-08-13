import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/dto.dart';
import '../api/idempotency.dart';
import '../api/player_api_client.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../theme/app_theme.dart';

/// Заказ еды и напитков за игровое место.
///
/// Открывается только при идущей сессии: сервер и меню-то отдаёт по месту, за которым игрок
/// сидит. Экран живёт отдельным маршрутом, а не вкладкой, ровно поэтому — вкладка, которая
/// половину времени пуста, выглядит сломанной.
class ShopScreen extends StatefulWidget {
  const ShopScreen({super.key, required this.api});

  final PlayerApiClient api;

  @override
  State<ShopScreen> createState() => _ShopScreenState();
}

class _ShopScreenState extends State<ShopScreen> {
  /// Как часто перечитывается судьба заказа. Оператор принимает и несёт заказ за минуты,
  /// и игрок смотрит на этот экран, пока ждёт.
  static const Duration _pollEvery = Duration(seconds: 5);

  List<ShopProduct>? _catalog;
  bool _loadFailed = false;

  /// Сколько чего в корзине. Товары без строки здесь просто не заказаны.
  final Map<String, int> _cart = {};

  ShopOrder? _order;
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
    final catalog = _catalog ?? const <ShopProduct>[];
    var total = 0;
    for (final product in catalog) {
      total += product.price.minorUnits * (_cart[product.productId] ?? 0);
    }
    return total;
  }

  String get _currencyCode =>
      _catalog?.firstOrNull?.price.currencyCode ?? _order?.total.currencyCode ?? 'TJS';

  void _changeQuantity(ShopProduct product, int delta) {
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
        idempotencyKey: newIdempotencyKey(),
      );
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
          'insufficient_funds' => l.customerShopErrFunds,
          'out_of_stock' => l.customerShopErrStock,
          'product_unavailable' => l.customerShopErrUnavailable,
          'placement_context_invalid' => l.customerShopErrNoSession,
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
    } on PlayerApiException {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l.customerShopCancelError)),
      );
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
      appBar: AppBar(title: Text(l.customerShopTitle)),
      body: _body(l),
      bottomNavigationBar: _order == null && (_catalog?.isNotEmpty ?? false) ? _checkout(l) : null,
    );
  }

  Widget _body(L l) {
    final theme = Theme.of(context);
    final order = _order;
    if (order != null) {
      return SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: _OrderCard(order: order, onCancel: _cancel, onBackToMenu: _backToMenu),
      );
    }

    final catalog = _catalog;
    if (catalog == null) {
      return _loadFailed
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text(l.customerShopLoadError,
                    style: TextStyle(color: theme.colorScheme.error)),
              ),
            )
          : const Center(child: CircularProgressIndicator());
    }

    if (catalog.isEmpty) return _EmptyMenu();

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: catalog.length,
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (_, index) {
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
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final total = _cartTotalMinorUnits;
    final empty = total == 0;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_error != null) ...[
              Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
              const SizedBox(height: 8),
            ],
            FilledButton(
              // Кнопка не выключается пустой корзиной молча: она говорит, чего не хватает.
              // Выключенная кнопка без объяснения — самый частый тупик в заказе.
              onPressed: _placing || empty ? null : _place,
              child: Text(
                _placing
                    ? l.customerShopPlacing
                    : empty
                        ? l.customerShopCartEmpty
                        : l.customerShopPlace(formatMoney(total, _currencyCode, locale: locale)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProductTile extends StatelessWidget {
  const _ProductTile({required this.product, required this.quantity, required this.onChange});

  final ShopProduct product;
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
      padding: const EdgeInsets.fromLTRB(16, 8, 8, 8),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(product.name, style: theme.textTheme.titleMedium),
                const SizedBox(height: 2),
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
          if (quantity == 0)
            IconButton.filledTonal(
              onPressed: () => onChange(1),
              icon: const Icon(Icons.add),
              tooltip: product.name,
            )
          else ...[
            IconButton(onPressed: () => onChange(-1), icon: const Icon(Icons.remove)),
            Text('$quantity', style: theme.textTheme.titleMedium),
            IconButton(onPressed: () => onChange(1), icon: const Icon(Icons.add)),
          ],
        ],
      ),
    );
  }
}

class _EmptyMenu extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.local_cafe_outlined, size: 40, color: theme.colorScheme.onSurfaceVariant),
            const SizedBox(height: 12),
            Text(l.customerShopEmpty, style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(
              l.customerShopEmptyHint,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}

/// Оформленный заказ: что несут, на сколько и где он сейчас.
class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order, required this.onCancel, required this.onBackToMenu});

  final ShopOrder order;
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
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(l.customerShopOrderTitle, style: theme.textTheme.titleMedium),
              const SizedBox(height: 6),
              Text(
                _status(l),
                style: theme.textTheme.titleLarge?.copyWith(color: theme.colorScheme.primary),
              ),
              const SizedBox(height: 16),
              for (final line in order.lines)
                Padding(
                  padding: const EdgeInsets.only(bottom: 6),
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
        const SizedBox(height: 16),
        if (order.isCancellable)
          OutlinedButton(onPressed: onCancel, child: Text(l.customerShopCancel))
        else
          FilledButton(onPressed: onBackToMenu, child: Text(l.customerShopNewOrder)),
      ],
    );
  }
}
