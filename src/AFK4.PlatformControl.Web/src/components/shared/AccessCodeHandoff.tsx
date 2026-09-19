import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { useI18n } from '@/i18n/I18nProvider';

/**
 * Выданный код доступа — и то, как его передать человеку.
 *
 * Код показывается ровно один раз, а дальше его надо донести до владельца клуба или нового
 * сотрудника платформы. Ссылка идёт первой и копируется одной кнопкой: приглашённому нужно
 * попасть на экран активации, а не запомнить строку. Код оставлен ниже для случая, когда ссылку
 * в переписке ломает или человек вводит код руками.
 */
export function AccessCodeHandoff({ code, activationUrl, idPrefix }: {
  code: string;
  activationUrl: string;
  /// Разные идентификаторы полей: на одном экране таких блоков может оказаться два.
  idPrefix: string;
}) {
  const { t } = useI18n();
  const [copied, setCopied] = useState<'link' | 'code' | null>(null);
  const [failed, setFailed] = useState(false);

  async function copy(what: 'link' | 'code', value: string) {
    try {
      await navigator.clipboard.writeText(value);
      setFailed(false);
      setCopied(what);
      setTimeout(() => setCopied(null), 1500);
    } catch {
      // Буфер обмена может быть закрыт настройками браузера или отсутствовать вовсе. Молчать
      // тут нельзя: человек уверен, что код у него в буфере, закрывает окно — и кода больше нет.
      setFailed(true);
    }
  }

  return (
    <>
      <p role="alert">{t('platform.inviteCode.warning')}</p>
      <Field label={t('platform.inviteCode.link')} htmlFor={`${idPrefix}-link`}>
        <Input id={`${idPrefix}-link`} readOnly value={activationUrl} onFocus={event => event.currentTarget.select()} />
      </Field>
      <Button onClick={() => void copy('link', activationUrl)}>
        {copied === 'link' ? t('platform.inviteCode.copied') : t('platform.inviteCode.copyLink')}
      </Button>
      <Field label={t('platform.inviteCode.code')} htmlFor={`${idPrefix}-code`}>
        <Input id={`${idPrefix}-code`} readOnly value={code} onFocus={event => event.currentTarget.select()} />
      </Field>
      <Button variant="outline" onClick={() => void copy('code', code)}>
        {copied === 'code' ? t('platform.inviteCode.copied') : t('platform.inviteCode.copyCode')}
      </Button>
      {failed ? <p role="alert" className="mgmt-drawer-hint">{t('platform.inviteCode.copyFailed')}</p> : null}
    </>
  );
}
