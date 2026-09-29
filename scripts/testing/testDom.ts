// Одна настройка DOM для всех веб-тестов (приложения и пакеты). Регистратор передаётся снаружи:
// @happy-dom/global-registrator стоит в каждом рабочем пространстве, а не в корне.

type Registrator = { register(options: object): void };

export function registerTestDom(registrator: Registrator, url: string): void {
  registrator.register({
    url,
    settings: {
      fetch: {
        interceptor: {
          // Тест не выходит в сеть. Раньше запрос, начатый к концу теста (размонтированный экран
          // дочитывал историю игрока), уходил уже штатным fetch happy-dom на localhost, где никого
          // нет, — ECONNREFUSED в логе и время прогона на ожидание сокета. Теперь такой запрос
          // сразу падает, как в браузере без сети, и называет адрес.
          beforeAsyncRequest: async ({ request }: { request: { url: string } }) => {
            throw new TypeError(`Тест не ходит в сеть: ${request.url}`);
          }
        }
      }
    }
  });

  // Провал проверки печатает полученный элемент. bun печатал DOM-узел целиком — через ownerDocument
  // весь документ, а у отрисованного React ещё и дерево компонентов: мегабайты текста и минуты
  // работы в нативном коде. Тест, у которого первая попытка waitFor не удалась (под нагрузкой кнопка
  // оживала на миг позже), висел по пять минут. Узел печатается своим HTML, обрезанным.
  (Node.prototype as unknown as Record<symbol, unknown>)[Symbol.for('nodejs.util.inspect.custom')] = function printShort(this: Node) {
    const text = this instanceof Element ? this.outerHTML : `#${this.nodeName} ${JSON.stringify(this.textContent ?? '')}`;
    return text.length > 300 ? `${text.slice(0, 300)}…` : text;
  };
}
