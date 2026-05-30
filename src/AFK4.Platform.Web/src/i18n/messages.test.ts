import { it, expect } from 'vitest';
import { messages } from './messages';

it('ru and en have identical key sets', () => {
  expect(Object.keys(messages.en).sort()).toEqual(Object.keys(messages.ru).sort());
});

it('includes the new venue/devices keys', () => {
  for (const key of ['venue.title', 'venue.tab.devices', 'venue.tab.pending',
    'devices.col.name', 'devices.action.rename', 'devices.action.remove',
    'common.save', 'common.cancel', 'toast.saved'] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
