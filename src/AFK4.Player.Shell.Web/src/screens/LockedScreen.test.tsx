import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { LockedScreen } from './LockedScreen';
import type { PlayerShellState } from '../shellContracts';

const base: PlayerShellState = {
  organizationId: 'org', branchId: 'branch', deviceId: 'device',
  state: 'locked', sessionId: null, leaseExpiresAtUtc: null, remainingSeconds: null,
  isOnline: true, isGraceMode: false, warningThresholdSeconds: 300,
  message: 'Этот ПК свободен', launcherApps: [], locale: 'ru', warningKind: 'none'
};

// Код набирают с этого экрана в телефоне — если его не видно через зал, он бесполезен.
it('показывает код посадки крупно, когда сервер его прислал', () => {
  render(<LockedScreen state={{ ...base, seatingCode: '482913' }} onRequestOperator={() => {}} />);

  expect(screen.getByText('482913')).toBeInTheDocument();
  expect(screen.getByText(/введите код/i)).toBeInTheDocument();
});

// У занятой машины сервер код не выдаёт: звать к ней некого. Показать старый значит позвать
// человека туда, куда его не пустят.
it('молчит про код, когда сервер его не прислал', () => {
  render(<LockedScreen state={base} onRequestOperator={() => {}} />);

  expect(screen.queryByText(/введите код/i)).not.toBeInTheDocument();
});
