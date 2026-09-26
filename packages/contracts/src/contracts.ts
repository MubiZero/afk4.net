// Сгенерировано из src/AFK4.Shared.Contracts. Руками не править:
// правка живёт в записи C#, а сюда приезжает через `bun run gen` в packages/contracts.

/** Идентификатор. На проводе это строка, и сравнивать его надо как строку. */
export type Guid = string;

/** Момент времени, ISO-8601 с зоной: `2026-09-15T10:00:00+05:00`. */
export type IsoDateTime = string;

/** Дата без времени: `2026-09-15`. */
export type IsoDate = string;

/** Время без даты: `22:30`. */
export type IsoTime = string;

/** Длительность, ISO-8601: `PT2H30M`. */
export type IsoDuration = string;

/** Словарь: Ads/AdContracts.cs */
export const AdCampaignStateNames = {
  Draft: 'draft',
  /** Идёт в своих датах, если у неё есть одобренный креатив. */
  Active: 'active',
  Paused: 'paused',
} as const;
export type AdCampaignStateName = (typeof AdCampaignStateNames)[keyof typeof AdCampaignStateNames];

/** Словарь: Ads/AdContracts.cs */
export const AdCategoryNames = {
  Food: 'food',
  Electronics: 'electronics',
  Games: 'games',
  Education: 'education',
  Services: 'services',
  Telecom: 'telecom',
  Other: 'other',
  /** Лекарства без рецепта, медтехника, БАД, косметика, методы лечения — только с разрешением Минздрава (ст. 17). */
  HealthBeauty: 'health_beauty',
  /** Банки, страхование, инвестиции — без обещаний доходности (ст. 18). */
  Finance: 'finance',
  /** Социальная реклама — без брендов (ст. 19); считается отдельно. */
  Social: 'social',
} as const;
export type AdCategoryName = (typeof AdCategoryNames)[keyof typeof AdCategoryNames];

/** Словарь: Ads/AdContracts.cs */
export const AdErrorCodeNames = {
  Invalid: 'ad_invalid',
  NotApproved: 'ad_campaign_without_approved_creative',
  ConfirmationRequired: 'ad_moderation_confirmation_required',
  /** Для «Здоровья и красоты» нужен номер разрешения Минздрава. */
  PermitRequired: 'ad_permit_required',
  /** Одобренный креатив не правится: его хранят как показанный. Нужен новый креатив. */
  CreativeLocked: 'ad_creative_locked',
  /** Картинку не удалось скачать для хранения — одобрить без копии нельзя. */
  ImageUnavailable: 'ad_image_unavailable',
} as const;
export type AdErrorCodeName = (typeof AdErrorCodeNames)[keyof typeof AdErrorCodeNames];

/**
 * Отметки модератора при одобрении — по статьям закона РТ «О рекламе» (спека рекламы, §8.2).
 *
 * Словарь: Ads/AdContracts.cs
 */
export const AdModerationCheckNames = {
  /** Не другой клуб, не ставки и не казино — правило платформы. */
  NotClubOrBetting: 'not_club_or_betting',
  /** Нет запрещённого товара (ст. 17), рекламодатель не производит алкоголь и табак (ст. 20). */
  NoBannedGoods: 'no_banned_goods',
  /** Защита несовершеннолетних (ст. 21). */
  Minors: 'minors',
  /** Достоверно: превосходные степени — только с документом (ст. 7). */
  Truthful: 'truthful',
  /** Этично и честно: без оскорблений, порочащих сравнений, скрытых вставок (ст. 6, 8, 9, 10). */
  Ethical: 'ethical',
  /** Текст на картинке — на таджикском или есть и на таджикском (ст. 5). */
  TajikOnImage: 'tajik_on_image',
  /**
   * Только у кампаний «Финансы» (ст. 18): без обещаний доходности и гарантий, без умолчания
   * условий договора. В All не входит — у остальных категорий её нет.
   */
  FinanceTerms: 'finance_terms',
} as const;
export type AdModerationCheckName = (typeof AdModerationCheckNames)[keyof typeof AdModerationCheckNames];

/** Словарь: Ads/AdContracts.cs */
export const AdModerationNames = {
  Pending: 'pending',
  Approved: 'approved',
  Rejected: 'rejected',
} as const;
export type AdModerationName = (typeof AdModerationNames)[keyof typeof AdModerationNames];

/** Словарь: Platform/Billing/BillingIntervalNames.cs */
export const BillingIntervalNames = {
  Monthly: 'monthly',
  Yearly: 'yearly',
} as const;
export type BillingIntervalName = (typeof BillingIntervalNames)[keyof typeof BillingIntervalNames];

/** Словарь: Billing/BillingModeNames.cs */
export const BillingModeNames = {
  PrepaidWallet: 'prepaid_wallet',
  PostpaidDebt: 'postpaid_debt',
  Package: 'package',
} as const;
export type BillingModeName = (typeof BillingModeNames)[keyof typeof BillingModeNames];

/**
 * Что оператор может найти одной строкой из палитры. Строки, а не enum: контракт переживает
 * клиентов, и старый клиент, встретив незнакомый вид, просто его не покажет.
 *
 * Словарь: Operator/BranchSearchResultDto.cs
 */
export const BranchSearchKindNames = {
  Seat: 'seat',
  Player: 'player',
  Reservation: 'reservation',
  Receipt: 'receipt',
  Order: 'order',
} as const;
export type BranchSearchKindName = (typeof BranchSearchKindNames)[keyof typeof BranchSearchKindNames];

/** Словарь: Shifts/CashMovementTypeNames.cs */
export const CashMovementTypeNames = {
  CashIn: 'cash_in',
  CashOut: 'cash_out',
} as const;
export type CashMovementTypeName = (typeof CashMovementTypeNames)[keyof typeof CashMovementTypeNames];

/** Словарь: Platform/Billing/ClubPlanContracts.cs */
export const ClubPlanErrorCodeNames = {
  TrialUsed: 'plan_trial_used',
  /** Сначала оплатить просроченное — потом снова на тариф за ПК. */
  OverdueInvoices: 'plan_overdue_invoices',
  NothingToPromise: 'plan_nothing_to_promise',
  PromiseUsed: 'plan_promise_used',
  /** Платформа выключила пробный период (ноль дней в условиях оплаты). */
  TrialUnavailable: 'plan_trial_unavailable',
  /** Платформа выключила обещанный платёж (ноль дней в условиях оплаты). */
  PromiseUnavailable: 'plan_promise_unavailable',
  AlreadyOnPlan: 'plan_already_on_plan',
  /** Отмечено больше ПК, чем разрешает тариф. */
  TooManyDevices: 'plan_devices_too_many',
  /** В списке не игровой ПК клуба или неподтверждённый. */
  UnknownDevice: 'plan_device_unknown',
} as const;
export type ClubPlanErrorCodeName = (typeof ClubPlanErrorCodeNames)[keyof typeof ClubPlanErrorCodeNames];

/** Словарь: Platform/Billing/ClubPlanContracts.cs */
export const ClubPlanKindNames = {
  Free: 'free',
  PerPc: 'per_pc',
  Trial: 'trial',
  /** Прежняя сетка тарифов: условия у поддержки, цену экран не показывает. */
  Legacy: 'legacy',
} as const;
export type ClubPlanKindName = (typeof ClubPlanKindNames)[keyof typeof ClubPlanKindNames];

/** Словарь: Consoles/ConsoleSeatContracts.cs */
export const ConsoleSeatErrorCodeNames = {
  /** На месте уже стоит ПК или консоль. */
  SeatTaken: 'console_seat_taken',
  SeatNotFound: 'console_seat_not_found',
} as const;
export type ConsoleSeatErrorCodeName = (typeof ConsoleSeatErrorCodeNames)[keyof typeof ConsoleSeatErrorCodeNames];

/**
 * Почему сервер не принял команду администратора.
 *
 * Словарь: Devices/DeviceCommandErrorCodeNames.cs
 */
export const DeviceCommandErrorCodeNames = {
  /** Такой команды нет — раньше сервер принимал любую строку, и агент отвечал «не умею». */
  UnknownType: 'unknown_command_type',
  /** На ПК идёт сессия: перезагружать, выключать и уводить в обслуживание нельзя. */
  ActiveSession: 'device_has_active_session',
  InvalidPayload: 'invalid_command_payload',
  /** Разбудить нельзя: ПК ещё ни разу не сообщил свой сетевой адрес. */
  WakeTargetUnknown: 'wake_target_unknown',
  /** Разбудить некому: в подсети этого ПК нет ни одного включённого соседа. */
  NoWakeHelper: 'no_wake_helper',
} as const;
export type DeviceCommandErrorCodeName = (typeof DeviceCommandErrorCodeNames)[keyof typeof DeviceCommandErrorCodeNames];

/**
 * Чем закончилась команда на устройстве — машинным именем, а не фразой.
 * Журнал команд читает администратор клуба на своём языке. Агент до этого присылал только
 * человеческую строку и присылал её по-английски («Workstation locked (nothing)»), и она
 * доезжала до экрана как есть. Имя исхода переводится на стороне клиента — тот же порядок, что
 * у кодов ошибок API: сервер отдаёт код, клиент решает, какими словами о нём сказать.
 * Строку-сообщение это не отменяет: она остаётся деталью для инженера.
 *
 * Словарь: Devices/DeviceCommandOutcomeNames.cs
 */
export const DeviceCommandOutcomeNames = {
  /** Команда принята, отдельного исхода у неё нет. */
  Accepted: 'accepted',
  /** Аренда принята, место открыто гостю. */
  LeaseAccepted: 'lease-accepted',
  /** Аренда продлена: сессия продолжается. */
  LeaseRefreshed: 'lease-refreshed',
  /** Машина заперта. */
  WorkstationLocked: 'workstation-locked',
  /**
   * Агент не смог применить машинные политики: на самой машине ничего не изменилось. Раньше
   * такой исход сообщался как обычный успех — оператор видел «заблокировано» там, где
   * диспетчер задач остался доступен.
   */
  MachinePoliciesUnavailable: 'machine-policies-unavailable',
  /** Предупреждение показано на экране игрока. */
  WarningShown: 'warning-shown',
  /** Причина предупреждения агенту неизвестна — игрок ничего не увидел. */
  WarningReasonUnknown: 'warning-reason-unknown',
  /** Такой тип команды этот агент не исполняет. */
  CommandNotImplemented: 'command-not-implemented',
  /**
   * Исполнение сорвалось: диск, реестр, права. Агент обязан ответить и в этом случае — молчание
   * оператор читает как «команда где-то в пути», и ждать он будет бесконечно.
   */
  CommandExecutionFailed: 'command-execution-failed',
  /** В команде не было аренды сессии. */
  LeaseMissing: 'lease-missing',
  /** Аренду в команде не удалось прочитать. */
  LeaseUnreadable: 'lease-unreadable',
  /** Аренда не прошла проверку подписи или срока. */
  LeaseInvalid: 'lease-invalid',
  /** Windows перезагрузит ПК через десять секунд: ответ ушёл раньше. */
  RebootScheduled: 'reboot-scheduled',
  /** Windows выключит ПК через десять секунд. */
  ShutdownScheduled: 'shutdown-scheduled',
  /** На ПК идёт сессия: чужую игру агент не выключает и в обслуживание не уводит. */
  SessionInProgress: 'session-in-progress',
  /** Сосед отправил волшебный пакет. Проснулся ли ПК, скажет его сердцебиение. */
  WakePacketSent: 'wake-packet-sent',
  /** MAC или широковещательный адрес не годятся — или сосед уже в другой подсети. */
  WakeTargetInvalid: 'wake-target-invalid',
  MaintenanceStarted: 'maintenance-started',
  MaintenanceEnded: 'maintenance-ended',
  /** Выход игрока или сообщение переданы на экран ПК. */
  DeliveredToShell: 'delivered-to-shell',
  /** Экран игрока не запущен или не отвечает — передать некому. */
  ShellNotConnected: 'shell-not-connected',
  /** Профиля защиты у ПК пока нет — обновлять нечего. */
  NothingToRefresh: 'nothing-to-refresh',
  /** Профиль защиты перечитан и применён; что вышло по пунктам — в отчёте ПК. */
  ProtectionApplied: 'protection-applied',
  /** Профиль не удалось получить с сервера — ПК остаётся на прежнем. */
  ProtectionUnavailable: 'protection-unavailable',
} as const;
export type DeviceCommandOutcomeName = (typeof DeviceCommandOutcomeNames)[keyof typeof DeviceCommandOutcomeNames];

/**
 * Где команда: ждёт, отдана агенту, устарела или агент уже ответил.
 *
 * Словарь: Devices/DeviceCommandStatusNames.cs
 */
export const DeviceCommandStatusNames = {
  Pending: 'Pending',
  /**
   * Отдана агенту и больше не отдаётся — у неповторяемых команд (перезагрузка, выключение,
   * пробуждение). Повторная выдача той же перезагрузки после перезапуска агента была бы петлёй.
   */
  Delivered: 'Delivered',
  /**
   * Неповторяемая команда пролежала дольше срока и не отдана: перезагрузка, пришедшая через три
   * дня после просьбы, хуже потерянной.
   */
  Expired: 'Expired',
  Accepted: 'Accepted',
  Rejected: 'Rejected',
  Failed: 'Failed',
  Completed: 'Completed',
} as const;
export type DeviceCommandStatusName = (typeof DeviceCommandStatusNames)[keyof typeof DeviceCommandStatusNames];

/** Словарь: Devices/DeviceCommandTypeNames.cs */
export const DeviceCommandTypeNames = {
  Lock: 'lock',
  Unlock: 'unlock',
  RefreshSessionLease: 'refresh-session-lease',
  /**
   * A non-blocking warning overlay pushed to the shell (e.g. fixed time almost
   * up, or an open tab approaching its credit limit).
   */
  Warn: 'warn',
  /** Перезагрузить ПК. Только без живой сессии; отдаётся агенту один раз. */
  Reboot: 'reboot',
  /** Выключить ПК. Только без живой сессии; отдаётся агенту один раз. */
  Shutdown: 'shutdown',
  /**
   * «Разбудить этот ПК» — так просит администратор, называя спящую машину. Выключенный ПК
   * команду не получит, поэтому сервер передаёт её соседу по подсети как WakeNeighbor.
   */
  Wake: 'wake',
  /**
   * Агенту: отправь волшебный пакет (6×FF + 16×MAC, UDP 9) в свою подсеть. В теле — mac,
   * broadcast и targetDeviceId. Администратор эту команду не шлёт — её собирает сервер из
   * Wake.
   */
  WakeNeighbor: 'wake-neighbor',
  /** Хост выходит из аккаунта игрока; сессия, если идёт, продолжается. */
  SignOut: 'sign-out',
  /** Сообщение игроку: окно поверх игры или полоса на экране блокировки. В теле — text. */
  Message: 'message',
  /** Режим обслуживания: игрокам вход закрыт. Право organization.devices.maintenance. */
  MaintenanceOn: 'maintenance-on',
  /** Вернуть ПК в зал из обслуживания. */
  MaintenanceOff: 'maintenance-off',
  /** Перечитать профиль защиты. */
  PolicyRefresh: 'policy-refresh',
} as const;
export type DeviceCommandTypeName = (typeof DeviceCommandTypeNames)[keyof typeof DeviceCommandTypeNames];

/** Словарь: Install/DeviceEnrollmentStateNames.cs */
export const DeviceEnrollmentStateNames = {
  Approved: 'approved',
  Pending: 'pending',
  Rejected: 'rejected',
  Removed: 'removed',
} as const;
export type DeviceEnrollmentStateName = (typeof DeviceEnrollmentStateNames)[keyof typeof DeviceEnrollmentStateNames];

/** Словарь: Devices/DevicePlayerSignInContracts.cs */
export const DevicePlayerSignInErrorCodeNames = {
  /**
   * Номер или ПИН-код не подошли. Причина не уточняется: «нет такого номера» — это ответ на
   * вопрос, кто в этой сети играет.
   */
  SignInRefused: 'sign_in_refused',
  /** С этого ПК слишком много неудачных попыток; ответ несёт, когда можно снова. */
  TooManyAttempts: 'too_many_attempts',
  /** На ПК идёт чужая сессия: вход верный, но открыть вошедшему нечего. */
  SessionNotYours: 'session_not_yours',
  /** Клуб закрыл этот ПК на обслуживание: входить на нём некуда. */
  DeviceInMaintenance: 'device_in_maintenance',
} as const;
export type DevicePlayerSignInErrorCodeName = (typeof DevicePlayerSignInErrorCodeNames)[keyof typeof DevicePlayerSignInErrorCodeNames];

/** Словарь: Install/DeviceRoleNames.cs */
export const DeviceRoleNames = {
  GamingPc: 'gaming_pc',
  ManagerWorkstation: 'manager_workstation',
  /**
   * Консоль на месте — без агента: сессию ведёт администратор, ПК не отпирается и не запирается,
   * сердцебиения нет. Запись устройства нужна, чтобы у сессии, кассы и отчётов было то же место,
   * что у ПК.
   */
  Console: 'console',
} as const;
export type DeviceRoleName = (typeof DeviceRoleNames)[keyof typeof DeviceRoleNames];

/** Словарь: Devices/DeviceShellContextContracts.cs */
export const DeviceSessionOwnerKindNames = {
  /** Живой сессии на ПК нет. */
  None: 'none',
  /** Сессия без счёта игрока — посадили у стойки. */
  Guest: 'guest',
  /** Сессия на счёте игрока. */
  Player: 'player',
} as const;
export type DeviceSessionOwnerKindName = (typeof DeviceSessionOwnerKindNames)[keyof typeof DeviceSessionOwnerKindNames];

/**
 * Что с дружбой прямо сейчас.
 *
 * Словарь: Friends/FriendDtos.cs
 */
export const FriendshipStateNames = {
  /** Позвали, ответа ещё нет. */
  Pending: 'pending',
  Accepted: 'accepted',
  /** Отказали. Строка остаётся, чтобы человека не звали второй раз. */
  Declined: 'declined',
} as const;
export type FriendshipStateName = (typeof FriendshipStateNames)[keyof typeof FriendshipStateNames];

/**
 * Чем запускается игра (спека оболочки, §6.6). Путь к лаунчеру на каждом ПК свой — его находит
 * агент; сервер хранит только что запускать.
 *
 * Словарь: Games/GameLibraryContracts.cs
 */
export const GameLaunchKindNames = {
  /** Через Steam по AppID: `steam.exe -applaunch 730`. */
  Steam: 'steam',
  /** Через Epic Games Launcher по имени приложения: `Fortnite`. */
  Epic: 'epic',
  /** Через Riot Client по продукту: `league_of_legends`, `valorant`. */
  Riot: 'riot',
  /** Через Battle.net по коду игры: `WoW`, `Pro`. */
  BattleNet: 'battlenet',
  /** Своим exe по пути на ПК. */
  Executable: 'exe',
} as const;
export type GameLaunchKindName = (typeof GameLaunchKindNames)[keyof typeof GameLaunchKindNames];

/** Словарь: Games/GameLibraryContracts.cs */
export const GameLibraryErrorCodeNames = {
  InvalidGame: 'invalid_game',
  CatalogGameNotFound: 'catalog_game_not_found',
  LibraryFull: 'game_library_full',
} as const;
export type GameLibraryErrorCodeName = (typeof GameLibraryErrorCodeNames)[keyof typeof GameLibraryErrorCodeNames];

/** Словарь: Players/GuestImportContracts.cs */
export const GuestImportIssueNames = {
  InvalidPhone: 'invalid_phone',
  MissingName: 'missing_name',
  NegativeAmount: 'negative_amount',
  /** Тот же номер уже встречался выше в этом файле. */
  DuplicateInFile: 'duplicate_in_file',
  /** Гостю уже переносили остатки — второй перенос удвоил бы деньги. */
  AlreadyImported: 'already_imported',
} as const;
export type GuestImportIssueName = (typeof GuestImportIssueNames)[keyof typeof GuestImportIssueNames];

/** Словарь: Devices/DeviceHardwareContracts.cs */
export const HardwareComponentNames = {
  Cpu: 'cpu',
  Memory: 'memory',
  Gpu: 'gpu',
  Motherboard: 'motherboard',
  Disk: 'disk',
} as const;
export type HardwareComponentName = (typeof HardwareComponentNames)[keyof typeof HardwareComponentNames];

/**
 * Машинные имена отказов установки. Нужны затем, что мастер установки говорит на трёх языках, а
 * текст отказа с сервера — всегда английский: показать его человеку у ПК нельзя, а назвать
 * причину своими словами по коду — можно.
 *
 * Словарь: Install/InstallErrorCodeNames.cs
 */
export const InstallErrorCodeNames = {
  /** На выбранное место уже привязан другой ПК. */
  SeatOccupied: 'seat_occupied',
  /**
   * Код установки не подходит: неизвестен, истёк, отозван или исчерпан. Одна причина на все
   * четыре: угадывающему код не надо подсказывать, какой из них был почти верным.
   */
  InstallCodeInvalid: 'install_code_invalid',
} as const;
export type InstallErrorCodeName = (typeof InstallErrorCodeNames)[keyof typeof InstallErrorCodeNames];

/** Словарь: Platform/Billing/InvoiceKindNames.cs */
export const InvoiceKindNames = {
  Subscription: 'subscription',
  Proration: 'proration',
  /** Manually issued charge outside the subscription: setup, hardware, extra service. */
  OneOff: 'one_off',
  /** Money owed back to the club. Carries a negative amount so the balance is arithmetic. */
  Credit: 'credit',
} as const;
export type InvoiceKindName = (typeof InvoiceKindNames)[keyof typeof InvoiceKindNames];

/** Словарь: Platform/Billing/InvoiceStatusNames.cs */
export const InvoiceStatusNames = {
  Issued: 'issued',
  Paid: 'paid',
  Void: 'void',
  Overdue: 'overdue',
} as const;
export type InvoiceStatusName = (typeof InvoiceStatusNames)[keyof typeof InvoiceStatusNames];

/** Словарь: Billing/LedgerAccountTypeNames.cs */
export const LedgerAccountTypeNames = {
  Wallet: 'wallet',
  Debt: 'debt',
  PackageTime: 'package_time',
  BonusTime: 'bonus_time',
} as const;
export type LedgerAccountTypeName = (typeof LedgerAccountTypeNames)[keyof typeof LedgerAccountTypeNames];

/** Словарь: Billing/LedgerEntryTypeNames.cs */
export const LedgerEntryTypeNames = {
  TopUp: 'top_up',
  GameplayCharge: 'gameplay_charge',
  PackagePurchase: 'package_purchase',
  PackageConsumption: 'package_consumption',
  BonusGrant: 'bonus_grant',
  BonusConsumption: 'bonus_consumption',
  Refund: 'refund',
  ManualCorrection: 'manual_correction',
  PostpaidDebt: 'postpaid_debt',
  DebtPayment: 'debt_payment',
  WalletPayment: 'wallet_payment',
  Reversal: 'reversal',
  Cashback: 'cashback',
  /** Деньги за приведённого друга — платит клуб, обоим сразу. */
  ReferralBonus: 'referral_bonus',
  /**
   * Заморозка под бронь: деньги остаются игроку, но потратить их второй раз уже нельзя.
   * Оплатой не становится никогда — только снимается реверсом.
   */
  ReservationHold: 'reservation_hold',
  /**
   * Удержанная за неявку предоплата — выручка клуба, а не «деньги, которые не вернулись».
   * Пишется только если филиал так решил, и всегда после снятия заморозки.
   */
  ReservationNoShowFee: 'reservation_no_show_fee',
  /** Взнос за участие в событии клуба — списывается при записи. */
  TournamentEntryFee: 'tournament_entry_fee',
  /**
   * Возврат взноса: игрок снялся до начала или клуб отменил событие. Отдельно от общего
   * возврата, чтобы в выписке было видно, за что деньги вернулись.
   */
  TournamentEntryRefund: 'tournament_entry_refund',
  /** Чаевые администратору смены с кошелька игрока. Не выручка клуба: клуб их должен сотруднику. */
  Tip: 'tip',
  /**
   * Начальный остаток из прежней программы клуба: деньги гость заплатил туда, клуб берёт долг на
   * себя. Не выручка и не наличные смены.
   */
  OpeningBalance: 'opening_balance',
} as const;
export type LedgerEntryTypeName = (typeof LedgerEntryTypeNames)[keyof typeof LedgerEntryTypeNames];

/** Словарь: Media/MediaPurposeNames.cs */
export const MediaPurposeNames = {
  BranchLogo: 'branch-logo',
  /**
   * Логотип клуба целиком: его показывают приложение игрока, витрина и экран игрового ПК.
   * Отдельно от логотипа зала — у сети он один, а залов много.
   */
  OrganizationLogo: 'organization-logo',
  /** Фото зала для витрины клуба в приложении игрока. */
  BranchCover: 'branch-cover',
  /** Остальные фото зала: их несколько, и новая загрузка не заменяет прежние. */
  BranchGallery: 'branch-gallery',
  /** Картинка новости: её показывают приложение игрока и витрина свободного ПК. */
  NewsImage: 'news-image',
  /** Фото товара бара — для витрины ПК и меню бара. */
  ProductImage: 'product-image',
} as const;
export type MediaPurposeName = (typeof MediaPurposeNames)[keyof typeof MediaPurposeNames];

/**
 * Машинные имена отказов при подключении админки клуба к клубу.
 * Этот экран человек видит раньше всего остального — до входа, до языка интерфейса он уже
 * выбран. Английская фраза сервера («OrganizationSlug must contain only lowercase letters…»)
 * доезжала до него дословно: непонятно и похоже на поломку программы.
 *
 * Словарь: Platform/Operator/OperatorConnectionErrorCodeNames.cs
 */
export const OperatorConnectionErrorCodeNames = {
  /** Не указано ни адреса клуба и филиала, ни кода подключения. */
  InputMissing: 'connection_input_missing',
  /** Указано и то и другое сразу. */
  InputAmbiguous: 'connection_input_ambiguous',
  /** Адрес клуба или филиала записан не по формату. */
  SlugInvalid: 'connection_slug_invalid',
  /** Код подключения пустой. */
  SetupCodeRequired: 'setup_code_required',
  /** Кодом подключения уже воспользовались или его отозвали. */
  SetupCodeNotUsable: 'setup_code_not_usable',
} as const;
export type OperatorConnectionErrorCodeName = (typeof OperatorConnectionErrorCodeNames)[keyof typeof OperatorConnectionErrorCodeNames];

/** Словарь: Identity/AccountActivation/OrganizationOwnerInviteStatusNames.cs */
export const OrganizationOwnerInviteStatusNames = {
  Pending: 'pending',
  Accepted: 'accepted',
  Revoked: 'revoked',
  Expired: 'expired',
} as const;
export type OrganizationOwnerInviteStatusName = (typeof OrganizationOwnerInviteStatusNames)[keyof typeof OrganizationOwnerInviteStatusNames];

/** Словарь: Identity/OrganizationPermissionNames.cs */
export const OrganizationPermissionNames = {
  CreateDeviceEnrollmentCode: 'organization.devices.enrollment_codes.create',
  DispatchDeviceCommand: 'organization.devices.commands.dispatch',
  /**
   * Увести ПК в обслуживание и вернуть в зал. Отдельно от прочих команд: обслуживание закрывает
   * машину для игроков, и решать это — не каждому, кто может её перезапереть.
   */
  MaintainDevice: 'organization.devices.maintenance',
  ViewDeviceCommandStatus: 'organization.devices.commands.status.view',
  RotateDeviceCredential: 'organization.devices.credentials.rotate',
  RevokeDeviceCredential: 'organization.devices.credentials.revoke',
  AssignDeviceSeat: 'organization.devices.seat_assignment.assign',
  ViewDeviceDetail: 'organization.devices.detail.view',
  InstallDevice: 'organization.devices.install',
  ViewFloorMap: 'organization.floor_map.view',
  ManageLayout: 'organization.layout.manage',
  StartSession: 'organization.sessions.start',
  ExtendSession: 'organization.sessions.extend',
  /** Поставить сессию на паузу и снять её. Право того же круга, что продление: обе правят время. */
  PauseSession: 'organization.sessions.pause',
  TransferSession: 'organization.sessions.transfer',
  EndSession: 'organization.sessions.end',
  ViewSession: 'organization.sessions.view',
  CreatePlayerAccount: 'organization.players.create',
  ViewPlayers: 'organization.players.view',
  ViewBilling: 'organization.billing.view',
  TopUpWallet: 'organization.billing.wallet.top_up',
  RefundLedgerEntry: 'organization.billing.refund',
  ManualLedgerCorrection: 'organization.billing.manual_correction',
  PayDebt: 'organization.billing.debt.pay',
  /** Anti-fraud (§5.2/D2): approve an over-threshold high-risk money action raised by another actor. */
  ApproveMoneyAction: 'organization.billing.money_action.approve',
  ViewSubscription: 'organization.billing.subscription.view',
  /**
   * Сменить тариф клуба, начать пробный период, взять обещанный платёж. Это обязательство
   * платить — только у владельца.
   */
  ManageSubscription: 'organization.billing.subscription.manage',
  /**
   * Перенести гостей с балансами из прежней программы. Это деньги, которые клуб берёт на себя, —
   * только у владельца.
   */
  ImportPlayers: 'organization.players.import',
  ManageTariffs: 'organization.tariffs.manage',
  ViewTariffs: 'organization.tariffs.view',
  ManagePackages: 'organization.packages.manage',
  ViewPackages: 'organization.packages.view',
  PurchasePackage: 'organization.packages.purchase',
  OpenShift: 'organization.shifts.open',
  CloseShift: 'organization.shifts.close',
  /**
   * Закрыть СВОЮ смену — ту, которую сам и открыл. Узкое подмножество CloseShift.
   * Контроль от этого не слабеет: сверка кассы обязательна для всех (CountedCash), а
   * расхождение сверх допуска филиала по-прежнему требует подписи второго человека, который
   * не открывал и не закрывает смену (§5.7, EfShiftService). То есть «закрыть поверх
   * недостачи» в одиночку нельзя было и не станет можно.
   * Без этого права ночной кассир, которого гейт после входа ЗАСТАВИЛ открыть смену, не мог
   * её закрыть: в шесть утра он один, а закрывать обязан кто-то другой.
   */
  CloseOwnShift: 'organization.shifts.close_own',
  ViewShift: 'organization.shifts.view',
  ManageShiftCash: 'organization.shifts.cash.manage',
  ViewReports: 'organization.reports.view',
  ViewReservations: 'organization.reservations.view',
  ManageReservations: 'organization.reservations.manage',
  ManagePosCatalog: 'organization.pos.catalog.manage',
  CreatePosSale: 'organization.pos.sales.create',
  PayPosSale: 'organization.pos.sales.pay',
  RefundPosSale: 'organization.pos.sales.refund',
  VoidPosSale: 'organization.pos.sales.void',
  /**
   * Отменить СВОЙ чек, пробитый только что, без старшего — узкое подмножество VoidPosSale.
   * Границы правила и довод за него живут в `Pos/PosSelfVoidPolicy.cs`: право само по
   * себе ничего не разрешает, пока продажа не своя, не в текущей открытой смене и не свежая.
   */
  VoidOwnRecentPosSale: 'organization.pos.sales.void_own_recent',
  ManageInventoryStock: 'organization.inventory.stock.manage',
  ViewInventory: 'organization.inventory.view',
  ViewReceipt: 'organization.receipts.view',
  ViewUpdateStatus: 'organization.updates.status.view',
  ViewDiagnostics: 'organization.diagnostics.view',
  ManageBranchStaff: 'organization.identity.branch_staff.manage',
  ManageRoles: 'organization.identity.roles.manage',
  ViewAudit: 'organization.audit.view',
  /** Owner-only: read org-wide audit across all branches + org-level records. */
  ViewOrganizationAudit: 'organization.audit.organization.view',
  ManageBranchSettings: 'organization.branches.settings.manage',
  /** Owner-only: view the org-wide branch roster (network overview). */
  ViewBranches: 'organization.branches.view',
  /** Owner-only: connect/manage the club's DC-Bank payment cards (dcgate gateways). */
  ManagePaymentGateways: 'organization.payments.gateways.manage',
  /**
   * Очередь заказов бара разведена на два права по одной границе: двигаются ли деньги.
   * Serve — увидеть очередь, принять заказ, выдать его. Это чистая смена статуса, ничего не
   * списывается и не возвращается: заказ оплачен в момент оформления. Выдаёт еду кассир, ему
   * это право и нужно.
   * Manage — отменить заказ, а отмена идёт через денежный координатор и возвращает деньги.
   * Это денежное действие и остаётся за тем же кругом, что возвраты в кассе.
   */
  ServeShopOrders: 'organization.shop.orders.serve',
  ManageShopOrders: 'organization.shop.orders.manage',
  /**
   * Снять с места вызов оператора. Право того же круга, что и «отдать заказ»: зовут человека
   * с зала, а не того, кто правит настройки.
   */
  ResolveAssistanceRequest: 'organization.assistance.resolve',
  /** Owner-only: configure org-wide loyalty/cashback rates. */
  ManageLoyaltySettings: 'organization.loyalty.settings.manage',
  ManageNews: 'organization.news.manage',
  /**
   * Заводить и отменять события клуба. Отдельно от новостей: событие возвращает деньги
   * при отмене, и это право сильнее права написать объявление.
   */
  ManageTournaments: 'organization.tournaments.manage',
  /**
   * Библиотека игр филиала — что игрок запустит на ПК (спека оболочки, §6.6). У того, кто
   * ставит ПК и игры: владелец, управляющий, техник.
   */
  ManageGameLibrary: 'organization.games.manage',
  /**
   * Читать отзывы игроков о филиале. Отзыв бывает и о смене — поэтому у владельца и
   * управляющего, а не у всей стойки.
   */
  ViewReviews: 'organization.reviews.view',
  /**
   * Принять новое железо ПК как норму — после апгрейда или ремонта. У того, кто его меняет:
   * владелец, управляющий, техник.
   */
  AcceptDeviceHardware: 'organization.devices.hardware.accept',
  /**
   * Чаевые администратору с экрана ПК: включить у клуба и вернуть игроку, пока смена открыта.
   * Это движение денег, поэтому у владельца и управляющего, а не у стойки.
   */
  ManageTips: 'organization.tips.manage',
} as const;
export type OrganizationPermissionName = (typeof OrganizationPermissionNames)[keyof typeof OrganizationPermissionNames];

/** Словарь: Platform/Organizations/OrganizationPlanCodeNames.cs */
export const OrganizationPlanCodeNames = {
  /** Бесплатно: до 10 ПК, 1 зал, 3 сотрудника, с рекламой платформы. */
  Free: 'free',
  /** 10 сомони в месяц за каждый ПК сверх десяти; без лимитов и рекламы. */
  PerPc: 'per_pc',
  /** Прежняя сетка — снята с продажи (спека тарифов клуба, §2); клубы на ней остаются. */
  Starter: 'starter',
  Growth: 'growth',
  Scale: 'scale',
} as const;
export type OrganizationPlanCodeName = (typeof OrganizationPlanCodeNames)[keyof typeof OrganizationPlanCodeNames];

/** Словарь: Platform/Organizations/OrganizationStatusNames.cs */
export const OrganizationStatusNames = {
  Active: 'active',
  Suspended: 'suspended',
  DeletionPending: 'deletion_pending',
  /**
   * Клуб ушёл и его данные стёрты. Терминальный статус: архивная строка нужна отчётности по
   * деньгам, но отличать её от живой заявки на уход обязательно — иначе стёртый клуб выглядит
   * как ещё живая заявка.
   */
  Purged: 'purged',
} as const;
export type OrganizationStatusName = (typeof OrganizationStatusNames)[keyof typeof OrganizationStatusNames];

/** Словарь: Payments/PaymentMethodNames.cs */
export const PaymentMethodNames = {
  Cash: 'cash',
  CardManual: 'card_manual',
  Wallet: 'wallet',
} as const;
export type PaymentMethodName = (typeof PaymentMethodNames)[keyof typeof PaymentMethodNames];

/**
 * Машинные имена лимитов тарифа и код отказа. Фразу для человека собирает клиент —
 * сервер отдаёт только код и числа.
 *
 * Словарь: Platform/Organizations/PlanLimitNames.cs
 */
export const PlanLimitNames = {
  ReachedCode: 'plan_limit_reached',
  /**
   * Отказ запустить сессию на ПК «вне тарифа» — сверх предела ПК бесплатного тарифа (спека
   * тарифов клуба, §5a). Числа — в PlanLimitExceededDto с пределом Devices.
   */
  DeviceOutsidePlanCode: 'device_outside_plan',
  Branches: 'branches',
  DevicesPerBranch: 'devices_per_branch',
  /** Игровые ПК на весь клуб, без деления по залам. */
  Devices: 'devices',
  ConcurrentSessions: 'concurrent_sessions',
  StaffUsersPerBranch: 'staff_users_per_branch',
} as const;
export type PlanLimitName = (typeof PlanLimitNames)[keyof typeof PlanLimitNames];

