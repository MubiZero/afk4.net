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
    this.tariffName,
    this.pricePerHourMinorUnits,
    this.zoneName,
  });

  final String sessionId;
  final String seatName;
  final DateTime startedAtUtc;
  final SessionDurationMode durationMode;
  final int? remainingSeconds;
  final int? accruedCostMinorUnits;
  final String currencyCode;

  /// По какой цене идёт счёт и где человек сидит. Пусто у сессии без тарифа — гостевой,
  /// заведённой на стойке руками.
  final String? tariffName;
  final int? pricePerHourMinorUnits;
  final String? zoneName;

  factory ActiveSession.fromJson(Map<String, dynamic> json) => ActiveSession(
        sessionId: json['sessionId'] as String,
        seatName: json['seatName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        durationMode:
            json['durationMode'] == 'fixed' ? SessionDurationMode.fixed : SessionDurationMode.open,
        remainingSeconds: (json['remainingSeconds'] as num?)?.toInt(),
        accruedCostMinorUnits: (json['accruedCostMinorUnits'] as num?)?.toInt(),
        currencyCode: json['currencyCode'] as String,
        tariffName: json['tariffName'] as String?,
        pricePerHourMinorUnits: (json['pricePerHourMinorUnits'] as num?)?.toInt(),
        zoneName: json['zoneName'] as String?,
      );
}

class PlayerDashboard {
  const PlayerDashboard({
    required this.walletBalance,
    required this.heldBalance,
    required this.debtBalance,
    required this.activeSession,
  });

  final Money walletBalance;

  /// Придержанное под брони. Из остатка оно уже вычтено — это ответ на вопрос «куда делись
  /// мои деньги», а не вторая копилка.
  final Money heldBalance;
  final Money debtBalance;
  final ActiveSession? activeSession;

  factory PlayerDashboard.fromJson(Map<String, dynamic> json) => PlayerDashboard(
        walletBalance: Money.fromJson(json['walletBalance'] as Map<String, dynamic>),
        heldBalance: Money.fromJson(json['heldBalance'] as Map<String, dynamic>),
        debtBalance: Money.fromJson(json['debtBalance'] as Map<String, dynamic>),
        activeSession: json['activeSession'] == null
            ? null
            : ActiveSession.fromJson(json['activeSession'] as Map<String, dynamic>),
      );
}

/// Человек и его клубы одним ответом: «кто я» и «где у меня что».
///
/// Общей суммы денег здесь нет и не будет — у каждого клуба своя касса, и сложенный остаток
/// был бы числом, которое нельзя потратить ни в одном из них.
class Me {
  const Me({required this.person, required this.clubs});

  final MePerson person;
  final List<MyClub> clubs;

  /// Счёт в названном клубе. null — человек в этом клубе ещё ничего не делал, и счёта там
  /// пока нет. Это нормальное состояние, а не сбой.
  MyClub? clubAt(String? organizationId) {
    if (organizationId == null) return null;
    for (final club in clubs) {
      if (club.organizationId == organizationId) return club;
    }
    return null;
  }

  factory Me.fromJson(Map<String, dynamic> json) => Me(
        person: MePerson.fromJson(json['person'] as Map<String, dynamic>),
        clubs: (json['clubs'] as List<dynamic>? ?? const [])
            .map((entry) => MyClub.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
      );
}

/// Личность: то, что принадлежит человеку, а не клубу. PIN сюда не приходит — только
/// признак, задан он или ещё нет.
class MePerson {
  const MePerson({
    required this.platformPersonId,
    required this.phoneNumber,
    required this.displayName,
    required this.preferredLocale,
    required this.phoneVerified,
    required this.pinSet,
    required this.networkBanned,
    required this.networkBanReason,
  });

  final String platformPersonId;
  final String phoneNumber;
  final String displayName;
  final String? preferredLocale;
  final bool phoneVerified;
  final bool pinSet;
  final bool networkBanned;

  /// За что закрыт вход. Запрет, о котором человек не может узнать причину, читается как
  /// поломка приложения — и он идёт спорить к стойке, которая его не ставила.
  final String? networkBanReason;

