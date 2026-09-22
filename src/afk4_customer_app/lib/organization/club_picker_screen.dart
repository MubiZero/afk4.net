import 'dart:async';

import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';

import '../api/contracts.dart';
import '../l10n/app_localizations.dart';
import '../money/money.dart';
import '../shell/load_failure.dart';
import '../theme/brand_mark.dart';
import '../reviews/club_reviews_sheet.dart';
import 'club_card.dart';
import 'club_details_sheet.dart';
import 'club_map.dart';
import 'nearby_location.dart';
import 'organization.dart';
import 'organization_directory.dart';

/// Выбор клуба — первый экран приложения. У мобильной сборки нет поддомена, из которого веб
/// берёт организацию, а войти без неё нельзя: игрок опознаётся парой организация + телефон.
///
/// Это витрина, а не список настроек: клуб выбирают по тому, где он, сколько стоит час и как
/// выглядит зал. Списком удобно сравнивать, картой — понять, что рядом; поэтому и то, и другое.
class ClubPickerScreen extends StatefulWidget {
  const ClubPickerScreen({
    super.key,
    required this.directory,
    required this.onSelected,
    this.myClubs = const [],
    this.selectedOrganizationId,
    this.location = const DeviceNearbyLocation(),
  });

  final OrganizationDirectory directory;
  final ValueChanged<Organization> onSelected;

  /// Клубы, в которых у игрока уже есть счёт. Они идут первыми и со своими деньгами: аккаунт
  /// один на всю сеть, а кошелёк у каждого клуба свой, и это первое, что надо видеть.
  final List<MyClubDto> myClubs;

  /// Клуб, открытый прямо сейчас. Нужен, чтобы не звать переходить туда, где игрок уже есть.
  final String? selectedOrganizationId;

  /// Откуда берётся «рядом со мной». Спрашивается только по нажатию игрока — см.
  /// [NearbyLocation].
  final NearbyLocation location;

  @override
  State<ClubPickerScreen> createState() => _ClubPickerScreenState();
}

sealed class _Load {
  const _Load();
}

class _Loading extends _Load {
  const _Loading();
}

class _Failed extends _Load {
  const _Failed({this.offline = false});

  final bool offline;
}

class _Ready extends _Load {
  const _Ready(this.clubs);

  final List<Organization> clubs;
}

enum _View { list, map }

class _ClubPickerScreenState extends State<ClubPickerScreen> {
  /// Пауза перед запросом: набор «Аре…» иначе шлёт три запроса вместо одного.
  static const Duration _typingPause = Duration(milliseconds: 300);

  _Load _load = const _Loading();
  _View _view = _View.list;
  Timer? _debounce;
  String _query = '';
  int _requestSeq = 0;

  /// Город, по которому сузили витрину. null — все города.
  String? _city;

  /// Где игрок, если он сам попросил показать ближние клубы. Ни на диск, ни на сервер это
  /// не уходит и живёт ровно столько, сколько открыт экран.
  LatLng? _here;
  bool _locating = false;

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<void> _fetch() async {
    // Порядковый номер, а не флаг «идёт загрузка»: медленный ответ на «а» не должен
    // перетереть быстрый ответ на «арена», набранное следом.
    final seq = ++_requestSeq;
    setState(() => _load = const _Loading());
    try {
      final clubs = await widget.directory.search(query: _query);
      if (!mounted || seq != _requestSeq) return;
      setState(() => _load = _Ready(clubs));
    } on OrganizationDirectoryException catch (error) {
      if (!mounted || seq != _requestSeq) return;
      setState(() => _load = _Failed(offline: error.isOffline));
    }
  }

  void _onQueryChanged(String value) {
    _query = value;
    _debounce?.cancel();
    _debounce = Timer(_typingPause, _fetch);
  }

