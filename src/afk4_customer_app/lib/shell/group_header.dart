import 'package:flutter/material.dart';

import '../theme/space.dart';

/// Заголовок группы в списке или форме: «Друзья», «Прошлые заказы», «Тариф».
///
/// Группы подписывались то `titleSmall`, то `titleMedium`, и соседние экраны расходились в
/// иерархии: на одном заголовок группы был крупнее строк под ним, на другом — мельче.
class GroupHeader extends StatelessWidget {
  const GroupHeader(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: Space.s2),
        child: Semantics(
          header: true,
          child: Text(text, style: Theme.of(context).textTheme.titleMedium),
        ),
      );
}
