import { describe, expect, it, mock } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { clientMenuActions } from './ClientActionsMenu';

// Клавиатура, фокус и закрытие меню — у RowActions кита и проверяются там. Здесь — только какие
// пункты получает оператор и в каком порядке.
const t = createTranslator('ru');

const build = (over: Partial<Parameters<typeof clientMenuActions>[1]> = {}) =>
  clientMenuActions(t, { isActive: true, canManageClient: true, onEditProfile: () => {}, onToggleActive: () => {}, ...over });

describe('clientMenuActions', () => {
  it('edit and deactivate for a manager, deactivate marked dangerous', () => {
    const items = build();
    expect(items.map((item) => item.label)).toEqual(['Править профиль', 'Деактивировать']);
    expect(items.at(-1)?.danger).toBe(true);
  });

  it('offers activation, not a danger item, for an inactive client', () => {
    const toggle = build({ isActive: false }).at(-1)!;
    expect(toggle.label).toBe('Активировать');
    expect(toggle.danger).toBe(false);
  });

  it('is empty without any permission — the kit then draws no «⋯» at all', () => {
    expect(build({ canManageClient: false })).toEqual([]);
  });

  it('orders reservation first and correction before deactivate when permitted', () => {
    const onCreateReservation = mock(() => {});
    const items = build({ canCreateReservation: true, onCreateReservation, canCorrect: true, onCorrect: () => {}, canSellPackage: true, onSellPackage: () => {} });
    expect(items.map((item) => item.label)).toEqual([
      'Создать бронь',
      'Продать пакет',
      'Править профиль',
      'Ручная корректировка',
      'Деактивировать',
    ]);
    items[0].onSelect();
    expect(onCreateReservation).toHaveBeenCalled();
  });

  // «Посадить за ПК» — кнопка на виду в карточке, а не второй раз в меню.
  it('never repeats the seat-at-PC action that is already a visible button', () => {
    expect(build({ canCreateReservation: true, onCreateReservation: () => {} }).some((item) => item.id === 'startSession')).toBe(false);
  });

  it('omits reservation and correction without their permissions even for a manager', () => {
    const labels = build({ canCreateReservation: false, canCorrect: false }).map((item) => item.label);
    expect(labels).not.toContain('Создать бронь');
    expect(labels).not.toContain('Ручная корректировка');
  });
});