  void _openReviews(Organization club) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => ClubReviewsSheet(directory: widget.directory, club: club),
    );
  }

  void _openDetails(Organization club) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (sheetContext) => ClubDetailsSheet(
        club: club,
        onChoose: () {
          Navigator.of(sheetContext).pop();
          widget.onSelected(club);
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const BrandMark(),
                  const SizedBox(height: 18),
                  Text(
                    l.customerClubPickerTitle,
                    style: theme.textTheme.headlineMedium,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    l.customerClubPickerSubtitle,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    decoration: InputDecoration(
                      labelText: l.customerClubPickerSearch,
                      prefixIcon: const Icon(Icons.search),
                    ),
                    textInputAction: TextInputAction.search,
                    onChanged: _onQueryChanged,
                  ),
                  const SizedBox(height: 12),
                  ..._cityFilter(l),
                  // Список и карта — два взгляда на один и тот же каталог: поиск сверху
                  // относится к обоим, поэтому переключатель стоит под ним, а не над.
                  SegmentedButton<_View>(
                    segments: [
                      ButtonSegment(
                        value: _View.list,
                        icon: const Icon(Icons.view_agenda_outlined),
                        label: Text(l.customerClubPickerTabList),
                      ),
                      ButtonSegment(
                        value: _View.map,
                        icon: const Icon(Icons.map_outlined),
                        label: Text(l.customerClubPickerTabMap),
                      ),
                    ],
                    selected: {_view},
                    showSelectedIcon: false,
                    onSelectionChanged: (selection) =>
                        setState(() => _view = selection.first),
                  ),
                  const SizedBox(height: 12),
                ],
              ),
            ),
            Expanded(child: _buildBody(l)),
          ],
        ),
      ),
    );
  }

  /// Города каталога — как они пришли с сервера, в порядке появления. Их обычно единицы, и
  /// строка чипов честнее выпадающего списка: видно сразу, где вообще есть клубы.
  List<String> get _cities {
    final clubs = switch (_load) {
      _Ready(clubs: final list) => list,
      _ => const <Organization>[],
    };
    final cities = <String>[];
    for (final club in clubs) {
      for (final place in club.places) {
        if (place.city.isNotEmpty && !cities.contains(place.city)) {
          cities.add(place.city);
        }
      }
    }
    return cities;
  }

  /// Есть ли вообще у кого-то координаты: без них «рядом со мной» нечем считать.
  bool get _anyClubHasPoint => switch (_load) {
    _Ready(clubs: final clubs) => clubs.any(
      (club) => club.places.any((place) => place.hasPoint),
    ),
    _ => false,
  };

  /// Спросить местоположение и разложить витрину по близости. Отказ — это ответ игрока, а не
  /// сбой: говорим о нём один раз и возвращаемся к городам.
  Future<void> _findMe(L l) async {
    setState(() => _locating = true);
    final result = await widget.location.current();
    if (!mounted) return;
    setState(() {
      _locating = false;
      _here = result.point;
      if (result.point != null) _city = null;
    });

    if (result.point != null) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          result.outcome == NearbyOutcome.denied
              ? l.customerClubPickerNearbyDenied
              : l.customerClubPickerNearbyUnavailable,
        ),
      ),
    );
  }

  /// Клубы выбранного города. Сеть считается «в городе», если там есть хотя бы один её зал.
  List<Organization> _inCity(List<Organization> clubs) {
    final city = _city;
    if (city == null) return _byDistance(clubs);
    return clubs
        .where((club) => club.places.any((place) => place.city == city))
        .toList();
  }

  /// Ближние первыми — когда игрок сам попросил. Клубы без координат уходят вниз: выдать их за
  /// «рядом» нельзя, а прятать вовсе значит потерять заведения, которые просто не нанесли себя
  /// на карту.
  List<Organization> _byDistance(List<Organization> clubs) {
    final here = _here;
    if (here == null) return clubs;

    final sorted = [...clubs];
    sorted.sort((a, b) {
      final left = _distanceMeters(a);
      final right = _distanceMeters(b);
      if (left == null && right == null) return 0;
      if (left == null) return 1;
      if (right == null) return -1;
      return left.compareTo(right);
    });
    return sorted;
  }

  double? _distanceMeters(Organization club) {
    final here = _here;
    if (here == null) return null;
    return distanceToClubMeters(
      here,
      club.places
          .where((place) => place.hasPoint)
          .map((place) => LatLng(place.latitude!, place.longitude!)),
    );
  }

  /// Первый экран приложения спрашивал «в каком клубе вы играете» и ничем не помогал ответить:
  /// список шёл вперемешку по всей стране. Город — то, что человек знает про себя точно.
  List<Widget> _cityFilter(L l) {
    final cities = _cities;
    if (cities.length < 2 && !_anyClubHasPoint) return const [];

    return [
      SizedBox(
        height: 40,
        child: ListView(
          scrollDirection: Axis.horizontal,
          children: [
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(
                label: Text(l.customerClubPickerAllCities),
                selected: _city == null && _here == null,
                onSelected: (_) => setState(() {
                  _city = null;
                  _here = null;
                }),
              ),
            ),
            // «Рядом со мной» — по нажатию, а не при открытии экрана: доступ к местоположению
            // спрашивают, когда человек сам о нём попросил, и грубый — до района.
            if (_anyClubHasPoint)
              Padding(
                padding: const EdgeInsets.only(right: 8),
                child: ChoiceChip(
                  avatar: _locating
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.near_me_outlined, size: 18),
                  label: Text(l.customerClubPickerNearby),
                  selected: _here != null,
                  onSelected: _locating ? null : (_) => unawaited(_findMe(l)),
                ),
              ),
            // Города — только там, где их больше одного: единственный город выбирать не из чего.
            if (cities.length > 1)
              for (final city in cities)
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: ChoiceChip(
                    label: Text(city),
                    selected: _city == city,
                    // Город и «рядом со мной» — два ответа на один вопрос: выбранное вручную
                    // главнее вычисленного, иначе список менялся бы дважды от одного нажатия.
                    onSelected: (_) => setState(() {
                      _city = city;
                      _here = null;
                    }),
                  ),
                ),
          ],
        ),
      ),
      const SizedBox(height: 12),
    ];
  }

  /// «Ваши клубы» — свои заведения первыми и с деньгами каждого. Показывается только когда
  /// игрок не ищет: в результатах поиска отдельный список поверх найденного сбивает с толку.
  List<Widget> _myClubsSection(L l, List<Organization> catalogue) {
    if (widget.myClubs.isEmpty || _query.trim().isNotEmpty) return const [];

    final rows = <Widget>[];
    for (final mine in widget.myClubs) {
      final club = catalogue
          .where((candidate) => candidate.organizationId == mine.organizationId)
          .firstOrNull;
      // Клуба нет в каталоге — он закрылся или снялся с витрины. Строка, ведущая в никуда,
      // хуже её отсутствия.
      if (club == null) continue;
      rows.add(
        _MyClubRow(
          club: mine,
          here: mine.organizationId == widget.selectedOrganizationId,
          onOpen: () => widget.onSelected(club),
        ),
      );
    }
    if (rows.isEmpty) return const [];

    return [
      _SectionTitle(l.customerClubsMine),
      ...rows,
      const SizedBox(height: 8),
      _SectionTitle(l.customerClubsAll),
    ];
  }

  Widget _buildBody(L l) {
    return switch (_load) {
      _Loading() => const Center(child: CircularProgressIndicator()),
      _Failed(offline: final offline) => offline
          ? LoadFailure.offline(message: l.customerErrorOffline, onRetry: _fetch)
          : LoadFailure(message: l.customerClubPickerError, onRetry: _fetch),
      _Ready(clubs: final clubs) when clubs.isEmpty => _Message(
        text: l.customerClubPickerEmpty,
      ),
      _Ready(clubs: final clubs) when _inCity(clubs).isEmpty => _Message(
        text: l.customerClubPickerEmpty,
        actionLabel: l.customerClubPickerAllCities,
        onAction: () => setState(() => _city = null),
      ),
      _Ready(clubs: final clubs) => switch (_view) {
        _View.list => ListView(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          children: [
            ..._myClubsSection(l, clubs),
            for (final club in _inCity(clubs)) ...[
              ClubCard(
                club: club,
                distanceMeters: _distanceMeters(club),
                onTap: () => widget.onSelected(club),
                onOpenReviews: () => _openReviews(club),
                onOpenDetails: () => _openDetails(club),
              ),
              const SizedBox(height: 14),
            ],
          ],
        ),
        // Карта показывает то же, что список: выбранный город сужает оба, иначе переключение
        // вида молча отменяло бы фильтр.
        _View.map => Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          child: ClubMap(clubs: _inCity(clubs), onSelected: widget.onSelected),
        ),
      },
    };
  }
}

