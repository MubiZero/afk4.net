import 'package:flutter/material.dart';

import 'api/player_api_client.dart';
import 'app.dart';
import 'organization/organization_directory.dart';
import 'push/push_messages.dart';
import 'push/push_tokens.dart';

/// Адрес API задаётся при сборке: `--dart-define=AFK4_API_BASE=https://…`.
/// Пустая строка означает «тот же источник» и годится только для веб-сборки.
const String apiBase = String.fromEnvironment('AFK4_API_BASE');

void main() {
  runApp(CustomerApp(
    directory: OrganizationDirectory(baseUrl: apiBase),
    api: PlayerApiClient(baseUrl: apiBase),
    // На вебе и в тестах Firebase недоступен — там служба сама сообщает, что не поддержана,
    // и переключатель уведомлений не показывается.
    pushTokens: FirebasePushTokens(),
    pushMessages: FirebasePushMessages(),
  ));
}
