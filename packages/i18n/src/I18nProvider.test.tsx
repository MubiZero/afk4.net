import { render, screen } from '@testing-library/react';
import { it, expect } from 'bun:test';
import { I18nProvider, useI18n } from './I18nProvider';
import type { Locale, MessageKey } from './messages';

function Probe({ messageKey, count }: { messageKey: MessageKey; count: number }) {
  const { t } = useI18n();
  return <span>{t(messageKey, { count })}</span>;
}

function renderProbe(locale: Locale, count: number) {
  render(
    <I18nProvider initialLocale={locale}>
      <Probe messageKey="op.dashboard.signals" count={count} />
    </I18nProvider>
  );
}

it('applies Russian plural forms via ICU', () => {
  renderProbe('ru', 1);
  expect(screen.getByText('1 сигнал')).toBeInTheDocument();
});

it('selects the Russian few/many forms by count', () => {
  const { rerender } = render(
    <I18nProvider initialLocale="ru"><Probe messageKey="op.dashboard.signals" count={2} /></I18nProvider>
  );
  expect(screen.getByText('2 сигнала')).toBeInTheDocument();
  rerender(<I18nProvider initialLocale="ru"><Probe messageKey="op.dashboard.signals" count={5} /></I18nProvider>);
  expect(screen.getByText('5 сигналов')).toBeInTheDocument();
});

it('applies English plural forms for the en locale', () => {
  renderProbe('en', 1);
  expect(screen.getByText('1 signal')).toBeInTheDocument();
  renderProbe('en', 2);
  expect(screen.getByText('2 signals')).toBeInTheDocument();
});

it('returns a plain message unchanged when called without values', () => {
  function Plain() {
    const { t } = useI18n();
    return <span>{t('auth.field.password')}</span>;
  }
  render(<I18nProvider initialLocale="ru"><Plain /></I18nProvider>);
  expect(screen.getByText('Пароль')).toBeInTheDocument();
});
