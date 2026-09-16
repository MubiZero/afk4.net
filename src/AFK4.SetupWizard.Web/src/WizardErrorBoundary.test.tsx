import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, spyOn } from 'bun:test';
import { WizardErrorBoundary } from './WizardErrorBoundary';

function Boom({ explode }: { explode: boolean }): React.ReactElement {
  if (explode) {
    throw new Error('render exploded');
  }

  return <p>Экран мастера</p>;
}

function renderBoundary(explode: boolean) {
  // React печатает пойманное исключение в консоль сам — в выводе тестов это выглядит как сбой,
  // которого нет.
  const consoleError = spyOn(console, 'error').mockImplementation(() => {});
  const result = render(
    <WizardErrorBoundary message="Мастер установки споткнулся." retryLabel="Попробовать ещё раз">
      <Boom explode={explode} />
    </WizardErrorBoundary>,
  );
  return { ...result, consoleError };
}

describe('WizardErrorBoundary', () => {
  afterEach(cleanup);

  it('обычный экран показывает как есть', () => {
    const { consoleError } = renderBoundary(false);

    expect(screen.getByText('Экран мастера')).toBeInTheDocument();
    consoleError.mockRestore();
  });

  // Мастер живёт в WebView2 без адресной строки и без кнопки «обновить», а во время установки
  // придерживает и закрытие окна: белый прямоугольник — это тупик.
  it('вместо белого окна говорит, что произошло', () => {
    const { consoleError } = renderBoundary(true);

    expect(screen.getByRole('alert')).toHaveTextContent('Мастер установки споткнулся.');
    expect(screen.getByRole('button', { name: 'Попробовать ещё раз' })).toBeInTheDocument();
    consoleError.mockRestore();
  });

  it('повтор возвращает экран, когда он снова рисуется', () => {
    const consoleError = spyOn(console, 'error').mockImplementation(() => {});
    let explode = true;
    function Flaky() {
      if (explode) {
        throw new Error('render exploded');
      }

      return <p>Экран мастера</p>;
    }

    render(
      <WizardErrorBoundary message="Мастер установки споткнулся." retryLabel="Попробовать ещё раз">
        <Flaky />
      </WizardErrorBoundary>,
    );

    explode = false;
    fireEvent.click(screen.getByRole('button', { name: 'Попробовать ещё раз' }));

    expect(screen.getByText('Экран мастера')).toBeInTheDocument();
    consoleError.mockRestore();
  });
});
