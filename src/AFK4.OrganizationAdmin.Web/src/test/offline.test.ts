import { expect, it } from 'bun:test';

// Запрос, который тест не подменил (чаще всего — начатый экраном к концу теста), не уходит в
// сеть: раньше он стучался на localhost, получал ECONNREFUSED и тратил время прогона на сокет.
it('запрос мимо подмены падает сразу и называет адрес', async () => {
  await expect(window.fetch('http://localhost:5074/api/organizations/x/players')).rejects.toThrow(
    'Тест не ходит в сеть: http://localhost:5074/api/organizations/x/players'
  );
});
