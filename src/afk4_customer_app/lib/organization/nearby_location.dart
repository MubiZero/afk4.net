import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';

/// Чем закончилась попытка узнать, где игрок.
enum NearbyOutcome {
  /// Координаты получены.
  located,

  /// Человек отказал в доступе — это его право, и повторно клянчить нельзя.
  denied,

  /// Спросить не вышло: служба геопозиции выключена, платформа не умеет, устройство молчит.
  unavailable,
}

/// Результат попытки: точка или причина, по которой её нет.
typedef NearbyResult = ({NearbyOutcome outcome, LatLng? point});

/// Где сейчас игрок — чтобы показать ближние клубы первыми.
///
/// За интерфейсом — системная геопозиция. Он здесь ради тестов и ради честности: спрашиваем
/// только по нажатию на «Рядом со мной», только грубую точность (до города и района — этого
/// хватает, чтобы разложить витрину по близости) и нигде не храним. Приложению не нужно знать,
/// где человек живёт; ему нужно один раз понять, какой клуб ближе.
abstract class NearbyLocation {
  Future<NearbyResult> current();
}

class DeviceNearbyLocation implements NearbyLocation {
  const DeviceNearbyLocation();

  @override
  Future<NearbyResult> current() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        return (outcome: NearbyOutcome.unavailable, point: null);
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        return (outcome: NearbyOutcome.denied, point: null);
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.low,
        ),
      );
      return (
        outcome: NearbyOutcome.located,
        point: LatLng(position.latitude, position.longitude),
      );
    } catch (_) {
      // Платформа без геопозиции, отключённый модуль, таймаут устройства — для витрины это
      // одно и то же: ближние клубы показать нечем, остальное работает как работало.
      return (outcome: NearbyOutcome.unavailable, point: null);
    }
  }
}

/// Расстояние от точки до ближайшего зала клуба. null — у клуба нет координат ни у одного зала.
double? distanceToClubMeters(LatLng from, Iterable<LatLng> clubPoints) {
  const distance = Distance();
  double? nearest;
  for (final point in clubPoints) {
    final metres = distance.as(LengthUnit.Meter, from, point);
    if (nearest == null || metres < nearest) nearest = metres;
  }
  return nearest;
}