/// Заголовок группы в списке клубов.
class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 10),
    child: Text(text, style: Theme.of(context).textTheme.titleSmall),
  );
}

/// Свой клуб строкой: название, остаток кошелька и переход. Придержанное показывается
/// отдельно — иначе игрок не поймёт, почему остаток меньше, чем он помнит.
class _MyClubRow extends StatelessWidget {
  const _MyClubRow({
    required this.club,
    required this.here,
    required this.onOpen,
  });

  final MyClubDto club;
  final bool here;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: ListTile(
        title: Text(club.organizationName),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              formatMoney(
                club.walletBalanceMinorUnits,
                club.currencyCode,
                locale: locale,
              ),
            ),
            if (club.heldMinorUnits > 0)
              Text(
                '${l.customerWalletHeld}: '
                '${formatMoney(club.heldMinorUnits, club.currencyCode, locale: locale)}',
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
          ],
        ),
        trailing: here
            ? Text(
                l.customerClubsHere,
                style: theme.textTheme.labelLarge?.copyWith(
                  color: theme.colorScheme.primary,
                ),
              )
            : TextButton(onPressed: onOpen, child: Text(l.customerClubsOpen)),
        // Нажимается и текущий клуб — это и есть дорога назад для того, кто передумал
        // переходить.
        onTap: onOpen,
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.text, this.actionLabel, this.onAction});

  final String text;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(text, textAlign: TextAlign.center),
          if (actionLabel != null) ...[
            const SizedBox(height: 12),
            FilledButton(onPressed: onAction, child: Text(actionLabel!)),
          ],
        ],
      ),
    );
  }
}