/** Словарь: Platform/Auth/PlatformAdminPermissionNames.cs */
export const PlatformAdminPermissionNames = {
  UseSupportAccess: 'platform.support.access',
  ViewOrganizations: 'platform.organizations.view',
  CreateOrganization: 'platform.organizations.create',
  UpdateOrganizationStatus: 'platform.organizations.status.update',
  UpdateOrganizationLimits: 'platform.organizations.limits.update',
  UpdateOrganizationProfile: 'platform.organizations.profile.update',
  UpdateOrganizationUpdateChannel: 'platform.organizations.update_channel.update',
  ViewOrganizationSupportNotes: 'platform.organizations.support_notes.view',
  ManageOrganizationSupportNotes: 'platform.organizations.support_notes.manage',
  ManageOrganizationOwnerInvites: 'platform.organizations.owner_invites.manage',
  TransferOrganizationOwner: 'platform.organizations.owner.transfer',
  ViewOrganizationHealth: 'platform.organizations.health.view',
  ViewPlatformAudit: 'platform.audit.view',
  ViewBilling: 'platform.billing.view',
  ManagePlans: 'platform.billing.plans.manage',
  ManageSubscriptions: 'platform.billing.subscriptions.manage',
  ManageInvoices: 'platform.billing.invoices.manage',
  ViewUpdates: 'platform.updates.view',
  ManageUpdatePackages: 'platform.updates.packages.manage',
  ManageUpdateRollouts: 'platform.updates.rollouts.manage',
  ManagePlatformAdmins: 'platform.admins.manage',
  ViewPlatformHealth: 'platform.health.view',
  /**
   * Отправка проверочного письма. Отдельно от просмотра здоровья: это действие наружу,
   * а не чтение.
   */
  SendTestNotification: 'platform.health.test_email.send',
  ManageOrganizationFeatures: 'platform.organizations.features.manage',
  /**
   * Ведение анонсов платформы. Отдельного «смотреть анонсы» нет: как и у ролей, кто их ведёт,
   * тот их и читает — лишнее право усложнило бы модель, ничего не добавив.
   */
  ManageAnnouncements: 'platform.announcements.manage',
  /** Каталог игр, из которого клубы собирают библиотеку ПК (спека оболочки, §6.6). */
  ManageGameCatalog: 'platform.games.manage',
  /**
   * Реклама платформы в витрине ПК: рекламодатели, кампании, модерация креативов, отчёт
   * показов. Модерация — внутри этого же права: команда платформы маленькая.
   */
  ManageAds: 'platform.ads.manage',
  /**
   * Уход клуба: выгрузка его данных и стирание. Отдельно от правки лимитов и статуса — это
   * вынос персональных данных наружу и необратимое удаление, а не настройка. Одалживать чужое
   * право здесь значит раздать необратимое тем, кому дали настраивать.
   */
  ManageOffboarding: 'platform.organizations.offboarding.manage',
  /**
   * Сетевой запрет человеку: закрыть или открыть ему самообслуживание во всей сети. Право
   * платформы и только её — клуб решает за свой клуб, закрывая у себя карточку, и дальше его
   * решения не идут. Поддержке не даётся по той же причине, по которой ей не даётся репутация.
   */
  ManageNetworkBans: 'platform.people.network_ban.manage',
} as const;
export type PlatformAdminPermissionName = (typeof PlatformAdminPermissionNames)[keyof typeof PlatformAdminPermissionNames];

/** Словарь: Platform/Auth/PlatformAdminRoleNames.cs */
export const PlatformAdminRoleNames = {
  PlatformAdmin: 'platform_admin',
  PlatformSupport: 'platform_support',
} as const;
export type PlatformAdminRoleName = (typeof PlatformAdminRoleNames)[keyof typeof PlatformAdminRoleNames];

/**
 * Машинные имена отказов платформенного контура.
 * Причина та же, по которой они появились у офбординга (<see
 * cref="Organizations.OffboardingErrorCodes"/>): текст отказа сервер пишет по-английски и
 * именами своих полей («CurrentPeriodEndUtc must be later than…»), а панель работает на трёх
 * языках. Показать такой текст нельзя, и без кода панель показывала одно «не удалось сохранить
 * изменения» на десяток разных причин — в форме подписки из семи полей человек не мог понять,
 * какое из них поправить.
 * Здесь только те причины, которые человек за панелью исправляет сам. Остальное остаётся без
 * кода и честно называется общими словами.
 *
 * Словарь: Platform/Billing/PlatformErrorCodeNames.cs
 */
export const PlatformErrorCodeNames = {
  /** Счёт за текущий период уже выставлен. */
  InvoicePeriodAlreadyBilled: 'invoice_period_already_billed',
  /** Счёт уже оплачен. */
  InvoiceAlreadyPaid: 'invoice_already_paid',
  /** Счёт уже аннулирован. */
  InvoiceAlreadyVoid: 'invoice_already_void',
  /** Оплаченный счёт не аннулируют — на него выписывают кредит-ноту. */
  PaidInvoiceCannotBeVoided: 'paid_invoice_cannot_be_voided',
  /** Кредит-ноту не оплачивают: она учитывается в балансе. */
  CreditNoteNotPayable: 'credit_note_not_payable',
  /** Номер счёта занял параллельный запрос — можно повторить. */
  InvoiceNumberingConflict: 'invoice_numbering_conflict',
  /** Конец оплаченного периода не позже его начала. */
  SubscriptionPeriodEndNotAfterStart: 'subscription_period_end_not_after_start',
  /** Отсрочку ставят на будущее: прошедшая дата ничего не отсрочит. */
  SubscriptionGraceNotInFuture: 'subscription_grace_not_in_future',
  /** Перевод на пробный период без даты его окончания. */
  SubscriptionTrialNeedsPeriodEnd: 'subscription_trial_needs_period_end',
  /** Выбранного тарифа нет в каталоге. */
  SubscriptionPlanNotFound: 'subscription_plan_not_found',
  /** Такой адрес организации уже занят другим клубом. */
  OrganizationSlugTaken: 'organization_slug_taken',
  /** Такой адрес филиала уже занят в этой организации. */
  BranchSlugTaken: 'branch_slug_taken',
  /** Логин владельца уже занят в этой организации. */
  OwnerUserNameTaken: 'owner_username_taken',
} as const;
export type PlatformErrorCodeName = (typeof PlatformErrorCodeNames)[keyof typeof PlatformErrorCodeNames];

/**
 * Ключи фич, которые платформа умеет включать и выключать клубу. Каждый ключ обязан иметь
 * точку проверки в коде: флаг без потребителя — мусор, который невозможно опознать через месяц.
 *
 * Словарь: Platform/Features/PlatformFeatureNames.cs
 */
export const PlatformFeatureNames = {
  /** Код отказа, когда фича выключена. Фразу собирает клиент. */
  DisabledCode: 'feature_disabled',
  OnlineBooking: 'online_booking',
  Loyalty: 'loyalty',
  OnlineTopUp: 'online_topup',
  PlayerShop: 'player_shop',
  Tournaments: 'tournaments',
  /** Реклама платформы в витрине свободного ПК. Её включает бесплатный тариф. */
  PlatformAds: 'platform_ads',
} as const;
export type PlatformFeatureName = (typeof PlatformFeatureNames)[keyof typeof PlatformFeatureNames];

/** Словарь: Platform/Health/PlatformHealthContracts.cs */
export const PlatformQueueNames = {
  Notifications: 'notifications',
  BillingOutbox: 'billing_outbox',
} as const;
export type PlatformQueueName = (typeof PlatformQueueNames)[keyof typeof PlatformQueueNames];

/** Словарь: Platform/Updates/PlatformUpdateContracts.cs */
export const PlatformUpdateTargetKindNames = {
  Organization: 'organization',
  Branch: 'branch',
  Device: 'device',
} as const;
export type PlatformUpdateTargetKindName = (typeof PlatformUpdateTargetKindNames)[keyof typeof PlatformUpdateTargetKindNames];

/** Словарь: Players/PlayerOfferContracts.cs */
export const PlayerOfferUnavailableReasonNames = {
  /** Сессия начата по пакету: её продлевают новым стартом по пакету, а не деньгами. */
  PackageSession: 'package_session',
  /** Сессия не предоплаченная — у стойки или открытым счётом; продлевает администратор. */
  NotPrepaid: 'not_prepaid',
} as const;
export type PlayerOfferUnavailableReasonName = (typeof PlayerOfferUnavailableReasonNames)[keyof typeof PlayerOfferUnavailableReasonNames];

/** Словарь: Shell/PlayerShellStateNames.cs */
export const PlayerShellStateNames = {
  Locked: 'locked',
  Active: 'active',
  Grace: 'grace',
  Ending: 'ending',
  Maintenance: 'maintenance',
  Offline: 'offline',
  Error: 'error',
} as const;
export type PlayerShellStateName = (typeof PlayerShellStateNames)[keyof typeof PlayerShellStateNames];

/** Словарь: Devices/PlayerSignInClaimDeviceContracts.cs */
export const PlayerSignInClaimErrorCodeNames = {
  /** Заявки нет или она для другого ПК. */
  NotFound: 'claim_not_found',
  /** ПК не успел забрать заявку за отведённое время. */
  Expired: 'claim_expired',
  /** Заявку уже забрали: одна заявка — один вход. */
  AlreadyRedeemed: 'claim_already_redeemed',
  /** Клуб закрыл этот ПК на обслуживание, пока заявка ждала. */
  DeviceInMaintenance: 'device_in_maintenance',
} as const;
export type PlayerSignInClaimErrorCodeName = (typeof PlayerSignInClaimErrorCodeNames)[keyof typeof PlayerSignInClaimErrorCodeNames];

/** Словарь: Players/PlayerSignInClaimContracts.cs */
export const PlayerSignInClaimStatusNames = {
  /** ПК ещё не забрал заявку. */
  Pending: 'pending',
  /** ПК забрал заявку — человек вошёл. */
  Redeemed: 'redeemed',
  /** ПК не забрал заявку за отведённое время. */
  Expired: 'expired',
} as const;
export type PlayerSignInClaimStatusName = (typeof PlayerSignInClaimStatusNames)[keyof typeof PlayerSignInClaimStatusNames];

/** Словарь: Pos/PosSaleStateNames.cs */
export const PosSaleStateNames = {
  Draft: 'draft',
  PendingPayment: 'pending_payment',
  Paid: 'paid',
  Refunded: 'refunded',
  Voided: 'voided',
} as const;
export type PosSaleStateName = (typeof PosSaleStateNames)[keyof typeof PosSaleStateNames];

/**
 * Что именно агент запрещает на ПК — по пункту на строку отчёта.
 *
 * Словарь: Devices/ProtectionProfileContracts.cs
 */
export const ProtectionItemNames = {
  /** Постоянная основа киоска: меню Ctrl+Alt+Del без блокировки, выхода, смены пользователя и данных входа. */
  KioskBaseline: 'kiosk-baseline',
  RemovableStorage: 'removable-storage',
  BrowserDownloads: 'browser-downloads',
  BrowserIncognito: 'browser-incognito',
  BrowserUrlBlocklist: 'browser-url-blocklist',
  RunDialog: 'run-dialog',
  HiddenDrives: 'hidden-drives',
} as const;
export type ProtectionItemName = (typeof ProtectionItemNames)[keyof typeof ProtectionItemNames];

/**
 * Что получилось с пунктом. Скрытие дисков — отдельный исход: диск пропал из Проводника, но
 * программа откроет его по пути, и называть это «запрещено» было бы неправдой (§6.3).
 *
 * Словарь: Devices/ProtectionProfileContracts.cs
 */
export const ProtectionItemStatusNames = {
  Applied: 'applied',
  /** Действует только в Проводнике: это не запрет. */
  ExplorerOnly: 'explorer-only',
  Failed: 'failed',
  /** Здесь не применить: ПК не на Windows или агент без доступа к политикам машины. */
  Unsupported: 'unsupported',
  /** Снято на время обслуживания. */
  Released: 'released',
} as const;
export type ProtectionItemStatusName = (typeof ProtectionItemStatusNames)[keyof typeof ProtectionItemStatusNames];

/** Словарь: Devices/ProtectionProfileContracts.cs */
export const ProtectionProfileErrorCodeNames = {
  /** Профиль успели сохранить после того, как его открыли: нужно перечитать. */
  VersionConflict: 'protection_profile_version_conflict',
} as const;
export type ProtectionProfileErrorCodeName = (typeof ProtectionProfileErrorCodeNames)[keyof typeof ProtectionProfileErrorCodeNames];

/** Словарь: Platform/Pulse/PlatformPulseContracts.cs */
export const PulseAlertKindNames = {
  AgentSilent: 'agent_silent',
  ShiftNotClosed: 'shift_not_closed',
  PaymentOverdue: 'payment_overdue',
  RolloutFailed: 'rollout_failed',
} as const;
export type PulseAlertKindName = (typeof PulseAlertKindNames)[keyof typeof PulseAlertKindNames];

/** Словарь: Platform/Pulse/PlatformPulseContracts.cs */
export const PulseAlertLevelNames = {
  Normal: 'normal',
  Attention: 'attention',
  Critical: 'critical',
} as const;
export type PulseAlertLevelName = (typeof PulseAlertLevelNames)[keyof typeof PulseAlertLevelNames];

/**
 * How often a report schedule fires and the size of the window each run covers.
 *
 * Словарь: Reports/ReportScheduleNames.cs
 */
export const ReportScheduleFrequencyNames = {
  Daily: 'daily',
  Weekly: 'weekly',
  Monthly: 'monthly',
} as const;
export type ReportScheduleFrequencyName = (typeof ReportScheduleFrequencyNames)[keyof typeof ReportScheduleFrequencyNames];

/**
 * Машинные имена отказов по броням. См. Shifts.ShiftErrorCodeNames — та же причина:
 * стойка работает на трёх языках, а английская фраза сервера в интерфейс попадать не должна.
 * Почти все эти отказы — про состояние брони, которое успело измениться: сосед подтвердил её
 * раньше, гость уже сидит, заявку уже отклонили. Отличать их друг от друга оператору нужно
 * именно потому, что дальше он делает разное.
 *
 * Словарь: Reservations/ReservationErrorCodeNames.cs
 */
export const ReservationErrorCodeNames = {
  /** Подтвердить можно только заявку, на которую клуб ещё не ответил. */
  NotPending: 'reservation_not_pending',
  /** Менять можно бронь, которая ещё не отменена и не закрыта. */
  NotChangeable: 'reservation_not_changeable',
  /** Посадить можно только заявку или подтверждённую бронь. */
  NotSeatable: 'reservation_not_seatable',
  /** Бронь без места — сажать некуда. */
  SeatRequired: 'reservation_seat_required',
  /** Отменить можно только заявку или подтверждённую бронь. */
  NotCancellable: 'reservation_not_cancellable',
  /** Отмена без причины: её читает гость и она же уходит в журнал. */
  CancelReasonRequired: 'reservation_cancel_reason_required',
  /** Отказать можно в заявке, на которую клуб ещё не ответил. */
  NotRejectable: 'reservation_not_rejectable',
  /** Отказ «своими словами» без слов. */
  RefusalNoteRequired: 'reservation_refusal_note_required',
  /** Неизвестная причина отказа. */
  RejectReasonUnsupported: 'reservation_reject_reason_unsupported',
  /** Неявку отмечают у подтверждённой брони, время которой уже началось. */
  NoShowNotAllowed: 'reservation_no_show_not_allowed',
} as const;
export type ReservationErrorCodeName = (typeof ReservationErrorCodeNames)[keyof typeof ReservationErrorCodeNames];

/** Словарь: Reservations/ReservationSourceNames.cs */
export const ReservationSourceNames = {
  Operator: 'operator',
  Online: 'online',
} as const;
export type ReservationSourceName = (typeof ReservationSourceNames)[keyof typeof ReservationSourceNames];

/** Словарь: Reservations/ReservationStateNames.cs */
export const ReservationStateNames = {
  Pending: 'pending',
  Confirmed: 'confirmed',
  Seated: 'seated',
  Cancelled: 'cancelled',
  /**
   * Игрок не приехал. Отдельно от отмены намеренно: отменённая бронь — это решение человека
   * или клуба, а неявка — его отсутствие, и стоить она может денег. Пока оба исхода изображала
   * одна «отмена» с пометкой в свободном тексте, отличить их можно было только сравнением строк.
   */
  NoShow: 'no_show',
  /**
   * Клуб отказал в заявке — с причиной. Не отмена: игрок ничего не отменял, и в его репутации
   * чужой отказ появляться не должен.
   */
  Rejected: 'rejected',
} as const;
export type ReservationStateName = (typeof ReservationStateNames)[keyof typeof ReservationStateNames];

/**
 * The report kinds a schedule can deliver — one per existing report export endpoint.
 *
 * Словарь: Reports/ReportScheduleNames.cs
 */
export const ScheduledReportTypeNames = {
  Shifts: 'shifts',
  Sales: 'sales',
  GameplayTime: 'gameplay_time',
  CashOperations: 'cash_operations',
  OperatorActions: 'operator_actions',
} as const;
export type ScheduledReportTypeName = (typeof ScheduledReportTypeNames)[keyof typeof ScheduledReportTypeNames];

/**
 * Почему код посадки не приняли. Одни и те же у старта с телефона и у заявки на вход.
 *
 * Словарь: Players/PlayerSignInClaimContracts.cs
 */
export const SeatingCodeErrorCodeNames = {
  /**
   * Код не подошёл. Чужой клуб, истёкший код и опечатка снаружи неразличимы: иначе перебор
   * шестизначных цифр становится осмысленным.
   */
  Invalid: 'seating_code_invalid',
  /** Слишком много неверных кодов — у человека или у всего клуба; ответ несёт, когда можно снова. */
  AttemptsExceeded: 'seating_code_attempts_exceeded',
  /** Заявке нужен аккаунт AFK4, а у входа — только клубная карточка старого образца. */
  PlatformAccountRequired: 'platform_account_required',
} as const;
export type SeatingCodeErrorCodeName = (typeof SeatingCodeErrorCodeNames)[keyof typeof SeatingCodeErrorCodeNames];

/**
 * Состояние места на карте зала — то, что сервер кладёт в SeatStatusDto.State.
 * Пишутся с большой буквы, в отличие от состояний сессии: это отдельный словарь карты, а не код
 * сессии. Пока их писали литералами, Панель сравнивала сырое значение с «free» и «ready», которых
 * сервер не присылает никогда, и «Посадить за ПК» из карточки клиента не предлагало ни одного
 * места (#423).
 *
 * Словарь: FloorMap/SeatStateNames.cs
 */
export const SeatStateNames = {
  Free: 'Free',
  /** ПК на связи и заперт: гость может сесть — это то же «свободно». */
  Locked: 'Locked',
  Active: 'Active',
  Paused: 'Paused',
  Ending: 'Ending',
  /** ПК не привязан к месту или не одобрен. */
  Maintenance: 'Maintenance',
  Offline: 'Offline',
} as const;
export type SeatStateName = (typeof SeatStateNames)[keyof typeof SeatStateNames];

/**
 * С чего началась сессия. Раньше на этот вопрос отвечали догадкой по косвенным признакам —
 * «раз есть бронь, значит по брони», — и догадка врала на любом нестандартном вечере.
 * Пустая строка — законный ответ «неизвестно» для строк, заведённых до того, как вопрос начали
 * задавать. Подставлять вместо неё Operator нельзя: это уже утверждение, а не факт.
 *
 * Словарь: Sessions/SessionOriginNames.cs
 */
export const SessionOriginNames = {
  /** Посадил администратор за стойкой. */
  Operator: 'operator',
  /**
   * Игрок сел сам. Именно «сам», а не «по PIN»: самопосадка идёт и из приложения, где никакого
   * PIN человек не набирал, — имя по механизму врало бы в самом поле, заведённом ради правды.
   */
  SelfService: 'self_service',
  /** Сессия выросла из брони: человек пришёл на забронированное время. */
  Reservation: 'reservation',
} as const;
export type SessionOriginName = (typeof SessionOriginNames)[keyof typeof SessionOriginNames];

/** Словарь: Sessions/SessionStateNames.cs */
export const SessionStateNames = {
  Requested: 'requested',
  Active: 'active',
  Paused: 'paused',
  Ending: 'ending',
  Ended: 'ended',
  Failed: 'failed',
  Reconciled: 'reconciled',
} as const;
export type SessionStateName = (typeof SessionStateNames)[keyof typeof SessionStateNames];

/**
 * Следы, которые агент стирает, когда сессия кончилась и ПК заперт (спека оболочки, §6.4). Пути
 * у каждого пункта точные и зашиты в агента: из Панели приезжает только «стирать или нет», а не
 * путь — иначе профиль защиты стал бы пультом удаления любых файлов на ПК зала. Сохранения игр в
 * этих путях не лежат.
 *
 * Словарь: Devices/ProtectionProfileContracts.cs
 */
export const SessionTraceNames = {
  /** Вход в Steam: запомненные аккаунты, автовход, кэш входа, куки магазина. */
  Steam: 'steam',
  /** Профили браузеров целиком: пароли, куки, история, открытые вкладки. */
  Browsers: 'browsers',
  /** Вход в Epic, Battle.net, Riot и Ubisoft Connect. */
  Launchers: 'launchers',
  /** Discord и Telegram Desktop: вход и переписка. */
  Messengers: 'messengers',
} as const;
export type SessionTraceName = (typeof SessionTraceNames)[keyof typeof SessionTraceNames];

/** Словарь: Shell/ShellBridgeContracts.cs */
export const ShellBridgeErrorCodeNames = {
  /** Номер или ПИН-код не подошли. */
  SignInRefused: 'sign_in_refused',
  /** С этого ПК слишком много неудачных входов. */
  TooManyAttempts: 'too_many_attempts',
  /** На ПК идёт чужая сессия. */
  SessionNotYours: 'session_not_yours',
  /** Агента нет на связи — войти и запустить игру сейчас нельзя. */
  AgentUnavailable: 'agent_unavailable',
  /** Агент на месте, а до сервера клуба не достучался. */
  PlatformUnreachable: 'platform_unreachable',
  /** Такого хост пока не умеет: запрос из более новой страницы или раздел следующего этапа. */
  NotSupported: 'not_supported',
  /** Windows не дала поменять звук, микрофон или раскладку — например, нет устройства. */
  SystemUnavailable: 'system_unavailable',
} as const;
export type ShellBridgeErrorCodeName = (typeof ShellBridgeErrorCodeNames)[keyof typeof ShellBridgeErrorCodeNames];

/** Словарь: Shell/ShellBridgeContracts.cs */
export const ShellBridgeEventTypeNames = {
  /** Состояние ПК от агента — PlayerShellStateDto. */
  StateChanged: 'state.changed',
  /** Вошёл ли игрок на этом ПК — ShellAuthStateDto. */
  AuthChanged: 'auth.changed',
  /** Мышь или клавиатура тронуты: витрина уступает место окну входа. */
  InputActivity: 'input.activity',
  /** Тишина дольше порога: окно входа закрывается, вошедший выходит. */
  InputIdle: 'input.idle',
  /** Игра на переднем плане — ShellGameForegroundDto: страница засыпает, чтобы не отнимать кадр. */
  GameForeground: 'game.foreground',
  /** Громкость, микрофон, раскладка — ShellSystemStateDto. */
  SystemChanged: 'system.changed',
  ShowcaseChanged: 'showcase.changed',
} as const;
export type ShellBridgeEventTypeName = (typeof ShellBridgeEventTypeNames)[keyof typeof ShellBridgeEventTypeNames];

/**
 * Мост хост ↔ интерфейс оболочки, версия 2 (спека оболочки, §4.4). Конверт запроса и ответа —
 * общий, из @afk4/host-bridge; здесь — имена и тела. Записи C# дают типы и хосту, и странице:
 * две руками написанные копии однажды разошлись бы.
 *
 * Словарь: Shell/ShellBridgeContracts.cs
 */
export const ShellBridgeRequestTypeNames = {
  /**
   * Страница загрузилась и слушает. Ответ — ShellSnapshotDto: всё, что хост уже знает. Без
   * этого состояние, отправленное до того, как React подписался, терялось бы, и экран ждал бы
   * следующего пульса агента.
   */
  ShellReady: 'shell.ready',
  /** Войти номером и ПИН-кодом — через агента, токены привязаны к этому ПК. */
  AuthSignIn: 'auth.signIn',
  AuthSignOut: 'auth.signOut',
  /** Запустить игру из библиотеки клуба. */
  AppLaunch: 'app.launch',
  /** Позвать администратора к этому ПК. */
  AssistCall: 'assist.call',
  SystemSetVolume: 'system.setVolume',
  SystemSetMicMuted: 'system.setMicMuted',
  SystemSetLayout: 'system.setLayout',
  /** Язык интерфейса выбран на экране: хост запоминает его до выхода игрока. */
  UiSetLocale: 'ui.setLocale',
  /**
   * Рекламная карточка витрины ушла с экрана — ShellShowcaseImpressionDto. Хост передаёт агенту,
   * тот копит суммы и отправляет пачками; карточки клуба не считаются.
   */
  ShowcaseImpression: 'showcase.impression',
  /** Кнопка «Вернуть в зал» на полосе обслуживания. */
  MaintenanceReturn: 'maintenance.return',
} as const;
export type ShellBridgeRequestTypeName = (typeof ShellBridgeRequestTypeNames)[keyof typeof ShellBridgeRequestTypeNames];

/** Словарь: Shell/ShellBridgeContracts.cs */
export const ShellKeyboardLayoutNames = {
  Russian: 'RU',
  English: 'EN',
  Tajik: 'TG',
} as const;
export type ShellKeyboardLayoutName = (typeof ShellKeyboardLayoutNames)[keyof typeof ShellKeyboardLayoutNames];

/** Словарь: Shell/ShellPipeProtocol.cs */
export const ShellPipeErrorCodeNames = {
  ProtocolMismatch: 'protocol_mismatch',
  /**
   * Хост подключился не из консольной сессии — например, по удалённому рабочему столу.
   * Состояние этого ПК и запуск игр принадлежат тому, кто сидит за монитором.
   */
  WrongSession: 'wrong_session',
  InvalidPayload: 'invalid_payload',
  UnknownRequest: 'unknown_request',
  /** Игры запускаются только во время сессии. */
  NoSession: 'no_session',
  AppNotAllowed: 'app_not_allowed',
  /** Игра в списке клуба, но её файла на этом ПК нет. */
  AppMissing: 'app_missing',
  LaunchFailed: 'launch_failed',
  /** До платформы не достучались — стойка о вызове не узнала. */
  PlatformUnreachable: 'platform_unreachable',
  /** Хосту некуда отправить запрос: агента нет на другом конце канала. */
  AgentUnavailable: 'agent_unavailable',
  /** Номер или ПИН-код не подошли. Те же имена, что у сервера и моста к странице. */
  SignInRefused: 'sign_in_refused',
  TooManyAttempts: 'too_many_attempts',
  /** На ПК идёт чужая сессия: вход верный, но открыть вошедшему нечего. */
  SessionNotYours: 'session_not_yours',
  /** Клуб закрыл этот ПК на обслуживание — вход на нём закрыт. */
  DeviceInMaintenance: 'device_in_maintenance',
} as const;
export type ShellPipeErrorCodeName = (typeof ShellPipeErrorCodeNames)[keyof typeof ShellPipeErrorCodeNames];

/** Словарь: Shell/ShellPipeProtocol.cs */
export const ShellPipeMessageTypeNames = {
  /** Хост представляется первым; без этого агент ничего не шлёт. */
  Hello: 'hello',
  /** Агент прощается: версия протокола не та или хост не из той сессии. */
  Bye: 'bye',
  State: 'state',
  Request: 'request',
  Reply: 'reply',
  /** Агент передаёт хосту команду клуба: выйти из аккаунта игрока или показать сообщение. */
  Command: 'command',
  /**
   * Игрок вошёл: агент отдаёт хосту токены. Один кадр на оба пути — ПИН-код и QR: вход по QR
   * приходит без запроса хоста, и отвечать на него нечем, кроме отдельного кадра.
   */
  Auth: 'auth',
} as const;
export type ShellPipeMessageTypeName = (typeof ShellPipeMessageTypeNames)[keyof typeof ShellPipeMessageTypeNames];

/** Словарь: Shell/ShellPipeProtocol.cs */
export const ShellPipeRequestTypeNames = {
  /** Запустить игру из списка клуба. В теле — `appId`. */
  Launch: 'launch',
  /** Позвать администратора к этому ПК. */
  Assist: 'assist',
  /**
   * Войти номером и ПИН-кодом. В теле — `phone` и `pin`. Удачный ответ пуст: токены
   * приходят кадром ShellPipeMessageTypeNames.Auth.
   */
  SignInPin: 'signIn.pin',
  /**
   * «Вернуть в зал» с самого ПК (спека оболочки, §6.5): агент говорит серверу и закрывает
   * рабочий стол техника. Тело пустое.
   */
  MaintenanceReturn: 'maintenance.return',
  /**
   * За ПК кто-то есть: тронуты мышь или клавиатура. Не чаще раза в 20 секунд; по нему агент не
   * выключает простаивающий ПК под рукой человека и отменяет уже назначенное выключение. Тело пустое.
   */
  Activity: 'activity',
  /**
   * Рекламная карточка витрины отстояла на экране. В теле — `cardId` и `shownMs`.
   * Агент считает только рекламу и только на свободном ПК.
   */
  ShowcaseImpression: 'showcase.impression',
} as const;
export type ShellPipeRequestTypeName = (typeof ShellPipeRequestTypeNames)[keyof typeof ShellPipeRequestTypeNames];

/**
 * Машинные имена отказов по сменам и кассе. См. Tariffs.TariffErrorCodeNames — та же
 * причина: у кассы эти отказы самые частые, а без кода до кассира доезжала английская фраза
 * сервера вместе с сырым телом ответа.
 *
 * Словарь: Shifts/ShiftErrorCodeNames.cs
 */
export const ShiftErrorCodeNames = {
  /** В филиале уже открыта смена — вторую открыть нельзя. */
  AlreadyOpen: 'shift_already_open',
  /** Смену уже закрыли: скорее всего, это сделал сосед по кассе. */
  AlreadyClosed: 'shift_already_closed',
  /** Валюта операции не совпадает с валютой смены. */
  CurrencyMismatch: 'shift_currency_mismatch',
  /** Расхождение по кассе больше допуска — нужна подпись старшего. */
  SignOffRequired: 'shift_sign_off_required',
  /** Подписать расхождение должен не тот, кто смену открыл или закрывает. */
  SignOffMustDiffer: 'shift_sign_off_must_differ',
  /** У выбранного сотрудника нет права подписывать расхождение. */
  SignOffNotAuthorized: 'shift_sign_off_not_authorized',
  /** Внесение и изъятие наличных возможны только при открытой смене. */
  CashMovementNeedsOpenShift: 'cash_movement_needs_open_shift',
} as const;
export type ShiftErrorCodeName = (typeof ShiftErrorCodeNames)[keyof typeof ShiftErrorCodeNames];

/** Словарь: Shifts/ShiftStateNames.cs */
export const ShiftStateNames = {
  Open: 'open',
  Closed: 'closed',
} as const;
export type ShiftStateName = (typeof ShiftStateNames)[keyof typeof ShiftStateNames];

/** Словарь: Shop/ShopOrderStatusNames.cs */
export const ShopOrderStatusNames = {
  Placed: 'placed',
  Accepted: 'accepted',
  Delivered: 'delivered',
  Cancelled: 'cancelled',
} as const;
export type ShopOrderStatusName = (typeof ShopOrderStatusNames)[keyof typeof ShopOrderStatusNames];

/** Словарь: Showcase/ShowcaseContracts.cs */
export const ShowcaseCardKindNames = {
  News: 'news',
  Tariff: 'tariff',
  Product: 'product',
  Tournament: 'tournament',
  Packages: 'packages',
  BarHit: 'bar_hit',
  /** Реклама платформы: только на свободном ПК и с меткой «Реклама · рекламодатель». */
  Ad: 'ad',
} as const;
export type ShowcaseCardKindName = (typeof ShowcaseCardKindNames)[keyof typeof ShowcaseCardKindNames];

/**
 * Машинные причины отказа на входе сотрудника. Клиент по ним и подбирает слова: текст сервера
 * английский, а мастер и приложение клуба работают на трёх языках.
 *
 * Словарь: Identity/StaffAuthErrorCodeNames.cs
 */
export const StaffAuthErrorCodeNames = {
  /**
   * Пять промахов подряд — вход в эту учётную запись закрыт на четверть часа. Отдельно от
   * обычного «неверно»: там человек ищет опечатку, здесь ждёт.
   */
  TooManyPasswordAttempts: 'too_many_password_attempts',
} as const;
export type StaffAuthErrorCodeName = (typeof StaffAuthErrorCodeNames)[keyof typeof StaffAuthErrorCodeNames];

/**
 * Машинные имена отказов при приглашении сотрудника. См.
 * Install.InstallErrorCodeNames — та же причина: отказ нужно назвать на языке того,
 * кто его читает.
 *
 * Словарь: Identity/StaffInviteErrorCodeNames.cs
 */
export const StaffInviteErrorCodeNames = {
  /** Номер не похож на телефон — приглашение уходит SMS, слать его некуда. */
  InvalidPhone: 'invalid_phone',
  /** Логин уже занят другим сотрудником клуба. */
  UserNameTaken: 'staff_username_taken',
  /** Номер уже принадлежит сотруднику клуба. */
  PhoneTaken: 'staff_phone_taken',
} as const;
export type StaffInviteErrorCodeName = (typeof StaffInviteErrorCodeNames)[keyof typeof StaffInviteErrorCodeNames];

/** Словарь: Inventory/StockMovementTypeNames.cs */
export const StockMovementTypeNames = {
  Purchase: 'purchase',
  Sale: 'sale',
  Refund: 'refund',
  Adjustment: 'adjustment',
} as const;
export type StockMovementTypeName = (typeof StockMovementTypeNames)[keyof typeof StockMovementTypeNames];

/** Словарь: Platform/Organizations/SubscriptionStatusNames.cs */
export const SubscriptionStatusNames = {
  Trial: 'trial',
  Active: 'active',
  PastDue: 'past_due',
  Cancelled: 'cancelled',
} as const;
export type SubscriptionStatusName = (typeof SubscriptionStatusNames)[keyof typeof SubscriptionStatusNames];

/**
 * Машинные имена отказов по тарифам. См. Install.InstallErrorCodeNames — та же
 * причина: отказ нужно назвать на языке того, кто его читает.
 *
 * Словарь: Tariffs/TariffErrorCodeNames.cs
 */
export const TariffErrorCodeNames = {
  /** Тариф с таким именем в филиале уже есть. */
  NameTaken: 'tariff_name_taken',
} as const;
export type TariffErrorCodeName = (typeof TariffErrorCodeNames)[keyof typeof TariffErrorCodeNames];

/** Словарь: Tips/TipContracts.cs */
export const TipErrorCodeNames = {
  /** Вернуть чаевые можно только из открытой смены. */
  ShiftClosed: 'tip_shift_closed',
  AlreadyReversed: 'tip_already_reversed',
  /** Всё, что пришло за смену, уже выдано. */
  NothingToPay: 'tip_nothing_to_pay',
} as const;
export type TipErrorCodeName = (typeof TipErrorCodeNames)[keyof typeof TipErrorCodeNames];

/** Словарь: Tips/TipContracts.cs */
export const TipUnavailableReasonNames = {
  Disabled: 'disabled',
  /** В филиале нет открытой смены — деньги некому отдать. */
  NoShift: 'no_shift',
  NotEnded: 'not_ended',
  TooLate: 'too_late',
  AlreadyTipped: 'already_tipped',
  NotEnoughBalance: 'not_enough_balance',
  InvalidAmount: 'invalid_amount',
} as const;
export type TipUnavailableReasonName = (typeof TipUnavailableReasonNames)[keyof typeof TipUnavailableReasonNames];

/**
 * Что с записью игрока на событие.
 *
 * Словарь: Tournaments/TournamentStateNames.cs
 */
export const TournamentRegistrationStateNames = {
  Registered: 'registered',
  Cancelled: 'cancelled',
} as const;
export type TournamentRegistrationStateName = (typeof TournamentRegistrationStateNames)[keyof typeof TournamentRegistrationStateNames];

/**
 * Что с событием клуба прямо сейчас.
 *
 * Словарь: Tournaments/TournamentStateNames.cs
 */
export const TournamentStateNames = {
  /** Черновик: клуб составляет событие, игрок его не видит. */
  Draft: 'draft',
  /** Опубликовано: событие видно в приложении и на него записываются. */
  Published: 'published',
  /** Отменено клубом. Взносы возвращены всем записавшимся. */
  Cancelled: 'cancelled',
} as const;
export type TournamentStateName = (typeof TournamentStateNames)[keyof typeof TournamentStateNames];

