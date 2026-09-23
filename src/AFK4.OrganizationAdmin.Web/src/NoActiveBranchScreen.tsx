import { useState } from 'react';
import { AlertTriangle, Building2, LoaderCircle, LogOut, RefreshCw } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { AuthFrame } from './AuthFrame';

type RecheckState =
  | { status: 'idle' }
  | { status: 'checking' }
  | { status: 'still-none' }
  | { status: 'failed'; detail: string };

// Вход состоялся, а филиала нет. Зал, касса, брони и настройки клуба живут внутри филиала,
// поэтому оболочка называет причину здесь один раз — вместо того чтобы показывать формы, которые
// гаснут без объяснения, и «Платежи», которые списывали всё на подключение к серверу.
//
// Выбирать филиал тут не из чего: будь у сессии хоть один, он и стал бы активным
// (resolveActiveBranchId). Поэтому сотруднику — к кому идти и «Проверить снова» после
// назначения, без повторного входа; поддержке платформы — что у организации нет филиалов.
export function NoActiveBranchScreen(props:
  | { mode: 'staff'; onRecheck: () => Promise<boolean>; onLeave: () => void }
  | { mode: 'support'; onLeave: () => void }
) {
  const { t } = useI18n();
  const [recheck, setRecheck] = useState<RecheckState>({ status: 'idle' });
  const checking = recheck.status === 'checking';
  const isStaff = props.mode === 'staff';

  const runRecheck = async () => {
    if (props.mode !== 'staff' || checking) return;
    setRecheck({ status: 'checking' });
    try {
      // true — филиал нашёлся: оболочка сама уйдёт с этого экрана, и трогать состояние
      // размонтированного компонента незачем.
      if (!(await props.onRecheck())) {
        setRecheck({ status: 'still-none' });
      }
    } catch (error) {
      setRecheck({ status: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  return (
    <AuthFrame>
      <section className="auth-panel shift-gate-panel" aria-busy={checking}>
        <header className="auth-panel-head">
          <Building2 className="shift-gate-mark" size={36} aria-hidden="true" />
          <h1>{t('op.noBranch.title')}</h1>
          <p>{t(isStaff ? 'op.noBranch.body' : 'op.noBranch.support.body')}</p>
          <p>{t(isStaff ? 'op.noBranch.hint' : 'op.noBranch.support.hint')}</p>
        </header>

        {recheck.status === 'still-none' && (
          <div className="shift-gate-loading" role="status">
            <span>{t('op.noBranch.stillNone')}</span>
          </div>
        )}

        {recheck.status === 'failed' && (
          <div className="ui-alert" role="alert">
            <AlertTriangle size={17} aria-hidden="true" />
            <span>{recheck.detail}</span>
          </div>
        )}

        {isStaff && (
          <button
            type="button"
            className="ui-btn ui-btn--primary ui-btn--block"
            disabled={checking}
            onClick={() => void runRecheck()}
          >
            {checking
              ? <LoaderCircle className="ui-spinner" size={16} aria-hidden="true" />
              : <RefreshCw size={16} aria-hidden="true" />}
            <span>{t(checking ? 'op.noBranch.checking' : 'op.noBranch.recheck')}</span>
          </button>
        )}

        <button type="button" className="shift-gate-signout" onClick={props.onLeave}>
          <LogOut size={15} aria-hidden="true" />
          <span>{t(isStaff ? 'op.noBranch.signOut' : 'op.noBranch.support.exit')}</span>
        </button>
      </section>
    </AuthFrame>
  );
}
