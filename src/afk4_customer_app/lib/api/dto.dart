/// Данные главного экрана. Зеркала DTO из `AFK4.Shared.Contracts`: имена полей совпадают с
/// теми, что приходят по сети, чтобы расхождение ловилось на разборе, а не на экране.
library;

class Money {
  const Money({required this.currencyCode, required this.minorUnits});

  final String currencyCode;
  final int minorUnits;

  factory Money.fromJson(Map<String, dynamic> json) => Money(
        currencyCode: json['currencyCode'] as String,
        minorUnits: (json['minorUnits'] as num).toInt(),
      );
}

/// Режим сессии. `fixed` — оплачена наперёд, показывается остаток; `open` — счётчик времени
/// и накопленная стоимость.
enum SessionDurationMode { open, fixed }

class ActiveSession {
  const ActiveSession({
    required this.sessionId,
    required this.seatName,
    required this.startedAtUtc,
    required this.durationMode,
    required this.remainingSeconds,
    required this.accruedCostMinorUnits,
    required this.currencyCode,
  });

  final String sessionId;
  final String seatName;
  final DateTime startedAtUtc;
  final SessionDurationMode durationMode;
  final int? remainingSeconds;
  final int? accruedCostMinorUnits;
  final String currencyCode;

  factory ActiveSession.fromJson(Map<String, dynamic> json) => ActiveSession(
        sessionId: json['sessionId'] as String,
        seatName: json['seatName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        durationMode:
            json['durationMode'] == 'fixed' ? SessionDurationMode.fixed : SessionDurationMode.open,
        remainingSeconds: (json['remainingSeconds'] as num?)?.toInt(),
        accruedCostMinorUnits: (json['accruedCostMinorUnits'] as num?)?.toInt(),
        currencyCode: json['currencyCode'] as String,
      );
}

class PlayerDashboard {
  const PlayerDashboard({
    required this.walletBalance,
    required this.debtBalance,
    required this.activeSession,
  });

  final Money walletBalance;
  final Money debtBalance;
  final ActiveSession? activeSession;

  factory PlayerDashboard.fromJson(Map<String, dynamic> json) => PlayerDashboard(
        walletBalance: Money.fromJson(json['walletBalance'] as Map<String, dynamic>),
        debtBalance: Money.fromJson(json['debtBalance'] as Map<String, dynamic>),
        activeSession: json['activeSession'] == null
            ? null
            : ActiveSession.fromJson(json['activeSession'] as Map<String, dynamic>),
      );
}

/// Заявка на пополнение кошелька: игрок просит зачислить сумму, клуб подтверждает.
class TopUpIntent {
  const TopUpIntent({
    required this.paymentIntentId,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.state,
    required this.isExpired,
  });

  final String paymentIntentId;
  final int amountMinorUnits;
  final String currencyCode;
  final String state;
  final bool isExpired;

  factory TopUpIntent.fromJson(Map<String, dynamic> json) => TopUpIntent(
        paymentIntentId: json['paymentIntentId'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        state: json['state'] as String,
        isExpired: json['isExpired'] as bool? ?? false,
      );
}

/// Страница списка с курсором. Курсор — «продолжить отсюда», а не номер страницы: список
/// растёт с одного конца, и смещение съезжало бы на каждой новой записи.
class CursorPage<T> {
  const CursorPage({required this.items, required this.nextCursor});

  final List<T> items;

  /// null — дальше ничего нет.
  final String? nextCursor;

  factory CursorPage.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) itemFromJson,
  ) =>
      CursorPage(
        items: (json['items'] as List)
            .map((item) => itemFromJson(item as Map<String, dynamic>))
            .toList(),
        nextCursor: json['nextCursor'] as String?,
      );
}

/// Прошедший визит: где сидел, сколько пробыл и на сколько наиграл.
class PlayerVisit {
  const PlayerVisit({
    required this.sessionId,
    required this.seatName,
    required this.startedAtUtc,
    required this.endedAtUtc,
    required this.grandTotalMinorUnits,
    required this.currencyCode,
    required this.hasReceipt,
  });

