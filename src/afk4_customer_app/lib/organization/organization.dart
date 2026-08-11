/// Клуб, каким его видит игрок в выборе: достаточно, чтобы узнать, и ничего о делах бизнеса.
/// Зеркало `OrganizationDirectoryEntryDto` с сервера.
class Organization {
  const Organization({
    required this.organizationId,
    required this.slug,
    required this.name,
    this.logoUrl,
    this.accentColor,
  });

  final String organizationId;
  final String slug;
  final String name;
  final String? logoUrl;
  final String? accentColor;

  factory Organization.fromJson(Map<String, dynamic> json) => Organization(
        organizationId: json['organizationId'] as String,
        slug: json['slug'] as String,
        name: json['name'] as String,
        logoUrl: json['logoUrl'] as String?,
        accentColor: json['accentColor'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'slug': slug,
        'name': name,
        if (logoUrl != null) 'logoUrl': logoUrl,
        if (accentColor != null) 'accentColor': accentColor,
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
