import { describe, expect, it } from 'bun:test';
import { topUpQrPayload } from './topUpQrPayload';

/**
 * Экран рисовал код из `payUrl`, хотя банк присылает собственный `qr`, а сервер его отдаёт.
 * Разница видна не в вёрстке, а у человека с телефоном: банковское приложение узнаёт код банка,
 * а ссылку на веб-страницу — нет.
 */
describe('topUpQrPayload', () => {
  it('берёт код банка, когда он есть', () => {
    expect(topUpQrPayload({
      qr: '00020101021226...',
      deepLink: 'eskhata://pay?order=1',
      payUrl: 'https://pay.example/invoice/1'
    })).toBe('00020101021226...');
  });

  it('без кода банка открывает его приложение ссылкой', () => {
    expect(topUpQrPayload({
      qr: null,
      deepLink: 'eskhata://pay?order=1',
      payUrl: 'https://pay.example/invoice/1'
    })).toBe('eskhata://pay?order=1');
  });

  it('последним остаётся веб-счёт: лишний шаг, но работает всегда', () => {
    expect(topUpQrPayload({ qr: null, deepLink: null, payUrl: 'https://pay.example/invoice/1' }))
      .toBe('https://pay.example/invoice/1');
  });

  // Оплата на стойке: банка в этой истории нет вовсе, и показывать нечего.
  it('без единой ссылки кода нет', () => {
    expect(topUpQrPayload({ qr: null, deepLink: null, payUrl: null })).toBeNull();
  });

  // Пустая строка от шлюза — это отсутствие значения, а не значение.
  it('пустую строку за код не принимает', () => {
    expect(topUpQrPayload({ qr: '', deepLink: '', payUrl: 'https://pay.example/invoice/1' }))
      .toBe('https://pay.example/invoice/1');
  });
});
