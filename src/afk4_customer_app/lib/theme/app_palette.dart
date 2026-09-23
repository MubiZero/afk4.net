import 'package:flutter/material.dart';

/// Цвета со смыслом, которых нет в `ColorScheme`.
///
/// `ColorScheme` знает «акцент» и «ошибку», но не знает «всё в порядке», «деньги прибыли»,
/// «звёзды оценки» и «подпись поверх фото». Раньше каждый экран подбирал их числом по месту —
/// и число работало ровно на том фоне, где его подобрали: фирменный зелёный, читаемый на почти
/// чёрном, на белом листе светлой темы выцветал до 2:1, а красный цвет клуба красил приход
/// денег в выписке тем же красным, что и ошибку.
///
/// Поля названы по назначению, а не по цвету: у «успеха» и «прихода» сегодня одно значение,
/// но это совпадение, а не правило, и менять их будут по разным причинам.
@immutable
class AppPalette extends ThemeExtension<AppPalette> {
  const AppPalette({
    required this.success,
    required this.income,
    required this.rating,
    required this.onMedia,
    required this.onMediaMuted,
    required this.mediaScrim,
    required this.ratingOnMedia,
  });

  /// «Всё в порядке»: идёт сессия и до конца далеко, пакет действует. Пара к
  /// `colorScheme.error`, поэтому не следует цвету клуба: красный логотип не должен делать
  /// спокойное состояние похожим на тревогу.
  final Color success;

  /// «Деньги прибыли»: пополнение в выписке, начисленный кешбэк, бонусные часы.
  final Color income;

  /// Закрашенная звезда оценки на поверхности темы.
  final Color rating;

  /// Текст и знаки поверх фото зала и карты. Кадр одинаков в обеих темах, и подпись поверх
  /// него тоже одна: «текст темы» в светлой стал бы чёрным на затемнённом фото.
  final Color onMedia;

  /// Второстепенная подпись поверх фото.
  final Color onMediaMuted;

  /// Затемнение под подписями на фото: без него белое название клуба теряется на светлом кадре.
  final Color mediaScrim;

  /// Звезда оценки поверх фото — на затемнении, а не на поверхности темы.
  final Color ratingOnMedia;

  static const Color _white = Color(0xFFFFFFFF);

  static const AppPalette dark = AppPalette(
    success: Color(0xFF2CC592),
    income: Color(0xFF2CC592),
    rating: Color(0xFFFFC53D),
    onMedia: _white,
    onMediaMuted: Color(0xB3FFFFFF),
    mediaScrim: Color(0xCC000000),
    ratingOnMedia: Color(0xFFFFC53D),
  );

  /// Светлые значения темнее тёмных: это те же смыслы, подобранные под белый лист, а не
  /// другой набор цветов.
  static const AppPalette light = AppPalette(
    success: Color(0xFF067A59),
    income: Color(0xFF067A59),
    rating: Color(0xFFB7791F),
    onMedia: _white,
    onMediaMuted: Color(0xB3FFFFFF),
    mediaScrim: Color(0xCC000000),
    ratingOnMedia: Color(0xFFFFC53D),
  );

  /// Палитра для темы, собранной без неё, — например в тестовом `MaterialApp` без темы
  /// приложения. Смысл цвета не должен зависеть от того, кто собирал `ThemeData`.
  static AppPalette fallbackFor(Brightness brightness) =>
      brightness == Brightness.dark ? dark : light;

  static AppPalette of(BuildContext context) {
    final theme = Theme.of(context);
    return theme.extension<AppPalette>() ?? fallbackFor(theme.brightness);
  }

  @override
  AppPalette copyWith({
    Color? success,
    Color? income,
    Color? rating,
    Color? onMedia,
    Color? onMediaMuted,
    Color? mediaScrim,
    Color? ratingOnMedia,
  }) =>
      AppPalette(
        success: success ?? this.success,
        income: income ?? this.income,
        rating: rating ?? this.rating,
        onMedia: onMedia ?? this.onMedia,
        onMediaMuted: onMediaMuted ?? this.onMediaMuted,
        mediaScrim: mediaScrim ?? this.mediaScrim,
        ratingOnMedia: ratingOnMedia ?? this.ratingOnMedia,
      );

  @override
  AppPalette lerp(AppPalette? other, double t) {
    if (other == null) return this;
    return AppPalette(
      success: Color.lerp(success, other.success, t)!,
      income: Color.lerp(income, other.income, t)!,
      rating: Color.lerp(rating, other.rating, t)!,
      onMedia: Color.lerp(onMedia, other.onMedia, t)!,
      onMediaMuted: Color.lerp(onMediaMuted, other.onMediaMuted, t)!,
      mediaScrim: Color.lerp(mediaScrim, other.mediaScrim, t)!,
      ratingOnMedia: Color.lerp(ratingOnMedia, other.ratingOnMedia, t)!,
    );
  }

  @override
  bool operator ==(Object other) =>
      other is AppPalette &&
      other.success == success &&
      other.income == income &&
      other.rating == rating &&
      other.onMedia == onMedia &&
      other.onMediaMuted == onMediaMuted &&
      other.mediaScrim == mediaScrim &&
      other.ratingOnMedia == ratingOnMedia;

  @override
  int get hashCode =>
      Object.hash(success, income, rating, onMedia, onMediaMuted, mediaScrim, ratingOnMedia);
}
