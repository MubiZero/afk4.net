import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { RolesApi } from '@/api/platformClients/roles';
import type { PlatformRole } from '@/api/types';
import { useLoadable } from '../useLoadable';
import {
  describePermission,
  describePermissionGroup,
  describeRoleActionError,
  groupPermissions
} from './rolesModel';

type Client = Pick<RolesApi, 'listRoles' | 'listPermissions' | 'createRole' | 'updateRole' | 'deleteRole'>;

interface Draft {
  roleName: string;
  displayName: string;
  description: string;
  permissions: Set<string>;
}

function draftFrom(role: PlatformRole): Draft {
  return {
    roleName: role.roleName,
    displayName: role.displayName,
    description: role.description,
    permissions: new Set(role.permissions)
  };
}

export function RolesSection({ client }: { client: Client }) {
  const { t } = useI18n();
  const { toast } = useToast();
  // Роли и перечень прав грузятся порознь. Перечень нужен только редактору состава, а сами
  // роли — кто какую носит и сколько человек — читаются и без него: его отказ не должен
  // прятать раздел, и повтор перезапрашивает только его.
  const state = useLoadable(() => client.listRoles());
  const permissionsState = useLoadable(() => client.listPermissions());
  const [draft, setDraft] = useState<Draft | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<PlatformRole | null>(null);
  const [pending, setPending] = useState(false);

  function startNewRole() {
    setDraft({ roleName: '', displayName: '', description: '', permissions: new Set() });
  }

  function reload() {
    setDraft(null);
    setDeleteTarget(null);
    state.retry();
  }

  async function save() {
    if (draft === null || pending) return;
    const existing = state.status === 'ready' && state.data.some(role => role.roleName === draft.roleName);
    setPending(true);
    try {
      const payload = {
        displayName: draft.displayName.trim(),
        description: draft.description.trim(),
        permissions: [...draft.permissions]
      };
      if (existing) {
        await client.updateRole(draft.roleName, payload);
      } else {
        await client.createRole(draft.roleName.trim().toLowerCase(), payload);
      }
      toast({ title: t('platform.settings.roles.saved'), variant: 'success' });
      reload();
    } catch (cause) {
      toast({ title: describeRoleActionError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function remove() {
    if (deleteTarget === null || pending) return;
    setPending(true);
    try {
      await client.deleteRole(deleteTarget.roleName);
      toast({ title: t('platform.settings.roles.deleted'), variant: 'success' });
      reload();
    } catch (cause) {
      toast({ title: describeRoleActionError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'error') return <ErrorState title={t('platform.settings.roles.error.load')} message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? reload : undefined} />;
  if (state.status === 'loading') return <LoadingCards count={1} />;

  const roles = state.data;
  const nameIsValid = draft !== null
    && draft.roleName.trim().length > 0
    && draft.displayName.trim().length > 0;
  const hasPermissions = draft !== null && draft.permissions.size > 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.settings.roles.title')}</CardTitle>
        <Button
          variant="outline"
          onClick={startNewRole}
        >
          {t('platform.settings.roles.create')}
        </Button>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.settings.roles.description')}</p>

        {roles.length === 0 ? <EmptyState message={t('platform.settings.roles.empty')} next={{ label: t('platform.settings.roles.createFirst'), onClick: startNewRole }} /> : null}

        {roles.map(role => (
          <div key={role.roleName} className="pc-kv">
            <span>
              {role.displayName} <code>{role.roleName}</code>
              {role.isBuiltIn ? <Badge variant="outline">{t('platform.settings.roles.builtIn')}</Badge> : null}
              {role.grantsAllPermissions ? <Badge variant="success">{t('platform.settings.roles.fullAccess')}</Badge> : null}
            </span>
            <span>
              <span className="pc-num">{t('platform.settings.roles.holders', { count: role.adminCount })}</span>
              <Button variant="outline" disabled={pending} onClick={() => setDraft(draftFrom(role))}>
                {t('platform.settings.roles.edit')}
              </Button>
              {/* Удаление показывается только у не встроенной роли: рычаг, который заведомо
                  ответит отказом, хуже отсутствующего. */}
              {role.isBuiltIn ? null : (
                <Button variant="outline" disabled={pending} onClick={() => setDeleteTarget(role)}>
                  {t('platform.settings.roles.delete')}
                </Button>
              )}
            </span>
          </div>
        ))}

        {draft === null ? null : (
          <div className="mgmt-form">
            {roles.some(role => role.roleName === draft.roleName) ? null : (
              <label>
                {t('platform.settings.roles.name')}
                <Input
                  value={draft.roleName}
                  onChange={event => setDraft({ ...draft, roleName: event.target.value })}
                />
              </label>
            )}
            <label>
              {t('platform.settings.roles.displayName')}
              <Input
                value={draft.displayName}
                onChange={event => setDraft({ ...draft, displayName: event.target.value })}
              />
            </label>
            <label>
              {t('platform.settings.roles.descriptionField')}
              <Input
                value={draft.description}
                onChange={event => setDraft({ ...draft, description: event.target.value })}
              />
            </label>

            {/* У роли с полным доступом состав не редактируется: она описывается флагом, а не
                списком, чтобы получать и права, которых ещё не существует. */}
            {draft.permissions.size > 0 && roles.find(role => role.roleName === draft.roleName)?.grantsAllPermissions
              ? <p className="mgmt-drawer-hint">{t('platform.settings.roles.fullAccessNotEditable')}</p>
              : permissionsState.status === 'error' ? (
                <ErrorState
                  title={t('platform.settings.roles.error.permissions')}
                  message={permissionsState.message}
                  retryLabel={permissionsState.canRetry ? t('state.retry') : undefined}
                  onRetry={permissionsState.canRetry ? permissionsState.retry : undefined}
                />
              ) : permissionsState.status === 'loading' ? <LoadingCards count={1} /> : (
                <fieldset>
                  <legend>{t('platform.settings.roles.permissions')}</legend>
                  <p className="mgmt-drawer-hint">{t('platform.settings.roles.permissionsHint')}</p>
                  {groupPermissions(permissionsState.data).map(([group, groupPermissionNames]) => (
                    <div key={group}>
                      <strong>{describePermissionGroup(group, t)}</strong>
                      {groupPermissionNames.map(permission => (
                        <label key={permission}>
                          <input
                            type="checkbox"
                            checked={draft.permissions.has(permission)}
                            onChange={event => {
                              const next = new Set(draft.permissions);
                              if (event.target.checked) next.add(permission); else next.delete(permission);
                              setDraft({ ...draft, permissions: next });
                            }}
                          />
                          {describePermission(permission, t)}
                        </label>
                      ))}
                    </div>
                  ))}
                </fieldset>
              )}

            {nameIsValid ? null : <p className="mgmt-drawer-hint">{t('platform.settings.roles.nameRequired')}</p>}
            {hasPermissions ? null : <p className="mgmt-drawer-hint">{t('platform.settings.roles.permissionsRequired')}</p>}

            <Button disabled={pending || !nameIsValid || !hasPermissions} onClick={save}>
              {t('platform.settings.roles.save')}
            </Button>
            <Button variant="outline" disabled={pending} onClick={() => setDraft(null)}>
              {t('platform.settings.roles.cancel')}
            </Button>
          </div>
        )}

        <ConfirmDialog
          open={deleteTarget !== null}
          title={t('platform.settings.roles.deleteConfirm.title')}
          description={deleteTarget === null
            ? undefined
            : t('platform.settings.roles.deleteConfirm.body', { name: deleteTarget.displayName })}
          confirmLabel={t('platform.settings.roles.deleteConfirm.confirm')}
          cancelLabel={t('common.cancel')}
          destructive
          pending={pending}
          onConfirm={() => void remove()}
          onOpenChange={open => { if (!open) setDeleteTarget(null); }}
        />
      </CardContent>
    </Card>
  );
}
