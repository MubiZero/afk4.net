import 'package:flutter/material.dart';

import '../theme/space.dart';

/// Открыть лист снизу. Одни и те же три настройки стояли у каждого вызова, и там, где одну
/// забывали, лист либо не поднимался над клавиатурой, либо заезжал под вырез экрана.
Future<T?> showAppSheet<T>(BuildContext context, WidgetBuilder builder) => showModalBottomSheet<T>(
  context: context,
  isScrollControlled: true,
  useSafeArea: true,
  builder: builder,
);

/// Лист снизу: заголовок, содержимое и прибитые к низу действия.
///
/// Листы собирались каждый по-своему: поля 16 или 20, заголовок тремя стилями, кнопка то в
/// конце прокрутки, то у низа, и только у одного был отступ от жестовой полосы Android. Ошибка
/// пополнения при этом всплывала тостом под самим листом — её закрывал лист. Здесь всё это
/// решено один раз:
/// - поле [Space.sheet] по бокам, заголовок `titleLarge`;
/// - содержимое прокручивается, [actions] стоят ниже прокрутки и поднимаются над клавиатурой;
/// - ошибка — текстом над кнопками ([ActionStack.error]), в самом листе.
class AppSheet extends StatelessWidget {
  const AppSheet({super.key, required this.title, required this.content, this.actions});

  final String title;
  final List<Widget> content;

  /// Обычно [ActionStack]; ошибку листа передают через неё же.
  final Widget? actions;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      // Клавиатура не должна закрывать поле ввода и кнопку.
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      // Низ листа — над жестовой полосой телефона, с кнопками и без них.
      child: SafeArea(
        top: false,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Flexible(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(Space.sheet, 0, Space.sheet, Space.s4),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(title, style: theme.textTheme.titleLarge),
                    const SizedBox(height: Space.s4),
                    ...content,
                  ],
                ),
              ),
            ),
            if (actions case final actions?)
              Padding(
                padding: const EdgeInsets.fromLTRB(Space.sheet, 0, Space.sheet, Space.s4),
                child: actions,
              ),
          ],
        ),
      ),
    );
  }
}
