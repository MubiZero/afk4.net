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

/// Тариф клуба: по чём и с какими правилами считается время.
///
/// Цену по этим полям приложение НЕ считает — за этим есть расчёт на сервере: минимальное
/// оплачиваемое время и шаг округления живут в биллинге, и вторая арифметика здесь разошлась
/// бы с настоящим списанием.
class TariffOption {
  const TariffOption({
    required this.tariffVersionId,
    required this.name,
    required this.pricePerMinuteMinorUnits,
    required this.currencyCode,
  });

  final String tariffVersionId;
  final String name;
  final int pricePerMinuteMinorUnits;
  final String currencyCode;

  factory TariffOption.fromJson(Map<String, dynamic> json) => TariffOption(
        tariffVersionId: json['tariffVersionId'] as String,
        name: json['name'] as String,
        pricePerMinuteMinorUnits: (json['pricePerMinuteMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
      );
}

/// Во сколько обойдётся бронь по выбранному тарифу.
class ReservationQuote {
  const ReservationQuote({
    required this.requestedMinutes,
    required this.billableMinutes,
    required this.amountMinorUnits,
    required this.currencyCode,
  });

  final int requestedMinutes;

  /// Сколько минут оплачивается. Больше заказанного — когда у тарифа минимум или округление;
  /// ради этого расчёт и живёт на сервере.
  final int billableMinutes;
  final int amountMinorUnits;
  final String currencyCode;

  /// Тариф берёт больше, чем игрок забронировал, — об этом надо сказать до подтверждения.
  bool get hasMinimum => billableMinutes > requestedMinutes;

  factory ReservationQuote.fromJson(Map<String, dynamic> json) => ReservationQuote(
        requestedMinutes: (json['requestedMinutes'] as num).toInt(),
        billableMinutes: (json['billableMinutes'] as num).toInt(),
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
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
    this.tariffName,
    this.estimatedCostMinorUnits,
    this.currencyCode,
  });

  final String reservationId;

  /// null — клуб ещё не назначил конкретное место.
  final String? seatName;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final String state;

  /// Название выбранного тарифа и стоимость, посчитанная сервером при брони. null — бронь
  /// завели на стойке, там же её и посчитают.
  final String? tariffName;
  final int? estimatedCostMinorUnits;
  final String? currencyCode;

  /// Отменить можно то, что ещё не состоялось. Отменённую или уже отыгранную бронь трогать
  /// нечего — кнопка там только сбивает с толку.
  bool get isCancellable => state == 'pending' || state == 'confirmed';

  factory PlayerReservation.fromJson(Map<String, dynamic> json) => PlayerReservation(
        reservationId: json['reservationId'] as String,
        seatName: json['seatName'] as String?,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        state: json['state'] as String,
        tariffName: json['tariffName'] as String?,
        estimatedCostMinorUnits: (json['estimatedCostMinorUnits'] as num?)?.toInt(),
        currencyCode: json['currencyCode'] as String?,
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

/// Начисление кешбэка: сколько, когда и за что.
class CashbackEntry {
  const CashbackEntry({required this.amount, required this.reason, required this.createdAtUtc});

  final Money amount;

  /// Служебная причина вида `cashback:topup` или `cashback:shop:{id}`. Разбирается на экране
  /// в человеческую подпись: показывать игроку внутреннее имя события незачем.
  final String reason;
  final DateTime createdAtUtc;

  /// Источник начисления: `topup`, `shop`, `session` или null, если причина незнакомая.
  String? get source {
    final parts = reason.split(':');
    return parts.length >= 2 && parts[0] == 'cashback' ? parts[1] : null;
  }

  factory CashbackEntry.fromJson(Map<String, dynamic> json) => CashbackEntry(
        amount: Money(
          currencyCode: json['currencyCode'] as String,
          minorUnits: (json['amountMinorUnits'] as num).toInt(),
        ),
        reason: json['reason'] as String? ?? '',
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );
}

/// Кешбэк игрока: сколько накоплено и по каким правилам начисляется.
///
/// Кешбэк — не баллы: он приходит на кошелёк обычными деньгами, и тратится так же.
class PlayerLoyalty {
  const PlayerLoyalty({
    required this.topUpEnabled,
    required this.topUpPercentBasisPoints,
    required this.shopEnabled,
    required this.shopPercentBasisPoints,
    required this.sessionEnabled,
    required this.sessionPercentBasisPoints,
    required this.totalEarned,
    required this.recent,
  });

  final bool topUpEnabled;
  final int topUpPercentBasisPoints;
  final bool shopEnabled;
  final int shopPercentBasisPoints;
  final bool sessionEnabled;
  final int sessionPercentBasisPoints;
  final Money totalEarned;
  final List<CashbackEntry> recent;

  /// Клуб не начисляет кешбэк ни за что — рассказывать о нём нечего.
  bool get isOff =>
      !(topUpEnabled && topUpPercentBasisPoints > 0) &&
      !(shopEnabled && shopPercentBasisPoints > 0) &&
      !(sessionEnabled && sessionPercentBasisPoints > 0);

  factory PlayerLoyalty.fromJson(Map<String, dynamic> json) => PlayerLoyalty(
        topUpEnabled: json['topUpEnabled'] as bool? ?? false,
        topUpPercentBasisPoints: (json['topUpPercentBasisPoints'] as num?)?.toInt() ?? 0,
        shopEnabled: json['shopEnabled'] as bool? ?? false,
        shopPercentBasisPoints: (json['shopPercentBasisPoints'] as num?)?.toInt() ?? 0,
        sessionEnabled: json['sessionEnabled'] as bool? ?? false,
        sessionPercentBasisPoints: (json['sessionPercentBasisPoints'] as num?)?.toInt() ?? 0,
        totalEarned: Money.fromJson(json['totalEarned'] as Map<String, dynamic>),
        recent: (json['recent'] as List? ?? const [])
            .map((entry) => CashbackEntry.fromJson(entry as Map<String, dynamic>))
            .toList(),
      );
}

/// Новость или акция клуба.
class NewsItem {
  const NewsItem({
    required this.id,
    required this.title,
    required this.body,
    required this.imageUrl,
    required this.publishedAtUtc,
  });

  final String id;
  final String title;
  final String body;
  final String? imageUrl;
  final DateTime publishedAtUtc;

  factory NewsItem.fromJson(Map<String, dynamic> json) => NewsItem(
        id: json['id'] as String,
        title: json['title'] as String,
        body: json['body'] as String? ?? '',
        imageUrl: json['imageUrl'] as String?,
        // Дата публикации может быть не проставлена — тогда новость живёт с момента создания.
        publishedAtUtc: DateTime.parse(
            (json['publishAtUtc'] ?? json['createdAtUtc']) as String),
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
    this.homeBranchId,
    this.homeBranchName,
  });

  final String displayName;
  final String? phoneNumber;
  final bool phoneVerified;

  /// null — игрок не выбирал язык, и письма идут на языке клуба.
  final String? preferredLocale;
  final bool marketingOptIn;

  /// Филиал, к которому привязан аккаунт. По нему приложение спрашивает прайс: до этого сервер
  /// знал филиал только про себя, и спросить тарифы было не по чему.
  final String? homeBranchId;
  final String? homeBranchName;

  factory PlayerProfile.fromJson(Map<String, dynamic> json) => PlayerProfile(
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] as String?,
        phoneVerified: json['phoneVerified'] as bool? ?? false,
        preferredLocale: json['preferredLocale'] as String?,
        marketingOptIn: json['marketingOptIn'] as bool? ?? false,
        homeBranchId: json['homeBranchId'] as String?,
        homeBranchName: json['homeBranchName'] as String?,
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
