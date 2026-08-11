import 'package:intl/intl.dart';

/// Деньги на сервере — целое число минорных единиц (дирамы, копейки). Здесь перевод в
/// мажорные единицы, которые видит и вводит человек. Зеркало пакета `@afk4/money`:
/// расходиться этим двум нельзя, иначе одна и та же сумма читается по-разному в вебе и в
/// приложении.
double minorToMajor(int minorUnits) => minorUnits / 100;

/// Обратный перевод с округлением до минорной единицы. Прибавка epsilon — чтобы 1.005,
/// который в IEEE-754 лежит чуть ниже, округлялся вверх, как ждёт человек.
int majorToMinor(double major) => ((major + 1e-9) * 100).round();

/// Короткие знаки валют вместо ISO-кодов: «120,00 с.» читается, «120.00 TJS» — это жаргон.
/// Незнакомая валюта показывается своим кодом.
const Map<String, String> currencySymbols = {
  'TJS': 'с.',
  'USD': r'$',
  'EUR': '€',
  'RUB': '₽',
};

String currencySymbol(String currencyCode) =>
    currencySymbols[currencyCode.toUpperCase()] ?? currencyCode;

/// Сумма для показа: разряды и десятичный разделитель по языку интерфейса, знак валюты после
/// числа. Копейки не срезаются — это кошелёк игрока, а не сводка клуба: увидеть «120 с.»
/// вместо «120,50 с.» значит потерять деньги на экране.
String formatMoney(int minorUnits, String currencyCode, {String locale = 'ru'}) {
  final amount = NumberFormat.decimalPatternDigits(locale: numberLocale(locale), decimalDigits: 2)
      .format(minorToMajor(minorUnits));
  return '$amount ${currencySymbol(currencyCode)}';
}

/// `intl` не знает таджикского и на нём не форматирует, а падает с ошибкой. Откат тот же,
/// что у системных строк Flutter, — русский: аудитория та же, разряды и запятая совпадают.
/// Без этого приложение роняло бы каждую сумму, показанную на таджикском.
String numberLocale(String locale) =>
    Intl.verifiedLocale(locale, NumberFormat.localeExists, onFailure: (_) => 'ru')!;
