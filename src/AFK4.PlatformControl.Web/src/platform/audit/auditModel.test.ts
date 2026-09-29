import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { auditActionLabel, auditOutcomeLabel, auditSourceLabel } from './auditModel';

const t = createTranslator('ru');

// Карточка клуба на Пульте платформы (OrganizationHealthSection) и журнал сети показывают записи
// из клубов тем же кодом действия, что и Панель, — раньше здесь для них не было словаря вовсе,
// и «devices.commands.dispatch» оставался на экране кодом.
describe('auditActionLabel', () => {
  it('находит действие платформы в собственном каталоге', () => {
    expect(auditActionLabel('billing.plan.create', t)).toBe(t('platform.audit.action.billing.plan.create'));
  });

  it('падает в словарь клубных действий, если платформенного ключа нет', () => {
    expect(auditActionLabel('devices.commands.dispatch', t)).toBe(t('op.helper.audit.deviceCommand'));
  });

  it('возвращает null для кода, которого нет ни в одном словаре', () => {
    expect(auditActionLabel('no.such.action', t)).toBeNull();
  });
});

describe('auditSourceLabel / auditOutcomeLabel', () => {
  it('источник и исход — регистр сервера, не lowercase', () => {
    expect(auditSourceLabel('Agent', t)).toBe(t('platform.audit.source.Agent'));
    expect(auditOutcomeLabel('Denied', t)).toBe(t('journal.outcome.denied'));
  });
});
