import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:afk4_customer_app/api/player_api_client.dart';
import 'package:afk4_customer_app/app.dart';
import 'package:afk4_customer_app/organization/organization_directory.dart';
import 'package:afk4_customer_app/profile/profile_screen.dart';

import 'support/fake_backend.dart';

/// Сквозной сценарий клиентского приложения: выбор клуба → вход по SMS-коду → главная →
/// создание брони → выход.
///
/// Модульные тесты проверяют экраны поодиночке и подменяют всё вокруг. Здесь подменён
/// только сервер: приложение поднимается целиком, со своими хранилищами, маршрутизацией и
/// клиентом API. Такой тест ловит то, что не видно поэкранно, — потерянный при переходе
/// токен, забытую запись в хранилище, разошедшиеся адреса запросов.
///
/// Идёт на flutter-tester (`flutter test integration_test`), устройство не нужно.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  /// Мок пакета пишет сюда — заглядываем внутрь, чтобы отличить «сессии нет на экране» от
  /// «сессии нет на устройстве».
  late Map<String, String> secureValues;
  late FakeBackend backend;

  setUp(() {
    SharedPreferences.setMockInitialValues({});
    secureValues = {};
    FlutterSecureStorage.setMockInitialValues(secureValues);
    backend = FakeBackend();
  });

  Widget app() => CustomerApp(
        // Язык задан явно: сценарий читает подписи, а язык раннера не наш выбор.
        locale: const Locale('ru'),
        directory: OrganizationDirectory(baseUrl: 'https://api', httpClient: backend),
        api: PlayerApiClient(baseUrl: 'https://api', httpClient: backend),
      );

  /// Соглашается с предложенными значениями в системных диалогах даты и времени.
  /// Подпись кнопки зависит от языка («ОК» кириллицей на русском), поэтому ищется по обоим.
  final confirmButton = find.byWidgetPredicate(
    (widget) => widget is Text && (widget.data == 'OK' || widget.data == 'ОК'),
  );

  Future<void> pickDateTime(WidgetTester tester, String field) async {
    // Нажимается поле целиком, а не подпись внутри него: подпись сдвинута в угол рамки и
    // сама по себе нажатие не принимает.
    await tester.tap(find.ancestor(of: find.text(field), matching: find.byType(InkWell)).first);
    await tester.pumpAndSettle();
    await tester.tap(confirmButton);
    await tester.pumpAndSettle();
    await tester.tap(confirmButton);
    await tester.pumpAndSettle();
  }

  testWidgets('игрок выбирает клуб, входит по коду, бронирует место и выходит',
      (tester) async {
    await tester.pumpWidget(app());
    await tester.pumpAndSettle();

    // 1. Выбор клуба. У приложения из магазина нет поддомена, из которого веб-версия брала
    // организацию, — без этого шага входить некуда.
    expect(find.text('Выберите клуб'), findsOneWidget);
    await tester.tap(find.text(FakeBackend.clubName));
    await tester.pumpAndSettle();

    // 2. Вход по коду из SMS — единственная дверь, и клуба она не называет.
    expect(find.text('Вход'), findsOneWidget);
    await tester.enterText(find.byType(TextField).first, '+992900000000');
    await tester.tap(find.widgetWithText(FilledButton, 'Прислать код'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Код отправлен'), findsOneWidget);

    await tester.enterText(find.byType(TextField).last, backend.smsCode);
    await tester.tap(find.widgetWithText(FilledButton, 'Войти'));
    await tester.pumpAndSettle();

    // 3. Главная: имя игрока и его деньги. Сессия при этом легла в защищённое хранилище —
    // иначе следующий запуск снова спросил бы код.
    expect(find.text(FakeBackend.playerName), findsOneWidget);
    expect(find.text('Баланс кошелька'), findsOneWidget);
    // Разделитель разрядов у `intl` неразрывный — сумма ищется по хвосту, а не целиком.
    expect(find.textContaining('200,50 с.'), findsOneWidget);
    expect(secureValues, isNotEmpty);

    // Третье число кошелька: остаток уже без него, и без строки непонятно, куда оно делось.
    expect(find.textContaining('Придержано под брони'), findsOneWidget);

    // Закрытые разделы отвечают 401 без токена и 409 без названного клуба — раз данные на
    // экране есть, дошли оба заголовка.
    expect(backend.log, contains('GET /api/me'));
    expect(backend.log, contains('GET /api/me/dashboard'));

    // 4. Бронь. Раздел появился, потому что клуб принимает онлайн-брони.
    await tester.tap(find.text('Брони'));
    await tester.pumpAndSettle();
    expect(find.text('Броней пока нет'), findsOneWidget);

    await tester.tap(find.byType(FloatingActionButton));
    await tester.pumpAndSettle();
    // Филиал смотрит заявки руками, и лист говорит об этом до того, как игрок заполнит форму.
    expect(find.textContaining('Заявку смотрит администратор'), findsOneWidget);
    await pickDateTime(tester, 'Начало');
    await pickDateTime(tester, 'Конец');
    await tester.tap(find.widgetWithText(FilledButton, 'Забронировать'));
    await tester.pumpAndSettle();

    // Бронь доехала до сервера и вернулась в список — проверяются обе стороны обмена, а не
    // только то, что кнопка нажалась.
    expect(backend.reservations, hasLength(1));
    expect(find.text('Бронь создана'), findsOneWidget);
    expect(find.text('Ожидает подтверждения'), findsOneWidget);
    expect(find.text('Броней пока нет'), findsNothing);
    // У заявки есть срок ответа, и он виден игроку: молчание клуба не длится вечно.
    expect(find.textContaining('Клуб ответит до'), findsOneWidget);

    // Снекбар живёт четыре секунды и перекрывает низ следующего экрана — ждём, пока уйдёт.
    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();

    // 5. Профиль: здесь и только здесь задаётся PIN, которым игрок садится за ПК.
    await tester.tap(find.text('Профиль'));
    await tester.pumpAndSettle();
    expect(find.text('PIN для посадки за ПК'), findsOneWidget);
    expect(find.textContaining('PIN не задан'), findsOneWidget);

    final requestsBeforeSignOut = backend.log.length;

    // 6. Выход. Устройство бывает общим: следующий вошедший не должен увидеть чужой кошелёк.
    // Кнопка стоит в самом низу профиля — до неё надо доскроллить, и с запасом: прокрутка
    // «до видимости» оставляет её под прилипшей шапкой.
    final profileList = find.descendant(
      of: find.byType(ProfileScreen),
      matching: find.byType(Scrollable),
    );
    await tester.scrollUntilVisible(find.text('Выйти'), 200, scrollable: profileList);
    await tester.drag(profileList, const Offset(0, -160));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Выйти'));
    await tester.pumpAndSettle();

    expect(find.text('Вход'), findsOneWidget);
    expect(find.text(FakeBackend.playerName), findsNothing);
    // Сессия стёрта с устройства, а не только с экрана.
    expect(secureValues, isEmpty);
    // И запросы под ней прекратились. Опрос главной ходит раз в 30 секунд — пережидаем этот
    // срок, иначе проверка ничего не значила бы: живой таймер успел бы сходить с мёртвым токеном.
    await tester.pump(const Duration(seconds: 35));
    await tester.pumpAndSettle();
    expect(
      backend.log.skip(requestsBeforeSignOut).where((entry) => entry.contains('/api/me/')),
      isEmpty,
    );

    // Клуб при этом остался выбранным: игрок вышел из аккаунта, а не сменил заведение.
    expect(find.text(FakeBackend.clubName), findsOneWidget);
    expect(find.text('Выберите клуб'), findsNothing);
  });
}
