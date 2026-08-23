import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

GlobalRegistrator.register({ url: 'http://localhost/' });
expect.extend(matchers);

const { cleanup, configure } = await import('@testing-library/react');
// Ожидание асинхронных проверок — пять секунд вместо секунды по умолчанию.
//
// Секунды хватает на свободной машине и не хватает на занятой: ленивые экраны, таймеры
// happy-dom и рендер под нагрузкой уезжают за неё, и краснеет здоровый тест — обычно в чужом
// PR. Запас ничего не ослабляет: проходящая проверка проходит так же быстро, дольше становится
// только рассказ о настоящей поломке.
configure({ asyncUtilTimeout: 5000 });


afterEach(() => {
  cleanup();
  // Isolate locale persistence between tests (the i18n provider seeds from localStorage).
  try { localStorage.clear(); } catch { /* no localStorage in this env */ }
});
