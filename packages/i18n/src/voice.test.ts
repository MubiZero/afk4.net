import { it, expect } from 'bun:test';
import { messages, type Locale } from './messages';

// Enforces the copy glossary: docs/product/copy-voice-and-terminology.md

const LOCALES: Locale[] = ['ru', 'en', 'tg'];

// A Cyrillic ALL-CAPS word of 4+ letters is shouting (brand tone forbids caps).
// Short acronyms like «ПК» (2 letters) are intentionally allowed.
const SHOUT = /[А-ЯЁ]{4,}/;
// The gaming machine is «ПК», never «компьютер».
const FORBIDDEN_COMPUTER = /компьютер/i;
// «Код доступа» is reserved: the glossary gives that meaning to the six-digit PIN, so a
// one-time invitation code must not borrow the phrase — the two are entered in different
// places and confusing them is a support call.
const FORBIDDEN_ACCESS_CODE = /код[а-я]*\s+доступа/i;

// Остальной канон из того же документа. Он был записан в документе и держался на памяти
// того, кто пишет строку: «сторно» дожило в значке истории клиента до сентября, хотя
// выписка игрока уже говорила «Отмена операции» — одно событие, два разных слова на
// соседних экранах, и человек спрашивает, одно ли это и то же.
const GLOSSARY: { name: string; forbidden: RegExp; instead: string }[] = [
  { name: 'сторно', forbidden: /сторно/i, instead: 'отмена операции' },
  // Человек за стойкой клуба — администратор (решение владельца 2026-09-23). «Оператор» жил в
  // панели и в приложении игрока одновременно с «администратором»: игрок читал «подойдите к
  // администратору» на одном экране и «позовите оператора» на соседнем.
  { name: 'оператор', forbidden: /оператор/i, instead: 'администратор' },
  { name: 'operator', forbidden: /\boperators?\b/i, instead: 'admin' },
  { name: 'тенант', forbidden: /тенант/i, instead: 'организация' },
  { name: 'клуб-клиент', forbidden: /клуб-клиент/i, instead: 'организация' },
  { name: 'имперсонация', forbidden: /имперсонац/i, instead: 'режим поддержки' },
  { name: 'инвайт', forbidden: /инвайт/i, instead: 'код приглашения' },
  { name: 'дашборд', forbidden: /дашборд/i, instead: 'пульс' },
  { name: 'ручная коррекция', forbidden: /ручн[а-я]*\s+коррекц/i, instead: 'поправка вручную' },
  { name: 'компьютерный клуб', forbidden: /компьютерн[а-я]*\s+клуб/i, instead: 'киберклуб' },
  // Пароля в системе нет вовсе — вход везде шестизначный ПИН. Слово на экране заставляет
  // человека искать поле, которого не существует.
  { name: 'пароль', forbidden: /парол[ья]/i, instead: 'ПИН-код' },
  // Программа клуба у человека одна, и имя у неё одно — «Панель AFK4.net», как на ярлыке
  // (решение владельца 2026-09-23). Мастер звал её четырьмя именами: «панель клуба»,
  // «приложение клуба», «панель управляющего» и просто «панель», — а по-английски ещё
  // «dashboard», «admin panel», «club app» и «Organization Admin». Человек читал «создайте зал
  // в приложении клуба» и открывал приложение игрока. «Панель клуба» тоже ушла: рядом с
  // ярлыком «Панель AFK4.net» она читается как вторая программа.
  { name: 'панель клуба', forbidden: /панел[а-я]*\s+клуба/i, instead: 'Панель AFK4.net' },
  { name: 'панели клуб', forbidden: /панели\s+клуб/i, instead: 'Панели AFK4.net' },
  { name: 'club panel', forbidden: /club panel/i, instead: 'AFK4.net Panel' },
  { name: 'Organization Admin', forbidden: /Organization Admin/, instead: 'Панель AFK4.net' },
  { name: 'приложение клуба', forbidden: /приложени[а-я]*\s+клуба/i, instead: 'Панель AFK4.net' },
  { name: 'панель управляющего', forbidden: /панел[а-я]*\s+управляющ/i, instead: 'Панель AFK4.net' },
  { name: 'админка', forbidden: /админк/i, instead: 'Панель AFK4.net' },
  { name: 'барномаи клуб', forbidden: /барномаи\s+клуб/i, instead: 'Панели AFK4.net' },
  { name: 'панели идора', forbidden: /панели\s+идора/i, instead: 'Панели AFK4.net' }
];

