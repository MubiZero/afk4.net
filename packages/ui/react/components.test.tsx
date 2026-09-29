import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { Button, CloseButton, IconButton } from './Button';
import { CountChip, FilterChip, StatusBadge } from './chips';
import { EmptyState, LoadFailure } from './states';
import { Money, Num } from './numbers';
import { SectionHeader } from './SectionHeader';
import { Inspector } from './Inspector';
import { useBlockedReason } from './BlockedReason';

describe('Button', () => {
  it('maps a role onto the kit classes and is never a stray submit', () => {
    render(<Button variant="primary" size="lg" block>Сохранить</Button>);
    const button = screen.getByRole('button', { name: 'Сохранить' });
    expect(button.className).toBe('ui-btn ui-btn--primary ui-btn--lg ui-btn--block');
    expect(button).toHaveAttribute('type', 'button');
  });

  it('defaults to secondary: the main button has to be chosen on purpose', () => {
    render(<Button>Отмена</Button>);
    expect(screen.getByRole('button', { name: 'Отмена' }).className).toBe('ui-btn');
  });

  it('names an icon-only button for the screen reader and the mouse alike', () => {
    render(<IconButton label="Обновить" icon={<svg />} size="sm" />);
    const button = screen.getByRole('button', { name: 'Обновить' });
    expect(button).toHaveAttribute('title', 'Обновить');
    expect(button.className).toBe('ui-btn ui-btn--icon ui-btn--sm');
  });

  it('closes with one shared ✕', () => {
    const onClick = mock(() => {});
    render(<CloseButton label="Закрыть карточку" onClick={onClick} />);
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть карточку' }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});

describe('chips', () => {
  it('tells a pressed filter by aria-pressed, not by colour alone', () => {
    render(<FilterChip label="Есть долг" count={3} pressed onClick={() => {}} />);
    expect(screen.getByRole('button', { name: 'Есть долг 3' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('draws a count and a status from the fixed vocabulary', () => {
    const { container } = render(<><CountChip label="Долги" value="35 с." tone="critical" /><StatusBadge tone="danger">Долг</StatusBadge></>);
    expect(container.querySelector('.ui-chip--count.is-critical')).toHaveTextContent('Долги35 с.');
    expect(screen.getByText('Долг')).toHaveClass('ui-chip--status', 'is-danger');
  });
});

describe('EmptyState', () => {
  it('offers the next step as a button', () => {
    const onClick = mock(() => {});
    render(<EmptyState title="Товаров пока нет" next={{ kind: 'action', label: 'Добавить товар', onClick }} />);
    fireEvent.click(screen.getByRole('button', { name: 'Добавить товар' }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('names who holds the right instead of a button that would be refused', () => {
    const { container } = render(<EmptyState title="Сотрудников нет" next={{ kind: 'denied', hint: 'Пригласить может управляющий.' }} />);
    expect(container.querySelector('.empty-state button')).toBeNull();
    expect(screen.getByText('Пригласить может управляющий.')).toBeInTheDocument();
  });

  it('fits in one line when the panel has room for one', () => {
    const { container } = render(<EmptyState inline className="x" title="Броней нет." next={{ kind: 'calm', hint: 'Новые появятся здесь.' }} />);
    expect(container.querySelector('p.x')).toHaveTextContent('Броней нет. Новые появятся здесь.');
  });
});

describe('LoadFailure', () => {
  it('says what failed, why, and retries only when retry can help', () => {
    const onClick = mock(() => {});
    const { rerender } = render(<LoadFailure title="Счета не загрузились" detail="Сервер не ответил." retry={{ label: 'Повторить', onClick }} />);
    expect(screen.getByRole('alert')).toHaveTextContent('Счета не загрузилисьСервер не ответил.');
    // Заголовок панели состояния — заголовок и для диктора: по нему прыгают, как по заголовкам страницы.
    expect(screen.getByRole('heading', { level: 2, name: 'Счета не загрузились' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onClick).toHaveBeenCalledTimes(1);
    rerender(<LoadFailure title="Счета не загрузились" detail="Недостаточно прав." hint="Доступ выдаёт владелец." />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByRole('alert')).toHaveTextContent('Доступ выдаёт владелец.');
  });

  it('marks a failed part with one line and keeps the rest on screen', () => {
    render(<p>Баланс 450 с.</p>);
    render(<LoadFailure inline detail="История не загрузилась." retry={{ label: 'Повторить', onClick: () => {} }} />);
    expect(screen.getByRole('alert')).toHaveClass('ui-alert');
    expect(screen.getByText('Баланс 450 с.')).toBeInTheDocument();
  });
});

describe('numbers', () => {
  it('writes an unknown amount as a dash, not as a free «0 с.»', () => {
    render(<Money minorUnits={null} currencyCode="TJS" />);
    expect(screen.getByText('—')).toHaveClass('ui-money', 'ui-money--muted');
  });

  it('signs a movement with a real minus', () => {
    render(<><Money minorUnits={-1200} currencyCode="TJS" signed /><Money minorUnits={1200} currencyCode="TJS" signed /></>);
    expect(screen.getByText(/^−/)).toHaveClass('ui-money--neg');
    expect(screen.getByText(/^\+/)).toHaveClass('ui-money--pos');
  });

  it('keeps any other number in tabular figures', () => {
    render(<Num>3 из 10</Num>);
    expect(screen.getByText('3 из 10')).toHaveClass('ui-num');
  });
});

describe('SectionHeader', () => {
  it('lays out title, counts and one action, then tabs and tools', () => {
    const { container } = render(
      <SectionHeader
        title="Клиенты"
        counts={[{ label: 'Клиентов', value: 6 }, { label: 'Долги', value: '35 с.', tone: 'warning' }]}
        action={<Button variant="primary">Новый клиент</Button>}
        tabs={<div role="tablist" />}
        tools={<input aria-label="Поиск" />}
      />,
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Клиенты' })).toBeInTheDocument();
    expect(container.querySelectorAll('.ui-section-header-aside .ui-chip--count')).toHaveLength(2);
    const order = [...container.querySelector('.ui-section-header')!.children].map((node) => node.className || node.getAttribute('role'));
    expect(order).toEqual(['ui-section-header-main', 'tablist', 'ui-section-header-tools']);
  });
});

describe('Inspector', () => {
  it('is a region named by its title, with the menu and close in the head', () => {
    const onClose = mock(() => {});
    render(
      <Inspector
        title="Фариза Назарова"
        subtitle="+992 93 100 20 30"
        close={{ label: 'Закрыть карточку', onClose }}
        menu={{ label: 'Ещё', actions: [{ id: 'x', label: 'Ручная корректировка', onSelect: () => {} }] }}
        figure={{ label: 'Баланс', value: <Money minorUnits={45000} currencyCode="TJS" /> }}
      >
        <Inspector.Actions
          primary={<Button variant="primary" block>Пополнить</Button>}
          secondary={[<Button key="a">Бронь</Button>, <Button key="b">Сесть</Button>]}
        />
        <Inspector.Facts items={[{ label: 'Тариф', value: 'Growth' }]} />
        <Inspector.Section label="Пакеты"><p>Ночной 5ч</p></Inspector.Section>
      </Inspector>,
    );
    expect(screen.getByRole('complementary', { name: 'Фариза Назарова' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть карточку' }));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Ещё' })).toBeInTheDocument();
    expect(screen.getByRole('term')).toHaveTextContent('Тариф');
    expect(screen.getByRole('definition')).toHaveTextContent('Growth');
    expect(screen.getByRole('heading', { level: 3, name: 'Пакеты' })).toHaveClass('ui-section-label');
  });
});

describe('useBlockedReason', () => {
  function Pay({ reason }: { reason: string | null }) {
    const blocked = useBlockedReason(reason);
    return <><Button disabled={reason !== null} aria-describedby={blocked.describedBy}>Принять</Button>{blocked.hint}</>;
  }

  it('ties the reason to the button so it is read together with it', () => {
    render(<Pay reason="Сначала откройте смену." />);
    expect(screen.getByRole('button', { name: 'Принять' })).toHaveAccessibleDescription('Сначала откройте смену.');
  });

  it('says nothing when nothing blocks', () => {
    render(<Pay reason={null} />);
    expect(screen.getByRole('button', { name: 'Принять' })).not.toHaveAttribute('aria-describedby');
  });
});
