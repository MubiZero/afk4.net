import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';

http.Client clientReturning(String body, {int status = 200, void Function(Uri)? onRequest}) =>
    _FakeClient((request) {
      onRequest?.call(request.url);
      return http.Response(body, status, headers: {'content-type': 'application/json; charset=utf-8'});
    });

class _FakeClient extends http.BaseClient {
  _FakeClient(this.handler);

  final http.Response Function(http.BaseRequest request) handler;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final response = handler(request);
    return http.StreamedResponse(
      Stream.value(utf8.encode(response.body)),
      response.statusCode,
      headers: response.headers,
    );
  }
}

void main() {
  const listJson = '''
[
  {"organizationId":"11111111-1111-1111-1111-111111111111","slug":"cyberx","name":"CyberX","logoUrl":"https://cdn/x.png","accentColor":"#2cc592"},
  {"organizationId":"22222222-2222-2222-2222-222222222222","slug":"arena","name":"Arena","logoUrl":null,"accentColor":null}
]''';

  test('разбирает каталог, включая клуб без логотипа', () async {
    final directory = OrganizationDirectory(
      baseUrl: 'https://api.example',
      httpClient: clientReturning(listJson),
    );

    final clubs = await directory.search();

    expect(clubs, hasLength(2));
    expect(clubs.first.name, 'CyberX');
    expect(clubs.first.logoUrl, 'https://cdn/x.png');
    expect(clubs.last.logoUrl, isNull);
  });

  test('строка поиска уезжает в запрос, а не фильтруется на клиенте', () async {
    Uri? requested;
    final directory = OrganizationDirectory(
      baseUrl: 'https://api.example',
      httpClient: clientReturning(listJson, onRequest: (uri) => requested = uri),
    );

    await directory.search(query: 'аре');

    expect(requested!.path, '/api/public/organizations');
    expect(requested!.queryParameters['query'], 'аре');
  });

  test('пустой запрос не шлёт параметр — сервер не должен фильтровать по пустоте', () async {
    Uri? requested;
    final directory = OrganizationDirectory(
      baseUrl: 'https://api.example',
      httpClient: clientReturning(listJson, onRequest: (uri) => requested = uri),
    );

    await directory.search(query: '   ');

    expect(requested!.queryParameters.containsKey('query'), isFalse);
  });

  // Список клубов — единственный вход в приложение. Сетевой сбой должен доходить до экрана
  // как ошибка с возможностью повторить, а не как пустой список, который читается «клубов нет».
  test('ошибка сервера поднимается наверх, а не превращается в пустой список', () async {
    final directory = OrganizationDirectory(
      baseUrl: 'https://api.example',
      httpClient: clientReturning('{"error":"boom"}', status: 500),
    );

    expect(() => directory.search(), throwsA(isA<OrganizationDirectoryException>()));
  });

  test('модель переживает круг через JSON — её кладут в хранилище устройства', () {
    const club = Organization(
      organizationId: '11111111-1111-1111-1111-111111111111',
      slug: 'cyberx',
      name: 'CyberX',
      logoUrl: 'https://cdn/x.png',
      accentColor: '#2cc592',
    );

    expect(Organization.fromJson(jsonDecode(jsonEncode(club.toJson())) as Map<String, dynamic>), club);
  });
}
