import 'dart:io' show Platform;

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

/// Откуда берётся адрес телефона для пушей.
///
/// За интерфейсом — Firebase. Он здесь ради тестов: подключать настоящий Firebase к проверке
/// того, что при выходе токен снимается, — это проверять чужую библиотеку вместо своей логики.
abstract class PushTokens {
  /// Работают ли пуши на этом устройстве вообще. На вебе и в тестах — нет.
  bool get isSupported;

  /// Спросить разрешение. iOS спрашивает системным окном, Android 13+ тоже.
  Future<bool> requestPermission();

  /// Текущий токен устройства. null — разрешения нет или Firebase не настроен.
  Future<String?> token();

  /// Firebase иногда меняет токен сам — приложение обязано сообщить новый, иначе пуши
  /// молча перестанут доходить.
  Stream<String> get onTokenRefresh;

  /// Как называется платформа для сервера.
  String get platform;
}

class FirebasePushTokens implements PushTokens {
  FirebasePushTokens();

  bool _ready = false;

  @override
  bool get isSupported => !kIsWeb && (Platform.isAndroid || Platform.isIOS);

  @override
  String get platform => !kIsWeb && Platform.isIOS ? 'ios' : 'android';

  @override
  Future<bool> requestPermission() async {
    if (!isSupported) return false;
    if (!await _ensureReady()) return false;

    final settings = await FirebaseMessaging.instance.requestPermission();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  @override
  Future<String?> token() async {
    if (!isSupported || !await _ensureReady()) return null;
    try {
      return await FirebaseMessaging.instance.getToken();
    } catch (_) {
      // Нет сети, нет сервисов Google, не настроен проект — во всех случаях пушей просто
      // не будет. Ронять запуск приложения из-за этого нельзя.
      return null;
    }
  }

  @override
  Stream<String> get onTokenRefresh =>
      isSupported ? FirebaseMessaging.instance.onTokenRefresh : const Stream<String>.empty();

  /// Firebase инициализируется из google-services.json, который подкладывает сборка —
  /// ключи не живут в коде.
  Future<bool> _ensureReady() async {
    if (_ready) return true;
    try {
      if (Firebase.apps.isEmpty) {
        await Firebase.initializeApp();
      }
      _ready = true;
    } catch (_) {
      _ready = false;
    }
    return _ready;
  }
}
