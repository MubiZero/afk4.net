import { describe, expect, it } from 'bun:test';
import { PlayerApiError } from '../api/playerApi';
import { clubTime, durationKey, packageMinuteOptions, startErrorKey } from './offers';

describe('предложения времени', () => {
  it('из пакета — те же часы, пока хватает, и всё оставшееся', () => {
    expect(packageMinuteOptions(200)).toEqual([60, 120, 180, 200]);
    expect(packageMinuteOptions(300)).toEqual([60, 120, 180, 300]);
    expect(packageMinuteOptions(45)).toEqual([45]);
    expect(packageMinuteOptions(0)).toEqual([]);
  });

  it('длительность — часами и минутами', () => {
    expect(durationKey(120)).toEqual({ key: 'playerShell.duration.hours', values: { hours: 2 } });
    expect(durationKey(200)).toEqual({ key: 'playerShell.duration.hoursMinutes', values: { hours: 3, minutes: 20 } });
    expect(durationKey(30)).toEqual({ key: 'playerShell.duration.minutes', values: { minutes: 30 } });
  });

  it('«до скольки» — по часам клуба, а не ПК', () => {
    expect(clubTime('2026-09-25T15:40:00Z', 'Asia/Dushanbe', 'ru')).toBe('20:40');
  });

  it('отказы старта — своими словами, и после денежного цены перечитываются', () => {
    expect(startErrorKey(new PlayerApiError(409, 'insufficient_balance'))).toEqual({
      key: 'playerShell.chooseTime.error.insufficient',
      reload: true
    });
    expect(startErrorKey(new PlayerApiError(409, 'device_in_maintenance')).key).toBe('playerShell.chooseTime.error.maintenance');
    expect(startErrorKey(new TypeError('Failed to fetch')).key).toBe('playerShell.chooseTime.error.offline');
  });
});
