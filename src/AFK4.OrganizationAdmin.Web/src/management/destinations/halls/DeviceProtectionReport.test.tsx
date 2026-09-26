import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { DeviceProtectionReportDto } from '@afk4/contracts';
import { DeviceProtectionReport } from './DeviceProtectionReport';

const report: DeviceProtectionReportDto = {
  version: 2,
  appliedAtUtc: '2026-09-25T10:00:00Z',
  items: [
    { item: 'kiosk-baseline', status: 'applied', detail: null },
    { item: 'removable-storage', status: 'failed', detail: 'Access is denied.' },
    { item: 'hidden-drives', status: 'explorer-only', detail: null }
  ]
};

function renderReport(value: DeviceProtectionReportDto | null, branchVersion = 2) {
  render(
    <I18nProvider initialLocale="ru">
      <DeviceProtectionReport report={value} branchVersion={branchVersion} />
    </I18nProvider>
  );
}

describe('DeviceProtectionReport', () => {
  afterEach(cleanup);

  // Спека, §6.3: скрытый диск — не запрещённый, и карточка ПК говорит ровно это.
  it('names each rule by what it really does on the PC', () => {
    renderReport(report);

    expect(screen.getByText('Меню Ctrl+Alt+Del урезано').nextSibling?.textContent).toBe('действует');
    expect(screen.getByText('Флешки и внешние диски').nextSibling?.textContent).toBe('не применилось');
    expect(screen.getByText('Скрытые диски').nextSibling?.textContent).toBe('скрыты только в Проводнике');
  });

  it('flags a PC that has not caught up with the branch version yet', () => {
    renderReport(report, 3);

    expect(screen.getByText(/ПК на версии 2, в филиале уже 3/)).toBeInTheDocument();
  });

  it('says so when the PC has not reported at all', () => {
    renderReport(null);

    expect(screen.getByText('ПК ещё не докладывал о защите.')).toBeInTheDocument();
  });
});
