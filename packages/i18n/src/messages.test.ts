import { it, expect } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { messages } from './messages';

const localesDir = join(import.meta.dir, '..', '..', '..', 'locales');
const readLocale = (loc: string) => JSON.parse(readFileSync(join(localesDir, `${loc}.json`), 'utf8')) as Record<string, string>;

it('source locale catalogs contain no duplicate keys and early-start copy keeps its time interpolation', () => {
  for (const locale of ['ru', 'en', 'tg']) {
    const source = readFileSync(join(localesDir, `${locale}.json`), 'utf8');
    const keys = [...source.matchAll(/^\s*"([^"]+)"\s*:/gm)].map((match) => match[1]);
    expect(new Set(keys).size).toBe(keys.length);
    expect(readLocale(locale)['op.booking.start.earlyWarning']).toContain('{time}');
  }
});

it('ru, en and tg have identical key sets (catalog parity)', () => {
  const ruKeys = Object.keys(messages.ru).sort();
  expect(Object.keys(messages.en).sort()).toEqual(ruKeys);
  expect(Object.keys(messages.tg).sort()).toEqual(ruKeys);
});

// Keys whose tg value is intentionally identical to ru. Three legitimate reasons:
// (1) loanwords Tajik genuinely borrows from Russian (тариф, бонус, онлайн, логин,
// категория, артикул, версия, канал, платформа, сессия, чек, оператор, агент…);
// (2) brand/product tokens (AFK4.NET, MRR, Starter/Growth/Scale, Кола 0.5);
// (3) symbols/placeholders/numbers (№, +992…, {from} → {to}).
// Any OTHER tg===ru is a forbidden silent ru-copy pretending to be a translation —
// the runtime already falls back tg→ru, so copying the Russian text in adds no value,
// it only fakes "translated" coverage. Add a key here ONLY with a real reason above,
// never to silence the check. Native-Tajik review may move entries out of this list.
const TG_IDENTICAL_TO_RU_ALLOWED = new Set<string>([
    'auth.admin.title',
    'account.phone.placeholder',
    'clientPackages.field.package',
    'clients.col.phone',
    'clients.field.phone',
    'club.billing.subscription.plan',
    'club.billing.title',
    // «тариф» — то же устоявшееся заимствование, что club.billing.subscription.plan выше,
    // тот же смысл (подпись тарифного плана), другой namespace (Сеть → Оплата, org-audit fix).
    'op.network.billing.plan',
    'customer.profile.langEn',
    'customer.receipt.openLink',
    'customer.signin.phone',
    'devices.status.offline',
    'devices.status.online',
    // «ПК» и «онлайн» — те же устоявшиеся заимствования, что devices.status.online выше
    // (KPI-подпись «ПК онлайн» на своде Сеть → Филиалы).
    'op.network.branches.kpi.devices',
    'floor.seatDefault',
    'floor.zoneDefault',
    'journal.actor.system',
    'journal.col.target',
    // «журнал» — устоявшееся заимствование (как journal.actor.system/journal.col.target выше);
    // это заголовок вкладки «Сеть → Журнал» (org-audit), не отдельный термин.
    'op.network.dest.journal',
    // Тот же org-audit экран «Сеть → Журнал» (Task 8): «система»/«объект» — те же устоявшиеся
    // заимствования, что journal.actor.system/journal.col.target выше, тот же смысл (актёр-фоллбэк
    // и подпись колонки «объект действия»), просто отдельный namespace ключей под org-level экран.
    'op.network.journal.actor.system',
    'op.network.journal.col.target',
    'ledger.type.reversal',
    // «сторно» — международный бухгалтерский термин-заимствование (как ledger.type.reversal)
    'op.players.history.reversalBadge',
    // «клуб» — общеупотребимое заимствование в тадж. разговорной речи (нет отдельного нативного слова),
    // уже встречается нетранслируемым внутри op.settings.heading/tg
    'op.management.dest.club',
    // Eskhata Merchant — бренд и технические ярлыки реквизитов; одинаковы во всех языках.
    'op.eskhata.title',
    'op.eskhata.baseUrl',
    'op.eskhata.companyId',
    'op.eskhata.merchantId',
    'op.eskhata.hashKey',
    // DushanbeCity — бренд-имя платёжного метода; одинаково во всех локалях (subhead/topup.open переведены).
    'op.dc.title',
    'nav.billing',
    'nav.group.account',
    'nav.group.branch',
    'nav.group.platformAccount',
    'op.auth.operator',
    'op.booking.fallback.zeroSeats',
    'op.booking.seatsOne',
    'op.booking.source.operator',
    'op.cash.title',
    // Telegram / Instagram — бренд-топонимы, одинаковы во всех локалях.
    'op.club.field.telegram',
    'op.club.field.instagram',
    // Плейсхолдеры-примеры форматов и топоним «Душанбе» — одинаковы в ru/tg (число, url, @-хэндл, город).
    'op.club.ph.city',
    'op.club.ph.phone',
    'op.club.ph.telegram',
    'op.club.ph.website',
    'op.club.ph.instagram',
    'op.command.stage.cashier',
    'op.dashboard.pcs',
    'op.dashboard.signalsShort',
    'op.floor.duration.secShort',
    'op.floor.remaining.pcOffline',
    'op.floor.state.offline',
    'op.helper.appVer.agent',
    'op.helper.appVer.shell',
    'op.helper.audit.system',
    'op.helper.billing.package',
    'op.helper.deviceStatus.online',
    'op.helper.player.packageCount',
    'op.helper.player.packageFallback',
    'op.helper.player.platform',
    'op.helper.player.tariffFallback',
    'op.helper.pos.receiptNumber',
    'op.helper.pos.receiptType.fallback',
    'op.helper.pos.saleState.fallback',
    'op.helper.staff.operator',
    'op.helper.staff.technician',
    'op.helper.update.channel.beta',
    'op.helper.update.channel.fallback',
    'op.helper.zone.bootcamp',
    // «ПК» — та же неизменяемая аббревиатура, что и op.settings.layout.seatFallback
    'op.management.halls.addSeatCta',
    'op.management.halls.col.seatName',
    // «Тариф»/«Пакет» — те же нетранслируемые заимствования, что и op.settings.tariffs.tariffFallback/
    // op.settings.packages.packageFallback; «Бонус» — общеупотребимое заимствование без отдельного
    // нативного слова (см. op.settings.packages.bonusMinutes = «Бонус, дақ»).
    'op.management.tariffs.addTariffCta',
    'op.management.tariffs.addPackageCta',
    'op.management.tariffs.col.bonus',
    // «Логин» — то же общепринятое заимствование, что и operators.field.userName/
    // platform.profile.field.userName, отдельного нативного слова для UI-ярлыка нет.
    'op.management.staff.col.login',
    // «Артикул» — то же заимствование, что и op.settings.pos.sku.
    'op.management.goods.col.sku',
    'op.map.feedbackOffline',
    'op.map.panel.confirmStatusBilling',
    'op.map.panel.packageLabel',
    'op.map.panel.tariffLabel',
    'op.news.col.branch',
    'op.news.fieldBranch',
    'op.players.editProfile.phoneLabel',
    'op.players.profile.packageFallback',
    'op.players.strip.platform',
    'op.pos.catalog.categoryFallback',
    'op.pos.catalog.title',
    'op.pos.fixture.cola',
    'op.pos.fixture.hotdog',
    'op.pos.receipts.emptyPlatform',
    'op.pos.receipts.receiptFallback',
    'op.settings.devices.detail.agent',
    'op.settings.devices.detail.shell',
    'op.settings.devices.newCredentialEmpty',
    'op.settings.devices.offline',
    'op.settings.devices.online',
    'op.settings.layout.seatCount',
    'op.settings.layout.seatFallback',
    'op.settings.packageState.confirmPackage.packageFallback',
    'op.settings.packages.packageFallback',
    'op.settings.pos.category',
    'op.settings.pos.sku',
    'op.settings.prefill.categoryNameIndexed',
    'op.settings.prefill.tariffNameIndexed',
    'op.settings.profile.branch',
    'op.settings.rollouts.channel',
    'op.settings.rollouts.detail.channel',
    'op.settings.rollouts.detail.devices',
    'op.settings.rollouts.detail.package',
    'op.settings.rollouts.detail.versionArrow',
    'op.settings.rollouts.rolloutTargetDetail',
    'op.settings.tariffs.tariffFallback',
    'op.settings.updates.channel',
    'op.settings.updates.version',
    'op.settings.updates.versionFallback',
    'op.shell.account',
    'op.shell.appName',
    'op.shell.nav.dashboard',
    'op.shell.platform',
    'op.stock.journal.csv.sku',
    'operators.field.email',
    'operators.field.userName',
    'overview.attention.offline',
    'platform.billing.column.number',
    'platform.billing.column.plan',
    'platform.billing.column.organization',
    'platform.newOrganization.field.planCode',
    'platform.newOrganization.section.plan',
    'platform.plan.growth',
    'platform.plan.scale',
    'platform.plan.starter',
    'platform.profile.field.userName',
    'platform.organization.planForm.plan',
    'platform.organization.subscriptionForm.plan',
    // Organization Admin is the canonical product name in every locale.
    'op.helper.update.component.organizationAdmin',
    'products.categoryUnknown',
    'products.col.category',
    'products.col.sku',
    'products.field.category',
    'products.field.sku',
    'reports.col.operator',
    'roles.operator',
    'roles.technician',
    'setup.wizard.finished.summary.branch',
    'setup.wizard.stepper.branch',
    // «№» — the same symbol in ru and tg (numeral sign), same reasoning as other symbol entries
    // above (e.g. brand tokens, {from}→{to}) — the Сеть → Подписка invoices table's number column.
    'op.network.billing.col.number',
    // «система» — the same actor fallback loanword as op.network.journal.actor.system above,
    // reused verbatim in the History section's own Journal report (Task 3).
    'op.reports.journal.actor.system',
    // «Оператор» — the same established loanword as op.auth.operator above,
    // used as a report column label (Отчёты → История → Действия операторов).
    'op.reports.col.operator',
    // «Журнал» — тот же устоявшийся заимствование, что op.network.dest.journal выше
    // (карточка клиента Platform Control, вкладка «Журнал»).
    'platform.organization.tab.history',
    // «2FA» — technical abbreviation, not a translatable word; identical in every locale
    // (Platform Control → Settings, staff table column).
    'platform.settings.column.twoFactor',
]);

