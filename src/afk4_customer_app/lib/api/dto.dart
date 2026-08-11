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