const offenders = (match: RegExp): string[] => {
  const hits: string[] = [];
  for (const loc of LOCALES) {
    for (const [key, value] of Object.entries(messages[loc])) {
      if (match.test(value)) hits.push(`${loc}:${key} = "${value}"`);
    }
  }
  return hits;
};

it('has no Cyrillic ALL-CAPS shouting in catalog values (brand tone: no caps)', () => {
  expect(offenders(SHOUT)).toEqual([]);
});

it('uses «ПК», never «компьютер» (terminology glossary)', () => {
  expect(offenders(FORBIDDEN_COMPUTER)).toEqual([]);
});

it('keeps «код доступа» for the PIN alone (terminology glossary)', () => {
  expect(offenders(FORBIDDEN_ACCESS_CODE)).toEqual([]);
});

it.each(GLOSSARY)('keeps «$name» out of the catalog (use «$instead»)', ({ forbidden }) => {
  expect(offenders(forbidden)).toEqual([]);
});

// В мастере «панель» — всегда Панель AFK4.net, и названа она полностью. Голое «в панели» рядом
// со строкой «в Панели AFK4.net» читается как второе место: какой панели? В других поверхностях
// бывают «панель платформы» и «панель кассы», поэтому правило держится только за мастер.
it('мастер зовёт Панель AFK4.net одним полным именем', () => {
  const wizard = (loc: Locale) =>
    Object.entries(messages[loc]).filter(([key]) => key.startsWith('setup.wizard.'));
  const hits: string[] = [];
  const check = (loc: Locale, bad: RegExp) => {
    for (const [key, value] of wizard(loc)) if (bad.test(value)) hits.push(`${loc}:${key} = "${value}"`);
  };
  check('ru', /панел[а-я]*(?!\s+AFK4\.net)(?![а-я])/i);
  check('tg', /панел[а-яӣӯҳқғҷ]*(?!\s+AFK4\.net)(?![а-яӣӯҳқғҷ])/i);
  check('en', /(?<!AFK4\.net )panel|dashboard|club app|Organization Admin/i);

  expect(hits).toEqual([]);
});

// Мастер установки назывался четырьмя именами: «мастер установки» в заголовке, «Мастер настройки»
// в Панели AFK4.net, «приложение установки» на экране входа и голое «Setup Wizard» посреди
// русской фразы, а по-таджикски ещё «Мастер», «устоди танзим» и «барномаи насб». Администратор
// читал «подключите ПК через Мастер настройки» и не находил такой программы. Имя одно, как в
// заголовке мастера: «мастер установки», «setup wizard», «устоди насб». Английское «AFK4.NET
// Setup Wizard» допустимо только в кавычках — так подписаны окно и ярлык в меню «Пуск».
it('мастер установки зовётся одним именем во всех поверхностях', () => {
  const hits: string[] = [];
  const check = (loc: Locale, bad: RegExp) => {
    for (const [key, value] of Object.entries(messages[loc])) if (bad.test(value)) hits.push(`${loc}:${key} = "${value}"`);
  };
  const shortcut = /«AFK4\.NET Setup Wizard»/g;
  const outsideShortcut = (bad: RegExp) => ({ test: (value: string) => bad.test(value.replace(shortcut, '')) }) as RegExp;
  check('ru', /мастер[а-я]*\s+настройк|приложени[а-я]*\s+установк/i);
  check('ru', outsideShortcut(/setup\s+wizard/i));
  // «Устои насб», «усторо» — опечатка, которая прожила на экране сбоя: «усто» без «д» — другое слово.
  check('tg', /(?<![а-яӣӯҳқғҷ])мастер|(?<![а-яӣӯҳқғҷ])усто(?!д)|устод[а-яӣӯ]*\s+танзим|барномаи\s+насб/i);
  check('tg', outsideShortcut(/setup\s+wizard/i));
  check('en', /setup app/i);

  expect(hits).toEqual([]);
});
