import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { DeviceHealthIssueNames, DiskHealthNames, type DeviceHealthDto } from '@afk4/contracts';
import { DeviceHealthReadings } from './DeviceHealthReadings';

afterEach(cleanup);

const health: DeviceHealthDto = {
  cpuLoadPercent: 35,
  memoryUsedPercent: 60,
  boardTemperatureC: null,
  gpus: [{ name: 'NVIDIA GeForce RTX 3060', temperatureC: 96, fanPercent: 100, loadPercent: 98 }],
  disks: [{ model: 'Samsung SSD 980 1TB', health: DiskHealthNames.Healthy, temperatureC: 41, wearPercent: 7, powerOnHours: 5200, readErrorsUncorrected: 0 }],
  volumes: [{ name: 'C:', freeGb: 120, sizeGb: 476, isSystem: true }],
  wakeOnLan: { fastStartupOff: true, nicWakeEnabled: false }
};

describe('здоровье ПК', () => {
  it('называет проблему словами и показывает показатели; чего нет — не рисует', () => {
    render(<I18nProvider><DeviceHealthReadings health={health} reportedAtUtc="2026-10-28T10:00:00Z" issue={DeviceHealthIssueNames.GpuOverheating} /></I18nProvider>);

    expect(screen.getByRole('status').textContent).toContain('Видеокарта перегревается');
    expect(screen.getByText('96 °C · вентилятор 100% · загрузка 98%')).toBeDefined();
    expect(screen.getByText('исправен · 41 °C · износ 7%')).toBeDefined();
    expect(screen.getByText('свободно 120 из 476 ГБ')).toBeDefined();
    // Датчика платы нет — и строки нет, а не «—».
    expect(screen.queryByText('Плата (ACPI)')).toBeNull();
    // Сетевая карта не умеет будить — так и сказано, а не «готово».
    expect(screen.getByText('не готово: сетевая карта не умеет будить ПК')).toBeDefined();
  });
});
