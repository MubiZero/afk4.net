import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

GlobalRegistrator.register({ url: 'http://localhost/' });

// Провал проверки печатает полученный элемент. bun печатал DOM-узел целиком — через ownerDocument
// весь документ: мегабайты текста и минуты работы. Узел печатается своим HTML, обрезанным.
(Node.prototype as unknown as Record<symbol, unknown>)[Symbol.for('nodejs.util.inspect.custom')] = function printShort(this: Node) {
  const text = this instanceof Element ? this.outerHTML : `#${this.nodeName} ${JSON.stringify(this.textContent ?? '')}`;
  return text.length > 300 ? `${text.slice(0, 300)}…` : text;
};
expect.extend(matchers);

const { cleanup } = await import('@testing-library/react');

afterEach(() => {
  cleanup();
});
