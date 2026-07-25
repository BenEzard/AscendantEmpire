/**
 * A count field that may be temporarily empty while the user is editing (blank is coerced to 0 only in the
 * size calculation, so the maths never sees a NaN).
 */
export type CountValue = number | "";

/**
 * One tier of an organisation hierarchy.
 *
 * <remarks>
 * <para>
 * Organisation hierarchy is kept separate from officer rank. A tier carries a customisable name and a single
 * multiplier: for tier 1 it is the base number of soldiers; for tiers above it is the fan-out (how many of the
 * tier below group into one). `Level` (the positional rank) and the absolute soldier size are both derived, so
 * neither is stored here.
 * </para>
 * </remarks>
 */
export interface MilitaryOrganisationRow {
  /** Column 1 — whether this tier is in use. */
  inUse: boolean;
  /** Column 4 — the (cosmetic, customisable) tier name. Always a string (never null). */
  name: string;
  /** Column 5 — tier 1: base soldiers; tiers above: fan-out of the tier below. May be blank while editing. */
  multiplier: CountValue;
}

/** The default tier names, smallest to largest. Doubles as the maximum tier count (5). */
const DEFAULT_TIER_NAMES = ["Company", "Platoon", "Battalion", "Regiment", "Division"] as const;

/** The maximum number of tiers a hierarchy may have (the absolute ceiling; a domain may cap it lower). */
export const MILITARY_ORGANISATION_ROW_COUNT = DEFAULT_TIER_NAMES.length;

/** The default name for a given tier, used to restore it when a row is re-ticked. */
export function defaultTierName(rowIndex: number): string {
  return DEFAULT_TIER_NAMES[rowIndex] ?? "";
}

/** A cleared, not-in-use tier — used when a row is unticked and for the rows the untick cascades into. */
export function makeClearedMilitaryOrganisationRow(): MilitaryOrganisationRow {
  return { inUse: false, name: "", multiplier: "" };
}

interface MilitaryOrganisationTableProps {
  /** The rows to render (one per tier). */
  rows: MilitaryOrganisationRow[];
  /** The maximum number of tiers permitted, derived from the tag's domain rank ladder. */
  maxTiers: number;
  /** In-use rank titles for the domain, in level order (index 0 = level 1); empty when the tag has no domain. */
  rankTitles: string[];
  /** Called with a row index and a partial patch when an editable cell in that row changes. */
  onChangeRow: (rowIndex: number, patch: Partial<MilitaryOrganisationRow>) => void;
  /** Called when a row's In Use checkbox is toggled; the page owns the clear/cascade behaviour. */
  onToggleInUse: (rowIndex: number, inUse: boolean) => void;
}

/** Shared class for the editable text/number inputs, matching the app's light form fields. */
const inputClass = "w-full rounded border border-slate-300 px-2 py-0.5 text-sm";

/** Shared class for read-only cell values, rendered as muted plain text. */
const readonlyClass = "text-sm text-slate-500";

/** Coerces a possibly-empty count field to a number, treating blank as 0 for calculation purposes. */
function toCount(value: CountValue): number {
  return value === "" ? 0 : value;
}

/**
 * The compounded soldier size of every tier: tier 1 is its own multiplier (the base), each tier below is its
 * multiplier × the tier above's size (e.g. base 10, fan-out 5 → 50).
 */
export function computeSizes(rows: MilitaryOrganisationRow[]): number[] {
  const sizes: number[] = [];
  for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
    if (rowIndex === 0) {
      sizes.push(toCount(rows[0].multiplier));
    } else {
      sizes.push(toCount(rows[rowIndex].multiplier) * sizes[rowIndex - 1]);
    }
  }
  return sizes;
}

/** Parses a number input's text into a count, leaving a blank field empty so it can be cleared while typing. */
function parseCount(value: string): CountValue {
  if (value === "") {
    return "";
  }
  const parsed = parseInt(value, 10);
  return Number.isNaN(parsed) ? "" : parsed;
}

/**
 * The editable organisation-hierarchy table for a single tag.
 *
 * <remarks>
 * <para>
 * Columns: In Use (checkbox), Tier (read-only 1..N — the positional rank level), Rank (the domain's rank title
 * for that level), Tier Name (editable), Base / Fan-out (tier 1 base soldiers, tiers above the sub-formation
 * count), and Soldiers (a read-only compounding calculation). The number of tiers that can be turned on is
 * capped at <c>maxTiers</c> (from the domain's rank ladder).
 * </para>
 * </remarks>
 */
