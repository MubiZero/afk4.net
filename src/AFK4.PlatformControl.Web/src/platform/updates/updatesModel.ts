import type { MessageKey } from '@/i18n/messages';

/**
 * Из чего состоит парк, который платформа обновляет.
 *
 * Имена приходят с сервера и уходят на сервер как есть: он принимает ровно эти три и отвечает
 * отказом на любое другое. Форма регистрации пакета когда-то предлагала свои собственные
 * («organization_admin», «operator_app») — сервер отвергал каждую попытку, а список пакетов
 * печатал сырое «organization-admin» вместо названия.
 */
export const UPDATE_COMPONENTS: readonly { readonly name: string; readonly labelKey: MessageKey }[] = [
  { name: 'organization-admin', labelKey: 'op.helper.update.component.organizationAdmin' },
  { name: 'agent-service', labelKey: 'op.helper.update.component.agentService' },
  { name: 'player-shell', labelKey: 'op.helper.update.component.playerShell' }
];

export function componentLabelKey(name: string): MessageKey {
  return UPDATE_COMPONENTS.find(component => component.name === name)?.labelKey
    ?? 'op.helper.update.component.fallback';
}

/**
 * Каналы, по которым сборка доходит до клубов.
 *
 * Тот же перечень сервер проверяет и при регистрации пакета, и при закреплении канала за
 * клиентом. Карточка клиента предлагала «canary» — сервер такого канала не знает и отвечал
 * отказом; «internal», который он знает, не предлагалась нигде.
 */
export const UPDATE_CHANNELS: readonly { readonly name: string; readonly labelKey: MessageKey }[] = [
  { name: 'stable', labelKey: 'op.helper.update.channel.stable' },
  { name: 'beta', labelKey: 'op.helper.update.channel.beta' },
  { name: 'internal', labelKey: 'op.helper.update.channel.internal' }
];

export function channelLabelKey(name: string): MessageKey {
  return UPDATE_CHANNELS.find(channel => channel.name === name)?.labelKey
    ?? 'op.helper.update.channel.fallback';
}
