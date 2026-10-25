import { describe, expect, it, jest } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { ShellI18nProvider } from './i18n/ShellI18nProvider';
import { ShellErrorBoundary } from './ShellErrorBoundary';

function Broken(): never {
  throw new Error('render failed');
}

describe('ShellErrorBoundary', () => {
  it('вместо белого экрана говорит, что перезапускается, и перезагружает страницу', () => {
    jest.useFakeTimers();
    const reload = jest.fn();
    const consoleError = jest.spyOn(console, 'error').mockImplementation(() => {});
    try {
      render(
        <ShellI18nProvider>
          <ShellErrorBoundary reload={reload}>
            <Broken />
          </ShellErrorBoundary>
        </ShellI18nProvider>
      );

      expect(screen.getByRole('alert').textContent).toBe('Экран перезапускается…');
      expect(reload).not.toHaveBeenCalled();
      jest.advanceTimersByTime(3_000);
      expect(reload).toHaveBeenCalledTimes(1);
    } finally {
      consoleError.mockRestore();
      jest.useRealTimers();
    }
  });
});
