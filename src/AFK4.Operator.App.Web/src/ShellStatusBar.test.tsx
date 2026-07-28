import { I18nProvider } from '@afk4/i18n';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { ShellStatusBar } from './ShellStatusBar';

describe('ShellStatusBar', () => {
  it('renders the permanent authoritative system fields', () => {
    render(
      <I18nProvider initialLocale="ru">
        <ShellStatusBar
          operatorName="Иванов И.И."
          roleNames={['operator']}
          clubName="Арена"
          realtimeState="connected"
          realtimeError={null}
          dataSource="backend"
          appVersion="2.45.1"
          workspaceFeedback={null}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Иванов И.И.')).toBeInTheDocument();
    expect(screen.getByText('Оператор')).toBeInTheDocument();
    expect(screen.getByText('Арена')).toBeInTheDocument();
    expect(screen.getByText('Онлайн')).toBeInTheDocument();
    expect(screen.getByText('OK')).toBeInTheDocument();
    expect(screen.getByText('2.45.1')).toBeInTheDocument();
    expect(screen.getByText(/^\d{2}:\d{2}$/)).toBeInTheDocument();
    expect(screen.queryByText(/Касса:/)).not.toBeInTheDocument();
  });
});
