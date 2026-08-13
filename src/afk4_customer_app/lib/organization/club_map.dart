import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../l10n/app_localizations.dart';
import 'organization.dart';

/// Карта клубов. Список отвечает «какие клубы есть», карта — «какой из них рядом», и второй
/// вопрос игрок задаёт чаще: до клуба надо доехать.
///
/// Точки ставятся по координатам, которые владелец задаёт в профиле филиала. Клуб без
/// координат из каталога не исчезает — его просто нет на карте, и об этом сказано прямо.
class ClubMap extends StatelessWidget {
  const ClubMap({super.key, required this.clubs, required this.onSelected});

  final List<Organization> clubs;
  final ValueChanged<Organization> onSelected;

  List<(Organization, ClubPlace)> get _points => [
        for (final club in clubs)
          for (final place in club.places)
            if (place.hasPoint) (club, place),
      ];

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final points = _points;

    if (points.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.map_outlined, size: 40, color: theme.colorScheme.onSurfaceVariant),
              const SizedBox(height: 12),
              Text(
                l.customerClubPickerMapEmpty,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
            ],
          ),
        ),
      );
    }

    return Semantics(
      label: l.a11yClubMap,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(20),
        child: Stack(
          children: [
            FlutterMap(
              options: MapOptions(
                // Подложка под тайлами — цвет приложения: пока тайлы едут, карта не должна
                // светиться серым листом посреди тёмного экрана.
                backgroundColor: theme.colorScheme.surface,
                // Масштаб подбирается под разброс точек, а не задаётся числом: с двумя
                // клубами в разных городах любой фиксированный зум оставляет один из них
                // за кадром — и карта выглядит пустой при полном каталоге.
                initialCameraFit: CameraFit.bounds(
                  bounds: LatLngBounds.fromPoints([
                    for (final (_, place) in points) LatLng(place.latitude!, place.longitude!),
                  ]),
                  // Снизу запас больше: там лежит подсказка с авторством карты, и точка,
                  // попавшая под неё, не нажимается.
                  padding: const EdgeInsets.fromLTRB(48, 48, 48, 88),
                  maxZoom: 15,
                ),
                interactionOptions: const InteractionOptions(
                  // Вращение выключено: на карте города оно только сбивает, а вернуть север
                  // на место игроку нечем.
                  flags: InteractiveFlag.pinchZoom | InteractiveFlag.drag | InteractiveFlag.doubleTapZoom,
                ),
              ),
              children: [
                TileLayer(
                  urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                  // OpenStreetMap просит указывать приложение в User-Agent и показывать
                  // авторство — оно ниже, в углу карты.
                  userAgentPackageName: 'net.afk4.customer',
                  tileProvider: NetworkTileProvider(),
                ),
                MarkerLayer(
                  markers: [
                    for (final (club, place) in points)
                      Marker(
                        point: LatLng(place.latitude!, place.longitude!),
                        width: 44,
                        height: 44,
                        child: _Pin(club: club, place: place, onTap: () => onSelected(club)),
                      ),
                  ],
                ),
              ],
            ),
            Positioned(
              left: 12,
              right: 12,
              bottom: 12,
              child: _MapFooter(hint: l.customerClubPickerMapHint),
            ),
          ],
        ),
      ),
    );
  }

}

/// Точка клуба. Подпись рядом с точкой не рисуем: на карте города подписи налезают друг на
/// друга, а название клуба игрок увидит по нажатию.
class _Pin extends StatelessWidget {
  const _Pin({required this.club, required this.place, required this.onTap});

  final Organization club;
  final ClubPlace place;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Tooltip(
      message: place.address == null ? club.name : '${club.name} · ${place.address}',
      child: GestureDetector(
        onTap: onTap,
        child: Center(
          child: Container(
            width: 34,
            height: 34,
            decoration: BoxDecoration(
              color: theme.colorScheme.primary,
              shape: BoxShape.circle,
              border: Border.all(color: Colors.white, width: 2),
              boxShadow: const [BoxShadow(color: Color(0x66000000), blurRadius: 8, offset: Offset(0, 3))],
            ),
            child: Icon(Icons.sports_esports, size: 18, color: theme.colorScheme.onPrimary),
          ),
        ),
      ),
    );
  }
}

class _MapFooter extends StatelessWidget {
  const _MapFooter({required this.hint});

  final String hint;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return DecoratedBox(
      decoration: BoxDecoration(
        color: theme.colorScheme.surface.withValues(alpha: 0.86),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Row(
          children: [
            Expanded(
              child: Text(hint, style: theme.textTheme.labelMedium),
            ),
            // Авторство карты. Требование лицензии OpenStreetMap, а не украшение.
            Text(
              '© OpenStreetMap',
              style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ),
      ),
    );
  }
}
