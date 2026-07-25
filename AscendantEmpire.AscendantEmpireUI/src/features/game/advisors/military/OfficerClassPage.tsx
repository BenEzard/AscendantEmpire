import { forwardRef, useCallback, useEffect, useImperativeHandle, useState } from "react";

import { Button } from "@/components/ui";
import type { DomainType } from "@/features/unitdesign/types/UnitComponent";
import { getOfficerRanks, saveOfficerRanks, type OfficerRank } from "./militaryApi";
import type { MilitaryPageHandle } from "./militaryPage";
import OfficerClassTable, { type OfficerRow, defaultOfficerTitle, makeClearedOfficerRow, OFFICER_ROW_COUNT } from "./OfficerClassTable";

/**
 * The movement domains the officer-class ladders are configured for, in display order. These mirror the
 * domains seeded on the backend (see MilitaryOrganisationDefaults.SeededDomains) — NearEarthOrbit is
 * intentionally excluded for now.
 */
const OFFICER_DOMAINS: readonly { key: DomainType; label: string }[] = [
  { key: "Land", label: "Land" },
  { key: "Air", label: "Air" },
  { key: "Water", label: "Water" },
  { key: "BelowWater", label: "Below Water" },
];

/** A blank per-domain table (all rows cleared) — the starting point before the API load resolves. */
function makeEmptyRows(): OfficerRow[] {
  return Array.from({ length: OFFICER_ROW_COUNT }, makeClearedOfficerRow);
}

/** Turns the flat API rank list into the per-domain row tables the UI edits. */
function toTablesByDomain(ranks: OfficerRank[]): Record<DomainType, OfficerRow[]> {
  const tables = {} as Record<DomainType, OfficerRow[]>;

  for (const { key } of OFFICER_DOMAINS) {
    const rows = makeEmptyRows();
    // Place each rank at its (level - 1) slot; anything the API doesn't send stays a cleared row.
    for (const rank of ranks.filter((r) => r.movementDomain === key)) {
      const index = rank.level - 1;
      if (index >= 0 && index < rows.length) {
        rows[index] = {
          inUse: rank.inUse,
          title: rank.title,
          // A null ceiling (the top rank) shows as a blank field.
          maxSoldiers: rank.maxSoldiers ?? "",
          maxSpecialists: rank.maxAttachedSpecialists,
        };
      }
    }
    // Level 1 is always available, so it's always in use regardless of what came back (keeps the state in
    // step with the table's fixed-on level-1 checkbox).
    rows[0].inUse = true;
    tables[key] = rows;
  }

  return tables;
}

/** Flattens the per-domain tables back into the flat rank list the save endpoint expects. */
function toRankList(tables: Record<DomainType, OfficerRow[]>): OfficerRank[] {
  const ranks: OfficerRank[] = [];

  for (const { key } of OFFICER_DOMAINS) {
    tables[key].forEach((row, index) => {
      ranks.push({
        movementDomain: key,
        level: index + 1,
        title: row.title,
        // Blank ceiling → null (no cap); blank specialist cap → 0.
        maxSoldiers: row.maxSoldiers === "" ? null : row.maxSoldiers,
        maxAttachedSpecialists: row.maxSpecialists === "" ? 0 : row.maxSpecialists,
        inUse: row.inUse,
      });
    });
  }

  return ranks;
}

/**
 * Officer Class page of the Military Advisor dialog.
 *
 * <remarks>
 * <para>
 * A horizontal strip of domain buttons selects which ladder is shown. Each domain has its own rank table,
 * loaded from the backend. The page has no Save button of its own — it exposes a `save()` handle (see
 * {@link MilitaryPageHandle}) that the dialog's shared footer Save invokes, so all pages persist together.
 * </para>
 * </remarks>
 */
