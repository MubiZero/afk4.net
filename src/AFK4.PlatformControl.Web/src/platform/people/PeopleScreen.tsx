import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@afk4/i18n';
import { PlatformApiError } from '@/api/platformTransport';
import type { NetworkPeopleApi } from '@/api/platformClients/people';
import type { NetworkPerson } from '@/api/types';

type Client = Pick<NetworkPeopleApi, 'lookupPerson' | 'banPerson' | 'liftBan'>;

/**
 * Сетевой запрет — единственное решение платформы о живом игроке, а не о клубе.
 *
 * Человека здесь находят по точному номеру и никак иначе: список людей сети — ровно то, чего в
 * панели платформы быть не должно. Клубное решение остаётся клубным: клуб закрывает у себя
 * карточку, и дальше его слово не идёт.
 */
export function PeopleScreen({ client }: { client: Client }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [phone, setPhone] = useState('');
  const [person, setPerson] = useState<NetworkPerson | null>(null);
  const [searched, setSearched] = useState(false);
  const [pending, setPending] = useState(false);
  const [banning, setBanning] = useState(false);
  const [lifting, setLifting] = useState(false);

  async function find() {
    if (pending || phone.trim().length === 0) return;
    setPending(true);
    try {
      setPerson(await client.lookupPerson(phone.trim()));
    } catch (cause) {
      setPerson(null);
      // «Не нашли» — это ответ, а не сбой: экран говорит его сам, без красного тоста.
      if (!(cause instanceof PlatformApiError) || cause.status !== 404) {
        toast({ title: describeError(cause, t), variant: 'error' });
      }
    } finally {
      setSearched(true);
      setPending(false);
    }
  }

  async function run(action: () => Promise<NetworkPerson>, successKey: MessageKey) {
    if (pending) return;
    setPending(true);
    try {
      setPerson(await action());
      toast({ title: t(successKey), variant: 'success' });
      setBanning(false);
      setLifting(false);
    } catch (cause) {
      toast({ title: describeError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.people.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.people.description')}</p>

        <div className="mgmt-form">
          <label>
            {t('platform.people.field.phone')}
            <Input
              value={phone}
              inputMode="tel"
              placeholder="+992 90 000-00-00"
              onChange={event => setPhone(event.target.value)}
              onKeyDown={event => { if (event.key === 'Enter') void find(); }}
            />
          </label>
          <Button disabled={pending || phone.trim().length === 0} onClick={() => void find()}>
            {t('platform.people.find')}
          </Button>
        </div>

        {person === null
          ? (searched ? <EmptyState message={t('platform.people.notFound')} /> : null)
          : (
            <div className="mgmt-form">
              <p className="pc-num">{person.phoneNumber}</p>
              <p>{person.displayName}</p>
              <p className="mgmt-drawer-hint">
                {t('platform.people.registeredAt', { date: formatDate(person.registeredAtUtc) })}
              </p>

              {person.networkBanAtUtc === null ? (
                <>
                  <Badge variant="success">{t('platform.people.allowed')}</Badge>
                  <Button variant="destructive" disabled={pending} onClick={() => setBanning(true)}>
                    {t('platform.people.ban')}
                  </Button>
                </>
              ) : (
                <>
                  <Badge variant="destructive">
                    {t('platform.people.banned', { date: formatDate(person.networkBanAtUtc) })}
                  </Badge>
                  {person.networkBanReason === null ? null : (
                    <p>{t('platform.people.banReason')}: {person.networkBanReason}</p>
                  )}
                  <Button variant="outline" disabled={pending} onClick={() => setLifting(true)}>
                    {t('platform.people.lift')}
                  </Button>
                </>
              )}
            </div>
          )}

        <ConfirmDialog
          open={banning && person !== null}
          title={t('platform.people.ban.title')}
          description={t('platform.people.ban.description')}
          reasonLabel={t('platform.people.ban.reason')}
          confirmLabel={t('platform.people.ban.confirm')}
          cancelLabel={t('common.cancel')}
          destructive
          pending={pending}
          onConfirm={reason => void run(
            () => client.banPerson(person!.platformPersonId, reason), 'platform.people.banned.toast')}
          onOpenChange={open => setBanning(open)}
        />

        <ConfirmDialog
          open={lifting && person !== null}
          title={t('platform.people.lift.title')}
          description={t('platform.people.lift.description')}
          confirmLabel={t('platform.people.lift.confirm')}
          cancelLabel={t('common.cancel')}
          pending={pending}
          onConfirm={() => void run(
            () => client.liftBan(person!.platformPersonId), 'platform.people.lifted.toast')}
          onOpenChange={open => setLifting(open)}
        />
      </CardContent>
    </Card>
  );
}

function describeError(cause: unknown, t: (key: MessageKey) => string): string {
  if (cause instanceof PlatformApiError && cause.status === 400) {
    return t('platform.people.error.invalid');
  }

  return t('platform.people.error.failed');
}