/** Словарь: Updates/UpdateChannelNames.cs */
export const UpdateChannelNames = {
  Stable: 'stable',
  Beta: 'beta',
  Internal: 'internal',
} as const;
export type UpdateChannelName = (typeof UpdateChannelNames)[keyof typeof UpdateChannelNames];

/** Словарь: Updates/UpdateComponentNames.cs */
export const UpdateComponentNames = {
  OrganizationAdmin: 'organization-admin',
  AgentService: 'agent-service',
  PlayerShell: 'player-shell',
} as const;
export type UpdateComponentName = (typeof UpdateComponentNames)[keyof typeof UpdateComponentNames];

/** Словарь: Updates/UpdatePackageSignatureAlgorithmNames.cs */
export const UpdatePackageSignatureAlgorithmNames = {
  EcdsaP256Sha256IeeeP1363: 'ECDSA-P256-SHA256-IEEE-P1363',
} as const;
export type UpdatePackageSignatureAlgorithmName = (typeof UpdatePackageSignatureAlgorithmNames)[keyof typeof UpdatePackageSignatureAlgorithmNames];

/** Словарь: Updates/UpdatePackageStateNames.cs */
export const UpdatePackageStateNames = {
  Registered: 'registered',
  Validated: 'validated',
  Rejected: 'rejected',
  Retired: 'retired',
} as const;
export type UpdatePackageStateName = (typeof UpdatePackageStateNames)[keyof typeof UpdatePackageStateNames];

/** Словарь: Updates/UpdateRolloutStateNames.cs */
export const UpdateRolloutStateNames = {
  Draft: 'draft',
  Active: 'active',
  Paused: 'paused',
  Completed: 'completed',
  RollbackRequested: 'rollback-requested',
  RolledBack: 'rolled-back',
  Cancelled: 'cancelled',
} as const;
export type UpdateRolloutStateName = (typeof UpdateRolloutStateNames)[keyof typeof UpdateRolloutStateNames];

/** Словарь: Updates/UpdateStatusNames.cs */
export const UpdateStatusNames = {
  NotStarted: 'not-started',
  Offered: 'offered',
  Downloading: 'downloading',
  Downloaded: 'downloaded',
  Installing: 'installing',
  Installed: 'installed',
  /**
   * Пакет лёг, но файлы, занятые работающими процессами, Windows заменит только после
   * перезагрузки машины: до неё устройство работает на прежней сборке.
   */
  PendingRestart: 'pending-restart',
  Superseded: 'superseded',
  Failed: 'failed',
  RollbackStarted: 'rollback-started',
  RolledBack: 'rolled-back',
  Deferred: 'deferred',
  ReadyToInstall: 'ready-to-install',
  AwaitingAppExit: 'awaiting-app-exit',
  HealthChecking: 'health-checking',
  RollbackRequired: 'rollback-required',
} as const;
export type UpdateStatusName = (typeof UpdateStatusNames)[keyof typeof UpdateStatusNames];

/** Словарь: Updates/UpdateTargetKindNames.cs */
export const UpdateTargetKindNames = {
  Branch: 'branch',
  Device: 'device',
} as const;
export type UpdateTargetKindName = (typeof UpdateTargetKindNames)[keyof typeof UpdateTargetKindNames];

/** Контракт: Identity/AccountActivation/AcceptOrganizationOwnerInviteRequest.cs */
export interface AcceptOrganizationOwnerInviteRequest {
  code: string;
  userName: string;
  displayName: string;
  password: string;
}

/** Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs */
export interface AcceptPlatformAdminInvitationRequest {
  code: string;
  userName: string;
  displayName: string;
  password: string;
}

/**
 * Приём приглашения: номер, код из SMS и пароль, который человек придумывает себе сам.
 *
 * Контракт: Identity/AcceptStaffInviteRequest.cs
 */
export interface AcceptStaffInviteRequest {
  phoneNumber: string;
  code: string;
  password: string;
}

/**
 * Кем человек стал: клуб и его логин в нём.
 *
 * Контракт: Identity/AcceptStaffInviteRequest.cs
 */
export interface AcceptStaffInviteResponse {
  organizationId: Guid;
  userName: string;
}

/** Контракт: Players/ActiveSessionDto.cs */
export interface ActiveSessionDto {
  sessionId: Guid;
  seatId: Guid;
  seatName: string;
  startedAtUtc: IsoDateTime;
  /**
   * Режим сессии. "fixed" — оплачена наперёд, показывается остаток; "open" — счётчик времени и
   * накопленная стоимость.
   * "open" | "fixed"
   */
  durationMode: string;
  /** fixed only */
  remainingSeconds: number | null;
  /** open only */
  accruedCostMinorUnits: number | null;
  currencyCode: string;
  /**
   * По какой цене идёт счёт и где человек сидит. Растущая сумма без ставки — это число, которое
   * нечем проверить: видно, что платишь, и не видно, за что. Цена — за час, а не за минуту:
   * клуб продаёт часы, и в них же человек считает.
   * Пусто там, где тарифа у сессии нет вовсе (гостевая, заведённая на стойке руками) — врать
   * подставленной ставкой хуже, чем честно промолчать.
   */
  tariffName?: string | null;
  pricePerHourMinorUnits?: number | null;
  zoneName?: string | null;
}

/**
 * Что кампания обязана сказать на карточке по закону РТ «О рекламе» (спека рекламы, §8):
 * номер разрешения Минздрава, продажа на расстоянии, обязательная сертификация, условия сделки.
 *
 * Контракт: Ads/AdContracts.cs
 */
export interface AdCampaignComplianceDto {
  /** Разрешение или лицензия Минздрава — обязательно для «Здоровья и красоты» (ст. 17). */
  permitNumber?: string | null;
  /** Продажа на расстоянии: карточка печатает наименование, ИНН и адрес продавца (ст. 14(1)). */
  distanceSelling?: boolean;
  /** Товар подлежит обязательной сертификации: карточка печатает пометку (ст. 5). */
  requiresCertification?: boolean;
  /** В рекламе цена или условия сделки: карточка печатает срок предложения — конец кампании (ст. 26). */
  containsOffer?: boolean;
}

/** Контракт: Ads/AdContracts.cs */
export interface AdCampaignDto {
  campaignId: Guid;
  advertiserId: Guid;
  advertiserName: string;
  name: string;
  /** Одно из AdCategoryNames */
  category: AdCategoryName;
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  /** Пусто — все города. */
  cities: string[];
  /** Пусто — все клубы. */
  organizationIds: Guid[];
  /** Одно из AdCampaignStateNames */
  state: AdCampaignStateName;
  creatives: AdCreativeDto[];
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  compliance?: AdCampaignComplianceDto | null;
}

/** Контракт: Ads/AdContracts.cs */
export interface AdCreativeDto {
  creativeId: Guid;
  campaignId: Guid;
  /**
   * Заголовок и текст на государственном языке — таджикском (ст. 5 закона о рекламе, закон о
   * госязыке): обязательны и идут на карточке первыми.
   */
  title: string;
  body: string | null;
  imageUrl: string | null;
  /** Одно из AdModerationNames */
  moderation: AdModerationName;
  rejectedReason: string | null;
  moderatedAtUtc: IsoDateTime | null;
  createdAtUtc: IsoDateTime;
  /** Русский — второй строкой по желанию рекламодателя. */
  titleRu?: string | null;
  bodyRu?: string | null;
  /**
   * Слова, которые закон разрешает только с документом («лучший», «№ 1», ст. 7): модератору —
   * подсказка, а не запрет.
   */
  wordingFlags?: string[] | null;
  /** Снят с показа. Показанный креатив не правится и не удаляется — его хранят год (ст. 22). */
  archivedAtUtc?: IsoDateTime | null;
}

/** Контракт: Inventory/AddProductBarcodeRequest.cs */
export interface AddProductBarcodeRequest {
  organizationId: Guid;
  code: string;
  isPrimary?: boolean;
}

/**
 * Строка отчёта показов: креатив в филиале за день. Игрока в строке нет и быть не может.
 *
 * Контракт: Ads/AdContracts.cs
 */
export interface AdImpressionRowDto {
  day: string;
  campaignId: Guid;
  campaignName: string;
  creativeId: Guid;
  creativeTitle: string;
  organizationId: Guid;
  organizationName: string;
  branchId: Guid;
  branchName: string;
  city: string;
  impressions: number;
  shownSeconds: number;
}

/**
 * Реклама платформы в витрине свободного ПК (спека `2026-09-25-platform-ads-design.md`). Продаёт
 * её AFK4, показывается она только клубам с фичей `platform_ads` — это бесплатный тариф.
 *
 * Контракт: Ads/AdContracts.cs
 */
export interface AdvertiserDto {
  advertiserId: Guid;
  /** Имя на карточке: «Реклама · {Name}». */
  name: string;
  contact: string;
  createdAtUtc: IsoDateTime;
  /**
   * Реквизиты для договора и рекламы с продажей на расстоянии (закон РТ «О рекламе», ст. 14(1)):
   * наименование, ИНН (единый идентификационный номер) и место нахождения.
   */
  legalName?: string;
  taxId?: string;
  address?: string;
}

/**
 * Точка помесячного ряда. Год и месяц едут числами: название месяца — дело клиента,
 * у которого есть язык пользователя.
 *
 * Контракт: Platform/Analytics/PlatformAnalyticsContracts.cs
 */
export interface AnalyticsMonthDto {
  year: number;
  month: number;
  recurringMinorUnits: number;
  oneOffMinorUnits: number;
  joined: number;
  left: number;
  payingAtMonthEnd: number;
}

/**
 * Анонс глазами клуба: только то, что ему показывают, плюс прочитал ли ЭТОТ сотрудник.
 *
 * Контракт: Platform/Announcements/AnnouncementContracts.cs
 */
export interface AnnouncementFeedItemDto {
  announcementId: Guid;
  title: string;
  body: string;
  severity: string;
  showFromUtc: IsoDateTime;
  showUntilUtc: IsoDateTime;
  publishedAtUtc: IsoDateTime;
  isRead: boolean;
}

/** Контракт: Devices/AssignDeviceSeatRequest.cs */
export interface AssignDeviceSeatRequest {
  organizationId: Guid;
  seatId: Guid;
}

/** Контракт: Audit/AuditRecordDto.cs */
export interface AuditRecordDto {
  auditRecordId: Guid;
  organizationId: Guid;
  branchId: Guid | null;
  actorStaffUserId: Guid | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  sourceApp: string;
  detailsJson: string;
  createdAtUtc: IsoDateTime;
  actorPlatformAdminUserId: Guid | null;
  /**
   * Имя клуба-клиента, к которому относится запись. Панель платформы смотрит журнал
   * поверх всей сети, и опознавательный знак «кто» там — имя, а не идентификатор: наизусть их
   * не знает никто. Пусто, если организация к моменту чтения журнала уже удалена.
   */
  organizationName: string | null;
  amountMinorUnits: number | null;
}

/** Контракт: Audit/AuditSearchResultDto.cs */
export interface AuditSearchResultDto {
  records: AuditRecordDto[];
  limit: number;
}

/**
 * Create-seat request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.
 *
 * Контракт: Install/AuthenticatedInstallRequests.cs
 */
export interface AuthenticatedInstallCreateSeatRequest {
  branchId: Guid;
  zoneId: Guid;
  name: string;
}

/**
 * Device-enroll request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.
 *
 * Контракт: Install/AuthenticatedInstallRequests.cs
 */
export interface AuthenticatedInstallEnrollRequest {
  branchId: Guid;
  seatId: Guid | null;
  role: string;
  displayName: string;
  machineName: string;
  devicePublicKey: string;
}

/**
 * Условия оплаты для клубов (спека тарифов клуба, §3–§5): сколько длится пробный период,
 * обещанный платёж и льгота после срока счёта до перехода на бесплатный тариф. Задаёт платформа.
 *
 * Контракт: Platform/Billing/BillingTermsContracts.cs
 */
export interface BillingTermsDto {
  trialDays: number;
  promisedPaymentDays: number;
  fallbackAfterOverdueDays: number;
  /** Пусто — условия ещё не меняли, действуют значения по умолчанию. */
  updatedAtUtc: IsoDateTime | null;
}

/**
 * Правило закрытия окна: часть заголовка, класс окна или оба сразу.
 *
 * Контракт: Devices/ProtectionProfileContracts.cs
 */
export interface BlockedWindowRuleDto {
  titleContains: string | null;
  className: string | null;
}

/**
 * Настройки приёма гостей у филиала — то, что видит и правит клуб.
 * UpdatedAtUtc пуст, пока филиал ничего не настраивал: значения в этом случае
 * не «нулевые», а по умолчанию, и админу полезно отличать одно от другого.
 *
 * Контракт: Branches/BranchBookingSettingsDto.cs
 */
export interface BranchBookingSettingsDto {
  organizationId: Guid;
  branchId: Guid;
  acceptanceMode: string;
  respondWithinMinutes: number;
  requirePrepaymentFromNewGuests: boolean;
  maxActiveReservationsForNewGuests: number;
  regularAfterVisits: number;
  holdSeatAfterStartMinutes: number;
  keepPrepaymentOnNoShow: boolean;
  updatedAtUtc: IsoDateTime | null;
}

/** Контракт: Diagnostics/BranchDiagnosticsDto.cs */
export interface BranchDiagnosticsDto {
  organizationId: Guid;
  branchId: Guid;
  generatedAtUtc: IsoDateTime;
  deviceSummary: DeviceDiagnosticsSummaryDto;
  commandSummary: CommandDiagnosticsSummaryDto;
  updateSummary: UpdateDiagnosticsSummaryDto;
  staleDevices: StaleDeviceDiagnosticsDto[];
}

/**
 * Одни свёрнутые сутки клуба. `AgentAlive == null` — «неизвестно», не «мёртв».
 *
 * Контракт: Platform/Analytics/BranchDynamicsContracts.cs
 */
export interface BranchDynamicsDayDto {
  date: IsoDate;
  sessionCount: number;
  revenue: MoneyDto;
  shiftOpenedCount: number;
  agentAlive: boolean | null;
}

/** Контракт: Platform/Analytics/BranchDynamicsContracts.cs */
export interface BranchDynamicsDto {
  organizationId: Guid;
  branchId: Guid;
  fromDate: IsoDate;
  toDate: IsoDate;
  totalRevenue: MoneyDto;
  totalSessionCount: number;
  daysWithoutAgent: number;
  daysWithUnknownAgent: number;
  /** Сутки окна, за которые снимка нет вовсе. Нулями они НЕ дорисовываются. */
  missingDayCount: number;
  days: BranchDynamicsDayDto[];
}

/**
 * Игра в библиотеке филиала — то, что увидит игрок на ПК.
 *
 * Контракт: Games/GameLibraryContracts.cs
 */
export interface BranchGameDto {
  branchGameId: Guid;
  /** Из каталога — тогда обложка и возраст берутся оттуда; null — своя игра клуба. */
  catalogGameId: Guid | null;
  name: string;
  genre: string | null;
  minAge: number | null;
  coverUrl: string | null;
  /** Одно из GameLaunchKindNames. */
  launchKind: GameLaunchKindName;
  launchTarget: string | null;
  /** Свой путь к exe вместо лаунчера — когда игра стоит не там, где её ищет агент. */
  executablePath: string | null;
  arguments: string | null;
  /** Запускается и без сессии: лаунчер для пополнения Steam, например. */
  availableWithoutSession: boolean;
  isEnabled: boolean;
  sortOrder: number;
  /** Запускается сам в начале сессии: Discord, клиент Steam. */
  launchOnSessionStart?: boolean;
}

/**
 * Одно фото зала. MediaId нужен, чтобы удалить объект из хранилища вместе со
 * строкой галереи; у фото, добавленного ссылкой, его нет.
 *
 * Контракт: Branches/BranchPhotoDto.cs
 */
export interface BranchPhotoDto {
  url: string;
  mediaId: Guid | null;
}

/** Контракт: Branches/BranchProfileDto.cs */
export interface BranchProfileDto {
  organizationId: Guid;
  branchId: Guid;
  name: string;
  city: string;
  description: string | null;
  address: string | null;
  phone: string | null;
  telegram: string | null;
  website: string | null;
  instagram: string | null;
  logoUrl: string | null;
  logoMediaId: Guid | null;
  coverImageUrl: string | null;
  coverMediaId: Guid | null;
  photos: BranchPhotoDto[];
  latitude: number | null;
  longitude: number | null;
  timeZone: string;
  locale: string;
  workingHours: BranchWorkingHoursDayDto[];
  createdAtUtc: IsoDateTime;
}

/**
 * Профиль защиты филиала для Панели: сам профиль и кто его менял последним.
 *
 * Контракт: Devices/ProtectionProfileContracts.cs
 */
export interface BranchProtectionProfileDto {
  organizationId: Guid;
  branchId: Guid;
  profile: ProtectionProfileDto;
  updatedAtUtc: IsoDateTime | null;
}

/**
 * Отзыв для клуба: кто, за каким ПК и когда — чтобы «мышь липкая» можно было найти на ПК 07,
 * а не гадать, о каком из тридцати речь.
 *
 * Контракт: Reviews/ClubReviewDtos.cs
 */
export interface BranchReviewDto {
  reviewId: Guid;
  playerAccountId: Guid;
  authorName: string;
  rating: number;
  comment: string | null;
  createdAtUtc: IsoDateTime;
  sessionId: Guid;
  seatName: string | null;
}

/**
 * Отзывы филиала для Панели: итог по всем оценкам и страница списка.
 *
 * Контракт: Reviews/ClubReviewDtos.cs
 */
export interface BranchReviewsPageDto {
  /** Пусто — оценок пока нет. Это не ноль звёзд. */
  rating: number | null;
  reviewCount: number;
  /** Сколько оценок на каждую звезду: [1★, 2★, 3★, 4★, 5★]. */
  countsByRating: number[];
  items: BranchReviewDto[];
  /** Следующая страница — отзывы раньше этого времени; null — дальше нет. */
  nextBefore: IsoDateTime | null;
}

/**
 * Одна находка палитры: чем это открыть (Kind и Id) и как
 * узнать глазами (остальное).
 * <param name="Subtitle">
 * То, чем различают похожие строки: зал у места, телефон у клиента и у брони. Пусто там, где
 * различать нечем.
 * </param>
 * <param name="OccursAtUtc">
 * К какому моменту относится находка: начало брони, дата чека. Сырое время, а не готовая
 * подпись, — язык и часовой пояс знает клиент, а не сервер.
 * </param>
 * <param name="Status">
 * Где находка сейчас, если у неё есть ход жизни: у заказа — новый, готовится, выдан или отменён.
 * Код, а не подпись: подпись на своём языке ставит клиент.
 * </param>
 * <param name="Number">
 * Номер, по которому её называют, когда он не в заголовке: у заказа — номер его чека. По нему
 * человек и узнаёт, что нашлось именно то, что он набирал.
 * </param>
 *
 * Контракт: Operator/BranchSearchResultDto.cs
 */
export interface BranchSearchResultDto {
  kind: string;
  id: Guid;
  title: string;
  subtitle: string | null;
  occursAtUtc?: IsoDateTime | null;
  amountMinorUnits?: number | null;
  currencyCode?: string | null;
  status?: string | null;
  number?: string | null;
}

/** Контракт: Branches/BranchSettingsDto.cs */
export interface BranchSettingsDto {
  organizationId: Guid;
  branchId: Guid;
  requireManualDeviceApproval: boolean;
  preferredLocale: string;
  /**
   * Допустимое расхождение кассы при закрытии смены, в минорных единицах. Больше него смену
   * закрывает только подпись второго менеджера (анти-фрод §5.7), и стойка должна знать порог
   * заранее: спросить подпись до отправки честнее, чем отказать после.
   * Отдаётся уже разрешённым — с подставленным умолчанием, если у филиала своего нет.
   */
  shiftDiscrepancyToleranceMinorUnits?: number;
}

/**
 * Один день расписания клуба. DayOfWeek по ISO-8601: 1=Пн … 7=Вс.
 * Время — строка "HH:mm" (24ч); при IsClosed времена игнорируются.
 *
 * Контракт: Branches/BranchWorkingHoursDayDto.cs
 */
export interface BranchWorkingHoursDayDto {
  dayOfWeek: number;
  isClosed: boolean;
  openTime: string | null;
  closeTime: string | null;
}

/** Контракт: Tariffs/CalculateTariffRequest.cs */
export interface CalculateTariffRequest {
  organizationId: Guid;
  tariffVersionId: Guid;
  durationMinutes: number;
}

/** Контракт: Reservations/ReservationRequests.cs */
export interface CancelReservationRequest {
  organizationId: Guid;
  reason: string;
  expectedVersion: number;
}

/** Контракт: Tournaments/TournamentDtos.cs */
export interface CancelTournamentRequest {
  reason: string;
}

/**
 * Начисление кешбэка: сколько, когда и за что.
 *
 * Контракт: Loyalty/CashbackEntryDto.cs
 */
export interface CashbackEntryDto {
  amountMinorUnits: number;
  currencyCode: string;
  /**
   * Служебная причина вида `cashback:topup` или `cashback:shop:{id}`. Разбирается на экране в
   * человеческую подпись: показывать игроку внутреннее имя события незачем.
   */
  reason: string;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Shifts/CashMovementDto.cs */
export interface CashMovementDto {
  cashMovementId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  createdByStaffUserId: Guid;
  movementType: string;
  amount: MoneyDto;
  reason: string;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Reports/CashOperationReportResultDto.cs */
export interface CashOperationReportResultDto {
  rows: CashOperationReportRowDto[];
  limit: number;
  cashInTotal: MoneyDto;
  cashOutTotal: MoneyDto;
  netCashTotal: MoneyDto;
}

/** Контракт: Reports/CashOperationReportRowDto.cs */
export interface CashOperationReportRowDto {
  operationId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid | null;
  createdByStaffUserId: Guid;
  sourceType: string;
  operationType: string;
  cashImpact: MoneyDto;
  reason: string;
  createdAtUtc: IsoDateTime;
  /**
   * Кто провёл операцию. Журнал кассы отвечает на вопрос «кто взял деньги», а идентификатор
   * сотрудника на этот вопрос не отвечает: показывать кассиру GUID — то же, что не показывать.
   */
  createdByDisplayName?: string;
}

/** Контракт: Shifts/ShiftRevenueDto.cs */
export interface CashReconciliationDto {
  starting: MoneyDto;
  expected: MoneyDto;
  counted: MoneyDto | null;
  difference: MoneyDto | null;
}

/**
 * Игра в каталоге платформы — из него клубы добавляют игры себе.
 *
 * Контракт: Games/GameLibraryContracts.cs
 */
export interface CatalogGameDto {
  catalogGameId: Guid;
  name: string;
  description: string | null;
  genre: string | null;
  /** Возрастная отметка: 0, 12, 16, 18. Даты рождения у игрока нет — отметка только видна. */
  minAge: number | null;
  /** Одно из GameLaunchKindNames. */
  launchKind: GameLaunchKindName;
  /** AppID Steam, имя приложения Epic, продукт Riot, код Battle.net; для exe — путь по умолчанию. */
  launchTarget: string | null;
  coverUrl: string | null;
  isPublished: boolean;
  updatedAtUtc: IsoDateTime;
}

/** Контракт: Platform/Updates/PlatformUpdateContracts.cs */
export interface ChangePlatformUpdatePackageStateRequest {
  state: string;
  reason: string;
}

/** Контракт: Platform/Updates/PlatformUpdateContracts.cs */
export interface ChangePlatformUpdateRolloutStateRequest {
  state: string;
  reason: string;
}

/** Контракт: Loyalty/ReferralContracts.cs */
export interface ClaimReferralCodeRequest {
  code: string;
}

/** Контракт: Shifts/CloseShiftRequest.cs */
export interface CloseShiftRequest {
  organizationId: Guid;
  countedCash: MoneyDto;
  closingNote: string;
  idempotencyKey: string;
  /**
   * Anti-fraud §5.7: required only when the cash discrepancy exceeds the branch tolerance; must be a
   * manager other than the operator who opened or closed the shift.
   */
  managerSignOffStaffUserId?: Guid | null;
  signOffReason?: string | null;
}

/**
 * A physical club of the network: what the card shows and where the map puts its pin.
 *
 * Контракт: Branding/OrganizationDirectoryEntryDto.cs
 */
export interface ClubPlaceDto {
  branchId: Guid;
  name: string;
  city: string;
  address: string | null;
  description: string | null;
  coverImageUrl: string | null;
  latitude: number | null;
  longitude: number | null;
  /**
   * Расписание клуба: игрок хочет знать не только где клуб, но и открыт ли он сейчас.
   * Пустой список — расписание не задано.
   */
  workingHours?: BranchWorkingHoursDayDto[] | null;
  /** Залы этого клуба: сколько мест и на чём играют. По железу клубы и сравнивают. */
  zones?: ClubZoneDto[] | null;
  /** Фото зала по порядку: обложка первой, дальше галерея. Пусто — клуб фото не прислал. */
  photoUrls?: string[] | null;
  /** Всего мест в зале — сумма по его зонам. */
  seatCount?: number;
  /**
   * Сколько из них не занято прямо сейчас: ни сессии, ни чужой брони на ближайший час.
   * Считается независимо от расписания — «открыт ли зал» решает тот, кто показывает число.
   */
  freeSeatCount?: number;
}

/** Контракт: Platform/Billing/ClubPlanContracts.cs */
export interface ClubPlanDeviceDto {
  deviceId: Guid;
  name: string;
  branchName: string;
  /** Новые сессии на нём запускаются. */
  works: boolean;
  /** Владелец отметил его работающим на бесплатном тарифе. */
  kept: boolean;
}

/**
 * Игровые ПК клуба глазами тарифа: какие работают на бесплатном и какие отметил владелец.
 *
 * Контракт: Platform/Billing/ClubPlanContracts.cs
 */
export interface ClubPlanDevicesDto {
  /** Предел ПК на клуб; пусто — у тарифа предела нет, работают все. */
  limit: number | null;
  devices: ClubPlanDeviceDto[];
}

/**
 * Тариф клуба словами (спека `2026-09-25-club-plans-per-pc-design.md`): сколько ПК, сколько из них
 * платных, во что выйдет месяц и что клуб может сделать сам. Цену прежней сетки клуб не видит.
 *
 * Контракт: Platform/Billing/ClubPlanContracts.cs
 */
export interface ClubPlanDto {
  planCode: string;
  /** Одно из ClubPlanKindNames */
  kind: ClubPlanKindName;
  devices: number;
  includedDevices: number;
  billableDevices: number;
  pricePerDevice: MoneyDto;
  /** Счёт за месяц при сегодняшнем числе ПК. У бесплатного и пробного — ноль. */
  estimatedMonthly: MoneyDto;
  trialEndsAtUtc: IsoDateTime | null;
  trialAvailable: boolean;
  canSwitchToPerPc: boolean;
  promisedPaymentAvailable: boolean;
  promisedPaymentUntilUtc: IsoDateTime | null;
  /** Просроченное; пусто — долга нет. */
  overdue: MoneyDto | null;
  /** «Приведи клуб»: код клуба и сколько бесплатных месяцев накоплено за приведённых. */
  referralCode?: string | null;
  freeMonths?: number;
  referredClubs?: number;
  /** ПК, на которых новые сессии не запускаются: сверх предела бесплатного тарифа (§5a). */
  devicesOutsidePlan?: number;
  /** Когда клуб перейдёт на бесплатный тариф, если не оплатит просроченное. Пусто — не грозит. */
  fallbackAtUtc?: IsoDateTime | null;
  /** Условия, которые задала платформа: экран не должен обещать свои числа. */
  trialDays?: number;
  promisedPaymentDays?: number;
}

/**
 * A review as the club's shop window shows it: who, how many stars, and what they wrote.
 *
 * Контракт: Reviews/ClubReviewDtos.cs
 */
export interface ClubReviewDto {
  reviewId: Guid;
  authorName: string;
  rating: number;
  comment: string | null;
  createdAtUtc: IsoDateTime;
}

/**
 * The reviews page of a club: the average is what a player reads first, the reviews are why.
 *
 * Контракт: Reviews/ClubReviewDtos.cs
 */
export interface ClubReviewsPageDto {
  /** Пусто — оценок пока нет. Это не ноль звёзд. */
  rating: number | null;
  reviewCount: number;
  items: ClubReviewDto[];
}

/**
 * Зал клуба в витрине: название, сколько в нём мест, чем оснащён и сколько мест свободно.
 *
 * Контракт: Branding/OrganizationDirectoryEntryDto.cs
 */
export interface ClubZoneDto {
  name: string;
  seatCount: number;
  hardwareSummary: string | null;
  freeSeatCount?: number;
}

/** Контракт: Diagnostics/BranchDiagnosticsDto.cs */
export interface CommandDiagnosticsSummaryDto {
  pendingCommands: number;
  failedCommands: number;
  recentFailures: FailedCommandDiagnosticsDto[];
}

/** Контракт: Updates/ComponentUpdateInstructionDto.cs */
export interface ComponentUpdateInstructionDto {
  updateRolloutId: Guid;
  updatePackageId: Guid;
  component: string;
  version: string;
  channel: string;
  artifactUri: string;
  sha256: string;
  signature: string;
  signatureAlgorithm: string;
  sizeBytes: number;
  releaseNotes: string;
}

/** Контракт: Reservations/ReservationRequests.cs */
export interface ConfirmReservationRequest {
  organizationId: Guid;
  expectedVersion: number;
}

/**
 * Заявка на добавление филиала существующему клубу.
 *
 * Контракт: Platform/Organizations/CreateBranchRequest.cs
 */
export interface CreateBranchRequest {
  slug: string;
  name: string;
  city: string;
  preferredTimeZone: string | null;
}

/** Контракт: Reviews/ClubReviewDtos.cs */
export interface CreateClubReviewRequest {
  sessionId: Guid;
  rating: number;
  comment: string | null;
}

/**
 * Консоль на месте — без агента (план `2026-09-25-console-seats.md`): администратор сам начинает и
 * заканчивает сессию, тарифы, касса и отчёты — как у ПК. Снимается консоль тем же «Снять
 * устройство», что и ПК.
 *
 * Контракт: Consoles/ConsoleSeatContracts.cs
 */
export interface CreateConsoleSeatRequest {
  organizationId: Guid;
  seatId: Guid;
  displayName: string;
}

/** Контракт: Payments/DcTopUpDtos.cs */
export interface CreateDcTopUpRequest {
  playerAccountId: Guid;
  amountMinorUnits: number;
  currencyCode: string | null;
}

/** Контракт: Devices/CreateDeviceEnrollmentCodeRequest.cs */
export interface CreateDeviceEnrollmentCodeRequest {
  organizationId: Guid;
  expiresInSeconds: number;
}

/**
 * Код установки: техник ставит AFK4 на ПК зала без мастера —
 * `afk4-client.exe /quiet AFK4_INSTALL_CODE=…`. Код многоразовый, но ограничен сроком и
 * числом новых ПК; сервер хранит его хешем, открытым он виден один раз — при выдаче.
 *
 * Контракт: Install/InstallCodeContracts.cs
 */
export interface CreateInstallCodeRequest {
  lifetimeHours: number;
  maxDevices: number;
}

/** Контракт: Platform/Billing/CreateInvoiceRequest.cs */
export interface CreateInvoiceRequest {
  kind: string;
  amountMinorUnits: number;
  description: string;
  dueAtUtc: IsoDateTime | null;
}

/** Контракт: News/CreateNewsItemRequest.cs */
export interface CreateNewsItemRequest {
  branchId: Guid | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: IsoDateTime | null;
  expiresAtUtc: IsoDateTime | null;
  showOnPcs?: boolean;
}

/** Контракт: Identity/AccountActivation/CreateOrganizationOwnerInviteRequest.cs */
export interface CreateOrganizationOwnerInviteRequest {
  branchId: Guid;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  lifetime: IsoDuration | null;
  ownerEmail?: string | null;
}

/** Контракт: Platform/Organizations/CreateOrganizationRequest.cs */
export interface CreateOrganizationRequest {
  organizationSlug: string;
  organizationName: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  planCode: string;
  subscriptionStatus: string;
  limits: OrganizationLimitsDto | null;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  organizationOwnerInviteLifetime: IsoDuration | null;
  /** Код «Приведи клуб» того, кто привёл этот клуб. Пусто — клуб пришёл сам. */
  referralCode?: string | null;
}

/** Контракт: Platform/Organizations/CreateOrganizationResponse.cs */
export interface CreateOrganizationResponse {
  organization: OrganizationDetailDto;
  organizationOwnerInvite: OrganizationOwnerInviteDto;
}

/** Контракт: Platform/SupportNotes/CreateOrganizationSupportNoteRequest.cs */
export interface CreateOrganizationSupportNoteRequest {
  body: string;
}

/** Контракт: Packages/CreatePackageDefinitionRequest.cs */
export interface CreatePackageDefinitionRequest {
  organizationId: Guid;
  name: string;
  price: MoneyDto;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  idempotencyKey: string;
}

/** Контракт: Platform/Billing/CreatePlanRequest.cs */
export interface CreatePlanRequest {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  sortOrder: number;
  pricePerDeviceMinorUnits?: number;
  includedDevices?: number;
  maxDevices?: number | null;
  /** Ключи функций, которые тариф включает; пусто — решают значения функций по умолчанию. */
  includedFeatures?: string[] | null;
}

/** Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs */
export interface CreatePlatformAdminInvitationRequest {
  role: string;
  lifetimeHours: number;
}

/** Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs */
export interface CreatePlatformAdminInvitationResponse {
  invitation: PlatformAdminInvitationDto;
  code: string;
}

/** Контракт: Platform/Auth/PlatformRoleContracts.cs */
export interface CreatePlatformRoleRequest {
  roleName: string;
  displayName: string;
  description: string;
  permissions: string[];
}

/** Контракт: Platform/Support/PlatformSupportAccessContracts.cs */
export interface CreatePlatformSupportAccessGrantRequest {
  organizationId: Guid;
  reason: string;
  lifetimeMinutes: number;
}

/** Контракт: Platform/Updates/PlatformUpdateContracts.cs */
export interface CreatePlatformUpdatePackageRequest {
  component: string;
  version: string;
  channel: string;
  artifactUri: string;
  sha256: string;
  signature: string;
  signatureAlgorithm: string;
  sizeBytes: number;
  releaseNotes: string;
}

/** Контракт: Platform/Updates/PlatformUpdateContracts.cs */
export interface CreatePlatformUpdateRolloutRequest {
  updatePackageId: Guid;
  channel: string;
  targetKind: string;
  organizationIds: Guid[];
  branchIds: Guid[];
  deviceIds: Guid[];
  batchPercent: number;
  startsAtUtc: IsoDateTime;
  reason: string;
}

/** Контракт: Billing/CreatePlayerAccountRequest.cs */
export interface CreatePlayerAccountRequest {
  organizationId: Guid;
  displayName: string;
  phoneNumber: string | null;
  idempotencyKey: string;
}

/**
 * Бронь на компанию: несколько мест на одно время одним действием.
 * Мест здесь КОЛИЧЕСТВО, а не список. Игрок в приложении конкретную машину не выбирает — её
 * назначает клуб, — поэтому просить его выбрать пять машин было бы просьбой о том, чего он не
 * решает. Операторская групповая бронь наоборот берёт список: там человек тянет мышью по строкам
 * таймлайна и точно знает, какие места отдаёт.
 * Тариф один на всю компанию: сидят вместе, платят по одной цене, и разные тарифы внутри одной
 * брони — это уже не «бронь на компанию», а несколько разных броней.
 * <param name="BranchId">
 * Филиал, в который компания придёт. Нужен только в первом действии в клубе, где счёта ещё нет:
 * у сети с несколькими филиалами сервер не гадает, куда записать счёт.
 * </param>
 * <param name="IdempotencyKey">
 * Ключ одной попытки. Необязателен: установленные приложения его не шлют, и без него всё
 * работает как раньше. С ним повтор после обрыва возвращает уже созданную компанию, а не
 * бронирует вторую и не замораживает деньги второй раз.
 * </param>
 *
 * Контракт: Reservations/CreatePlayerReservationGroupRequest.cs
 */
export interface CreatePlayerReservationGroupRequest {
  seatCount: number;
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  note: string | null;
  tariffVersionId?: Guid | null;
  branchId?: Guid | null;
  idempotencyKey?: string | null;
}

/**
 * Player-initiated reservation request.
 * SeatId is optional (unassigned reservation). StartsAtUtc and EndsAtUtc are
 * absolute — the service derives DurationMinutes internally.
 * TariffVersionId carries the player's billing choice made in the app. It is optional so older
 * clients keep working, but a booking without it cannot be priced: the hold slice needs the exact
 * amount the player agreed to, not a branch-default guess. The package path (booking covered by
 * already-purchased time) belongs to that same slice — reserving package minutes is a write on the
 * package, not a field on the request.
 * BranchId — филиал, в который игрок придёт. Нужен только в первом действии в клубе, где счёта
 * ещё нет: у сети с несколькими филиалами сервер не гадает, куда записать счёт. У игрока со
 * счётом филиал уже известен, и присланный его не переписывает.
 * IdempotencyKey — ключ одной попытки. Необязателен: установленные приложения его не шлют, и
 * без него всё работает как раньше. С ним повтор после обрыва находит уже созданное, а не
 * создаёт второе.
 *
 * Контракт: Reservations/CreatePlayerReservationRequest.cs
 */
export interface CreatePlayerReservationRequest {
  seatId: Guid | null;
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  note: string | null;
  tariffVersionId?: Guid | null;
  branchId?: Guid | null;
  idempotencyKey?: string | null;
}

/**
 * Войти на ПК с телефона (спека оболочки, §5.4): приложение сканирует QR с монитора — в нём код
 * посадки — и просит сервер впустить своего человека на эту машину. Номер и ПИН-код у ПК при
 * этом не набираются вовсе.
 *
 * Контракт: Players/PlayerSignInClaimContracts.cs
 */
export interface CreatePlayerSignInClaimRequest {
  seatingCode: string;
  idempotencyKey: string;
}

/**
 * Строка чека в запросе на его создание: товар и сколько штук.
 * Это НЕ PosSaleLineDto. Имя товара, цену за штуку и сумму строки сервер берёт из
 * каталога и присланному не верит (см. EfPosService.CreateSaleAsync) — а раз так, требовать их в
 * запросе значит предлагать клиенту назначить цену и делать вид, что она чего-то стоит. Стойка
 * шлёт ровно то, что знает сама.
 *
 * Контракт: Pos/CreatePosSaleRequest.cs
 */
export interface CreatePosSaleLineDto {
  productId: Guid;
  quantity: number;
}

/** Контракт: Pos/CreatePosSaleRequest.cs */
export interface CreatePosSaleRequest {
  organizationId: Guid;
  shiftId: Guid;
  lines: CreatePosSaleLineDto[];
  idempotencyKey: string;
  playerAccountId?: Guid | null;
  /** When set, attaches this sale to an open session tab (settled at checkout). */
  sessionId?: Guid | null;
}

/** Контракт: Pos/CreateProductCategoryRequest.cs */
export interface CreateProductCategoryRequest {
  organizationId: Guid;
  name: string;
  idempotencyKey: string;
}

/** Контракт: Pos/CreateProductRequest.cs */
export interface CreateProductRequest {
  organizationId: Guid;
  categoryId: Guid;
  name: string;
  sku: string;
  price: MoneyDto;
  trackStock: boolean;
  allowNegativeStock: boolean;
  idempotencyKey: string;
  reorderThreshold: number;
  availableInShell: boolean;
  featuredOnPcs: boolean;
  imageUrl: string | null;
}

/**
 * Creates a recurring report delivery for a branch. ReportType is one of
 * ScheduledReportTypeNames; Frequency one of
 * ReportScheduleFrequencyNames.
 *
 * Контракт: Reports/ReportScheduleContracts.cs
 */
export interface CreateReportScheduleRequest {
  organizationId: Guid;
  reportType: string;
  frequency: string;
}

/**
 * Books several seats as one logical reservation (drag across timeline rows). All-or-nothing:
 * if any seat conflicts with an existing reservation or session, the whole group is rejected and
 * the conflicting seats are reported back so the operator can adjust the selection.
 *
 * Контракт: Reservations/ReservationGroup.cs
 */
export interface CreateReservationGroupRequest {
  organizationId: Guid;
  playerAccountId: Guid | null;
  seatIds: Guid[];
  customerName: string;
  phoneNumber: string | null;
  startsAtUtc: IsoDateTime;
  durationMinutes: number;
  source: string;
  note: string | null;
}

/** Контракт: Reservations/ReservationRequests.cs */
export interface CreateReservationRequest {
  organizationId: Guid;
  playerAccountId: Guid | null;
  seatId: Guid | null;
  customerName: string;
  phoneNumber: string | null;
  startsAtUtc: IsoDateTime;
  durationMinutes: number;
  source: string;
  note: string | null;
}

/** Контракт: Layout/CreateSeatRequest.cs */
export interface CreateSeatRequest {
  organizationId: Guid;
  zoneId: Guid;
  name: string;
  sortOrder: number;
}

/**
 * Приглашение сотрудника по номеру телефона: человек принимает его коротким кодом из SMS и сам
 * задаёт себе пароль. Единственный путь завести сотрудника — заведение с готовым паролем убрано
 * намеренно, чтобы пароль знал только его владелец.
 * <param name="Email">Необязательна: назвали — уйдёт и письмо, не назвали — хватит SMS.</param>
 *
 * Контракт: Identity/CreateStaffInviteRequest.cs
 */
export interface CreateStaffInviteRequest {
  organizationId: Guid;
  userName: string;
  displayName: string;
  phoneNumber: string;
  email: string | null;
  roleNames: string[];
}

/** Контракт: Inventory/CreateStockMovementRequest.cs */
export interface CreateStockMovementRequest {
  organizationId: Guid;
  productId: Guid;
  movementType: string;
  quantityDelta: number;
  unitCost: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

/**
 * `Schedule` не передан — новый тариф действует круглосуточно и каждый день.
 *
 * Контракт: Tariffs/CreateTariffRequest.cs
 */
export interface CreateTariffRequest {
  organizationId: Guid;
  name: string;
  idempotencyKey: string;
  schedule?: TariffScheduleDto | null;
}

/** Контракт: Tariffs/CreateTariffVersionRequest.cs */
export interface CreateTariffVersionRequest {
  organizationId: Guid;
  tariffId: Guid;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: IsoDateTime;
  idempotencyKey: string;
}

/** Контракт: Tournaments/TournamentDtos.cs */
export interface CreateTournamentRequest {
  branchId: Guid;
  title: string;
  description: string;
  discipline: string;
  startsAtUtc: IsoDateTime;
  entryFeeMinorUnits: number;
  capacity: number;
}

/** Контракт: Updates/CreateUpdatePackageRequest.cs */
export interface CreateUpdatePackageRequest {
  organizationId: Guid;
  component: string;
  version: string;
  channel: string;
  artifactUri: string;
  sha256: string;
  signature: string;
  signatureAlgorithm: string;
  sizeBytes: number;
  releaseNotes: string;
}

/** Контракт: Updates/CreateUpdateRolloutRequest.cs */
export interface CreateUpdateRolloutRequest {
  organizationId: Guid;
  updatePackageId: Guid;
  channel: string;
  targetKind: string;
  targetDeviceIds: Guid[];
  batchPercent: number;
  startsAtUtc: IsoDateTime;
  reason: string;
}

/** Контракт: Layout/CreateZoneRequest.cs */
export interface CreateZoneRequest {
  organizationId: Guid;
  name: string;
  sortOrder: number;
  hardwareSummary?: string | null;
}

/**
 * A page of results plus the cursor to fetch the next page (null when exhausted).
 * Курсор — это «продолжить отсюда», а не номер страницы: список растёт с одного конца, и смещение
 * съезжало бы на каждой новой записи.
 *
 * Контракт: Common/CursorPage.cs
 */
export interface CursorPage<T> {
  items: T[];
  nextCursor: string | null;
}

/**
 * GET-ответ: PAN не возвращаем — только факт наличия и last4.
 *
 * Контракт: Payments/DcPayLinkConfigDtos.cs
 */
export interface DcPayLinkConfigDto {
  cardSet: boolean;
  cardLast4: string;
  commentTemplate: string;
  isActive: boolean;
}

/** Контракт: Payments/DcTopUpDtos.cs */
export interface DcTopUpDto {
  intentId: Guid;
  payUrl: string;
  comment: string;
  amountMinorUnits: number;
  currencyCode: string;
  cardLast4: string;
}

/**
 * One club that needs a money decision: either it owes money, or it is still suspended
 * after settling. Days overdue and the dunning stage answer "how long has this been ignored".
 *
 * Контракт: Platform/Billing/DebtRowDto.cs
 */
export interface DebtRowDto {
  organizationId: Guid;
  organizationName: string;
  organizationSlug: string;
  organizationStatus: string;
  subscriptionStatus: string;
  outstandingMinorUnits: number;
  currencyCode: string;
  oldestOverdueInvoiceNumber: number | null;
  oldestOverdueInvoiceId: Guid | null;
  daysOverdue: number;
  dunningStage: number;
  graceUntilUtc: IsoDateTime | null;
  settledButSuspended: boolean;
}

/**
 * Игрок позвал оператора со своей машины. Приходит от агента: устройство известно всегда, а
 * сессии может не быть вовсе — кнопка есть и на запертом экране.
 *
 * Контракт: Devices/AssistanceRequestContracts.cs
 */
export interface DeviceAssistanceRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  requestedAtUtc: IsoDateTime;
}

/**
 * Состояние вызова после обращения: когда позвали. Null — вызова нет.
 *
 * Контракт: Devices/AssistanceRequestContracts.cs
 */
export interface DeviceAssistanceStateDto {
  deviceId: Guid;
  assistanceRequestedAtUtc: IsoDateTime | null;
}

/** Контракт: Devices/DeviceCommandDto.cs */
export interface DeviceCommandDto {
  commandId: Guid;
  type: string;
  createdAtUtc: IsoDateTime;
  payload: Record<string, string>;
}

/** Контракт: Devices/DeviceCommandResultDto.cs */
export interface DeviceCommandResultDto {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  commandId: Guid;
  status: string;
  message: string;
  observedAtUtc: IsoDateTime;
  outcome?: string;
}

/** Контракт: Devices/DeviceCommandStatusDto.cs */
export interface DeviceCommandStatusDto {
  deviceId: Guid;
  commandId: Guid;
  type: string;
  status: string;
  message: string | null;
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  outcome?: string | null;
}

/** Контракт: Updates/DeviceComponentVersionDto.cs */
export interface DeviceComponentVersionDto {
  component: string;
  version: string;
}

/** Контракт: Devices/DeviceConnectionRequest.cs */
export interface DeviceConnectionRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  credentialSecret: string;
  connectedAtUtc: IsoDateTime;
  activeSessionId: Guid | null;
  activeSessionLeaseExpiresAtUtc: IsoDateTime | null;
  activeSessionLeaseSequence: number | null;
}

/** Контракт: Devices/DeviceDetailDto.cs */
export interface DeviceDetailDto {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  enrolledAtUtc: IsoDateTime;
  lastHeartbeatAtUtc: IsoDateTime | null;
  isOnline: boolean;
  isLocked: boolean;
  seatId: Guid | null;
  seatName: string | null;
  zoneId: Guid | null;
  zoneName: string | null;
  activeCredentialCount: number;
  installedAppCount: number;
  recentCommands: DeviceCommandStatusDto[];
  displayName?: string;
  role?: string;
  enrollmentState?: string;
  /** Последний отчёт ПК о защите; null — ПК ещё не докладывал. */
  protectionReport?: DeviceProtectionReportDto | null;
  /** Текущая версия профиля филиала: отчёт со старой версией значит «ПК ещё не применил». */
  branchProtectionVersion?: number;
}

/** Контракт: Diagnostics/BranchDiagnosticsDto.cs */
export interface DeviceDiagnosticsSummaryDto {
  totalDevices: number;
  onlineDevices: number;
  lockedDevices: number;
  staleDevices: number;
  staleThresholdSeconds: number;
  newestHeartbeatAtUtc: IsoDateTime | null;
}

/** Контракт: Devices/DeviceEnrollmentCodeDto.cs */
export interface DeviceEnrollmentCodeDto {
  organizationId: Guid;
  branchId: Guid;
  code: string;
  expiresAtUtc: IsoDateTime;
}

/** Контракт: Devices/DeviceEnrollmentRequest.cs */
export interface DeviceEnrollmentRequest {
  organizationId: Guid;
  branchId: Guid;
  enrollmentCode: string;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  requestedAtUtc: IsoDateTime;
}

/** Контракт: Devices/DeviceEnrollmentResponse.cs */
export interface DeviceEnrollmentResponse {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  credentialId: Guid;
  credentialSecret: string;
  enrolledAtUtc: IsoDateTime;
}

/**
 * Игра для агента: всё, чтобы найти лаунчер на этом ПК и показать плитку.
 *
 * Контракт: Games/GameLibraryContracts.cs
 */
export interface DeviceGameDto {
  appId: string;
  displayName: string;
  genre: string | null;
  minAge: number | null;
  coverUrl: string | null;
  /** Одно из GameLaunchKindNames. */
  launchKind: GameLaunchKindName;
  launchTarget: string | null;
  executablePath: string | null;
  arguments: string | null;
  availableWithoutSession: boolean;
  launchOnSessionStart?: boolean;
}

/**
 * Библиотека филиала для агента. Версия едет в сердцебиении.
 *
 * Контракт: Games/GameLibraryContracts.cs
 */
export interface DeviceGameLibraryDto {
  version: number;
  games: DeviceGameDto[];
}

/**
 * Железо ПК для карточки в Панели: сейчас, принятое и чем они отличаются.
 *
 * Контракт: Devices/DeviceHardwareContracts.cs
 */
export interface DeviceHardwareDto {
  current: HardwareSnapshotDto | null;
  reportedAtUtc: IsoDateTime | null;
  accepted: HardwareSnapshotDto | null;
  acceptedAtUtc: IsoDateTime | null;
  /** Кто принял; null — первый снимок, принятый сам. */
  acceptedByName: string | null;
  changes: HardwareChangeDto[];
}

/** Контракт: Devices/DeviceHardwareContracts.cs */
export interface DeviceHardwareReportRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  collectedAtUtc: IsoDateTime;
  snapshot: HardwareSnapshotDto;
}

