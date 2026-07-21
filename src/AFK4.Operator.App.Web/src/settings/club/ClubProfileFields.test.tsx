import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClubProfileFields, type ClubProfileForm } from './ClubProfileFields';
import { defaultWorkingHours } from './workingHours';

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

const form: ClubProfileForm = {
  name: 'AFK4 Центр',
  city: 'Душанбе',
  description: '',
  address: '',
  phone: '',
  telegram: '',
  website: '',
  logoUrl: null,
  logoMediaId: null,
  timeZone: 'Asia/Dushanbe',
  locale: 'ru',
  workingHours: defaultWorkingHours()
};

afterEach(cleanup);

describe('ClubProfileFields', () => {
  it('renders name value and section titles', () => {
    render(
      <I18nProvider initialLocale="ru">
        <ClubProfileFields form={form} currencyCode="TJS" backend={backend} onField={() => {}} />
      </I18nProvider>
    );
    expect(screen.getByDisplayValue('AFK4 Центр')).toBeInTheDocument();
    expect(screen.getByText('Адрес и контакты')).toBeInTheDocument();
    expect(screen.getByText('TJS')).toBeInTheDocument();
  });

  it('editing name calls onField', () => {
    const onField = mock((_k: unknown, _v: unknown) => {});
    render(
      <I18nProvider initialLocale="ru">
        <ClubProfileFields form={form} currencyCode="TJS" backend={backend} onField={onField} />
      </I18nProvider>
    );
    fireEvent.change(screen.getByDisplayValue('AFK4 Центр'), { target: { value: 'AFK4 X' } });
    expect(onField).toHaveBeenCalledWith('name', 'AFK4 X');
  });
});
