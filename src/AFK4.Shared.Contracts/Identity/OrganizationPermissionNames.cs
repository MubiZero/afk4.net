namespace AFK4.Shared.Contracts.Identity;

public static class OrganizationPermissionNames
{
    public const string CreateDeviceEnrollmentCode = "organization.devices.enrollment_codes.create";

    public const string DispatchDeviceCommand = "organization.devices.commands.dispatch";

    /// <summary>
    /// Увести ПК в обслуживание и вернуть в зал. Отдельно от прочих команд: обслуживание закрывает
    /// машину для игроков, и решать это — не каждому, кто может её перезапереть.
    /// </summary>
    public const string MaintainDevice = "organization.devices.maintenance";

    public const string ViewDeviceCommandStatus = "organization.devices.commands.status.view";

    public const string RotateDeviceCredential = "organization.devices.credentials.rotate";

    public const string RevokeDeviceCredential = "organization.devices.credentials.revoke";

    public const string AssignDeviceSeat = "organization.devices.seat_assignment.assign";

    public const string ViewDeviceDetail = "organization.devices.detail.view";

    public const string InstallDevice = "organization.devices.install";

    public const string ViewFloorMap = "organization.floor_map.view";

    public const string ManageLayout = "organization.layout.manage";

    public const string StartSession = "organization.sessions.start";

    public const string ExtendSession = "organization.sessions.extend";

    // Поставить сессию на паузу и снять её. Право того же круга, что продление: обе правят время.
    public const string PauseSession = "organization.sessions.pause";

    public const string TransferSession = "organization.sessions.transfer";

    public const string EndSession = "organization.sessions.end";

    public const string ViewSession = "organization.sessions.view";

    public const string CreatePlayerAccount = "organization.players.create";

    public const string ViewPlayers = "organization.players.view";

    public const string ViewBilling = "organization.billing.view";

    public const string TopUpWallet = "organization.billing.wallet.top_up";

    public const string RefundLedgerEntry = "organization.billing.refund";

    public const string ManualLedgerCorrection = "organization.billing.manual_correction";

    public const string PayDebt = "organization.billing.debt.pay";

    // Anti-fraud (§5.2/D2): approve an over-threshold high-risk money action raised by another actor.
    public const string ApproveMoneyAction = "organization.billing.money_action.approve";

    public const string ViewSubscription = "organization.billing.subscription.view";

    /// Сменить тариф клуба, начать пробный период, взять обещанный платёж. Это обязательство
    /// платить — только у владельца.
    public const string ManageSubscription = "organization.billing.subscription.manage";

    /// Перенести гостей с балансами из прежней программы. Это деньги, которые клуб берёт на себя, —
    /// только у владельца.
    public const string ImportPlayers = "organization.players.import";

    public const string ManageTariffs = "organization.tariffs.manage";

    public const string ViewTariffs = "organization.tariffs.view";

    public const string ManagePackages = "organization.packages.manage";

    public const string ViewPackages = "organization.packages.view";

    public const string PurchasePackage = "organization.packages.purchase";

    public const string OpenShift = "organization.shifts.open";

    public const string CloseShift = "organization.shifts.close";

    /// Закрыть СВОЮ смену — ту, которую сам и открыл. Узкое подмножество CloseShift.
    ///
    /// Контроль от этого не слабеет: сверка кассы обязательна для всех (CountedCash), а
    /// расхождение сверх допуска филиала по-прежнему требует подписи второго человека, который
    /// не открывал и не закрывает смену (§5.7, EfShiftService). То есть «закрыть поверх
    /// недостачи» в одиночку нельзя было и не станет можно.
    ///
    /// Без этого права ночной кассир, которого гейт после входа ЗАСТАВИЛ открыть смену, не мог
    /// её закрыть: в шесть утра он один, а закрывать обязан кто-то другой.
    public const string CloseOwnShift = "organization.shifts.close_own";

    public const string ViewShift = "organization.shifts.view";

    public const string ManageShiftCash = "organization.shifts.cash.manage";

    public const string ViewReports = "organization.reports.view";

