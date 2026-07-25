/**
 * A count field that may be temporarily empty while the user is editing.
 *
 * <remarks>
 * <para>
 * Number inputs are allowed to go blank (empty string) rather than snapping back to 0 on every keystroke.
 * For the soldier ceiling a blank also carries meaning — it is the "no ceiling" state of the top rank — so
 * it round-trips to null on save.
 * </para>
 * </remarks>
 */
export type CountValue = number | "";

/**
 * One row of the officer-class (rank) table.
 *
 * <remarks>
 * <para>
 * Officer ranks are now kept separate from organisation structure: a rank carries only its title and the
 * limits it imposes (soldier ceiling, specialist cap). The tier sizes live on the Organisation page. `Level`
 * is derived from the row position (row index + 1), so it isn't stored here.
 * </para>
 * </remarks>
 */
export interface OfficerRow {
  /** Column 1 — whether this level is in use. */
  inUse: boolean;
  /** Column 3 — the rank title (e.g. "General"). Always a string (never null). */
  title: string;
  /** Column 4 — the hard soldier ceiling. Blank means "no ceiling" (the top rank); saved as null. */
  maxSoldiers: CountValue;
  /** Column 5 — the maximum number of attached specialists. */
  maxSpecialists: CountValue;
}

/** The default rank titles for the Land ladder, low to high. Doubles as the row count. */
const DEFAULT_OFFICER_TITLES = ["Lieutenant", "Captain", "Major", "Colonel", "Major General", "General"] as const;

/** The fixed number of rows every officer-class table has (one per rank level). */
export const OFFICER_ROW_COUNT = DEFAULT_OFFICER_TITLES.length;

/** A cleared, not-in-use row — used when a row is unticked and for the rows the untick cascades into. */
export function makeClearedOfficerRow(): OfficerRow {
  return { inUse: false, title: "", maxSoldiers: "", maxSpecialists: "" };
}

/** The default title for a given row/level, used to restore it when a row is re-ticked. */
export function defaultOfficerTitle(rowIndex: number): string {
  return DEFAULT_OFFICER_TITLES[rowIndex] ?? "";
}

interface OfficerClassTableProps {
  /** The rows to render (one per rank level). */
  rows: OfficerRow[];
  /** Called with a row index and a partial patch when an editable cell in that row changes. */
  onChangeRow: (rowIndex: number, patch: Partial<OfficerRow>) => void;
  /** Called when a row's In Use checkbox is toggled; the page owns the clear/cascade behaviour. */
  onToggleInUse: (rowIndex: number, inUse: boolean) => void;
}

/** Shared class for the editable text/number inputs, matching the app's light form fields. */
const inputClass = "w-full rounded border border-slate-300 px-2 py-0.5 text-sm";

/** Fixed-width class for the Rank title field so it fills its column without stretching the whole row. */
const rankInputClass = "w-52 rounded border border-slate-300 px-2 py-0.5 text-sm";

/** Shared class for read-only cell values, rendered as muted plain text. */
const readonlyClass = "text-sm text-slate-500";

/** Formats a count with thousands separators for display (a blank stays blank). */
function formatGrouped(value: CountValue): string {
  return value === "" ? "" : value.toLocaleString("en-US");
}

/**
 * Parses a possibly comma-grouped number field. Any non-digit characters (the separators the user sees, plus
 * stray input) are stripped first, so "10,000" reads back as 10000 and a cleared field stays blank.
 */
function parseGrouped(value: string): CountValue {
  const digits = value.replace(/[^0-9]/g, "");
  return digits === "" ? "" : parseInt(digits, 10);
}

/**
 * The editable officer-class (rank) table shown under each domain.
 *
 * <remarks>
 * <para>
 * Columns: In Use (checkbox), Level (read-only 1..6), Rank (editable title), Max Soldiers (the hard soldier
 * ceiling; blank = no ceiling, for the top rank), and Max Specialists (the specialist attachment cap).
 * </para>
 * </remarks>
 */
