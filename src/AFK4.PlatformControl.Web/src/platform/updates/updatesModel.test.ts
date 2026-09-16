import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { UPDATE_COMPONENTS, componentLabelKey } from './updatesModel';

const componentNamesSource = readFileSync(
  join(import.meta.dir, '..', '..', '..', '..', '..', 'src', 'AFK4.Shared.Contracts', 'Updates', 'UpdateComponentNames.cs'),
  'utf8'
);

// Список в панели и список на сервере расходятся молча: форма отправляет своё имя, сервер
// отвечает «Unsupported update component», и в интерфейсе это выглядит как «не сохранилось».
// Ровно так регистрация пакета была сломана целиком: форма предлагала organization_admin,
// operator_app, agent_service и player_shell — сервер не принимал ни одного из четырёх.
describe('UPDATE_COMPONENTS', () => {
  it('совпадает с тем, что принимает сервер', () => {
    const serverNames = [...componentNamesSource.matchAll(/=\s*"(?<name>[a-z-]+)"/g)]
      .map(match => match.groups!.name)
      .sort();

    expect(UPDATE_COMPONENTS.map(component => component.name).sort()).toEqual(serverNames);
  });

  it('незнакомое имя не печатается сырым', () => {
    expect(componentLabelKey('something-new')).toBe('op.helper.update.component.fallback');
  });
});
