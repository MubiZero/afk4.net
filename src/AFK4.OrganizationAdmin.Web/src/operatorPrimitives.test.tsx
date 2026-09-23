import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CriticalActionConfirmation, EmptyState, Skeleton } from './operatorPrimitives';

afterEach(cleanup);

describe('Skeleton', () => {
  it('renders a block placeholder hidden from a11y, keeping a custom class', () => {
    const { container } = render(<Skeleton className="seat-skeleton" />);
    const block = container.querySelector('.skeleton-block');
    expect(block).not.toBeNull();
    expect(block).toHaveClass('seat-skeleton');
    expect(block).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders the requested number of text lines', () => {
    const { container } = render(<Skeleton variant="text" lines={3} />);
    expect(container.querySelectorAll('.skeleton-block')).toHaveLength(3);
  });

  it('renders a circle variant', () => {
    const { container } = render(<Skeleton variant="circle" />);
    expect(container.querySelector('.skeleton-circle')).not.toBeNull();
  });
});

describe('EmptyState', () => {
  it('renders title and description', () => {
    render(<EmptyState title="Нет ПК" description="Смените фильтр" next={{ kind: 'calm', hint: 'Появятся после установки.' }} />);
    expect(screen.getByText('Нет ПК')).toBeInTheDocument();
    expect(screen.getByText('Смените фильтр')).toBeInTheDocument();
  });

  it('renders an action button that fires onClick', () => {
    const onClick = mock(() => {});
    render(<EmptyState title="Пусто" next={{ kind: 'action', label: 'Создать', onClick }} />);
    fireEvent.click(screen.getByText('Создать'));
    expect(onClick).toHaveBeenCalled();
  });

  // Спокойная пустота, шаг в другом месте и шаг не для этого человека — три разных «кнопки нет»,
  // и каждая обязана сказать своё: что здесь появится, где это делается, у кого есть право.
  it.each([
    ['calm', 'Заявки появятся здесь, как только игрок их подаст.'],
    ['elsewhere', 'Товары заводятся в Управлении → Товары.'],
    ['denied', 'Это делает управляющий или владелец организации.']
  ] as const)('%s: no button, and the hint says what to expect', (kind, hint) => {
    const { container } = render(<EmptyState title="Пусто" next={{ kind, hint }} />);
    expect(screen.getByText(hint)).toBeInTheDocument();
    expect(container.querySelector('button')).toBeNull();
  });

  it('renders an icon when provided', () => {
    const { container } = render(<EmptyState title="Пусто" icon={<svg data-testid="ico" />} next={{ kind: 'calm', hint: 'x' }} />);
    expect(container.querySelector('.empty-state-icon')).not.toBeNull();
    expect(container.querySelector('[data-testid="ico"]')).not.toBeNull();
  });

  // Строка внутри панели, где под пустой список есть место на одну строчку: вёрстка чужая,
  // решение то же самое.
  it('inline: one line in the caller class, action as a quiet button', () => {
    const onClick = mock(() => {});
    const { container } = render(
      <EmptyState inline className="cash-shift-empty-note" title="Движений нет" next={{ kind: 'action', label: 'Сбросить фильтр', onClick }} />
    );
    const line = container.querySelector('p.cash-shift-empty-note');
    expect(line).not.toBeNull();
    expect(line?.textContent).toContain('Движений нет');
    fireEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));
    expect(onClick).toHaveBeenCalled();
  });

  // Главное свойство: пустое состояние без решения о следующем шаге не собирается. Если строка
  // ниже вдруг скомпилируется, директива сама станет ошибкой, и `tsc -b` упадёт.
  it('cannot be written without a decision about the next step', () => {
    // @ts-expect-error — next обязателен: «Нет товаров» без следующего шага больше не пишется
    const silent = <EmptyState title="Нет товаров" />;
    // @ts-expect-error — «нет кнопки по правам» обязано назвать, у кого право есть
    const denied = <EmptyState title="Нет товаров" next={{ kind: 'denied' }} />;
    // @ts-expect-error — «делается в другом месте» обязано назвать, где
    const elsewhere = <EmptyState title="Нет товаров" next={{ kind: 'elsewhere' }} />;
    // @ts-expect-error — «пусто, и это нормально» обязано сказать, что здесь появится
    const calm = <EmptyState title="Нет заявок" next={{ kind: 'calm' }} />;
    expect([silent, denied, elsewhere, calm]).toHaveLength(4);
  });
});

// Подтверждение стоит в потоке экрана: в разделах со списком и карточкой оно вставало под
// списком, за краем видимой области. Появившись, оно показывается и забирает фокус — на «Отмену».
describe('CriticalActionConfirmation', () => {
  it('scrolls itself into view and focuses the cancel button, not the dangerous one', () => {
    const scrolled = mock(() => {});
    const original = HTMLElement.prototype.scrollIntoView;
    HTMLElement.prototype.scrollIntoView = scrolled;
    try {
      render(
        <I18nProvider initialLocale="ru">
          <CriticalActionConfirmation title="Снять?" detail="Марина" impact="Роли снимутся" confirmLabel="Снять" onConfirm={() => {}} onCancel={() => {}} />
        </I18nProvider>
      );
      expect(scrolled).toHaveBeenCalled();
      expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Отмена' }));
    } finally {
      HTMLElement.prototype.scrollIntoView = original;
    }
  });
});
