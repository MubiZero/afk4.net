import 'dart:ui';

import 'package:flutter/material.dart';

/// Шапка раздела: крупный заголовок, который сжимается при прокрутке.
///
/// Раньше каждый экран носил свою `AppBar` фиксированной высоты, и на главной 76 точек
/// уходило на приветствие, которое не исчезало никогда. Крупный заголовок при открытии и
/// компактный при прокрутке — то, как устроены современные мобильные приложения: заголовок
/// заявляет раздел, а на середине списка отдаёт место содержимому, не пропадая совсем.
///
/// Одна шапка на все разделы, а не повторённая вручную вёрстка: иначе они разъедутся по
/// высоте и отступам на второй же правке.
SliverAppBar appHeader(
  BuildContext context, {
  required String title,
  String? eyebrow,
  String? place,
  String? placeLogoUrl,
  List<Widget>? actions,
  TabBar? tabs,
}) {
  final theme = Theme.of(context);
  final tabsHeight = tabs == null ? 0.0 : tabs.preferredSize.height;

  return SliverAppBar(
    pinned: true,
    // Раскрытая высота считается от строк, которые в ней стоят: пустая полоса над текстом
    // выглядит как недогрузившийся экран, а не как воздух.
    expandedHeight: 104 + (eyebrow == null ? 0 : 20) + (place == null ? 0 : 22) + tabsHeight,
    actions: actions,
    bottom: tabs,
    // Свёрнутая шапка полупрозрачна и размывает то, что уходит под неё. Непрозрачная
    // заливка перекрыла бы свет зала и дала шов поперёк экрана, а совсем прозрачная —
    // накладывала бы заголовок прямо на карточки.
    flexibleSpace: ClipRect(
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 18, sigmaY: 18),
        child: DecoratedBox(
          decoration: BoxDecoration(color: theme.canvasColor.withValues(alpha: 0.55)),
          child: FlexibleSpaceBar(
            titlePadding: EdgeInsetsDirectional.only(start: 20, end: 20, bottom: 14 + tabsHeight),
            title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
            background: eyebrow == null && place == null
                ? null
                : SafeArea(
                    child: Align(
                      alignment: Alignment.bottomLeft,
                      child: Padding(
                        padding: EdgeInsets.only(left: 20, right: 20, bottom: 50 + tabsHeight),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            // Где игрок находится. Клубов у сети несколько, а узнать, в какой
                            // ты вошёл, можно было только через профиль.
                            if (place != null) ...[
                              Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  _PlaceMark(logoUrl: placeLogoUrl),
                                  const SizedBox(width: 6),
                                  Text(
                                    place,
                                    style: theme.textTheme.labelMedium?.copyWith(
                                      color: theme.colorScheme.primary,
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 4),
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
      ),
    ),
  );
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
  });

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
        ),
        ...slivers,
        // Хвост под последней карточкой: без него нижний край содержимого упирается в
        // панель разделов и читается как обрезанный.
        const SliverToBoxAdapter(child: SizedBox(height: 24)),
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
const EdgeInsets sectionPadding = EdgeInsets.fromLTRB(16, 8, 16, 0);
