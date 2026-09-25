// Тумблер правила: тактильнее галочки и уже знаком оператору по «Платежам и лояльности».
export function RuleSwitch({
  checked, onChange, disabled, name, hint
}: {
  checked: boolean;
  onChange: (value: boolean) => void;
  disabled: boolean;
  name: string;
  hint: string;
}) {
  return (
    <div className={`payset-rule${checked ? ' is-on' : ''}`}>
      <div className="payset-rule-top">
        <label className="payset-switch">
          <input
            type="checkbox"
            aria-label={name}
            checked={checked}
            disabled={disabled}
            onChange={(event) => onChange(event.currentTarget.checked)}
          />
          <span className="payset-track" />
          <span className="payset-knob" />
        </label>
        <div className="payset-rule-text">
          <div className="payset-rule-name">{name}</div>
          <div className="payset-rule-hint">{hint}</div>
        </div>
      </div>
    </div>
  );
}
