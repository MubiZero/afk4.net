import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select } from '@/components/ui/select';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor, majorToMinor } from '@/lib/money';
import { validatePlanForm, type PlanForm } from './billingModel';

interface Props {
  open: boolean;
  mode: 'create' | 'edit';
  form: PlanForm;
  pending: boolean;
  onChange: (form: PlanForm) => void;
  onSubmit: () => void;
  onOpenChange: (open: boolean) => void;
}

export function PlanFormDialog({ open, mode, form, pending, onChange, onSubmit, onOpenChange }: Props) {
  const { t } = useI18n();
  const valid = validatePlanForm(form);

  const numberField = (id: string, label: string, value: number | null, set: (next: number | null) => void) => (
    <Field key={id} label={label} htmlFor={id}>
      <Input
        id={id}
        type="number"
        value={value === null ? '' : String(value)}
        onChange={event => set(event.target.value === '' ? null : Math.max(0, Math.trunc(Number(event.target.value))))}
      />
    </Field>
  );

  return (
    <Dialog
      open={open}
      title={mode === 'create' ? t('platform.billing.planForm.createTitle') : t('platform.billing.planForm.editTitle')}
      description={t('platform.billing.planForm.description')}
      onClose={() => onOpenChange(false)}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('platform.billing.action.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={onSubmit}>{t('platform.billing.planForm.save')}</Button>
        </>
      }
    >
      <div className="pc-form">
        <Field label={t('platform.billing.planForm.code')} htmlFor="plan-code">
          <Input id="plan-code" value={form.planCode} disabled={mode === 'edit'} onChange={event => onChange({ ...form, planCode: event.target.value })} />
        </Field>
        <Field label={t('platform.billing.planForm.name')} htmlFor="plan-name">
          <Input id="plan-name" value={form.name} onChange={event => onChange({ ...form, name: event.target.value })} />
        </Field>

        <div className="pc-form-grid">
          <Field label={t('platform.billing.planForm.price')} htmlFor="plan-price">
            <Input
              id="plan-price"
              type="number"
              value={String(minorToMajor(form.priceMinorUnits))}
              onChange={event => onChange({ ...form, priceMinorUnits: majorToMinor(Math.max(0, Number(event.target.value) || 0)) })}
            />
          </Field>
          <Field label={t('platform.billing.planForm.currency')} htmlFor="plan-currency">
            <Input id="plan-currency" value={form.currencyCode} onChange={event => onChange({ ...form, currencyCode: event.target.value.toUpperCase() })} />
          </Field>
          <Field label={t('platform.billing.planForm.interval')} htmlFor="plan-interval">
            <Select id="plan-interval" value={form.billingInterval} onChange={event => onChange({ ...form, billingInterval: event.target.value })}>
              <option value="monthly">{t('platform.billing.interval.monthly')}</option>
              <option value="yearly">{t('platform.billing.interval.yearly')}</option>
            </Select>
          </Field>
        </div>

        <div className="pc-form-grid">
          {numberField('plan-max-branches', t('platform.billing.planForm.maxBranches'), form.maxBranches, next => onChange({ ...form, maxBranches: next }))}
          {numberField('plan-max-devices', t('platform.billing.planForm.maxDevices'), form.maxDevicesPerBranch, next => onChange({ ...form, maxDevicesPerBranch: next }))}
          {numberField('plan-max-sessions', t('platform.billing.planForm.maxSessions'), form.maxConcurrentSessions, next => onChange({ ...form, maxConcurrentSessions: next }))}
          {numberField('plan-max-staff', t('platform.billing.planForm.maxStaff'), form.maxStaffUsersPerBranch, next => onChange({ ...form, maxStaffUsersPerBranch: next }))}
          {numberField('plan-sort-order', t('platform.billing.planForm.sortOrder'), form.sortOrder, next => onChange({ ...form, sortOrder: next ?? 0 }))}
        </div>

        {mode === 'edit' ? (
          <label className="pc-check-row">
            <Switch checked={form.isActive} onCheckedChange={checked => onChange({ ...form, isActive: checked })} />
            {t('platform.billing.planForm.active')}
          </label>
        ) : null}
      </div>
    </Dialog>
  );
}
