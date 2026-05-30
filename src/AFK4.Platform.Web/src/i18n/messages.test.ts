import { it, expect } from 'vitest';
import { messages } from './messages';

it('ru and en have identical key sets', () => {
  expect(Object.keys(messages.en).sort()).toEqual(Object.keys(messages.ru).sort());
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
