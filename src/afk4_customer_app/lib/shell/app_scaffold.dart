import 'dart:ui';

import 'package:flutter/material.dart';
import '../theme/space.dart';

/// Шапка раздела: крупный заголовок, который сжимается при прокрутке.
///
/// Раньше каждый экран носил свою `AppBar` фиксированной высоты, и на главной 76 точек
/// уходило на приветствие, которое не исчезало никогда. Крупный заголовок при открытии и
/// компактный при прокрутке — то, как устроены современные мобильные приложения: заголовок
/// заявляет раздел, а на середине списка отдаёт место содержимому, не пропадая совсем.
///
/// [compact] — шапка вложенного экрана (Бар, Пакеты, Уведомления): тот же вид, что у свёрнутой
/// шапки раздела, — фон, шрифт, строка клуба, — только без раскрытой части. Раньше вложенные
/// носили стандартную `AppBar` со своим размером заголовка, а Уведомления и Стаж — крупную
/// шапку раздела, и понять по шапке, где ты — в разделе или на экране внутри него, было нельзя.
///
/// Одна шапка на все экраны, а не повторённая вручную вёрстка: иначе они разъедутся по
/// высоте и отступам на второй же правке.
SliverAppBar appHeader(
  BuildContext context, {
  required String title,
  String? eyebrow,
  String? place,
  String? placeLogoUrl,
  List<Widget>? actions,
  TabBar? tabs,
  bool compact = false,
}) {
  final theme = Theme.of(context);
  final tabsHeight = tabs == null ? 0.0 : tabs.preferredSize.height;

  if (compact) {
    return SliverAppBar(
      pinned: true,
      centerTitle: false,
      toolbarHeight: _compactHeight(place),
      actions: actions,
      bottom: tabs,
      title: _CompactTitle(title: title, place: place, placeLogoUrl: placeLogoUrl),
      flexibleSpace: const _HeaderBackground(),
    );
  }

  return SliverAppBar(
    pinned: true,
    // Раскрытая высота считается от строк, которые в ней стоят: пустая полоса над текстом
    // выглядит как недогрузившийся экран, а не как воздух.
    expandedHeight: 104 + (eyebrow == null ? 0 : 20) + (place == null ? 0 : 22) + tabsHeight,
    actions: actions,
    bottom: tabs,
    flexibleSpace: _HeaderBackground(
      child: FlexibleSpaceBar(
        // Заголовок у левого края на любой платформе: по центру он стоял только там, где
        // Flutter решал за нас (iOS, macOS), и шапки одного приложения расходились.
        centerTitle: false,
        titlePadding: EdgeInsetsDirectional.only(
          start: Space.s5,
          end: Space.s5,
          bottom: 14 + tabsHeight,
        ),
        title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
        background: eyebrow == null && place == null
            ? null
            : SafeArea(
                child: Align(
                  alignment: Alignment.bottomLeft,
                  child: Padding(
                    padding: EdgeInsets.only(left: Space.s5, right: Space.s5, bottom: 50 + tabsHeight),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // Где игрок находится. Клубов у сети несколько, а узнать, в какой
                        // ты вошёл, можно было только через профиль.
                        if (place != null) ...[
                          _PlaceLine(place: place, logoUrl: placeLogoUrl),
                          const SizedBox(height: Space.s1),
                        ],
                        if (eyebrow != null)
                          Text(
                            eyebrow,
                            style: theme.textTheme.bodySmall?.copyWith(
                              color: theme.colorScheme.onSurfaceVariant,
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              ),
      ),
    ),
  );
}

/// Шапка вложенного экрана для `Scaffold.appBar` — та же, что `appHeader(compact: true)`.
AppBar nestedAppBar(
  BuildContext context, {
  required String title,
  String? place,
  String? placeLogoUrl,
  List<Widget>? actions,
}) =>
    AppBar(
      centerTitle: false,
      toolbarHeight: _compactHeight(place),
      actions: actions,
      title: _CompactTitle(title: title, place: place, placeLogoUrl: placeLogoUrl),
      flexibleSpace: const _HeaderBackground(),
    );

/// Строка клуба над заголовком добавляет высоты, а не сжимает заголовок.
double _compactHeight(String? place) => place == null ? kToolbarHeight : kToolbarHeight + 18;

/// Свёрнутая шапка полупрозрачна и размывает то, что уходит под неё. Непрозрачная заливка
/// перекрыла бы свет зала и дала шов поперёк экрана, а совсем прозрачная — накладывала бы
/// заголовок прямо на карточки.
class _HeaderBackground extends StatelessWidget {
  const _HeaderBackground({this.child});

  final Widget? child;

  @override
  Widget build(BuildContext context) => ClipRect(
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 18, sigmaY: 18),
          child: DecoratedBox(
            decoration: BoxDecoration(color: Theme.of(context).canvasColor.withValues(alpha: 0.55)),
            child: child ?? const SizedBox.expand(),
          ),
        ),
      );
}

/// Заголовок компактной шапки: строка клуба и название экрана, тем же шрифтом, что у свёрнутой
/// шапки раздела.
class _CompactTitle extends StatelessWidget {
  const _CompactTitle({required this.title, this.place, this.placeLogoUrl});

  final String title;
  final String? place;
  final String? placeLogoUrl;

  @override
  Widget build(BuildContext context) {
    final text = Text(
      title,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: Theme.of(context).textTheme.titleLarge,
    );
    final where = place;
    if (where == null) return text;
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _PlaceLine(place: where, logoUrl: placeLogoUrl),
        text,
      ],
    );
  }
}

