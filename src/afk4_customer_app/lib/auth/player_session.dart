/// Сессия игрока: кто вошёл и чем подписаны запросы. Зеркало `PlayerSignInResponse`.
class PlayerSession {
  const PlayerSession({
    required this.playerAccountId,
    required this.organizationId,
    required this.displayName,
    required this.phoneVerified,
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.refreshTokenExpiresAtUtc,
  });

  final String playerAccountId;
  final String organizationId;
  final String displayName;
  final bool phoneVerified;
  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final DateTime refreshTokenExpiresAtUtc;

  factory PlayerSession.fromJson(Map<String, dynamic> json) => PlayerSession(
        playerAccountId: json['playerAccountId'] as String,
        organizationId: json['organizationId'] as String,
        displayName: json['displayName'] as String,
        phoneVerified: json['phoneVerified'] as bool? ?? false,
        accessToken: json['accessToken'] as String,
        accessTokenExpiresAtUtc: DateTime.parse(json['accessTokenExpiresAtUtc'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshTokenExpiresAtUtc: DateTime.parse(json['refreshTokenExpiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'organizationId': organizationId,
        'displayName': displayName,
        'phoneVerified': phoneVerified,
        'accessToken': accessToken,
        'accessTokenExpiresAtUtc': accessTokenExpiresAtUtc.toIso8601String(),
        'refreshToken': refreshToken,
        'refreshTokenExpiresAtUtc': refreshTokenExpiresAtUtc.toIso8601String(),
      };

  @override
  bool operator ==(Object other) =>
      other is PlayerSession &&
      other.playerAccountId == playerAccountId &&
      other.organizationId == organizationId &&
      other.displayName == displayName &&
      other.phoneVerified == phoneVerified &&
      other.accessToken == accessToken &&
      other.refreshToken == refreshToken;

  @override
  int get hashCode =>
      Object.hash(playerAccountId, organizationId, displayName, phoneVerified, accessToken, refreshToken);
}
