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
  { name: 'тенант', forbidden: /тенант/i, instead: 'организация' },
  { name: 'клуб-клиент', forbidden: /клуб-клиент/i, instead: 'организация' },
  { name: 'имперсонация', forbidden: /имперсонац/i, instead: 'режим поддержки' },
  { name: 'инвайт', forbidden: /инвайт/i, instead: 'код приглашения' },
  { name: 'дашборд', forbidden: /дашборд/i, instead: 'пульс' },
  { name: 'ручная коррекция', forbidden: /ручн[а-я]*\s+коррекц/i, instead: 'поправка вручную' },
  { name: 'компьютерный клуб', forbidden: /компьютерн[а-я]*\s+клуб/i, instead: 'киберклуб' },
  // Пароля в системе нет вовсе — вход везде шестизначный ПИН. Слово на экране заставляет
  // человека искать поле, которого не существует.
  { name: 'пароль', forbidden: /парол[ья]/i, instead: 'ПИН-код' }
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