  final String sessionId;
  final String seatName;
  final DateTime startedAtUtc;

  /// null — визит ещё не закрыт.
  final DateTime? endedAtUtc;
  final int grandTotalMinorUnits;
  final String currencyCode;
  final bool hasReceipt;

  factory PlayerVisit.fromJson(Map<String, dynamic> json) => PlayerVisit(
        sessionId: json['sessionId'] as String,
        seatName: json['seatName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        endedAtUtc: json['endedAtUtc'] == null ? null : DateTime.parse(json['endedAtUtc'] as String),
        grandTotalMinorUnits: (json['grandTotalMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        hasReceipt: json['hasReceipt'] as bool? ?? false,
      );
}

/// Строка покупки: что, сколько и на какую сумму.
class PurchaseLine {
  const PurchaseLine({
    required this.productName,
    required this.quantity,
    required this.lineTotalMinorUnits,
  });

  final String productName;
  final int quantity;
  final int lineTotalMinorUnits;

  factory PurchaseLine.fromJson(Map<String, dynamic> json) => PurchaseLine(
        productName: json['productName'] as String,
        quantity: (json['quantity'] as num).toInt(),
        lineTotalMinorUnits: (json['lineTotalMinorUnits'] as num).toInt(),
      );
}

/// Чек визита: время, покупки и итог.
class VisitReceipt {
  const VisitReceipt({
    required this.receiptNumber,
    required this.createdAtUtc,
    required this.seatName,
    required this.startedAtUtc,
    required this.endedAtUtc,
    required this.timeChargeMinorUnits,
    required this.lines,
    required this.grandTotalMinorUnits,
    required this.currencyCode,
  });

  final String receiptNumber;
  final DateTime createdAtUtc;
  final String seatName;
  final DateTime startedAtUtc;
  final DateTime? endedAtUtc;
  final int timeChargeMinorUnits;
  final List<PurchaseLine> lines;
  final int grandTotalMinorUnits;
  final String currencyCode;

  factory VisitReceipt.fromJson(Map<String, dynamic> json) => VisitReceipt(
        receiptNumber: json['receiptNumber'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        seatName: json['seatName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        endedAtUtc: json['endedAtUtc'] == null ? null : DateTime.parse(json['endedAtUtc'] as String),
        timeChargeMinorUnits: (json['timeChargeMinorUnits'] as num).toInt(),
        lines: (json['posLines'] as List)
            .map((line) => PurchaseLine.fromJson(line as Map<String, dynamic>))
            .toList(),
        grandTotalMinorUnits: (json['grandTotalMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
      );
}

/// Покупка в баре: когда, что и на сколько.
class PlayerPurchase {
  const PlayerPurchase({
    required this.posSaleId,
    required this.createdAtUtc,
    required this.totalMinorUnits,
    required this.currencyCode,
    required this.lines,
  });

  final String posSaleId;
  final DateTime createdAtUtc;
  final int totalMinorUnits;
  final String currencyCode;
  final List<PurchaseLine> lines;

  factory PlayerPurchase.fromJson(Map<String, dynamic> json) => PlayerPurchase(
        posSaleId: json['posSaleId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        totalMinorUnits: (json['totalMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        lines: (json['lines'] as List)
            .map((line) => PurchaseLine.fromJson(line as Map<String, dynamic>))
            .toList(),
      );
}

/// Бронь места: когда, где и в каком состоянии.
class PlayerReservation {
  const PlayerReservation({
    required this.reservationId,
    required this.seatName,
    required this.startsAtUtc,
    required this.endsAtUtc,
    required this.state,
  });

  final String reservationId;

  /// null — клуб ещё не назначил конкретное место.
  final String? seatName;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final String state;

  /// Отменить можно то, что ещё не состоялось. Отменённую или уже отыгранную бронь трогать
  /// нечего — кнопка там только сбивает с толку.
  bool get isCancellable => state == 'pending' || state == 'confirmed';

  factory PlayerReservation.fromJson(Map<String, dynamic> json) => PlayerReservation(
        reservationId: json['reservationId'] as String,
        seatName: json['seatName'] as String?,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        state: json['state'] as String,
      );
}

/// Позиция меню бара: что можно заказать к месту прямо во время сессии.
class ShopProduct {
  const ShopProduct({
    required this.productId,
    required this.name,
    required this.price,
    required this.stockOnHand,
  });

  final String productId;
  final String name;
  final Money price;

  /// Остаток на складе филиала. Сервер уже убрал отсюда то, что кончилось и не продаётся
  /// в минус, поэтому число нужно только чтобы предупредить о последних штуках.
  final int stockOnHand;

  factory ShopProduct.fromJson(Map<String, dynamic> json) => ShopProduct(
        productId: json['productId'] as String,
        name: json['name'] as String,
        price: Money.fromJson(json['price'] as Map<String, dynamic>),
        stockOnHand: (json['stockOnHand'] as num?)?.toInt() ?? 0,
      );
}

/// Строка заказа: что и сколько.
class ShopOrderLine {
  const ShopOrderLine({required this.name, required this.quantity, required this.lineTotal});

  final String name;
  final int quantity;
  final Money lineTotal;

  factory ShopOrderLine.fromJson(Map<String, dynamic> json) => ShopOrderLine(
        name: json['name'] as String,
        quantity: (json['quantity'] as num).toInt(),
        lineTotal: Money.fromJson(json['lineTotal'] as Map<String, dynamic>),
      );
}

/// Заказ к месту и его судьба: оформлен, готовится, принесли, отменён.
class ShopOrder {
  const ShopOrder({
    required this.id,
    required this.status,
    required this.total,
    required this.lines,
  });

  final String id;
  final String status;
  final Money total;
  final List<ShopOrderLine> lines;

  /// Отменить можно, пока заказ не выдан: после «принесли» отменять нечего.
  bool get isCancellable => status == 'placed' || status == 'accepted';

  /// Заказ ещё в работе — за ним есть смысл следить.
  bool get isOpen => status == 'placed' || status == 'accepted';

  factory ShopOrder.fromJson(Map<String, dynamic> json) => ShopOrder(
        id: json['id'] as String,
        status: json['status'] as String,
        total: Money.fromJson(json['total'] as Map<String, dynamic>),
        lines: (json['lines'] as List? ?? const [])
            .map((line) => ShopOrderLine.fromJson(line as Map<String, dynamic>))
            .toList(),
      );
}

/// Профиль игрока: как его зовут, чем он подписан и что он разрешил присылать.
class PlayerProfile {
  const PlayerProfile({
    required this.displayName,
    required this.phoneNumber,
    required this.phoneVerified,
    required this.preferredLocale,
    required this.marketingOptIn,
  });

  final String displayName;
  final String? phoneNumber;
  final bool phoneVerified;

  /// null — игрок не выбирал язык, и письма идут на языке клуба.
  final String? preferredLocale;
  final bool marketingOptIn;

  factory PlayerProfile.fromJson(Map<String, dynamic> json) => PlayerProfile(
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] as String?,
        phoneVerified: json['phoneVerified'] as bool? ?? false,
        preferredLocale: json['preferredLocale'] as String?,
        marketingOptIn: json['marketingOptIn'] as bool? ?? false,
      );
}

/// Ответ на просьбу прислать код: сколько он живёт и когда можно просить следующий.
class PhoneVerificationStarted {
  const PhoneVerificationStarted({required this.expiresInSeconds, required this.resendAfterSeconds});

  final int expiresInSeconds;
  final int resendAfterSeconds;

  factory PhoneVerificationStarted.fromJson(Map<String, dynamic> json) => PhoneVerificationStarted(
        expiresInSeconds: (json['expiresInSeconds'] as num).toInt(),
        resendAfterSeconds: (json['resendAfterSeconds'] as num).toInt(),
      );
}
