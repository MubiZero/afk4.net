import { describe, expect, it } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider, useI18n } from './I18nProvider';

function Probe() {
  const { t, locale, setLocale, formatCurrency } = useI18n();
  return (
    <div>
      <span>nav:{t('nav.overview')}</span>
      <span>loc:{locale}</span>
      <span>money:{formatCurrency(4250, 'TJS')}</span>
      <button onClick={() => setLocale('en')}>en</button>
    </div>
  );
}

describe('i18n', () => {
  it('defaults to Russian', () => {
    render(<I18nProvider><Probe /></I18nProvider>);
    expect(screen.getByText('nav:Обзор')).toBeInTheDocument();
    expect(screen.getByText('loc:ru')).toBeInTheDocument();
  });
  it('switches to English', () => {
    render(<I18nProvider><Probe /></I18nProvider>);
    fireEvent.click(screen.getByRole('button'));
    expect(screen.getByText('nav:Overview')).toBeInTheDocument();
  });
  it('returns the key when a translation is missing', () => {
    render(<I18nProvider><MissingProbe /></I18nProvider>);
    expect(screen.getByText('out:does.not.exist')).toBeInTheDocument();
  });
  it('seeds the initial locale from localStorage', () => {
    localStorage.setItem('afk4.locale', 'en');
    render(<I18nProvider><Probe /></I18nProvider>);
    expect(screen.getByText('loc:en')).toBeInTheDocument();
  });
  it('clamps an unknown persisted locale to ru', () => {
    localStorage.setItem('afk4.locale', 'zz');
    render(<I18nProvider><Probe /></I18nProvider>);
    expect(screen.getByText('loc:ru')).toBeInTheDocument();
  });
  it('persists the locale to localStorage on change', () => {
    render(<I18nProvider><Probe /></I18nProvider>);
    fireEvent.click(screen.getByRole('button')); // setLocale('en')
    expect(localStorage.getItem('afk4.locale')).toBe('en');
  });
});

function MissingProbe() {
  const { t } = useI18n();
  return <span>out:{t('does.not.exist' as never)}</span>;
}
