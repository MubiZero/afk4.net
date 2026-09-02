import 'package:flutter_test/flutter_test.dart';

import 'package:afk4_customer_app/push/push_notification.dart';

void main() {
  group('разбор уведомления', () {
    test('берёт текст и данные сервера', () {
      final notification = PushNotification.fromData(
        title: 'Сессия скоро закончится',
        body: '10 мин до конца игры за PC-07.',
        data: const {'template': 'player.session_ending', 'branchId': 'b1'},
      );

      expect(notification.title, 'Сессия скоро закончится');
      expect(notification.body, '10 мин до конца игры за PC-07.');
      expect(notification.template, 'player.session_ending');
      expect(notification.branchId, 'b1');
    });

    test('переживает уведомление без данных', () {
      final notification = PushNotification.fromData(body: 'Просто текст');

      expect(notification.template, isNull);
      expect(notification.branchId, isNull);
      expect(notification.body, 'Просто текст');
    });

    test('пустые и нестроковые значения считает отсутствующими', () {
      final notification = PushNotification.fromData(
        data: const {'template': '   ', 'branchId': 42},
      );

      expect(notification.template, isNull);
      expect(notification.branchId, isNull);
    });
  });

  group('куда ведёт событие', () {
    test('каждое событие игрока ведёт туда, где с ним можно что-то сделать', () {
      expect(pushDestinationFor('player.session_ending'), PushDestination.home);
      expect(pushDestinationFor('player.reservation_soon'), PushDestination.reservations);
      expect(pushDestinationFor('player.balance_topped_up'), PushDestination.wallet);
      expect(pushDestinationFor('player.order_ready'), PushDestination.shop);
      expect(pushDestinationFor('platform.announcement'), PushDestination.home);
    });

    // Уведомление незнакомого вида приложение просто открывает. Увести наугад хуже: игрок
    // окажется не там, где обещали, и решит, что приложение врёт.
    test('незнакомое или пустое событие никуда не ведёт', () {
      expect(pushDestinationFor('staff.invite'), isNull);
      expect(pushDestinationFor(''), isNull);
      expect(pushDestinationFor(null), isNull);
    });
  });
}