/// Клуб, в котором игрок сейчас: знак и название.
class _PlaceLine extends StatelessWidget {
  const _PlaceLine({required this.place, this.logoUrl});

  final String place;
  final String? logoUrl;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        _PlaceMark(logoUrl: logoUrl),
        const SizedBox(width: Space.s2),
        Flexible(
          child: Text(
            place,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.labelMedium?.copyWith(color: theme.colorScheme.primary),
          ),
        ),
      ],
    );
  }
}

/// Знак клуба перед его названием. Логотип клуб задаёт сам; пока он не загрузился или не
/// задан вовсе, на его месте стоит значок места — дырки в строке не остаётся.
class _PlaceMark extends StatelessWidget {
  const _PlaceMark({required this.logoUrl});

  final String? logoUrl;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final fallback = Icon(Icons.place_outlined, size: 14, color: theme.colorScheme.primary);
    final url = logoUrl;
    if (url == null || url.isEmpty) return fallback;

    return ClipRRect(
      borderRadius: BorderRadius.circular(4),
      child: Image.network(
        url,
        width: 16,
        height: 16,
        fit: BoxFit.cover,
        // Не загрузившийся логотип не должен оставлять пустоту вместо строки.
        errorBuilder: (_, _, _) => fallback,
      ),
    );
  }
}

/// Каркас раздела: шапка выше, прокручиваемое содержимое под ней.
class AppScaffold extends StatelessWidget {
  const AppScaffold({
    super.key,
    required this.title,
    required this.slivers,
    this.eyebrow,
    this.place,
    this.placeLogoUrl,
    this.actions,
    this.onRefresh,
    this.floatingActionButton,
    this.compact = false,
  });

  /// Вложенный экран: шапка без раскрытой части — см. [appHeader].
  final bool compact;

  final String title;

  /// Надстрочник над заголовком: к кому обращаемся. Виден только у раскрытой шапки — при
  /// прокрутке он уходит первым, как менее важный из двух.
  final String? eyebrow;

  /// Клуб, в котором игрок сейчас. Стоит над заголовком и уходит при прокрутке.
  final String? place;

  /// Логотип этого клуба — его владелец задаёт на сеть.
  final String? placeLogoUrl;

  final List<Widget>? actions;
  final Future<void> Function()? onRefresh;
  final Widget? floatingActionButton;

  final List<Widget> slivers;

  @override
  Widget build(BuildContext context) {
    final scroll = CustomScrollView(
      // Тянуть вниз можно и на коротком списке: жест обновления не должен зависеть от того,
      // сколько сегодня карточек.
      physics: const AlwaysScrollableScrollPhysics(),
      slivers: [
        appHeader(
          context,
          title: title,
          eyebrow: eyebrow,
          place: place,
          placeLogoUrl: placeLogoUrl,
          actions: actions,
          compact: compact,
        ),
        ...slivers,
        // Хвост под последней карточкой: без него нижний край содержимого упирается в
        // панель разделов и читается как обрезанный.
        const SliverToBoxAdapter(child: SizedBox(height: Space.s6)),
      ],
    );

    return Scaffold(
      floatingActionButton: floatingActionButton,
      body: onRefresh == null ? scroll : RefreshIndicator(onRefresh: onRefresh!, child: scroll),
    );
  }
}

/// Отступы содержимого раздела. Одно значение на все экраны: разные поля у соседних вкладок
/// заметны при переключении сильнее, чем кажется на макете.
const EdgeInsets sectionPadding = EdgeInsets.fromLTRB(Space.s4, Space.s2, Space.s4, 0);
