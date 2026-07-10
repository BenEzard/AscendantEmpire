// SearchTable — a generic, reusable inline table with a search bar and click-to-sort column headers.
//
// It renders a filter textfield pinned to the top and a column-driven <table> below it. Typing filters the
// rows client-side (case-insensitive substring match against each row's search text, exactly like
// SearchDrawer). Clicking a sortable column header sorts the (already filtered) rows by that column, and
// clicking again toggles ascending/descending. Double-clicking a row activates it (`onActivate`).
//
// The component is deliberately generic over the row type `T`: callers describe the columns (a header, a
// per-row cell renderer, and an optional sort accessor) and supply small accessor functions for the row key
// and the searchable text — so the same table can list components, tags, or anything else. It mirrors the
// accessor API of SearchDrawer and the column-config API of EditableTable.

import { useMemo, useState, type ReactNode } from "react";

import Alert from "./Alert";
import Spinner from "./Spinner";

/** Describes one column: its header text, how to render each cell, and (optionally) how to sort by it. */
export interface SearchTableColumn<T> {
  /** Header text shown at the top of the column. */
  header: string;
  /** Renders the cell contents for `item`. */
  renderCell: (item: T) => ReactNode;
  /**
   * When present, the column header becomes clickable to sort by this value (ascending/descending). Omit
   * for a non-sortable column. Return a string (compared with localeCompare) or a number (compared
   * numerically).
   */
  getSortValue?: (item: T) => string | number;
  /** Optional class applied to both the header and the body cells (width/alignment hints). */
  className?: string;
}

export interface SearchTableProps<T> {
  /** The full, unfiltered set of rows to show (filtering and sorting happen inside the table). */
  items: T[];
  /** Column definitions, left to right. */
  columns: SearchTableColumn<T>[];
  /** While true, a spinner replaces the table (e.g. the initial fetch is in flight). */
  loading?: boolean;
  /** When set, an error alert replaces the table. */
  error?: string | null;
  /** Stable React key for a row. */
  getKey: (item: T) => string | number;
  /** The text a row is matched against when filtering. */
  getSearchText: (item: T) => string;
  /** Called when a row is double-clicked. */
  onActivate: (item: T) => void;
  /** Placeholder for the filter textfield. */
  placeholder?: string;
  /** Message shown in place of the table when there are no rows / no matches. */
  emptyMessage?: string;
}

/** The active sort: which column (by index into `columns`) and in which direction. `null` = unsorted. */
interface SortState {
  columnIndex: number;
  direction: "asc" | "desc";
}

export default function SearchTable<T>({
  items,
  columns,
  loading = false,
  error = null,
  getKey,
  getSearchText,
  onActivate,
  placeholder = "Search…",
  emptyMessage = "No matches.",
}: SearchTableProps<T>) {
  // The current filter text. Typing narrows the rows below.
  const [searchText, setSearchText] = useState("");

  // The active sort column + direction, or null to keep the incoming `items` order untouched.
  const [sort, setSort] = useState<SortState | null>(null);

  // Case-insensitive substring filter over each row's search text. Recomputed only when the inputs change
  // so typing through a large catalogue stays cheap. (Same logic as SearchDrawer.)
  const filteredItems = useMemo(() => {
    const needle = searchText.trim().toLowerCase();
    if (needle === "") {
      return items;
    }

    return items.filter((item) => getSearchText(item).toLowerCase().includes(needle));
  }, [items, searchText, getSearchText]);

  // Sort the already-filtered rows by the active column's sort value. When there is no active sort (or the
  // active column somehow has no sort accessor) we leave the filtered order as-is. Numbers compare
  // numerically; everything else compares as strings via localeCompare.
  const visibleItems = useMemo(() => {
    if (sort === null) {
      return filteredItems;
    }

    const column = columns[sort.columnIndex];
    if (column?.getSortValue === undefined) {
      return filteredItems;
    }

    const getSortValue = column.getSortValue;
    const directionFactor = sort.direction === "asc" ? 1 : -1;

    // Copy before sorting so we never mutate the memoised `filteredItems` array in place.
    return [...filteredItems].sort((first, second) => {
      const firstValue = getSortValue(first);
      const secondValue = getSortValue(second);

      let comparison: number;
      if (typeof firstValue === "number" && typeof secondValue === "number") {
        comparison = firstValue - secondValue;
      } else {
        comparison = String(firstValue).localeCompare(String(secondValue));
      }

      return comparison * directionFactor;
    });
  }, [filteredItems, sort, columns]);

  // Clicking a sortable header: activate it ascending, or (if already active) flip its direction.
  const handleHeaderClick = (columnIndex: number) => {
    setSort((current) => {
      if (current !== null && current.columnIndex === columnIndex) {
        return { columnIndex, direction: current.direction === "asc" ? "desc" : "asc" };
      }

      return { columnIndex, direction: "asc" };
    });
  };

  // The body content swaps between loading / error / empty / table states, mirroring SearchDrawer.
  let body: ReactNode;
  if (loading) {
    body = (
      <div className="flex items-center justify-center py-12">
        <Spinner />
      </div>
    );
  } else if (error != null) {
    body = <Alert variant="danger">{error}</Alert>;
  } else if (visibleItems.length === 0) {
    body = <p className="py-12 text-center text-sm text-slate-400">{emptyMessage}</p>;
  } else {
    body = (
      // Dark inline grid matching UsersTable so it sits correctly on the tool's dark content area.
      <div className="overflow-x-auto rounded-lg border border-slate-700">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="bg-slate-800 text-left text-slate-300">
              {columns.map((column, columnIndex) => {
                const sortable = column.getSortValue !== undefined;
                const isActiveSort = sort !== null && sort.columnIndex === columnIndex;

                // Non-sortable columns render a plain header; sortable ones render a clickable button that
                // shows a direction arrow (▲/▼) only while it is the active sort column.
                return (
                  <th key={column.header} className={`px-3 py-2 font-semibold ${column.className ?? ""}`}>
                    {sortable ? (
                      <button
                        type="button"
                        className="flex items-center gap-1 font-semibold hover:text-slate-100 focus:outline-none"
                        onClick={() => handleHeaderClick(columnIndex)}>
                        {column.header}
                        {/* Reserve the arrow slot only when active, so headers don't shift on first sort. */}
                        {isActiveSort && <span aria-hidden>{sort.direction === "asc" ? "▲" : "▼"}</span>}
                      </button>
                    ) : (
                      column.header
                    )}
                  </th>
                );
              })}
            </tr>
          </thead>

          <tbody>
            {visibleItems.map((item) => (
              <tr
                key={getKey(item)}
                className="cursor-pointer select-none border-t border-slate-700 text-slate-200 hover:bg-slate-800/60"
                // Double-click opens the row in its editor; single clicks are intentionally inert.
                onDoubleClick={() => onActivate(item)}>
                {columns.map((column) => (
                  <td key={column.header} className={`px-3 py-2 ${column.className ?? ""}`}>
                    {column.renderCell(item)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  return (
    // Full flex column: the search input stays pinned while the table below it fills the remaining space.
    <div className="flex flex-col gap-3">
      <input
        className="w-full rounded-md border border-slate-600 bg-slate-800 px-3 py-1.5 text-sm text-slate-200 outline-none placeholder:text-slate-500 focus:border-slate-400"
        value={searchText}
        placeholder={placeholder}
        autoFocus
        onChange={(event) => setSearchText(event.target.value)}
      />
      {body}
    </div>
  );
}
