import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { UPDATE_CHANNELS, UPDATE_COMPONENTS, channelLabelKey, componentLabelKey } from './updatesModel';

function namesFrom(fileName: string): string[] {
  const source = readFileSync(
    join(import.meta.dir, '..', '..', '..', '..', '..', 'src', 'AFK4.Shared.Contracts', 'Updates', fileName),
    'utf8'
  );
  return [...source.matchAll(/=\s*"(?<name>[a-z-]+)"/g)].map(match => match.groups!.name).sort();
}

// Список в панели и список на сервере расходятся молча: форма отправляет своё имя, сервер
// отвечает «Unsupported update component», и в интерфейсе это выглядит как «не сохранилось».
// Ровно так регистрация пакета была сломана целиком: форма предлагала organization_admin,
// operator_app, agent_service и player_shell — сервер не принимал ни одного из четырёх.
describe('UPDATE_COMPONENTS', () => {
  it('совпадает с тем, что принимает сервер', () => {
    expect(UPDATE_COMPONENTS.map(component => component.name).sort()).toEqual(namesFrom('UpdateComponentNames.cs'));
  });

  it('незнакомое имя не печатается сырым', () => {
    expect(componentLabelKey('something-new')).toBe('op.helper.update.component.fallback');
  });
});

// Карточка клиента предлагала канал «canary», которого сервер не знает: закрепить его за клубом
// было нельзя, отказ приходил каждый раз. А «internal», который сервер принимает, не предлагался.
describe('UPDATE_CHANNELS', () => {
  it('совпадает с тем, что принимает сервер', () => {
    expect(UPDATE_CHANNELS.map(channel => channel.name).sort()).toEqual(namesFrom('UpdateChannelNames.cs'));
  });

  it('незнакомый канал не печатается сырым', () => {
    expect(channelLabelKey('canary')).toBe('op.helper.update.channel.fallback');
  });
});
