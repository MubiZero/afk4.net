import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor, majorToMinor } from '@/club/money';
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

  const numberField = (label: string, value: number | null, set: (n: number | null) => void) => (
    <label className="block text-sm">
      <span className="mb-1 block text-muted-foreground">{label}</span>
      <Input
        type="number"
        value={value === null ? '' : String(value)}
        onChange={e => set(e.target.value === '' ? null : Math.max(0, Math.trunc(Number(e.target.value))))}
      />
    </label>
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{mode === 'create' ? t('platform.billing.planForm.createTitle') : t('platform.billing.planForm.editTitle')}</DialogTitle>
        <div className="flex max-h-[60vh] flex-col gap-3 overflow-y-auto">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.code')}</span>
            <Input value={form.planCode} disabled={mode === 'edit'} onChange={e => onChange({ ...form, planCode: e.target.value })} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.name')}</span>
            <Input value={form.name} onChange={e => onChange({ ...form, name: e.target.value })} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.price')}</span>
            <Input type="number" value={String(minorToMajor(form.priceMinorUnits))} onChange={e => { const major = Math.max(0, Number(e.target.value) || 0); onChange({ ...form, priceMinorUnits: majorToMinor(major) }); }} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.currency')}</span>
            <Input value={form.currencyCode} onChange={e => onChange({ ...form, currencyCode: e.target.value.toUpperCase() })} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.interval')}</span>
            <Select value={form.billingInterval} onValueChange={v => onChange({ ...form, billingInterval: v })}>
              <SelectTrigger aria-label={t('platform.billing.planForm.interval')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="monthly">{t('platform.billing.interval.monthly')}</SelectItem>
                <SelectItem value="yearly">{t('platform.billing.interval.yearly')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
          {numberField(t('platform.billing.planForm.maxBranches'), form.maxBranches, n => onChange({ ...form, maxBranches: n }))}
          {numberField(t('platform.billing.planForm.maxDevices'), form.maxDevicesPerBranch, n => onChange({ ...form, maxDevicesPerBranch: n }))}
          {numberField(t('platform.billing.planForm.maxSessions'), form.maxConcurrentSessions, n => onChange({ ...form, maxConcurrentSessions: n }))}
          {numberField(t('platform.billing.planForm.maxStaff'), form.maxStaffUsersPerBranch, n => onChange({ ...form, maxStaffUsersPerBranch: n }))}
          {numberField(t('platform.billing.planForm.sortOrder'), form.sortOrder, n => onChange({ ...form, sortOrder: n ?? 0 }))}
          {mode === 'edit' && (
            <label className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">{t('platform.billing.planForm.active')}</span>
              <Switch checked={form.isActive} onCheckedChange={c => onChange({ ...form, isActive: c })} />
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('platform.billing.action.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={onSubmit}>{t('platform.billing.planForm.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