export default function MilitaryOrganisationTable({ rows, maxTiers, rankTitles, onChangeRow, onToggleInUse }: MilitaryOrganisationTableProps) {
  const sizes = computeSizes(rows);

  // The first not-in-use tier. In-use tiers form a contiguous prefix (unticking cascades down), so this is the
  // single "next" row we still show — as a checkbox only — to let the user add that tier. -1 means all in use.
  const firstOffIndex = rows.findIndex((row) => !row.inUse);

  return (
    <div className="rounded-md border border-slate-200">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 text-left text-xs text-slate-500">
            <th className="w-16 whitespace-nowrap px-2 py-0.5 text-center font-medium">In Use</th>
            <th className="w-14 whitespace-nowrap px-2 py-0.5 text-center font-medium">Tier</th>
            <th className="w-40 whitespace-nowrap px-2 py-0.5 font-medium">Rank</th>
            <th className="w-44 whitespace-nowrap px-2 py-0.5 font-medium">Tier Name</th>
            <th className="w-28 whitespace-nowrap px-2 py-0.5 font-medium">Base / Fan-out</th>
            <th className="w-28 whitespace-nowrap px-2 py-0.5 font-medium">Soldiers</th>
            {/* Spacer soaks up leftover width so the real columns stay packed on the left. */}
            <th className="w-full px-2 py-0.5" />
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => {
            // Rows below the first not-in-use tier are hidden — only that next row's checkbox is offered.
            if (firstOffIndex !== -1 && rowIndex > firstOffIndex) {
              return null;
            }
            // Also hide the "add next tier" checkbox once the in-use count has reached the domain's cap
            // (firstOffIndex equals the number of in-use rows).
            if (firstOffIndex !== -1 && rowIndex === firstOffIndex && firstOffIndex >= maxTiers) {
              return null;
            }
            const isFirstRow = rowIndex === 0;
            // The domain's rank title for this tier's level (tier k ↔ rank level k). Falls back to a dash when
            // the tag has no domain (no titles supplied).
            const rankTitle = rankTitles[rowIndex] ?? "—";
            return (
              <tr key={rowIndex} className="h-8 border-b border-slate-100 last:border-b-0">
                {/* Column 1 — In Use checkbox. Always shown, even when the rest of the row is hidden. */}
                <td className="px-2 py-0.5 text-center align-middle">
                  <input
                    type="checkbox"
                    checked={row.inUse}
                    // Tier 1 is mandatory — a hierarchy always has at least one tier — so its checkbox is fixed on.
                    disabled={isFirstRow}
                    aria-label={`In use, tier ${rowIndex + 1}`}
                    onChange={(event) => onToggleInUse(rowIndex, event.target.checked)}
                  />
                </td>

                {!row.inUse ? (
                  <>
                    <td className="px-2 py-0.5" />
                    <td className="px-2 py-0.5" />
                    <td className="px-2 py-0.5" />
                    <td className="px-2 py-0.5" />
                    <td className="px-2 py-0.5" />
                  </>
                ) : (
                  <>
                    {/* Column 2 — Tier (read-only). Also the positional officer rank level. */}
                    <td className="px-2 py-0.5 text-center align-middle">
                      <span className={readonlyClass}>{rowIndex + 1}</span>
                    </td>

                    {/* Column 3 — Rank title for this tier's level (read-only, from the domain's ladder). */}
                    <td className="px-2 py-0.5 align-middle">
                      <span className={readonlyClass}>{rankTitle}</span>
                    </td>

                    {/* Column 4 — Tier Name, editable. */}
                    <td className="px-2 py-0.5 align-middle">
                      <input
                        type="text"
                        className={inputClass}
                        value={row.name}
                        placeholder="Tier name"
                        aria-label={`Tier name, tier ${rowIndex + 1}`}
                        onChange={(event) => onChangeRow(rowIndex, { name: event.target.value })}
                      />
                    </td>

                    {/* Column 5 — Base soldiers (tier 1) or fan-out (tiers above). */}
                    <td className="px-2 py-0.5 align-middle">
                      <input
                        type="number"
                        min={1}
                        className={inputClass}
                        value={row.multiplier}
                        placeholder={isFirstRow ? "Base" : "×"}
                        aria-label={isFirstRow ? "Base soldiers, tier 1" : `Fan-out, tier ${rowIndex + 1}`}
                        onChange={(event) => onChangeRow(rowIndex, { multiplier: parseCount(event.target.value) })}
                      />
                    </td>

                    {/* Column 6 — Soldiers, a read-only compounding calculation. */}
                    <td className="px-2 py-0.5 align-middle">
                      <span className={readonlyClass}>{sizes[rowIndex].toLocaleString("en-US")}</span>
                    </td>
                  </>
                )}

                {/* Trailing spacer cell so every row's dividers span the full width. */}
                <td className="w-full" />
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
