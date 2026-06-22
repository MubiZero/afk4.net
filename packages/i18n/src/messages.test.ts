import { it, expect } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { messages } from './messages';

const localesDir = join(import.meta.dir, '..', '..', '..', 'locales');
const readLocale = (loc: string) => JSON.parse(readFileSync(join(localesDir, `${loc}.json`), 'utf8')) as Record<string, string>;

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
    'account.phone.placeholder',
    'clientPackages.field.package',
    'clients.col.phone',
    'clients.field.phone',
    'club.billing.subscription.plan',
    'club.billing.title',
    'customer.profile.langEn',
    'customer.receipt.openLink',
    'customer.signin.phone',
    'devices.status.offline',
    'devices.status.online',
    'floor.seatDefault',
    'floor.zoneDefault',
    'journal.actor.system',
    'journal.col.target',
    'ledger.type.reversal',
    // «сторно» — международный бухгалтерский термин-заимствование (как ledger.type.reversal)
    'op.players.history.reversalBadge',
    'op.players.pin.openBtn',
    'nav.billing',
    'nav.group.account',
    'nav.group.branch',
    'nav.group.platformAccount',
    'nav.platform.billing',
    'op.auth.operator',
    'op.booking.fallback.zeroSeats',
    'op.booking.seatsOne',
    'op.booking.source.operator',
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
    'op.helper.staff.cashierOperator',
    'op.helper.staff.technician',
    'op.helper.update.channel.beta',
    'op.helper.update.channel.fallback',
    'op.helper.zone.bootcamp',
    'op.logs.detailKey.branch',
    'op.logs.detailKey.device',
    'op.logs.detailKey.param',
    'op.logs.detailKey.sale',
    'op.logs.detailKey.session',
    'op.logs.detailKey.tariff',
    'op.logs.filter.phFrom',
    'op.logs.filter.phTo',
    'op.logs.kind.audit',
    'op.logs.logFilter.operator',
    'op.logs.ph.source.operator',
    'op.logs.ph.source.platform',
    'op.logs.row.component',
    'op.logs.row.operator',
    'op.logs.source.agent',
    'op.logs.source.operator',
    'op.logs.source.platform',
    'op.logs.sourceApp.agent',
    'op.logs.sourceApp.platform',
    'op.logs.target.branch',
    'op.logs.target.device',
    'op.logs.target.object',
    'op.logs.target.receipt',
    'op.logs.target.session',
    'op.logs.target.tariff',
    'op.logs.tone.session',
    'op.map.feedbackOffline',
    'op.map.panel.confirmStatusBilling',
    'op.map.panel.packageLabel',
    'op.map.panel.tariffLabel',
    'op.news.fieldBranch',
    'op.players.actions.packageBonus',
    'op.players.profile.packageFallback',
    'op.players.profile.platformSource',
    'op.players.segments.vip',
    'op.players.status.vip',
    'op.players.strip.platform',
    'op.pos.cart.newCardPhoneLabel',
    'op.pos.catalog.categoryFallback',
    'op.pos.catalog.title',
    'op.pos.fixture.cola',
    'op.pos.fixture.hotdog',
    'op.pos.quick.writeOffLabel',
    'op.pos.receipts.emptyPlatform',
    'op.pos.receipts.receiptFallback',
    'op.review.platformLabel',
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
    'operators.field.email',
    'operators.field.userName',
    'overview.attention.offline',
    'payments_cards.scope.branch',
    'platform.billing.column.number',
    'platform.billing.column.plan',
    'platform.billing.column.tenant',
    'platform.newTenant.field.planCode',
    'platform.newTenant.section.plan',
    'platform.overview.kpi.mrr',
    'platform.plan.growth',
    'platform.plan.scale',
    'platform.plan.starter',
    'platform.profile.field.userName',
    'platform.tenant.planForm.plan',
    'platform.tenant.subscriptionForm.plan',
    'platform.tenants.col.plan',
    'platform.tenants.filter.plan',
    'products.categoryUnknown',
    'products.col.category',
    'products.col.sku',
    'products.field.category',
    'products.field.sku',
    'reports.col.operator',
    'roles.cashier_operator',
    'roles.technician',
    'setup.wizard.finished.summary.branch',
    'setup.wizard.stepper.branch',
]);

