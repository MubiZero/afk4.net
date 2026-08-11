import 'dart:convert';

import 'package:http/http.dart' as http;

/// Заглушка сервера для тестов.
///
/// Обработчик отдаёт СТРОКУ и код, а не готовый `http.Response`: его конструктор кодирует
/// тело в latin1, когда в заголовках нет charset, и кириллица приезжает битой. Байты
/// собираются здесь так же, как их отдаёт настоящий сервер.
class FakeHttpClient extends http.BaseClient {
  FakeHttpClient(this.handler, {this.delay = Duration.zero});

  /// Ответ на запрос: тело и код состояния.
  final (String, int) Function(http.BaseRequest request) handler;

  /// Задержка ответа — нужна тестам, которые проверяют состояние «отправляем…».
  final Duration delay;

  final List<http.BaseRequest> requests = [];
  final List<Map<String, dynamic>> bodies = [];

  /// Пути запросов по порядку — по ним удобно проверять, что экран сходил куда надо.
  List<String> get paths => requests.map((r) => r.url.path).toList();

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    requests.add(request);
    if (request is http.Request && request.body.isNotEmpty) {
      bodies.add(jsonDecode(request.body) as Map<String, dynamic>);
    }
    if (delay > Duration.zero) await Future<void>.delayed(delay);
    final (body, status) = handler(request);
    return http.StreamedResponse(
      Stream.value(utf8.encode(body)),
      status,
      headers: const {'content-type': 'application/json; charset=utf-8'},
    );
  }
}