/** Контракт: Devices/DeviceHeartbeatRequest.cs */
export interface DeviceHeartbeatRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  observedAtUtc: IsoDateTime;
  isLocked: boolean;
  activeSessionId: Guid | null;
  activeSessionLeaseExpiresAtUtc: IsoDateTime | null;
  activeSessionLeaseSequence: number | null;
  /**
   * MAC проводного адаптера со шлюзом («AA-BB-CC-DD-EE-FF»): по нему этот ПК будит сосед, когда
   * он выключен. Пусто — агент ещё не умеет его сообщать.
   */
  networkMacAddress?: string | null;
  /** Подсеть этого адаптера («192.168.1.0/24»): будить можно только из той же подсети. */
  networkSubnet?: string | null;
  /** Широковещательный адрес подсети — куда сосед шлёт волшебный пакет. */
  networkBroadcastAddress?: string | null;
}

/** Контракт: Devices/DeviceHeartbeatResponse.cs */
export interface DeviceHeartbeatResponse {
  serverTimeUtc: IsoDateTime;
  heartbeatIntervalSeconds: number;
  commands: DeviceCommandDto[];
  /**
   * Effective offline grace window (minutes) for this device's branch. The agent applies it locally
   * to keep a paying customer playing for this long after the network actually drops (spec §6.1).
   */
  effectiveGraceMinutes?: number;
  /**
   * Код, который простаивающий ПК показывает на мониторе, чтобы человек мог сесть за него из
   * приложения. Едет здесь, а не своим маршрутом: сердцебиение и так стучит раз в десять
   * секунд, а код живёт минуты — вторая труба к тому же серверу за тем же самым ничего бы не
   * добавила, кроме второго места, где это можно сломать.
   * null, когда за ПК уже играют: звать к занятой машине незачем.
   */
  seatingCode?: string | null;
  seatingCodeExpiresAtUtc?: IsoDateTime | null;
  /**
   * Клуб попросил сменить ключ этой машины. Агент меняет его сам и записывает новый — без
   * визита к ПК и без простоя. Едет сердцебиением по той же причине, что и код посадки:
   * вторая труба к тому же серверу за тем же самым ничего не добавила бы.
   */
  rotateCredential?: boolean;
  /**
   * Оформление клуба для экрана игрока: название, логотип, цвет. Едет сердцебиением по той же
   * причине, что код посадки и ротация ключа. Хранить его в конфиге машины было бы хуже: клуб
   * меняет логотип в панели, а не обходом всех ПК с переустановкой.
   * null, когда оформление не задано, — оболочка показывает нейтральный экран.
   */
  branding?: ShellBrandingDto | null;
  /** Место этого ПК: оболочка пишет его в шапке. null — ПК ни к какому месту не привязан. */
  seat?: DeviceSeatDto | null;
  /** Чья сессия идёт на ПК: вошедшему не владельцу оболочка чужую сессию не откроет. */
  sessionOwner?: DeviceSessionOwnerDto | null;
  /**
   * Права организации по тарифу (PlatformFeatureNames): оболочка прячет разделы, которых у клуба
   * нет, — бар без player_shop, кэшбек без loyalty. Тот же расчёт, что у /api/me/features.
   */
  features?: string[] | null;
  /**
   * Заявка на вход с телефона, которую ПК ещё не забрал, — на случай, если сигнал SignalR
   * потерялся. null — ждать нечего.
   */
  pendingSignInClaim?: PlayerSignInClaimedDto | null;
  /**
   * ПК на обслуживании. Команду maintenance-on агент получает сразу, а по этому признаку
   * догоняет, если её пропустил, и выходит из обслуживания, если пропустил maintenance-off.
   */
  maintenance?: boolean;
  /**
   * С какого момента и кто включил обслуживание: оболочка пишет это на полосе поверх рабочего
   * стола, чтобы техник у ПК видел, чей это ПК сейчас и с каких пор.
   */
  maintenanceSinceUtc?: IsoDateTime | null;
  maintenanceByName?: string | null;
  /** Версия профиля защиты филиала (§6.3). Сменилась — агент перечитывает профиль; 0 — профиля нет. */
  policyProfileVersion?: number;
  /** Версия библиотеки игр филиала: по её смене агент перечитывает список игр (спека оболочки, §6.6). */
  gameLibraryVersion?: number;
}

/** Контракт: Devices/DeviceInventoryItemDto.cs */
export interface DeviceInventoryItemDto {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  enrolledAtUtc: IsoDateTime;
  lastHeartbeatAtUtc: IsoDateTime | null;
  isOnline: boolean;
  isLocked: boolean;
  seatId: Guid | null;
  seatName: string | null;
  zoneId: Guid | null;
  zoneName: string | null;
  activeCredentialCount: number;
  installedAppCount: number;
  pendingCommandCount: number;
  failedCommandCount: number;
  displayName?: string;
  role?: string;
  enrollmentState?: string;
  /** Железо отличается от принятого — в карточке видно, что поменялось, и кнопка «Принять». */
  hardwareChanged?: boolean;
}

/**
 * «Вернуть в зал» с самого ПК (спека оболочки, §6.5): техник закончил и нажал кнопку на полосе.
 * Агент зовёт сервер ключом устройства, а не ждёт Панель — иначе ПК стоял бы открытым, пока
 * кто-нибудь не дойдёт до стойки.
 *
 * Контракт: Devices/DeviceMaintenanceContracts.cs
 */
export interface DeviceMaintenanceReturnRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
}

/**
 * Отказ входа на ПК. RetryAfterUtc — только у too_many_attempts.
 *
 * Контракт: Devices/DevicePlayerSignInContracts.cs
 */
export interface DevicePlayerSignInErrorDto {
  /** Одно из DevicePlayerSignInErrorCodeNames. */
  error: DevicePlayerSignInErrorCodeName;
  retryAfterUtc?: IsoDateTime | null;
}

/**
 * Игрок входит на самом ПК: номер и ПИН-код. Идёт от агента с ключом устройства, а не с
 * публичного входа: сервер знает, на каком ПК вошли, привязывает токены к этому ПК и считает
 * попытки на устройство, а не на адрес всего клуба за одним роутером.
 *
 * Контракт: Devices/DevicePlayerSignInContracts.cs
 */
export interface DevicePlayerSignInRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  phoneNumber: string;
  pin: string;
}

/**
 * Последний отчёт ПК о защите — для карточки ПК в Панели.
 *
 * Контракт: Devices/ProtectionProfileContracts.cs
 */
export interface DeviceProtectionReportDto {
  version: number;
  appliedAtUtc: IsoDateTime;
  items: ProtectionItemReportDto[];
}

/**
 * Агент применил профиль (или снял его на обслуживание) и докладывает, что вышло.
 *
 * Контракт: Devices/ProtectionProfileContracts.cs
 */
export interface DeviceProtectionReportRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  version: number;
  appliedAtUtc: IsoDateTime;
  items: ProtectionItemReportDto[];
}

/**
 * ПК забирает заявку ключом устройства — в ответ токены, привязанные к этому ПК.
 *
 * Контракт: Devices/PlayerSignInClaimDeviceContracts.cs
 */
export interface DeviceRedeemSignInClaimRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
}

/** Контракт: Devices/DeviceSeatAssignmentDto.cs */
export interface DeviceSeatAssignmentDto {
  deviceSeatAssignmentId: Guid;
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  deviceId: Guid;
  attachedAtUtc: IsoDateTime;
  detachedAtUtc: IsoDateTime | null;
}

/**
 * Место, к которому привязан ПК, — то, что оболочка пишет в шапке: «ПК 07 · Общий зал». Имя
 * места клуб набирает сам, номера отдельно от имени нет.
 *
 * Контракт: Devices/DeviceShellContextContracts.cs
 */
export interface DeviceSeatDto {
  label: string;
  /** Пусто — место без зоны или зона удалена. */
  zoneName: string | null;
}

/**
 * Чья сессия идёт на ПК. Оболочке это нужно, чтобы не открыть вошедшему чужую сессию: посаженный
 * у стойки гость и игрок со своим счётом выглядят по-разному, а вошедший не владелец видит «эта
 * сессия не ваша».
 *
 * Контракт: Devices/DeviceShellContextContracts.cs
 */
export interface DeviceSessionOwnerDto {
  /** Одно из DeviceSessionOwnerKindNames. */
  kind: DeviceSessionOwnerKindName;
  /** Счёт игрока; только у Kind = player. */
  playerAccountId?: Guid | null;
}

/** Контракт: Sessions/DeviceSessionSnapshotRequest.cs */
export interface DeviceSessionSnapshotRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  activeSessionId: Guid | null;
  activeLease: SessionLeaseDto | null;
  isLocked: boolean;
  pendingLocalEventCount: number;
  observedAtUtc: IsoDateTime;
}

/**
 * Витрина свободного ПК (спека оболочки, §5.7): что показывает экран, пока за ПК никто не сидит.
 * Тексты — словами клуба, как их написали в Панели; подписи вроде «Турнир» переводит оболочка.
 *
 * Контракт: Showcase/ShowcaseContracts.cs
 */
export interface DeviceShowcaseDto {
  cards: ShowcaseCardDto[];
}

/**
 * Пачка показов с ПК: суммы по карточке за день.
 *
 * Контракт: Ads/AdContracts.cs
 */
export interface DeviceShowcaseImpressionsRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  /** Ключ пачки: повтор той же пачки после обрыва связи не удваивает счёт. */
  batchId: string;
  items: ShowcaseImpressionDto[];
}

/** Контракт: Devices/DeviceStateChangeRequest.cs */
export interface DeviceStateChangeRequest {
  organizationId: Guid;
  reason?: string | null;
}

/** Контракт: Devices/DeviceStatusChangedDto.cs */
export interface DeviceStatusChangedDto {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  machineName: string;
  isOnline: boolean;
  isLocked: boolean;
  observedAtUtc: IsoDateTime;
  displayName?: string;
  role?: string;
  enrollmentState?: string;
  seatId?: Guid | null;
}

/** Контракт: Updates/DeviceUpdateCheckRequest.cs */
export interface DeviceUpdateCheckRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  channel: string;
  checkedAtUtc: IsoDateTime;
  installedComponents: DeviceComponentVersionDto[];
}

/** Контракт: Updates/DeviceUpdateCheckResponse.cs */
export interface DeviceUpdateCheckResponse {
  serverTimeUtc: IsoDateTime;
  updates: ComponentUpdateInstructionDto[];
  organizationAdminPreference?: OrganizationAdminUpdatePreferenceDto | null;
}

/** Контракт: Updates/DeviceUpdateStatusReportRequest.cs */
export interface DeviceUpdateStatusReportRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  updateRolloutId: Guid;
  updatePackageId: Guid;
  component: string;
  installedVersion: string;
  targetVersion: string;
  status: string;
  message: string;
  observedAtUtc: IsoDateTime;
}

/** Контракт: Updates/DeviceUpdateStatusResultDto.cs */
export interface DeviceUpdateStatusResultDto {
  deviceId: Guid;
  updateRolloutId: Guid;
  updatePackageId: Guid;
  component: string;
  status: string;
  message: string;
  updatedAtUtc: IsoDateTime;
}

/** Контракт: Updates/UpdateRolloutStatusDto.cs */
export interface DeviceUpdateStatusSnapshotDto {
  deviceId: Guid;
  updateRolloutId: Guid;
  updatePackageId: Guid;
  component: string;
  installedVersion: string;
  targetVersion: string;
  status: string;
  message: string;
  updatedAtUtc: IsoDateTime;
}

/** Контракт: Devices/DispatchDeviceCommandRequest.cs */
export interface DispatchDeviceCommandRequest {
  type: string;
  payload: Record<string, string>;
}

/**
 * Чем смена заработала: проданным временем, товаром и удержанной за неявку предоплатой.
 * NoShow стоит отдельно от Time намеренно: удержание — это
 * не проданное время, и сложить их значит показать кассе наигранные часы, которых не было.
 * В Total оно входит — это заработанные деньги, и потерять их в отчёте нельзя.
 *
 * Контракт: Shifts/ShiftRevenueDto.cs
 */
export interface EarnedBreakdownDto {
  time: MoneyDto;
  goods: MoneyDto;
  noShow: MoneyDto;
  total: MoneyDto;
}

/**
 * Список включённых фич для клубского приложения.
 *
 * Контракт: Platform/Features/FeatureContracts.cs
 */
export interface EnabledFeaturesDto {
  features: string[];
}

/** Контракт: Sessions/EndSessionRequest.cs */
export interface EndSessionRequest {
  reason: string;
  idempotencyKey: string;
  expectedVersion?: number | null;
}

/**
 * GET response. The Hash key is never returned — only whether one is stored, so the UI can show
 * "задан" without exposing the secret (mirrors how the dcgate apiKey is never round-tripped).
 *
 * Контракт: Payments/EskhataMerchantConfigDtos.cs
 */
export interface EskhataMerchantConfigDto {
  baseUrl: string;
  companyId: string;
  merchantId: number;
  hashKeySet: boolean;
  status: string;
}

/** Контракт: Sessions/ExtendSessionRequest.cs */
export interface ExtendSessionRequest {
  additionalMinutes: number;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  playerAccountId?: Guid | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
  expectedVersion?: number | null;
}

/** Контракт: Diagnostics/BranchDiagnosticsDto.cs */
export interface FailedCommandDiagnosticsDto {
  deviceId: Guid;
  machineName: string;
  commandId: Guid;
  type: string;
  status: string;
  message: string | null;
  updatedAtUtc: IsoDateTime;
}

/** Контракт: Diagnostics/BranchDiagnosticsDto.cs */
export interface FailedUpdateDiagnosticsDto {
  deviceId: Guid;
  machineName: string;
  updateRolloutId: Guid;
  component: string;
  targetVersion: string;
  status: string;
  message: string;
  updatedAtUtc: IsoDateTime;
}

/** Контракт: FloorMap/FloorMapDto.cs */
export interface FloorMapDto {
  branchId: Guid;
  branchName: string;
  seats: SeatStatusDto[];
  zones: FloorMapZoneDto[];
}

/** Контракт: FloorMap/FloorMapDto.cs */
export interface FloorMapZoneDto {
  zoneId: Guid;
  name: string;
  sortOrder: number;
}

/**
 * Друг и то, единственное, что о нём видно: имя и «сейчас в зале» — если он сам это показывает.
 * Ни телефона, ни денег, ни истории: дружба не даёт доступа к чужому счёту.
 *
 * Контракт: Friends/FriendDtos.cs
 */
export interface FriendDto {
  platformPersonId: Guid;
  displayName: string;
  /**
   * Где он сейчас играет. null — не в зале, или он скрыл своё присутствие. Разницы снаружи
   * нет намеренно: иначе «скрыт» читалось бы как «он там, но прячется».
   */
  presence: FriendPresenceDto | null;
}

/**
 * Клуб и зал, в которых друг сейчас за ПК.
 *
 * Контракт: Friends/FriendDtos.cs
 */
export interface FriendPresenceDto {
  organizationName: string;
  branchName: string;
}

/**
 * Заявка в друзья. Пришедшую можно принять или отклонить, отправленную — только ждать:
 * отзывать её незачем, а кнопка «отозвать» превратила бы список в пульт.
 *
 * Контракт: Friends/FriendDtos.cs
 */
export interface FriendRequestDto {
  friendRequestId: Guid;
  platformPersonId: Guid;
  displayName: string;
  createdAtUtc: IsoDateTime;
}

/**
 * Друзья целиком: принятые, пришедшие заявки и отправленные. Один ответ на весь экран —
 * три запроса ради трёх списков платили бы сетью за одно открытие.
 *
 * Контракт: Friends/FriendDtos.cs
 */
export interface FriendsDto {
  friends: FriendDto[];
  incoming: FriendRequestDto[];
  outgoing: FriendRequestDto[];
  /**
   * Видят ли друзья, что человек сейчас в зале. Выключено — список друзей у него остаётся,
   * но его самого в залах никто не видит.
   */
  showsPresence: boolean;
}

/** Контракт: Reports/GameplayTimeReportResultDto.cs */
export interface GameplayTimeReportResultDto {
  rows: GameplayTimeReportRowDto[];
  limit: number;
  totalDurationSeconds: number;
  totalPackageSeconds: number;
  totalBonusSeconds: number;
  gameplayRevenueTotal: MoneyDto;
}

/** Контракт: Reports/GameplayTimeReportRowDto.cs */
export interface GameplayTimeReportRowDto {
  sessionId: Guid;
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  deviceId: Guid;
  createdByStaffUserId: Guid;
  playerKind: string;
  playerAccountId: Guid | null;
  state: string;
  durationSeconds: number;
  packageSeconds: number;
  bonusSeconds: number;
  gameplayRevenue: MoneyDto;
  startedAtUtc: IsoDateTime | null;
  endedAtUtc: IsoDateTime | null;
  endsAtUtc: IsoDateTime | null;
}

/** Контракт: Players/GuestImportContracts.cs */
export interface GuestImportIssueDto {
  /** Номер строки в файле, с единицы, без заголовка. */
  row: number;
  /** Одно из GuestImportIssueNames */
  code: GuestImportIssueName;
}

/** Контракт: Players/GuestImportContracts.cs */
export interface GuestImportRequest {
  organizationId: Guid;
  currencyCode: string;
  /** Откуда перенос — «SmartShell», «Langame»: в журнал и в описание остатков. */
  source: string;
  rows: GuestImportRowDto[];
  /** true — только проверить и посчитать, ничего не записывать. */
  dryRun: boolean;
  idempotencyKey: string;
}

/** Контракт: Players/GuestImportContracts.cs */
export interface GuestImportResultDto {
  committed: boolean;
  total: number;
  /** Новых карточек гостей. */
  created: number;
  /** Гость с этим номером уже есть в клубе — остатки легли на его карточку. */
  matched: number;
  /** Строки, которые не переносятся (причина — в Issues). */
  skipped: number;
  balanceTotal: MoneyDto;
  bonusTotal: MoneyDto;
  issues: GuestImportIssueDto[];
}

/**
 * Перенос гостей из прежней программы клуба (план `2026-09-25-guest-import.md`): номер, имя,
 * баланс и бонусы становятся карточкой гостя и начальными остатками в журнале. Выгрузку делает
 * владелец клуба; сначала — пробный прогон без записи, потом перенос.
 *
 * Контракт: Players/GuestImportContracts.cs
 */
export interface GuestImportRowDto {
  phone: string | null;
  name: string | null;
  balanceMinorUnits: number;
  bonusMinorUnits: number;
}

/**
 * Что в железе отличается от принятого: было → стало.
 *
 * Контракт: Devices/DeviceHardwareContracts.cs
 */
export interface HardwareChangeDto {
  /** Одно из HardwareComponentNames. */
  component: HardwareComponentName;
  was: string | null;
  now: string | null;
}

/**
 * <param name="Name">Буква диска: «C:».</param>
 *
 * Контракт: Devices/DeviceHardwareContracts.cs
 */
export interface HardwareDiskDto {
  name: string;
  sizeGb: number;
}

/** Контракт: Devices/DeviceHardwareContracts.cs */
export interface HardwareGpuDto {
  name: string;
  memoryGb: number | null;
}

/**
 * Снимок железа ПК (спека оболочки, P9): что стоит внутри. Сравнивается с принятым — поменяли
 * видеокарту или вынули планку памяти, и клуб видит это в карточке ПК, а не узнаёт от игрока.
 *
 * Контракт: Devices/DeviceHardwareContracts.cs
 */
export interface HardwareSnapshotDto {
  cpu: string | null;
  cpuThreads: number;
  /** Вся память в гигабайтах, округлённо: 15,9 ГБ Windows — это 16 ГБ в корпусе. */
  memoryGb: number;
  gpus: HardwareGpuDto[];
  motherboard: string | null;
  disks: HardwareDiskDto[];
  /** Windows и её сборка — видна, но не считается изменением железа: обновления идут каждый месяц. */
  os: string | null;
  bios: string | null;
}

/** Контракт: Platform/Health/PlatformHealthContracts.cs */
export interface IncidentDto {
  incidentId: Guid;
  kind: string;
  dedupKey: string;
  severity: string;
  detailsJson: string;
  openedAtUtc: IsoDateTime;
  lastSeenAtUtc: IsoDateTime;
}

/** Контракт: Shifts/ShiftRevenueDto.cs */
export interface InflowBreakdownDto {
  cash: MoneyDto;
  nonCash: MoneyDto;
  walletTopUps: MoneyDto;
  directTotal: MoneyDto;
}

/** Контракт: Install/InstallDiscoverResponse.cs */
export interface InstallBranchDto {
  branchId: Guid;
  slug: string;
  name: string;
  floorMap: FloorMapDto;
  freeSeatIds: Guid[];
  /** В зале есть хотя бы один действующий тариф — значит платную сессию начать есть чем. */
  hasTariff?: boolean;
  /** В зале есть кто-то кроме владельца: приглашать первого сотрудника уже не нужно. */
  hasStaffBesidesOwner?: boolean;
}

/**
 * Действующий код установки филиала.
 * <param name="Code">Сам код — только в ответе на выдачу; в списке его нет.</param>
 * <param name="UsedDevices">Сколько новых ПК уже встало по коду. Переустановка того же ПК код не тратит.</param>
 *
 * Контракт: Install/InstallCodeContracts.cs
 */
export interface InstallCodeDto {
  installCodeId: Guid;
  branchId: Guid;
  code: string | null;
  createdAtUtc: IsoDateTime;
  expiresAtUtc: IsoDateTime;
  maxDevices: number;
  usedDevices: number;
}

/**
 * Тихая регистрация ПК по коду.
 * <param name="SeatName">
 * Место по имени. Не названо — ищется место с именем компьютера. Не нашлось или занято другим
 * ПК — ПК встаёт без места, и его привязывают в Панели: отказ из-за опечатки в имени оставил бы
 * ПК вовсе не зарегистрированным, а узнал бы о нём техник только обходом зала.
 * </param>
 *
 * Контракт: Install/InstallCodeContracts.cs
 */
export interface InstallCodeEnrollRequest {
  code: string;
  seatName: string | null;
  displayName: string | null;
  machineName: string;
  devicePublicKey: string;
}

/** Контракт: Install/InstallCreateSeatResponse.cs */
export interface InstallCreateSeatResponse {
  organizationId: Guid;
  branchId: Guid;
  zoneId: Guid;
  seatId: Guid;
  name: string;
  sortOrder: number;
}

/** Контракт: Install/InstallDiscoverResponse.cs */
export interface InstallDiscoverResponse {
  ownerDisplayName: string;
  branches: InstallBranchDto[];
  /**
   * Оформление клуба уже задано. Мастер спрашивает про логотип и цвет только когда их нет:
   * админских ПК в клубе бывает несколько, и на втором это был бы не вопрос, а шанс затереть
   * настроенное. В конце списка и с умолчанием — старый мастер продолжит работать.
   */
  brandingConfigured?: boolean;
}

/** Контракт: Devices/InstalledAppDto.cs */
export interface InstalledAppDto {
  displayName: string;
  version: string | null;
  publisher: string | null;
  installLocation: string | null;
  installedAtUtc: IsoDateTime | null;
}

/** Контракт: Devices/InstalledAppReportRequest.cs */
export interface InstalledAppReportRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  reportedAtUtc: IsoDateTime;
  apps: InstalledAppDto[];
}

