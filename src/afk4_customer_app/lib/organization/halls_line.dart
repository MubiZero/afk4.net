import '../l10n/app_localizations.dart';
import 'organization.dart';

/// Строка «где этот клуб» под его названием — одна на карточку в витрине и на лист подробностей.
///
/// У клуба с единственным залом это его адрес: зал и есть клуб. У сети — сколько в ней залов и
/// в каких городах. Адрес одного зала на этом месте выдавал бы себя за адрес всей сети: игрок
/// поехал бы по нему, а сидеть собирался в другом конце города.
///
/// Пусто — клуб не назвал ни одного зала: тогда строки нет вовсе, а не пустая полоска.
String hallsLine(L l, List<ClubPlace> halls) {
  if (halls.isEmpty) return '';
  if (halls.length == 1) return halls.single.fullAddress;

  final cities = <String>[];
  for (final hall in halls) {
    if (hall.city.isNotEmpty && !cities.contains(hall.city)) cities.add(hall.city);
  }
  return l.customerClubDetailsHallsIn(halls.length, cities.join(', '));
}
