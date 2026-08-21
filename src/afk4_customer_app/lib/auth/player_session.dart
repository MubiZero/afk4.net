/// Сессия игрока: кто вошёл и чем подписаны запросы. Зеркало `PlatformPersonSessionResponse`.
///
/// Аккаунт принадлежит человеку, а не клубу, поэтому клуба в сессии может не быть вовсе —
/// так выглядит тот, кто зарегистрировался дома и ещё никуда не заходил.
class PlayerSession {
  const PlayerSession({
    required this.displayName,
    required this.phoneVerified,
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.refreshTokenExpiresAtUtc,
    this.playerAccountId,
    this.organizationId,
    this.platformPersonId,
    this.preferredLocale,
    this.profileCompleted = true,
  });

  /// Счёт и клуб, закреплённые за токеном. null — клуб ещё не выбран или счёта в нём нет.
  final String? playerAccountId;
  final String? organizationId;
  final String displayName;
  final bool phoneVerified;
  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final DateTime refreshTokenExpiresAtUtc;

  /// Личность — то, что одно на все клубы сети.
  final String? platformPersonId;
  final String? preferredLocale;

  /// Спрошены ли имя и язык. Решает сервер: только он знает, новый это человек или давний.
  final bool profileCompleted;

  factory PlayerSession.fromJson(Map<String, dynamic> json) => PlayerSession(
        playerAccountId: json['playerAccountId'] as String?,
        organizationId: json['organizationId'] as String?,
        displayName: json['displayName'] as String,
        phoneVerified: json['phoneVerified'] as bool? ?? false,
        accessToken: json['accessToken'] as String,
        accessTokenExpiresAtUtc: DateTime.parse(json['accessTokenExpiresAtUtc'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshTokenExpiresAtUtc: DateTime.parse(json['refreshTokenExpiresAtUtc'] as String),
        platformPersonId: json['platformPersonId'] as String?,
        preferredLocale: json['preferredLocale'] as String?,
        // Старый ответ этого поля не несёт, и человек в нём заведомо давний.
        profileCompleted: json['profileCompleted'] as bool? ?? true,
      );

  Map<String, dynamic> toJson() => {
        if (playerAccountId != null) 'playerAccountId': playerAccountId,
        if (organizationId != null) 'organizationId': organizationId,
        'displayName': displayName,
        'phoneVerified': phoneVerified,
        'accessToken': accessToken,
        'accessTokenExpiresAtUtc': accessTokenExpiresAtUtc.toIso8601String(),
        'refreshToken': refreshToken,
        'refreshTokenExpiresAtUtc': refreshTokenExpiresAtUtc.toIso8601String(),
        if (platformPersonId != null) 'platformPersonId': platformPersonId,
        if (preferredLocale != null) 'preferredLocale': preferredLocale,
        'profileCompleted': profileCompleted,
      };

  PlayerSession withProfileCompleted(String displayName) => PlayerSession(
        playerAccountId: playerAccountId,
        organizationId: organizationId,
        displayName: displayName,
        phoneVerified: phoneVerified,
        accessToken: accessToken,
        accessTokenExpiresAtUtc: accessTokenExpiresAtUtc,
        refreshToken: refreshToken,
        refreshTokenExpiresAtUtc: refreshTokenExpiresAtUtc,
        platformPersonId: platformPersonId,
        preferredLocale: preferredLocale,
        profileCompleted: true,
      );

  @override
  bool operator ==(Object other) =>
      other is PlayerSession &&
      other.playerAccountId == playerAccountId &&
      other.organizationId == organizationId &&
      other.displayName == displayName &&
      other.phoneVerified == phoneVerified &&
      other.accessToken == accessToken &&
      other.refreshToken == refreshToken &&
      other.profileCompleted == profileCompleted;

  @override
  int get hashCode => Object.hash(playerAccountId, organizationId, displayName, phoneVerified,
      accessToken, refreshToken, profileCompleted);
}
