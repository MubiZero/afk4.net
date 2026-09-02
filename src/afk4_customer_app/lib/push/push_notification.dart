/// Уведомление, пришедшее на телефон.
///
/// Сервер кладёт в него не только текст, но и `template` — имя события. По нему приложение
/// понимает, куда вести игрока: «сессия скоро кончится» и «заказ готов» требуют разных
/// экранов, а уведомление, которое просто открывает главную, заставляет искать заново то,
/// о чём само и сообщило.
class PushNotification {
  const PushNotification({this.title, this.body, this.template, this.branchId});

  /// Разбор данных FCM. Всё необязательно: чужое сообщение, старая версия сервера или
  /// уведомление без полезной нагрузки не должны ронять приложение.
  factory PushNotification.fromData({
    String? title,
    String? body,
    Map<String, dynamic> data = const {},
  }) {
    String? string(String key) {
      final value = data[key];
      if (value is String && value.trim().isNotEmpty) return value.trim();
      return null;
    }

    return PushNotification(
      title: title,
      body: body,
      template: string('template'),
      branchId: string('branchId'),
    );
  }

  final String? title;
  final String? body;

  /// Имя шаблона на сервере, например `player.session_ending`.
  final String? template;

  /// Филиал, к которому относится событие. Пока не используется для перехода, но приходит
  /// с сервера и понадобится, когда у игрока будет несколько залов в одном клубе.
  final String? branchId;
}

/// Раздел приложения, который открывает нажатие на уведомление.
enum PushDestination { home, reservations, wallet, shop }

/// Куда ведёт событие. `null` — событие не про игрока или незнакомое: тогда нажатие просто
/// открывает приложение, ничего не подменяя. Молча вести не туда хуже, чем не вести никуда.
PushDestination? pushDestinationFor(String? template) => switch (template) {
      // Продлить можно с главной, там же живая сессия с обратным отсчётом.
      'player.session_ending' => PushDestination.home,
      'player.reservation_soon' => PushDestination.reservations,
      'player.balance_topped_up' => PushDestination.wallet,
      // Заказ живёт в магазине: там его состояние и отмена.
      'player.order_ready' => PushDestination.shop,
      // Объявление клуба или платформы читается на главной, в ленте новостей.
      'platform.announcement' => PushDestination.home,
      _ => null,
    };
