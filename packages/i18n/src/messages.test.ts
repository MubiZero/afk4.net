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
    // «Кешбэк» и «Тариф» — заимствования, в таджикском пишутся так же; переводить их нечем.
    'customer.loyalty.title', 'customer.reservations.tariff',
    'roles.operator', 'roles.technician',
    'op.network.billing.col.number',
    'auth.admin.title', 'account.phone.placeholder', 'clients.field.phone', 'op.network.billing.plan',
    'platform.health.queue.billing_outbox', 'customer.profile.langEn', 'customer.receipt.openLink', 'customer.signin.phone',
    'op.network.branches.kpi.devices', 'platform.analytics.month.3', 'platform.analytics.month.5', 'journal.actor.system',
    'journal.col.target', 'op.network.dest.journal', 'op.network.journal.actor.system', 'op.network.journal.col.target',
    'ledger.type.reversal', 'op.players.history.reversalBadge', 'op.management.dest.club', 'platform.dynamics.branch.label',
    'op.eskhata.title', 'op.eskhata.baseUrl', 'op.eskhata.companyId', 'op.eskhata.merchantId',
    'op.eskhata.hashKey', 'op.dc.title', 'op.auth.operator', 'op.booking.source.operator',
    'op.cash.title', 'op.club.field.telegram', 'op.club.field.instagram', 'op.club.ph.city',
    'op.club.ph.phone', 'op.club.ph.telegram', 'op.club.ph.website', 'op.club.ph.instagram',
    'op.command.stage.cashier', 'op.floor.duration.secShort', 'op.floor.remaining.pcOffline', 'op.helper.appVer.agent',
    'op.helper.appVer.shell', 'op.helper.audit.system', 'op.helper.billing.package', 'op.helper.deviceStatus.online',
    'op.helper.player.packageCount', 'op.helper.player.packageFallback', 'op.helper.player.platform', 'op.helper.player.tariffFallback',
    'op.helper.pos.receiptNumber', 'op.helper.pos.receiptType.fallback', 'op.helper.pos.saleState.fallback', 'op.helper.staff.operator',
    'op.helper.staff.technician', 'op.helper.update.channel.beta', 'op.helper.update.channel.fallback', 'op.helper.zone.bootcamp',
    'op.management.halls.addSeatCta', 'op.management.halls.col.seatName', 'op.management.tariffs.addTariffCta', 'op.management.tariffs.addPackageCta',
    'op.management.tariffs.col.bonus', 'op.management.staff.col.login', 'op.management.goods.col.sku', 'op.map.panel.packageLabel',
    'op.map.panel.tariffLabel', 'op.news.col.branch', 'op.news.fieldBranch', 'op.players.editProfile.phoneLabel',
    'op.players.profile.packageFallback', 'op.pos.catalog.categoryFallback', 'op.pos.catalog.title', 'op.pos.fixture.cola',
    'op.pos.fixture.hotdog', 'op.pos.receipts.emptyPlatform', 'op.pos.receipts.receiptFallback', 'op.settings.devices.detail.agent',
    'op.settings.devices.detail.shell', 'op.settings.devices.newCredentialEmpty', 'op.settings.devices.offline', 'op.settings.devices.online',
    'op.settings.layout.seatCount', 'op.settings.layout.seatFallback', 'op.settings.packages.packageFallback', 'op.settings.pos.category',
    'op.settings.pos.sku', 'op.settings.prefill.categoryNameIndexed', 'op.settings.prefill.tariffNameIndexed', 'op.settings.tariffs.tariffFallback',
    'op.shell.nav.dashboard', 'op.stock.journal.csv.sku', 'platform.billing.column.number', 'platform.billing.column.plan',
    'platform.billing.column.organization', 'platform.newOrganization.field.planCode', 'platform.newOrganization.section.plan', 'platform.plan.growth',
    'platform.plan.scale', 'platform.plan.starter', 'platform.organization.subscriptionForm.plan', 'op.helper.update.component.organizationAdmin',
    'setup.wizard.finished.summary.branch', 'setup.wizard.stepper.branch', 'platform.organization.tab.history', 'platform.settings.column.twoFactor',
    // Чистый шаблон склейки «{day}, {time}» — переводить в нём нечего.
    'customer.common.dayAtTime'
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

