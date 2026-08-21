import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../api/dto.dart';
import '../api/player_api_client.dart';
import '../format/date_time.dart';
import '../organization/branch_choice.dart';
import '../money/money.dart';
import '../l10n/app_localizations.dart';
import '../phone/phone_verification_sheet.dart';
import '../shell/app_scaffold.dart';
import 'new_reservation_sheet.dart';

/// Что не так со временем — до отправки. Сервер проверяет то же самое, но отвечает общей
/// ошибкой, а игроку нужно знать, какое из двух полей чинить. null — всё в порядке.
String? reservationTimeProblem(L l, DateTime? start, DateTime? end, {required DateTime now}) {
  if (start == null || end == null) return l.customerReservationsTimeError;
  if (!start.isAfter(now)) return l.customerReservationsStartInPast;
  if (!end.isAfter(start)) return l.customerReservationsEndBeforeStart;
  return null;
}

/// Брони игрока: что уже забронировано и как забронировать ещё.
class ReservationsScreen extends StatefulWidget {
  const ReservationsScreen({
    super.key,
    required this.api,
    required this.phoneVerified,
    this.accountOpen = true,
    this.branch = const BranchChoice(),
    this.onPhoneVerified,
    this.onAccountOpened,
    this.clock = DateTime.now,
  });

  final PlayerApiClient api;
  final bool phoneVerified;

  /// Есть ли у игрока счёт в этом клубе. Пока нет, броней тоже нет — спрашивать не о чем,
  /// но забронировать можно: этой самой бронью счёт и открывается.
  final bool accountOpen;

  /// Зал, в который придёт игрок. Нужен первой брони в сети из нескольких залов: ею
  /// открывается счёт, и сервер не гадает, в каком зале его завести.
  final BranchChoice branch;

  /// Номер подтвердили прямо из гейта — оболочке пора считать игрока подтверждённым.
  final VoidCallback? onPhoneVerified;

  /// Бронь состоялась в клубе, где счёта не было, — оболочке пора перечитать клубы.
  final Future<void> Function()? onAccountOpened;
  final DateTime Function() clock;

  @override
  State<ReservationsScreen> createState() => _ReservationsScreenState();
}

enum _Load { loading, failed, ready }

class _ReservationsScreenState extends State<ReservationsScreen> {
  _Load _state = _Load.loading;
  List<PlayerReservation> _reservations = const [];

  /// Как часто перерисовывается обратный отсчёт у заявки, ждущей ответа. Минута — шаг, в
  /// котором он и показан; чаще незачем, реже — цифра застынет на глазах.
  static const Duration _countdownTick = Duration(seconds: 20);

  Timer? _countdown;

  @override
  void initState() {
    super.initState();
    // В клубе, который игрока ещё не знает, броней заведомо нет — и спрашивать о них нечего.
    // Вечный спиннер на их месте выглядел бы как навсегда зависшая загрузка.
    if (widget.accountOpen) {
      _refresh();
    } else {
      _state = _Load.ready;
    }
  }