it('tg has no silent ru-copies (untranslated strings posing as Tajik)', () => {
  const ru = messages.ru as Record<string, string>;
  const tg = messages.tg as Record<string, string>;
  const offenders = Object.keys(ru).filter(
    (k) => ru[k].trim() !== '' && tg[k] === ru[k] && !TG_IDENTICAL_TO_RU_ALLOWED.has(k)
  );
  expect(offenders).toEqual([]);
});

it('TG_IDENTICAL_TO_RU_ALLOWED has no stale entries (every listed key is actually tg===ru)', () => {
  const ru = messages.ru as Record<string, string>;
  const tg = messages.tg as Record<string, string>;
  const stale = [...TG_IDENTICAL_TO_RU_ALLOWED].filter((k) => tg[k] !== ru[k]);
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
    'nav.platform.overview', 'nav.platform.tenants', 'nav.platform.billing', 'nav.platform.profile',
    'platform.overview.kpi.tenants', 'platform.overview.kpi.active', 'platform.overview.kpi.suspended',
    'platform.overview.kpi.trial', 'platform.overview.kpi.branches', 'platform.overview.kpi.new30d',
    'platform.overview.byPlan.title', 'platform.overview.attention.title', 'platform.overview.attention.empty',
    'platform.overview.attention.suspended', 'platform.overview.attention.pastDue',
    'platform.plan.starter', 'platform.plan.growth', 'platform.plan.scale'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the tenants admin keys', () => {
  for (const key of [
    'platform.tenants.search',
    'platform.tenants.col.name',
    'platform.tenant.status.suspended',
    'platform.tenant.subscription.pastDue',
    'platform.tenant.section.status',
    'platform.tenant.planForm.apply',
    'platform.tenant.limitsForm.maxBranches',
    'platform.tenant.action.error'
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

it('includes the new-tenant keys', () => {
  for (const key of [
    'platform.newTenant.section.organization', 'platform.newTenant.section.branch',
    'platform.newTenant.section.plan', 'platform.newTenant.section.limits', 'platform.newTenant.section.owner',
    'platform.newTenant.field.orgSlug', 'platform.newTenant.field.orgSlugHint', 'platform.newTenant.field.orgName',
    'platform.newTenant.field.branchSlug', 'platform.newTenant.field.branchName', 'platform.newTenant.field.branchCity',
    'platform.newTenant.field.planCode', 'platform.newTenant.field.subscriptionStatus',
    'platform.newTenant.field.maxBranches', 'platform.newTenant.field.maxDevices',
    'platform.newTenant.field.maxSessions', 'platform.newTenant.field.maxStaff',
    'platform.newTenant.field.ownerUserName', 'platform.newTenant.field.ownerDisplayName',
    'platform.newTenant.sub.trial', 'platform.newTenant.sub.active', 'platform.newTenant.sub.pastDue', 'platform.newTenant.sub.cancelled',
    'platform.newTenant.submit', 'platform.newTenant.submitting', 'platform.newTenant.cancel',
    'platform.newTenant.created', 'platform.newTenant.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the tenant health keys', () => {
  for (const key of [
    'platform.tenant.section.health', 'platform.tenant.health.refresh',
    'platform.tenant.health.status', 'platform.tenant.health.branches', 'platform.tenant.health.devices',
    'platform.tenant.health.activeStaff', 'platform.tenant.health.lastSignIn', 'platform.tenant.health.latestMigration',
    'platform.tenant.health.recentErrors', 'platform.tenant.health.recentErrorsEmpty',
    'platform.tenant.health.col.time', 'platform.tenant.health.col.source', 'platform.tenant.health.col.action',
    'platform.tenant.health.col.outcome', 'platform.tenant.health.col.message', 'platform.tenant.health.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
