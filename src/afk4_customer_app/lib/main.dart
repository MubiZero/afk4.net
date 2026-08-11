import 'package:flutter/material.dart';

import 'app.dart';
import 'organization/organization_directory.dart';

/// Адрес API задаётся при сборке: `--dart-define=AFK4_API_BASE=https://…`.
/// Пустая строка означает «тот же источник» и годится только для веб-сборки.
const String apiBase = String.fromEnvironment('AFK4_API_BASE');

void main() {
  runApp(CustomerApp(directory: OrganizationDirectory(baseUrl: apiBase)));
}