it('includes the shared action keys', () => {
  for (const key of ['venue.title', 'common.save', 'common.cancel'] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the new settings/operators/roles keys', () => {
  for (const key of [
    'operators.col.name'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the new branches keys', () => {
  for (const key of [
    'branches.unnamed', 'branches.totals.branches', 'branches.rename',
    'branches.rename.title', 'branches.add', 'branches.add.unavailable',
    'branches.card.error', 'branches.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});


it('includes the monetization + tariffs keys', () => {
  for (const key of [
    'tariffs.col.name', 'tariffs.col.price', 'tariffs.col.minMinutes',
    'tariffs.col.rounding'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});



it('includes the clients/CRM keys', () => {
  for (const key of [
    'clients.search.placeholder', 'clients.search.label', 'clients.field.phone',
    'ledger.type.top_up', 'ledger.type.gameplay_charge', 'ledger.type.package_purchase',
    'ledger.type.package_consumption', 'ledger.type.bonus_grant', 'ledger.type.bonus_consumption',
    'ledger.type.refund', 'ledger.type.manual_correction', 'ledger.type.postpaid_debt',
    'ledger.type.debt_payment', 'ledger.type.reversal', 'ledger.account.package_time',
    'ledger.account.bonus_time'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the money-operations keys', () => {
  for (const key of [
    'money.correction', 'money.refund'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});


it('includes the reports keys', () => {
  for (const key of [
    'reports.noAccess', 'reports.empty', 'reports.export',
    'reports.col.expectedCash', 'reports.col.countedCash', 'reports.col.difference',
    'reports.col.count'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the journal keys', () => {
  for (const key of [
    'journal.empty', 'journal.limitNote', 'journal.filter.action',
    'journal.filter.outcome', 'journal.filter.targetType', 'journal.filter.apply',
    'journal.filter.reset', 'journal.outcome.all', 'journal.outcome.succeeded',
    'journal.outcome.denied', 'journal.col.date', 'journal.col.actor',
    'journal.col.action', 'journal.col.target', 'journal.col.outcome',
    'journal.col.source', 'journal.col.details', 'journal.actor.system'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the profile + install keys', () => {
  for (const key of [
    'profile.permissions.title', 'install.subtitle', 'install.download',
    'install.branches.title', 'install.branches.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the platform admin keys', () => {
  for (const key of [
    'nav.platform.clubs', 'nav.platform.money', 'nav.platform.journal',
    'platform.clubs.title', 'platform.clubs.view.label', 'platform.clubs.view.now',
    'platform.clubs.view.all', 'platform.clubs.view.debt', 'platform.clubs.empty.now',
    'platform.clubs.empty.all', 'platform.clubs.empty.debt', 'platform.plan.starter',
    'platform.plan.growth', 'platform.plan.scale'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the organization card keys', () => {
  for (const key of [
    'platform.organization.status.suspended', 'platform.organization.subscription.pastDue', 'platform.organization.section.status',
    'platform.organization.limitsForm.maxBranches', 'platform.organization.action.error'
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
    'platform.organization.section.health', 'platform.organization.health.refresh', 'platform.organization.health.branches',
    'platform.organization.health.devices', 'platform.organization.health.activeStaff', 'platform.organization.health.lastSignIn',
    'platform.organization.health.recentErrors', 'platform.organization.health.recentErrorsEmpty', 'platform.organization.health.col.time',
    'platform.organization.health.col.source', 'platform.organization.health.col.action', 'platform.organization.health.col.outcome',
    'platform.organization.health.col.message', 'platform.organization.health.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the network section keys', () => {
  for (const key of [
    'op.shell.navGroup.network', 'op.network.dest.branches', 'op.network.dest.branches.subtitle',
    'op.network.dest.billing', 'op.network.dest.billing.subtitle', 'op.network.dest.install',
    'op.network.dest.install.subtitle', 'op.network.dest.journal', 'op.network.dest.journal.subtitle',
    'op.network.noAccess', 'op.network.install.get.title', 'op.network.install.get.lead',
    'op.network.install.download', 'op.network.install.noUrl', 'op.network.install.steps.title',
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
