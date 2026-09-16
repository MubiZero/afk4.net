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
