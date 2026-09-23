import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { TariffScheduleFields } from './TariffScheduleFields';
import { ALL_HOURS } from './tariffSchedule';

afterEach(cleanup);

const renderFields = (from: string, to: string) =>
  render(
    <I18nProvider initialLocale="ru">
      <TariffScheduleFields value={{ ...ALL_HOURS, from, to }} onChange={() => {}} />
    </I18nProvider>
  );

// Владелец, поставивший 08:00–16:00, иначе ждёт, что в 16:00 сессия оборвётся или подорожает.
describe('TariffScheduleFields', () => {
  it('says a session started within the hours stays on this tariff to the end', () => {
    renderFields('08:00', '16:00');
    expect(screen.getByText(/до конца считается по этому тарифу/)).toBeInTheDocument();
  });

  it('does not talk about the start rule for a round-the-clock tariff', () => {
    renderFields('', '');
    expect(screen.queryByText(/до конца считается по этому тарифу/)).toBeNull();
  });
});
