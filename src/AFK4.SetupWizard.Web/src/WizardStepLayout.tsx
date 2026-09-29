import type { FormEvent, ReactNode } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Button } from '@afk4/ui/react';

interface WizardStepLayoutProps {
  /// Номер шага в этом прогоне; у ответвлений (сброс ПИН-кода) номера нет.
  stepNumber?: number;
  /// Строка над заголовком: чей клуб и какой филиал настраиваем.
  context?: string;
  title: string;
  subtitle?: ReactNode;
  children?: ReactNode;
  /// Экран — форма: Enter в поле жмёт главную кнопку внизу, а не проглатывается.
  onSubmit?: (event: FormEvent<HTMLFormElement>) => void;
  onBack?: () => void;
  /// По умолчанию «Назад»; со сброса ПИН-кода — «Вернуться ко входу», это точнее.
  backLabel?: string;
  backDisabled?: boolean;
  /// «Пропустить» — тихой ссылкой справа: шаг законно пройти мимо, но это не следующий шаг.
  skip?: { label: string; onClick: () => void; disabled?: boolean } | null;
  /// Одна главная кнопка шага — справа внизу. Её рисует экран: у кого-то это submit формы.
  primary?: ReactNode;
}

/**
 * Каркас шага мастера. Раньше каждый экран собирал шапку и кнопки сам: у одного «Назад» был
 * ссылкой, у другого кнопкой, «Пропустить» стоял то обводкой, то главной, ширина карточки была
 * трёх видов, а на «Готово» заголовок уезжал в центр. Здесь одна раскладка на все шаги: шапка
 * слева (контекст → номер и заголовок → подзаголовок), середина прокручивается, внизу слева
 * «Назад», справа — «Пропустить» тихой ссылкой и одна главная.
 */
export function WizardStepLayout({
  stepNumber,
  context,
  title,
  subtitle,
  children,
  onSubmit,
  onBack,
  backLabel,
  backDisabled = false,
  skip = null,
  primary,
}: WizardStepLayoutProps) {
  const { t } = useI18n();
  const hasFooter = onBack !== undefined || skip !== null || primary !== undefined;

  const content = (
    <>
      <header className="wizard-screen-head">
        {context ? <span className="wizard-screen-context">{context}</span> : null}
        <div className="wizard-screen-title-row">
          {stepNumber === undefined ? null : <span className="wizard-screen-step" aria-hidden>{stepNumber}</span>}
          <h1>{title}</h1>
        </div>
        {subtitle ? <p>{subtitle}</p> : null}
      </header>

      <div className="wizard-scroll">{children}</div>

      {hasFooter ? (
        <footer className="wizard-actions">
          {onBack ? (
            <Button onClick={onBack} disabled={backDisabled}>
              <ArrowLeft size={16} aria-hidden />
              {backLabel ?? t('setup.wizard.common.back')}
            </Button>
          ) : <span />}
          <div className="wizard-actions-next">
            {skip ? (
              <Button variant="ghost" onClick={skip.onClick} disabled={skip.disabled}>
                {skip.label}
              </Button>
            ) : null}
            {primary}
          </div>
        </footer>
      ) : null}
    </>
  );

  return onSubmit ? (
    <form className="wizard-screen" onSubmit={onSubmit} noValidate>{content}</form>
  ) : (
    <section className="wizard-screen">{content}</section>
  );
}
