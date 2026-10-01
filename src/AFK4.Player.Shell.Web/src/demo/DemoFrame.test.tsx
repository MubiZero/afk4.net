import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen } from '@testing-library/react';
import { ShellI18nProvider } from '../i18n/ShellI18nProvider';
import type { DevHostControl, DevScenario } from '../host/devHost';
import { DemoBand, DemoFrame } from './DemoFrame';

function fakeControl(initial: DevScenario = 'idle', touch: () => void = () => {}) {
  let scenario = initial;
  const watchers = new Set<() => void>();
  const control: DevHostControl = {
    touch,
    getScenario: () => scenario,
    subscribe: (listener) => {
      watchers.add(listener);
      return () => watchers.delete(listener);
    },
    setScenario: mock((next: DevScenario) => {
      scenario = next;
      for (const watcher of watchers) watcher();
    })
  };
  return control;
}

describe('DemoBand', () => {
  it('says the club is made up and switches scenarios by their Russian names', () => {
    const control = fakeControl();
    render(<ShellI18nProvider><DemoBand control={control} /></ShellI18nProvider>);

    expect(screen.getByRole('group', { name: 'Демо' }).textContent).toContain('ничего не уходит в сеть');
    const select = screen.getByRole('combobox', { name: 'Что на экране' }) as HTMLSelectElement;
    expect(Array.from(select.options).map((option) => option.textContent)).toContain('Нет связи, сессия идёт');
    expect(select.options.length).toBe(9);

    fireEvent.change(select, { target: { value: 'maintenance' } });
    expect(control.setScenario).toHaveBeenCalledWith('maintenance');
    expect(select.value).toBe('maintenance');
  });

  it('starts over from a clean page and links to the Panel demo', () => {
    const reload = mock(() => {});
    localStorage.setItem('afk4-demo-leftover', '1');
    sessionStorage.setItem('afk4-demo-leftover', '1');
    render(<ShellI18nProvider><DemoBand control={fakeControl()} reload={reload} /></ShellI18nProvider>);

    fireEvent.click(screen.getByRole('button', { name: 'Начать заново' }));

    expect(localStorage.getItem('afk4-demo-leftover')).toBeNull();
    expect(sessionStorage.getItem('afk4-demo-leftover')).toBeNull();
    expect(reload).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('link', { name: 'Панель AFK4.NET' }).getAttribute('href')).toBe('../');
  });
});

describe('DemoFrame', () => {
  // Любая клавиша на свободном ПК — «подошли». Tab и клавиши самой полосы — нет: иначе окно входа
  // перехватывало бы фокус, пока клавиатурный посетитель идёт к переключателю сценариев.
  it('treats typing as walking up to the PC, but not Tab and not the band', () => {
    const touch = mock(() => {});
    render(<ShellI18nProvider><DemoFrame control={fakeControl('idle', touch)}><p>экран</p></DemoFrame></ShellI18nProvider>);

    fireEvent.keyDown(window, { key: 'Tab' });
    fireEvent.keyDown(screen.getByRole('combobox'), { key: 'ArrowDown' });
    expect(touch).not.toHaveBeenCalled();

    fireEvent.keyDown(screen.getByText('экран'), { key: 'a' });
    expect(touch).toHaveBeenCalledTimes(1);
  });
});
