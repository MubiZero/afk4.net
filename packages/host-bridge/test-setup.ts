import { GlobalRegistrator } from '@happy-dom/global-registrator';

// Мост живёт в окне: ему нужны window.chrome, setTimeout и crypto.randomUUID.
GlobalRegistrator.register({ url: 'http://localhost/' });

// Провал проверки печатает полученный элемент. bun печатал DOM-узел целиком — через ownerDocument
// весь документ, а у отрисованного React ещё и дерево компонентов: мегабайты текста и минуты
// работы в нативном коде. Тест, у которого первая попытка waitFor не удалась (под нагрузкой кнопка
// оживала на миг позже), висел по пять минут. Узел печатается своим HTML, обрезанным.
(Node.prototype as unknown as Record<symbol, unknown>)[Symbol.for('nodejs.util.inspect.custom')] = function printShort(this: Node) {
  const text = this instanceof Element ? this.outerHTML : `#${this.nodeName} ${JSON.stringify(this.textContent ?? '')}`;
  return text.length > 300 ? `${text.slice(0, 300)}…` : text;
};
