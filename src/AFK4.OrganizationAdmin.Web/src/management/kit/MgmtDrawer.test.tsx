import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { MgmtDrawer } from './MgmtDrawer';

const wrap = (ui: React.ReactNode) => render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);

afterEach(cleanup);

describe('MgmtDrawer', () => {
  it('renders title, subtitle, body and footer', () => {
    wrap(
      <MgmtDrawer title="Зал А" subtitle="6 ПК" onClose={() => {}} footer={<span>подвал</span>}>
        <p>тело</p>
      </MgmtDrawer>
    );
    expect(screen.getByText('Зал А')).toBeTruthy();
    expect(screen.getByText('6 ПК')).toBeTruthy();
    expect(screen.getByText('тело')).toBeTruthy();
    expect(screen.getByText('подвал')).toBeTruthy();
  });

  it('calls onClose from the close button', () => {
    const onClose = mock(() => {});
    wrap(<MgmtDrawer title="Зал А" onClose={onClose}><i /></MgmtDrawer>);
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('renders a ⋯ menu when actions are given', () => {
    wrap(
      <MgmtDrawer title="Зал А" onClose={() => {}} actions={[{ id: 'del', label: 'Удалить зал', onSelect: () => {}, danger: true }]}>
        <i />
      </MgmtDrawer>
    );
    expect(screen.getByRole('button', { name: 'Действия' })).toBeTruthy();
  });
});