  factory MePerson.fromJson(Map<String, dynamic> json) => MePerson(
        platformPersonId: json['platformPersonId'] as String,
        phoneNumber: json['phoneNumber'] as String,
        displayName: json['displayName'] as String,
        preferredLocale: json['preferredLocale'] as String?,
        phoneVerified: json['phoneVerified'] as bool? ?? false,
        pinSet: json['pinSet'] as bool? ?? false,
        networkBanned: json['networkBanned'] as bool? ?? false,
        networkBanReason: json['networkBanReason'] as String?,
      );
}

/// Один клуб глазами игрока: сколько можно потратить, сколько придержано под брони, сколько
/// он должен и сколько раз приходил.
class MyClub {
  const MyClub({
    required this.organizationId,
    required this.organizationName,
    required this.playerAccountId,
    required this.homeBranchId,
    required this.walletBalance,
    required this.heldBalance,
    required this.debtBalance,
    required this.visitCount,
  });

  final String organizationId;
  final String organizationName;
  final String playerAccountId;
  final String homeBranchId;
  final Money walletBalance;
  final Money heldBalance;
  final Money debtBalance;
  final int visitCount;

  factory MyClub.fromJson(Map<String, dynamic> json) {
    final currency = json['currencyCode'] as String? ?? 'TJS';
    Money money(String field) =>
        Money(currencyCode: currency, minorUnits: (json[field] as num?)?.toInt() ?? 0);

    return MyClub(
      organizationId: json['organizationId'] as String,
      organizationName: json['organizationName'] as String? ?? '',
      playerAccountId: json['playerAccountId'] as String,
      homeBranchId: json['homeBranchId'] as String,
      walletBalance: money('walletBalanceMinorUnits'),
      heldBalance: money('heldMinorUnits'),
      debtBalance: money('debtMinorUnits'),
      visitCount: (json['visitCount'] as num?)?.toInt() ?? 0,
    );
  }
}

/// Правила брони этого филиала — для этого игрока. Всё посчитано сервером под конкретного
/// человека: предоплата нужна именно ему, потолок броней именно его.
class PlayerBookingRules {
  const PlayerBookingRules({
    required this.branchId,
    required this.acceptanceMode,
    required this.respondWithinMinutes,
    required this.prepaymentRequired,
    required this.activeReservations,
    required this.maxActiveReservations,
    required this.holdSeatAfterStartMinutes,
  });

  final String branchId;

  /// `auto` — клуб подтверждает сам, `manual` — заявку смотрит администратор, `off` — брони
  /// из приложения не принимаются.
  final String acceptanceMode;
  final int respondWithinMinutes;
  final bool prepaymentRequired;
  final int activeReservations;

  /// null — потолка нет: игрок в этом филиале уже свой.
  final int? maxActiveReservations;
  final int holdSeatAfterStartMinutes;

  bool get bookingOff => acceptanceMode == 'off';
  bool get reviewedByStaff => acceptanceMode == 'manual';

  factory PlayerBookingRules.fromJson(Map<String, dynamic> json) => PlayerBookingRules(
        branchId: json['branchId'] as String,
        acceptanceMode: json['acceptanceMode'] as String,
        respondWithinMinutes: (json['respondWithinMinutes'] as num?)?.toInt() ?? 0,
        prepaymentRequired: json['prepaymentRequired'] as bool? ?? false,
        activeReservations: (json['activeReservations'] as num?)?.toInt() ?? 0,
        maxActiveReservations: (json['maxActiveReservations'] as num?)?.toInt(),
        holdSeatAfterStartMinutes: (json['holdSeatAfterStartMinutes'] as num?)?.toInt() ?? 0,
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
    this.method = 'counter',
    this.deepLink,
    this.payUrl,
  });

  final String paymentIntentId;
  final int amountMinorUnits;
  final String currencyCode;
  final String state;
  final bool isExpired;

  /// `counter` — деньги вносят на стойке, `eskhata` — платят из приложения банка.
  final String method;

  /// Ссылка, открывающая приложение банка. Null, если платят на стойке или банк её не дал.
  final String? deepLink;

  /// Страница оплаты в браузере — запасной путь для телефона без приложения банка.
  final String? payUrl;