  @override
  void didUpdateWidget(ReservationsScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.accountOpen && !oldWidget.accountOpen) _refresh();
  }

  @override
  void dispose() {
    _countdown?.cancel();
    super.dispose();
  }

  /// Отсчёт тикает, только пока есть чему тикать: у экрана без заявок в ожидании таймера нет.
  void _syncCountdown() {
    final waiting = _reservations.any((r) => r.state == 'pending' && r.respondByUtc != null);
    if (waiting && _countdown == null) {
      _countdown = Timer.periodic(_countdownTick, (_) {
        if (mounted) setState(() {});
      });
    } else if (!waiting) {
      _countdown?.cancel();
      _countdown = null;
    }
  }

  Future<void> _refresh() async {
    setState(() => _state = _state == _Load.ready ? _Load.ready : _Load.loading);
    try {
      final reservations = await widget.api.getReservations();
      if (!mounted) return;
      setState(() {
        _reservations = reservations;
        _state = _Load.ready;
      });
      _syncCountdown();
    } on PlayerApiException catch (error) {
      // Счёта в клубе ещё нет — значит и броней нет. Это пустой раздел, а не сбой.
      if (error.isNoAccountInClub) {
        if (mounted) {
          setState(() {
            _reservations = const [];
            _state = _Load.ready;
          });
        }
        return;
      }
      if (!mounted) return;
      // Пустой список вместо ошибки — враньё: «броней нет» и «мы их не увидели» это разные
      // вещи, и на первом игрок спокойно уйдёт мимо своей брони.
      setState(() => _state = _Load.failed);
    }
  }

  /// Форма живёт в листе снизу: раздел открывают, чтобы посмотреть свои брони, а не
  /// бронировать заново — развёрнутая форма занимала первый экран у всех.
  Future<void> _openForm() async {
    final l = L.of(context);
    final created = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => NewReservationSheet(
        api: widget.api,
        clock: widget.clock,
        accountOpen: widget.accountOpen,
        branch: widget.branch,
      ),
    );
    if (created != true || !mounted) return;

    _say(l.customerReservationsCreated);
    // Первая бронь в незнакомом клубе открывает счёт — оболочка узнаёт об этом только так.
    await widget.onAccountOpened?.call();
    if (!mounted) return;
    await _refresh();
  }

  Future<void> _cancel(ReservationEntry entry) async {
    final l = L.of(context);
    final locale = Localizations.localeOf(context).languageCode;
    final reservation = entry.first;
    final confirmed = await showDialog<bool>(
      context: context,
      // Обе кнопки называют своё действие целиком. Пара «Отменить» / «Назад» читалась как
      // два способа закрыть диалог, и промах стоил игроку брони.
      builder: (context) => AlertDialog(
        title: Text(l.customerReservationsCancelConfirm),
        // Какую именно бронь отменяем: при двух бронях безымянный вопрос ничего не значит.
        content: Text(
          '${entry.isCompany ? l.customerReservationsCompanySeats(entry.seatCount) : reservation.seatName ?? l.customerReservationsNoSeat}\n'
          '${formatTimeRange(l, reservation.startsAtUtc, reservation.endsAtUtc, locale, now: widget.clock())}',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l.customerReservationsCancelKeep),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(context).colorScheme.error,
              foregroundColor: Theme.of(context).colorScheme.onError,
            ),
            child: Text(l.customerReservationsCancelAction),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      // Компания отменяется одним запросом: четыре отдельных отмены — это четыре шанса
      // оборваться на полпути и оставить часть денег замороженной.
      if (entry.isCompany && entry.groupId != null) {
        await widget.api.cancelReservationGroup(entry.groupId!);
      } else {
        await widget.api.cancelReservation(reservation.reservationId);
      }
      if (!mounted) return;
      _say(l.customerReservationsCancelled);
      await _refresh();
    } on PlayerApiException {
      if (mounted) _say(l.customerReservationsCancelError);
    }
  }

  void _say(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);

    return AppScaffold(
      title: l.customerReservationsTitle,
      // Бронировать можно только с подтверждённым телефоном; без него кнопка не появляется,
      // а объяснение стоит на месте списка.
      floatingActionButton: widget.phoneVerified
          ? FloatingActionButton.extended(
              onPressed: _openForm,
              icon: const Icon(Icons.add),
              label: Text(l.customerReservationsCreate),
            )
          : null,
      // Потянуть вниз обновляет список — тот же жест, что на главной и в кошельке.
      onRefresh: _refresh,
      slivers: [
        SliverPadding(
          padding: sectionPadding,
          sliver: SliverList.list(
            children: [
            if (!widget.phoneVerified) ...[
              _gate(l, theme),
              const SizedBox(height: 16),
            ],
            switch (_state) {
              _Load.loading => Semantics(
                  label: l.a11yLoadingReservations,
                  child: const Center(child: Padding(
                    padding: EdgeInsets.all(32),
                    child: CircularProgressIndicator(),
                  )),
                ),
              _Load.failed => Center(
                  child: Column(
                    children: [
                      Text(l.customerReservationsLoadError,
                          style: TextStyle(color: theme.colorScheme.error)),
                      const SizedBox(height: 8),
                      TextButton(onPressed: _refresh, child: Text(l.customerCommonRetry)),
                    ],
                  ),
                ),
              // Пустой раздел объясняет, зачем он нужен, а не сообщает о пустоте: серая
              // строка «броней пока нет» не отвечает на вопрос, что здесь делать.
              _Load.ready when _reservations.isEmpty => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 48, horizontal: 24),
                  child: Column(
                    children: [
                      Icon(Icons.event_available_outlined,
                          size: 40, color: theme.colorScheme.onSurfaceVariant),
                      const SizedBox(height: 12),
                      Text(l.customerReservationsNone, style: theme.textTheme.titleMedium),
                      const SizedBox(height: 4),
                      Text(
                        l.customerReservationsNoneHint,
                        textAlign: TextAlign.center,
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
              _Load.ready => Column(
                  children: [
                    for (final entry in groupReservations(_reservations))
                      Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: _ReservationCard(
                          entry: entry,
                          now: widget.clock(),
                          onCancel: () => _cancel(entry),
                        ),
                      ),
                  ],
                ),
            },
              // Плавающая кнопка «Забронировать» перекрывает последнюю карточку списка —
              // место под ней освобождается заранее.
              const SizedBox(height: 72),
            ],
          ),
        ),
      ],
    );
  }

  /// Гейт с выходом: объяснение и кнопка, а не тупик с отсылкой к администратору.
  Widget _gate(L l, ThemeData theme) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                l.customerReservationsGate,
                style:
                    theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: _verifyPhone,
                child: Text(l.customerWalletGateAction),
              ),
            ],
          ),
        ),
      );

  Future<void> _verifyPhone() async {
    final l = L.of(context);
    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => PhoneVerificationSheet(api: widget.api),
    );
    if (confirmed != true || !mounted) return;

    _say(l.customerPhoneDone);
    widget.onPhoneVerified?.call();
  }
}

