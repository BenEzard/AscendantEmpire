// EditableTable — a small, generic table for editing a growable list of rows.
//
// Callers describe the columns (a header plus a per-row cell renderer) and supply the row data; the
// table renders one row per item with a trailing "−" button to remove that row, and a single "+"
// button below the table to append a new one. It is deliberately model-agnostic: it knows nothing
// about what a row contains — the cell renderers (and the add/remove callbacks) own all of that.

import type { ReactNode } from "react";
import { HiTrash } from "react-icons/hi2";

import Tooltip from "./Tooltip";

/** Describes one column: its header text and how to render the cell for a given row. */
export interface EditableTableColumn<TRow> {
  /** Header text shown at the top of the column. */
  header: string;
  /** Renders the cell contents for `row` (e.g. an input or a select). */
  renderCell: (row: TRow, rowIndex: number) => ReactNode;
  /** Optional class applied to both the header and the body cells (width/alignment hints). */
  className?: string;
}

export interface EditableTableProps<TRow> {
  /** Column definitions, left to right. The remove-button column is appended automatically. */
  columns: EditableTableColumn<TRow>[];
  /** The rows to render, in order. */
  rows: TRow[];
  /** Called to append a new row when the user clicks "+". */
  onAddRow: () => void;
  /** Called with the row index to remove when the user clicks that row's "−". */
  onRemoveRow: (rowIndex: number) => void;
  /** When true, the "+" button is disabled (e.g. every available key is already used). */
  addDisabled?: boolean;
  /** Name of the thing being added; used in the "+" button tooltip (e.g. "Damage Type" -> "Add Damage Type"). */
  addLabel?: string;
  /** Message shown in place of the rows when there are none yet. */
  emptyHint?: string;
  /**
   * A stable React key for a row, for callers that re-order `rows` while the table is on screen.
   * <para>
   * Without it the rows are keyed by position, so a row moving from one position to another leaves any state held
   * *inside* a cell (a CodeField's manually-edited flag, say) behind with the old position — it stays with the slot
   * rather than following the row. Return something that identifies the row itself and the state moves with it.
   * </para>
   * <para>
   * Callers whose row order never changes can leave this unset; keying by position is then correct and is the default.
   * </para>
   */
  rowKey?: (row: TRow, rowIndex: number) => string | number;
}

export default function EditableTable<TRow>({
  columns,
  rows,
  onAddRow,
  onRemoveRow,
  addDisabled,
  addLabel,
  emptyHint,
  rowKey,
}: EditableTableProps<TRow>) {
  return (
    <div className="rounded-md border border-slate-200">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 text-left text-xs text-slate-500">
            {/* Cell padding is deliberately tight (px-1, so 8px between neighbouring cells) */}
            {columns.map((column) => (
              <th key={column.header} className={`px-1 py-1 font-medium ${column.className ?? ""}`}>
                {column.header}
              </th>
            ))}
            {/* The "+" add button lives in the trailing header, directly above the per-row trash buttons. */}
            <th className="w-8 px-1 py-1 text-center">
              <Tooltip content={addLabel ? `Add ${addLabel}` : "Add row"}>
                {/* Plain icon-only control (no border/padding), matching the per-row trash buttons. */}
                <button
                  type="button"
                  className="text-lg leading-none text-[var(--bs-primary)] hover:opacity-70 focus:outline-none disabled:opacity-40 disabled:cursor-not-allowed"
                  disabled={addDisabled}
                  aria-label={addLabel ? `Add ${addLabel}` : "Add row"}
                  onClick={onAddRow}
                >
                  +
                </button>
              </Tooltip>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              {/* Span every column (data columns + the add/remove column) for the empty message. */}
              <td colSpan={columns.length + 1} className="px-1 py-3 text-center text-xs text-slate-400">
                {emptyHint ?? "No rows yet."}
              </td>
            </tr>
          ) : (
            rows.map((row, rowIndex) => (
              <tr key={rowKey ? rowKey(row, rowIndex) : rowIndex} className="border-b border-slate-100 last:border-b-0">
                {columns.map((column) => (
                  <td key={column.header} className={`px-1 py-0 align-middle ${column.className ?? ""}`}>
                    {column.renderCell(row, rowIndex)}
                  </td>
                ))}
                <td className="px-1 py-0 text-center align-middle">
                  <Tooltip content="Remove row">
                    {/* A plain icon-only control (no border/padding) so it reads as a subtle action, not a button. */}
                    <button
                      type="button"
                      className="text-slate-400 hover:text-[var(--bs-danger)] focus:outline-none"
                      aria-label="Remove row"
                      onClick={() => onRemoveRow(rowIndex)}
                    >
                      <HiTrash size={14} aria-hidden />
                    </button>
                  </Tooltip>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
