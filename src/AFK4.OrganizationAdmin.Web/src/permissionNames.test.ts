import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { permissionNames } from './permissionNames';

describe('organization permission names', () => {
  it('uses the organization namespace exactly once for every permission', () => {
    const values = Object.values(permissionNames);

    expect(values.length).toBeGreaterThan(0);
    expect(values.every((value) => value.startsWith('organization.'))).toBe(true);
    expect(values.some((value) => value.startsWith('organization.organization.'))).toBe(false);
    expect(new Set(values).size).toBe(values.length);
  });

  // Сверка с сервером, а не с собой. Прошлая версия этого файла проверяла только форму строк и
  // была зелёной, пока в каталоге не хватало organization.shop.orders.manage: очередь заказов
  // бара гейтилась правом на создание продажи, кассир видел ленту и получал 403 на каждое
  // «Принять/Выдать/Отменить». Право, которого нет в каталоге, невозможно спросить — значит
  // раздел либо открыт всем, либо закрыт всем, и оба исхода неверны.
  //
  // Читаем исходник контрактов напрямую: генератора TS из C# в репозитории нет, а сверять две
  // рукописные копии друг с другом бессмысленно.
  it('covers every permission the server declares', () => {
    const source = readFileSync(
      join(import.meta.dir, '..', '..', 'AFK4.Shared.Contracts', 'Identity', 'OrganizationPermissionNames.cs'),
      'utf8'
    );
    const serverPermissions = [...source.matchAll(/public const string \w+ = "([^"]+)";/g)].map((m) => m[1]);

    expect(serverPermissions.length).toBeGreaterThan(0);

    const clientPermissions = new Set(Object.values(permissionNames));
    const missing = serverPermissions.filter((permission) => !clientPermissions.has(permission));

    expect(missing).toEqual([]);
  });

  // И наоборот: право, которое клиент спрашивает, а сервер не выдаёт никому, — это раздел,
  // невидимый навсегда. Такой гейт выглядит рабочим и молча прячет функциональность.
  it('asks for no permission the server does not declare', () => {
    const source = readFileSync(
      join(import.meta.dir, '..', '..', 'AFK4.Shared.Contracts', 'Identity', 'OrganizationPermissionNames.cs'),
      'utf8'
    );
    const serverPermissions = new Set(
      [...source.matchAll(/public const string \w+ = "([^"]+)";/g)].map((m) => m[1])
    );

    const unknown = Object.values(permissionNames).filter((permission) => !serverPermissions.has(permission));

    expect(unknown).toEqual([]);
  });
});