/// Строка списка: обычная бронь или целая компания.
///
/// Компания приходит с сервера несколькими бронями с общим идентификатором группы. Показывать их
/// отдельными строками значит показать четыре одинаковых карточки, между которыми не видно
/// разницы, — игрок не поймёт, четыре у него брони или одна на четверых.
class ReservationEntry {
  ReservationEntry(this.reservations);

  final List<PlayerReservation> reservations;

  bool get isCompany => reservations.length > 1;

  PlayerReservation get first => reservations.first;

  String? get groupId => first.reservationGroupId;

  /// Места, которые ещё в силе. Отменённые из счёта уходят: «4 места» рядом с двумя
  /// отменёнными — это неправда о брони.
  List<PlayerReservation> get live =>
      reservations
          .where((r) =>
              r.state != 'cancelled' && r.state != 'no_show' && r.state != 'rejected')
          .toList();

  int get seatCount => live.isNotEmpty ? live.length : reservations.length;

  /// Состояние компании — состояние её живых мест. Когда живых не осталось, компания кончилась
  /// тем же, чем кончились её места: подставлять сюда «отменена» значит называть отменой и
  /// неявку, за которую человек мог заплатить.
  String get state => live.isNotEmpty ? live.first.state : first.state;

  bool get isCancellable => live.any((r) => r.isCancellable);

  /// Сумма по всем живым местам. null — бронь без тарифа, её считают на стойке.
  int? get totalMinorUnits {
    final priced = live.where((r) => r.estimatedCostMinorUnits != null).toList();
    if (priced.isEmpty) return null;
    return priced.fold<int>(0, (sum, r) => sum + r.estimatedCostMinorUnits!);
  }

  String? get currencyCode => live.isNotEmpty ? live.first.currencyCode : first.currencyCode;
}

/// Собирает брони в строки списка: компании — под своим идентификатором группы, одиночные —
/// сами по себе. Порядок сервера сохраняется: он уже отсортирован по времени.
List<ReservationEntry> groupReservations(List<PlayerReservation> reservations) {
  final entries = <ReservationEntry>[];
  final byGroup = <String, ReservationEntry>{};

  for (final reservation in reservations) {
    final groupId = reservation.reservationGroupId;
    if (groupId == null) {
      entries.add(ReservationEntry([reservation]));
      continue;
    }

    final existing = byGroup[groupId];
    if (existing == null) {
      final entry = ReservationEntry([reservation]);
      byGroup[groupId] = entry;
      entries.add(entry);
    } else {
      existing.reservations.add(reservation);
    }
  }

  return entries;
}

