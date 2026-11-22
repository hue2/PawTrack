interface FilterSelectProps {
  label: string;
  allLabel: string;
  options: string[];
  value: string;
  onChange: (value: string) => void;
}

export default function FilterSelect({ label, allLabel, options, value, onChange }: FilterSelectProps) {
  return (
    <div className="col-auto">
      <select
        aria-label={label}
        className="form-select form-select-sm"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      >
        <option value="">{allLabel}</option>
        {options.map((o) => (
          <option key={o} value={o}>{o}</option>
        ))}
      </select>
    </div>
  );
}
