import 'dart:convert';

import 'package:http/http.dart' as http;

import '../api/contracts.dart';
import 'organization.dart';

/// Сбой при загрузке каталога. Отдельный тип, а не пустой список: «клубов не нашлось» и
/// «не смогли спросить» — разные вещи, и на экране они выглядят по-разному.
class OrganizationDirectoryException implements Exception {
  const OrganizationDirectoryException(this.statusCode) : isOffline = false;

  /// Запрос не дошёл до сервера. Отдельно от прочих отказов: «не удалось загрузить» игрок
  /// читает как поломку клуба и звонит в поддержку, а причина — его собственный интернет.
  /// Пустой statusCode сам по себе этого не говорит: его же получает ответ, который пришёл,
  /// но не разобрался.
  const OrganizationDirectoryException.offline()
      : statusCode = null,
        isOffline = true;

  final int? statusCode;
  final bool isOffline;

  @override
  String toString() => 'OrganizationDirectoryException(statusCode: $statusCode, offline: $isOffline)';
}

/// Публичный каталог клубов. Читается до входа: у мобильного приложения нет поддомена, из
/// которого веб-сборка берёт организацию, а войти без неё нельзя.
class OrganizationDirectory {
  OrganizationDirectory({required this.baseUrl, http.Client? httpClient})
      : _http = httpClient ?? http.Client();

  final String baseUrl;
  final http.Client _http;

  Future<List<Organization>> search({String? query}) async {
    final trimmed = query?.trim();
    final uri = Uri.parse('$baseUrl/api/public/organizations').replace(
      // Пустую строку не отправляем: сервер иначе получит фильтр «содержит пустоту» вместо
      // «фильтра нет» — тот же зарок, что и у сброса фильтров в журнале Оператора.
      queryParameters: trimmed == null || trimmed.isEmpty ? null : {'query': trimmed},
    );

    final http.Response response;
    try {
      response = await _http.get(uri);
    } catch (_) {
      throw const OrganizationDirectoryException.offline();
    }

    if (response.statusCode != 200) {
      throw OrganizationDirectoryException(response.statusCode);
    }

    // Разбор — тоже отказ каталога, а не поломка приложения: без этой обёртки битый ответ летел
    // наружу сырым FormatException, экран его не ловил (он ждёт OrganizationDirectoryException)
    // и витрина падала вместо того, чтобы предложить повтор. В reviews() обёртка была, здесь нет.
    try {
      final decoded = jsonDecode(utf8.decode(response.bodyBytes)) as List<dynamic>;
      return decoded
          .map((entry) => Organization.fromJson(entry as Map<String, dynamic>))
          .toList(growable: false);
    } catch (_) {
      throw const OrganizationDirectoryException(null);
    }
  }

  /// Отзывы о клубе. Читаются до входа — за этим их и пишут: игрок решает, идти ли сюда,
  /// ещё не будучи ничьим игроком.
  Future<ClubReviewsPageDto> reviews(String organizationId) async {
    final uri = Uri.parse('$baseUrl/api/public/organizations/$organizationId/reviews');

    final http.Response response;
    try {
      response = await _http.get(uri);
    } catch (_) {
      throw const OrganizationDirectoryException.offline();
    }

    if (response.statusCode != 200) {
      throw OrganizationDirectoryException(response.statusCode);
    }

    try {
      return ClubReviewsPageDto.fromJson(
          jsonDecode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>);
    } catch (_) {
      throw const OrganizationDirectoryException(null);
    }
  }
}
