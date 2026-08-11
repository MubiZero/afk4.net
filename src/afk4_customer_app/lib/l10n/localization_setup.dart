import 'package:flutter/widgets.dart';

import 'app_localizations.dart';
import 'tajik_fallback_delegates.dart';

/// Единственная точка, где собирается список делегатов локализации. И приложение, и тесты
/// берут его отсюда: иначе тест легко проходит на наборе, которого в приложении нет.
const List<LocalizationsDelegate<dynamic>> appLocalizationsDelegates = [
  ...tajikFallbackDelegates,
  ...L.localizationsDelegates,
];

const List<Locale> appSupportedLocales = L.supportedLocales;
