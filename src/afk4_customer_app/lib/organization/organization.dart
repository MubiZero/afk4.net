import 'opening_hours.dart';

/// Клуб, каким его видит игрок в выборе: достаточно, чтобы выбрать, и ничего о делах бизнеса.
/// Зеркало `OrganizationDirectoryEntryDto` с сервера.
class Organization {
  const Organization({
    required this.organizationId,
    required this.slug,
    required this.name,
    this.logoUrl,
    this.accentColor,
    this.places = const [],
    this.pricePerHourFromMinorUnits,
    this.currencyCode,
    this.seatCount = 0,
    this.rating,
    this.reviewCount = 0,
  });

  final String organizationId;
  final String slug;
  final String name;
  final String? logoUrl;
  final String? accentColor;

  /// Залы сети: то, что показывает карточка, и то, что карта ставит точками.
  final List<ClubPlace> places;

  /// Цена часа «от» — по действующим тарифам сети.
  final int? pricePerHourFromMinorUnits;
  final String? currencyCode;
  final int seatCount;

  /// Средняя оценка по отзывам игроков; null — оценок пока нет (это не ноль звёзд).
  final double? rating;
  final int reviewCount;

  /// Город, в котором клуб. Сеть может быть в нескольких — тогда показываем первый и считаем
  /// остальные: «Душанбе +2» честнее, чем один город, выданный за всю сеть.
  String? get city => places.isEmpty ? null : places.first.city;

  /// Фото для карточки — из первого зала сети, который их прислал. Смешивать фотографии
  /// разных залов в одну ленту нельзя: игрок пролистал бы чужой зал, думая, что смотрит этот.
  List<String> get photoUrls {
    for (final place in places) {
      if (place.photoUrls.isNotEmpty) return place.photoUrls;
      if (place.coverImageUrl != null && place.coverImageUrl!.isNotEmpty) {
        return [place.coverImageUrl!];
      }
    }
    return const [];
  }

  factory Organization.fromJson(Map<String, dynamic> json) => Organization(
        organizationId: json['organizationId'] as String,
        slug: json['slug'] as String,
        name: json['name'] as String,
        logoUrl: json['logoUrl'] as String?,
        accentColor: json['accentColor'] as String?,
        places: (json['places'] as List<dynamic>? ?? const [])
            .map((entry) => ClubPlace.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
        pricePerHourFromMinorUnits: (json['pricePerHourFromMinorUnits'] as num?)?.toInt(),
        currencyCode: json['currencyCode'] as String?,
        seatCount: (json['seatCount'] as num?)?.toInt() ?? 0,
        rating: (json['rating'] as num?)?.toDouble(),
        reviewCount: (json['reviewCount'] as num?)?.toInt() ?? 0,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'slug': slug,
        'name': name,
        if (logoUrl != null) 'logoUrl': logoUrl,
        if (accentColor != null) 'accentColor': accentColor,
        if (places.isNotEmpty) 'places': places.map((place) => place.toJson()).toList(),
        if (pricePerHourFromMinorUnits != null)
          'pricePerHourFromMinorUnits': pricePerHourFromMinorUnits,
        if (currencyCode != null) 'currencyCode': currencyCode,
        if (seatCount != 0) 'seatCount': seatCount,
        if (rating != null) 'rating': rating,
        if (reviewCount != 0) 'reviewCount': reviewCount,
      };

  @override
  bool operator ==(Object other) =>
      other is Organization &&
      other.organizationId == organizationId &&
      other.slug == slug &&
      other.name == name &&
      other.logoUrl == logoUrl &&
      other.accentColor == accentColor;

  @override
  int get hashCode => Object.hash(organizationId, slug, name, logoUrl, accentColor);
}

/// Один зал сети: адрес, фото и точка на карте.
class ClubPlace {
  const ClubPlace({
    required this.branchId,
    required this.name,
    required this.city,
    this.address,
    this.description,
    this.coverImageUrl,
    this.photoUrls = const [],
    this.latitude,
    this.longitude,
    this.workingHours = const [],
    this.zones = const [],
  });

  final String branchId;
  final String name;
  final String city;
  final String? address;
  final String? description;
  final String? coverImageUrl;

  /// Фото зала по порядку: обложка первой, дальше галерея — так их выстроил владелец.
  final List<String> photoUrls;

  /// Координат может не быть: владелец их ещё не поставил. Такой клуб остаётся в списке —
  /// просто на карте его нет.
  final double? latitude;
  final double? longitude;

  /// Расписание клуба. Пусто — расписание не задано, и врать «открыто» об этом нельзя.
  final List<OpeningDay> workingHours;

  /// Залы этого клуба: сколько мест и на чём играют.
  final List<ClubZone> zones;

  bool get hasPoint => latitude != null && longitude != null;

  /// Как зал зовут на экране. Пустое имя бывает у клуба, который его не задал: тогда за
  /// название работает город, а безымянной строки на экране не остаётся.
  String get displayName => name.isNotEmpty ? name : city;

  /// Адрес зала целиком — вместе с городом. Так его читают там, где имя зала не показано.
  String get fullAddress =>
      [city, address ?? ''].where((part) => part.isNotEmpty).join(', ');

  /// Адрес под названием зала. Город не повторяется, когда он и есть название.
  String get addressUnderName => name.isNotEmpty ? fullAddress : (address ?? '');

  factory ClubPlace.fromJson(Map<String, dynamic> json) => ClubPlace(
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        city: json['city'] as String? ?? '',
        address: json['address'] as String?,
        description: json['description'] as String?,
        coverImageUrl: json['coverImageUrl'] as String?,
        photoUrls: (json['photoUrls'] as List<dynamic>? ?? const [])
            .map((entry) => entry as String)
            .toList(growable: false),
        latitude: (json['latitude'] as num?)?.toDouble(),
        longitude: (json['longitude'] as num?)?.toDouble(),
        workingHours: (json['workingHours'] as List<dynamic>? ?? const [])
            .map((entry) => OpeningDay.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
        zones: (json['zones'] as List<dynamic>? ?? const [])
            .map((entry) => ClubZone.fromJson(entry as Map<String, dynamic>))
            .toList(growable: false),
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'name': name,
        'city': city,
        if (address != null) 'address': address,
        if (description != null) 'description': description,
        if (coverImageUrl != null) 'coverImageUrl': coverImageUrl,
        if (photoUrls.isNotEmpty) 'photoUrls': photoUrls,
        if (latitude != null) 'latitude': latitude,
        if (longitude != null) 'longitude': longitude,
        if (workingHours.isNotEmpty)
          'workingHours': workingHours.map((day) => day.toJson()).toList(),
        if (zones.isNotEmpty) 'zones': zones.map((zone) => zone.toJson()).toList(),
      };
}

/// Зал клуба: название, сколько в нём мест и чем оснащён.
class ClubZone {
  const ClubZone({required this.name, this.seatCount = 0, this.hardwareSummary});

  final String name;
  final int seatCount;

  /// Железо словами владельца. null — не указано, и выдумывать за него нечего.
  final String? hardwareSummary;

  factory ClubZone.fromJson(Map<String, dynamic> json) => ClubZone(
        name: json['name'] as String? ?? '',
        seatCount: (json['seatCount'] as num?)?.toInt() ?? 0,
        hardwareSummary: json['hardwareSummary'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'seatCount': seatCount,
        if (hardwareSummary != null) 'hardwareSummary': hardwareSummary,
      };
}
