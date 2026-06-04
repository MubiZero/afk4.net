import { it, expect } from 'bun:test';
import { messages, type Locale } from './messages';

// Enforces the copy glossary: docs/product/copy-voice-and-terminology.md

const LOCALES: Locale[] = ['ru', 'en', 'tg'];

// A Cyrillic ALL-CAPS word of 4+ letters is shouting (brand tone forbids caps).
// Short acronyms like «ПК» (2 letters) are intentionally allowed.
const SHOUT = /[А-ЯЁ]{4,}/;
// The gaming machine is «ПК», never «компьютер».
const FORBIDDEN_COMPUTER = /компьютер/i;

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
