import { useEffect, useRef, useState } from "react";

export interface SelectionOption {
  id: number;
  label: string;
}

export interface DescriptiveSettingDropDownProps {
  multiSelect: boolean;
  options: SelectionOption[];
  /** Represents the "nothing selected" state. Not included in the options list. */
  noSelectionOption?: SelectionOption;
  /** Represents the "all selected" state. Not included in the options list. */
  allSelectionOption?: SelectionOption;
  /** Separator between items when multiple are selected. Defaults to ", ". */
  separator?: string;
  /** Separator used before the last item. When omitted, falls back to separator. */
  finalSeparator?: string;
  /** Current selection – the parent owns this state. */
  value: SelectionOption[];
  onChange: (selected: SelectionOption[]) => void;
}

export default function DescriptiveSettingDropDown({
  multiSelect,
  options,
  noSelectionOption,
  allSelectionOption,
  separator = ", ",
  finalSeparator,
  value,
  onChange,
}: DescriptiveSettingDropDownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLSpanElement | null>(null);

  // Close on outside click or Escape
  useEffect(() => {
    if (!isOpen) return;

    const onPointerDown = (e: PointerEvent) => {
      const el = rootRef.current;
      if (!el) return;
      if (e.target instanceof Node && el.contains(e.target)) return;
      setIsOpen(false);
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
    };
  }, [isOpen]);

  const listboxRef = useRef<HTMLDivElement | null>(null);

  // Focus first option when dropdown opens
  useEffect(() => {
    if (!isOpen) return;
    const firstOption = listboxRef.current?.querySelector<HTMLElement>('[role="option"]');
    firstOption?.focus();
  }, [isOpen]);

  // Derive display label
  const displayLabel = (() => {
    if (value.length === 0) return noSelectionOption?.label ?? "";
    if (allSelectionOption && options.every((o) => value.some((v) => v.id === o.id))) return allSelectionOption.label;
    const labels = value.map((v) => v.label);
    if (labels.length <= 1) return labels[0] ?? "";
    const effectiveFinal = finalSeparator ?? separator;
    return labels.slice(0, -1).join(separator) + effectiveFinal + labels[labels.length - 1];
  })();

  const isSelected = (option: SelectionOption) => value.some((v) => v.id === option.id);

  const handleOptionClick = (option: SelectionOption) => {
    if (multiSelect) {
      if (isSelected(option)) {
        onChange(value.filter((v) => v.id !== option.id));
      } else {
        onChange([...value, option]);
      }
    } else {
      if (isSelected(option) && noSelectionOption !== undefined) {
        // Deselect – return to no-selection state
        onChange([]);
      } else {
        onChange([option]);
      }
      setIsOpen(false);
    }
  };

  return (
    <span ref={rootRef} className="relative inline-block">
      {/* Display label – underlined, acts as the toggle trigger */}
      <span
        role="button"
        tabIndex={0}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        className="cursor-pointer underline decoration-current underline-offset-2"
        onClick={() => setIsOpen((o) => !o)}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            setIsOpen((o) => !o);
          }
        }}>
        {displayLabel || "\u00A0"}
      </span>

      {/* Dropdown */}
      {isOpen && (
        <div
          role="listbox"
          aria-multiselectable={multiSelect}
          ref={listboxRef}
          onKeyDown={(e) => {
            if (e.key === "Escape") {
              e.stopPropagation();
              setIsOpen(false);
            }
          }}
          className="absolute left-0 top-full z-50 mt-1 min-w-max rounded border border-slate-300 bg-white py-1 shadow-md">
          {options.map((option) => {
            const selected = isSelected(option);
            return (
              <div
                key={option.id}
                role="option"
                aria-selected={selected}
                tabIndex={0}
                className={[
                  "flex cursor-pointer select-none items-center gap-2 px-3 py-1.5 text-sm hover:bg-slate-100 focus:outline-none focus-visible:bg-slate-100",
                  selected ? "font-medium" : "",
                ].join(" ")}
                onClick={() => handleOptionClick(option)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    handleOptionClick(option);
                  } else if (e.key === "ArrowDown") {
                    e.preventDefault();
                    (e.currentTarget.nextElementSibling as HTMLElement | null)?.focus();
                  } else if (e.key === "ArrowUp") {
                    e.preventDefault();
                    (e.currentTarget.previousElementSibling as HTMLElement | null)?.focus();
                  }
                }}>
                {/* Checkbox (multi) or radio (single) indicator */}
                {multiSelect ? (
                  <span
                    className={[
                      "flex h-4 w-4 shrink-0 items-center justify-center rounded border",
                      selected ? "border-[var(--bs-primary)] bg-[var(--bs-primary)]" : "border-slate-400 bg-white",
                    ].join(" ")}>
                    {selected && (
                      <svg viewBox="0 0 10 8" className="h-2.5 w-2.5" aria-hidden="true">
                        <path d="M1 4l2.5 2.5L9 1" stroke="white" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" fill="none" />
                      </svg>
                    )}
                  </span>
                ) : (
                  <span
                    className={[
                      "flex h-4 w-4 shrink-0 items-center justify-center rounded-full border",
                      selected ? "border-[var(--bs-primary)]" : "border-slate-400",
                    ].join(" ")}>
                    {selected && <span className="h-2 w-2 rounded-full bg-[var(--bs-primary)]" />}
                  </span>
                )}

                <span>{option.label}</span>
              </div>
            );
          })}
        </div>
      )}
    </span>
  );
}
