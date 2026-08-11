import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:afk4_customer_app/app.dart';
import 'package:afk4_customer_app/organization/organization.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';

class _StubDirectory extends OrganizationDirectory {
  _StubDirectory(this.clubs) : super(baseUrl: 'https://stub');

  final List<Organization> clubs;

  @override
  Future<List<Organization>> search({String? query}) async => clubs;
}

const _cyberx = Organization(
  organizationId: '11111111-1111-1111-1111-111111111111',
  slug: 'cyberx',
  name: 'CyberX',
);

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({}));

  testWidgets('первый запуск ведёт на выбор клуба', (tester) async {
    await tester.pumpWidget(CustomerApp(locale: const Locale('ru'), directory: _StubDirectory(const [_cyberx])));
    await tester.pumpAndSettle();

    expect(find.text('Выберите клуб'), findsOneWidget);
  });

  // Спрашивать клуб при каждом запуске — то же, что спрашивать логин заново.
  testWidgets('выбранный клуб переживает перезапуск', (tester) async {
    await tester.pumpWidget(CustomerApp(locale: const Locale('ru'), directory: _StubDirectory(const [_cyberx])));
    await tester.pumpAndSettle();
    await tester.tap(find.text('CyberX'));
    await tester.pumpAndSettle();

    // Перезапуск: новое дерево поверх тех же настроек устройства.
    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(CustomerApp(locale: const Locale('ru'), directory: _StubDirectory(const [_cyberx])));
    await tester.pumpAndSettle();

    expect(find.text('Выберите клуб'), findsNothing);
    expect(find.text('CyberX'), findsOneWidget);
  });

  testWidgets('смена клуба возвращает к выбору', (tester) async {
    await tester.pumpWidget(CustomerApp(locale: const Locale('ru'), directory: _StubDirectory(const [_cyberx])));
    await tester.pumpAndSettle();
    await tester.tap(find.text('CyberX'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Сменить клуб'));
    await tester.pumpAndSettle();

    expect(find.text('Выберите клуб'), findsOneWidget);
  });
}
