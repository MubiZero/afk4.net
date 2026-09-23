import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

// happy-dom must be registered before @testing-library is imported: testing-library's
// `screen` binds to `document.body` at module-evaluation time, so a dynamic import keeps
// that evaluation after the DOM globals exist.
GlobalRegistrator.register({ url: 'https://player.afk4.local/' });

// Провал проверки печатает полученный элемент. bun печатал DOM-узел целиком — через ownerDocument
// весь документ, а у отрисованного React ещё и дерево компонентов: мегабайты текста и минуты
// работы в нативном коде. Тест, у которого первая попытка waitFor не удалась (под нагрузкой кнопка
// оживала на миг позже), висел по пять минут. Узел печатается своим HTML, обрезанным.
(Node.prototype as unknown as Record<symbol, unknown>)[Symbol.for('nodejs.util.inspect.custom')] = function printShort(this: Node) {
  const text = this instanceof Element ? this.outerHTML : `#${this.nodeName} ${JSON.stringify(this.textContent ?? '')}`;
  return text.length > 300 ? `${text.slice(0, 300)}…` : text;
};
expect.extend(matchers);

// Tell React 18+ that this is a test environment so it flushes state updates
// synchronously after async callbacks, making waitFor() reliable without flushSync.
(globalThis as any).IS_REACT_ACT_ENVIRONMENT = true;

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
});