/** Контракт: Install/InstallEnrollResponse.cs */
export interface InstallEnrollResponse {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  credentialId: Guid;
  credentialSecret: string;
  enrollmentState: string;
  apiBaseUrl: string;
  updateChannel: string;
  enrolledAtUtc: IsoDateTime;
  leaseSigningPublicKeyPem: string;
  updatePackageSigningPublicKeyPem: string;
  /** На какое место встал ПК. Null — без места: при тихой установке место по имени не нашлось или занято. */
  assignedSeatName: string | null;
}

/** Контракт: Inventory/InventoryStockDto.cs */
export interface InventoryStockDto {
  productId: Guid;
  productName: string;
  sku: string;
  trackStock: boolean;
  stockOnHand: number;
}

/** Контракт: Platform/Billing/InvoiceDto.cs */
export interface InvoiceDto {
  invoiceId: Guid;
  organizationId: Guid;
  number: number;
  kind: string;
  periodStartUtc: IsoDateTime;
  periodEndUtc: IsoDateTime;
  issuedAtUtc: IsoDateTime;
  dueAtUtc: IsoDateTime;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
  paidAtUtc: IsoDateTime | null;
  voidedAtUtc: IsoDateTime | null;
  voidReason: string | null;
  description: string;
  grossAmountMinorUnits: number;
  discountMinorUnits: number;
}

/** Контракт: Platform/Billing/InvoiceListItemDto.cs */
export interface InvoiceListItemDto {
  invoiceId: Guid;
  organizationId: Guid;
  organizationName: string;
  organizationSlug: string;
  number: number;
  kind: string;
  issuedAtUtc: IsoDateTime;
  dueAtUtc: IsoDateTime;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
}

/**
 * Состояние одного задания. Kind/JobName едут кодом: клиент никогда не рендерит серверную
 * строку как пользовательский текст — у каждого имени есть перевод в каталоге.
 *
 * Контракт: Platform/Health/PlatformHealthContracts.cs
 */
export interface JobHealthDto {
  jobName: string;
  lastRunAtUtc: IsoDateTime | null;
  lastSuccessAtUtc: IsoDateTime | null;
  lastOutcome: string | null;
  lastItemsProcessed: number;
  lastError: string | null;
  consecutiveFailures: number;
}

/** Контракт: Shell/LauncherAppDto.cs */
export interface LauncherAppDto {
  appId: string;
  displayName: string;
  category: string;
  iconUri: string | null;
  isAvailable: boolean;
  /** Возрастная отметка игры (0, 12, 16, 18). Проверить её не на чем — у игрока нет даты рождения. */
  minAge?: number | null;
}

