import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

GlobalRegistrator.register({ url: 'http://localhost/' });

// Провал проверки печатает полученный элемент. bun печатал DOM-узел целиком — через ownerDocument
// весь документ, а у отрисованного React ещё и дерево компонентов: мегабайты текста и минуты
// работы в нативном коде. Тест, у которого первая попытка waitFor не удалась (под нагрузкой кнопка
// оживала на миг позже), висел по пять минут. Узел печатается своим HTML, обрезанным.
(Node.prototype as unknown as Record<symbol, unknown>)[Symbol.for('nodejs.util.inspect.custom')] = function printShort(this: Node) {
  const text = this instanceof Element ? this.outerHTML : `#${this.nodeName} ${JSON.stringify(this.textContent ?? '')}`;
  return text.length > 300 ? `${text.slice(0, 300)}…` : text;
};
expect.extend(matchers);

const { cleanup, configure } = await import('@testing-library/react');
// Ожидание асинхронных проверок — пять секунд вместо секунды по умолчанию.
//
// Секунды хватает на свободной машине и не хватает на занятой: ленивые экраны, таймеры
// happy-dom и рендер под нагрузкой уезжают за неё, и краснеет здоровый тест — обычно в чужом
// PR. Запас ничего не ослабляет: проходящая проверка проходит так же быстро, дольше становится
// только рассказ о настоящей поломке.
// Пятнадцать, а не пять: сам тест бун обрывает на двадцати секундах (--timeout 20000 в
// package.json), и пятисекундное ожидание внутри просто не давало этому запасу сработать —
// под нагрузкой параллельного прогона краснели здоровые тесты.
configure({ asyncUtilTimeout: 15000 });


afterEach(() => {
  cleanup();
  // Isolate locale persistence between tests (the provider seeds from localStorage).
  try { localStorage.clear(); } catch { /* no localStorage in this env */ }
});
