import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

import 'push_notification.dart';

/// Входящие уведомления: три случая, которые Firebase различает, а игрок — нет.
///
/// За интерфейсом — Firebase, и он здесь ради тестов: проверять переход по нажатию,
/// поднимая настоящий Firebase, значит тестировать чужую библиотеку вместо своей логики.
abstract class PushMessages {
  /// Приложение было закрыто и запустилось нажатием на уведомление. Ответ приходит один раз.
  Future<PushNotification?> initialMessage();

  /// Нажатие, когда приложение висело в фоне.
  Stream<PushNotification> get onOpenedApp;

  /// Уведомление пришло, пока игрок в приложении. Система в этом случае ничего не показывает —
  /// показать обязано приложение, иначе событие пропадёт совсем.
  Stream<PushNotification> get onForegroundMessage;
}

class FirebasePushMessages implements PushMessages {
  FirebasePushMessages();

  bool get _isSupported => !kIsWeb && (defaultTargetPlatform == TargetPlatform.android ||
      defaultTargetPlatform == TargetPlatform.iOS);

  @override
  Future<PushNotification?> initialMessage() async {
    if (!await _ensureReady()) return null;
    try {
      final message = await FirebaseMessaging.instance.getInitialMessage();
      return message == null ? null : _map(message);
    } catch (_) {
      return null;
    }
  }

  @override
  Stream<PushNotification> get onOpenedApp => _guarded(() => FirebaseMessaging.onMessageOpenedApp);

  @override
  Stream<PushNotification> get onForegroundMessage => _guarded(() => FirebaseMessaging.onMessage);

  /// Firebase роняет обращение к своим потокам, если приложение не настроено (нет сервисов
  /// Google, нет `google-services.json`). Для игрока это значит «пушей нет», а не «приложение
  /// падает», поэтому поток в таком случае просто пустой.
  Stream<PushNotification> _guarded(Stream<RemoteMessage> Function() source) async* {
    if (!await _ensureReady()) return;
    yield* source().map(_map);
  }

  PushNotification _map(RemoteMessage message) => PushNotification.fromData(
        title: message.notification?.title,
        body: message.notification?.body,
        data: message.data,
      );

  Future<bool> _ensureReady() async {
    if (!_isSupported) return false;
    try {
      if (Firebase.apps.isEmpty) {
        await Firebase.initializeApp();
      }
      return true;
    } catch (_) {
      return false;
    }
  }
}