class _ReservationCard extends StatelessWidget {
  const _ReservationCard({
    required this.entry,
    required this.now,
    required this.onCancel,
  });

  final ReservationEntry entry;
  final DateTime now;
  final VoidCallback onCancel;

  PlayerReservation get reservation => entry.first;

  /// Состояние словами. Незнакомое приходит с сервера как есть — лучше сырой код, чем
  /// уверенное враньё про «подтверждена».
  String _stateLabel(L l) => switch (entry.state) {
        'pending' => l.customerReservationsStatePending,
        'confirmed' => l.customerReservationsStateConfirmed,
        'seated' => l.customerReservationsStateSeated,
        'cancelled' => l.customerReservationsStateCancelled,
        'no_show' => l.customerReservationsStateNoShow,
        'rejected' => l.customerReservationsStateRejected,
        _ => reservation.state,
      };

  /// Почему клуб отказал — словами, а не кодом состояния. Причина приходит кодом из общего
  /// справочника: текст на языке стойки игроку не помог бы, а перевод у кода свой на каждый язык.
  /// Пояснение администратора, если он его написал, идёт следом — оно и есть вся конкретика.
  List<Widget> _rejection(L l, ThemeData theme) {
    if (entry.state != 'rejected') return const [];
    final reason = switch (reservation.rejectReasonCode) {
      'no_seats' => l.bookingRejectNoSeats,
      'maintenance' => l.bookingRejectMaintenance,
      'event' => l.bookingRejectEvent,
      'other' => null,
      _ => null,
    };
    final note = reservation.rejectReasonNote?.trim();
    final lines = [
      ?reason,
      if (note != null && note.isNotEmpty) note,
      // Заморозка при отказе возвращается всегда: человеку это важнее самой причины.
      l.customerReservationsRejectMoneyBack,
    ];
    return [
      for (final line in lines)
        Text(
          line,
          style: theme.textTheme.bodyMedium
              ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
    ];
  }

  /// Сколько клуб ещё может думать над заявкой. Срок стоит только у заявок в ожидании: у
  /// подтверждённой отвечать больше не на что.
  ///
  /// Молчание до срока — не отказ и не потеря денег: заявка снимется сама, а замороженное
  /// вернётся целиком. Игрок должен видеть и то, и другое, иначе будет звонить на стойку.
  List<Widget> _respondBy(L l, ThemeData theme, String locale) {
    final respondBy = reservation.respondByUtc;
    if (entry.state != 'pending' || respondBy == null) return const [];

    final left = respondBy.difference(now);
    if (left.isNegative) {
      return [
        const SizedBox(height: 4),
        Text(
          l.customerReservationsRespondOver,
          style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error),
        ),
      ];
    }

    final minutes = left.inMinutes;
    final remaining = minutes < 1
        ? l.customerReservationsRespondSoon
        : l.customerReservationsRespondLeft(minutes);
    final at = DateFormat.Hm(dateLocale(locale)).format(respondBy.toLocal());

    return [
      const SizedBox(height: 4),
      Text(
        '${l.customerReservationsRespondBy(at)} · $remaining',
        style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.primary),
      ),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final l = L.of(context);
    final theme = Theme.of(context);
    final locale = Localizations.localeOf(context).languageCode;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  entry.isCompany
                      ? l.customerReservationsCompanySeats(entry.seatCount)
                      : reservation.seatName ?? l.customerReservationsNoSeat,
                  style: theme.textTheme.titleMedium,
                ),
                Text(
                  _stateLabel(l),
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              formatTimeRange(l, reservation.startsAtUtc, reservation.endsAtUtc, locale, now: now),
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            ..._respondBy(l, theme, locale),
            ..._rejection(l, theme),
            if (entry.isCompany && entry.totalMinorUnits != null)
              Text(
                formatMoney(entry.totalMinorUnits!, entry.currencyCode ?? 'TJS', locale: locale),
                style: theme.textTheme.bodyMedium,
              ),
            if (entry.isCancellable)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(
                  onPressed: onCancel,
                  style: TextButton.styleFrom(foregroundColor: theme.colorScheme.error),
                  child: Text(entry.isCompany
                      ? l.customerReservationsCancelCompany
                      : l.customerReservationsCancel),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