  factory TopUpIntent.fromJson(Map<String, dynamic> json) => TopUpIntent(
        paymentIntentId: json['paymentIntentId'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        state: json['state'] as String,
        isExpired: json['isExpired'] as bool? ?? false,
        method: json['method'] as String? ?? 'counter',
        deepLink: json['deepLink'] as String?,
        payUrl: json['payUrl'] as String?,
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
/// Движение по кошельку: что случилось с деньгами и когда. Тип приходит кодом — приложение
/// называет его словами из общего каталога, того же, что читает стойка.
class PlayerLedgerEntry {
  const PlayerLedgerEntry({
    required this.ledgerEntryId,
    required this.entryType,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.quantitySeconds,
    required this.createdAtUtc,
  });

  final String ledgerEntryId;
  final String entryType;
  final int amountMinorUnits;
  final String currencyCode;

  /// Сколько времени принесла или забрала запись: у пакетов и бонусных часов деньги — не вся
  /// правда. Ноль у обычных денежных строк.
  final int quantitySeconds;
  final DateTime createdAtUtc;

  factory PlayerLedgerEntry.fromJson(Map<String, dynamic> json) => PlayerLedgerEntry(
        ledgerEntryId: json['ledgerEntryId'] as String,
        entryType: json['entryType'] as String,
        amountMinorUnits: ((json['amount'] as Map<String, dynamic>)['minorUnits'] as num).toInt(),
        currencyCode: (json['amount'] as Map<String, dynamic>)['currencyCode'] as String,
        quantitySeconds: (json['quantitySeconds'] as num?)?.toInt() ?? 0,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );
}

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
    this.appliesNow = true,
    this.appliesOnDaysMask = 0,
    this.appliesFromMinuteOfDay,
    this.appliesToMinuteOfDay,
  });

  final String tariffVersionId;
  final String name;
  final int pricePerMinuteMinorUnits;
  final String currencyCode;

  /// Биты дней недели с понедельника (1) по воскресенье (64); 0 — каждый день.
  /// Действует ли тариф прямо сейчас — по часам клуба, а не телефона. Важно там, где играть
  /// начинают сию секунду; для брони на завтра ответ никакого значения не имеет.
  final bool appliesNow;

  final int appliesOnDaysMask;

  /// Окно местного времени клуба, минуты от полуночи. Оба null — круглосуточно, начало больше
  /// конца — переход через полночь.
  ///
  /// Действует ли тариф на выбранное время, решает сервер: у него есть часовой пояс филиала, а
  /// у телефона — свой собственный, и в поездке он другой. Здесь часы только показываются.
  final int? appliesFromMinuteOfDay;
  final int? appliesToMinuteOfDay;

