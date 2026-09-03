// FreeTextComboBox — a controlled text field with a dropdown of suggestions.
//
// The distinction from SearchableComboBox is which half is authoritative. That one is a *picker*: it
// owns its text, clears it once an item is chosen, and only ever reports the item. This one is a
// *field*: the caller owns the value, whatever is typed is the value, and the suggestions are only a
// shortcut to typing one of them. Anything not on the list is a perfectly good answer.
//
// The dropdown is portaled and fixed-positioned, the same way ColorPicker's popup is, because these
// fields sit inside dialogs whose content column scrolls (`overflow-y-auto`) — an absolutely
// positioned panel would be clipped by it.

import { useEffect, useLayoutEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { createPortal } from "react-dom";
import { inputClass } from "./FieldRow";

export interface FreeTextComboBoxProps {
  /** The current value. Free text — never constrained to `suggestions`. */
  value: string;

  /** Raised on every keystroke and when a suggestion is picked. */
  onChange: (value: string) => void;

  /** The offered shortcuts, filtered as the user types. An empty list simply means no dropdown. */
  suggestions: readonly string[];

  placeholder?: string;

  /** Overrides the input's CSS classes, e.g. to add the error border. Defaults to the shared field styling. */
  inputClassName?: string;

  disabled?: boolean;

  /** Accessible name for the input, for the (common) case where the visible label is a sibling FieldRow label. */
  ariaLabel?: string;
}

export default function FreeTextComboBox({
  value,
  onChange,
  suggestions,
  placeholder,
  inputClassName,
  disabled = false,
  ariaLabel,
}: FreeTextComboBoxProps) {
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<{ top: number; left: number; width: number }>({ top: 0, left: 0, width: 0 });

  const inputRef = useRef<HTMLInputElement | null>(null);
  const listRef = useRef<HTMLDivElement | null>(null);

  // Substring match rather than prefix: a player typing "duke" should still find "Grand Duke".
  const filtered = useMemo(() => {
    const needle = value.trim().toLowerCase();
    if (needle === "") {
      return [...suggestions];
    }
    return suggestions.filter((suggestion) => suggestion.toLowerCase().includes(needle));
  }, [suggestions, value]);

  // Close on any pointer press outside the input and the (portaled) list, and on Escape.
  useEffect(() => {
    if (!open) {
      return;
    }

    const onPointerDown = (e: PointerEvent) => {
      if (!(e.target instanceof Node)) {
        return;
      }
      if (inputRef.current?.contains(e.target) || listRef.current?.contains(e.target)) {
        return;
      }
      setOpen(false);
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
    };
  }, [open]);

  // Anchor the list under the input, matching its width, flipping above when there is no room below.
  useLayoutEffect(() => {
    if (!open) {
      return;
    }

    const reposition = () => {
      const input = inputRef.current;
      const list = listRef.current;
      if (!input || !list) {
        return;
      }

      const inputRect = input.getBoundingClientRect();
      const listRect = list.getBoundingClientRect();
      const pad = 8;
      const gap = 4;

      let top = inputRect.bottom + gap;
      if (top + listRect.height > window.innerHeight - pad) {
        const above = inputRect.top - gap - listRect.height;
        // Flip above when there is room; otherwise pin to the bottom edge (the list scrolls internally).
        top = above >= pad ? above : Math.max(pad, window.innerHeight - pad - listRect.height);
      }

      const left = Math.max(pad, Math.min(inputRect.left, window.innerWidth - inputRect.width - pad));

      setPosition({ top: Math.round(top), left: Math.round(left), width: Math.round(inputRect.width) });
    };

    reposition();
    window.addEventListener("resize", reposition);
    // Capture phase so scrolling any ancestor — not just the window — keeps the list pinned to its input.
    window.addEventListener("scroll", reposition, { capture: true, passive: true });
    return () => {
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition, { capture: true } as AddEventListenerOptions);
    };
  }, [open, filtered.length]);

  const pick = (suggestion: string) => {
    onChange(suggestion);
    setOpen(false);
    inputRef.current?.focus();
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Escape" && open) {
      // Swallowed so the dialog this usually sits in does not treat it as Cancel while the list is open.
      e.stopPropagation();
      e.preventDefault();
      setOpen(false);
      return;
    }

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setOpen(true);
      listRef.current?.querySelector<HTMLElement>('[role="option"]')?.focus();
    }
  };

  const handleOptionKeyDown = (e: KeyboardEvent<HTMLDivElement>, suggestion: string) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      pick(suggestion);
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      (e.currentTarget.nextElementSibling as HTMLElement | null)?.focus();
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      const previous = e.currentTarget.previousElementSibling as HTMLElement | null;
      if (previous) {
        previous.focus();
      } else {
        // Off the top of the list: hand focus back to the field so typing carries on.
        inputRef.current?.focus();
      }
    } else if (e.key === "Escape") {
      e.stopPropagation();
      setOpen(false);
      inputRef.current?.focus();
    }
  };

  return (
    <>
      <input
        ref={inputRef}
        className={inputClassName ?? inputClass}
        role="combobox"
        aria-expanded={open}
        aria-autocomplete="list"
        aria-label={ariaLabel}
        value={value}
        placeholder={placeholder}
        disabled={disabled}
        onFocus={() => setOpen(true)}
        // Re-opens the list after Escape closed it, where onFocus won't fire again (the input kept focus).
        onClick={() => setOpen(true)}
        onChange={(e) => {
          onChange(e.target.value);
          setOpen(true);
        }}
        onKeyDown={handleKeyDown}
      />

      {open &&
        !disabled &&
        filtered.length > 0 &&
        createPortal(
          <div
            ref={listRef}
            role="listbox"
            // Above Modal (z-1080) and Drawer (z-1050), below Tooltip (z-1200) — the same band ColorPicker's popup uses.
            className="fixed z-[1150] max-h-60 overflow-auto rounded-md border border-slate-300 bg-white py-1 shadow-lg"
            style={{ top: position.top, left: position.left, width: position.width }}>
            {filtered.map((suggestion) => (
              <div
                key={suggestion}
                role="option"
                aria-selected={suggestion === value}
                tabIndex={0}
                className={`cursor-pointer select-none px-3 py-1.5 text-sm text-slate-800 hover:bg-slate-100 focus:outline-none focus-visible:bg-slate-100 ${
                  suggestion === value ? "font-medium" : ""
                }`}
                onClick={() => pick(suggestion)}
                onKeyDown={(e) => handleOptionKeyDown(e, suggestion)}>
                {suggestion}
              </div>
            ))}
          </div>,
          document.body,
        )}
    </>
  );
}
