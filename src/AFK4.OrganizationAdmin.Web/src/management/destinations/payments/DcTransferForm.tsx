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

// DushanbeCity — второй способ приёма, ручной перевод по карте (нет Merchant API, поэтому нет
// статуса «активен» от банка — только собственный тумблер isActive). Как и Eskhata: свёрнутая
// строка со статусом, «Настроить» разворачивает форму, своя кнопка «Сохранить». Полный номер
// карты наружу не приходит: сервер отдаёт только cardSet/cardLast4; пустое поле при сохранении
// оставляет прежнюю карту. Шаблон комментария обязан содержать {ref} — это плейсхолдер, который
// подставляет intent id при создании pay-link (см. dcTopUps), иначе перевод нельзя сопоставить с игроком.
export function DcTransferForm({ backend }: Props) {
  const { t } = useI18n();
  const [cardNumber, setCardNumber] = useState('');
  const [cardSet, setCardSet] = useState(false);
  const [cardLast4, setCardLast4] = useState('');
  const [commentTemplate, setCommentTemplate] = useState('');
  const [isActive, setIsActive] = useState(false);
  const [saving, setSaving] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  useEffect(() => {
    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.dcConfig.get()
      .then((config) => {
        if (!active) return;
        setCardSet(config.cardSet);
        setCardLast4(config.cardLast4);
        setCommentTemplate(config.commentTemplate);
        setIsActive(config.isActive);
      })
      .catch((error) => {
        if (active) setFeedback({ label: t('op.dc.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
      });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend.config, backend.session]);

  const valid = (cardSet || cardNumber.trim().length >= 12)
    && commentTemplate.includes('{ref}');

  const save = async () => {
    if (!valid) {
      setFeedback({ label: t('op.dc.feedbackLabel'), state: 'failed', detail: t('op.dc.invalid') });
      return;
    }
    setSaving(true);
    setFeedback({ label: t('op.dc.feedbackLabel'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const config = await clients.dcConfig.update({
        cardNumber: cardNumber.trim() || null,
        commentTemplate,
        isActive
      });
      setCardSet(config.cardSet);
      setCardLast4(config.cardLast4);
      setCommentTemplate(config.commentTemplate);
      setIsActive(config.isActive);
      setCardNumber('');
      setFeedback({ label: t('op.dc.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.dc.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const summary = cardSet
    ? `${t('op.dc.statusConfigured')}. ${t('op.dc.note')}`
    : t('op.dc.note');

  return (
    <div className="dc-transfer-form">
      <div className="payset-method">
        <span className="payset-method-icon" aria-hidden="true">
          <CreditCard size={18} strokeWidth={2} />
        </span>
        <div className="payset-method-body">
          <div className="payset-method-title">{t('op.dc.title')}</div>
          <div className="payset-method-sub">{summary}</div>
        </div>
        <button type="button" className="ui-btn" onClick={() => setExpanded((v) => !v)}>
          {expanded ? t('common.cancel') : t('op.dc.configure')}
        </button>
      </div>

      {expanded && (
        <div className="payset-reveal">
          <div className="mgmt-form">
            <div className="mgmt-form-grid">
              <label className="mgmt-form-wide">{t('op.dc.card')}
                <input
                  type="text"
                  inputMode="numeric"
                  value={cardNumber}
                  disabled={saving}
                  placeholder={cardSet ? `${t('op.dc.cardSet')} · ••••${cardLast4}` : ''}
                  onChange={(event) => setCardNumber(event.currentTarget.value)}
                />
              </label>
              <label className="mgmt-form-wide">{t('op.dc.commentTemplate')}
                <input
                  type="text"
                  value={commentTemplate}
                  disabled={saving}
                  onChange={(event) => setCommentTemplate(event.currentTarget.value)}
                />
              </label>
              <label className="mgmt-check mgmt-form-wide">
                <input
                  type="checkbox"
                  checked={isActive}
                  disabled={saving}
                  onChange={(event) => setIsActive(event.currentTarget.checked)}
                />
                {t('op.dc.active')}
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