it('tg has no silent ru-copies (untranslated strings posing as Tajik)', () => {
  const ru = messages.ru as Record<string, string>;
  const tg = messages.tg as Record<string, string>;
  const offenders = Object.keys(ru).filter(
    (k) => ru[k].trim() !== '' && tg[k] === ru[k] && !TG_IDENTICAL_TO_RU_ALLOWED.has(k)
  );
  expect(offenders).toEqual([]);
});

it('TG_IDENTICAL_TO_RU_ALLOWED has no stale entries (every listed key exists and is actually tg===ru)', () => {
  const ru = messages.ru as Record<string, string>;
  const tg = messages.tg as Record<string, string>;
  const stale = [...TG_IDENTICAL_TO_RU_ALLOWED].filter(
    (k) => !(k in ru) || !(k in tg) || tg[k] !== ru[k]
  );
  expect(stale).toEqual([]);
});

it('generated messages.ts matches the locales/*.json source of truth', () => {
  // Guards against forgetting to re-run `bun run gen` after editing the JSON.
  for (const loc of ['ru', 'en', 'tg'] as const) {
    expect(messages[loc]).toEqual(readLocale(loc));
  }
});

it('includes the new venue/devices keys', () => {
  for (const key of ['venue.title', 'venue.tab.devices', 'venue.tab.pending',
    'devices.col.name', 'devices.action.rename', 'devices.action.remove',
    'common.save', 'common.cancel', 'toast.saved'] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the new settings/operators/roles keys', () => {
  for (const key of [
    'settings.tab.branch', 'settings.tab.operators', 'settings.branch.name', 'settings.branch.city',
    'settings.branch.approval', 'settings.ownerOnly',
    'operators.col.name', 'operators.status.active', 'operators.save.profile',
    'operators.action.deactivate', 'operators.action.resetPassword', 'operators.password.tooShort',
    'operators.create.title', 'operators.create.submit',
    'roles.branch_manager', 'roles.technician', 'roles.unknown'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the new branches keys', () => {
  for (const key of [
    'branches.unnamed', 'branches.totals.title', 'branches.totals.branches',
    'branches.open', 'branches.rename', 'branches.rename.title',
    'branches.add', 'branches.add.unavailable', 'branches.card.error', 'branches.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the floor-map editor keys', () => {
  for (const key of [
    'floor.reload', 'floor.save', 'floor.addZone', 'floor.addSeat',
    'floor.zoneName', 'floor.seatName', 'floor.removeZone', 'floor.removeSeat',
    'floor.moveUp', 'floor.moveDown', 'floor.empty', 'floor.conflict',
    'floor.readonly', 'floor.zoneDefault', 'floor.seatDefault'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the monetization + tariffs keys', () => {
  for (const key of [
    'monetization.tab.tariffs', 'monetization.tab.products', 'monetization.tab.loyalty',
    'monetization.soon', 'monetization.ownerOnly',
    'tariffs.create', 'tariffs.create.title', 'tariffs.create.submit',
    'tariffs.edit.title', 'tariffs.edit.submit', 'tariffs.empty', 'tariffs.activeOnlyNote',
    'tariffs.col.name', 'tariffs.col.price', 'tariffs.col.minMinutes', 'tariffs.col.rounding', 'tariffs.col.effectiveFrom',
    'tariffs.field.name', 'tariffs.field.pricePerMinute', 'tariffs.field.minMinutes', 'tariffs.field.rounding', 'tariffs.field.currency', 'tariffs.field.active'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the products (catalog) keys', () => {
  for (const key of [
    'products.create', 'products.create.title', 'products.create.submit',
    'products.edit.title', 'products.edit.submit', 'products.empty',
    'products.createCategory', 'products.createCategory.title', 'products.createCategory.submit',
    'products.categoryNote', 'products.categoryUnknown',
    'products.col.category', 'products.col.name', 'products.col.sku', 'products.col.price', 'products.col.stock', 'products.col.status',
    'products.field.category', 'products.field.categoryName', 'products.field.name', 'products.field.sku',
    'products.field.price', 'products.field.currency', 'products.field.trackStock', 'products.field.allowNegativeStock', 'products.field.active',
    'products.status.active', 'products.status.inactive'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the loyalty (packages) keys', () => {
  for (const key of [
    'loyalty.create', 'loyalty.create.title', 'loyalty.create.submit',
    'loyalty.edit.title', 'loyalty.edit.submit', 'loyalty.empty', 'loyalty.activeOnlyNote',
    'loyalty.col.name', 'loyalty.col.price', 'loyalty.col.included', 'loyalty.col.bonus', 'loyalty.col.expires',
    'loyalty.field.name', 'loyalty.field.price', 'loyalty.field.currency',
    'loyalty.field.included', 'loyalty.field.bonus', 'loyalty.field.expires', 'loyalty.field.active'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the clients/CRM keys', () => {
  for (const key of [
    'clients.search.placeholder', 'clients.search.label', 'clients.create', 'clients.create.title',
    'clients.create.submit', 'clients.field.displayName', 'clients.field.phone',
    'clients.empty', 'clients.noAccess', 'clients.selectHint', 'clients.editUnavailable',
    'clients.col.name', 'clients.col.phone', 'clients.col.wallet', 'clients.col.debt',
    'clients.col.packages', 'clients.col.status', 'clients.status.active', 'clients.status.inactive',
    'clients.billing.noAccess', 'clients.balance.wallet', 'clients.balance.debt',
    'clients.history.title', 'clients.history.empty', 'clients.history.note',
    'clients.history.col.date', 'clients.history.col.type', 'clients.history.col.account',
    'clients.history.col.amount', 'clients.history.col.minutes', 'clients.history.col.reason',
    'ledger.type.top_up', 'ledger.type.gameplay_charge', 'ledger.type.package_purchase',
    'ledger.type.package_consumption', 'ledger.type.bonus_grant', 'ledger.type.bonus_consumption',
    'ledger.type.refund', 'ledger.type.manual_correction', 'ledger.type.postpaid_debt',
    'ledger.type.debt_payment', 'ledger.type.reversal',
    'ledger.account.wallet', 'ledger.account.debt', 'ledger.account.package_time', 'ledger.account.bonus_time'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the money-operations keys', () => {
  for (const key of [
    'money.topUp', 'money.topUp.title', 'money.payDebt', 'money.payDebt.title',
    'money.correction', 'money.correction.title', 'money.refund', 'money.refund.title',
    'money.field.amount', 'money.field.minutes', 'money.field.reason', 'money.field.account',
    'money.submit'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the client-packages keys', () => {
  for (const key of [
    'clientPackages.title', 'clientPackages.empty', 'clientPackages.purchase',
    'clientPackages.purchase.title', 'clientPackages.purchase.submit', 'clientPackages.field.package',
    'clientPackages.col.name', 'clientPackages.col.included', 'clientPackages.col.bonus',
    'clientPackages.col.expires', 'clientPackages.noExpiry', 'clientPackages.noChoices'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the reports keys', () => {
  for (const key of [
    'reports.noAccess', 'reports.empty', 'reports.limitNote', 'reports.export', 'reports.export.error',
    'reports.tab.shifts', 'reports.tab.sales', 'reports.tab.gameplay', 'reports.tab.cash', 'reports.tab.operatorActions',
    'reports.range.today', 'reports.range.7d', 'reports.range.30d', 'reports.range.from', 'reports.range.to',
    'reports.sum.gross', 'reports.sum.refunds', 'reports.sum.net',
    'reports.sum.duration', 'reports.sum.package', 'reports.sum.bonus', 'reports.sum.revenue',
    'reports.sum.cashIn', 'reports.sum.cashOut', 'reports.sum.netCash', 'reports.sum.actions',
    'reports.col.state', 'reports.col.opened', 'reports.col.closed', 'reports.col.movements',
    'reports.col.expectedCash', 'reports.col.countedCash', 'reports.col.difference',
    'reports.col.total', 'reports.col.paid', 'reports.col.refund', 'reports.col.lines', 'reports.col.qty',
    'reports.col.created', 'reports.col.paidAt', 'reports.col.seat', 'reports.col.device',
    'reports.col.playerKind', 'reports.col.duration', 'reports.col.revenue',
    'reports.col.source', 'reports.col.opType', 'reports.col.impact', 'reports.col.reason',
    'reports.col.operator', 'reports.col.action', 'reports.col.outcome', 'reports.col.count',
    'reports.col.first', 'reports.col.last'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the journal keys', () => {
  for (const key of [
    'nav.journal',
    'journal.noAccess', 'journal.empty', 'journal.limitNote',
    'journal.filter.action', 'journal.filter.outcome', 'journal.filter.targetType',
    'journal.filter.apply', 'journal.filter.reset',
    'journal.outcome.all', 'journal.outcome.succeeded', 'journal.outcome.denied',
    'journal.col.date', 'journal.col.actor', 'journal.col.action', 'journal.col.target',
    'journal.col.outcome', 'journal.col.source', 'journal.col.details', 'journal.actor.system'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the profile + install keys', () => {
  for (const key of [
    'profile.identity.title', 'profile.field.displayName', 'profile.field.organization',
    'profile.field.staffId', 'profile.field.role', 'profile.branches.title', 'profile.branches.empty',
    'profile.permissions.title', 'profile.permissions.empty', 'profile.editUnavailable',
    'install.title', 'install.subtitle', 'install.download',
    'install.wizard.title', 'install.wizard.step1', 'install.wizard.step2',
    'install.wizard.step3', 'install.wizard.step4',
    'install.branches.title', 'install.branches.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the platform admin keys', () => {
  for (const key of [
    'nav.group.controlPlane', 'nav.group.platformAccount',
    'nav.platform.clubs', 'nav.platform.money', 'nav.platform.journal', 'nav.platform.profile',
    'platform.clubs.title', 'platform.clubs.view.label', 'platform.clubs.view.now',
    'platform.clubs.view.all', 'platform.clubs.view.debt',
    'platform.clubs.empty.now', 'platform.clubs.empty.all', 'platform.clubs.empty.debt',
    'platform.plan.starter', 'platform.plan.growth', 'platform.plan.scale'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the organization card keys', () => {
  for (const key of [
    'platform.organization.status.suspended',
    'platform.organization.subscription.pastDue',
    'platform.organization.section.status',
    'platform.organization.planForm.apply',
    'platform.organization.limitsForm.maxBranches',
    'platform.organization.action.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the platform profile keys', () => {
  for (const key of [
    'platform.profile.field.userName', 'platform.profile.field.adminId',
    'platform.profile.roles.title', 'platform.profile.roles.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the new-organization keys', () => {
  for (const key of [
    'platform.newOrganization.section.organization', 'platform.newOrganization.section.branch',
    'platform.newOrganization.section.plan', 'platform.newOrganization.section.limits', 'platform.newOrganization.section.owner',
    'platform.newOrganization.field.orgSlug', 'platform.newOrganization.field.orgSlugHint', 'platform.newOrganization.field.orgName',
    'platform.newOrganization.field.branchSlug', 'platform.newOrganization.field.branchName', 'platform.newOrganization.field.branchCity',
    'platform.newOrganization.field.planCode', 'platform.newOrganization.field.subscriptionStatus',
    'platform.newOrganization.field.maxBranches', 'platform.newOrganization.field.maxDevices',
    'platform.newOrganization.field.maxSessions', 'platform.newOrganization.field.maxStaff',
    'platform.newOrganization.field.ownerUserName', 'platform.newOrganization.field.ownerDisplayName',
    'platform.newOrganization.sub.trial', 'platform.newOrganization.sub.active', 'platform.newOrganization.sub.pastDue', 'platform.newOrganization.sub.cancelled',
    'platform.newOrganization.submit', 'platform.newOrganization.submitting', 'platform.newOrganization.cancel',
    'platform.newOrganization.created', 'platform.newOrganization.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the organization health keys', () => {
  for (const key of [
    'platform.organization.section.health', 'platform.organization.health.refresh',
    'platform.organization.health.status', 'platform.organization.health.branches', 'platform.organization.health.devices',
    'platform.organization.health.activeStaff', 'platform.organization.health.lastSignIn', 'platform.organization.health.latestMigration',
    'platform.organization.health.recentErrors', 'platform.organization.health.recentErrorsEmpty',
    'platform.organization.health.col.time', 'platform.organization.health.col.source', 'platform.organization.health.col.action',
    'platform.organization.health.col.outcome', 'platform.organization.health.col.message', 'platform.organization.health.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the network section keys', () => {
  for (const key of [
    'op.shell.navGroup.network',
    'op.network.dest.branches', 'op.network.dest.branches.subtitle',
    'op.network.dest.billing', 'op.network.dest.billing.subtitle',
    'op.network.dest.install', 'op.network.dest.install.subtitle',
    'op.network.dest.journal', 'op.network.dest.journal.subtitle',
    'op.network.noAccess', 'op.network.placeholder',
    'op.network.install.get.title', 'op.network.install.get.lead', 'op.network.install.download', 'op.network.install.noUrl',
    'op.network.install.steps.title',
    'op.network.install.step.run', 'op.network.install.step.signIn', 'op.network.install.step.branch',
    'op.network.install.step.role', 'op.network.install.step.name', 'op.network.install.step.done',
    'op.network.install.branches.title', 'op.network.install.branches.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
    expect(messages.tg[key]).toBeTruthy();
  }
});

it('includes the network billing (subscription) screen keys', () => {
  for (const key of [
    'op.network.billing.subscription',
    'op.network.billing.plan',
    'op.network.billing.status',
    'op.network.billing.amount',
    'op.network.billing.period',
    'op.network.billing.nextInvoice',
    'op.network.billing.invoices',
    'op.network.billing.invoices.empty',
    'op.network.billing.col.number',
    'op.network.billing.col.issued',
    'op.network.billing.col.due',
    'op.network.billing.col.amount',
    'op.network.billing.col.status',
    'op.network.billing.subStatus.trial',
    'op.network.billing.subStatus.active',
    'op.network.billing.subStatus.pastDue',
    'op.network.billing.subStatus.cancelled',
    'op.network.billing.invStatus.issued',
    'op.network.billing.invStatus.paid',
    'op.network.billing.invStatus.void',
    'op.network.billing.invStatus.overdue'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
    expect(messages.tg[key]).toBeTruthy();
  }
});

it('includes the network journal (org-audit) screen keys', () => {
  for (const key of [
    'op.network.journal.actor.system',
    'op.network.journal.empty',
    'op.network.journal.limitNote',
    'op.network.journal.range.today',
    'op.network.journal.range.7d',
    'op.network.journal.range.30d',
    'op.network.journal.range.from',
    'op.network.journal.range.to',
    'op.network.journal.filter.action',
    'op.network.journal.filter.targetType',
    'op.network.journal.filter.outcome',
    'op.network.journal.outcome.all',
    'op.network.journal.outcome.succeeded',
    'op.network.journal.outcome.denied',
    'op.network.journal.filter.apply',
    'op.network.journal.filter.reset',
    'op.network.journal.col.date',
    'op.network.journal.col.actor',
    'op.network.journal.col.action',
    'op.network.journal.col.target',
    'op.network.journal.col.outcome',
    'op.network.journal.col.source',
    'op.network.journal.col.details'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
    expect(messages.tg[key]).toBeTruthy();
  }
});

it('includes the network branches rollup screen keys', () => {
  for (const key of [
    'op.network.branches.unnamed',
    'op.network.branches.totals.branches',
    'op.network.branches.kpi.devices',
    'op.network.branches.kpi.sessions',
    'op.network.branches.kpi.revenue',
    'op.network.branches.kpi.attention',
    'op.network.branches.card.error',
    'op.network.branches.empty',
    'op.network.branches.rename',
    'op.network.branches.rename.title',
    'op.network.branches.field.name',
    'op.network.branches.field.city',
    'op.network.branches.add',
    'op.network.branches.add.unavailable'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
    expect(messages.tg[key]).toBeTruthy();
  }
});
