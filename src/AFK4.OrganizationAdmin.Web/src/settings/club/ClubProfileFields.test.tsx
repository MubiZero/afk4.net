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
  instagram: '',
  logoUrl: null,
  logoMediaId: null,
  coverImageUrl: null,
  coverMediaId: null,
  latitude: '',
  longitude: '',
  timeZone: 'Asia/Dushanbe',
  locale: 'ru',
  workingHours: defaultWorkingHours()
};

afterEach(cleanup);

describe('ClubProfileFields', () => {
  it('renders name value and section titles', () => {
    render(
      <I18nProvider initialLocale="ru">
        <ClubProfileFields form={form} currencyCode="TJS" backend={backend} onField={() => {}} preview={null} />
      </I18nProvider>
    );
    expect(screen.getByDisplayValue('AFK4 Центр')).toBeInTheDocument();
    expect(screen.getByText('Адрес и контакты')).toBeInTheDocument();
    expect(screen.getByDisplayValue('TJS')).toBeInTheDocument();
  });

  it('editing name calls onField', () => {
    const onField = mock((_k: unknown, _v: unknown) => {});
    render(
      <I18nProvider initialLocale="ru">
        <ClubProfileFields form={form} currencyCode="TJS" backend={backend} onField={onField} preview={null} />
      </I18nProvider>
    );
    fireEvent.change(screen.getByDisplayValue('AFK4 Центр'), { target: { value: 'AFK4 X' } });
    expect(onField).toHaveBeenCalledWith('name', 'AFK4 X');
  });

  // Координаты ставят клуб на карту в приложении игрока. В форме они живут строкой: числом
  // их делает сборка запроса, где пустое поле читается как «не задано».
  it('координаты набираются посимвольно, вместе с точкой', () => {
    const onField = mock((_k: unknown, _v: unknown) => {});
    render(
      <I18nProvider initialLocale="ru">
        <ClubProfileFields form={form} currencyCode="TJS" backend={backend} onField={onField} preview={null} />
      </I18nProvider>
    );

    const latitude = screen.getByPlaceholderText('38.5598');
    fireEvent.change(latitude, { target: { value: '38.' } });

    // Набранное уходит в форму как есть: превращение в число на полпути съело бы точку и
    // дробную часть было бы уже не дописать.
    expect(onField).toHaveBeenCalledWith('latitude', '38.');
  });
});
