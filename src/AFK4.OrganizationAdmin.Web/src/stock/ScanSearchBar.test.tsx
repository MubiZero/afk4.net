import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { Search } from 'lucide-react';
import { ScanSearchBar } from './ScanSearchBar';

describe('ScanSearchBar', () => {
  it('рендерит поле поиска и бейдж «Сканер активен»; зовёт onChange', () => {
    const onChange = mock((_value: string) => {});
    render(
      <I18nProvider initialLocale="ru">
        <ScanSearchBar
          icon={<Search size={16} aria-hidden="true" />}
          value=""
          onChange={onChange}
          placeholder="Название или SKU…"
          ariaLabel="Добавить товар"
        />
      </I18nProvider>
    );
    expect(screen.getByLabelText('Сканер активен')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Добавить товар'), { target: { value: 'cola' } });
    expect(onChange).toHaveBeenCalledWith('cola');
  });

  it('trailing-контент рендерится в полосе', () => {
    render(
      <I18nProvider initialLocale="ru">
        <ScanSearchBar
          icon={<Search size={16} aria-hidden="true" />}
          value=""
          onChange={() => {}}
          placeholder="Поиск товара"
          ariaLabel="Поиск товара"
          trailing={<button type="button">Сброс</button>}
        />
      </I18nProvider>
    );
    expect(screen.getByRole('button', { name: 'Сброс' })).toBeInTheDocument();
  });
});