    public const string ViewReservations = "organization.reservations.view";

    public const string ManageReservations = "organization.reservations.manage";

    public const string ManagePosCatalog = "organization.pos.catalog.manage";

    public const string CreatePosSale = "organization.pos.sales.create";

    public const string PayPosSale = "organization.pos.sales.pay";

    public const string RefundPosSale = "organization.pos.sales.refund";

    public const string VoidPosSale = "organization.pos.sales.void";

    /// Отменить СВОЙ чек, пробитый только что, без старшего — узкое подмножество VoidPosSale.
    /// Границы правила и довод за него живут в <c>Pos/PosSelfVoidPolicy.cs</c>: право само по
    /// себе ничего не разрешает, пока продажа не своя, не в текущей открытой смене и не свежая.
    public const string VoidOwnRecentPosSale = "organization.pos.sales.void_own_recent";

    public const string ManageInventoryStock = "organization.inventory.stock.manage";

    public const string ViewInventory = "organization.inventory.view";

    public const string ViewReceipt = "organization.receipts.view";

    public const string ViewUpdateStatus = "organization.updates.status.view";

    public const string ViewDiagnostics = "organization.diagnostics.view";

    public const string ManageBranchStaff = "organization.identity.branch_staff.manage";

    public const string ManageRoles = "organization.identity.roles.manage";

    public const string ViewAudit = "organization.audit.view";

    // Owner-only: read org-wide audit across all branches + org-level records.
    public const string ViewOrganizationAudit = "organization.audit.organization.view";

    public const string ManageBranchSettings = "organization.branches.settings.manage";

    // Owner-only: view the org-wide branch roster (network overview).
    public const string ViewBranches = "organization.branches.view";

    // Owner-only: connect/manage the club's DC-Bank payment cards (dcgate gateways).
    public const string ManagePaymentGateways = "organization.payments.gateways.manage";

    // Очередь заказов бара разведена на два права по одной границе: двигаются ли деньги.
    //
    // Serve — увидеть очередь, принять заказ, выдать его. Это чистая смена статуса, ничего не
    // списывается и не возвращается: заказ оплачен в момент оформления. Выдаёт еду кассир, ему
    // это право и нужно.
    //
    // Manage — отменить заказ, а отмена идёт через денежный координатор и возвращает деньги.
    // Это денежное действие и остаётся за тем же кругом, что возвраты в кассе.
    public const string ServeShopOrders = "organization.shop.orders.serve";

    public const string ManageShopOrders = "organization.shop.orders.manage";

    // Снять с места вызов оператора. Право того же круга, что и «отдать заказ»: зовут человека
    // с зала, а не того, кто правит настройки.
    public const string ResolveAssistanceRequest = "organization.assistance.resolve";

    // Owner-only: configure org-wide loyalty/cashback rates.
    public const string ManageLoyaltySettings = "organization.loyalty.settings.manage";

    public const string ManageNews = "organization.news.manage";

    /// Заводить и отменять события клуба. Отдельно от новостей: событие возвращает деньги
    /// при отмене, и это право сильнее права написать объявление.
    public const string ManageTournaments = "organization.tournaments.manage";

    /// Библиотека игр филиала — что игрок запустит на ПК (спека оболочки, §6.6). У того, кто
    /// ставит ПК и игры: владелец, управляющий, техник.
    public const string ManageGameLibrary = "organization.games.manage";

    /// Читать отзывы игроков о филиале. Отзыв бывает и о смене — поэтому у владельца и
    /// управляющего, а не у всей стойки.
    public const string ViewReviews = "organization.reviews.view";

    /// Принять новое железо ПК как норму — после апгрейда или ремонта. У того, кто его меняет:
    /// владелец, управляющий, техник.
    public const string AcceptDeviceHardware = "organization.devices.hardware.accept";

    /// Чаевые администратору с экрана ПК: включить у клуба и вернуть игроку, пока смена открыта.
    /// Это движение денег, поэтому у владельца и управляющего, а не у стойки.
    public const string ManageTips = "organization.tips.manage";
}
