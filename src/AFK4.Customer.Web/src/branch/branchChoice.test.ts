import { it, expect } from 'bun:test';
import { branchChoice } from './branchChoice';
import type { BrandingHallDto } from '@/api/types';

const hall = (branchId: string, name: string): BrandingHallDto =>
  ({ branchId, name, city: 'Душанбе', address: null });

it('у человека со счётом в клубе зал уже записан — спрашивать нечего', () => {
  const choice = branchChoice([hall('b1', 'На Рудаки'), hall('b2', 'В Худжанде')], null, 'b9');

  expect(choice.asks).toBe(false);
  expect(choice.unanswered).toBe(false);
  expect(choice.branchId).toBe('b9');
});

it('единственный зал сети сам себе ответ', () => {
  const choice = branchChoice([hall('b1', 'На Рудаки')], null, null);

  expect(choice.asks).toBe(false);
  expect(choice.unanswered).toBe(false);
  expect(choice.branchId).toBe('b1');
});

it('у сети из нескольких залов вопрос есть, и до ответа называть нечего', () => {
  const choice = branchChoice([hall('b1', 'На Рудаки'), hall('b2', 'В Худжанде')], null, null);

  expect(choice.asks).toBe(true);
  expect(choice.unanswered).toBe(true);
  expect(choice.branchId).toBeNull();
});

it('ответ игрока и есть зал запроса', () => {
  const choice = branchChoice([hall('b1', 'На Рудаки'), hall('b2', 'В Худжанде')], 'b2', null);

  expect(choice.unanswered).toBe(false);
  expect(choice.branchId).toBe('b2');
});

// Клуб мог не завести ни одного зала: тогда всё работает как раньше — зал в запросе не
// упоминается, а сервер отвечает как отвечал.
it('клуб без залов вопроса не задаёт и запрос не держит', () => {
  const choice = branchChoice([], null, null);

  expect(choice.asks).toBe(false);
  expect(choice.unanswered).toBe(false);
  expect(choice.branchId).toBeNull();
});
