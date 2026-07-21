import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { CreditCard } from 'lucide-react';
import { projectOperatorError } from '../../../apiErrors';
import { createAuthenticatedOperatorClients, emptyFeedback } from '../../../operatorHelpers';
import { useFeedbackToasts } from '../../../useFeedbackToasts';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

interface Props {
  backend: OperatorBackendContext;
}

// Eskhata Merchant — тихий резервный способ приёма: свёрнутая строка со статусом, «Настроить»
// разворачивает форму реквизитов. Своя кнопка «Сохранить» (модель мгновенных действий, общего
// save-бара нет). Hash key наружу не приходит: сервер отдаёт только hashKeySet; пустое поле при
// сохранении оставляет прежний секрет. Приём платежей (Merchant API) — отложенный эпик, поэтому
// статус максимум «настроен · не активен».
export function EskhataGatewayForm({ backend }: Props) {
  const { t } = useI18n();
  const [baseUrl, setBaseUrl] = useState('');
  const [companyId, setCompanyId] = useState('');
  const [merchantId, setMerchantId] = useState('');
  const [hashKey, setHashKey] = useState('');
  const [hashKeySet, setHashKeySet] = useState(false);
  const [status, setStatus] = useState('inactive');
  const [saving, setSaving] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  useEffect(() => {
    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.eskhataConfig.get()
      .then((config) => {
        if (!active) return;
        setBaseUrl(config.baseUrl);
        setCompanyId(config.companyId);
        setMerchantId(config.merchantId > 0 ? String(config.merchantId) : '');
        setHashKeySet(config.hashKeySet);
        setStatus(config.status);
      })
      .catch((error) => {
        if (active) setFeedback({ label: t('op.eskhata.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
      });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend.config, backend.session]);

  const merchNumber = Number(merchantId);
  const valid = baseUrl.trim().length > 0
    && companyId.trim().length > 0
    && Number.isInteger(merchNumber) && merchNumber > 0
    && (hashKeySet || hashKey.trim().length > 0);

  const save = async () => {
    if (!valid) {
      setFeedback({ label: t('op.eskhata.feedbackLabel'), state: 'failed', detail: t('op.eskhata.invalid') });
      return;
    }
    setSaving(true);
    setFeedback({ label: t('op.eskhata.feedbackLabel'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const config = await clients.eskhataConfig.update({
        baseUrl: baseUrl.trim(),
        companyId: companyId.trim(),
        merchantId: merchNumber,
        hashKey: hashKey.trim() || null
      });
      setBaseUrl(config.baseUrl);
      setCompanyId(config.companyId);
      setMerchantId(config.merchantId > 0 ? String(config.merchantId) : '');
      setHashKeySet(config.hashKeySet);
      setStatus(config.status);
      setHashKey('');
      setFeedback({ label: t('op.eskhata.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.eskhata.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const summary = status === 'configured'
    ? `${t('op.eskhata.statusConfigured')}. ${t('op.eskhata.note')}`
    : t('op.eskhata.note');

  return (
    <div className="eskhata-gateway">
      <div className="payset-method">
        <span className="payset-method-icon" aria-hidden="true">
          <CreditCard size={18} strokeWidth={2} />
        </span>
        <div className="payset-method-body">
          <div className="payset-method-title">{t('op.eskhata.title')}</div>
          <div className="payset-method-sub">{summary}</div>
        </div>
        <button type="button" className="ui-btn" onClick={() => setExpanded((v) => !v)}>
          {expanded ? t('common.cancel') : t('op.eskhata.configure')}
        </button>
      </div>

      {expanded && (
        <div className="payset-reveal">
          <div className="mgmt-form">
            <div className="mgmt-form-grid">
              <label className="mgmt-form-wide">{t('op.eskhata.baseUrl')}
                <input
                  inputMode="url"
                  value={baseUrl}
                  disabled={saving}
                  onChange={(event) => setBaseUrl(event.currentTarget.value)}
                />
              </label>
              <label>{t('op.eskhata.companyId')}
                <input
                  value={companyId}
                  disabled={saving}
                  onChange={(event) => setCompanyId(event.currentTarget.value)}
                />
              </label>
              <label>{t('op.eskhata.merchantId')}
                <input
                  inputMode="numeric"
                  value={merchantId}
                  disabled={saving}
                  onChange={(event) => setMerchantId(event.currentTarget.value)}
                />
              </label>
              <label className="mgmt-form-wide">{t('op.eskhata.hashKey')}
                <input
                  type="password"
                  value={hashKey}
                  disabled={saving}
                  placeholder={hashKeySet ? `${t('op.eskhata.hashKeySet')} · ${t('op.eskhata.hashKeyPlaceholder')}` : ''}
                  onChange={(event) => setHashKey(event.currentTarget.value)}
                />
              </label>
            </div>

            <div className="mgmt-form-actions">
              <button
                type="button"
                className="ui-btn ui-btn--primary"
                disabled={saving || !valid}
                onClick={() => void save()}
              >
                {t('common.save')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
