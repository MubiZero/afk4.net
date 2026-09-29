/// Шкала отступов приложения — шаг 4.
///
/// Раньше отступы стояли литералами от 2 до 32, и соседние экраны расходились на 2–6 точек там,
/// где глаз ждал одинакового: у листа 16 или 20, между кнопками 6, 8 или 12. Шкала оставляет
/// выбор из немногих значений, и одинаковое становится одинаковым само.
abstract final class Space {
  static const double s1 = 4;
  static const double s2 = 8;
  static const double s3 = 12;
  static const double s4 = 16;
  static const double s5 = 20;
  static const double s6 = 24;
  static const double s8 = 32;

  /// Поле экрана по бокам.
  static const double screen = s4;

  /// Поле листа снизу: лист у́же экрана не бывает, и ему нужно больше воздуха по краям, чтобы
  /// не читаться продолжением экрана под ним.
  static const double sheet = s5;
}