/** Контракт: Billing/LedgerEntryDto.cs */
export interface LedgerEntryDto {
  ledgerEntryId: Guid;
  organizationId: Guid;
  branchId: Guid;
  playerAccountId: Guid;
  sessionId: Guid | null;
  playerPackageId: Guid | null;
  entryType: string;
  accountType: string;
  amount: MoneyDto;
  quantitySeconds: number;
  description: string;
  reason: string;
  reversesLedgerEntryId: Guid | null;
  createdByStaffUserId: Guid;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Updates/LocalUpdateCoordinationMessage.cs */
export interface LocalUpdateCoordinationRequest {
  secret: string;
  operation: string;
  updateRolloutId: Guid | null;
  updatePackageId: Guid | null;
}

/** Контракт: Updates/LocalUpdateCoordinationMessage.cs */
export interface LocalUpdateCoordinationResponse {
  status: string;
  message: string;
}

/** Контракт: Loyalty/LoyaltySettingsDto.cs */
export interface LoyaltySettingsDto {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
  sessionEnabled: boolean;
  sessionPercentBasisPoints: number;
  cashbackCapMinorUnits: number;
  minimumSourceMinorUnits: number;
}

/** Контракт: Billing/ManualLedgerCorrectionRequest.cs */
export interface ManualLedgerCorrectionRequest {
  organizationId: Guid;
  accountType: string;
  amount: MoneyDto;
  quantitySeconds: number;
  reason: string;
  idempotencyKey: string;
}

/** Контракт: Payments/ManualPaymentRequest.cs */
export interface ManualPaymentRequest {
  organizationId: Guid;
  paymentMethod: string;
  amount: MoneyDto;
  note: string;
  idempotencyKey: string;
}

/** Контракт: Platform/Billing/MarkInvoicePaidRequest.cs */
export interface MarkInvoicePaidRequest {
  reference: string | null;
}

/**
 * «Он не приехал» — сказанное человеком за стойкой, а не выведенное таймером.
 * Автоматика ждёт столько, сколько велел филиал, и разбирает только брони с замороженными
 * деньгами. Администратор видит пустое место раньше и знает про бронь без предоплаты то, чего
 * не знает ни один таймер, — поэтому отметить неявку он может сам.
 * <param name="ExpectedVersion">
 * Версия брони, которую видел администратор. Пусто — не спорить о версиях: повторный клик по
 * уже отмеченной неявке не должен выглядеть конфликтом.
 * </param>
 *
 * Контракт: Reservations/MarkReservationNoShowRequest.cs
 */
export interface MarkReservationNoShowRequest {
  organizationId: Guid;
  expectedVersion?: number | null;
}

/**
 * Человек и его клубы одним ответом. Приложение открывается на этом: сначала «кто я», потом
 * «где у меня что». Общей суммы денег здесь нет и не будет — у каждого клуба своя касса, и
 * складывать остатки разных клубов значит показать число, которое ниоткуда нельзя потратить.
 *
 * Контракт: Players/MeDto.cs
 */
export interface MeDto {
  person: MePersonDto;
  clubs: MyClubDto[];
}

/**
 * Личность: то, что принадлежит человеку, а не клубу. PIN сюда не попадает никогда — только
 * признак, задан он или ещё нет.
 *
 * Контракт: Players/MeDto.cs
 */
export interface MePersonDto {
  platformPersonId: Guid;
  phoneNumber: string;
  displayName: string;
  preferredLocale: string | null;
  phoneVerified: boolean;
  pinSet: boolean;
  networkBanned: boolean;
  /**
   * За что закрыт вход. Запрет, о котором человек не может узнать причину, читается как поломка
   * приложения — и он идёт спорить к стойке, которая его не ставила.
   */
  networkBanReason: string | null;
}

/** Контракт: Ads/AdContracts.cs */
export interface ModerateAdCreativeRequest {
  approve: boolean;
  /** Причина отказа — рекламодателю через менеджера платформы. Обязательна при отказе. */
  reason: string | null;
  /**
   * Модератор подтверждает то, чего код не проверит, — каждую строку AdModerationCheckNames.
   * Без всех отметок одобрить нельзя.
   */
  confirmed?: string[] | null;
}

/**
 * Approve or reject a pending money action; the optional note is recorded on the request.
 *
 * Контракт: Billing/MoneyActionContracts.cs
 */
export interface MoneyActionDecisionRequest {
  decisionReason: string | null;
}

/**
 * A pending money action for the Manager Review screen (§5.5).
 *
 * Контракт: Billing/MoneyActionContracts.cs
 */
export interface MoneyActionRequestDto {
  moneyActionRequestId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  actionType: string;
  requestedByStaffUserId: Guid;
  amountMinorUnits: number;
  currencyCode: string;
  reason: string;
  state: string;
  createdAtUtc: IsoDateTime;
  expiresAtUtc: IsoDateTime;
}

/**
 * The pending-approvals feed.
 *
 * Контракт: Billing/MoneyActionContracts.cs
 */
export interface MoneyActionRequestListResponse {
  requests: MoneyActionRequestDto[];
}

/**
 * Submit a high-risk money action through the anti-fraud control layer (§5.2). The guard decides
 * whether it executes now, is held for approval, or is refused on a cap breach. `ActionType` is
 * `refund` or `manual_correction`; a debt-reducing correction is classified as a write-off
 * server-side. `SignedAmountMinorUnits` is signed (corrections may be ±); refunds use its magnitude.
 *
 * Контракт: Billing/MoneyActionContracts.cs
 */
export interface MoneyActionSubmitRequest {
  organizationId: Guid;
  actionType: string;
  playerAccountId: Guid;
  ledgerEntryId: Guid | null;
  accountType: string;
  signedAmountMinorUnits: number;
  currencyCode: string;
  quantitySeconds: number;
  reason: string;
  idempotencyKey: string;
}

/**
 * Outcome of a submitted money action: `executed` / `pending_approval` / `rejected`.
 *
 * Контракт: Billing/MoneyActionContracts.cs
 */
export interface MoneyActionSubmitResponse {
  outcome: string;
  resultingLedgerEntryId: Guid | null;
  moneyActionRequestId: Guid | null;
}

/** Контракт: Billing/MoneyDto.cs */
export interface MoneyDto {
  currencyCode: string;
  minorUnits: number;
}

/**
 * Перенос собственной брони игроком: новое время и, если нужно, другое место.
 * Длительности здесь нет намеренно. «Перенести» — это то же самое на другое время; изменить
 * длину — другое решение с другой ценой, и прятать его в ту же кнопку значит однажды удивить
 * человека суммой.
 *
 * Контракт: Reservations/MovePlayerReservationRequest.cs
 */
export interface MovePlayerReservationRequest {
  startsAtUtc: IsoDateTime;
  seatId?: Guid | null;
  expectedVersion?: number | null;
}

/**
 * Один клуб глазами игрока: сколько можно потратить, сколько придержано под брони, сколько
 * он должен и сколько раз приходил.
 * Клуба нет в списке — значит человек в нём ещё ничего не делал, и счёта там пока нет. Это
 * нормальное состояние, а не сбой: показывать его ошибкой значит пугать на ровном месте.
 *
 * Контракт: Players/MeDto.cs
 */
export interface MyClubDto {
  organizationId: Guid;
  organizationName: string;
  playerAccountId: Guid;
  homeBranchId: Guid;
  currencyCode: string;
  walletBalanceMinorUnits: number;
  heldMinorUnits: number;
  debtMinorUnits: number;
  visitCount: number;
}

/**
 * Человек сети глазами платформы: ровно столько, сколько нужно, чтобы решить вопрос о запрете.
 * Ни клубов, ни денег, ни визитов здесь нет — это клубные сведения, и панель платформы не место,
 * где их собирают в одну карточку.
 *
 * Контракт: Platform/People/NetworkPeopleContracts.cs
 */
export interface NetworkPersonDto {
  platformPersonId: Guid;
  phoneNumber: string;
  displayName: string;
  registeredAtUtc: IsoDateTime;
  networkBanAtUtc: IsoDateTime | null;
  networkBanReason: string | null;
}

/**
 * Спрос по точному номеру. Поиска по части номера нет намеренно.
 *
 * Контракт: Platform/People/NetworkPeopleContracts.cs
 */
export interface NetworkPersonLookupRequest {
  phoneNumber: string;
}

/** Контракт: News/NewsItemDto.cs */
export interface NewsItemDto {
  id: Guid;
  branchId: Guid | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: IsoDateTime | null;
  expiresAtUtc: IsoDateTime | null;
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  /** Новость крутится и на экране свободного ПК (витрина), а не только в приложении. */
  showOnPcs?: boolean;
}

/**
 * The outcome of a user-waiting send (OTP / password reset) after its first dispatch attempt.
 *
 * Контракт: Notifications/NotificationContracts.cs
 */
export interface NotificationDeliveryResult {
  handle: NotificationHandle;
  delivered: boolean;
  error: string | null;
}

/**
 * The result of enqueuing a notification: the outbox row id(s) and whether new rows were created
 * (`false` when a duplicate NotificationRequest.IdempotencyKey collapsed the send).
 *
 * Контракт: Notifications/NotificationContracts.cs
 */
export interface NotificationHandle {
  outboxIds: Guid[];
  created: boolean;
}

/**
 * A resolved delivery target. Locale is BCP-47-ish (ru/en/tg) resolved upstream; address fields are
 * channel-specific. Staff/player ids provide audit linkage and a future in-app target.
 *
 * Контракт: Notifications/NotificationContracts.cs
 */
export interface NotificationRecipient {
  locale: string;
  emailAddress?: string | null;
  phoneNumber?: string | null;
  staffUserId?: Guid | null;
  playerAccountId?: Guid | null;
}

/** Контракт: Shifts/OpenShiftRequest.cs */
export interface OpenShiftRequest {
  organizationId: Guid;
  startingCash: MoneyDto;
  openingNote: string;
  idempotencyKey: string;
}

/** Контракт: Reports/OperatorActionReportResultDto.cs */
export interface OperatorActionReportResultDto {
  rows: OperatorActionReportRowDto[];
  limit: number;
  totalActionCount: number;
}

/** Контракт: Reports/OperatorActionReportRowDto.cs */
export interface OperatorActionReportRowDto {
  actorStaffUserId: Guid | null;
  actorDisplayName: string;
  action: string;
  outcome: string;
  count: number;
  firstAtUtc: IsoDateTime;
  lastAtUtc: IsoDateTime;
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardAlertPressureDto {
  pendingCommands: number;
  failedCommands: number;
  offlineDevices: number;
  endingSessions: number;
  totalAlerts: number;
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardQueueItemDto {
  tone: string;
  target: string;
  title: string;
  detail: string;
  seatId: Guid | null;
  deviceId: Guid | null;
  createdAtUtc: IsoDateTime;
  sourceType: string;
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardRecentPaymentDto {
  paymentId: Guid;
  posSaleId: Guid | null;
  shiftId: Guid;
  createdByStaffUserId: Guid;
  paymentKind: string;
  paymentMethod: string;
  amount: MoneyDto;
  createdAtUtc: IsoDateTime;
  sessionId?: Guid | null;
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardReservationSummaryDto {
  activeReservations: number;
  availableSlots: number;
  source: string;
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardRevenueSummaryDto {
  posNetSales: MoneyDto;
  gameplayRevenue: MoneyDto;
  totalRevenue: MoneyDto;
  posCheckCount: number;
  newPlayerCount: number;
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardShiftSummaryDto {
  shiftId: Guid | null;
  state: string;
  openedAtUtc: IsoDateTime | null;
  openedByStaffUserId: Guid | null;
  expectedCash: MoneyDto;
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardSummaryDto {
  organizationId: Guid;
  branchId: Guid;
  fromUtc: IsoDateTime;
  toUtc: IsoDateTime;
  generatedAtUtc: IsoDateTime;
  shift: OperatorDashboardShiftSummaryDto;
  revenue: OperatorDashboardRevenueSummaryDto;
  utilization: OperatorDashboardUtilizationSummaryDto;
  alertPressure: OperatorDashboardAlertPressureDto;
  reservations: OperatorDashboardReservationSummaryDto;
  focusQueue: OperatorDashboardQueueItemDto[];
  recentPayments: OperatorDashboardRecentPaymentDto[];
}

/** Контракт: Dashboard/OperatorDashboardSummaryDto.cs */
export interface OperatorDashboardUtilizationSummaryDto {
  totalSeats: number;
  activeSessions: number;
  endingSessions: number;
  onlineDevices: number;
  offlineDevices: number;
  sessionStarts: number;
  utilizationPercent: number;
}

/** Контракт: Players/OperatorTopUpIntentDto.cs */
export interface OperatorTopUpIntentDto {
  paymentIntentId: Guid;
  playerAccountId: Guid;
  displayName: string;
  amountMinorUnits: number;
  currencyCode: string;
  state: string;
  method: string;
  createdAtUtc: IsoDateTime;
  seatName: string | null;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminActiveShiftDto {
  shiftId: Guid;
  openedByStaffUserId: Guid;
  openedAtUtc: IsoDateTime;
  expectedCash: MoneyDto;
  isProvisional: boolean;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminReportAttentionDto {
  kind: string;
  title: string;
  detail: string;
  targetId: Guid | null;
  amount: MoneyDto | null;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminReportFiguresDto {
  netRevenue: MoneyDto;
  gameplayRevenue: MoneyDto;
  posNetSales: MoneyDto;
  gameplaySeconds: number;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminReportPeriodDto {
  fromDate: IsoDate;
  toDate: IsoDate;
  timeZone: string;
  fromUtc: IsoDateTime;
  toUtc: IsoDateTime;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminRevenueBreakdownDto {
  key: string;
  label: string;
  revenue: MoneyDto;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminRevenueComparisonDto {
  previousNetRevenue: MoneyDto;
  differenceMinorUnits: number;
  changePercent: number | null;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminRevenueReportDto {
  period: OrganizationAdminReportPeriodDto;
  grossRevenue: MoneyDto;
  refunds: MoneyDto;
  netRevenue: MoneyDto;
  gameplayRevenue: MoneyDto;
  gameplaySeconds: number;
  posNetSales: MoneyDto;
  comparison: OrganizationAdminRevenueComparisonDto;
  sources: OrganizationAdminRevenueSourceDto[];
  paymentMethods: OrganizationAdminRevenueBreakdownDto[];
  operators: OrganizationAdminRevenueBreakdownDto[];
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminRevenueSourceDto {
  source: string;
  revenue: MoneyDto;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminRevenueTrendPointDto {
  date: IsoDate;
  netRevenue: MoneyDto;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminShiftCashReportDto {
  period: OrganizationAdminReportPeriodDto;
  shifts: ShiftReportRowDto[];
  cashOperations: CashOperationReportRowDto[];
  cashInTotal: MoneyDto;
  cashOutTotal: MoneyDto;
  netCashTotal: MoneyDto;
}

/** Контракт: Reports/OrganizationAdminReportContracts.cs */
export interface OrganizationAdminSummaryReportDto {
  period: OrganizationAdminReportPeriodDto;
  attentionTotalCount: number;
  attentionItems: OrganizationAdminReportAttentionDto[];
  figures: OrganizationAdminReportFiguresDto;
  trend: OrganizationAdminRevenueTrendPointDto[];
  activeShift: OrganizationAdminActiveShiftDto | null;
}

/** Контракт: Updates/OrganizationAdminUpdatePreferenceDto.cs */
export interface OrganizationAdminUpdatePreferenceDto {
  organizationId: Guid;
  branchId: Guid;
  maintenanceWindowStart: IsoTime;
  maintenanceWindowEnd: IsoTime;
  timeZone: string;
}

/**
 * Compact arrears summary for the club's own admin banner: enough to say what is owed and
 * how late it is, without pulling the whole invoice list on every screen load.
 *
 * Контракт: Platform/Billing/OrganizationBillingStatusDto.cs
 */
export interface OrganizationBillingStatusDto {
  inArrears: boolean;
  outstandingMinorUnits: number;
  currencyCode: string;
  oldestOverdueInvoiceNumber: number | null;
  daysOverdue: number;
  graceUntilUtc: IsoDateTime | null;
}

/** Контракт: Platform/Organizations/OrganizationBranchDto.cs */
export interface OrganizationBranchDto {
  branchId: Guid;
  slug: string;
  name: string;
  city: string;
  createdAtUtc: IsoDateTime;
}

/**
 * Клуб, каким его видит мастер установки: как называется и как выглядит.
 *
 * Контракт: Branding/OrganizationBrandingDto.cs
 */
export interface OrganizationBrandingDto {
  organizationId: Guid;
  name: string;
  logoUrl: string | null;
  accentColor: string | null;
}

/** Контракт: Platform/Organizations/OrganizationDetailDto.cs */
export interface OrganizationDetailDto {
  organizationId: Guid;
  slug: string;
  name: string;
  status: string;
  statusReason: string | null;
  statusChangedAtUtc: IsoDateTime | null;
  planCode: string;
  subscriptionStatus: string;
  limits: OrganizationLimitsDto;
  branches: OrganizationBranchDto[];
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  contactEmail?: string | null;
  contactPhone?: string | null;
  legalDetails?: string | null;
  updateChannel?: string;
  pinnedClientVersion?: string | null;
}

/**
 * A club as it appears in the public picker: enough to choose it, nothing about how the
 * business is doing. The mobile app has no hostname to derive a club from, so the player picks
 * one from this list before signing in.
 * The showcase fields below (places, price, seats) are what turns a list of names into a
 * shop window: a player picks a club by where it is and what an hour costs, and a name alone
 * answers neither question. They are optional so a club that filled nothing in still appears.
 *
 * Контракт: Branding/OrganizationDirectoryEntryDto.cs
 */
export interface OrganizationDirectoryEntryDto {
  organizationId: Guid;
  slug: string;
  name: string;
  logoUrl: string | null;
  accentColor: string | null;
  places?: ClubPlaceDto[] | null;
  pricePerHourFromMinorUnits?: number | null;
  currencyCode?: string | null;
  seatCount?: number;
  rating?: number | null;
  reviewCount?: number;
}

/**
 * Состояние фичи для клуба вместе с тем, ЧЕМ оно решено: «не куплено» и «не выкачено» —
 * разные ответы клиенту, и панель обязана их различать.
 *
 * Контракт: Platform/Features/FeatureContracts.cs
 */
export interface OrganizationFeatureStateDto {
  featureKey: string;
  name: string;
  description: string;
  isEnabled: boolean;
  decisionLevel: string;
  overrideValue: boolean | null;
  overrideReason: string | null;
  overrideSetAtUtc: IsoDateTime | null;
  planValue: boolean | null;
  defaultValue: boolean;
}

/** Контракт: Platform/Health/OrganizationHealthDto.cs */
export interface OrganizationHealthDto {
  organizationId: Guid;
  status: string;
  branchCount: number;
  deviceCount: number;
  activeStaffUserCount: number;
  latestStaffSignInAtUtc: IsoDateTime | null;
  latestMigration: string | null;
  recentErrorCount: number;
  recentErrors: OrganizationHealthErrorDto[];
}

/** Контракт: Platform/Health/OrganizationHealthErrorDto.cs */
export interface OrganizationHealthErrorDto {
  createdAtUtc: IsoDateTime;
  source: string;
  action: string;
  outcome: string;
  message: string | null;
}

/** Контракт: Platform/Organizations/OrganizationLimitsDto.cs */
export interface OrganizationLimitsDto {
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  /**
   * Игровых ПК на весь клуб, без деления по залам: бесплатный тариф — «до десяти ПК», сколько бы
   * залов ни было (спека тарифов клуба, §2). Консоли не считаются.
   */
  maxDevices?: number | null;
}

/**
 * Состояние ухода клуба: где он в цикле «заявка → выгрузка → стирание».
 *
 * Контракт: Platform/Organizations/OffboardingContracts.cs
 */
export interface OrganizationOffboardingDto {
  organizationId: Guid;
  slug: string;
  status: string;
  purgeEligibleAtUtc: IsoDateTime | null;
  purgedAtUtc: IsoDateTime | null;
  canPurge: boolean;
}

/** Контракт: Identity/AccountActivation/OrganizationOwnerAccountActivationResult.cs */
export interface OrganizationOwnerAccountActivationResult {
  organizationId: Guid;
  branchId: Guid;
  nextStep: string;
}

/** Контракт: Identity/AccountActivation/OrganizationOwnerInviteDto.cs */
export interface OrganizationOwnerInviteDto {
  organizationOwnerInviteId: Guid;
  organizationId: Guid;
  branchId: Guid;
  code: string;
  status: string;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  expiresAtUtc: IsoDateTime;
  acceptedAtUtc: IsoDateTime | null;
  revokedAtUtc: IsoDateTime | null;
  revokedReason: string | null;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Identity/AccountActivation/OrganizationOwnerInviteSummaryDto.cs */
export interface OrganizationOwnerInviteSummaryDto {
  organizationOwnerInviteId: Guid;
  organizationId: Guid;
  branchId: Guid;
  codeSuffix: string;
  status: string;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  expiresAtUtc: IsoDateTime;
  acceptedAtUtc: IsoDateTime | null;
  revokedAtUtc: IsoDateTime | null;
  revokedReason: string | null;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Platform/Billing/OrganizationSubscriptionDto.cs */
export interface OrganizationSubscriptionDto {
  organizationSubscriptionId: Guid;
  organizationId: Guid;
  planCode: string;
  status: string;
  currentPeriodStartUtc: IsoDateTime;
  currentPeriodEndUtc: IsoDateTime;
  nextInvoiceUtc: IsoDateTime | null;
  amountMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  cancelAtPeriodEnd: boolean;
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  paymentGraceUntilUtc: IsoDateTime | null;
  discountPercent: number | null;
  discountAmountMinorUnits: number | null;
  discountUntilUtc: IsoDateTime | null;
  discountReason: string | null;
}

/** Контракт: Platform/Organizations/OrganizationSummaryDto.cs */
export interface OrganizationSummaryDto {
  organizationId: Guid;
  slug: string;
  name: string;
  status: string;
  planCode: string;
  subscriptionStatus: string;
  branchCount: number;
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  recentErrorCount: number;
  expiringOwnerInviteCount: number;
  rolloutAttentionCount: number;
}

/** Контракт: Platform/SupportNotes/OrganizationSupportNoteDto.cs */
export interface OrganizationSupportNoteDto {
  organizationSupportNoteId: Guid;
  organizationId: Guid;
  authorPlatformAdminId: Guid;
  authorDisplayName: string;
  body: string;
  createdAtUtc: IsoDateTime;
}

/** Контракт: News/OwnerBranchSummaryDto.cs */
export interface OwnerBranchSummaryDto {
  branchId: Guid;
  name: string;
}

/**
 * Anti-fraud §5.6: the owner's daily "watch the staff" digest — per-actor refunds, comps, manual
 * corrections / debt write-offs, and shift discrepancies for a single branch-day.
 *
 * Контракт: Reports/OwnerDailySummaryResultDto.cs
 */
export interface OwnerDailySummaryActorRowDto {
  actorStaffUserId: Guid | null;
  actorDisplayName: string;
  refundCount: number;
  refundTotalMinorUnits: number;
  compCount: number;
  compValueMinorUnits: number;
  manualCorrectionCount: number;
  manualCorrectionTotalMinorUnits: number;
  writeOffCount: number;
  writeOffTotalMinorUnits: number;
  discrepancyShiftCount: number;
  discrepancyTotalMinorUnits: number;
}

/** Контракт: Reports/OwnerDailySummaryResultDto.cs */
export interface OwnerDailySummaryResultDto {
  date: IsoDate;
  currencyCode: string;
  rows: OwnerDailySummaryActorRowDto[];
  totalRefundMinorUnits: number;
  totalCompCount: number;
  totalCompValueMinorUnits: number;
  totalManualCorrectionMinorUnits: number;
  totalWriteOffMinorUnits: number;
  totalDiscrepancyMinorUnits: number;
}

/** Контракт: Packages/PackageDefinitionDto.cs */
export interface PackageDefinitionDto {
  packageDefinitionId: Guid;
  organizationId: Guid;
  branchId: Guid;
  name: string;
  price: MoneyDto;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  isActive: boolean;
  createdAtUtc: IsoDateTime;
}

/**
 * Пакет часов в прайсе клуба: предоплата, за которую час выходит дешевле поминутного тарифа.
 *
 * Контракт: Operator/PackageOptionDto.cs
 */
export interface PackageOptionDto {
  packageDefinitionId: Guid;
  name: string;
  currencyCode: string;
  priceMinorUnits: number;
  /**
   * Оплаченное и бонусное время — две величины одного: игрок покупает часы, а не два
   * отдельных счётчика, и складывать их полагается тому, кто показывает.
   */
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
}

/**
 * Пауза сессии: счётчик времени встаёт, ПК запирается, место остаётся за игроком. Стоять на
 * паузе бесконечно нельзя — филиал задаёт предел, после которого сессия закрывается сама.
 *
 * Контракт: Sessions/PauseSessionRequest.cs
 */
export interface PauseSessionRequest {
  reason: string;
  idempotencyKey: string;
  expectedVersion?: number | null;
}

/** Контракт: Billing/PayDebtRequest.cs */
export interface PayDebtRequest {
  organizationId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

/**
 * One part of a split payment: a method and the amount tendered with it.
 *
 * Контракт: Sessions/PaymentPartDto.cs
 */
export interface PaymentPartDto {
  paymentMethod: string;
  amount: MoneyDto;
}

/** Контракт: Tips/TipContracts.cs */
export interface PayOutShiftTipsRequest {
  idempotencyKey: string;
}

/**
 * A finished visit that has not been reviewed yet — what the app offers to rate.
 * Оценить предлагается один раз и только пока вечер свежий в памяти.
 *
 * Контракт: Reviews/ClubReviewDtos.cs
 */
export interface PendingClubReviewDto {
  sessionId: Guid;
  branchName: string;
  seatName: string;
  endedAtUtc: IsoDateTime;
}

/** Контракт: Shop/PlaceShopOrderRequest.cs */
export interface PlaceShopOrderRequest {
  lines: ShopOrderLineInput[];
  idempotencyKey: string;
}

/** Контракт: Platform/Billing/SubscriptionPlanDto.cs */
export interface PlanFeatureDto {
  featureKey: string;
  name: string;
  isIncluded: boolean;
}

/**
 * Тело отказа по лимиту тарифа. Current и Limit едят
 * клиенту, чтобы отказ читался как «филиалов 2 из 2», а не как «нельзя».
 *
 * Контракт: Platform/Organizations/PlanLimitExceededDto.cs
 */
export interface PlanLimitExceededDto {
  code: string;
  limitName: string;
  limit: number;
  current: number;
  planCode: string;
}

/** Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs */
export interface PlatformAdminInvitationDto {
  invitationId: Guid;
  role: string;
  status: string;
  expiresAtUtc: IsoDateTime;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs */
export interface PlatformAdminListItem {
  platformAdminUserId: Guid;
  userName: string;
  displayName: string;
  role: string;
  isActive: boolean;
  twoFactorEnabled: boolean;
  lastSignInAtUtc: IsoDateTime | null;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Platform/Auth/PlatformAdminRefreshTokenRequest.cs */
export interface PlatformAdminRefreshTokenRequest {
  refreshToken: string;
}

/**
 * First step of sign-in: password alone no longer issues a working session. The caller must present
 * this challenge token to one of the /auth/2fa/* routes (setup or verify) to receive the real
 * PlatformAdminSignInResponse above. The token is short-lived and opaque — it authorizes nothing
 * except those 2FA routes.
 *
 * Контракт: Platform/Auth/PlatformAdminSignInResponse.cs
 */
export interface PlatformAdminSignInChallengeResponse {
  challengeToken: string;
  expiresAtUtc: IsoDateTime;
  twoFactorConfigured: boolean;
}

/** Контракт: Platform/Auth/PlatformAdminSignInRequest.cs */
export interface PlatformAdminSignInRequest {
  userName: string;
  password: string;
}

/** Контракт: Platform/Auth/PlatformAdminSignInResponse.cs */
export interface PlatformAdminSignInResponse {
  platformAdminId: Guid;
  userName: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: IsoDateTime;
  refreshToken: string;
  refreshTokenExpiresAtUtc: IsoDateTime;
  roles: string[];
  permissions: string[];
}

/** Контракт: Platform/Auth/PlatformAdminSignOutRequest.cs */
export interface PlatformAdminSignOutRequest {
  refreshToken: string;
}

/** Контракт: Platform/Analytics/PlatformAnalyticsContracts.cs */
export interface PlatformAnalyticsOverviewDto {
  generatedAtUtc: IsoDateTime;
  currencyCode: string;
  months: AnalyticsMonthDto[];
  currentMrrMinorUnits: number;
  currentPayingClubs: number;
  averageRevenuePerClubMinorUnits: number;
  outstandingMinorUnits: number;
}

/**
 * Анонс глазами платформы: что написано, кому, в каком он состоянии и дошёл ли.
 *
 * Контракт: Platform/Announcements/AnnouncementContracts.cs
 */
export interface PlatformAnnouncementDto {
  announcementId: Guid;
  title: string;
  body: string;
  severity: string;
  showFromUtc: IsoDateTime;
  showUntilUtc: IsoDateTime;
  audienceKind: string;
  audiencePlanCodes: string[];
  audienceOrganizationIds: Guid[];
  status: string;
  publishedAtUtc: IsoDateTime | null;
  emailDispatched: boolean;
  readCount: number;
}

/** Контракт: Platform/Health/PlatformHealthContracts.cs */
export interface PlatformHealthOverviewDto {
  generatedAtUtc: IsoDateTime;
  jobs: JobHealthDto[];
  queues: QueueHealthDto[];
  openIncidents: IncidentDto[];
  recentFailures: QueueFailureDto[];
  /**
   * Хранилище файлов не настроено: логотипы и фото зала загрузить нельзя ни из мастера, ни из
   * панели. Видно здесь, а не при первой попытке загрузки — иначе об этом узнаёт клуб, а не мы.
   */
  mediaStorageConfigured?: boolean;
  /**
   * Резервный канал критических оповещений: SMS уходят только по одобренному шаблону шлюза, и
   * без него канал молчит. Пока это было видно лишь в деталях провалившегося прогона, «почта
   * умерла — придёт SMS» оставалось обещанием, которое некому было проверить.
   */
  alertSmsConfigured?: boolean;
}

/**
 * Сессия человека. Первые восемь полей — дословно те же, что в PlayerSignInResponse,
 * поэтому старый клиент читает этот ответ, не заметив разницы. Отличие одно и оно про модель:
 * клуба может не быть вовсе — так выглядит человек, зарегистрировавшийся дома и ещё никуда не
 * зашедший.
 *
 * Контракт: Identity/RegistrationContracts.cs
 */
export interface PlatformPersonSessionResponse {
  playerAccountId: Guid | null;
  organizationId: Guid | null;
  displayName: string;
  phoneVerified: boolean;
  accessToken: string;
  accessTokenExpiresAtUtc: IsoDateTime;
  refreshToken: string;
  refreshTokenExpiresAtUtc: IsoDateTime;
  platformPersonId: Guid;
  preferredLocale: string | null;
  /** Спрошены ли имя и язык. Показывать ли экран «как вас зовут», решает сервер. */
  profileCompleted: boolean;
}

/** Контракт: Platform/Pulse/PlatformPulseContracts.cs */
export interface PlatformPulseDto {
  generatedAtUtc: IsoDateTime;
  organizations: PulseOrganizationDto[];
}

/**
 * Роль платформы вместе с тем, что она даёт и сколько человек её носят.
 *
 * Контракт: Platform/Auth/PlatformRoleContracts.cs
 */
export interface PlatformRoleDto {
  roleName: string;
  displayName: string;
  description: string;
  isBuiltIn: boolean;
  grantsAllPermissions: boolean;
  permissions: string[];
  adminCount: number;
}

/** Контракт: Platform/Search/PlatformSearchResultDto.cs */
export interface PlatformSearchResultDto {
  kind: string;
  id: Guid;
  title: string;
  context: string;
  href: string;
}

/** Контракт: Platform/Support/PlatformSupportAccessContracts.cs */
export interface PlatformSupportAccessGrantDto {
  grantId: Guid;
  organizationId: Guid;
  reason: string;
  issuedAtUtc: IsoDateTime;
  expiresAtUtc: IsoDateTime;
  revokedAtUtc: IsoDateTime | null;
}

/** Контракт: Platform/Support/PlatformSupportAccessContracts.cs */
export interface PlatformSupportAccessGrantIssue {
  grant: PlatformSupportAccessGrantDto;
  ticket: string;
  adminUrl: string;
}

/**
 * Живой доступ в клуб, каким его видит тот, кто решает — оставить или оборвать. Отдельно от
 * PlatformSupportAccessGrantDto: чтобы понять, кого обрывать, нужно имя выдавшего (Guid ничего не
 * говорит), а чтобы понять, стоит ли, — вошёл ли он вообще. Невостребованный билет означает, что
 * внутрь никто не заходил.
 *
 * Контракт: Platform/Support/PlatformSupportAccessContracts.cs
 */
export interface PlatformSupportAccessGrantListItem {
  grantId: Guid;
  organizationId: Guid;
  reason: string;
  issuedAtUtc: IsoDateTime;
  expiresAtUtc: IsoDateTime;
  platformAdminUserId: Guid;
  platformAdminDisplayName: string;
  enteredAtUtc: IsoDateTime | null;
}

/**
 * The organization-admin shell is built around branches (it cannot render without at least one) —
 * support needs to see the same list a club's own staff would, not just the organization name.
 *
 * Контракт: Platform/Support/PlatformSupportAccessContracts.cs
 */
export interface PlatformSupportSessionBranchDto {
  branchId: Guid;
  name: string;
}

/** Контракт: Platform/Support/PlatformSupportAccessContracts.cs */
export interface PlatformSupportSessionDto {
  sessionToken: string;
  organizationId: Guid;
  organizationName: string;
  reason: string;
  expiresAtUtc: IsoDateTime;
  writableAreas: string[];
  branches: PlatformSupportSessionBranchDto[];
}

/** Контракт: Platform/Updates/PlatformUpdateContracts.cs */
export interface PlatformUpdatePackageDto {
  updatePackageId: Guid;
  component: string;
  version: string;
  channel: string;
  artifactUri: string;
  sha256: string;
  signature: string;
  signatureAlgorithm: string;
  sizeBytes: number;
  state: string;
  releaseNotes: string;
  createdByPlatformAdminUserId: Guid;
  createdAtUtc: IsoDateTime;
  validatedByPlatformAdminUserId: Guid | null;
  validatedAtUtc: IsoDateTime | null;
  retiredAtUtc: IsoDateTime | null;
}

/** Контракт: Platform/Updates/PlatformUpdateContracts.cs */
export interface PlatformUpdateRolloutDto {
  updateRolloutId: Guid;
  updatePackageId: Guid;
  component: string;
  version: string;
  channel: string;
  state: string;
  targetKind: string;
  organizationIds: Guid[];
  branchIds: Guid[];
  deviceIds: Guid[];
  batchPercent: number;
  reason: string;
  createdByPlatformAdminUserId: Guid;
  createdAtUtc: IsoDateTime;
  startsAtUtc: IsoDateTime;
  completedAtUtc: IsoDateTime | null;
}

/** Контракт: Billing/PlayerAccountDto.cs */
export interface PlayerAccountDto {
  playerAccountId: Guid;
  organizationId: Guid;
  homeBranchId: Guid;
  displayName: string;
  phoneNumber: string | null;
  isActive: boolean;
  createdAtUtc: IsoDateTime;
  /**
   * Личность за карточкой — то, чем оператор спрашивает сеть про знакомого ему человека, не
   * диктуя его телефон в запись аудита. Null — нормальный случай: карточку завели на стойке,
   * и никакой личности за ней пока нет.
   */
  platformPersonId?: Guid | null;
  /**
   * Карточка завелась сама, первым действием игрока из приложения. Список клиентов растёт без
   * участия стойки, и это единственное, чем ей объяснить незнакомую строку.
   */
  createdFromApp?: boolean;
}

/**
 * Одно достижение: код, порог и насколько игрок к нему подошёл. Прогресс виден и до
 * получения — иначе список выглядит как набор запертых дверей без замочных скважин.
 *
 * Контракт: Players/PlayerAchievementsDto.cs
 */
export interface PlayerAchievementDto {
  code: string;
  progress: number;
  target: number;
  unlockedAtUtc: IsoDateTime | null;
}

/**
 * Игровой стаж как его видит игрок: уровень, часы за ПК и список достижений.
 * Названия достижений сюда не попадают — только коды: подписи живут в приложении, где у них
 * есть три языка. Сервер, который присылал бы «Ночной житель» строкой, говорил бы с игроком
 * на языке базы данных.
 *
 * Контракт: Players/PlayerAchievementsDto.cs
 */
export interface PlayerAchievementsDto {
  level: number;
  visitCount: number;
  playedMinutes: number;
  /** Сколько минут до следующего уровня; null — уровень последний. */
  minutesToNextLevel: number | null;
  achievements: PlayerAchievementDto[];
}

/**
 * Правила брони этого филиала для этого игрока — то, чем приложение объясняет «так решил клуб».
 * Всё посчитано сервером под конкретного человека: предоплата нужна именно ему, потолок броней
 * именно его. Ни одного поля про других игроков здесь нет и быть не должно — иначе приложение
 * одного клуба становится окном в клиентскую базу.
 * <param name="MaxActiveReservations">
 * Пусто — значит потолка нет: игрок в этом филиале уже свой.
 * </param>
 *
 * Контракт: Reservations/PlayerBookingRulesDto.cs
 */
export interface PlayerBookingRulesDto {
  branchId: Guid;
  /**
   * `auto` — клуб подтверждает сам, `manual` — заявку смотрит администратор, `off` — брони из
   * приложения не принимаются.
   */
  acceptanceMode: string;
  respondWithinMinutes: number;
  prepaymentRequired: boolean;
  activeReservations: number;
  maxActiveReservations: number | null;
  holdSeatAfterStartMinutes: number;
}

/**
 * Ответ на просьбу прислать код: сколько он живёт и когда можно просить следующий. Общий для
 * входа-регистрации и для подтверждения номера в профиле.
 *
 * Контракт: Players/PlayerPhoneVerificationContracts.cs
 */
export interface PlayerCodeSignInStartedResponse {
  expiresInSeconds: number;
  resendAfterSeconds: number;
}

/**
 * Главный экран игрока: три числа кошелька и текущая сессия, если она идёт.
 * HeldBalance — придержанное под брони; из WalletBalance оно
 * уже вычтено, и это ответ на вопрос «а куда делись мои деньги», а не четвёртое место их хранения.
 *
 * Контракт: Players/PlayerDashboardDto.cs
 */
export interface PlayerDashboardDto {
  walletBalance: MoneyDto;
  heldBalance: MoneyDto;
  debtBalance: MoneyDto;
  activeSession: ActiveSessionDto | null;
}

/**
 * Гашение долга игроком с собственного кошелька. Сумма приходит явно, а не «весь долг»: человек
 * вправе закрыть часть, а «весь» на момент нажатия и на момент записи — это разные числа.
 *
 * Контракт: Players/PlayerDebtPaymentContracts.cs
 */
export interface PlayerDebtPaymentRequest {
  amount: MoneyDto;
  idempotencyKey: string;
}

/** Контракт: Players/PlayerOfferContracts.cs */
export interface PlayerDurationOfferDto {
  /** Сколько времени берёт игрок. */
  minutes: number;
  /** Сколько минут будет оплачено: минимум и шаг округления тарифа уже применены. */
  billableMinutes: number;
  endsAtUtc: IsoDateTime;
  amount: MoneyDto;
  balanceAfter: MoneyDto;
  affordable: boolean;
}

/**
 * «Сколько вернётся, если встать сейчас» — до нажатия. Тот же расчёт, что у самого выхода: экран
 * не обещает одну сумму, чтобы вернуть другую.
 *
 * Контракт: Players/PlayerSelfEndSessionContracts.cs
 */
export interface PlayerEndQuoteDto {
  /** Сколько минут будет списано: сыгранное за вычетом пауз, с правилами тарифа. */
  billedMinutes: number;
  refund: MoneyDto;
  packageMinutesReturned: number;
}

/**
 * Чем можно продлить идущую сессию — по тарифу, на котором она началась.
 *
 * Контракт: Players/PlayerOfferContracts.cs
 */
export interface PlayerExtendOffersDto {
  sessionId: Guid;
  balance: MoneyDto;
  options: PlayerDurationOfferDto[];
  /** Одно из PlayerOfferUnavailableReasonNames; пусто, если продлить можно. */
  unavailableReason?: PlayerOfferUnavailableReasonName | null;
}

/**
 * Строка выписки глазами игрока: что случилось с его деньгами и когда.
 * Не то же самое, что LedgerEntryDto у стойки, и не должно им быть: там есть
 * табельный номер проведшего сотрудника и служебная причина вида
 * `reservation_hold:{guid}`. Оператору это нужно — он разбирает спор; игроку это чужая
 * внутренняя кухня, которой в его выписке взяться неоткуда.
 * <param name="EntryType">
 * Что произошло, кодом из LedgerEntryTypeNames. Приложение называет его словами на
 * языке человека — текст с сервера был бы на языке сервера.
 * </param>
 * <param name="QuantitySeconds">
 * Сколько времени принесла или забрала запись: у пакетов и бонусных часов деньги — не вся правда.
 * Ноль у обычных денежных строк.
 * </param>
 * <param name="ReceiptSessionId">
 * Визит, чеком которого объясняется эта строка. Пусто, когда объяснять нечем: у записи нет
 * сессии или по сессии не выбит чек. Без него «Списание за игру −45 с.» — тупик: сумма есть,
 * а из чего она сложилась, видно только в другой вкладке и только по времени на глаз.
 * </param>
 * <param name="HoldReleaseCause">
 * Почему вернулись деньги, придержанные под бронь, — только у строки, которая снимает такое
 * удержание: `seated` (бронь началась, дальше считает сессия), `cancelled`,
 * `rejected`, `request_expired`, `no_show`, `moved`. Без повода строка
 * читалась бы «Отмена операции +15 с.» — и человек не знал бы, что именно отменили.
 * </param>
 * <param name="WalletBalanceAfter">
 * Сколько осталось на кошельке сразу после этой строки. Пусто у строк не про кошелёк — пакетное
 * и бонусное время, долг: они остаток не двигают. Сходится с балансом наверху экрана, потому что
 * удержание под бронь в выписке тоже есть — прятать его значило бы получить остаток, который не
 * складывается из видимых строк.
 * </param>
 *
 * Контракт: Players/PlayerLedgerEntryDto.cs
 */
export interface PlayerLedgerEntryDto {
  ledgerEntryId: Guid;
  entryType: string;
  amount: MoneyDto;
  quantitySeconds: number;
  createdAtUtc: IsoDateTime;
  receiptSessionId?: Guid | null;
  holdReleaseCause?: string | null;
  walletBalanceAfter?: MoneyDto | null;
}

/**
 * Кешбэк игрока: сколько накоплено и по каким правилам начисляется.
 * Кешбэк — не баллы: он приходит на кошелёк обычными деньгами, и тратится так же.
 *
 * Контракт: Loyalty/PlayerLoyaltyDto.cs
 */
export interface PlayerLoyaltyDto {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
  sessionEnabled: boolean;
  sessionPercentBasisPoints: number;
  totalEarned: MoneyDto;
  recent: CashbackEntryDto[];
}

/**
 * Новость или акция клуба.
 *
 * Контракт: News/PlayerNewsItemDto.cs
 */
export interface PlayerNewsItemDto {
  id: Guid;
  title: string;
  body: string;
  imageUrl: string | null;
  publishedAtUtc: IsoDateTime;
}

/**
 * Уведомление, каким его видит игрок в приложении.
 * Берётся из той же очереди, что и пуш: отдельного хранилища у центра уведомлений нет и не нужно —
 * текст уже отрисован и сохранён там, где сообщение ставилось в отправку. Поэтому список
 * показывает и то, что до телефона не доехало: пуш, потерянный из-за выключенных уведомлений или
 * переустановленного приложения, до сих пор было невозможно прочитать нигде.
 *
 * Контракт: Notifications/PlayerNotificationContracts.cs
 */
export interface PlayerNotificationDto {
  notificationId: Guid;
  /**
   * Служебное имя события (`player.order_ready` и подобные). Приложение по нему ставит значок —
   * показывать его человеку незачем.
   */
  templateKey: string;
  subject: string;
  body: string;
  branchId: Guid | null;
  createdAtUtc: IsoDateTime;
  isUnread: boolean;
}

/** Контракт: Notifications/PlayerNotificationContracts.cs */
export interface PlayerNotificationsDto {
  notifications: PlayerNotificationDto[];
  unreadCount: number;
}

/**
 * Купленный пакет с остатком времени.
 *
 * Контракт: Packages/PlayerPackageDto.cs
 */
export interface PlayerPackageDto {
  playerPackageId: Guid;
  packageDefinitionId: Guid;
  playerAccountId: Guid;
  name: string;
  purchasedPrice: MoneyDto;
  includedSeconds: number;
  bonusSeconds: number;
  remainingIncludedSeconds: number;
  remainingBonusSeconds: number;
  purchasedAtUtc: IsoDateTime;
  expiresAtUtc: IsoDateTime | null;
}

/** Контракт: Players/PlayerOfferContracts.cs */
export interface PlayerPackageOfferDto {
  playerPackageId: Guid;
  name: string;
  remainingMinutes: number;
  expiresAtUtc: IsoDateTime | null;
}

/** Контракт: Players/PlayerPhoneVerificationContracts.cs */
export interface PlayerPhoneConfirmedResponse {
  phone: string;
}

/** Контракт: Players/PlayerPhoneVerificationContracts.cs */
export interface PlayerPhoneConfirmRequest {
  code: string;
}

/**
 * Asks for a code to be sent to Phone — the number the player claims.
 *
 * Контракт: Players/PlayerPhoneVerificationContracts.cs
 */
export interface PlayerPhoneStartVerificationRequest {
  phone: string;
}

/** Контракт: Players/PlayerPhoneVerificationContracts.cs */
export interface PlayerPhoneVerificationStartedResponse {
  expiresInSeconds: number;
  resendAfterSeconds: number;
}

/**
 * Профиль игрока: как его зовут, чем он подписан и что он разрешил присылать.
 * HomeBranchId is what lets the app ask for the club's price list at all: the catalog endpoints are
 * per-branch, and until now the player had no way to learn which branch the account belongs to —
 * the server resolved it silently on every write. The name comes along so the app can say where it
 * is booking without a second round-trip.
 *
 * Контракт: Players/PlayerProfileDto.cs
 */
export interface PlayerProfileDto {
  playerAccountId: Guid;
  displayName: string;
  phoneNumber: string | null;
  phoneVerified: boolean;
  /** Пусто — игрок не выбирал язык, и письма идут на языке клуба. */
  preferredLocale: string | null;
  marketingOptIn: boolean;
  homeBranchId?: Guid | null;
  homeBranchName?: string | null;
}

/**
 * Покупка в баре: когда, что и на сколько.
 *
 * Контракт: Players/PlayerPurchaseDto.cs
 */
export interface PlayerPurchaseDto {
  posSaleId: Guid;
  createdAtUtc: IsoDateTime;
  totalMinorUnits: number;
  currencyCode: string;
  lines: PlayerPurchaseLineDto[];
}

/**
 * Строка покупки: что, сколько и на какую сумму.
 *
 * Контракт: Players/PlayerPurchaseLineDto.cs
 */
export interface PlayerPurchaseLineDto {
  productName: string;
  quantity: number;
  unitPriceMinorUnits: number;
  lineTotalMinorUnits: number;
}

/**
 * Экран «Приведи друга» глазами игрока: свой код, условия и что уже вышло.
 * Суммы и условия приходят с сервера, а не зашиты в приложение: их назначает клуб, и каждый
 * назначает свои.
 *
 * Контракт: Loyalty/ReferralContracts.cs
 */
export interface PlayerReferralDto {
  /** Клуб платит за приглашения. false — экран честно говорит, что программы нет. */
  enabled: boolean;
  code: string | null;
  referrerBonusMinorUnits: number;
  inviteeBonusMinorUnits: number;
  minimumTopUpMinorUnits: number;
  currencyCode: string;
  invitedCount: number;
  rewardedCount: number;
  earnedMinorUnits: number;
  /** Игрок сам пришёл по чужому коду — второй раз назвать код нельзя. */
  hasClaimedCode: boolean;
  /** Назвать код ещё можно: приглашение не использовано и окно не закрылось. */
  canClaimCode: boolean;
}

/** Контракт: Players/PlayerRefreshRequest.cs */
export interface PlayerRefreshRequest {
  refreshToken: string;
}

/**
 * Единственный факт, который сеть сообщает клубу о незнакомом госте: можно ли ему доверять.
 * Полей ровно четыре, и это граница приватности, а не текущая версия модели. Ни названия чужих
 * клубов, ни даты визитов, ни суммы, ни филиалы, ни тарифы сюда не добавляются: «скрыто в UI» —
 * не защита, операторское приложение ходит в тот же API, что и curl. Состав закреплён
 * рефлексивным тестом, чтобы пятое поле не появилось «на минутку».
 * <param name="NetworkVisits">Завершённых визитов во всей сети — точное число из суточного снимка.</param>
 * <param name="NetworkNoShows">Броней, на которые человек не приехал, — из того же снимка.</param>
 * <param name="NetworkBanned">Закрыт ли человеку вход в сеть решением платформы. Читается вживую: запрет не ждёт суток.</param>
 * <param name="CalculatedAtUtc">На какой момент посчитан снимок. Значение общее для всей сети, а не личное: по личному времени пересчёта можно было бы вычислить, когда человек играл.</param>
 *
 * Контракт: Players/PlayerReputationDto.cs
 */
export interface PlayerReputationDto {
  networkVisits: number;
  networkNoShows: number;
  networkBanned: boolean;
  calculatedAtUtc: IsoDateTime;
}

/**
 * Спрос репутации по точному номеру. Номер едет телом, а не в адресе: адреса оседают в логах
 * прокси и в истории браузера, а это чужой телефон.
 *
 * Контракт: Players/PlayerReputationDto.cs
 */
export interface PlayerReputationLookupRequest {
  phoneNumber: string;
}

/**
 * Player-facing reservation view — no staff-only fields (no CustomerName separate from
 * context, no CreatedByStaffUserId, no UpdatedBy, no ZoneName leak).
 * The tariff and the estimated cost are the player's own choice priced by the server: the app must
 * never re-derive the amount from the price list, because the minimum-billable and rounding rules
 * live in billing and would drift the moment either side changed.
 *
 * Контракт: Reservations/PlayerReservationDto.cs
 */
export interface PlayerReservationDto {
  reservationId: Guid;
  seatId: Guid | null;
  /** Пусто — клуб ещё не назначил конкретное место. */
  seatName: string | null;
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  /**
   * Отменить можно то, что ещё не состоялось: `pending` и `confirmed`. Отменённую или уже
   * отыгранную бронь трогать нечего — кнопка там только сбивает с толку. Одно из ReservationStateNames.
   */
  state: ReservationStateName;
  note: string | null;
  tariffVersionId?: Guid | null;
  /**
   * Название выбранного тарифа и стоимость, посчитанная сервером при брони. Пусто — бронь
   * завели на стойке, там же её и посчитают.
   */
  tariffName?: string | null;
  estimatedCostMinorUnits?: number | null;
  currencyCode?: string | null;
  /**
   * Бронь на компанию: у всех мест группы он общий. Без него приложение показало бы компанию из
   * четырёх человек четырьмя одинаковыми строками, между которыми не видно разницы.
   */
  reservationGroupId?: Guid | null;
  /**
   * Докуда клуб обещал ответить на заявку — по нему в приложении идёт обратный отсчёт. У
   * подтверждённой брони его нет: отвечать больше не на что.
   */
  respondByUtc?: IsoDateTime | null;
  /**
   * Почему клуб отказал. Код — чтобы приложение сказало это на языке игрока; слова — то, что
   * администратор добавил от себя. Без них отказ снова стал бы молчаливым исчезновением брони.
   */
  rejectReasonCode?: string | null;
  rejectReasonNote?: string | null;
}

/**
 * Что получилось из групповой брони: сама группа и её брони. Отдельного состояния у группы нет —
 * оно складывается из состояний броней, а дублировать его значит однажды разойтись с ними.
 *
 * Контракт: Reservations/CreatePlayerReservationGroupRequest.cs
 */
export interface PlayerReservationGroupDto {
  reservationGroupId: Guid;
  reservations: PlayerReservationDto[];
  /**
   * Сумма по всей компании — она же замороженная. Пусто — бронь без тарифа, её посчитают
   * на стойке.
   */
  totalEstimatedCostMinorUnits: number | null;
  currencyCode: string | null;
}

/** Контракт: Operator/PlayerSearchResultDto.cs */
export interface PlayerSearchResultDto {
  playerAccountId: Guid;
  displayName: string;
  phoneNumber: string | null;
  walletBalanceMinorUnits: number;
  debtBalanceMinorUnits: number;
  activePackageCount: number;
  isActive: boolean;
  createdAtUtc: IsoDateTime;
  lastActivityAtUtc: IsoDateTime | null;
  activePackageName: string | null;
  activePackageRemainingMinutes: number;
  /**
   * См. Billing.PlayerAccountDto: те же два ответа на «кто это и откуда он взялся»,
   * потому что в списке клиентов они нужны раньше, чем в карточке.
   */
  platformPersonId?: Guid | null;
  createdFromApp?: boolean;
}

/**
 * Место в зале глазами игрока: как называется, где стоит и свободно ли.
 * Идентификатора устройства здесь больше нет: сессия начинается кодом с монитора, а не выбором из
 * списка. Список остался витриной — «есть ли вообще куда сесть», — и занятое место в нём тоже
 * нужно: «PC-07 занят» это ответ, а исчезнувшее место выглядит сбоем приложения.
 *
 * Контракт: Players/PlayerSeatDto.cs
 */
export interface PlayerSeatDto {
  seatId: Guid;
  seatName: string;
  zoneName: string;
  isAvailable: boolean;
  /**
   * Почему занято: "session" — за ним играют, "reservation" — забронировано на ближайшее время,
   * "offline" — компьютер не на связи. null, когда место свободно.
   */
  unavailableReason: string | null;
}

/**
 * Игрок сам заканчивает свою сессию и освобождает место.
 *
 * Контракт: Players/PlayerSelfEndSessionContracts.cs
 */
export interface PlayerSelfEndSessionRequest {
  idempotencyKey: string;
}

/**
 * Чем закончился ранний выход. Возврат показывается игроку явно: «я встал раньше» и «мне
 * вернули столько-то» — это одно событие, и узнавать вторую половину из истории кошелька
 * человек не должен.
 * <param name="BilledMinutes">
 * Сколько минут списано. Это не фактические минуты, а тарифицируемые: минимальная
 * длительность и шаг округления тарифа уже применены, ровно как у стойки.
 * </param>
 *
 * Контракт: Players/PlayerSelfEndSessionContracts.cs
 */
export interface PlayerSelfEndSessionResponse {
  billedMinutes: number;
  /** Сколько вернулось на кошелёк. Ноль — значит время было отыграно полностью. */
  refunded: MoneyDto;
  /** Сколько минут вернулось в пакет — у сессии, начатой по пакету. */
  packageMinutesReturned?: number;
}

/** Контракт: Players/PlayerSelfExtendRequest.cs */
export interface PlayerSelfExtendRequest {
  additionalMinutes: number;
  idempotencyKey: string;
}

/**
 * Человек садится сам — назвав код с монитора той машины, перед которой стоит.
 * Раньше здесь был идентификатор устройства, и приложение брало его из списка мест. Это значило,
 * что занять свободный ПК можно было не приходя в клуб: сервер видел «игрок назвал устройство» и
 * доказательства присутствия не имел никакого. Код видно только с экрана — он и есть
 * доказательство, и живёт минуты, чтобы снятая на телефон цифра никому не пригодилась.
 * Оболочка ПК кода не шлёт: её токен привязан к машине и сам доказывает, где человек сидит
 * (спека оболочки, §5.3). Код ей и не годится — вход по QR его гасит.
 *
 * Контракт: Players/PlayerSelfStartRequest.cs
 */
export interface PlayerSelfStartRequest {
  /** Код с монитора; пусто — только с токеном, привязанным к ПК. */
  seatingCode: string;
  tariffRuleVersionId: string;
  durationMinutes: number;
  idempotencyKey: string;
  /**
   * Сесть по своему пакету: минуты списываются из пакета, а не с кошелька. Тариф при этом не
   * нужен — у пакета своя цена, уже заплаченная; минуты — сколько взять из остатка.
   */
  playerPackageId?: Guid | null;
}

/** Контракт: Shell/PlayerShellStateDto.cs */
export interface PlayerShellStateDto {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  state: string;
  sessionId: Guid | null;
  leaseExpiresAtUtc: IsoDateTime | null;
  remainingSeconds: number | null;
  isOnline: boolean;
  isGraceMode: boolean;
  warningThresholdSeconds: number;
  message: string;
  launcherApps: LauncherAppDto[];
  locale?: string;
  warningKind?: string;
  branding?: ShellBrandingDto | null;
  /**
   * Код с этого монитора: человек набирает его в приложении и садится именно за эту машину.
   * Пусто, когда за ПК уже играют или связи с сервером нет — показать старый код значит
   * позвать человека к машине, которую сервер ему не отдаст.
   */
  seatingCode?: string | null;
  /** Когда код сменится: оболочка показывает, сколько ему осталось, а просроченный не рисует. */
  seatingCodeExpiresAtUtc?: IsoDateTime | null;
  /**
   * Время платформы в момент, когда агент собрал это состояние. Срок аренды — тоже время
   * платформы, а часы ПК могут от неё отставать: поправку хост считает по этому полю.
   */
  observedAtUtc?: IsoDateTime | null;
  /** Когда агент в последний раз достучался до платформы. Пусто — ни разу с запуска службы. */
  lastContactUtc?: IsoDateTime | null;
  /** Адрес платформы из настроек агента: хосту больше не нужно угадывать, куда ходить. */
  apiBaseUrl?: string | null;
  /** Место этого ПК — «ПК 07»: первое, что читается на экране, и видно от стойки. */
  seatLabel?: string | null;
  /** Зона места — «Общий зал». */
  zoneName?: string | null;
  /**
   * Чья сессия идёт: none, guest или player. Вошедшему не владельцу экран говорит «эта сессия
   * не ваша» и ничего не открывает.
   */
  sessionOwnerKind?: string | null;
  /** Счёт владельца сессии — только у player. */
  sessionOwnerPlayerAccountId?: Guid | null;
  /** Права организации по тарифу: без player_shop нет вкладки «Бар», без loyalty — кэшбека. */
  features?: string[] | null;
  /**
   * Обслуживание: с какого момента и кто его включил — для полосы «Включено из Панели AFK4.net
   * в 14:05 · Шерзод». Пусто вне обслуживания; имя пусто, если его включила поддержка без имени.
   */
  maintenanceSinceUtc?: IsoDateTime | null;
  maintenanceByName?: string | null;
  /**
   * Окна, которые хост закрывает, едва они появятся (профиль защиты, §6.3). Служба в сессии 0
   * окон игрока не видит, поэтому правила едут хосту. В обслуживании список пуст.
   */
  blockedWindows?: BlockedWindowRuleDto[] | null;
  /** Правила клуба из настроек ПК: кнопка на экране свободного ПК их открывает. */
  clubRules?: string | null;
  /**
   * Свободный ПК выключится от простоя в это время: экран показывает отсчёт, движение мыши его
   * отменяет. null — выключение не назначено.
   */
  idleShutdownAtUtc?: IsoDateTime | null;
  /** Витрина свободного ПК: карточки клуба с картинками из кэша ПК. Пусто — оформление клуба. */
  showcase?: ShowcaseCardDto[] | null;
}

/**
 * Заявка на вход и что с ней стало: приложение показывает «Вы вошли на ПК 07».
 *
 * Контракт: Players/PlayerSignInClaimContracts.cs
 */
export interface PlayerSignInClaimDto {
  claimId: Guid;
  /** Одно из PlayerSignInClaimStatusNames. */
  status: PlayerSignInClaimStatusName;
  expiresAtUtc: IsoDateTime;
  /** Имя места: «ПК 07». Пусто, если ПК не привязан к месту. */
  seatLabel: string | null;
}

/**
 * ПК должен забрать заявку на вход: человек отсканировал QR с его монитора. Приходит в группу
 * устройства по SignalR и, на случай обрыва, в ответе на сердцебиение.
 *
 * Контракт: Devices/PlayerSignInClaimDeviceContracts.cs
 */
export interface PlayerSignInClaimedDto {
  claimId: Guid;
  expiresAtUtc: IsoDateTime;
}

/**
 * Самопосадка за игровой ПК: клуб, номер и сетевой PIN. Поле называется `Password` с тех
 * времён, когда PIN был клубным паролем, — переименование сломало бы установленные в поле
 * оболочки ради одного слова.
 * <param name="BranchId">
 * Филиал, у ПК которого стоит человек. Нужен ровно в одном случае: клуб с несколькими филиалами,
 * а счёта у человека в нём ещё нет — гадать филиал за него нельзя, в отчётах это выглядело бы как
 * два разных гостя. Клиенты, которые филиала не называют, работают как работали.
 * </param>
 *
 * Контракт: Players/PlayerSignInRequest.cs
 */
export interface PlayerSignInRequest {
  organizationId: Guid;
  phoneNumber: string;
  password: string;
  branchId?: Guid | null;
}

/** Контракт: Players/PlayerSignInResponse.cs */
export interface PlayerSignInResponse {
  playerAccountId: Guid;
  organizationId: Guid;
  displayName: string;
  phoneVerified: boolean;
  accessToken: string;
  accessTokenExpiresAtUtc: IsoDateTime;
  refreshToken: string;
  refreshTokenExpiresAtUtc: IsoDateTime;
}

/** Контракт: Players/PlayerSignOutRequest.cs */
export interface PlayerSignOutRequest {
  refreshToken: string;
}

/**
 * Что можно купить, сев за этот ПК, — одним запросом, с готовыми суммами (спека оболочки,
 * §5.5). Клиент цену не считает: суммы считает тот же расчёт, что и списание, иначе экран
 * однажды пообещал бы одну цифру, а касса списала бы другую.
 *
 * Контракт: Players/PlayerOfferContracts.cs
 */
export interface PlayerStartOffersDto {
  seatLabel: string | null;
  zoneName: string | null;
  /** Часовой пояс клуба (IANA): «до скольки» показывается по времени клуба, а не телефона. */
  timeZone: string;
  balance: MoneyDto;
  tariffs: PlayerTariffOfferDto[];
  packages: PlayerPackageOfferDto[];
}

/** Контракт: Players/PlayerOfferContracts.cs */
export interface PlayerTariffOfferDto {
  tariffVersionId: Guid;
  /** То, что передаётся в старт как TariffRuleVersionId. */
  tariffRuleVersionId: string;
  name: string;
  pricePerHour: MoneyDto;
  appliesNow: boolean;
  /** Когда тариф откроется, если сейчас он не действует; вариантов у такого тарифа нет. */
  startsAtUtc: IsoDateTime | null;
  options: PlayerDurationOfferDto[];
}

/**
 * Можно ли оставить чаевые за этот визит — и сколько.
 *
 * Контракт: Tips/TipContracts.cs
 */
export interface PlayerTipOfferDto {
  available: boolean;
  /** Одно из TipUnavailableReasonNames; пусто, если можно. */
  unavailableReason: TipUnavailableReasonName | null;
  presets: MoneyDto[];
  balance: MoneyDto;
  /** Имя администратора смены — первое слово: «Чаевые Шерзоду». */
  recipientName: string | null;
  /** Чаевые, уже оставленные за этот визит. */
  given: MoneyDto | null;
}

/** Контракт: Tips/TipContracts.cs */
export interface PlayerTipRequest {
  amount: MoneyDto;
  idempotencyKey: string;
}

/** Контракт: Tips/TipContracts.cs */
export interface PlayerTipResponse {
  amount: MoneyDto;
  balanceAfter: MoneyDto;
  recipientName: string | null;
}

/**
 * Заявка на пополнение кошелька: игрок просит зачислить сумму, клуб подтверждает.
 *
 * Контракт: Players/PlayerTopUpIntentDto.cs
 */
export interface PlayerTopUpIntentDto {
  paymentIntentId: Guid;
  amountMinorUnits: number;
  currencyCode: string;
  state: string;
  purpose: string;
  /** `counter` — деньги вносят на стойке, `eskhata` — платят из приложения банка. */
  method: string;
  createdAtUtc: IsoDateTime;
  fulfilledAtUtc: IsoDateTime | null;
  isExpired: boolean;
  /** Страница оплаты в браузере — запасной путь для телефона без приложения банка. */
  payUrl?: string | null;
  comment?: string | null;
  gatewayExpiresAtUtc?: IsoDateTime | null;
  qr?: string | null;
  /** Ссылка, открывающая приложение банка. Пусто, если платят на стойке или банк её не дал. */
  deepLink?: string | null;
}

/**
 * Player requests a wallet top-up.
 * CurrencyCode defaults to "TJS" when null or blank.
 * Method ∈ { "counter", "dcgate", "eskhata" }; null/blank → "counter" (operator-confirmed at the desk).
 * BranchId — филиал, в который человек придёт. Он нужен только в первом действии в клубе, где
 * счёта ещё нет: у сети с несколькими филиалами сервер не гадает, куда записать счёт. Поле
 * необязательное — клуб с одним филиалом называть нечего, а у человека со счётом филиал уже
 * известен, и присланный не переписывает его.
 * IdempotencyKey — ключ одной попытки. Необязателен: установленные приложения его не шлют, и
 * без него всё работает как раньше. С ним повтор после обрыва находит уже созданное, а не
 * создаёт второе.
 *
 * Контракт: Players/PlayerTopUpIntentRequest.cs
 */
export interface PlayerTopUpIntentRequest {
  amountMinorUnits: number;
  currencyCode: string | null;
  method?: string | null;
  branchId?: Guid | null;
  idempotencyKey?: string | null;
}

/**
 * Чем клуб принимает деньги прямо сейчас. Стойка — всегда: это наличные в кассе. Онлайн держится
 * на двух вещах сразу — тариф платформы разрешает и у клуба заведён мерчант банка, — и приложение
 * обязано узнать это до того, как предложит человеку кнопку.
 *
 * Контракт: Players/PlayerTopUpIntentDto.cs
 */
export interface PlayerTopUpMethodsDto {
  counter: boolean;
  online: boolean;
}

/**
 * Событие клуба — турнир, ночь игры, чемпионат зала — глазами игрока: только то, по чему решают,
 * идти ли. Черновиков здесь не бывает, а вместо списка участников — сколько мест осталось и
 * записан ли он сам.
 *
 * Контракт: Tournaments/TournamentDtos.cs
 */
export interface PlayerTournamentDto {
  tournamentId: Guid;
  branchId: Guid;
  branchName: string;
  title: string;
  description: string;
  /** Игра словами клуба («Dota 2», «FIFA»). Пусто — клуб не уточнил. */
  discipline: string;
  startsAtUtc: IsoDateTime;
  /**
   * Взнос за участие. 0 — бесплатно, и это обычный случай для вечера, которым клуб просто
   * заполняет будний день.
   */
  entryFee: MoneyDto;
  /** Сколько человек берут. 0 — без ограничения. */
  capacity: number;
  registeredCount: number;
  isRegistered: boolean;
  /** Одно из TournamentStateNames. */
  state: TournamentStateName;
  /** Почему клуб отменил. Пусто, пока событие в силе. */
  cancelReason: string;
}

/**
 * Прошедший визит: где сидел, сколько пробыл и на сколько наиграл.
 *
 * Контракт: Players/PlayerVisitDto.cs
 */
export interface PlayerVisitDto {
  sessionId: Guid;
  seatId: Guid;
  seatName: string;
  startedAtUtc: IsoDateTime;
  /** Пусто — визит ещё не закрыт. */
  endedAtUtc: IsoDateTime | null;
  timeChargeMinorUnits: number;
  posTotalMinorUnits: number;
  grandTotalMinorUnits: number;
  currencyCode: string;
  hasReceipt: boolean;
}

/**
 * Чек визита: время, покупки и итог.
 *
 * Контракт: Players/PlayerVisitReceiptDto.cs
 */
export interface PlayerVisitReceiptDto {
  receiptNumber: string;
  createdAtUtc: IsoDateTime;
  sessionId: Guid;
  seatName: string;
  startedAtUtc: IsoDateTime;
  endedAtUtc: IsoDateTime | null;
  timeChargeMinorUnits: number;
  posLines: PlayerPurchaseLineDto[];
  posTotalMinorUnits: number;
  grandTotalMinorUnits: number;
  currencyCode: string;
}

/** Контракт: Pos/PosProductCategoryDto.cs */
export interface PosProductCategoryDto {
  categoryId: Guid;
  organizationId: Guid;
  branchId: Guid;
  name: string;
  isActive: boolean;
  sortOrder: number;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Pos/PosProductDto.cs */
export interface PosProductDto {
  productId: Guid;
  organizationId: Guid;
  branchId: Guid;
  categoryId: Guid;
  name: string;
  sku: string;
  price: MoneyDto;
  trackStock: boolean;
  allowNegativeStock: boolean;
  isActive: boolean;
  stockOnHand: number;
  createdAtUtc: IsoDateTime;
  reorderThreshold?: number;
  availableInShell?: boolean;
  avgCostMinorUnits?: number;
  barcodes?: string[] | null;
  featuredOnPcs?: boolean;
  imageUrl?: string | null;
}

/** Контракт: Pos/PosSaleDto.cs */
export interface PosSaleDto {
  posSaleId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  state: string;
  lines: PosSaleLineDto[];
  total: MoneyDto;
  createdByStaffUserId: Guid;
  createdAtUtc: IsoDateTime;
  paidAtUtc: IsoDateTime | null;
  refundedAtUtc: IsoDateTime | null;
  voidedAtUtc: IsoDateTime | null;
  latestReceipt?: ReceiptDto | null;
  playerAccountId?: Guid | null;
  shopOrderId?: Guid | null;
  /**
   * Чем за чек заплатили. Пусто у черновика — его ещё не оплачивали.
   * До этого поля стойка рисовала в карточке чека секцию «Оплаты», которая всегда оставалась
   * пустой: она читала поле, которого в контракте не было. Строки оплат в базе лежали всё это
   * время — их просто не клали в ответ.
   */
  payments?: PaymentPartDto[] | null;
}

/** Контракт: Pos/PosSaleLineDto.cs */
export interface PosSaleLineDto {
  productId: Guid;
  productName: string;
  quantity: number;
  unitPrice: MoneyDto;
  lineTotal: MoneyDto;
}

/** Контракт: Inventory/ProductBarcodeDto.cs */
export interface ProductBarcodeDto {
  barcodeId: Guid;
  productId: Guid;
  code: string;
  isPrimary: boolean;
}

/**
 * Строка отчёта: пункт, исход (ProtectionItemStatusNames) и подробность для разбора.
 *
 * Контракт: Devices/ProtectionProfileContracts.cs
 */
export interface ProtectionItemReportDto {
  /** Одно из ProtectionItemNames. */
  item: ProtectionItemName;
  /** Одно из ProtectionItemStatusNames. */
  status: ProtectionItemStatusName;
  detail: string | null;
}

/**
 * Профиль защиты ПК филиала (спека оболочки, §6.3): что агент запрещает на игровом ПК. Версия
 * растёт с каждым сохранением и едет в сердцебиении — по её смене агент перечитывает профиль.
 * Версия 0 — клуб профиль не настраивал, действует только постоянная база киоска.
 *
 * Контракт: Devices/ProtectionProfileContracts.cs
 */
export interface ProtectionProfileDto {
  version: number;
  /** Флешки и внешние диски — запрет Windows на все съёмные накопители. */
  blockRemovableStorage: boolean;
  /** Скачивание в Chrome и Edge. */
  blockBrowserDownloads: boolean;
  /** Режим инкогнито в Chrome и InPrivate в Edge. */
  blockBrowserIncognito: boolean;
  /** Окно «Выполнить» (Win+R). */
  disableRunDialog: boolean;
  /** Буквы дисков, скрытых в Проводнике. Это не запрет: программа откроет диск по пути. */
  hiddenDrives: string[];
  /** Адреса и шаблоны, которые Chrome и Edge не открывают (формат URLBlocklist). */
  urlBlocklist: string[];
  /** Окна, которые оболочка закрывает, едва они появятся. */
  blockedWindows: BlockedWindowRuleDto[];
  /** Что стереть после сессии игрока (§6.4). Каждый пункт — из SessionTraceNames. */
  clearAfterSession: string[];
  /** Выключить свободный ПК, за которым столько минут никого нет; null — не выключать. */
  idleShutdownMinutes?: number | null;
  /** Правила клуба на экране ПК — текст клуба как есть, на его языке. */
  clubRules?: string | null;
}

/**
 * DetailValue carries the single numeric figure behind an alert, meaning depends on Kind:
 * minutes since the last agent heartbeat for AgentSilent, minutes since the shift was
 * opened for ShiftNotClosed, count of devices that reported a failed install for
 * RolloutFailed. It is null for alert kinds/situations with no such figure
 * (PaymentOverdue always; AgentSilent when the device has never reported a heartbeat at
 * all; RolloutFailed when the rollout was flagged manually before any device reported a
 * failure). Clients must never render a raw backend string as user-facing alert text —
 * every kind has a translated label, and any parameterized detail is built client-side
 * from DetailValue, not shipped as pre-rendered prose.
 *
 * Контракт: Platform/Pulse/PlatformPulseContracts.cs
 */
export interface PulseAlertDto {
  kind: string;
  level: string;
  detailValue: number | null;
}

/** Контракт: Platform/Pulse/PlatformPulseContracts.cs */
export interface PulseClubDto {
  branchId: Guid;
  name: string;
  city: string;
  devicesOnline: number;
  devicesTotal: number;
  seatsOccupied: number;
  seatsTotal: number;
  shiftOpen: boolean;
  shiftOpenedAtUtc: IsoDateTime | null;
  lastHeartbeatAtUtc: IsoDateTime | null;
  alerts: PulseAlertDto[];
}

/** Контракт: Platform/Pulse/PlatformPulseContracts.cs */
export interface PulseOrganizationDto {
  organizationId: Guid;
  name: string;
  status: string;
  planCode: string;
  subscriptionStatus: string;
  alertLevel: string;
  outstandingMinorUnits: number;
  currencyCode: string;
  alerts: PulseAlertDto[];
  clubs: PulseClubDto[];
}

/**
 * The player buying a package for themselves. Only the idempotency key comes from the client: the
 * organization comes from the authenticated player and the branch and package from the route, so a
 * caller cannot buy in someone else's name or out of another club's price list.
 *
 * Контракт: Players/PurchasePackageFromAppRequest.cs
 */
export interface PurchasePackageFromAppRequest {
  idempotencyKey: string;
}

/** Контракт: Billing/PurchasePackageRequest.cs */
export interface PurchasePackageRequest {
  organizationId: Guid;
  packageDefinitionId: Guid;
  idempotencyKey: string;
}

/**
 * Стирание подтверждается коротким именем клуба, набранным руками. Кнопка «да, я уверен» здесь
 * недостаточна: ошибиться клубом в списке легко, набрать чужой slug по памяти — нет.
 *
 * Контракт: Platform/Organizations/OffboardingContracts.cs
 */
export interface PurgeOrganizationRequest {
  slug: string;
}

/**
 * Сколько строк унесло стирание — по группам, для записи в аудит и показа человеку.
 *
 * Контракт: Platform/Organizations/OffboardingContracts.cs
 */
export interface PurgeOrganizationResultDto {
  players: number;
  staffUsers: number;
  sessions: number;
  sales: number;
  devices: number;
  branches: number;
  clubAuditRecords: number;
}

/**
 * Одна провалившаяся строка очереди. Без причины провала счётчик «провалено» не диагностируем:
 * видно, что письма не уходят, и не видно почему. Адрес маскирован — домен для разбора важен,
 * полный адрес человека нет.
 *
 * Контракт: Platform/Health/PlatformHealthContracts.cs
 */
export interface QueueFailureDto {
  queueName: string;
  failedAtUtc: IsoDateTime | null;
  kind: string;
  recipientMasked: string;
  attemptCount: number;
  lastError: string | null;
}

/** Контракт: Platform/Health/PlatformHealthContracts.cs */
export interface QueueHealthDto {
  queueName: string;
  pendingCount: number;
  failedCount: number;
  stuckCount: number;
}

/** Контракт: Receipts/ReceiptDto.cs */
export interface ReceiptDto {
  receiptId: Guid;
  organizationId: Guid;
  branchId: Guid;
  posSaleId: Guid | null;
  receiptNumber: string;
  receiptType: string;
  total: MoneyDto;
  createdAtUtc: IsoDateTime;
  sessionId?: Guid | null;
  shopOrderId?: Guid | null;
}

/** Контракт: Shifts/RecordCashMovementRequest.cs */
export interface RecordCashMovementRequest {
  organizationId: Guid;
  movementType: string;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

/** Контракт: Platform/Support/PlatformSupportAccessContracts.cs */
export interface RedeemSupportAccessTicketRequest {
  ticket: string;
}

/**
 * Настройки «приведи друга» глазами клуба.
 *
 * Контракт: Loyalty/ReferralContracts.cs
 */
export interface ReferralSettingsDto {
  enabled: boolean;
  referrerBonusMinorUnits: number;
  inviteeBonusMinorUnits: number;
  minimumTopUpMinorUnits: number;
  claimWindowDays: number;
  maxRewardedPerReferrer: number;
}

/** Контракт: Billing/RefundLedgerEntryRequest.cs */
export interface RefundLedgerEntryRequest {
  organizationId: Guid;
  ledgerEntryId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

/** Контракт: Pos/RefundPosSaleRequest.cs */
export interface RefundPosSaleRequest {
  organizationId: Guid;
  reason: string;
  idempotencyKey: string;
}

/**
 * Регистрация телефона игрока для пушей. Токен выдаёт FCM, платформа — `android` или
 * `ios`, локаль — язык приложения на этом устройстве.
 * Язык здесь не спрашивается: пуш уходит на языке аккаунта (PlayerAccount.PreferredLocale),
 * который человек выбирает сам в профиле. Установленные приложения поле ещё шлют — лишнее поле
 * в теле сервер молча пропускает.
 *
 * Контракт: Notifications/NotificationContracts.cs
 */
export interface RegisterPlayerDeviceRequest {
  pushToken: string | null;
  platform: string | null;
}

/** Контракт: Identity/RegistrationContracts.cs */
export interface RegistrationConfirmRequest {
  phoneNumber: string;
  code: string;
}

/**
 * Ответ на просьбу прислать код. Он одинаков для знакомого и незнакомого номера — ни одного поля,
 * по которому можно отличить одно от другого, здесь нет и быть не должно.
 *
 * Контракт: Identity/RegistrationContracts.cs
 */
export interface RegistrationStartedResponse {
  expiresInSeconds: number;
  resendAfterSeconds: number;
}

/**
 * Просьба прислать код на номер. Клуб здесь не называется: человек заводит себя сам.
 *
 * Контракт: Identity/RegistrationContracts.cs
 */
export interface RegistrationStartRequest {
  phoneNumber: string;
}

/**
 * «Не в этот раз» — сказанное клубом заявке, которую ещё не принимали.
 * Отдельно от отмены намеренно: игрок ничего не отменял, деньги ему возвращаются целиком при
 * любых настройках филиала, и в его сетевые числа этот отказ не попадает.
 * <param name="ReasonCode">Код из RejectReasonCodes.</param>
 * <param name="Note">
 * Пояснение администратора своими словами. Обязательно при RejectReasonCodes.Other:
 * код «своими словами» без слов — тот же пустой отказ, от которого уходили.
 * </param>
 *
 * Контракт: Reservations/RejectReservationRequest.cs
 */
export interface RejectReservationRequest {
  organizationId: Guid;
  reasonCode: string;
  note?: string | null;
  expectedVersion?: number | null;
}

/** Контракт: Devices/RenameDeviceRequest.cs */
export interface RenameDeviceRequest {
  organizationId: Guid;
  displayName: string;
}

/**
 * Порядок игр в библиотеке: все игры филиала в новом порядке.
 *
 * Контракт: Games/GameLibraryContracts.cs
 */
export interface ReorderBranchGamesRequest {
  organizationId: Guid;
  branchGameIds: Guid[];
}

/**
 * Новый порядок категорий филиала: весь список целиком, сверху вниз.
 * Список, а не пара «категория + номер»: порядок — свойство набора, и присланный целиком он не
 * оставляет места расхождению. Пара «id + номер» на каждое перетаскивание порождала бы дыры и
 * совпадения в нумерации, которые потом нечем разрешить.
 *
 * Контракт: Pos/ReorderProductCategoriesRequest.cs
 */
export interface ReorderProductCategoriesRequest {
  organizationId: Guid;
  categoryIds: Guid[];
}

/**
 * A configured report schedule.
 *
 * Контракт: Reports/ReportScheduleContracts.cs
 */
export interface ReportScheduleDto {
  reportScheduleId: Guid;
  organizationId: Guid;
  branchId: Guid;
  reportType: string;
  frequency: string;
  isActive: boolean;
  nextRunUtc: IsoDateTime;
  lastRunUtc: IsoDateTime | null;
  createdAtUtc: IsoDateTime;
}

/**
 * Что случилось с бронью — стойке, прямо сейчас.
 * Полоса заявок до сих пор обновлялась опросом: администратор видел чужое решение через
 * несколько секунд, а два администратора на разных машинах какое-то время видели разное. Хуже
 * того, решения принимают и таймеры — срок ответа истекает сам, — и о них узнать было неоткуда,
 * кроме следующего опроса.
 * Форма повторяет `SessionLifecycleChangedDto` намеренно: у операторского экрана уже есть
 * приёмник таких событий с отбором по филиалу, и второй способ доставлять то же самое разошёлся
 * бы с первым на первом же исправлении.
 * <param name="Kind">Что именно произошло — ReservationChangeKinds.</param>
 * <param name="State">Состояние брони после изменения.</param>
 *
 * Контракт: Reservations/ReservationChangedDto.cs
 */
export interface ReservationChangedDto {
  organizationId: Guid;
  branchId: Guid;
  reservationId: Guid;
  seatId: Guid | null;
  kind: string;
  state: string;
  version: number;
  startsAtUtc: IsoDateTime;
  observedAtUtc: IsoDateTime;
}

/** Контракт: Reservations/ReservationDto.cs */
export interface ReservationDto {
  reservationId: Guid;
  organizationId: Guid;
  branchId: Guid;
  playerAccountId: Guid | null;
  seatId: Guid | null;
  seatName: string | null;
  zoneName: string | null;
  customerName: string;
  phoneNumber: string | null;
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  durationMinutes: number;
  state: string;
  source: string;
  note: string;
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  cancelledAtUtc: IsoDateTime | null;
  cancelReason: string;
  reservationGroupId: Guid | null;
  version?: number;
  startedSessionId?: Guid | null;
  /**
   * Billing choice carried by a self-service booking, and the price the server computed for it.
   * Null for desk-created bookings — those are still priced when the player is seated.
   */
  tariffVersionId?: Guid | null;
  tariffName?: string | null;
  estimatedCostMinorUnits?: number | null;
  currencyCode?: string | null;
  /**
   * Докуда клуб обещал ответить на заявку и когда ответил. Срок есть только у заявки, которая
   * ждёт решения стойки; подтверждённую бронь по таймеру никто не снимает.
   */
  respondByUtc?: IsoDateTime | null;
  confirmedAtUtc?: IsoDateTime | null;
  /**
   * Личность за счётом, с которого пришла заявка. Клуб, решающий её судьбу, спрашивает сеть
   * этим идентификатором, а не телефоном гостя. У заявки, записанной на стойке одним номером,
   * счёта ещё нет — и называть некого.
   */
  platformPersonId?: Guid | null;
  /**
   * Чем кончилась бронь, в которую человек не приехал: когда это признали и сколько филиал
   * оставил себе по своей же настройке. Сумма пустая, а не нулевая, когда не удерживали вовсе:
   * ноль читался бы как «удержали нисколько», хотя удержания не было.
   */
  noShowAtUtc?: IsoDateTime | null;
  retainedAmountMinorUnits?: number | null;
  /**
   * Отказ клуба: когда, по какой причине из справочника и что администратор добавил словами.
   * Причину читает игрок — поэтому код, а не текст: текст на языке стойки ему не поможет.
   */
  rejectedAtUtc?: IsoDateTime | null;
  rejectReasonCode?: string | null;
  rejectReasonNote?: string | null;
}

/** Контракт: Reservations/ReservationGroup.cs */
export interface ReservationGroupConflictDto {
  seatId: Guid;
  reason: string;
}

/**
 * Group-create outcome. On success ReservationGroupId and Reservations
 * are populated and Conflicts is empty; on conflict it is the other way round.
 *
 * Контракт: Reservations/ReservationGroup.cs
 */
export interface ReservationGroupResultDto {
  reservationGroupId: Guid | null;
  reservations: ReservationDto[];
  conflicts: ReservationGroupConflictDto[];
}

/**
 * BillableMinutes can exceed the booked span — that is the point of showing it: an hour booked on a
 * tariff with a two-hour minimum is billed as two hours, and the player sees why before booking.
 * AmountMinorUnits — сумма за ВСЮ бронь, включая все её места. Отдавать цену одного места и
 * оставлять умножение приложению значило бы показывать не то число, которое будет заморожено.
 *
 * Контракт: Reservations/ReservationQuoteContracts.cs
 */
export interface ReservationQuoteDto {
  tariffVersionId: Guid;
  tariffName: string;
  requestedMinutes: number;
  billableMinutes: number;
  amountMinorUnits: number;
  currencyCode: string;
  seatCount?: number;
}

/**
 * What a booking will cost before the player commits to it.
 * The price is asked of the server rather than computed in the app on purpose: the minimum-billable
 * floor and the rounding increment are billing rules, and a second implementation in the client
 * would quietly disagree with the charge the moment either side changed.
 * SeatCount умножает цену на число мест на стороне сервера. Умножение сложным не выглядит, но
 * показанная и списанная суммы обязаны приходить из одного места: считаясь порознь, они однажды
 * разойдутся — а это та цена, на которую игрок согласился.
 *
 * Контракт: Reservations/ReservationQuoteContracts.cs
 */
export interface ReservationQuoteRequest {
  tariffVersionId: Guid;
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  seatCount?: number;
}

/** Контракт: Reservations/ReservationDto.cs */
export interface ReservationSearchResultDto {
  reservations: ReservationDto[];
  limit: number;
}

/**
 * Места филиала, на которые бронь в окне [StartsAtUtc, EndsAtUtc)
 * встанет без конфликта — тем же правилом, которым сервер эту бронь примет или отклонит.
 * Окно возвращается вместе с ответом: панель спрашивает его для конкретной брони, и ответ на
 * окно, которое уже сменилось, не должен тихо стать списком для нового.
 *
 * Контракт: Reservations/ReservationSeatAvailabilityDto.cs
 */
export interface ReservationSeatAvailabilityDto {
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  freeSeatIds: Guid[];
}

/** Контракт: Identity/ResetStaffUserPasswordRequest.cs */
export interface ResetStaffUserPasswordRequest {
  organizationId: Guid;
  newPassword: string;
}

/** Контракт: Platform/Operator/ResolveOperatorConnectionRequest.cs */
export interface ResolveOperatorConnectionRequest {
  organizationSlug: string | null;
  branchSlug: string | null;
  setupCode: string | null;
}

/** Контракт: Platform/Operator/ResolveOperatorConnectionResponse.cs */
export interface ResolveOperatorConnectionResponse {
  organizationId: Guid;
  organizationSlug: string;
  organizationName: string;
  organizationStatus: string;
  organizationStatusReason: string | null;
  branchId: Guid;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  source: string;
}

/**
 * Снятие паузы: ПК отпирается, а конец фиксированной сессии сдвигается на простой.
 *
 * Контракт: Sessions/PauseSessionRequest.cs
 */
export interface ResumeSessionRequest {
  reason: string;
  idempotencyKey: string;
  expectedVersion?: number | null;
}

/** Контракт: Devices/RevokeDeviceCredentialResponse.cs */
export interface RevokeDeviceCredentialResponse {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  credentialId: Guid;
  revokedAtUtc: IsoDateTime;
}

/** Контракт: Identity/AccountActivation/RevokeOrganizationOwnerInviteRequest.cs */
export interface RevokeOrganizationOwnerInviteRequest {
  reason: string;
}

/** Контракт: Devices/RotateDeviceCredentialResponse.cs */
export interface RotateDeviceCredentialResponse {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  credentialId: Guid;
  credentialSecret: string;
  rotatedAtUtc: IsoDateTime;
}

/** Контракт: Reports/SalesReportResultDto.cs */
export interface SalesReportResultDto {
  rows: SalesReportRowDto[];
  limit: number;
  grossSalesTotal: MoneyDto;
  refundsTotal: MoneyDto;
  netSalesTotal: MoneyDto;
  grossCostOfGoodsTotal: MoneyDto;
  refundedCostOfGoodsTotal: MoneyDto;
  netCostOfGoodsTotal: MoneyDto;
}

/** Контракт: Reports/SalesReportRowDto.cs */
export interface SalesReportRowDto {
  posSaleId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  createdByStaffUserId: Guid;
  state: string;
  total: MoneyDto;
  paidAmount: MoneyDto;
  refundAmount: MoneyDto;
  lineCount: number;
  itemQuantity: number;
  createdAtUtc: IsoDateTime;
  paidAtUtc: IsoDateTime | null;
  refundedAtUtc: IsoDateTime | null;
  voidedAtUtc: IsoDateTime | null;
  grossCostOfGoods: MoneyDto;
  refundedCostOfGoods: MoneyDto;
  netCostOfGoods: MoneyDto;
}

/** Контракт: Platform/Announcements/AnnouncementContracts.cs */
export interface SavePlatformAnnouncementRequest {
  title: string;
  body: string;
  severity: string;
  showFromUtc: IsoDateTime;
  showUntilUtc: IsoDateTime;
  audienceKind: string;
  audiencePlanCodes: string[];
  audienceOrganizationIds: Guid[];
}

/** Контракт: Layout/SeatDto.cs */
export interface SeatDto {
  seatId: Guid;
  organizationId: Guid;
  branchId: Guid;
  zoneId: Guid;
  name: string;
  sortOrder: number;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Reservations/ReservationRequests.cs */
export interface SeatReservationRequest {
  organizationId: Guid;
  expectedVersion: number;
}

/** Контракт: FloorMap/SeatStatusDto.cs */
export interface SeatStatusDto {
  seatId: Guid;
  seatName: string;
  zoneId: Guid;
  zoneName: string;
  sortOrder: number;
  /** Одно из SeatStateNames. */
  state: SeatStateName;
  deviceId: Guid | null;
  deviceName: string | null;
  isDeviceOnline: boolean | null;
  isDeviceLocked: boolean | null;
  lastHeartbeatAtUtc: IsoDateTime | null;
  agentVersion: string | null;
  shellVersion: string | null;
  activeSessionId: Guid | null;
  remainingSeconds: number | null;
  /**
   * Live accrued time cost for an open-tab session (count-up). Null for fixed
   * sessions (which expose RemainingSeconds instead) and unbilled guests.
   */
  accruedCostMinorUnits?: number | null;
  currencyCode?: string | null;
  /**
   * Optimistic-concurrency version of the active session; the operator echoes it back as
   * ExpectedVersion on a seat mutation so a stale view loses the race with a 409.
   */
  sessionVersion?: number | null;
  /**
   * Who is on the seat right now: the active session's player display name. Null for a
   * guest session with no account, or a free seat.
   */
  playerDisplayName?: string | null;
  /**
   * The tariff the active session bills against. Null for guest/package sessions that
   * carry no named tariff, or a free seat.
   */
  tariffName?: string | null;
  /** When the active session started (UTC) — lets the operator show real elapsed time. */
  sessionStartedAtUtc?: IsoDateTime | null;
  /**
   * Когда с этого места позвали оператора. Null — не зовут. Время, а не флаг: стойке важно,
   * кто ждёт дольше.
   */
  assistanceRequestedAtUtc?: IsoDateTime | null;
  /**
   * С какого момента ПК на обслуживании по решению клуба. Null — ПК в зале. Отдельно от State:
   * «обслуживание» на карте бывает и у неподтверждённого ПК, а вернуть в зал можно только того,
   * кого туда увели.
   */
  maintenanceSinceUtc?: IsoDateTime | null;
  /** Место с консолью без агента: сессию ведёт администратор, команд ПК у места нет. */
  isConsole?: boolean;
  /** ПК сверх предела бесплатного тарифа: новые сессии на нём не запускаются, идущая доживает. */
  isOutsidePlan?: boolean;
}

/**
 * Агент просит выдать себе новый ключ, предъявив действующий заголовком. Организация и филиал
 * в теле — те же, что в сердцебиении: сервер сверяет ключ именно с этой машиной, а не просто
 * «с каким-нибудь».
 *
 * Контракт: Devices/SelfRotateDeviceCredentialRequest.cs
 */
export interface SelfRotateDeviceCredentialRequest {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
}

/**
 * Позвать в друзья по номеру — тому, который человек и так знает.
 *
 * Контракт: Friends/FriendDtos.cs
 */
export interface SendFriendRequestRequest {
  phoneNumber: string;
}

/**
 * Проверка доставки почты: письмо уходит боевым путём, а причина отказа возвращается сразу.
 * Раньше единственным способом проверить почту было послать кому-то настоящее приглашение и
 * гадать по счётчику «провалено» без текста ошибки.
 *
 * Контракт: Platform/Health/PlatformHealthContracts.cs
 */
export interface SendTestEmailRequest {
  email: string;
}

/** Контракт: Platform/Health/PlatformHealthContracts.cs */
export interface SendTestEmailResultDto {
  delivered: boolean;
  error: string | null;
}

/**
 * Read-only preview of a session checkout: the bill breakdown the operator needs
 * to enter split payments (time charge + attached POS = grand total), the billable
 * seconds for the "Наиграно" display, and — when the session has a player — the
 * wallet balance so a wallet part can be auto-suggested. No state is changed.
 *
 * Контракт: Sessions/SessionCheckoutQuoteResponse.cs
 */
export interface SessionCheckoutQuoteResponse {
  sessionId: Guid;
  timeCharge: MoneyDto;
  posTotal: MoneyDto;
  grandTotal: MoneyDto;
  billableSeconds: number;
  playerAccountId: Guid | null;
  walletBalance: MoneyDto | null;
}

/**
 * Settle a session in one action: pay the unified bill (time charge + attached
 * POS sales) with one or more PaymentPartDto parts, then lock the PC.
 *
 * Контракт: Sessions/SessionCheckoutRequest.cs
 */
export interface SessionCheckoutRequest {
  organizationId: Guid;
  payments: PaymentPartDto[];
  idempotencyKey: string;
  expectedVersion?: number | null;
}

/**
 * Result of a unified session checkout: the bill breakdown, the recorded payment
 * parts, the single receipt covering time + POS, the ending session, and the lock
 * command dispatched to the device.
 *
 * Контракт: Sessions/SessionCheckoutResponse.cs
 */
export interface SessionCheckoutResponse {
  idempotencyKey: string;
  sessionId: Guid;
  timeCharge: MoneyDto;
  posTotal: MoneyDto;
  grandTotal: MoneyDto;
  payments: PaymentPartDto[];
  receipt: ReceiptDto;
  session: SessionDto;
  deviceCommands: DeviceCommandDto[];
}

/** Контракт: Sessions/SessionCommandResponse.cs */
export interface SessionCommandResponse {
  idempotencyKey: string;
  session: SessionDto;
  deviceCommands: DeviceCommandDto[];
  /** Anti-fraud §5.4: the assessed value of a comp (free) session; null for non-comp starts. */
  compValueMinorUnits?: number | null;
}

/** Контракт: Sessions/SessionDto.cs */
export interface SessionDto {
  sessionId: Guid;
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  deviceId: Guid;
  state: string;
  tariffRuleVersionId: string;
  startedAtUtc: IsoDateTime | null;
  endsAtUtc: IsoDateTime | null;
  endedAtUtc: IsoDateTime | null;
  remainingSeconds: number | null;
  currentLease: SessionLeaseDto | null;
  /** Optimistic-concurrency version the client echoes back as ExpectedVersion on the next mutation. */
  version?: number;
}

/** Контракт: Sessions/SessionLeaseDto.cs */
export interface SessionLeaseDto {
  sessionId: Guid;
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  deviceId: Guid;
  state: string;
  sequence: number;
  issuedAtUtc: IsoDateTime;
  expiresAtUtc: IsoDateTime;
  signatureAlgorithm: string;
  signature: string;
}

/**
 * A session-lifecycle change broadcast over the DeviceHub branch group so operator clients can
 * patch the floor map and apply a dashboard delta without polling. Pushes are hints; the client
 * reconciles against an authoritative reload and ignores an event whose Version is
 * not newer than the one it already applied for that session.
 *
 * Контракт: Sessions/SessionLifecycleChangedDto.cs
 */
export interface SessionLifecycleChangedDto {
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  sessionId: Guid;
  kind: string;
  state: string;
  version: number;
  startedAtUtc: IsoDateTime | null;
  endsAtUtc: IsoDateTime | null;
  observedAtUtc: IsoDateTime;
  accruedCostMinorUnits?: number | null;
  currencyCode?: string | null;
}

/** Контракт: Sessions/SessionReconciliationResponse.cs */
export interface SessionReconciliationResponse {
  action: string;
  reason: string;
  sessionId: Guid | null;
  lease: SessionLeaseDto | null;
}

/**
 * A started game session projected onto the booking timeline: when it began, its scheduled end
 * (fixed/prepaid) and its actual end (null while still running). The operator UI derives the bar —
 * open tab (no scheduled, no actual end) renders open-ended; otherwise bounded.
 *
 * Контракт: Sessions/SessionTimelineDto.cs
 */
export interface SessionTimelineItemDto {
  sessionId: Guid;
  seatId: Guid;
  seatName: string;
  zoneId: Guid;
  zoneName: string;
  state: string;
  playerAccountId: Guid | null;
  playerDisplayName: string | null;
  tariffName: string | null;
  startedAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime | null;
  endedAtUtc: IsoDateTime | null;
}

/** Контракт: Sessions/SessionTimelineDto.cs */
export interface SessionTimelineResult {
  sessions: SessionTimelineItemDto[];
}

/** Контракт: Ads/AdContracts.cs */
export interface SetAdCampaignStateRequest {
  /** Одно из AdCampaignStateNames */
  state: AdCampaignStateName;
}

/**
 * Какие ПК работают на бесплатном тарифе — не больше предела; пустой список снимает выбор.
 *
 * Контракт: Platform/Billing/ClubPlanContracts.cs
 */
export interface SetClubPlanDevicesRequest {
  deviceIds: Guid[];
}

/**
 * Постановка ручного исключения для клуба. Причина обязательна.
 *
 * Контракт: Platform/Features/FeatureContracts.cs
 */
export interface SetFeatureOverrideRequest {
  isEnabled: boolean;
  reason: string;
}

/**
 * Новый сетевой PIN. Старый здесь не спрашивается намеренно: человек уже вошёл в приложение, а
 * потребовать старое значило бы запереть выход ровно тому, кто его забыл. Именно это и делает
 * приложение единственным местом, где PIN задают, — ни одной SMS на это не тратится.
 *
 * Контракт: Identity/PinContracts.cs
 */
export interface SetMyPinRequest {
  pin: string;
}

/**
 * Причина обязательна: запрет без неё некому объяснить и не на каком основании снять.
 *
 * Контракт: Platform/People/NetworkPeopleContracts.cs
 */
export interface SetNetworkBanRequest {
  reason: string;
}

/** Контракт: Players/SetPlayerActiveStateRequest.cs */
export interface SetPlayerActiveStateRequest {
  organizationId: Guid;
  isActive: boolean;
}

/** Контракт: Pos/SettlePosSaleRequest.cs */
export interface SettlePosSaleRequest {
  organizationId: Guid;
  payments: PaymentPartDto[];
  note: string;
  idempotencyKey: string;
}

/** Контракт: Shell/ShellBridgeContracts.cs */
export interface ShellAuthSignInRequest {
  phone: string;
  pin: string;
}

/**
 * Кто вошёл на этом ПК. Токены страница не видит: их держит хост.
 *
 * Контракт: Shell/ShellBridgeContracts.cs
 */
export interface ShellAuthStateDto {
  signedIn: boolean;
  displayName?: string | null;
  playerAccountId?: Guid | null;
}

/** Контракт: Shell/ShellBrandingDto.cs */
export interface ShellBrandingDto {
  clubName: string;
  logoUrl: string | null;
  accentColor: string | null;
}

/** Контракт: Shell/ShellBridgeContracts.cs */
export interface ShellGameForegroundDto {
  active: boolean;
}

/** Контракт: Shell/ShellBridgeContracts.cs */
export interface ShellLaunchRequest {
  appId: string;
}

/**
 * Команда клуба, которую исполняет хост: у агента нет ни окна, ни аккаунта игрока.
 *
 * Контракт: Shell/ShellPipeMessage.cs
 */
export interface ShellPipeCommandDto {
  commandId: Guid;
  /** DeviceCommandTypeNames.SignOut или DeviceCommandTypeNames.Message. */
  type: DeviceCommandTypeName;
  /** Текст сообщения; только у message. */
  text?: string | null;
}

/** Контракт: Shell/ShellPipeMessage.cs */
export interface ShellPipeHelloDto {
  protocol: number;
  hostVersion: string;
  /**
   * Сессия Windows, в которой живёт хост. Агент сверяет её с консольной: хост из чужой
   * сессии получать состояние этого ПК не должен.
   */
  sessionId: number;
}

/**
 * Один кадр канала агент ↔ хост. Заполнено ровно то поле, которое называет Type:
 * так кадр читается одним типом, без второго разбора по виду сообщения.
 *
 * Контракт: Shell/ShellPipeMessage.cs
 */
export interface ShellPipeMessage {
  /** Одно из ShellPipeMessageTypeNames. */
  type: ShellPipeMessageTypeName;
  hello?: ShellPipeHelloDto | null;
  state?: PlayerShellStateDto | null;
  request?: ShellPipeRequestDto | null;
  reply?: ShellPipeReplyDto | null;
  /** Почему агент попрощался; только у bye. */
  reason?: string | null;
  command?: ShellPipeCommandDto | null;
  /**
   * Игрок вошёл на этом ПК — номером и ПИН-кодом или по QR с телефона. Токены привязаны к ПК;
   * хост держит их в памяти и странице не отдаёт.
   */
  auth?: PlatformPersonSessionResponse | null;
}

/** Контракт: Shell/ShellPipeMessage.cs */
export interface ShellPipeReplyDto {
  requestId: Guid;
  ok: boolean;
  /** Одно из ShellPipeErrorCodeNames; пусто при успехе. */
  errorCode?: ShellPipeErrorCodeName | null;
  message?: string | null;
}

/** Контракт: Shell/ShellPipeMessage.cs */
export interface ShellPipeRequestDto {
  requestId: Guid;
  /** Одно из ShellPipeRequestTypeNames. */
  type: ShellPipeRequestTypeName;
  payload: Record<string, string>;
}

/** Контракт: Shell/ShellBridgeContracts.cs */
export interface ShellSetLayoutRequest {
  /** Одно из ShellKeyboardLayoutNames. */
  layout: ShellKeyboardLayoutName;
}

/** Контракт: Shell/ShellBridgeContracts.cs */
export interface ShellSetMicMutedRequest {
  micMuted: boolean;
}

/** Контракт: Shell/ShellBridgeContracts.cs */
export interface ShellSetVolumeRequest {
  /** 0–100. */
  volume: number;
}

/**
 * Показ карточки витрины: какая и сколько миллисекунд стояла на экране.
 *
 * Контракт: Shell/ShellBridgeContracts.cs
 */
export interface ShellShowcaseImpressionDto {
  cardId: string;
  shownMs: number;
}

/**
 * Всё, что хост знает к моменту, когда страница загрузилась.
 *
 * Контракт: Shell/ShellBridgeContracts.cs
 */
export interface ShellSnapshotDto {
  /** Пусто — агент ещё не прислал состояния: экран говорит «подключаемся к ПК». */
  state: PlayerShellStateDto | null;
  auth: ShellAuthStateDto;
  system?: ShellSystemStateDto | null;
}

/**
 * Звук, микрофон и раскладка ПК. Пусто — у ПК этого нет или Windows не ответила (нет
 * микрофона, звуковая карта отключена): страница прячет кнопку, а не показывает выдуманное.
 *
 * Контракт: Shell/ShellBridgeContracts.cs
 */
export interface ShellSystemStateDto {
  /** 0–100. */
  volume: number | null;
  micMuted: boolean | null;
  /** Раскладка клавиатуры — одно из ShellKeyboardLayoutNames. */
  layout: ShellKeyboardLayoutName | null;
}

/** Контракт: Shifts/ShiftDto.cs */
export interface ShiftDto {
  shiftId: Guid;
  organizationId: Guid;
  branchId: Guid;
  openedByStaffUserId: Guid;
  closedByStaffUserId: Guid | null;
  state: string;
  startingCash: MoneyDto;
  countedCash: MoneyDto | null;
  expectedCash: MoneyDto | null;
  difference: MoneyDto | null;
  openingNote: string;
  closingNote: string;
  openedAtUtc: IsoDateTime;
  closedAtUtc: IsoDateTime | null;
  managerSignOffStaffUserId?: Guid | null;
  signOffReason?: string | null;
}

/** Контракт: Reports/ShiftReportResultDto.cs */
export interface ShiftReportResultDto {
  rows: ShiftReportRowDto[];
  limit: number;
}

/** Контракт: Reports/ShiftReportRowDto.cs */
export interface ShiftReportRowDto {
  shiftId: Guid;
  organizationId: Guid;
  branchId: Guid;
  openedByStaffUserId: Guid;
  closedByStaffUserId: Guid | null;
  state: string;
  startingCash: MoneyDto;
  cashMovementsTotal: MoneyDto;
  posCashPaymentsTotal: MoneyDto;
  posRefundsTotal: MoneyDto;
  billingCashImpactTotal: MoneyDto;
  expectedCash: MoneyDto;
  countedCash: MoneyDto | null;
  difference: MoneyDto | null;
  openedAtUtc: IsoDateTime;
  closedAtUtc: IsoDateTime | null;
}

/** Контракт: Shifts/ShiftRevenueDto.cs */
export interface ShiftRevenueDto {
  shiftId: Guid;
  organizationId: Guid;
  branchId: Guid;
  openedByStaffUserId: Guid;
  closedByStaffUserId: Guid | null;
  state: string;
  earned: EarnedBreakdownDto;
  inflow: InflowBreakdownDto;
  cash: CashReconciliationDto;
  openedAtUtc: IsoDateTime;
  closedAtUtc: IsoDateTime | null;
}

/** Контракт: Shifts/ShiftRevenueDto.cs */
export interface ShiftRevenueListDto {
  shifts: ShiftRevenueDto[];
  limit: number;
}

/** Контракт: Shifts/ShiftSummaryDto.cs */
export interface ShiftSummaryDto {
  shiftId: Guid;
  startingCash: MoneyDto;
  cashMovementsTotal: MoneyDto;
  posCashPaymentsTotal: MoneyDto;
  posRefundsTotal: MoneyDto;
  expectedCash: MoneyDto;
  countedCash: MoneyDto;
  difference: MoneyDto;
}

/** Контракт: Tips/TipContracts.cs */
export interface ShiftTipDto {
  ledgerEntryId: Guid;
  amount: MoneyDto;
  seatLabel: string | null;
  createdAtUtc: IsoDateTime;
  /** Возвращены игроку — в сумму смены не входят. */
  reversed: boolean;
}

/**
 * Чаевые смены для Панели. Имени игрока нет: администратору важны сумма и ПК.
 *
 * Контракт: Tips/TipContracts.cs
 */
export interface ShiftTipsDto {
  shiftId: Guid;
  recipientStaffUserId: Guid;
  recipientName: string;
  total: MoneyDto;
  tips: ShiftTipDto[];
  /** Уже выдано из кассы за эту смену: выдать ту же сумму второй раз нельзя. */
  paidOut?: MoneyDto | null;
}

/**
 * Позиция меню бара: что можно заказать к месту прямо во время сессии.
 *
 * Контракт: Shop/ShopCatalogItemDto.cs
 */
export interface ShopCatalogItemDto {
  productId: Guid;
  name: string;
  sku: string;
  price: MoneyDto;
  /**
   * Остаток на складе филиала. Сервер уже убрал отсюда то, что кончилось и не продаётся в минус,
   * поэтому число нужно только чтобы предупредить о последних штуках.
   */
  stockOnHand: number;
}

/**
 * Заказ к месту и его судьба: оформлен, готовится, принесли, отменён.
 *
 * Контракт: Shop/ShopOrderDto.cs
 */
export interface ShopOrderDto {
  id: Guid;
  branchId: Guid;
  seatId: Guid;
  playerAccountId: Guid;
  playerDisplayName: string;
  /**
   * `placed` и `accepted` — заказ ещё в работе, за ним есть смысл следить и его ещё можно
   * отменить. После «принесли» отменять нечего. Одно из ShopOrderStatusNames.
   */
  status: ShopOrderStatusName;
  total: MoneyDto;
  lines: ShopOrderLineDto[];
  /**
   * Когда заказ оформили. Нужно списку прошлых заказов: без времени «принесли» и «отменён»
   * сливаются в кучу одинаковых строк.
   */
  placedAtUtc: IsoDateTime;
  acceptedAtUtc: IsoDateTime | null;
  deliveredAtUtc: IsoDateTime | null;
  cancelledAtUtc: IsoDateTime | null;
  version: number;
  posSaleId?: Guid | null;
  /**
   * Имя места на стене — «PC-12»: туда и несут заказ. Без него лента на стойке могла показать
   * только идентификатор места, который вслух никто не произносит.
   */
  seatName?: string | null;
}

/**
 * Строка заказа: что и сколько.
 *
 * Контракт: Shop/ShopOrderLineDto.cs
 */
export interface ShopOrderLineDto {
  productId: Guid;
  name: string;
  unitPrice: MoneyDto;
  quantity: number;
  lineTotal: MoneyDto;
}

/** Контракт: Shop/ShopOrderLineInput.cs */
export interface ShopOrderLineInput {
  productId: Guid;
  quantity: number;
}

/** Контракт: Showcase/ShowcaseContracts.cs */
export interface ShowcaseCardDto {
  /**
   * Стабильный ключ карточки: «news:…», «tariff:…». По нему агент узнаёт карточку между
   * обновлениями, а экран не перезапускает показ, когда список не изменился.
   */
  cardId: string;
  /** Одно из ShowcaseCardKindNames */
  kind: ShowcaseCardKindName;
  /** У «Пакетов» заголовок пуст: его пишет оболочка на языке экрана. */
  title: string;
  body?: string | null;
  /** Короткая строка рядом с видом карточки: дисциплина турнира («Dota 2»). */
  subtitle?: string | null;
  /**
   * Адрес картинки. С сервера — адрес в медиа-хранилище, на экран — адрес в кэше ПК: чужих
   * адресов экран не получает.
   */
  imageUrl?: string | null;
  /** Цена: час тарифа, товар, взнос турнира. Пусто — цены у карточки нет (или взнос бесплатный). */
  price?: MoneyDto | null;
  /** Часы тарифа по времени клуба, «22:00–06:00». Пусто — круглые сутки. */
  timeWindow?: string | null;
  /** Начало турнира. */
  startsAtUtc?: IsoDateTime | null;
  /** Строки карточки «Пакеты». */
  packages?: ShowcasePackageLineDto[] | null;
  /** Рекламодатель — только у рекламы: экран пишет «Реклама · {рекламодатель}». */
  advertiser?: string | null;
  /** Реклама: русский вариант — второй строкой под таджикским. */
  secondaryTitle?: string | null;
  secondaryBody?: string | null;
  /**
   * Реклама с продажей на расстоянии: наименование, ИНН и адрес продавца (закон о рекламе,
   * ст. 14(1)). Подписи к ним экран пишет на своём языке.
   */
  seller?: ShowcaseSellerDto | null;
  /** Реклама: пометка «подлежит обязательной сертификации» (ст. 5). */
  requiresCertification?: boolean;
  /** Реклама с ценой или условиями: до какого дня действует предложение (ст. 26). */
  offerUntilUtc?: IsoDateTime | null;
}

/** Контракт: Ads/AdContracts.cs */
export interface ShowcaseImpressionDto {
  cardId: string;
  /** День показа по UTC, «2026-09-25». */
  day: string;
  impressions: number;
  shownMs: number;
}

/** Контракт: Showcase/ShowcaseContracts.cs */
export interface ShowcasePackageLineDto {
  name: string;
  price: MoneyDto;
  minutes: number;
}

/** Контракт: Showcase/ShowcaseContracts.cs */
export interface ShowcaseSellerDto {
  legalName: string;
  taxId: string;
  address: string;
}

/**
 * Сотрудник организации, которого нет в этом филиале: его можно добавить сюда ролями. Филиалы, где
 * он уже работает, — названиями; пустой список значит, что назначений у него не осталось вовсе и
 * войти в Панель ему некуда, пока его не вернут в филиал.
 *
 * Контракт: Identity/StaffBranchCandidateDto.cs
 */
export interface StaffBranchCandidateDto {
  staffUserId: Guid;
  userName: string;
  displayName: string;
  isActive: boolean;
  branchNames: string[];
}

/**
 * Requests an SMS password-reset code to a staff account's verified phone.
 *
 * Контракт: Identity/StaffForgotPasswordByPhoneRequest.cs
 */
export interface StaffForgotPasswordByPhoneRequest {
  phoneNumber: string;
}

/**
 * Self-service password-reset request. Resolved by username or contact email.
 *
 * Контракт: Identity/StaffForgotPasswordRequest.cs
 */
export interface StaffForgotPasswordRequest {
  userNameOrEmail: string;
}

/**
 * The created staff invite; Code is returned once so an admin can also share it out of band.
 *
 * Контракт: Identity/StaffInviteDto.cs
 */
export interface StaffInviteDto {
  staffInviteId: Guid;
  code: string;
  expiresAtUtc: IsoDateTime;
}

/** Контракт: Identity/StaffPhoneVerificationContracts.cs */
export interface StaffPhoneConfirmedResponse {
  phone: string;
}

/** Контракт: Identity/StaffPhoneVerificationContracts.cs */
export interface StaffPhoneConfirmRequest {
  code: string;
}

/** Контракт: Identity/StaffPhoneVerificationContracts.cs */
export interface StaffPhoneStartVerificationRequest {
  phone: string;
}

/**
 * Current staff member's phone state (self-read). Both null until a phone is set/verified.
 * Phone is in E.164 display form (e.g. "+992937380070").
 *
 * Контракт: Identity/StaffPhoneStatusResponse.cs
 */
export interface StaffPhoneStatusResponse {
  phone: string | null;
  phoneVerifiedAtUtc: IsoDateTime | null;
}

/** Контракт: Identity/StaffPhoneVerificationContracts.cs */
export interface StaffPhoneVerificationStartedResponse {
  expiresInSeconds: number;
  resendAfterSeconds: number;
}

/** Контракт: Identity/StaffRefreshTokenRequest.cs */
export interface StaffRefreshTokenRequest {
  organizationId: Guid;
  refreshToken: string;
}

/**
 * Completes an SMS password reset using the code delivered to the verified phone.
 *
 * Контракт: Identity/StaffResetPasswordByPhoneRequest.cs
 */
export interface StaffResetPasswordByPhoneRequest {
  phoneNumber: string;
  code: string;
  newPassword: string;
}

/**
 * Completes a self-service password reset using the 6-digit code emailed to the account.
 *
 * Контракт: Identity/StaffResetPasswordRequest.cs
 */
export interface StaffResetPasswordRequest {
  userNameOrEmail: string;
  code: string;
  newPassword: string;
}

/** Контракт: Identity/StaffSignInByLoginRequest.cs */
export interface StaffSignInByLoginRequest {
  login: string;
  password: string;
}

/** Контракт: Identity/StaffSignInByOrganizationKeyRequest.cs */
export interface StaffSignInByOrganizationKeyRequest {
  organizationKey: string;
  userName: string;
  password: string;
}

/** Контракт: Identity/StaffSignInByPhoneRequest.cs */
export interface StaffSignInByPhoneRequest {
  phoneNumber: string;
  password: string;
}

/** Контракт: Identity/StaffSignInChooseClubResponse.cs */
export interface StaffSignInChooseClubResponse {
  clubs: StaffSignInClubChoice[];
}

/** Контракт: Identity/StaffSignInClubChoice.cs */
export interface StaffSignInClubChoice {
  organizationId: Guid;
  name: string;
}

/** Контракт: Identity/StaffSignInRequest.cs */
export interface StaffSignInRequest {
  organizationId: Guid;
  userName: string;
  password: string;
}

/** Контракт: Identity/StaffSignInResponse.cs */
export interface StaffSignInResponse {
  staffUserId: Guid;
  organizationId: Guid;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: IsoDateTime;
  refreshToken: string;
  refreshTokenExpiresAtUtc: IsoDateTime;
  branchIds: Guid[];
  permissions: string[];
  roleNames: string[];
}

/** Контракт: Identity/StaffSignOutRequest.cs */
export interface StaffSignOutRequest {
  organizationId: Guid;
  refreshToken: string;
}

/** Контракт: Identity/StaffUserDto.cs */
export interface StaffUserDto {
  staffUserId: Guid;
  organizationId: Guid;
  userName: string;
  displayName: string;
  isActive: boolean;
  roleNames: string[];
  createdAtUtc: IsoDateTime;
}

/** Контракт: Diagnostics/BranchDiagnosticsDto.cs */
export interface StaleDeviceDiagnosticsDto {
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  isOnline: boolean;
  isLocked: boolean;
  lastHeartbeatAtUtc: IsoDateTime | null;
  lastHeartbeatAgeSeconds: number | null;
}

/** Контракт: Sessions/StartGuestSessionRequest.cs */
export interface StartGuestSessionRequest {
  organizationId: Guid;
  seatId: Guid;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  durationMode?: string;
  durationMinutes?: number | null;
  playerAccountId?: Guid | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
  /** Anti-fraud §5.4: an explicit comp (free session). Requires a reason; routes to a session.comp audit. */
  isComp?: boolean;
  compReason?: string | null;
}

/** Контракт: Reservations/StartReservationSessionRequest.cs */
export interface StartReservationSessionRequest {
  organizationId: Guid;
  expectedVersion: number;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  durationMode?: string;
  durationMinutes?: number | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
  isComp?: boolean;
  compReason?: string | null;
}

/** Контракт: Reservations/StartReservationSessionResponse.cs */
export interface StartReservationSessionResponse {
  reservation: ReservationDto;
  session: SessionCommandResponse;
}

/** Контракт: Inventory/StockMovementDto.cs */
export interface StockMovementDto {
  stockMovementId: Guid;
  organizationId: Guid;
  branchId: Guid;
  productId: Guid;
  movementType: string;
  quantityDelta: number;
  unitCost: MoneyDto;
  reason: string;
  createdByStaffUserId: Guid;
  createdAtUtc: IsoDateTime;
  createdByDisplayName?: string | null;
}

/** Контракт: Platform/Billing/SubscriptionListItemDto.cs */
export interface SubscriptionListItemDto {
  organizationSubscriptionId: Guid;
  organizationId: Guid;
  organizationName: string;
  organizationSlug: string;
  planCode: string;
  status: string;
  billingInterval: string;
  amountMinorUnits: number;
  currencyCode: string;
  currentPeriodEndUtc: IsoDateTime;
  nextInvoiceUtc: IsoDateTime | null;
  cancelAtPeriodEnd: boolean;
}

/** Контракт: Platform/Billing/SubscriptionPlanDto.cs */
export interface SubscriptionPlanDto {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
  /** Цена каждого ПК сверх включённых — у тарифа за ПК; у прочих ноль. */
  pricePerDeviceMinorUnits?: number;
  includedDevices?: number;
  /** Игровых ПК на весь клуб; пусто — без предела. */
  maxDevices?: number | null;
  /** Каждая функция платформы и включена ли она этим тарифом. */
  features?: PlanFeatureDto[] | null;
  /** Сколько клубов сейчас на этом тарифе — им «применить лимиты» при правке. */
  clubs?: number;
}

/** Контракт: Tariffs/TariffCalculationResult.cs */
export interface TariffCalculationResult {
  tariffId: Guid;
  tariffVersionId: Guid;
  tariffRuleVersionId: string;
  durationMinutes: number;
  billableMinutes: number;
  amount: MoneyDto;
}

/**
 * Расписание: `AppliesOnDaysMask` — биты дней недели с понедельника (1) по воскресенье (64),
 * `0` означает «каждый день». Часы — минуты от полуночи по местному времени филиала; оба
 * `null` означают «круглые сутки», а начало больше конца — окно через полночь.
 *
 * Контракт: Tariffs/TariffDto.cs
 */
export interface TariffDto {
  tariffId: Guid;
  organizationId: Guid;
  branchId: Guid;
  name: string;
  isActive: boolean;
  createdAtUtc: IsoDateTime;
  appliesOnDaysMask?: number;
  appliesFromMinuteOfDay?: number | null;
  appliesToMinuteOfDay?: number | null;
  /** Тариф крутится в витрине свободного ПК. */
  featuredOnPcs?: boolean;
}

/**
 * Тариф, который можно выбрать: по чём и с какими правилами считается время. `AppliesNow`
 * считает сервер по часовому поясу филиала: клиент, повторивший этот расчёт у себя, ошибётся на
 * телефоне с чужим часовым поясом и предложит утреннюю цену вечером.
 * Цену по этим полям клиент НЕ считает — за этим есть расчёт на сервере: минимальное
 * оплачиваемое время и шаг округления живут в биллинге, и вторая арифметика здесь разошлась бы
 * с настоящим списанием.
 * AppliesFromMinuteOfDay и AppliesToMinuteOfDay — окно
 * местного времени клуба, минуты от полуночи. Оба пусты — круглосуточно, начало больше конца —
 * переход через полночь.
 *
 * Контракт: Operator/TariffOptionDto.cs
 */
export interface TariffOptionDto {
  tariffId: Guid;
  tariffVersionId: Guid;
  name: string;
  tariffRuleVersionId: string;
  versionNumber: number;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: IsoDateTime;
  /** Биты дней недели с понедельника (1) по воскресенье (64); 0 — каждый день. */
  appliesOnDaysMask?: number;
  appliesFromMinuteOfDay?: number | null;
  appliesToMinuteOfDay?: number | null;
  /**
   * Действует ли тариф прямо сейчас — по часам клуба, а не телефона. Важно там, где играть
   * начинают сию секунду; для брони на завтра ответ никакого значения не имеет.
   */
  appliesNow?: boolean;
  /** Тариф крутится в витрине свободного ПК. */
  featuredOnPcs?: boolean;
}

/**
 * Когда действует тариф: дни недели и окно местного времени филиала.
 * `AppliesOnDaysMask` — биты с понедельника (1) по воскресенье (64); `0` означает
 * «каждый день». Часы — минуты от полуночи; оба `null` означают «круглые сутки», а начало
 * больше конца — окно через полночь.
 * Расписание вынесено отдельным объектом намеренно. В запросе на изменение тарифа плоские поля
 * со значениями по умолчанию означали бы, что вызывающий, не знающий про расписание, стирает его
 * молча: PATCH, у которого пропущенное поле уничтожает данные, — ловушка. Здесь `null` у
 * всего объекта означает «не трогать», и не знать про расписание безопасно.
 *
 * Контракт: Tariffs/TariffScheduleDto.cs
 */
export interface TariffScheduleDto {
  appliesOnDaysMask?: number;
  appliesFromMinuteOfDay?: number | null;
  appliesToMinuteOfDay?: number | null;
}

/** Контракт: Tariffs/TariffVersionDto.cs */
export interface TariffVersionDto {
  tariffVersionId: Guid;
  tariffId: Guid;
  versionNumber: number;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: IsoDateTime;
  retiredAtUtc: IsoDateTime | null;
  createdAtUtc: IsoDateTime;
}

/**
 * Чаевые администратору смены с экрана итога (спека `2026-09-25-visit-tips-design.md`). Клуб их
 * включает сам; деньги уходят с кошелька игрока записью журнала `tip` и выручкой не считаются.
 *
 * Контракт: Tips/TipContracts.cs
 */
export interface TipSettingsDto {
  enabled: boolean;
}

/** Контракт: Billing/TopUpWalletRequest.cs */
export interface TopUpWalletRequest {
  organizationId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

/**
 * Событие клуба глазами стойки: всё, включая черновики и счётчик записавшихся.
 *
 * Контракт: Tournaments/TournamentDtos.cs
 */
export interface TournamentDto {
  tournamentId: Guid;
  branchId: Guid;
  title: string;
  description: string;
  discipline: string;
  startsAtUtc: IsoDateTime;
  entryFee: MoneyDto;
  capacity: number;
  state: string;
  registeredCount: number;
  createdAtUtc: IsoDateTime;
  updatedAtUtc: IsoDateTime;
  cancelledAtUtc: IsoDateTime | null;
  cancelReason: string;
}

/**
 * Кто записался — список для стойки: по нему встречают на входе.
 *
 * Контракт: Tournaments/TournamentDtos.cs
 */
export interface TournamentParticipantDto {
  tournamentRegistrationId: Guid;
  playerAccountId: Guid;
  displayName: string;
  phoneNumber: string | null;
  entryFeePaid: MoneyDto;
  registeredAtUtc: IsoDateTime;
}

/** Контракт: Platform/Organizations/TransferOrganizationOwnerRequest.cs */
export interface TransferOrganizationOwnerRequest {
  newOwnerEmail: string;
  reason: string;
}

/** Контракт: Sessions/TransferSessionRequest.cs */
export interface TransferSessionRequest {
  targetSeatId: Guid;
  idempotencyKey: string;
  expectedVersion?: number | null;
}

/** Контракт: Platform/Billing/BillingTermsContracts.cs */
export interface UpdateBillingTermsRequest {
  trialDays: number;
  promisedPaymentDays: number;
  fallbackAfterOverdueDays: number;
}

/** Контракт: Branches/UpdateBranchBookingSettingsRequest.cs */
export interface UpdateBranchBookingSettingsRequest {
  organizationId: Guid;
  acceptanceMode: string;
  respondWithinMinutes: number;
  requirePrepaymentFromNewGuests: boolean;
  maxActiveReservationsForNewGuests: number;
  regularAfterVisits: number;
  holdSeatAfterStartMinutes: number;
  keepPrepaymentOnNoShow: boolean;
}

/** Контракт: Branches/UpdateBranchProfileRequest.cs */
export interface UpdateBranchProfileRequest {
  organizationId: Guid;
  name: string;
  city: string;
  description: string | null;
  address: string | null;
  phone: string | null;
  telegram: string | null;
  website: string | null;
  instagram: string | null;
  logoUrl: string | null;
  logoMediaId: Guid | null;
  timeZone: string;
  locale: string;
  workingHours: BranchWorkingHoursDayDto[];
  /**
   * Витрина клуба в приложении игрока: фото зала и точка на карте. В конце списка и с
   * умолчаниями — чтобы старый клиент, который их не шлёт, продолжал сохранять профиль.
   */
  coverImageUrl?: string | null;
  coverMediaId?: Guid | null;
  photos?: BranchPhotoDto[] | null;
  latitude?: number | null;
  longitude?: number | null;
}

/**
 * Сохранить профиль. ExpectedVersion — версия, которую человек открыл: если
 * профиль успели поменять, сохранение отказывает, а не затирает чужую правку молча.
 *
 * Контракт: Devices/ProtectionProfileContracts.cs
 */
export interface UpdateBranchProtectionProfileRequest {
  organizationId: Guid;
  expectedVersion: number;
  blockRemovableStorage: boolean;
  blockBrowserDownloads: boolean;
  blockBrowserIncognito: boolean;
  disableRunDialog: boolean;
  hiddenDrives: string[];
  urlBlocklist: string[];
  blockedWindows: BlockedWindowRuleDto[];
  clearAfterSession: string[];
  idleShutdownMinutes?: number | null;
  clubRules?: string | null;
}

/** Контракт: Branches/UpdateBranchSettingsRequest.cs */
export interface UpdateBranchSettingsRequest {
  organizationId: Guid;
  requireManualDeviceApproval: boolean;
  preferredLocale: string;
}

/**
 * POST-запрос: CardNumber опционален — null/пусто сохраняет прежнюю карту, непустой заменяет.
 *
 * Контракт: Payments/DcPayLinkConfigDtos.cs
 */
export interface UpdateDcPayLinkConfigRequest {
  cardNumber: string | null;
  commentTemplate: string;
  isActive: boolean;
}

/** Контракт: Diagnostics/BranchDiagnosticsDto.cs */
export interface UpdateDiagnosticsSummaryDto {
  activeRollouts: number;
  installingDevices: number;
  failedDevices: number;
  rollbackDevices: number;
  recentFailures: FailedUpdateDiagnosticsDto[];
}

/**
 * POST request. HashKey is optional: null/empty keeps the stored secret, a non-empty value
 * replaces it. All four fields present (with a stored-or-supplied hash key) → Status "configured".
 *
 * Контракт: Payments/EskhataMerchantConfigDtos.cs
 */
export interface UpdateEskhataMerchantConfigRequest {
  baseUrl: string;
  companyId: string;
  merchantId: number;
  hashKey: string | null;
}

/** Контракт: Loyalty/UpdateLoyaltySettingsRequest.cs */
export interface UpdateLoyaltySettingsRequest {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
  sessionEnabled: boolean;
  sessionPercentBasisPoints: number;
  cashbackCapMinorUnits: number;
  minimumSourceMinorUnits: number;
}

/**
 * Имя и язык человека — ровно те два поля, которые спрашиваются при регистрации. PIN сюда не
 * входит: его задают позже и в ту секунду, когда он впервые нужен.
 *
 * Контракт: Identity/RegistrationContracts.cs
 */
export interface UpdateMyProfileRequest {
  displayName: string;
  preferredLocale: string | null;
}

/** Контракт: News/UpdateNewsItemRequest.cs */
export interface UpdateNewsItemRequest {
  branchId: Guid | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: IsoDateTime | null;
  expiresAtUtc: IsoDateTime | null;
  showOnPcs?: boolean;
}

/** Контракт: Updates/UpdateOrganizationAdminUpdatePreferenceRequest.cs */
export interface UpdateOrganizationAdminUpdatePreferenceRequest {
  organizationId: Guid;
  maintenanceWindowStart: IsoTime;
  maintenanceWindowEnd: IsoTime;
}

/**
 * Оформление клуба: логотип и цвет. Поля у организации были с самого начала и читались публичной
 * витриной и приложением игрока, но записать их было нечем — единственное присвоение жило в сидере
 * для разработки.
 *
 * Контракт: Branding/UpdateOrganizationBrandingRequest.cs
 */
export interface UpdateOrganizationBrandingRequest {
  logoUrl: string | null;
  accentColor: string | null;
}

/** Контракт: Platform/Organizations/UpdateOrganizationLimitsRequest.cs */
export interface UpdateOrganizationLimitsRequest {
  limits: OrganizationLimitsDto;
}

/** Контракт: Platform/Organizations/UpdateOrganizationProfileRequest.cs */
export interface UpdateOrganizationProfileRequest {
  name: string;
  contactEmail: string | null;
  contactPhone: string | null;
  legalDetails: string | null;
}

/** Контракт: Platform/Organizations/UpdateOrganizationStatusRequest.cs */
export interface UpdateOrganizationStatusRequest {
  status: string;
  reason: string;
}

/** Контракт: Platform/SupportNotes/UpdateOrganizationSupportNoteRequest.cs */
export interface UpdateOrganizationSupportNoteRequest {
  body: string;
}

/** Контракт: Platform/Organizations/UpdateOrganizationUpdateChannelRequest.cs */
export interface UpdateOrganizationUpdateChannelRequest {
  channel: string;
  pinnedClientVersion: string | null;
}

/** Контракт: Packages/UpdatePackageDefinitionRequest.cs */
export interface UpdatePackageDefinitionRequest {
  organizationId: Guid;
  name: string;
  price: MoneyDto;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  isActive: boolean;
}

/** Контракт: Updates/UpdatePackageDto.cs */
export interface UpdatePackageDto {
  updatePackageId: Guid;
  organizationId: Guid;
  branchId: Guid;
  component: string;
  version: string;
  channel: string;
  artifactUri: string;
  sha256: string;
  signature: string;
  signatureAlgorithm: string;
  sizeBytes: number;
  state: string;
  releaseNotes: string;
  createdAtUtc: IsoDateTime;
}

/** Контракт: Updates/UpdatePackageStateChangeRequest.cs */
export interface UpdatePackageStateChangeRequest {
  organizationId: Guid;
  state: string;
  reason: string;
}

/** Контракт: Platform/Billing/UpdatePlanRequest.cs */
export interface UpdatePlanRequest {
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
  /** Не переданы — остаются прежними: старый редактор тарифов о них не знает. */
  pricePerDeviceMinorUnits?: number | null;
  includedDevices?: number | null;
  /** Не передан — остаётся прежним; снять предел — RemoveMaxDevices. */
  maxDevices?: number | null;
  removeMaxDevices?: boolean;
  /** Передан — заменяет набор функций тарифа целиком. */
  includedFeatures?: string[] | null;
  /**
   * Клубы на этом тарифе получают его новые лимиты. Без отметки лимиты клубов не меняются:
   * платформа могла задать клубу свои.
   */
  applyLimitsToClubs?: boolean;
}

/** Контракт: Platform/Auth/PlatformAdminDirectoryContracts.cs */
export interface UpdatePlatformAdminRequest {
  role: string | null;
  isActive: boolean | null;
}

/** Контракт: Platform/Auth/PlatformRoleContracts.cs */
export interface UpdatePlatformRoleRequest {
  displayName: string;
  description: string;
  permissions: string[];
}

/** Контракт: Players/UpdatePlayerAccountRequest.cs */
export interface UpdatePlayerAccountRequest {
  organizationId: Guid;
  displayName: string;
  phoneNumber: string | null;
}

/**
 * Player-editable profile fields. Both optional; null means "leave unchanged".
 *
 * Контракт: Players/UpdatePlayerProfileRequest.cs
 */
export interface UpdatePlayerProfileRequest {
  preferredLocale: string | null;
  marketingOptIn: boolean | null;
}

/**
 * Показывать ли друзьям, что я сейчас в зале.
 *
 * Контракт: Friends/FriendDtos.cs
 */
export interface UpdatePresenceVisibilityRequest {
  showsPresence: boolean;
}

/**
 * Правка категории: имя и видимость на стойке. Оба поля необязательны — присланное меняется,
 * пропущенное остаётся как было, поэтому переименование не гасит категорию заодно.
 * Ключа идемпотентности здесь нет намеренно: повтор приводит к тому же состоянию, а денег
 * операция не двигает — в отличие от создания, где повтор завёл бы вторую категорию.
 *
 * Контракт: Pos/UpdateProductCategoryRequest.cs
 */
export interface UpdateProductCategoryRequest {
  organizationId: Guid;
  name?: string | null;
  isActive?: boolean | null;
}

/** Контракт: Pos/UpdateProductRequest.cs */
export interface UpdateProductRequest {
  organizationId: Guid;
  categoryId: Guid;
  name: string;
  sku: string;
  price: MoneyDto;
  trackStock: boolean;
  allowNegativeStock: boolean;
  isActive: boolean;
  reorderThreshold?: number;
  availableInShell?: boolean;
  /** Товар крутится в витрине свободного ПК. */
  featuredOnPcs?: boolean;
  /** Фото товара: адрес загрузки с назначением product-image. Пусто — без фото. */
  imageUrl?: string | null;
}

/** Контракт: Loyalty/ReferralContracts.cs */
export interface UpdateReferralSettingsRequest {
  enabled: boolean;
  referrerBonusMinorUnits: number;
  inviteeBonusMinorUnits: number;
  minimumTopUpMinorUnits: number;
  claimWindowDays: number;
  maxRewardedPerReferrer: number;
}

/**
 * Правка рассылки: частота и пауза. Оба поля необязательны — присланное меняется,
 * пропущенное остаётся как было.
 * Пауза, а не удаление: уйти в отпуск на две недели и не получать письма — не то же самое,
 * что отказаться от рассылки совсем и заводить её заново.
 *
 * Контракт: Reports/ReportScheduleContracts.cs
 */
export interface UpdateReportScheduleRequest {
  organizationId: Guid;
  frequency?: string | null;
  isActive?: boolean | null;
}

/** Контракт: Reservations/ReservationRequests.cs */
export interface UpdateReservationRequest {
  organizationId: Guid;
  playerAccountId: Guid | null;
  seatId: Guid | null;
  customerName: string | null;
  phoneNumber: string | null;
  startsAtUtc: IsoDateTime | null;
  durationMinutes: number | null;
  source: string | null;
  note: string | null;
  expectedVersion: number;
}

/** Контракт: Updates/UpdateRolloutDto.cs */
export interface UpdateRolloutDto {
  updateRolloutId: Guid;
  organizationId: Guid;
  branchId: Guid;
  updatePackageId: Guid;
  component: string;
  version: string;
  channel: string;
  state: string;
  targetKind: string;
  targetDeviceIds: Guid[];
  batchPercent: number;
  createdAtUtc: IsoDateTime;
  startsAtUtc: IsoDateTime;
  completedAtUtc: IsoDateTime | null;
}

/** Контракт: Updates/UpdateRolloutStateChangeRequest.cs */
export interface UpdateRolloutStateChangeRequest {
  organizationId: Guid;
  state: string;
  reason: string;
}

/** Контракт: Updates/UpdateRolloutStatusDto.cs */
export interface UpdateRolloutStatusDto {
  updateRolloutId: Guid;
  organizationId: Guid;
  branchId: Guid;
  updatePackageId: Guid;
  component: string;
  version: string;
  channel: string;
  state: string;
  targetKind: string;
  targetDeviceIds: Guid[];
  batchPercent: number;
  createdAtUtc: IsoDateTime;
  startsAtUtc: IsoDateTime;
  completedAtUtc: IsoDateTime | null;
  deviceStatuses: DeviceUpdateStatusSnapshotDto[];
}

/** Контракт: Layout/UpdateSeatRequest.cs */
export interface UpdateSeatRequest {
  organizationId: Guid;
  zoneId: Guid;
  name: string;
  sortOrder: number;
}

/** Контракт: Identity/UpdateStaffUserProfileRequest.cs */
export interface UpdateStaffUserProfileRequest {
  organizationId: Guid;
  userName: string;
  displayName: string;
}

/** Контракт: Identity/UpdateStaffUserRolesRequest.cs */
export interface UpdateStaffUserRolesRequest {
  organizationId: Guid;
  roleNames: string[];
}

/** Контракт: Identity/UpdateStaffUserStateRequest.cs */
export interface UpdateStaffUserStateRequest {
  organizationId: Guid;
  isActive: boolean;
}

/** Контракт: Platform/Billing/UpdateSubscriptionRequest.cs */
export interface UpdateSubscriptionRequest {
  planCode: string | null;
  billingInterval: string | null;
  status: string | null;
  cancelAtPeriodEnd: boolean | null;
  amountMinorUnits: number | null;
  currentPeriodEndUtc: IsoDateTime | null;
  paymentGraceUntilUtc: IsoDateTime | null;
  clearPaymentGrace?: boolean | null;
  discountPercent?: number | null;
  discountAmountMinorUnits?: number | null;
  discountUntilUtc?: IsoDateTime | null;
  discountReason?: string | null;
  clearDiscount?: boolean | null;
}

/**
 * `Schedule` не передан — расписание остаётся прежним. Снятие тарифа с продажи и
 * переименование не должны требовать от вызывающего знания о часах. Так же и
 * `FeaturedOnPcs`: не передан — отметка в витрине не меняется.
 *
 * Контракт: Tariffs/UpdateTariffRequest.cs
 */
export interface UpdateTariffRequest {
  organizationId: Guid;
  name: string;
  isActive: boolean;
  schedule?: TariffScheduleDto | null;
  featuredOnPcs?: boolean | null;
}

/** Контракт: Tariffs/UpdateTariffVersionRequest.cs */
export interface UpdateTariffVersionRequest {
  organizationId: Guid;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: IsoDateTime;
  isActive: boolean;
}

/** Контракт: Tips/TipContracts.cs */
export interface UpdateTipSettingsRequest {
  enabled: boolean;
}

/**
 * Правка события. Незаполненное поле означает «оставить как было» — стойка правит одну строку,
 * а не переписывает событие целиком.
 *
 * Контракт: Tournaments/TournamentDtos.cs
 */
export interface UpdateTournamentRequest {
  title?: string | null;
  description?: string | null;
  discipline?: string | null;
  startsAtUtc?: IsoDateTime | null;
  entryFeeMinorUnits?: number | null;
  capacity?: number | null;
}

/** Контракт: Layout/UpdateZoneRequest.cs */
export interface UpdateZoneRequest {
  organizationId: Guid;
  name: string;
  sortOrder: number;
  hardwareSummary?: string | null;
}

/** Контракт: Media/UploadedMediaDto.cs */
export interface UploadedMediaDto {
  mediaId: Guid;
  url: string;
  contentType: string;
  sizeBytes: number;
}

/** Контракт: Ads/AdContracts.cs */
export interface UpsertAdCampaignRequest {
  advertiserId: Guid;
  name: string;
  category: string;
  startsAtUtc: IsoDateTime;
  endsAtUtc: IsoDateTime;
  cities: string[] | null;
  organizationIds: Guid[] | null;
  compliance?: AdCampaignComplianceDto | null;
}

/** Контракт: Ads/AdContracts.cs */
export interface UpsertAdCreativeRequest {
  title: string;
  body: string | null;
  imageUrl: string | null;
  titleRu?: string | null;
  bodyRu?: string | null;
}

/** Контракт: Ads/AdContracts.cs */
export interface UpsertAdvertiserRequest {
  name: string;
  contact: string | null;
  legalName?: string | null;
  taxId?: string | null;
  address?: string | null;
}

/** Контракт: Games/GameLibraryContracts.cs */
export interface UpsertBranchGameRequest {
  organizationId: Guid;
  catalogGameId: Guid | null;
  name: string;
  genre: string | null;
  minAge: number | null;
  launchKind: string;
  launchTarget: string | null;
  executablePath: string | null;
  arguments: string | null;
  availableWithoutSession: boolean;
  isEnabled: boolean;
  launchOnSessionStart?: boolean;
}

/** Контракт: Games/GameLibraryContracts.cs */
export interface UpsertCatalogGameRequest {
  name: string;
  description: string | null;
  genre: string | null;
  minAge: number | null;
  launchKind: string;
  launchTarget: string | null;
  coverUrl: string | null;
  isPublished: boolean;
}

/** Контракт: Platform/Billing/VoidInvoiceRequest.cs */
export interface VoidInvoiceRequest {
  reason: string;
}

/** Контракт: Pos/VoidPosSaleRequest.cs */
export interface VoidPosSaleRequest {
  organizationId: Guid;
  reason: string;
  idempotencyKey: string;
}

/**
 * Деньги игрока в одном клубе: сколько можно потратить, сколько придержано под брони, сколько он
 * должен.
 * WalletBalance — доступный остаток, и он таким и остаётся: заморозка под бронь
 * из него уже вычтена, потому что холд и есть отрицательная запись журнала.
 * HeldBalance ничего не переносит и не пересчитывает — оно объясняет, куда
 * делась часть остатка.
 * Это ответ на денежную операцию, и он отдельно от `PlayerDashboardDto` намеренно: идущей
 * сессии здесь нет, и делать вид, что она просто «пустая», значит однажды показать «сессии нет»
 * там, где она есть.
 *
 * Контракт: Billing/WalletSummaryDto.cs
 */
export interface WalletSummaryDto {
  playerAccountId: Guid;
  walletBalance: MoneyDto;
  heldBalance: MoneyDto;
  debtBalance: MoneyDto;
  recentEntries: LedgerEntryDto[];
}

/**
 * Зал и его места. HardwareSummary — необязательный хвост: новое поле не должно
 * ломать позиционные вызовы, которых у этого контракта хватает и в Windows-проектах.
 *
 * Контракт: Layout/ZoneDto.cs
 */
export interface ZoneDto {
  zoneId: Guid;
  organizationId: Guid;
  branchId: Guid;
  name: string;
  sortOrder: number;
  createdAtUtc: IsoDateTime;
  seats: SeatDto[];
  hardwareSummary?: string | null;
}