  factory TariffOption.fromJson(Map<String, dynamic> json) => TariffOption(
        tariffVersionId: json['tariffVersionId'] as String,
        name: json['name'] as String,
        pricePerMinuteMinorUnits: (json['pricePerMinuteMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        appliesNow: json['appliesNow'] as bool? ?? true,
        appliesOnDaysMask: (json['appliesOnDaysMask'] as num?)?.toInt() ?? 0,
        appliesFromMinuteOfDay: (json['appliesFromMinuteOfDay'] as num?)?.toInt(),
        appliesToMinuteOfDay: (json['appliesToMinuteOfDay'] as num?)?.toInt(),
      );
}

/// Пакет часов в прайсе клуба: предоплата, за которую час выходит дешевле поминутного тарифа.
class PackageOption {
  const PackageOption({
    required this.packageDefinitionId,
    required this.name,
    required this.priceMinorUnits,
    required this.currencyCode,
    required this.includedSeconds,
    required this.bonusSeconds,
    required this.expiresAfterDays,
  });

  final String packageDefinitionId;
  final String name;
  final int priceMinorUnits;
  final String currencyCode;
  final int includedSeconds;
  final int bonusSeconds;
  final int expiresAfterDays;

  /// Всё время пакета вместе с бонусным: игрок покупает часы, а не две отдельные величины.
  int get totalSeconds => includedSeconds + bonusSeconds;

  factory PackageOption.fromJson(Map<String, dynamic> json) => PackageOption(
        packageDefinitionId: json['packageDefinitionId'] as String,
        name: json['name'] as String,
        priceMinorUnits: (json['priceMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        includedSeconds: (json['includedSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num?)?.toInt() ?? 0,
        expiresAfterDays: (json['expiresAfterDays'] as num?)?.toInt() ?? 0,
      );
}

/// Друг: имя и то, где он сейчас играет. Ни телефона, ни денег — дружба не даёт доступа
/// к чужому счёту.
class Friend {
  const Friend({
    required this.platformPersonId,
    required this.displayName,
    this.presence,
  });

  final String platformPersonId;
  final String displayName;

  /// Где он сейчас за ПК. null — не в зале либо скрыл присутствие; разницы снаружи нет
  /// намеренно, иначе «скрыт» читалось бы как «он там, но прячется».
  final FriendPresence? presence;

  factory Friend.fromJson(Map<String, dynamic> json) => Friend(
        platformPersonId: json['platformPersonId'] as String,
        displayName: json['displayName'] as String? ?? '',
        presence: json['presence'] == null
            ? null
            : FriendPresence.fromJson(json['presence'] as Map<String, dynamic>),
      );
}

/// Клуб и зал, в которых друг сейчас играет.
class FriendPresence {
  const FriendPresence({required this.organizationName, required this.branchName});

  final String organizationName;
  final String branchName;

  factory FriendPresence.fromJson(Map<String, dynamic> json) => FriendPresence(
        organizationName: json['organizationName'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
      );
}

/// Заявка в друзья — пришедшая или отправленная.
class FriendRequest {
  const FriendRequest({
    required this.friendRequestId,
    required this.platformPersonId,
    required this.displayName,
  });

  final String friendRequestId;
  final String platformPersonId;
  final String displayName;

  factory FriendRequest.fromJson(Map<String, dynamic> json) => FriendRequest(
        friendRequestId: json['friendRequestId'] as String,
        platformPersonId: json['platformPersonId'] as String,
        displayName: json['displayName'] as String? ?? '',
      );
}

/// Друзья целиком: принятые, пришедшие заявки, отправленные и видно ли меня самого.
class FriendsView {
  const FriendsView({
    this.friends = const [],
    this.incoming = const [],
    this.outgoing = const [],
    this.showsPresence = true,
  });

  final List<Friend> friends;
  final List<FriendRequest> incoming;
  final List<FriendRequest> outgoing;
  final bool showsPresence;

  factory FriendsView.fromJson(Map<String, dynamic> json) => FriendsView(
        friends: (json['friends'] as List<dynamic>? ?? const [])
            .map((entry) => Friend.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
        incoming: (json['incoming'] as List<dynamic>? ?? const [])
            .map((entry) => FriendRequest.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
        outgoing: (json['outgoing'] as List<dynamic>? ?? const [])
            .map((entry) => FriendRequest.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
        showsPresence: json['showsPresence'] as bool? ?? true,
      );
}

/// Событие клуба: турнир, ночь игры, чемпионат зала. Зеркало `PlayerTournamentDto`.
class ClubEvent {
  const ClubEvent({
    required this.tournamentId,
    required this.branchId,
    required this.branchName,
    required this.title,
    required this.description,
    required this.discipline,
    required this.startsAtUtc,
    required this.entryFeeMinorUnits,
    required this.currencyCode,
    required this.capacity,
    required this.registeredCount,
    required this.isRegistered,
    required this.state,
    required this.cancelReason,
  });

  final String tournamentId;
  final String branchId;
  final String branchName;
  final String title;
  final String description;

  /// Игра словами клуба («Dota 2», «FIFA»). Пусто — клуб не уточнил.
  final String discipline;
  final DateTime startsAtUtc;

  /// Взнос за участие. 0 — бесплатно, и это обычный случай для вечера, которым клуб просто
  /// заполняет будний день.
  final int entryFeeMinorUnits;
  final String currencyCode;

  /// Сколько человек берут. 0 — без ограничения.
  final int capacity;
  final int registeredCount;
  final bool isRegistered;
  final String state;

  /// Почему клуб отменил. Пусто, пока событие в силе.
  final String cancelReason;

  bool get isCancelled => state == 'cancelled';

  bool get isFree => entryFeeMinorUnits == 0;

  /// Сколько мест осталось. null — потолка нет, и «осталось N» было бы выдумкой.
  int? get freeSpots => capacity == 0 ? null : (capacity - registeredCount).clamp(0, capacity);

  factory ClubEvent.fromJson(Map<String, dynamic> json) => ClubEvent(
        tournamentId: json['tournamentId'] as String,
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String? ?? '',
        title: json['title'] as String? ?? '',
        description: json['description'] as String? ?? '',
        discipline: json['discipline'] as String? ?? '',
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String).toUtc(),
        entryFeeMinorUnits: ((json['entryFee'] as Map<String, dynamic>?)?['minorUnits'] as num?)?.toInt() ?? 0,
        currencyCode: (json['entryFee'] as Map<String, dynamic>?)?['currencyCode'] as String? ?? 'TJS',
        capacity: (json['capacity'] as num?)?.toInt() ?? 0,
        registeredCount: (json['registeredCount'] as num?)?.toInt() ?? 0,
        isRegistered: json['isRegistered'] as bool? ?? false,
        state: json['state'] as String? ?? '',
        cancelReason: json['cancelReason'] as String? ?? '',
      );
}

/// Купленный пакет с остатком времени.
class PlayerPackage {
  const PlayerPackage({
    required this.playerPackageId,
    required this.name,
    required this.purchasedPrice,
    required this.includedSeconds,
    required this.bonusSeconds,
    required this.remainingIncludedSeconds,
    required this.remainingBonusSeconds,
    required this.purchasedAtUtc,
    required this.expiresAtUtc,
  });

  final String playerPackageId;
  final String name;
  final Money purchasedPrice;
  final int includedSeconds;
  final int bonusSeconds;
  final int remainingIncludedSeconds;
  final int remainingBonusSeconds;
  final DateTime purchasedAtUtc;
  final DateTime? expiresAtUtc;

  int get remainingSeconds => remainingIncludedSeconds + remainingBonusSeconds;

  bool get isSpent => remainingSeconds <= 0;

  bool isExpired(DateTime now) => expiresAtUtc != null && !expiresAtUtc!.isAfter(now);

  /// Пакетом ещё можно играть: время осталось и срок не вышел.
  bool isUsable(DateTime now) => !isSpent && !isExpired(now);

  factory PlayerPackage.fromJson(Map<String, dynamic> json) => PlayerPackage(
        playerPackageId: json['playerPackageId'] as String,
        name: json['name'] as String,
        purchasedPrice: Money.fromJson(json['purchasedPrice'] as Map<String, dynamic>),
        includedSeconds: (json['includedSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num?)?.toInt() ?? 0,
        remainingIncludedSeconds: (json['remainingIncludedSeconds'] as num?)?.toInt() ?? 0,
        remainingBonusSeconds: (json['remainingBonusSeconds'] as num?)?.toInt() ?? 0,
        purchasedAtUtc: DateTime.parse(json['purchasedAtUtc'] as String).toLocal(),
        expiresAtUtc: json['expiresAtUtc'] == null
            ? null
            : DateTime.parse(json['expiresAtUtc'] as String).toLocal(),
      );
}

/// Место в зале глазами игрока, который выбирает, куда сесть.
class PlayerSeat {
  const PlayerSeat({
    required this.seatId,
    required this.seatName,
    required this.zoneName,
    required this.isAvailable,
    required this.unavailableReason,
  });

  final String seatId;
  final String seatName;
  final String zoneName;
  final bool isAvailable;

  /// `session`, `reservation` или `offline`. null — место свободно.
  final String? unavailableReason;

  factory PlayerSeat.fromJson(Map<String, dynamic> json) => PlayerSeat(
        seatId: json['seatId'] as String,
        seatName: json['seatName'] as String,
        zoneName: json['zoneName'] as String? ?? '',
        isAvailable: json['isAvailable'] as bool? ?? false,
        unavailableReason: json['unavailableReason'] as String?,
      );
}

/// Во сколько обойдётся бронь по выбранному тарифу.
class ReservationQuote {
  const ReservationQuote({
    required this.requestedMinutes,
    required this.billableMinutes,
    required this.amountMinorUnits,
    required this.currencyCode,
    this.seatCount = 1,
  });

  final int requestedMinutes;

  /// Сколько минут оплачивается. Больше заказанного — когда у тарифа минимум или округление;
  /// ради этого расчёт и живёт на сервере.
  final int billableMinutes;
  /// Сумма за ВСЮ бронь, включая все её места: столько и заморозится.
  final int amountMinorUnits;
  final String currencyCode;

  /// Сколько мест посчитано.
  final int seatCount;

  /// Тариф берёт больше, чем игрок забронировал, — об этом надо сказать до подтверждения.
  bool get hasMinimum => billableMinutes > requestedMinutes;

  factory ReservationQuote.fromJson(Map<String, dynamic> json) => ReservationQuote(
        requestedMinutes: (json['requestedMinutes'] as num).toInt(),
        billableMinutes: (json['billableMinutes'] as num).toInt(),
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        seatCount: (json['seatCount'] as num?)?.toInt() ?? 1,
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
    this.reservationGroupId,
    this.respondByUtc,
    this.rejectReasonCode,
    this.rejectReasonNote,
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

  /// Бронь на компанию: у всех мест группы он общий. null — обычная бронь на одного.
  final String? reservationGroupId;

  /// Докуда клуб обещал ответить на заявку. null — отвечать больше не на что: бронь уже
  /// подтверждена, отменена или отыграна.
  final DateTime? respondByUtc;

  /// Почему клуб отказал: код из общего справочника и слова администратора. Оба пусты у всех
  /// броней, кроме отказанных.
  final String? rejectReasonCode;
  final String? rejectReasonNote;

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
        reservationGroupId: json['reservationGroupId'] as String?,
        respondByUtc: json['respondByUtc'] == null
            ? null
            : DateTime.parse(json['respondByUtc'] as String),
        rejectReasonCode: json['rejectReasonCode'] as String?,
        rejectReasonNote: json['rejectReasonNote'] as String?,
      );
}

/// Забронированная компания: её брони и общая замороженная сумма.
class PlayerReservationGroup {
  const PlayerReservationGroup({
    required this.reservationGroupId,
    required this.reservations,
    required this.totalEstimatedCostMinorUnits,
    required this.currencyCode,
  });

  final String reservationGroupId;
  final List<PlayerReservation> reservations;

  /// Сумма по всей компании. null — бронь без тарифа, её посчитают на стойке.
  final int? totalEstimatedCostMinorUnits;
  final String? currencyCode;

  factory PlayerReservationGroup.fromJson(Map<String, dynamic> json) => PlayerReservationGroup(
        reservationGroupId: json['reservationGroupId'] as String,
        reservations: (json['reservations'] as List<dynamic>)
            .map((item) => PlayerReservation.fromJson(item as Map<String, dynamic>))
            .toList(),
        totalEstimatedCostMinorUnits:
            (json['totalEstimatedCostMinorUnits'] as num?)?.toInt(),
        currencyCode: json['currencyCode'] as String?,
      );
}

/// Экран «Приведи друга»: свой код, условия клуба и что уже вышло.
class PlayerReferral {
  const PlayerReferral({
    required this.enabled,
    required this.code,
    required this.referrerBonusMinorUnits,
    required this.inviteeBonusMinorUnits,
    required this.minimumTopUpMinorUnits,
    required this.currencyCode,
    required this.invitedCount,
    required this.rewardedCount,
    required this.earnedMinorUnits,
    required this.hasClaimedCode,
    required this.canClaimCode,
  });

  /// Клуб платит за приглашения. false — экран честно говорит, что программы нет.
  final bool enabled;
  final String? code;
  final int referrerBonusMinorUnits;
  final int inviteeBonusMinorUnits;
  final int minimumTopUpMinorUnits;
  final String currencyCode;
  final int invitedCount;
  final int rewardedCount;
  final int earnedMinorUnits;

  /// Игрок сам пришёл по чужому коду.
  final bool hasClaimedCode;

  /// Назвать чужой код ещё можно: приглашение не использовано и окно не закрылось.
  final bool canClaimCode;

  factory PlayerReferral.fromJson(Map<String, dynamic> json) => PlayerReferral(
        enabled: json['enabled'] as bool? ?? false,
        code: json['code'] as String?,
        referrerBonusMinorUnits: (json['referrerBonusMinorUnits'] as num?)?.toInt() ?? 0,
        inviteeBonusMinorUnits: (json['inviteeBonusMinorUnits'] as num?)?.toInt() ?? 0,
        minimumTopUpMinorUnits: (json['minimumTopUpMinorUnits'] as num?)?.toInt() ?? 0,
        currencyCode: json['currencyCode'] as String? ?? 'TJS',
        invitedCount: (json['invitedCount'] as num?)?.toInt() ?? 0,
        rewardedCount: (json['rewardedCount'] as num?)?.toInt() ?? 0,
        earnedMinorUnits: (json['earnedMinorUnits'] as num?)?.toInt() ?? 0,
        hasClaimedCode: json['hasClaimedCode'] as bool? ?? false,
        canClaimCode: json['canClaimCode'] as bool? ?? false,
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

/// Закрытый визит, о котором ещё не спрашивали. Приложение предлагает его оценить —
/// один раз и только пока вечер свежий в памяти.
class PendingReview {
  const PendingReview({
    required this.sessionId,
    required this.branchName,
    required this.seatName,
    required this.endedAtUtc,
  });

  final String sessionId;
  final String branchName;
  final String seatName;
  final DateTime endedAtUtc;

  factory PendingReview.fromJson(Map<String, dynamic> json) => PendingReview(
        sessionId: json['sessionId'] as String,
        branchName: json['branchName'] as String? ?? '',
        seatName: json['seatName'] as String? ?? '',
        endedAtUtc: DateTime.parse(json['endedAtUtc'] as String).toUtc(),
      );
}

/// Отзыв игрока о клубе, каким его читают до входа.
class ClubReview {
  const ClubReview({
    required this.reviewId,
    required this.authorName,
    required this.rating,
    required this.createdAtUtc,
    this.comment,
  });

  final String reviewId;
  final String authorName;
  final int rating;
  final String? comment;
  final DateTime createdAtUtc;

  factory ClubReview.fromJson(Map<String, dynamic> json) => ClubReview(
        reviewId: json['reviewId'] as String,
        authorName: json['authorName'] as String? ?? '',
        rating: (json['rating'] as num).toInt(),
        comment: json['comment'] as String?,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String).toUtc(),
      );
}

/// Страница отзывов клуба: средняя оценка — то, что читают первым, отзывы — почему она такая.
class ClubReviews {
  const ClubReviews({this.rating, this.reviewCount = 0, this.items = const []});

  /// null — оценок пока нет. Это не ноль звёзд.
  final double? rating;
  final int reviewCount;
  final List<ClubReview> items;

  factory ClubReviews.fromJson(Map<String, dynamic> json) => ClubReviews(
        rating: (json['rating'] as num?)?.toDouble(),
        reviewCount: (json['reviewCount'] as num?)?.toInt() ?? 0,
        items: (json['items'] as List<dynamic>? ?? const [])
            .map((entry) => ClubReview.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
      );
}

/// Стаж игрока: уровень, часы за ПК и достижения. Названий достижений сервер не присылает —
/// только коды: подписи живут здесь, где у них есть три языка.
class PlayerAchievements {
  const PlayerAchievements({
    required this.level,
    required this.visitCount,
    required this.playedMinutes,
    this.minutesToNextLevel,
    this.achievements = const [],
  });

  final int level;
  final int visitCount;
  final int playedMinutes;

  /// null — уровень последний, дальше расти некуда.
  final int? minutesToNextLevel;
  final List<PlayerAchievement> achievements;

  factory PlayerAchievements.fromJson(Map<String, dynamic> json) => PlayerAchievements(
        level: (json['level'] as num).toInt(),
        visitCount: (json['visitCount'] as num?)?.toInt() ?? 0,
        playedMinutes: (json['playedMinutes'] as num?)?.toInt() ?? 0,
        minutesToNextLevel: (json['minutesToNextLevel'] as num?)?.toInt(),
        achievements: (json['achievements'] as List<dynamic>? ?? const [])
            .map((entry) => PlayerAchievement.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
      );
}

class PlayerAchievement {
  const PlayerAchievement({
    required this.code,
    required this.progress,
    required this.target,
    this.unlockedAtUtc,
  });

  final String code;
  final int progress;
  final int target;
  final DateTime? unlockedAtUtc;

  bool get unlocked => unlockedAtUtc != null;

  factory PlayerAchievement.fromJson(Map<String, dynamic> json) => PlayerAchievement(
        code: json['code'] as String,
        progress: (json['progress'] as num?)?.toInt() ?? 0,
        target: (json['target'] as num?)?.toInt() ?? 1,
        unlockedAtUtc: json['unlockedAtUtc'] == null
            ? null
            : DateTime.parse(json['unlockedAtUtc'] as String).toUtc(),
      );
}

/// Чем клуб принимает деньги прямо сейчас. Стойка — всегда: это наличные в кассе. Онлайн
/// держится и на тарифе платформы, и на заведённом мерчанте банка, поэтому спрашивается у
/// сервера, а не выводится в приложении.
class TopUpMethods {
  const TopUpMethods({required this.counter, required this.online});

  final bool counter;
  final bool online;

  factory TopUpMethods.fromJson(Map<String, dynamic> json) => TopUpMethods(
        counter: json['counter'] as bool? ?? true,
        online: json['online'] as bool? ?? false,
      );
}
