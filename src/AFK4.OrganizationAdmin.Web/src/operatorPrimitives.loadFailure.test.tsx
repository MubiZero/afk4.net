import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { createTranslator, I18nProvider } from '@afk4/i18n';
import type { ReactNode } from 'react';
import { projectOperatorError } from './apiErrors';
import { PlatformApiError } from './platformApi';
import { LoadFailureState, PartialLoadFailure } from './operatorPrimitives';
import { ManagementScreen } from './management/ManagementScreen';
import { SectionState } from './network/SectionState';

afterEach(cleanup);

const t = createTranslator('ru');
const refusal = (status: number) => new PlatformApiError('failed', status, 'Status', '');
const failure = (status: number) => projectOperatorError(refusal(status), t);
const renderRu = (ui: ReactNode) => render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);
const accessHint = 'Попросите доступ у управляющего или владельца организации.';

// Отказ по правам не чинится кнопкой: ответ останется тем же, сколько её ни жми. Кнопка, которая
// обещает обратное, уводит от единственного настоящего действия — попросить доступ, — поэтому на
// 403 её нет, а вместо неё названо, к кому идти. Сбой сервера проходит, и кнопка на месте.
describe('LoadFailureState', () => {
  it('на 403 не предлагает повтор и говорит, к кому идти за доступом', () => {
    const onRetry = mock(() => {});
    renderRu(<LoadFailureState title="Не удалось загрузить" failure={failure(403)} onRetry={onRetry} />);

    expect(screen.getByText('Недостаточно прав для этого действия.')).toBeInTheDocument();
    expect(screen.getByText(accessHint)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).toBeNull();
  });

  it('на 503 предлагает повтор и не говорит про доступ', () => {
    const onRetry = mock(() => {});
    renderRu(<LoadFailureState title="Не удалось загрузить" failure={failure(503)} onRetry={onRetry} />);

    expect(screen.queryByText(accessHint)).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('на 404 не предлагает повтор и не зовёт к управляющему', () => {
    renderRu(<LoadFailureState title="Не удалось загрузить" failure={failure(404)} onRetry={() => {}} />);

    expect(screen.queryByRole('button', { name: 'Повторить' })).toBeNull();
    expect(screen.queryByText(accessHint)).toBeNull();
  });
});

describe('PartialLoadFailure', () => {
  it('на 403 не предлагает повтор и говорит, к кому идти за доступом', () => {
    const projection = failure(403);
    renderRu(<PartialLoadFailure text={`Не удалось загрузить филиалы. ${projection.detail}`} failure={projection} onRetry={() => {}} />);

    expect(screen.getByRole('alert')).toHaveTextContent(accessHint);
    expect(screen.queryByRole('button', { name: 'Повторить' })).toBeNull();
  });

  it('на 503 предлагает повтор, который перезапрашивает эту часть', () => {
    const onRetry = mock(() => {});
    const projection = failure(503);
    renderRu(<PartialLoadFailure text={`Не удалось загрузить филиалы. ${projection.detail}`} failure={projection} onRetry={onRetry} />);

    expect(screen.getByRole('alert')).not.toHaveTextContent(accessHint);
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});

describe('ManagementScreen', () => {
  it('на 403 вместо тела показывает причину и подсказку, без повтора', () => {
    renderRu(
      <ManagementScreen title="Отчёт" subtitle="s" state="error" failure={failure(403)} onRetry={() => {}}>
        <p>тело</p>
      </ManagementScreen>
    );

    expect(screen.getByText(accessHint)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).toBeNull();
    expect(screen.queryByText('тело')).toBeNull();
  });

  it('на 503 показывает повтор', () => {
    const onRetry = mock(() => {});
    renderRu(
      <ManagementScreen title="Отчёт" subtitle="s" state="error" failure={failure(503)} onRetry={onRetry}>
        <p>тело</p>
      </ManagementScreen>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});

describe('SectionState', () => {
  it('на 403 не предлагает повтор и говорит, к кому идти за доступом', () => {
    renderRu(<SectionState section={{ status: 'error', error: refusal(403), retry: () => {} }} failedTitle="Не удалось загрузить счета" />);

    expect(screen.getByText(accessHint)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).toBeNull();
  });

  it('на 503 предлагает повтор', () => {
    const retry = mock(() => {});
    renderRu(<SectionState section={{ status: 'error', error: refusal(503), retry }} failedTitle="Не удалось загрузить счета" />);

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(retry).toHaveBeenCalledTimes(1);
  });
});
