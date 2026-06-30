// SearchDrawer — a generic, reusable left-hand search panel built on top of the base Drawer.
//
// It renders a filter textfield pinned to the top and a scrollable list filling the remaining vertical
// space. Typing filters the list client-side (case-insensitive substring match against each item's
// search text). Double-clicking a row activates it (`onActivate`). The component is deliberately generic
// over the item type `T`: callers supply small accessor functions for the key, the primary/secondary
// display text, and the text to search against, so the same drawer can list components, tags, or anything
// else.

import { useMemo, useState, type ReactNode } from "react";

import Alert from "./Alert";
import Drawer from "./Drawer";
import Spinner from "./Spinner";

export interface SearchDrawerProps<T> {
  open: boolean;
  title: string;
  /** The full, unfiltered set of items to show (filtering happens inside the drawer). */
  items: T[];
  /** While true, a spinner replaces the list (e.g. the initial fetch is in flight). */
  loading?: boolean;
  /** When set, an error alert replaces the list. */
  error?: string | null;
  /** Stable React key for a row. */
  getKey: (item: T) => string | number;
  /** Primary (bold) line for a row — typically the display name. */
  getPrimaryText: (item: T) => string;
  /** Optional secondary (dimmed) line for a row — typically the code. */
  getSecondaryText?: (item: T) => string;
  /** The text a row is matched against when filtering. */
  getSearchText: (item: T) => string;
  /** Placeholder for the filter textfield. */
  placeholder?: string;
  onClose: () => void;
  /** Called when a row is double-clicked. */
  onActivate: (item: T) => void;
}

export default function SearchDrawer<T>({
  open,
  title,
  items,
  loading = false,
  error = null,
  getKey,
  getPrimaryText,
  getSecondaryText,
  getSearchText,
  placeholder = "Search…",
  onClose,
  onActivate,
}: SearchDrawerProps<T>) {
  // The current filter text. Reset is handled by the Drawer unmounting when closed (so a fresh open
  // starts blank without an explicit effect).
  const [searchText, setSearchText] = useState("");

  // Case-insensitive substring filter over each item's search text. Recomputed only when the inputs
  // change so typing through a large catalog stays cheap.
  const filteredItems = useMemo(() => {
    const needle = searchText.trim().toLowerCase();
    if (needle === "") {
      return items;
    }

    return items.filter((item) => getSearchText(item).toLowerCase().includes(needle));
  }, [items, searchText, getSearchText]);

  // The body content swaps between loading / error / empty / list states.
  let body: ReactNode;
  if (loading) {
    body = (
      <div className="flex flex-1 items-center justify-center">
        <Spinner />
      </div>
    );
  } else if (error != null) {
    body = <Alert variant="danger">{error}</Alert>;
  } else if (filteredItems.length === 0) {
    body = <div className="flex flex-1 items-center justify-center text-sm text-slate-400">No matches.</div>;
  } else {
    body = (
      // The list fills the remaining height and scrolls on overflow. min-h-0 lets it shrink inside the
      // flex column so the scroll happens here rather than on the whole drawer.
      <ul className="min-h-0 flex-1 overflow-auto">
        {filteredItems.map((item) => {
          const secondary = getSecondaryText?.(item);
          return (
            <li
              key={getKey(item)}
              className="cursor-pointer select-none rounded-md px-2 py-1.5 hover:bg-slate-100"
              // Double-click opens the item in its editor; single clicks are intentionally inert.
              onDoubleClick={() => onActivate(item)}>
              <div className="text-sm font-medium text-slate-800">{getPrimaryText(item)}</div>
              {secondary != null && secondary !== "" && <div className="text-xs text-slate-400">{secondary}</div>}
            </li>
          );
        })}
      </ul>
    );
  }

  return (
    <Drawer open={open} title={title} side="left" onClose={onClose}>
      {/* Full-height flex column: the textfield stays put while the list below it scrolls. */}
      <div className="flex h-full flex-col gap-3">
        <input
          className="shrink-0 rounded-md border border-slate-300 px-2 py-1.5 text-sm text-slate-800 outline-none focus:border-slate-500"
          value={searchText}
          placeholder={placeholder}
          autoFocus
          onChange={(e) => setSearchText(e.target.value)}
        />
        {body}
      </div>
    </Drawer>
  );
}