export default function OfficerClassTable({ rows, onChangeRow, onToggleInUse }: OfficerClassTableProps) {
  // The first not-in-use row. In-use rows form a contiguous prefix (unticking cascades down), so this is the
  // single "next" row we still show — as a checkbox only — to let the user turn that level on. -1 means every
  // row is in use, so nothing is hidden.
  const firstOffIndex = rows.findIndex((row) => !row.inUse);

  return (
    <div className="rounded-md border border-slate-200">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 text-left text-xs text-slate-500">
            <th className="w-20 whitespace-nowrap px-2 py-0.5 text-center font-medium">In Use</th>
            <th className="w-20 whitespace-nowrap px-2 py-0.5 text-center font-medium">Level</th>
            <th className="w-56 whitespace-nowrap px-2 py-0.5 font-medium">Rank</th>
            <th className="w-48 whitespace-nowrap px-2 py-0.5 text-center font-medium">Max Soldiers</th>
            <th className="w-48 whitespace-nowrap px-2 py-0.5 text-center font-medium">Max Specialists</th>
            {/* Spacer column: soaks up all the leftover width so the real columns stay packed together on the
                left with no gaps between them, and the empty space falls on the right. */}
            <th className="w-full px-1 py-0.5" />
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => {
            // Rows below the first not-in-use row are hidden entirely — only that next row's checkbox is
            // offered (to turn the level back on).
            if (firstOffIndex !== -1 && rowIndex > firstOffIndex) {
              return null;
            }
            // Level 1 is always available (see the design doc), so its row is always in use and its checkbox
            // is fixed on. Everything below cascades as before.
            const isFirstRow = rowIndex === 0;
            const isInUse = isFirstRow || row.inUse;
            // Fixed row height so the row doesn't jump when it collapses to just the In Use checkbox.
            return (
              <tr key={rowIndex} className="h-8 border-b border-slate-100 last:border-b-0">
                {/* Column 1 — In Use checkbox. Always shown; level 1 is fixed on and disabled. */}
                <td className="px-1 py-0.5 text-center align-middle">
                  <input
                    type="checkbox"
                    checked={isInUse}
                    disabled={isFirstRow}
                    aria-label={`In use, level ${rowIndex + 1}`}
                    onChange={(event) => onToggleInUse(rowIndex, event.target.checked)}
                  />
                </td>

                {!isInUse ? (
                  // Not in use — collapse to just the checkbox by rendering empty cells.
                  <>
                    <td className="px-1 py-0.5" />
                    <td className="px-1 py-0.5" />
                    <td className="px-1 py-0.5" />
                    <td className="px-1 py-0.5" />
                  </>
                ) : (
                  <>
                    {/* Column 2 — Level, read-only, derived from the row position. */}
                    <td className="px-1 py-0.5 text-center align-middle">
                      <span className={readonlyClass}>{rowIndex + 1}</span>
                    </td>

                    {/* Column 3 — Rank title, editable text. */}
                    <td className="px-1 py-0.5 align-middle">
                      <input
                        type="text"
                        className={rankInputClass}
                        value={row.title}
                        placeholder="Rank title"
                        aria-label={`Rank title, level ${rowIndex + 1}`}
                        onChange={(event) => onChangeRow(rowIndex, { title: event.target.value })}
                      />
                    </td>

                    {/* Column 4 — Max Soldiers (hard ceiling), centred. Blank means "no ceiling" — the expected
                        state for the top rank, whose reach is governed by its max number of Units. */}
                    <td className="px-1 py-0.5 align-middle text-center">
                      {/* A text field (not number) so the value can render with thousands separators —
                          e.g. "10,000". The commas are stripped on the way back in (see parseGrouped). */}
                      <input
                        type="text"
                        inputMode="numeric"
                        className={`${inputClass} text-center`}
                        value={formatGrouped(row.maxSoldiers)}
                        placeholder="—"
                        aria-label={`Max soldiers, level ${rowIndex + 1}`}
                        onChange={(event) => onChangeRow(rowIndex, { maxSoldiers: parseGrouped(event.target.value) })}
                      />
                    </td>

                    {/* Column 5 — Max attached Specialists, centred. Read-only: this cap is set by the game,
                        not the player, so it's shown as plain text rather than an editable field. */}
                    <td className="px-1 py-0.5 align-middle text-center">
                      <span className={readonlyClass}>{row.maxSpecialists === "" ? "—" : row.maxSpecialists}</span>
                    </td>
                  </>
                )}

                {/* Trailing spacer cell (pairs with the spacer header) so every row's dividers span the full
                    width while the real columns stay left-packed. */}
                <td className="w-full" />
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
