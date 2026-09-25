// Сгенерировано из src/AFK4.Shared.Contracts. Руками не править:
// правка живёт в записи C#, а сюда приезжает через `bun run gen` в packages/contracts.
//
// ignore_for_file: lines_longer_than_80_chars
library;

/// Словарь: Ads/AdContracts.cs
abstract final class AdCampaignStateNames {
  static const String draft = 'draft';
  /// Идёт в своих датах, если у неё есть одобренный креатив.
  static const String active = 'active';
  static const String paused = 'paused';
}

/// Словарь: Ads/AdContracts.cs
abstract final class AdCategoryNames {
  static const String food = 'food';
  static const String electronics = 'electronics';
  static const String games = 'games';
  static const String education = 'education';
  static const String services = 'services';
  static const String telecom = 'telecom';
  static const String other = 'other';
}

/// Словарь: Ads/AdContracts.cs
abstract final class AdErrorCodeNames {
  static const String invalid = 'ad_invalid';
  static const String notApproved = 'ad_campaign_without_approved_creative';
  static const String confirmationRequired = 'ad_moderation_confirmation_required';
}

/// Словарь: Ads/AdContracts.cs
abstract final class AdModerationNames {
  static const String pending = 'pending';
  static const String approved = 'approved';
  static const String rejected = 'rejected';
}

/// Словарь: Platform/Billing/BillingIntervalNames.cs
abstract final class BillingIntervalNames {
  static const String monthly = 'monthly';
  static const String yearly = 'yearly';
}

/// Словарь: Billing/BillingModeNames.cs
abstract final class BillingModeNames {
  static const String prepaidWallet = 'prepaid_wallet';
  static const String postpaidDebt = 'postpaid_debt';
  static const String package = 'package';
}

/// Что оператор может найти одной строкой из палитры. Строки, а не enum: контракт переживает
/// клиентов, и старый клиент, встретив незнакомый вид, просто его не покажет.
///
/// Словарь: Operator/BranchSearchResultDto.cs
abstract final class BranchSearchKindNames {
  static const String seat = 'seat';
  static const String player = 'player';
  static const String reservation = 'reservation';
  static const String receipt = 'receipt';
  static const String order = 'order';
}

/// Словарь: Shifts/CashMovementTypeNames.cs
abstract final class CashMovementTypeNames {
  static const String cashIn = 'cash_in';
  static const String cashOut = 'cash_out';
}

/// Словарь: Platform/Billing/ClubPlanContracts.cs
abstract final class ClubPlanErrorCodeNames {
  static const String trialUsed = 'plan_trial_used';
  /// Сначала оплатить просроченное — потом снова на тариф за ПК.
  static const String overdueInvoices = 'plan_overdue_invoices';
  static const String nothingToPromise = 'plan_nothing_to_promise';
  static const String promiseUsed = 'plan_promise_used';
  static const String alreadyOnPlan = 'plan_already_on_plan';
}

/// Словарь: Platform/Billing/ClubPlanContracts.cs
abstract final class ClubPlanKindNames {
  static const String free = 'free';
  static const String perPc = 'per_pc';
  static const String trial = 'trial';
  /// Прежняя сетка тарифов: условия у поддержки, цену экран не показывает.
  static const String legacy = 'legacy';
}

/// Словарь: Consoles/ConsoleSeatContracts.cs
abstract final class ConsoleSeatErrorCodeNames {
  /// На месте уже стоит ПК или консоль.
  static const String seatTaken = 'console_seat_taken';
  static const String seatNotFound = 'console_seat_not_found';
}

/// Почему сервер не принял команду администратора.
///
/// Словарь: Devices/DeviceCommandErrorCodeNames.cs
abstract final class DeviceCommandErrorCodeNames {
  /// Такой команды нет — раньше сервер принимал любую строку, и агент отвечал «не умею».
  static const String unknownType = 'unknown_command_type';
  /// На ПК идёт сессия: перезагружать, выключать и уводить в обслуживание нельзя.
  static const String activeSession = 'device_has_active_session';
  static const String invalidPayload = 'invalid_command_payload';
  /// Разбудить нельзя: ПК ещё ни разу не сообщил свой сетевой адрес.
  static const String wakeTargetUnknown = 'wake_target_unknown';
  /// Разбудить некому: в подсети этого ПК нет ни одного включённого соседа.
  static const String noWakeHelper = 'no_wake_helper';
}

/// Чем закончилась команда на устройстве — машинным именем, а не фразой.
/// Журнал команд читает администратор клуба на своём языке. Агент до этого присылал только
/// человеческую строку и присылал её по-английски («Workstation locked (nothing)»), и она
/// доезжала до экрана как есть. Имя исхода переводится на стороне клиента — тот же порядок, что
/// у кодов ошибок API: сервер отдаёт код, клиент решает, какими словами о нём сказать.
/// Строку-сообщение это не отменяет: она остаётся деталью для инженера.
///
/// Словарь: Devices/DeviceCommandOutcomeNames.cs
abstract final class DeviceCommandOutcomeNames {
  /// Команда принята, отдельного исхода у неё нет.
  static const String accepted = 'accepted';
  /// Аренда принята, место открыто гостю.
  static const String leaseAccepted = 'lease-accepted';
  /// Аренда продлена: сессия продолжается.
  static const String leaseRefreshed = 'lease-refreshed';
  /// Машина заперта.
  static const String workstationLocked = 'workstation-locked';
  /// Агент не смог применить машинные политики: на самой машине ничего не изменилось. Раньше
  /// такой исход сообщался как обычный успех — оператор видел «заблокировано» там, где
  /// диспетчер задач остался доступен.
  static const String machinePoliciesUnavailable = 'machine-policies-unavailable';
  /// Предупреждение показано на экране игрока.
  static const String warningShown = 'warning-shown';
  /// Причина предупреждения агенту неизвестна — игрок ничего не увидел.
  static const String warningReasonUnknown = 'warning-reason-unknown';
  /// Такой тип команды этот агент не исполняет.
  static const String commandNotImplemented = 'command-not-implemented';
  /// Исполнение сорвалось: диск, реестр, права. Агент обязан ответить и в этом случае — молчание
  /// оператор читает как «команда где-то в пути», и ждать он будет бесконечно.
  static const String commandExecutionFailed = 'command-execution-failed';
  /// В команде не было аренды сессии.
  static const String leaseMissing = 'lease-missing';
  /// Аренду в команде не удалось прочитать.
  static const String leaseUnreadable = 'lease-unreadable';
  /// Аренда не прошла проверку подписи или срока.
  static const String leaseInvalid = 'lease-invalid';
  /// Windows перезагрузит ПК через десять секунд: ответ ушёл раньше.
  static const String rebootScheduled = 'reboot-scheduled';
  /// Windows выключит ПК через десять секунд.
  static const String shutdownScheduled = 'shutdown-scheduled';
  /// На ПК идёт сессия: чужую игру агент не выключает и в обслуживание не уводит.
  static const String sessionInProgress = 'session-in-progress';
  /// Сосед отправил волшебный пакет. Проснулся ли ПК, скажет его сердцебиение.
  static const String wakePacketSent = 'wake-packet-sent';
  /// MAC или широковещательный адрес не годятся — или сосед уже в другой подсети.
  static const String wakeTargetInvalid = 'wake-target-invalid';
  static const String maintenanceStarted = 'maintenance-started';
  static const String maintenanceEnded = 'maintenance-ended';
  /// Выход игрока или сообщение переданы на экран ПК.
  static const String deliveredToShell = 'delivered-to-shell';
  /// Экран игрока не запущен или не отвечает — передать некому.
  static const String shellNotConnected = 'shell-not-connected';
  /// Профиля защиты у ПК пока нет — обновлять нечего.
  static const String nothingToRefresh = 'nothing-to-refresh';
  /// Профиль защиты перечитан и применён; что вышло по пунктам — в отчёте ПК.
  static const String protectionApplied = 'protection-applied';
  /// Профиль не удалось получить с сервера — ПК остаётся на прежнем.
  static const String protectionUnavailable = 'protection-unavailable';
}

/// Где команда: ждёт, отдана агенту, устарела или агент уже ответил.
///
/// Словарь: Devices/DeviceCommandStatusNames.cs
abstract final class DeviceCommandStatusNames {
  static const String pending = 'Pending';
  /// Отдана агенту и больше не отдаётся — у неповторяемых команд (перезагрузка, выключение,
  /// пробуждение). Повторная выдача той же перезагрузки после перезапуска агента была бы петлёй.
  static const String delivered = 'Delivered';
  /// Неповторяемая команда пролежала дольше срока и не отдана: перезагрузка, пришедшая через три
  /// дня после просьбы, хуже потерянной.
  static const String expired = 'Expired';
  static const String accepted = 'Accepted';
  static const String rejected = 'Rejected';
  static const String failed = 'Failed';
  static const String completed = 'Completed';
}

/// Словарь: Devices/DeviceCommandTypeNames.cs
abstract final class DeviceCommandTypeNames {
  static const String lock = 'lock';
  static const String unlock = 'unlock';
  static const String refreshSessionLease = 'refresh-session-lease';
  /// A non-blocking warning overlay pushed to the shell (e.g. fixed time almost
  /// up, or an open tab approaching its credit limit).
  static const String warn = 'warn';
  /// Перезагрузить ПК. Только без живой сессии; отдаётся агенту один раз.
  static const String reboot = 'reboot';
  /// Выключить ПК. Только без живой сессии; отдаётся агенту один раз.
  static const String shutdown = 'shutdown';
  /// «Разбудить этот ПК» — так просит администратор, называя спящую машину. Выключенный ПК
  /// команду не получит, поэтому сервер передаёт её соседу по подсети как WakeNeighbor.
  static const String wake = 'wake';
  /// Агенту: отправь волшебный пакет (6×FF + 16×MAC, UDP 9) в свою подсеть. В теле — mac,
  /// broadcast и targetDeviceId. Администратор эту команду не шлёт — её собирает сервер из
  /// Wake.
  static const String wakeNeighbor = 'wake-neighbor';
  /// Хост выходит из аккаунта игрока; сессия, если идёт, продолжается.
  static const String signOut = 'sign-out';
  /// Сообщение игроку: окно поверх игры или полоса на экране блокировки. В теле — text.
  static const String message = 'message';
  /// Режим обслуживания: игрокам вход закрыт. Право organization.devices.maintenance.
  static const String maintenanceOn = 'maintenance-on';
  /// Вернуть ПК в зал из обслуживания.
  static const String maintenanceOff = 'maintenance-off';
  /// Перечитать профиль защиты.
  static const String policyRefresh = 'policy-refresh';
}

/// Словарь: Install/DeviceEnrollmentStateNames.cs
abstract final class DeviceEnrollmentStateNames {
  static const String approved = 'approved';
  static const String pending = 'pending';
  static const String rejected = 'rejected';
  static const String removed = 'removed';
}

/// Словарь: Devices/DevicePlayerSignInContracts.cs
abstract final class DevicePlayerSignInErrorCodeNames {
  /// Номер или ПИН-код не подошли. Причина не уточняется: «нет такого номера» — это ответ на
  /// вопрос, кто в этой сети играет.
  static const String signInRefused = 'sign_in_refused';
  /// С этого ПК слишком много неудачных попыток; ответ несёт, когда можно снова.
  static const String tooManyAttempts = 'too_many_attempts';
  /// На ПК идёт чужая сессия: вход верный, но открыть вошедшему нечего.
  static const String sessionNotYours = 'session_not_yours';
  /// Клуб закрыл этот ПК на обслуживание: входить на нём некуда.
  static const String deviceInMaintenance = 'device_in_maintenance';
}

/// Словарь: Install/DeviceRoleNames.cs
abstract final class DeviceRoleNames {
  static const String gamingPc = 'gaming_pc';
  static const String managerWorkstation = 'manager_workstation';
  /// Консоль на месте — без агента: сессию ведёт администратор, ПК не отпирается и не запирается,
  /// сердцебиения нет. Запись устройства нужна, чтобы у сессии, кассы и отчётов было то же место,
  /// что у ПК.
  static const String console = 'console';
}

/// Словарь: Devices/DeviceShellContextContracts.cs
abstract final class DeviceSessionOwnerKindNames {
  /// Живой сессии на ПК нет.
  static const String none = 'none';
  /// Сессия без счёта игрока — посадили у стойки.
  static const String guest = 'guest';
  /// Сессия на счёте игрока.
  static const String player = 'player';
}

/// Что с дружбой прямо сейчас.
///
/// Словарь: Friends/FriendDtos.cs
abstract final class FriendshipStateNames {
  /// Позвали, ответа ещё нет.
  static const String pending = 'pending';
  static const String accepted = 'accepted';
  /// Отказали. Строка остаётся, чтобы человека не звали второй раз.
  static const String declined = 'declined';
}

/// Чем запускается игра (спека оболочки, §6.6). Путь к лаунчеру на каждом ПК свой — его находит
/// агент; сервер хранит только что запускать.
///
/// Словарь: Games/GameLibraryContracts.cs
abstract final class GameLaunchKindNames {
  /// Через Steam по AppID: `steam.exe -applaunch 730`.
  static const String steam = 'steam';
  /// Через Epic Games Launcher по имени приложения: `Fortnite`.
  static const String epic = 'epic';
  /// Через Riot Client по продукту: `league_of_legends`, `valorant`.
  static const String riot = 'riot';
  /// Через Battle.net по коду игры: `WoW`, `Pro`.
  static const String battleNet = 'battlenet';
  /// Своим exe по пути на ПК.
  static const String executable = 'exe';
}

/// Словарь: Games/GameLibraryContracts.cs
abstract final class GameLibraryErrorCodeNames {
  static const String invalidGame = 'invalid_game';
  static const String catalogGameNotFound = 'catalog_game_not_found';
  static const String libraryFull = 'game_library_full';
}

/// Словарь: Players/GuestImportContracts.cs
abstract final class GuestImportIssueNames {
  static const String invalidPhone = 'invalid_phone';
  static const String missingName = 'missing_name';
  static const String negativeAmount = 'negative_amount';
  /// Тот же номер уже встречался выше в этом файле.
  static const String duplicateInFile = 'duplicate_in_file';
  /// Гостю уже переносили остатки — второй перенос удвоил бы деньги.
  static const String alreadyImported = 'already_imported';
}

/// Словарь: Devices/DeviceHardwareContracts.cs
abstract final class HardwareComponentNames {
  static const String cpu = 'cpu';
  static const String memory = 'memory';
  static const String gpu = 'gpu';
  static const String motherboard = 'motherboard';
  static const String disk = 'disk';
}

/// Машинные имена отказов установки. Нужны затем, что мастер установки говорит на трёх языках, а
/// текст отказа с сервера — всегда английский: показать его человеку у ПК нельзя, а назвать
/// причину своими словами по коду — можно.
///
/// Словарь: Install/InstallErrorCodeNames.cs
abstract final class InstallErrorCodeNames {
  /// На выбранное место уже привязан другой ПК.
  static const String seatOccupied = 'seat_occupied';
  /// Код установки не подходит: неизвестен, истёк, отозван или исчерпан. Одна причина на все
  /// четыре: угадывающему код не надо подсказывать, какой из них был почти верным.
  static const String installCodeInvalid = 'install_code_invalid';
}

/// Словарь: Platform/Billing/InvoiceKindNames.cs
abstract final class InvoiceKindNames {
  static const String subscription = 'subscription';
  static const String proration = 'proration';
  /// Manually issued charge outside the subscription: setup, hardware, extra service.
  static const String oneOff = 'one_off';
  /// Money owed back to the club. Carries a negative amount so the balance is arithmetic.
  static const String credit = 'credit';
}

/// Словарь: Platform/Billing/InvoiceStatusNames.cs
abstract final class InvoiceStatusNames {
  static const String issued = 'issued';
  static const String paid = 'paid';
  static const String void_ = 'void';
  static const String overdue = 'overdue';
}

/// Словарь: Billing/LedgerAccountTypeNames.cs
abstract final class LedgerAccountTypeNames {
  static const String wallet = 'wallet';
  static const String debt = 'debt';
  static const String packageTime = 'package_time';
  static const String bonusTime = 'bonus_time';
}

/// Словарь: Billing/LedgerEntryTypeNames.cs
abstract final class LedgerEntryTypeNames {
  static const String topUp = 'top_up';
  static const String gameplayCharge = 'gameplay_charge';
  static const String packagePurchase = 'package_purchase';
  static const String packageConsumption = 'package_consumption';
  static const String bonusGrant = 'bonus_grant';
  static const String bonusConsumption = 'bonus_consumption';
  static const String refund = 'refund';
  static const String manualCorrection = 'manual_correction';
  static const String postpaidDebt = 'postpaid_debt';
  static const String debtPayment = 'debt_payment';
  static const String walletPayment = 'wallet_payment';
  static const String reversal = 'reversal';
  static const String cashback = 'cashback';
  /// Деньги за приведённого друга — платит клуб, обоим сразу.
  static const String referralBonus = 'referral_bonus';
  /// Заморозка под бронь: деньги остаются игроку, но потратить их второй раз уже нельзя.
  /// Оплатой не становится никогда — только снимается реверсом.
  static const String reservationHold = 'reservation_hold';
  /// Удержанная за неявку предоплата — выручка клуба, а не «деньги, которые не вернулись».
  /// Пишется только если филиал так решил, и всегда после снятия заморозки.
  static const String reservationNoShowFee = 'reservation_no_show_fee';
  /// Взнос за участие в событии клуба — списывается при записи.
  static const String tournamentEntryFee = 'tournament_entry_fee';
  /// Возврат взноса: игрок снялся до начала или клуб отменил событие. Отдельно от общего
  /// возврата, чтобы в выписке было видно, за что деньги вернулись.
  static const String tournamentEntryRefund = 'tournament_entry_refund';
  /// Чаевые администратору смены с кошелька игрока. Не выручка клуба: клуб их должен сотруднику.
  static const String tip = 'tip';
  /// Начальный остаток из прежней программы клуба: деньги гость заплатил туда, клуб берёт долг на
  /// себя. Не выручка и не наличные смены.
  static const String openingBalance = 'opening_balance';
}

/// Словарь: Media/MediaPurposeNames.cs
abstract final class MediaPurposeNames {
  static const String branchLogo = 'branch-logo';
  /// Логотип клуба целиком: его показывают приложение игрока, витрина и экран игрового ПК.
  /// Отдельно от логотипа зала — у сети он один, а залов много.
  static const String organizationLogo = 'organization-logo';
  /// Фото зала для витрины клуба в приложении игрока.
  static const String branchCover = 'branch-cover';
  /// Остальные фото зала: их несколько, и новая загрузка не заменяет прежние.
  static const String branchGallery = 'branch-gallery';
  /// Картинка новости: её показывают приложение игрока и витрина свободного ПК.
  static const String newsImage = 'news-image';
  /// Фото товара бара — для витрины ПК и меню бара.
  static const String productImage = 'product-image';
}

/// Машинные имена отказов при подключении админки клуба к клубу.
/// Этот экран человек видит раньше всего остального — до входа, до языка интерфейса он уже
/// выбран. Английская фраза сервера («OrganizationSlug must contain only lowercase letters…»)
/// доезжала до него дословно: непонятно и похоже на поломку программы.
///
/// Словарь: Platform/Operator/OperatorConnectionErrorCodeNames.cs
abstract final class OperatorConnectionErrorCodeNames {
  /// Не указано ни адреса клуба и филиала, ни кода подключения.
  static const String inputMissing = 'connection_input_missing';
  /// Указано и то и другое сразу.
  static const String inputAmbiguous = 'connection_input_ambiguous';
  /// Адрес клуба или филиала записан не по формату.
  static const String slugInvalid = 'connection_slug_invalid';
  /// Код подключения пустой.
  static const String setupCodeRequired = 'setup_code_required';
  /// Кодом подключения уже воспользовались или его отозвали.
  static const String setupCodeNotUsable = 'setup_code_not_usable';
}

/// Словарь: Identity/AccountActivation/OrganizationOwnerInviteStatusNames.cs
abstract final class OrganizationOwnerInviteStatusNames {
  static const String pending = 'pending';
  static const String accepted = 'accepted';
  static const String revoked = 'revoked';
  static const String expired = 'expired';
}

/// Словарь: Identity/OrganizationPermissionNames.cs
abstract final class OrganizationPermissionNames {
  static const String createDeviceEnrollmentCode = 'organization.devices.enrollment_codes.create';
  static const String dispatchDeviceCommand = 'organization.devices.commands.dispatch';
  /// Увести ПК в обслуживание и вернуть в зал. Отдельно от прочих команд: обслуживание закрывает
  /// машину для игроков, и решать это — не каждому, кто может её перезапереть.
  static const String maintainDevice = 'organization.devices.maintenance';
  static const String viewDeviceCommandStatus = 'organization.devices.commands.status.view';
  static const String rotateDeviceCredential = 'organization.devices.credentials.rotate';
  static const String revokeDeviceCredential = 'organization.devices.credentials.revoke';
  static const String assignDeviceSeat = 'organization.devices.seat_assignment.assign';
  static const String viewDeviceDetail = 'organization.devices.detail.view';
  static const String installDevice = 'organization.devices.install';
  static const String viewFloorMap = 'organization.floor_map.view';
  static const String manageLayout = 'organization.layout.manage';
  static const String startSession = 'organization.sessions.start';
  static const String extendSession = 'organization.sessions.extend';
  /// Поставить сессию на паузу и снять её. Право того же круга, что продление: обе правят время.
  static const String pauseSession = 'organization.sessions.pause';
  static const String transferSession = 'organization.sessions.transfer';
  static const String endSession = 'organization.sessions.end';
  static const String viewSession = 'organization.sessions.view';
  static const String createPlayerAccount = 'organization.players.create';
  static const String viewPlayers = 'organization.players.view';
  static const String viewBilling = 'organization.billing.view';
  static const String topUpWallet = 'organization.billing.wallet.top_up';
  static const String refundLedgerEntry = 'organization.billing.refund';
  static const String manualLedgerCorrection = 'organization.billing.manual_correction';
  static const String payDebt = 'organization.billing.debt.pay';
  /// Anti-fraud (§5.2/D2): approve an over-threshold high-risk money action raised by another actor.
  static const String approveMoneyAction = 'organization.billing.money_action.approve';
  static const String viewSubscription = 'organization.billing.subscription.view';
  /// Сменить тариф клуба, начать пробный период, взять обещанный платёж. Это обязательство
  /// платить — только у владельца.
  static const String manageSubscription = 'organization.billing.subscription.manage';
  /// Перенести гостей с балансами из прежней программы. Это деньги, которые клуб берёт на себя, —
  /// только у владельца.
  static const String importPlayers = 'organization.players.import';
  static const String manageTariffs = 'organization.tariffs.manage';
  static const String viewTariffs = 'organization.tariffs.view';
  static const String managePackages = 'organization.packages.manage';
  static const String viewPackages = 'organization.packages.view';
  static const String purchasePackage = 'organization.packages.purchase';
  static const String openShift = 'organization.shifts.open';
  static const String closeShift = 'organization.shifts.close';
  /// Закрыть СВОЮ смену — ту, которую сам и открыл. Узкое подмножество CloseShift.
  /// Контроль от этого не слабеет: сверка кассы обязательна для всех (CountedCash), а
  /// расхождение сверх допуска филиала по-прежнему требует подписи второго человека, который
  /// не открывал и не закрывает смену (§5.7, EfShiftService). То есть «закрыть поверх
  /// недостачи» в одиночку нельзя было и не станет можно.
  /// Без этого права ночной кассир, которого гейт после входа ЗАСТАВИЛ открыть смену, не мог
  /// её закрыть: в шесть утра он один, а закрывать обязан кто-то другой.
  static const String closeOwnShift = 'organization.shifts.close_own';
  static const String viewShift = 'organization.shifts.view';
  static const String manageShiftCash = 'organization.shifts.cash.manage';
  static const String viewReports = 'organization.reports.view';
  static const String viewReservations = 'organization.reservations.view';
  static const String manageReservations = 'organization.reservations.manage';
  static const String managePosCatalog = 'organization.pos.catalog.manage';
  static const String createPosSale = 'organization.pos.sales.create';
  static const String payPosSale = 'organization.pos.sales.pay';
  static const String refundPosSale = 'organization.pos.sales.refund';
  static const String voidPosSale = 'organization.pos.sales.void';
  /// Отменить СВОЙ чек, пробитый только что, без старшего — узкое подмножество VoidPosSale.
  /// Границы правила и довод за него живут в `Pos/PosSelfVoidPolicy.cs`: право само по
  /// себе ничего не разрешает, пока продажа не своя, не в текущей открытой смене и не свежая.
  static const String voidOwnRecentPosSale = 'organization.pos.sales.void_own_recent';
  static const String manageInventoryStock = 'organization.inventory.stock.manage';
  static const String viewInventory = 'organization.inventory.view';
  static const String viewReceipt = 'organization.receipts.view';
  static const String viewUpdateStatus = 'organization.updates.status.view';
  static const String viewDiagnostics = 'organization.diagnostics.view';
  static const String manageBranchStaff = 'organization.identity.branch_staff.manage';
  static const String manageRoles = 'organization.identity.roles.manage';
  static const String viewAudit = 'organization.audit.view';
  /// Owner-only: read org-wide audit across all branches + org-level records.
  static const String viewOrganizationAudit = 'organization.audit.organization.view';
  static const String manageBranchSettings = 'organization.branches.settings.manage';
  /// Owner-only: view the org-wide branch roster (network overview).
  static const String viewBranches = 'organization.branches.view';
  /// Owner-only: connect/manage the club's DC-Bank payment cards (dcgate gateways).
  static const String managePaymentGateways = 'organization.payments.gateways.manage';
  /// Очередь заказов бара разведена на два права по одной границе: двигаются ли деньги.
  /// Serve — увидеть очередь, принять заказ, выдать его. Это чистая смена статуса, ничего не
  /// списывается и не возвращается: заказ оплачен в момент оформления. Выдаёт еду кассир, ему
  /// это право и нужно.
  /// Manage — отменить заказ, а отмена идёт через денежный координатор и возвращает деньги.
  /// Это денежное действие и остаётся за тем же кругом, что возвраты в кассе.
  static const String serveShopOrders = 'organization.shop.orders.serve';
  static const String manageShopOrders = 'organization.shop.orders.manage';
  /// Снять с места вызов оператора. Право того же круга, что и «отдать заказ»: зовут человека
  /// с зала, а не того, кто правит настройки.
  static const String resolveAssistanceRequest = 'organization.assistance.resolve';
  /// Owner-only: configure org-wide loyalty/cashback rates.
  static const String manageLoyaltySettings = 'organization.loyalty.settings.manage';
  static const String manageNews = 'organization.news.manage';
  /// Заводить и отменять события клуба. Отдельно от новостей: событие возвращает деньги
  /// при отмене, и это право сильнее права написать объявление.
  static const String manageTournaments = 'organization.tournaments.manage';
  /// Библиотека игр филиала — что игрок запустит на ПК (спека оболочки, §6.6). У того, кто
  /// ставит ПК и игры: владелец, управляющий, техник.
  static const String manageGameLibrary = 'organization.games.manage';
  /// Читать отзывы игроков о филиале. Отзыв бывает и о смене — поэтому у владельца и
  /// управляющего, а не у всей стойки.
  static const String viewReviews = 'organization.reviews.view';
  /// Принять новое железо ПК как норму — после апгрейда или ремонта. У того, кто его меняет:
  /// владелец, управляющий, техник.
  static const String acceptDeviceHardware = 'organization.devices.hardware.accept';
  /// Чаевые администратору с экрана ПК: включить у клуба и вернуть игроку, пока смена открыта.
  /// Это движение денег, поэтому у владельца и управляющего, а не у стойки.
  static const String manageTips = 'organization.tips.manage';
}

/// Словарь: Platform/Organizations/OrganizationPlanCodeNames.cs
abstract final class OrganizationPlanCodeNames {
  /// Бесплатно: до 10 ПК, 1 зал, 3 сотрудника, с рекламой платформы.
  static const String free = 'free';
  /// 10 сомони в месяц за каждый ПК сверх десяти; без лимитов и рекламы.
  static const String perPc = 'per_pc';
  /// Прежняя сетка — снята с продажи (спека тарифов клуба, §2); клубы на ней остаются.
  static const String starter = 'starter';
  static const String growth = 'growth';
  static const String scale = 'scale';
}

/// Словарь: Platform/Organizations/OrganizationStatusNames.cs
abstract final class OrganizationStatusNames {
  static const String active = 'active';
  static const String suspended = 'suspended';
  static const String deletionPending = 'deletion_pending';
  /// Клуб ушёл и его данные стёрты. Терминальный статус: архивная строка нужна отчётности по
  /// деньгам, но отличать её от живой заявки на уход обязательно — иначе стёртый клуб выглядит
  /// как ещё живая заявка.
  static const String purged = 'purged';
}

/// Словарь: Payments/PaymentMethodNames.cs
abstract final class PaymentMethodNames {
  static const String cash = 'cash';
  static const String cardManual = 'card_manual';
  static const String wallet = 'wallet';
}

/// Машинные имена лимитов тарифа и код отказа. Фразу для человека собирает клиент —
/// сервер отдаёт только код и числа.
///
/// Словарь: Platform/Organizations/PlanLimitNames.cs
abstract final class PlanLimitNames {
  static const String reachedCode = 'plan_limit_reached';
  static const String branches = 'branches';
  static const String devicesPerBranch = 'devices_per_branch';
  static const String concurrentSessions = 'concurrent_sessions';
  static const String staffUsersPerBranch = 'staff_users_per_branch';
}

/// Словарь: Platform/Auth/PlatformAdminPermissionNames.cs
abstract final class PlatformAdminPermissionNames {
  static const String useSupportAccess = 'platform.support.access';
  static const String viewOrganizations = 'platform.organizations.view';
  static const String createOrganization = 'platform.organizations.create';
  static const String updateOrganizationStatus = 'platform.organizations.status.update';
  static const String updateOrganizationLimits = 'platform.organizations.limits.update';
  static const String updateOrganizationProfile = 'platform.organizations.profile.update';
  static const String updateOrganizationUpdateChannel = 'platform.organizations.update_channel.update';
  static const String viewOrganizationSupportNotes = 'platform.organizations.support_notes.view';
  static const String manageOrganizationSupportNotes = 'platform.organizations.support_notes.manage';
  static const String manageOrganizationOwnerInvites = 'platform.organizations.owner_invites.manage';
  static const String transferOrganizationOwner = 'platform.organizations.owner.transfer';
  static const String viewOrganizationHealth = 'platform.organizations.health.view';
  static const String viewPlatformAudit = 'platform.audit.view';
  static const String viewBilling = 'platform.billing.view';
  static const String managePlans = 'platform.billing.plans.manage';
  static const String manageSubscriptions = 'platform.billing.subscriptions.manage';
  static const String manageInvoices = 'platform.billing.invoices.manage';
  static const String viewUpdates = 'platform.updates.view';
  static const String manageUpdatePackages = 'platform.updates.packages.manage';
  static const String manageUpdateRollouts = 'platform.updates.rollouts.manage';
  static const String managePlatformAdmins = 'platform.admins.manage';
  static const String viewPlatformHealth = 'platform.health.view';
  /// Отправка проверочного письма. Отдельно от просмотра здоровья: это действие наружу,
  /// а не чтение.
  static const String sendTestNotification = 'platform.health.test_email.send';
  static const String manageOrganizationFeatures = 'platform.organizations.features.manage';
  /// Ведение анонсов платформы. Отдельного «смотреть анонсы» нет: как и у ролей, кто их ведёт,
  /// тот их и читает — лишнее право усложнило бы модель, ничего не добавив.
  static const String manageAnnouncements = 'platform.announcements.manage';
  /// Каталог игр, из которого клубы собирают библиотеку ПК (спека оболочки, §6.6).
  static const String manageGameCatalog = 'platform.games.manage';
  /// Реклама платформы в витрине ПК: рекламодатели, кампании, модерация креативов, отчёт
  /// показов. Модерация — внутри этого же права: команда платформы маленькая.
  static const String manageAds = 'platform.ads.manage';
  /// Уход клуба: выгрузка его данных и стирание. Отдельно от правки лимитов и статуса — это
  /// вынос персональных данных наружу и необратимое удаление, а не настройка. Одалживать чужое
  /// право здесь значит раздать необратимое тем, кому дали настраивать.
  static const String manageOffboarding = 'platform.organizations.offboarding.manage';
  /// Сетевой запрет человеку: закрыть или открыть ему самообслуживание во всей сети. Право
  /// платформы и только её — клуб решает за свой клуб, закрывая у себя карточку, и дальше его
  /// решения не идут. Поддержке не даётся по той же причине, по которой ей не даётся репутация.
  static const String manageNetworkBans = 'platform.people.network_ban.manage';
}

/// Словарь: Platform/Auth/PlatformAdminRoleNames.cs
abstract final class PlatformAdminRoleNames {
  static const String platformAdmin = 'platform_admin';
  static const String platformSupport = 'platform_support';
}

/// Машинные имена отказов платформенного контура.
/// Причина та же, по которой они появились у офбординга (<see
/// cref="Organizations.OffboardingErrorCodes"/>): текст отказа сервер пишет по-английски и
/// именами своих полей («CurrentPeriodEndUtc must be later than…»), а панель работает на трёх
/// языках. Показать такой текст нельзя, и без кода панель показывала одно «не удалось сохранить
/// изменения» на десяток разных причин — в форме подписки из семи полей человек не мог понять,
/// какое из них поправить.
/// Здесь только те причины, которые человек за панелью исправляет сам. Остальное остаётся без
/// кода и честно называется общими словами.
///
/// Словарь: Platform/Billing/PlatformErrorCodeNames.cs
abstract final class PlatformErrorCodeNames {
  /// Счёт за текущий период уже выставлен.
  static const String invoicePeriodAlreadyBilled = 'invoice_period_already_billed';
  /// Счёт уже оплачен.
  static const String invoiceAlreadyPaid = 'invoice_already_paid';
  /// Счёт уже аннулирован.
  static const String invoiceAlreadyVoid = 'invoice_already_void';
  /// Оплаченный счёт не аннулируют — на него выписывают кредит-ноту.
  static const String paidInvoiceCannotBeVoided = 'paid_invoice_cannot_be_voided';
  /// Кредит-ноту не оплачивают: она учитывается в балансе.
  static const String creditNoteNotPayable = 'credit_note_not_payable';
  /// Номер счёта занял параллельный запрос — можно повторить.
  static const String invoiceNumberingConflict = 'invoice_numbering_conflict';
  /// Конец оплаченного периода не позже его начала.
  static const String subscriptionPeriodEndNotAfterStart = 'subscription_period_end_not_after_start';
  /// Отсрочку ставят на будущее: прошедшая дата ничего не отсрочит.
  static const String subscriptionGraceNotInFuture = 'subscription_grace_not_in_future';
  /// Перевод на пробный период без даты его окончания.
  static const String subscriptionTrialNeedsPeriodEnd = 'subscription_trial_needs_period_end';
  /// Выбранного тарифа нет в каталоге.
  static const String subscriptionPlanNotFound = 'subscription_plan_not_found';
  /// Такой адрес организации уже занят другим клубом.
  static const String organizationSlugTaken = 'organization_slug_taken';
  /// Такой адрес филиала уже занят в этой организации.
  static const String branchSlugTaken = 'branch_slug_taken';
  /// Логин владельца уже занят в этой организации.
  static const String ownerUserNameTaken = 'owner_username_taken';
}

/// Ключи фич, которые платформа умеет включать и выключать клубу. Каждый ключ обязан иметь
/// точку проверки в коде: флаг без потребителя — мусор, который невозможно опознать через месяц.
///
/// Словарь: Platform/Features/PlatformFeatureNames.cs
abstract final class PlatformFeatureNames {
  /// Код отказа, когда фича выключена. Фразу собирает клиент.
  static const String disabledCode = 'feature_disabled';
  static const String onlineBooking = 'online_booking';
  static const String loyalty = 'loyalty';
  static const String onlineTopUp = 'online_topup';
  static const String playerShop = 'player_shop';
  static const String tournaments = 'tournaments';
  /// Реклама платформы в витрине свободного ПК. Её включает бесплатный тариф.
  static const String platformAds = 'platform_ads';
}

/// Словарь: Platform/Health/PlatformHealthContracts.cs
abstract final class PlatformQueueNames {
  static const String notifications = 'notifications';
  static const String billingOutbox = 'billing_outbox';
}

/// Словарь: Platform/Updates/PlatformUpdateContracts.cs
abstract final class PlatformUpdateTargetKindNames {
  static const String organization = 'organization';
  static const String branch = 'branch';
  static const String device = 'device';
}

/// Словарь: Players/PlayerOfferContracts.cs
abstract final class PlayerOfferUnavailableReasonNames {
  /// Сессия начата по пакету: её продлевают новым стартом по пакету, а не деньгами.
  static const String packageSession = 'package_session';
  /// Сессия не предоплаченная — у стойки или открытым счётом; продлевает администратор.
  static const String notPrepaid = 'not_prepaid';
}

/// Словарь: Shell/PlayerShellStateNames.cs
abstract final class PlayerShellStateNames {
  static const String locked = 'locked';
  static const String active = 'active';
  static const String grace = 'grace';
  static const String ending = 'ending';
  static const String maintenance = 'maintenance';
  static const String offline = 'offline';
  static const String error = 'error';
}

/// Словарь: Devices/PlayerSignInClaimDeviceContracts.cs
abstract final class PlayerSignInClaimErrorCodeNames {
  /// Заявки нет или она для другого ПК.
  static const String notFound = 'claim_not_found';
  /// ПК не успел забрать заявку за отведённое время.
  static const String expired = 'claim_expired';
  /// Заявку уже забрали: одна заявка — один вход.
  static const String alreadyRedeemed = 'claim_already_redeemed';
  /// Клуб закрыл этот ПК на обслуживание, пока заявка ждала.
  static const String deviceInMaintenance = 'device_in_maintenance';
}

/// Словарь: Players/PlayerSignInClaimContracts.cs
abstract final class PlayerSignInClaimStatusNames {
  /// ПК ещё не забрал заявку.
  static const String pending = 'pending';
  /// ПК забрал заявку — человек вошёл.
  static const String redeemed = 'redeemed';
  /// ПК не забрал заявку за отведённое время.
  static const String expired = 'expired';
}

/// Словарь: Pos/PosSaleStateNames.cs
abstract final class PosSaleStateNames {
  static const String draft = 'draft';
  static const String pendingPayment = 'pending_payment';
  static const String paid = 'paid';
  static const String refunded = 'refunded';
  static const String voided = 'voided';
}

/// Что именно агент запрещает на ПК — по пункту на строку отчёта.
///
/// Словарь: Devices/ProtectionProfileContracts.cs
abstract final class ProtectionItemNames {
  /// Постоянная основа киоска: меню Ctrl+Alt+Del без блокировки, выхода, смены пользователя и данных входа.
  static const String kioskBaseline = 'kiosk-baseline';
  static const String removableStorage = 'removable-storage';
  static const String browserDownloads = 'browser-downloads';
  static const String browserIncognito = 'browser-incognito';
  static const String browserUrlBlocklist = 'browser-url-blocklist';
  static const String runDialog = 'run-dialog';
  static const String hiddenDrives = 'hidden-drives';
}

/// Что получилось с пунктом. Скрытие дисков — отдельный исход: диск пропал из Проводника, но
/// программа откроет его по пути, и называть это «запрещено» было бы неправдой (§6.3).
///
/// Словарь: Devices/ProtectionProfileContracts.cs
abstract final class ProtectionItemStatusNames {
  static const String applied = 'applied';
  /// Действует только в Проводнике: это не запрет.
  static const String explorerOnly = 'explorer-only';
  static const String failed = 'failed';
  /// Здесь не применить: ПК не на Windows или агент без доступа к политикам машины.
  static const String unsupported = 'unsupported';
  /// Снято на время обслуживания.
  static const String released = 'released';
}

/// Словарь: Devices/ProtectionProfileContracts.cs
abstract final class ProtectionProfileErrorCodeNames {
  /// Профиль успели сохранить после того, как его открыли: нужно перечитать.
  static const String versionConflict = 'protection_profile_version_conflict';
}

/// Словарь: Platform/Pulse/PlatformPulseContracts.cs
abstract final class PulseAlertKindNames {
  static const String agentSilent = 'agent_silent';
  static const String shiftNotClosed = 'shift_not_closed';
  static const String paymentOverdue = 'payment_overdue';
  static const String rolloutFailed = 'rollout_failed';
}

/// Словарь: Platform/Pulse/PlatformPulseContracts.cs
abstract final class PulseAlertLevelNames {
  static const String normal = 'normal';
  static const String attention = 'attention';
  static const String critical = 'critical';
}

/// How often a report schedule fires and the size of the window each run covers.
///
/// Словарь: Reports/ReportScheduleNames.cs
abstract final class ReportScheduleFrequencyNames {
  static const String daily = 'daily';
  static const String weekly = 'weekly';
  static const String monthly = 'monthly';
}

/// Машинные имена отказов по броням. См. Shifts.ShiftErrorCodeNames — та же причина:
/// стойка работает на трёх языках, а английская фраза сервера в интерфейс попадать не должна.
/// Почти все эти отказы — про состояние брони, которое успело измениться: сосед подтвердил её
/// раньше, гость уже сидит, заявку уже отклонили. Отличать их друг от друга оператору нужно
/// именно потому, что дальше он делает разное.
///
/// Словарь: Reservations/ReservationErrorCodeNames.cs
abstract final class ReservationErrorCodeNames {
  /// Подтвердить можно только заявку, на которую клуб ещё не ответил.
  static const String notPending = 'reservation_not_pending';
  /// Менять можно бронь, которая ещё не отменена и не закрыта.
  static const String notChangeable = 'reservation_not_changeable';
  /// Посадить можно только заявку или подтверждённую бронь.
  static const String notSeatable = 'reservation_not_seatable';
  /// Бронь без места — сажать некуда.
  static const String seatRequired = 'reservation_seat_required';
  /// Отменить можно только заявку или подтверждённую бронь.
  static const String notCancellable = 'reservation_not_cancellable';
  /// Отмена без причины: её читает гость и она же уходит в журнал.
  static const String cancelReasonRequired = 'reservation_cancel_reason_required';
  /// Отказать можно в заявке, на которую клуб ещё не ответил.
  static const String notRejectable = 'reservation_not_rejectable';
  /// Отказ «своими словами» без слов.
  static const String refusalNoteRequired = 'reservation_refusal_note_required';
  /// Неизвестная причина отказа.
  static const String rejectReasonUnsupported = 'reservation_reject_reason_unsupported';
  /// Неявку отмечают у подтверждённой брони, время которой уже началось.
  static const String noShowNotAllowed = 'reservation_no_show_not_allowed';
}

/// Словарь: Reservations/ReservationSourceNames.cs
abstract final class ReservationSourceNames {
  static const String operator = 'operator';
  static const String online = 'online';
}

/// Словарь: Reservations/ReservationStateNames.cs
abstract final class ReservationStateNames {
  static const String pending = 'pending';
  static const String confirmed = 'confirmed';
  static const String seated = 'seated';
  static const String cancelled = 'cancelled';
  /// Игрок не приехал. Отдельно от отмены намеренно: отменённая бронь — это решение человека
  /// или клуба, а неявка — его отсутствие, и стоить она может денег. Пока оба исхода изображала
  /// одна «отмена» с пометкой в свободном тексте, отличить их можно было только сравнением строк.
  static const String noShow = 'no_show';
  /// Клуб отказал в заявке — с причиной. Не отмена: игрок ничего не отменял, и в его репутации
  /// чужой отказ появляться не должен.
  static const String rejected = 'rejected';
}

/// The report kinds a schedule can deliver — one per existing report export endpoint.
///
/// Словарь: Reports/ReportScheduleNames.cs
abstract final class ScheduledReportTypeNames {
  static const String shifts = 'shifts';
  static const String sales = 'sales';
  static const String gameplayTime = 'gameplay_time';
  static const String cashOperations = 'cash_operations';
  static const String operatorActions = 'operator_actions';
}

/// Почему код посадки не приняли. Одни и те же у старта с телефона и у заявки на вход.
///
/// Словарь: Players/PlayerSignInClaimContracts.cs
abstract final class SeatingCodeErrorCodeNames {
  /// Код не подошёл. Чужой клуб, истёкший код и опечатка снаружи неразличимы: иначе перебор
  /// шестизначных цифр становится осмысленным.
  static const String invalid = 'seating_code_invalid';
  /// Слишком много неверных кодов — у человека или у всего клуба; ответ несёт, когда можно снова.
  static const String attemptsExceeded = 'seating_code_attempts_exceeded';
  /// Заявке нужен аккаунт AFK4, а у входа — только клубная карточка старого образца.
  static const String platformAccountRequired = 'platform_account_required';
}

/// Состояние места на карте зала — то, что сервер кладёт в SeatStatusDto.State.
/// Пишутся с большой буквы, в отличие от состояний сессии: это отдельный словарь карты, а не код
/// сессии. Пока их писали литералами, Панель сравнивала сырое значение с «free» и «ready», которых
/// сервер не присылает никогда, и «Посадить за ПК» из карточки клиента не предлагало ни одного
/// места (#423).
///
/// Словарь: FloorMap/SeatStateNames.cs
abstract final class SeatStateNames {
  static const String free = 'Free';
  /// ПК на связи и заперт: гость может сесть — это то же «свободно».
  static const String locked = 'Locked';
  static const String active = 'Active';
  static const String paused = 'Paused';
  static const String ending = 'Ending';
  /// ПК не привязан к месту или не одобрен.
  static const String maintenance = 'Maintenance';
  static const String offline = 'Offline';
}

/// С чего началась сессия. Раньше на этот вопрос отвечали догадкой по косвенным признакам —
/// «раз есть бронь, значит по брони», — и догадка врала на любом нестандартном вечере.
/// Пустая строка — законный ответ «неизвестно» для строк, заведённых до того, как вопрос начали
/// задавать. Подставлять вместо неё Operator нельзя: это уже утверждение, а не факт.
///
/// Словарь: Sessions/SessionOriginNames.cs
abstract final class SessionOriginNames {
  /// Посадил администратор за стойкой.
  static const String operator = 'operator';
  /// Игрок сел сам. Именно «сам», а не «по PIN»: самопосадка идёт и из приложения, где никакого
  /// PIN человек не набирал, — имя по механизму врало бы в самом поле, заведённом ради правды.
  static const String selfService = 'self_service';
  /// Сессия выросла из брони: человек пришёл на забронированное время.
  static const String reservation = 'reservation';
}

/// Словарь: Sessions/SessionStateNames.cs
abstract final class SessionStateNames {
  static const String requested = 'requested';
  static const String active = 'active';
  static const String paused = 'paused';
  static const String ending = 'ending';
  static const String ended = 'ended';
  static const String failed = 'failed';
  static const String reconciled = 'reconciled';
}

/// Следы, которые агент стирает, когда сессия кончилась и ПК заперт (спека оболочки, §6.4). Пути
/// у каждого пункта точные и зашиты в агента: из Панели приезжает только «стирать или нет», а не
/// путь — иначе профиль защиты стал бы пультом удаления любых файлов на ПК зала. Сохранения игр в
/// этих путях не лежат.
///
/// Словарь: Devices/ProtectionProfileContracts.cs
abstract final class SessionTraceNames {
  /// Вход в Steam: запомненные аккаунты, автовход, кэш входа, куки магазина.
  static const String steam = 'steam';
  /// Профили браузеров целиком: пароли, куки, история, открытые вкладки.
  static const String browsers = 'browsers';
  /// Вход в Epic, Battle.net, Riot и Ubisoft Connect.
  static const String launchers = 'launchers';
  /// Discord и Telegram Desktop: вход и переписка.
  static const String messengers = 'messengers';
}

/// Словарь: Shell/ShellBridgeContracts.cs
abstract final class ShellBridgeErrorCodeNames {
  /// Номер или ПИН-код не подошли.
  static const String signInRefused = 'sign_in_refused';
  /// С этого ПК слишком много неудачных входов.
  static const String tooManyAttempts = 'too_many_attempts';
  /// На ПК идёт чужая сессия.
  static const String sessionNotYours = 'session_not_yours';
  /// Агента нет на связи — войти и запустить игру сейчас нельзя.
  static const String agentUnavailable = 'agent_unavailable';
  /// Агент на месте, а до сервера клуба не достучался.
  static const String platformUnreachable = 'platform_unreachable';
  /// Такого хост пока не умеет: запрос из более новой страницы или раздел следующего этапа.
  static const String notSupported = 'not_supported';
  /// Windows не дала поменять звук, микрофон или раскладку — например, нет устройства.
  static const String systemUnavailable = 'system_unavailable';
}

/// Словарь: Shell/ShellBridgeContracts.cs
abstract final class ShellBridgeEventTypeNames {
  /// Состояние ПК от агента — PlayerShellStateDto.
  static const String stateChanged = 'state.changed';
  /// Вошёл ли игрок на этом ПК — ShellAuthStateDto.
  static const String authChanged = 'auth.changed';
  /// Мышь или клавиатура тронуты: витрина уступает место окну входа.
  static const String inputActivity = 'input.activity';
  /// Тишина дольше порога: окно входа закрывается, вошедший выходит.
  static const String inputIdle = 'input.idle';
  /// Игра на переднем плане — ShellGameForegroundDto: страница засыпает, чтобы не отнимать кадр.
  static const String gameForeground = 'game.foreground';
  /// Громкость, микрофон, раскладка — ShellSystemStateDto.
  static const String systemChanged = 'system.changed';
  static const String showcaseChanged = 'showcase.changed';
}

/// Мост хост ↔ интерфейс оболочки, версия 2 (спека оболочки, §4.4). Конверт запроса и ответа —
/// общий, из @afk4/host-bridge; здесь — имена и тела. Записи C# дают типы и хосту, и странице:
/// две руками написанные копии однажды разошлись бы.
///
/// Словарь: Shell/ShellBridgeContracts.cs
abstract final class ShellBridgeRequestTypeNames {
  /// Страница загрузилась и слушает. Ответ — ShellSnapshotDto: всё, что хост уже знает. Без
  /// этого состояние, отправленное до того, как React подписался, терялось бы, и экран ждал бы
  /// следующего пульса агента.
  static const String shellReady = 'shell.ready';
  /// Войти номером и ПИН-кодом — через агента, токены привязаны к этому ПК.
  static const String authSignIn = 'auth.signIn';
  static const String authSignOut = 'auth.signOut';
  /// Запустить игру из библиотеки клуба.
  static const String appLaunch = 'app.launch';
  /// Позвать администратора к этому ПК.
  static const String assistCall = 'assist.call';
  static const String systemSetVolume = 'system.setVolume';
  static const String systemSetMicMuted = 'system.setMicMuted';
  static const String systemSetLayout = 'system.setLayout';
  /// Язык интерфейса выбран на экране: хост запоминает его до выхода игрока.
  static const String uiSetLocale = 'ui.setLocale';
  /// Рекламная карточка витрины ушла с экрана — ShellShowcaseImpressionDto. Хост передаёт агенту,
  /// тот копит суммы и отправляет пачками; карточки клуба не считаются.
  static const String showcaseImpression = 'showcase.impression';
  /// Кнопка «Вернуть в зал» на полосе обслуживания.
  static const String maintenanceReturn = 'maintenance.return';
}

/// Словарь: Shell/ShellBridgeContracts.cs
abstract final class ShellKeyboardLayoutNames {
  static const String russian = 'RU';
  static const String english = 'EN';
  static const String tajik = 'TG';
}

/// Словарь: Shell/ShellPipeProtocol.cs
abstract final class ShellPipeErrorCodeNames {
  static const String protocolMismatch = 'protocol_mismatch';
  /// Хост подключился не из консольной сессии — например, по удалённому рабочему столу.
  /// Состояние этого ПК и запуск игр принадлежат тому, кто сидит за монитором.
  static const String wrongSession = 'wrong_session';
  static const String invalidPayload = 'invalid_payload';
  static const String unknownRequest = 'unknown_request';
  /// Игры запускаются только во время сессии.
  static const String noSession = 'no_session';
  static const String appNotAllowed = 'app_not_allowed';
  /// Игра в списке клуба, но её файла на этом ПК нет.
  static const String appMissing = 'app_missing';
  static const String launchFailed = 'launch_failed';
  /// До платформы не достучались — стойка о вызове не узнала.
  static const String platformUnreachable = 'platform_unreachable';
  /// Хосту некуда отправить запрос: агента нет на другом конце канала.
  static const String agentUnavailable = 'agent_unavailable';
  /// Номер или ПИН-код не подошли. Те же имена, что у сервера и моста к странице.
  static const String signInRefused = 'sign_in_refused';
  static const String tooManyAttempts = 'too_many_attempts';
  /// На ПК идёт чужая сессия: вход верный, но открыть вошедшему нечего.
  static const String sessionNotYours = 'session_not_yours';
  /// Клуб закрыл этот ПК на обслуживание — вход на нём закрыт.
  static const String deviceInMaintenance = 'device_in_maintenance';
}

/// Словарь: Shell/ShellPipeProtocol.cs
abstract final class ShellPipeMessageTypeNames {
  /// Хост представляется первым; без этого агент ничего не шлёт.
  static const String hello = 'hello';
  /// Агент прощается: версия протокола не та или хост не из той сессии.
  static const String bye = 'bye';
  static const String state = 'state';
  static const String request = 'request';
  static const String reply = 'reply';
  /// Агент передаёт хосту команду клуба: выйти из аккаунта игрока или показать сообщение.
  static const String command = 'command';
  /// Игрок вошёл: агент отдаёт хосту токены. Один кадр на оба пути — ПИН-код и QR: вход по QR
  /// приходит без запроса хоста, и отвечать на него нечем, кроме отдельного кадра.
  static const String auth = 'auth';
}

/// Словарь: Shell/ShellPipeProtocol.cs
abstract final class ShellPipeRequestTypeNames {
  /// Запустить игру из списка клуба. В теле — `appId`.
  static const String launch = 'launch';
  /// Позвать администратора к этому ПК.
  static const String assist = 'assist';
  /// Войти номером и ПИН-кодом. В теле — `phone` и `pin`. Удачный ответ пуст: токены
  /// приходят кадром ShellPipeMessageTypeNames.Auth.
  static const String signInPin = 'signIn.pin';
  /// «Вернуть в зал» с самого ПК (спека оболочки, §6.5): агент говорит серверу и закрывает
  /// рабочий стол техника. Тело пустое.
  static const String maintenanceReturn = 'maintenance.return';
  /// За ПК кто-то есть: тронуты мышь или клавиатура. Не чаще раза в 20 секунд; по нему агент не
  /// выключает простаивающий ПК под рукой человека и отменяет уже назначенное выключение. Тело пустое.
  static const String activity = 'activity';
  /// Рекламная карточка витрины отстояла на экране. В теле — `cardId` и `shownMs`.
  /// Агент считает только рекламу и только на свободном ПК.
  static const String showcaseImpression = 'showcase.impression';
}

/// Машинные имена отказов по сменам и кассе. См. Tariffs.TariffErrorCodeNames — та же
/// причина: у кассы эти отказы самые частые, а без кода до кассира доезжала английская фраза
/// сервера вместе с сырым телом ответа.
///
/// Словарь: Shifts/ShiftErrorCodeNames.cs
abstract final class ShiftErrorCodeNames {
  /// В филиале уже открыта смена — вторую открыть нельзя.
  static const String alreadyOpen = 'shift_already_open';
  /// Смену уже закрыли: скорее всего, это сделал сосед по кассе.
  static const String alreadyClosed = 'shift_already_closed';
  /// Валюта операции не совпадает с валютой смены.
  static const String currencyMismatch = 'shift_currency_mismatch';
  /// Расхождение по кассе больше допуска — нужна подпись старшего.
  static const String signOffRequired = 'shift_sign_off_required';
  /// Подписать расхождение должен не тот, кто смену открыл или закрывает.
  static const String signOffMustDiffer = 'shift_sign_off_must_differ';
  /// У выбранного сотрудника нет права подписывать расхождение.
  static const String signOffNotAuthorized = 'shift_sign_off_not_authorized';
  /// Внесение и изъятие наличных возможны только при открытой смене.
  static const String cashMovementNeedsOpenShift = 'cash_movement_needs_open_shift';
}

/// Словарь: Shifts/ShiftStateNames.cs
abstract final class ShiftStateNames {
  static const String open = 'open';
  static const String closed = 'closed';
}

/// Словарь: Shop/ShopOrderStatusNames.cs
abstract final class ShopOrderStatusNames {
  static const String placed = 'placed';
  static const String accepted = 'accepted';
  static const String delivered = 'delivered';
  static const String cancelled = 'cancelled';
}

/// Словарь: Showcase/ShowcaseContracts.cs
abstract final class ShowcaseCardKindNames {
  static const String news = 'news';
  static const String tariff = 'tariff';
  static const String product = 'product';
  static const String tournament = 'tournament';
  static const String packages = 'packages';
  static const String barHit = 'bar_hit';
  /// Реклама платформы: только на свободном ПК и с меткой «Реклама · рекламодатель».
  static const String ad = 'ad';
}

/// Машинные причины отказа на входе сотрудника. Клиент по ним и подбирает слова: текст сервера
/// английский, а мастер и приложение клуба работают на трёх языках.
///
/// Словарь: Identity/StaffAuthErrorCodeNames.cs
abstract final class StaffAuthErrorCodeNames {
  /// Пять промахов подряд — вход в эту учётную запись закрыт на четверть часа. Отдельно от
  /// обычного «неверно»: там человек ищет опечатку, здесь ждёт.
  static const String tooManyPasswordAttempts = 'too_many_password_attempts';
}

/// Машинные имена отказов при приглашении сотрудника. См.
/// Install.InstallErrorCodeNames — та же причина: отказ нужно назвать на языке того,
/// кто его читает.
///
/// Словарь: Identity/StaffInviteErrorCodeNames.cs
abstract final class StaffInviteErrorCodeNames {
  /// Номер не похож на телефон — приглашение уходит SMS, слать его некуда.
  static const String invalidPhone = 'invalid_phone';
  /// Логин уже занят другим сотрудником клуба.
  static const String userNameTaken = 'staff_username_taken';
  /// Номер уже принадлежит сотруднику клуба.
  static const String phoneTaken = 'staff_phone_taken';
}

/// Словарь: Inventory/StockMovementTypeNames.cs
abstract final class StockMovementTypeNames {
  static const String purchase = 'purchase';
  static const String sale = 'sale';
  static const String refund = 'refund';
  static const String adjustment = 'adjustment';
}

/// Словарь: Platform/Organizations/SubscriptionStatusNames.cs
abstract final class SubscriptionStatusNames {
  static const String trial = 'trial';
  static const String active = 'active';
  static const String pastDue = 'past_due';
  static const String cancelled = 'cancelled';
}

/// Машинные имена отказов по тарифам. См. Install.InstallErrorCodeNames — та же
/// причина: отказ нужно назвать на языке того, кто его читает.
///
/// Словарь: Tariffs/TariffErrorCodeNames.cs
abstract final class TariffErrorCodeNames {
  /// Тариф с таким именем в филиале уже есть.
  static const String nameTaken = 'tariff_name_taken';
}

/// Словарь: Tips/TipContracts.cs
abstract final class TipErrorCodeNames {
  /// Вернуть чаевые можно только из открытой смены.
  static const String shiftClosed = 'tip_shift_closed';
  static const String alreadyReversed = 'tip_already_reversed';
  /// Всё, что пришло за смену, уже выдано.
  static const String nothingToPay = 'tip_nothing_to_pay';
}

/// Словарь: Tips/TipContracts.cs
abstract final class TipUnavailableReasonNames {
  static const String disabled = 'disabled';
  /// В филиале нет открытой смены — деньги некому отдать.
  static const String noShift = 'no_shift';
  static const String notEnded = 'not_ended';
  static const String tooLate = 'too_late';
  static const String alreadyTipped = 'already_tipped';
  static const String notEnoughBalance = 'not_enough_balance';
  static const String invalidAmount = 'invalid_amount';
}

/// Что с записью игрока на событие.
///
/// Словарь: Tournaments/TournamentStateNames.cs
abstract final class TournamentRegistrationStateNames {
  static const String registered = 'registered';
  static const String cancelled = 'cancelled';
}

/// Что с событием клуба прямо сейчас.
///
/// Словарь: Tournaments/TournamentStateNames.cs
abstract final class TournamentStateNames {
  /// Черновик: клуб составляет событие, игрок его не видит.
  static const String draft = 'draft';
  /// Опубликовано: событие видно в приложении и на него записываются.
  static const String published = 'published';
  /// Отменено клубом. Взносы возвращены всем записавшимся.
  static const String cancelled = 'cancelled';
}

/// Словарь: Updates/UpdateChannelNames.cs
abstract final class UpdateChannelNames {
  static const String stable = 'stable';
  static const String beta = 'beta';
  static const String internal = 'internal';
}

/// Словарь: Updates/UpdateComponentNames.cs
abstract final class UpdateComponentNames {
  static const String organizationAdmin = 'organization-admin';
  static const String agentService = 'agent-service';
  static const String playerShell = 'player-shell';
}

/// Словарь: Updates/UpdatePackageSignatureAlgorithmNames.cs
abstract final class UpdatePackageSignatureAlgorithmNames {
  static const String ecdsaP256Sha256IeeeP1363 = 'ECDSA-P256-SHA256-IEEE-P1363';
}

/// Словарь: Updates/UpdatePackageStateNames.cs
abstract final class UpdatePackageStateNames {
  static const String registered = 'registered';
  static const String validated = 'validated';
  static const String rejected = 'rejected';
  static const String retired = 'retired';
}

/// Словарь: Updates/UpdateRolloutStateNames.cs
abstract final class UpdateRolloutStateNames {
  static const String draft = 'draft';
  static const String active = 'active';
  static const String paused = 'paused';
  static const String completed = 'completed';
  static const String rollbackRequested = 'rollback-requested';
  static const String rolledBack = 'rolled-back';
  static const String cancelled = 'cancelled';
}

/// Словарь: Updates/UpdateStatusNames.cs
abstract final class UpdateStatusNames {
  static const String notStarted = 'not-started';
  static const String offered = 'offered';
  static const String downloading = 'downloading';
  static const String downloaded = 'downloaded';
  static const String installing = 'installing';
  static const String installed = 'installed';
  /// Пакет лёг, но файлы, занятые работающими процессами, Windows заменит только после
  /// перезагрузки машины: до неё устройство работает на прежней сборке.
  static const String pendingRestart = 'pending-restart';
  static const String superseded = 'superseded';
  static const String failed = 'failed';
  static const String rollbackStarted = 'rollback-started';
  static const String rolledBack = 'rolled-back';
  static const String deferred = 'deferred';
  static const String readyToInstall = 'ready-to-install';
  static const String awaitingAppExit = 'awaiting-app-exit';
  static const String healthChecking = 'health-checking';
  static const String rollbackRequired = 'rollback-required';
}

/// Словарь: Updates/UpdateTargetKindNames.cs
abstract final class UpdateTargetKindNames {
  static const String branch = 'branch';
  static const String device = 'device';
}

/// Контракт: Identity/AccountActivation/AcceptOrganizationOwnerInviteRequest.cs
class AcceptOrganizationOwnerInviteRequest {
  const AcceptOrganizationOwnerInviteRequest({
    required this.code,
    required this.userName,
    required this.displayName,
    required this.password,
  });

  final String code;
  final String userName;
  final String displayName;
  final String password;

  factory AcceptOrganizationOwnerInviteRequest.fromJson(Map<String, dynamic> json) => AcceptOrganizationOwnerInviteRequest(
        code: json['code'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
        'userName': userName,
        'displayName': displayName,
        'password': password,
      };
}

/// Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs
class AcceptPlatformAdminInvitationRequest {
  const AcceptPlatformAdminInvitationRequest({
    required this.code,
    required this.userName,
    required this.displayName,
    required this.password,
  });

  final String code;
  final String userName;
  final String displayName;
  final String password;

  factory AcceptPlatformAdminInvitationRequest.fromJson(Map<String, dynamic> json) => AcceptPlatformAdminInvitationRequest(
        code: json['code'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
        'userName': userName,
        'displayName': displayName,
        'password': password,
      };
}

/// Приём приглашения: номер, код из SMS и пароль, который человек придумывает себе сам.
///
/// Контракт: Identity/AcceptStaffInviteRequest.cs
class AcceptStaffInviteRequest {
  const AcceptStaffInviteRequest({
    required this.phoneNumber,
    required this.code,
    required this.password,
  });

  final String phoneNumber;
  final String code;
  final String password;

  factory AcceptStaffInviteRequest.fromJson(Map<String, dynamic> json) => AcceptStaffInviteRequest(
        phoneNumber: json['phoneNumber'] as String,
        code: json['code'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
        'code': code,
        'password': password,
      };
}

/// Кем человек стал: клуб и его логин в нём.
///
/// Контракт: Identity/AcceptStaffInviteRequest.cs
class AcceptStaffInviteResponse {
  const AcceptStaffInviteResponse({
    required this.organizationId,
    required this.userName,
  });

  final String organizationId;
  final String userName;

  factory AcceptStaffInviteResponse.fromJson(Map<String, dynamic> json) => AcceptStaffInviteResponse(
        organizationId: json['organizationId'] as String,
        userName: json['userName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'userName': userName,
      };
}

/// Контракт: Players/ActiveSessionDto.cs
class ActiveSessionDto {
  const ActiveSessionDto({
    required this.sessionId,
    required this.seatId,
    required this.seatName,
    required this.startedAtUtc,
    required this.durationMode,
    this.remainingSeconds,
    this.accruedCostMinorUnits,
    required this.currencyCode,
    this.tariffName,
    this.pricePerHourMinorUnits,
    this.zoneName,
  });

  final String sessionId;
  final String seatId;
  final String seatName;
  final DateTime startedAtUtc;

  /// Режим сессии. "fixed" — оплачена наперёд, показывается остаток; "open" — счётчик времени и
  /// накопленная стоимость.
  /// "open" | "fixed"
  final String durationMode;

  /// fixed only
  final int? remainingSeconds;

  /// open only
  final int? accruedCostMinorUnits;
  final String currencyCode;

  /// По какой цене идёт счёт и где человек сидит. Растущая сумма без ставки — это число, которое
  /// нечем проверить: видно, что платишь, и не видно, за что. Цена — за час, а не за минуту:
  /// клуб продаёт часы, и в них же человек считает.
  /// Пусто там, где тарифа у сессии нет вовсе (гостевая, заведённая на стойке руками) — врать
  /// подставленной ставкой хуже, чем честно промолчать.
  final String? tariffName;
  final int? pricePerHourMinorUnits;
  final String? zoneName;

  factory ActiveSessionDto.fromJson(Map<String, dynamic> json) => ActiveSessionDto(
        sessionId: json['sessionId'] as String,
        seatId: json['seatId'] as String,
        seatName: json['seatName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        durationMode: json['durationMode'] as String,
        remainingSeconds: json['remainingSeconds'] == null ? null : (json['remainingSeconds'] as num).toInt(),
        accruedCostMinorUnits: json['accruedCostMinorUnits'] == null ? null : (json['accruedCostMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        tariffName: json['tariffName'] == null ? null : json['tariffName'] as String,
        pricePerHourMinorUnits: json['pricePerHourMinorUnits'] == null ? null : (json['pricePerHourMinorUnits'] as num).toInt(),
        zoneName: json['zoneName'] == null ? null : json['zoneName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'seatId': seatId,
        'seatName': seatName,
        'startedAtUtc': startedAtUtc.toIso8601String(),
        'durationMode': durationMode,
        'remainingSeconds': remainingSeconds,
        'accruedCostMinorUnits': accruedCostMinorUnits,
        'currencyCode': currencyCode,
        'tariffName': tariffName,
        'pricePerHourMinorUnits': pricePerHourMinorUnits,
        'zoneName': zoneName,
      };
}

/// Контракт: Ads/AdContracts.cs
class AdCampaignDto {
  const AdCampaignDto({
    required this.campaignId,
    required this.advertiserId,
    required this.advertiserName,
    required this.name,
    required this.category,
    required this.startsAtUtc,
    required this.endsAtUtc,
    required this.cities,
    required this.organizationIds,
    required this.state,
    required this.creatives,
    required this.createdAtUtc,
    required this.updatedAtUtc,
  });

  final String campaignId;
  final String advertiserId;
  final String advertiserName;
  final String name;

  /// Одно из AdCategoryNames
  final String category;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;

  /// Пусто — все города.
  final List<String> cities;

  /// Пусто — все клубы.
  final List<String> organizationIds;

  /// Одно из AdCampaignStateNames
  final String state;
  final List<AdCreativeDto> creatives;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;

  factory AdCampaignDto.fromJson(Map<String, dynamic> json) => AdCampaignDto(
        campaignId: json['campaignId'] as String,
        advertiserId: json['advertiserId'] as String,
        advertiserName: json['advertiserName'] as String,
        name: json['name'] as String,
        category: json['category'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        cities: (json['cities'] as List<dynamic>).map((item) => item as String).toList(),
        organizationIds: (json['organizationIds'] as List<dynamic>).map((item) => item as String).toList(),
        state: json['state'] as String,
        creatives: (json['creatives'] as List<dynamic>).map((item) => AdCreativeDto.fromJson(item as Map<String, dynamic>)).toList(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'campaignId': campaignId,
        'advertiserId': advertiserId,
        'advertiserName': advertiserName,
        'name': name,
        'category': category,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'cities': cities.map((item) => item).toList(),
        'organizationIds': organizationIds.map((item) => item).toList(),
        'state': state,
        'creatives': creatives.map((item) => item.toJson()).toList(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
      };
}

/// Контракт: Ads/AdContracts.cs
class AdCreativeDto {
  const AdCreativeDto({
    required this.creativeId,
    required this.campaignId,
    required this.title,
    this.body,
    this.imageUrl,
    required this.moderation,
    this.rejectedReason,
    this.moderatedAtUtc,
    required this.createdAtUtc,
  });

  final String creativeId;
  final String campaignId;
  final String title;
  final String? body;
  final String? imageUrl;

  /// Одно из AdModerationNames
  final String moderation;
  final String? rejectedReason;
  final DateTime? moderatedAtUtc;
  final DateTime createdAtUtc;

  factory AdCreativeDto.fromJson(Map<String, dynamic> json) => AdCreativeDto(
        creativeId: json['creativeId'] as String,
        campaignId: json['campaignId'] as String,
        title: json['title'] as String,
        body: json['body'] == null ? null : json['body'] as String,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
        moderation: json['moderation'] as String,
        rejectedReason: json['rejectedReason'] == null ? null : json['rejectedReason'] as String,
        moderatedAtUtc: json['moderatedAtUtc'] == null ? null : DateTime.parse(json['moderatedAtUtc'] as String),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'creativeId': creativeId,
        'campaignId': campaignId,
        'title': title,
        'body': body,
        'imageUrl': imageUrl,
        'moderation': moderation,
        'rejectedReason': rejectedReason,
        'moderatedAtUtc': moderatedAtUtc?.toIso8601String(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Inventory/AddProductBarcodeRequest.cs
class AddProductBarcodeRequest {
  const AddProductBarcodeRequest({
    required this.organizationId,
    required this.code,
    this.isPrimary,
  });

  final String organizationId;
  final String code;
  final bool? isPrimary;

  factory AddProductBarcodeRequest.fromJson(Map<String, dynamic> json) => AddProductBarcodeRequest(
        organizationId: json['organizationId'] as String,
        code: json['code'] as String,
        isPrimary: json['isPrimary'] == null ? null : json['isPrimary'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'code': code,
        'isPrimary': isPrimary,
      };
}

/// Строка отчёта показов: креатив в филиале за день. Игрока в строке нет и быть не может.
///
/// Контракт: Ads/AdContracts.cs
class AdImpressionRowDto {
  const AdImpressionRowDto({
    required this.day,
    required this.campaignId,
    required this.campaignName,
    required this.creativeId,
    required this.creativeTitle,
    required this.organizationId,
    required this.organizationName,
    required this.branchId,
    required this.branchName,
    required this.city,
    required this.impressions,
    required this.shownSeconds,
  });

  final String day;
  final String campaignId;
  final String campaignName;
  final String creativeId;
  final String creativeTitle;
  final String organizationId;
  final String organizationName;
  final String branchId;
  final String branchName;
  final String city;
  final int impressions;
  final int shownSeconds;

  factory AdImpressionRowDto.fromJson(Map<String, dynamic> json) => AdImpressionRowDto(
        day: json['day'] as String,
        campaignId: json['campaignId'] as String,
        campaignName: json['campaignName'] as String,
        creativeId: json['creativeId'] as String,
        creativeTitle: json['creativeTitle'] as String,
        organizationId: json['organizationId'] as String,
        organizationName: json['organizationName'] as String,
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String,
        city: json['city'] as String,
        impressions: (json['impressions'] as num).toInt(),
        shownSeconds: (json['shownSeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'day': day,
        'campaignId': campaignId,
        'campaignName': campaignName,
        'creativeId': creativeId,
        'creativeTitle': creativeTitle,
        'organizationId': organizationId,
        'organizationName': organizationName,
        'branchId': branchId,
        'branchName': branchName,
        'city': city,
        'impressions': impressions,
        'shownSeconds': shownSeconds,
      };
}

/// Реклама платформы в витрине свободного ПК (спека `2026-09-25-platform-ads-design.md`). Продаёт
/// её AFK4, показывается она только клубам с фичей `platform_ads` — это бесплатный тариф.
///
/// Контракт: Ads/AdContracts.cs
class AdvertiserDto {
  const AdvertiserDto({
    required this.advertiserId,
    required this.name,
    required this.contact,
    required this.createdAtUtc,
  });

  final String advertiserId;
  final String name;
  final String contact;
  final DateTime createdAtUtc;

  factory AdvertiserDto.fromJson(Map<String, dynamic> json) => AdvertiserDto(
        advertiserId: json['advertiserId'] as String,
        name: json['name'] as String,
        contact: json['contact'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'advertiserId': advertiserId,
        'name': name,
        'contact': contact,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Точка помесячного ряда. Год и месяц едут числами: название месяца — дело клиента,
/// у которого есть язык пользователя.
///
/// Контракт: Platform/Analytics/PlatformAnalyticsContracts.cs
class AnalyticsMonthDto {
  const AnalyticsMonthDto({
    required this.year,
    required this.month,
    required this.recurringMinorUnits,
    required this.oneOffMinorUnits,
    required this.joined,
    required this.left,
    required this.payingAtMonthEnd,
  });

  final int year;
  final int month;
  final int recurringMinorUnits;
  final int oneOffMinorUnits;
  final int joined;
  final int left;
  final int payingAtMonthEnd;

  factory AnalyticsMonthDto.fromJson(Map<String, dynamic> json) => AnalyticsMonthDto(
        year: (json['year'] as num).toInt(),
        month: (json['month'] as num).toInt(),
        recurringMinorUnits: (json['recurringMinorUnits'] as num).toInt(),
        oneOffMinorUnits: (json['oneOffMinorUnits'] as num).toInt(),
        joined: (json['joined'] as num).toInt(),
        left: (json['left'] as num).toInt(),
        payingAtMonthEnd: (json['payingAtMonthEnd'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'year': year,
        'month': month,
        'recurringMinorUnits': recurringMinorUnits,
        'oneOffMinorUnits': oneOffMinorUnits,
        'joined': joined,
        'left': left,
        'payingAtMonthEnd': payingAtMonthEnd,
      };
}

/// Анонс глазами клуба: только то, что ему показывают, плюс прочитал ли ЭТОТ сотрудник.
///
/// Контракт: Platform/Announcements/AnnouncementContracts.cs
class AnnouncementFeedItemDto {
  const AnnouncementFeedItemDto({
    required this.announcementId,
    required this.title,
    required this.body,
    required this.severity,
    required this.showFromUtc,
    required this.showUntilUtc,
    required this.publishedAtUtc,
    required this.isRead,
  });

  final String announcementId;
  final String title;
  final String body;
  final String severity;
  final DateTime showFromUtc;
  final DateTime showUntilUtc;
  final DateTime publishedAtUtc;
  final bool isRead;

  factory AnnouncementFeedItemDto.fromJson(Map<String, dynamic> json) => AnnouncementFeedItemDto(
        announcementId: json['announcementId'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        severity: json['severity'] as String,
        showFromUtc: DateTime.parse(json['showFromUtc'] as String),
        showUntilUtc: DateTime.parse(json['showUntilUtc'] as String),
        publishedAtUtc: DateTime.parse(json['publishedAtUtc'] as String),
        isRead: json['isRead'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'announcementId': announcementId,
        'title': title,
        'body': body,
        'severity': severity,
        'showFromUtc': showFromUtc.toIso8601String(),
        'showUntilUtc': showUntilUtc.toIso8601String(),
        'publishedAtUtc': publishedAtUtc.toIso8601String(),
        'isRead': isRead,
      };
}

/// Контракт: Devices/AssignDeviceSeatRequest.cs
class AssignDeviceSeatRequest {
  const AssignDeviceSeatRequest({
    required this.organizationId,
    required this.seatId,
  });

  final String organizationId;
  final String seatId;

  factory AssignDeviceSeatRequest.fromJson(Map<String, dynamic> json) => AssignDeviceSeatRequest(
        organizationId: json['organizationId'] as String,
        seatId: json['seatId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'seatId': seatId,
      };
}

/// Контракт: Audit/AuditRecordDto.cs
class AuditRecordDto {
  const AuditRecordDto({
    required this.auditRecordId,
    required this.organizationId,
    this.branchId,
    this.actorStaffUserId,
    required this.action,
    required this.targetType,
    this.targetId,
    required this.outcome,
    required this.sourceApp,
    required this.detailsJson,
    required this.createdAtUtc,
    this.actorPlatformAdminUserId,
    this.organizationName,
    this.amountMinorUnits,
  });

  final String auditRecordId;
  final String organizationId;
  final String? branchId;
  final String? actorStaffUserId;
  final String action;
  final String targetType;
  final String? targetId;
  final String outcome;
  final String sourceApp;
  final String detailsJson;
  final DateTime createdAtUtc;
  final String? actorPlatformAdminUserId;

  /// Имя клуба-клиента, к которому относится запись. Панель платформы смотрит журнал
  /// поверх всей сети, и опознавательный знак «кто» там — имя, а не идентификатор: наизусть их
  /// не знает никто. Пусто, если организация к моменту чтения журнала уже удалена.
  final String? organizationName;
  final int? amountMinorUnits;

  factory AuditRecordDto.fromJson(Map<String, dynamic> json) => AuditRecordDto(
        auditRecordId: json['auditRecordId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        actorStaffUserId: json['actorStaffUserId'] == null ? null : json['actorStaffUserId'] as String,
        action: json['action'] as String,
        targetType: json['targetType'] as String,
        targetId: json['targetId'] == null ? null : json['targetId'] as String,
        outcome: json['outcome'] as String,
        sourceApp: json['sourceApp'] as String,
        detailsJson: json['detailsJson'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        actorPlatformAdminUserId: json['actorPlatformAdminUserId'] == null ? null : json['actorPlatformAdminUserId'] as String,
        organizationName: json['organizationName'] == null ? null : json['organizationName'] as String,
        amountMinorUnits: json['amountMinorUnits'] == null ? null : (json['amountMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'auditRecordId': auditRecordId,
        'organizationId': organizationId,
        'branchId': branchId,
        'actorStaffUserId': actorStaffUserId,
        'action': action,
        'targetType': targetType,
        'targetId': targetId,
        'outcome': outcome,
        'sourceApp': sourceApp,
        'detailsJson': detailsJson,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'actorPlatformAdminUserId': actorPlatformAdminUserId,
        'organizationName': organizationName,
        'amountMinorUnits': amountMinorUnits,
      };
}

/// Контракт: Audit/AuditSearchResultDto.cs
class AuditSearchResultDto {
  const AuditSearchResultDto({
    required this.records,
    required this.limit,
  });

  final List<AuditRecordDto> records;
  final int limit;

  factory AuditSearchResultDto.fromJson(Map<String, dynamic> json) => AuditSearchResultDto(
        records: (json['records'] as List<dynamic>).map((item) => AuditRecordDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'records': records.map((item) => item.toJson()).toList(),
        'limit': limit,
      };
}

/// Create-seat request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.
///
/// Контракт: Install/AuthenticatedInstallRequests.cs
class AuthenticatedInstallCreateSeatRequest {
  const AuthenticatedInstallCreateSeatRequest({
    required this.branchId,
    required this.zoneId,
    required this.name,
  });

  final String branchId;
  final String zoneId;
  final String name;

  factory AuthenticatedInstallCreateSeatRequest.fromJson(Map<String, dynamic> json) => AuthenticatedInstallCreateSeatRequest(
        branchId: json['branchId'] as String,
        zoneId: json['zoneId'] as String,
        name: json['name'] as String,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'zoneId': zoneId,
        'name': name,
      };
}

/// Device-enroll request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.
///
/// Контракт: Install/AuthenticatedInstallRequests.cs
class AuthenticatedInstallEnrollRequest {
  const AuthenticatedInstallEnrollRequest({
    required this.branchId,
    this.seatId,
    required this.role,
    required this.displayName,
    required this.machineName,
    required this.devicePublicKey,
  });

  final String branchId;
  final String? seatId;
  final String role;
  final String displayName;
  final String machineName;
  final String devicePublicKey;

  factory AuthenticatedInstallEnrollRequest.fromJson(Map<String, dynamic> json) => AuthenticatedInstallEnrollRequest(
        branchId: json['branchId'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        role: json['role'] as String,
        displayName: json['displayName'] as String,
        machineName: json['machineName'] as String,
        devicePublicKey: json['devicePublicKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'seatId': seatId,
        'role': role,
        'displayName': displayName,
        'machineName': machineName,
        'devicePublicKey': devicePublicKey,
      };
}

/// Правило закрытия окна: часть заголовка, класс окна или оба сразу.
///
/// Контракт: Devices/ProtectionProfileContracts.cs
class BlockedWindowRuleDto {
  const BlockedWindowRuleDto({
    this.titleContains,
    this.className,
  });

  final String? titleContains;
  final String? className;

  factory BlockedWindowRuleDto.fromJson(Map<String, dynamic> json) => BlockedWindowRuleDto(
        titleContains: json['titleContains'] == null ? null : json['titleContains'] as String,
        className: json['className'] == null ? null : json['className'] as String,
      );

  Map<String, dynamic> toJson() => {
        'titleContains': titleContains,
        'className': className,
      };
}

/// Настройки приёма гостей у филиала — то, что видит и правит клуб.
/// UpdatedAtUtc пуст, пока филиал ничего не настраивал: значения в этом случае
/// не «нулевые», а по умолчанию, и админу полезно отличать одно от другого.
///
/// Контракт: Branches/BranchBookingSettingsDto.cs
class BranchBookingSettingsDto {
  const BranchBookingSettingsDto({
    required this.organizationId,
    required this.branchId,
    required this.acceptanceMode,
    required this.respondWithinMinutes,
    required this.requirePrepaymentFromNewGuests,
    required this.maxActiveReservationsForNewGuests,
    required this.regularAfterVisits,
    required this.holdSeatAfterStartMinutes,
    required this.keepPrepaymentOnNoShow,
    this.updatedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String acceptanceMode;
  final int respondWithinMinutes;
  final bool requirePrepaymentFromNewGuests;
  final int maxActiveReservationsForNewGuests;
  final int regularAfterVisits;
  final int holdSeatAfterStartMinutes;
  final bool keepPrepaymentOnNoShow;
  final DateTime? updatedAtUtc;

  factory BranchBookingSettingsDto.fromJson(Map<String, dynamic> json) => BranchBookingSettingsDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        acceptanceMode: json['acceptanceMode'] as String,
        respondWithinMinutes: (json['respondWithinMinutes'] as num).toInt(),
        requirePrepaymentFromNewGuests: json['requirePrepaymentFromNewGuests'] as bool,
        maxActiveReservationsForNewGuests: (json['maxActiveReservationsForNewGuests'] as num).toInt(),
        regularAfterVisits: (json['regularAfterVisits'] as num).toInt(),
        holdSeatAfterStartMinutes: (json['holdSeatAfterStartMinutes'] as num).toInt(),
        keepPrepaymentOnNoShow: json['keepPrepaymentOnNoShow'] as bool,
        updatedAtUtc: json['updatedAtUtc'] == null ? null : DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'acceptanceMode': acceptanceMode,
        'respondWithinMinutes': respondWithinMinutes,
        'requirePrepaymentFromNewGuests': requirePrepaymentFromNewGuests,
        'maxActiveReservationsForNewGuests': maxActiveReservationsForNewGuests,
        'regularAfterVisits': regularAfterVisits,
        'holdSeatAfterStartMinutes': holdSeatAfterStartMinutes,
        'keepPrepaymentOnNoShow': keepPrepaymentOnNoShow,
        'updatedAtUtc': updatedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Diagnostics/BranchDiagnosticsDto.cs
class BranchDiagnosticsDto {
  const BranchDiagnosticsDto({
    required this.organizationId,
    required this.branchId,
    required this.generatedAtUtc,
    required this.deviceSummary,
    required this.commandSummary,
    required this.updateSummary,
    required this.staleDevices,
  });

  final String organizationId;
  final String branchId;
  final DateTime generatedAtUtc;
  final DeviceDiagnosticsSummaryDto deviceSummary;
  final CommandDiagnosticsSummaryDto commandSummary;
  final UpdateDiagnosticsSummaryDto updateSummary;
  final List<StaleDeviceDiagnosticsDto> staleDevices;

  factory BranchDiagnosticsDto.fromJson(Map<String, dynamic> json) => BranchDiagnosticsDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        generatedAtUtc: DateTime.parse(json['generatedAtUtc'] as String),
        deviceSummary: DeviceDiagnosticsSummaryDto.fromJson(json['deviceSummary'] as Map<String, dynamic>),
        commandSummary: CommandDiagnosticsSummaryDto.fromJson(json['commandSummary'] as Map<String, dynamic>),
        updateSummary: UpdateDiagnosticsSummaryDto.fromJson(json['updateSummary'] as Map<String, dynamic>),
        staleDevices: (json['staleDevices'] as List<dynamic>).map((item) => StaleDeviceDiagnosticsDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'generatedAtUtc': generatedAtUtc.toIso8601String(),
        'deviceSummary': deviceSummary.toJson(),
        'commandSummary': commandSummary.toJson(),
        'updateSummary': updateSummary.toJson(),
        'staleDevices': staleDevices.map((item) => item.toJson()).toList(),
      };
}

/// Одни свёрнутые сутки клуба. `AgentAlive == null` — «неизвестно», не «мёртв».
///
/// Контракт: Platform/Analytics/BranchDynamicsContracts.cs
class BranchDynamicsDayDto {
  const BranchDynamicsDayDto({
    required this.date,
    required this.sessionCount,
    required this.revenue,
    required this.shiftOpenedCount,
    this.agentAlive,
  });

  final String date;
  final int sessionCount;
  final MoneyDto revenue;
  final int shiftOpenedCount;
  final bool? agentAlive;

  factory BranchDynamicsDayDto.fromJson(Map<String, dynamic> json) => BranchDynamicsDayDto(
        date: json['date'] as String,
        sessionCount: (json['sessionCount'] as num).toInt(),
        revenue: MoneyDto.fromJson(json['revenue'] as Map<String, dynamic>),
        shiftOpenedCount: (json['shiftOpenedCount'] as num).toInt(),
        agentAlive: json['agentAlive'] == null ? null : json['agentAlive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'date': date,
        'sessionCount': sessionCount,
        'revenue': revenue.toJson(),
        'shiftOpenedCount': shiftOpenedCount,
        'agentAlive': agentAlive,
      };
}

/// Контракт: Platform/Analytics/BranchDynamicsContracts.cs
class BranchDynamicsDto {
  const BranchDynamicsDto({
    required this.organizationId,
    required this.branchId,
    required this.fromDate,
    required this.toDate,
    required this.totalRevenue,
    required this.totalSessionCount,
    required this.daysWithoutAgent,
    required this.daysWithUnknownAgent,
    required this.missingDayCount,
    required this.days,
  });

  final String organizationId;
  final String branchId;
  final String fromDate;
  final String toDate;
  final MoneyDto totalRevenue;
  final int totalSessionCount;
  final int daysWithoutAgent;
  final int daysWithUnknownAgent;

  /// Сутки окна, за которые снимка нет вовсе. Нулями они НЕ дорисовываются.
  final int missingDayCount;
  final List<BranchDynamicsDayDto> days;

  factory BranchDynamicsDto.fromJson(Map<String, dynamic> json) => BranchDynamicsDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        fromDate: json['fromDate'] as String,
        toDate: json['toDate'] as String,
        totalRevenue: MoneyDto.fromJson(json['totalRevenue'] as Map<String, dynamic>),
        totalSessionCount: (json['totalSessionCount'] as num).toInt(),
        daysWithoutAgent: (json['daysWithoutAgent'] as num).toInt(),
        daysWithUnknownAgent: (json['daysWithUnknownAgent'] as num).toInt(),
        missingDayCount: (json['missingDayCount'] as num).toInt(),
        days: (json['days'] as List<dynamic>).map((item) => BranchDynamicsDayDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'fromDate': fromDate,
        'toDate': toDate,
        'totalRevenue': totalRevenue.toJson(),
        'totalSessionCount': totalSessionCount,
        'daysWithoutAgent': daysWithoutAgent,
        'daysWithUnknownAgent': daysWithUnknownAgent,
        'missingDayCount': missingDayCount,
        'days': days.map((item) => item.toJson()).toList(),
      };
}

/// Игра в библиотеке филиала — то, что увидит игрок на ПК.
///
/// Контракт: Games/GameLibraryContracts.cs
class BranchGameDto {
  const BranchGameDto({
    required this.branchGameId,
    this.catalogGameId,
    required this.name,
    this.genre,
    this.minAge,
    this.coverUrl,
    required this.launchKind,
    this.launchTarget,
    this.executablePath,
    this.arguments,
    required this.availableWithoutSession,
    required this.isEnabled,
    required this.sortOrder,
    this.launchOnSessionStart,
  });

  final String branchGameId;

  /// Из каталога — тогда обложка и возраст берутся оттуда; null — своя игра клуба.
  final String? catalogGameId;
  final String name;
  final String? genre;
  final int? minAge;
  final String? coverUrl;

  /// Одно из GameLaunchKindNames.
  final String launchKind;
  final String? launchTarget;

  /// Свой путь к exe вместо лаунчера — когда игра стоит не там, где её ищет агент.
  final String? executablePath;
  final String? arguments;

  /// Запускается и без сессии: лаунчер для пополнения Steam, например.
  final bool availableWithoutSession;
  final bool isEnabled;
  final int sortOrder;

  /// Запускается сам в начале сессии: Discord, клиент Steam.
  final bool? launchOnSessionStart;

  factory BranchGameDto.fromJson(Map<String, dynamic> json) => BranchGameDto(
        branchGameId: json['branchGameId'] as String,
        catalogGameId: json['catalogGameId'] == null ? null : json['catalogGameId'] as String,
        name: json['name'] as String,
        genre: json['genre'] == null ? null : json['genre'] as String,
        minAge: json['minAge'] == null ? null : (json['minAge'] as num).toInt(),
        coverUrl: json['coverUrl'] == null ? null : json['coverUrl'] as String,
        launchKind: json['launchKind'] as String,
        launchTarget: json['launchTarget'] == null ? null : json['launchTarget'] as String,
        executablePath: json['executablePath'] == null ? null : json['executablePath'] as String,
        arguments: json['arguments'] == null ? null : json['arguments'] as String,
        availableWithoutSession: json['availableWithoutSession'] as bool,
        isEnabled: json['isEnabled'] as bool,
        sortOrder: (json['sortOrder'] as num).toInt(),
        launchOnSessionStart: json['launchOnSessionStart'] == null ? null : json['launchOnSessionStart'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'branchGameId': branchGameId,
        'catalogGameId': catalogGameId,
        'name': name,
        'genre': genre,
        'minAge': minAge,
        'coverUrl': coverUrl,
        'launchKind': launchKind,
        'launchTarget': launchTarget,
        'executablePath': executablePath,
        'arguments': arguments,
        'availableWithoutSession': availableWithoutSession,
        'isEnabled': isEnabled,
        'sortOrder': sortOrder,
        'launchOnSessionStart': launchOnSessionStart,
      };
}

/// Одно фото зала. MediaId нужен, чтобы удалить объект из хранилища вместе со
/// строкой галереи; у фото, добавленного ссылкой, его нет.
///
/// Контракт: Branches/BranchPhotoDto.cs
class BranchPhotoDto {
  const BranchPhotoDto({
    required this.url,
    this.mediaId,
  });

  final String url;
  final String? mediaId;

  factory BranchPhotoDto.fromJson(Map<String, dynamic> json) => BranchPhotoDto(
        url: json['url'] as String,
        mediaId: json['mediaId'] == null ? null : json['mediaId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'url': url,
        'mediaId': mediaId,
      };
}

/// Контракт: Branches/BranchProfileDto.cs
class BranchProfileDto {
  const BranchProfileDto({
    required this.organizationId,
    required this.branchId,
    required this.name,
    required this.city,
    this.description,
    this.address,
    this.phone,
    this.telegram,
    this.website,
    this.instagram,
    this.logoUrl,
    this.logoMediaId,
    this.coverImageUrl,
    this.coverMediaId,
    required this.photos,
    this.latitude,
    this.longitude,
    required this.timeZone,
    required this.locale,
    required this.workingHours,
    required this.createdAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String name;
  final String city;
  final String? description;
  final String? address;
  final String? phone;
  final String? telegram;
  final String? website;
  final String? instagram;
  final String? logoUrl;
  final String? logoMediaId;
  final String? coverImageUrl;
  final String? coverMediaId;
  final List<BranchPhotoDto> photos;
  final double? latitude;
  final double? longitude;
  final String timeZone;
  final String locale;
  final List<BranchWorkingHoursDayDto> workingHours;
  final DateTime createdAtUtc;

  factory BranchProfileDto.fromJson(Map<String, dynamic> json) => BranchProfileDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        city: json['city'] as String,
        description: json['description'] == null ? null : json['description'] as String,
        address: json['address'] == null ? null : json['address'] as String,
        phone: json['phone'] == null ? null : json['phone'] as String,
        telegram: json['telegram'] == null ? null : json['telegram'] as String,
        website: json['website'] == null ? null : json['website'] as String,
        instagram: json['instagram'] == null ? null : json['instagram'] as String,
        logoUrl: json['logoUrl'] == null ? null : json['logoUrl'] as String,
        logoMediaId: json['logoMediaId'] == null ? null : json['logoMediaId'] as String,
        coverImageUrl: json['coverImageUrl'] == null ? null : json['coverImageUrl'] as String,
        coverMediaId: json['coverMediaId'] == null ? null : json['coverMediaId'] as String,
        photos: (json['photos'] as List<dynamic>).map((item) => BranchPhotoDto.fromJson(item as Map<String, dynamic>)).toList(),
        latitude: json['latitude'] == null ? null : (json['latitude'] as num).toDouble(),
        longitude: json['longitude'] == null ? null : (json['longitude'] as num).toDouble(),
        timeZone: json['timeZone'] as String,
        locale: json['locale'] as String,
        workingHours: (json['workingHours'] as List<dynamic>).map((item) => BranchWorkingHoursDayDto.fromJson(item as Map<String, dynamic>)).toList(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'name': name,
        'city': city,
        'description': description,
        'address': address,
        'phone': phone,
        'telegram': telegram,
        'website': website,
        'instagram': instagram,
        'logoUrl': logoUrl,
        'logoMediaId': logoMediaId,
        'coverImageUrl': coverImageUrl,
        'coverMediaId': coverMediaId,
        'photos': photos.map((item) => item.toJson()).toList(),
        'latitude': latitude,
        'longitude': longitude,
        'timeZone': timeZone,
        'locale': locale,
        'workingHours': workingHours.map((item) => item.toJson()).toList(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Профиль защиты филиала для Панели: сам профиль и кто его менял последним.
///
/// Контракт: Devices/ProtectionProfileContracts.cs
class BranchProtectionProfileDto {
  const BranchProtectionProfileDto({
    required this.organizationId,
    required this.branchId,
    required this.profile,
    this.updatedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final ProtectionProfileDto profile;
  final DateTime? updatedAtUtc;

  factory BranchProtectionProfileDto.fromJson(Map<String, dynamic> json) => BranchProtectionProfileDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        profile: ProtectionProfileDto.fromJson(json['profile'] as Map<String, dynamic>),
        updatedAtUtc: json['updatedAtUtc'] == null ? null : DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'profile': profile.toJson(),
        'updatedAtUtc': updatedAtUtc?.toIso8601String(),
      };
}

/// Отзыв для клуба: кто, за каким ПК и когда — чтобы «мышь липкая» можно было найти на ПК 07,
/// а не гадать, о каком из тридцати речь.
///
/// Контракт: Reviews/ClubReviewDtos.cs
class BranchReviewDto {
  const BranchReviewDto({
    required this.reviewId,
    required this.playerAccountId,
    required this.authorName,
    required this.rating,
    this.comment,
    required this.createdAtUtc,
    required this.sessionId,
    this.seatName,
  });

  final String reviewId;
  final String playerAccountId;
  final String authorName;
  final int rating;
  final String? comment;
  final DateTime createdAtUtc;
  final String sessionId;
  final String? seatName;

  factory BranchReviewDto.fromJson(Map<String, dynamic> json) => BranchReviewDto(
        reviewId: json['reviewId'] as String,
        playerAccountId: json['playerAccountId'] as String,
        authorName: json['authorName'] as String,
        rating: (json['rating'] as num).toInt(),
        comment: json['comment'] == null ? null : json['comment'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        sessionId: json['sessionId'] as String,
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reviewId': reviewId,
        'playerAccountId': playerAccountId,
        'authorName': authorName,
        'rating': rating,
        'comment': comment,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'sessionId': sessionId,
        'seatName': seatName,
      };
}

/// Отзывы филиала для Панели: итог по всем оценкам и страница списка.
///
/// Контракт: Reviews/ClubReviewDtos.cs
class BranchReviewsPageDto {
  const BranchReviewsPageDto({
    this.rating,
    required this.reviewCount,
    required this.countsByRating,
    required this.items,
    this.nextBefore,
  });


  /// Пусто — оценок пока нет. Это не ноль звёзд.
  final double? rating;
  final int reviewCount;

  /// Сколько оценок на каждую звезду: [1★, 2★, 3★, 4★, 5★].
  final List<int> countsByRating;
  final List<BranchReviewDto> items;

  /// Следующая страница — отзывы раньше этого времени; null — дальше нет.
  final DateTime? nextBefore;

  factory BranchReviewsPageDto.fromJson(Map<String, dynamic> json) => BranchReviewsPageDto(
        rating: json['rating'] == null ? null : (json['rating'] as num).toDouble(),
        reviewCount: (json['reviewCount'] as num).toInt(),
        countsByRating: (json['countsByRating'] as List<dynamic>).map((item) => (item as num).toInt()).toList(),
        items: (json['items'] as List<dynamic>).map((item) => BranchReviewDto.fromJson(item as Map<String, dynamic>)).toList(),
        nextBefore: json['nextBefore'] == null ? null : DateTime.parse(json['nextBefore'] as String),
      );

  Map<String, dynamic> toJson() => {
        'rating': rating,
        'reviewCount': reviewCount,
        'countsByRating': countsByRating.map((item) => item).toList(),
        'items': items.map((item) => item.toJson()).toList(),
        'nextBefore': nextBefore?.toIso8601String(),
      };
}

/// Одна находка палитры: чем это открыть (Kind и Id) и как
/// узнать глазами (остальное).
/// <param name="Subtitle">
/// То, чем различают похожие строки: зал у места, телефон у клиента и у брони. Пусто там, где
/// различать нечем.
/// </param>
/// <param name="OccursAtUtc">
/// К какому моменту относится находка: начало брони, дата чека. Сырое время, а не готовая
/// подпись, — язык и часовой пояс знает клиент, а не сервер.
/// </param>
/// <param name="Status">
/// Где находка сейчас, если у неё есть ход жизни: у заказа — новый, готовится, выдан или отменён.
/// Код, а не подпись: подпись на своём языке ставит клиент.
/// </param>
/// <param name="Number">
/// Номер, по которому её называют, когда он не в заголовке: у заказа — номер его чека. По нему
/// человек и узнаёт, что нашлось именно то, что он набирал.
/// </param>
///
/// Контракт: Operator/BranchSearchResultDto.cs
class BranchSearchResultDto {
  const BranchSearchResultDto({
    required this.kind,
    required this.id,
    required this.title,
    this.subtitle,
    this.occursAtUtc,
    this.amountMinorUnits,
    this.currencyCode,
    this.status,
    this.number,
  });

  final String kind;
  final String id;
  final String title;
  final String? subtitle;
  final DateTime? occursAtUtc;
  final int? amountMinorUnits;
  final String? currencyCode;
  final String? status;
  final String? number;

  factory BranchSearchResultDto.fromJson(Map<String, dynamic> json) => BranchSearchResultDto(
        kind: json['kind'] as String,
        id: json['id'] as String,
        title: json['title'] as String,
        subtitle: json['subtitle'] == null ? null : json['subtitle'] as String,
        occursAtUtc: json['occursAtUtc'] == null ? null : DateTime.parse(json['occursAtUtc'] as String),
        amountMinorUnits: json['amountMinorUnits'] == null ? null : (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
        status: json['status'] == null ? null : json['status'] as String,
        number: json['number'] == null ? null : json['number'] as String,
      );

  Map<String, dynamic> toJson() => {
        'kind': kind,
        'id': id,
        'title': title,
        'subtitle': subtitle,
        'occursAtUtc': occursAtUtc?.toIso8601String(),
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'status': status,
        'number': number,
      };
}

/// Контракт: Branches/BranchSettingsDto.cs
class BranchSettingsDto {
  const BranchSettingsDto({
    required this.organizationId,
    required this.branchId,
    required this.requireManualDeviceApproval,
    required this.preferredLocale,
    this.shiftDiscrepancyToleranceMinorUnits,
  });

  final String organizationId;
  final String branchId;
  final bool requireManualDeviceApproval;
  final String preferredLocale;

  /// Допустимое расхождение кассы при закрытии смены, в минорных единицах. Больше него смену
  /// закрывает только подпись второго менеджера (анти-фрод §5.7), и стойка должна знать порог
  /// заранее: спросить подпись до отправки честнее, чем отказать после.
  /// Отдаётся уже разрешённым — с подставленным умолчанием, если у филиала своего нет.
  final int? shiftDiscrepancyToleranceMinorUnits;

  factory BranchSettingsDto.fromJson(Map<String, dynamic> json) => BranchSettingsDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        requireManualDeviceApproval: json['requireManualDeviceApproval'] as bool,
        preferredLocale: json['preferredLocale'] as String,
        shiftDiscrepancyToleranceMinorUnits: json['shiftDiscrepancyToleranceMinorUnits'] == null ? null : (json['shiftDiscrepancyToleranceMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'requireManualDeviceApproval': requireManualDeviceApproval,
        'preferredLocale': preferredLocale,
        'shiftDiscrepancyToleranceMinorUnits': shiftDiscrepancyToleranceMinorUnits,
      };
}

/// Один день расписания клуба. DayOfWeek по ISO-8601: 1=Пн … 7=Вс.
/// Время — строка "HH:mm" (24ч); при IsClosed времена игнорируются.
///
/// Контракт: Branches/BranchWorkingHoursDayDto.cs
class BranchWorkingHoursDayDto {
  const BranchWorkingHoursDayDto({
    required this.dayOfWeek,
    required this.isClosed,
    this.openTime,
    this.closeTime,
  });

  final int dayOfWeek;
  final bool isClosed;
  final String? openTime;
  final String? closeTime;

  factory BranchWorkingHoursDayDto.fromJson(Map<String, dynamic> json) => BranchWorkingHoursDayDto(
        dayOfWeek: (json['dayOfWeek'] as num).toInt(),
        isClosed: json['isClosed'] as bool,
        openTime: json['openTime'] == null ? null : json['openTime'] as String,
        closeTime: json['closeTime'] == null ? null : json['closeTime'] as String,
      );

  Map<String, dynamic> toJson() => {
        'dayOfWeek': dayOfWeek,
        'isClosed': isClosed,
        'openTime': openTime,
        'closeTime': closeTime,
      };
}

/// Контракт: Tariffs/CalculateTariffRequest.cs
class CalculateTariffRequest {
  const CalculateTariffRequest({
    required this.organizationId,
    required this.tariffVersionId,
    required this.durationMinutes,
  });

  final String organizationId;
  final String tariffVersionId;
  final int durationMinutes;

  factory CalculateTariffRequest.fromJson(Map<String, dynamic> json) => CalculateTariffRequest(
        organizationId: json['organizationId'] as String,
        tariffVersionId: json['tariffVersionId'] as String,
        durationMinutes: (json['durationMinutes'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'tariffVersionId': tariffVersionId,
        'durationMinutes': durationMinutes,
      };
}

/// Контракт: Reservations/ReservationRequests.cs
class CancelReservationRequest {
  const CancelReservationRequest({
    required this.organizationId,
    required this.reason,
    required this.expectedVersion,
  });

  final String organizationId;
  final String reason;
  final int expectedVersion;

  factory CancelReservationRequest.fromJson(Map<String, dynamic> json) => CancelReservationRequest(
        organizationId: json['organizationId'] as String,
        reason: json['reason'] as String,
        expectedVersion: (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'reason': reason,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: Tournaments/TournamentDtos.cs
class CancelTournamentRequest {
  const CancelTournamentRequest({
    required this.reason,
  });

  final String reason;

  factory CancelTournamentRequest.fromJson(Map<String, dynamic> json) => CancelTournamentRequest(
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reason': reason,
      };
}

/// Начисление кешбэка: сколько, когда и за что.
///
/// Контракт: Loyalty/CashbackEntryDto.cs
class CashbackEntryDto {
  const CashbackEntryDto({
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.reason,
    required this.createdAtUtc,
  });

  final int amountMinorUnits;
  final String currencyCode;

  /// Служебная причина вида `cashback:topup` или `cashback:shop:{id}`. Разбирается на экране в
  /// человеческую подпись: показывать игроку внутреннее имя события незачем.
  final String reason;
  final DateTime createdAtUtc;

  factory CashbackEntryDto.fromJson(Map<String, dynamic> json) => CashbackEntryDto(
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        reason: json['reason'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'reason': reason,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Shifts/CashMovementDto.cs
class CashMovementDto {
  const CashMovementDto({
    required this.cashMovementId,
    required this.organizationId,
    required this.branchId,
    required this.shiftId,
    required this.createdByStaffUserId,
    required this.movementType,
    required this.amount,
    required this.reason,
    required this.createdAtUtc,
  });

  final String cashMovementId;
  final String organizationId;
  final String branchId;
  final String shiftId;
  final String createdByStaffUserId;
  final String movementType;
  final MoneyDto amount;
  final String reason;
  final DateTime createdAtUtc;

  factory CashMovementDto.fromJson(Map<String, dynamic> json) => CashMovementDto(
        cashMovementId: json['cashMovementId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        shiftId: json['shiftId'] as String,
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        movementType: json['movementType'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'cashMovementId': cashMovementId,
        'organizationId': organizationId,
        'branchId': branchId,
        'shiftId': shiftId,
        'createdByStaffUserId': createdByStaffUserId,
        'movementType': movementType,
        'amount': amount.toJson(),
        'reason': reason,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Reports/CashOperationReportResultDto.cs
class CashOperationReportResultDto {
  const CashOperationReportResultDto({
    required this.rows,
    required this.limit,
    required this.cashInTotal,
    required this.cashOutTotal,
    required this.netCashTotal,
  });

  final List<CashOperationReportRowDto> rows;
  final int limit;
  final MoneyDto cashInTotal;
  final MoneyDto cashOutTotal;
  final MoneyDto netCashTotal;

  factory CashOperationReportResultDto.fromJson(Map<String, dynamic> json) => CashOperationReportResultDto(
        rows: (json['rows'] as List<dynamic>).map((item) => CashOperationReportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
        cashInTotal: MoneyDto.fromJson(json['cashInTotal'] as Map<String, dynamic>),
        cashOutTotal: MoneyDto.fromJson(json['cashOutTotal'] as Map<String, dynamic>),
        netCashTotal: MoneyDto.fromJson(json['netCashTotal'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'rows': rows.map((item) => item.toJson()).toList(),
        'limit': limit,
        'cashInTotal': cashInTotal.toJson(),
        'cashOutTotal': cashOutTotal.toJson(),
        'netCashTotal': netCashTotal.toJson(),
      };
}

/// Контракт: Reports/CashOperationReportRowDto.cs
class CashOperationReportRowDto {
  const CashOperationReportRowDto({
    required this.operationId,
    required this.organizationId,
    required this.branchId,
    this.shiftId,
    required this.createdByStaffUserId,
    required this.sourceType,
    required this.operationType,
    required this.cashImpact,
    required this.reason,
    required this.createdAtUtc,
    this.createdByDisplayName,
  });

  final String operationId;
  final String organizationId;
  final String branchId;
  final String? shiftId;
  final String createdByStaffUserId;
  final String sourceType;
  final String operationType;
  final MoneyDto cashImpact;
  final String reason;
  final DateTime createdAtUtc;

  /// Кто провёл операцию. Журнал кассы отвечает на вопрос «кто взял деньги», а идентификатор
  /// сотрудника на этот вопрос не отвечает: показывать кассиру GUID — то же, что не показывать.
  final String? createdByDisplayName;

  factory CashOperationReportRowDto.fromJson(Map<String, dynamic> json) => CashOperationReportRowDto(
        operationId: json['operationId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        shiftId: json['shiftId'] == null ? null : json['shiftId'] as String,
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        sourceType: json['sourceType'] as String,
        operationType: json['operationType'] as String,
        cashImpact: MoneyDto.fromJson(json['cashImpact'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        createdByDisplayName: json['createdByDisplayName'] == null ? null : json['createdByDisplayName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'operationId': operationId,
        'organizationId': organizationId,
        'branchId': branchId,
        'shiftId': shiftId,
        'createdByStaffUserId': createdByStaffUserId,
        'sourceType': sourceType,
        'operationType': operationType,
        'cashImpact': cashImpact.toJson(),
        'reason': reason,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'createdByDisplayName': createdByDisplayName,
      };
}

/// Контракт: Shifts/ShiftRevenueDto.cs
class CashReconciliationDto {
  const CashReconciliationDto({
    required this.starting,
    required this.expected,
    this.counted,
    this.difference,
  });

  final MoneyDto starting;
  final MoneyDto expected;
  final MoneyDto? counted;
  final MoneyDto? difference;

  factory CashReconciliationDto.fromJson(Map<String, dynamic> json) => CashReconciliationDto(
        starting: MoneyDto.fromJson(json['starting'] as Map<String, dynamic>),
        expected: MoneyDto.fromJson(json['expected'] as Map<String, dynamic>),
        counted: json['counted'] == null ? null : MoneyDto.fromJson(json['counted'] as Map<String, dynamic>),
        difference: json['difference'] == null ? null : MoneyDto.fromJson(json['difference'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'starting': starting.toJson(),
        'expected': expected.toJson(),
        'counted': counted?.toJson(),
        'difference': difference?.toJson(),
      };
}

/// Игра в каталоге платформы — из него клубы добавляют игры себе.
///
/// Контракт: Games/GameLibraryContracts.cs
class CatalogGameDto {
  const CatalogGameDto({
    required this.catalogGameId,
    required this.name,
    this.description,
    this.genre,
    this.minAge,
    required this.launchKind,
    this.launchTarget,
    this.coverUrl,
    required this.isPublished,
    required this.updatedAtUtc,
  });

  final String catalogGameId;
  final String name;
  final String? description;
  final String? genre;

  /// Возрастная отметка: 0, 12, 16, 18. Даты рождения у игрока нет — отметка только видна.
  final int? minAge;

  /// Одно из GameLaunchKindNames.
  final String launchKind;

  /// AppID Steam, имя приложения Epic, продукт Riot, код Battle.net; для exe — путь по умолчанию.
  final String? launchTarget;
  final String? coverUrl;
  final bool isPublished;
  final DateTime updatedAtUtc;

  factory CatalogGameDto.fromJson(Map<String, dynamic> json) => CatalogGameDto(
        catalogGameId: json['catalogGameId'] as String,
        name: json['name'] as String,
        description: json['description'] == null ? null : json['description'] as String,
        genre: json['genre'] == null ? null : json['genre'] as String,
        minAge: json['minAge'] == null ? null : (json['minAge'] as num).toInt(),
        launchKind: json['launchKind'] as String,
        launchTarget: json['launchTarget'] == null ? null : json['launchTarget'] as String,
        coverUrl: json['coverUrl'] == null ? null : json['coverUrl'] as String,
        isPublished: json['isPublished'] as bool,
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'catalogGameId': catalogGameId,
        'name': name,
        'description': description,
        'genre': genre,
        'minAge': minAge,
        'launchKind': launchKind,
        'launchTarget': launchTarget,
        'coverUrl': coverUrl,
        'isPublished': isPublished,
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
      };
}

/// Контракт: Platform/Updates/PlatformUpdateContracts.cs
class ChangePlatformUpdatePackageStateRequest {
  const ChangePlatformUpdatePackageStateRequest({
    required this.state,
    required this.reason,
  });

  final String state;
  final String reason;

  factory ChangePlatformUpdatePackageStateRequest.fromJson(Map<String, dynamic> json) => ChangePlatformUpdatePackageStateRequest(
        state: json['state'] as String,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'state': state,
        'reason': reason,
      };
}

/// Контракт: Platform/Updates/PlatformUpdateContracts.cs
class ChangePlatformUpdateRolloutStateRequest {
  const ChangePlatformUpdateRolloutStateRequest({
    required this.state,
    required this.reason,
  });

  final String state;
  final String reason;

  factory ChangePlatformUpdateRolloutStateRequest.fromJson(Map<String, dynamic> json) => ChangePlatformUpdateRolloutStateRequest(
        state: json['state'] as String,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'state': state,
        'reason': reason,
      };
}

/// Контракт: Loyalty/ReferralContracts.cs
class ClaimReferralCodeRequest {
  const ClaimReferralCodeRequest({
    required this.code,
  });

  final String code;

  factory ClaimReferralCodeRequest.fromJson(Map<String, dynamic> json) => ClaimReferralCodeRequest(
        code: json['code'] as String,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
      };
}

/// Контракт: Shifts/CloseShiftRequest.cs
class CloseShiftRequest {
  const CloseShiftRequest({
    required this.organizationId,
    required this.countedCash,
    required this.closingNote,
    required this.idempotencyKey,
    this.managerSignOffStaffUserId,
    this.signOffReason,
  });

  final String organizationId;
  final MoneyDto countedCash;
  final String closingNote;
  final String idempotencyKey;

  /// Anti-fraud §5.7: required only when the cash discrepancy exceeds the branch tolerance; must be a
  /// manager other than the operator who opened or closed the shift.
  final String? managerSignOffStaffUserId;
  final String? signOffReason;

  factory CloseShiftRequest.fromJson(Map<String, dynamic> json) => CloseShiftRequest(
        organizationId: json['organizationId'] as String,
        countedCash: MoneyDto.fromJson(json['countedCash'] as Map<String, dynamic>),
        closingNote: json['closingNote'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        managerSignOffStaffUserId: json['managerSignOffStaffUserId'] == null ? null : json['managerSignOffStaffUserId'] as String,
        signOffReason: json['signOffReason'] == null ? null : json['signOffReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'countedCash': countedCash.toJson(),
        'closingNote': closingNote,
        'idempotencyKey': idempotencyKey,
        'managerSignOffStaffUserId': managerSignOffStaffUserId,
        'signOffReason': signOffReason,
      };
}

/// A physical club of the network: what the card shows and where the map puts its pin.
///
/// Контракт: Branding/OrganizationDirectoryEntryDto.cs
class ClubPlaceDto {
  const ClubPlaceDto({
    required this.branchId,
    required this.name,
    required this.city,
    this.address,
    this.description,
    this.coverImageUrl,
    this.latitude,
    this.longitude,
    this.workingHours,
    this.zones,
    this.photoUrls,
    this.seatCount,
    this.freeSeatCount,
  });

  final String branchId;
  final String name;
  final String city;
  final String? address;
  final String? description;
  final String? coverImageUrl;
  final double? latitude;
  final double? longitude;

  /// Расписание клуба: игрок хочет знать не только где клуб, но и открыт ли он сейчас.
  /// Пустой список — расписание не задано.
  final List<BranchWorkingHoursDayDto>? workingHours;

  /// Залы этого клуба: сколько мест и на чём играют. По железу клубы и сравнивают.
  final List<ClubZoneDto>? zones;

  /// Фото зала по порядку: обложка первой, дальше галерея. Пусто — клуб фото не прислал.
  final List<String>? photoUrls;

  /// Всего мест в зале — сумма по его зонам.
  final int? seatCount;

  /// Сколько из них не занято прямо сейчас: ни сессии, ни чужой брони на ближайший час.
  /// Считается независимо от расписания — «открыт ли зал» решает тот, кто показывает число.
  final int? freeSeatCount;

  factory ClubPlaceDto.fromJson(Map<String, dynamic> json) => ClubPlaceDto(
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        city: json['city'] as String,
        address: json['address'] == null ? null : json['address'] as String,
        description: json['description'] == null ? null : json['description'] as String,
        coverImageUrl: json['coverImageUrl'] == null ? null : json['coverImageUrl'] as String,
        latitude: json['latitude'] == null ? null : (json['latitude'] as num).toDouble(),
        longitude: json['longitude'] == null ? null : (json['longitude'] as num).toDouble(),
        workingHours: json['workingHours'] == null ? null : (json['workingHours'] as List<dynamic>).map((item) => BranchWorkingHoursDayDto.fromJson(item as Map<String, dynamic>)).toList(),
        zones: json['zones'] == null ? null : (json['zones'] as List<dynamic>).map((item) => ClubZoneDto.fromJson(item as Map<String, dynamic>)).toList(),
        photoUrls: json['photoUrls'] == null ? null : (json['photoUrls'] as List<dynamic>).map((item) => item as String).toList(),
        seatCount: json['seatCount'] == null ? null : (json['seatCount'] as num).toInt(),
        freeSeatCount: json['freeSeatCount'] == null ? null : (json['freeSeatCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'name': name,
        'city': city,
        'address': address,
        'description': description,
        'coverImageUrl': coverImageUrl,
        'latitude': latitude,
        'longitude': longitude,
        'workingHours': workingHours?.map((item) => item.toJson()).toList(),
        'zones': zones?.map((item) => item.toJson()).toList(),
        'photoUrls': photoUrls?.map((item) => item).toList(),
        'seatCount': seatCount,
        'freeSeatCount': freeSeatCount,
      };
}

/// Тариф клуба словами (спека `2026-09-25-club-plans-per-pc-design.md`): сколько ПК, сколько из них
/// платных, во что выйдет месяц и что клуб может сделать сам. Цену прежней сетки клуб не видит.
///
/// Контракт: Platform/Billing/ClubPlanContracts.cs
class ClubPlanDto {
  const ClubPlanDto({
    required this.planCode,
    required this.kind,
    required this.devices,
    required this.includedDevices,
    required this.billableDevices,
    required this.pricePerDevice,
    required this.estimatedMonthly,
    this.trialEndsAtUtc,
    required this.trialAvailable,
    required this.canSwitchToPerPc,
    required this.promisedPaymentAvailable,
    this.promisedPaymentUntilUtc,
    this.overdue,
    this.referralCode,
    this.freeMonths,
    this.referredClubs,
  });

  final String planCode;

  /// Одно из ClubPlanKindNames
  final String kind;
  final int devices;
  final int includedDevices;
  final int billableDevices;
  final MoneyDto pricePerDevice;

  /// Счёт за месяц при сегодняшнем числе ПК. У бесплатного и пробного — ноль.
  final MoneyDto estimatedMonthly;
  final DateTime? trialEndsAtUtc;
  final bool trialAvailable;
  final bool canSwitchToPerPc;
  final bool promisedPaymentAvailable;
  final DateTime? promisedPaymentUntilUtc;

  /// Просроченное; пусто — долга нет.
  final MoneyDto? overdue;

  /// «Приведи клуб»: код клуба и сколько бесплатных месяцев накоплено за приведённых.
  final String? referralCode;
  final int? freeMonths;
  final int? referredClubs;

  factory ClubPlanDto.fromJson(Map<String, dynamic> json) => ClubPlanDto(
        planCode: json['planCode'] as String,
        kind: json['kind'] as String,
        devices: (json['devices'] as num).toInt(),
        includedDevices: (json['includedDevices'] as num).toInt(),
        billableDevices: (json['billableDevices'] as num).toInt(),
        pricePerDevice: MoneyDto.fromJson(json['pricePerDevice'] as Map<String, dynamic>),
        estimatedMonthly: MoneyDto.fromJson(json['estimatedMonthly'] as Map<String, dynamic>),
        trialEndsAtUtc: json['trialEndsAtUtc'] == null ? null : DateTime.parse(json['trialEndsAtUtc'] as String),
        trialAvailable: json['trialAvailable'] as bool,
        canSwitchToPerPc: json['canSwitchToPerPc'] as bool,
        promisedPaymentAvailable: json['promisedPaymentAvailable'] as bool,
        promisedPaymentUntilUtc: json['promisedPaymentUntilUtc'] == null ? null : DateTime.parse(json['promisedPaymentUntilUtc'] as String),
        overdue: json['overdue'] == null ? null : MoneyDto.fromJson(json['overdue'] as Map<String, dynamic>),
        referralCode: json['referralCode'] == null ? null : json['referralCode'] as String,
        freeMonths: json['freeMonths'] == null ? null : (json['freeMonths'] as num).toInt(),
        referredClubs: json['referredClubs'] == null ? null : (json['referredClubs'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'planCode': planCode,
        'kind': kind,
        'devices': devices,
        'includedDevices': includedDevices,
        'billableDevices': billableDevices,
        'pricePerDevice': pricePerDevice.toJson(),
        'estimatedMonthly': estimatedMonthly.toJson(),
        'trialEndsAtUtc': trialEndsAtUtc?.toIso8601String(),
        'trialAvailable': trialAvailable,
        'canSwitchToPerPc': canSwitchToPerPc,
        'promisedPaymentAvailable': promisedPaymentAvailable,
        'promisedPaymentUntilUtc': promisedPaymentUntilUtc?.toIso8601String(),
        'overdue': overdue?.toJson(),
        'referralCode': referralCode,
        'freeMonths': freeMonths,
        'referredClubs': referredClubs,
      };
}

/// A review as the club's shop window shows it: who, how many stars, and what they wrote.
///
/// Контракт: Reviews/ClubReviewDtos.cs
class ClubReviewDto {
  const ClubReviewDto({
    required this.reviewId,
    required this.authorName,
    required this.rating,
    this.comment,
    required this.createdAtUtc,
  });

  final String reviewId;
  final String authorName;
  final int rating;
  final String? comment;
  final DateTime createdAtUtc;

  factory ClubReviewDto.fromJson(Map<String, dynamic> json) => ClubReviewDto(
        reviewId: json['reviewId'] as String,
        authorName: json['authorName'] as String,
        rating: (json['rating'] as num).toInt(),
        comment: json['comment'] == null ? null : json['comment'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'reviewId': reviewId,
        'authorName': authorName,
        'rating': rating,
        'comment': comment,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// The reviews page of a club: the average is what a player reads first, the reviews are why.
///
/// Контракт: Reviews/ClubReviewDtos.cs
class ClubReviewsPageDto {
  const ClubReviewsPageDto({
    this.rating,
    required this.reviewCount,
    required this.items,
  });


  /// Пусто — оценок пока нет. Это не ноль звёзд.
  final double? rating;
  final int reviewCount;
  final List<ClubReviewDto> items;

  factory ClubReviewsPageDto.fromJson(Map<String, dynamic> json) => ClubReviewsPageDto(
        rating: json['rating'] == null ? null : (json['rating'] as num).toDouble(),
        reviewCount: (json['reviewCount'] as num).toInt(),
        items: (json['items'] as List<dynamic>).map((item) => ClubReviewDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'rating': rating,
        'reviewCount': reviewCount,
        'items': items.map((item) => item.toJson()).toList(),
      };
}

/// Зал клуба в витрине: название, сколько в нём мест, чем оснащён и сколько мест свободно.
///
/// Контракт: Branding/OrganizationDirectoryEntryDto.cs
class ClubZoneDto {
  const ClubZoneDto({
    required this.name,
    required this.seatCount,
    this.hardwareSummary,
    this.freeSeatCount,
  });

  final String name;
  final int seatCount;
  final String? hardwareSummary;
  final int? freeSeatCount;

  factory ClubZoneDto.fromJson(Map<String, dynamic> json) => ClubZoneDto(
        name: json['name'] as String,
        seatCount: (json['seatCount'] as num).toInt(),
        hardwareSummary: json['hardwareSummary'] == null ? null : json['hardwareSummary'] as String,
        freeSeatCount: json['freeSeatCount'] == null ? null : (json['freeSeatCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'seatCount': seatCount,
        'hardwareSummary': hardwareSummary,
        'freeSeatCount': freeSeatCount,
      };
}

/// Контракт: Diagnostics/BranchDiagnosticsDto.cs
class CommandDiagnosticsSummaryDto {
  const CommandDiagnosticsSummaryDto({
    required this.pendingCommands,
    required this.failedCommands,
    required this.recentFailures,
  });

  final int pendingCommands;
  final int failedCommands;
  final List<FailedCommandDiagnosticsDto> recentFailures;

  factory CommandDiagnosticsSummaryDto.fromJson(Map<String, dynamic> json) => CommandDiagnosticsSummaryDto(
        pendingCommands: (json['pendingCommands'] as num).toInt(),
        failedCommands: (json['failedCommands'] as num).toInt(),
        recentFailures: (json['recentFailures'] as List<dynamic>).map((item) => FailedCommandDiagnosticsDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'pendingCommands': pendingCommands,
        'failedCommands': failedCommands,
        'recentFailures': recentFailures.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Updates/ComponentUpdateInstructionDto.cs
class ComponentUpdateInstructionDto {
  const ComponentUpdateInstructionDto({
    required this.updateRolloutId,
    required this.updatePackageId,
    required this.component,
    required this.version,
    required this.channel,
    required this.artifactUri,
    required this.sha256,
    required this.signature,
    required this.signatureAlgorithm,
    required this.sizeBytes,
    required this.releaseNotes,
  });

  final String updateRolloutId;
  final String updatePackageId;
  final String component;
  final String version;
  final String channel;
  final String artifactUri;
  final String sha256;
  final String signature;
  final String signatureAlgorithm;
  final int sizeBytes;
  final String releaseNotes;

  factory ComponentUpdateInstructionDto.fromJson(Map<String, dynamic> json) => ComponentUpdateInstructionDto(
        updateRolloutId: json['updateRolloutId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        artifactUri: json['artifactUri'] as String,
        sha256: json['sha256'] as String,
        signature: json['signature'] as String,
        signatureAlgorithm: json['signatureAlgorithm'] as String,
        sizeBytes: (json['sizeBytes'] as num).toInt(),
        releaseNotes: json['releaseNotes'] as String,
      );

  Map<String, dynamic> toJson() => {
        'updateRolloutId': updateRolloutId,
        'updatePackageId': updatePackageId,
        'component': component,
        'version': version,
        'channel': channel,
        'artifactUri': artifactUri,
        'sha256': sha256,
        'signature': signature,
        'signatureAlgorithm': signatureAlgorithm,
        'sizeBytes': sizeBytes,
        'releaseNotes': releaseNotes,
      };
}

/// Контракт: Reservations/ReservationRequests.cs
class ConfirmReservationRequest {
  const ConfirmReservationRequest({
    required this.organizationId,
    required this.expectedVersion,
  });

  final String organizationId;
  final int expectedVersion;

  factory ConfirmReservationRequest.fromJson(Map<String, dynamic> json) => ConfirmReservationRequest(
        organizationId: json['organizationId'] as String,
        expectedVersion: (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'expectedVersion': expectedVersion,
      };
}

/// Заявка на добавление филиала существующему клубу.
///
/// Контракт: Platform/Organizations/CreateBranchRequest.cs
class CreateBranchRequest {
  const CreateBranchRequest({
    required this.slug,
    required this.name,
    required this.city,
    this.preferredTimeZone,
  });

  final String slug;
  final String name;
  final String city;
  final String? preferredTimeZone;

  factory CreateBranchRequest.fromJson(Map<String, dynamic> json) => CreateBranchRequest(
        slug: json['slug'] as String,
        name: json['name'] as String,
        city: json['city'] as String,
        preferredTimeZone: json['preferredTimeZone'] == null ? null : json['preferredTimeZone'] as String,
      );

  Map<String, dynamic> toJson() => {
        'slug': slug,
        'name': name,
        'city': city,
        'preferredTimeZone': preferredTimeZone,
      };
}

/// Контракт: Reviews/ClubReviewDtos.cs
class CreateClubReviewRequest {
  const CreateClubReviewRequest({
    required this.sessionId,
    required this.rating,
    this.comment,
  });

  final String sessionId;
  final int rating;
  final String? comment;

  factory CreateClubReviewRequest.fromJson(Map<String, dynamic> json) => CreateClubReviewRequest(
        sessionId: json['sessionId'] as String,
        rating: (json['rating'] as num).toInt(),
        comment: json['comment'] == null ? null : json['comment'] as String,
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'rating': rating,
        'comment': comment,
      };
}

/// Консоль на месте — без агента (план `2026-09-25-console-seats.md`): администратор сам начинает и
/// заканчивает сессию, тарифы, касса и отчёты — как у ПК. Снимается консоль тем же «Снять
/// устройство», что и ПК.
///
/// Контракт: Consoles/ConsoleSeatContracts.cs
class CreateConsoleSeatRequest {
  const CreateConsoleSeatRequest({
    required this.organizationId,
    required this.seatId,
    required this.displayName,
  });

  final String organizationId;
  final String seatId;
  final String displayName;

  factory CreateConsoleSeatRequest.fromJson(Map<String, dynamic> json) => CreateConsoleSeatRequest(
        organizationId: json['organizationId'] as String,
        seatId: json['seatId'] as String,
        displayName: json['displayName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'seatId': seatId,
        'displayName': displayName,
      };
}

/// Контракт: Payments/DcTopUpDtos.cs
class CreateDcTopUpRequest {
  const CreateDcTopUpRequest({
    required this.playerAccountId,
    required this.amountMinorUnits,
    this.currencyCode,
  });

  final String playerAccountId;
  final int amountMinorUnits;
  final String? currencyCode;

  factory CreateDcTopUpRequest.fromJson(Map<String, dynamic> json) => CreateDcTopUpRequest(
        playerAccountId: json['playerAccountId'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
      };
}

/// Контракт: Devices/CreateDeviceEnrollmentCodeRequest.cs
class CreateDeviceEnrollmentCodeRequest {
  const CreateDeviceEnrollmentCodeRequest({
    required this.organizationId,
    required this.expiresInSeconds,
  });

  final String organizationId;
  final int expiresInSeconds;

  factory CreateDeviceEnrollmentCodeRequest.fromJson(Map<String, dynamic> json) => CreateDeviceEnrollmentCodeRequest(
        organizationId: json['organizationId'] as String,
        expiresInSeconds: (json['expiresInSeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'expiresInSeconds': expiresInSeconds,
      };
}

/// Код установки: техник ставит AFK4 на ПК зала без мастера —
/// `afk4-client.exe /quiet AFK4_INSTALL_CODE=…`. Код многоразовый, но ограничен сроком и
/// числом новых ПК; сервер хранит его хешем, открытым он виден один раз — при выдаче.
///
/// Контракт: Install/InstallCodeContracts.cs
class CreateInstallCodeRequest {
  const CreateInstallCodeRequest({
    required this.lifetimeHours,
    required this.maxDevices,
  });

  final int lifetimeHours;
  final int maxDevices;

  factory CreateInstallCodeRequest.fromJson(Map<String, dynamic> json) => CreateInstallCodeRequest(
        lifetimeHours: (json['lifetimeHours'] as num).toInt(),
        maxDevices: (json['maxDevices'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'lifetimeHours': lifetimeHours,
        'maxDevices': maxDevices,
      };
}

/// Контракт: Platform/Billing/CreateInvoiceRequest.cs
class CreateInvoiceRequest {
  const CreateInvoiceRequest({
    required this.kind,
    required this.amountMinorUnits,
    required this.description,
    this.dueAtUtc,
  });

  final String kind;
  final int amountMinorUnits;
  final String description;
  final DateTime? dueAtUtc;

  factory CreateInvoiceRequest.fromJson(Map<String, dynamic> json) => CreateInvoiceRequest(
        kind: json['kind'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        description: json['description'] as String,
        dueAtUtc: json['dueAtUtc'] == null ? null : DateTime.parse(json['dueAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'kind': kind,
        'amountMinorUnits': amountMinorUnits,
        'description': description,
        'dueAtUtc': dueAtUtc?.toIso8601String(),
      };
}

/// Контракт: News/CreateNewsItemRequest.cs
class CreateNewsItemRequest {
  const CreateNewsItemRequest({
    this.branchId,
    required this.title,
    required this.body,
    this.imageUrl,
    required this.isPublished,
    this.publishAtUtc,
    this.expiresAtUtc,
    this.showOnPcs,
  });

  final String? branchId;
  final String title;
  final String body;
  final String? imageUrl;
  final bool isPublished;
  final DateTime? publishAtUtc;
  final DateTime? expiresAtUtc;
  final bool? showOnPcs;

  factory CreateNewsItemRequest.fromJson(Map<String, dynamic> json) => CreateNewsItemRequest(
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
        isPublished: json['isPublished'] as bool,
        publishAtUtc: json['publishAtUtc'] == null ? null : DateTime.parse(json['publishAtUtc'] as String),
        expiresAtUtc: json['expiresAtUtc'] == null ? null : DateTime.parse(json['expiresAtUtc'] as String),
        showOnPcs: json['showOnPcs'] == null ? null : json['showOnPcs'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'title': title,
        'body': body,
        'imageUrl': imageUrl,
        'isPublished': isPublished,
        'publishAtUtc': publishAtUtc?.toIso8601String(),
        'expiresAtUtc': expiresAtUtc?.toIso8601String(),
        'showOnPcs': showOnPcs,
      };
}

/// Контракт: Identity/AccountActivation/CreateOrganizationOwnerInviteRequest.cs
class CreateOrganizationOwnerInviteRequest {
  const CreateOrganizationOwnerInviteRequest({
    required this.branchId,
    this.ownerUserName,
    this.ownerDisplayName,
    this.lifetime,
    this.ownerEmail,
  });

  final String branchId;
  final String? ownerUserName;
  final String? ownerDisplayName;
  final String? lifetime;
  final String? ownerEmail;

  factory CreateOrganizationOwnerInviteRequest.fromJson(Map<String, dynamic> json) => CreateOrganizationOwnerInviteRequest(
        branchId: json['branchId'] as String,
        ownerUserName: json['ownerUserName'] == null ? null : json['ownerUserName'] as String,
        ownerDisplayName: json['ownerDisplayName'] == null ? null : json['ownerDisplayName'] as String,
        lifetime: json['lifetime'] == null ? null : json['lifetime'] as String,
        ownerEmail: json['ownerEmail'] == null ? null : json['ownerEmail'] as String,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'ownerUserName': ownerUserName,
        'ownerDisplayName': ownerDisplayName,
        'lifetime': lifetime,
        'ownerEmail': ownerEmail,
      };
}

/// Контракт: Platform/Organizations/CreateOrganizationRequest.cs
class CreateOrganizationRequest {
  const CreateOrganizationRequest({
    required this.organizationSlug,
    required this.organizationName,
    required this.branchSlug,
    required this.branchName,
    required this.branchCity,
    required this.planCode,
    required this.subscriptionStatus,
    this.limits,
    this.ownerUserName,
    this.ownerDisplayName,
    this.organizationOwnerInviteLifetime,
    this.referralCode,
  });

  final String organizationSlug;
  final String organizationName;
  final String branchSlug;
  final String branchName;
  final String branchCity;
  final String planCode;
  final String subscriptionStatus;
  final OrganizationLimitsDto? limits;
  final String? ownerUserName;
  final String? ownerDisplayName;
  final String? organizationOwnerInviteLifetime;

  /// Код «Приведи клуб» того, кто привёл этот клуб. Пусто — клуб пришёл сам.
  final String? referralCode;

  factory CreateOrganizationRequest.fromJson(Map<String, dynamic> json) => CreateOrganizationRequest(
        organizationSlug: json['organizationSlug'] as String,
        organizationName: json['organizationName'] as String,
        branchSlug: json['branchSlug'] as String,
        branchName: json['branchName'] as String,
        branchCity: json['branchCity'] as String,
        planCode: json['planCode'] as String,
        subscriptionStatus: json['subscriptionStatus'] as String,
        limits: json['limits'] == null ? null : OrganizationLimitsDto.fromJson(json['limits'] as Map<String, dynamic>),
        ownerUserName: json['ownerUserName'] == null ? null : json['ownerUserName'] as String,
        ownerDisplayName: json['ownerDisplayName'] == null ? null : json['ownerDisplayName'] as String,
        organizationOwnerInviteLifetime: json['organizationOwnerInviteLifetime'] == null ? null : json['organizationOwnerInviteLifetime'] as String,
        referralCode: json['referralCode'] == null ? null : json['referralCode'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationSlug': organizationSlug,
        'organizationName': organizationName,
        'branchSlug': branchSlug,
        'branchName': branchName,
        'branchCity': branchCity,
        'planCode': planCode,
        'subscriptionStatus': subscriptionStatus,
        'limits': limits?.toJson(),
        'ownerUserName': ownerUserName,
        'ownerDisplayName': ownerDisplayName,
        'organizationOwnerInviteLifetime': organizationOwnerInviteLifetime,
        'referralCode': referralCode,
      };
}

/// Контракт: Platform/Organizations/CreateOrganizationResponse.cs
class CreateOrganizationResponse {
  const CreateOrganizationResponse({
    required this.organization,
    required this.organizationOwnerInvite,
  });

  final OrganizationDetailDto organization;
  final OrganizationOwnerInviteDto organizationOwnerInvite;

  factory CreateOrganizationResponse.fromJson(Map<String, dynamic> json) => CreateOrganizationResponse(
        organization: OrganizationDetailDto.fromJson(json['organization'] as Map<String, dynamic>),
        organizationOwnerInvite: OrganizationOwnerInviteDto.fromJson(json['organizationOwnerInvite'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'organization': organization.toJson(),
        'organizationOwnerInvite': organizationOwnerInvite.toJson(),
      };
}

/// Контракт: Platform/SupportNotes/CreateOrganizationSupportNoteRequest.cs
class CreateOrganizationSupportNoteRequest {
  const CreateOrganizationSupportNoteRequest({
    required this.body,
  });

  final String body;

  factory CreateOrganizationSupportNoteRequest.fromJson(Map<String, dynamic> json) => CreateOrganizationSupportNoteRequest(
        body: json['body'] as String,
      );

  Map<String, dynamic> toJson() => {
        'body': body,
      };
}

/// Контракт: Packages/CreatePackageDefinitionRequest.cs
class CreatePackageDefinitionRequest {
  const CreatePackageDefinitionRequest({
    required this.organizationId,
    required this.name,
    required this.price,
    required this.includedSeconds,
    required this.bonusSeconds,
    required this.expiresAfterDays,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String name;
  final MoneyDto price;
  final int includedSeconds;
  final int bonusSeconds;
  final int expiresAfterDays;
  final String idempotencyKey;

  factory CreatePackageDefinitionRequest.fromJson(Map<String, dynamic> json) => CreatePackageDefinitionRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        includedSeconds: (json['includedSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num).toInt(),
        expiresAfterDays: (json['expiresAfterDays'] as num).toInt(),
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'price': price.toJson(),
        'includedSeconds': includedSeconds,
        'bonusSeconds': bonusSeconds,
        'expiresAfterDays': expiresAfterDays,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Platform/Billing/CreatePlanRequest.cs
class CreatePlanRequest {
  const CreatePlanRequest({
    required this.planCode,
    required this.name,
    required this.priceMinorUnits,
    required this.currencyCode,
    required this.billingInterval,
    this.maxBranches,
    this.maxDevicesPerBranch,
    this.maxConcurrentSessions,
    this.maxStaffUsersPerBranch,
    required this.sortOrder,
    this.pricePerDeviceMinorUnits,
    this.includedDevices,
  });

  final String planCode;
  final String name;
  final int priceMinorUnits;
  final String currencyCode;
  final String billingInterval;
  final int? maxBranches;
  final int? maxDevicesPerBranch;
  final int? maxConcurrentSessions;
  final int? maxStaffUsersPerBranch;
  final int sortOrder;
  final int? pricePerDeviceMinorUnits;
  final int? includedDevices;

  factory CreatePlanRequest.fromJson(Map<String, dynamic> json) => CreatePlanRequest(
        planCode: json['planCode'] as String,
        name: json['name'] as String,
        priceMinorUnits: (json['priceMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        billingInterval: json['billingInterval'] as String,
        maxBranches: json['maxBranches'] == null ? null : (json['maxBranches'] as num).toInt(),
        maxDevicesPerBranch: json['maxDevicesPerBranch'] == null ? null : (json['maxDevicesPerBranch'] as num).toInt(),
        maxConcurrentSessions: json['maxConcurrentSessions'] == null ? null : (json['maxConcurrentSessions'] as num).toInt(),
        maxStaffUsersPerBranch: json['maxStaffUsersPerBranch'] == null ? null : (json['maxStaffUsersPerBranch'] as num).toInt(),
        sortOrder: (json['sortOrder'] as num).toInt(),
        pricePerDeviceMinorUnits: json['pricePerDeviceMinorUnits'] == null ? null : (json['pricePerDeviceMinorUnits'] as num).toInt(),
        includedDevices: json['includedDevices'] == null ? null : (json['includedDevices'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'planCode': planCode,
        'name': name,
        'priceMinorUnits': priceMinorUnits,
        'currencyCode': currencyCode,
        'billingInterval': billingInterval,
        'maxBranches': maxBranches,
        'maxDevicesPerBranch': maxDevicesPerBranch,
        'maxConcurrentSessions': maxConcurrentSessions,
        'maxStaffUsersPerBranch': maxStaffUsersPerBranch,
        'sortOrder': sortOrder,
        'pricePerDeviceMinorUnits': pricePerDeviceMinorUnits,
        'includedDevices': includedDevices,
      };
}

/// Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs
class CreatePlatformAdminInvitationRequest {
  const CreatePlatformAdminInvitationRequest({
    required this.role,
    required this.lifetimeHours,
  });

  final String role;
  final int lifetimeHours;

  factory CreatePlatformAdminInvitationRequest.fromJson(Map<String, dynamic> json) => CreatePlatformAdminInvitationRequest(
        role: json['role'] as String,
        lifetimeHours: (json['lifetimeHours'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'role': role,
        'lifetimeHours': lifetimeHours,
      };
}

/// Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs
class CreatePlatformAdminInvitationResponse {
  const CreatePlatformAdminInvitationResponse({
    required this.invitation,
    required this.code,
  });

  final PlatformAdminInvitationDto invitation;
  final String code;

  factory CreatePlatformAdminInvitationResponse.fromJson(Map<String, dynamic> json) => CreatePlatformAdminInvitationResponse(
        invitation: PlatformAdminInvitationDto.fromJson(json['invitation'] as Map<String, dynamic>),
        code: json['code'] as String,
      );

  Map<String, dynamic> toJson() => {
        'invitation': invitation.toJson(),
        'code': code,
      };
}

/// Контракт: Platform/Auth/PlatformRoleContracts.cs
class CreatePlatformRoleRequest {
  const CreatePlatformRoleRequest({
    required this.roleName,
    required this.displayName,
    required this.description,
    required this.permissions,
  });

  final String roleName;
  final String displayName;
  final String description;
  final List<String> permissions;

  factory CreatePlatformRoleRequest.fromJson(Map<String, dynamic> json) => CreatePlatformRoleRequest(
        roleName: json['roleName'] as String,
        displayName: json['displayName'] as String,
        description: json['description'] as String,
        permissions: (json['permissions'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'roleName': roleName,
        'displayName': displayName,
        'description': description,
        'permissions': permissions.map((item) => item).toList(),
      };
}

/// Контракт: Platform/Support/PlatformSupportAccessContracts.cs
class CreatePlatformSupportAccessGrantRequest {
  const CreatePlatformSupportAccessGrantRequest({
    required this.organizationId,
    required this.reason,
    required this.lifetimeMinutes,
  });

  final String organizationId;
  final String reason;
  final int lifetimeMinutes;

  factory CreatePlatformSupportAccessGrantRequest.fromJson(Map<String, dynamic> json) => CreatePlatformSupportAccessGrantRequest(
        organizationId: json['organizationId'] as String,
        reason: json['reason'] as String,
        lifetimeMinutes: (json['lifetimeMinutes'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'reason': reason,
        'lifetimeMinutes': lifetimeMinutes,
      };
}

/// Контракт: Platform/Updates/PlatformUpdateContracts.cs
class CreatePlatformUpdatePackageRequest {
  const CreatePlatformUpdatePackageRequest({
    required this.component,
    required this.version,
    required this.channel,
    required this.artifactUri,
    required this.sha256,
    required this.signature,
    required this.signatureAlgorithm,
    required this.sizeBytes,
    required this.releaseNotes,
  });

  final String component;
  final String version;
  final String channel;
  final String artifactUri;
  final String sha256;
  final String signature;
  final String signatureAlgorithm;
  final int sizeBytes;
  final String releaseNotes;

  factory CreatePlatformUpdatePackageRequest.fromJson(Map<String, dynamic> json) => CreatePlatformUpdatePackageRequest(
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        artifactUri: json['artifactUri'] as String,
        sha256: json['sha256'] as String,
        signature: json['signature'] as String,
        signatureAlgorithm: json['signatureAlgorithm'] as String,
        sizeBytes: (json['sizeBytes'] as num).toInt(),
        releaseNotes: json['releaseNotes'] as String,
      );

  Map<String, dynamic> toJson() => {
        'component': component,
        'version': version,
        'channel': channel,
        'artifactUri': artifactUri,
        'sha256': sha256,
        'signature': signature,
        'signatureAlgorithm': signatureAlgorithm,
        'sizeBytes': sizeBytes,
        'releaseNotes': releaseNotes,
      };
}

/// Контракт: Platform/Updates/PlatformUpdateContracts.cs
class CreatePlatformUpdateRolloutRequest {
  const CreatePlatformUpdateRolloutRequest({
    required this.updatePackageId,
    required this.channel,
    required this.targetKind,
    required this.organizationIds,
    required this.branchIds,
    required this.deviceIds,
    required this.batchPercent,
    required this.startsAtUtc,
    required this.reason,
  });

  final String updatePackageId;
  final String channel;
  final String targetKind;
  final List<String> organizationIds;
  final List<String> branchIds;
  final List<String> deviceIds;
  final int batchPercent;
  final DateTime startsAtUtc;
  final String reason;

  factory CreatePlatformUpdateRolloutRequest.fromJson(Map<String, dynamic> json) => CreatePlatformUpdateRolloutRequest(
        updatePackageId: json['updatePackageId'] as String,
        channel: json['channel'] as String,
        targetKind: json['targetKind'] as String,
        organizationIds: (json['organizationIds'] as List<dynamic>).map((item) => item as String).toList(),
        branchIds: (json['branchIds'] as List<dynamic>).map((item) => item as String).toList(),
        deviceIds: (json['deviceIds'] as List<dynamic>).map((item) => item as String).toList(),
        batchPercent: (json['batchPercent'] as num).toInt(),
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'updatePackageId': updatePackageId,
        'channel': channel,
        'targetKind': targetKind,
        'organizationIds': organizationIds.map((item) => item).toList(),
        'branchIds': branchIds.map((item) => item).toList(),
        'deviceIds': deviceIds.map((item) => item).toList(),
        'batchPercent': batchPercent,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'reason': reason,
      };
}

/// Контракт: Billing/CreatePlayerAccountRequest.cs
class CreatePlayerAccountRequest {
  const CreatePlayerAccountRequest({
    required this.organizationId,
    required this.displayName,
    this.phoneNumber,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String displayName;
  final String? phoneNumber;
  final String idempotencyKey;

  factory CreatePlayerAccountRequest.fromJson(Map<String, dynamic> json) => CreatePlayerAccountRequest(
        organizationId: json['organizationId'] as String,
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'displayName': displayName,
        'phoneNumber': phoneNumber,
        'idempotencyKey': idempotencyKey,
      };
}

/// Бронь на компанию: несколько мест на одно время одним действием.
/// Мест здесь КОЛИЧЕСТВО, а не список. Игрок в приложении конкретную машину не выбирает — её
/// назначает клуб, — поэтому просить его выбрать пять машин было бы просьбой о том, чего он не
/// решает. Операторская групповая бронь наоборот берёт список: там человек тянет мышью по строкам
/// таймлайна и точно знает, какие места отдаёт.
/// Тариф один на всю компанию: сидят вместе, платят по одной цене, и разные тарифы внутри одной
/// брони — это уже не «бронь на компанию», а несколько разных броней.
/// <param name="BranchId">
/// Филиал, в который компания придёт. Нужен только в первом действии в клубе, где счёта ещё нет:
/// у сети с несколькими филиалами сервер не гадает, куда записать счёт.
/// </param>
/// <param name="IdempotencyKey">
/// Ключ одной попытки. Необязателен: установленные приложения его не шлют, и без него всё
/// работает как раньше. С ним повтор после обрыва возвращает уже созданную компанию, а не
/// бронирует вторую и не замораживает деньги второй раз.
/// </param>
///
/// Контракт: Reservations/CreatePlayerReservationGroupRequest.cs
class CreatePlayerReservationGroupRequest {
  const CreatePlayerReservationGroupRequest({
    required this.seatCount,
    required this.startsAtUtc,
    required this.endsAtUtc,
    this.note,
    this.tariffVersionId,
    this.branchId,
    this.idempotencyKey,
  });

  final int seatCount;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final String? note;
  final String? tariffVersionId;
  final String? branchId;
  final String? idempotencyKey;

  factory CreatePlayerReservationGroupRequest.fromJson(Map<String, dynamic> json) => CreatePlayerReservationGroupRequest(
        seatCount: (json['seatCount'] as num).toInt(),
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        note: json['note'] == null ? null : json['note'] as String,
        tariffVersionId: json['tariffVersionId'] == null ? null : json['tariffVersionId'] as String,
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        idempotencyKey: json['idempotencyKey'] == null ? null : json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'seatCount': seatCount,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'note': note,
        'tariffVersionId': tariffVersionId,
        'branchId': branchId,
        'idempotencyKey': idempotencyKey,
      };
}

/// Player-initiated reservation request.
/// SeatId is optional (unassigned reservation). StartsAtUtc and EndsAtUtc are
/// absolute — the service derives DurationMinutes internally.
/// TariffVersionId carries the player's billing choice made in the app. It is optional so older
/// clients keep working, but a booking without it cannot be priced: the hold slice needs the exact
/// amount the player agreed to, not a branch-default guess. The package path (booking covered by
/// already-purchased time) belongs to that same slice — reserving package minutes is a write on the
/// package, not a field on the request.
/// BranchId — филиал, в который игрок придёт. Нужен только в первом действии в клубе, где счёта
/// ещё нет: у сети с несколькими филиалами сервер не гадает, куда записать счёт. У игрока со
/// счётом филиал уже известен, и присланный его не переписывает.
/// IdempotencyKey — ключ одной попытки. Необязателен: установленные приложения его не шлют, и
/// без него всё работает как раньше. С ним повтор после обрыва находит уже созданное, а не
/// создаёт второе.
///
/// Контракт: Reservations/CreatePlayerReservationRequest.cs
class CreatePlayerReservationRequest {
  const CreatePlayerReservationRequest({
    this.seatId,
    required this.startsAtUtc,
    required this.endsAtUtc,
    this.note,
    this.tariffVersionId,
    this.branchId,
    this.idempotencyKey,
  });

  final String? seatId;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final String? note;
  final String? tariffVersionId;
  final String? branchId;
  final String? idempotencyKey;

  factory CreatePlayerReservationRequest.fromJson(Map<String, dynamic> json) => CreatePlayerReservationRequest(
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        note: json['note'] == null ? null : json['note'] as String,
        tariffVersionId: json['tariffVersionId'] == null ? null : json['tariffVersionId'] as String,
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        idempotencyKey: json['idempotencyKey'] == null ? null : json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'seatId': seatId,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'note': note,
        'tariffVersionId': tariffVersionId,
        'branchId': branchId,
        'idempotencyKey': idempotencyKey,
      };
}

/// Войти на ПК с телефона (спека оболочки, §5.4): приложение сканирует QR с монитора — в нём код
/// посадки — и просит сервер впустить своего человека на эту машину. Номер и ПИН-код у ПК при
/// этом не набираются вовсе.
///
/// Контракт: Players/PlayerSignInClaimContracts.cs
class CreatePlayerSignInClaimRequest {
  const CreatePlayerSignInClaimRequest({
    required this.seatingCode,
    required this.idempotencyKey,
  });

  final String seatingCode;
  final String idempotencyKey;

  factory CreatePlayerSignInClaimRequest.fromJson(Map<String, dynamic> json) => CreatePlayerSignInClaimRequest(
        seatingCode: json['seatingCode'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'seatingCode': seatingCode,
        'idempotencyKey': idempotencyKey,
      };
}

/// Строка чека в запросе на его создание: товар и сколько штук.
/// Это НЕ PosSaleLineDto. Имя товара, цену за штуку и сумму строки сервер берёт из
/// каталога и присланному не верит (см. EfPosService.CreateSaleAsync) — а раз так, требовать их в
/// запросе значит предлагать клиенту назначить цену и делать вид, что она чего-то стоит. Стойка
/// шлёт ровно то, что знает сама.
///
/// Контракт: Pos/CreatePosSaleRequest.cs
class CreatePosSaleLineDto {
  const CreatePosSaleLineDto({
    required this.productId,
    required this.quantity,
  });

  final String productId;
  final int quantity;

  factory CreatePosSaleLineDto.fromJson(Map<String, dynamic> json) => CreatePosSaleLineDto(
        productId: json['productId'] as String,
        quantity: (json['quantity'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'quantity': quantity,
      };
}

/// Контракт: Pos/CreatePosSaleRequest.cs
class CreatePosSaleRequest {
  const CreatePosSaleRequest({
    required this.organizationId,
    required this.shiftId,
    required this.lines,
    required this.idempotencyKey,
    this.playerAccountId,
    this.sessionId,
  });

  final String organizationId;
  final String shiftId;
  final List<CreatePosSaleLineDto> lines;
  final String idempotencyKey;
  final String? playerAccountId;

  /// When set, attaches this sale to an open session tab (settled at checkout).
  final String? sessionId;

  factory CreatePosSaleRequest.fromJson(Map<String, dynamic> json) => CreatePosSaleRequest(
        organizationId: json['organizationId'] as String,
        shiftId: json['shiftId'] as String,
        lines: (json['lines'] as List<dynamic>).map((item) => CreatePosSaleLineDto.fromJson(item as Map<String, dynamic>)).toList(),
        idempotencyKey: json['idempotencyKey'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        sessionId: json['sessionId'] == null ? null : json['sessionId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'shiftId': shiftId,
        'lines': lines.map((item) => item.toJson()).toList(),
        'idempotencyKey': idempotencyKey,
        'playerAccountId': playerAccountId,
        'sessionId': sessionId,
      };
}

/// Контракт: Pos/CreateProductCategoryRequest.cs
class CreateProductCategoryRequest {
  const CreateProductCategoryRequest({
    required this.organizationId,
    required this.name,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String name;
  final String idempotencyKey;

  factory CreateProductCategoryRequest.fromJson(Map<String, dynamic> json) => CreateProductCategoryRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Pos/CreateProductRequest.cs
class CreateProductRequest {
  const CreateProductRequest({
    required this.organizationId,
    required this.categoryId,
    required this.name,
    required this.sku,
    required this.price,
    required this.trackStock,
    required this.allowNegativeStock,
    required this.idempotencyKey,
    required this.reorderThreshold,
    required this.availableInShell,
    required this.featuredOnPcs,
    this.imageUrl,
  });

  final String organizationId;
  final String categoryId;
  final String name;
  final String sku;
  final MoneyDto price;
  final bool trackStock;
  final bool allowNegativeStock;
  final String idempotencyKey;
  final int reorderThreshold;
  final bool availableInShell;
  final bool featuredOnPcs;
  final String? imageUrl;

  factory CreateProductRequest.fromJson(Map<String, dynamic> json) => CreateProductRequest(
        organizationId: json['organizationId'] as String,
        categoryId: json['categoryId'] as String,
        name: json['name'] as String,
        sku: json['sku'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        trackStock: json['trackStock'] as bool,
        allowNegativeStock: json['allowNegativeStock'] as bool,
        idempotencyKey: json['idempotencyKey'] as String,
        reorderThreshold: (json['reorderThreshold'] as num).toInt(),
        availableInShell: json['availableInShell'] as bool,
        featuredOnPcs: json['featuredOnPcs'] as bool,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'categoryId': categoryId,
        'name': name,
        'sku': sku,
        'price': price.toJson(),
        'trackStock': trackStock,
        'allowNegativeStock': allowNegativeStock,
        'idempotencyKey': idempotencyKey,
        'reorderThreshold': reorderThreshold,
        'availableInShell': availableInShell,
        'featuredOnPcs': featuredOnPcs,
        'imageUrl': imageUrl,
      };
}

/// Creates a recurring report delivery for a branch. ReportType is one of
/// ScheduledReportTypeNames; Frequency one of
/// ReportScheduleFrequencyNames.
///
/// Контракт: Reports/ReportScheduleContracts.cs
class CreateReportScheduleRequest {
  const CreateReportScheduleRequest({
    required this.organizationId,
    required this.reportType,
    required this.frequency,
  });

  final String organizationId;
  final String reportType;
  final String frequency;

  factory CreateReportScheduleRequest.fromJson(Map<String, dynamic> json) => CreateReportScheduleRequest(
        organizationId: json['organizationId'] as String,
        reportType: json['reportType'] as String,
        frequency: json['frequency'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'reportType': reportType,
        'frequency': frequency,
      };
}

/// Books several seats as one logical reservation (drag across timeline rows). All-or-nothing:
/// if any seat conflicts with an existing reservation or session, the whole group is rejected and
/// the conflicting seats are reported back so the operator can adjust the selection.
///
/// Контракт: Reservations/ReservationGroup.cs
class CreateReservationGroupRequest {
  const CreateReservationGroupRequest({
    required this.organizationId,
    this.playerAccountId,
    required this.seatIds,
    required this.customerName,
    this.phoneNumber,
    required this.startsAtUtc,
    required this.durationMinutes,
    required this.source,
    this.note,
  });

  final String organizationId;
  final String? playerAccountId;
  final List<String> seatIds;
  final String customerName;
  final String? phoneNumber;
  final DateTime startsAtUtc;
  final int durationMinutes;
  final String source;
  final String? note;

  factory CreateReservationGroupRequest.fromJson(Map<String, dynamic> json) => CreateReservationGroupRequest(
        organizationId: json['organizationId'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        seatIds: (json['seatIds'] as List<dynamic>).map((item) => item as String).toList(),
        customerName: json['customerName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        durationMinutes: (json['durationMinutes'] as num).toInt(),
        source: json['source'] as String,
        note: json['note'] == null ? null : json['note'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'playerAccountId': playerAccountId,
        'seatIds': seatIds.map((item) => item).toList(),
        'customerName': customerName,
        'phoneNumber': phoneNumber,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'durationMinutes': durationMinutes,
        'source': source,
        'note': note,
      };
}

/// Контракт: Reservations/ReservationRequests.cs
class CreateReservationRequest {
  const CreateReservationRequest({
    required this.organizationId,
    this.playerAccountId,
    this.seatId,
    required this.customerName,
    this.phoneNumber,
    required this.startsAtUtc,
    required this.durationMinutes,
    required this.source,
    this.note,
  });

  final String organizationId;
  final String? playerAccountId;
  final String? seatId;
  final String customerName;
  final String? phoneNumber;
  final DateTime startsAtUtc;
  final int durationMinutes;
  final String source;
  final String? note;

  factory CreateReservationRequest.fromJson(Map<String, dynamic> json) => CreateReservationRequest(
        organizationId: json['organizationId'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        customerName: json['customerName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        durationMinutes: (json['durationMinutes'] as num).toInt(),
        source: json['source'] as String,
        note: json['note'] == null ? null : json['note'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'playerAccountId': playerAccountId,
        'seatId': seatId,
        'customerName': customerName,
        'phoneNumber': phoneNumber,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'durationMinutes': durationMinutes,
        'source': source,
        'note': note,
      };
}

/// Контракт: Layout/CreateSeatRequest.cs
class CreateSeatRequest {
  const CreateSeatRequest({
    required this.organizationId,
    required this.zoneId,
    required this.name,
    required this.sortOrder,
  });

  final String organizationId;
  final String zoneId;
  final String name;
  final int sortOrder;

  factory CreateSeatRequest.fromJson(Map<String, dynamic> json) => CreateSeatRequest(
        organizationId: json['organizationId'] as String,
        zoneId: json['zoneId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'zoneId': zoneId,
        'name': name,
        'sortOrder': sortOrder,
      };
}

/// Приглашение сотрудника по номеру телефона: человек принимает его коротким кодом из SMS и сам
/// задаёт себе пароль. Единственный путь завести сотрудника — заведение с готовым паролем убрано
/// намеренно, чтобы пароль знал только его владелец.
/// <param name="Email">Необязательна: назвали — уйдёт и письмо, не назвали — хватит SMS.</param>
///
/// Контракт: Identity/CreateStaffInviteRequest.cs
class CreateStaffInviteRequest {
  const CreateStaffInviteRequest({
    required this.organizationId,
    required this.userName,
    required this.displayName,
    required this.phoneNumber,
    this.email,
    required this.roleNames,
  });

  final String organizationId;
  final String userName;
  final String displayName;
  final String phoneNumber;
  final String? email;
  final List<String> roleNames;

  factory CreateStaffInviteRequest.fromJson(Map<String, dynamic> json) => CreateStaffInviteRequest(
        organizationId: json['organizationId'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] as String,
        email: json['email'] == null ? null : json['email'] as String,
        roleNames: (json['roleNames'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'userName': userName,
        'displayName': displayName,
        'phoneNumber': phoneNumber,
        'email': email,
        'roleNames': roleNames.map((item) => item).toList(),
      };
}

/// Контракт: Inventory/CreateStockMovementRequest.cs
class CreateStockMovementRequest {
  const CreateStockMovementRequest({
    required this.organizationId,
    required this.productId,
    required this.movementType,
    required this.quantityDelta,
    required this.unitCost,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String productId;
  final String movementType;
  final int quantityDelta;
  final MoneyDto unitCost;
  final String reason;
  final String idempotencyKey;

  factory CreateStockMovementRequest.fromJson(Map<String, dynamic> json) => CreateStockMovementRequest(
        organizationId: json['organizationId'] as String,
        productId: json['productId'] as String,
        movementType: json['movementType'] as String,
        quantityDelta: (json['quantityDelta'] as num).toInt(),
        unitCost: MoneyDto.fromJson(json['unitCost'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'productId': productId,
        'movementType': movementType,
        'quantityDelta': quantityDelta,
        'unitCost': unitCost.toJson(),
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// `Schedule` не передан — новый тариф действует круглосуточно и каждый день.
///
/// Контракт: Tariffs/CreateTariffRequest.cs
class CreateTariffRequest {
  const CreateTariffRequest({
    required this.organizationId,
    required this.name,
    required this.idempotencyKey,
    this.schedule,
  });

  final String organizationId;
  final String name;
  final String idempotencyKey;
  final TariffScheduleDto? schedule;

  factory CreateTariffRequest.fromJson(Map<String, dynamic> json) => CreateTariffRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        schedule: json['schedule'] == null ? null : TariffScheduleDto.fromJson(json['schedule'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'idempotencyKey': idempotencyKey,
        'schedule': schedule?.toJson(),
      };
}

/// Контракт: Tariffs/CreateTariffVersionRequest.cs
class CreateTariffVersionRequest {
  const CreateTariffVersionRequest({
    required this.organizationId,
    required this.tariffId,
    required this.currencyCode,
    required this.pricePerMinuteMinorUnits,
    required this.minimumBillableMinutes,
    required this.roundingIncrementMinutes,
    required this.effectiveFromUtc,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String tariffId;
  final String currencyCode;
  final int pricePerMinuteMinorUnits;
  final int minimumBillableMinutes;
  final int roundingIncrementMinutes;
  final DateTime effectiveFromUtc;
  final String idempotencyKey;

  factory CreateTariffVersionRequest.fromJson(Map<String, dynamic> json) => CreateTariffVersionRequest(
        organizationId: json['organizationId'] as String,
        tariffId: json['tariffId'] as String,
        currencyCode: json['currencyCode'] as String,
        pricePerMinuteMinorUnits: (json['pricePerMinuteMinorUnits'] as num).toInt(),
        minimumBillableMinutes: (json['minimumBillableMinutes'] as num).toInt(),
        roundingIncrementMinutes: (json['roundingIncrementMinutes'] as num).toInt(),
        effectiveFromUtc: DateTime.parse(json['effectiveFromUtc'] as String),
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'tariffId': tariffId,
        'currencyCode': currencyCode,
        'pricePerMinuteMinorUnits': pricePerMinuteMinorUnits,
        'minimumBillableMinutes': minimumBillableMinutes,
        'roundingIncrementMinutes': roundingIncrementMinutes,
        'effectiveFromUtc': effectiveFromUtc.toIso8601String(),
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Tournaments/TournamentDtos.cs
class CreateTournamentRequest {
  const CreateTournamentRequest({
    required this.branchId,
    required this.title,
    required this.description,
    required this.discipline,
    required this.startsAtUtc,
    required this.entryFeeMinorUnits,
    required this.capacity,
  });

  final String branchId;
  final String title;
  final String description;
  final String discipline;
  final DateTime startsAtUtc;
  final int entryFeeMinorUnits;
  final int capacity;

  factory CreateTournamentRequest.fromJson(Map<String, dynamic> json) => CreateTournamentRequest(
        branchId: json['branchId'] as String,
        title: json['title'] as String,
        description: json['description'] as String,
        discipline: json['discipline'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        entryFeeMinorUnits: (json['entryFeeMinorUnits'] as num).toInt(),
        capacity: (json['capacity'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'title': title,
        'description': description,
        'discipline': discipline,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'entryFeeMinorUnits': entryFeeMinorUnits,
        'capacity': capacity,
      };
}

/// Контракт: Updates/CreateUpdatePackageRequest.cs
class CreateUpdatePackageRequest {
  const CreateUpdatePackageRequest({
    required this.organizationId,
    required this.component,
    required this.version,
    required this.channel,
    required this.artifactUri,
    required this.sha256,
    required this.signature,
    required this.signatureAlgorithm,
    required this.sizeBytes,
    required this.releaseNotes,
  });

  final String organizationId;
  final String component;
  final String version;
  final String channel;
  final String artifactUri;
  final String sha256;
  final String signature;
  final String signatureAlgorithm;
  final int sizeBytes;
  final String releaseNotes;

  factory CreateUpdatePackageRequest.fromJson(Map<String, dynamic> json) => CreateUpdatePackageRequest(
        organizationId: json['organizationId'] as String,
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        artifactUri: json['artifactUri'] as String,
        sha256: json['sha256'] as String,
        signature: json['signature'] as String,
        signatureAlgorithm: json['signatureAlgorithm'] as String,
        sizeBytes: (json['sizeBytes'] as num).toInt(),
        releaseNotes: json['releaseNotes'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'component': component,
        'version': version,
        'channel': channel,
        'artifactUri': artifactUri,
        'sha256': sha256,
        'signature': signature,
        'signatureAlgorithm': signatureAlgorithm,
        'sizeBytes': sizeBytes,
        'releaseNotes': releaseNotes,
      };
}

/// Контракт: Updates/CreateUpdateRolloutRequest.cs
class CreateUpdateRolloutRequest {
  const CreateUpdateRolloutRequest({
    required this.organizationId,
    required this.updatePackageId,
    required this.channel,
    required this.targetKind,
    required this.targetDeviceIds,
    required this.batchPercent,
    required this.startsAtUtc,
    required this.reason,
  });

  final String organizationId;
  final String updatePackageId;
  final String channel;
  final String targetKind;
  final List<String> targetDeviceIds;
  final int batchPercent;
  final DateTime startsAtUtc;
  final String reason;

  factory CreateUpdateRolloutRequest.fromJson(Map<String, dynamic> json) => CreateUpdateRolloutRequest(
        organizationId: json['organizationId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        channel: json['channel'] as String,
        targetKind: json['targetKind'] as String,
        targetDeviceIds: (json['targetDeviceIds'] as List<dynamic>).map((item) => item as String).toList(),
        batchPercent: (json['batchPercent'] as num).toInt(),
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'updatePackageId': updatePackageId,
        'channel': channel,
        'targetKind': targetKind,
        'targetDeviceIds': targetDeviceIds.map((item) => item).toList(),
        'batchPercent': batchPercent,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'reason': reason,
      };
}

/// Контракт: Layout/CreateZoneRequest.cs
class CreateZoneRequest {
  const CreateZoneRequest({
    required this.organizationId,
    required this.name,
    required this.sortOrder,
    this.hardwareSummary,
  });

  final String organizationId;
  final String name;
  final int sortOrder;
  final String? hardwareSummary;

  factory CreateZoneRequest.fromJson(Map<String, dynamic> json) => CreateZoneRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
        hardwareSummary: json['hardwareSummary'] == null ? null : json['hardwareSummary'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'sortOrder': sortOrder,
        'hardwareSummary': hardwareSummary,
      };
}

/// A page of results plus the cursor to fetch the next page (null when exhausted).
/// Курсор — это «продолжить отсюда», а не номер страницы: список растёт с одного конца, и смещение
/// съезжало бы на каждой новой записи.
///
/// Контракт: Common/CursorPage.cs
class CursorPage<T> {
  const CursorPage({
    required this.items,
    this.nextCursor,
  });

  final List<T> items;
  final String? nextCursor;

  factory CursorPage.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) item,
  ) => CursorPage<T>(
        items: (json['items'] as List<dynamic>).map((entry) => item(entry as Map<String, dynamic>)).toList(),
        nextCursor: json['nextCursor'] == null ? null : json['nextCursor'] as String,
      );

}

/// GET-ответ: PAN не возвращаем — только факт наличия и last4.
///
/// Контракт: Payments/DcPayLinkConfigDtos.cs
class DcPayLinkConfigDto {
  const DcPayLinkConfigDto({
    required this.cardSet,
    required this.cardLast4,
    required this.commentTemplate,
    required this.isActive,
  });

  final bool cardSet;
  final String cardLast4;
  final String commentTemplate;
  final bool isActive;

  factory DcPayLinkConfigDto.fromJson(Map<String, dynamic> json) => DcPayLinkConfigDto(
        cardSet: json['cardSet'] as bool,
        cardLast4: json['cardLast4'] as String,
        commentTemplate: json['commentTemplate'] as String,
        isActive: json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'cardSet': cardSet,
        'cardLast4': cardLast4,
        'commentTemplate': commentTemplate,
        'isActive': isActive,
      };
}

/// Контракт: Payments/DcTopUpDtos.cs
class DcTopUpDto {
  const DcTopUpDto({
    required this.intentId,
    required this.payUrl,
    required this.comment,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.cardLast4,
  });

  final String intentId;
  final String payUrl;
  final String comment;
  final int amountMinorUnits;
  final String currencyCode;
  final String cardLast4;

  factory DcTopUpDto.fromJson(Map<String, dynamic> json) => DcTopUpDto(
        intentId: json['intentId'] as String,
        payUrl: json['payUrl'] as String,
        comment: json['comment'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        cardLast4: json['cardLast4'] as String,
      );

  Map<String, dynamic> toJson() => {
        'intentId': intentId,
        'payUrl': payUrl,
        'comment': comment,
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'cardLast4': cardLast4,
      };
}

/// One club that needs a money decision: either it owes money, or it is still suspended
/// after settling. Days overdue and the dunning stage answer "how long has this been ignored".
///
/// Контракт: Platform/Billing/DebtRowDto.cs
class DebtRowDto {
  const DebtRowDto({
    required this.organizationId,
    required this.organizationName,
    required this.organizationSlug,
    required this.organizationStatus,
    required this.subscriptionStatus,
    required this.outstandingMinorUnits,
    required this.currencyCode,
    this.oldestOverdueInvoiceNumber,
    this.oldestOverdueInvoiceId,
    required this.daysOverdue,
    required this.dunningStage,
    this.graceUntilUtc,
    required this.settledButSuspended,
  });

  final String organizationId;
  final String organizationName;
  final String organizationSlug;
  final String organizationStatus;
  final String subscriptionStatus;
  final int outstandingMinorUnits;
  final String currencyCode;
  final int? oldestOverdueInvoiceNumber;
  final String? oldestOverdueInvoiceId;
  final int daysOverdue;
  final int dunningStage;
  final DateTime? graceUntilUtc;
  final bool settledButSuspended;

  factory DebtRowDto.fromJson(Map<String, dynamic> json) => DebtRowDto(
        organizationId: json['organizationId'] as String,
        organizationName: json['organizationName'] as String,
        organizationSlug: json['organizationSlug'] as String,
        organizationStatus: json['organizationStatus'] as String,
        subscriptionStatus: json['subscriptionStatus'] as String,
        outstandingMinorUnits: (json['outstandingMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        oldestOverdueInvoiceNumber: json['oldestOverdueInvoiceNumber'] == null ? null : (json['oldestOverdueInvoiceNumber'] as num).toInt(),
        oldestOverdueInvoiceId: json['oldestOverdueInvoiceId'] == null ? null : json['oldestOverdueInvoiceId'] as String,
        daysOverdue: (json['daysOverdue'] as num).toInt(),
        dunningStage: (json['dunningStage'] as num).toInt(),
        graceUntilUtc: json['graceUntilUtc'] == null ? null : DateTime.parse(json['graceUntilUtc'] as String),
        settledButSuspended: json['settledButSuspended'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'organizationName': organizationName,
        'organizationSlug': organizationSlug,
        'organizationStatus': organizationStatus,
        'subscriptionStatus': subscriptionStatus,
        'outstandingMinorUnits': outstandingMinorUnits,
        'currencyCode': currencyCode,
        'oldestOverdueInvoiceNumber': oldestOverdueInvoiceNumber,
        'oldestOverdueInvoiceId': oldestOverdueInvoiceId,
        'daysOverdue': daysOverdue,
        'dunningStage': dunningStage,
        'graceUntilUtc': graceUntilUtc?.toIso8601String(),
        'settledButSuspended': settledButSuspended,
      };
}

/// Игрок позвал оператора со своей машины. Приходит от агента: устройство известно всегда, а
/// сессии может не быть вовсе — кнопка есть и на запертом экране.
///
/// Контракт: Devices/AssistanceRequestContracts.cs
class DeviceAssistanceRequest {
  const DeviceAssistanceRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.requestedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final DateTime requestedAtUtc;

  factory DeviceAssistanceRequest.fromJson(Map<String, dynamic> json) => DeviceAssistanceRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        requestedAtUtc: DateTime.parse(json['requestedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'requestedAtUtc': requestedAtUtc.toIso8601String(),
      };
}

/// Состояние вызова после обращения: когда позвали. Null — вызова нет.
///
/// Контракт: Devices/AssistanceRequestContracts.cs
class DeviceAssistanceStateDto {
  const DeviceAssistanceStateDto({
    required this.deviceId,
    this.assistanceRequestedAtUtc,
  });

  final String deviceId;
  final DateTime? assistanceRequestedAtUtc;

  factory DeviceAssistanceStateDto.fromJson(Map<String, dynamic> json) => DeviceAssistanceStateDto(
        deviceId: json['deviceId'] as String,
        assistanceRequestedAtUtc: json['assistanceRequestedAtUtc'] == null ? null : DateTime.parse(json['assistanceRequestedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'assistanceRequestedAtUtc': assistanceRequestedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Devices/DeviceCommandDto.cs
class DeviceCommandDto {
  const DeviceCommandDto({
    required this.commandId,
    required this.type,
    required this.createdAtUtc,
    required this.payload,
  });

  final String commandId;
  final String type;
  final DateTime createdAtUtc;
  final Map<String, String> payload;

  factory DeviceCommandDto.fromJson(Map<String, dynamic> json) => DeviceCommandDto(
        commandId: json['commandId'] as String,
        type: json['type'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        payload: (json['payload'] as Map<String, dynamic>).map((key, value) => MapEntry(key, value as String)),
      );

  Map<String, dynamic> toJson() => {
        'commandId': commandId,
        'type': type,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'payload': payload.map((key, value) => MapEntry(key, value)),
      };
}

/// Контракт: Devices/DeviceCommandResultDto.cs
class DeviceCommandResultDto {
  const DeviceCommandResultDto({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.commandId,
    required this.status,
    required this.message,
    required this.observedAtUtc,
    this.outcome,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String commandId;
  final String status;
  final String message;
  final DateTime observedAtUtc;
  final String? outcome;

  factory DeviceCommandResultDto.fromJson(Map<String, dynamic> json) => DeviceCommandResultDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        commandId: json['commandId'] as String,
        status: json['status'] as String,
        message: json['message'] as String,
        observedAtUtc: DateTime.parse(json['observedAtUtc'] as String),
        outcome: json['outcome'] == null ? null : json['outcome'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'commandId': commandId,
        'status': status,
        'message': message,
        'observedAtUtc': observedAtUtc.toIso8601String(),
        'outcome': outcome,
      };
}

/// Контракт: Devices/DeviceCommandStatusDto.cs
class DeviceCommandStatusDto {
  const DeviceCommandStatusDto({
    required this.deviceId,
    required this.commandId,
    required this.type,
    required this.status,
    this.message,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    this.outcome,
  });

  final String deviceId;
  final String commandId;
  final String type;
  final String status;
  final String? message;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;
  final String? outcome;

  factory DeviceCommandStatusDto.fromJson(Map<String, dynamic> json) => DeviceCommandStatusDto(
        deviceId: json['deviceId'] as String,
        commandId: json['commandId'] as String,
        type: json['type'] as String,
        status: json['status'] as String,
        message: json['message'] == null ? null : json['message'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
        outcome: json['outcome'] == null ? null : json['outcome'] as String,
      );

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'commandId': commandId,
        'type': type,
        'status': status,
        'message': message,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
        'outcome': outcome,
      };
}

/// Контракт: Updates/DeviceComponentVersionDto.cs
class DeviceComponentVersionDto {
  const DeviceComponentVersionDto({
    required this.component,
    required this.version,
  });

  final String component;
  final String version;

  factory DeviceComponentVersionDto.fromJson(Map<String, dynamic> json) => DeviceComponentVersionDto(
        component: json['component'] as String,
        version: json['version'] as String,
      );

  Map<String, dynamic> toJson() => {
        'component': component,
        'version': version,
      };
}

/// Контракт: Devices/DeviceConnectionRequest.cs
class DeviceConnectionRequest {
  const DeviceConnectionRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.machineName,
    required this.agentVersion,
    required this.shellVersion,
    required this.credentialSecret,
    required this.connectedAtUtc,
    this.activeSessionId,
    this.activeSessionLeaseExpiresAtUtc,
    this.activeSessionLeaseSequence,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String machineName;
  final String agentVersion;
  final String shellVersion;
  final String credentialSecret;
  final DateTime connectedAtUtc;
  final String? activeSessionId;
  final DateTime? activeSessionLeaseExpiresAtUtc;
  final int? activeSessionLeaseSequence;

  factory DeviceConnectionRequest.fromJson(Map<String, dynamic> json) => DeviceConnectionRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        agentVersion: json['agentVersion'] as String,
        shellVersion: json['shellVersion'] as String,
        credentialSecret: json['credentialSecret'] as String,
        connectedAtUtc: DateTime.parse(json['connectedAtUtc'] as String),
        activeSessionId: json['activeSessionId'] == null ? null : json['activeSessionId'] as String,
        activeSessionLeaseExpiresAtUtc: json['activeSessionLeaseExpiresAtUtc'] == null ? null : DateTime.parse(json['activeSessionLeaseExpiresAtUtc'] as String),
        activeSessionLeaseSequence: json['activeSessionLeaseSequence'] == null ? null : (json['activeSessionLeaseSequence'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'machineName': machineName,
        'agentVersion': agentVersion,
        'shellVersion': shellVersion,
        'credentialSecret': credentialSecret,
        'connectedAtUtc': connectedAtUtc.toIso8601String(),
        'activeSessionId': activeSessionId,
        'activeSessionLeaseExpiresAtUtc': activeSessionLeaseExpiresAtUtc?.toIso8601String(),
        'activeSessionLeaseSequence': activeSessionLeaseSequence,
      };
}

/// Контракт: Devices/DeviceDetailDto.cs
class DeviceDetailDto {
  const DeviceDetailDto({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.machineName,
    required this.agentVersion,
    required this.shellVersion,
    required this.enrolledAtUtc,
    this.lastHeartbeatAtUtc,
    required this.isOnline,
    required this.isLocked,
    this.seatId,
    this.seatName,
    this.zoneId,
    this.zoneName,
    required this.activeCredentialCount,
    required this.installedAppCount,
    required this.recentCommands,
    this.displayName,
    this.role,
    this.enrollmentState,
    this.protectionReport,
    this.branchProtectionVersion,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String machineName;
  final String agentVersion;
  final String shellVersion;
  final DateTime enrolledAtUtc;
  final DateTime? lastHeartbeatAtUtc;
  final bool isOnline;
  final bool isLocked;
  final String? seatId;
  final String? seatName;
  final String? zoneId;
  final String? zoneName;
  final int activeCredentialCount;
  final int installedAppCount;
  final List<DeviceCommandStatusDto> recentCommands;
  final String? displayName;
  final String? role;
  final String? enrollmentState;

  /// Последний отчёт ПК о защите; null — ПК ещё не докладывал.
  final DeviceProtectionReportDto? protectionReport;

  /// Текущая версия профиля филиала: отчёт со старой версией значит «ПК ещё не применил».
  final int? branchProtectionVersion;

  factory DeviceDetailDto.fromJson(Map<String, dynamic> json) => DeviceDetailDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        agentVersion: json['agentVersion'] as String,
        shellVersion: json['shellVersion'] as String,
        enrolledAtUtc: DateTime.parse(json['enrolledAtUtc'] as String),
        lastHeartbeatAtUtc: json['lastHeartbeatAtUtc'] == null ? null : DateTime.parse(json['lastHeartbeatAtUtc'] as String),
        isOnline: json['isOnline'] as bool,
        isLocked: json['isLocked'] as bool,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
        zoneId: json['zoneId'] == null ? null : json['zoneId'] as String,
        zoneName: json['zoneName'] == null ? null : json['zoneName'] as String,
        activeCredentialCount: (json['activeCredentialCount'] as num).toInt(),
        installedAppCount: (json['installedAppCount'] as num).toInt(),
        recentCommands: (json['recentCommands'] as List<dynamic>).map((item) => DeviceCommandStatusDto.fromJson(item as Map<String, dynamic>)).toList(),
        displayName: json['displayName'] == null ? null : json['displayName'] as String,
        role: json['role'] == null ? null : json['role'] as String,
        enrollmentState: json['enrollmentState'] == null ? null : json['enrollmentState'] as String,
        protectionReport: json['protectionReport'] == null ? null : DeviceProtectionReportDto.fromJson(json['protectionReport'] as Map<String, dynamic>),
        branchProtectionVersion: json['branchProtectionVersion'] == null ? null : (json['branchProtectionVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'machineName': machineName,
        'agentVersion': agentVersion,
        'shellVersion': shellVersion,
        'enrolledAtUtc': enrolledAtUtc.toIso8601String(),
        'lastHeartbeatAtUtc': lastHeartbeatAtUtc?.toIso8601String(),
        'isOnline': isOnline,
        'isLocked': isLocked,
        'seatId': seatId,
        'seatName': seatName,
        'zoneId': zoneId,
        'zoneName': zoneName,
        'activeCredentialCount': activeCredentialCount,
        'installedAppCount': installedAppCount,
        'recentCommands': recentCommands.map((item) => item.toJson()).toList(),
        'displayName': displayName,
        'role': role,
        'enrollmentState': enrollmentState,
        'protectionReport': protectionReport?.toJson(),
        'branchProtectionVersion': branchProtectionVersion,
      };
}

/// Контракт: Diagnostics/BranchDiagnosticsDto.cs
class DeviceDiagnosticsSummaryDto {
  const DeviceDiagnosticsSummaryDto({
    required this.totalDevices,
    required this.onlineDevices,
    required this.lockedDevices,
    required this.staleDevices,
    required this.staleThresholdSeconds,
    this.newestHeartbeatAtUtc,
  });

  final int totalDevices;
  final int onlineDevices;
  final int lockedDevices;
  final int staleDevices;
  final int staleThresholdSeconds;
  final DateTime? newestHeartbeatAtUtc;

  factory DeviceDiagnosticsSummaryDto.fromJson(Map<String, dynamic> json) => DeviceDiagnosticsSummaryDto(
        totalDevices: (json['totalDevices'] as num).toInt(),
        onlineDevices: (json['onlineDevices'] as num).toInt(),
        lockedDevices: (json['lockedDevices'] as num).toInt(),
        staleDevices: (json['staleDevices'] as num).toInt(),
        staleThresholdSeconds: (json['staleThresholdSeconds'] as num).toInt(),
        newestHeartbeatAtUtc: json['newestHeartbeatAtUtc'] == null ? null : DateTime.parse(json['newestHeartbeatAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'totalDevices': totalDevices,
        'onlineDevices': onlineDevices,
        'lockedDevices': lockedDevices,
        'staleDevices': staleDevices,
        'staleThresholdSeconds': staleThresholdSeconds,
        'newestHeartbeatAtUtc': newestHeartbeatAtUtc?.toIso8601String(),
      };
}

/// Контракт: Devices/DeviceEnrollmentCodeDto.cs
class DeviceEnrollmentCodeDto {
  const DeviceEnrollmentCodeDto({
    required this.organizationId,
    required this.branchId,
    required this.code,
    required this.expiresAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String code;
  final DateTime expiresAtUtc;

  factory DeviceEnrollmentCodeDto.fromJson(Map<String, dynamic> json) => DeviceEnrollmentCodeDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        code: json['code'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'code': code,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
      };
}

/// Контракт: Devices/DeviceEnrollmentRequest.cs
class DeviceEnrollmentRequest {
  const DeviceEnrollmentRequest({
    required this.organizationId,
    required this.branchId,
    required this.enrollmentCode,
    required this.machineName,
    required this.agentVersion,
    required this.shellVersion,
    required this.requestedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String enrollmentCode;
  final String machineName;
  final String agentVersion;
  final String shellVersion;
  final DateTime requestedAtUtc;

  factory DeviceEnrollmentRequest.fromJson(Map<String, dynamic> json) => DeviceEnrollmentRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        enrollmentCode: json['enrollmentCode'] as String,
        machineName: json['machineName'] as String,
        agentVersion: json['agentVersion'] as String,
        shellVersion: json['shellVersion'] as String,
        requestedAtUtc: DateTime.parse(json['requestedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'enrollmentCode': enrollmentCode,
        'machineName': machineName,
        'agentVersion': agentVersion,
        'shellVersion': shellVersion,
        'requestedAtUtc': requestedAtUtc.toIso8601String(),
      };
}

/// Контракт: Devices/DeviceEnrollmentResponse.cs
class DeviceEnrollmentResponse {
  const DeviceEnrollmentResponse({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.credentialId,
    required this.credentialSecret,
    required this.enrolledAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String credentialId;
  final String credentialSecret;
  final DateTime enrolledAtUtc;

  factory DeviceEnrollmentResponse.fromJson(Map<String, dynamic> json) => DeviceEnrollmentResponse(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        credentialId: json['credentialId'] as String,
        credentialSecret: json['credentialSecret'] as String,
        enrolledAtUtc: DateTime.parse(json['enrolledAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'credentialId': credentialId,
        'credentialSecret': credentialSecret,
        'enrolledAtUtc': enrolledAtUtc.toIso8601String(),
      };
}

/// Игра для агента: всё, чтобы найти лаунчер на этом ПК и показать плитку.
///
/// Контракт: Games/GameLibraryContracts.cs
class DeviceGameDto {
  const DeviceGameDto({
    required this.appId,
    required this.displayName,
    this.genre,
    this.minAge,
    this.coverUrl,
    required this.launchKind,
    this.launchTarget,
    this.executablePath,
    this.arguments,
    required this.availableWithoutSession,
    this.launchOnSessionStart,
  });

  final String appId;
  final String displayName;
  final String? genre;
  final int? minAge;
  final String? coverUrl;

  /// Одно из GameLaunchKindNames.
  final String launchKind;
  final String? launchTarget;
  final String? executablePath;
  final String? arguments;
  final bool availableWithoutSession;
  final bool? launchOnSessionStart;

  factory DeviceGameDto.fromJson(Map<String, dynamic> json) => DeviceGameDto(
        appId: json['appId'] as String,
        displayName: json['displayName'] as String,
        genre: json['genre'] == null ? null : json['genre'] as String,
        minAge: json['minAge'] == null ? null : (json['minAge'] as num).toInt(),
        coverUrl: json['coverUrl'] == null ? null : json['coverUrl'] as String,
        launchKind: json['launchKind'] as String,
        launchTarget: json['launchTarget'] == null ? null : json['launchTarget'] as String,
        executablePath: json['executablePath'] == null ? null : json['executablePath'] as String,
        arguments: json['arguments'] == null ? null : json['arguments'] as String,
        availableWithoutSession: json['availableWithoutSession'] as bool,
        launchOnSessionStart: json['launchOnSessionStart'] == null ? null : json['launchOnSessionStart'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'appId': appId,
        'displayName': displayName,
        'genre': genre,
        'minAge': minAge,
        'coverUrl': coverUrl,
        'launchKind': launchKind,
        'launchTarget': launchTarget,
        'executablePath': executablePath,
        'arguments': arguments,
        'availableWithoutSession': availableWithoutSession,
        'launchOnSessionStart': launchOnSessionStart,
      };
}

/// Библиотека филиала для агента. Версия едет в сердцебиении.
///
/// Контракт: Games/GameLibraryContracts.cs
class DeviceGameLibraryDto {
  const DeviceGameLibraryDto({
    required this.version,
    required this.games,
  });

  final int version;
  final List<DeviceGameDto> games;

  factory DeviceGameLibraryDto.fromJson(Map<String, dynamic> json) => DeviceGameLibraryDto(
        version: (json['version'] as num).toInt(),
        games: (json['games'] as List<dynamic>).map((item) => DeviceGameDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'version': version,
        'games': games.map((item) => item.toJson()).toList(),
      };
}

/// Железо ПК для карточки в Панели: сейчас, принятое и чем они отличаются.
///
/// Контракт: Devices/DeviceHardwareContracts.cs
class DeviceHardwareDto {
  const DeviceHardwareDto({
    this.current,
    this.reportedAtUtc,
    this.accepted,
    this.acceptedAtUtc,
    this.acceptedByName,
    required this.changes,
  });

  final HardwareSnapshotDto? current;
  final DateTime? reportedAtUtc;
  final HardwareSnapshotDto? accepted;
  final DateTime? acceptedAtUtc;

  /// Кто принял; null — первый снимок, принятый сам.
  final String? acceptedByName;
  final List<HardwareChangeDto> changes;

  factory DeviceHardwareDto.fromJson(Map<String, dynamic> json) => DeviceHardwareDto(
        current: json['current'] == null ? null : HardwareSnapshotDto.fromJson(json['current'] as Map<String, dynamic>),
        reportedAtUtc: json['reportedAtUtc'] == null ? null : DateTime.parse(json['reportedAtUtc'] as String),
        accepted: json['accepted'] == null ? null : HardwareSnapshotDto.fromJson(json['accepted'] as Map<String, dynamic>),
        acceptedAtUtc: json['acceptedAtUtc'] == null ? null : DateTime.parse(json['acceptedAtUtc'] as String),
        acceptedByName: json['acceptedByName'] == null ? null : json['acceptedByName'] as String,
        changes: (json['changes'] as List<dynamic>).map((item) => HardwareChangeDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'current': current?.toJson(),
        'reportedAtUtc': reportedAtUtc?.toIso8601String(),
        'accepted': accepted?.toJson(),
        'acceptedAtUtc': acceptedAtUtc?.toIso8601String(),
        'acceptedByName': acceptedByName,
        'changes': changes.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Devices/DeviceHardwareContracts.cs
class DeviceHardwareReportRequest {
  const DeviceHardwareReportRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.collectedAtUtc,
    required this.snapshot,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final DateTime collectedAtUtc;
  final HardwareSnapshotDto snapshot;

  factory DeviceHardwareReportRequest.fromJson(Map<String, dynamic> json) => DeviceHardwareReportRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        collectedAtUtc: DateTime.parse(json['collectedAtUtc'] as String),
        snapshot: HardwareSnapshotDto.fromJson(json['snapshot'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'collectedAtUtc': collectedAtUtc.toIso8601String(),
        'snapshot': snapshot.toJson(),
      };
}

/// Контракт: Devices/DeviceHeartbeatRequest.cs
class DeviceHeartbeatRequest {
  const DeviceHeartbeatRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.machineName,
    required this.agentVersion,
    required this.shellVersion,
    required this.observedAtUtc,
    required this.isLocked,
    this.activeSessionId,
    this.activeSessionLeaseExpiresAtUtc,
    this.activeSessionLeaseSequence,
    this.networkMacAddress,
    this.networkSubnet,
    this.networkBroadcastAddress,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String machineName;
  final String agentVersion;
  final String shellVersion;
  final DateTime observedAtUtc;
  final bool isLocked;
  final String? activeSessionId;
  final DateTime? activeSessionLeaseExpiresAtUtc;
  final int? activeSessionLeaseSequence;

  /// MAC проводного адаптера со шлюзом («AA-BB-CC-DD-EE-FF»): по нему этот ПК будит сосед, когда
  /// он выключен. Пусто — агент ещё не умеет его сообщать.
  final String? networkMacAddress;

  /// Подсеть этого адаптера («192.168.1.0/24»): будить можно только из той же подсети.
  final String? networkSubnet;

  /// Широковещательный адрес подсети — куда сосед шлёт волшебный пакет.
  final String? networkBroadcastAddress;

  factory DeviceHeartbeatRequest.fromJson(Map<String, dynamic> json) => DeviceHeartbeatRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        agentVersion: json['agentVersion'] as String,
        shellVersion: json['shellVersion'] as String,
        observedAtUtc: DateTime.parse(json['observedAtUtc'] as String),
        isLocked: json['isLocked'] as bool,
        activeSessionId: json['activeSessionId'] == null ? null : json['activeSessionId'] as String,
        activeSessionLeaseExpiresAtUtc: json['activeSessionLeaseExpiresAtUtc'] == null ? null : DateTime.parse(json['activeSessionLeaseExpiresAtUtc'] as String),
        activeSessionLeaseSequence: json['activeSessionLeaseSequence'] == null ? null : (json['activeSessionLeaseSequence'] as num).toInt(),
        networkMacAddress: json['networkMacAddress'] == null ? null : json['networkMacAddress'] as String,
        networkSubnet: json['networkSubnet'] == null ? null : json['networkSubnet'] as String,
        networkBroadcastAddress: json['networkBroadcastAddress'] == null ? null : json['networkBroadcastAddress'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'machineName': machineName,
        'agentVersion': agentVersion,
        'shellVersion': shellVersion,
        'observedAtUtc': observedAtUtc.toIso8601String(),
        'isLocked': isLocked,
        'activeSessionId': activeSessionId,
        'activeSessionLeaseExpiresAtUtc': activeSessionLeaseExpiresAtUtc?.toIso8601String(),
        'activeSessionLeaseSequence': activeSessionLeaseSequence,
        'networkMacAddress': networkMacAddress,
        'networkSubnet': networkSubnet,
        'networkBroadcastAddress': networkBroadcastAddress,
      };
}

/// Контракт: Devices/DeviceHeartbeatResponse.cs
class DeviceHeartbeatResponse {
  const DeviceHeartbeatResponse({
    required this.serverTimeUtc,
    required this.heartbeatIntervalSeconds,
    required this.commands,
    this.effectiveGraceMinutes,
    this.seatingCode,
    this.seatingCodeExpiresAtUtc,
    this.rotateCredential,
    this.branding,
    this.seat,
    this.sessionOwner,
    this.features,
    this.pendingSignInClaim,
    this.maintenance,
    this.maintenanceSinceUtc,
    this.maintenanceByName,
    this.policyProfileVersion,
    this.gameLibraryVersion,
  });

  final DateTime serverTimeUtc;
  final int heartbeatIntervalSeconds;
  final List<DeviceCommandDto> commands;

  /// Effective offline grace window (minutes) for this device's branch. The agent applies it locally
  /// to keep a paying customer playing for this long after the network actually drops (spec §6.1).
  final int? effectiveGraceMinutes;

  /// Код, который простаивающий ПК показывает на мониторе, чтобы человек мог сесть за него из
  /// приложения. Едет здесь, а не своим маршрутом: сердцебиение и так стучит раз в десять
  /// секунд, а код живёт минуты — вторая труба к тому же серверу за тем же самым ничего бы не
  /// добавила, кроме второго места, где это можно сломать.
  /// null, когда за ПК уже играют: звать к занятой машине незачем.
  final String? seatingCode;
  final DateTime? seatingCodeExpiresAtUtc;

  /// Клуб попросил сменить ключ этой машины. Агент меняет его сам и записывает новый — без
  /// визита к ПК и без простоя. Едет сердцебиением по той же причине, что и код посадки:
  /// вторая труба к тому же серверу за тем же самым ничего не добавила бы.
  final bool? rotateCredential;

  /// Оформление клуба для экрана игрока: название, логотип, цвет. Едет сердцебиением по той же
  /// причине, что код посадки и ротация ключа. Хранить его в конфиге машины было бы хуже: клуб
  /// меняет логотип в панели, а не обходом всех ПК с переустановкой.
  /// null, когда оформление не задано, — оболочка показывает нейтральный экран.
  final ShellBrandingDto? branding;

  /// Место этого ПК: оболочка пишет его в шапке. null — ПК ни к какому месту не привязан.
  final DeviceSeatDto? seat;

  /// Чья сессия идёт на ПК: вошедшему не владельцу оболочка чужую сессию не откроет.
  final DeviceSessionOwnerDto? sessionOwner;

  /// Права организации по тарифу (PlatformFeatureNames): оболочка прячет разделы, которых у клуба
  /// нет, — бар без player_shop, кэшбек без loyalty. Тот же расчёт, что у /api/me/features.
  final List<String>? features;

  /// Заявка на вход с телефона, которую ПК ещё не забрал, — на случай, если сигнал SignalR
  /// потерялся. null — ждать нечего.
  final PlayerSignInClaimedDto? pendingSignInClaim;

  /// ПК на обслуживании. Команду maintenance-on агент получает сразу, а по этому признаку
  /// догоняет, если её пропустил, и выходит из обслуживания, если пропустил maintenance-off.
  final bool? maintenance;

  /// С какого момента и кто включил обслуживание: оболочка пишет это на полосе поверх рабочего
  /// стола, чтобы техник у ПК видел, чей это ПК сейчас и с каких пор.
  final DateTime? maintenanceSinceUtc;
  final String? maintenanceByName;

  /// Версия профиля защиты филиала (§6.3). Сменилась — агент перечитывает профиль; 0 — профиля нет.
  final int? policyProfileVersion;

  /// Версия библиотеки игр филиала: по её смене агент перечитывает список игр (спека оболочки, §6.6).
  final int? gameLibraryVersion;

  factory DeviceHeartbeatResponse.fromJson(Map<String, dynamic> json) => DeviceHeartbeatResponse(
        serverTimeUtc: DateTime.parse(json['serverTimeUtc'] as String),
        heartbeatIntervalSeconds: (json['heartbeatIntervalSeconds'] as num).toInt(),
        commands: (json['commands'] as List<dynamic>).map((item) => DeviceCommandDto.fromJson(item as Map<String, dynamic>)).toList(),
        effectiveGraceMinutes: json['effectiveGraceMinutes'] == null ? null : (json['effectiveGraceMinutes'] as num).toInt(),
        seatingCode: json['seatingCode'] == null ? null : json['seatingCode'] as String,
        seatingCodeExpiresAtUtc: json['seatingCodeExpiresAtUtc'] == null ? null : DateTime.parse(json['seatingCodeExpiresAtUtc'] as String),
        rotateCredential: json['rotateCredential'] == null ? null : json['rotateCredential'] as bool,
        branding: json['branding'] == null ? null : ShellBrandingDto.fromJson(json['branding'] as Map<String, dynamic>),
        seat: json['seat'] == null ? null : DeviceSeatDto.fromJson(json['seat'] as Map<String, dynamic>),
        sessionOwner: json['sessionOwner'] == null ? null : DeviceSessionOwnerDto.fromJson(json['sessionOwner'] as Map<String, dynamic>),
        features: json['features'] == null ? null : (json['features'] as List<dynamic>).map((item) => item as String).toList(),
        pendingSignInClaim: json['pendingSignInClaim'] == null ? null : PlayerSignInClaimedDto.fromJson(json['pendingSignInClaim'] as Map<String, dynamic>),
        maintenance: json['maintenance'] == null ? null : json['maintenance'] as bool,
        maintenanceSinceUtc: json['maintenanceSinceUtc'] == null ? null : DateTime.parse(json['maintenanceSinceUtc'] as String),
        maintenanceByName: json['maintenanceByName'] == null ? null : json['maintenanceByName'] as String,
        policyProfileVersion: json['policyProfileVersion'] == null ? null : (json['policyProfileVersion'] as num).toInt(),
        gameLibraryVersion: json['gameLibraryVersion'] == null ? null : (json['gameLibraryVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'serverTimeUtc': serverTimeUtc.toIso8601String(),
        'heartbeatIntervalSeconds': heartbeatIntervalSeconds,
        'commands': commands.map((item) => item.toJson()).toList(),
        'effectiveGraceMinutes': effectiveGraceMinutes,
        'seatingCode': seatingCode,
        'seatingCodeExpiresAtUtc': seatingCodeExpiresAtUtc?.toIso8601String(),
        'rotateCredential': rotateCredential,
        'branding': branding?.toJson(),
        'seat': seat?.toJson(),
        'sessionOwner': sessionOwner?.toJson(),
        'features': features?.map((item) => item).toList(),
        'pendingSignInClaim': pendingSignInClaim?.toJson(),
        'maintenance': maintenance,
        'maintenanceSinceUtc': maintenanceSinceUtc?.toIso8601String(),
        'maintenanceByName': maintenanceByName,
        'policyProfileVersion': policyProfileVersion,
        'gameLibraryVersion': gameLibraryVersion,
      };
}

/// Контракт: Devices/DeviceInventoryItemDto.cs
class DeviceInventoryItemDto {
  const DeviceInventoryItemDto({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.machineName,
    required this.agentVersion,
    required this.shellVersion,
    required this.enrolledAtUtc,
    this.lastHeartbeatAtUtc,
    required this.isOnline,
    required this.isLocked,
    this.seatId,
    this.seatName,
    this.zoneId,
    this.zoneName,
    required this.activeCredentialCount,
    required this.installedAppCount,
    required this.pendingCommandCount,
    required this.failedCommandCount,
    this.displayName,
    this.role,
    this.enrollmentState,
    this.hardwareChanged,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String machineName;
  final String agentVersion;
  final String shellVersion;
  final DateTime enrolledAtUtc;
  final DateTime? lastHeartbeatAtUtc;
  final bool isOnline;
  final bool isLocked;
  final String? seatId;
  final String? seatName;
  final String? zoneId;
  final String? zoneName;
  final int activeCredentialCount;
  final int installedAppCount;
  final int pendingCommandCount;
  final int failedCommandCount;
  final String? displayName;
  final String? role;
  final String? enrollmentState;

  /// Железо отличается от принятого — в карточке видно, что поменялось, и кнопка «Принять».
  final bool? hardwareChanged;

  factory DeviceInventoryItemDto.fromJson(Map<String, dynamic> json) => DeviceInventoryItemDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        agentVersion: json['agentVersion'] as String,
        shellVersion: json['shellVersion'] as String,
        enrolledAtUtc: DateTime.parse(json['enrolledAtUtc'] as String),
        lastHeartbeatAtUtc: json['lastHeartbeatAtUtc'] == null ? null : DateTime.parse(json['lastHeartbeatAtUtc'] as String),
        isOnline: json['isOnline'] as bool,
        isLocked: json['isLocked'] as bool,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
        zoneId: json['zoneId'] == null ? null : json['zoneId'] as String,
        zoneName: json['zoneName'] == null ? null : json['zoneName'] as String,
        activeCredentialCount: (json['activeCredentialCount'] as num).toInt(),
        installedAppCount: (json['installedAppCount'] as num).toInt(),
        pendingCommandCount: (json['pendingCommandCount'] as num).toInt(),
        failedCommandCount: (json['failedCommandCount'] as num).toInt(),
        displayName: json['displayName'] == null ? null : json['displayName'] as String,
        role: json['role'] == null ? null : json['role'] as String,
        enrollmentState: json['enrollmentState'] == null ? null : json['enrollmentState'] as String,
        hardwareChanged: json['hardwareChanged'] == null ? null : json['hardwareChanged'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'machineName': machineName,
        'agentVersion': agentVersion,
        'shellVersion': shellVersion,
        'enrolledAtUtc': enrolledAtUtc.toIso8601String(),
        'lastHeartbeatAtUtc': lastHeartbeatAtUtc?.toIso8601String(),
        'isOnline': isOnline,
        'isLocked': isLocked,
        'seatId': seatId,
        'seatName': seatName,
        'zoneId': zoneId,
        'zoneName': zoneName,
        'activeCredentialCount': activeCredentialCount,
        'installedAppCount': installedAppCount,
        'pendingCommandCount': pendingCommandCount,
        'failedCommandCount': failedCommandCount,
        'displayName': displayName,
        'role': role,
        'enrollmentState': enrollmentState,
        'hardwareChanged': hardwareChanged,
      };
}

/// «Вернуть в зал» с самого ПК (спека оболочки, §6.5): техник закончил и нажал кнопку на полосе.
/// Агент зовёт сервер ключом устройства, а не ждёт Панель — иначе ПК стоял бы открытым, пока
/// кто-нибудь не дойдёт до стойки.
///
/// Контракт: Devices/DeviceMaintenanceContracts.cs
class DeviceMaintenanceReturnRequest {
  const DeviceMaintenanceReturnRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;

  factory DeviceMaintenanceReturnRequest.fromJson(Map<String, dynamic> json) => DeviceMaintenanceReturnRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
      };
}

/// Отказ входа на ПК. RetryAfterUtc — только у too_many_attempts.
///
/// Контракт: Devices/DevicePlayerSignInContracts.cs
class DevicePlayerSignInErrorDto {
  const DevicePlayerSignInErrorDto({
    required this.error,
    this.retryAfterUtc,
  });


  /// Одно из DevicePlayerSignInErrorCodeNames.
  final String error;
  final DateTime? retryAfterUtc;

  factory DevicePlayerSignInErrorDto.fromJson(Map<String, dynamic> json) => DevicePlayerSignInErrorDto(
        error: json['error'] as String,
        retryAfterUtc: json['retryAfterUtc'] == null ? null : DateTime.parse(json['retryAfterUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'error': error,
        'retryAfterUtc': retryAfterUtc?.toIso8601String(),
      };
}

/// Игрок входит на самом ПК: номер и ПИН-код. Идёт от агента с ключом устройства, а не с
/// публичного входа: сервер знает, на каком ПК вошли, привязывает токены к этому ПК и считает
/// попытки на устройство, а не на адрес всего клуба за одним роутером.
///
/// Контракт: Devices/DevicePlayerSignInContracts.cs
class DevicePlayerSignInRequest {
  const DevicePlayerSignInRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.phoneNumber,
    required this.pin,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String phoneNumber;
  final String pin;

  factory DevicePlayerSignInRequest.fromJson(Map<String, dynamic> json) => DevicePlayerSignInRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        phoneNumber: json['phoneNumber'] as String,
        pin: json['pin'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'phoneNumber': phoneNumber,
        'pin': pin,
      };
}

/// Последний отчёт ПК о защите — для карточки ПК в Панели.
///
/// Контракт: Devices/ProtectionProfileContracts.cs
class DeviceProtectionReportDto {
  const DeviceProtectionReportDto({
    required this.version,
    required this.appliedAtUtc,
    required this.items,
  });

  final int version;
  final DateTime appliedAtUtc;
  final List<ProtectionItemReportDto> items;

  factory DeviceProtectionReportDto.fromJson(Map<String, dynamic> json) => DeviceProtectionReportDto(
        version: (json['version'] as num).toInt(),
        appliedAtUtc: DateTime.parse(json['appliedAtUtc'] as String),
        items: (json['items'] as List<dynamic>).map((item) => ProtectionItemReportDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'version': version,
        'appliedAtUtc': appliedAtUtc.toIso8601String(),
        'items': items.map((item) => item.toJson()).toList(),
      };
}

/// Агент применил профиль (или снял его на обслуживание) и докладывает, что вышло.
///
/// Контракт: Devices/ProtectionProfileContracts.cs
class DeviceProtectionReportRequest {
  const DeviceProtectionReportRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.version,
    required this.appliedAtUtc,
    required this.items,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final int version;
  final DateTime appliedAtUtc;
  final List<ProtectionItemReportDto> items;

  factory DeviceProtectionReportRequest.fromJson(Map<String, dynamic> json) => DeviceProtectionReportRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        version: (json['version'] as num).toInt(),
        appliedAtUtc: DateTime.parse(json['appliedAtUtc'] as String),
        items: (json['items'] as List<dynamic>).map((item) => ProtectionItemReportDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'version': version,
        'appliedAtUtc': appliedAtUtc.toIso8601String(),
        'items': items.map((item) => item.toJson()).toList(),
      };
}

/// ПК забирает заявку ключом устройства — в ответ токены, привязанные к этому ПК.
///
/// Контракт: Devices/PlayerSignInClaimDeviceContracts.cs
class DeviceRedeemSignInClaimRequest {
  const DeviceRedeemSignInClaimRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;

  factory DeviceRedeemSignInClaimRequest.fromJson(Map<String, dynamic> json) => DeviceRedeemSignInClaimRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
      };
}

/// Контракт: Devices/DeviceSeatAssignmentDto.cs
class DeviceSeatAssignmentDto {
  const DeviceSeatAssignmentDto({
    required this.deviceSeatAssignmentId,
    required this.organizationId,
    required this.branchId,
    required this.seatId,
    required this.deviceId,
    required this.attachedAtUtc,
    this.detachedAtUtc,
  });

  final String deviceSeatAssignmentId;
  final String organizationId;
  final String branchId;
  final String seatId;
  final String deviceId;
  final DateTime attachedAtUtc;
  final DateTime? detachedAtUtc;

  factory DeviceSeatAssignmentDto.fromJson(Map<String, dynamic> json) => DeviceSeatAssignmentDto(
        deviceSeatAssignmentId: json['deviceSeatAssignmentId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        seatId: json['seatId'] as String,
        deviceId: json['deviceId'] as String,
        attachedAtUtc: DateTime.parse(json['attachedAtUtc'] as String),
        detachedAtUtc: json['detachedAtUtc'] == null ? null : DateTime.parse(json['detachedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'deviceSeatAssignmentId': deviceSeatAssignmentId,
        'organizationId': organizationId,
        'branchId': branchId,
        'seatId': seatId,
        'deviceId': deviceId,
        'attachedAtUtc': attachedAtUtc.toIso8601String(),
        'detachedAtUtc': detachedAtUtc?.toIso8601String(),
      };
}

/// Место, к которому привязан ПК, — то, что оболочка пишет в шапке: «ПК 07 · Общий зал». Имя
/// места клуб набирает сам, номера отдельно от имени нет.
///
/// Контракт: Devices/DeviceShellContextContracts.cs
class DeviceSeatDto {
  const DeviceSeatDto({
    required this.label,
    this.zoneName,
  });

  final String label;

  /// Пусто — место без зоны или зона удалена.
  final String? zoneName;

  factory DeviceSeatDto.fromJson(Map<String, dynamic> json) => DeviceSeatDto(
        label: json['label'] as String,
        zoneName: json['zoneName'] == null ? null : json['zoneName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'label': label,
        'zoneName': zoneName,
      };
}

/// Чья сессия идёт на ПК. Оболочке это нужно, чтобы не открыть вошедшему чужую сессию: посаженный
/// у стойки гость и игрок со своим счётом выглядят по-разному, а вошедший не владелец видит «эта
/// сессия не ваша».
///
/// Контракт: Devices/DeviceShellContextContracts.cs
class DeviceSessionOwnerDto {
  const DeviceSessionOwnerDto({
    required this.kind,
    this.playerAccountId,
  });


  /// Одно из DeviceSessionOwnerKindNames.
  final String kind;

  /// Счёт игрока; только у Kind = player.
  final String? playerAccountId;

  factory DeviceSessionOwnerDto.fromJson(Map<String, dynamic> json) => DeviceSessionOwnerDto(
        kind: json['kind'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'kind': kind,
        'playerAccountId': playerAccountId,
      };
}

/// Контракт: Sessions/DeviceSessionSnapshotRequest.cs
class DeviceSessionSnapshotRequest {
  const DeviceSessionSnapshotRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    this.activeSessionId,
    this.activeLease,
    required this.isLocked,
    required this.pendingLocalEventCount,
    required this.observedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String? activeSessionId;
  final SessionLeaseDto? activeLease;
  final bool isLocked;
  final int pendingLocalEventCount;
  final DateTime observedAtUtc;

  factory DeviceSessionSnapshotRequest.fromJson(Map<String, dynamic> json) => DeviceSessionSnapshotRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        activeSessionId: json['activeSessionId'] == null ? null : json['activeSessionId'] as String,
        activeLease: json['activeLease'] == null ? null : SessionLeaseDto.fromJson(json['activeLease'] as Map<String, dynamic>),
        isLocked: json['isLocked'] as bool,
        pendingLocalEventCount: (json['pendingLocalEventCount'] as num).toInt(),
        observedAtUtc: DateTime.parse(json['observedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'activeSessionId': activeSessionId,
        'activeLease': activeLease?.toJson(),
        'isLocked': isLocked,
        'pendingLocalEventCount': pendingLocalEventCount,
        'observedAtUtc': observedAtUtc.toIso8601String(),
      };
}

/// Витрина свободного ПК (спека оболочки, §5.7): что показывает экран, пока за ПК никто не сидит.
/// Тексты — словами клуба, как их написали в Панели; подписи вроде «Турнир» переводит оболочка.
///
/// Контракт: Showcase/ShowcaseContracts.cs
class DeviceShowcaseDto {
  const DeviceShowcaseDto({
    required this.cards,
  });

  final List<ShowcaseCardDto> cards;

  factory DeviceShowcaseDto.fromJson(Map<String, dynamic> json) => DeviceShowcaseDto(
        cards: (json['cards'] as List<dynamic>).map((item) => ShowcaseCardDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'cards': cards.map((item) => item.toJson()).toList(),
      };
}

/// Пачка показов с ПК: суммы по карточке за день.
///
/// Контракт: Ads/AdContracts.cs
class DeviceShowcaseImpressionsRequest {
  const DeviceShowcaseImpressionsRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.batchId,
    required this.items,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;

  /// Ключ пачки: повтор той же пачки после обрыва связи не удваивает счёт.
  final String batchId;
  final List<ShowcaseImpressionDto> items;

  factory DeviceShowcaseImpressionsRequest.fromJson(Map<String, dynamic> json) => DeviceShowcaseImpressionsRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        batchId: json['batchId'] as String,
        items: (json['items'] as List<dynamic>).map((item) => ShowcaseImpressionDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'batchId': batchId,
        'items': items.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Devices/DeviceStateChangeRequest.cs
class DeviceStateChangeRequest {
  const DeviceStateChangeRequest({
    required this.organizationId,
    this.reason,
  });

  final String organizationId;
  final String? reason;

  factory DeviceStateChangeRequest.fromJson(Map<String, dynamic> json) => DeviceStateChangeRequest(
        organizationId: json['organizationId'] as String,
        reason: json['reason'] == null ? null : json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'reason': reason,
      };
}

/// Контракт: Devices/DeviceStatusChangedDto.cs
class DeviceStatusChangedDto {
  const DeviceStatusChangedDto({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.machineName,
    required this.isOnline,
    required this.isLocked,
    required this.observedAtUtc,
    this.displayName,
    this.role,
    this.enrollmentState,
    this.seatId,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String machineName;
  final bool isOnline;
  final bool isLocked;
  final DateTime observedAtUtc;
  final String? displayName;
  final String? role;
  final String? enrollmentState;
  final String? seatId;

  factory DeviceStatusChangedDto.fromJson(Map<String, dynamic> json) => DeviceStatusChangedDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        isOnline: json['isOnline'] as bool,
        isLocked: json['isLocked'] as bool,
        observedAtUtc: DateTime.parse(json['observedAtUtc'] as String),
        displayName: json['displayName'] == null ? null : json['displayName'] as String,
        role: json['role'] == null ? null : json['role'] as String,
        enrollmentState: json['enrollmentState'] == null ? null : json['enrollmentState'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'machineName': machineName,
        'isOnline': isOnline,
        'isLocked': isLocked,
        'observedAtUtc': observedAtUtc.toIso8601String(),
        'displayName': displayName,
        'role': role,
        'enrollmentState': enrollmentState,
        'seatId': seatId,
      };
}

/// Контракт: Updates/DeviceUpdateCheckRequest.cs
class DeviceUpdateCheckRequest {
  const DeviceUpdateCheckRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.channel,
    required this.checkedAtUtc,
    required this.installedComponents,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String channel;
  final DateTime checkedAtUtc;
  final List<DeviceComponentVersionDto> installedComponents;

  factory DeviceUpdateCheckRequest.fromJson(Map<String, dynamic> json) => DeviceUpdateCheckRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        channel: json['channel'] as String,
        checkedAtUtc: DateTime.parse(json['checkedAtUtc'] as String),
        installedComponents: (json['installedComponents'] as List<dynamic>).map((item) => DeviceComponentVersionDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'channel': channel,
        'checkedAtUtc': checkedAtUtc.toIso8601String(),
        'installedComponents': installedComponents.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Updates/DeviceUpdateCheckResponse.cs
class DeviceUpdateCheckResponse {
  const DeviceUpdateCheckResponse({
    required this.serverTimeUtc,
    required this.updates,
    this.organizationAdminPreference,
  });

  final DateTime serverTimeUtc;
  final List<ComponentUpdateInstructionDto> updates;
  final OrganizationAdminUpdatePreferenceDto? organizationAdminPreference;

  factory DeviceUpdateCheckResponse.fromJson(Map<String, dynamic> json) => DeviceUpdateCheckResponse(
        serverTimeUtc: DateTime.parse(json['serverTimeUtc'] as String),
        updates: (json['updates'] as List<dynamic>).map((item) => ComponentUpdateInstructionDto.fromJson(item as Map<String, dynamic>)).toList(),
        organizationAdminPreference: json['organizationAdminPreference'] == null ? null : OrganizationAdminUpdatePreferenceDto.fromJson(json['organizationAdminPreference'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'serverTimeUtc': serverTimeUtc.toIso8601String(),
        'updates': updates.map((item) => item.toJson()).toList(),
        'organizationAdminPreference': organizationAdminPreference?.toJson(),
      };
}

/// Контракт: Updates/DeviceUpdateStatusReportRequest.cs
class DeviceUpdateStatusReportRequest {
  const DeviceUpdateStatusReportRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.updateRolloutId,
    required this.updatePackageId,
    required this.component,
    required this.installedVersion,
    required this.targetVersion,
    required this.status,
    required this.message,
    required this.observedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String updateRolloutId;
  final String updatePackageId;
  final String component;
  final String installedVersion;
  final String targetVersion;
  final String status;
  final String message;
  final DateTime observedAtUtc;

  factory DeviceUpdateStatusReportRequest.fromJson(Map<String, dynamic> json) => DeviceUpdateStatusReportRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        updateRolloutId: json['updateRolloutId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        installedVersion: json['installedVersion'] as String,
        targetVersion: json['targetVersion'] as String,
        status: json['status'] as String,
        message: json['message'] as String,
        observedAtUtc: DateTime.parse(json['observedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'updateRolloutId': updateRolloutId,
        'updatePackageId': updatePackageId,
        'component': component,
        'installedVersion': installedVersion,
        'targetVersion': targetVersion,
        'status': status,
        'message': message,
        'observedAtUtc': observedAtUtc.toIso8601String(),
      };
}

/// Контракт: Updates/DeviceUpdateStatusResultDto.cs
class DeviceUpdateStatusResultDto {
  const DeviceUpdateStatusResultDto({
    required this.deviceId,
    required this.updateRolloutId,
    required this.updatePackageId,
    required this.component,
    required this.status,
    required this.message,
    required this.updatedAtUtc,
  });

  final String deviceId;
  final String updateRolloutId;
  final String updatePackageId;
  final String component;
  final String status;
  final String message;
  final DateTime updatedAtUtc;

  factory DeviceUpdateStatusResultDto.fromJson(Map<String, dynamic> json) => DeviceUpdateStatusResultDto(
        deviceId: json['deviceId'] as String,
        updateRolloutId: json['updateRolloutId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        status: json['status'] as String,
        message: json['message'] as String,
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'updateRolloutId': updateRolloutId,
        'updatePackageId': updatePackageId,
        'component': component,
        'status': status,
        'message': message,
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
      };
}

/// Контракт: Updates/UpdateRolloutStatusDto.cs
class DeviceUpdateStatusSnapshotDto {
  const DeviceUpdateStatusSnapshotDto({
    required this.deviceId,
    required this.updateRolloutId,
    required this.updatePackageId,
    required this.component,
    required this.installedVersion,
    required this.targetVersion,
    required this.status,
    required this.message,
    required this.updatedAtUtc,
  });

  final String deviceId;
  final String updateRolloutId;
  final String updatePackageId;
  final String component;
  final String installedVersion;
  final String targetVersion;
  final String status;
  final String message;
  final DateTime updatedAtUtc;

  factory DeviceUpdateStatusSnapshotDto.fromJson(Map<String, dynamic> json) => DeviceUpdateStatusSnapshotDto(
        deviceId: json['deviceId'] as String,
        updateRolloutId: json['updateRolloutId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        installedVersion: json['installedVersion'] as String,
        targetVersion: json['targetVersion'] as String,
        status: json['status'] as String,
        message: json['message'] as String,
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'updateRolloutId': updateRolloutId,
        'updatePackageId': updatePackageId,
        'component': component,
        'installedVersion': installedVersion,
        'targetVersion': targetVersion,
        'status': status,
        'message': message,
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
      };
}

/// Контракт: Devices/DispatchDeviceCommandRequest.cs
class DispatchDeviceCommandRequest {
  const DispatchDeviceCommandRequest({
    required this.type,
    required this.payload,
  });

  final String type;
  final Map<String, String> payload;

  factory DispatchDeviceCommandRequest.fromJson(Map<String, dynamic> json) => DispatchDeviceCommandRequest(
        type: json['type'] as String,
        payload: (json['payload'] as Map<String, dynamic>).map((key, value) => MapEntry(key, value as String)),
      );

  Map<String, dynamic> toJson() => {
        'type': type,
        'payload': payload.map((key, value) => MapEntry(key, value)),
      };
}

/// Чем смена заработала: проданным временем, товаром и удержанной за неявку предоплатой.
/// NoShow стоит отдельно от Time намеренно: удержание — это
/// не проданное время, и сложить их значит показать кассе наигранные часы, которых не было.
/// В Total оно входит — это заработанные деньги, и потерять их в отчёте нельзя.
///
/// Контракт: Shifts/ShiftRevenueDto.cs
class EarnedBreakdownDto {
  const EarnedBreakdownDto({
    required this.time,
    required this.goods,
    required this.noShow,
    required this.total,
  });

  final MoneyDto time;
  final MoneyDto goods;
  final MoneyDto noShow;
  final MoneyDto total;

  factory EarnedBreakdownDto.fromJson(Map<String, dynamic> json) => EarnedBreakdownDto(
        time: MoneyDto.fromJson(json['time'] as Map<String, dynamic>),
        goods: MoneyDto.fromJson(json['goods'] as Map<String, dynamic>),
        noShow: MoneyDto.fromJson(json['noShow'] as Map<String, dynamic>),
        total: MoneyDto.fromJson(json['total'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'time': time.toJson(),
        'goods': goods.toJson(),
        'noShow': noShow.toJson(),
        'total': total.toJson(),
      };
}

/// Список включённых фич для клубского приложения.
///
/// Контракт: Platform/Features/FeatureContracts.cs
class EnabledFeaturesDto {
  const EnabledFeaturesDto({
    required this.features,
  });

  final List<String> features;

  factory EnabledFeaturesDto.fromJson(Map<String, dynamic> json) => EnabledFeaturesDto(
        features: (json['features'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'features': features.map((item) => item).toList(),
      };
}

/// Контракт: Sessions/EndSessionRequest.cs
class EndSessionRequest {
  const EndSessionRequest({
    required this.reason,
    required this.idempotencyKey,
    this.expectedVersion,
  });

  final String reason;
  final String idempotencyKey;
  final int? expectedVersion;

  factory EndSessionRequest.fromJson(Map<String, dynamic> json) => EndSessionRequest(
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'reason': reason,
        'idempotencyKey': idempotencyKey,
        'expectedVersion': expectedVersion,
      };
}

/// GET response. The Hash key is never returned — only whether one is stored, so the UI can show
/// "задан" without exposing the secret (mirrors how the dcgate apiKey is never round-tripped).
///
/// Контракт: Payments/EskhataMerchantConfigDtos.cs
class EskhataMerchantConfigDto {
  const EskhataMerchantConfigDto({
    required this.baseUrl,
    required this.companyId,
    required this.merchantId,
    required this.hashKeySet,
    required this.status,
  });

  final String baseUrl;
  final String companyId;
  final int merchantId;
  final bool hashKeySet;
  final String status;

  factory EskhataMerchantConfigDto.fromJson(Map<String, dynamic> json) => EskhataMerchantConfigDto(
        baseUrl: json['baseUrl'] as String,
        companyId: json['companyId'] as String,
        merchantId: (json['merchantId'] as num).toInt(),
        hashKeySet: json['hashKeySet'] as bool,
        status: json['status'] as String,
      );

  Map<String, dynamic> toJson() => {
        'baseUrl': baseUrl,
        'companyId': companyId,
        'merchantId': merchantId,
        'hashKeySet': hashKeySet,
        'status': status,
      };
}

/// Контракт: Sessions/ExtendSessionRequest.cs
class ExtendSessionRequest {
  const ExtendSessionRequest({
    required this.additionalMinutes,
    required this.tariffRuleVersionId,
    required this.idempotencyKey,
    this.playerAccountId,
    this.billingMode,
    this.tariffVersionId,
    this.playerPackageId,
    this.expectedVersion,
  });

  final int additionalMinutes;
  final String tariffRuleVersionId;
  final String idempotencyKey;
  final String? playerAccountId;
  final String? billingMode;
  final String? tariffVersionId;
  final String? playerPackageId;
  final int? expectedVersion;

  factory ExtendSessionRequest.fromJson(Map<String, dynamic> json) => ExtendSessionRequest(
        additionalMinutes: (json['additionalMinutes'] as num).toInt(),
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        billingMode: json['billingMode'] == null ? null : json['billingMode'] as String,
        tariffVersionId: json['tariffVersionId'] == null ? null : json['tariffVersionId'] as String,
        playerPackageId: json['playerPackageId'] == null ? null : json['playerPackageId'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'additionalMinutes': additionalMinutes,
        'tariffRuleVersionId': tariffRuleVersionId,
        'idempotencyKey': idempotencyKey,
        'playerAccountId': playerAccountId,
        'billingMode': billingMode,
        'tariffVersionId': tariffVersionId,
        'playerPackageId': playerPackageId,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: Diagnostics/BranchDiagnosticsDto.cs
class FailedCommandDiagnosticsDto {
  const FailedCommandDiagnosticsDto({
    required this.deviceId,
    required this.machineName,
    required this.commandId,
    required this.type,
    required this.status,
    this.message,
    required this.updatedAtUtc,
  });

  final String deviceId;
  final String machineName;
  final String commandId;
  final String type;
  final String status;
  final String? message;
  final DateTime updatedAtUtc;

  factory FailedCommandDiagnosticsDto.fromJson(Map<String, dynamic> json) => FailedCommandDiagnosticsDto(
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        commandId: json['commandId'] as String,
        type: json['type'] as String,
        status: json['status'] as String,
        message: json['message'] == null ? null : json['message'] as String,
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'machineName': machineName,
        'commandId': commandId,
        'type': type,
        'status': status,
        'message': message,
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
      };
}

/// Контракт: Diagnostics/BranchDiagnosticsDto.cs
class FailedUpdateDiagnosticsDto {
  const FailedUpdateDiagnosticsDto({
    required this.deviceId,
    required this.machineName,
    required this.updateRolloutId,
    required this.component,
    required this.targetVersion,
    required this.status,
    required this.message,
    required this.updatedAtUtc,
  });

  final String deviceId;
  final String machineName;
  final String updateRolloutId;
  final String component;
  final String targetVersion;
  final String status;
  final String message;
  final DateTime updatedAtUtc;

  factory FailedUpdateDiagnosticsDto.fromJson(Map<String, dynamic> json) => FailedUpdateDiagnosticsDto(
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        updateRolloutId: json['updateRolloutId'] as String,
        component: json['component'] as String,
        targetVersion: json['targetVersion'] as String,
        status: json['status'] as String,
        message: json['message'] as String,
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'machineName': machineName,
        'updateRolloutId': updateRolloutId,
        'component': component,
        'targetVersion': targetVersion,
        'status': status,
        'message': message,
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
      };
}

/// Контракт: FloorMap/FloorMapDto.cs
class FloorMapDto {
  const FloorMapDto({
    required this.branchId,
    required this.branchName,
    required this.seats,
    required this.zones,
  });

  final String branchId;
  final String branchName;
  final List<SeatStatusDto> seats;
  final List<FloorMapZoneDto> zones;

  factory FloorMapDto.fromJson(Map<String, dynamic> json) => FloorMapDto(
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String,
        seats: (json['seats'] as List<dynamic>).map((item) => SeatStatusDto.fromJson(item as Map<String, dynamic>)).toList(),
        zones: (json['zones'] as List<dynamic>).map((item) => FloorMapZoneDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'branchName': branchName,
        'seats': seats.map((item) => item.toJson()).toList(),
        'zones': zones.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: FloorMap/FloorMapDto.cs
class FloorMapZoneDto {
  const FloorMapZoneDto({
    required this.zoneId,
    required this.name,
    required this.sortOrder,
  });

  final String zoneId;
  final String name;
  final int sortOrder;

  factory FloorMapZoneDto.fromJson(Map<String, dynamic> json) => FloorMapZoneDto(
        zoneId: json['zoneId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'zoneId': zoneId,
        'name': name,
        'sortOrder': sortOrder,
      };
}

/// Друг и то, единственное, что о нём видно: имя и «сейчас в зале» — если он сам это показывает.
/// Ни телефона, ни денег, ни истории: дружба не даёт доступа к чужому счёту.
///
/// Контракт: Friends/FriendDtos.cs
class FriendDto {
  const FriendDto({
    required this.platformPersonId,
    required this.displayName,
    this.presence,
  });

  final String platformPersonId;
  final String displayName;

  /// Где он сейчас играет. null — не в зале, или он скрыл своё присутствие. Разницы снаружи
  /// нет намеренно: иначе «скрыт» читалось бы как «он там, но прячется».
  final FriendPresenceDto? presence;

  factory FriendDto.fromJson(Map<String, dynamic> json) => FriendDto(
        platformPersonId: json['platformPersonId'] as String,
        displayName: json['displayName'] as String,
        presence: json['presence'] == null ? null : FriendPresenceDto.fromJson(json['presence'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'platformPersonId': platformPersonId,
        'displayName': displayName,
        'presence': presence?.toJson(),
      };
}

/// Клуб и зал, в которых друг сейчас за ПК.
///
/// Контракт: Friends/FriendDtos.cs
class FriendPresenceDto {
  const FriendPresenceDto({
    required this.organizationName,
    required this.branchName,
  });

  final String organizationName;
  final String branchName;

  factory FriendPresenceDto.fromJson(Map<String, dynamic> json) => FriendPresenceDto(
        organizationName: json['organizationName'] as String,
        branchName: json['branchName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationName': organizationName,
        'branchName': branchName,
      };
}

/// Заявка в друзья. Пришедшую можно принять или отклонить, отправленную — только ждать:
/// отзывать её незачем, а кнопка «отозвать» превратила бы список в пульт.
///
/// Контракт: Friends/FriendDtos.cs
class FriendRequestDto {
  const FriendRequestDto({
    required this.friendRequestId,
    required this.platformPersonId,
    required this.displayName,
    required this.createdAtUtc,
  });

  final String friendRequestId;
  final String platformPersonId;
  final String displayName;
  final DateTime createdAtUtc;

  factory FriendRequestDto.fromJson(Map<String, dynamic> json) => FriendRequestDto(
        friendRequestId: json['friendRequestId'] as String,
        platformPersonId: json['platformPersonId'] as String,
        displayName: json['displayName'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'friendRequestId': friendRequestId,
        'platformPersonId': platformPersonId,
        'displayName': displayName,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Друзья целиком: принятые, пришедшие заявки и отправленные. Один ответ на весь экран —
/// три запроса ради трёх списков платили бы сетью за одно открытие.
///
/// Контракт: Friends/FriendDtos.cs
class FriendsDto {
  const FriendsDto({
    required this.friends,
    required this.incoming,
    required this.outgoing,
    required this.showsPresence,
  });

  final List<FriendDto> friends;
  final List<FriendRequestDto> incoming;
  final List<FriendRequestDto> outgoing;

  /// Видят ли друзья, что человек сейчас в зале. Выключено — список друзей у него остаётся,
  /// но его самого в залах никто не видит.
  final bool showsPresence;

  factory FriendsDto.fromJson(Map<String, dynamic> json) => FriendsDto(
        friends: (json['friends'] as List<dynamic>).map((item) => FriendDto.fromJson(item as Map<String, dynamic>)).toList(),
        incoming: (json['incoming'] as List<dynamic>).map((item) => FriendRequestDto.fromJson(item as Map<String, dynamic>)).toList(),
        outgoing: (json['outgoing'] as List<dynamic>).map((item) => FriendRequestDto.fromJson(item as Map<String, dynamic>)).toList(),
        showsPresence: json['showsPresence'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'friends': friends.map((item) => item.toJson()).toList(),
        'incoming': incoming.map((item) => item.toJson()).toList(),
        'outgoing': outgoing.map((item) => item.toJson()).toList(),
        'showsPresence': showsPresence,
      };
}

/// Контракт: Reports/GameplayTimeReportResultDto.cs
class GameplayTimeReportResultDto {
  const GameplayTimeReportResultDto({
    required this.rows,
    required this.limit,
    required this.totalDurationSeconds,
    required this.totalPackageSeconds,
    required this.totalBonusSeconds,
    required this.gameplayRevenueTotal,
  });

  final List<GameplayTimeReportRowDto> rows;
  final int limit;
  final int totalDurationSeconds;
  final int totalPackageSeconds;
  final int totalBonusSeconds;
  final MoneyDto gameplayRevenueTotal;

  factory GameplayTimeReportResultDto.fromJson(Map<String, dynamic> json) => GameplayTimeReportResultDto(
        rows: (json['rows'] as List<dynamic>).map((item) => GameplayTimeReportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
        totalDurationSeconds: (json['totalDurationSeconds'] as num).toInt(),
        totalPackageSeconds: (json['totalPackageSeconds'] as num).toInt(),
        totalBonusSeconds: (json['totalBonusSeconds'] as num).toInt(),
        gameplayRevenueTotal: MoneyDto.fromJson(json['gameplayRevenueTotal'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'rows': rows.map((item) => item.toJson()).toList(),
        'limit': limit,
        'totalDurationSeconds': totalDurationSeconds,
        'totalPackageSeconds': totalPackageSeconds,
        'totalBonusSeconds': totalBonusSeconds,
        'gameplayRevenueTotal': gameplayRevenueTotal.toJson(),
      };
}

/// Контракт: Reports/GameplayTimeReportRowDto.cs
class GameplayTimeReportRowDto {
  const GameplayTimeReportRowDto({
    required this.sessionId,
    required this.organizationId,
    required this.branchId,
    required this.seatId,
    required this.deviceId,
    required this.createdByStaffUserId,
    required this.playerKind,
    this.playerAccountId,
    required this.state,
    required this.durationSeconds,
    required this.packageSeconds,
    required this.bonusSeconds,
    required this.gameplayRevenue,
    this.startedAtUtc,
    this.endedAtUtc,
    this.endsAtUtc,
  });

  final String sessionId;
  final String organizationId;
  final String branchId;
  final String seatId;
  final String deviceId;
  final String createdByStaffUserId;
  final String playerKind;
  final String? playerAccountId;
  final String state;
  final int durationSeconds;
  final int packageSeconds;
  final int bonusSeconds;
  final MoneyDto gameplayRevenue;
  final DateTime? startedAtUtc;
  final DateTime? endedAtUtc;
  final DateTime? endsAtUtc;

  factory GameplayTimeReportRowDto.fromJson(Map<String, dynamic> json) => GameplayTimeReportRowDto(
        sessionId: json['sessionId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        seatId: json['seatId'] as String,
        deviceId: json['deviceId'] as String,
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        playerKind: json['playerKind'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        state: json['state'] as String,
        durationSeconds: (json['durationSeconds'] as num).toInt(),
        packageSeconds: (json['packageSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num).toInt(),
        gameplayRevenue: MoneyDto.fromJson(json['gameplayRevenue'] as Map<String, dynamic>),
        startedAtUtc: json['startedAtUtc'] == null ? null : DateTime.parse(json['startedAtUtc'] as String),
        endedAtUtc: json['endedAtUtc'] == null ? null : DateTime.parse(json['endedAtUtc'] as String),
        endsAtUtc: json['endsAtUtc'] == null ? null : DateTime.parse(json['endsAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'organizationId': organizationId,
        'branchId': branchId,
        'seatId': seatId,
        'deviceId': deviceId,
        'createdByStaffUserId': createdByStaffUserId,
        'playerKind': playerKind,
        'playerAccountId': playerAccountId,
        'state': state,
        'durationSeconds': durationSeconds,
        'packageSeconds': packageSeconds,
        'bonusSeconds': bonusSeconds,
        'gameplayRevenue': gameplayRevenue.toJson(),
        'startedAtUtc': startedAtUtc?.toIso8601String(),
        'endedAtUtc': endedAtUtc?.toIso8601String(),
        'endsAtUtc': endsAtUtc?.toIso8601String(),
      };
}

/// Контракт: Players/GuestImportContracts.cs
class GuestImportIssueDto {
  const GuestImportIssueDto({
    required this.row,
    required this.code,
  });


  /// Номер строки в файле, с единицы, без заголовка.
  final int row;

  /// Одно из GuestImportIssueNames
  final String code;

  factory GuestImportIssueDto.fromJson(Map<String, dynamic> json) => GuestImportIssueDto(
        row: (json['row'] as num).toInt(),
        code: json['code'] as String,
      );

  Map<String, dynamic> toJson() => {
        'row': row,
        'code': code,
      };
}

/// Контракт: Players/GuestImportContracts.cs
class GuestImportRequest {
  const GuestImportRequest({
    required this.organizationId,
    required this.currencyCode,
    required this.source,
    required this.rows,
    required this.dryRun,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String currencyCode;

  /// Откуда перенос — «SmartShell», «Langame»: в журнал и в описание остатков.
  final String source;
  final List<GuestImportRowDto> rows;

  /// true — только проверить и посчитать, ничего не записывать.
  final bool dryRun;
  final String idempotencyKey;

  factory GuestImportRequest.fromJson(Map<String, dynamic> json) => GuestImportRequest(
        organizationId: json['organizationId'] as String,
        currencyCode: json['currencyCode'] as String,
        source: json['source'] as String,
        rows: (json['rows'] as List<dynamic>).map((item) => GuestImportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        dryRun: json['dryRun'] as bool,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'currencyCode': currencyCode,
        'source': source,
        'rows': rows.map((item) => item.toJson()).toList(),
        'dryRun': dryRun,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Players/GuestImportContracts.cs
class GuestImportResultDto {
  const GuestImportResultDto({
    required this.committed,
    required this.total,
    required this.created,
    required this.matched,
    required this.skipped,
    required this.balanceTotal,
    required this.bonusTotal,
    required this.issues,
  });

  final bool committed;
  final int total;

  /// Новых карточек гостей.
  final int created;

  /// Гость с этим номером уже есть в клубе — остатки легли на его карточку.
  final int matched;

  /// Строки, которые не переносятся (причина — в Issues).
  final int skipped;
  final MoneyDto balanceTotal;
  final MoneyDto bonusTotal;
  final List<GuestImportIssueDto> issues;

  factory GuestImportResultDto.fromJson(Map<String, dynamic> json) => GuestImportResultDto(
        committed: json['committed'] as bool,
        total: (json['total'] as num).toInt(),
        created: (json['created'] as num).toInt(),
        matched: (json['matched'] as num).toInt(),
        skipped: (json['skipped'] as num).toInt(),
        balanceTotal: MoneyDto.fromJson(json['balanceTotal'] as Map<String, dynamic>),
        bonusTotal: MoneyDto.fromJson(json['bonusTotal'] as Map<String, dynamic>),
        issues: (json['issues'] as List<dynamic>).map((item) => GuestImportIssueDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'committed': committed,
        'total': total,
        'created': created,
        'matched': matched,
        'skipped': skipped,
        'balanceTotal': balanceTotal.toJson(),
        'bonusTotal': bonusTotal.toJson(),
        'issues': issues.map((item) => item.toJson()).toList(),
      };
}

/// Перенос гостей из прежней программы клуба (план `2026-09-25-guest-import.md`): номер, имя,
/// баланс и бонусы становятся карточкой гостя и начальными остатками в журнале. Выгрузку делает
/// владелец клуба; сначала — пробный прогон без записи, потом перенос.
///
/// Контракт: Players/GuestImportContracts.cs
class GuestImportRowDto {
  const GuestImportRowDto({
    this.phone,
    this.name,
    required this.balanceMinorUnits,
    required this.bonusMinorUnits,
  });

  final String? phone;
  final String? name;
  final int balanceMinorUnits;
  final int bonusMinorUnits;

  factory GuestImportRowDto.fromJson(Map<String, dynamic> json) => GuestImportRowDto(
        phone: json['phone'] == null ? null : json['phone'] as String,
        name: json['name'] == null ? null : json['name'] as String,
        balanceMinorUnits: (json['balanceMinorUnits'] as num).toInt(),
        bonusMinorUnits: (json['bonusMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'phone': phone,
        'name': name,
        'balanceMinorUnits': balanceMinorUnits,
        'bonusMinorUnits': bonusMinorUnits,
      };
}

/// Что в железе отличается от принятого: было → стало.
///
/// Контракт: Devices/DeviceHardwareContracts.cs
class HardwareChangeDto {
  const HardwareChangeDto({
    required this.component,
    this.was,
    this.now,
  });


  /// Одно из HardwareComponentNames.
  final String component;
  final String? was;
  final String? now;

  factory HardwareChangeDto.fromJson(Map<String, dynamic> json) => HardwareChangeDto(
        component: json['component'] as String,
        was: json['was'] == null ? null : json['was'] as String,
        now: json['now'] == null ? null : json['now'] as String,
      );

  Map<String, dynamic> toJson() => {
        'component': component,
        'was': was,
        'now': now,
      };
}

/// <param name="Name">Буква диска: «C:».</param>
///
/// Контракт: Devices/DeviceHardwareContracts.cs
class HardwareDiskDto {
  const HardwareDiskDto({
    required this.name,
    required this.sizeGb,
  });

  final String name;
  final int sizeGb;

  factory HardwareDiskDto.fromJson(Map<String, dynamic> json) => HardwareDiskDto(
        name: json['name'] as String,
        sizeGb: (json['sizeGb'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'sizeGb': sizeGb,
      };
}

/// Контракт: Devices/DeviceHardwareContracts.cs
class HardwareGpuDto {
  const HardwareGpuDto({
    required this.name,
    this.memoryGb,
  });

  final String name;
  final int? memoryGb;

  factory HardwareGpuDto.fromJson(Map<String, dynamic> json) => HardwareGpuDto(
        name: json['name'] as String,
        memoryGb: json['memoryGb'] == null ? null : (json['memoryGb'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'memoryGb': memoryGb,
      };
}

/// Снимок железа ПК (спека оболочки, P9): что стоит внутри. Сравнивается с принятым — поменяли
/// видеокарту или вынули планку памяти, и клуб видит это в карточке ПК, а не узнаёт от игрока.
///
/// Контракт: Devices/DeviceHardwareContracts.cs
class HardwareSnapshotDto {
  const HardwareSnapshotDto({
    this.cpu,
    required this.cpuThreads,
    required this.memoryGb,
    required this.gpus,
    this.motherboard,
    required this.disks,
    this.os,
    this.bios,
  });

  final String? cpu;
  final int cpuThreads;

  /// Вся память в гигабайтах, округлённо: 15,9 ГБ Windows — это 16 ГБ в корпусе.
  final int memoryGb;
  final List<HardwareGpuDto> gpus;
  final String? motherboard;
  final List<HardwareDiskDto> disks;

  /// Windows и её сборка — видна, но не считается изменением железа: обновления идут каждый месяц.
  final String? os;
  final String? bios;

  factory HardwareSnapshotDto.fromJson(Map<String, dynamic> json) => HardwareSnapshotDto(
        cpu: json['cpu'] == null ? null : json['cpu'] as String,
        cpuThreads: (json['cpuThreads'] as num).toInt(),
        memoryGb: (json['memoryGb'] as num).toInt(),
        gpus: (json['gpus'] as List<dynamic>).map((item) => HardwareGpuDto.fromJson(item as Map<String, dynamic>)).toList(),
        motherboard: json['motherboard'] == null ? null : json['motherboard'] as String,
        disks: (json['disks'] as List<dynamic>).map((item) => HardwareDiskDto.fromJson(item as Map<String, dynamic>)).toList(),
        os: json['os'] == null ? null : json['os'] as String,
        bios: json['bios'] == null ? null : json['bios'] as String,
      );

  Map<String, dynamic> toJson() => {
        'cpu': cpu,
        'cpuThreads': cpuThreads,
        'memoryGb': memoryGb,
        'gpus': gpus.map((item) => item.toJson()).toList(),
        'motherboard': motherboard,
        'disks': disks.map((item) => item.toJson()).toList(),
        'os': os,
        'bios': bios,
      };
}

/// Контракт: Platform/Health/PlatformHealthContracts.cs
class IncidentDto {
  const IncidentDto({
    required this.incidentId,
    required this.kind,
    required this.dedupKey,
    required this.severity,
    required this.detailsJson,
    required this.openedAtUtc,
    required this.lastSeenAtUtc,
  });

  final String incidentId;
  final String kind;
  final String dedupKey;
  final String severity;
  final String detailsJson;
  final DateTime openedAtUtc;
  final DateTime lastSeenAtUtc;

  factory IncidentDto.fromJson(Map<String, dynamic> json) => IncidentDto(
        incidentId: json['incidentId'] as String,
        kind: json['kind'] as String,
        dedupKey: json['dedupKey'] as String,
        severity: json['severity'] as String,
        detailsJson: json['detailsJson'] as String,
        openedAtUtc: DateTime.parse(json['openedAtUtc'] as String),
        lastSeenAtUtc: DateTime.parse(json['lastSeenAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'incidentId': incidentId,
        'kind': kind,
        'dedupKey': dedupKey,
        'severity': severity,
        'detailsJson': detailsJson,
        'openedAtUtc': openedAtUtc.toIso8601String(),
        'lastSeenAtUtc': lastSeenAtUtc.toIso8601String(),
      };
}

/// Контракт: Shifts/ShiftRevenueDto.cs
class InflowBreakdownDto {
  const InflowBreakdownDto({
    required this.cash,
    required this.nonCash,
    required this.walletTopUps,
    required this.directTotal,
  });

  final MoneyDto cash;
  final MoneyDto nonCash;
  final MoneyDto walletTopUps;
  final MoneyDto directTotal;

  factory InflowBreakdownDto.fromJson(Map<String, dynamic> json) => InflowBreakdownDto(
        cash: MoneyDto.fromJson(json['cash'] as Map<String, dynamic>),
        nonCash: MoneyDto.fromJson(json['nonCash'] as Map<String, dynamic>),
        walletTopUps: MoneyDto.fromJson(json['walletTopUps'] as Map<String, dynamic>),
        directTotal: MoneyDto.fromJson(json['directTotal'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'cash': cash.toJson(),
        'nonCash': nonCash.toJson(),
        'walletTopUps': walletTopUps.toJson(),
        'directTotal': directTotal.toJson(),
      };
}

/// Контракт: Install/InstallDiscoverResponse.cs
class InstallBranchDto {
  const InstallBranchDto({
    required this.branchId,
    required this.slug,
    required this.name,
    required this.floorMap,
    required this.freeSeatIds,
    this.hasTariff,
    this.hasStaffBesidesOwner,
  });

  final String branchId;
  final String slug;
  final String name;
  final FloorMapDto floorMap;
  final List<String> freeSeatIds;

  /// В зале есть хотя бы один действующий тариф — значит платную сессию начать есть чем.
  final bool? hasTariff;

  /// В зале есть кто-то кроме владельца: приглашать первого сотрудника уже не нужно.
  final bool? hasStaffBesidesOwner;

  factory InstallBranchDto.fromJson(Map<String, dynamic> json) => InstallBranchDto(
        branchId: json['branchId'] as String,
        slug: json['slug'] as String,
        name: json['name'] as String,
        floorMap: FloorMapDto.fromJson(json['floorMap'] as Map<String, dynamic>),
        freeSeatIds: (json['freeSeatIds'] as List<dynamic>).map((item) => item as String).toList(),
        hasTariff: json['hasTariff'] == null ? null : json['hasTariff'] as bool,
        hasStaffBesidesOwner: json['hasStaffBesidesOwner'] == null ? null : json['hasStaffBesidesOwner'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'slug': slug,
        'name': name,
        'floorMap': floorMap.toJson(),
        'freeSeatIds': freeSeatIds.map((item) => item).toList(),
        'hasTariff': hasTariff,
        'hasStaffBesidesOwner': hasStaffBesidesOwner,
      };
}

/// Действующий код установки филиала.
/// <param name="Code">Сам код — только в ответе на выдачу; в списке его нет.</param>
/// <param name="UsedDevices">Сколько новых ПК уже встало по коду. Переустановка того же ПК код не тратит.</param>
///
/// Контракт: Install/InstallCodeContracts.cs
class InstallCodeDto {
  const InstallCodeDto({
    required this.installCodeId,
    required this.branchId,
    this.code,
    required this.createdAtUtc,
    required this.expiresAtUtc,
    required this.maxDevices,
    required this.usedDevices,
  });

  final String installCodeId;
  final String branchId;
  final String? code;
  final DateTime createdAtUtc;
  final DateTime expiresAtUtc;
  final int maxDevices;
  final int usedDevices;

  factory InstallCodeDto.fromJson(Map<String, dynamic> json) => InstallCodeDto(
        installCodeId: json['installCodeId'] as String,
        branchId: json['branchId'] as String,
        code: json['code'] == null ? null : json['code'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        maxDevices: (json['maxDevices'] as num).toInt(),
        usedDevices: (json['usedDevices'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'installCodeId': installCodeId,
        'branchId': branchId,
        'code': code,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'maxDevices': maxDevices,
        'usedDevices': usedDevices,
      };
}

/// Тихая регистрация ПК по коду.
/// <param name="SeatName">
/// Место по имени. Не названо — ищется место с именем компьютера. Не нашлось или занято другим
/// ПК — ПК встаёт без места, и его привязывают в Панели: отказ из-за опечатки в имени оставил бы
/// ПК вовсе не зарегистрированным, а узнал бы о нём техник только обходом зала.
/// </param>
///
/// Контракт: Install/InstallCodeContracts.cs
class InstallCodeEnrollRequest {
  const InstallCodeEnrollRequest({
    required this.code,
    this.seatName,
    this.displayName,
    required this.machineName,
    required this.devicePublicKey,
  });

  final String code;
  final String? seatName;
  final String? displayName;
  final String machineName;
  final String devicePublicKey;

  factory InstallCodeEnrollRequest.fromJson(Map<String, dynamic> json) => InstallCodeEnrollRequest(
        code: json['code'] as String,
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
        displayName: json['displayName'] == null ? null : json['displayName'] as String,
        machineName: json['machineName'] as String,
        devicePublicKey: json['devicePublicKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
        'seatName': seatName,
        'displayName': displayName,
        'machineName': machineName,
        'devicePublicKey': devicePublicKey,
      };
}

/// Контракт: Install/InstallCreateSeatResponse.cs
class InstallCreateSeatResponse {
  const InstallCreateSeatResponse({
    required this.organizationId,
    required this.branchId,
    required this.zoneId,
    required this.seatId,
    required this.name,
    required this.sortOrder,
  });

  final String organizationId;
  final String branchId;
  final String zoneId;
  final String seatId;
  final String name;
  final int sortOrder;

  factory InstallCreateSeatResponse.fromJson(Map<String, dynamic> json) => InstallCreateSeatResponse(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        zoneId: json['zoneId'] as String,
        seatId: json['seatId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'zoneId': zoneId,
        'seatId': seatId,
        'name': name,
        'sortOrder': sortOrder,
      };
}

/// Контракт: Install/InstallDiscoverResponse.cs
class InstallDiscoverResponse {
  const InstallDiscoverResponse({
    required this.ownerDisplayName,
    required this.branches,
    this.brandingConfigured,
  });

  final String ownerDisplayName;
  final List<InstallBranchDto> branches;

  /// Оформление клуба уже задано. Мастер спрашивает про логотип и цвет только когда их нет:
  /// админских ПК в клубе бывает несколько, и на втором это был бы не вопрос, а шанс затереть
  /// настроенное. В конце списка и с умолчанием — старый мастер продолжит работать.
  final bool? brandingConfigured;

  factory InstallDiscoverResponse.fromJson(Map<String, dynamic> json) => InstallDiscoverResponse(
        ownerDisplayName: json['ownerDisplayName'] as String,
        branches: (json['branches'] as List<dynamic>).map((item) => InstallBranchDto.fromJson(item as Map<String, dynamic>)).toList(),
        brandingConfigured: json['brandingConfigured'] == null ? null : json['brandingConfigured'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'ownerDisplayName': ownerDisplayName,
        'branches': branches.map((item) => item.toJson()).toList(),
        'brandingConfigured': brandingConfigured,
      };
}

/// Контракт: Devices/InstalledAppDto.cs
class InstalledAppDto {
  const InstalledAppDto({
    required this.displayName,
    this.version,
    this.publisher,
    this.installLocation,
    this.installedAtUtc,
  });

  final String displayName;
  final String? version;
  final String? publisher;
  final String? installLocation;
  final DateTime? installedAtUtc;

  factory InstalledAppDto.fromJson(Map<String, dynamic> json) => InstalledAppDto(
        displayName: json['displayName'] as String,
        version: json['version'] == null ? null : json['version'] as String,
        publisher: json['publisher'] == null ? null : json['publisher'] as String,
        installLocation: json['installLocation'] == null ? null : json['installLocation'] as String,
        installedAtUtc: json['installedAtUtc'] == null ? null : DateTime.parse(json['installedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'displayName': displayName,
        'version': version,
        'publisher': publisher,
        'installLocation': installLocation,
        'installedAtUtc': installedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Devices/InstalledAppReportRequest.cs
class InstalledAppReportRequest {
  const InstalledAppReportRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.reportedAtUtc,
    required this.apps,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final DateTime reportedAtUtc;
  final List<InstalledAppDto> apps;

  factory InstalledAppReportRequest.fromJson(Map<String, dynamic> json) => InstalledAppReportRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        reportedAtUtc: DateTime.parse(json['reportedAtUtc'] as String),
        apps: (json['apps'] as List<dynamic>).map((item) => InstalledAppDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'reportedAtUtc': reportedAtUtc.toIso8601String(),
        'apps': apps.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Install/InstallEnrollResponse.cs
class InstallEnrollResponse {
  const InstallEnrollResponse({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.credentialId,
    required this.credentialSecret,
    required this.enrollmentState,
    required this.apiBaseUrl,
    required this.updateChannel,
    required this.enrolledAtUtc,
    required this.leaseSigningPublicKeyPem,
    required this.updatePackageSigningPublicKeyPem,
    this.assignedSeatName,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String credentialId;
  final String credentialSecret;
  final String enrollmentState;
  final String apiBaseUrl;
  final String updateChannel;
  final DateTime enrolledAtUtc;
  final String leaseSigningPublicKeyPem;
  final String updatePackageSigningPublicKeyPem;

  /// На какое место встал ПК. Null — без места: при тихой установке место по имени не нашлось или занято.
  final String? assignedSeatName;

  factory InstallEnrollResponse.fromJson(Map<String, dynamic> json) => InstallEnrollResponse(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        credentialId: json['credentialId'] as String,
        credentialSecret: json['credentialSecret'] as String,
        enrollmentState: json['enrollmentState'] as String,
        apiBaseUrl: json['apiBaseUrl'] as String,
        updateChannel: json['updateChannel'] as String,
        enrolledAtUtc: DateTime.parse(json['enrolledAtUtc'] as String),
        leaseSigningPublicKeyPem: json['leaseSigningPublicKeyPem'] as String,
        updatePackageSigningPublicKeyPem: json['updatePackageSigningPublicKeyPem'] as String,
        assignedSeatName: json['assignedSeatName'] == null ? null : json['assignedSeatName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'credentialId': credentialId,
        'credentialSecret': credentialSecret,
        'enrollmentState': enrollmentState,
        'apiBaseUrl': apiBaseUrl,
        'updateChannel': updateChannel,
        'enrolledAtUtc': enrolledAtUtc.toIso8601String(),
        'leaseSigningPublicKeyPem': leaseSigningPublicKeyPem,
        'updatePackageSigningPublicKeyPem': updatePackageSigningPublicKeyPem,
        'assignedSeatName': assignedSeatName,
      };
}

/// Контракт: Inventory/InventoryStockDto.cs
class InventoryStockDto {
  const InventoryStockDto({
    required this.productId,
    required this.productName,
    required this.sku,
    required this.trackStock,
    required this.stockOnHand,
  });

  final String productId;
  final String productName;
  final String sku;
  final bool trackStock;
  final int stockOnHand;

  factory InventoryStockDto.fromJson(Map<String, dynamic> json) => InventoryStockDto(
        productId: json['productId'] as String,
        productName: json['productName'] as String,
        sku: json['sku'] as String,
        trackStock: json['trackStock'] as bool,
        stockOnHand: (json['stockOnHand'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'productName': productName,
        'sku': sku,
        'trackStock': trackStock,
        'stockOnHand': stockOnHand,
      };
}

/// Контракт: Platform/Billing/InvoiceDto.cs
class InvoiceDto {
  const InvoiceDto({
    required this.invoiceId,
    required this.organizationId,
    required this.number,
    required this.kind,
    required this.periodStartUtc,
    required this.periodEndUtc,
    required this.issuedAtUtc,
    required this.dueAtUtc,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.status,
    this.paidAtUtc,
    this.voidedAtUtc,
    this.voidReason,
    required this.description,
    required this.grossAmountMinorUnits,
    required this.discountMinorUnits,
  });

  final String invoiceId;
  final String organizationId;
  final int number;
  final String kind;
  final DateTime periodStartUtc;
  final DateTime periodEndUtc;
  final DateTime issuedAtUtc;
  final DateTime dueAtUtc;
  final int amountMinorUnits;
  final String currencyCode;
  final String status;
  final DateTime? paidAtUtc;
  final DateTime? voidedAtUtc;
  final String? voidReason;
  final String description;
  final int grossAmountMinorUnits;
  final int discountMinorUnits;

  factory InvoiceDto.fromJson(Map<String, dynamic> json) => InvoiceDto(
        invoiceId: json['invoiceId'] as String,
        organizationId: json['organizationId'] as String,
        number: (json['number'] as num).toInt(),
        kind: json['kind'] as String,
        periodStartUtc: DateTime.parse(json['periodStartUtc'] as String),
        periodEndUtc: DateTime.parse(json['periodEndUtc'] as String),
        issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String),
        dueAtUtc: DateTime.parse(json['dueAtUtc'] as String),
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        status: json['status'] as String,
        paidAtUtc: json['paidAtUtc'] == null ? null : DateTime.parse(json['paidAtUtc'] as String),
        voidedAtUtc: json['voidedAtUtc'] == null ? null : DateTime.parse(json['voidedAtUtc'] as String),
        voidReason: json['voidReason'] == null ? null : json['voidReason'] as String,
        description: json['description'] as String,
        grossAmountMinorUnits: (json['grossAmountMinorUnits'] as num).toInt(),
        discountMinorUnits: (json['discountMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'invoiceId': invoiceId,
        'organizationId': organizationId,
        'number': number,
        'kind': kind,
        'periodStartUtc': periodStartUtc.toIso8601String(),
        'periodEndUtc': periodEndUtc.toIso8601String(),
        'issuedAtUtc': issuedAtUtc.toIso8601String(),
        'dueAtUtc': dueAtUtc.toIso8601String(),
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'status': status,
        'paidAtUtc': paidAtUtc?.toIso8601String(),
        'voidedAtUtc': voidedAtUtc?.toIso8601String(),
        'voidReason': voidReason,
        'description': description,
        'grossAmountMinorUnits': grossAmountMinorUnits,
        'discountMinorUnits': discountMinorUnits,
      };
}

/// Контракт: Platform/Billing/InvoiceListItemDto.cs
class InvoiceListItemDto {
  const InvoiceListItemDto({
    required this.invoiceId,
    required this.organizationId,
    required this.organizationName,
    required this.organizationSlug,
    required this.number,
    required this.kind,
    required this.issuedAtUtc,
    required this.dueAtUtc,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.status,
  });

  final String invoiceId;
  final String organizationId;
  final String organizationName;
  final String organizationSlug;
  final int number;
  final String kind;
  final DateTime issuedAtUtc;
  final DateTime dueAtUtc;
  final int amountMinorUnits;
  final String currencyCode;
  final String status;

  factory InvoiceListItemDto.fromJson(Map<String, dynamic> json) => InvoiceListItemDto(
        invoiceId: json['invoiceId'] as String,
        organizationId: json['organizationId'] as String,
        organizationName: json['organizationName'] as String,
        organizationSlug: json['organizationSlug'] as String,
        number: (json['number'] as num).toInt(),
        kind: json['kind'] as String,
        issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String),
        dueAtUtc: DateTime.parse(json['dueAtUtc'] as String),
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        status: json['status'] as String,
      );

  Map<String, dynamic> toJson() => {
        'invoiceId': invoiceId,
        'organizationId': organizationId,
        'organizationName': organizationName,
        'organizationSlug': organizationSlug,
        'number': number,
        'kind': kind,
        'issuedAtUtc': issuedAtUtc.toIso8601String(),
        'dueAtUtc': dueAtUtc.toIso8601String(),
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'status': status,
      };
}

/// Состояние одного задания. Kind/JobName едут кодом: клиент никогда не рендерит серверную
/// строку как пользовательский текст — у каждого имени есть перевод в каталоге.
///
/// Контракт: Platform/Health/PlatformHealthContracts.cs
class JobHealthDto {
  const JobHealthDto({
    required this.jobName,
    this.lastRunAtUtc,
    this.lastSuccessAtUtc,
    this.lastOutcome,
    required this.lastItemsProcessed,
    this.lastError,
    required this.consecutiveFailures,
  });

  final String jobName;
  final DateTime? lastRunAtUtc;
  final DateTime? lastSuccessAtUtc;
  final String? lastOutcome;
  final int lastItemsProcessed;
  final String? lastError;
  final int consecutiveFailures;

  factory JobHealthDto.fromJson(Map<String, dynamic> json) => JobHealthDto(
        jobName: json['jobName'] as String,
        lastRunAtUtc: json['lastRunAtUtc'] == null ? null : DateTime.parse(json['lastRunAtUtc'] as String),
        lastSuccessAtUtc: json['lastSuccessAtUtc'] == null ? null : DateTime.parse(json['lastSuccessAtUtc'] as String),
        lastOutcome: json['lastOutcome'] == null ? null : json['lastOutcome'] as String,
        lastItemsProcessed: (json['lastItemsProcessed'] as num).toInt(),
        lastError: json['lastError'] == null ? null : json['lastError'] as String,
        consecutiveFailures: (json['consecutiveFailures'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'jobName': jobName,
        'lastRunAtUtc': lastRunAtUtc?.toIso8601String(),
        'lastSuccessAtUtc': lastSuccessAtUtc?.toIso8601String(),
        'lastOutcome': lastOutcome,
        'lastItemsProcessed': lastItemsProcessed,
        'lastError': lastError,
        'consecutiveFailures': consecutiveFailures,
      };
}

/// Контракт: Shell/LauncherAppDto.cs
class LauncherAppDto {
  const LauncherAppDto({
    required this.appId,
    required this.displayName,
    required this.category,
    this.iconUri,
    required this.isAvailable,
    this.minAge,
  });

  final String appId;
  final String displayName;
  final String category;
  final String? iconUri;
  final bool isAvailable;

  /// Возрастная отметка игры (0, 12, 16, 18). Проверить её не на чем — у игрока нет даты рождения.
  final int? minAge;

  factory LauncherAppDto.fromJson(Map<String, dynamic> json) => LauncherAppDto(
        appId: json['appId'] as String,
        displayName: json['displayName'] as String,
        category: json['category'] as String,
        iconUri: json['iconUri'] == null ? null : json['iconUri'] as String,
        isAvailable: json['isAvailable'] as bool,
        minAge: json['minAge'] == null ? null : (json['minAge'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'appId': appId,
        'displayName': displayName,
        'category': category,
        'iconUri': iconUri,
        'isAvailable': isAvailable,
        'minAge': minAge,
      };
}

/// Контракт: Billing/LedgerEntryDto.cs
class LedgerEntryDto {
  const LedgerEntryDto({
    required this.ledgerEntryId,
    required this.organizationId,
    required this.branchId,
    required this.playerAccountId,
    this.sessionId,
    this.playerPackageId,
    required this.entryType,
    required this.accountType,
    required this.amount,
    required this.quantitySeconds,
    required this.description,
    required this.reason,
    this.reversesLedgerEntryId,
    required this.createdByStaffUserId,
    required this.createdAtUtc,
  });

  final String ledgerEntryId;
  final String organizationId;
  final String branchId;
  final String playerAccountId;
  final String? sessionId;
  final String? playerPackageId;
  final String entryType;
  final String accountType;
  final MoneyDto amount;
  final int quantitySeconds;
  final String description;
  final String reason;
  final String? reversesLedgerEntryId;
  final String createdByStaffUserId;
  final DateTime createdAtUtc;

  factory LedgerEntryDto.fromJson(Map<String, dynamic> json) => LedgerEntryDto(
        ledgerEntryId: json['ledgerEntryId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        playerAccountId: json['playerAccountId'] as String,
        sessionId: json['sessionId'] == null ? null : json['sessionId'] as String,
        playerPackageId: json['playerPackageId'] == null ? null : json['playerPackageId'] as String,
        entryType: json['entryType'] as String,
        accountType: json['accountType'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        quantitySeconds: (json['quantitySeconds'] as num).toInt(),
        description: json['description'] as String,
        reason: json['reason'] as String,
        reversesLedgerEntryId: json['reversesLedgerEntryId'] == null ? null : json['reversesLedgerEntryId'] as String,
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'ledgerEntryId': ledgerEntryId,
        'organizationId': organizationId,
        'branchId': branchId,
        'playerAccountId': playerAccountId,
        'sessionId': sessionId,
        'playerPackageId': playerPackageId,
        'entryType': entryType,
        'accountType': accountType,
        'amount': amount.toJson(),
        'quantitySeconds': quantitySeconds,
        'description': description,
        'reason': reason,
        'reversesLedgerEntryId': reversesLedgerEntryId,
        'createdByStaffUserId': createdByStaffUserId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Updates/LocalUpdateCoordinationMessage.cs
class LocalUpdateCoordinationRequest {
  const LocalUpdateCoordinationRequest({
    required this.secret,
    required this.operation,
    this.updateRolloutId,
    this.updatePackageId,
  });

  final String secret;
  final String operation;
  final String? updateRolloutId;
  final String? updatePackageId;

  factory LocalUpdateCoordinationRequest.fromJson(Map<String, dynamic> json) => LocalUpdateCoordinationRequest(
        secret: json['secret'] as String,
        operation: json['operation'] as String,
        updateRolloutId: json['updateRolloutId'] == null ? null : json['updateRolloutId'] as String,
        updatePackageId: json['updatePackageId'] == null ? null : json['updatePackageId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'secret': secret,
        'operation': operation,
        'updateRolloutId': updateRolloutId,
        'updatePackageId': updatePackageId,
      };
}

/// Контракт: Updates/LocalUpdateCoordinationMessage.cs
class LocalUpdateCoordinationResponse {
  const LocalUpdateCoordinationResponse({
    required this.status,
    required this.message,
  });

  final String status;
  final String message;

  factory LocalUpdateCoordinationResponse.fromJson(Map<String, dynamic> json) => LocalUpdateCoordinationResponse(
        status: json['status'] as String,
        message: json['message'] as String,
      );

  Map<String, dynamic> toJson() => {
        'status': status,
        'message': message,
      };
}

/// Контракт: Loyalty/LoyaltySettingsDto.cs
class LoyaltySettingsDto {
  const LoyaltySettingsDto({
    required this.topUpEnabled,
    required this.topUpPercentBasisPoints,
    required this.shopEnabled,
    required this.shopPercentBasisPoints,
    required this.sessionEnabled,
    required this.sessionPercentBasisPoints,
    required this.cashbackCapMinorUnits,
    required this.minimumSourceMinorUnits,
  });

  final bool topUpEnabled;
  final int topUpPercentBasisPoints;
  final bool shopEnabled;
  final int shopPercentBasisPoints;
  final bool sessionEnabled;
  final int sessionPercentBasisPoints;
  final int cashbackCapMinorUnits;
  final int minimumSourceMinorUnits;

  factory LoyaltySettingsDto.fromJson(Map<String, dynamic> json) => LoyaltySettingsDto(
        topUpEnabled: json['topUpEnabled'] as bool,
        topUpPercentBasisPoints: (json['topUpPercentBasisPoints'] as num).toInt(),
        shopEnabled: json['shopEnabled'] as bool,
        shopPercentBasisPoints: (json['shopPercentBasisPoints'] as num).toInt(),
        sessionEnabled: json['sessionEnabled'] as bool,
        sessionPercentBasisPoints: (json['sessionPercentBasisPoints'] as num).toInt(),
        cashbackCapMinorUnits: (json['cashbackCapMinorUnits'] as num).toInt(),
        minimumSourceMinorUnits: (json['minimumSourceMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'topUpEnabled': topUpEnabled,
        'topUpPercentBasisPoints': topUpPercentBasisPoints,
        'shopEnabled': shopEnabled,
        'shopPercentBasisPoints': shopPercentBasisPoints,
        'sessionEnabled': sessionEnabled,
        'sessionPercentBasisPoints': sessionPercentBasisPoints,
        'cashbackCapMinorUnits': cashbackCapMinorUnits,
        'minimumSourceMinorUnits': minimumSourceMinorUnits,
      };
}

/// Контракт: Billing/ManualLedgerCorrectionRequest.cs
class ManualLedgerCorrectionRequest {
  const ManualLedgerCorrectionRequest({
    required this.organizationId,
    required this.accountType,
    required this.amount,
    required this.quantitySeconds,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String accountType;
  final MoneyDto amount;
  final int quantitySeconds;
  final String reason;
  final String idempotencyKey;

  factory ManualLedgerCorrectionRequest.fromJson(Map<String, dynamic> json) => ManualLedgerCorrectionRequest(
        organizationId: json['organizationId'] as String,
        accountType: json['accountType'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        quantitySeconds: (json['quantitySeconds'] as num).toInt(),
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'accountType': accountType,
        'amount': amount.toJson(),
        'quantitySeconds': quantitySeconds,
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Payments/ManualPaymentRequest.cs
class ManualPaymentRequest {
  const ManualPaymentRequest({
    required this.organizationId,
    required this.paymentMethod,
    required this.amount,
    required this.note,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String paymentMethod;
  final MoneyDto amount;
  final String note;
  final String idempotencyKey;

  factory ManualPaymentRequest.fromJson(Map<String, dynamic> json) => ManualPaymentRequest(
        organizationId: json['organizationId'] as String,
        paymentMethod: json['paymentMethod'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        note: json['note'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'paymentMethod': paymentMethod,
        'amount': amount.toJson(),
        'note': note,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Platform/Billing/MarkInvoicePaidRequest.cs
class MarkInvoicePaidRequest {
  const MarkInvoicePaidRequest({
    this.reference,
  });

  final String? reference;

  factory MarkInvoicePaidRequest.fromJson(Map<String, dynamic> json) => MarkInvoicePaidRequest(
        reference: json['reference'] == null ? null : json['reference'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reference': reference,
      };
}

/// «Он не приехал» — сказанное человеком за стойкой, а не выведенное таймером.
/// Автоматика ждёт столько, сколько велел филиал, и разбирает только брони с замороженными
/// деньгами. Администратор видит пустое место раньше и знает про бронь без предоплаты то, чего
/// не знает ни один таймер, — поэтому отметить неявку он может сам.
/// <param name="ExpectedVersion">
/// Версия брони, которую видел администратор. Пусто — не спорить о версиях: повторный клик по
/// уже отмеченной неявке не должен выглядеть конфликтом.
/// </param>
///
/// Контракт: Reservations/MarkReservationNoShowRequest.cs
class MarkReservationNoShowRequest {
  const MarkReservationNoShowRequest({
    required this.organizationId,
    this.expectedVersion,
  });

  final String organizationId;
  final int? expectedVersion;

  factory MarkReservationNoShowRequest.fromJson(Map<String, dynamic> json) => MarkReservationNoShowRequest(
        organizationId: json['organizationId'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'expectedVersion': expectedVersion,
      };
}

/// Человек и его клубы одним ответом. Приложение открывается на этом: сначала «кто я», потом
/// «где у меня что». Общей суммы денег здесь нет и не будет — у каждого клуба своя касса, и
/// складывать остатки разных клубов значит показать число, которое ниоткуда нельзя потратить.
///
/// Контракт: Players/MeDto.cs
class MeDto {
  const MeDto({
    required this.person,
    required this.clubs,
  });

  final MePersonDto person;
  final List<MyClubDto> clubs;

  factory MeDto.fromJson(Map<String, dynamic> json) => MeDto(
        person: MePersonDto.fromJson(json['person'] as Map<String, dynamic>),
        clubs: (json['clubs'] as List<dynamic>).map((item) => MyClubDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'person': person.toJson(),
        'clubs': clubs.map((item) => item.toJson()).toList(),
      };
}

/// Личность: то, что принадлежит человеку, а не клубу. PIN сюда не попадает никогда — только
/// признак, задан он или ещё нет.
///
/// Контракт: Players/MeDto.cs
class MePersonDto {
  const MePersonDto({
    required this.platformPersonId,
    required this.phoneNumber,
    required this.displayName,
    this.preferredLocale,
    required this.phoneVerified,
    required this.pinSet,
    required this.networkBanned,
    this.networkBanReason,
  });

  final String platformPersonId;
  final String phoneNumber;
  final String displayName;
  final String? preferredLocale;
  final bool phoneVerified;
  final bool pinSet;
  final bool networkBanned;

  /// За что закрыт вход. Запрет, о котором человек не может узнать причину, читается как поломка
  /// приложения — и он идёт спорить к стойке, которая его не ставила.
  final String? networkBanReason;

  factory MePersonDto.fromJson(Map<String, dynamic> json) => MePersonDto(
        platformPersonId: json['platformPersonId'] as String,
        phoneNumber: json['phoneNumber'] as String,
        displayName: json['displayName'] as String,
        preferredLocale: json['preferredLocale'] == null ? null : json['preferredLocale'] as String,
        phoneVerified: json['phoneVerified'] as bool,
        pinSet: json['pinSet'] as bool,
        networkBanned: json['networkBanned'] as bool,
        networkBanReason: json['networkBanReason'] == null ? null : json['networkBanReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'platformPersonId': platformPersonId,
        'phoneNumber': phoneNumber,
        'displayName': displayName,
        'preferredLocale': preferredLocale,
        'phoneVerified': phoneVerified,
        'pinSet': pinSet,
        'networkBanned': networkBanned,
        'networkBanReason': networkBanReason,
      };
}

/// Контракт: Ads/AdContracts.cs
class ModerateAdCreativeRequest {
  const ModerateAdCreativeRequest({
    required this.approve,
    this.reason,
    required this.confirmedAllowed,
  });

  final bool approve;

  /// Причина отказа — рекламодателю через менеджера платформы. Обязательна при отказе.
  final String? reason;

  /// Модератор подтверждает то, чего код не проверит: это не другой клуб, не алкоголь, не табак и
  /// не ставки. Без отметки одобрить нельзя.
  final bool confirmedAllowed;

  factory ModerateAdCreativeRequest.fromJson(Map<String, dynamic> json) => ModerateAdCreativeRequest(
        approve: json['approve'] as bool,
        reason: json['reason'] == null ? null : json['reason'] as String,
        confirmedAllowed: json['confirmedAllowed'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'approve': approve,
        'reason': reason,
        'confirmedAllowed': confirmedAllowed,
      };
}

/// Approve or reject a pending money action; the optional note is recorded on the request.
///
/// Контракт: Billing/MoneyActionContracts.cs
class MoneyActionDecisionRequest {
  const MoneyActionDecisionRequest({
    this.decisionReason,
  });

  final String? decisionReason;

  factory MoneyActionDecisionRequest.fromJson(Map<String, dynamic> json) => MoneyActionDecisionRequest(
        decisionReason: json['decisionReason'] == null ? null : json['decisionReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'decisionReason': decisionReason,
      };
}

/// A pending money action for the Manager Review screen (§5.5).
///
/// Контракт: Billing/MoneyActionContracts.cs
class MoneyActionRequestDto {
  const MoneyActionRequestDto({
    required this.moneyActionRequestId,
    required this.organizationId,
    required this.branchId,
    required this.shiftId,
    required this.actionType,
    required this.requestedByStaffUserId,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.reason,
    required this.state,
    required this.createdAtUtc,
    required this.expiresAtUtc,
  });

  final String moneyActionRequestId;
  final String organizationId;
  final String branchId;
  final String shiftId;
  final String actionType;
  final String requestedByStaffUserId;
  final int amountMinorUnits;
  final String currencyCode;
  final String reason;
  final String state;
  final DateTime createdAtUtc;
  final DateTime expiresAtUtc;

  factory MoneyActionRequestDto.fromJson(Map<String, dynamic> json) => MoneyActionRequestDto(
        moneyActionRequestId: json['moneyActionRequestId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        shiftId: json['shiftId'] as String,
        actionType: json['actionType'] as String,
        requestedByStaffUserId: json['requestedByStaffUserId'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        reason: json['reason'] as String,
        state: json['state'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'moneyActionRequestId': moneyActionRequestId,
        'organizationId': organizationId,
        'branchId': branchId,
        'shiftId': shiftId,
        'actionType': actionType,
        'requestedByStaffUserId': requestedByStaffUserId,
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'reason': reason,
        'state': state,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
      };
}

/// The pending-approvals feed.
///
/// Контракт: Billing/MoneyActionContracts.cs
class MoneyActionRequestListResponse {
  const MoneyActionRequestListResponse({
    required this.requests,
  });

  final List<MoneyActionRequestDto> requests;

  factory MoneyActionRequestListResponse.fromJson(Map<String, dynamic> json) => MoneyActionRequestListResponse(
        requests: (json['requests'] as List<dynamic>).map((item) => MoneyActionRequestDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'requests': requests.map((item) => item.toJson()).toList(),
      };
}

/// Submit a high-risk money action through the anti-fraud control layer (§5.2). The guard decides
/// whether it executes now, is held for approval, or is refused on a cap breach. `ActionType` is
/// `refund` or `manual_correction`; a debt-reducing correction is classified as a write-off
/// server-side. `SignedAmountMinorUnits` is signed (corrections may be ±); refunds use its magnitude.
///
/// Контракт: Billing/MoneyActionContracts.cs
class MoneyActionSubmitRequest {
  const MoneyActionSubmitRequest({
    required this.organizationId,
    required this.actionType,
    required this.playerAccountId,
    this.ledgerEntryId,
    required this.accountType,
    required this.signedAmountMinorUnits,
    required this.currencyCode,
    required this.quantitySeconds,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String actionType;
  final String playerAccountId;
  final String? ledgerEntryId;
  final String accountType;
  final int signedAmountMinorUnits;
  final String currencyCode;
  final int quantitySeconds;
  final String reason;
  final String idempotencyKey;

  factory MoneyActionSubmitRequest.fromJson(Map<String, dynamic> json) => MoneyActionSubmitRequest(
        organizationId: json['organizationId'] as String,
        actionType: json['actionType'] as String,
        playerAccountId: json['playerAccountId'] as String,
        ledgerEntryId: json['ledgerEntryId'] == null ? null : json['ledgerEntryId'] as String,
        accountType: json['accountType'] as String,
        signedAmountMinorUnits: (json['signedAmountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        quantitySeconds: (json['quantitySeconds'] as num).toInt(),
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'actionType': actionType,
        'playerAccountId': playerAccountId,
        'ledgerEntryId': ledgerEntryId,
        'accountType': accountType,
        'signedAmountMinorUnits': signedAmountMinorUnits,
        'currencyCode': currencyCode,
        'quantitySeconds': quantitySeconds,
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// Outcome of a submitted money action: `executed` / `pending_approval` / `rejected`.
///
/// Контракт: Billing/MoneyActionContracts.cs
class MoneyActionSubmitResponse {
  const MoneyActionSubmitResponse({
    required this.outcome,
    this.resultingLedgerEntryId,
    this.moneyActionRequestId,
  });

  final String outcome;
  final String? resultingLedgerEntryId;
  final String? moneyActionRequestId;

  factory MoneyActionSubmitResponse.fromJson(Map<String, dynamic> json) => MoneyActionSubmitResponse(
        outcome: json['outcome'] as String,
        resultingLedgerEntryId: json['resultingLedgerEntryId'] == null ? null : json['resultingLedgerEntryId'] as String,
        moneyActionRequestId: json['moneyActionRequestId'] == null ? null : json['moneyActionRequestId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'outcome': outcome,
        'resultingLedgerEntryId': resultingLedgerEntryId,
        'moneyActionRequestId': moneyActionRequestId,
      };
}

/// Контракт: Billing/MoneyDto.cs
class MoneyDto {
  const MoneyDto({
    required this.currencyCode,
    required this.minorUnits,
  });

  final String currencyCode;
  final int minorUnits;

  factory MoneyDto.fromJson(Map<String, dynamic> json) => MoneyDto(
        currencyCode: json['currencyCode'] as String,
        minorUnits: (json['minorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'currencyCode': currencyCode,
        'minorUnits': minorUnits,
      };
}

/// Перенос собственной брони игроком: новое время и, если нужно, другое место.
/// Длительности здесь нет намеренно. «Перенести» — это то же самое на другое время; изменить
/// длину — другое решение с другой ценой, и прятать его в ту же кнопку значит однажды удивить
/// человека суммой.
///
/// Контракт: Reservations/MovePlayerReservationRequest.cs
class MovePlayerReservationRequest {
  const MovePlayerReservationRequest({
    required this.startsAtUtc,
    this.seatId,
    this.expectedVersion,
  });

  final DateTime startsAtUtc;
  final String? seatId;
  final int? expectedVersion;

  factory MovePlayerReservationRequest.fromJson(Map<String, dynamic> json) => MovePlayerReservationRequest(
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'seatId': seatId,
        'expectedVersion': expectedVersion,
      };
}

/// Один клуб глазами игрока: сколько можно потратить, сколько придержано под брони, сколько
/// он должен и сколько раз приходил.
/// Клуба нет в списке — значит человек в нём ещё ничего не делал, и счёта там пока нет. Это
/// нормальное состояние, а не сбой: показывать его ошибкой значит пугать на ровном месте.
///
/// Контракт: Players/MeDto.cs
class MyClubDto {
  const MyClubDto({
    required this.organizationId,
    required this.organizationName,
    required this.playerAccountId,
    required this.homeBranchId,
    required this.currencyCode,
    required this.walletBalanceMinorUnits,
    required this.heldMinorUnits,
    required this.debtMinorUnits,
    required this.visitCount,
  });

  final String organizationId;
  final String organizationName;
  final String playerAccountId;
  final String homeBranchId;
  final String currencyCode;
  final int walletBalanceMinorUnits;
  final int heldMinorUnits;
  final int debtMinorUnits;
  final int visitCount;

  factory MyClubDto.fromJson(Map<String, dynamic> json) => MyClubDto(
        organizationId: json['organizationId'] as String,
        organizationName: json['organizationName'] as String,
        playerAccountId: json['playerAccountId'] as String,
        homeBranchId: json['homeBranchId'] as String,
        currencyCode: json['currencyCode'] as String,
        walletBalanceMinorUnits: (json['walletBalanceMinorUnits'] as num).toInt(),
        heldMinorUnits: (json['heldMinorUnits'] as num).toInt(),
        debtMinorUnits: (json['debtMinorUnits'] as num).toInt(),
        visitCount: (json['visitCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'organizationName': organizationName,
        'playerAccountId': playerAccountId,
        'homeBranchId': homeBranchId,
        'currencyCode': currencyCode,
        'walletBalanceMinorUnits': walletBalanceMinorUnits,
        'heldMinorUnits': heldMinorUnits,
        'debtMinorUnits': debtMinorUnits,
        'visitCount': visitCount,
      };
}

/// Человек сети глазами платформы: ровно столько, сколько нужно, чтобы решить вопрос о запрете.
/// Ни клубов, ни денег, ни визитов здесь нет — это клубные сведения, и панель платформы не место,
/// где их собирают в одну карточку.
///
/// Контракт: Platform/People/NetworkPeopleContracts.cs
class NetworkPersonDto {
  const NetworkPersonDto({
    required this.platformPersonId,
    required this.phoneNumber,
    required this.displayName,
    required this.registeredAtUtc,
    this.networkBanAtUtc,
    this.networkBanReason,
  });

  final String platformPersonId;
  final String phoneNumber;
  final String displayName;
  final DateTime registeredAtUtc;
  final DateTime? networkBanAtUtc;
  final String? networkBanReason;

  factory NetworkPersonDto.fromJson(Map<String, dynamic> json) => NetworkPersonDto(
        platformPersonId: json['platformPersonId'] as String,
        phoneNumber: json['phoneNumber'] as String,
        displayName: json['displayName'] as String,
        registeredAtUtc: DateTime.parse(json['registeredAtUtc'] as String),
        networkBanAtUtc: json['networkBanAtUtc'] == null ? null : DateTime.parse(json['networkBanAtUtc'] as String),
        networkBanReason: json['networkBanReason'] == null ? null : json['networkBanReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'platformPersonId': platformPersonId,
        'phoneNumber': phoneNumber,
        'displayName': displayName,
        'registeredAtUtc': registeredAtUtc.toIso8601String(),
        'networkBanAtUtc': networkBanAtUtc?.toIso8601String(),
        'networkBanReason': networkBanReason,
      };
}

/// Спрос по точному номеру. Поиска по части номера нет намеренно.
///
/// Контракт: Platform/People/NetworkPeopleContracts.cs
class NetworkPersonLookupRequest {
  const NetworkPersonLookupRequest({
    required this.phoneNumber,
  });

  final String phoneNumber;

  factory NetworkPersonLookupRequest.fromJson(Map<String, dynamic> json) => NetworkPersonLookupRequest(
        phoneNumber: json['phoneNumber'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
      };
}

/// Контракт: News/NewsItemDto.cs
class NewsItemDto {
  const NewsItemDto({
    required this.id,
    this.branchId,
    required this.title,
    required this.body,
    this.imageUrl,
    required this.isPublished,
    this.publishAtUtc,
    this.expiresAtUtc,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    this.showOnPcs,
  });

  final String id;
  final String? branchId;
  final String title;
  final String body;
  final String? imageUrl;
  final bool isPublished;
  final DateTime? publishAtUtc;
  final DateTime? expiresAtUtc;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;

  /// Новость крутится и на экране свободного ПК (витрина), а не только в приложении.
  final bool? showOnPcs;

  factory NewsItemDto.fromJson(Map<String, dynamic> json) => NewsItemDto(
        id: json['id'] as String,
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
        isPublished: json['isPublished'] as bool,
        publishAtUtc: json['publishAtUtc'] == null ? null : DateTime.parse(json['publishAtUtc'] as String),
        expiresAtUtc: json['expiresAtUtc'] == null ? null : DateTime.parse(json['expiresAtUtc'] as String),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
        showOnPcs: json['showOnPcs'] == null ? null : json['showOnPcs'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'branchId': branchId,
        'title': title,
        'body': body,
        'imageUrl': imageUrl,
        'isPublished': isPublished,
        'publishAtUtc': publishAtUtc?.toIso8601String(),
        'expiresAtUtc': expiresAtUtc?.toIso8601String(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
        'showOnPcs': showOnPcs,
      };
}

/// The outcome of a user-waiting send (OTP / password reset) after its first dispatch attempt.
///
/// Контракт: Notifications/NotificationContracts.cs
class NotificationDeliveryResult {
  const NotificationDeliveryResult({
    required this.handle,
    required this.delivered,
    this.error,
  });

  final NotificationHandle handle;
  final bool delivered;
  final String? error;

  factory NotificationDeliveryResult.fromJson(Map<String, dynamic> json) => NotificationDeliveryResult(
        handle: NotificationHandle.fromJson(json['handle'] as Map<String, dynamic>),
        delivered: json['delivered'] as bool,
        error: json['error'] == null ? null : json['error'] as String,
      );

  Map<String, dynamic> toJson() => {
        'handle': handle.toJson(),
        'delivered': delivered,
        'error': error,
      };
}

/// The result of enqueuing a notification: the outbox row id(s) and whether new rows were created
/// (`false` when a duplicate NotificationRequest.IdempotencyKey collapsed the send).
///
/// Контракт: Notifications/NotificationContracts.cs
class NotificationHandle {
  const NotificationHandle({
    required this.outboxIds,
    required this.created,
  });

  final List<String> outboxIds;
  final bool created;

  factory NotificationHandle.fromJson(Map<String, dynamic> json) => NotificationHandle(
        outboxIds: (json['outboxIds'] as List<dynamic>).map((item) => item as String).toList(),
        created: json['created'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'outboxIds': outboxIds.map((item) => item).toList(),
        'created': created,
      };
}

/// A resolved delivery target. Locale is BCP-47-ish (ru/en/tg) resolved upstream; address fields are
/// channel-specific. Staff/player ids provide audit linkage and a future in-app target.
///
/// Контракт: Notifications/NotificationContracts.cs
class NotificationRecipient {
  const NotificationRecipient({
    required this.locale,
    this.emailAddress,
    this.phoneNumber,
    this.staffUserId,
    this.playerAccountId,
  });

  final String locale;
  final String? emailAddress;
  final String? phoneNumber;
  final String? staffUserId;
  final String? playerAccountId;

  factory NotificationRecipient.fromJson(Map<String, dynamic> json) => NotificationRecipient(
        locale: json['locale'] as String,
        emailAddress: json['emailAddress'] == null ? null : json['emailAddress'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        staffUserId: json['staffUserId'] == null ? null : json['staffUserId'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'locale': locale,
        'emailAddress': emailAddress,
        'phoneNumber': phoneNumber,
        'staffUserId': staffUserId,
        'playerAccountId': playerAccountId,
      };
}

/// Контракт: Shifts/OpenShiftRequest.cs
class OpenShiftRequest {
  const OpenShiftRequest({
    required this.organizationId,
    required this.startingCash,
    required this.openingNote,
    required this.idempotencyKey,
  });

  final String organizationId;
  final MoneyDto startingCash;
  final String openingNote;
  final String idempotencyKey;

  factory OpenShiftRequest.fromJson(Map<String, dynamic> json) => OpenShiftRequest(
        organizationId: json['organizationId'] as String,
        startingCash: MoneyDto.fromJson(json['startingCash'] as Map<String, dynamic>),
        openingNote: json['openingNote'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'startingCash': startingCash.toJson(),
        'openingNote': openingNote,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Reports/OperatorActionReportResultDto.cs
class OperatorActionReportResultDto {
  const OperatorActionReportResultDto({
    required this.rows,
    required this.limit,
    required this.totalActionCount,
  });

  final List<OperatorActionReportRowDto> rows;
  final int limit;
  final int totalActionCount;

  factory OperatorActionReportResultDto.fromJson(Map<String, dynamic> json) => OperatorActionReportResultDto(
        rows: (json['rows'] as List<dynamic>).map((item) => OperatorActionReportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
        totalActionCount: (json['totalActionCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'rows': rows.map((item) => item.toJson()).toList(),
        'limit': limit,
        'totalActionCount': totalActionCount,
      };
}

/// Контракт: Reports/OperatorActionReportRowDto.cs
class OperatorActionReportRowDto {
  const OperatorActionReportRowDto({
    this.actorStaffUserId,
    required this.actorDisplayName,
    required this.action,
    required this.outcome,
    required this.count,
    required this.firstAtUtc,
    required this.lastAtUtc,
  });

  final String? actorStaffUserId;
  final String actorDisplayName;
  final String action;
  final String outcome;
  final int count;
  final DateTime firstAtUtc;
  final DateTime lastAtUtc;

  factory OperatorActionReportRowDto.fromJson(Map<String, dynamic> json) => OperatorActionReportRowDto(
        actorStaffUserId: json['actorStaffUserId'] == null ? null : json['actorStaffUserId'] as String,
        actorDisplayName: json['actorDisplayName'] as String,
        action: json['action'] as String,
        outcome: json['outcome'] as String,
        count: (json['count'] as num).toInt(),
        firstAtUtc: DateTime.parse(json['firstAtUtc'] as String),
        lastAtUtc: DateTime.parse(json['lastAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'actorStaffUserId': actorStaffUserId,
        'actorDisplayName': actorDisplayName,
        'action': action,
        'outcome': outcome,
        'count': count,
        'firstAtUtc': firstAtUtc.toIso8601String(),
        'lastAtUtc': lastAtUtc.toIso8601String(),
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardAlertPressureDto {
  const OperatorDashboardAlertPressureDto({
    required this.pendingCommands,
    required this.failedCommands,
    required this.offlineDevices,
    required this.endingSessions,
    required this.totalAlerts,
  });

  final int pendingCommands;
  final int failedCommands;
  final int offlineDevices;
  final int endingSessions;
  final int totalAlerts;

  factory OperatorDashboardAlertPressureDto.fromJson(Map<String, dynamic> json) => OperatorDashboardAlertPressureDto(
        pendingCommands: (json['pendingCommands'] as num).toInt(),
        failedCommands: (json['failedCommands'] as num).toInt(),
        offlineDevices: (json['offlineDevices'] as num).toInt(),
        endingSessions: (json['endingSessions'] as num).toInt(),
        totalAlerts: (json['totalAlerts'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'pendingCommands': pendingCommands,
        'failedCommands': failedCommands,
        'offlineDevices': offlineDevices,
        'endingSessions': endingSessions,
        'totalAlerts': totalAlerts,
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardQueueItemDto {
  const OperatorDashboardQueueItemDto({
    required this.tone,
    required this.target,
    required this.title,
    required this.detail,
    this.seatId,
    this.deviceId,
    required this.createdAtUtc,
    required this.sourceType,
  });

  final String tone;
  final String target;
  final String title;
  final String detail;
  final String? seatId;
  final String? deviceId;
  final DateTime createdAtUtc;
  final String sourceType;

  factory OperatorDashboardQueueItemDto.fromJson(Map<String, dynamic> json) => OperatorDashboardQueueItemDto(
        tone: json['tone'] as String,
        target: json['target'] as String,
        title: json['title'] as String,
        detail: json['detail'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        deviceId: json['deviceId'] == null ? null : json['deviceId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        sourceType: json['sourceType'] as String,
      );

  Map<String, dynamic> toJson() => {
        'tone': tone,
        'target': target,
        'title': title,
        'detail': detail,
        'seatId': seatId,
        'deviceId': deviceId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'sourceType': sourceType,
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardRecentPaymentDto {
  const OperatorDashboardRecentPaymentDto({
    required this.paymentId,
    this.posSaleId,
    required this.shiftId,
    required this.createdByStaffUserId,
    required this.paymentKind,
    required this.paymentMethod,
    required this.amount,
    required this.createdAtUtc,
    this.sessionId,
  });

  final String paymentId;
  final String? posSaleId;
  final String shiftId;
  final String createdByStaffUserId;
  final String paymentKind;
  final String paymentMethod;
  final MoneyDto amount;
  final DateTime createdAtUtc;
  final String? sessionId;

  factory OperatorDashboardRecentPaymentDto.fromJson(Map<String, dynamic> json) => OperatorDashboardRecentPaymentDto(
        paymentId: json['paymentId'] as String,
        posSaleId: json['posSaleId'] == null ? null : json['posSaleId'] as String,
        shiftId: json['shiftId'] as String,
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        paymentKind: json['paymentKind'] as String,
        paymentMethod: json['paymentMethod'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        sessionId: json['sessionId'] == null ? null : json['sessionId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'paymentId': paymentId,
        'posSaleId': posSaleId,
        'shiftId': shiftId,
        'createdByStaffUserId': createdByStaffUserId,
        'paymentKind': paymentKind,
        'paymentMethod': paymentMethod,
        'amount': amount.toJson(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'sessionId': sessionId,
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardReservationSummaryDto {
  const OperatorDashboardReservationSummaryDto({
    required this.activeReservations,
    required this.availableSlots,
    required this.source,
  });

  final int activeReservations;
  final int availableSlots;
  final String source;

  factory OperatorDashboardReservationSummaryDto.fromJson(Map<String, dynamic> json) => OperatorDashboardReservationSummaryDto(
        activeReservations: (json['activeReservations'] as num).toInt(),
        availableSlots: (json['availableSlots'] as num).toInt(),
        source: json['source'] as String,
      );

  Map<String, dynamic> toJson() => {
        'activeReservations': activeReservations,
        'availableSlots': availableSlots,
        'source': source,
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardRevenueSummaryDto {
  const OperatorDashboardRevenueSummaryDto({
    required this.posNetSales,
    required this.gameplayRevenue,
    required this.totalRevenue,
    required this.posCheckCount,
    required this.newPlayerCount,
  });

  final MoneyDto posNetSales;
  final MoneyDto gameplayRevenue;
  final MoneyDto totalRevenue;
  final int posCheckCount;
  final int newPlayerCount;

  factory OperatorDashboardRevenueSummaryDto.fromJson(Map<String, dynamic> json) => OperatorDashboardRevenueSummaryDto(
        posNetSales: MoneyDto.fromJson(json['posNetSales'] as Map<String, dynamic>),
        gameplayRevenue: MoneyDto.fromJson(json['gameplayRevenue'] as Map<String, dynamic>),
        totalRevenue: MoneyDto.fromJson(json['totalRevenue'] as Map<String, dynamic>),
        posCheckCount: (json['posCheckCount'] as num).toInt(),
        newPlayerCount: (json['newPlayerCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'posNetSales': posNetSales.toJson(),
        'gameplayRevenue': gameplayRevenue.toJson(),
        'totalRevenue': totalRevenue.toJson(),
        'posCheckCount': posCheckCount,
        'newPlayerCount': newPlayerCount,
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardShiftSummaryDto {
  const OperatorDashboardShiftSummaryDto({
    this.shiftId,
    required this.state,
    this.openedAtUtc,
    this.openedByStaffUserId,
    required this.expectedCash,
  });

  final String? shiftId;
  final String state;
  final DateTime? openedAtUtc;
  final String? openedByStaffUserId;
  final MoneyDto expectedCash;

  factory OperatorDashboardShiftSummaryDto.fromJson(Map<String, dynamic> json) => OperatorDashboardShiftSummaryDto(
        shiftId: json['shiftId'] == null ? null : json['shiftId'] as String,
        state: json['state'] as String,
        openedAtUtc: json['openedAtUtc'] == null ? null : DateTime.parse(json['openedAtUtc'] as String),
        openedByStaffUserId: json['openedByStaffUserId'] == null ? null : json['openedByStaffUserId'] as String,
        expectedCash: MoneyDto.fromJson(json['expectedCash'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'state': state,
        'openedAtUtc': openedAtUtc?.toIso8601String(),
        'openedByStaffUserId': openedByStaffUserId,
        'expectedCash': expectedCash.toJson(),
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardSummaryDto {
  const OperatorDashboardSummaryDto({
    required this.organizationId,
    required this.branchId,
    required this.fromUtc,
    required this.toUtc,
    required this.generatedAtUtc,
    required this.shift,
    required this.revenue,
    required this.utilization,
    required this.alertPressure,
    required this.reservations,
    required this.focusQueue,
    required this.recentPayments,
  });

  final String organizationId;
  final String branchId;
  final DateTime fromUtc;
  final DateTime toUtc;
  final DateTime generatedAtUtc;
  final OperatorDashboardShiftSummaryDto shift;
  final OperatorDashboardRevenueSummaryDto revenue;
  final OperatorDashboardUtilizationSummaryDto utilization;
  final OperatorDashboardAlertPressureDto alertPressure;
  final OperatorDashboardReservationSummaryDto reservations;
  final List<OperatorDashboardQueueItemDto> focusQueue;
  final List<OperatorDashboardRecentPaymentDto> recentPayments;

  factory OperatorDashboardSummaryDto.fromJson(Map<String, dynamic> json) => OperatorDashboardSummaryDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        fromUtc: DateTime.parse(json['fromUtc'] as String),
        toUtc: DateTime.parse(json['toUtc'] as String),
        generatedAtUtc: DateTime.parse(json['generatedAtUtc'] as String),
        shift: OperatorDashboardShiftSummaryDto.fromJson(json['shift'] as Map<String, dynamic>),
        revenue: OperatorDashboardRevenueSummaryDto.fromJson(json['revenue'] as Map<String, dynamic>),
        utilization: OperatorDashboardUtilizationSummaryDto.fromJson(json['utilization'] as Map<String, dynamic>),
        alertPressure: OperatorDashboardAlertPressureDto.fromJson(json['alertPressure'] as Map<String, dynamic>),
        reservations: OperatorDashboardReservationSummaryDto.fromJson(json['reservations'] as Map<String, dynamic>),
        focusQueue: (json['focusQueue'] as List<dynamic>).map((item) => OperatorDashboardQueueItemDto.fromJson(item as Map<String, dynamic>)).toList(),
        recentPayments: (json['recentPayments'] as List<dynamic>).map((item) => OperatorDashboardRecentPaymentDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'fromUtc': fromUtc.toIso8601String(),
        'toUtc': toUtc.toIso8601String(),
        'generatedAtUtc': generatedAtUtc.toIso8601String(),
        'shift': shift.toJson(),
        'revenue': revenue.toJson(),
        'utilization': utilization.toJson(),
        'alertPressure': alertPressure.toJson(),
        'reservations': reservations.toJson(),
        'focusQueue': focusQueue.map((item) => item.toJson()).toList(),
        'recentPayments': recentPayments.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Dashboard/OperatorDashboardSummaryDto.cs
class OperatorDashboardUtilizationSummaryDto {
  const OperatorDashboardUtilizationSummaryDto({
    required this.totalSeats,
    required this.activeSessions,
    required this.endingSessions,
    required this.onlineDevices,
    required this.offlineDevices,
    required this.sessionStarts,
    required this.utilizationPercent,
  });

  final int totalSeats;
  final int activeSessions;
  final int endingSessions;
  final int onlineDevices;
  final int offlineDevices;
  final int sessionStarts;
  final int utilizationPercent;

  factory OperatorDashboardUtilizationSummaryDto.fromJson(Map<String, dynamic> json) => OperatorDashboardUtilizationSummaryDto(
        totalSeats: (json['totalSeats'] as num).toInt(),
        activeSessions: (json['activeSessions'] as num).toInt(),
        endingSessions: (json['endingSessions'] as num).toInt(),
        onlineDevices: (json['onlineDevices'] as num).toInt(),
        offlineDevices: (json['offlineDevices'] as num).toInt(),
        sessionStarts: (json['sessionStarts'] as num).toInt(),
        utilizationPercent: (json['utilizationPercent'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'totalSeats': totalSeats,
        'activeSessions': activeSessions,
        'endingSessions': endingSessions,
        'onlineDevices': onlineDevices,
        'offlineDevices': offlineDevices,
        'sessionStarts': sessionStarts,
        'utilizationPercent': utilizationPercent,
      };
}

/// Контракт: Players/OperatorTopUpIntentDto.cs
class OperatorTopUpIntentDto {
  const OperatorTopUpIntentDto({
    required this.paymentIntentId,
    required this.playerAccountId,
    required this.displayName,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.state,
    required this.method,
    required this.createdAtUtc,
    this.seatName,
  });

  final String paymentIntentId;
  final String playerAccountId;
  final String displayName;
  final int amountMinorUnits;
  final String currencyCode;
  final String state;
  final String method;
  final DateTime createdAtUtc;
  final String? seatName;

  factory OperatorTopUpIntentDto.fromJson(Map<String, dynamic> json) => OperatorTopUpIntentDto(
        paymentIntentId: json['paymentIntentId'] as String,
        playerAccountId: json['playerAccountId'] as String,
        displayName: json['displayName'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        state: json['state'] as String,
        method: json['method'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'paymentIntentId': paymentIntentId,
        'playerAccountId': playerAccountId,
        'displayName': displayName,
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'state': state,
        'method': method,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'seatName': seatName,
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminActiveShiftDto {
  const OrganizationAdminActiveShiftDto({
    required this.shiftId,
    required this.openedByStaffUserId,
    required this.openedAtUtc,
    required this.expectedCash,
    required this.isProvisional,
  });

  final String shiftId;
  final String openedByStaffUserId;
  final DateTime openedAtUtc;
  final MoneyDto expectedCash;
  final bool isProvisional;

  factory OrganizationAdminActiveShiftDto.fromJson(Map<String, dynamic> json) => OrganizationAdminActiveShiftDto(
        shiftId: json['shiftId'] as String,
        openedByStaffUserId: json['openedByStaffUserId'] as String,
        openedAtUtc: DateTime.parse(json['openedAtUtc'] as String),
        expectedCash: MoneyDto.fromJson(json['expectedCash'] as Map<String, dynamic>),
        isProvisional: json['isProvisional'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'openedByStaffUserId': openedByStaffUserId,
        'openedAtUtc': openedAtUtc.toIso8601String(),
        'expectedCash': expectedCash.toJson(),
        'isProvisional': isProvisional,
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminReportAttentionDto {
  const OrganizationAdminReportAttentionDto({
    required this.kind,
    required this.title,
    required this.detail,
    this.targetId,
    this.amount,
  });

  final String kind;
  final String title;
  final String detail;
  final String? targetId;
  final MoneyDto? amount;

  factory OrganizationAdminReportAttentionDto.fromJson(Map<String, dynamic> json) => OrganizationAdminReportAttentionDto(
        kind: json['kind'] as String,
        title: json['title'] as String,
        detail: json['detail'] as String,
        targetId: json['targetId'] == null ? null : json['targetId'] as String,
        amount: json['amount'] == null ? null : MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'kind': kind,
        'title': title,
        'detail': detail,
        'targetId': targetId,
        'amount': amount?.toJson(),
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminReportFiguresDto {
  const OrganizationAdminReportFiguresDto({
    required this.netRevenue,
    required this.gameplayRevenue,
    required this.posNetSales,
    required this.gameplaySeconds,
  });

  final MoneyDto netRevenue;
  final MoneyDto gameplayRevenue;
  final MoneyDto posNetSales;
  final int gameplaySeconds;

  factory OrganizationAdminReportFiguresDto.fromJson(Map<String, dynamic> json) => OrganizationAdminReportFiguresDto(
        netRevenue: MoneyDto.fromJson(json['netRevenue'] as Map<String, dynamic>),
        gameplayRevenue: MoneyDto.fromJson(json['gameplayRevenue'] as Map<String, dynamic>),
        posNetSales: MoneyDto.fromJson(json['posNetSales'] as Map<String, dynamic>),
        gameplaySeconds: (json['gameplaySeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'netRevenue': netRevenue.toJson(),
        'gameplayRevenue': gameplayRevenue.toJson(),
        'posNetSales': posNetSales.toJson(),
        'gameplaySeconds': gameplaySeconds,
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminReportPeriodDto {
  const OrganizationAdminReportPeriodDto({
    required this.fromDate,
    required this.toDate,
    required this.timeZone,
    required this.fromUtc,
    required this.toUtc,
  });

  final String fromDate;
  final String toDate;
  final String timeZone;
  final DateTime fromUtc;
  final DateTime toUtc;

  factory OrganizationAdminReportPeriodDto.fromJson(Map<String, dynamic> json) => OrganizationAdminReportPeriodDto(
        fromDate: json['fromDate'] as String,
        toDate: json['toDate'] as String,
        timeZone: json['timeZone'] as String,
        fromUtc: DateTime.parse(json['fromUtc'] as String),
        toUtc: DateTime.parse(json['toUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'fromDate': fromDate,
        'toDate': toDate,
        'timeZone': timeZone,
        'fromUtc': fromUtc.toIso8601String(),
        'toUtc': toUtc.toIso8601String(),
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminRevenueBreakdownDto {
  const OrganizationAdminRevenueBreakdownDto({
    required this.key,
    required this.label,
    required this.revenue,
  });

  final String key;
  final String label;
  final MoneyDto revenue;

  factory OrganizationAdminRevenueBreakdownDto.fromJson(Map<String, dynamic> json) => OrganizationAdminRevenueBreakdownDto(
        key: json['key'] as String,
        label: json['label'] as String,
        revenue: MoneyDto.fromJson(json['revenue'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'key': key,
        'label': label,
        'revenue': revenue.toJson(),
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminRevenueComparisonDto {
  const OrganizationAdminRevenueComparisonDto({
    required this.previousNetRevenue,
    required this.differenceMinorUnits,
    this.changePercent,
  });

  final MoneyDto previousNetRevenue;
  final int differenceMinorUnits;
  final double? changePercent;

  factory OrganizationAdminRevenueComparisonDto.fromJson(Map<String, dynamic> json) => OrganizationAdminRevenueComparisonDto(
        previousNetRevenue: MoneyDto.fromJson(json['previousNetRevenue'] as Map<String, dynamic>),
        differenceMinorUnits: (json['differenceMinorUnits'] as num).toInt(),
        changePercent: json['changePercent'] == null ? null : (json['changePercent'] as num).toDouble(),
      );

  Map<String, dynamic> toJson() => {
        'previousNetRevenue': previousNetRevenue.toJson(),
        'differenceMinorUnits': differenceMinorUnits,
        'changePercent': changePercent,
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminRevenueReportDto {
  const OrganizationAdminRevenueReportDto({
    required this.period,
    required this.grossRevenue,
    required this.refunds,
    required this.netRevenue,
    required this.gameplayRevenue,
    required this.gameplaySeconds,
    required this.posNetSales,
    required this.comparison,
    required this.sources,
    required this.paymentMethods,
    required this.operators,
  });

  final OrganizationAdminReportPeriodDto period;
  final MoneyDto grossRevenue;
  final MoneyDto refunds;
  final MoneyDto netRevenue;
  final MoneyDto gameplayRevenue;
  final int gameplaySeconds;
  final MoneyDto posNetSales;
  final OrganizationAdminRevenueComparisonDto comparison;
  final List<OrganizationAdminRevenueSourceDto> sources;
  final List<OrganizationAdminRevenueBreakdownDto> paymentMethods;
  final List<OrganizationAdminRevenueBreakdownDto> operators;

  factory OrganizationAdminRevenueReportDto.fromJson(Map<String, dynamic> json) => OrganizationAdminRevenueReportDto(
        period: OrganizationAdminReportPeriodDto.fromJson(json['period'] as Map<String, dynamic>),
        grossRevenue: MoneyDto.fromJson(json['grossRevenue'] as Map<String, dynamic>),
        refunds: MoneyDto.fromJson(json['refunds'] as Map<String, dynamic>),
        netRevenue: MoneyDto.fromJson(json['netRevenue'] as Map<String, dynamic>),
        gameplayRevenue: MoneyDto.fromJson(json['gameplayRevenue'] as Map<String, dynamic>),
        gameplaySeconds: (json['gameplaySeconds'] as num).toInt(),
        posNetSales: MoneyDto.fromJson(json['posNetSales'] as Map<String, dynamic>),
        comparison: OrganizationAdminRevenueComparisonDto.fromJson(json['comparison'] as Map<String, dynamic>),
        sources: (json['sources'] as List<dynamic>).map((item) => OrganizationAdminRevenueSourceDto.fromJson(item as Map<String, dynamic>)).toList(),
        paymentMethods: (json['paymentMethods'] as List<dynamic>).map((item) => OrganizationAdminRevenueBreakdownDto.fromJson(item as Map<String, dynamic>)).toList(),
        operators: (json['operators'] as List<dynamic>).map((item) => OrganizationAdminRevenueBreakdownDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'period': period.toJson(),
        'grossRevenue': grossRevenue.toJson(),
        'refunds': refunds.toJson(),
        'netRevenue': netRevenue.toJson(),
        'gameplayRevenue': gameplayRevenue.toJson(),
        'gameplaySeconds': gameplaySeconds,
        'posNetSales': posNetSales.toJson(),
        'comparison': comparison.toJson(),
        'sources': sources.map((item) => item.toJson()).toList(),
        'paymentMethods': paymentMethods.map((item) => item.toJson()).toList(),
        'operators': operators.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminRevenueSourceDto {
  const OrganizationAdminRevenueSourceDto({
    required this.source,
    required this.revenue,
  });

  final String source;
  final MoneyDto revenue;

  factory OrganizationAdminRevenueSourceDto.fromJson(Map<String, dynamic> json) => OrganizationAdminRevenueSourceDto(
        source: json['source'] as String,
        revenue: MoneyDto.fromJson(json['revenue'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'source': source,
        'revenue': revenue.toJson(),
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminRevenueTrendPointDto {
  const OrganizationAdminRevenueTrendPointDto({
    required this.date,
    required this.netRevenue,
  });

  final String date;
  final MoneyDto netRevenue;

  factory OrganizationAdminRevenueTrendPointDto.fromJson(Map<String, dynamic> json) => OrganizationAdminRevenueTrendPointDto(
        date: json['date'] as String,
        netRevenue: MoneyDto.fromJson(json['netRevenue'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'date': date,
        'netRevenue': netRevenue.toJson(),
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminShiftCashReportDto {
  const OrganizationAdminShiftCashReportDto({
    required this.period,
    required this.shifts,
    required this.cashOperations,
    required this.cashInTotal,
    required this.cashOutTotal,
    required this.netCashTotal,
  });

  final OrganizationAdminReportPeriodDto period;
  final List<ShiftReportRowDto> shifts;
  final List<CashOperationReportRowDto> cashOperations;
  final MoneyDto cashInTotal;
  final MoneyDto cashOutTotal;
  final MoneyDto netCashTotal;

  factory OrganizationAdminShiftCashReportDto.fromJson(Map<String, dynamic> json) => OrganizationAdminShiftCashReportDto(
        period: OrganizationAdminReportPeriodDto.fromJson(json['period'] as Map<String, dynamic>),
        shifts: (json['shifts'] as List<dynamic>).map((item) => ShiftReportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        cashOperations: (json['cashOperations'] as List<dynamic>).map((item) => CashOperationReportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        cashInTotal: MoneyDto.fromJson(json['cashInTotal'] as Map<String, dynamic>),
        cashOutTotal: MoneyDto.fromJson(json['cashOutTotal'] as Map<String, dynamic>),
        netCashTotal: MoneyDto.fromJson(json['netCashTotal'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'period': period.toJson(),
        'shifts': shifts.map((item) => item.toJson()).toList(),
        'cashOperations': cashOperations.map((item) => item.toJson()).toList(),
        'cashInTotal': cashInTotal.toJson(),
        'cashOutTotal': cashOutTotal.toJson(),
        'netCashTotal': netCashTotal.toJson(),
      };
}

/// Контракт: Reports/OrganizationAdminReportContracts.cs
class OrganizationAdminSummaryReportDto {
  const OrganizationAdminSummaryReportDto({
    required this.period,
    required this.attentionTotalCount,
    required this.attentionItems,
    required this.figures,
    required this.trend,
    this.activeShift,
  });

  final OrganizationAdminReportPeriodDto period;
  final int attentionTotalCount;
  final List<OrganizationAdminReportAttentionDto> attentionItems;
  final OrganizationAdminReportFiguresDto figures;
  final List<OrganizationAdminRevenueTrendPointDto> trend;
  final OrganizationAdminActiveShiftDto? activeShift;

  factory OrganizationAdminSummaryReportDto.fromJson(Map<String, dynamic> json) => OrganizationAdminSummaryReportDto(
        period: OrganizationAdminReportPeriodDto.fromJson(json['period'] as Map<String, dynamic>),
        attentionTotalCount: (json['attentionTotalCount'] as num).toInt(),
        attentionItems: (json['attentionItems'] as List<dynamic>).map((item) => OrganizationAdminReportAttentionDto.fromJson(item as Map<String, dynamic>)).toList(),
        figures: OrganizationAdminReportFiguresDto.fromJson(json['figures'] as Map<String, dynamic>),
        trend: (json['trend'] as List<dynamic>).map((item) => OrganizationAdminRevenueTrendPointDto.fromJson(item as Map<String, dynamic>)).toList(),
        activeShift: json['activeShift'] == null ? null : OrganizationAdminActiveShiftDto.fromJson(json['activeShift'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'period': period.toJson(),
        'attentionTotalCount': attentionTotalCount,
        'attentionItems': attentionItems.map((item) => item.toJson()).toList(),
        'figures': figures.toJson(),
        'trend': trend.map((item) => item.toJson()).toList(),
        'activeShift': activeShift?.toJson(),
      };
}

/// Контракт: Updates/OrganizationAdminUpdatePreferenceDto.cs
class OrganizationAdminUpdatePreferenceDto {
  const OrganizationAdminUpdatePreferenceDto({
    required this.organizationId,
    required this.branchId,
    required this.maintenanceWindowStart,
    required this.maintenanceWindowEnd,
    required this.timeZone,
  });

  final String organizationId;
  final String branchId;
  final String maintenanceWindowStart;
  final String maintenanceWindowEnd;
  final String timeZone;

  factory OrganizationAdminUpdatePreferenceDto.fromJson(Map<String, dynamic> json) => OrganizationAdminUpdatePreferenceDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        maintenanceWindowStart: json['maintenanceWindowStart'] as String,
        maintenanceWindowEnd: json['maintenanceWindowEnd'] as String,
        timeZone: json['timeZone'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'maintenanceWindowStart': maintenanceWindowStart,
        'maintenanceWindowEnd': maintenanceWindowEnd,
        'timeZone': timeZone,
      };
}

/// Compact arrears summary for the club's own admin banner: enough to say what is owed and
/// how late it is, without pulling the whole invoice list on every screen load.
///
/// Контракт: Platform/Billing/OrganizationBillingStatusDto.cs
class OrganizationBillingStatusDto {
  const OrganizationBillingStatusDto({
    required this.inArrears,
    required this.outstandingMinorUnits,
    required this.currencyCode,
    this.oldestOverdueInvoiceNumber,
    required this.daysOverdue,
    this.graceUntilUtc,
  });

  final bool inArrears;
  final int outstandingMinorUnits;
  final String currencyCode;
  final int? oldestOverdueInvoiceNumber;
  final int daysOverdue;
  final DateTime? graceUntilUtc;

  factory OrganizationBillingStatusDto.fromJson(Map<String, dynamic> json) => OrganizationBillingStatusDto(
        inArrears: json['inArrears'] as bool,
        outstandingMinorUnits: (json['outstandingMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        oldestOverdueInvoiceNumber: json['oldestOverdueInvoiceNumber'] == null ? null : (json['oldestOverdueInvoiceNumber'] as num).toInt(),
        daysOverdue: (json['daysOverdue'] as num).toInt(),
        graceUntilUtc: json['graceUntilUtc'] == null ? null : DateTime.parse(json['graceUntilUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'inArrears': inArrears,
        'outstandingMinorUnits': outstandingMinorUnits,
        'currencyCode': currencyCode,
        'oldestOverdueInvoiceNumber': oldestOverdueInvoiceNumber,
        'daysOverdue': daysOverdue,
        'graceUntilUtc': graceUntilUtc?.toIso8601String(),
      };
}

/// Контракт: Platform/Organizations/OrganizationBranchDto.cs
class OrganizationBranchDto {
  const OrganizationBranchDto({
    required this.branchId,
    required this.slug,
    required this.name,
    required this.city,
    required this.createdAtUtc,
  });

  final String branchId;
  final String slug;
  final String name;
  final String city;
  final DateTime createdAtUtc;

  factory OrganizationBranchDto.fromJson(Map<String, dynamic> json) => OrganizationBranchDto(
        branchId: json['branchId'] as String,
        slug: json['slug'] as String,
        name: json['name'] as String,
        city: json['city'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'slug': slug,
        'name': name,
        'city': city,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Клуб, каким его видит мастер установки: как называется и как выглядит.
///
/// Контракт: Branding/OrganizationBrandingDto.cs
class OrganizationBrandingDto {
  const OrganizationBrandingDto({
    required this.organizationId,
    required this.name,
    this.logoUrl,
    this.accentColor,
  });

  final String organizationId;
  final String name;
  final String? logoUrl;
  final String? accentColor;

  factory OrganizationBrandingDto.fromJson(Map<String, dynamic> json) => OrganizationBrandingDto(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        logoUrl: json['logoUrl'] == null ? null : json['logoUrl'] as String,
        accentColor: json['accentColor'] == null ? null : json['accentColor'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'logoUrl': logoUrl,
        'accentColor': accentColor,
      };
}

/// Контракт: Platform/Organizations/OrganizationDetailDto.cs
class OrganizationDetailDto {
  const OrganizationDetailDto({
    required this.organizationId,
    required this.slug,
    required this.name,
    required this.status,
    this.statusReason,
    this.statusChangedAtUtc,
    required this.planCode,
    required this.subscriptionStatus,
    required this.limits,
    required this.branches,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    this.contactEmail,
    this.contactPhone,
    this.legalDetails,
    this.updateChannel,
    this.pinnedClientVersion,
  });

  final String organizationId;
  final String slug;
  final String name;
  final String status;
  final String? statusReason;
  final DateTime? statusChangedAtUtc;
  final String planCode;
  final String subscriptionStatus;
  final OrganizationLimitsDto limits;
  final List<OrganizationBranchDto> branches;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;
  final String? contactEmail;
  final String? contactPhone;
  final String? legalDetails;
  final String? updateChannel;
  final String? pinnedClientVersion;

  factory OrganizationDetailDto.fromJson(Map<String, dynamic> json) => OrganizationDetailDto(
        organizationId: json['organizationId'] as String,
        slug: json['slug'] as String,
        name: json['name'] as String,
        status: json['status'] as String,
        statusReason: json['statusReason'] == null ? null : json['statusReason'] as String,
        statusChangedAtUtc: json['statusChangedAtUtc'] == null ? null : DateTime.parse(json['statusChangedAtUtc'] as String),
        planCode: json['planCode'] as String,
        subscriptionStatus: json['subscriptionStatus'] as String,
        limits: OrganizationLimitsDto.fromJson(json['limits'] as Map<String, dynamic>),
        branches: (json['branches'] as List<dynamic>).map((item) => OrganizationBranchDto.fromJson(item as Map<String, dynamic>)).toList(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
        contactEmail: json['contactEmail'] == null ? null : json['contactEmail'] as String,
        contactPhone: json['contactPhone'] == null ? null : json['contactPhone'] as String,
        legalDetails: json['legalDetails'] == null ? null : json['legalDetails'] as String,
        updateChannel: json['updateChannel'] == null ? null : json['updateChannel'] as String,
        pinnedClientVersion: json['pinnedClientVersion'] == null ? null : json['pinnedClientVersion'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'slug': slug,
        'name': name,
        'status': status,
        'statusReason': statusReason,
        'statusChangedAtUtc': statusChangedAtUtc?.toIso8601String(),
        'planCode': planCode,
        'subscriptionStatus': subscriptionStatus,
        'limits': limits.toJson(),
        'branches': branches.map((item) => item.toJson()).toList(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
        'contactEmail': contactEmail,
        'contactPhone': contactPhone,
        'legalDetails': legalDetails,
        'updateChannel': updateChannel,
        'pinnedClientVersion': pinnedClientVersion,
      };
}

/// A club as it appears in the public picker: enough to choose it, nothing about how the
/// business is doing. The mobile app has no hostname to derive a club from, so the player picks
/// one from this list before signing in.
/// The showcase fields below (places, price, seats) are what turns a list of names into a
/// shop window: a player picks a club by where it is and what an hour costs, and a name alone
/// answers neither question. They are optional so a club that filled nothing in still appears.
///
/// Контракт: Branding/OrganizationDirectoryEntryDto.cs
class OrganizationDirectoryEntryDto {
  const OrganizationDirectoryEntryDto({
    required this.organizationId,
    required this.slug,
    required this.name,
    this.logoUrl,
    this.accentColor,
    this.places,
    this.pricePerHourFromMinorUnits,
    this.currencyCode,
    this.seatCount,
    this.rating,
    this.reviewCount,
  });

  final String organizationId;
  final String slug;
  final String name;
  final String? logoUrl;
  final String? accentColor;
  final List<ClubPlaceDto>? places;
  final int? pricePerHourFromMinorUnits;
  final String? currencyCode;
  final int? seatCount;
  final double? rating;
  final int? reviewCount;

  factory OrganizationDirectoryEntryDto.fromJson(Map<String, dynamic> json) => OrganizationDirectoryEntryDto(
        organizationId: json['organizationId'] as String,
        slug: json['slug'] as String,
        name: json['name'] as String,
        logoUrl: json['logoUrl'] == null ? null : json['logoUrl'] as String,
        accentColor: json['accentColor'] == null ? null : json['accentColor'] as String,
        places: json['places'] == null ? null : (json['places'] as List<dynamic>).map((item) => ClubPlaceDto.fromJson(item as Map<String, dynamic>)).toList(),
        pricePerHourFromMinorUnits: json['pricePerHourFromMinorUnits'] == null ? null : (json['pricePerHourFromMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
        seatCount: json['seatCount'] == null ? null : (json['seatCount'] as num).toInt(),
        rating: json['rating'] == null ? null : (json['rating'] as num).toDouble(),
        reviewCount: json['reviewCount'] == null ? null : (json['reviewCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'slug': slug,
        'name': name,
        'logoUrl': logoUrl,
        'accentColor': accentColor,
        'places': places?.map((item) => item.toJson()).toList(),
        'pricePerHourFromMinorUnits': pricePerHourFromMinorUnits,
        'currencyCode': currencyCode,
        'seatCount': seatCount,
        'rating': rating,
        'reviewCount': reviewCount,
      };
}

/// Состояние фичи для клуба вместе с тем, ЧЕМ оно решено: «не куплено» и «не выкачено» —
/// разные ответы клиенту, и панель обязана их различать.
///
/// Контракт: Platform/Features/FeatureContracts.cs
class OrganizationFeatureStateDto {
  const OrganizationFeatureStateDto({
    required this.featureKey,
    required this.name,
    required this.description,
    required this.isEnabled,
    required this.decisionLevel,
    this.overrideValue,
    this.overrideReason,
    this.overrideSetAtUtc,
    this.planValue,
    required this.defaultValue,
  });

  final String featureKey;
  final String name;
  final String description;
  final bool isEnabled;
  final String decisionLevel;
  final bool? overrideValue;
  final String? overrideReason;
  final DateTime? overrideSetAtUtc;
  final bool? planValue;
  final bool defaultValue;

  factory OrganizationFeatureStateDto.fromJson(Map<String, dynamic> json) => OrganizationFeatureStateDto(
        featureKey: json['featureKey'] as String,
        name: json['name'] as String,
        description: json['description'] as String,
        isEnabled: json['isEnabled'] as bool,
        decisionLevel: json['decisionLevel'] as String,
        overrideValue: json['overrideValue'] == null ? null : json['overrideValue'] as bool,
        overrideReason: json['overrideReason'] == null ? null : json['overrideReason'] as String,
        overrideSetAtUtc: json['overrideSetAtUtc'] == null ? null : DateTime.parse(json['overrideSetAtUtc'] as String),
        planValue: json['planValue'] == null ? null : json['planValue'] as bool,
        defaultValue: json['defaultValue'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'featureKey': featureKey,
        'name': name,
        'description': description,
        'isEnabled': isEnabled,
        'decisionLevel': decisionLevel,
        'overrideValue': overrideValue,
        'overrideReason': overrideReason,
        'overrideSetAtUtc': overrideSetAtUtc?.toIso8601String(),
        'planValue': planValue,
        'defaultValue': defaultValue,
      };
}

/// Контракт: Platform/Health/OrganizationHealthDto.cs
class OrganizationHealthDto {
  const OrganizationHealthDto({
    required this.organizationId,
    required this.status,
    required this.branchCount,
    required this.deviceCount,
    required this.activeStaffUserCount,
    this.latestStaffSignInAtUtc,
    this.latestMigration,
    required this.recentErrorCount,
    required this.recentErrors,
  });

  final String organizationId;
  final String status;
  final int branchCount;
  final int deviceCount;
  final int activeStaffUserCount;
  final DateTime? latestStaffSignInAtUtc;
  final String? latestMigration;
  final int recentErrorCount;
  final List<OrganizationHealthErrorDto> recentErrors;

  factory OrganizationHealthDto.fromJson(Map<String, dynamic> json) => OrganizationHealthDto(
        organizationId: json['organizationId'] as String,
        status: json['status'] as String,
        branchCount: (json['branchCount'] as num).toInt(),
        deviceCount: (json['deviceCount'] as num).toInt(),
        activeStaffUserCount: (json['activeStaffUserCount'] as num).toInt(),
        latestStaffSignInAtUtc: json['latestStaffSignInAtUtc'] == null ? null : DateTime.parse(json['latestStaffSignInAtUtc'] as String),
        latestMigration: json['latestMigration'] == null ? null : json['latestMigration'] as String,
        recentErrorCount: (json['recentErrorCount'] as num).toInt(),
        recentErrors: (json['recentErrors'] as List<dynamic>).map((item) => OrganizationHealthErrorDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'status': status,
        'branchCount': branchCount,
        'deviceCount': deviceCount,
        'activeStaffUserCount': activeStaffUserCount,
        'latestStaffSignInAtUtc': latestStaffSignInAtUtc?.toIso8601String(),
        'latestMigration': latestMigration,
        'recentErrorCount': recentErrorCount,
        'recentErrors': recentErrors.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Platform/Health/OrganizationHealthErrorDto.cs
class OrganizationHealthErrorDto {
  const OrganizationHealthErrorDto({
    required this.createdAtUtc,
    required this.source,
    required this.action,
    required this.outcome,
    this.message,
  });

  final DateTime createdAtUtc;
  final String source;
  final String action;
  final String outcome;
  final String? message;

  factory OrganizationHealthErrorDto.fromJson(Map<String, dynamic> json) => OrganizationHealthErrorDto(
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        source: json['source'] as String,
        action: json['action'] as String,
        outcome: json['outcome'] as String,
        message: json['message'] == null ? null : json['message'] as String,
      );

  Map<String, dynamic> toJson() => {
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'source': source,
        'action': action,
        'outcome': outcome,
        'message': message,
      };
}

/// Контракт: Platform/Organizations/OrganizationLimitsDto.cs
class OrganizationLimitsDto {
  const OrganizationLimitsDto({
    this.maxBranches,
    this.maxDevicesPerBranch,
    this.maxConcurrentSessions,
    this.maxStaffUsersPerBranch,
  });

  final int? maxBranches;
  final int? maxDevicesPerBranch;
  final int? maxConcurrentSessions;
  final int? maxStaffUsersPerBranch;

  factory OrganizationLimitsDto.fromJson(Map<String, dynamic> json) => OrganizationLimitsDto(
        maxBranches: json['maxBranches'] == null ? null : (json['maxBranches'] as num).toInt(),
        maxDevicesPerBranch: json['maxDevicesPerBranch'] == null ? null : (json['maxDevicesPerBranch'] as num).toInt(),
        maxConcurrentSessions: json['maxConcurrentSessions'] == null ? null : (json['maxConcurrentSessions'] as num).toInt(),
        maxStaffUsersPerBranch: json['maxStaffUsersPerBranch'] == null ? null : (json['maxStaffUsersPerBranch'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'maxBranches': maxBranches,
        'maxDevicesPerBranch': maxDevicesPerBranch,
        'maxConcurrentSessions': maxConcurrentSessions,
        'maxStaffUsersPerBranch': maxStaffUsersPerBranch,
      };
}

/// Состояние ухода клуба: где он в цикле «заявка → выгрузка → стирание».
///
/// Контракт: Platform/Organizations/OffboardingContracts.cs
class OrganizationOffboardingDto {
  const OrganizationOffboardingDto({
    required this.organizationId,
    required this.slug,
    required this.status,
    this.purgeEligibleAtUtc,
    this.purgedAtUtc,
    required this.canPurge,
  });

  final String organizationId;
  final String slug;
  final String status;
  final DateTime? purgeEligibleAtUtc;
  final DateTime? purgedAtUtc;
  final bool canPurge;

  factory OrganizationOffboardingDto.fromJson(Map<String, dynamic> json) => OrganizationOffboardingDto(
        organizationId: json['organizationId'] as String,
        slug: json['slug'] as String,
        status: json['status'] as String,
        purgeEligibleAtUtc: json['purgeEligibleAtUtc'] == null ? null : DateTime.parse(json['purgeEligibleAtUtc'] as String),
        purgedAtUtc: json['purgedAtUtc'] == null ? null : DateTime.parse(json['purgedAtUtc'] as String),
        canPurge: json['canPurge'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'slug': slug,
        'status': status,
        'purgeEligibleAtUtc': purgeEligibleAtUtc?.toIso8601String(),
        'purgedAtUtc': purgedAtUtc?.toIso8601String(),
        'canPurge': canPurge,
      };
}

/// Контракт: Identity/AccountActivation/OrganizationOwnerAccountActivationResult.cs
class OrganizationOwnerAccountActivationResult {
  const OrganizationOwnerAccountActivationResult({
    required this.organizationId,
    required this.branchId,
    required this.nextStep,
  });

  final String organizationId;
  final String branchId;
  final String nextStep;

  factory OrganizationOwnerAccountActivationResult.fromJson(Map<String, dynamic> json) => OrganizationOwnerAccountActivationResult(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        nextStep: json['nextStep'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'nextStep': nextStep,
      };
}

/// Контракт: Identity/AccountActivation/OrganizationOwnerInviteDto.cs
class OrganizationOwnerInviteDto {
  const OrganizationOwnerInviteDto({
    required this.organizationOwnerInviteId,
    required this.organizationId,
    required this.branchId,
    required this.code,
    required this.status,
    this.ownerUserName,
    this.ownerDisplayName,
    required this.expiresAtUtc,
    this.acceptedAtUtc,
    this.revokedAtUtc,
    this.revokedReason,
    required this.createdAtUtc,
  });

  final String organizationOwnerInviteId;
  final String organizationId;
  final String branchId;
  final String code;
  final String status;
  final String? ownerUserName;
  final String? ownerDisplayName;
  final DateTime expiresAtUtc;
  final DateTime? acceptedAtUtc;
  final DateTime? revokedAtUtc;
  final String? revokedReason;
  final DateTime createdAtUtc;

  factory OrganizationOwnerInviteDto.fromJson(Map<String, dynamic> json) => OrganizationOwnerInviteDto(
        organizationOwnerInviteId: json['organizationOwnerInviteId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        code: json['code'] as String,
        status: json['status'] as String,
        ownerUserName: json['ownerUserName'] == null ? null : json['ownerUserName'] as String,
        ownerDisplayName: json['ownerDisplayName'] == null ? null : json['ownerDisplayName'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        acceptedAtUtc: json['acceptedAtUtc'] == null ? null : DateTime.parse(json['acceptedAtUtc'] as String),
        revokedAtUtc: json['revokedAtUtc'] == null ? null : DateTime.parse(json['revokedAtUtc'] as String),
        revokedReason: json['revokedReason'] == null ? null : json['revokedReason'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationOwnerInviteId': organizationOwnerInviteId,
        'organizationId': organizationId,
        'branchId': branchId,
        'code': code,
        'status': status,
        'ownerUserName': ownerUserName,
        'ownerDisplayName': ownerDisplayName,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'acceptedAtUtc': acceptedAtUtc?.toIso8601String(),
        'revokedAtUtc': revokedAtUtc?.toIso8601String(),
        'revokedReason': revokedReason,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Identity/AccountActivation/OrganizationOwnerInviteSummaryDto.cs
class OrganizationOwnerInviteSummaryDto {
  const OrganizationOwnerInviteSummaryDto({
    required this.organizationOwnerInviteId,
    required this.organizationId,
    required this.branchId,
    required this.codeSuffix,
    required this.status,
    this.ownerUserName,
    this.ownerDisplayName,
    required this.expiresAtUtc,
    this.acceptedAtUtc,
    this.revokedAtUtc,
    this.revokedReason,
    required this.createdAtUtc,
  });

  final String organizationOwnerInviteId;
  final String organizationId;
  final String branchId;
  final String codeSuffix;
  final String status;
  final String? ownerUserName;
  final String? ownerDisplayName;
  final DateTime expiresAtUtc;
  final DateTime? acceptedAtUtc;
  final DateTime? revokedAtUtc;
  final String? revokedReason;
  final DateTime createdAtUtc;

  factory OrganizationOwnerInviteSummaryDto.fromJson(Map<String, dynamic> json) => OrganizationOwnerInviteSummaryDto(
        organizationOwnerInviteId: json['organizationOwnerInviteId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        codeSuffix: json['codeSuffix'] as String,
        status: json['status'] as String,
        ownerUserName: json['ownerUserName'] == null ? null : json['ownerUserName'] as String,
        ownerDisplayName: json['ownerDisplayName'] == null ? null : json['ownerDisplayName'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        acceptedAtUtc: json['acceptedAtUtc'] == null ? null : DateTime.parse(json['acceptedAtUtc'] as String),
        revokedAtUtc: json['revokedAtUtc'] == null ? null : DateTime.parse(json['revokedAtUtc'] as String),
        revokedReason: json['revokedReason'] == null ? null : json['revokedReason'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationOwnerInviteId': organizationOwnerInviteId,
        'organizationId': organizationId,
        'branchId': branchId,
        'codeSuffix': codeSuffix,
        'status': status,
        'ownerUserName': ownerUserName,
        'ownerDisplayName': ownerDisplayName,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'acceptedAtUtc': acceptedAtUtc?.toIso8601String(),
        'revokedAtUtc': revokedAtUtc?.toIso8601String(),
        'revokedReason': revokedReason,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Platform/Billing/OrganizationSubscriptionDto.cs
class OrganizationSubscriptionDto {
  const OrganizationSubscriptionDto({
    required this.organizationSubscriptionId,
    required this.organizationId,
    required this.planCode,
    required this.status,
    required this.currentPeriodStartUtc,
    required this.currentPeriodEndUtc,
    this.nextInvoiceUtc,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.billingInterval,
    required this.cancelAtPeriodEnd,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    this.paymentGraceUntilUtc,
    this.discountPercent,
    this.discountAmountMinorUnits,
    this.discountUntilUtc,
    this.discountReason,
  });

  final String organizationSubscriptionId;
  final String organizationId;
  final String planCode;
  final String status;
  final DateTime currentPeriodStartUtc;
  final DateTime currentPeriodEndUtc;
  final DateTime? nextInvoiceUtc;
  final int amountMinorUnits;
  final String currencyCode;
  final String billingInterval;
  final bool cancelAtPeriodEnd;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;
  final DateTime? paymentGraceUntilUtc;
  final int? discountPercent;
  final int? discountAmountMinorUnits;
  final DateTime? discountUntilUtc;
  final String? discountReason;

  factory OrganizationSubscriptionDto.fromJson(Map<String, dynamic> json) => OrganizationSubscriptionDto(
        organizationSubscriptionId: json['organizationSubscriptionId'] as String,
        organizationId: json['organizationId'] as String,
        planCode: json['planCode'] as String,
        status: json['status'] as String,
        currentPeriodStartUtc: DateTime.parse(json['currentPeriodStartUtc'] as String),
        currentPeriodEndUtc: DateTime.parse(json['currentPeriodEndUtc'] as String),
        nextInvoiceUtc: json['nextInvoiceUtc'] == null ? null : DateTime.parse(json['nextInvoiceUtc'] as String),
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        billingInterval: json['billingInterval'] as String,
        cancelAtPeriodEnd: json['cancelAtPeriodEnd'] as bool,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
        paymentGraceUntilUtc: json['paymentGraceUntilUtc'] == null ? null : DateTime.parse(json['paymentGraceUntilUtc'] as String),
        discountPercent: json['discountPercent'] == null ? null : (json['discountPercent'] as num).toInt(),
        discountAmountMinorUnits: json['discountAmountMinorUnits'] == null ? null : (json['discountAmountMinorUnits'] as num).toInt(),
        discountUntilUtc: json['discountUntilUtc'] == null ? null : DateTime.parse(json['discountUntilUtc'] as String),
        discountReason: json['discountReason'] == null ? null : json['discountReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationSubscriptionId': organizationSubscriptionId,
        'organizationId': organizationId,
        'planCode': planCode,
        'status': status,
        'currentPeriodStartUtc': currentPeriodStartUtc.toIso8601String(),
        'currentPeriodEndUtc': currentPeriodEndUtc.toIso8601String(),
        'nextInvoiceUtc': nextInvoiceUtc?.toIso8601String(),
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'billingInterval': billingInterval,
        'cancelAtPeriodEnd': cancelAtPeriodEnd,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
        'paymentGraceUntilUtc': paymentGraceUntilUtc?.toIso8601String(),
        'discountPercent': discountPercent,
        'discountAmountMinorUnits': discountAmountMinorUnits,
        'discountUntilUtc': discountUntilUtc?.toIso8601String(),
        'discountReason': discountReason,
      };
}

/// Контракт: Platform/Organizations/OrganizationSummaryDto.cs
class OrganizationSummaryDto {
  const OrganizationSummaryDto({
    required this.organizationId,
    required this.slug,
    required this.name,
    required this.status,
    required this.planCode,
    required this.subscriptionStatus,
    required this.branchCount,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    required this.recentErrorCount,
    required this.expiringOwnerInviteCount,
    required this.rolloutAttentionCount,
  });

  final String organizationId;
  final String slug;
  final String name;
  final String status;
  final String planCode;
  final String subscriptionStatus;
  final int branchCount;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;
  final int recentErrorCount;
  final int expiringOwnerInviteCount;
  final int rolloutAttentionCount;

  factory OrganizationSummaryDto.fromJson(Map<String, dynamic> json) => OrganizationSummaryDto(
        organizationId: json['organizationId'] as String,
        slug: json['slug'] as String,
        name: json['name'] as String,
        status: json['status'] as String,
        planCode: json['planCode'] as String,
        subscriptionStatus: json['subscriptionStatus'] as String,
        branchCount: (json['branchCount'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
        recentErrorCount: (json['recentErrorCount'] as num).toInt(),
        expiringOwnerInviteCount: (json['expiringOwnerInviteCount'] as num).toInt(),
        rolloutAttentionCount: (json['rolloutAttentionCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'slug': slug,
        'name': name,
        'status': status,
        'planCode': planCode,
        'subscriptionStatus': subscriptionStatus,
        'branchCount': branchCount,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
        'recentErrorCount': recentErrorCount,
        'expiringOwnerInviteCount': expiringOwnerInviteCount,
        'rolloutAttentionCount': rolloutAttentionCount,
      };
}

/// Контракт: Platform/SupportNotes/OrganizationSupportNoteDto.cs
class OrganizationSupportNoteDto {
  const OrganizationSupportNoteDto({
    required this.organizationSupportNoteId,
    required this.organizationId,
    required this.authorPlatformAdminId,
    required this.authorDisplayName,
    required this.body,
    required this.createdAtUtc,
  });

  final String organizationSupportNoteId;
  final String organizationId;
  final String authorPlatformAdminId;
  final String authorDisplayName;
  final String body;
  final DateTime createdAtUtc;

  factory OrganizationSupportNoteDto.fromJson(Map<String, dynamic> json) => OrganizationSupportNoteDto(
        organizationSupportNoteId: json['organizationSupportNoteId'] as String,
        organizationId: json['organizationId'] as String,
        authorPlatformAdminId: json['authorPlatformAdminId'] as String,
        authorDisplayName: json['authorDisplayName'] as String,
        body: json['body'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationSupportNoteId': organizationSupportNoteId,
        'organizationId': organizationId,
        'authorPlatformAdminId': authorPlatformAdminId,
        'authorDisplayName': authorDisplayName,
        'body': body,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: News/OwnerBranchSummaryDto.cs
class OwnerBranchSummaryDto {
  const OwnerBranchSummaryDto({
    required this.branchId,
    required this.name,
  });

  final String branchId;
  final String name;

  factory OwnerBranchSummaryDto.fromJson(Map<String, dynamic> json) => OwnerBranchSummaryDto(
        branchId: json['branchId'] as String,
        name: json['name'] as String,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'name': name,
      };
}

/// Anti-fraud §5.6: the owner's daily "watch the staff" digest — per-actor refunds, comps, manual
/// corrections / debt write-offs, and shift discrepancies for a single branch-day.
///
/// Контракт: Reports/OwnerDailySummaryResultDto.cs
class OwnerDailySummaryActorRowDto {
  const OwnerDailySummaryActorRowDto({
    this.actorStaffUserId,
    required this.actorDisplayName,
    required this.refundCount,
    required this.refundTotalMinorUnits,
    required this.compCount,
    required this.compValueMinorUnits,
    required this.manualCorrectionCount,
    required this.manualCorrectionTotalMinorUnits,
    required this.writeOffCount,
    required this.writeOffTotalMinorUnits,
    required this.discrepancyShiftCount,
    required this.discrepancyTotalMinorUnits,
  });

  final String? actorStaffUserId;
  final String actorDisplayName;
  final int refundCount;
  final int refundTotalMinorUnits;
  final int compCount;
  final int compValueMinorUnits;
  final int manualCorrectionCount;
  final int manualCorrectionTotalMinorUnits;
  final int writeOffCount;
  final int writeOffTotalMinorUnits;
  final int discrepancyShiftCount;
  final int discrepancyTotalMinorUnits;

  factory OwnerDailySummaryActorRowDto.fromJson(Map<String, dynamic> json) => OwnerDailySummaryActorRowDto(
        actorStaffUserId: json['actorStaffUserId'] == null ? null : json['actorStaffUserId'] as String,
        actorDisplayName: json['actorDisplayName'] as String,
        refundCount: (json['refundCount'] as num).toInt(),
        refundTotalMinorUnits: (json['refundTotalMinorUnits'] as num).toInt(),
        compCount: (json['compCount'] as num).toInt(),
        compValueMinorUnits: (json['compValueMinorUnits'] as num).toInt(),
        manualCorrectionCount: (json['manualCorrectionCount'] as num).toInt(),
        manualCorrectionTotalMinorUnits: (json['manualCorrectionTotalMinorUnits'] as num).toInt(),
        writeOffCount: (json['writeOffCount'] as num).toInt(),
        writeOffTotalMinorUnits: (json['writeOffTotalMinorUnits'] as num).toInt(),
        discrepancyShiftCount: (json['discrepancyShiftCount'] as num).toInt(),
        discrepancyTotalMinorUnits: (json['discrepancyTotalMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'actorStaffUserId': actorStaffUserId,
        'actorDisplayName': actorDisplayName,
        'refundCount': refundCount,
        'refundTotalMinorUnits': refundTotalMinorUnits,
        'compCount': compCount,
        'compValueMinorUnits': compValueMinorUnits,
        'manualCorrectionCount': manualCorrectionCount,
        'manualCorrectionTotalMinorUnits': manualCorrectionTotalMinorUnits,
        'writeOffCount': writeOffCount,
        'writeOffTotalMinorUnits': writeOffTotalMinorUnits,
        'discrepancyShiftCount': discrepancyShiftCount,
        'discrepancyTotalMinorUnits': discrepancyTotalMinorUnits,
      };
}

/// Контракт: Reports/OwnerDailySummaryResultDto.cs
class OwnerDailySummaryResultDto {
  const OwnerDailySummaryResultDto({
    required this.date,
    required this.currencyCode,
    required this.rows,
    required this.totalRefundMinorUnits,
    required this.totalCompCount,
    required this.totalCompValueMinorUnits,
    required this.totalManualCorrectionMinorUnits,
    required this.totalWriteOffMinorUnits,
    required this.totalDiscrepancyMinorUnits,
  });

  final String date;
  final String currencyCode;
  final List<OwnerDailySummaryActorRowDto> rows;
  final int totalRefundMinorUnits;
  final int totalCompCount;
  final int totalCompValueMinorUnits;
  final int totalManualCorrectionMinorUnits;
  final int totalWriteOffMinorUnits;
  final int totalDiscrepancyMinorUnits;

  factory OwnerDailySummaryResultDto.fromJson(Map<String, dynamic> json) => OwnerDailySummaryResultDto(
        date: json['date'] as String,
        currencyCode: json['currencyCode'] as String,
        rows: (json['rows'] as List<dynamic>).map((item) => OwnerDailySummaryActorRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        totalRefundMinorUnits: (json['totalRefundMinorUnits'] as num).toInt(),
        totalCompCount: (json['totalCompCount'] as num).toInt(),
        totalCompValueMinorUnits: (json['totalCompValueMinorUnits'] as num).toInt(),
        totalManualCorrectionMinorUnits: (json['totalManualCorrectionMinorUnits'] as num).toInt(),
        totalWriteOffMinorUnits: (json['totalWriteOffMinorUnits'] as num).toInt(),
        totalDiscrepancyMinorUnits: (json['totalDiscrepancyMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'date': date,
        'currencyCode': currencyCode,
        'rows': rows.map((item) => item.toJson()).toList(),
        'totalRefundMinorUnits': totalRefundMinorUnits,
        'totalCompCount': totalCompCount,
        'totalCompValueMinorUnits': totalCompValueMinorUnits,
        'totalManualCorrectionMinorUnits': totalManualCorrectionMinorUnits,
        'totalWriteOffMinorUnits': totalWriteOffMinorUnits,
        'totalDiscrepancyMinorUnits': totalDiscrepancyMinorUnits,
      };
}

/// Контракт: Packages/PackageDefinitionDto.cs
class PackageDefinitionDto {
  const PackageDefinitionDto({
    required this.packageDefinitionId,
    required this.organizationId,
    required this.branchId,
    required this.name,
    required this.price,
    required this.includedSeconds,
    required this.bonusSeconds,
    required this.expiresAfterDays,
    required this.isActive,
    required this.createdAtUtc,
  });

  final String packageDefinitionId;
  final String organizationId;
  final String branchId;
  final String name;
  final MoneyDto price;
  final int includedSeconds;
  final int bonusSeconds;
  final int expiresAfterDays;
  final bool isActive;
  final DateTime createdAtUtc;

  factory PackageDefinitionDto.fromJson(Map<String, dynamic> json) => PackageDefinitionDto(
        packageDefinitionId: json['packageDefinitionId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        includedSeconds: (json['includedSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num).toInt(),
        expiresAfterDays: (json['expiresAfterDays'] as num).toInt(),
        isActive: json['isActive'] as bool,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'packageDefinitionId': packageDefinitionId,
        'organizationId': organizationId,
        'branchId': branchId,
        'name': name,
        'price': price.toJson(),
        'includedSeconds': includedSeconds,
        'bonusSeconds': bonusSeconds,
        'expiresAfterDays': expiresAfterDays,
        'isActive': isActive,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Пакет часов в прайсе клуба: предоплата, за которую час выходит дешевле поминутного тарифа.
///
/// Контракт: Operator/PackageOptionDto.cs
class PackageOptionDto {
  const PackageOptionDto({
    required this.packageDefinitionId,
    required this.name,
    required this.currencyCode,
    required this.priceMinorUnits,
    required this.includedSeconds,
    required this.bonusSeconds,
    required this.expiresAfterDays,
  });

  final String packageDefinitionId;
  final String name;
  final String currencyCode;
  final int priceMinorUnits;

  /// Оплаченное и бонусное время — две величины одного: игрок покупает часы, а не два
  /// отдельных счётчика, и складывать их полагается тому, кто показывает.
  final int includedSeconds;
  final int bonusSeconds;
  final int expiresAfterDays;

  factory PackageOptionDto.fromJson(Map<String, dynamic> json) => PackageOptionDto(
        packageDefinitionId: json['packageDefinitionId'] as String,
        name: json['name'] as String,
        currencyCode: json['currencyCode'] as String,
        priceMinorUnits: (json['priceMinorUnits'] as num).toInt(),
        includedSeconds: (json['includedSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num).toInt(),
        expiresAfterDays: (json['expiresAfterDays'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'packageDefinitionId': packageDefinitionId,
        'name': name,
        'currencyCode': currencyCode,
        'priceMinorUnits': priceMinorUnits,
        'includedSeconds': includedSeconds,
        'bonusSeconds': bonusSeconds,
        'expiresAfterDays': expiresAfterDays,
      };
}

/// Пауза сессии: счётчик времени встаёт, ПК запирается, место остаётся за игроком. Стоять на
/// паузе бесконечно нельзя — филиал задаёт предел, после которого сессия закрывается сама.
///
/// Контракт: Sessions/PauseSessionRequest.cs
class PauseSessionRequest {
  const PauseSessionRequest({
    required this.reason,
    required this.idempotencyKey,
    this.expectedVersion,
  });

  final String reason;
  final String idempotencyKey;
  final int? expectedVersion;

  factory PauseSessionRequest.fromJson(Map<String, dynamic> json) => PauseSessionRequest(
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'reason': reason,
        'idempotencyKey': idempotencyKey,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: Billing/PayDebtRequest.cs
class PayDebtRequest {
  const PayDebtRequest({
    required this.organizationId,
    required this.amount,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final MoneyDto amount;
  final String reason;
  final String idempotencyKey;

  factory PayDebtRequest.fromJson(Map<String, dynamic> json) => PayDebtRequest(
        organizationId: json['organizationId'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'amount': amount.toJson(),
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// One part of a split payment: a method and the amount tendered with it.
///
/// Контракт: Sessions/PaymentPartDto.cs
class PaymentPartDto {
  const PaymentPartDto({
    required this.paymentMethod,
    required this.amount,
  });

  final String paymentMethod;
  final MoneyDto amount;

  factory PaymentPartDto.fromJson(Map<String, dynamic> json) => PaymentPartDto(
        paymentMethod: json['paymentMethod'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'paymentMethod': paymentMethod,
        'amount': amount.toJson(),
      };
}

/// Контракт: Tips/TipContracts.cs
class PayOutShiftTipsRequest {
  const PayOutShiftTipsRequest({
    required this.idempotencyKey,
  });

  final String idempotencyKey;

  factory PayOutShiftTipsRequest.fromJson(Map<String, dynamic> json) => PayOutShiftTipsRequest(
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'idempotencyKey': idempotencyKey,
      };
}

/// A finished visit that has not been reviewed yet — what the app offers to rate.
/// Оценить предлагается один раз и только пока вечер свежий в памяти.
///
/// Контракт: Reviews/ClubReviewDtos.cs
class PendingClubReviewDto {
  const PendingClubReviewDto({
    required this.sessionId,
    required this.branchName,
    required this.seatName,
    required this.endedAtUtc,
  });

  final String sessionId;
  final String branchName;
  final String seatName;
  final DateTime endedAtUtc;

  factory PendingClubReviewDto.fromJson(Map<String, dynamic> json) => PendingClubReviewDto(
        sessionId: json['sessionId'] as String,
        branchName: json['branchName'] as String,
        seatName: json['seatName'] as String,
        endedAtUtc: DateTime.parse(json['endedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'branchName': branchName,
        'seatName': seatName,
        'endedAtUtc': endedAtUtc.toIso8601String(),
      };
}

/// Контракт: Shop/PlaceShopOrderRequest.cs
class PlaceShopOrderRequest {
  const PlaceShopOrderRequest({
    required this.lines,
    required this.idempotencyKey,
  });

  final List<ShopOrderLineInput> lines;
  final String idempotencyKey;

  factory PlaceShopOrderRequest.fromJson(Map<String, dynamic> json) => PlaceShopOrderRequest(
        lines: (json['lines'] as List<dynamic>).map((item) => ShopOrderLineInput.fromJson(item as Map<String, dynamic>)).toList(),
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'lines': lines.map((item) => item.toJson()).toList(),
        'idempotencyKey': idempotencyKey,
      };
}

/// Тело отказа по лимиту тарифа. Current и Limit едят
/// клиенту, чтобы отказ читался как «филиалов 2 из 2», а не как «нельзя».
///
/// Контракт: Platform/Organizations/PlanLimitExceededDto.cs
class PlanLimitExceededDto {
  const PlanLimitExceededDto({
    required this.code,
    required this.limitName,
    required this.limit,
    required this.current,
    required this.planCode,
  });

  final String code;
  final String limitName;
  final int limit;
  final int current;
  final String planCode;

  factory PlanLimitExceededDto.fromJson(Map<String, dynamic> json) => PlanLimitExceededDto(
        code: json['code'] as String,
        limitName: json['limitName'] as String,
        limit: (json['limit'] as num).toInt(),
        current: (json['current'] as num).toInt(),
        planCode: json['planCode'] as String,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
        'limitName': limitName,
        'limit': limit,
        'current': current,
        'planCode': planCode,
      };
}

/// Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs
class PlatformAdminInvitationDto {
  const PlatformAdminInvitationDto({
    required this.invitationId,
    required this.role,
    required this.status,
    required this.expiresAtUtc,
    required this.createdAtUtc,
  });

  final String invitationId;
  final String role;
  final String status;
  final DateTime expiresAtUtc;
  final DateTime createdAtUtc;

  factory PlatformAdminInvitationDto.fromJson(Map<String, dynamic> json) => PlatformAdminInvitationDto(
        invitationId: json['invitationId'] as String,
        role: json['role'] as String,
        status: json['status'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'invitationId': invitationId,
        'role': role,
        'status': status,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs
class PlatformAdminListItem {
  const PlatformAdminListItem({
    required this.platformAdminUserId,
    required this.userName,
    required this.displayName,
    required this.role,
    required this.isActive,
    required this.twoFactorEnabled,
    this.lastSignInAtUtc,
    required this.createdAtUtc,
  });

  final String platformAdminUserId;
  final String userName;
  final String displayName;
  final String role;
  final bool isActive;
  final bool twoFactorEnabled;
  final DateTime? lastSignInAtUtc;
  final DateTime createdAtUtc;

  factory PlatformAdminListItem.fromJson(Map<String, dynamic> json) => PlatformAdminListItem(
        platformAdminUserId: json['platformAdminUserId'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
        role: json['role'] as String,
        isActive: json['isActive'] as bool,
        twoFactorEnabled: json['twoFactorEnabled'] as bool,
        lastSignInAtUtc: json['lastSignInAtUtc'] == null ? null : DateTime.parse(json['lastSignInAtUtc'] as String),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'platformAdminUserId': platformAdminUserId,
        'userName': userName,
        'displayName': displayName,
        'role': role,
        'isActive': isActive,
        'twoFactorEnabled': twoFactorEnabled,
        'lastSignInAtUtc': lastSignInAtUtc?.toIso8601String(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Platform/Auth/PlatformAdminRefreshTokenRequest.cs
class PlatformAdminRefreshTokenRequest {
  const PlatformAdminRefreshTokenRequest({
    required this.refreshToken,
  });

  final String refreshToken;

  factory PlatformAdminRefreshTokenRequest.fromJson(Map<String, dynamic> json) => PlatformAdminRefreshTokenRequest(
        refreshToken: json['refreshToken'] as String,
      );

  Map<String, dynamic> toJson() => {
        'refreshToken': refreshToken,
      };
}

/// First step of sign-in: password alone no longer issues a working session. The caller must present
/// this challenge token to one of the /auth/2fa/* routes (setup or verify) to receive the real
/// PlatformAdminSignInResponse above. The token is short-lived and opaque — it authorizes nothing
/// except those 2FA routes.
///
/// Контракт: Platform/Auth/PlatformAdminSignInResponse.cs
class PlatformAdminSignInChallengeResponse {
  const PlatformAdminSignInChallengeResponse({
    required this.challengeToken,
    required this.expiresAtUtc,
    required this.twoFactorConfigured,
  });

  final String challengeToken;
  final DateTime expiresAtUtc;
  final bool twoFactorConfigured;

  factory PlatformAdminSignInChallengeResponse.fromJson(Map<String, dynamic> json) => PlatformAdminSignInChallengeResponse(
        challengeToken: json['challengeToken'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        twoFactorConfigured: json['twoFactorConfigured'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'challengeToken': challengeToken,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'twoFactorConfigured': twoFactorConfigured,
      };
}

/// Контракт: Platform/Auth/PlatformAdminSignInRequest.cs
class PlatformAdminSignInRequest {
  const PlatformAdminSignInRequest({
    required this.userName,
    required this.password,
  });

  final String userName;
  final String password;

  factory PlatformAdminSignInRequest.fromJson(Map<String, dynamic> json) => PlatformAdminSignInRequest(
        userName: json['userName'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'userName': userName,
        'password': password,
      };
}

/// Контракт: Platform/Auth/PlatformAdminSignInResponse.cs
class PlatformAdminSignInResponse {
  const PlatformAdminSignInResponse({
    required this.platformAdminId,
    required this.userName,
    required this.displayName,
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.refreshTokenExpiresAtUtc,
    required this.roles,
    required this.permissions,
  });

  final String platformAdminId;
  final String userName;
  final String displayName;
  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final DateTime refreshTokenExpiresAtUtc;
  final List<String> roles;
  final List<String> permissions;

  factory PlatformAdminSignInResponse.fromJson(Map<String, dynamic> json) => PlatformAdminSignInResponse(
        platformAdminId: json['platformAdminId'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
        accessToken: json['accessToken'] as String,
        accessTokenExpiresAtUtc: DateTime.parse(json['accessTokenExpiresAtUtc'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshTokenExpiresAtUtc: DateTime.parse(json['refreshTokenExpiresAtUtc'] as String),
        roles: (json['roles'] as List<dynamic>).map((item) => item as String).toList(),
        permissions: (json['permissions'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'platformAdminId': platformAdminId,
        'userName': userName,
        'displayName': displayName,
        'accessToken': accessToken,
        'accessTokenExpiresAtUtc': accessTokenExpiresAtUtc.toIso8601String(),
        'refreshToken': refreshToken,
        'refreshTokenExpiresAtUtc': refreshTokenExpiresAtUtc.toIso8601String(),
        'roles': roles.map((item) => item).toList(),
        'permissions': permissions.map((item) => item).toList(),
      };
}

/// Контракт: Platform/Auth/PlatformAdminSignOutRequest.cs
class PlatformAdminSignOutRequest {
  const PlatformAdminSignOutRequest({
    required this.refreshToken,
  });

  final String refreshToken;

  factory PlatformAdminSignOutRequest.fromJson(Map<String, dynamic> json) => PlatformAdminSignOutRequest(
        refreshToken: json['refreshToken'] as String,
      );

  Map<String, dynamic> toJson() => {
        'refreshToken': refreshToken,
      };
}

/// Контракт: Platform/Analytics/PlatformAnalyticsContracts.cs
class PlatformAnalyticsOverviewDto {
  const PlatformAnalyticsOverviewDto({
    required this.generatedAtUtc,
    required this.currencyCode,
    required this.months,
    required this.currentMrrMinorUnits,
    required this.currentPayingClubs,
    required this.averageRevenuePerClubMinorUnits,
    required this.outstandingMinorUnits,
  });

  final DateTime generatedAtUtc;
  final String currencyCode;
  final List<AnalyticsMonthDto> months;
  final int currentMrrMinorUnits;
  final int currentPayingClubs;
  final int averageRevenuePerClubMinorUnits;
  final int outstandingMinorUnits;

  factory PlatformAnalyticsOverviewDto.fromJson(Map<String, dynamic> json) => PlatformAnalyticsOverviewDto(
        generatedAtUtc: DateTime.parse(json['generatedAtUtc'] as String),
        currencyCode: json['currencyCode'] as String,
        months: (json['months'] as List<dynamic>).map((item) => AnalyticsMonthDto.fromJson(item as Map<String, dynamic>)).toList(),
        currentMrrMinorUnits: (json['currentMrrMinorUnits'] as num).toInt(),
        currentPayingClubs: (json['currentPayingClubs'] as num).toInt(),
        averageRevenuePerClubMinorUnits: (json['averageRevenuePerClubMinorUnits'] as num).toInt(),
        outstandingMinorUnits: (json['outstandingMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'generatedAtUtc': generatedAtUtc.toIso8601String(),
        'currencyCode': currencyCode,
        'months': months.map((item) => item.toJson()).toList(),
        'currentMrrMinorUnits': currentMrrMinorUnits,
        'currentPayingClubs': currentPayingClubs,
        'averageRevenuePerClubMinorUnits': averageRevenuePerClubMinorUnits,
        'outstandingMinorUnits': outstandingMinorUnits,
      };
}

/// Анонс глазами платформы: что написано, кому, в каком он состоянии и дошёл ли.
///
/// Контракт: Platform/Announcements/AnnouncementContracts.cs
class PlatformAnnouncementDto {
  const PlatformAnnouncementDto({
    required this.announcementId,
    required this.title,
    required this.body,
    required this.severity,
    required this.showFromUtc,
    required this.showUntilUtc,
    required this.audienceKind,
    required this.audiencePlanCodes,
    required this.audienceOrganizationIds,
    required this.status,
    this.publishedAtUtc,
    required this.emailDispatched,
    required this.readCount,
  });

  final String announcementId;
  final String title;
  final String body;
  final String severity;
  final DateTime showFromUtc;
  final DateTime showUntilUtc;
  final String audienceKind;
  final List<String> audiencePlanCodes;
  final List<String> audienceOrganizationIds;
  final String status;
  final DateTime? publishedAtUtc;
  final bool emailDispatched;
  final int readCount;

  factory PlatformAnnouncementDto.fromJson(Map<String, dynamic> json) => PlatformAnnouncementDto(
        announcementId: json['announcementId'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        severity: json['severity'] as String,
        showFromUtc: DateTime.parse(json['showFromUtc'] as String),
        showUntilUtc: DateTime.parse(json['showUntilUtc'] as String),
        audienceKind: json['audienceKind'] as String,
        audiencePlanCodes: (json['audiencePlanCodes'] as List<dynamic>).map((item) => item as String).toList(),
        audienceOrganizationIds: (json['audienceOrganizationIds'] as List<dynamic>).map((item) => item as String).toList(),
        status: json['status'] as String,
        publishedAtUtc: json['publishedAtUtc'] == null ? null : DateTime.parse(json['publishedAtUtc'] as String),
        emailDispatched: json['emailDispatched'] as bool,
        readCount: (json['readCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'announcementId': announcementId,
        'title': title,
        'body': body,
        'severity': severity,
        'showFromUtc': showFromUtc.toIso8601String(),
        'showUntilUtc': showUntilUtc.toIso8601String(),
        'audienceKind': audienceKind,
        'audiencePlanCodes': audiencePlanCodes.map((item) => item).toList(),
        'audienceOrganizationIds': audienceOrganizationIds.map((item) => item).toList(),
        'status': status,
        'publishedAtUtc': publishedAtUtc?.toIso8601String(),
        'emailDispatched': emailDispatched,
        'readCount': readCount,
      };
}

/// Контракт: Platform/Health/PlatformHealthContracts.cs
class PlatformHealthOverviewDto {
  const PlatformHealthOverviewDto({
    required this.generatedAtUtc,
    required this.jobs,
    required this.queues,
    required this.openIncidents,
    required this.recentFailures,
    this.mediaStorageConfigured,
    this.alertSmsConfigured,
  });

  final DateTime generatedAtUtc;
  final List<JobHealthDto> jobs;
  final List<QueueHealthDto> queues;
  final List<IncidentDto> openIncidents;
  final List<QueueFailureDto> recentFailures;

  /// Хранилище файлов не настроено: логотипы и фото зала загрузить нельзя ни из мастера, ни из
  /// панели. Видно здесь, а не при первой попытке загрузки — иначе об этом узнаёт клуб, а не мы.
  final bool? mediaStorageConfigured;

  /// Резервный канал критических оповещений: SMS уходят только по одобренному шаблону шлюза, и
  /// без него канал молчит. Пока это было видно лишь в деталях провалившегося прогона, «почта
  /// умерла — придёт SMS» оставалось обещанием, которое некому было проверить.
  final bool? alertSmsConfigured;

  factory PlatformHealthOverviewDto.fromJson(Map<String, dynamic> json) => PlatformHealthOverviewDto(
        generatedAtUtc: DateTime.parse(json['generatedAtUtc'] as String),
        jobs: (json['jobs'] as List<dynamic>).map((item) => JobHealthDto.fromJson(item as Map<String, dynamic>)).toList(),
        queues: (json['queues'] as List<dynamic>).map((item) => QueueHealthDto.fromJson(item as Map<String, dynamic>)).toList(),
        openIncidents: (json['openIncidents'] as List<dynamic>).map((item) => IncidentDto.fromJson(item as Map<String, dynamic>)).toList(),
        recentFailures: (json['recentFailures'] as List<dynamic>).map((item) => QueueFailureDto.fromJson(item as Map<String, dynamic>)).toList(),
        mediaStorageConfigured: json['mediaStorageConfigured'] == null ? null : json['mediaStorageConfigured'] as bool,
        alertSmsConfigured: json['alertSmsConfigured'] == null ? null : json['alertSmsConfigured'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'generatedAtUtc': generatedAtUtc.toIso8601String(),
        'jobs': jobs.map((item) => item.toJson()).toList(),
        'queues': queues.map((item) => item.toJson()).toList(),
        'openIncidents': openIncidents.map((item) => item.toJson()).toList(),
        'recentFailures': recentFailures.map((item) => item.toJson()).toList(),
        'mediaStorageConfigured': mediaStorageConfigured,
        'alertSmsConfigured': alertSmsConfigured,
      };
}

/// Сессия человека. Первые восемь полей — дословно те же, что в PlayerSignInResponse,
/// поэтому старый клиент читает этот ответ, не заметив разницы. Отличие одно и оно про модель:
/// клуба может не быть вовсе — так выглядит человек, зарегистрировавшийся дома и ещё никуда не
/// зашедший.
///
/// Контракт: Identity/RegistrationContracts.cs
class PlatformPersonSessionResponse {
  const PlatformPersonSessionResponse({
    this.playerAccountId,
    this.organizationId,
    required this.displayName,
    required this.phoneVerified,
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.refreshTokenExpiresAtUtc,
    required this.platformPersonId,
    this.preferredLocale,
    required this.profileCompleted,
  });

  final String? playerAccountId;
  final String? organizationId;
  final String displayName;
  final bool phoneVerified;
  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final DateTime refreshTokenExpiresAtUtc;
  final String platformPersonId;
  final String? preferredLocale;

  /// Спрошены ли имя и язык. Показывать ли экран «как вас зовут», решает сервер.
  final bool profileCompleted;

  factory PlatformPersonSessionResponse.fromJson(Map<String, dynamic> json) => PlatformPersonSessionResponse(
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        organizationId: json['organizationId'] == null ? null : json['organizationId'] as String,
        displayName: json['displayName'] as String,
        phoneVerified: json['phoneVerified'] as bool,
        accessToken: json['accessToken'] as String,
        accessTokenExpiresAtUtc: DateTime.parse(json['accessTokenExpiresAtUtc'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshTokenExpiresAtUtc: DateTime.parse(json['refreshTokenExpiresAtUtc'] as String),
        platformPersonId: json['platformPersonId'] as String,
        preferredLocale: json['preferredLocale'] == null ? null : json['preferredLocale'] as String,
        profileCompleted: json['profileCompleted'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'organizationId': organizationId,
        'displayName': displayName,
        'phoneVerified': phoneVerified,
        'accessToken': accessToken,
        'accessTokenExpiresAtUtc': accessTokenExpiresAtUtc.toIso8601String(),
        'refreshToken': refreshToken,
        'refreshTokenExpiresAtUtc': refreshTokenExpiresAtUtc.toIso8601String(),
        'platformPersonId': platformPersonId,
        'preferredLocale': preferredLocale,
        'profileCompleted': profileCompleted,
      };
}

/// Контракт: Platform/Pulse/PlatformPulseContracts.cs
class PlatformPulseDto {
  const PlatformPulseDto({
    required this.generatedAtUtc,
    required this.organizations,
  });

  final DateTime generatedAtUtc;
  final List<PulseOrganizationDto> organizations;

  factory PlatformPulseDto.fromJson(Map<String, dynamic> json) => PlatformPulseDto(
        generatedAtUtc: DateTime.parse(json['generatedAtUtc'] as String),
        organizations: (json['organizations'] as List<dynamic>).map((item) => PulseOrganizationDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'generatedAtUtc': generatedAtUtc.toIso8601String(),
        'organizations': organizations.map((item) => item.toJson()).toList(),
      };
}

/// Роль платформы вместе с тем, что она даёт и сколько человек её носят.
///
/// Контракт: Platform/Auth/PlatformRoleContracts.cs
class PlatformRoleDto {
  const PlatformRoleDto({
    required this.roleName,
    required this.displayName,
    required this.description,
    required this.isBuiltIn,
    required this.grantsAllPermissions,
    required this.permissions,
    required this.adminCount,
  });

  final String roleName;
  final String displayName;
  final String description;
  final bool isBuiltIn;
  final bool grantsAllPermissions;
  final List<String> permissions;
  final int adminCount;

  factory PlatformRoleDto.fromJson(Map<String, dynamic> json) => PlatformRoleDto(
        roleName: json['roleName'] as String,
        displayName: json['displayName'] as String,
        description: json['description'] as String,
        isBuiltIn: json['isBuiltIn'] as bool,
        grantsAllPermissions: json['grantsAllPermissions'] as bool,
        permissions: (json['permissions'] as List<dynamic>).map((item) => item as String).toList(),
        adminCount: (json['adminCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'roleName': roleName,
        'displayName': displayName,
        'description': description,
        'isBuiltIn': isBuiltIn,
        'grantsAllPermissions': grantsAllPermissions,
        'permissions': permissions.map((item) => item).toList(),
        'adminCount': adminCount,
      };
}

/// Контракт: Platform/Search/PlatformSearchResultDto.cs
class PlatformSearchResultDto {
  const PlatformSearchResultDto({
    required this.kind,
    required this.id,
    required this.title,
    required this.context,
    required this.href,
  });

  final String kind;
  final String id;
  final String title;
  final String context;
  final String href;

  factory PlatformSearchResultDto.fromJson(Map<String, dynamic> json) => PlatformSearchResultDto(
        kind: json['kind'] as String,
        id: json['id'] as String,
        title: json['title'] as String,
        context: json['context'] as String,
        href: json['href'] as String,
      );

  Map<String, dynamic> toJson() => {
        'kind': kind,
        'id': id,
        'title': title,
        'context': context,
        'href': href,
      };
}

/// Контракт: Platform/Support/PlatformSupportAccessContracts.cs
class PlatformSupportAccessGrantDto {
  const PlatformSupportAccessGrantDto({
    required this.grantId,
    required this.organizationId,
    required this.reason,
    required this.issuedAtUtc,
    required this.expiresAtUtc,
    this.revokedAtUtc,
  });

  final String grantId;
  final String organizationId;
  final String reason;
  final DateTime issuedAtUtc;
  final DateTime expiresAtUtc;
  final DateTime? revokedAtUtc;

  factory PlatformSupportAccessGrantDto.fromJson(Map<String, dynamic> json) => PlatformSupportAccessGrantDto(
        grantId: json['grantId'] as String,
        organizationId: json['organizationId'] as String,
        reason: json['reason'] as String,
        issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String),
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        revokedAtUtc: json['revokedAtUtc'] == null ? null : DateTime.parse(json['revokedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'grantId': grantId,
        'organizationId': organizationId,
        'reason': reason,
        'issuedAtUtc': issuedAtUtc.toIso8601String(),
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'revokedAtUtc': revokedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Platform/Support/PlatformSupportAccessContracts.cs
class PlatformSupportAccessGrantIssue {
  const PlatformSupportAccessGrantIssue({
    required this.grant,
    required this.ticket,
    required this.adminUrl,
  });

  final PlatformSupportAccessGrantDto grant;
  final String ticket;
  final String adminUrl;

  factory PlatformSupportAccessGrantIssue.fromJson(Map<String, dynamic> json) => PlatformSupportAccessGrantIssue(
        grant: PlatformSupportAccessGrantDto.fromJson(json['grant'] as Map<String, dynamic>),
        ticket: json['ticket'] as String,
        adminUrl: json['adminUrl'] as String,
      );

  Map<String, dynamic> toJson() => {
        'grant': grant.toJson(),
        'ticket': ticket,
        'adminUrl': adminUrl,
      };
}

/// Живой доступ в клуб, каким его видит тот, кто решает — оставить или оборвать. Отдельно от
/// PlatformSupportAccessGrantDto: чтобы понять, кого обрывать, нужно имя выдавшего (Guid ничего не
/// говорит), а чтобы понять, стоит ли, — вошёл ли он вообще. Невостребованный билет означает, что
/// внутрь никто не заходил.
///
/// Контракт: Platform/Support/PlatformSupportAccessContracts.cs
class PlatformSupportAccessGrantListItem {
  const PlatformSupportAccessGrantListItem({
    required this.grantId,
    required this.organizationId,
    required this.reason,
    required this.issuedAtUtc,
    required this.expiresAtUtc,
    required this.platformAdminUserId,
    required this.platformAdminDisplayName,
    this.enteredAtUtc,
  });

  final String grantId;
  final String organizationId;
  final String reason;
  final DateTime issuedAtUtc;
  final DateTime expiresAtUtc;
  final String platformAdminUserId;
  final String platformAdminDisplayName;
  final DateTime? enteredAtUtc;

  factory PlatformSupportAccessGrantListItem.fromJson(Map<String, dynamic> json) => PlatformSupportAccessGrantListItem(
        grantId: json['grantId'] as String,
        organizationId: json['organizationId'] as String,
        reason: json['reason'] as String,
        issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String),
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        platformAdminUserId: json['platformAdminUserId'] as String,
        platformAdminDisplayName: json['platformAdminDisplayName'] as String,
        enteredAtUtc: json['enteredAtUtc'] == null ? null : DateTime.parse(json['enteredAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'grantId': grantId,
        'organizationId': organizationId,
        'reason': reason,
        'issuedAtUtc': issuedAtUtc.toIso8601String(),
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'platformAdminUserId': platformAdminUserId,
        'platformAdminDisplayName': platformAdminDisplayName,
        'enteredAtUtc': enteredAtUtc?.toIso8601String(),
      };
}

/// The organization-admin shell is built around branches (it cannot render without at least one) —
/// support needs to see the same list a club's own staff would, not just the organization name.
///
/// Контракт: Platform/Support/PlatformSupportAccessContracts.cs
class PlatformSupportSessionBranchDto {
  const PlatformSupportSessionBranchDto({
    required this.branchId,
    required this.name,
  });

  final String branchId;
  final String name;

  factory PlatformSupportSessionBranchDto.fromJson(Map<String, dynamic> json) => PlatformSupportSessionBranchDto(
        branchId: json['branchId'] as String,
        name: json['name'] as String,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'name': name,
      };
}

/// Контракт: Platform/Support/PlatformSupportAccessContracts.cs
class PlatformSupportSessionDto {
  const PlatformSupportSessionDto({
    required this.sessionToken,
    required this.organizationId,
    required this.organizationName,
    required this.reason,
    required this.expiresAtUtc,
    required this.writableAreas,
    required this.branches,
  });

  final String sessionToken;
  final String organizationId;
  final String organizationName;
  final String reason;
  final DateTime expiresAtUtc;
  final List<String> writableAreas;
  final List<PlatformSupportSessionBranchDto> branches;

  factory PlatformSupportSessionDto.fromJson(Map<String, dynamic> json) => PlatformSupportSessionDto(
        sessionToken: json['sessionToken'] as String,
        organizationId: json['organizationId'] as String,
        organizationName: json['organizationName'] as String,
        reason: json['reason'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        writableAreas: (json['writableAreas'] as List<dynamic>).map((item) => item as String).toList(),
        branches: (json['branches'] as List<dynamic>).map((item) => PlatformSupportSessionBranchDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'sessionToken': sessionToken,
        'organizationId': organizationId,
        'organizationName': organizationName,
        'reason': reason,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'writableAreas': writableAreas.map((item) => item).toList(),
        'branches': branches.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Platform/Updates/PlatformUpdateContracts.cs
class PlatformUpdatePackageDto {
  const PlatformUpdatePackageDto({
    required this.updatePackageId,
    required this.component,
    required this.version,
    required this.channel,
    required this.artifactUri,
    required this.sha256,
    required this.signature,
    required this.signatureAlgorithm,
    required this.sizeBytes,
    required this.state,
    required this.releaseNotes,
    required this.createdByPlatformAdminUserId,
    required this.createdAtUtc,
    this.validatedByPlatformAdminUserId,
    this.validatedAtUtc,
    this.retiredAtUtc,
  });

  final String updatePackageId;
  final String component;
  final String version;
  final String channel;
  final String artifactUri;
  final String sha256;
  final String signature;
  final String signatureAlgorithm;
  final int sizeBytes;
  final String state;
  final String releaseNotes;
  final String createdByPlatformAdminUserId;
  final DateTime createdAtUtc;
  final String? validatedByPlatformAdminUserId;
  final DateTime? validatedAtUtc;
  final DateTime? retiredAtUtc;

  factory PlatformUpdatePackageDto.fromJson(Map<String, dynamic> json) => PlatformUpdatePackageDto(
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        artifactUri: json['artifactUri'] as String,
        sha256: json['sha256'] as String,
        signature: json['signature'] as String,
        signatureAlgorithm: json['signatureAlgorithm'] as String,
        sizeBytes: (json['sizeBytes'] as num).toInt(),
        state: json['state'] as String,
        releaseNotes: json['releaseNotes'] as String,
        createdByPlatformAdminUserId: json['createdByPlatformAdminUserId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        validatedByPlatformAdminUserId: json['validatedByPlatformAdminUserId'] == null ? null : json['validatedByPlatformAdminUserId'] as String,
        validatedAtUtc: json['validatedAtUtc'] == null ? null : DateTime.parse(json['validatedAtUtc'] as String),
        retiredAtUtc: json['retiredAtUtc'] == null ? null : DateTime.parse(json['retiredAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'updatePackageId': updatePackageId,
        'component': component,
        'version': version,
        'channel': channel,
        'artifactUri': artifactUri,
        'sha256': sha256,
        'signature': signature,
        'signatureAlgorithm': signatureAlgorithm,
        'sizeBytes': sizeBytes,
        'state': state,
        'releaseNotes': releaseNotes,
        'createdByPlatformAdminUserId': createdByPlatformAdminUserId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'validatedByPlatformAdminUserId': validatedByPlatformAdminUserId,
        'validatedAtUtc': validatedAtUtc?.toIso8601String(),
        'retiredAtUtc': retiredAtUtc?.toIso8601String(),
      };
}

/// Контракт: Platform/Updates/PlatformUpdateContracts.cs
class PlatformUpdateRolloutDto {
  const PlatformUpdateRolloutDto({
    required this.updateRolloutId,
    required this.updatePackageId,
    required this.component,
    required this.version,
    required this.channel,
    required this.state,
    required this.targetKind,
    required this.organizationIds,
    required this.branchIds,
    required this.deviceIds,
    required this.batchPercent,
    required this.reason,
    required this.createdByPlatformAdminUserId,
    required this.createdAtUtc,
    required this.startsAtUtc,
    this.completedAtUtc,
  });

  final String updateRolloutId;
  final String updatePackageId;
  final String component;
  final String version;
  final String channel;
  final String state;
  final String targetKind;
  final List<String> organizationIds;
  final List<String> branchIds;
  final List<String> deviceIds;
  final int batchPercent;
  final String reason;
  final String createdByPlatformAdminUserId;
  final DateTime createdAtUtc;
  final DateTime startsAtUtc;
  final DateTime? completedAtUtc;

  factory PlatformUpdateRolloutDto.fromJson(Map<String, dynamic> json) => PlatformUpdateRolloutDto(
        updateRolloutId: json['updateRolloutId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        state: json['state'] as String,
        targetKind: json['targetKind'] as String,
        organizationIds: (json['organizationIds'] as List<dynamic>).map((item) => item as String).toList(),
        branchIds: (json['branchIds'] as List<dynamic>).map((item) => item as String).toList(),
        deviceIds: (json['deviceIds'] as List<dynamic>).map((item) => item as String).toList(),
        batchPercent: (json['batchPercent'] as num).toInt(),
        reason: json['reason'] as String,
        createdByPlatformAdminUserId: json['createdByPlatformAdminUserId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        completedAtUtc: json['completedAtUtc'] == null ? null : DateTime.parse(json['completedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'updateRolloutId': updateRolloutId,
        'updatePackageId': updatePackageId,
        'component': component,
        'version': version,
        'channel': channel,
        'state': state,
        'targetKind': targetKind,
        'organizationIds': organizationIds.map((item) => item).toList(),
        'branchIds': branchIds.map((item) => item).toList(),
        'deviceIds': deviceIds.map((item) => item).toList(),
        'batchPercent': batchPercent,
        'reason': reason,
        'createdByPlatformAdminUserId': createdByPlatformAdminUserId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'completedAtUtc': completedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Billing/PlayerAccountDto.cs
class PlayerAccountDto {
  const PlayerAccountDto({
    required this.playerAccountId,
    required this.organizationId,
    required this.homeBranchId,
    required this.displayName,
    this.phoneNumber,
    required this.isActive,
    required this.createdAtUtc,
    this.platformPersonId,
    this.createdFromApp,
  });

  final String playerAccountId;
  final String organizationId;
  final String homeBranchId;
  final String displayName;
  final String? phoneNumber;
  final bool isActive;
  final DateTime createdAtUtc;

  /// Личность за карточкой — то, чем оператор спрашивает сеть про знакомого ему человека, не
  /// диктуя его телефон в запись аудита. Null — нормальный случай: карточку завели на стойке,
  /// и никакой личности за ней пока нет.
  final String? platformPersonId;

  /// Карточка завелась сама, первым действием игрока из приложения. Список клиентов растёт без
  /// участия стойки, и это единственное, чем ей объяснить незнакомую строку.
  final bool? createdFromApp;

  factory PlayerAccountDto.fromJson(Map<String, dynamic> json) => PlayerAccountDto(
        playerAccountId: json['playerAccountId'] as String,
        organizationId: json['organizationId'] as String,
        homeBranchId: json['homeBranchId'] as String,
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        isActive: json['isActive'] as bool,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        platformPersonId: json['platformPersonId'] == null ? null : json['platformPersonId'] as String,
        createdFromApp: json['createdFromApp'] == null ? null : json['createdFromApp'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'organizationId': organizationId,
        'homeBranchId': homeBranchId,
        'displayName': displayName,
        'phoneNumber': phoneNumber,
        'isActive': isActive,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'platformPersonId': platformPersonId,
        'createdFromApp': createdFromApp,
      };
}

/// Одно достижение: код, порог и насколько игрок к нему подошёл. Прогресс виден и до
/// получения — иначе список выглядит как набор запертых дверей без замочных скважин.
///
/// Контракт: Players/PlayerAchievementsDto.cs
class PlayerAchievementDto {
  const PlayerAchievementDto({
    required this.code,
    required this.progress,
    required this.target,
    this.unlockedAtUtc,
  });

  final String code;
  final int progress;
  final int target;
  final DateTime? unlockedAtUtc;

  factory PlayerAchievementDto.fromJson(Map<String, dynamic> json) => PlayerAchievementDto(
        code: json['code'] as String,
        progress: (json['progress'] as num).toInt(),
        target: (json['target'] as num).toInt(),
        unlockedAtUtc: json['unlockedAtUtc'] == null ? null : DateTime.parse(json['unlockedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'code': code,
        'progress': progress,
        'target': target,
        'unlockedAtUtc': unlockedAtUtc?.toIso8601String(),
      };
}

/// Игровой стаж как его видит игрок: уровень, часы за ПК и список достижений.
/// Названия достижений сюда не попадают — только коды: подписи живут в приложении, где у них
/// есть три языка. Сервер, который присылал бы «Ночной житель» строкой, говорил бы с игроком
/// на языке базы данных.
///
/// Контракт: Players/PlayerAchievementsDto.cs
class PlayerAchievementsDto {
  const PlayerAchievementsDto({
    required this.level,
    required this.visitCount,
    required this.playedMinutes,
    this.minutesToNextLevel,
    required this.achievements,
  });

  final int level;
  final int visitCount;
  final int playedMinutes;

  /// Сколько минут до следующего уровня; null — уровень последний.
  final int? minutesToNextLevel;
  final List<PlayerAchievementDto> achievements;

  factory PlayerAchievementsDto.fromJson(Map<String, dynamic> json) => PlayerAchievementsDto(
        level: (json['level'] as num).toInt(),
        visitCount: (json['visitCount'] as num).toInt(),
        playedMinutes: (json['playedMinutes'] as num).toInt(),
        minutesToNextLevel: json['minutesToNextLevel'] == null ? null : (json['minutesToNextLevel'] as num).toInt(),
        achievements: (json['achievements'] as List<dynamic>).map((item) => PlayerAchievementDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'level': level,
        'visitCount': visitCount,
        'playedMinutes': playedMinutes,
        'minutesToNextLevel': minutesToNextLevel,
        'achievements': achievements.map((item) => item.toJson()).toList(),
      };
}

/// Правила брони этого филиала для этого игрока — то, чем приложение объясняет «так решил клуб».
/// Всё посчитано сервером под конкретного человека: предоплата нужна именно ему, потолок броней
/// именно его. Ни одного поля про других игроков здесь нет и быть не должно — иначе приложение
/// одного клуба становится окном в клиентскую базу.
/// <param name="MaxActiveReservations">
/// Пусто — значит потолка нет: игрок в этом филиале уже свой.
/// </param>
///
/// Контракт: Reservations/PlayerBookingRulesDto.cs
class PlayerBookingRulesDto {
  const PlayerBookingRulesDto({
    required this.branchId,
    required this.acceptanceMode,
    required this.respondWithinMinutes,
    required this.prepaymentRequired,
    required this.activeReservations,
    this.maxActiveReservations,
    required this.holdSeatAfterStartMinutes,
  });

  final String branchId;

  /// `auto` — клуб подтверждает сам, `manual` — заявку смотрит администратор, `off` — брони из
  /// приложения не принимаются.
  final String acceptanceMode;
  final int respondWithinMinutes;
  final bool prepaymentRequired;
  final int activeReservations;
  final int? maxActiveReservations;
  final int holdSeatAfterStartMinutes;

  factory PlayerBookingRulesDto.fromJson(Map<String, dynamic> json) => PlayerBookingRulesDto(
        branchId: json['branchId'] as String,
        acceptanceMode: json['acceptanceMode'] as String,
        respondWithinMinutes: (json['respondWithinMinutes'] as num).toInt(),
        prepaymentRequired: json['prepaymentRequired'] as bool,
        activeReservations: (json['activeReservations'] as num).toInt(),
        maxActiveReservations: json['maxActiveReservations'] == null ? null : (json['maxActiveReservations'] as num).toInt(),
        holdSeatAfterStartMinutes: (json['holdSeatAfterStartMinutes'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'acceptanceMode': acceptanceMode,
        'respondWithinMinutes': respondWithinMinutes,
        'prepaymentRequired': prepaymentRequired,
        'activeReservations': activeReservations,
        'maxActiveReservations': maxActiveReservations,
        'holdSeatAfterStartMinutes': holdSeatAfterStartMinutes,
      };
}

/// Ответ на просьбу прислать код: сколько он живёт и когда можно просить следующий. Общий для
/// входа-регистрации и для подтверждения номера в профиле.
///
/// Контракт: Players/PlayerPhoneVerificationContracts.cs
class PlayerCodeSignInStartedResponse {
  const PlayerCodeSignInStartedResponse({
    required this.expiresInSeconds,
    required this.resendAfterSeconds,
  });

  final int expiresInSeconds;
  final int resendAfterSeconds;

  factory PlayerCodeSignInStartedResponse.fromJson(Map<String, dynamic> json) => PlayerCodeSignInStartedResponse(
        expiresInSeconds: (json['expiresInSeconds'] as num).toInt(),
        resendAfterSeconds: (json['resendAfterSeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'expiresInSeconds': expiresInSeconds,
        'resendAfterSeconds': resendAfterSeconds,
      };
}

/// Главный экран игрока: три числа кошелька и текущая сессия, если она идёт.
/// HeldBalance — придержанное под брони; из WalletBalance оно
/// уже вычтено, и это ответ на вопрос «а куда делись мои деньги», а не четвёртое место их хранения.
///
/// Контракт: Players/PlayerDashboardDto.cs
class PlayerDashboardDto {
  const PlayerDashboardDto({
    required this.walletBalance,
    required this.heldBalance,
    required this.debtBalance,
    this.activeSession,
  });

  final MoneyDto walletBalance;
  final MoneyDto heldBalance;
  final MoneyDto debtBalance;
  final ActiveSessionDto? activeSession;

  factory PlayerDashboardDto.fromJson(Map<String, dynamic> json) => PlayerDashboardDto(
        walletBalance: MoneyDto.fromJson(json['walletBalance'] as Map<String, dynamic>),
        heldBalance: MoneyDto.fromJson(json['heldBalance'] as Map<String, dynamic>),
        debtBalance: MoneyDto.fromJson(json['debtBalance'] as Map<String, dynamic>),
        activeSession: json['activeSession'] == null ? null : ActiveSessionDto.fromJson(json['activeSession'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'walletBalance': walletBalance.toJson(),
        'heldBalance': heldBalance.toJson(),
        'debtBalance': debtBalance.toJson(),
        'activeSession': activeSession?.toJson(),
      };
}

/// Гашение долга игроком с собственного кошелька. Сумма приходит явно, а не «весь долг»: человек
/// вправе закрыть часть, а «весь» на момент нажатия и на момент записи — это разные числа.
///
/// Контракт: Players/PlayerDebtPaymentContracts.cs
class PlayerDebtPaymentRequest {
  const PlayerDebtPaymentRequest({
    required this.amount,
    required this.idempotencyKey,
  });

  final MoneyDto amount;
  final String idempotencyKey;

  factory PlayerDebtPaymentRequest.fromJson(Map<String, dynamic> json) => PlayerDebtPaymentRequest(
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'amount': amount.toJson(),
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Players/PlayerOfferContracts.cs
class PlayerDurationOfferDto {
  const PlayerDurationOfferDto({
    required this.minutes,
    required this.billableMinutes,
    required this.endsAtUtc,
    required this.amount,
    required this.balanceAfter,
    required this.affordable,
  });


  /// Сколько времени берёт игрок.
  final int minutes;

  /// Сколько минут будет оплачено: минимум и шаг округления тарифа уже применены.
  final int billableMinutes;
  final DateTime endsAtUtc;
  final MoneyDto amount;
  final MoneyDto balanceAfter;
  final bool affordable;

  factory PlayerDurationOfferDto.fromJson(Map<String, dynamic> json) => PlayerDurationOfferDto(
        minutes: (json['minutes'] as num).toInt(),
        billableMinutes: (json['billableMinutes'] as num).toInt(),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        balanceAfter: MoneyDto.fromJson(json['balanceAfter'] as Map<String, dynamic>),
        affordable: json['affordable'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'minutes': minutes,
        'billableMinutes': billableMinutes,
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'amount': amount.toJson(),
        'balanceAfter': balanceAfter.toJson(),
        'affordable': affordable,
      };
}

/// «Сколько вернётся, если встать сейчас» — до нажатия. Тот же расчёт, что у самого выхода: экран
/// не обещает одну сумму, чтобы вернуть другую.
///
/// Контракт: Players/PlayerSelfEndSessionContracts.cs
class PlayerEndQuoteDto {
  const PlayerEndQuoteDto({
    required this.billedMinutes,
    required this.refund,
    required this.packageMinutesReturned,
  });


  /// Сколько минут будет списано: сыгранное за вычетом пауз, с правилами тарифа.
  final int billedMinutes;
  final MoneyDto refund;
  final int packageMinutesReturned;

  factory PlayerEndQuoteDto.fromJson(Map<String, dynamic> json) => PlayerEndQuoteDto(
        billedMinutes: (json['billedMinutes'] as num).toInt(),
        refund: MoneyDto.fromJson(json['refund'] as Map<String, dynamic>),
        packageMinutesReturned: (json['packageMinutesReturned'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'billedMinutes': billedMinutes,
        'refund': refund.toJson(),
        'packageMinutesReturned': packageMinutesReturned,
      };
}

/// Чем можно продлить идущую сессию — по тарифу, на котором она началась.
///
/// Контракт: Players/PlayerOfferContracts.cs
class PlayerExtendOffersDto {
  const PlayerExtendOffersDto({
    required this.sessionId,
    required this.balance,
    required this.options,
    this.unavailableReason,
  });

  final String sessionId;
  final MoneyDto balance;
  final List<PlayerDurationOfferDto> options;

  /// Одно из PlayerOfferUnavailableReasonNames; пусто, если продлить можно.
  final String? unavailableReason;

  factory PlayerExtendOffersDto.fromJson(Map<String, dynamic> json) => PlayerExtendOffersDto(
        sessionId: json['sessionId'] as String,
        balance: MoneyDto.fromJson(json['balance'] as Map<String, dynamic>),
        options: (json['options'] as List<dynamic>).map((item) => PlayerDurationOfferDto.fromJson(item as Map<String, dynamic>)).toList(),
        unavailableReason: json['unavailableReason'] == null ? null : json['unavailableReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'balance': balance.toJson(),
        'options': options.map((item) => item.toJson()).toList(),
        'unavailableReason': unavailableReason,
      };
}

/// Строка выписки глазами игрока: что случилось с его деньгами и когда.
/// Не то же самое, что LedgerEntryDto у стойки, и не должно им быть: там есть
/// табельный номер проведшего сотрудника и служебная причина вида
/// `reservation_hold:{guid}`. Оператору это нужно — он разбирает спор; игроку это чужая
/// внутренняя кухня, которой в его выписке взяться неоткуда.
/// <param name="EntryType">
/// Что произошло, кодом из LedgerEntryTypeNames. Приложение называет его словами на
/// языке человека — текст с сервера был бы на языке сервера.
/// </param>
/// <param name="QuantitySeconds">
/// Сколько времени принесла или забрала запись: у пакетов и бонусных часов деньги — не вся правда.
/// Ноль у обычных денежных строк.
/// </param>
/// <param name="ReceiptSessionId">
/// Визит, чеком которого объясняется эта строка. Пусто, когда объяснять нечем: у записи нет
/// сессии или по сессии не выбит чек. Без него «Списание за игру −45 с.» — тупик: сумма есть,
/// а из чего она сложилась, видно только в другой вкладке и только по времени на глаз.
/// </param>
/// <param name="HoldReleaseCause">
/// Почему вернулись деньги, придержанные под бронь, — только у строки, которая снимает такое
/// удержание: `seated` (бронь началась, дальше считает сессия), `cancelled`,
/// `rejected`, `request_expired`, `no_show`, `moved`. Без повода строка
/// читалась бы «Отмена операции +15 с.» — и человек не знал бы, что именно отменили.
/// </param>
/// <param name="WalletBalanceAfter">
/// Сколько осталось на кошельке сразу после этой строки. Пусто у строк не про кошелёк — пакетное
/// и бонусное время, долг: они остаток не двигают. Сходится с балансом наверху экрана, потому что
/// удержание под бронь в выписке тоже есть — прятать его значило бы получить остаток, который не
/// складывается из видимых строк.
/// </param>
///
/// Контракт: Players/PlayerLedgerEntryDto.cs
class PlayerLedgerEntryDto {
  const PlayerLedgerEntryDto({
    required this.ledgerEntryId,
    required this.entryType,
    required this.amount,
    required this.quantitySeconds,
    required this.createdAtUtc,
    this.receiptSessionId,
    this.holdReleaseCause,
    this.walletBalanceAfter,
  });

  final String ledgerEntryId;
  final String entryType;
  final MoneyDto amount;
  final int quantitySeconds;
  final DateTime createdAtUtc;
  final String? receiptSessionId;
  final String? holdReleaseCause;
  final MoneyDto? walletBalanceAfter;

  factory PlayerLedgerEntryDto.fromJson(Map<String, dynamic> json) => PlayerLedgerEntryDto(
        ledgerEntryId: json['ledgerEntryId'] as String,
        entryType: json['entryType'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        quantitySeconds: (json['quantitySeconds'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        receiptSessionId: json['receiptSessionId'] == null ? null : json['receiptSessionId'] as String,
        holdReleaseCause: json['holdReleaseCause'] == null ? null : json['holdReleaseCause'] as String,
        walletBalanceAfter: json['walletBalanceAfter'] == null ? null : MoneyDto.fromJson(json['walletBalanceAfter'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'ledgerEntryId': ledgerEntryId,
        'entryType': entryType,
        'amount': amount.toJson(),
        'quantitySeconds': quantitySeconds,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'receiptSessionId': receiptSessionId,
        'holdReleaseCause': holdReleaseCause,
        'walletBalanceAfter': walletBalanceAfter?.toJson(),
      };
}

/// Кешбэк игрока: сколько накоплено и по каким правилам начисляется.
/// Кешбэк — не баллы: он приходит на кошелёк обычными деньгами, и тратится так же.
///
/// Контракт: Loyalty/PlayerLoyaltyDto.cs
class PlayerLoyaltyDto {
  const PlayerLoyaltyDto({
    required this.topUpEnabled,
    required this.topUpPercentBasisPoints,
    required this.shopEnabled,
    required this.shopPercentBasisPoints,
    required this.sessionEnabled,
    required this.sessionPercentBasisPoints,
    required this.totalEarned,
    required this.recent,
  });

  final bool topUpEnabled;
  final int topUpPercentBasisPoints;
  final bool shopEnabled;
  final int shopPercentBasisPoints;
  final bool sessionEnabled;
  final int sessionPercentBasisPoints;
  final MoneyDto totalEarned;
  final List<CashbackEntryDto> recent;

  factory PlayerLoyaltyDto.fromJson(Map<String, dynamic> json) => PlayerLoyaltyDto(
        topUpEnabled: json['topUpEnabled'] as bool,
        topUpPercentBasisPoints: (json['topUpPercentBasisPoints'] as num).toInt(),
        shopEnabled: json['shopEnabled'] as bool,
        shopPercentBasisPoints: (json['shopPercentBasisPoints'] as num).toInt(),
        sessionEnabled: json['sessionEnabled'] as bool,
        sessionPercentBasisPoints: (json['sessionPercentBasisPoints'] as num).toInt(),
        totalEarned: MoneyDto.fromJson(json['totalEarned'] as Map<String, dynamic>),
        recent: (json['recent'] as List<dynamic>).map((item) => CashbackEntryDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'topUpEnabled': topUpEnabled,
        'topUpPercentBasisPoints': topUpPercentBasisPoints,
        'shopEnabled': shopEnabled,
        'shopPercentBasisPoints': shopPercentBasisPoints,
        'sessionEnabled': sessionEnabled,
        'sessionPercentBasisPoints': sessionPercentBasisPoints,
        'totalEarned': totalEarned.toJson(),
        'recent': recent.map((item) => item.toJson()).toList(),
      };
}

/// Новость или акция клуба.
///
/// Контракт: News/PlayerNewsItemDto.cs
class PlayerNewsItemDto {
  const PlayerNewsItemDto({
    required this.id,
    required this.title,
    required this.body,
    this.imageUrl,
    required this.publishedAtUtc,
  });

  final String id;
  final String title;
  final String body;
  final String? imageUrl;
  final DateTime publishedAtUtc;

  factory PlayerNewsItemDto.fromJson(Map<String, dynamic> json) => PlayerNewsItemDto(
        id: json['id'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
        publishedAtUtc: DateTime.parse(json['publishedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'title': title,
        'body': body,
        'imageUrl': imageUrl,
        'publishedAtUtc': publishedAtUtc.toIso8601String(),
      };
}

/// Уведомление, каким его видит игрок в приложении.
/// Берётся из той же очереди, что и пуш: отдельного хранилища у центра уведомлений нет и не нужно —
/// текст уже отрисован и сохранён там, где сообщение ставилось в отправку. Поэтому список
/// показывает и то, что до телефона не доехало: пуш, потерянный из-за выключенных уведомлений или
/// переустановленного приложения, до сих пор было невозможно прочитать нигде.
///
/// Контракт: Notifications/PlayerNotificationContracts.cs
class PlayerNotificationDto {
  const PlayerNotificationDto({
    required this.notificationId,
    required this.templateKey,
    required this.subject,
    required this.body,
    this.branchId,
    required this.createdAtUtc,
    required this.isUnread,
  });

  final String notificationId;

  /// Служебное имя события (`player.order_ready` и подобные). Приложение по нему ставит значок —
  /// показывать его человеку незачем.
  final String templateKey;
  final String subject;
  final String body;
  final String? branchId;
  final DateTime createdAtUtc;
  final bool isUnread;

  factory PlayerNotificationDto.fromJson(Map<String, dynamic> json) => PlayerNotificationDto(
        notificationId: json['notificationId'] as String,
        templateKey: json['templateKey'] as String,
        subject: json['subject'] as String,
        body: json['body'] as String,
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        isUnread: json['isUnread'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'notificationId': notificationId,
        'templateKey': templateKey,
        'subject': subject,
        'body': body,
        'branchId': branchId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'isUnread': isUnread,
      };
}

/// Контракт: Notifications/PlayerNotificationContracts.cs
class PlayerNotificationsDto {
  const PlayerNotificationsDto({
    required this.notifications,
    required this.unreadCount,
  });

  final List<PlayerNotificationDto> notifications;
  final int unreadCount;

  factory PlayerNotificationsDto.fromJson(Map<String, dynamic> json) => PlayerNotificationsDto(
        notifications: (json['notifications'] as List<dynamic>).map((item) => PlayerNotificationDto.fromJson(item as Map<String, dynamic>)).toList(),
        unreadCount: (json['unreadCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'notifications': notifications.map((item) => item.toJson()).toList(),
        'unreadCount': unreadCount,
      };
}

/// Купленный пакет с остатком времени.
///
/// Контракт: Packages/PlayerPackageDto.cs
class PlayerPackageDto {
  const PlayerPackageDto({
    required this.playerPackageId,
    required this.packageDefinitionId,
    required this.playerAccountId,
    required this.name,
    required this.purchasedPrice,
    required this.includedSeconds,
    required this.bonusSeconds,
    required this.remainingIncludedSeconds,
    required this.remainingBonusSeconds,
    required this.purchasedAtUtc,
    this.expiresAtUtc,
  });

  final String playerPackageId;
  final String packageDefinitionId;
  final String playerAccountId;
  final String name;
  final MoneyDto purchasedPrice;
  final int includedSeconds;
  final int bonusSeconds;
  final int remainingIncludedSeconds;
  final int remainingBonusSeconds;
  final DateTime purchasedAtUtc;
  final DateTime? expiresAtUtc;

  factory PlayerPackageDto.fromJson(Map<String, dynamic> json) => PlayerPackageDto(
        playerPackageId: json['playerPackageId'] as String,
        packageDefinitionId: json['packageDefinitionId'] as String,
        playerAccountId: json['playerAccountId'] as String,
        name: json['name'] as String,
        purchasedPrice: MoneyDto.fromJson(json['purchasedPrice'] as Map<String, dynamic>),
        includedSeconds: (json['includedSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num).toInt(),
        remainingIncludedSeconds: (json['remainingIncludedSeconds'] as num).toInt(),
        remainingBonusSeconds: (json['remainingBonusSeconds'] as num).toInt(),
        purchasedAtUtc: DateTime.parse(json['purchasedAtUtc'] as String),
        expiresAtUtc: json['expiresAtUtc'] == null ? null : DateTime.parse(json['expiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'playerPackageId': playerPackageId,
        'packageDefinitionId': packageDefinitionId,
        'playerAccountId': playerAccountId,
        'name': name,
        'purchasedPrice': purchasedPrice.toJson(),
        'includedSeconds': includedSeconds,
        'bonusSeconds': bonusSeconds,
        'remainingIncludedSeconds': remainingIncludedSeconds,
        'remainingBonusSeconds': remainingBonusSeconds,
        'purchasedAtUtc': purchasedAtUtc.toIso8601String(),
        'expiresAtUtc': expiresAtUtc?.toIso8601String(),
      };
}

/// Контракт: Players/PlayerOfferContracts.cs
class PlayerPackageOfferDto {
  const PlayerPackageOfferDto({
    required this.playerPackageId,
    required this.name,
    required this.remainingMinutes,
    this.expiresAtUtc,
  });

  final String playerPackageId;
  final String name;
  final int remainingMinutes;
  final DateTime? expiresAtUtc;

  factory PlayerPackageOfferDto.fromJson(Map<String, dynamic> json) => PlayerPackageOfferDto(
        playerPackageId: json['playerPackageId'] as String,
        name: json['name'] as String,
        remainingMinutes: (json['remainingMinutes'] as num).toInt(),
        expiresAtUtc: json['expiresAtUtc'] == null ? null : DateTime.parse(json['expiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'playerPackageId': playerPackageId,
        'name': name,
        'remainingMinutes': remainingMinutes,
        'expiresAtUtc': expiresAtUtc?.toIso8601String(),
      };
}

/// Контракт: Players/PlayerPhoneVerificationContracts.cs
class PlayerPhoneConfirmedResponse {
  const PlayerPhoneConfirmedResponse({
    required this.phone,
  });

  final String phone;

  factory PlayerPhoneConfirmedResponse.fromJson(Map<String, dynamic> json) => PlayerPhoneConfirmedResponse(
        phone: json['phone'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phone': phone,
      };
}

/// Контракт: Players/PlayerPhoneVerificationContracts.cs
class PlayerPhoneConfirmRequest {
  const PlayerPhoneConfirmRequest({
    required this.code,
  });

  final String code;

  factory PlayerPhoneConfirmRequest.fromJson(Map<String, dynamic> json) => PlayerPhoneConfirmRequest(
        code: json['code'] as String,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
      };
}

/// Asks for a code to be sent to Phone — the number the player claims.
///
/// Контракт: Players/PlayerPhoneVerificationContracts.cs
class PlayerPhoneStartVerificationRequest {
  const PlayerPhoneStartVerificationRequest({
    required this.phone,
  });

  final String phone;

  factory PlayerPhoneStartVerificationRequest.fromJson(Map<String, dynamic> json) => PlayerPhoneStartVerificationRequest(
        phone: json['phone'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phone': phone,
      };
}

/// Контракт: Players/PlayerPhoneVerificationContracts.cs
class PlayerPhoneVerificationStartedResponse {
  const PlayerPhoneVerificationStartedResponse({
    required this.expiresInSeconds,
    required this.resendAfterSeconds,
  });

  final int expiresInSeconds;
  final int resendAfterSeconds;

  factory PlayerPhoneVerificationStartedResponse.fromJson(Map<String, dynamic> json) => PlayerPhoneVerificationStartedResponse(
        expiresInSeconds: (json['expiresInSeconds'] as num).toInt(),
        resendAfterSeconds: (json['resendAfterSeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'expiresInSeconds': expiresInSeconds,
        'resendAfterSeconds': resendAfterSeconds,
      };
}

/// Профиль игрока: как его зовут, чем он подписан и что он разрешил присылать.
/// HomeBranchId is what lets the app ask for the club's price list at all: the catalog endpoints are
/// per-branch, and until now the player had no way to learn which branch the account belongs to —
/// the server resolved it silently on every write. The name comes along so the app can say where it
/// is booking without a second round-trip.
///
/// Контракт: Players/PlayerProfileDto.cs
class PlayerProfileDto {
  const PlayerProfileDto({
    required this.playerAccountId,
    required this.displayName,
    this.phoneNumber,
    required this.phoneVerified,
    this.preferredLocale,
    required this.marketingOptIn,
    this.homeBranchId,
    this.homeBranchName,
  });

  final String playerAccountId;
  final String displayName;
  final String? phoneNumber;
  final bool phoneVerified;

  /// Пусто — игрок не выбирал язык, и письма идут на языке клуба.
  final String? preferredLocale;
  final bool marketingOptIn;
  final String? homeBranchId;
  final String? homeBranchName;

  factory PlayerProfileDto.fromJson(Map<String, dynamic> json) => PlayerProfileDto(
        playerAccountId: json['playerAccountId'] as String,
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        phoneVerified: json['phoneVerified'] as bool,
        preferredLocale: json['preferredLocale'] == null ? null : json['preferredLocale'] as String,
        marketingOptIn: json['marketingOptIn'] as bool,
        homeBranchId: json['homeBranchId'] == null ? null : json['homeBranchId'] as String,
        homeBranchName: json['homeBranchName'] == null ? null : json['homeBranchName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'displayName': displayName,
        'phoneNumber': phoneNumber,
        'phoneVerified': phoneVerified,
        'preferredLocale': preferredLocale,
        'marketingOptIn': marketingOptIn,
        'homeBranchId': homeBranchId,
        'homeBranchName': homeBranchName,
      };
}

/// Покупка в баре: когда, что и на сколько.
///
/// Контракт: Players/PlayerPurchaseDto.cs
class PlayerPurchaseDto {
  const PlayerPurchaseDto({
    required this.posSaleId,
    required this.createdAtUtc,
    required this.totalMinorUnits,
    required this.currencyCode,
    required this.lines,
  });

  final String posSaleId;
  final DateTime createdAtUtc;
  final int totalMinorUnits;
  final String currencyCode;
  final List<PlayerPurchaseLineDto> lines;

  factory PlayerPurchaseDto.fromJson(Map<String, dynamic> json) => PlayerPurchaseDto(
        posSaleId: json['posSaleId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        totalMinorUnits: (json['totalMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        lines: (json['lines'] as List<dynamic>).map((item) => PlayerPurchaseLineDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'posSaleId': posSaleId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'totalMinorUnits': totalMinorUnits,
        'currencyCode': currencyCode,
        'lines': lines.map((item) => item.toJson()).toList(),
      };
}

/// Строка покупки: что, сколько и на какую сумму.
///
/// Контракт: Players/PlayerPurchaseLineDto.cs
class PlayerPurchaseLineDto {
  const PlayerPurchaseLineDto({
    required this.productName,
    required this.quantity,
    required this.unitPriceMinorUnits,
    required this.lineTotalMinorUnits,
  });

  final String productName;
  final int quantity;
  final int unitPriceMinorUnits;
  final int lineTotalMinorUnits;

  factory PlayerPurchaseLineDto.fromJson(Map<String, dynamic> json) => PlayerPurchaseLineDto(
        productName: json['productName'] as String,
        quantity: (json['quantity'] as num).toInt(),
        unitPriceMinorUnits: (json['unitPriceMinorUnits'] as num).toInt(),
        lineTotalMinorUnits: (json['lineTotalMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'productName': productName,
        'quantity': quantity,
        'unitPriceMinorUnits': unitPriceMinorUnits,
        'lineTotalMinorUnits': lineTotalMinorUnits,
      };
}

/// Экран «Приведи друга» глазами игрока: свой код, условия и что уже вышло.
/// Суммы и условия приходят с сервера, а не зашиты в приложение: их назначает клуб, и каждый
/// назначает свои.
///
/// Контракт: Loyalty/ReferralContracts.cs
class PlayerReferralDto {
  const PlayerReferralDto({
    required this.enabled,
    this.code,
    required this.referrerBonusMinorUnits,
    required this.inviteeBonusMinorUnits,
    required this.minimumTopUpMinorUnits,
    required this.currencyCode,
    required this.invitedCount,
    required this.rewardedCount,
    required this.earnedMinorUnits,
    required this.hasClaimedCode,
    required this.canClaimCode,
  });


  /// Клуб платит за приглашения. false — экран честно говорит, что программы нет.
  final bool enabled;
  final String? code;
  final int referrerBonusMinorUnits;
  final int inviteeBonusMinorUnits;
  final int minimumTopUpMinorUnits;
  final String currencyCode;
  final int invitedCount;
  final int rewardedCount;
  final int earnedMinorUnits;

  /// Игрок сам пришёл по чужому коду — второй раз назвать код нельзя.
  final bool hasClaimedCode;

  /// Назвать код ещё можно: приглашение не использовано и окно не закрылось.
  final bool canClaimCode;

  factory PlayerReferralDto.fromJson(Map<String, dynamic> json) => PlayerReferralDto(
        enabled: json['enabled'] as bool,
        code: json['code'] == null ? null : json['code'] as String,
        referrerBonusMinorUnits: (json['referrerBonusMinorUnits'] as num).toInt(),
        inviteeBonusMinorUnits: (json['inviteeBonusMinorUnits'] as num).toInt(),
        minimumTopUpMinorUnits: (json['minimumTopUpMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        invitedCount: (json['invitedCount'] as num).toInt(),
        rewardedCount: (json['rewardedCount'] as num).toInt(),
        earnedMinorUnits: (json['earnedMinorUnits'] as num).toInt(),
        hasClaimedCode: json['hasClaimedCode'] as bool,
        canClaimCode: json['canClaimCode'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'enabled': enabled,
        'code': code,
        'referrerBonusMinorUnits': referrerBonusMinorUnits,
        'inviteeBonusMinorUnits': inviteeBonusMinorUnits,
        'minimumTopUpMinorUnits': minimumTopUpMinorUnits,
        'currencyCode': currencyCode,
        'invitedCount': invitedCount,
        'rewardedCount': rewardedCount,
        'earnedMinorUnits': earnedMinorUnits,
        'hasClaimedCode': hasClaimedCode,
        'canClaimCode': canClaimCode,
      };
}

/// Контракт: Players/PlayerRefreshRequest.cs
class PlayerRefreshRequest {
  const PlayerRefreshRequest({
    required this.refreshToken,
  });

  final String refreshToken;

  factory PlayerRefreshRequest.fromJson(Map<String, dynamic> json) => PlayerRefreshRequest(
        refreshToken: json['refreshToken'] as String,
      );

  Map<String, dynamic> toJson() => {
        'refreshToken': refreshToken,
      };
}

/// Единственный факт, который сеть сообщает клубу о незнакомом госте: можно ли ему доверять.
/// Полей ровно четыре, и это граница приватности, а не текущая версия модели. Ни названия чужих
/// клубов, ни даты визитов, ни суммы, ни филиалы, ни тарифы сюда не добавляются: «скрыто в UI» —
/// не защита, операторское приложение ходит в тот же API, что и curl. Состав закреплён
/// рефлексивным тестом, чтобы пятое поле не появилось «на минутку».
/// <param name="NetworkVisits">Завершённых визитов во всей сети — точное число из суточного снимка.</param>
/// <param name="NetworkNoShows">Броней, на которые человек не приехал, — из того же снимка.</param>
/// <param name="NetworkBanned">Закрыт ли человеку вход в сеть решением платформы. Читается вживую: запрет не ждёт суток.</param>
/// <param name="CalculatedAtUtc">На какой момент посчитан снимок. Значение общее для всей сети, а не личное: по личному времени пересчёта можно было бы вычислить, когда человек играл.</param>
///
/// Контракт: Players/PlayerReputationDto.cs
class PlayerReputationDto {
  const PlayerReputationDto({
    required this.networkVisits,
    required this.networkNoShows,
    required this.networkBanned,
    required this.calculatedAtUtc,
  });

  final int networkVisits;
  final int networkNoShows;
  final bool networkBanned;
  final DateTime calculatedAtUtc;

  factory PlayerReputationDto.fromJson(Map<String, dynamic> json) => PlayerReputationDto(
        networkVisits: (json['networkVisits'] as num).toInt(),
        networkNoShows: (json['networkNoShows'] as num).toInt(),
        networkBanned: json['networkBanned'] as bool,
        calculatedAtUtc: DateTime.parse(json['calculatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'networkVisits': networkVisits,
        'networkNoShows': networkNoShows,
        'networkBanned': networkBanned,
        'calculatedAtUtc': calculatedAtUtc.toIso8601String(),
      };
}

/// Спрос репутации по точному номеру. Номер едет телом, а не в адресе: адреса оседают в логах
/// прокси и в истории браузера, а это чужой телефон.
///
/// Контракт: Players/PlayerReputationDto.cs
class PlayerReputationLookupRequest {
  const PlayerReputationLookupRequest({
    required this.phoneNumber,
  });

  final String phoneNumber;

  factory PlayerReputationLookupRequest.fromJson(Map<String, dynamic> json) => PlayerReputationLookupRequest(
        phoneNumber: json['phoneNumber'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
      };
}

/// Player-facing reservation view — no staff-only fields (no CustomerName separate from
/// context, no CreatedByStaffUserId, no UpdatedBy, no ZoneName leak).
/// The tariff and the estimated cost are the player's own choice priced by the server: the app must
/// never re-derive the amount from the price list, because the minimum-billable and rounding rules
/// live in billing and would drift the moment either side changed.
///
/// Контракт: Reservations/PlayerReservationDto.cs
class PlayerReservationDto {
  const PlayerReservationDto({
    required this.reservationId,
    this.seatId,
    this.seatName,
    required this.startsAtUtc,
    required this.endsAtUtc,
    required this.state,
    this.note,
    this.tariffVersionId,
    this.tariffName,
    this.estimatedCostMinorUnits,
    this.currencyCode,
    this.reservationGroupId,
    this.respondByUtc,
    this.rejectReasonCode,
    this.rejectReasonNote,
  });

  final String reservationId;
  final String? seatId;

  /// Пусто — клуб ещё не назначил конкретное место.
  final String? seatName;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;

  /// Отменить можно то, что ещё не состоялось: `pending` и `confirmed`. Отменённую или уже
  /// отыгранную бронь трогать нечего — кнопка там только сбивает с толку. Одно из ReservationStateNames.
  final String state;
  final String? note;
  final String? tariffVersionId;

  /// Название выбранного тарифа и стоимость, посчитанная сервером при брони. Пусто — бронь
  /// завели на стойке, там же её и посчитают.
  final String? tariffName;
  final int? estimatedCostMinorUnits;
  final String? currencyCode;

  /// Бронь на компанию: у всех мест группы он общий. Без него приложение показало бы компанию из
  /// четырёх человек четырьмя одинаковыми строками, между которыми не видно разницы.
  final String? reservationGroupId;

  /// Докуда клуб обещал ответить на заявку — по нему в приложении идёт обратный отсчёт. У
  /// подтверждённой брони его нет: отвечать больше не на что.
  final DateTime? respondByUtc;

  /// Почему клуб отказал. Код — чтобы приложение сказало это на языке игрока; слова — то, что
  /// администратор добавил от себя. Без них отказ снова стал бы молчаливым исчезновением брони.
  final String? rejectReasonCode;
  final String? rejectReasonNote;

  factory PlayerReservationDto.fromJson(Map<String, dynamic> json) => PlayerReservationDto(
        reservationId: json['reservationId'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        state: json['state'] as String,
        note: json['note'] == null ? null : json['note'] as String,
        tariffVersionId: json['tariffVersionId'] == null ? null : json['tariffVersionId'] as String,
        tariffName: json['tariffName'] == null ? null : json['tariffName'] as String,
        estimatedCostMinorUnits: json['estimatedCostMinorUnits'] == null ? null : (json['estimatedCostMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
        reservationGroupId: json['reservationGroupId'] == null ? null : json['reservationGroupId'] as String,
        respondByUtc: json['respondByUtc'] == null ? null : DateTime.parse(json['respondByUtc'] as String),
        rejectReasonCode: json['rejectReasonCode'] == null ? null : json['rejectReasonCode'] as String,
        rejectReasonNote: json['rejectReasonNote'] == null ? null : json['rejectReasonNote'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reservationId': reservationId,
        'seatId': seatId,
        'seatName': seatName,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'state': state,
        'note': note,
        'tariffVersionId': tariffVersionId,
        'tariffName': tariffName,
        'estimatedCostMinorUnits': estimatedCostMinorUnits,
        'currencyCode': currencyCode,
        'reservationGroupId': reservationGroupId,
        'respondByUtc': respondByUtc?.toIso8601String(),
        'rejectReasonCode': rejectReasonCode,
        'rejectReasonNote': rejectReasonNote,
      };
}

/// Что получилось из групповой брони: сама группа и её брони. Отдельного состояния у группы нет —
/// оно складывается из состояний броней, а дублировать его значит однажды разойтись с ними.
///
/// Контракт: Reservations/CreatePlayerReservationGroupRequest.cs
class PlayerReservationGroupDto {
  const PlayerReservationGroupDto({
    required this.reservationGroupId,
    required this.reservations,
    this.totalEstimatedCostMinorUnits,
    this.currencyCode,
  });

  final String reservationGroupId;
  final List<PlayerReservationDto> reservations;

  /// Сумма по всей компании — она же замороженная. Пусто — бронь без тарифа, её посчитают
  /// на стойке.
  final int? totalEstimatedCostMinorUnits;
  final String? currencyCode;

  factory PlayerReservationGroupDto.fromJson(Map<String, dynamic> json) => PlayerReservationGroupDto(
        reservationGroupId: json['reservationGroupId'] as String,
        reservations: (json['reservations'] as List<dynamic>).map((item) => PlayerReservationDto.fromJson(item as Map<String, dynamic>)).toList(),
        totalEstimatedCostMinorUnits: json['totalEstimatedCostMinorUnits'] == null ? null : (json['totalEstimatedCostMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reservationGroupId': reservationGroupId,
        'reservations': reservations.map((item) => item.toJson()).toList(),
        'totalEstimatedCostMinorUnits': totalEstimatedCostMinorUnits,
        'currencyCode': currencyCode,
      };
}

/// Контракт: Operator/PlayerSearchResultDto.cs
class PlayerSearchResultDto {
  const PlayerSearchResultDto({
    required this.playerAccountId,
    required this.displayName,
    this.phoneNumber,
    required this.walletBalanceMinorUnits,
    required this.debtBalanceMinorUnits,
    required this.activePackageCount,
    required this.isActive,
    required this.createdAtUtc,
    this.lastActivityAtUtc,
    this.activePackageName,
    required this.activePackageRemainingMinutes,
    this.platformPersonId,
    this.createdFromApp,
  });

  final String playerAccountId;
  final String displayName;
  final String? phoneNumber;
  final int walletBalanceMinorUnits;
  final int debtBalanceMinorUnits;
  final int activePackageCount;
  final bool isActive;
  final DateTime createdAtUtc;
  final DateTime? lastActivityAtUtc;
  final String? activePackageName;
  final int activePackageRemainingMinutes;

  /// См. Billing.PlayerAccountDto: те же два ответа на «кто это и откуда он взялся»,
  /// потому что в списке клиентов они нужны раньше, чем в карточке.
  final String? platformPersonId;
  final bool? createdFromApp;

  factory PlayerSearchResultDto.fromJson(Map<String, dynamic> json) => PlayerSearchResultDto(
        playerAccountId: json['playerAccountId'] as String,
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        walletBalanceMinorUnits: (json['walletBalanceMinorUnits'] as num).toInt(),
        debtBalanceMinorUnits: (json['debtBalanceMinorUnits'] as num).toInt(),
        activePackageCount: (json['activePackageCount'] as num).toInt(),
        isActive: json['isActive'] as bool,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        lastActivityAtUtc: json['lastActivityAtUtc'] == null ? null : DateTime.parse(json['lastActivityAtUtc'] as String),
        activePackageName: json['activePackageName'] == null ? null : json['activePackageName'] as String,
        activePackageRemainingMinutes: (json['activePackageRemainingMinutes'] as num).toInt(),
        platformPersonId: json['platformPersonId'] == null ? null : json['platformPersonId'] as String,
        createdFromApp: json['createdFromApp'] == null ? null : json['createdFromApp'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'displayName': displayName,
        'phoneNumber': phoneNumber,
        'walletBalanceMinorUnits': walletBalanceMinorUnits,
        'debtBalanceMinorUnits': debtBalanceMinorUnits,
        'activePackageCount': activePackageCount,
        'isActive': isActive,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'lastActivityAtUtc': lastActivityAtUtc?.toIso8601String(),
        'activePackageName': activePackageName,
        'activePackageRemainingMinutes': activePackageRemainingMinutes,
        'platformPersonId': platformPersonId,
        'createdFromApp': createdFromApp,
      };
}

/// Место в зале глазами игрока: как называется, где стоит и свободно ли.
/// Идентификатора устройства здесь больше нет: сессия начинается кодом с монитора, а не выбором из
/// списка. Список остался витриной — «есть ли вообще куда сесть», — и занятое место в нём тоже
/// нужно: «PC-07 занят» это ответ, а исчезнувшее место выглядит сбоем приложения.
///
/// Контракт: Players/PlayerSeatDto.cs
class PlayerSeatDto {
  const PlayerSeatDto({
    required this.seatId,
    required this.seatName,
    required this.zoneName,
    required this.isAvailable,
    this.unavailableReason,
  });

  final String seatId;
  final String seatName;
  final String zoneName;
  final bool isAvailable;

  /// Почему занято: "session" — за ним играют, "reservation" — забронировано на ближайшее время,
  /// "offline" — компьютер не на связи. null, когда место свободно.
  final String? unavailableReason;

  factory PlayerSeatDto.fromJson(Map<String, dynamic> json) => PlayerSeatDto(
        seatId: json['seatId'] as String,
        seatName: json['seatName'] as String,
        zoneName: json['zoneName'] as String,
        isAvailable: json['isAvailable'] as bool,
        unavailableReason: json['unavailableReason'] == null ? null : json['unavailableReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'seatId': seatId,
        'seatName': seatName,
        'zoneName': zoneName,
        'isAvailable': isAvailable,
        'unavailableReason': unavailableReason,
      };
}

/// Игрок сам заканчивает свою сессию и освобождает место.
///
/// Контракт: Players/PlayerSelfEndSessionContracts.cs
class PlayerSelfEndSessionRequest {
  const PlayerSelfEndSessionRequest({
    required this.idempotencyKey,
  });

  final String idempotencyKey;

  factory PlayerSelfEndSessionRequest.fromJson(Map<String, dynamic> json) => PlayerSelfEndSessionRequest(
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'idempotencyKey': idempotencyKey,
      };
}

/// Чем закончился ранний выход. Возврат показывается игроку явно: «я встал раньше» и «мне
/// вернули столько-то» — это одно событие, и узнавать вторую половину из истории кошелька
/// человек не должен.
/// <param name="BilledMinutes">
/// Сколько минут списано. Это не фактические минуты, а тарифицируемые: минимальная
/// длительность и шаг округления тарифа уже применены, ровно как у стойки.
/// </param>
///
/// Контракт: Players/PlayerSelfEndSessionContracts.cs
class PlayerSelfEndSessionResponse {
  const PlayerSelfEndSessionResponse({
    required this.billedMinutes,
    required this.refunded,
    this.packageMinutesReturned,
  });

  final int billedMinutes;

  /// Сколько вернулось на кошелёк. Ноль — значит время было отыграно полностью.
  final MoneyDto refunded;

  /// Сколько минут вернулось в пакет — у сессии, начатой по пакету.
  final int? packageMinutesReturned;

  factory PlayerSelfEndSessionResponse.fromJson(Map<String, dynamic> json) => PlayerSelfEndSessionResponse(
        billedMinutes: (json['billedMinutes'] as num).toInt(),
        refunded: MoneyDto.fromJson(json['refunded'] as Map<String, dynamic>),
        packageMinutesReturned: json['packageMinutesReturned'] == null ? null : (json['packageMinutesReturned'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'billedMinutes': billedMinutes,
        'refunded': refunded.toJson(),
        'packageMinutesReturned': packageMinutesReturned,
      };
}

/// Контракт: Players/PlayerSelfExtendRequest.cs
class PlayerSelfExtendRequest {
  const PlayerSelfExtendRequest({
    required this.additionalMinutes,
    required this.idempotencyKey,
  });

  final int additionalMinutes;
  final String idempotencyKey;

  factory PlayerSelfExtendRequest.fromJson(Map<String, dynamic> json) => PlayerSelfExtendRequest(
        additionalMinutes: (json['additionalMinutes'] as num).toInt(),
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'additionalMinutes': additionalMinutes,
        'idempotencyKey': idempotencyKey,
      };
}

/// Человек садится сам — назвав код с монитора той машины, перед которой стоит.
/// Раньше здесь был идентификатор устройства, и приложение брало его из списка мест. Это значило,
/// что занять свободный ПК можно было не приходя в клуб: сервер видел «игрок назвал устройство» и
/// доказательства присутствия не имел никакого. Код видно только с экрана — он и есть
/// доказательство, и живёт минуты, чтобы снятая на телефон цифра никому не пригодилась.
/// Оболочка ПК кода не шлёт: её токен привязан к машине и сам доказывает, где человек сидит
/// (спека оболочки, §5.3). Код ей и не годится — вход по QR его гасит.
///
/// Контракт: Players/PlayerSelfStartRequest.cs
class PlayerSelfStartRequest {
  const PlayerSelfStartRequest({
    required this.seatingCode,
    required this.tariffRuleVersionId,
    required this.durationMinutes,
    required this.idempotencyKey,
    this.playerPackageId,
  });


  /// Код с монитора; пусто — только с токеном, привязанным к ПК.
  final String seatingCode;
  final String tariffRuleVersionId;
  final int durationMinutes;
  final String idempotencyKey;

  /// Сесть по своему пакету: минуты списываются из пакета, а не с кошелька. Тариф при этом не
  /// нужен — у пакета своя цена, уже заплаченная; минуты — сколько взять из остатка.
  final String? playerPackageId;

  factory PlayerSelfStartRequest.fromJson(Map<String, dynamic> json) => PlayerSelfStartRequest(
        seatingCode: json['seatingCode'] as String,
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        durationMinutes: (json['durationMinutes'] as num).toInt(),
        idempotencyKey: json['idempotencyKey'] as String,
        playerPackageId: json['playerPackageId'] == null ? null : json['playerPackageId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'seatingCode': seatingCode,
        'tariffRuleVersionId': tariffRuleVersionId,
        'durationMinutes': durationMinutes,
        'idempotencyKey': idempotencyKey,
        'playerPackageId': playerPackageId,
      };
}

/// Контракт: Shell/PlayerShellStateDto.cs
class PlayerShellStateDto {
  const PlayerShellStateDto({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.state,
    this.sessionId,
    this.leaseExpiresAtUtc,
    this.remainingSeconds,
    required this.isOnline,
    required this.isGraceMode,
    required this.warningThresholdSeconds,
    required this.message,
    required this.launcherApps,
    this.locale,
    this.warningKind,
    this.branding,
    this.seatingCode,
    this.seatingCodeExpiresAtUtc,
    this.observedAtUtc,
    this.lastContactUtc,
    this.apiBaseUrl,
    this.seatLabel,
    this.zoneName,
    this.sessionOwnerKind,
    this.sessionOwnerPlayerAccountId,
    this.features,
    this.maintenanceSinceUtc,
    this.maintenanceByName,
    this.blockedWindows,
    this.clubRules,
    this.idleShutdownAtUtc,
    this.showcase,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String state;
  final String? sessionId;
  final DateTime? leaseExpiresAtUtc;
  final int? remainingSeconds;
  final bool isOnline;
  final bool isGraceMode;
  final int warningThresholdSeconds;
  final String message;
  final List<LauncherAppDto> launcherApps;
  final String? locale;
  final String? warningKind;
  final ShellBrandingDto? branding;

  /// Код с этого монитора: человек набирает его в приложении и садится именно за эту машину.
  /// Пусто, когда за ПК уже играют или связи с сервером нет — показать старый код значит
  /// позвать человека к машине, которую сервер ему не отдаст.
  final String? seatingCode;

  /// Когда код сменится: оболочка показывает, сколько ему осталось, а просроченный не рисует.
  final DateTime? seatingCodeExpiresAtUtc;

  /// Время платформы в момент, когда агент собрал это состояние. Срок аренды — тоже время
  /// платформы, а часы ПК могут от неё отставать: поправку хост считает по этому полю.
  final DateTime? observedAtUtc;

  /// Когда агент в последний раз достучался до платформы. Пусто — ни разу с запуска службы.
  final DateTime? lastContactUtc;

  /// Адрес платформы из настроек агента: хосту больше не нужно угадывать, куда ходить.
  final String? apiBaseUrl;

  /// Место этого ПК — «ПК 07»: первое, что читается на экране, и видно от стойки.
  final String? seatLabel;

  /// Зона места — «Общий зал».
  final String? zoneName;

  /// Чья сессия идёт: none, guest или player. Вошедшему не владельцу экран говорит «эта сессия
  /// не ваша» и ничего не открывает.
  final String? sessionOwnerKind;

  /// Счёт владельца сессии — только у player.
  final String? sessionOwnerPlayerAccountId;

  /// Права организации по тарифу: без player_shop нет вкладки «Бар», без loyalty — кэшбека.
  final List<String>? features;

  /// Обслуживание: с какого момента и кто его включил — для полосы «Включено из Панели AFK4.net
  /// в 14:05 · Шерзод». Пусто вне обслуживания; имя пусто, если его включила поддержка без имени.
  final DateTime? maintenanceSinceUtc;
  final String? maintenanceByName;

  /// Окна, которые хост закрывает, едва они появятся (профиль защиты, §6.3). Служба в сессии 0
  /// окон игрока не видит, поэтому правила едут хосту. В обслуживании список пуст.
  final List<BlockedWindowRuleDto>? blockedWindows;

  /// Правила клуба из настроек ПК: кнопка на экране свободного ПК их открывает.
  final String? clubRules;

  /// Свободный ПК выключится от простоя в это время: экран показывает отсчёт, движение мыши его
  /// отменяет. null — выключение не назначено.
  final DateTime? idleShutdownAtUtc;

  /// Витрина свободного ПК: карточки клуба с картинками из кэша ПК. Пусто — оформление клуба.
  final List<ShowcaseCardDto>? showcase;

  factory PlayerShellStateDto.fromJson(Map<String, dynamic> json) => PlayerShellStateDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        state: json['state'] as String,
        sessionId: json['sessionId'] == null ? null : json['sessionId'] as String,
        leaseExpiresAtUtc: json['leaseExpiresAtUtc'] == null ? null : DateTime.parse(json['leaseExpiresAtUtc'] as String),
        remainingSeconds: json['remainingSeconds'] == null ? null : (json['remainingSeconds'] as num).toInt(),
        isOnline: json['isOnline'] as bool,
        isGraceMode: json['isGraceMode'] as bool,
        warningThresholdSeconds: (json['warningThresholdSeconds'] as num).toInt(),
        message: json['message'] as String,
        launcherApps: (json['launcherApps'] as List<dynamic>).map((item) => LauncherAppDto.fromJson(item as Map<String, dynamic>)).toList(),
        locale: json['locale'] == null ? null : json['locale'] as String,
        warningKind: json['warningKind'] == null ? null : json['warningKind'] as String,
        branding: json['branding'] == null ? null : ShellBrandingDto.fromJson(json['branding'] as Map<String, dynamic>),
        seatingCode: json['seatingCode'] == null ? null : json['seatingCode'] as String,
        seatingCodeExpiresAtUtc: json['seatingCodeExpiresAtUtc'] == null ? null : DateTime.parse(json['seatingCodeExpiresAtUtc'] as String),
        observedAtUtc: json['observedAtUtc'] == null ? null : DateTime.parse(json['observedAtUtc'] as String),
        lastContactUtc: json['lastContactUtc'] == null ? null : DateTime.parse(json['lastContactUtc'] as String),
        apiBaseUrl: json['apiBaseUrl'] == null ? null : json['apiBaseUrl'] as String,
        seatLabel: json['seatLabel'] == null ? null : json['seatLabel'] as String,
        zoneName: json['zoneName'] == null ? null : json['zoneName'] as String,
        sessionOwnerKind: json['sessionOwnerKind'] == null ? null : json['sessionOwnerKind'] as String,
        sessionOwnerPlayerAccountId: json['sessionOwnerPlayerAccountId'] == null ? null : json['sessionOwnerPlayerAccountId'] as String,
        features: json['features'] == null ? null : (json['features'] as List<dynamic>).map((item) => item as String).toList(),
        maintenanceSinceUtc: json['maintenanceSinceUtc'] == null ? null : DateTime.parse(json['maintenanceSinceUtc'] as String),
        maintenanceByName: json['maintenanceByName'] == null ? null : json['maintenanceByName'] as String,
        blockedWindows: json['blockedWindows'] == null ? null : (json['blockedWindows'] as List<dynamic>).map((item) => BlockedWindowRuleDto.fromJson(item as Map<String, dynamic>)).toList(),
        clubRules: json['clubRules'] == null ? null : json['clubRules'] as String,
        idleShutdownAtUtc: json['idleShutdownAtUtc'] == null ? null : DateTime.parse(json['idleShutdownAtUtc'] as String),
        showcase: json['showcase'] == null ? null : (json['showcase'] as List<dynamic>).map((item) => ShowcaseCardDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'state': state,
        'sessionId': sessionId,
        'leaseExpiresAtUtc': leaseExpiresAtUtc?.toIso8601String(),
        'remainingSeconds': remainingSeconds,
        'isOnline': isOnline,
        'isGraceMode': isGraceMode,
        'warningThresholdSeconds': warningThresholdSeconds,
        'message': message,
        'launcherApps': launcherApps.map((item) => item.toJson()).toList(),
        'locale': locale,
        'warningKind': warningKind,
        'branding': branding?.toJson(),
        'seatingCode': seatingCode,
        'seatingCodeExpiresAtUtc': seatingCodeExpiresAtUtc?.toIso8601String(),
        'observedAtUtc': observedAtUtc?.toIso8601String(),
        'lastContactUtc': lastContactUtc?.toIso8601String(),
        'apiBaseUrl': apiBaseUrl,
        'seatLabel': seatLabel,
        'zoneName': zoneName,
        'sessionOwnerKind': sessionOwnerKind,
        'sessionOwnerPlayerAccountId': sessionOwnerPlayerAccountId,
        'features': features?.map((item) => item).toList(),
        'maintenanceSinceUtc': maintenanceSinceUtc?.toIso8601String(),
        'maintenanceByName': maintenanceByName,
        'blockedWindows': blockedWindows?.map((item) => item.toJson()).toList(),
        'clubRules': clubRules,
        'idleShutdownAtUtc': idleShutdownAtUtc?.toIso8601String(),
        'showcase': showcase?.map((item) => item.toJson()).toList(),
      };
}

/// Заявка на вход и что с ней стало: приложение показывает «Вы вошли на ПК 07».
///
/// Контракт: Players/PlayerSignInClaimContracts.cs
class PlayerSignInClaimDto {
  const PlayerSignInClaimDto({
    required this.claimId,
    required this.status,
    required this.expiresAtUtc,
    this.seatLabel,
  });

  final String claimId;

  /// Одно из PlayerSignInClaimStatusNames.
  final String status;
  final DateTime expiresAtUtc;

  /// Имя места: «ПК 07». Пусто, если ПК не привязан к месту.
  final String? seatLabel;

  factory PlayerSignInClaimDto.fromJson(Map<String, dynamic> json) => PlayerSignInClaimDto(
        claimId: json['claimId'] as String,
        status: json['status'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        seatLabel: json['seatLabel'] == null ? null : json['seatLabel'] as String,
      );

  Map<String, dynamic> toJson() => {
        'claimId': claimId,
        'status': status,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'seatLabel': seatLabel,
      };
}

/// ПК должен забрать заявку на вход: человек отсканировал QR с его монитора. Приходит в группу
/// устройства по SignalR и, на случай обрыва, в ответе на сердцебиение.
///
/// Контракт: Devices/PlayerSignInClaimDeviceContracts.cs
class PlayerSignInClaimedDto {
  const PlayerSignInClaimedDto({
    required this.claimId,
    required this.expiresAtUtc,
  });

  final String claimId;
  final DateTime expiresAtUtc;

  factory PlayerSignInClaimedDto.fromJson(Map<String, dynamic> json) => PlayerSignInClaimedDto(
        claimId: json['claimId'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'claimId': claimId,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
      };
}

/// Самопосадка за игровой ПК: клуб, номер и сетевой PIN. Поле называется `Password` с тех
/// времён, когда PIN был клубным паролем, — переименование сломало бы установленные в поле
/// оболочки ради одного слова.
/// <param name="BranchId">
/// Филиал, у ПК которого стоит человек. Нужен ровно в одном случае: клуб с несколькими филиалами,
/// а счёта у человека в нём ещё нет — гадать филиал за него нельзя, в отчётах это выглядело бы как
/// два разных гостя. Клиенты, которые филиала не называют, работают как работали.
/// </param>
///
/// Контракт: Players/PlayerSignInRequest.cs
class PlayerSignInRequest {
  const PlayerSignInRequest({
    required this.organizationId,
    required this.phoneNumber,
    required this.password,
    this.branchId,
  });

  final String organizationId;
  final String phoneNumber;
  final String password;
  final String? branchId;

  factory PlayerSignInRequest.fromJson(Map<String, dynamic> json) => PlayerSignInRequest(
        organizationId: json['organizationId'] as String,
        phoneNumber: json['phoneNumber'] as String,
        password: json['password'] as String,
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'phoneNumber': phoneNumber,
        'password': password,
        'branchId': branchId,
      };
}

/// Контракт: Players/PlayerSignInResponse.cs
class PlayerSignInResponse {
  const PlayerSignInResponse({
    required this.playerAccountId,
    required this.organizationId,
    required this.displayName,
    required this.phoneVerified,
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.refreshTokenExpiresAtUtc,
  });

  final String playerAccountId;
  final String organizationId;
  final String displayName;
  final bool phoneVerified;
  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final DateTime refreshTokenExpiresAtUtc;

  factory PlayerSignInResponse.fromJson(Map<String, dynamic> json) => PlayerSignInResponse(
        playerAccountId: json['playerAccountId'] as String,
        organizationId: json['organizationId'] as String,
        displayName: json['displayName'] as String,
        phoneVerified: json['phoneVerified'] as bool,
        accessToken: json['accessToken'] as String,
        accessTokenExpiresAtUtc: DateTime.parse(json['accessTokenExpiresAtUtc'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshTokenExpiresAtUtc: DateTime.parse(json['refreshTokenExpiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'organizationId': organizationId,
        'displayName': displayName,
        'phoneVerified': phoneVerified,
        'accessToken': accessToken,
        'accessTokenExpiresAtUtc': accessTokenExpiresAtUtc.toIso8601String(),
        'refreshToken': refreshToken,
        'refreshTokenExpiresAtUtc': refreshTokenExpiresAtUtc.toIso8601String(),
      };
}

/// Контракт: Players/PlayerSignOutRequest.cs
class PlayerSignOutRequest {
  const PlayerSignOutRequest({
    required this.refreshToken,
  });

  final String refreshToken;

  factory PlayerSignOutRequest.fromJson(Map<String, dynamic> json) => PlayerSignOutRequest(
        refreshToken: json['refreshToken'] as String,
      );

  Map<String, dynamic> toJson() => {
        'refreshToken': refreshToken,
      };
}

/// Что можно купить, сев за этот ПК, — одним запросом, с готовыми суммами (спека оболочки,
/// §5.5). Клиент цену не считает: суммы считает тот же расчёт, что и списание, иначе экран
/// однажды пообещал бы одну цифру, а касса списала бы другую.
///
/// Контракт: Players/PlayerOfferContracts.cs
class PlayerStartOffersDto {
  const PlayerStartOffersDto({
    this.seatLabel,
    this.zoneName,
    required this.timeZone,
    required this.balance,
    required this.tariffs,
    required this.packages,
  });

  final String? seatLabel;
  final String? zoneName;

  /// Часовой пояс клуба (IANA): «до скольки» показывается по времени клуба, а не телефона.
  final String timeZone;
  final MoneyDto balance;
  final List<PlayerTariffOfferDto> tariffs;
  final List<PlayerPackageOfferDto> packages;

  factory PlayerStartOffersDto.fromJson(Map<String, dynamic> json) => PlayerStartOffersDto(
        seatLabel: json['seatLabel'] == null ? null : json['seatLabel'] as String,
        zoneName: json['zoneName'] == null ? null : json['zoneName'] as String,
        timeZone: json['timeZone'] as String,
        balance: MoneyDto.fromJson(json['balance'] as Map<String, dynamic>),
        tariffs: (json['tariffs'] as List<dynamic>).map((item) => PlayerTariffOfferDto.fromJson(item as Map<String, dynamic>)).toList(),
        packages: (json['packages'] as List<dynamic>).map((item) => PlayerPackageOfferDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'seatLabel': seatLabel,
        'zoneName': zoneName,
        'timeZone': timeZone,
        'balance': balance.toJson(),
        'tariffs': tariffs.map((item) => item.toJson()).toList(),
        'packages': packages.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Players/PlayerOfferContracts.cs
class PlayerTariffOfferDto {
  const PlayerTariffOfferDto({
    required this.tariffVersionId,
    required this.tariffRuleVersionId,
    required this.name,
    required this.pricePerHour,
    required this.appliesNow,
    this.startsAtUtc,
    required this.options,
  });

  final String tariffVersionId;

  /// То, что передаётся в старт как TariffRuleVersionId.
  final String tariffRuleVersionId;
  final String name;
  final MoneyDto pricePerHour;
  final bool appliesNow;

  /// Когда тариф откроется, если сейчас он не действует; вариантов у такого тарифа нет.
  final DateTime? startsAtUtc;
  final List<PlayerDurationOfferDto> options;

  factory PlayerTariffOfferDto.fromJson(Map<String, dynamic> json) => PlayerTariffOfferDto(
        tariffVersionId: json['tariffVersionId'] as String,
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        name: json['name'] as String,
        pricePerHour: MoneyDto.fromJson(json['pricePerHour'] as Map<String, dynamic>),
        appliesNow: json['appliesNow'] as bool,
        startsAtUtc: json['startsAtUtc'] == null ? null : DateTime.parse(json['startsAtUtc'] as String),
        options: (json['options'] as List<dynamic>).map((item) => PlayerDurationOfferDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'tariffVersionId': tariffVersionId,
        'tariffRuleVersionId': tariffRuleVersionId,
        'name': name,
        'pricePerHour': pricePerHour.toJson(),
        'appliesNow': appliesNow,
        'startsAtUtc': startsAtUtc?.toIso8601String(),
        'options': options.map((item) => item.toJson()).toList(),
      };
}

/// Можно ли оставить чаевые за этот визит — и сколько.
///
/// Контракт: Tips/TipContracts.cs
class PlayerTipOfferDto {
  const PlayerTipOfferDto({
    required this.available,
    this.unavailableReason,
    required this.presets,
    required this.balance,
    this.recipientName,
    this.given,
  });

  final bool available;

  /// Одно из TipUnavailableReasonNames; пусто, если можно.
  final String? unavailableReason;
  final List<MoneyDto> presets;
  final MoneyDto balance;

  /// Имя администратора смены — первое слово: «Чаевые Шерзоду».
  final String? recipientName;

  /// Чаевые, уже оставленные за этот визит.
  final MoneyDto? given;

  factory PlayerTipOfferDto.fromJson(Map<String, dynamic> json) => PlayerTipOfferDto(
        available: json['available'] as bool,
        unavailableReason: json['unavailableReason'] == null ? null : json['unavailableReason'] as String,
        presets: (json['presets'] as List<dynamic>).map((item) => MoneyDto.fromJson(item as Map<String, dynamic>)).toList(),
        balance: MoneyDto.fromJson(json['balance'] as Map<String, dynamic>),
        recipientName: json['recipientName'] == null ? null : json['recipientName'] as String,
        given: json['given'] == null ? null : MoneyDto.fromJson(json['given'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'available': available,
        'unavailableReason': unavailableReason,
        'presets': presets.map((item) => item.toJson()).toList(),
        'balance': balance.toJson(),
        'recipientName': recipientName,
        'given': given?.toJson(),
      };
}

/// Контракт: Tips/TipContracts.cs
class PlayerTipRequest {
  const PlayerTipRequest({
    required this.amount,
    required this.idempotencyKey,
  });

  final MoneyDto amount;
  final String idempotencyKey;

  factory PlayerTipRequest.fromJson(Map<String, dynamic> json) => PlayerTipRequest(
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'amount': amount.toJson(),
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Tips/TipContracts.cs
class PlayerTipResponse {
  const PlayerTipResponse({
    required this.amount,
    required this.balanceAfter,
    this.recipientName,
  });

  final MoneyDto amount;
  final MoneyDto balanceAfter;
  final String? recipientName;

  factory PlayerTipResponse.fromJson(Map<String, dynamic> json) => PlayerTipResponse(
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        balanceAfter: MoneyDto.fromJson(json['balanceAfter'] as Map<String, dynamic>),
        recipientName: json['recipientName'] == null ? null : json['recipientName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'amount': amount.toJson(),
        'balanceAfter': balanceAfter.toJson(),
        'recipientName': recipientName,
      };
}

/// Заявка на пополнение кошелька: игрок просит зачислить сумму, клуб подтверждает.
///
/// Контракт: Players/PlayerTopUpIntentDto.cs
class PlayerTopUpIntentDto {
  const PlayerTopUpIntentDto({
    required this.paymentIntentId,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.state,
    required this.purpose,
    required this.method,
    required this.createdAtUtc,
    this.fulfilledAtUtc,
    required this.isExpired,
    this.payUrl,
    this.comment,
    this.gatewayExpiresAtUtc,
    this.qr,
    this.deepLink,
  });

  final String paymentIntentId;
  final int amountMinorUnits;
  final String currencyCode;
  final String state;
  final String purpose;

  /// `counter` — деньги вносят на стойке, `eskhata` — платят из приложения банка.
  final String method;
  final DateTime createdAtUtc;
  final DateTime? fulfilledAtUtc;
  final bool isExpired;

  /// Страница оплаты в браузере — запасной путь для телефона без приложения банка.
  final String? payUrl;
  final String? comment;
  final DateTime? gatewayExpiresAtUtc;
  final String? qr;

  /// Ссылка, открывающая приложение банка. Пусто, если платят на стойке или банк её не дал.
  final String? deepLink;

  factory PlayerTopUpIntentDto.fromJson(Map<String, dynamic> json) => PlayerTopUpIntentDto(
        paymentIntentId: json['paymentIntentId'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        state: json['state'] as String,
        purpose: json['purpose'] as String,
        method: json['method'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        fulfilledAtUtc: json['fulfilledAtUtc'] == null ? null : DateTime.parse(json['fulfilledAtUtc'] as String),
        isExpired: json['isExpired'] as bool,
        payUrl: json['payUrl'] == null ? null : json['payUrl'] as String,
        comment: json['comment'] == null ? null : json['comment'] as String,
        gatewayExpiresAtUtc: json['gatewayExpiresAtUtc'] == null ? null : DateTime.parse(json['gatewayExpiresAtUtc'] as String),
        qr: json['qr'] == null ? null : json['qr'] as String,
        deepLink: json['deepLink'] == null ? null : json['deepLink'] as String,
      );

  Map<String, dynamic> toJson() => {
        'paymentIntentId': paymentIntentId,
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'state': state,
        'purpose': purpose,
        'method': method,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'fulfilledAtUtc': fulfilledAtUtc?.toIso8601String(),
        'isExpired': isExpired,
        'payUrl': payUrl,
        'comment': comment,
        'gatewayExpiresAtUtc': gatewayExpiresAtUtc?.toIso8601String(),
        'qr': qr,
        'deepLink': deepLink,
      };
}

/// Player requests a wallet top-up.
/// CurrencyCode defaults to "TJS" when null or blank.
/// Method ∈ { "counter", "dcgate", "eskhata" }; null/blank → "counter" (operator-confirmed at the desk).
/// BranchId — филиал, в который человек придёт. Он нужен только в первом действии в клубе, где
/// счёта ещё нет: у сети с несколькими филиалами сервер не гадает, куда записать счёт. Поле
/// необязательное — клуб с одним филиалом называть нечего, а у человека со счётом филиал уже
/// известен, и присланный не переписывает его.
/// IdempotencyKey — ключ одной попытки. Необязателен: установленные приложения его не шлют, и
/// без него всё работает как раньше. С ним повтор после обрыва находит уже созданное, а не
/// создаёт второе.
///
/// Контракт: Players/PlayerTopUpIntentRequest.cs
class PlayerTopUpIntentRequest {
  const PlayerTopUpIntentRequest({
    required this.amountMinorUnits,
    this.currencyCode,
    this.method,
    this.branchId,
    this.idempotencyKey,
  });

  final int amountMinorUnits;
  final String? currencyCode;
  final String? method;
  final String? branchId;
  final String? idempotencyKey;

  factory PlayerTopUpIntentRequest.fromJson(Map<String, dynamic> json) => PlayerTopUpIntentRequest(
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
        method: json['method'] == null ? null : json['method'] as String,
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        idempotencyKey: json['idempotencyKey'] == null ? null : json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'method': method,
        'branchId': branchId,
        'idempotencyKey': idempotencyKey,
      };
}

/// Чем клуб принимает деньги прямо сейчас. Стойка — всегда: это наличные в кассе. Онлайн держится
/// на двух вещах сразу — тариф платформы разрешает и у клуба заведён мерчант банка, — и приложение
/// обязано узнать это до того, как предложит человеку кнопку.
///
/// Контракт: Players/PlayerTopUpIntentDto.cs
class PlayerTopUpMethodsDto {
  const PlayerTopUpMethodsDto({
    required this.counter,
    required this.online,
  });

  final bool counter;
  final bool online;

  factory PlayerTopUpMethodsDto.fromJson(Map<String, dynamic> json) => PlayerTopUpMethodsDto(
        counter: json['counter'] as bool,
        online: json['online'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'counter': counter,
        'online': online,
      };
}

/// Событие клуба — турнир, ночь игры, чемпионат зала — глазами игрока: только то, по чему решают,
/// идти ли. Черновиков здесь не бывает, а вместо списка участников — сколько мест осталось и
/// записан ли он сам.
///
/// Контракт: Tournaments/TournamentDtos.cs
class PlayerTournamentDto {
  const PlayerTournamentDto({
    required this.tournamentId,
    required this.branchId,
    required this.branchName,
    required this.title,
    required this.description,
    required this.discipline,
    required this.startsAtUtc,
    required this.entryFee,
    required this.capacity,
    required this.registeredCount,
    required this.isRegistered,
    required this.state,
    required this.cancelReason,
  });

  final String tournamentId;
  final String branchId;
  final String branchName;
  final String title;
  final String description;

  /// Игра словами клуба («Dota 2», «FIFA»). Пусто — клуб не уточнил.
  final String discipline;
  final DateTime startsAtUtc;

  /// Взнос за участие. 0 — бесплатно, и это обычный случай для вечера, которым клуб просто
  /// заполняет будний день.
  final MoneyDto entryFee;

  /// Сколько человек берут. 0 — без ограничения.
  final int capacity;
  final int registeredCount;
  final bool isRegistered;

  /// Одно из TournamentStateNames.
  final String state;

  /// Почему клуб отменил. Пусто, пока событие в силе.
  final String cancelReason;

  factory PlayerTournamentDto.fromJson(Map<String, dynamic> json) => PlayerTournamentDto(
        tournamentId: json['tournamentId'] as String,
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String,
        title: json['title'] as String,
        description: json['description'] as String,
        discipline: json['discipline'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        entryFee: MoneyDto.fromJson(json['entryFee'] as Map<String, dynamic>),
        capacity: (json['capacity'] as num).toInt(),
        registeredCount: (json['registeredCount'] as num).toInt(),
        isRegistered: json['isRegistered'] as bool,
        state: json['state'] as String,
        cancelReason: json['cancelReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'tournamentId': tournamentId,
        'branchId': branchId,
        'branchName': branchName,
        'title': title,
        'description': description,
        'discipline': discipline,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'entryFee': entryFee.toJson(),
        'capacity': capacity,
        'registeredCount': registeredCount,
        'isRegistered': isRegistered,
        'state': state,
        'cancelReason': cancelReason,
      };
}

/// Прошедший визит: где сидел, сколько пробыл и на сколько наиграл.
///
/// Контракт: Players/PlayerVisitDto.cs
class PlayerVisitDto {
  const PlayerVisitDto({
    required this.sessionId,
    required this.seatId,
    required this.seatName,
    required this.startedAtUtc,
    this.endedAtUtc,
    required this.timeChargeMinorUnits,
    required this.posTotalMinorUnits,
    required this.grandTotalMinorUnits,
    required this.currencyCode,
    required this.hasReceipt,
  });

  final String sessionId;
  final String seatId;
  final String seatName;
  final DateTime startedAtUtc;

  /// Пусто — визит ещё не закрыт.
  final DateTime? endedAtUtc;
  final int timeChargeMinorUnits;
  final int posTotalMinorUnits;
  final int grandTotalMinorUnits;
  final String currencyCode;
  final bool hasReceipt;

  factory PlayerVisitDto.fromJson(Map<String, dynamic> json) => PlayerVisitDto(
        sessionId: json['sessionId'] as String,
        seatId: json['seatId'] as String,
        seatName: json['seatName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        endedAtUtc: json['endedAtUtc'] == null ? null : DateTime.parse(json['endedAtUtc'] as String),
        timeChargeMinorUnits: (json['timeChargeMinorUnits'] as num).toInt(),
        posTotalMinorUnits: (json['posTotalMinorUnits'] as num).toInt(),
        grandTotalMinorUnits: (json['grandTotalMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        hasReceipt: json['hasReceipt'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'seatId': seatId,
        'seatName': seatName,
        'startedAtUtc': startedAtUtc.toIso8601String(),
        'endedAtUtc': endedAtUtc?.toIso8601String(),
        'timeChargeMinorUnits': timeChargeMinorUnits,
        'posTotalMinorUnits': posTotalMinorUnits,
        'grandTotalMinorUnits': grandTotalMinorUnits,
        'currencyCode': currencyCode,
        'hasReceipt': hasReceipt,
      };
}

/// Чек визита: время, покупки и итог.
///
/// Контракт: Players/PlayerVisitReceiptDto.cs
class PlayerVisitReceiptDto {
  const PlayerVisitReceiptDto({
    required this.receiptNumber,
    required this.createdAtUtc,
    required this.sessionId,
    required this.seatName,
    required this.startedAtUtc,
    this.endedAtUtc,
    required this.timeChargeMinorUnits,
    required this.posLines,
    required this.posTotalMinorUnits,
    required this.grandTotalMinorUnits,
    required this.currencyCode,
  });

  final String receiptNumber;
  final DateTime createdAtUtc;
  final String sessionId;
  final String seatName;
  final DateTime startedAtUtc;
  final DateTime? endedAtUtc;
  final int timeChargeMinorUnits;
  final List<PlayerPurchaseLineDto> posLines;
  final int posTotalMinorUnits;
  final int grandTotalMinorUnits;
  final String currencyCode;

  factory PlayerVisitReceiptDto.fromJson(Map<String, dynamic> json) => PlayerVisitReceiptDto(
        receiptNumber: json['receiptNumber'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        sessionId: json['sessionId'] as String,
        seatName: json['seatName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        endedAtUtc: json['endedAtUtc'] == null ? null : DateTime.parse(json['endedAtUtc'] as String),
        timeChargeMinorUnits: (json['timeChargeMinorUnits'] as num).toInt(),
        posLines: (json['posLines'] as List<dynamic>).map((item) => PlayerPurchaseLineDto.fromJson(item as Map<String, dynamic>)).toList(),
        posTotalMinorUnits: (json['posTotalMinorUnits'] as num).toInt(),
        grandTotalMinorUnits: (json['grandTotalMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
      );

  Map<String, dynamic> toJson() => {
        'receiptNumber': receiptNumber,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'sessionId': sessionId,
        'seatName': seatName,
        'startedAtUtc': startedAtUtc.toIso8601String(),
        'endedAtUtc': endedAtUtc?.toIso8601String(),
        'timeChargeMinorUnits': timeChargeMinorUnits,
        'posLines': posLines.map((item) => item.toJson()).toList(),
        'posTotalMinorUnits': posTotalMinorUnits,
        'grandTotalMinorUnits': grandTotalMinorUnits,
        'currencyCode': currencyCode,
      };
}

/// Контракт: Pos/PosProductCategoryDto.cs
class PosProductCategoryDto {
  const PosProductCategoryDto({
    required this.categoryId,
    required this.organizationId,
    required this.branchId,
    required this.name,
    required this.isActive,
    required this.sortOrder,
    required this.createdAtUtc,
  });

  final String categoryId;
  final String organizationId;
  final String branchId;
  final String name;
  final bool isActive;
  final int sortOrder;
  final DateTime createdAtUtc;

  factory PosProductCategoryDto.fromJson(Map<String, dynamic> json) => PosProductCategoryDto(
        categoryId: json['categoryId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        isActive: json['isActive'] as bool,
        sortOrder: (json['sortOrder'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'categoryId': categoryId,
        'organizationId': organizationId,
        'branchId': branchId,
        'name': name,
        'isActive': isActive,
        'sortOrder': sortOrder,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Pos/PosProductDto.cs
class PosProductDto {
  const PosProductDto({
    required this.productId,
    required this.organizationId,
    required this.branchId,
    required this.categoryId,
    required this.name,
    required this.sku,
    required this.price,
    required this.trackStock,
    required this.allowNegativeStock,
    required this.isActive,
    required this.stockOnHand,
    required this.createdAtUtc,
    this.reorderThreshold,
    this.availableInShell,
    this.avgCostMinorUnits,
    this.barcodes,
    this.featuredOnPcs,
    this.imageUrl,
  });

  final String productId;
  final String organizationId;
  final String branchId;
  final String categoryId;
  final String name;
  final String sku;
  final MoneyDto price;
  final bool trackStock;
  final bool allowNegativeStock;
  final bool isActive;
  final int stockOnHand;
  final DateTime createdAtUtc;
  final int? reorderThreshold;
  final bool? availableInShell;
  final int? avgCostMinorUnits;
  final List<String>? barcodes;
  final bool? featuredOnPcs;
  final String? imageUrl;

  factory PosProductDto.fromJson(Map<String, dynamic> json) => PosProductDto(
        productId: json['productId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        categoryId: json['categoryId'] as String,
        name: json['name'] as String,
        sku: json['sku'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        trackStock: json['trackStock'] as bool,
        allowNegativeStock: json['allowNegativeStock'] as bool,
        isActive: json['isActive'] as bool,
        stockOnHand: (json['stockOnHand'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        reorderThreshold: json['reorderThreshold'] == null ? null : (json['reorderThreshold'] as num).toInt(),
        availableInShell: json['availableInShell'] == null ? null : json['availableInShell'] as bool,
        avgCostMinorUnits: json['avgCostMinorUnits'] == null ? null : (json['avgCostMinorUnits'] as num).toInt(),
        barcodes: json['barcodes'] == null ? null : (json['barcodes'] as List<dynamic>).map((item) => item as String).toList(),
        featuredOnPcs: json['featuredOnPcs'] == null ? null : json['featuredOnPcs'] as bool,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
      );

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'organizationId': organizationId,
        'branchId': branchId,
        'categoryId': categoryId,
        'name': name,
        'sku': sku,
        'price': price.toJson(),
        'trackStock': trackStock,
        'allowNegativeStock': allowNegativeStock,
        'isActive': isActive,
        'stockOnHand': stockOnHand,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'reorderThreshold': reorderThreshold,
        'availableInShell': availableInShell,
        'avgCostMinorUnits': avgCostMinorUnits,
        'barcodes': barcodes?.map((item) => item).toList(),
        'featuredOnPcs': featuredOnPcs,
        'imageUrl': imageUrl,
      };
}

/// Контракт: Pos/PosSaleDto.cs
class PosSaleDto {
  const PosSaleDto({
    required this.posSaleId,
    required this.organizationId,
    required this.branchId,
    required this.shiftId,
    required this.state,
    required this.lines,
    required this.total,
    required this.createdByStaffUserId,
    required this.createdAtUtc,
    this.paidAtUtc,
    this.refundedAtUtc,
    this.voidedAtUtc,
    this.latestReceipt,
    this.playerAccountId,
    this.shopOrderId,
    this.payments,
  });

  final String posSaleId;
  final String organizationId;
  final String branchId;
  final String shiftId;
  final String state;
  final List<PosSaleLineDto> lines;
  final MoneyDto total;
  final String createdByStaffUserId;
  final DateTime createdAtUtc;
  final DateTime? paidAtUtc;
  final DateTime? refundedAtUtc;
  final DateTime? voidedAtUtc;
  final ReceiptDto? latestReceipt;
  final String? playerAccountId;
  final String? shopOrderId;

  /// Чем за чек заплатили. Пусто у черновика — его ещё не оплачивали.
  /// До этого поля стойка рисовала в карточке чека секцию «Оплаты», которая всегда оставалась
  /// пустой: она читала поле, которого в контракте не было. Строки оплат в базе лежали всё это
  /// время — их просто не клали в ответ.
  final List<PaymentPartDto>? payments;

  factory PosSaleDto.fromJson(Map<String, dynamic> json) => PosSaleDto(
        posSaleId: json['posSaleId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        shiftId: json['shiftId'] as String,
        state: json['state'] as String,
        lines: (json['lines'] as List<dynamic>).map((item) => PosSaleLineDto.fromJson(item as Map<String, dynamic>)).toList(),
        total: MoneyDto.fromJson(json['total'] as Map<String, dynamic>),
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        paidAtUtc: json['paidAtUtc'] == null ? null : DateTime.parse(json['paidAtUtc'] as String),
        refundedAtUtc: json['refundedAtUtc'] == null ? null : DateTime.parse(json['refundedAtUtc'] as String),
        voidedAtUtc: json['voidedAtUtc'] == null ? null : DateTime.parse(json['voidedAtUtc'] as String),
        latestReceipt: json['latestReceipt'] == null ? null : ReceiptDto.fromJson(json['latestReceipt'] as Map<String, dynamic>),
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        shopOrderId: json['shopOrderId'] == null ? null : json['shopOrderId'] as String,
        payments: json['payments'] == null ? null : (json['payments'] as List<dynamic>).map((item) => PaymentPartDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'posSaleId': posSaleId,
        'organizationId': organizationId,
        'branchId': branchId,
        'shiftId': shiftId,
        'state': state,
        'lines': lines.map((item) => item.toJson()).toList(),
        'total': total.toJson(),
        'createdByStaffUserId': createdByStaffUserId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'paidAtUtc': paidAtUtc?.toIso8601String(),
        'refundedAtUtc': refundedAtUtc?.toIso8601String(),
        'voidedAtUtc': voidedAtUtc?.toIso8601String(),
        'latestReceipt': latestReceipt?.toJson(),
        'playerAccountId': playerAccountId,
        'shopOrderId': shopOrderId,
        'payments': payments?.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Pos/PosSaleLineDto.cs
class PosSaleLineDto {
  const PosSaleLineDto({
    required this.productId,
    required this.productName,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });

  final String productId;
  final String productName;
  final int quantity;
  final MoneyDto unitPrice;
  final MoneyDto lineTotal;

  factory PosSaleLineDto.fromJson(Map<String, dynamic> json) => PosSaleLineDto(
        productId: json['productId'] as String,
        productName: json['productName'] as String,
        quantity: (json['quantity'] as num).toInt(),
        unitPrice: MoneyDto.fromJson(json['unitPrice'] as Map<String, dynamic>),
        lineTotal: MoneyDto.fromJson(json['lineTotal'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'productName': productName,
        'quantity': quantity,
        'unitPrice': unitPrice.toJson(),
        'lineTotal': lineTotal.toJson(),
      };
}

/// Контракт: Inventory/ProductBarcodeDto.cs
class ProductBarcodeDto {
  const ProductBarcodeDto({
    required this.barcodeId,
    required this.productId,
    required this.code,
    required this.isPrimary,
  });

  final String barcodeId;
  final String productId;
  final String code;
  final bool isPrimary;

  factory ProductBarcodeDto.fromJson(Map<String, dynamic> json) => ProductBarcodeDto(
        barcodeId: json['barcodeId'] as String,
        productId: json['productId'] as String,
        code: json['code'] as String,
        isPrimary: json['isPrimary'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'barcodeId': barcodeId,
        'productId': productId,
        'code': code,
        'isPrimary': isPrimary,
      };
}

/// Строка отчёта: пункт, исход (ProtectionItemStatusNames) и подробность для разбора.
///
/// Контракт: Devices/ProtectionProfileContracts.cs
class ProtectionItemReportDto {
  const ProtectionItemReportDto({
    required this.item,
    required this.status,
    this.detail,
  });


  /// Одно из ProtectionItemNames.
  final String item;

  /// Одно из ProtectionItemStatusNames.
  final String status;
  final String? detail;

  factory ProtectionItemReportDto.fromJson(Map<String, dynamic> json) => ProtectionItemReportDto(
        item: json['item'] as String,
        status: json['status'] as String,
        detail: json['detail'] == null ? null : json['detail'] as String,
      );

  Map<String, dynamic> toJson() => {
        'item': item,
        'status': status,
        'detail': detail,
      };
}

/// Профиль защиты ПК филиала (спека оболочки, §6.3): что агент запрещает на игровом ПК. Версия
/// растёт с каждым сохранением и едет в сердцебиении — по её смене агент перечитывает профиль.
/// Версия 0 — клуб профиль не настраивал, действует только постоянная база киоска.
///
/// Контракт: Devices/ProtectionProfileContracts.cs
class ProtectionProfileDto {
  const ProtectionProfileDto({
    required this.version,
    required this.blockRemovableStorage,
    required this.blockBrowserDownloads,
    required this.blockBrowserIncognito,
    required this.disableRunDialog,
    required this.hiddenDrives,
    required this.urlBlocklist,
    required this.blockedWindows,
    required this.clearAfterSession,
    this.idleShutdownMinutes,
    this.clubRules,
  });

  final int version;

  /// Флешки и внешние диски — запрет Windows на все съёмные накопители.
  final bool blockRemovableStorage;

  /// Скачивание в Chrome и Edge.
  final bool blockBrowserDownloads;

  /// Режим инкогнито в Chrome и InPrivate в Edge.
  final bool blockBrowserIncognito;

  /// Окно «Выполнить» (Win+R).
  final bool disableRunDialog;

  /// Буквы дисков, скрытых в Проводнике. Это не запрет: программа откроет диск по пути.
  final List<String> hiddenDrives;

  /// Адреса и шаблоны, которые Chrome и Edge не открывают (формат URLBlocklist).
  final List<String> urlBlocklist;

  /// Окна, которые оболочка закрывает, едва они появятся.
  final List<BlockedWindowRuleDto> blockedWindows;

  /// Что стереть после сессии игрока (§6.4). Каждый пункт — из SessionTraceNames.
  final List<String> clearAfterSession;

  /// Выключить свободный ПК, за которым столько минут никого нет; null — не выключать.
  final int? idleShutdownMinutes;

  /// Правила клуба на экране ПК — текст клуба как есть, на его языке.
  final String? clubRules;

  factory ProtectionProfileDto.fromJson(Map<String, dynamic> json) => ProtectionProfileDto(
        version: (json['version'] as num).toInt(),
        blockRemovableStorage: json['blockRemovableStorage'] as bool,
        blockBrowserDownloads: json['blockBrowserDownloads'] as bool,
        blockBrowserIncognito: json['blockBrowserIncognito'] as bool,
        disableRunDialog: json['disableRunDialog'] as bool,
        hiddenDrives: (json['hiddenDrives'] as List<dynamic>).map((item) => item as String).toList(),
        urlBlocklist: (json['urlBlocklist'] as List<dynamic>).map((item) => item as String).toList(),
        blockedWindows: (json['blockedWindows'] as List<dynamic>).map((item) => BlockedWindowRuleDto.fromJson(item as Map<String, dynamic>)).toList(),
        clearAfterSession: (json['clearAfterSession'] as List<dynamic>).map((item) => item as String).toList(),
        idleShutdownMinutes: json['idleShutdownMinutes'] == null ? null : (json['idleShutdownMinutes'] as num).toInt(),
        clubRules: json['clubRules'] == null ? null : json['clubRules'] as String,
      );

  Map<String, dynamic> toJson() => {
        'version': version,
        'blockRemovableStorage': blockRemovableStorage,
        'blockBrowserDownloads': blockBrowserDownloads,
        'blockBrowserIncognito': blockBrowserIncognito,
        'disableRunDialog': disableRunDialog,
        'hiddenDrives': hiddenDrives.map((item) => item).toList(),
        'urlBlocklist': urlBlocklist.map((item) => item).toList(),
        'blockedWindows': blockedWindows.map((item) => item.toJson()).toList(),
        'clearAfterSession': clearAfterSession.map((item) => item).toList(),
        'idleShutdownMinutes': idleShutdownMinutes,
        'clubRules': clubRules,
      };
}

/// DetailValue carries the single numeric figure behind an alert, meaning depends on Kind:
/// minutes since the last agent heartbeat for AgentSilent, minutes since the shift was
/// opened for ShiftNotClosed, count of devices that reported a failed install for
/// RolloutFailed. It is null for alert kinds/situations with no such figure
/// (PaymentOverdue always; AgentSilent when the device has never reported a heartbeat at
/// all; RolloutFailed when the rollout was flagged manually before any device reported a
/// failure). Clients must never render a raw backend string as user-facing alert text —
/// every kind has a translated label, and any parameterized detail is built client-side
/// from DetailValue, not shipped as pre-rendered prose.
///
/// Контракт: Platform/Pulse/PlatformPulseContracts.cs
class PulseAlertDto {
  const PulseAlertDto({
    required this.kind,
    required this.level,
    this.detailValue,
  });

  final String kind;
  final String level;
  final int? detailValue;

  factory PulseAlertDto.fromJson(Map<String, dynamic> json) => PulseAlertDto(
        kind: json['kind'] as String,
        level: json['level'] as String,
        detailValue: json['detailValue'] == null ? null : (json['detailValue'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'kind': kind,
        'level': level,
        'detailValue': detailValue,
      };
}

/// Контракт: Platform/Pulse/PlatformPulseContracts.cs
class PulseClubDto {
  const PulseClubDto({
    required this.branchId,
    required this.name,
    required this.city,
    required this.devicesOnline,
    required this.devicesTotal,
    required this.seatsOccupied,
    required this.seatsTotal,
    required this.shiftOpen,
    this.shiftOpenedAtUtc,
    this.lastHeartbeatAtUtc,
    required this.alerts,
  });

  final String branchId;
  final String name;
  final String city;
  final int devicesOnline;
  final int devicesTotal;
  final int seatsOccupied;
  final int seatsTotal;
  final bool shiftOpen;
  final DateTime? shiftOpenedAtUtc;
  final DateTime? lastHeartbeatAtUtc;
  final List<PulseAlertDto> alerts;

  factory PulseClubDto.fromJson(Map<String, dynamic> json) => PulseClubDto(
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        city: json['city'] as String,
        devicesOnline: (json['devicesOnline'] as num).toInt(),
        devicesTotal: (json['devicesTotal'] as num).toInt(),
        seatsOccupied: (json['seatsOccupied'] as num).toInt(),
        seatsTotal: (json['seatsTotal'] as num).toInt(),
        shiftOpen: json['shiftOpen'] as bool,
        shiftOpenedAtUtc: json['shiftOpenedAtUtc'] == null ? null : DateTime.parse(json['shiftOpenedAtUtc'] as String),
        lastHeartbeatAtUtc: json['lastHeartbeatAtUtc'] == null ? null : DateTime.parse(json['lastHeartbeatAtUtc'] as String),
        alerts: (json['alerts'] as List<dynamic>).map((item) => PulseAlertDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'name': name,
        'city': city,
        'devicesOnline': devicesOnline,
        'devicesTotal': devicesTotal,
        'seatsOccupied': seatsOccupied,
        'seatsTotal': seatsTotal,
        'shiftOpen': shiftOpen,
        'shiftOpenedAtUtc': shiftOpenedAtUtc?.toIso8601String(),
        'lastHeartbeatAtUtc': lastHeartbeatAtUtc?.toIso8601String(),
        'alerts': alerts.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Platform/Pulse/PlatformPulseContracts.cs
class PulseOrganizationDto {
  const PulseOrganizationDto({
    required this.organizationId,
    required this.name,
    required this.status,
    required this.planCode,
    required this.subscriptionStatus,
    required this.alertLevel,
    required this.outstandingMinorUnits,
    required this.currencyCode,
    required this.alerts,
    required this.clubs,
  });

  final String organizationId;
  final String name;
  final String status;
  final String planCode;
  final String subscriptionStatus;
  final String alertLevel;
  final int outstandingMinorUnits;
  final String currencyCode;
  final List<PulseAlertDto> alerts;
  final List<PulseClubDto> clubs;

  factory PulseOrganizationDto.fromJson(Map<String, dynamic> json) => PulseOrganizationDto(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        status: json['status'] as String,
        planCode: json['planCode'] as String,
        subscriptionStatus: json['subscriptionStatus'] as String,
        alertLevel: json['alertLevel'] as String,
        outstandingMinorUnits: (json['outstandingMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        alerts: (json['alerts'] as List<dynamic>).map((item) => PulseAlertDto.fromJson(item as Map<String, dynamic>)).toList(),
        clubs: (json['clubs'] as List<dynamic>).map((item) => PulseClubDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'status': status,
        'planCode': planCode,
        'subscriptionStatus': subscriptionStatus,
        'alertLevel': alertLevel,
        'outstandingMinorUnits': outstandingMinorUnits,
        'currencyCode': currencyCode,
        'alerts': alerts.map((item) => item.toJson()).toList(),
        'clubs': clubs.map((item) => item.toJson()).toList(),
      };
}

/// The player buying a package for themselves. Only the idempotency key comes from the client: the
/// organization comes from the authenticated player and the branch and package from the route, so a
/// caller cannot buy in someone else's name or out of another club's price list.
///
/// Контракт: Players/PurchasePackageFromAppRequest.cs
class PurchasePackageFromAppRequest {
  const PurchasePackageFromAppRequest({
    required this.idempotencyKey,
  });

  final String idempotencyKey;

  factory PurchasePackageFromAppRequest.fromJson(Map<String, dynamic> json) => PurchasePackageFromAppRequest(
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Billing/PurchasePackageRequest.cs
class PurchasePackageRequest {
  const PurchasePackageRequest({
    required this.organizationId,
    required this.packageDefinitionId,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String packageDefinitionId;
  final String idempotencyKey;

  factory PurchasePackageRequest.fromJson(Map<String, dynamic> json) => PurchasePackageRequest(
        organizationId: json['organizationId'] as String,
        packageDefinitionId: json['packageDefinitionId'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'packageDefinitionId': packageDefinitionId,
        'idempotencyKey': idempotencyKey,
      };
}

/// Стирание подтверждается коротким именем клуба, набранным руками. Кнопка «да, я уверен» здесь
/// недостаточна: ошибиться клубом в списке легко, набрать чужой slug по памяти — нет.
///
/// Контракт: Platform/Organizations/OffboardingContracts.cs
class PurgeOrganizationRequest {
  const PurgeOrganizationRequest({
    required this.slug,
  });

  final String slug;

  factory PurgeOrganizationRequest.fromJson(Map<String, dynamic> json) => PurgeOrganizationRequest(
        slug: json['slug'] as String,
      );

  Map<String, dynamic> toJson() => {
        'slug': slug,
      };
}

/// Сколько строк унесло стирание — по группам, для записи в аудит и показа человеку.
///
/// Контракт: Platform/Organizations/OffboardingContracts.cs
class PurgeOrganizationResultDto {
  const PurgeOrganizationResultDto({
    required this.players,
    required this.staffUsers,
    required this.sessions,
    required this.sales,
    required this.devices,
    required this.branches,
    required this.clubAuditRecords,
  });

  final int players;
  final int staffUsers;
  final int sessions;
  final int sales;
  final int devices;
  final int branches;
  final int clubAuditRecords;

  factory PurgeOrganizationResultDto.fromJson(Map<String, dynamic> json) => PurgeOrganizationResultDto(
        players: (json['players'] as num).toInt(),
        staffUsers: (json['staffUsers'] as num).toInt(),
        sessions: (json['sessions'] as num).toInt(),
        sales: (json['sales'] as num).toInt(),
        devices: (json['devices'] as num).toInt(),
        branches: (json['branches'] as num).toInt(),
        clubAuditRecords: (json['clubAuditRecords'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'players': players,
        'staffUsers': staffUsers,
        'sessions': sessions,
        'sales': sales,
        'devices': devices,
        'branches': branches,
        'clubAuditRecords': clubAuditRecords,
      };
}

/// Одна провалившаяся строка очереди. Без причины провала счётчик «провалено» не диагностируем:
/// видно, что письма не уходят, и не видно почему. Адрес маскирован — домен для разбора важен,
/// полный адрес человека нет.
///
/// Контракт: Platform/Health/PlatformHealthContracts.cs
class QueueFailureDto {
  const QueueFailureDto({
    required this.queueName,
    this.failedAtUtc,
    required this.kind,
    required this.recipientMasked,
    required this.attemptCount,
    this.lastError,
  });

  final String queueName;
  final DateTime? failedAtUtc;
  final String kind;
  final String recipientMasked;
  final int attemptCount;
  final String? lastError;

  factory QueueFailureDto.fromJson(Map<String, dynamic> json) => QueueFailureDto(
        queueName: json['queueName'] as String,
        failedAtUtc: json['failedAtUtc'] == null ? null : DateTime.parse(json['failedAtUtc'] as String),
        kind: json['kind'] as String,
        recipientMasked: json['recipientMasked'] as String,
        attemptCount: (json['attemptCount'] as num).toInt(),
        lastError: json['lastError'] == null ? null : json['lastError'] as String,
      );

  Map<String, dynamic> toJson() => {
        'queueName': queueName,
        'failedAtUtc': failedAtUtc?.toIso8601String(),
        'kind': kind,
        'recipientMasked': recipientMasked,
        'attemptCount': attemptCount,
        'lastError': lastError,
      };
}

/// Контракт: Platform/Health/PlatformHealthContracts.cs
class QueueHealthDto {
  const QueueHealthDto({
    required this.queueName,
    required this.pendingCount,
    required this.failedCount,
    required this.stuckCount,
  });

  final String queueName;
  final int pendingCount;
  final int failedCount;
  final int stuckCount;

  factory QueueHealthDto.fromJson(Map<String, dynamic> json) => QueueHealthDto(
        queueName: json['queueName'] as String,
        pendingCount: (json['pendingCount'] as num).toInt(),
        failedCount: (json['failedCount'] as num).toInt(),
        stuckCount: (json['stuckCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'queueName': queueName,
        'pendingCount': pendingCount,
        'failedCount': failedCount,
        'stuckCount': stuckCount,
      };
}

/// Контракт: Receipts/ReceiptDto.cs
class ReceiptDto {
  const ReceiptDto({
    required this.receiptId,
    required this.organizationId,
    required this.branchId,
    this.posSaleId,
    required this.receiptNumber,
    required this.receiptType,
    required this.total,
    required this.createdAtUtc,
    this.sessionId,
    this.shopOrderId,
  });

  final String receiptId;
  final String organizationId;
  final String branchId;
  final String? posSaleId;
  final String receiptNumber;
  final String receiptType;
  final MoneyDto total;
  final DateTime createdAtUtc;
  final String? sessionId;
  final String? shopOrderId;

  factory ReceiptDto.fromJson(Map<String, dynamic> json) => ReceiptDto(
        receiptId: json['receiptId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        posSaleId: json['posSaleId'] == null ? null : json['posSaleId'] as String,
        receiptNumber: json['receiptNumber'] as String,
        receiptType: json['receiptType'] as String,
        total: MoneyDto.fromJson(json['total'] as Map<String, dynamic>),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        sessionId: json['sessionId'] == null ? null : json['sessionId'] as String,
        shopOrderId: json['shopOrderId'] == null ? null : json['shopOrderId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'receiptId': receiptId,
        'organizationId': organizationId,
        'branchId': branchId,
        'posSaleId': posSaleId,
        'receiptNumber': receiptNumber,
        'receiptType': receiptType,
        'total': total.toJson(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'sessionId': sessionId,
        'shopOrderId': shopOrderId,
      };
}

/// Контракт: Shifts/RecordCashMovementRequest.cs
class RecordCashMovementRequest {
  const RecordCashMovementRequest({
    required this.organizationId,
    required this.movementType,
    required this.amount,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String movementType;
  final MoneyDto amount;
  final String reason;
  final String idempotencyKey;

  factory RecordCashMovementRequest.fromJson(Map<String, dynamic> json) => RecordCashMovementRequest(
        organizationId: json['organizationId'] as String,
        movementType: json['movementType'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'movementType': movementType,
        'amount': amount.toJson(),
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Platform/Support/PlatformSupportAccessContracts.cs
class RedeemSupportAccessTicketRequest {
  const RedeemSupportAccessTicketRequest({
    required this.ticket,
  });

  final String ticket;

  factory RedeemSupportAccessTicketRequest.fromJson(Map<String, dynamic> json) => RedeemSupportAccessTicketRequest(
        ticket: json['ticket'] as String,
      );

  Map<String, dynamic> toJson() => {
        'ticket': ticket,
      };
}

/// Настройки «приведи друга» глазами клуба.
///
/// Контракт: Loyalty/ReferralContracts.cs
class ReferralSettingsDto {
  const ReferralSettingsDto({
    required this.enabled,
    required this.referrerBonusMinorUnits,
    required this.inviteeBonusMinorUnits,
    required this.minimumTopUpMinorUnits,
    required this.claimWindowDays,
    required this.maxRewardedPerReferrer,
  });

  final bool enabled;
  final int referrerBonusMinorUnits;
  final int inviteeBonusMinorUnits;
  final int minimumTopUpMinorUnits;
  final int claimWindowDays;
  final int maxRewardedPerReferrer;

  factory ReferralSettingsDto.fromJson(Map<String, dynamic> json) => ReferralSettingsDto(
        enabled: json['enabled'] as bool,
        referrerBonusMinorUnits: (json['referrerBonusMinorUnits'] as num).toInt(),
        inviteeBonusMinorUnits: (json['inviteeBonusMinorUnits'] as num).toInt(),
        minimumTopUpMinorUnits: (json['minimumTopUpMinorUnits'] as num).toInt(),
        claimWindowDays: (json['claimWindowDays'] as num).toInt(),
        maxRewardedPerReferrer: (json['maxRewardedPerReferrer'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'enabled': enabled,
        'referrerBonusMinorUnits': referrerBonusMinorUnits,
        'inviteeBonusMinorUnits': inviteeBonusMinorUnits,
        'minimumTopUpMinorUnits': minimumTopUpMinorUnits,
        'claimWindowDays': claimWindowDays,
        'maxRewardedPerReferrer': maxRewardedPerReferrer,
      };
}

/// Контракт: Billing/RefundLedgerEntryRequest.cs
class RefundLedgerEntryRequest {
  const RefundLedgerEntryRequest({
    required this.organizationId,
    required this.ledgerEntryId,
    required this.amount,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String ledgerEntryId;
  final MoneyDto amount;
  final String reason;
  final String idempotencyKey;

  factory RefundLedgerEntryRequest.fromJson(Map<String, dynamic> json) => RefundLedgerEntryRequest(
        organizationId: json['organizationId'] as String,
        ledgerEntryId: json['ledgerEntryId'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'ledgerEntryId': ledgerEntryId,
        'amount': amount.toJson(),
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Pos/RefundPosSaleRequest.cs
class RefundPosSaleRequest {
  const RefundPosSaleRequest({
    required this.organizationId,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String reason;
  final String idempotencyKey;

  factory RefundPosSaleRequest.fromJson(Map<String, dynamic> json) => RefundPosSaleRequest(
        organizationId: json['organizationId'] as String,
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// Регистрация телефона игрока для пушей. Токен выдаёт FCM, платформа — `android` или
/// `ios`, локаль — язык приложения на этом устройстве.
/// Язык здесь не спрашивается: пуш уходит на языке аккаунта (PlayerAccount.PreferredLocale),
/// который человек выбирает сам в профиле. Установленные приложения поле ещё шлют — лишнее поле
/// в теле сервер молча пропускает.
///
/// Контракт: Notifications/NotificationContracts.cs
class RegisterPlayerDeviceRequest {
  const RegisterPlayerDeviceRequest({
    this.pushToken,
    this.platform,
  });

  final String? pushToken;
  final String? platform;

  factory RegisterPlayerDeviceRequest.fromJson(Map<String, dynamic> json) => RegisterPlayerDeviceRequest(
        pushToken: json['pushToken'] == null ? null : json['pushToken'] as String,
        platform: json['platform'] == null ? null : json['platform'] as String,
      );

  Map<String, dynamic> toJson() => {
        'pushToken': pushToken,
        'platform': platform,
      };
}

/// Контракт: Identity/RegistrationContracts.cs
class RegistrationConfirmRequest {
  const RegistrationConfirmRequest({
    required this.phoneNumber,
    required this.code,
  });

  final String phoneNumber;
  final String code;

  factory RegistrationConfirmRequest.fromJson(Map<String, dynamic> json) => RegistrationConfirmRequest(
        phoneNumber: json['phoneNumber'] as String,
        code: json['code'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
        'code': code,
      };
}

/// Ответ на просьбу прислать код. Он одинаков для знакомого и незнакомого номера — ни одного поля,
/// по которому можно отличить одно от другого, здесь нет и быть не должно.
///
/// Контракт: Identity/RegistrationContracts.cs
class RegistrationStartedResponse {
  const RegistrationStartedResponse({
    required this.expiresInSeconds,
    required this.resendAfterSeconds,
  });

  final int expiresInSeconds;
  final int resendAfterSeconds;

  factory RegistrationStartedResponse.fromJson(Map<String, dynamic> json) => RegistrationStartedResponse(
        expiresInSeconds: (json['expiresInSeconds'] as num).toInt(),
        resendAfterSeconds: (json['resendAfterSeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'expiresInSeconds': expiresInSeconds,
        'resendAfterSeconds': resendAfterSeconds,
      };
}

/// Просьба прислать код на номер. Клуб здесь не называется: человек заводит себя сам.
///
/// Контракт: Identity/RegistrationContracts.cs
class RegistrationStartRequest {
  const RegistrationStartRequest({
    required this.phoneNumber,
  });

  final String phoneNumber;

  factory RegistrationStartRequest.fromJson(Map<String, dynamic> json) => RegistrationStartRequest(
        phoneNumber: json['phoneNumber'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
      };
}

/// «Не в этот раз» — сказанное клубом заявке, которую ещё не принимали.
/// Отдельно от отмены намеренно: игрок ничего не отменял, деньги ему возвращаются целиком при
/// любых настройках филиала, и в его сетевые числа этот отказ не попадает.
/// <param name="ReasonCode">Код из RejectReasonCodes.</param>
/// <param name="Note">
/// Пояснение администратора своими словами. Обязательно при RejectReasonCodes.Other:
/// код «своими словами» без слов — тот же пустой отказ, от которого уходили.
/// </param>
///
/// Контракт: Reservations/RejectReservationRequest.cs
class RejectReservationRequest {
  const RejectReservationRequest({
    required this.organizationId,
    required this.reasonCode,
    this.note,
    this.expectedVersion,
  });

  final String organizationId;
  final String reasonCode;
  final String? note;
  final int? expectedVersion;

  factory RejectReservationRequest.fromJson(Map<String, dynamic> json) => RejectReservationRequest(
        organizationId: json['organizationId'] as String,
        reasonCode: json['reasonCode'] as String,
        note: json['note'] == null ? null : json['note'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'reasonCode': reasonCode,
        'note': note,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: Devices/RenameDeviceRequest.cs
class RenameDeviceRequest {
  const RenameDeviceRequest({
    required this.organizationId,
    required this.displayName,
  });

  final String organizationId;
  final String displayName;

  factory RenameDeviceRequest.fromJson(Map<String, dynamic> json) => RenameDeviceRequest(
        organizationId: json['organizationId'] as String,
        displayName: json['displayName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'displayName': displayName,
      };
}

/// Порядок игр в библиотеке: все игры филиала в новом порядке.
///
/// Контракт: Games/GameLibraryContracts.cs
class ReorderBranchGamesRequest {
  const ReorderBranchGamesRequest({
    required this.organizationId,
    required this.branchGameIds,
  });

  final String organizationId;
  final List<String> branchGameIds;

  factory ReorderBranchGamesRequest.fromJson(Map<String, dynamic> json) => ReorderBranchGamesRequest(
        organizationId: json['organizationId'] as String,
        branchGameIds: (json['branchGameIds'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchGameIds': branchGameIds.map((item) => item).toList(),
      };
}

/// Новый порядок категорий филиала: весь список целиком, сверху вниз.
/// Список, а не пара «категория + номер»: порядок — свойство набора, и присланный целиком он не
/// оставляет места расхождению. Пара «id + номер» на каждое перетаскивание порождала бы дыры и
/// совпадения в нумерации, которые потом нечем разрешить.
///
/// Контракт: Pos/ReorderProductCategoriesRequest.cs
class ReorderProductCategoriesRequest {
  const ReorderProductCategoriesRequest({
    required this.organizationId,
    required this.categoryIds,
  });

  final String organizationId;
  final List<String> categoryIds;

  factory ReorderProductCategoriesRequest.fromJson(Map<String, dynamic> json) => ReorderProductCategoriesRequest(
        organizationId: json['organizationId'] as String,
        categoryIds: (json['categoryIds'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'categoryIds': categoryIds.map((item) => item).toList(),
      };
}

/// A configured report schedule.
///
/// Контракт: Reports/ReportScheduleContracts.cs
class ReportScheduleDto {
  const ReportScheduleDto({
    required this.reportScheduleId,
    required this.organizationId,
    required this.branchId,
    required this.reportType,
    required this.frequency,
    required this.isActive,
    required this.nextRunUtc,
    this.lastRunUtc,
    required this.createdAtUtc,
  });

  final String reportScheduleId;
  final String organizationId;
  final String branchId;
  final String reportType;
  final String frequency;
  final bool isActive;
  final DateTime nextRunUtc;
  final DateTime? lastRunUtc;
  final DateTime createdAtUtc;

  factory ReportScheduleDto.fromJson(Map<String, dynamic> json) => ReportScheduleDto(
        reportScheduleId: json['reportScheduleId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        reportType: json['reportType'] as String,
        frequency: json['frequency'] as String,
        isActive: json['isActive'] as bool,
        nextRunUtc: DateTime.parse(json['nextRunUtc'] as String),
        lastRunUtc: json['lastRunUtc'] == null ? null : DateTime.parse(json['lastRunUtc'] as String),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'reportScheduleId': reportScheduleId,
        'organizationId': organizationId,
        'branchId': branchId,
        'reportType': reportType,
        'frequency': frequency,
        'isActive': isActive,
        'nextRunUtc': nextRunUtc.toIso8601String(),
        'lastRunUtc': lastRunUtc?.toIso8601String(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Что случилось с бронью — стойке, прямо сейчас.
/// Полоса заявок до сих пор обновлялась опросом: администратор видел чужое решение через
/// несколько секунд, а два администратора на разных машинах какое-то время видели разное. Хуже
/// того, решения принимают и таймеры — срок ответа истекает сам, — и о них узнать было неоткуда,
/// кроме следующего опроса.
/// Форма повторяет `SessionLifecycleChangedDto` намеренно: у операторского экрана уже есть
/// приёмник таких событий с отбором по филиалу, и второй способ доставлять то же самое разошёлся
/// бы с первым на первом же исправлении.
/// <param name="Kind">Что именно произошло — ReservationChangeKinds.</param>
/// <param name="State">Состояние брони после изменения.</param>
///
/// Контракт: Reservations/ReservationChangedDto.cs
class ReservationChangedDto {
  const ReservationChangedDto({
    required this.organizationId,
    required this.branchId,
    required this.reservationId,
    this.seatId,
    required this.kind,
    required this.state,
    required this.version,
    required this.startsAtUtc,
    required this.observedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String reservationId;
  final String? seatId;
  final String kind;
  final String state;
  final int version;
  final DateTime startsAtUtc;
  final DateTime observedAtUtc;

  factory ReservationChangedDto.fromJson(Map<String, dynamic> json) => ReservationChangedDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        reservationId: json['reservationId'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        kind: json['kind'] as String,
        state: json['state'] as String,
        version: (json['version'] as num).toInt(),
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        observedAtUtc: DateTime.parse(json['observedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'reservationId': reservationId,
        'seatId': seatId,
        'kind': kind,
        'state': state,
        'version': version,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'observedAtUtc': observedAtUtc.toIso8601String(),
      };
}

/// Контракт: Reservations/ReservationDto.cs
class ReservationDto {
  const ReservationDto({
    required this.reservationId,
    required this.organizationId,
    required this.branchId,
    this.playerAccountId,
    this.seatId,
    this.seatName,
    this.zoneName,
    required this.customerName,
    this.phoneNumber,
    required this.startsAtUtc,
    required this.endsAtUtc,
    required this.durationMinutes,
    required this.state,
    required this.source,
    required this.note,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    this.cancelledAtUtc,
    required this.cancelReason,
    this.reservationGroupId,
    this.version,
    this.startedSessionId,
    this.tariffVersionId,
    this.tariffName,
    this.estimatedCostMinorUnits,
    this.currencyCode,
    this.respondByUtc,
    this.confirmedAtUtc,
    this.platformPersonId,
    this.noShowAtUtc,
    this.retainedAmountMinorUnits,
    this.rejectedAtUtc,
    this.rejectReasonCode,
    this.rejectReasonNote,
  });

  final String reservationId;
  final String organizationId;
  final String branchId;
  final String? playerAccountId;
  final String? seatId;
  final String? seatName;
  final String? zoneName;
  final String customerName;
  final String? phoneNumber;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final int durationMinutes;
  final String state;
  final String source;
  final String note;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;
  final DateTime? cancelledAtUtc;
  final String cancelReason;
  final String? reservationGroupId;
  final int? version;
  final String? startedSessionId;

  /// Billing choice carried by a self-service booking, and the price the server computed for it.
  /// Null for desk-created bookings — those are still priced when the player is seated.
  final String? tariffVersionId;
  final String? tariffName;
  final int? estimatedCostMinorUnits;
  final String? currencyCode;

  /// Докуда клуб обещал ответить на заявку и когда ответил. Срок есть только у заявки, которая
  /// ждёт решения стойки; подтверждённую бронь по таймеру никто не снимает.
  final DateTime? respondByUtc;
  final DateTime? confirmedAtUtc;

  /// Личность за счётом, с которого пришла заявка. Клуб, решающий её судьбу, спрашивает сеть
  /// этим идентификатором, а не телефоном гостя. У заявки, записанной на стойке одним номером,
  /// счёта ещё нет — и называть некого.
  final String? platformPersonId;

  /// Чем кончилась бронь, в которую человек не приехал: когда это признали и сколько филиал
  /// оставил себе по своей же настройке. Сумма пустая, а не нулевая, когда не удерживали вовсе:
  /// ноль читался бы как «удержали нисколько», хотя удержания не было.
  final DateTime? noShowAtUtc;
  final int? retainedAmountMinorUnits;

  /// Отказ клуба: когда, по какой причине из справочника и что администратор добавил словами.
  /// Причину читает игрок — поэтому код, а не текст: текст на языке стойки ему не поможет.
  final DateTime? rejectedAtUtc;
  final String? rejectReasonCode;
  final String? rejectReasonNote;

  factory ReservationDto.fromJson(Map<String, dynamic> json) => ReservationDto(
        reservationId: json['reservationId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
        zoneName: json['zoneName'] == null ? null : json['zoneName'] as String,
        customerName: json['customerName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        durationMinutes: (json['durationMinutes'] as num).toInt(),
        state: json['state'] as String,
        source: json['source'] as String,
        note: json['note'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
        cancelledAtUtc: json['cancelledAtUtc'] == null ? null : DateTime.parse(json['cancelledAtUtc'] as String),
        cancelReason: json['cancelReason'] as String,
        reservationGroupId: json['reservationGroupId'] == null ? null : json['reservationGroupId'] as String,
        version: json['version'] == null ? null : (json['version'] as num).toInt(),
        startedSessionId: json['startedSessionId'] == null ? null : json['startedSessionId'] as String,
        tariffVersionId: json['tariffVersionId'] == null ? null : json['tariffVersionId'] as String,
        tariffName: json['tariffName'] == null ? null : json['tariffName'] as String,
        estimatedCostMinorUnits: json['estimatedCostMinorUnits'] == null ? null : (json['estimatedCostMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
        respondByUtc: json['respondByUtc'] == null ? null : DateTime.parse(json['respondByUtc'] as String),
        confirmedAtUtc: json['confirmedAtUtc'] == null ? null : DateTime.parse(json['confirmedAtUtc'] as String),
        platformPersonId: json['platformPersonId'] == null ? null : json['platformPersonId'] as String,
        noShowAtUtc: json['noShowAtUtc'] == null ? null : DateTime.parse(json['noShowAtUtc'] as String),
        retainedAmountMinorUnits: json['retainedAmountMinorUnits'] == null ? null : (json['retainedAmountMinorUnits'] as num).toInt(),
        rejectedAtUtc: json['rejectedAtUtc'] == null ? null : DateTime.parse(json['rejectedAtUtc'] as String),
        rejectReasonCode: json['rejectReasonCode'] == null ? null : json['rejectReasonCode'] as String,
        rejectReasonNote: json['rejectReasonNote'] == null ? null : json['rejectReasonNote'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reservationId': reservationId,
        'organizationId': organizationId,
        'branchId': branchId,
        'playerAccountId': playerAccountId,
        'seatId': seatId,
        'seatName': seatName,
        'zoneName': zoneName,
        'customerName': customerName,
        'phoneNumber': phoneNumber,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'durationMinutes': durationMinutes,
        'state': state,
        'source': source,
        'note': note,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
        'cancelledAtUtc': cancelledAtUtc?.toIso8601String(),
        'cancelReason': cancelReason,
        'reservationGroupId': reservationGroupId,
        'version': version,
        'startedSessionId': startedSessionId,
        'tariffVersionId': tariffVersionId,
        'tariffName': tariffName,
        'estimatedCostMinorUnits': estimatedCostMinorUnits,
        'currencyCode': currencyCode,
        'respondByUtc': respondByUtc?.toIso8601String(),
        'confirmedAtUtc': confirmedAtUtc?.toIso8601String(),
        'platformPersonId': platformPersonId,
        'noShowAtUtc': noShowAtUtc?.toIso8601String(),
        'retainedAmountMinorUnits': retainedAmountMinorUnits,
        'rejectedAtUtc': rejectedAtUtc?.toIso8601String(),
        'rejectReasonCode': rejectReasonCode,
        'rejectReasonNote': rejectReasonNote,
      };
}

/// Контракт: Reservations/ReservationGroup.cs
class ReservationGroupConflictDto {
  const ReservationGroupConflictDto({
    required this.seatId,
    required this.reason,
  });

  final String seatId;
  final String reason;

  factory ReservationGroupConflictDto.fromJson(Map<String, dynamic> json) => ReservationGroupConflictDto(
        seatId: json['seatId'] as String,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'seatId': seatId,
        'reason': reason,
      };
}

/// Group-create outcome. On success ReservationGroupId and Reservations
/// are populated and Conflicts is empty; on conflict it is the other way round.
///
/// Контракт: Reservations/ReservationGroup.cs
class ReservationGroupResultDto {
  const ReservationGroupResultDto({
    this.reservationGroupId,
    required this.reservations,
    required this.conflicts,
  });

  final String? reservationGroupId;
  final List<ReservationDto> reservations;
  final List<ReservationGroupConflictDto> conflicts;

  factory ReservationGroupResultDto.fromJson(Map<String, dynamic> json) => ReservationGroupResultDto(
        reservationGroupId: json['reservationGroupId'] == null ? null : json['reservationGroupId'] as String,
        reservations: (json['reservations'] as List<dynamic>).map((item) => ReservationDto.fromJson(item as Map<String, dynamic>)).toList(),
        conflicts: (json['conflicts'] as List<dynamic>).map((item) => ReservationGroupConflictDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'reservationGroupId': reservationGroupId,
        'reservations': reservations.map((item) => item.toJson()).toList(),
        'conflicts': conflicts.map((item) => item.toJson()).toList(),
      };
}

/// BillableMinutes can exceed the booked span — that is the point of showing it: an hour booked on a
/// tariff with a two-hour minimum is billed as two hours, and the player sees why before booking.
/// AmountMinorUnits — сумма за ВСЮ бронь, включая все её места. Отдавать цену одного места и
/// оставлять умножение приложению значило бы показывать не то число, которое будет заморожено.
///
/// Контракт: Reservations/ReservationQuoteContracts.cs
class ReservationQuoteDto {
  const ReservationQuoteDto({
    required this.tariffVersionId,
    required this.tariffName,
    required this.requestedMinutes,
    required this.billableMinutes,
    required this.amountMinorUnits,
    required this.currencyCode,
    this.seatCount,
  });

  final String tariffVersionId;
  final String tariffName;
  final int requestedMinutes;
  final int billableMinutes;
  final int amountMinorUnits;
  final String currencyCode;
  final int? seatCount;

  factory ReservationQuoteDto.fromJson(Map<String, dynamic> json) => ReservationQuoteDto(
        tariffVersionId: json['tariffVersionId'] as String,
        tariffName: json['tariffName'] as String,
        requestedMinutes: (json['requestedMinutes'] as num).toInt(),
        billableMinutes: (json['billableMinutes'] as num).toInt(),
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        seatCount: json['seatCount'] == null ? null : (json['seatCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'tariffVersionId': tariffVersionId,
        'tariffName': tariffName,
        'requestedMinutes': requestedMinutes,
        'billableMinutes': billableMinutes,
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'seatCount': seatCount,
      };
}

/// What a booking will cost before the player commits to it.
/// The price is asked of the server rather than computed in the app on purpose: the minimum-billable
/// floor and the rounding increment are billing rules, and a second implementation in the client
/// would quietly disagree with the charge the moment either side changed.
/// SeatCount умножает цену на число мест на стороне сервера. Умножение сложным не выглядит, но
/// показанная и списанная суммы обязаны приходить из одного места: считаясь порознь, они однажды
/// разойдутся — а это та цена, на которую игрок согласился.
///
/// Контракт: Reservations/ReservationQuoteContracts.cs
class ReservationQuoteRequest {
  const ReservationQuoteRequest({
    required this.tariffVersionId,
    required this.startsAtUtc,
    required this.endsAtUtc,
    this.seatCount,
  });

  final String tariffVersionId;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final int? seatCount;

  factory ReservationQuoteRequest.fromJson(Map<String, dynamic> json) => ReservationQuoteRequest(
        tariffVersionId: json['tariffVersionId'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        seatCount: json['seatCount'] == null ? null : (json['seatCount'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'tariffVersionId': tariffVersionId,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'seatCount': seatCount,
      };
}

/// Контракт: Reservations/ReservationDto.cs
class ReservationSearchResultDto {
  const ReservationSearchResultDto({
    required this.reservations,
    required this.limit,
  });

  final List<ReservationDto> reservations;
  final int limit;

  factory ReservationSearchResultDto.fromJson(Map<String, dynamic> json) => ReservationSearchResultDto(
        reservations: (json['reservations'] as List<dynamic>).map((item) => ReservationDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'reservations': reservations.map((item) => item.toJson()).toList(),
        'limit': limit,
      };
}

/// Места филиала, на которые бронь в окне [StartsAtUtc, EndsAtUtc)
/// встанет без конфликта — тем же правилом, которым сервер эту бронь примет или отклонит.
/// Окно возвращается вместе с ответом: панель спрашивает его для конкретной брони, и ответ на
/// окно, которое уже сменилось, не должен тихо стать списком для нового.
///
/// Контракт: Reservations/ReservationSeatAvailabilityDto.cs
class ReservationSeatAvailabilityDto {
  const ReservationSeatAvailabilityDto({
    required this.startsAtUtc,
    required this.endsAtUtc,
    required this.freeSeatIds,
  });

  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final List<String> freeSeatIds;

  factory ReservationSeatAvailabilityDto.fromJson(Map<String, dynamic> json) => ReservationSeatAvailabilityDto(
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        freeSeatIds: (json['freeSeatIds'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'freeSeatIds': freeSeatIds.map((item) => item).toList(),
      };
}

/// Контракт: Identity/ResetStaffUserPasswordRequest.cs
class ResetStaffUserPasswordRequest {
  const ResetStaffUserPasswordRequest({
    required this.organizationId,
    required this.newPassword,
  });

  final String organizationId;
  final String newPassword;

  factory ResetStaffUserPasswordRequest.fromJson(Map<String, dynamic> json) => ResetStaffUserPasswordRequest(
        organizationId: json['organizationId'] as String,
        newPassword: json['newPassword'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'newPassword': newPassword,
      };
}

/// Контракт: Platform/Operator/ResolveOperatorConnectionRequest.cs
class ResolveOperatorConnectionRequest {
  const ResolveOperatorConnectionRequest({
    this.organizationSlug,
    this.branchSlug,
    this.setupCode,
  });

  final String? organizationSlug;
  final String? branchSlug;
  final String? setupCode;

  factory ResolveOperatorConnectionRequest.fromJson(Map<String, dynamic> json) => ResolveOperatorConnectionRequest(
        organizationSlug: json['organizationSlug'] == null ? null : json['organizationSlug'] as String,
        branchSlug: json['branchSlug'] == null ? null : json['branchSlug'] as String,
        setupCode: json['setupCode'] == null ? null : json['setupCode'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationSlug': organizationSlug,
        'branchSlug': branchSlug,
        'setupCode': setupCode,
      };
}

/// Контракт: Platform/Operator/ResolveOperatorConnectionResponse.cs
class ResolveOperatorConnectionResponse {
  const ResolveOperatorConnectionResponse({
    required this.organizationId,
    required this.organizationSlug,
    required this.organizationName,
    required this.organizationStatus,
    this.organizationStatusReason,
    required this.branchId,
    required this.branchSlug,
    required this.branchName,
    required this.branchCity,
    required this.source,
  });

  final String organizationId;
  final String organizationSlug;
  final String organizationName;
  final String organizationStatus;
  final String? organizationStatusReason;
  final String branchId;
  final String branchSlug;
  final String branchName;
  final String branchCity;
  final String source;

  factory ResolveOperatorConnectionResponse.fromJson(Map<String, dynamic> json) => ResolveOperatorConnectionResponse(
        organizationId: json['organizationId'] as String,
        organizationSlug: json['organizationSlug'] as String,
        organizationName: json['organizationName'] as String,
        organizationStatus: json['organizationStatus'] as String,
        organizationStatusReason: json['organizationStatusReason'] == null ? null : json['organizationStatusReason'] as String,
        branchId: json['branchId'] as String,
        branchSlug: json['branchSlug'] as String,
        branchName: json['branchName'] as String,
        branchCity: json['branchCity'] as String,
        source: json['source'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'organizationSlug': organizationSlug,
        'organizationName': organizationName,
        'organizationStatus': organizationStatus,
        'organizationStatusReason': organizationStatusReason,
        'branchId': branchId,
        'branchSlug': branchSlug,
        'branchName': branchName,
        'branchCity': branchCity,
        'source': source,
      };
}

/// Снятие паузы: ПК отпирается, а конец фиксированной сессии сдвигается на простой.
///
/// Контракт: Sessions/PauseSessionRequest.cs
class ResumeSessionRequest {
  const ResumeSessionRequest({
    required this.reason,
    required this.idempotencyKey,
    this.expectedVersion,
  });

  final String reason;
  final String idempotencyKey;
  final int? expectedVersion;

  factory ResumeSessionRequest.fromJson(Map<String, dynamic> json) => ResumeSessionRequest(
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'reason': reason,
        'idempotencyKey': idempotencyKey,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: Devices/RevokeDeviceCredentialResponse.cs
class RevokeDeviceCredentialResponse {
  const RevokeDeviceCredentialResponse({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.credentialId,
    required this.revokedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String credentialId;
  final DateTime revokedAtUtc;

  factory RevokeDeviceCredentialResponse.fromJson(Map<String, dynamic> json) => RevokeDeviceCredentialResponse(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        credentialId: json['credentialId'] as String,
        revokedAtUtc: DateTime.parse(json['revokedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'credentialId': credentialId,
        'revokedAtUtc': revokedAtUtc.toIso8601String(),
      };
}

/// Контракт: Identity/AccountActivation/RevokeOrganizationOwnerInviteRequest.cs
class RevokeOrganizationOwnerInviteRequest {
  const RevokeOrganizationOwnerInviteRequest({
    required this.reason,
  });

  final String reason;

  factory RevokeOrganizationOwnerInviteRequest.fromJson(Map<String, dynamic> json) => RevokeOrganizationOwnerInviteRequest(
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reason': reason,
      };
}

/// Контракт: Devices/RotateDeviceCredentialResponse.cs
class RotateDeviceCredentialResponse {
  const RotateDeviceCredentialResponse({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
    required this.credentialId,
    required this.credentialSecret,
    required this.rotatedAtUtc,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;
  final String credentialId;
  final String credentialSecret;
  final DateTime rotatedAtUtc;

  factory RotateDeviceCredentialResponse.fromJson(Map<String, dynamic> json) => RotateDeviceCredentialResponse(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
        credentialId: json['credentialId'] as String,
        credentialSecret: json['credentialSecret'] as String,
        rotatedAtUtc: DateTime.parse(json['rotatedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
        'credentialId': credentialId,
        'credentialSecret': credentialSecret,
        'rotatedAtUtc': rotatedAtUtc.toIso8601String(),
      };
}

/// Контракт: Reports/SalesReportResultDto.cs
class SalesReportResultDto {
  const SalesReportResultDto({
    required this.rows,
    required this.limit,
    required this.grossSalesTotal,
    required this.refundsTotal,
    required this.netSalesTotal,
    required this.grossCostOfGoodsTotal,
    required this.refundedCostOfGoodsTotal,
    required this.netCostOfGoodsTotal,
  });

  final List<SalesReportRowDto> rows;
  final int limit;
  final MoneyDto grossSalesTotal;
  final MoneyDto refundsTotal;
  final MoneyDto netSalesTotal;
  final MoneyDto grossCostOfGoodsTotal;
  final MoneyDto refundedCostOfGoodsTotal;
  final MoneyDto netCostOfGoodsTotal;

  factory SalesReportResultDto.fromJson(Map<String, dynamic> json) => SalesReportResultDto(
        rows: (json['rows'] as List<dynamic>).map((item) => SalesReportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
        grossSalesTotal: MoneyDto.fromJson(json['grossSalesTotal'] as Map<String, dynamic>),
        refundsTotal: MoneyDto.fromJson(json['refundsTotal'] as Map<String, dynamic>),
        netSalesTotal: MoneyDto.fromJson(json['netSalesTotal'] as Map<String, dynamic>),
        grossCostOfGoodsTotal: MoneyDto.fromJson(json['grossCostOfGoodsTotal'] as Map<String, dynamic>),
        refundedCostOfGoodsTotal: MoneyDto.fromJson(json['refundedCostOfGoodsTotal'] as Map<String, dynamic>),
        netCostOfGoodsTotal: MoneyDto.fromJson(json['netCostOfGoodsTotal'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'rows': rows.map((item) => item.toJson()).toList(),
        'limit': limit,
        'grossSalesTotal': grossSalesTotal.toJson(),
        'refundsTotal': refundsTotal.toJson(),
        'netSalesTotal': netSalesTotal.toJson(),
        'grossCostOfGoodsTotal': grossCostOfGoodsTotal.toJson(),
        'refundedCostOfGoodsTotal': refundedCostOfGoodsTotal.toJson(),
        'netCostOfGoodsTotal': netCostOfGoodsTotal.toJson(),
      };
}

/// Контракт: Reports/SalesReportRowDto.cs
class SalesReportRowDto {
  const SalesReportRowDto({
    required this.posSaleId,
    required this.organizationId,
    required this.branchId,
    required this.shiftId,
    required this.createdByStaffUserId,
    required this.state,
    required this.total,
    required this.paidAmount,
    required this.refundAmount,
    required this.lineCount,
    required this.itemQuantity,
    required this.createdAtUtc,
    this.paidAtUtc,
    this.refundedAtUtc,
    this.voidedAtUtc,
    required this.grossCostOfGoods,
    required this.refundedCostOfGoods,
    required this.netCostOfGoods,
  });

  final String posSaleId;
  final String organizationId;
  final String branchId;
  final String shiftId;
  final String createdByStaffUserId;
  final String state;
  final MoneyDto total;
  final MoneyDto paidAmount;
  final MoneyDto refundAmount;
  final int lineCount;
  final int itemQuantity;
  final DateTime createdAtUtc;
  final DateTime? paidAtUtc;
  final DateTime? refundedAtUtc;
  final DateTime? voidedAtUtc;
  final MoneyDto grossCostOfGoods;
  final MoneyDto refundedCostOfGoods;
  final MoneyDto netCostOfGoods;

  factory SalesReportRowDto.fromJson(Map<String, dynamic> json) => SalesReportRowDto(
        posSaleId: json['posSaleId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        shiftId: json['shiftId'] as String,
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        state: json['state'] as String,
        total: MoneyDto.fromJson(json['total'] as Map<String, dynamic>),
        paidAmount: MoneyDto.fromJson(json['paidAmount'] as Map<String, dynamic>),
        refundAmount: MoneyDto.fromJson(json['refundAmount'] as Map<String, dynamic>),
        lineCount: (json['lineCount'] as num).toInt(),
        itemQuantity: (json['itemQuantity'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        paidAtUtc: json['paidAtUtc'] == null ? null : DateTime.parse(json['paidAtUtc'] as String),
        refundedAtUtc: json['refundedAtUtc'] == null ? null : DateTime.parse(json['refundedAtUtc'] as String),
        voidedAtUtc: json['voidedAtUtc'] == null ? null : DateTime.parse(json['voidedAtUtc'] as String),
        grossCostOfGoods: MoneyDto.fromJson(json['grossCostOfGoods'] as Map<String, dynamic>),
        refundedCostOfGoods: MoneyDto.fromJson(json['refundedCostOfGoods'] as Map<String, dynamic>),
        netCostOfGoods: MoneyDto.fromJson(json['netCostOfGoods'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'posSaleId': posSaleId,
        'organizationId': organizationId,
        'branchId': branchId,
        'shiftId': shiftId,
        'createdByStaffUserId': createdByStaffUserId,
        'state': state,
        'total': total.toJson(),
        'paidAmount': paidAmount.toJson(),
        'refundAmount': refundAmount.toJson(),
        'lineCount': lineCount,
        'itemQuantity': itemQuantity,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'paidAtUtc': paidAtUtc?.toIso8601String(),
        'refundedAtUtc': refundedAtUtc?.toIso8601String(),
        'voidedAtUtc': voidedAtUtc?.toIso8601String(),
        'grossCostOfGoods': grossCostOfGoods.toJson(),
        'refundedCostOfGoods': refundedCostOfGoods.toJson(),
        'netCostOfGoods': netCostOfGoods.toJson(),
      };
}

/// Контракт: Platform/Announcements/AnnouncementContracts.cs
class SavePlatformAnnouncementRequest {
  const SavePlatformAnnouncementRequest({
    required this.title,
    required this.body,
    required this.severity,
    required this.showFromUtc,
    required this.showUntilUtc,
    required this.audienceKind,
    required this.audiencePlanCodes,
    required this.audienceOrganizationIds,
  });

  final String title;
  final String body;
  final String severity;
  final DateTime showFromUtc;
  final DateTime showUntilUtc;
  final String audienceKind;
  final List<String> audiencePlanCodes;
  final List<String> audienceOrganizationIds;

  factory SavePlatformAnnouncementRequest.fromJson(Map<String, dynamic> json) => SavePlatformAnnouncementRequest(
        title: json['title'] as String,
        body: json['body'] as String,
        severity: json['severity'] as String,
        showFromUtc: DateTime.parse(json['showFromUtc'] as String),
        showUntilUtc: DateTime.parse(json['showUntilUtc'] as String),
        audienceKind: json['audienceKind'] as String,
        audiencePlanCodes: (json['audiencePlanCodes'] as List<dynamic>).map((item) => item as String).toList(),
        audienceOrganizationIds: (json['audienceOrganizationIds'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'title': title,
        'body': body,
        'severity': severity,
        'showFromUtc': showFromUtc.toIso8601String(),
        'showUntilUtc': showUntilUtc.toIso8601String(),
        'audienceKind': audienceKind,
        'audiencePlanCodes': audiencePlanCodes.map((item) => item).toList(),
        'audienceOrganizationIds': audienceOrganizationIds.map((item) => item).toList(),
      };
}

/// Контракт: Layout/SeatDto.cs
class SeatDto {
  const SeatDto({
    required this.seatId,
    required this.organizationId,
    required this.branchId,
    required this.zoneId,
    required this.name,
    required this.sortOrder,
    required this.createdAtUtc,
  });

  final String seatId;
  final String organizationId;
  final String branchId;
  final String zoneId;
  final String name;
  final int sortOrder;
  final DateTime createdAtUtc;

  factory SeatDto.fromJson(Map<String, dynamic> json) => SeatDto(
        seatId: json['seatId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        zoneId: json['zoneId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'seatId': seatId,
        'organizationId': organizationId,
        'branchId': branchId,
        'zoneId': zoneId,
        'name': name,
        'sortOrder': sortOrder,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Reservations/ReservationRequests.cs
class SeatReservationRequest {
  const SeatReservationRequest({
    required this.organizationId,
    required this.expectedVersion,
  });

  final String organizationId;
  final int expectedVersion;

  factory SeatReservationRequest.fromJson(Map<String, dynamic> json) => SeatReservationRequest(
        organizationId: json['organizationId'] as String,
        expectedVersion: (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: FloorMap/SeatStatusDto.cs
class SeatStatusDto {
  const SeatStatusDto({
    required this.seatId,
    required this.seatName,
    required this.zoneId,
    required this.zoneName,
    required this.sortOrder,
    required this.state,
    this.deviceId,
    this.deviceName,
    this.isDeviceOnline,
    this.isDeviceLocked,
    this.lastHeartbeatAtUtc,
    this.agentVersion,
    this.shellVersion,
    this.activeSessionId,
    this.remainingSeconds,
    this.accruedCostMinorUnits,
    this.currencyCode,
    this.sessionVersion,
    this.playerDisplayName,
    this.tariffName,
    this.sessionStartedAtUtc,
    this.assistanceRequestedAtUtc,
    this.maintenanceSinceUtc,
    this.isConsole,
  });

  final String seatId;
  final String seatName;
  final String zoneId;
  final String zoneName;
  final int sortOrder;

  /// Одно из SeatStateNames.
  final String state;
  final String? deviceId;
  final String? deviceName;
  final bool? isDeviceOnline;
  final bool? isDeviceLocked;
  final DateTime? lastHeartbeatAtUtc;
  final String? agentVersion;
  final String? shellVersion;
  final String? activeSessionId;
  final int? remainingSeconds;

  /// Live accrued time cost for an open-tab session (count-up). Null for fixed
  /// sessions (which expose RemainingSeconds instead) and unbilled guests.
  final int? accruedCostMinorUnits;
  final String? currencyCode;

  /// Optimistic-concurrency version of the active session; the operator echoes it back as
  /// ExpectedVersion on a seat mutation so a stale view loses the race with a 409.
  final int? sessionVersion;

  /// Who is on the seat right now: the active session's player display name. Null for a
  /// guest session with no account, or a free seat.
  final String? playerDisplayName;

  /// The tariff the active session bills against. Null for guest/package sessions that
  /// carry no named tariff, or a free seat.
  final String? tariffName;

  /// When the active session started (UTC) — lets the operator show real elapsed time.
  final DateTime? sessionStartedAtUtc;

  /// Когда с этого места позвали оператора. Null — не зовут. Время, а не флаг: стойке важно,
  /// кто ждёт дольше.
  final DateTime? assistanceRequestedAtUtc;

  /// С какого момента ПК на обслуживании по решению клуба. Null — ПК в зале. Отдельно от State:
  /// «обслуживание» на карте бывает и у неподтверждённого ПК, а вернуть в зал можно только того,
  /// кого туда увели.
  final DateTime? maintenanceSinceUtc;

  /// Место с консолью без агента: сессию ведёт администратор, команд ПК у места нет.
  final bool? isConsole;

  factory SeatStatusDto.fromJson(Map<String, dynamic> json) => SeatStatusDto(
        seatId: json['seatId'] as String,
        seatName: json['seatName'] as String,
        zoneId: json['zoneId'] as String,
        zoneName: json['zoneName'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
        state: json['state'] as String,
        deviceId: json['deviceId'] == null ? null : json['deviceId'] as String,
        deviceName: json['deviceName'] == null ? null : json['deviceName'] as String,
        isDeviceOnline: json['isDeviceOnline'] == null ? null : json['isDeviceOnline'] as bool,
        isDeviceLocked: json['isDeviceLocked'] == null ? null : json['isDeviceLocked'] as bool,
        lastHeartbeatAtUtc: json['lastHeartbeatAtUtc'] == null ? null : DateTime.parse(json['lastHeartbeatAtUtc'] as String),
        agentVersion: json['agentVersion'] == null ? null : json['agentVersion'] as String,
        shellVersion: json['shellVersion'] == null ? null : json['shellVersion'] as String,
        activeSessionId: json['activeSessionId'] == null ? null : json['activeSessionId'] as String,
        remainingSeconds: json['remainingSeconds'] == null ? null : (json['remainingSeconds'] as num).toInt(),
        accruedCostMinorUnits: json['accruedCostMinorUnits'] == null ? null : (json['accruedCostMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
        sessionVersion: json['sessionVersion'] == null ? null : (json['sessionVersion'] as num).toInt(),
        playerDisplayName: json['playerDisplayName'] == null ? null : json['playerDisplayName'] as String,
        tariffName: json['tariffName'] == null ? null : json['tariffName'] as String,
        sessionStartedAtUtc: json['sessionStartedAtUtc'] == null ? null : DateTime.parse(json['sessionStartedAtUtc'] as String),
        assistanceRequestedAtUtc: json['assistanceRequestedAtUtc'] == null ? null : DateTime.parse(json['assistanceRequestedAtUtc'] as String),
        maintenanceSinceUtc: json['maintenanceSinceUtc'] == null ? null : DateTime.parse(json['maintenanceSinceUtc'] as String),
        isConsole: json['isConsole'] == null ? null : json['isConsole'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'seatId': seatId,
        'seatName': seatName,
        'zoneId': zoneId,
        'zoneName': zoneName,
        'sortOrder': sortOrder,
        'state': state,
        'deviceId': deviceId,
        'deviceName': deviceName,
        'isDeviceOnline': isDeviceOnline,
        'isDeviceLocked': isDeviceLocked,
        'lastHeartbeatAtUtc': lastHeartbeatAtUtc?.toIso8601String(),
        'agentVersion': agentVersion,
        'shellVersion': shellVersion,
        'activeSessionId': activeSessionId,
        'remainingSeconds': remainingSeconds,
        'accruedCostMinorUnits': accruedCostMinorUnits,
        'currencyCode': currencyCode,
        'sessionVersion': sessionVersion,
        'playerDisplayName': playerDisplayName,
        'tariffName': tariffName,
        'sessionStartedAtUtc': sessionStartedAtUtc?.toIso8601String(),
        'assistanceRequestedAtUtc': assistanceRequestedAtUtc?.toIso8601String(),
        'maintenanceSinceUtc': maintenanceSinceUtc?.toIso8601String(),
        'isConsole': isConsole,
      };
}

/// Агент просит выдать себе новый ключ, предъявив действующий заголовком. Организация и филиал
/// в теле — те же, что в сердцебиении: сервер сверяет ключ именно с этой машиной, а не просто
/// «с каким-нибудь».
///
/// Контракт: Devices/SelfRotateDeviceCredentialRequest.cs
class SelfRotateDeviceCredentialRequest {
  const SelfRotateDeviceCredentialRequest({
    required this.organizationId,
    required this.branchId,
    required this.deviceId,
  });

  final String organizationId;
  final String branchId;
  final String deviceId;

  factory SelfRotateDeviceCredentialRequest.fromJson(Map<String, dynamic> json) => SelfRotateDeviceCredentialRequest(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        deviceId: json['deviceId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'deviceId': deviceId,
      };
}

/// Позвать в друзья по номеру — тому, который человек и так знает.
///
/// Контракт: Friends/FriendDtos.cs
class SendFriendRequestRequest {
  const SendFriendRequestRequest({
    required this.phoneNumber,
  });

  final String phoneNumber;

  factory SendFriendRequestRequest.fromJson(Map<String, dynamic> json) => SendFriendRequestRequest(
        phoneNumber: json['phoneNumber'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
      };
}

/// Проверка доставки почты: письмо уходит боевым путём, а причина отказа возвращается сразу.
/// Раньше единственным способом проверить почту было послать кому-то настоящее приглашение и
/// гадать по счётчику «провалено» без текста ошибки.
///
/// Контракт: Platform/Health/PlatformHealthContracts.cs
class SendTestEmailRequest {
  const SendTestEmailRequest({
    required this.email,
  });

  final String email;

  factory SendTestEmailRequest.fromJson(Map<String, dynamic> json) => SendTestEmailRequest(
        email: json['email'] as String,
      );

  Map<String, dynamic> toJson() => {
        'email': email,
      };
}

/// Контракт: Platform/Health/PlatformHealthContracts.cs
class SendTestEmailResultDto {
  const SendTestEmailResultDto({
    required this.delivered,
    this.error,
  });

  final bool delivered;
  final String? error;

  factory SendTestEmailResultDto.fromJson(Map<String, dynamic> json) => SendTestEmailResultDto(
        delivered: json['delivered'] as bool,
        error: json['error'] == null ? null : json['error'] as String,
      );

  Map<String, dynamic> toJson() => {
        'delivered': delivered,
        'error': error,
      };
}

/// Read-only preview of a session checkout: the bill breakdown the operator needs
/// to enter split payments (time charge + attached POS = grand total), the billable
/// seconds for the "Наиграно" display, and — when the session has a player — the
/// wallet balance so a wallet part can be auto-suggested. No state is changed.
///
/// Контракт: Sessions/SessionCheckoutQuoteResponse.cs
class SessionCheckoutQuoteResponse {
  const SessionCheckoutQuoteResponse({
    required this.sessionId,
    required this.timeCharge,
    required this.posTotal,
    required this.grandTotal,
    required this.billableSeconds,
    this.playerAccountId,
    this.walletBalance,
  });

  final String sessionId;
  final MoneyDto timeCharge;
  final MoneyDto posTotal;
  final MoneyDto grandTotal;
  final int billableSeconds;
  final String? playerAccountId;
  final MoneyDto? walletBalance;

  factory SessionCheckoutQuoteResponse.fromJson(Map<String, dynamic> json) => SessionCheckoutQuoteResponse(
        sessionId: json['sessionId'] as String,
        timeCharge: MoneyDto.fromJson(json['timeCharge'] as Map<String, dynamic>),
        posTotal: MoneyDto.fromJson(json['posTotal'] as Map<String, dynamic>),
        grandTotal: MoneyDto.fromJson(json['grandTotal'] as Map<String, dynamic>),
        billableSeconds: (json['billableSeconds'] as num).toInt(),
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        walletBalance: json['walletBalance'] == null ? null : MoneyDto.fromJson(json['walletBalance'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'timeCharge': timeCharge.toJson(),
        'posTotal': posTotal.toJson(),
        'grandTotal': grandTotal.toJson(),
        'billableSeconds': billableSeconds,
        'playerAccountId': playerAccountId,
        'walletBalance': walletBalance?.toJson(),
      };
}

/// Settle a session in one action: pay the unified bill (time charge + attached
/// POS sales) with one or more PaymentPartDto parts, then lock the PC.
///
/// Контракт: Sessions/SessionCheckoutRequest.cs
class SessionCheckoutRequest {
  const SessionCheckoutRequest({
    required this.organizationId,
    required this.payments,
    required this.idempotencyKey,
    this.expectedVersion,
  });

  final String organizationId;
  final List<PaymentPartDto> payments;
  final String idempotencyKey;
  final int? expectedVersion;

  factory SessionCheckoutRequest.fromJson(Map<String, dynamic> json) => SessionCheckoutRequest(
        organizationId: json['organizationId'] as String,
        payments: (json['payments'] as List<dynamic>).map((item) => PaymentPartDto.fromJson(item as Map<String, dynamic>)).toList(),
        idempotencyKey: json['idempotencyKey'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'payments': payments.map((item) => item.toJson()).toList(),
        'idempotencyKey': idempotencyKey,
        'expectedVersion': expectedVersion,
      };
}

/// Result of a unified session checkout: the bill breakdown, the recorded payment
/// parts, the single receipt covering time + POS, the ending session, and the lock
/// command dispatched to the device.
///
/// Контракт: Sessions/SessionCheckoutResponse.cs
class SessionCheckoutResponse {
  const SessionCheckoutResponse({
    required this.idempotencyKey,
    required this.sessionId,
    required this.timeCharge,
    required this.posTotal,
    required this.grandTotal,
    required this.payments,
    required this.receipt,
    required this.session,
    required this.deviceCommands,
  });

  final String idempotencyKey;
  final String sessionId;
  final MoneyDto timeCharge;
  final MoneyDto posTotal;
  final MoneyDto grandTotal;
  final List<PaymentPartDto> payments;
  final ReceiptDto receipt;
  final SessionDto session;
  final List<DeviceCommandDto> deviceCommands;

  factory SessionCheckoutResponse.fromJson(Map<String, dynamic> json) => SessionCheckoutResponse(
        idempotencyKey: json['idempotencyKey'] as String,
        sessionId: json['sessionId'] as String,
        timeCharge: MoneyDto.fromJson(json['timeCharge'] as Map<String, dynamic>),
        posTotal: MoneyDto.fromJson(json['posTotal'] as Map<String, dynamic>),
        grandTotal: MoneyDto.fromJson(json['grandTotal'] as Map<String, dynamic>),
        payments: (json['payments'] as List<dynamic>).map((item) => PaymentPartDto.fromJson(item as Map<String, dynamic>)).toList(),
        receipt: ReceiptDto.fromJson(json['receipt'] as Map<String, dynamic>),
        session: SessionDto.fromJson(json['session'] as Map<String, dynamic>),
        deviceCommands: (json['deviceCommands'] as List<dynamic>).map((item) => DeviceCommandDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'idempotencyKey': idempotencyKey,
        'sessionId': sessionId,
        'timeCharge': timeCharge.toJson(),
        'posTotal': posTotal.toJson(),
        'grandTotal': grandTotal.toJson(),
        'payments': payments.map((item) => item.toJson()).toList(),
        'receipt': receipt.toJson(),
        'session': session.toJson(),
        'deviceCommands': deviceCommands.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Sessions/SessionCommandResponse.cs
class SessionCommandResponse {
  const SessionCommandResponse({
    required this.idempotencyKey,
    required this.session,
    required this.deviceCommands,
    this.compValueMinorUnits,
  });

  final String idempotencyKey;
  final SessionDto session;
  final List<DeviceCommandDto> deviceCommands;

  /// Anti-fraud §5.4: the assessed value of a comp (free) session; null for non-comp starts.
  final int? compValueMinorUnits;

  factory SessionCommandResponse.fromJson(Map<String, dynamic> json) => SessionCommandResponse(
        idempotencyKey: json['idempotencyKey'] as String,
        session: SessionDto.fromJson(json['session'] as Map<String, dynamic>),
        deviceCommands: (json['deviceCommands'] as List<dynamic>).map((item) => DeviceCommandDto.fromJson(item as Map<String, dynamic>)).toList(),
        compValueMinorUnits: json['compValueMinorUnits'] == null ? null : (json['compValueMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'idempotencyKey': idempotencyKey,
        'session': session.toJson(),
        'deviceCommands': deviceCommands.map((item) => item.toJson()).toList(),
        'compValueMinorUnits': compValueMinorUnits,
      };
}

/// Контракт: Sessions/SessionDto.cs
class SessionDto {
  const SessionDto({
    required this.sessionId,
    required this.organizationId,
    required this.branchId,
    required this.seatId,
    required this.deviceId,
    required this.state,
    required this.tariffRuleVersionId,
    this.startedAtUtc,
    this.endsAtUtc,
    this.endedAtUtc,
    this.remainingSeconds,
    this.currentLease,
    this.version,
  });

  final String sessionId;
  final String organizationId;
  final String branchId;
  final String seatId;
  final String deviceId;
  final String state;
  final String tariffRuleVersionId;
  final DateTime? startedAtUtc;
  final DateTime? endsAtUtc;
  final DateTime? endedAtUtc;
  final int? remainingSeconds;
  final SessionLeaseDto? currentLease;

  /// Optimistic-concurrency version the client echoes back as ExpectedVersion on the next mutation.
  final int? version;

  factory SessionDto.fromJson(Map<String, dynamic> json) => SessionDto(
        sessionId: json['sessionId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        seatId: json['seatId'] as String,
        deviceId: json['deviceId'] as String,
        state: json['state'] as String,
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        startedAtUtc: json['startedAtUtc'] == null ? null : DateTime.parse(json['startedAtUtc'] as String),
        endsAtUtc: json['endsAtUtc'] == null ? null : DateTime.parse(json['endsAtUtc'] as String),
        endedAtUtc: json['endedAtUtc'] == null ? null : DateTime.parse(json['endedAtUtc'] as String),
        remainingSeconds: json['remainingSeconds'] == null ? null : (json['remainingSeconds'] as num).toInt(),
        currentLease: json['currentLease'] == null ? null : SessionLeaseDto.fromJson(json['currentLease'] as Map<String, dynamic>),
        version: json['version'] == null ? null : (json['version'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'organizationId': organizationId,
        'branchId': branchId,
        'seatId': seatId,
        'deviceId': deviceId,
        'state': state,
        'tariffRuleVersionId': tariffRuleVersionId,
        'startedAtUtc': startedAtUtc?.toIso8601String(),
        'endsAtUtc': endsAtUtc?.toIso8601String(),
        'endedAtUtc': endedAtUtc?.toIso8601String(),
        'remainingSeconds': remainingSeconds,
        'currentLease': currentLease?.toJson(),
        'version': version,
      };
}

/// Контракт: Sessions/SessionLeaseDto.cs
class SessionLeaseDto {
  const SessionLeaseDto({
    required this.sessionId,
    required this.organizationId,
    required this.branchId,
    required this.seatId,
    required this.deviceId,
    required this.state,
    required this.sequence,
    required this.issuedAtUtc,
    required this.expiresAtUtc,
    required this.signatureAlgorithm,
    required this.signature,
  });

  final String sessionId;
  final String organizationId;
  final String branchId;
  final String seatId;
  final String deviceId;
  final String state;
  final int sequence;
  final DateTime issuedAtUtc;
  final DateTime expiresAtUtc;
  final String signatureAlgorithm;
  final String signature;

  factory SessionLeaseDto.fromJson(Map<String, dynamic> json) => SessionLeaseDto(
        sessionId: json['sessionId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        seatId: json['seatId'] as String,
        deviceId: json['deviceId'] as String,
        state: json['state'] as String,
        sequence: (json['sequence'] as num).toInt(),
        issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String),
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        signatureAlgorithm: json['signatureAlgorithm'] as String,
        signature: json['signature'] as String,
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'organizationId': organizationId,
        'branchId': branchId,
        'seatId': seatId,
        'deviceId': deviceId,
        'state': state,
        'sequence': sequence,
        'issuedAtUtc': issuedAtUtc.toIso8601String(),
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
        'signatureAlgorithm': signatureAlgorithm,
        'signature': signature,
      };
}

/// A session-lifecycle change broadcast over the DeviceHub branch group so operator clients can
/// patch the floor map and apply a dashboard delta without polling. Pushes are hints; the client
/// reconciles against an authoritative reload and ignores an event whose Version is
/// not newer than the one it already applied for that session.
///
/// Контракт: Sessions/SessionLifecycleChangedDto.cs
class SessionLifecycleChangedDto {
  const SessionLifecycleChangedDto({
    required this.organizationId,
    required this.branchId,
    required this.seatId,
    required this.sessionId,
    required this.kind,
    required this.state,
    required this.version,
    this.startedAtUtc,
    this.endsAtUtc,
    required this.observedAtUtc,
    this.accruedCostMinorUnits,
    this.currencyCode,
  });

  final String organizationId;
  final String branchId;
  final String seatId;
  final String sessionId;
  final String kind;
  final String state;
  final int version;
  final DateTime? startedAtUtc;
  final DateTime? endsAtUtc;
  final DateTime observedAtUtc;
  final int? accruedCostMinorUnits;
  final String? currencyCode;

  factory SessionLifecycleChangedDto.fromJson(Map<String, dynamic> json) => SessionLifecycleChangedDto(
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        seatId: json['seatId'] as String,
        sessionId: json['sessionId'] as String,
        kind: json['kind'] as String,
        state: json['state'] as String,
        version: (json['version'] as num).toInt(),
        startedAtUtc: json['startedAtUtc'] == null ? null : DateTime.parse(json['startedAtUtc'] as String),
        endsAtUtc: json['endsAtUtc'] == null ? null : DateTime.parse(json['endsAtUtc'] as String),
        observedAtUtc: DateTime.parse(json['observedAtUtc'] as String),
        accruedCostMinorUnits: json['accruedCostMinorUnits'] == null ? null : (json['accruedCostMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] == null ? null : json['currencyCode'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'branchId': branchId,
        'seatId': seatId,
        'sessionId': sessionId,
        'kind': kind,
        'state': state,
        'version': version,
        'startedAtUtc': startedAtUtc?.toIso8601String(),
        'endsAtUtc': endsAtUtc?.toIso8601String(),
        'observedAtUtc': observedAtUtc.toIso8601String(),
        'accruedCostMinorUnits': accruedCostMinorUnits,
        'currencyCode': currencyCode,
      };
}

/// Контракт: Sessions/SessionReconciliationResponse.cs
class SessionReconciliationResponse {
  const SessionReconciliationResponse({
    required this.action,
    required this.reason,
    this.sessionId,
    this.lease,
  });

  final String action;
  final String reason;
  final String? sessionId;
  final SessionLeaseDto? lease;

  factory SessionReconciliationResponse.fromJson(Map<String, dynamic> json) => SessionReconciliationResponse(
        action: json['action'] as String,
        reason: json['reason'] as String,
        sessionId: json['sessionId'] == null ? null : json['sessionId'] as String,
        lease: json['lease'] == null ? null : SessionLeaseDto.fromJson(json['lease'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'action': action,
        'reason': reason,
        'sessionId': sessionId,
        'lease': lease?.toJson(),
      };
}

/// A started game session projected onto the booking timeline: when it began, its scheduled end
/// (fixed/prepaid) and its actual end (null while still running). The operator UI derives the bar —
/// open tab (no scheduled, no actual end) renders open-ended; otherwise bounded.
///
/// Контракт: Sessions/SessionTimelineDto.cs
class SessionTimelineItemDto {
  const SessionTimelineItemDto({
    required this.sessionId,
    required this.seatId,
    required this.seatName,
    required this.zoneId,
    required this.zoneName,
    required this.state,
    this.playerAccountId,
    this.playerDisplayName,
    this.tariffName,
    required this.startedAtUtc,
    this.endsAtUtc,
    this.endedAtUtc,
  });

  final String sessionId;
  final String seatId;
  final String seatName;
  final String zoneId;
  final String zoneName;
  final String state;
  final String? playerAccountId;
  final String? playerDisplayName;
  final String? tariffName;
  final DateTime startedAtUtc;
  final DateTime? endsAtUtc;
  final DateTime? endedAtUtc;

  factory SessionTimelineItemDto.fromJson(Map<String, dynamic> json) => SessionTimelineItemDto(
        sessionId: json['sessionId'] as String,
        seatId: json['seatId'] as String,
        seatName: json['seatName'] as String,
        zoneId: json['zoneId'] as String,
        zoneName: json['zoneName'] as String,
        state: json['state'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        playerDisplayName: json['playerDisplayName'] == null ? null : json['playerDisplayName'] as String,
        tariffName: json['tariffName'] == null ? null : json['tariffName'] as String,
        startedAtUtc: DateTime.parse(json['startedAtUtc'] as String),
        endsAtUtc: json['endsAtUtc'] == null ? null : DateTime.parse(json['endsAtUtc'] as String),
        endedAtUtc: json['endedAtUtc'] == null ? null : DateTime.parse(json['endedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'sessionId': sessionId,
        'seatId': seatId,
        'seatName': seatName,
        'zoneId': zoneId,
        'zoneName': zoneName,
        'state': state,
        'playerAccountId': playerAccountId,
        'playerDisplayName': playerDisplayName,
        'tariffName': tariffName,
        'startedAtUtc': startedAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc?.toIso8601String(),
        'endedAtUtc': endedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Sessions/SessionTimelineDto.cs
class SessionTimelineResult {
  const SessionTimelineResult({
    required this.sessions,
  });

  final List<SessionTimelineItemDto> sessions;

  factory SessionTimelineResult.fromJson(Map<String, dynamic> json) => SessionTimelineResult(
        sessions: (json['sessions'] as List<dynamic>).map((item) => SessionTimelineItemDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'sessions': sessions.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Ads/AdContracts.cs
class SetAdCampaignStateRequest {
  const SetAdCampaignStateRequest({
    required this.state,
  });


  /// Одно из AdCampaignStateNames
  final String state;

  factory SetAdCampaignStateRequest.fromJson(Map<String, dynamic> json) => SetAdCampaignStateRequest(
        state: json['state'] as String,
      );

  Map<String, dynamic> toJson() => {
        'state': state,
      };
}

/// Постановка ручного исключения для клуба. Причина обязательна.
///
/// Контракт: Platform/Features/FeatureContracts.cs
class SetFeatureOverrideRequest {
  const SetFeatureOverrideRequest({
    required this.isEnabled,
    required this.reason,
  });

  final bool isEnabled;
  final String reason;

  factory SetFeatureOverrideRequest.fromJson(Map<String, dynamic> json) => SetFeatureOverrideRequest(
        isEnabled: json['isEnabled'] as bool,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'isEnabled': isEnabled,
        'reason': reason,
      };
}

/// Новый сетевой PIN. Старый здесь не спрашивается намеренно: человек уже вошёл в приложение, а
/// потребовать старое значило бы запереть выход ровно тому, кто его забыл. Именно это и делает
/// приложение единственным местом, где PIN задают, — ни одной SMS на это не тратится.
///
/// Контракт: Identity/PinContracts.cs
class SetMyPinRequest {
  const SetMyPinRequest({
    required this.pin,
  });

  final String pin;

  factory SetMyPinRequest.fromJson(Map<String, dynamic> json) => SetMyPinRequest(
        pin: json['pin'] as String,
      );

  Map<String, dynamic> toJson() => {
        'pin': pin,
      };
}

/// Причина обязательна: запрет без неё некому объяснить и не на каком основании снять.
///
/// Контракт: Platform/People/NetworkPeopleContracts.cs
class SetNetworkBanRequest {
  const SetNetworkBanRequest({
    required this.reason,
  });

  final String reason;

  factory SetNetworkBanRequest.fromJson(Map<String, dynamic> json) => SetNetworkBanRequest(
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reason': reason,
      };
}

/// Контракт: Players/SetPlayerActiveStateRequest.cs
class SetPlayerActiveStateRequest {
  const SetPlayerActiveStateRequest({
    required this.organizationId,
    required this.isActive,
  });

  final String organizationId;
  final bool isActive;

  factory SetPlayerActiveStateRequest.fromJson(Map<String, dynamic> json) => SetPlayerActiveStateRequest(
        organizationId: json['organizationId'] as String,
        isActive: json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'isActive': isActive,
      };
}

/// Контракт: Pos/SettlePosSaleRequest.cs
class SettlePosSaleRequest {
  const SettlePosSaleRequest({
    required this.organizationId,
    required this.payments,
    required this.note,
    required this.idempotencyKey,
  });

  final String organizationId;
  final List<PaymentPartDto> payments;
  final String note;
  final String idempotencyKey;

  factory SettlePosSaleRequest.fromJson(Map<String, dynamic> json) => SettlePosSaleRequest(
        organizationId: json['organizationId'] as String,
        payments: (json['payments'] as List<dynamic>).map((item) => PaymentPartDto.fromJson(item as Map<String, dynamic>)).toList(),
        note: json['note'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'payments': payments.map((item) => item.toJson()).toList(),
        'note': note,
        'idempotencyKey': idempotencyKey,
      };
}

/// Контракт: Shell/ShellBridgeContracts.cs
class ShellAuthSignInRequest {
  const ShellAuthSignInRequest({
    required this.phone,
    required this.pin,
  });

  final String phone;
  final String pin;

  factory ShellAuthSignInRequest.fromJson(Map<String, dynamic> json) => ShellAuthSignInRequest(
        phone: json['phone'] as String,
        pin: json['pin'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phone': phone,
        'pin': pin,
      };
}

/// Кто вошёл на этом ПК. Токены страница не видит: их держит хост.
///
/// Контракт: Shell/ShellBridgeContracts.cs
class ShellAuthStateDto {
  const ShellAuthStateDto({
    required this.signedIn,
    this.displayName,
    this.playerAccountId,
  });

  final bool signedIn;
  final String? displayName;
  final String? playerAccountId;

  factory ShellAuthStateDto.fromJson(Map<String, dynamic> json) => ShellAuthStateDto(
        signedIn: json['signedIn'] as bool,
        displayName: json['displayName'] == null ? null : json['displayName'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'signedIn': signedIn,
        'displayName': displayName,
        'playerAccountId': playerAccountId,
      };
}

/// Контракт: Shell/ShellBrandingDto.cs
class ShellBrandingDto {
  const ShellBrandingDto({
    required this.clubName,
    this.logoUrl,
    this.accentColor,
  });

  final String clubName;
  final String? logoUrl;
  final String? accentColor;

  factory ShellBrandingDto.fromJson(Map<String, dynamic> json) => ShellBrandingDto(
        clubName: json['clubName'] as String,
        logoUrl: json['logoUrl'] == null ? null : json['logoUrl'] as String,
        accentColor: json['accentColor'] == null ? null : json['accentColor'] as String,
      );

  Map<String, dynamic> toJson() => {
        'clubName': clubName,
        'logoUrl': logoUrl,
        'accentColor': accentColor,
      };
}

/// Контракт: Shell/ShellBridgeContracts.cs
class ShellGameForegroundDto {
  const ShellGameForegroundDto({
    required this.active,
  });

  final bool active;

  factory ShellGameForegroundDto.fromJson(Map<String, dynamic> json) => ShellGameForegroundDto(
        active: json['active'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'active': active,
      };
}

/// Контракт: Shell/ShellBridgeContracts.cs
class ShellLaunchRequest {
  const ShellLaunchRequest({
    required this.appId,
  });

  final String appId;

  factory ShellLaunchRequest.fromJson(Map<String, dynamic> json) => ShellLaunchRequest(
        appId: json['appId'] as String,
      );

  Map<String, dynamic> toJson() => {
        'appId': appId,
      };
}

/// Команда клуба, которую исполняет хост: у агента нет ни окна, ни аккаунта игрока.
///
/// Контракт: Shell/ShellPipeMessage.cs
class ShellPipeCommandDto {
  const ShellPipeCommandDto({
    required this.commandId,
    required this.type,
    this.text,
  });

  final String commandId;

  /// DeviceCommandTypeNames.SignOut или DeviceCommandTypeNames.Message.
  final String type;

  /// Текст сообщения; только у message.
  final String? text;

  factory ShellPipeCommandDto.fromJson(Map<String, dynamic> json) => ShellPipeCommandDto(
        commandId: json['commandId'] as String,
        type: json['type'] as String,
        text: json['text'] == null ? null : json['text'] as String,
      );

  Map<String, dynamic> toJson() => {
        'commandId': commandId,
        'type': type,
        'text': text,
      };
}

/// Контракт: Shell/ShellPipeMessage.cs
class ShellPipeHelloDto {
  const ShellPipeHelloDto({
    required this.protocol,
    required this.hostVersion,
    required this.sessionId,
  });

  final int protocol;
  final String hostVersion;

  /// Сессия Windows, в которой живёт хост. Агент сверяет её с консольной: хост из чужой
  /// сессии получать состояние этого ПК не должен.
  final int sessionId;

  factory ShellPipeHelloDto.fromJson(Map<String, dynamic> json) => ShellPipeHelloDto(
        protocol: (json['protocol'] as num).toInt(),
        hostVersion: json['hostVersion'] as String,
        sessionId: (json['sessionId'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'protocol': protocol,
        'hostVersion': hostVersion,
        'sessionId': sessionId,
      };
}

/// Один кадр канала агент ↔ хост. Заполнено ровно то поле, которое называет Type:
/// так кадр читается одним типом, без второго разбора по виду сообщения.
///
/// Контракт: Shell/ShellPipeMessage.cs
class ShellPipeMessage {
  const ShellPipeMessage({
    required this.type,
    this.hello,
    this.state,
    this.request,
    this.reply,
    this.reason,
    this.command,
    this.auth,
  });


  /// Одно из ShellPipeMessageTypeNames.
  final String type;
  final ShellPipeHelloDto? hello;
  final PlayerShellStateDto? state;
  final ShellPipeRequestDto? request;
  final ShellPipeReplyDto? reply;

  /// Почему агент попрощался; только у bye.
  final String? reason;
  final ShellPipeCommandDto? command;

  /// Игрок вошёл на этом ПК — номером и ПИН-кодом или по QR с телефона. Токены привязаны к ПК;
  /// хост держит их в памяти и странице не отдаёт.
  final PlatformPersonSessionResponse? auth;

  factory ShellPipeMessage.fromJson(Map<String, dynamic> json) => ShellPipeMessage(
        type: json['type'] as String,
        hello: json['hello'] == null ? null : ShellPipeHelloDto.fromJson(json['hello'] as Map<String, dynamic>),
        state: json['state'] == null ? null : PlayerShellStateDto.fromJson(json['state'] as Map<String, dynamic>),
        request: json['request'] == null ? null : ShellPipeRequestDto.fromJson(json['request'] as Map<String, dynamic>),
        reply: json['reply'] == null ? null : ShellPipeReplyDto.fromJson(json['reply'] as Map<String, dynamic>),
        reason: json['reason'] == null ? null : json['reason'] as String,
        command: json['command'] == null ? null : ShellPipeCommandDto.fromJson(json['command'] as Map<String, dynamic>),
        auth: json['auth'] == null ? null : PlatformPersonSessionResponse.fromJson(json['auth'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'type': type,
        'hello': hello?.toJson(),
        'state': state?.toJson(),
        'request': request?.toJson(),
        'reply': reply?.toJson(),
        'reason': reason,
        'command': command?.toJson(),
        'auth': auth?.toJson(),
      };
}

/// Контракт: Shell/ShellPipeMessage.cs
class ShellPipeReplyDto {
  const ShellPipeReplyDto({
    required this.requestId,
    required this.ok,
    this.errorCode,
    this.message,
  });

  final String requestId;
  final bool ok;

  /// Одно из ShellPipeErrorCodeNames; пусто при успехе.
  final String? errorCode;
  final String? message;

  factory ShellPipeReplyDto.fromJson(Map<String, dynamic> json) => ShellPipeReplyDto(
        requestId: json['requestId'] as String,
        ok: json['ok'] as bool,
        errorCode: json['errorCode'] == null ? null : json['errorCode'] as String,
        message: json['message'] == null ? null : json['message'] as String,
      );

  Map<String, dynamic> toJson() => {
        'requestId': requestId,
        'ok': ok,
        'errorCode': errorCode,
        'message': message,
      };
}

/// Контракт: Shell/ShellPipeMessage.cs
class ShellPipeRequestDto {
  const ShellPipeRequestDto({
    required this.requestId,
    required this.type,
    required this.payload,
  });

  final String requestId;

  /// Одно из ShellPipeRequestTypeNames.
  final String type;
  final Map<String, String> payload;

  factory ShellPipeRequestDto.fromJson(Map<String, dynamic> json) => ShellPipeRequestDto(
        requestId: json['requestId'] as String,
        type: json['type'] as String,
        payload: (json['payload'] as Map<String, dynamic>).map((key, value) => MapEntry(key, value as String)),
      );

  Map<String, dynamic> toJson() => {
        'requestId': requestId,
        'type': type,
        'payload': payload.map((key, value) => MapEntry(key, value)),
      };
}

/// Контракт: Shell/ShellBridgeContracts.cs
class ShellSetLayoutRequest {
  const ShellSetLayoutRequest({
    required this.layout,
  });


  /// Одно из ShellKeyboardLayoutNames.
  final String layout;

  factory ShellSetLayoutRequest.fromJson(Map<String, dynamic> json) => ShellSetLayoutRequest(
        layout: json['layout'] as String,
      );

  Map<String, dynamic> toJson() => {
        'layout': layout,
      };
}

/// Контракт: Shell/ShellBridgeContracts.cs
class ShellSetMicMutedRequest {
  const ShellSetMicMutedRequest({
    required this.micMuted,
  });

  final bool micMuted;

  factory ShellSetMicMutedRequest.fromJson(Map<String, dynamic> json) => ShellSetMicMutedRequest(
        micMuted: json['micMuted'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'micMuted': micMuted,
      };
}

/// Контракт: Shell/ShellBridgeContracts.cs
class ShellSetVolumeRequest {
  const ShellSetVolumeRequest({
    required this.volume,
  });


  /// 0–100.
  final int volume;

  factory ShellSetVolumeRequest.fromJson(Map<String, dynamic> json) => ShellSetVolumeRequest(
        volume: (json['volume'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'volume': volume,
      };
}

/// Показ карточки витрины: какая и сколько миллисекунд стояла на экране.
///
/// Контракт: Shell/ShellBridgeContracts.cs
class ShellShowcaseImpressionDto {
  const ShellShowcaseImpressionDto({
    required this.cardId,
    required this.shownMs,
  });

  final String cardId;
  final int shownMs;

  factory ShellShowcaseImpressionDto.fromJson(Map<String, dynamic> json) => ShellShowcaseImpressionDto(
        cardId: json['cardId'] as String,
        shownMs: (json['shownMs'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'cardId': cardId,
        'shownMs': shownMs,
      };
}

/// Всё, что хост знает к моменту, когда страница загрузилась.
///
/// Контракт: Shell/ShellBridgeContracts.cs
class ShellSnapshotDto {
  const ShellSnapshotDto({
    this.state,
    required this.auth,
    this.system,
  });


  /// Пусто — агент ещё не прислал состояния: экран говорит «подключаемся к ПК».
  final PlayerShellStateDto? state;
  final ShellAuthStateDto auth;
  final ShellSystemStateDto? system;

  factory ShellSnapshotDto.fromJson(Map<String, dynamic> json) => ShellSnapshotDto(
        state: json['state'] == null ? null : PlayerShellStateDto.fromJson(json['state'] as Map<String, dynamic>),
        auth: ShellAuthStateDto.fromJson(json['auth'] as Map<String, dynamic>),
        system: json['system'] == null ? null : ShellSystemStateDto.fromJson(json['system'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'state': state?.toJson(),
        'auth': auth.toJson(),
        'system': system?.toJson(),
      };
}

/// Звук, микрофон и раскладка ПК. Пусто — у ПК этого нет или Windows не ответила (нет
/// микрофона, звуковая карта отключена): страница прячет кнопку, а не показывает выдуманное.
///
/// Контракт: Shell/ShellBridgeContracts.cs
class ShellSystemStateDto {
  const ShellSystemStateDto({
    this.volume,
    this.micMuted,
    this.layout,
  });


  /// 0–100.
  final int? volume;
  final bool? micMuted;

  /// Раскладка клавиатуры — одно из ShellKeyboardLayoutNames.
  final String? layout;

  factory ShellSystemStateDto.fromJson(Map<String, dynamic> json) => ShellSystemStateDto(
        volume: json['volume'] == null ? null : (json['volume'] as num).toInt(),
        micMuted: json['micMuted'] == null ? null : json['micMuted'] as bool,
        layout: json['layout'] == null ? null : json['layout'] as String,
      );

  Map<String, dynamic> toJson() => {
        'volume': volume,
        'micMuted': micMuted,
        'layout': layout,
      };
}

/// Контракт: Shifts/ShiftDto.cs
class ShiftDto {
  const ShiftDto({
    required this.shiftId,
    required this.organizationId,
    required this.branchId,
    required this.openedByStaffUserId,
    this.closedByStaffUserId,
    required this.state,
    required this.startingCash,
    this.countedCash,
    this.expectedCash,
    this.difference,
    required this.openingNote,
    required this.closingNote,
    required this.openedAtUtc,
    this.closedAtUtc,
    this.managerSignOffStaffUserId,
    this.signOffReason,
  });

  final String shiftId;
  final String organizationId;
  final String branchId;
  final String openedByStaffUserId;
  final String? closedByStaffUserId;
  final String state;
  final MoneyDto startingCash;
  final MoneyDto? countedCash;
  final MoneyDto? expectedCash;
  final MoneyDto? difference;
  final String openingNote;
  final String closingNote;
  final DateTime openedAtUtc;
  final DateTime? closedAtUtc;
  final String? managerSignOffStaffUserId;
  final String? signOffReason;

  factory ShiftDto.fromJson(Map<String, dynamic> json) => ShiftDto(
        shiftId: json['shiftId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        openedByStaffUserId: json['openedByStaffUserId'] as String,
        closedByStaffUserId: json['closedByStaffUserId'] == null ? null : json['closedByStaffUserId'] as String,
        state: json['state'] as String,
        startingCash: MoneyDto.fromJson(json['startingCash'] as Map<String, dynamic>),
        countedCash: json['countedCash'] == null ? null : MoneyDto.fromJson(json['countedCash'] as Map<String, dynamic>),
        expectedCash: json['expectedCash'] == null ? null : MoneyDto.fromJson(json['expectedCash'] as Map<String, dynamic>),
        difference: json['difference'] == null ? null : MoneyDto.fromJson(json['difference'] as Map<String, dynamic>),
        openingNote: json['openingNote'] as String,
        closingNote: json['closingNote'] as String,
        openedAtUtc: DateTime.parse(json['openedAtUtc'] as String),
        closedAtUtc: json['closedAtUtc'] == null ? null : DateTime.parse(json['closedAtUtc'] as String),
        managerSignOffStaffUserId: json['managerSignOffStaffUserId'] == null ? null : json['managerSignOffStaffUserId'] as String,
        signOffReason: json['signOffReason'] == null ? null : json['signOffReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'organizationId': organizationId,
        'branchId': branchId,
        'openedByStaffUserId': openedByStaffUserId,
        'closedByStaffUserId': closedByStaffUserId,
        'state': state,
        'startingCash': startingCash.toJson(),
        'countedCash': countedCash?.toJson(),
        'expectedCash': expectedCash?.toJson(),
        'difference': difference?.toJson(),
        'openingNote': openingNote,
        'closingNote': closingNote,
        'openedAtUtc': openedAtUtc.toIso8601String(),
        'closedAtUtc': closedAtUtc?.toIso8601String(),
        'managerSignOffStaffUserId': managerSignOffStaffUserId,
        'signOffReason': signOffReason,
      };
}

/// Контракт: Reports/ShiftReportResultDto.cs
class ShiftReportResultDto {
  const ShiftReportResultDto({
    required this.rows,
    required this.limit,
  });

  final List<ShiftReportRowDto> rows;
  final int limit;

  factory ShiftReportResultDto.fromJson(Map<String, dynamic> json) => ShiftReportResultDto(
        rows: (json['rows'] as List<dynamic>).map((item) => ShiftReportRowDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'rows': rows.map((item) => item.toJson()).toList(),
        'limit': limit,
      };
}

/// Контракт: Reports/ShiftReportRowDto.cs
class ShiftReportRowDto {
  const ShiftReportRowDto({
    required this.shiftId,
    required this.organizationId,
    required this.branchId,
    required this.openedByStaffUserId,
    this.closedByStaffUserId,
    required this.state,
    required this.startingCash,
    required this.cashMovementsTotal,
    required this.posCashPaymentsTotal,
    required this.posRefundsTotal,
    required this.billingCashImpactTotal,
    required this.expectedCash,
    this.countedCash,
    this.difference,
    required this.openedAtUtc,
    this.closedAtUtc,
  });

  final String shiftId;
  final String organizationId;
  final String branchId;
  final String openedByStaffUserId;
  final String? closedByStaffUserId;
  final String state;
  final MoneyDto startingCash;
  final MoneyDto cashMovementsTotal;
  final MoneyDto posCashPaymentsTotal;
  final MoneyDto posRefundsTotal;
  final MoneyDto billingCashImpactTotal;
  final MoneyDto expectedCash;
  final MoneyDto? countedCash;
  final MoneyDto? difference;
  final DateTime openedAtUtc;
  final DateTime? closedAtUtc;

  factory ShiftReportRowDto.fromJson(Map<String, dynamic> json) => ShiftReportRowDto(
        shiftId: json['shiftId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        openedByStaffUserId: json['openedByStaffUserId'] as String,
        closedByStaffUserId: json['closedByStaffUserId'] == null ? null : json['closedByStaffUserId'] as String,
        state: json['state'] as String,
        startingCash: MoneyDto.fromJson(json['startingCash'] as Map<String, dynamic>),
        cashMovementsTotal: MoneyDto.fromJson(json['cashMovementsTotal'] as Map<String, dynamic>),
        posCashPaymentsTotal: MoneyDto.fromJson(json['posCashPaymentsTotal'] as Map<String, dynamic>),
        posRefundsTotal: MoneyDto.fromJson(json['posRefundsTotal'] as Map<String, dynamic>),
        billingCashImpactTotal: MoneyDto.fromJson(json['billingCashImpactTotal'] as Map<String, dynamic>),
        expectedCash: MoneyDto.fromJson(json['expectedCash'] as Map<String, dynamic>),
        countedCash: json['countedCash'] == null ? null : MoneyDto.fromJson(json['countedCash'] as Map<String, dynamic>),
        difference: json['difference'] == null ? null : MoneyDto.fromJson(json['difference'] as Map<String, dynamic>),
        openedAtUtc: DateTime.parse(json['openedAtUtc'] as String),
        closedAtUtc: json['closedAtUtc'] == null ? null : DateTime.parse(json['closedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'organizationId': organizationId,
        'branchId': branchId,
        'openedByStaffUserId': openedByStaffUserId,
        'closedByStaffUserId': closedByStaffUserId,
        'state': state,
        'startingCash': startingCash.toJson(),
        'cashMovementsTotal': cashMovementsTotal.toJson(),
        'posCashPaymentsTotal': posCashPaymentsTotal.toJson(),
        'posRefundsTotal': posRefundsTotal.toJson(),
        'billingCashImpactTotal': billingCashImpactTotal.toJson(),
        'expectedCash': expectedCash.toJson(),
        'countedCash': countedCash?.toJson(),
        'difference': difference?.toJson(),
        'openedAtUtc': openedAtUtc.toIso8601String(),
        'closedAtUtc': closedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Shifts/ShiftRevenueDto.cs
class ShiftRevenueDto {
  const ShiftRevenueDto({
    required this.shiftId,
    required this.organizationId,
    required this.branchId,
    required this.openedByStaffUserId,
    this.closedByStaffUserId,
    required this.state,
    required this.earned,
    required this.inflow,
    required this.cash,
    required this.openedAtUtc,
    this.closedAtUtc,
  });

  final String shiftId;
  final String organizationId;
  final String branchId;
  final String openedByStaffUserId;
  final String? closedByStaffUserId;
  final String state;
  final EarnedBreakdownDto earned;
  final InflowBreakdownDto inflow;
  final CashReconciliationDto cash;
  final DateTime openedAtUtc;
  final DateTime? closedAtUtc;

  factory ShiftRevenueDto.fromJson(Map<String, dynamic> json) => ShiftRevenueDto(
        shiftId: json['shiftId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        openedByStaffUserId: json['openedByStaffUserId'] as String,
        closedByStaffUserId: json['closedByStaffUserId'] == null ? null : json['closedByStaffUserId'] as String,
        state: json['state'] as String,
        earned: EarnedBreakdownDto.fromJson(json['earned'] as Map<String, dynamic>),
        inflow: InflowBreakdownDto.fromJson(json['inflow'] as Map<String, dynamic>),
        cash: CashReconciliationDto.fromJson(json['cash'] as Map<String, dynamic>),
        openedAtUtc: DateTime.parse(json['openedAtUtc'] as String),
        closedAtUtc: json['closedAtUtc'] == null ? null : DateTime.parse(json['closedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'organizationId': organizationId,
        'branchId': branchId,
        'openedByStaffUserId': openedByStaffUserId,
        'closedByStaffUserId': closedByStaffUserId,
        'state': state,
        'earned': earned.toJson(),
        'inflow': inflow.toJson(),
        'cash': cash.toJson(),
        'openedAtUtc': openedAtUtc.toIso8601String(),
        'closedAtUtc': closedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Shifts/ShiftRevenueDto.cs
class ShiftRevenueListDto {
  const ShiftRevenueListDto({
    required this.shifts,
    required this.limit,
  });

  final List<ShiftRevenueDto> shifts;
  final int limit;

  factory ShiftRevenueListDto.fromJson(Map<String, dynamic> json) => ShiftRevenueListDto(
        shifts: (json['shifts'] as List<dynamic>).map((item) => ShiftRevenueDto.fromJson(item as Map<String, dynamic>)).toList(),
        limit: (json['limit'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'shifts': shifts.map((item) => item.toJson()).toList(),
        'limit': limit,
      };
}

/// Контракт: Shifts/ShiftSummaryDto.cs
class ShiftSummaryDto {
  const ShiftSummaryDto({
    required this.shiftId,
    required this.startingCash,
    required this.cashMovementsTotal,
    required this.posCashPaymentsTotal,
    required this.posRefundsTotal,
    required this.expectedCash,
    required this.countedCash,
    required this.difference,
  });

  final String shiftId;
  final MoneyDto startingCash;
  final MoneyDto cashMovementsTotal;
  final MoneyDto posCashPaymentsTotal;
  final MoneyDto posRefundsTotal;
  final MoneyDto expectedCash;
  final MoneyDto countedCash;
  final MoneyDto difference;

  factory ShiftSummaryDto.fromJson(Map<String, dynamic> json) => ShiftSummaryDto(
        shiftId: json['shiftId'] as String,
        startingCash: MoneyDto.fromJson(json['startingCash'] as Map<String, dynamic>),
        cashMovementsTotal: MoneyDto.fromJson(json['cashMovementsTotal'] as Map<String, dynamic>),
        posCashPaymentsTotal: MoneyDto.fromJson(json['posCashPaymentsTotal'] as Map<String, dynamic>),
        posRefundsTotal: MoneyDto.fromJson(json['posRefundsTotal'] as Map<String, dynamic>),
        expectedCash: MoneyDto.fromJson(json['expectedCash'] as Map<String, dynamic>),
        countedCash: MoneyDto.fromJson(json['countedCash'] as Map<String, dynamic>),
        difference: MoneyDto.fromJson(json['difference'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'startingCash': startingCash.toJson(),
        'cashMovementsTotal': cashMovementsTotal.toJson(),
        'posCashPaymentsTotal': posCashPaymentsTotal.toJson(),
        'posRefundsTotal': posRefundsTotal.toJson(),
        'expectedCash': expectedCash.toJson(),
        'countedCash': countedCash.toJson(),
        'difference': difference.toJson(),
      };
}

/// Контракт: Tips/TipContracts.cs
class ShiftTipDto {
  const ShiftTipDto({
    required this.ledgerEntryId,
    required this.amount,
    this.seatLabel,
    required this.createdAtUtc,
    required this.reversed,
  });

  final String ledgerEntryId;
  final MoneyDto amount;
  final String? seatLabel;
  final DateTime createdAtUtc;

  /// Возвращены игроку — в сумму смены не входят.
  final bool reversed;

  factory ShiftTipDto.fromJson(Map<String, dynamic> json) => ShiftTipDto(
        ledgerEntryId: json['ledgerEntryId'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        seatLabel: json['seatLabel'] == null ? null : json['seatLabel'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        reversed: json['reversed'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'ledgerEntryId': ledgerEntryId,
        'amount': amount.toJson(),
        'seatLabel': seatLabel,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'reversed': reversed,
      };
}

/// Чаевые смены для Панели. Имени игрока нет: администратору важны сумма и ПК.
///
/// Контракт: Tips/TipContracts.cs
class ShiftTipsDto {
  const ShiftTipsDto({
    required this.shiftId,
    required this.recipientStaffUserId,
    required this.recipientName,
    required this.total,
    required this.tips,
    this.paidOut,
  });

  final String shiftId;
  final String recipientStaffUserId;
  final String recipientName;
  final MoneyDto total;
  final List<ShiftTipDto> tips;

  /// Уже выдано из кассы за эту смену: выдать ту же сумму второй раз нельзя.
  final MoneyDto? paidOut;

  factory ShiftTipsDto.fromJson(Map<String, dynamic> json) => ShiftTipsDto(
        shiftId: json['shiftId'] as String,
        recipientStaffUserId: json['recipientStaffUserId'] as String,
        recipientName: json['recipientName'] as String,
        total: MoneyDto.fromJson(json['total'] as Map<String, dynamic>),
        tips: (json['tips'] as List<dynamic>).map((item) => ShiftTipDto.fromJson(item as Map<String, dynamic>)).toList(),
        paidOut: json['paidOut'] == null ? null : MoneyDto.fromJson(json['paidOut'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'shiftId': shiftId,
        'recipientStaffUserId': recipientStaffUserId,
        'recipientName': recipientName,
        'total': total.toJson(),
        'tips': tips.map((item) => item.toJson()).toList(),
        'paidOut': paidOut?.toJson(),
      };
}

/// Позиция меню бара: что можно заказать к месту прямо во время сессии.
///
/// Контракт: Shop/ShopCatalogItemDto.cs
class ShopCatalogItemDto {
  const ShopCatalogItemDto({
    required this.productId,
    required this.name,
    required this.sku,
    required this.price,
    required this.stockOnHand,
  });

  final String productId;
  final String name;
  final String sku;
  final MoneyDto price;

  /// Остаток на складе филиала. Сервер уже убрал отсюда то, что кончилось и не продаётся в минус,
  /// поэтому число нужно только чтобы предупредить о последних штуках.
  final int stockOnHand;

  factory ShopCatalogItemDto.fromJson(Map<String, dynamic> json) => ShopCatalogItemDto(
        productId: json['productId'] as String,
        name: json['name'] as String,
        sku: json['sku'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        stockOnHand: (json['stockOnHand'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'name': name,
        'sku': sku,
        'price': price.toJson(),
        'stockOnHand': stockOnHand,
      };
}

/// Заказ к месту и его судьба: оформлен, готовится, принесли, отменён.
///
/// Контракт: Shop/ShopOrderDto.cs
class ShopOrderDto {
  const ShopOrderDto({
    required this.id,
    required this.branchId,
    required this.seatId,
    required this.playerAccountId,
    required this.playerDisplayName,
    required this.status,
    required this.total,
    required this.lines,
    required this.placedAtUtc,
    this.acceptedAtUtc,
    this.deliveredAtUtc,
    this.cancelledAtUtc,
    required this.version,
    this.posSaleId,
    this.seatName,
  });

  final String id;
  final String branchId;
  final String seatId;
  final String playerAccountId;
  final String playerDisplayName;

  /// `placed` и `accepted` — заказ ещё в работе, за ним есть смысл следить и его ещё можно
  /// отменить. После «принесли» отменять нечего. Одно из ShopOrderStatusNames.
  final String status;
  final MoneyDto total;
  final List<ShopOrderLineDto> lines;

  /// Когда заказ оформили. Нужно списку прошлых заказов: без времени «принесли» и «отменён»
  /// сливаются в кучу одинаковых строк.
  final DateTime placedAtUtc;
  final DateTime? acceptedAtUtc;
  final DateTime? deliveredAtUtc;
  final DateTime? cancelledAtUtc;
  final int version;
  final String? posSaleId;

  /// Имя места на стене — «PC-12»: туда и несут заказ. Без него лента на стойке могла показать
  /// только идентификатор места, который вслух никто не произносит.
  final String? seatName;

  factory ShopOrderDto.fromJson(Map<String, dynamic> json) => ShopOrderDto(
        id: json['id'] as String,
        branchId: json['branchId'] as String,
        seatId: json['seatId'] as String,
        playerAccountId: json['playerAccountId'] as String,
        playerDisplayName: json['playerDisplayName'] as String,
        status: json['status'] as String,
        total: MoneyDto.fromJson(json['total'] as Map<String, dynamic>),
        lines: (json['lines'] as List<dynamic>).map((item) => ShopOrderLineDto.fromJson(item as Map<String, dynamic>)).toList(),
        placedAtUtc: DateTime.parse(json['placedAtUtc'] as String),
        acceptedAtUtc: json['acceptedAtUtc'] == null ? null : DateTime.parse(json['acceptedAtUtc'] as String),
        deliveredAtUtc: json['deliveredAtUtc'] == null ? null : DateTime.parse(json['deliveredAtUtc'] as String),
        cancelledAtUtc: json['cancelledAtUtc'] == null ? null : DateTime.parse(json['cancelledAtUtc'] as String),
        version: (json['version'] as num).toInt(),
        posSaleId: json['posSaleId'] == null ? null : json['posSaleId'] as String,
        seatName: json['seatName'] == null ? null : json['seatName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'branchId': branchId,
        'seatId': seatId,
        'playerAccountId': playerAccountId,
        'playerDisplayName': playerDisplayName,
        'status': status,
        'total': total.toJson(),
        'lines': lines.map((item) => item.toJson()).toList(),
        'placedAtUtc': placedAtUtc.toIso8601String(),
        'acceptedAtUtc': acceptedAtUtc?.toIso8601String(),
        'deliveredAtUtc': deliveredAtUtc?.toIso8601String(),
        'cancelledAtUtc': cancelledAtUtc?.toIso8601String(),
        'version': version,
        'posSaleId': posSaleId,
        'seatName': seatName,
      };
}

/// Строка заказа: что и сколько.
///
/// Контракт: Shop/ShopOrderLineDto.cs
class ShopOrderLineDto {
  const ShopOrderLineDto({
    required this.productId,
    required this.name,
    required this.unitPrice,
    required this.quantity,
    required this.lineTotal,
  });

  final String productId;
  final String name;
  final MoneyDto unitPrice;
  final int quantity;
  final MoneyDto lineTotal;

  factory ShopOrderLineDto.fromJson(Map<String, dynamic> json) => ShopOrderLineDto(
        productId: json['productId'] as String,
        name: json['name'] as String,
        unitPrice: MoneyDto.fromJson(json['unitPrice'] as Map<String, dynamic>),
        quantity: (json['quantity'] as num).toInt(),
        lineTotal: MoneyDto.fromJson(json['lineTotal'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'name': name,
        'unitPrice': unitPrice.toJson(),
        'quantity': quantity,
        'lineTotal': lineTotal.toJson(),
      };
}

/// Контракт: Shop/ShopOrderLineInput.cs
class ShopOrderLineInput {
  const ShopOrderLineInput({
    required this.productId,
    required this.quantity,
  });

  final String productId;
  final int quantity;

  factory ShopOrderLineInput.fromJson(Map<String, dynamic> json) => ShopOrderLineInput(
        productId: json['productId'] as String,
        quantity: (json['quantity'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'quantity': quantity,
      };
}

/// Контракт: Showcase/ShowcaseContracts.cs
class ShowcaseCardDto {
  const ShowcaseCardDto({
    required this.cardId,
    required this.kind,
    required this.title,
    this.body,
    this.subtitle,
    this.imageUrl,
    this.price,
    this.timeWindow,
    this.startsAtUtc,
    this.packages,
    this.advertiser,
  });


  /// Стабильный ключ карточки: «news:…», «tariff:…». По нему агент узнаёт карточку между
  /// обновлениями, а экран не перезапускает показ, когда список не изменился.
  final String cardId;

  /// Одно из ShowcaseCardKindNames
  final String kind;

  /// У «Пакетов» заголовок пуст: его пишет оболочка на языке экрана.
  final String title;
  final String? body;

  /// Короткая строка рядом с видом карточки: дисциплина турнира («Dota 2»).
  final String? subtitle;

  /// Адрес картинки. С сервера — адрес в медиа-хранилище, на экран — адрес в кэше ПК: чужих
  /// адресов экран не получает.
  final String? imageUrl;

  /// Цена: час тарифа, товар, взнос турнира. Пусто — цены у карточки нет (или взнос бесплатный).
  final MoneyDto? price;

  /// Часы тарифа по времени клуба, «22:00–06:00». Пусто — круглые сутки.
  final String? timeWindow;

  /// Начало турнира.
  final DateTime? startsAtUtc;

  /// Строки карточки «Пакеты».
  final List<ShowcasePackageLineDto>? packages;

  /// Рекламодатель — только у рекламы: экран пишет «Реклама · {рекламодатель}».
  final String? advertiser;

  factory ShowcaseCardDto.fromJson(Map<String, dynamic> json) => ShowcaseCardDto(
        cardId: json['cardId'] as String,
        kind: json['kind'] as String,
        title: json['title'] as String,
        body: json['body'] == null ? null : json['body'] as String,
        subtitle: json['subtitle'] == null ? null : json['subtitle'] as String,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
        price: json['price'] == null ? null : MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        timeWindow: json['timeWindow'] == null ? null : json['timeWindow'] as String,
        startsAtUtc: json['startsAtUtc'] == null ? null : DateTime.parse(json['startsAtUtc'] as String),
        packages: json['packages'] == null ? null : (json['packages'] as List<dynamic>).map((item) => ShowcasePackageLineDto.fromJson(item as Map<String, dynamic>)).toList(),
        advertiser: json['advertiser'] == null ? null : json['advertiser'] as String,
      );

  Map<String, dynamic> toJson() => {
        'cardId': cardId,
        'kind': kind,
        'title': title,
        'body': body,
        'subtitle': subtitle,
        'imageUrl': imageUrl,
        'price': price?.toJson(),
        'timeWindow': timeWindow,
        'startsAtUtc': startsAtUtc?.toIso8601String(),
        'packages': packages?.map((item) => item.toJson()).toList(),
        'advertiser': advertiser,
      };
}

/// Контракт: Ads/AdContracts.cs
class ShowcaseImpressionDto {
  const ShowcaseImpressionDto({
    required this.cardId,
    required this.day,
    required this.impressions,
    required this.shownMs,
  });

  final String cardId;

  /// День показа по UTC, «2026-09-25».
  final String day;
  final int impressions;
  final int shownMs;

  factory ShowcaseImpressionDto.fromJson(Map<String, dynamic> json) => ShowcaseImpressionDto(
        cardId: json['cardId'] as String,
        day: json['day'] as String,
        impressions: (json['impressions'] as num).toInt(),
        shownMs: (json['shownMs'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'cardId': cardId,
        'day': day,
        'impressions': impressions,
        'shownMs': shownMs,
      };
}

/// Контракт: Showcase/ShowcaseContracts.cs
class ShowcasePackageLineDto {
  const ShowcasePackageLineDto({
    required this.name,
    required this.price,
    required this.minutes,
  });

  final String name;
  final MoneyDto price;
  final int minutes;

  factory ShowcasePackageLineDto.fromJson(Map<String, dynamic> json) => ShowcasePackageLineDto(
        name: json['name'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        minutes: (json['minutes'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'price': price.toJson(),
        'minutes': minutes,
      };
}

/// Сотрудник организации, которого нет в этом филиале: его можно добавить сюда ролями. Филиалы, где
/// он уже работает, — названиями; пустой список значит, что назначений у него не осталось вовсе и
/// войти в Панель ему некуда, пока его не вернут в филиал.
///
/// Контракт: Identity/StaffBranchCandidateDto.cs
class StaffBranchCandidateDto {
  const StaffBranchCandidateDto({
    required this.staffUserId,
    required this.userName,
    required this.displayName,
    required this.isActive,
    required this.branchNames,
  });

  final String staffUserId;
  final String userName;
  final String displayName;
  final bool isActive;
  final List<String> branchNames;

  factory StaffBranchCandidateDto.fromJson(Map<String, dynamic> json) => StaffBranchCandidateDto(
        staffUserId: json['staffUserId'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
        isActive: json['isActive'] as bool,
        branchNames: (json['branchNames'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'staffUserId': staffUserId,
        'userName': userName,
        'displayName': displayName,
        'isActive': isActive,
        'branchNames': branchNames.map((item) => item).toList(),
      };
}

/// Requests an SMS password-reset code to a staff account's verified phone.
///
/// Контракт: Identity/StaffForgotPasswordByPhoneRequest.cs
class StaffForgotPasswordByPhoneRequest {
  const StaffForgotPasswordByPhoneRequest({
    required this.phoneNumber,
  });

  final String phoneNumber;

  factory StaffForgotPasswordByPhoneRequest.fromJson(Map<String, dynamic> json) => StaffForgotPasswordByPhoneRequest(
        phoneNumber: json['phoneNumber'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
      };
}

/// Self-service password-reset request. Resolved by username or contact email.
///
/// Контракт: Identity/StaffForgotPasswordRequest.cs
class StaffForgotPasswordRequest {
  const StaffForgotPasswordRequest({
    required this.userNameOrEmail,
  });

  final String userNameOrEmail;

  factory StaffForgotPasswordRequest.fromJson(Map<String, dynamic> json) => StaffForgotPasswordRequest(
        userNameOrEmail: json['userNameOrEmail'] as String,
      );

  Map<String, dynamic> toJson() => {
        'userNameOrEmail': userNameOrEmail,
      };
}

/// The created staff invite; Code is returned once so an admin can also share it out of band.
///
/// Контракт: Identity/StaffInviteDto.cs
class StaffInviteDto {
  const StaffInviteDto({
    required this.staffInviteId,
    required this.code,
    required this.expiresAtUtc,
  });

  final String staffInviteId;
  final String code;
  final DateTime expiresAtUtc;

  factory StaffInviteDto.fromJson(Map<String, dynamic> json) => StaffInviteDto(
        staffInviteId: json['staffInviteId'] as String,
        code: json['code'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'staffInviteId': staffInviteId,
        'code': code,
        'expiresAtUtc': expiresAtUtc.toIso8601String(),
      };
}

/// Контракт: Identity/StaffPhoneVerificationContracts.cs
class StaffPhoneConfirmedResponse {
  const StaffPhoneConfirmedResponse({
    required this.phone,
  });

  final String phone;

  factory StaffPhoneConfirmedResponse.fromJson(Map<String, dynamic> json) => StaffPhoneConfirmedResponse(
        phone: json['phone'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phone': phone,
      };
}

/// Контракт: Identity/StaffPhoneVerificationContracts.cs
class StaffPhoneConfirmRequest {
  const StaffPhoneConfirmRequest({
    required this.code,
  });

  final String code;

  factory StaffPhoneConfirmRequest.fromJson(Map<String, dynamic> json) => StaffPhoneConfirmRequest(
        code: json['code'] as String,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
      };
}

/// Контракт: Identity/StaffPhoneVerificationContracts.cs
class StaffPhoneStartVerificationRequest {
  const StaffPhoneStartVerificationRequest({
    required this.phone,
  });

  final String phone;

  factory StaffPhoneStartVerificationRequest.fromJson(Map<String, dynamic> json) => StaffPhoneStartVerificationRequest(
        phone: json['phone'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phone': phone,
      };
}

/// Current staff member's phone state (self-read). Both null until a phone is set/verified.
/// Phone is in E.164 display form (e.g. "+992937380070").
///
/// Контракт: Identity/StaffPhoneStatusResponse.cs
class StaffPhoneStatusResponse {
  const StaffPhoneStatusResponse({
    this.phone,
    this.phoneVerifiedAtUtc,
  });

  final String? phone;
  final DateTime? phoneVerifiedAtUtc;

  factory StaffPhoneStatusResponse.fromJson(Map<String, dynamic> json) => StaffPhoneStatusResponse(
        phone: json['phone'] == null ? null : json['phone'] as String,
        phoneVerifiedAtUtc: json['phoneVerifiedAtUtc'] == null ? null : DateTime.parse(json['phoneVerifiedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'phone': phone,
        'phoneVerifiedAtUtc': phoneVerifiedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Identity/StaffPhoneVerificationContracts.cs
class StaffPhoneVerificationStartedResponse {
  const StaffPhoneVerificationStartedResponse({
    required this.expiresInSeconds,
    required this.resendAfterSeconds,
  });

  final int expiresInSeconds;
  final int resendAfterSeconds;

  factory StaffPhoneVerificationStartedResponse.fromJson(Map<String, dynamic> json) => StaffPhoneVerificationStartedResponse(
        expiresInSeconds: (json['expiresInSeconds'] as num).toInt(),
        resendAfterSeconds: (json['resendAfterSeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'expiresInSeconds': expiresInSeconds,
        'resendAfterSeconds': resendAfterSeconds,
      };
}

/// Контракт: Identity/StaffRefreshTokenRequest.cs
class StaffRefreshTokenRequest {
  const StaffRefreshTokenRequest({
    required this.organizationId,
    required this.refreshToken,
  });

  final String organizationId;
  final String refreshToken;

  factory StaffRefreshTokenRequest.fromJson(Map<String, dynamic> json) => StaffRefreshTokenRequest(
        organizationId: json['organizationId'] as String,
        refreshToken: json['refreshToken'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'refreshToken': refreshToken,
      };
}

/// Completes an SMS password reset using the code delivered to the verified phone.
///
/// Контракт: Identity/StaffResetPasswordByPhoneRequest.cs
class StaffResetPasswordByPhoneRequest {
  const StaffResetPasswordByPhoneRequest({
    required this.phoneNumber,
    required this.code,
    required this.newPassword,
  });

  final String phoneNumber;
  final String code;
  final String newPassword;

  factory StaffResetPasswordByPhoneRequest.fromJson(Map<String, dynamic> json) => StaffResetPasswordByPhoneRequest(
        phoneNumber: json['phoneNumber'] as String,
        code: json['code'] as String,
        newPassword: json['newPassword'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
        'code': code,
        'newPassword': newPassword,
      };
}

/// Completes a self-service password reset using the 6-digit code emailed to the account.
///
/// Контракт: Identity/StaffResetPasswordRequest.cs
class StaffResetPasswordRequest {
  const StaffResetPasswordRequest({
    required this.userNameOrEmail,
    required this.code,
    required this.newPassword,
  });

  final String userNameOrEmail;
  final String code;
  final String newPassword;

  factory StaffResetPasswordRequest.fromJson(Map<String, dynamic> json) => StaffResetPasswordRequest(
        userNameOrEmail: json['userNameOrEmail'] as String,
        code: json['code'] as String,
        newPassword: json['newPassword'] as String,
      );

  Map<String, dynamic> toJson() => {
        'userNameOrEmail': userNameOrEmail,
        'code': code,
        'newPassword': newPassword,
      };
}

/// Контракт: Identity/StaffSignInByLoginRequest.cs
class StaffSignInByLoginRequest {
  const StaffSignInByLoginRequest({
    required this.login,
    required this.password,
  });

  final String login;
  final String password;

  factory StaffSignInByLoginRequest.fromJson(Map<String, dynamic> json) => StaffSignInByLoginRequest(
        login: json['login'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'login': login,
        'password': password,
      };
}

/// Контракт: Identity/StaffSignInByOrganizationKeyRequest.cs
class StaffSignInByOrganizationKeyRequest {
  const StaffSignInByOrganizationKeyRequest({
    required this.organizationKey,
    required this.userName,
    required this.password,
  });

  final String organizationKey;
  final String userName;
  final String password;

  factory StaffSignInByOrganizationKeyRequest.fromJson(Map<String, dynamic> json) => StaffSignInByOrganizationKeyRequest(
        organizationKey: json['organizationKey'] as String,
        userName: json['userName'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationKey': organizationKey,
        'userName': userName,
        'password': password,
      };
}

/// Контракт: Identity/StaffSignInByPhoneRequest.cs
class StaffSignInByPhoneRequest {
  const StaffSignInByPhoneRequest({
    required this.phoneNumber,
    required this.password,
  });

  final String phoneNumber;
  final String password;

  factory StaffSignInByPhoneRequest.fromJson(Map<String, dynamic> json) => StaffSignInByPhoneRequest(
        phoneNumber: json['phoneNumber'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
        'password': password,
      };
}

/// Контракт: Identity/StaffSignInChooseClubResponse.cs
class StaffSignInChooseClubResponse {
  const StaffSignInChooseClubResponse({
    required this.clubs,
  });

  final List<StaffSignInClubChoice> clubs;

  factory StaffSignInChooseClubResponse.fromJson(Map<String, dynamic> json) => StaffSignInChooseClubResponse(
        clubs: (json['clubs'] as List<dynamic>).map((item) => StaffSignInClubChoice.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'clubs': clubs.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Identity/StaffSignInClubChoice.cs
class StaffSignInClubChoice {
  const StaffSignInClubChoice({
    required this.organizationId,
    required this.name,
  });

  final String organizationId;
  final String name;

  factory StaffSignInClubChoice.fromJson(Map<String, dynamic> json) => StaffSignInClubChoice(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
      };
}

/// Контракт: Identity/StaffSignInRequest.cs
class StaffSignInRequest {
  const StaffSignInRequest({
    required this.organizationId,
    required this.userName,
    required this.password,
  });

  final String organizationId;
  final String userName;
  final String password;

  factory StaffSignInRequest.fromJson(Map<String, dynamic> json) => StaffSignInRequest(
        organizationId: json['organizationId'] as String,
        userName: json['userName'] as String,
        password: json['password'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'userName': userName,
        'password': password,
      };
}

/// Контракт: Identity/StaffSignInResponse.cs
class StaffSignInResponse {
  const StaffSignInResponse({
    required this.staffUserId,
    required this.organizationId,
    required this.displayName,
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.refreshTokenExpiresAtUtc,
    required this.branchIds,
    required this.permissions,
    required this.roleNames,
  });

  final String staffUserId;
  final String organizationId;
  final String displayName;
  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final DateTime refreshTokenExpiresAtUtc;
  final List<String> branchIds;
  final List<String> permissions;
  final List<String> roleNames;

  factory StaffSignInResponse.fromJson(Map<String, dynamic> json) => StaffSignInResponse(
        staffUserId: json['staffUserId'] as String,
        organizationId: json['organizationId'] as String,
        displayName: json['displayName'] as String,
        accessToken: json['accessToken'] as String,
        accessTokenExpiresAtUtc: DateTime.parse(json['accessTokenExpiresAtUtc'] as String),
        refreshToken: json['refreshToken'] as String,
        refreshTokenExpiresAtUtc: DateTime.parse(json['refreshTokenExpiresAtUtc'] as String),
        branchIds: (json['branchIds'] as List<dynamic>).map((item) => item as String).toList(),
        permissions: (json['permissions'] as List<dynamic>).map((item) => item as String).toList(),
        roleNames: (json['roleNames'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'staffUserId': staffUserId,
        'organizationId': organizationId,
        'displayName': displayName,
        'accessToken': accessToken,
        'accessTokenExpiresAtUtc': accessTokenExpiresAtUtc.toIso8601String(),
        'refreshToken': refreshToken,
        'refreshTokenExpiresAtUtc': refreshTokenExpiresAtUtc.toIso8601String(),
        'branchIds': branchIds.map((item) => item).toList(),
        'permissions': permissions.map((item) => item).toList(),
        'roleNames': roleNames.map((item) => item).toList(),
      };
}

/// Контракт: Identity/StaffSignOutRequest.cs
class StaffSignOutRequest {
  const StaffSignOutRequest({
    required this.organizationId,
    required this.refreshToken,
  });

  final String organizationId;
  final String refreshToken;

  factory StaffSignOutRequest.fromJson(Map<String, dynamic> json) => StaffSignOutRequest(
        organizationId: json['organizationId'] as String,
        refreshToken: json['refreshToken'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'refreshToken': refreshToken,
      };
}

/// Контракт: Identity/StaffUserDto.cs
class StaffUserDto {
  const StaffUserDto({
    required this.staffUserId,
    required this.organizationId,
    required this.userName,
    required this.displayName,
    required this.isActive,
    required this.roleNames,
    required this.createdAtUtc,
  });

  final String staffUserId;
  final String organizationId;
  final String userName;
  final String displayName;
  final bool isActive;
  final List<String> roleNames;
  final DateTime createdAtUtc;

  factory StaffUserDto.fromJson(Map<String, dynamic> json) => StaffUserDto(
        staffUserId: json['staffUserId'] as String,
        organizationId: json['organizationId'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
        isActive: json['isActive'] as bool,
        roleNames: (json['roleNames'] as List<dynamic>).map((item) => item as String).toList(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'staffUserId': staffUserId,
        'organizationId': organizationId,
        'userName': userName,
        'displayName': displayName,
        'isActive': isActive,
        'roleNames': roleNames.map((item) => item).toList(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Diagnostics/BranchDiagnosticsDto.cs
class StaleDeviceDiagnosticsDto {
  const StaleDeviceDiagnosticsDto({
    required this.deviceId,
    required this.machineName,
    required this.agentVersion,
    required this.shellVersion,
    required this.isOnline,
    required this.isLocked,
    this.lastHeartbeatAtUtc,
    this.lastHeartbeatAgeSeconds,
  });

  final String deviceId;
  final String machineName;
  final String agentVersion;
  final String shellVersion;
  final bool isOnline;
  final bool isLocked;
  final DateTime? lastHeartbeatAtUtc;
  final int? lastHeartbeatAgeSeconds;

  factory StaleDeviceDiagnosticsDto.fromJson(Map<String, dynamic> json) => StaleDeviceDiagnosticsDto(
        deviceId: json['deviceId'] as String,
        machineName: json['machineName'] as String,
        agentVersion: json['agentVersion'] as String,
        shellVersion: json['shellVersion'] as String,
        isOnline: json['isOnline'] as bool,
        isLocked: json['isLocked'] as bool,
        lastHeartbeatAtUtc: json['lastHeartbeatAtUtc'] == null ? null : DateTime.parse(json['lastHeartbeatAtUtc'] as String),
        lastHeartbeatAgeSeconds: json['lastHeartbeatAgeSeconds'] == null ? null : (json['lastHeartbeatAgeSeconds'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'machineName': machineName,
        'agentVersion': agentVersion,
        'shellVersion': shellVersion,
        'isOnline': isOnline,
        'isLocked': isLocked,
        'lastHeartbeatAtUtc': lastHeartbeatAtUtc?.toIso8601String(),
        'lastHeartbeatAgeSeconds': lastHeartbeatAgeSeconds,
      };
}

/// Контракт: Sessions/StartGuestSessionRequest.cs
class StartGuestSessionRequest {
  const StartGuestSessionRequest({
    required this.organizationId,
    required this.seatId,
    required this.tariffRuleVersionId,
    required this.idempotencyKey,
    this.durationMode,
    this.durationMinutes,
    this.playerAccountId,
    this.billingMode,
    this.tariffVersionId,
    this.playerPackageId,
    this.isComp,
    this.compReason,
  });

  final String organizationId;
  final String seatId;
  final String tariffRuleVersionId;
  final String idempotencyKey;
  final String? durationMode;
  final int? durationMinutes;
  final String? playerAccountId;
  final String? billingMode;
  final String? tariffVersionId;
  final String? playerPackageId;

  /// Anti-fraud §5.4: an explicit comp (free session). Requires a reason; routes to a session.comp audit.
  final bool? isComp;
  final String? compReason;

  factory StartGuestSessionRequest.fromJson(Map<String, dynamic> json) => StartGuestSessionRequest(
        organizationId: json['organizationId'] as String,
        seatId: json['seatId'] as String,
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        durationMode: json['durationMode'] == null ? null : json['durationMode'] as String,
        durationMinutes: json['durationMinutes'] == null ? null : (json['durationMinutes'] as num).toInt(),
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        billingMode: json['billingMode'] == null ? null : json['billingMode'] as String,
        tariffVersionId: json['tariffVersionId'] == null ? null : json['tariffVersionId'] as String,
        playerPackageId: json['playerPackageId'] == null ? null : json['playerPackageId'] as String,
        isComp: json['isComp'] == null ? null : json['isComp'] as bool,
        compReason: json['compReason'] == null ? null : json['compReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'seatId': seatId,
        'tariffRuleVersionId': tariffRuleVersionId,
        'idempotencyKey': idempotencyKey,
        'durationMode': durationMode,
        'durationMinutes': durationMinutes,
        'playerAccountId': playerAccountId,
        'billingMode': billingMode,
        'tariffVersionId': tariffVersionId,
        'playerPackageId': playerPackageId,
        'isComp': isComp,
        'compReason': compReason,
      };
}

/// Контракт: Reservations/StartReservationSessionRequest.cs
class StartReservationSessionRequest {
  const StartReservationSessionRequest({
    required this.organizationId,
    required this.expectedVersion,
    required this.tariffRuleVersionId,
    required this.idempotencyKey,
    this.durationMode,
    this.durationMinutes,
    this.billingMode,
    this.tariffVersionId,
    this.playerPackageId,
    this.isComp,
    this.compReason,
  });

  final String organizationId;
  final int expectedVersion;
  final String tariffRuleVersionId;
  final String idempotencyKey;
  final String? durationMode;
  final int? durationMinutes;
  final String? billingMode;
  final String? tariffVersionId;
  final String? playerPackageId;
  final bool? isComp;
  final String? compReason;

  factory StartReservationSessionRequest.fromJson(Map<String, dynamic> json) => StartReservationSessionRequest(
        organizationId: json['organizationId'] as String,
        expectedVersion: (json['expectedVersion'] as num).toInt(),
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        durationMode: json['durationMode'] == null ? null : json['durationMode'] as String,
        durationMinutes: json['durationMinutes'] == null ? null : (json['durationMinutes'] as num).toInt(),
        billingMode: json['billingMode'] == null ? null : json['billingMode'] as String,
        tariffVersionId: json['tariffVersionId'] == null ? null : json['tariffVersionId'] as String,
        playerPackageId: json['playerPackageId'] == null ? null : json['playerPackageId'] as String,
        isComp: json['isComp'] == null ? null : json['isComp'] as bool,
        compReason: json['compReason'] == null ? null : json['compReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'expectedVersion': expectedVersion,
        'tariffRuleVersionId': tariffRuleVersionId,
        'idempotencyKey': idempotencyKey,
        'durationMode': durationMode,
        'durationMinutes': durationMinutes,
        'billingMode': billingMode,
        'tariffVersionId': tariffVersionId,
        'playerPackageId': playerPackageId,
        'isComp': isComp,
        'compReason': compReason,
      };
}

/// Контракт: Reservations/StartReservationSessionResponse.cs
class StartReservationSessionResponse {
  const StartReservationSessionResponse({
    required this.reservation,
    required this.session,
  });

  final ReservationDto reservation;
  final SessionCommandResponse session;

  factory StartReservationSessionResponse.fromJson(Map<String, dynamic> json) => StartReservationSessionResponse(
        reservation: ReservationDto.fromJson(json['reservation'] as Map<String, dynamic>),
        session: SessionCommandResponse.fromJson(json['session'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'reservation': reservation.toJson(),
        'session': session.toJson(),
      };
}

/// Контракт: Inventory/StockMovementDto.cs
class StockMovementDto {
  const StockMovementDto({
    required this.stockMovementId,
    required this.organizationId,
    required this.branchId,
    required this.productId,
    required this.movementType,
    required this.quantityDelta,
    required this.unitCost,
    required this.reason,
    required this.createdByStaffUserId,
    required this.createdAtUtc,
    this.createdByDisplayName,
  });

  final String stockMovementId;
  final String organizationId;
  final String branchId;
  final String productId;
  final String movementType;
  final int quantityDelta;
  final MoneyDto unitCost;
  final String reason;
  final String createdByStaffUserId;
  final DateTime createdAtUtc;
  final String? createdByDisplayName;

  factory StockMovementDto.fromJson(Map<String, dynamic> json) => StockMovementDto(
        stockMovementId: json['stockMovementId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        productId: json['productId'] as String,
        movementType: json['movementType'] as String,
        quantityDelta: (json['quantityDelta'] as num).toInt(),
        unitCost: MoneyDto.fromJson(json['unitCost'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        createdByStaffUserId: json['createdByStaffUserId'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        createdByDisplayName: json['createdByDisplayName'] == null ? null : json['createdByDisplayName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'stockMovementId': stockMovementId,
        'organizationId': organizationId,
        'branchId': branchId,
        'productId': productId,
        'movementType': movementType,
        'quantityDelta': quantityDelta,
        'unitCost': unitCost.toJson(),
        'reason': reason,
        'createdByStaffUserId': createdByStaffUserId,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'createdByDisplayName': createdByDisplayName,
      };
}

/// Контракт: Platform/Billing/SubscriptionListItemDto.cs
class SubscriptionListItemDto {
  const SubscriptionListItemDto({
    required this.organizationSubscriptionId,
    required this.organizationId,
    required this.organizationName,
    required this.organizationSlug,
    required this.planCode,
    required this.status,
    required this.billingInterval,
    required this.amountMinorUnits,
    required this.currencyCode,
    required this.currentPeriodEndUtc,
    this.nextInvoiceUtc,
    required this.cancelAtPeriodEnd,
  });

  final String organizationSubscriptionId;
  final String organizationId;
  final String organizationName;
  final String organizationSlug;
  final String planCode;
  final String status;
  final String billingInterval;
  final int amountMinorUnits;
  final String currencyCode;
  final DateTime currentPeriodEndUtc;
  final DateTime? nextInvoiceUtc;
  final bool cancelAtPeriodEnd;

  factory SubscriptionListItemDto.fromJson(Map<String, dynamic> json) => SubscriptionListItemDto(
        organizationSubscriptionId: json['organizationSubscriptionId'] as String,
        organizationId: json['organizationId'] as String,
        organizationName: json['organizationName'] as String,
        organizationSlug: json['organizationSlug'] as String,
        planCode: json['planCode'] as String,
        status: json['status'] as String,
        billingInterval: json['billingInterval'] as String,
        amountMinorUnits: (json['amountMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        currentPeriodEndUtc: DateTime.parse(json['currentPeriodEndUtc'] as String),
        nextInvoiceUtc: json['nextInvoiceUtc'] == null ? null : DateTime.parse(json['nextInvoiceUtc'] as String),
        cancelAtPeriodEnd: json['cancelAtPeriodEnd'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationSubscriptionId': organizationSubscriptionId,
        'organizationId': organizationId,
        'organizationName': organizationName,
        'organizationSlug': organizationSlug,
        'planCode': planCode,
        'status': status,
        'billingInterval': billingInterval,
        'amountMinorUnits': amountMinorUnits,
        'currencyCode': currencyCode,
        'currentPeriodEndUtc': currentPeriodEndUtc.toIso8601String(),
        'nextInvoiceUtc': nextInvoiceUtc?.toIso8601String(),
        'cancelAtPeriodEnd': cancelAtPeriodEnd,
      };
}

/// Контракт: Platform/Billing/SubscriptionPlanDto.cs
class SubscriptionPlanDto {
  const SubscriptionPlanDto({
    required this.planCode,
    required this.name,
    required this.priceMinorUnits,
    required this.currencyCode,
    required this.billingInterval,
    this.maxBranches,
    this.maxDevicesPerBranch,
    this.maxConcurrentSessions,
    this.maxStaffUsersPerBranch,
    required this.isActive,
    required this.sortOrder,
    this.pricePerDeviceMinorUnits,
    this.includedDevices,
  });

  final String planCode;
  final String name;
  final int priceMinorUnits;
  final String currencyCode;
  final String billingInterval;
  final int? maxBranches;
  final int? maxDevicesPerBranch;
  final int? maxConcurrentSessions;
  final int? maxStaffUsersPerBranch;
  final bool isActive;
  final int sortOrder;

  /// Цена каждого ПК сверх включённых — у тарифа за ПК; у прочих ноль.
  final int? pricePerDeviceMinorUnits;
  final int? includedDevices;

  factory SubscriptionPlanDto.fromJson(Map<String, dynamic> json) => SubscriptionPlanDto(
        planCode: json['planCode'] as String,
        name: json['name'] as String,
        priceMinorUnits: (json['priceMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        billingInterval: json['billingInterval'] as String,
        maxBranches: json['maxBranches'] == null ? null : (json['maxBranches'] as num).toInt(),
        maxDevicesPerBranch: json['maxDevicesPerBranch'] == null ? null : (json['maxDevicesPerBranch'] as num).toInt(),
        maxConcurrentSessions: json['maxConcurrentSessions'] == null ? null : (json['maxConcurrentSessions'] as num).toInt(),
        maxStaffUsersPerBranch: json['maxStaffUsersPerBranch'] == null ? null : (json['maxStaffUsersPerBranch'] as num).toInt(),
        isActive: json['isActive'] as bool,
        sortOrder: (json['sortOrder'] as num).toInt(),
        pricePerDeviceMinorUnits: json['pricePerDeviceMinorUnits'] == null ? null : (json['pricePerDeviceMinorUnits'] as num).toInt(),
        includedDevices: json['includedDevices'] == null ? null : (json['includedDevices'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'planCode': planCode,
        'name': name,
        'priceMinorUnits': priceMinorUnits,
        'currencyCode': currencyCode,
        'billingInterval': billingInterval,
        'maxBranches': maxBranches,
        'maxDevicesPerBranch': maxDevicesPerBranch,
        'maxConcurrentSessions': maxConcurrentSessions,
        'maxStaffUsersPerBranch': maxStaffUsersPerBranch,
        'isActive': isActive,
        'sortOrder': sortOrder,
        'pricePerDeviceMinorUnits': pricePerDeviceMinorUnits,
        'includedDevices': includedDevices,
      };
}

/// Контракт: Tariffs/TariffCalculationResult.cs
class TariffCalculationResult {
  const TariffCalculationResult({
    required this.tariffId,
    required this.tariffVersionId,
    required this.tariffRuleVersionId,
    required this.durationMinutes,
    required this.billableMinutes,
    required this.amount,
  });

  final String tariffId;
  final String tariffVersionId;
  final String tariffRuleVersionId;
  final int durationMinutes;
  final int billableMinutes;
  final MoneyDto amount;

  factory TariffCalculationResult.fromJson(Map<String, dynamic> json) => TariffCalculationResult(
        tariffId: json['tariffId'] as String,
        tariffVersionId: json['tariffVersionId'] as String,
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        durationMinutes: (json['durationMinutes'] as num).toInt(),
        billableMinutes: (json['billableMinutes'] as num).toInt(),
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'tariffId': tariffId,
        'tariffVersionId': tariffVersionId,
        'tariffRuleVersionId': tariffRuleVersionId,
        'durationMinutes': durationMinutes,
        'billableMinutes': billableMinutes,
        'amount': amount.toJson(),
      };
}

/// Расписание: `AppliesOnDaysMask` — биты дней недели с понедельника (1) по воскресенье (64),
/// `0` означает «каждый день». Часы — минуты от полуночи по местному времени филиала; оба
/// `null` означают «круглые сутки», а начало больше конца — окно через полночь.
///
/// Контракт: Tariffs/TariffDto.cs
class TariffDto {
  const TariffDto({
    required this.tariffId,
    required this.organizationId,
    required this.branchId,
    required this.name,
    required this.isActive,
    required this.createdAtUtc,
    this.appliesOnDaysMask,
    this.appliesFromMinuteOfDay,
    this.appliesToMinuteOfDay,
    this.featuredOnPcs,
  });

  final String tariffId;
  final String organizationId;
  final String branchId;
  final String name;
  final bool isActive;
  final DateTime createdAtUtc;
  final int? appliesOnDaysMask;
  final int? appliesFromMinuteOfDay;
  final int? appliesToMinuteOfDay;

  /// Тариф крутится в витрине свободного ПК.
  final bool? featuredOnPcs;

  factory TariffDto.fromJson(Map<String, dynamic> json) => TariffDto(
        tariffId: json['tariffId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        isActive: json['isActive'] as bool,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        appliesOnDaysMask: json['appliesOnDaysMask'] == null ? null : (json['appliesOnDaysMask'] as num).toInt(),
        appliesFromMinuteOfDay: json['appliesFromMinuteOfDay'] == null ? null : (json['appliesFromMinuteOfDay'] as num).toInt(),
        appliesToMinuteOfDay: json['appliesToMinuteOfDay'] == null ? null : (json['appliesToMinuteOfDay'] as num).toInt(),
        featuredOnPcs: json['featuredOnPcs'] == null ? null : json['featuredOnPcs'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'tariffId': tariffId,
        'organizationId': organizationId,
        'branchId': branchId,
        'name': name,
        'isActive': isActive,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'appliesOnDaysMask': appliesOnDaysMask,
        'appliesFromMinuteOfDay': appliesFromMinuteOfDay,
        'appliesToMinuteOfDay': appliesToMinuteOfDay,
        'featuredOnPcs': featuredOnPcs,
      };
}

/// Тариф, который можно выбрать: по чём и с какими правилами считается время. `AppliesNow`
/// считает сервер по часовому поясу филиала: клиент, повторивший этот расчёт у себя, ошибётся на
/// телефоне с чужим часовым поясом и предложит утреннюю цену вечером.
/// Цену по этим полям клиент НЕ считает — за этим есть расчёт на сервере: минимальное
/// оплачиваемое время и шаг округления живут в биллинге, и вторая арифметика здесь разошлась бы
/// с настоящим списанием.
/// AppliesFromMinuteOfDay и AppliesToMinuteOfDay — окно
/// местного времени клуба, минуты от полуночи. Оба пусты — круглосуточно, начало больше конца —
/// переход через полночь.
///
/// Контракт: Operator/TariffOptionDto.cs
class TariffOptionDto {
  const TariffOptionDto({
    required this.tariffId,
    required this.tariffVersionId,
    required this.name,
    required this.tariffRuleVersionId,
    required this.versionNumber,
    required this.currencyCode,
    required this.pricePerMinuteMinorUnits,
    required this.minimumBillableMinutes,
    required this.roundingIncrementMinutes,
    required this.effectiveFromUtc,
    this.appliesOnDaysMask,
    this.appliesFromMinuteOfDay,
    this.appliesToMinuteOfDay,
    this.appliesNow,
    this.featuredOnPcs,
  });

  final String tariffId;
  final String tariffVersionId;
  final String name;
  final String tariffRuleVersionId;
  final int versionNumber;
  final String currencyCode;
  final int pricePerMinuteMinorUnits;
  final int minimumBillableMinutes;
  final int roundingIncrementMinutes;
  final DateTime effectiveFromUtc;

  /// Биты дней недели с понедельника (1) по воскресенье (64); 0 — каждый день.
  final int? appliesOnDaysMask;
  final int? appliesFromMinuteOfDay;
  final int? appliesToMinuteOfDay;

  /// Действует ли тариф прямо сейчас — по часам клуба, а не телефона. Важно там, где играть
  /// начинают сию секунду; для брони на завтра ответ никакого значения не имеет.
  final bool? appliesNow;

  /// Тариф крутится в витрине свободного ПК.
  final bool? featuredOnPcs;

  factory TariffOptionDto.fromJson(Map<String, dynamic> json) => TariffOptionDto(
        tariffId: json['tariffId'] as String,
        tariffVersionId: json['tariffVersionId'] as String,
        name: json['name'] as String,
        tariffRuleVersionId: json['tariffRuleVersionId'] as String,
        versionNumber: (json['versionNumber'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        pricePerMinuteMinorUnits: (json['pricePerMinuteMinorUnits'] as num).toInt(),
        minimumBillableMinutes: (json['minimumBillableMinutes'] as num).toInt(),
        roundingIncrementMinutes: (json['roundingIncrementMinutes'] as num).toInt(),
        effectiveFromUtc: DateTime.parse(json['effectiveFromUtc'] as String),
        appliesOnDaysMask: json['appliesOnDaysMask'] == null ? null : (json['appliesOnDaysMask'] as num).toInt(),
        appliesFromMinuteOfDay: json['appliesFromMinuteOfDay'] == null ? null : (json['appliesFromMinuteOfDay'] as num).toInt(),
        appliesToMinuteOfDay: json['appliesToMinuteOfDay'] == null ? null : (json['appliesToMinuteOfDay'] as num).toInt(),
        appliesNow: json['appliesNow'] == null ? null : json['appliesNow'] as bool,
        featuredOnPcs: json['featuredOnPcs'] == null ? null : json['featuredOnPcs'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'tariffId': tariffId,
        'tariffVersionId': tariffVersionId,
        'name': name,
        'tariffRuleVersionId': tariffRuleVersionId,
        'versionNumber': versionNumber,
        'currencyCode': currencyCode,
        'pricePerMinuteMinorUnits': pricePerMinuteMinorUnits,
        'minimumBillableMinutes': minimumBillableMinutes,
        'roundingIncrementMinutes': roundingIncrementMinutes,
        'effectiveFromUtc': effectiveFromUtc.toIso8601String(),
        'appliesOnDaysMask': appliesOnDaysMask,
        'appliesFromMinuteOfDay': appliesFromMinuteOfDay,
        'appliesToMinuteOfDay': appliesToMinuteOfDay,
        'appliesNow': appliesNow,
        'featuredOnPcs': featuredOnPcs,
      };
}

/// Когда действует тариф: дни недели и окно местного времени филиала.
/// `AppliesOnDaysMask` — биты с понедельника (1) по воскресенье (64); `0` означает
/// «каждый день». Часы — минуты от полуночи; оба `null` означают «круглые сутки», а начало
/// больше конца — окно через полночь.
/// Расписание вынесено отдельным объектом намеренно. В запросе на изменение тарифа плоские поля
/// со значениями по умолчанию означали бы, что вызывающий, не знающий про расписание, стирает его
/// молча: PATCH, у которого пропущенное поле уничтожает данные, — ловушка. Здесь `null` у
/// всего объекта означает «не трогать», и не знать про расписание безопасно.
///
/// Контракт: Tariffs/TariffScheduleDto.cs
class TariffScheduleDto {
  const TariffScheduleDto({
    this.appliesOnDaysMask,
    this.appliesFromMinuteOfDay,
    this.appliesToMinuteOfDay,
  });

  final int? appliesOnDaysMask;
  final int? appliesFromMinuteOfDay;
  final int? appliesToMinuteOfDay;

  factory TariffScheduleDto.fromJson(Map<String, dynamic> json) => TariffScheduleDto(
        appliesOnDaysMask: json['appliesOnDaysMask'] == null ? null : (json['appliesOnDaysMask'] as num).toInt(),
        appliesFromMinuteOfDay: json['appliesFromMinuteOfDay'] == null ? null : (json['appliesFromMinuteOfDay'] as num).toInt(),
        appliesToMinuteOfDay: json['appliesToMinuteOfDay'] == null ? null : (json['appliesToMinuteOfDay'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'appliesOnDaysMask': appliesOnDaysMask,
        'appliesFromMinuteOfDay': appliesFromMinuteOfDay,
        'appliesToMinuteOfDay': appliesToMinuteOfDay,
      };
}

/// Контракт: Tariffs/TariffVersionDto.cs
class TariffVersionDto {
  const TariffVersionDto({
    required this.tariffVersionId,
    required this.tariffId,
    required this.versionNumber,
    required this.currencyCode,
    required this.pricePerMinuteMinorUnits,
    required this.minimumBillableMinutes,
    required this.roundingIncrementMinutes,
    required this.effectiveFromUtc,
    this.retiredAtUtc,
    required this.createdAtUtc,
  });

  final String tariffVersionId;
  final String tariffId;
  final int versionNumber;
  final String currencyCode;
  final int pricePerMinuteMinorUnits;
  final int minimumBillableMinutes;
  final int roundingIncrementMinutes;
  final DateTime effectiveFromUtc;
  final DateTime? retiredAtUtc;
  final DateTime createdAtUtc;

  factory TariffVersionDto.fromJson(Map<String, dynamic> json) => TariffVersionDto(
        tariffVersionId: json['tariffVersionId'] as String,
        tariffId: json['tariffId'] as String,
        versionNumber: (json['versionNumber'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        pricePerMinuteMinorUnits: (json['pricePerMinuteMinorUnits'] as num).toInt(),
        minimumBillableMinutes: (json['minimumBillableMinutes'] as num).toInt(),
        roundingIncrementMinutes: (json['roundingIncrementMinutes'] as num).toInt(),
        effectiveFromUtc: DateTime.parse(json['effectiveFromUtc'] as String),
        retiredAtUtc: json['retiredAtUtc'] == null ? null : DateTime.parse(json['retiredAtUtc'] as String),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'tariffVersionId': tariffVersionId,
        'tariffId': tariffId,
        'versionNumber': versionNumber,
        'currencyCode': currencyCode,
        'pricePerMinuteMinorUnits': pricePerMinuteMinorUnits,
        'minimumBillableMinutes': minimumBillableMinutes,
        'roundingIncrementMinutes': roundingIncrementMinutes,
        'effectiveFromUtc': effectiveFromUtc.toIso8601String(),
        'retiredAtUtc': retiredAtUtc?.toIso8601String(),
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Чаевые администратору смены с экрана итога (спека `2026-09-25-visit-tips-design.md`). Клуб их
/// включает сам; деньги уходят с кошелька игрока записью журнала `tip` и выручкой не считаются.
///
/// Контракт: Tips/TipContracts.cs
class TipSettingsDto {
  const TipSettingsDto({
    required this.enabled,
  });

  final bool enabled;

  factory TipSettingsDto.fromJson(Map<String, dynamic> json) => TipSettingsDto(
        enabled: json['enabled'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'enabled': enabled,
      };
}

/// Контракт: Billing/TopUpWalletRequest.cs
class TopUpWalletRequest {
  const TopUpWalletRequest({
    required this.organizationId,
    required this.amount,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final MoneyDto amount;
  final String reason;
  final String idempotencyKey;

  factory TopUpWalletRequest.fromJson(Map<String, dynamic> json) => TopUpWalletRequest(
        organizationId: json['organizationId'] as String,
        amount: MoneyDto.fromJson(json['amount'] as Map<String, dynamic>),
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'amount': amount.toJson(),
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// Событие клуба глазами стойки: всё, включая черновики и счётчик записавшихся.
///
/// Контракт: Tournaments/TournamentDtos.cs
class TournamentDto {
  const TournamentDto({
    required this.tournamentId,
    required this.branchId,
    required this.title,
    required this.description,
    required this.discipline,
    required this.startsAtUtc,
    required this.entryFee,
    required this.capacity,
    required this.state,
    required this.registeredCount,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    this.cancelledAtUtc,
    required this.cancelReason,
  });

  final String tournamentId;
  final String branchId;
  final String title;
  final String description;
  final String discipline;
  final DateTime startsAtUtc;
  final MoneyDto entryFee;
  final int capacity;
  final String state;
  final int registeredCount;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;
  final DateTime? cancelledAtUtc;
  final String cancelReason;

  factory TournamentDto.fromJson(Map<String, dynamic> json) => TournamentDto(
        tournamentId: json['tournamentId'] as String,
        branchId: json['branchId'] as String,
        title: json['title'] as String,
        description: json['description'] as String,
        discipline: json['discipline'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        entryFee: MoneyDto.fromJson(json['entryFee'] as Map<String, dynamic>),
        capacity: (json['capacity'] as num).toInt(),
        state: json['state'] as String,
        registeredCount: (json['registeredCount'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
        cancelledAtUtc: json['cancelledAtUtc'] == null ? null : DateTime.parse(json['cancelledAtUtc'] as String),
        cancelReason: json['cancelReason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'tournamentId': tournamentId,
        'branchId': branchId,
        'title': title,
        'description': description,
        'discipline': discipline,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'entryFee': entryFee.toJson(),
        'capacity': capacity,
        'state': state,
        'registeredCount': registeredCount,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'updatedAtUtc': updatedAtUtc.toIso8601String(),
        'cancelledAtUtc': cancelledAtUtc?.toIso8601String(),
        'cancelReason': cancelReason,
      };
}

/// Кто записался — список для стойки: по нему встречают на входе.
///
/// Контракт: Tournaments/TournamentDtos.cs
class TournamentParticipantDto {
  const TournamentParticipantDto({
    required this.tournamentRegistrationId,
    required this.playerAccountId,
    required this.displayName,
    this.phoneNumber,
    required this.entryFeePaid,
    required this.registeredAtUtc,
  });

  final String tournamentRegistrationId;
  final String playerAccountId;
  final String displayName;
  final String? phoneNumber;
  final MoneyDto entryFeePaid;
  final DateTime registeredAtUtc;

  factory TournamentParticipantDto.fromJson(Map<String, dynamic> json) => TournamentParticipantDto(
        tournamentRegistrationId: json['tournamentRegistrationId'] as String,
        playerAccountId: json['playerAccountId'] as String,
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        entryFeePaid: MoneyDto.fromJson(json['entryFeePaid'] as Map<String, dynamic>),
        registeredAtUtc: DateTime.parse(json['registeredAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'tournamentRegistrationId': tournamentRegistrationId,
        'playerAccountId': playerAccountId,
        'displayName': displayName,
        'phoneNumber': phoneNumber,
        'entryFeePaid': entryFeePaid.toJson(),
        'registeredAtUtc': registeredAtUtc.toIso8601String(),
      };
}

/// Контракт: Platform/Organizations/TransferOrganizationOwnerRequest.cs
class TransferOrganizationOwnerRequest {
  const TransferOrganizationOwnerRequest({
    required this.newOwnerEmail,
    required this.reason,
  });

  final String newOwnerEmail;
  final String reason;

  factory TransferOrganizationOwnerRequest.fromJson(Map<String, dynamic> json) => TransferOrganizationOwnerRequest(
        newOwnerEmail: json['newOwnerEmail'] as String,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'newOwnerEmail': newOwnerEmail,
        'reason': reason,
      };
}

/// Контракт: Sessions/TransferSessionRequest.cs
class TransferSessionRequest {
  const TransferSessionRequest({
    required this.targetSeatId,
    required this.idempotencyKey,
    this.expectedVersion,
  });

  final String targetSeatId;
  final String idempotencyKey;
  final int? expectedVersion;

  factory TransferSessionRequest.fromJson(Map<String, dynamic> json) => TransferSessionRequest(
        targetSeatId: json['targetSeatId'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
        expectedVersion: json['expectedVersion'] == null ? null : (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'targetSeatId': targetSeatId,
        'idempotencyKey': idempotencyKey,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: Branches/UpdateBranchBookingSettingsRequest.cs
class UpdateBranchBookingSettingsRequest {
  const UpdateBranchBookingSettingsRequest({
    required this.organizationId,
    required this.acceptanceMode,
    required this.respondWithinMinutes,
    required this.requirePrepaymentFromNewGuests,
    required this.maxActiveReservationsForNewGuests,
    required this.regularAfterVisits,
    required this.holdSeatAfterStartMinutes,
    required this.keepPrepaymentOnNoShow,
  });

  final String organizationId;
  final String acceptanceMode;
  final int respondWithinMinutes;
  final bool requirePrepaymentFromNewGuests;
  final int maxActiveReservationsForNewGuests;
  final int regularAfterVisits;
  final int holdSeatAfterStartMinutes;
  final bool keepPrepaymentOnNoShow;

  factory UpdateBranchBookingSettingsRequest.fromJson(Map<String, dynamic> json) => UpdateBranchBookingSettingsRequest(
        organizationId: json['organizationId'] as String,
        acceptanceMode: json['acceptanceMode'] as String,
        respondWithinMinutes: (json['respondWithinMinutes'] as num).toInt(),
        requirePrepaymentFromNewGuests: json['requirePrepaymentFromNewGuests'] as bool,
        maxActiveReservationsForNewGuests: (json['maxActiveReservationsForNewGuests'] as num).toInt(),
        regularAfterVisits: (json['regularAfterVisits'] as num).toInt(),
        holdSeatAfterStartMinutes: (json['holdSeatAfterStartMinutes'] as num).toInt(),
        keepPrepaymentOnNoShow: json['keepPrepaymentOnNoShow'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'acceptanceMode': acceptanceMode,
        'respondWithinMinutes': respondWithinMinutes,
        'requirePrepaymentFromNewGuests': requirePrepaymentFromNewGuests,
        'maxActiveReservationsForNewGuests': maxActiveReservationsForNewGuests,
        'regularAfterVisits': regularAfterVisits,
        'holdSeatAfterStartMinutes': holdSeatAfterStartMinutes,
        'keepPrepaymentOnNoShow': keepPrepaymentOnNoShow,
      };
}

/// Контракт: Branches/UpdateBranchProfileRequest.cs
class UpdateBranchProfileRequest {
  const UpdateBranchProfileRequest({
    required this.organizationId,
    required this.name,
    required this.city,
    this.description,
    this.address,
    this.phone,
    this.telegram,
    this.website,
    this.instagram,
    this.logoUrl,
    this.logoMediaId,
    required this.timeZone,
    required this.locale,
    required this.workingHours,
    this.coverImageUrl,
    this.coverMediaId,
    this.photos,
    this.latitude,
    this.longitude,
  });

  final String organizationId;
  final String name;
  final String city;
  final String? description;
  final String? address;
  final String? phone;
  final String? telegram;
  final String? website;
  final String? instagram;
  final String? logoUrl;
  final String? logoMediaId;
  final String timeZone;
  final String locale;
  final List<BranchWorkingHoursDayDto> workingHours;

  /// Витрина клуба в приложении игрока: фото зала и точка на карте. В конце списка и с
  /// умолчаниями — чтобы старый клиент, который их не шлёт, продолжал сохранять профиль.
  final String? coverImageUrl;
  final String? coverMediaId;
  final List<BranchPhotoDto>? photos;
  final double? latitude;
  final double? longitude;

  factory UpdateBranchProfileRequest.fromJson(Map<String, dynamic> json) => UpdateBranchProfileRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        city: json['city'] as String,
        description: json['description'] == null ? null : json['description'] as String,
        address: json['address'] == null ? null : json['address'] as String,
        phone: json['phone'] == null ? null : json['phone'] as String,
        telegram: json['telegram'] == null ? null : json['telegram'] as String,
        website: json['website'] == null ? null : json['website'] as String,
        instagram: json['instagram'] == null ? null : json['instagram'] as String,
        logoUrl: json['logoUrl'] == null ? null : json['logoUrl'] as String,
        logoMediaId: json['logoMediaId'] == null ? null : json['logoMediaId'] as String,
        timeZone: json['timeZone'] as String,
        locale: json['locale'] as String,
        workingHours: (json['workingHours'] as List<dynamic>).map((item) => BranchWorkingHoursDayDto.fromJson(item as Map<String, dynamic>)).toList(),
        coverImageUrl: json['coverImageUrl'] == null ? null : json['coverImageUrl'] as String,
        coverMediaId: json['coverMediaId'] == null ? null : json['coverMediaId'] as String,
        photos: json['photos'] == null ? null : (json['photos'] as List<dynamic>).map((item) => BranchPhotoDto.fromJson(item as Map<String, dynamic>)).toList(),
        latitude: json['latitude'] == null ? null : (json['latitude'] as num).toDouble(),
        longitude: json['longitude'] == null ? null : (json['longitude'] as num).toDouble(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'city': city,
        'description': description,
        'address': address,
        'phone': phone,
        'telegram': telegram,
        'website': website,
        'instagram': instagram,
        'logoUrl': logoUrl,
        'logoMediaId': logoMediaId,
        'timeZone': timeZone,
        'locale': locale,
        'workingHours': workingHours.map((item) => item.toJson()).toList(),
        'coverImageUrl': coverImageUrl,
        'coverMediaId': coverMediaId,
        'photos': photos?.map((item) => item.toJson()).toList(),
        'latitude': latitude,
        'longitude': longitude,
      };
}

/// Сохранить профиль. ExpectedVersion — версия, которую человек открыл: если
/// профиль успели поменять, сохранение отказывает, а не затирает чужую правку молча.
///
/// Контракт: Devices/ProtectionProfileContracts.cs
class UpdateBranchProtectionProfileRequest {
  const UpdateBranchProtectionProfileRequest({
    required this.organizationId,
    required this.expectedVersion,
    required this.blockRemovableStorage,
    required this.blockBrowserDownloads,
    required this.blockBrowserIncognito,
    required this.disableRunDialog,
    required this.hiddenDrives,
    required this.urlBlocklist,
    required this.blockedWindows,
    required this.clearAfterSession,
    this.idleShutdownMinutes,
    this.clubRules,
  });

  final String organizationId;
  final int expectedVersion;
  final bool blockRemovableStorage;
  final bool blockBrowserDownloads;
  final bool blockBrowserIncognito;
  final bool disableRunDialog;
  final List<String> hiddenDrives;
  final List<String> urlBlocklist;
  final List<BlockedWindowRuleDto> blockedWindows;
  final List<String> clearAfterSession;
  final int? idleShutdownMinutes;
  final String? clubRules;

  factory UpdateBranchProtectionProfileRequest.fromJson(Map<String, dynamic> json) => UpdateBranchProtectionProfileRequest(
        organizationId: json['organizationId'] as String,
        expectedVersion: (json['expectedVersion'] as num).toInt(),
        blockRemovableStorage: json['blockRemovableStorage'] as bool,
        blockBrowserDownloads: json['blockBrowserDownloads'] as bool,
        blockBrowserIncognito: json['blockBrowserIncognito'] as bool,
        disableRunDialog: json['disableRunDialog'] as bool,
        hiddenDrives: (json['hiddenDrives'] as List<dynamic>).map((item) => item as String).toList(),
        urlBlocklist: (json['urlBlocklist'] as List<dynamic>).map((item) => item as String).toList(),
        blockedWindows: (json['blockedWindows'] as List<dynamic>).map((item) => BlockedWindowRuleDto.fromJson(item as Map<String, dynamic>)).toList(),
        clearAfterSession: (json['clearAfterSession'] as List<dynamic>).map((item) => item as String).toList(),
        idleShutdownMinutes: json['idleShutdownMinutes'] == null ? null : (json['idleShutdownMinutes'] as num).toInt(),
        clubRules: json['clubRules'] == null ? null : json['clubRules'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'expectedVersion': expectedVersion,
        'blockRemovableStorage': blockRemovableStorage,
        'blockBrowserDownloads': blockBrowserDownloads,
        'blockBrowserIncognito': blockBrowserIncognito,
        'disableRunDialog': disableRunDialog,
        'hiddenDrives': hiddenDrives.map((item) => item).toList(),
        'urlBlocklist': urlBlocklist.map((item) => item).toList(),
        'blockedWindows': blockedWindows.map((item) => item.toJson()).toList(),
        'clearAfterSession': clearAfterSession.map((item) => item).toList(),
        'idleShutdownMinutes': idleShutdownMinutes,
        'clubRules': clubRules,
      };
}

/// Контракт: Branches/UpdateBranchSettingsRequest.cs
class UpdateBranchSettingsRequest {
  const UpdateBranchSettingsRequest({
    required this.organizationId,
    required this.requireManualDeviceApproval,
    required this.preferredLocale,
  });

  final String organizationId;
  final bool requireManualDeviceApproval;
  final String preferredLocale;

  factory UpdateBranchSettingsRequest.fromJson(Map<String, dynamic> json) => UpdateBranchSettingsRequest(
        organizationId: json['organizationId'] as String,
        requireManualDeviceApproval: json['requireManualDeviceApproval'] as bool,
        preferredLocale: json['preferredLocale'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'requireManualDeviceApproval': requireManualDeviceApproval,
        'preferredLocale': preferredLocale,
      };
}

/// POST-запрос: CardNumber опционален — null/пусто сохраняет прежнюю карту, непустой заменяет.
///
/// Контракт: Payments/DcPayLinkConfigDtos.cs
class UpdateDcPayLinkConfigRequest {
  const UpdateDcPayLinkConfigRequest({
    this.cardNumber,
    required this.commentTemplate,
    required this.isActive,
  });

  final String? cardNumber;
  final String commentTemplate;
  final bool isActive;

  factory UpdateDcPayLinkConfigRequest.fromJson(Map<String, dynamic> json) => UpdateDcPayLinkConfigRequest(
        cardNumber: json['cardNumber'] == null ? null : json['cardNumber'] as String,
        commentTemplate: json['commentTemplate'] as String,
        isActive: json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'cardNumber': cardNumber,
        'commentTemplate': commentTemplate,
        'isActive': isActive,
      };
}

/// Контракт: Diagnostics/BranchDiagnosticsDto.cs
class UpdateDiagnosticsSummaryDto {
  const UpdateDiagnosticsSummaryDto({
    required this.activeRollouts,
    required this.installingDevices,
    required this.failedDevices,
    required this.rollbackDevices,
    required this.recentFailures,
  });

  final int activeRollouts;
  final int installingDevices;
  final int failedDevices;
  final int rollbackDevices;
  final List<FailedUpdateDiagnosticsDto> recentFailures;

  factory UpdateDiagnosticsSummaryDto.fromJson(Map<String, dynamic> json) => UpdateDiagnosticsSummaryDto(
        activeRollouts: (json['activeRollouts'] as num).toInt(),
        installingDevices: (json['installingDevices'] as num).toInt(),
        failedDevices: (json['failedDevices'] as num).toInt(),
        rollbackDevices: (json['rollbackDevices'] as num).toInt(),
        recentFailures: (json['recentFailures'] as List<dynamic>).map((item) => FailedUpdateDiagnosticsDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'activeRollouts': activeRollouts,
        'installingDevices': installingDevices,
        'failedDevices': failedDevices,
        'rollbackDevices': rollbackDevices,
        'recentFailures': recentFailures.map((item) => item.toJson()).toList(),
      };
}

/// POST request. HashKey is optional: null/empty keeps the stored secret, a non-empty value
/// replaces it. All four fields present (with a stored-or-supplied hash key) → Status "configured".
///
/// Контракт: Payments/EskhataMerchantConfigDtos.cs
class UpdateEskhataMerchantConfigRequest {
  const UpdateEskhataMerchantConfigRequest({
    required this.baseUrl,
    required this.companyId,
    required this.merchantId,
    this.hashKey,
  });

  final String baseUrl;
  final String companyId;
  final int merchantId;
  final String? hashKey;

  factory UpdateEskhataMerchantConfigRequest.fromJson(Map<String, dynamic> json) => UpdateEskhataMerchantConfigRequest(
        baseUrl: json['baseUrl'] as String,
        companyId: json['companyId'] as String,
        merchantId: (json['merchantId'] as num).toInt(),
        hashKey: json['hashKey'] == null ? null : json['hashKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'baseUrl': baseUrl,
        'companyId': companyId,
        'merchantId': merchantId,
        'hashKey': hashKey,
      };
}

/// Контракт: Loyalty/UpdateLoyaltySettingsRequest.cs
class UpdateLoyaltySettingsRequest {
  const UpdateLoyaltySettingsRequest({
    required this.topUpEnabled,
    required this.topUpPercentBasisPoints,
    required this.shopEnabled,
    required this.shopPercentBasisPoints,
    required this.sessionEnabled,
    required this.sessionPercentBasisPoints,
    required this.cashbackCapMinorUnits,
    required this.minimumSourceMinorUnits,
  });

  final bool topUpEnabled;
  final int topUpPercentBasisPoints;
  final bool shopEnabled;
  final int shopPercentBasisPoints;
  final bool sessionEnabled;
  final int sessionPercentBasisPoints;
  final int cashbackCapMinorUnits;
  final int minimumSourceMinorUnits;

  factory UpdateLoyaltySettingsRequest.fromJson(Map<String, dynamic> json) => UpdateLoyaltySettingsRequest(
        topUpEnabled: json['topUpEnabled'] as bool,
        topUpPercentBasisPoints: (json['topUpPercentBasisPoints'] as num).toInt(),
        shopEnabled: json['shopEnabled'] as bool,
        shopPercentBasisPoints: (json['shopPercentBasisPoints'] as num).toInt(),
        sessionEnabled: json['sessionEnabled'] as bool,
        sessionPercentBasisPoints: (json['sessionPercentBasisPoints'] as num).toInt(),
        cashbackCapMinorUnits: (json['cashbackCapMinorUnits'] as num).toInt(),
        minimumSourceMinorUnits: (json['minimumSourceMinorUnits'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'topUpEnabled': topUpEnabled,
        'topUpPercentBasisPoints': topUpPercentBasisPoints,
        'shopEnabled': shopEnabled,
        'shopPercentBasisPoints': shopPercentBasisPoints,
        'sessionEnabled': sessionEnabled,
        'sessionPercentBasisPoints': sessionPercentBasisPoints,
        'cashbackCapMinorUnits': cashbackCapMinorUnits,
        'minimumSourceMinorUnits': minimumSourceMinorUnits,
      };
}

/// Имя и язык человека — ровно те два поля, которые спрашиваются при регистрации. PIN сюда не
/// входит: его задают позже и в ту секунду, когда он впервые нужен.
///
/// Контракт: Identity/RegistrationContracts.cs
class UpdateMyProfileRequest {
  const UpdateMyProfileRequest({
    required this.displayName,
    this.preferredLocale,
  });

  final String displayName;
  final String? preferredLocale;

  factory UpdateMyProfileRequest.fromJson(Map<String, dynamic> json) => UpdateMyProfileRequest(
        displayName: json['displayName'] as String,
        preferredLocale: json['preferredLocale'] == null ? null : json['preferredLocale'] as String,
      );

  Map<String, dynamic> toJson() => {
        'displayName': displayName,
        'preferredLocale': preferredLocale,
      };
}

/// Контракт: News/UpdateNewsItemRequest.cs
class UpdateNewsItemRequest {
  const UpdateNewsItemRequest({
    this.branchId,
    required this.title,
    required this.body,
    this.imageUrl,
    required this.isPublished,
    this.publishAtUtc,
    this.expiresAtUtc,
    this.showOnPcs,
  });

  final String? branchId;
  final String title;
  final String body;
  final String? imageUrl;
  final bool isPublished;
  final DateTime? publishAtUtc;
  final DateTime? expiresAtUtc;
  final bool? showOnPcs;

  factory UpdateNewsItemRequest.fromJson(Map<String, dynamic> json) => UpdateNewsItemRequest(
        branchId: json['branchId'] == null ? null : json['branchId'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
        isPublished: json['isPublished'] as bool,
        publishAtUtc: json['publishAtUtc'] == null ? null : DateTime.parse(json['publishAtUtc'] as String),
        expiresAtUtc: json['expiresAtUtc'] == null ? null : DateTime.parse(json['expiresAtUtc'] as String),
        showOnPcs: json['showOnPcs'] == null ? null : json['showOnPcs'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'branchId': branchId,
        'title': title,
        'body': body,
        'imageUrl': imageUrl,
        'isPublished': isPublished,
        'publishAtUtc': publishAtUtc?.toIso8601String(),
        'expiresAtUtc': expiresAtUtc?.toIso8601String(),
        'showOnPcs': showOnPcs,
      };
}

/// Контракт: Updates/UpdateOrganizationAdminUpdatePreferenceRequest.cs
class UpdateOrganizationAdminUpdatePreferenceRequest {
  const UpdateOrganizationAdminUpdatePreferenceRequest({
    required this.organizationId,
    required this.maintenanceWindowStart,
    required this.maintenanceWindowEnd,
  });

  final String organizationId;
  final String maintenanceWindowStart;
  final String maintenanceWindowEnd;

  factory UpdateOrganizationAdminUpdatePreferenceRequest.fromJson(Map<String, dynamic> json) => UpdateOrganizationAdminUpdatePreferenceRequest(
        organizationId: json['organizationId'] as String,
        maintenanceWindowStart: json['maintenanceWindowStart'] as String,
        maintenanceWindowEnd: json['maintenanceWindowEnd'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'maintenanceWindowStart': maintenanceWindowStart,
        'maintenanceWindowEnd': maintenanceWindowEnd,
      };
}

/// Оформление клуба: логотип и цвет. Поля у организации были с самого начала и читались публичной
/// витриной и приложением игрока, но записать их было нечем — единственное присвоение жило в сидере
/// для разработки.
///
/// Контракт: Branding/UpdateOrganizationBrandingRequest.cs
class UpdateOrganizationBrandingRequest {
  const UpdateOrganizationBrandingRequest({
    this.logoUrl,
    this.accentColor,
  });

  final String? logoUrl;
  final String? accentColor;

  factory UpdateOrganizationBrandingRequest.fromJson(Map<String, dynamic> json) => UpdateOrganizationBrandingRequest(
        logoUrl: json['logoUrl'] == null ? null : json['logoUrl'] as String,
        accentColor: json['accentColor'] == null ? null : json['accentColor'] as String,
      );

  Map<String, dynamic> toJson() => {
        'logoUrl': logoUrl,
        'accentColor': accentColor,
      };
}

/// Контракт: Platform/Organizations/UpdateOrganizationLimitsRequest.cs
class UpdateOrganizationLimitsRequest {
  const UpdateOrganizationLimitsRequest({
    required this.limits,
  });

  final OrganizationLimitsDto limits;

  factory UpdateOrganizationLimitsRequest.fromJson(Map<String, dynamic> json) => UpdateOrganizationLimitsRequest(
        limits: OrganizationLimitsDto.fromJson(json['limits'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'limits': limits.toJson(),
      };
}

/// Контракт: Platform/Organizations/UpdateOrganizationProfileRequest.cs
class UpdateOrganizationProfileRequest {
  const UpdateOrganizationProfileRequest({
    required this.name,
    this.contactEmail,
    this.contactPhone,
    this.legalDetails,
  });

  final String name;
  final String? contactEmail;
  final String? contactPhone;
  final String? legalDetails;

  factory UpdateOrganizationProfileRequest.fromJson(Map<String, dynamic> json) => UpdateOrganizationProfileRequest(
        name: json['name'] as String,
        contactEmail: json['contactEmail'] == null ? null : json['contactEmail'] as String,
        contactPhone: json['contactPhone'] == null ? null : json['contactPhone'] as String,
        legalDetails: json['legalDetails'] == null ? null : json['legalDetails'] as String,
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'contactEmail': contactEmail,
        'contactPhone': contactPhone,
        'legalDetails': legalDetails,
      };
}

/// Контракт: Platform/Organizations/UpdateOrganizationStatusRequest.cs
class UpdateOrganizationStatusRequest {
  const UpdateOrganizationStatusRequest({
    required this.status,
    required this.reason,
  });

  final String status;
  final String reason;

  factory UpdateOrganizationStatusRequest.fromJson(Map<String, dynamic> json) => UpdateOrganizationStatusRequest(
        status: json['status'] as String,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'status': status,
        'reason': reason,
      };
}

/// Контракт: Platform/SupportNotes/UpdateOrganizationSupportNoteRequest.cs
class UpdateOrganizationSupportNoteRequest {
  const UpdateOrganizationSupportNoteRequest({
    required this.body,
  });

  final String body;

  factory UpdateOrganizationSupportNoteRequest.fromJson(Map<String, dynamic> json) => UpdateOrganizationSupportNoteRequest(
        body: json['body'] as String,
      );

  Map<String, dynamic> toJson() => {
        'body': body,
      };
}

/// Контракт: Platform/Organizations/UpdateOrganizationUpdateChannelRequest.cs
class UpdateOrganizationUpdateChannelRequest {
  const UpdateOrganizationUpdateChannelRequest({
    required this.channel,
    this.pinnedClientVersion,
  });

  final String channel;
  final String? pinnedClientVersion;

  factory UpdateOrganizationUpdateChannelRequest.fromJson(Map<String, dynamic> json) => UpdateOrganizationUpdateChannelRequest(
        channel: json['channel'] as String,
        pinnedClientVersion: json['pinnedClientVersion'] == null ? null : json['pinnedClientVersion'] as String,
      );

  Map<String, dynamic> toJson() => {
        'channel': channel,
        'pinnedClientVersion': pinnedClientVersion,
      };
}

/// Контракт: Packages/UpdatePackageDefinitionRequest.cs
class UpdatePackageDefinitionRequest {
  const UpdatePackageDefinitionRequest({
    required this.organizationId,
    required this.name,
    required this.price,
    required this.includedSeconds,
    required this.bonusSeconds,
    required this.expiresAfterDays,
    required this.isActive,
  });

  final String organizationId;
  final String name;
  final MoneyDto price;
  final int includedSeconds;
  final int bonusSeconds;
  final int expiresAfterDays;
  final bool isActive;

  factory UpdatePackageDefinitionRequest.fromJson(Map<String, dynamic> json) => UpdatePackageDefinitionRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        includedSeconds: (json['includedSeconds'] as num).toInt(),
        bonusSeconds: (json['bonusSeconds'] as num).toInt(),
        expiresAfterDays: (json['expiresAfterDays'] as num).toInt(),
        isActive: json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'price': price.toJson(),
        'includedSeconds': includedSeconds,
        'bonusSeconds': bonusSeconds,
        'expiresAfterDays': expiresAfterDays,
        'isActive': isActive,
      };
}

/// Контракт: Updates/UpdatePackageDto.cs
class UpdatePackageDto {
  const UpdatePackageDto({
    required this.updatePackageId,
    required this.organizationId,
    required this.branchId,
    required this.component,
    required this.version,
    required this.channel,
    required this.artifactUri,
    required this.sha256,
    required this.signature,
    required this.signatureAlgorithm,
    required this.sizeBytes,
    required this.state,
    required this.releaseNotes,
    required this.createdAtUtc,
  });

  final String updatePackageId;
  final String organizationId;
  final String branchId;
  final String component;
  final String version;
  final String channel;
  final String artifactUri;
  final String sha256;
  final String signature;
  final String signatureAlgorithm;
  final int sizeBytes;
  final String state;
  final String releaseNotes;
  final DateTime createdAtUtc;

  factory UpdatePackageDto.fromJson(Map<String, dynamic> json) => UpdatePackageDto(
        updatePackageId: json['updatePackageId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        artifactUri: json['artifactUri'] as String,
        sha256: json['sha256'] as String,
        signature: json['signature'] as String,
        signatureAlgorithm: json['signatureAlgorithm'] as String,
        sizeBytes: (json['sizeBytes'] as num).toInt(),
        state: json['state'] as String,
        releaseNotes: json['releaseNotes'] as String,
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'updatePackageId': updatePackageId,
        'organizationId': organizationId,
        'branchId': branchId,
        'component': component,
        'version': version,
        'channel': channel,
        'artifactUri': artifactUri,
        'sha256': sha256,
        'signature': signature,
        'signatureAlgorithm': signatureAlgorithm,
        'sizeBytes': sizeBytes,
        'state': state,
        'releaseNotes': releaseNotes,
        'createdAtUtc': createdAtUtc.toIso8601String(),
      };
}

/// Контракт: Updates/UpdatePackageStateChangeRequest.cs
class UpdatePackageStateChangeRequest {
  const UpdatePackageStateChangeRequest({
    required this.organizationId,
    required this.state,
    required this.reason,
  });

  final String organizationId;
  final String state;
  final String reason;

  factory UpdatePackageStateChangeRequest.fromJson(Map<String, dynamic> json) => UpdatePackageStateChangeRequest(
        organizationId: json['organizationId'] as String,
        state: json['state'] as String,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'state': state,
        'reason': reason,
      };
}

/// Контракт: Platform/Billing/UpdatePlanRequest.cs
class UpdatePlanRequest {
  const UpdatePlanRequest({
    required this.name,
    required this.priceMinorUnits,
    required this.currencyCode,
    required this.billingInterval,
    this.maxBranches,
    this.maxDevicesPerBranch,
    this.maxConcurrentSessions,
    this.maxStaffUsersPerBranch,
    required this.isActive,
    required this.sortOrder,
    this.pricePerDeviceMinorUnits,
    this.includedDevices,
  });

  final String name;
  final int priceMinorUnits;
  final String currencyCode;
  final String billingInterval;
  final int? maxBranches;
  final int? maxDevicesPerBranch;
  final int? maxConcurrentSessions;
  final int? maxStaffUsersPerBranch;
  final bool isActive;
  final int sortOrder;

  /// Не переданы — остаются прежними: старый редактор тарифов о них не знает.
  final int? pricePerDeviceMinorUnits;
  final int? includedDevices;

  factory UpdatePlanRequest.fromJson(Map<String, dynamic> json) => UpdatePlanRequest(
        name: json['name'] as String,
        priceMinorUnits: (json['priceMinorUnits'] as num).toInt(),
        currencyCode: json['currencyCode'] as String,
        billingInterval: json['billingInterval'] as String,
        maxBranches: json['maxBranches'] == null ? null : (json['maxBranches'] as num).toInt(),
        maxDevicesPerBranch: json['maxDevicesPerBranch'] == null ? null : (json['maxDevicesPerBranch'] as num).toInt(),
        maxConcurrentSessions: json['maxConcurrentSessions'] == null ? null : (json['maxConcurrentSessions'] as num).toInt(),
        maxStaffUsersPerBranch: json['maxStaffUsersPerBranch'] == null ? null : (json['maxStaffUsersPerBranch'] as num).toInt(),
        isActive: json['isActive'] as bool,
        sortOrder: (json['sortOrder'] as num).toInt(),
        pricePerDeviceMinorUnits: json['pricePerDeviceMinorUnits'] == null ? null : (json['pricePerDeviceMinorUnits'] as num).toInt(),
        includedDevices: json['includedDevices'] == null ? null : (json['includedDevices'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'priceMinorUnits': priceMinorUnits,
        'currencyCode': currencyCode,
        'billingInterval': billingInterval,
        'maxBranches': maxBranches,
        'maxDevicesPerBranch': maxDevicesPerBranch,
        'maxConcurrentSessions': maxConcurrentSessions,
        'maxStaffUsersPerBranch': maxStaffUsersPerBranch,
        'isActive': isActive,
        'sortOrder': sortOrder,
        'pricePerDeviceMinorUnits': pricePerDeviceMinorUnits,
        'includedDevices': includedDevices,
      };
}

/// Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs
class UpdatePlatformAdminRequest {
  const UpdatePlatformAdminRequest({
    this.role,
    this.isActive,
  });

  final String? role;
  final bool? isActive;

  factory UpdatePlatformAdminRequest.fromJson(Map<String, dynamic> json) => UpdatePlatformAdminRequest(
        role: json['role'] == null ? null : json['role'] as String,
        isActive: json['isActive'] == null ? null : json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'role': role,
        'isActive': isActive,
      };
}

/// Контракт: Platform/Auth/PlatformRoleContracts.cs
class UpdatePlatformRoleRequest {
  const UpdatePlatformRoleRequest({
    required this.displayName,
    required this.description,
    required this.permissions,
  });

  final String displayName;
  final String description;
  final List<String> permissions;

  factory UpdatePlatformRoleRequest.fromJson(Map<String, dynamic> json) => UpdatePlatformRoleRequest(
        displayName: json['displayName'] as String,
        description: json['description'] as String,
        permissions: (json['permissions'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'displayName': displayName,
        'description': description,
        'permissions': permissions.map((item) => item).toList(),
      };
}

/// Контракт: Players/UpdatePlayerAccountRequest.cs
class UpdatePlayerAccountRequest {
  const UpdatePlayerAccountRequest({
    required this.organizationId,
    required this.displayName,
    this.phoneNumber,
  });

  final String organizationId;
  final String displayName;
  final String? phoneNumber;

  factory UpdatePlayerAccountRequest.fromJson(Map<String, dynamic> json) => UpdatePlayerAccountRequest(
        organizationId: json['organizationId'] as String,
        displayName: json['displayName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'displayName': displayName,
        'phoneNumber': phoneNumber,
      };
}

/// Player-editable profile fields. Both optional; null means "leave unchanged".
///
/// Контракт: Players/UpdatePlayerProfileRequest.cs
class UpdatePlayerProfileRequest {
  const UpdatePlayerProfileRequest({
    this.preferredLocale,
    this.marketingOptIn,
  });

  final String? preferredLocale;
  final bool? marketingOptIn;

  factory UpdatePlayerProfileRequest.fromJson(Map<String, dynamic> json) => UpdatePlayerProfileRequest(
        preferredLocale: json['preferredLocale'] == null ? null : json['preferredLocale'] as String,
        marketingOptIn: json['marketingOptIn'] == null ? null : json['marketingOptIn'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'preferredLocale': preferredLocale,
        'marketingOptIn': marketingOptIn,
      };
}

/// Показывать ли друзьям, что я сейчас в зале.
///
/// Контракт: Friends/FriendDtos.cs
class UpdatePresenceVisibilityRequest {
  const UpdatePresenceVisibilityRequest({
    required this.showsPresence,
  });

  final bool showsPresence;

  factory UpdatePresenceVisibilityRequest.fromJson(Map<String, dynamic> json) => UpdatePresenceVisibilityRequest(
        showsPresence: json['showsPresence'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'showsPresence': showsPresence,
      };
}

/// Правка категории: имя и видимость на стойке. Оба поля необязательны — присланное меняется,
/// пропущенное остаётся как было, поэтому переименование не гасит категорию заодно.
/// Ключа идемпотентности здесь нет намеренно: повтор приводит к тому же состоянию, а денег
/// операция не двигает — в отличие от создания, где повтор завёл бы вторую категорию.
///
/// Контракт: Pos/UpdateProductCategoryRequest.cs
class UpdateProductCategoryRequest {
  const UpdateProductCategoryRequest({
    required this.organizationId,
    this.name,
    this.isActive,
  });

  final String organizationId;
  final String? name;
  final bool? isActive;

  factory UpdateProductCategoryRequest.fromJson(Map<String, dynamic> json) => UpdateProductCategoryRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] == null ? null : json['name'] as String,
        isActive: json['isActive'] == null ? null : json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'isActive': isActive,
      };
}

/// Контракт: Pos/UpdateProductRequest.cs
class UpdateProductRequest {
  const UpdateProductRequest({
    required this.organizationId,
    required this.categoryId,
    required this.name,
    required this.sku,
    required this.price,
    required this.trackStock,
    required this.allowNegativeStock,
    required this.isActive,
    this.reorderThreshold,
    this.availableInShell,
    this.featuredOnPcs,
    this.imageUrl,
  });

  final String organizationId;
  final String categoryId;
  final String name;
  final String sku;
  final MoneyDto price;
  final bool trackStock;
  final bool allowNegativeStock;
  final bool isActive;
  final int? reorderThreshold;
  final bool? availableInShell;

  /// Товар крутится в витрине свободного ПК.
  final bool? featuredOnPcs;

  /// Фото товара: адрес загрузки с назначением product-image. Пусто — без фото.
  final String? imageUrl;

  factory UpdateProductRequest.fromJson(Map<String, dynamic> json) => UpdateProductRequest(
        organizationId: json['organizationId'] as String,
        categoryId: json['categoryId'] as String,
        name: json['name'] as String,
        sku: json['sku'] as String,
        price: MoneyDto.fromJson(json['price'] as Map<String, dynamic>),
        trackStock: json['trackStock'] as bool,
        allowNegativeStock: json['allowNegativeStock'] as bool,
        isActive: json['isActive'] as bool,
        reorderThreshold: json['reorderThreshold'] == null ? null : (json['reorderThreshold'] as num).toInt(),
        availableInShell: json['availableInShell'] == null ? null : json['availableInShell'] as bool,
        featuredOnPcs: json['featuredOnPcs'] == null ? null : json['featuredOnPcs'] as bool,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'categoryId': categoryId,
        'name': name,
        'sku': sku,
        'price': price.toJson(),
        'trackStock': trackStock,
        'allowNegativeStock': allowNegativeStock,
        'isActive': isActive,
        'reorderThreshold': reorderThreshold,
        'availableInShell': availableInShell,
        'featuredOnPcs': featuredOnPcs,
        'imageUrl': imageUrl,
      };
}

/// Контракт: Loyalty/ReferralContracts.cs
class UpdateReferralSettingsRequest {
  const UpdateReferralSettingsRequest({
    required this.enabled,
    required this.referrerBonusMinorUnits,
    required this.inviteeBonusMinorUnits,
    required this.minimumTopUpMinorUnits,
    required this.claimWindowDays,
    required this.maxRewardedPerReferrer,
  });

  final bool enabled;
  final int referrerBonusMinorUnits;
  final int inviteeBonusMinorUnits;
  final int minimumTopUpMinorUnits;
  final int claimWindowDays;
  final int maxRewardedPerReferrer;

  factory UpdateReferralSettingsRequest.fromJson(Map<String, dynamic> json) => UpdateReferralSettingsRequest(
        enabled: json['enabled'] as bool,
        referrerBonusMinorUnits: (json['referrerBonusMinorUnits'] as num).toInt(),
        inviteeBonusMinorUnits: (json['inviteeBonusMinorUnits'] as num).toInt(),
        minimumTopUpMinorUnits: (json['minimumTopUpMinorUnits'] as num).toInt(),
        claimWindowDays: (json['claimWindowDays'] as num).toInt(),
        maxRewardedPerReferrer: (json['maxRewardedPerReferrer'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'enabled': enabled,
        'referrerBonusMinorUnits': referrerBonusMinorUnits,
        'inviteeBonusMinorUnits': inviteeBonusMinorUnits,
        'minimumTopUpMinorUnits': minimumTopUpMinorUnits,
        'claimWindowDays': claimWindowDays,
        'maxRewardedPerReferrer': maxRewardedPerReferrer,
      };
}

/// Правка рассылки: частота и пауза. Оба поля необязательны — присланное меняется,
/// пропущенное остаётся как было.
/// Пауза, а не удаление: уйти в отпуск на две недели и не получать письма — не то же самое,
/// что отказаться от рассылки совсем и заводить её заново.
///
/// Контракт: Reports/ReportScheduleContracts.cs
class UpdateReportScheduleRequest {
  const UpdateReportScheduleRequest({
    required this.organizationId,
    this.frequency,
    this.isActive,
  });

  final String organizationId;
  final String? frequency;
  final bool? isActive;

  factory UpdateReportScheduleRequest.fromJson(Map<String, dynamic> json) => UpdateReportScheduleRequest(
        organizationId: json['organizationId'] as String,
        frequency: json['frequency'] == null ? null : json['frequency'] as String,
        isActive: json['isActive'] == null ? null : json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'frequency': frequency,
        'isActive': isActive,
      };
}

/// Контракт: Reservations/ReservationRequests.cs
class UpdateReservationRequest {
  const UpdateReservationRequest({
    required this.organizationId,
    this.playerAccountId,
    this.seatId,
    this.customerName,
    this.phoneNumber,
    this.startsAtUtc,
    this.durationMinutes,
    this.source,
    this.note,
    required this.expectedVersion,
  });

  final String organizationId;
  final String? playerAccountId;
  final String? seatId;
  final String? customerName;
  final String? phoneNumber;
  final DateTime? startsAtUtc;
  final int? durationMinutes;
  final String? source;
  final String? note;
  final int expectedVersion;

  factory UpdateReservationRequest.fromJson(Map<String, dynamic> json) => UpdateReservationRequest(
        organizationId: json['organizationId'] as String,
        playerAccountId: json['playerAccountId'] == null ? null : json['playerAccountId'] as String,
        seatId: json['seatId'] == null ? null : json['seatId'] as String,
        customerName: json['customerName'] == null ? null : json['customerName'] as String,
        phoneNumber: json['phoneNumber'] == null ? null : json['phoneNumber'] as String,
        startsAtUtc: json['startsAtUtc'] == null ? null : DateTime.parse(json['startsAtUtc'] as String),
        durationMinutes: json['durationMinutes'] == null ? null : (json['durationMinutes'] as num).toInt(),
        source: json['source'] == null ? null : json['source'] as String,
        note: json['note'] == null ? null : json['note'] as String,
        expectedVersion: (json['expectedVersion'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'playerAccountId': playerAccountId,
        'seatId': seatId,
        'customerName': customerName,
        'phoneNumber': phoneNumber,
        'startsAtUtc': startsAtUtc?.toIso8601String(),
        'durationMinutes': durationMinutes,
        'source': source,
        'note': note,
        'expectedVersion': expectedVersion,
      };
}

/// Контракт: Updates/UpdateRolloutDto.cs
class UpdateRolloutDto {
  const UpdateRolloutDto({
    required this.updateRolloutId,
    required this.organizationId,
    required this.branchId,
    required this.updatePackageId,
    required this.component,
    required this.version,
    required this.channel,
    required this.state,
    required this.targetKind,
    required this.targetDeviceIds,
    required this.batchPercent,
    required this.createdAtUtc,
    required this.startsAtUtc,
    this.completedAtUtc,
  });

  final String updateRolloutId;
  final String organizationId;
  final String branchId;
  final String updatePackageId;
  final String component;
  final String version;
  final String channel;
  final String state;
  final String targetKind;
  final List<String> targetDeviceIds;
  final int batchPercent;
  final DateTime createdAtUtc;
  final DateTime startsAtUtc;
  final DateTime? completedAtUtc;

  factory UpdateRolloutDto.fromJson(Map<String, dynamic> json) => UpdateRolloutDto(
        updateRolloutId: json['updateRolloutId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        state: json['state'] as String,
        targetKind: json['targetKind'] as String,
        targetDeviceIds: (json['targetDeviceIds'] as List<dynamic>).map((item) => item as String).toList(),
        batchPercent: (json['batchPercent'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        completedAtUtc: json['completedAtUtc'] == null ? null : DateTime.parse(json['completedAtUtc'] as String),
      );

  Map<String, dynamic> toJson() => {
        'updateRolloutId': updateRolloutId,
        'organizationId': organizationId,
        'branchId': branchId,
        'updatePackageId': updatePackageId,
        'component': component,
        'version': version,
        'channel': channel,
        'state': state,
        'targetKind': targetKind,
        'targetDeviceIds': targetDeviceIds.map((item) => item).toList(),
        'batchPercent': batchPercent,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'completedAtUtc': completedAtUtc?.toIso8601String(),
      };
}

/// Контракт: Updates/UpdateRolloutStateChangeRequest.cs
class UpdateRolloutStateChangeRequest {
  const UpdateRolloutStateChangeRequest({
    required this.organizationId,
    required this.state,
    required this.reason,
  });

  final String organizationId;
  final String state;
  final String reason;

  factory UpdateRolloutStateChangeRequest.fromJson(Map<String, dynamic> json) => UpdateRolloutStateChangeRequest(
        organizationId: json['organizationId'] as String,
        state: json['state'] as String,
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'state': state,
        'reason': reason,
      };
}

/// Контракт: Updates/UpdateRolloutStatusDto.cs
class UpdateRolloutStatusDto {
  const UpdateRolloutStatusDto({
    required this.updateRolloutId,
    required this.organizationId,
    required this.branchId,
    required this.updatePackageId,
    required this.component,
    required this.version,
    required this.channel,
    required this.state,
    required this.targetKind,
    required this.targetDeviceIds,
    required this.batchPercent,
    required this.createdAtUtc,
    required this.startsAtUtc,
    this.completedAtUtc,
    required this.deviceStatuses,
  });

  final String updateRolloutId;
  final String organizationId;
  final String branchId;
  final String updatePackageId;
  final String component;
  final String version;
  final String channel;
  final String state;
  final String targetKind;
  final List<String> targetDeviceIds;
  final int batchPercent;
  final DateTime createdAtUtc;
  final DateTime startsAtUtc;
  final DateTime? completedAtUtc;
  final List<DeviceUpdateStatusSnapshotDto> deviceStatuses;

  factory UpdateRolloutStatusDto.fromJson(Map<String, dynamic> json) => UpdateRolloutStatusDto(
        updateRolloutId: json['updateRolloutId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        updatePackageId: json['updatePackageId'] as String,
        component: json['component'] as String,
        version: json['version'] as String,
        channel: json['channel'] as String,
        state: json['state'] as String,
        targetKind: json['targetKind'] as String,
        targetDeviceIds: (json['targetDeviceIds'] as List<dynamic>).map((item) => item as String).toList(),
        batchPercent: (json['batchPercent'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        completedAtUtc: json['completedAtUtc'] == null ? null : DateTime.parse(json['completedAtUtc'] as String),
        deviceStatuses: (json['deviceStatuses'] as List<dynamic>).map((item) => DeviceUpdateStatusSnapshotDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'updateRolloutId': updateRolloutId,
        'organizationId': organizationId,
        'branchId': branchId,
        'updatePackageId': updatePackageId,
        'component': component,
        'version': version,
        'channel': channel,
        'state': state,
        'targetKind': targetKind,
        'targetDeviceIds': targetDeviceIds.map((item) => item).toList(),
        'batchPercent': batchPercent,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'completedAtUtc': completedAtUtc?.toIso8601String(),
        'deviceStatuses': deviceStatuses.map((item) => item.toJson()).toList(),
      };
}

/// Контракт: Layout/UpdateSeatRequest.cs
class UpdateSeatRequest {
  const UpdateSeatRequest({
    required this.organizationId,
    required this.zoneId,
    required this.name,
    required this.sortOrder,
  });

  final String organizationId;
  final String zoneId;
  final String name;
  final int sortOrder;

  factory UpdateSeatRequest.fromJson(Map<String, dynamic> json) => UpdateSeatRequest(
        organizationId: json['organizationId'] as String,
        zoneId: json['zoneId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'zoneId': zoneId,
        'name': name,
        'sortOrder': sortOrder,
      };
}

/// Контракт: Identity/UpdateStaffUserProfileRequest.cs
class UpdateStaffUserProfileRequest {
  const UpdateStaffUserProfileRequest({
    required this.organizationId,
    required this.userName,
    required this.displayName,
  });

  final String organizationId;
  final String userName;
  final String displayName;

  factory UpdateStaffUserProfileRequest.fromJson(Map<String, dynamic> json) => UpdateStaffUserProfileRequest(
        organizationId: json['organizationId'] as String,
        userName: json['userName'] as String,
        displayName: json['displayName'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'userName': userName,
        'displayName': displayName,
      };
}

/// Контракт: Identity/UpdateStaffUserRolesRequest.cs
class UpdateStaffUserRolesRequest {
  const UpdateStaffUserRolesRequest({
    required this.organizationId,
    required this.roleNames,
  });

  final String organizationId;
  final List<String> roleNames;

  factory UpdateStaffUserRolesRequest.fromJson(Map<String, dynamic> json) => UpdateStaffUserRolesRequest(
        organizationId: json['organizationId'] as String,
        roleNames: (json['roleNames'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'roleNames': roleNames.map((item) => item).toList(),
      };
}

/// Контракт: Identity/UpdateStaffUserStateRequest.cs
class UpdateStaffUserStateRequest {
  const UpdateStaffUserStateRequest({
    required this.organizationId,
    required this.isActive,
  });

  final String organizationId;
  final bool isActive;

  factory UpdateStaffUserStateRequest.fromJson(Map<String, dynamic> json) => UpdateStaffUserStateRequest(
        organizationId: json['organizationId'] as String,
        isActive: json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'isActive': isActive,
      };
}

/// Контракт: Platform/Billing/UpdateSubscriptionRequest.cs
class UpdateSubscriptionRequest {
  const UpdateSubscriptionRequest({
    this.planCode,
    this.billingInterval,
    this.status,
    this.cancelAtPeriodEnd,
    this.amountMinorUnits,
    this.currentPeriodEndUtc,
    this.paymentGraceUntilUtc,
    this.clearPaymentGrace,
    this.discountPercent,
    this.discountAmountMinorUnits,
    this.discountUntilUtc,
    this.discountReason,
    this.clearDiscount,
  });

  final String? planCode;
  final String? billingInterval;
  final String? status;
  final bool? cancelAtPeriodEnd;
  final int? amountMinorUnits;
  final DateTime? currentPeriodEndUtc;
  final DateTime? paymentGraceUntilUtc;
  final bool? clearPaymentGrace;
  final int? discountPercent;
  final int? discountAmountMinorUnits;
  final DateTime? discountUntilUtc;
  final String? discountReason;
  final bool? clearDiscount;

  factory UpdateSubscriptionRequest.fromJson(Map<String, dynamic> json) => UpdateSubscriptionRequest(
        planCode: json['planCode'] == null ? null : json['planCode'] as String,
        billingInterval: json['billingInterval'] == null ? null : json['billingInterval'] as String,
        status: json['status'] == null ? null : json['status'] as String,
        cancelAtPeriodEnd: json['cancelAtPeriodEnd'] == null ? null : json['cancelAtPeriodEnd'] as bool,
        amountMinorUnits: json['amountMinorUnits'] == null ? null : (json['amountMinorUnits'] as num).toInt(),
        currentPeriodEndUtc: json['currentPeriodEndUtc'] == null ? null : DateTime.parse(json['currentPeriodEndUtc'] as String),
        paymentGraceUntilUtc: json['paymentGraceUntilUtc'] == null ? null : DateTime.parse(json['paymentGraceUntilUtc'] as String),
        clearPaymentGrace: json['clearPaymentGrace'] == null ? null : json['clearPaymentGrace'] as bool,
        discountPercent: json['discountPercent'] == null ? null : (json['discountPercent'] as num).toInt(),
        discountAmountMinorUnits: json['discountAmountMinorUnits'] == null ? null : (json['discountAmountMinorUnits'] as num).toInt(),
        discountUntilUtc: json['discountUntilUtc'] == null ? null : DateTime.parse(json['discountUntilUtc'] as String),
        discountReason: json['discountReason'] == null ? null : json['discountReason'] as String,
        clearDiscount: json['clearDiscount'] == null ? null : json['clearDiscount'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'planCode': planCode,
        'billingInterval': billingInterval,
        'status': status,
        'cancelAtPeriodEnd': cancelAtPeriodEnd,
        'amountMinorUnits': amountMinorUnits,
        'currentPeriodEndUtc': currentPeriodEndUtc?.toIso8601String(),
        'paymentGraceUntilUtc': paymentGraceUntilUtc?.toIso8601String(),
        'clearPaymentGrace': clearPaymentGrace,
        'discountPercent': discountPercent,
        'discountAmountMinorUnits': discountAmountMinorUnits,
        'discountUntilUtc': discountUntilUtc?.toIso8601String(),
        'discountReason': discountReason,
        'clearDiscount': clearDiscount,
      };
}

/// `Schedule` не передан — расписание остаётся прежним. Снятие тарифа с продажи и
/// переименование не должны требовать от вызывающего знания о часах. Так же и
/// `FeaturedOnPcs`: не передан — отметка в витрине не меняется.
///
/// Контракт: Tariffs/UpdateTariffRequest.cs
class UpdateTariffRequest {
  const UpdateTariffRequest({
    required this.organizationId,
    required this.name,
    required this.isActive,
    this.schedule,
    this.featuredOnPcs,
  });

  final String organizationId;
  final String name;
  final bool isActive;
  final TariffScheduleDto? schedule;
  final bool? featuredOnPcs;

  factory UpdateTariffRequest.fromJson(Map<String, dynamic> json) => UpdateTariffRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        isActive: json['isActive'] as bool,
        schedule: json['schedule'] == null ? null : TariffScheduleDto.fromJson(json['schedule'] as Map<String, dynamic>),
        featuredOnPcs: json['featuredOnPcs'] == null ? null : json['featuredOnPcs'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'isActive': isActive,
        'schedule': schedule?.toJson(),
        'featuredOnPcs': featuredOnPcs,
      };
}

/// Контракт: Tariffs/UpdateTariffVersionRequest.cs
class UpdateTariffVersionRequest {
  const UpdateTariffVersionRequest({
    required this.organizationId,
    required this.currencyCode,
    required this.pricePerMinuteMinorUnits,
    required this.minimumBillableMinutes,
    required this.roundingIncrementMinutes,
    required this.effectiveFromUtc,
    required this.isActive,
  });

  final String organizationId;
  final String currencyCode;
  final int pricePerMinuteMinorUnits;
  final int minimumBillableMinutes;
  final int roundingIncrementMinutes;
  final DateTime effectiveFromUtc;
  final bool isActive;

  factory UpdateTariffVersionRequest.fromJson(Map<String, dynamic> json) => UpdateTariffVersionRequest(
        organizationId: json['organizationId'] as String,
        currencyCode: json['currencyCode'] as String,
        pricePerMinuteMinorUnits: (json['pricePerMinuteMinorUnits'] as num).toInt(),
        minimumBillableMinutes: (json['minimumBillableMinutes'] as num).toInt(),
        roundingIncrementMinutes: (json['roundingIncrementMinutes'] as num).toInt(),
        effectiveFromUtc: DateTime.parse(json['effectiveFromUtc'] as String),
        isActive: json['isActive'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'currencyCode': currencyCode,
        'pricePerMinuteMinorUnits': pricePerMinuteMinorUnits,
        'minimumBillableMinutes': minimumBillableMinutes,
        'roundingIncrementMinutes': roundingIncrementMinutes,
        'effectiveFromUtc': effectiveFromUtc.toIso8601String(),
        'isActive': isActive,
      };
}

/// Контракт: Tips/TipContracts.cs
class UpdateTipSettingsRequest {
  const UpdateTipSettingsRequest({
    required this.enabled,
  });

  final bool enabled;

  factory UpdateTipSettingsRequest.fromJson(Map<String, dynamic> json) => UpdateTipSettingsRequest(
        enabled: json['enabled'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'enabled': enabled,
      };
}

/// Правка события. Незаполненное поле означает «оставить как было» — стойка правит одну строку,
/// а не переписывает событие целиком.
///
/// Контракт: Tournaments/TournamentDtos.cs
class UpdateTournamentRequest {
  const UpdateTournamentRequest({
    this.title,
    this.description,
    this.discipline,
    this.startsAtUtc,
    this.entryFeeMinorUnits,
    this.capacity,
  });

  final String? title;
  final String? description;
  final String? discipline;
  final DateTime? startsAtUtc;
  final int? entryFeeMinorUnits;
  final int? capacity;

  factory UpdateTournamentRequest.fromJson(Map<String, dynamic> json) => UpdateTournamentRequest(
        title: json['title'] == null ? null : json['title'] as String,
        description: json['description'] == null ? null : json['description'] as String,
        discipline: json['discipline'] == null ? null : json['discipline'] as String,
        startsAtUtc: json['startsAtUtc'] == null ? null : DateTime.parse(json['startsAtUtc'] as String),
        entryFeeMinorUnits: json['entryFeeMinorUnits'] == null ? null : (json['entryFeeMinorUnits'] as num).toInt(),
        capacity: json['capacity'] == null ? null : (json['capacity'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'title': title,
        'description': description,
        'discipline': discipline,
        'startsAtUtc': startsAtUtc?.toIso8601String(),
        'entryFeeMinorUnits': entryFeeMinorUnits,
        'capacity': capacity,
      };
}

/// Контракт: Layout/UpdateZoneRequest.cs
class UpdateZoneRequest {
  const UpdateZoneRequest({
    required this.organizationId,
    required this.name,
    required this.sortOrder,
    this.hardwareSummary,
  });

  final String organizationId;
  final String name;
  final int sortOrder;
  final String? hardwareSummary;

  factory UpdateZoneRequest.fromJson(Map<String, dynamic> json) => UpdateZoneRequest(
        organizationId: json['organizationId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
        hardwareSummary: json['hardwareSummary'] == null ? null : json['hardwareSummary'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'name': name,
        'sortOrder': sortOrder,
        'hardwareSummary': hardwareSummary,
      };
}

/// Контракт: Media/UploadedMediaDto.cs
class UploadedMediaDto {
  const UploadedMediaDto({
    required this.mediaId,
    required this.url,
    required this.contentType,
    required this.sizeBytes,
  });

  final String mediaId;
  final String url;
  final String contentType;
  final int sizeBytes;

  factory UploadedMediaDto.fromJson(Map<String, dynamic> json) => UploadedMediaDto(
        mediaId: json['mediaId'] as String,
        url: json['url'] as String,
        contentType: json['contentType'] as String,
        sizeBytes: (json['sizeBytes'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {
        'mediaId': mediaId,
        'url': url,
        'contentType': contentType,
        'sizeBytes': sizeBytes,
      };
}

/// Контракт: Ads/AdContracts.cs
class UpsertAdCampaignRequest {
  const UpsertAdCampaignRequest({
    required this.advertiserId,
    required this.name,
    required this.category,
    required this.startsAtUtc,
    required this.endsAtUtc,
    this.cities,
    this.organizationIds,
  });

  final String advertiserId;
  final String name;
  final String category;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final List<String>? cities;
  final List<String>? organizationIds;

  factory UpsertAdCampaignRequest.fromJson(Map<String, dynamic> json) => UpsertAdCampaignRequest(
        advertiserId: json['advertiserId'] as String,
        name: json['name'] as String,
        category: json['category'] as String,
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        endsAtUtc: DateTime.parse(json['endsAtUtc'] as String),
        cities: json['cities'] == null ? null : (json['cities'] as List<dynamic>).map((item) => item as String).toList(),
        organizationIds: json['organizationIds'] == null ? null : (json['organizationIds'] as List<dynamic>).map((item) => item as String).toList(),
      );

  Map<String, dynamic> toJson() => {
        'advertiserId': advertiserId,
        'name': name,
        'category': category,
        'startsAtUtc': startsAtUtc.toIso8601String(),
        'endsAtUtc': endsAtUtc.toIso8601String(),
        'cities': cities?.map((item) => item).toList(),
        'organizationIds': organizationIds?.map((item) => item).toList(),
      };
}

/// Контракт: Ads/AdContracts.cs
class UpsertAdCreativeRequest {
  const UpsertAdCreativeRequest({
    required this.title,
    this.body,
    this.imageUrl,
  });

  final String title;
  final String? body;
  final String? imageUrl;

  factory UpsertAdCreativeRequest.fromJson(Map<String, dynamic> json) => UpsertAdCreativeRequest(
        title: json['title'] as String,
        body: json['body'] == null ? null : json['body'] as String,
        imageUrl: json['imageUrl'] == null ? null : json['imageUrl'] as String,
      );

  Map<String, dynamic> toJson() => {
        'title': title,
        'body': body,
        'imageUrl': imageUrl,
      };
}

/// Контракт: Ads/AdContracts.cs
class UpsertAdvertiserRequest {
  const UpsertAdvertiserRequest({
    required this.name,
    this.contact,
  });

  final String name;
  final String? contact;

  factory UpsertAdvertiserRequest.fromJson(Map<String, dynamic> json) => UpsertAdvertiserRequest(
        name: json['name'] as String,
        contact: json['contact'] == null ? null : json['contact'] as String,
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'contact': contact,
      };
}

/// Контракт: Games/GameLibraryContracts.cs
class UpsertBranchGameRequest {
  const UpsertBranchGameRequest({
    required this.organizationId,
    this.catalogGameId,
    required this.name,
    this.genre,
    this.minAge,
    required this.launchKind,
    this.launchTarget,
    this.executablePath,
    this.arguments,
    required this.availableWithoutSession,
    required this.isEnabled,
    this.launchOnSessionStart,
  });

  final String organizationId;
  final String? catalogGameId;
  final String name;
  final String? genre;
  final int? minAge;
  final String launchKind;
  final String? launchTarget;
  final String? executablePath;
  final String? arguments;
  final bool availableWithoutSession;
  final bool isEnabled;
  final bool? launchOnSessionStart;

  factory UpsertBranchGameRequest.fromJson(Map<String, dynamic> json) => UpsertBranchGameRequest(
        organizationId: json['organizationId'] as String,
        catalogGameId: json['catalogGameId'] == null ? null : json['catalogGameId'] as String,
        name: json['name'] as String,
        genre: json['genre'] == null ? null : json['genre'] as String,
        minAge: json['minAge'] == null ? null : (json['minAge'] as num).toInt(),
        launchKind: json['launchKind'] as String,
        launchTarget: json['launchTarget'] == null ? null : json['launchTarget'] as String,
        executablePath: json['executablePath'] == null ? null : json['executablePath'] as String,
        arguments: json['arguments'] == null ? null : json['arguments'] as String,
        availableWithoutSession: json['availableWithoutSession'] as bool,
        isEnabled: json['isEnabled'] as bool,
        launchOnSessionStart: json['launchOnSessionStart'] == null ? null : json['launchOnSessionStart'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'catalogGameId': catalogGameId,
        'name': name,
        'genre': genre,
        'minAge': minAge,
        'launchKind': launchKind,
        'launchTarget': launchTarget,
        'executablePath': executablePath,
        'arguments': arguments,
        'availableWithoutSession': availableWithoutSession,
        'isEnabled': isEnabled,
        'launchOnSessionStart': launchOnSessionStart,
      };
}

/// Контракт: Games/GameLibraryContracts.cs
class UpsertCatalogGameRequest {
  const UpsertCatalogGameRequest({
    required this.name,
    this.description,
    this.genre,
    this.minAge,
    required this.launchKind,
    this.launchTarget,
    this.coverUrl,
    required this.isPublished,
  });

  final String name;
  final String? description;
  final String? genre;
  final int? minAge;
  final String launchKind;
  final String? launchTarget;
  final String? coverUrl;
  final bool isPublished;

  factory UpsertCatalogGameRequest.fromJson(Map<String, dynamic> json) => UpsertCatalogGameRequest(
        name: json['name'] as String,
        description: json['description'] == null ? null : json['description'] as String,
        genre: json['genre'] == null ? null : json['genre'] as String,
        minAge: json['minAge'] == null ? null : (json['minAge'] as num).toInt(),
        launchKind: json['launchKind'] as String,
        launchTarget: json['launchTarget'] == null ? null : json['launchTarget'] as String,
        coverUrl: json['coverUrl'] == null ? null : json['coverUrl'] as String,
        isPublished: json['isPublished'] as bool,
      );

  Map<String, dynamic> toJson() => {
        'name': name,
        'description': description,
        'genre': genre,
        'minAge': minAge,
        'launchKind': launchKind,
        'launchTarget': launchTarget,
        'coverUrl': coverUrl,
        'isPublished': isPublished,
      };
}

/// Контракт: Platform/Billing/VoidInvoiceRequest.cs
class VoidInvoiceRequest {
  const VoidInvoiceRequest({
    required this.reason,
  });

  final String reason;

  factory VoidInvoiceRequest.fromJson(Map<String, dynamic> json) => VoidInvoiceRequest(
        reason: json['reason'] as String,
      );

  Map<String, dynamic> toJson() => {
        'reason': reason,
      };
}

/// Контракт: Pos/VoidPosSaleRequest.cs
class VoidPosSaleRequest {
  const VoidPosSaleRequest({
    required this.organizationId,
    required this.reason,
    required this.idempotencyKey,
  });

  final String organizationId;
  final String reason;
  final String idempotencyKey;

  factory VoidPosSaleRequest.fromJson(Map<String, dynamic> json) => VoidPosSaleRequest(
        organizationId: json['organizationId'] as String,
        reason: json['reason'] as String,
        idempotencyKey: json['idempotencyKey'] as String,
      );

  Map<String, dynamic> toJson() => {
        'organizationId': organizationId,
        'reason': reason,
        'idempotencyKey': idempotencyKey,
      };
}

/// Деньги игрока в одном клубе: сколько можно потратить, сколько придержано под брони, сколько он
/// должен.
/// WalletBalance — доступный остаток, и он таким и остаётся: заморозка под бронь
/// из него уже вычтена, потому что холд и есть отрицательная запись журнала.
/// HeldBalance ничего не переносит и не пересчитывает — оно объясняет, куда
/// делась часть остатка.
/// Это ответ на денежную операцию, и он отдельно от `PlayerDashboardDto` намеренно: идущей
/// сессии здесь нет, и делать вид, что она просто «пустая», значит однажды показать «сессии нет»
/// там, где она есть.
///
/// Контракт: Billing/WalletSummaryDto.cs
class WalletSummaryDto {
  const WalletSummaryDto({
    required this.playerAccountId,
    required this.walletBalance,
    required this.heldBalance,
    required this.debtBalance,
    required this.recentEntries,
  });

  final String playerAccountId;
  final MoneyDto walletBalance;
  final MoneyDto heldBalance;
  final MoneyDto debtBalance;
  final List<LedgerEntryDto> recentEntries;

  factory WalletSummaryDto.fromJson(Map<String, dynamic> json) => WalletSummaryDto(
        playerAccountId: json['playerAccountId'] as String,
        walletBalance: MoneyDto.fromJson(json['walletBalance'] as Map<String, dynamic>),
        heldBalance: MoneyDto.fromJson(json['heldBalance'] as Map<String, dynamic>),
        debtBalance: MoneyDto.fromJson(json['debtBalance'] as Map<String, dynamic>),
        recentEntries: (json['recentEntries'] as List<dynamic>).map((item) => LedgerEntryDto.fromJson(item as Map<String, dynamic>)).toList(),
      );

  Map<String, dynamic> toJson() => {
        'playerAccountId': playerAccountId,
        'walletBalance': walletBalance.toJson(),
        'heldBalance': heldBalance.toJson(),
        'debtBalance': debtBalance.toJson(),
        'recentEntries': recentEntries.map((item) => item.toJson()).toList(),
      };
}

/// Зал и его места. HardwareSummary — необязательный хвост: новое поле не должно
/// ломать позиционные вызовы, которых у этого контракта хватает и в Windows-проектах.
///
/// Контракт: Layout/ZoneDto.cs
class ZoneDto {
  const ZoneDto({
    required this.zoneId,
    required this.organizationId,
    required this.branchId,
    required this.name,
    required this.sortOrder,
    required this.createdAtUtc,
    required this.seats,
    this.hardwareSummary,
  });

  final String zoneId;
  final String organizationId;
  final String branchId;
  final String name;
  final int sortOrder;
  final DateTime createdAtUtc;
  final List<SeatDto> seats;
  final String? hardwareSummary;

  factory ZoneDto.fromJson(Map<String, dynamic> json) => ZoneDto(
        zoneId: json['zoneId'] as String,
        organizationId: json['organizationId'] as String,
        branchId: json['branchId'] as String,
        name: json['name'] as String,
        sortOrder: (json['sortOrder'] as num).toInt(),
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
        seats: (json['seats'] as List<dynamic>).map((item) => SeatDto.fromJson(item as Map<String, dynamic>)).toList(),
        hardwareSummary: json['hardwareSummary'] == null ? null : json['hardwareSummary'] as String,
      );

  Map<String, dynamic> toJson() => {
        'zoneId': zoneId,
        'organizationId': organizationId,
        'branchId': branchId,
        'name': name,
        'sortOrder': sortOrder,
        'createdAtUtc': createdAtUtc.toIso8601String(),
        'seats': seats.map((item) => item.toJson()).toList(),
        'hardwareSummary': hardwareSummary,
      };
}
