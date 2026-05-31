export type ToggleSwitchInlineLabel = "on-off" | "yes-no";

export interface ToggleSwitchProps {
  id: number;
  value: boolean;
  onChange: (id: number, value: boolean) => void;
  label?: string;
  inlineLabel?: ToggleSwitchInlineLabel;
  disabled?: boolean;
}

const INLINE_LABELS: Record<ToggleSwitchInlineLabel, [string, string]> = {
  "on-off": ["On", "Off"],
  "yes-no": ["Yes", "No"],
};

export default function ToggleSwitch({ id, value, onChange, label, inlineLabel, disabled = false }: ToggleSwitchProps) {
  const switchId = `toggle-switch-${id}`;
  const hasInlineLabel = inlineLabel !== undefined;

  const trackClass = hasInlineLabel
    ? "relative h-6 w-12 shrink-0 rounded-full border-2 border-transparent transition-colors duration-200"
    : "relative inline-flex h-5 w-9 shrink-0 rounded-full border-2 border-transparent transition-colors duration-200";

  // Track inner width (padding area) = 48px (w-12) - 4px (border-2 × 2) = 44px
  // Thumb width = 20px (h-5/w-5), gap = 2px → on-position left = 44 - 20 - 2 = 22px = 1.375rem
  const thumbClass = hasInlineLabel
    ? ["absolute inset-y-0 my-auto h-5 w-5 rounded-full bg-white shadow transition-all duration-200", value ? "left-[1.375rem]" : "left-0.5"].join(
        " ",
      )
    : [
        "pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transform transition-transform duration-200",
        value ? "translate-x-4" : "translate-x-0",
      ].join(" ");

  const [onText, offText] = hasInlineLabel ? INLINE_LABELS[inlineLabel] : ["", ""];

  return (
    <label
      htmlFor={switchId}
      className={`inline-flex items-center gap-2 select-none ${disabled ? "opacity-60 cursor-not-allowed" : "cursor-pointer"}`}>
      <span
        role="switch"
        aria-checked={value}
        id={switchId}
        onClick={() => !disabled && onChange(id, !value)}
        onKeyDown={(e) => {
          if (!disabled && (e.key === " " || e.key === "Enter")) {
            e.preventDefault();
            onChange(id, !value);
          }
        }}
        tabIndex={disabled ? -1 : 0}
        className={[
          trackClass,
          "focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-slate-400",
          value ? "bg-[var(--bs-primary)]" : "bg-slate-300",
        ].join(" ")}>
        <span className={thumbClass} />
        {hasInlineLabel && (
          <span
            className={[
              "absolute top-0 bottom-0 flex items-center text-[9px] font-semibold leading-none text-white select-none pointer-events-none",
              value ? "left-1.5" : "right-1.5",
            ].join(" ")}>
            {value ? onText : offText}
          </span>
        )}
      </span>
      {label && <span className="text-sm">{label}</span>}
    </label>
  );
}
