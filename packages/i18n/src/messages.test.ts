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
    'install.ownerCode.title', 'install.ownerCode.noAccess', 'install.ownerCode.none',
    'install.ownerCode.validUntil', 'install.ownerCode.lastUsed', 'install.ownerCode.failed',
    'install.ownerCode.generate', 'install.ownerCode.rotate', 'install.ownerCode.reason',
    'install.ownerCode.generated', 'install.ownerCode.rotated', 'install.ownerCode.error',
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
