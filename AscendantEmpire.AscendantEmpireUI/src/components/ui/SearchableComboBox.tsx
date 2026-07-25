// SearchableComboBox — a generic, reusable inline "type to filter, click to pick" combo box.
//
// Unlike SearchDrawer (a full side-drawer) this is a compact input meant to sit inline in a toolbar/header:
// typing filters the item list client-side (case-insensitive substring match, mirroring SearchDrawer's
// filtering), and the dropdown below the input (styled after DescriptiveSettingDropDown's popover) closes
// on outside click, Escape, or a successful pick. The component is generic over the item type `T` so the
// same combo box can list templates, components, or anything else via small accessor functions.

import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";

export interface SearchableComboBoxProps<T> {
  /** The full, unfiltered set of items to choose from (filtering happens inside the combo box). */
  items: T[];
  /** Stable React key for an item. */
  getKey: (item: T) => string | number;
  /** Primary (bold) line for a row — typically the display name. */
  getPrimaryText: (item: T) => string;
  /** Optional secondary (dimmed) line for a row. */
  getSecondaryText?: (item: T) => string;
  /** The text an item is matched against when filtering. */
  getSearchText: (item: T) => string;
  /** Placeholder for the filter textbox. */
  placeholder?: string;
  /** While true, the dropdown shows a busy message instead of the list. */
  loading?: boolean;
  /** When set, the dropdown shows this message instead of the list. */
  error?: string | null;
  /** Disables the input entirely. */
  disabled?: boolean;
  /**
   * Overrides the input's CSS classes. The default styling is tuned for a dark toolbar header (white text on
   * a translucent fill); pass a light-field class set when placing the combo box on a light surface.
   */
  inputClassName?: string;
  /** Called with the picked item; the combo box then clears its text and closes. */
  onSelect: (item: T) => void;
}

export default function SearchableComboBox<T>({
  items,
  getKey,
  getPrimaryText,
  getSecondaryText,
  getSearchText,
  placeholder = "Search…",
  loading = false,
  error = null,
  disabled = false,
  inputClassName,
  onSelect,
}: SearchableComboBoxProps<T>) {
  const [searchText, setSearchText] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement | null>(null);

  // Close on outside click (mirrors DescriptiveSettingDropDown's outside-click handling).
  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const onPointerDown = (e: PointerEvent) => {
      const el = rootRef.current;
      if (!el) {
        return;
      }
      if (e.target instanceof Node && el.contains(e.target)) {
        return;
      }
      setIsOpen(false);
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
    };
  }, [isOpen]);

  const filteredItems = useMemo(() => {
    const needle = searchText.trim().toLowerCase();
    if (needle === "") {
      return items;
    }
    return items.filter((item) => getSearchText(item).toLowerCase().includes(needle));
  }, [items, searchText, getSearchText]);

  const handleSelect = (item: T) => {
    onSelect(item);
    setSearchText("");
    setIsOpen(false);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Escape") {
      setIsOpen(false);
      return;
    }
    if (e.key === "ArrowDown") {
      e.preventDefault();
      const firstOption = rootRef.current?.querySelector<HTMLElement>('[role="option"]');
      firstOption?.focus();
    }
  };

  const handleOptionKeyDown = (e: KeyboardEvent<HTMLDivElement>, item: T) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      handleSelect(item);
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      (e.currentTarget.nextElementSibling as HTMLElement | null)?.focus();
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      (e.currentTarget.previousElementSibling as HTMLElement | null)?.focus();
    } else if (e.key === "Escape") {
      setIsOpen(false);
    }
  };

  return (
    <div ref={rootRef} className="relative inline-block">
      <input
        className={
          inputClassName ??
          "rounded-md border border-white/20 bg-white/10 px-2 py-1 text-sm text-white placeholder-white/50 outline-none focus-visible:ring-2 focus-visible:ring-white/30"
        }
        value={searchText}
        placeholder={placeholder}
        disabled={disabled}
        onFocus={() => setIsOpen(true)}
        onChange={(e) => {
          setSearchText(e.target.value);
          setIsOpen(true);
        }}
        onKeyDown={handleKeyDown}
      />

      {isOpen && !disabled && (
        <div
          role="listbox"
          className="absolute left-0 top-full z-50 mt-1 max-h-72 w-72 overflow-auto rounded border border-slate-300 bg-white py-1 shadow-md">
          {loading && <div className="px-3 py-1.5 text-sm text-slate-500">Loading…</div>}
          {!loading && error != null && <div className="px-3 py-1.5 text-sm text-red-600">{error}</div>}
          {!loading && error == null && filteredItems.length === 0 && <div className="px-3 py-1.5 text-sm text-slate-500">No matches.</div>}
          {!loading &&
            error == null &&
            filteredItems.map((item) => {
              const secondary = getSecondaryText?.(item);
              return (
                <div
                  key={getKey(item)}
                  role="option"
                  aria-selected={false}
                  tabIndex={0}
                  className="cursor-pointer select-none px-3 py-1.5 text-sm hover:bg-slate-100 focus:outline-none focus-visible:bg-slate-100"
                  onClick={() => handleSelect(item)}
                  onKeyDown={(e) => handleOptionKeyDown(e, item)}>
                  <div className="font-medium text-slate-800">{getPrimaryText(item)}</div>
                  {secondary != null && secondary !== "" && <div className="text-xs text-slate-500">{secondary}</div>}
                </div>
              );
            })}
        </div>
      )}
    </div>
  );
}
