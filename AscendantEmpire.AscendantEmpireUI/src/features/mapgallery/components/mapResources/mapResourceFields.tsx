// Small controlled inputs shared by the MapResource Type Manager's pages.
//
// Every numeric value the manager edits is one of two things: a whole number (quantities, SpreadLimits, weights), or a fraction the designer sees
// as a percent (likelihoods, growth, fuzzy factors). These wrap a plain <input> so each page states which it is, and an empty box means "not
// set" (null) rather than 0.
//
// PercentSlider is the exception to that last part: a dragged knob has no empty state, so it reads a null as 0 and always writes a number back.
// A percent only belongs on a slider where null and 0 mean the same thing to the field behind it; where "not set" is a distinct choice (a
// CaptureLikelihood that only applies to the Capture method, say), the field stays a PercentInput.

import { useEffect, useLayoutEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { createPortal } from "react-dom";

import { HintIcon } from "@/components/ui";
import { fromPercentInput, fromPercentSlider, fromWholeInput, toPercentInput, toPercentSlider } from "../../tools/mapResourceDraft";

// Full-width for the label/control rows; compact for table cells, where a page may show up to nine columns.
const FULL_CLASS = "w-full rounded-md border border-slate-300 px-2 py-2 text-sm disabled:bg-slate-100 disabled:text-slate-400";
const COMPACT_CLASS = "w-full min-w-[4rem] rounded border border-slate-300 px-1.5 py-1 text-xs disabled:bg-slate-100 disabled:text-slate-400";

type NumberInputProps<T extends number | null> = {
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
  compact?: boolean;
  placeholder?: string;
  ariaLabel?: string;
};

/**
 * A whole-number input. With `required`, clearing the box gives 0 rather than null, for the values the server always needs (a layer's quantity
 * range, a scenario amount).
 */
export function WholeNumberInput<T extends number | null>({
  value,
  onChange,
  disabled,
  compact,
  placeholder,
  ariaLabel,
  required,
}: NumberInputProps<T> & { required?: boolean }) {
  return (
    <input
      type="number"
      step={1}
      min={0}
      className={compact ? COMPACT_CLASS : FULL_CLASS}
      value={value ?? ""}
      placeholder={placeholder}
      aria-label={ariaLabel}
      disabled={disabled}
      onChange={(e) => {
        const parsed = fromWholeInput(e.target.value);
        onChange((parsed ?? (required ? 0 : null)) as T);
      }}
    />
  );
}

/** A fraction shown and typed as a percent (0.25 ⇄ 25). With `required`, clearing the box gives 0 rather than null. */
export function PercentInput<T extends number | null>({
  value,
  onChange,
  disabled,
  compact,
  placeholder,
  ariaLabel,
  required,
}: NumberInputProps<T> & { required?: boolean }) {
  return (
    <div className="relative">
      <input
        type="number"
        step={0.1}
        min={0}
        max={100}
        className={`${compact ? COMPACT_CLASS : FULL_CLASS} pr-6`}
        value={toPercentInput(value)}
        placeholder={placeholder}
        aria-label={ariaLabel}
        disabled={disabled}
        onChange={(e) => {
          const parsed = fromPercentInput(e.target.value);
          onChange((parsed ?? (required ? 0 : null)) as T);
        }}
      />
      <span className="pointer-events-none absolute inset-y-0 right-2 flex items-center text-xs text-slate-400" aria-hidden="true">
        %
      </span>
    </div>
  );
}

/**
 * A fraction dragged as a percent, for the full-width label/control rows.
 * <remarks>
 * The knob steps in whole percents; the readout beside it shows the stored value to a tenth, so a value that arrived from the server carrying
 * tenths is reported honestly rather than being silently rounded by the mere act of opening the dialog. Dragging then brings the two together.
 * </remarks>
 */
export function PercentSlider({
  value,
  onChange,
  disabled,
  ariaLabel,
}: {
  value: number | null;
  onChange: (value: number) => void;
  disabled?: boolean;
  ariaLabel?: string;
}) {
  return (
    <div className="flex items-center gap-3">
      <input
        type="range"
        step={1}
        min={0}
        max={100}
        className="w-full disabled:opacity-50"
        value={toPercentSlider(value)}
        aria-label={ariaLabel}
        disabled={disabled}
        onChange={(e) => onChange(fromPercentSlider(Number(e.target.value)))}
      />
      {/* Fixed width so the track does not shift as the number gains or loses a digit while being dragged. */}
      <span className={`w-14 shrink-0 text-right text-sm tabular-nums ${disabled ? "text-slate-400" : "text-slate-700"}`}>
        {toPercentInput(value ?? 0)}%
      </span>
    </div>
  );
}

/**
 * A whole number dragged on a slider, for the full-width label/control rows.
 * <remarks>
 * Like PercentSlider, a dragged knob has no empty state: it reads a null as `min` and always writes a number back. So this belongs only on a
 * field that is always set — one the server insists on, or one a kind switch defaults — and a field where "not set" is a distinct choice stays
 * a WholeNumberInput.
 *
 * The caller gives the bounds, because they are the field's own rule rather than anything general: the knob cannot leave them, which is the
 * point of choosing a slider over a box for a value with a small, known range.
 * </remarks>
 */
export function WholeNumberSlider({
  value,
  onChange,
  min,
  max,
  disabled,
  ariaLabel,
  unitLabel,
}: {
  value: number | null;
  onChange: (value: number) => void;
  min: number;
  max: number;
  disabled?: boolean;
  ariaLabel?: string;
  /** Appended to the readout, e.g. "Rounds". Singular/plural is the caller's business; omit it for a bare number. */
  unitLabel?: string;
}) {
  return (
    <div className="flex items-center gap-3">
      <input
        type="range"
        step={1}
        min={min}
        max={max}
        className="w-full disabled:opacity-50"
        value={value ?? min}
        aria-label={ariaLabel}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
      />
      {/* Fixed width so the track does not shift as the number gains or loses a digit while being dragged — the same reason PercentSlider pins
          its readout. Wide enough for the longest unit label the page uses. */}
      <span className={`w-20 shrink-0 text-right text-sm tabular-nums ${disabled ? "text-slate-400" : "text-slate-700"}`}>
        {value ?? min}
        {unitLabel ? ` ${unitLabel}` : ""}
      </span>
    </div>
  );
}

/** A labelled checkbox, laid out like the Biome Manager's. */
export function CheckboxRow({
  label,
  checked,
  onChange,
  disabled,
  hint,
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  hint?: string;
}) {
  return (
    <label className="mb-2 flex items-center gap-2 text-sm text-slate-700">
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      {label}
      {/* Same info-icon affordance as FieldRow's hint, so a checkbox row reads identically to the rows around it. */}
      <HintIcon hint={hint} className="align-middle" />
    </label>
  );
}

/**
 * A compact combo box for table cells: shows the chosen option, and typing filters the list.
 *
 * <remarks>
 * For a picker whose list is long enough that scrolling a native dropdown is the slow way round — the Assets list, which grows with the economy.
 * A short, fixed list (a Biome, a Geography) stays a CompactSelect, where the native dropdown is better: it is keyboard-navigable for free, needs
 * no portal, and cannot drift out of step with its value.
 *
 * Unlike the shared SearchableComboBox, which is a search affordance that clears itself after a pick, this edits a value: the current option's
 * label is always on display, and closing without picking puts it back. Typing replaces the label (the text is selected on open) so the first
 * keystroke filters rather than appending to it.
 *
 * Generic over the value type, and the picked option is handed back whole, so the caller gets the option's own value with no parsing — the
 * stringly-typed round trip through a DOM &lt;select&gt; is what let a Number() coercion silently produce NaN here before.
 *
 * The list is portaled to the body and positioned fixed, like Tooltip's bubble, because a table cell inside the dialog's scrolling page region
 * would otherwise clip it.
 * </remarks>
 */
export function CompactComboBox<TValue extends number | string>({
  value,
  onChange,
  options,
  ariaLabel,
  disabled,
  emptyHint = "No matches.",
}: {
  value: TValue;
  onChange: (value: TValue) => void;
  options: { value: TValue; label: string }[];
  ariaLabel: string;
  disabled?: boolean;
  /** Shown in place of the list when the typed text matches nothing. */
  emptyHint?: string;
}) {
  const [isOpen, setIsOpen] = useState(false);
  /**
   * What the designer has typed since the list opened, or null if they have not typed yet.
   *
   * Null rather than "" because the two have to behave differently: while untouched the box shows the chosen option's label but the list is
   * unfiltered, so opening it offers everything rather than just the one option already picked. Seeding this with the label instead would filter
   * the list down to that label on sight.
   */
  const [typedText, setTypedText] = useState<string | null>(null);
  // Which row the arrow keys are on. Reset whenever the filter changes, since the row it pointed at may no longer be listed.
  const [activeIndex, setActiveIndex] = useState(0);
  const [listStyle, setListStyle] = useState({ top: 0, left: 0, width: 0 });

  const rootRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const listRef = useRef<HTMLDivElement | null>(null);

  // The label for the value as it stands. An option list that does not hold the value is possible (assetOptions keeps a deleted Asset as its own
  // option for exactly this reason), so fall back to the raw value rather than showing an empty box.
  const selectedLabel = options.find((option) => option.value === value)?.label ?? String(value);

  const filteredOptions = useMemo(() => {
    const needle = (typedText ?? "").trim().toLowerCase();
    if (needle === "") {
      return options;
    }
    return options.filter((option) => option.label.toLowerCase().includes(needle));
  }, [options, typedText]);

  const close = () => {
    setIsOpen(false);
    setTypedText(null);
  };

  const open = () => {
    if (disabled) {
      return;
    }
    setIsOpen(true);
    setTypedText(null);
    setActiveIndex(0);
    // The label is selected, so the first keystroke replaces it rather than appending to it — the box reads as the current value until the
    // designer starts typing over it.
    requestAnimationFrame(() => inputRef.current?.select());
  };

  const pick = (option: { value: TValue; label: string }) => {
    onChange(option.value);
    close();
  };

  // Positioned against the trigger and kept there, the same way Tooltip tracks its bubble: fixed coordinates, recomputed on scroll of any
  // ancestor (capture phase, since scroll does not bubble) and on resize.
  useLayoutEffect(() => {
    if (!isOpen) {
      return;
    }

    const reposition = () => {
      const trigger = rootRef.current;
      const list = listRef.current;
      if (!trigger) {
        return;
      }

      const triggerRect = trigger.getBoundingClientRect();
      const listHeight = list?.offsetHeight ?? 0;
      const gap = 2;

      // Below the field by default; above it when there is no room below, so a row near the bottom of the dialog still shows its list.
      const roomBelow = window.innerHeight - triggerRect.bottom;
      const openUpwards = roomBelow < listHeight + gap && triggerRect.top > roomBelow;

      setListStyle({
        top: openUpwards ? triggerRect.top - listHeight - gap : triggerRect.bottom + gap,
        left: triggerRect.left,
        // Matched to the field, but never so narrow that the labels are unreadable in a cramped column.
        width: Math.max(triggerRect.width, 180),
      });
    };

    reposition();
    window.addEventListener("resize", reposition);
    window.addEventListener("scroll", reposition, { capture: true, passive: true });

    return () => {
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition, { capture: true } as EventListenerOptions);
    };
  }, [isOpen, filteredOptions.length]);

  // Closing on an outside pointer press rather than on blur: blur fires before the click on an option lands, which would close the list out from
  // under the pick. The list is portaled, so it is not inside rootRef — both have to be checked.
  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const onPointerDown = (e: PointerEvent) => {
      if (e.target instanceof Node && (rootRef.current?.contains(e.target) || listRef.current?.contains(e.target))) {
        return;
      }
      close();
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
    };
  }, [isOpen]);

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Escape") {
      // Abandons the typing and puts the chosen option's label back, leaving the value alone.
      e.stopPropagation();
      close();
      return;
    }

    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      if (!isOpen) {
        open();
        return;
      }
      if (filteredOptions.length > 0) {
        const step = e.key === "ArrowDown" ? 1 : -1;
        setActiveIndex((current) => (current + step + filteredOptions.length) % filteredOptions.length);
      }
      return;
    }

    if (e.key === "Enter" && isOpen) {
      e.preventDefault();
      const option = filteredOptions[activeIndex];
      if (option) {
        pick(option);
      }
    }
  };

  return (
    <div ref={rootRef} className="relative">
      <input
        ref={inputRef}
        type="text"
        role="combobox"
        aria-expanded={isOpen}
        aria-controls={isOpen ? `${ariaLabel}-options` : undefined}
        autoComplete="off"
        className={COMPACT_CLASS}
        value={isOpen ? (typedText ?? selectedLabel) : selectedLabel}
        aria-label={ariaLabel}
        disabled={disabled}
        onFocus={open}
        // Re-opens after Escape, where the input kept focus and onFocus will not fire again.
        onClick={() => (isOpen ? undefined : open())}
        onChange={(e) => {
          setTypedText(e.target.value);
          setActiveIndex(0);
          setIsOpen(true);
        }}
        onKeyDown={handleKeyDown}
      />

      {isOpen &&
        !disabled &&
        createPortal(
          <div
            ref={listRef}
            id={`${ariaLabel}-options`}
            role="listbox"
            // Above the dialog (z-[1080]) for the same reason Tooltip's bubble is: portaled to the body, it is a sibling of the modal rather
            // than nested inside it, so a lower z-index would put it behind.
            className="fixed z-[1090] max-h-56 overflow-auto rounded border border-slate-300 bg-white py-1 text-xs shadow-md"
            style={{ top: listStyle.top, left: listStyle.left, width: listStyle.width }}>
            {filteredOptions.length === 0 ? (
              <div className="px-2 py-1.5 text-slate-500">{emptyHint}</div>
            ) : (
              filteredOptions.map((option, index) => (
                <div
                  key={option.value}
                  role="option"
                  aria-selected={option.value === value}
                  className={`cursor-pointer select-none px-2 py-1 ${index === activeIndex ? "bg-slate-100" : ""} ${
                    option.value === value ? "font-semibold text-slate-900" : "text-slate-700"
                  }`}
                  // Pointer-down rather than click, so the pick lands before the input's blur can interfere.
                  onPointerDown={(e) => {
                    e.preventDefault();
                    pick(option);
                  }}
                  onPointerEnter={() => setActiveIndex(index)}>
                  {option.label}
                </div>
              ))
            )}
          </div>,
          document.body,
        )}
    </div>
  );
}

/** A compact select for table cells. */
export function CompactSelect({
  value,
  onChange,
  options,
  ariaLabel,
  disabled,
}: {
  value: number | string;
  onChange: (value: string) => void;
  options: { value: number | string; label: string }[];
  ariaLabel: string;
  disabled?: boolean;
}) {
  return (
    <select className={COMPACT_CLASS} value={value} aria-label={ariaLabel} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
      {options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}
