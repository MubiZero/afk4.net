import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

/// Таджикского нет среди локалей Flutter: системные строки — кнопки диалогов, подписи полей,
/// названия месяцев в выборе даты — для `tg` не существуют, и приложение падает предупреждением
/// «delegate that supports the tg locale was not found».
///
/// Наши собственные строки при этом переведены по-настоящему (см. guard-тест каталога против
/// tg===ru). Недостающий системный слой берём с русского — тем же откатом `tg → ru`, что уже
/// принят в вебе. Практическое следствие: в таджикском интерфейсе кнопка «Отмена» в системном
/// диалоге будет русской. Это видимый компромисс, а не недосмотр; уйдёт, когда Flutter завезёт
/// таджикскую локаль.
const Locale _systemFallback = Locale('ru');

class _TajikMaterialDelegate extends LocalizationsDelegate<MaterialLocalizations> {
  const _TajikMaterialDelegate();

  @override
  bool isSupported(Locale locale) => locale.languageCode == 'tg';

  @override
  Future<MaterialLocalizations> load(Locale locale) =>
      GlobalMaterialLocalizations.delegate.load(_systemFallback);

  @override
  bool shouldReload(_) => false;
}

class _TajikCupertinoDelegate extends LocalizationsDelegate<CupertinoLocalizations> {
  const _TajikCupertinoDelegate();

  @override
  bool isSupported(Locale locale) => locale.languageCode == 'tg';

  @override
  Future<CupertinoLocalizations> load(Locale locale) =>
      GlobalCupertinoLocalizations.delegate.load(_systemFallback);

  @override
  bool shouldReload(_) => false;
}

class _TajikWidgetsDelegate extends LocalizationsDelegate<WidgetsLocalizations> {
  const _TajikWidgetsDelegate();

  @override
  bool isSupported(Locale locale) => locale.languageCode == 'tg';

  @override
  Future<WidgetsLocalizations> load(Locale locale) =>
      GlobalWidgetsLocalizations.delegate.load(_systemFallback);

  @override
  bool shouldReload(_) => false;
}

/// Ставится ПЕРЕД глобальными делегатами: Flutter берёт первый подходящий, а глобальные
/// на `tg` не отзовутся вовсе.
const List<LocalizationsDelegate<dynamic>> tajikFallbackDelegates = [
  _TajikMaterialDelegate(),
  _TajikCupertinoDelegate(),
  _TajikWidgetsDelegate(),
];
