// EditableTable — a small, generic table for editing a growable list of rows.
//
// Callers describe the columns (a header plus a per-row cell renderer) and supply the row data; the
// table renders one row per item with a trailing "−" button to remove that row, and a single "+"
// button below the table to append a new one. It is deliberately model-agnostic: it knows nothing
// about what a row contains — the cell renderers (and the add/remove callbacks) own all of that.

import type { ReactNode } from "react";
import { HiTrash } from "react-icons/hi2";

import Button from "./Button";
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
}

export default function EditableTable<TRow>({
  columns,
  rows,
  onAddRow,
  onRemoveRow,
  addDisabled,
  addLabel,
  emptyHint,
}: EditableTableProps<TRow>) {
  return (
    <div className="rounded-md border border-slate-200">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 text-left text-xs text-slate-500">
            {columns.map((column) => (
              <th key={column.header} className={`px-2 py-1 font-medium ${column.className ?? ""}`}>
                {column.header}
              </th>
            ))}
            {/* The "+" add button lives in the trailing header, directly above the per-row trash buttons. */}
            <th className="w-8 px-2 py-1 text-center">
              <Tooltip content={addLabel ? `Add ${addLabel}` : "Add row"}>
                <Button
                  variant="outline-primary"
                  className="px-1.5 py-1 text-xs leading-none"
                  disabled={addDisabled}
                  aria-label={addLabel ? `Add ${addLabel}` : "Add row"}
                  onClick={onAddRow}
                >
                  +
                </Button>
              </Tooltip>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              {/* Span every column (data columns + the add/remove column) for the empty message. */}
              <td colSpan={columns.length + 1} className="px-2 py-3 text-center text-xs text-slate-400">
                {emptyHint ?? "No rows yet."}
              </td>
            </tr>
          ) : (
            rows.map((row, rowIndex) => (
              <tr key={rowIndex} className="border-b border-slate-100 last:border-b-0">
                {columns.map((column) => (
                  <td key={column.header} className={`px-2 py-0 align-middle ${column.className ?? ""}`}>
                    {column.renderCell(row, rowIndex)}
                  </td>
                ))}
                <td className="px-2 py-0 text-center align-middle">
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