const OfficerClassPage = forwardRef<MilitaryPageHandle>(function OfficerClassPage(_props, ref) {
  // Land first — it's the domain every game starts with and matches the button order.
  const [selectedDomain, setSelectedDomain] = useState<DomainType>("Land");

  const [tablesByDomain, setTablesByDomain] = useState<Record<DomainType, OfficerRow[]>>(() => toTablesByDomain([]));

  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [savedNote, setSavedNote] = useState(false);

  // Load the player's rank ladders once on mount.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        setIsLoading(true);
        const ranks = await getOfficerRanks();
        if (!cancelled) {
          setTablesByDomain(toTablesByDomain(ranks));
          setError(null);
        }
      } catch {
        if (!cancelled) {
          setError("Could not load officer ranks.");
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const rows = tablesByDomain[selectedDomain];

  // Applies a single row's patch to the currently-selected domain's table.
  function handleChangeRow(rowIndex: number, patch: Partial<OfficerRow>) {
    setSavedNote(false);
    setTablesByDomain((previous) => {
      const nextRows = previous[selectedDomain].map((row, index) => (index === rowIndex ? { ...row, ...patch } : row));
      return { ...previous, [selectedDomain]: nextRows };
    });
  }

  // Handles an In Use toggle for the selected domain, including the clear/cascade rules:
  //  - Ticking a row marks it in use, restoring its default title if it had been cleared.
  //  - Unticking a row clears it and, because a hidden row hides everything below it, clears every row after
  //    it too. Rows above are left untouched.
  function handleToggleInUse(rowIndex: number, inUse: boolean) {
    setSavedNote(false);
    setTablesByDomain((previous) => {
      const nextRows = previous[selectedDomain].map((row, index) => {
        if (index < rowIndex) {
          return row;
        }
        if (index === rowIndex) {
          return inUse ? { ...row, inUse: true, title: row.title || defaultOfficerTitle(index) } : makeClearedOfficerRow();
        }
        return inUse ? row : makeClearedOfficerRow();
      });
      return { ...previous, [selectedDomain]: nextRows };
    });
  }

  // Persists every domain's ladder. Exposed to the dialog via the imperative handle below; the dialog's
  // footer Save calls this (alongside the other pages' saves) so all edits go up together. Errors are
  // surfaced here and re-thrown so the dialog can reflect the overall failure.
  const save = useCallback(async () => {
    try {
      setError(null);
      await saveOfficerRanks(toRankList(tablesByDomain));
      setSavedNote(true);
    } catch (err) {
      setError("Could not save officer ranks.");
      throw err;
    }
  }, [tablesByDomain]);

  useImperativeHandle(ref, () => ({ save }), [save]);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-slate-800">Officer Class</h2>
        {savedNote && <span className="text-sm text-green-600">Saved.</span>}
        {error && <span className="text-sm text-red-600">{error}</span>}
      </div>

      {/* Single-select domain strip. */}
      <div className="inline-flex items-center">
        <span className="pr-3 text-sm text-slate-600">Domain:</span>
        {OFFICER_DOMAINS.map((domain, index) => {
          const isSelected = domain.key === selectedDomain;
          const isFirst = index === 0;
          const isLast = index === OFFICER_DOMAINS.length - 1;
          // Round only the group's outer edges so the strip reads as a single pill.
          const cornerClass = isFirst ? "rounded-l-md rounded-r-none" : isLast ? "rounded-r-md rounded-l-none" : "rounded-none";
          // Overlap adjacent borders so the shared line doesn't double up.
          const joinClass = isFirst ? "" : "-ml-px";
          return (
            <Button
              key={domain.key}
              variant="outline-secondary"
              className={`w-24 !px-2 !py-1 whitespace-nowrap ${cornerClass} ${joinClass} ${isSelected ? "nav-item-selected" : ""}`.trim()}
              onClick={() => setSelectedDomain(domain.key)}>
              {domain.label}
            </Button>
          );
        })}
      </div>

      {isLoading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : (
        <OfficerClassTable rows={rows} onChangeRow={handleChangeRow} onToggleInUse={handleToggleInUse} />
      )}
    </div>
  );
});

export default OfficerClassPage;
