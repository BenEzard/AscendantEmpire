import { forwardRef, useCallback, useEffect, useImperativeHandle, useState } from "react";

import { SearchableComboBox } from "@/components/ui";
import { DOMAIN_TYPE_LABELS } from "@/components/game/domainLabels";
import {
  getOrganisationStructure,
  getOrganisationTags,
  saveOrganisationStructure,
  type OrganisationStructure,
  type OrganisationTag,
  type OrganisationTier,
} from "./militaryApi";
import type { MilitaryPageHandle } from "./militaryPage";
import MilitaryOrganisationTable, {
  type MilitaryOrganisationRow,
  defaultTierName,
  makeClearedMilitaryOrganisationRow,
  MILITARY_ORGANISATION_ROW_COUNT,
} from "./MilitaryOrganisationTable";

/** A blank tier table (all rows cleared) — the starting point before a structure is loaded. */
function makeEmptyRows(): MilitaryOrganisationRow[] {
  return Array.from({ length: MILITARY_ORGANISATION_ROW_COUNT }, makeClearedMilitaryOrganisationRow);
}

/** Turns a loaded structure's tiers into the editable rows (in-use prefix = the tiers present). */
function toRows(structure: OrganisationStructure): MilitaryOrganisationRow[] {
  const rows = makeEmptyRows();
  for (const tier of structure.tiers) {
    const index = tier.tierIndex - 1;
    if (index >= 0 && index < rows.length) {
      rows[index] = { inUse: true, name: tier.name, multiplier: tier.multiplier };
    }
  }
  return rows;
}

/** Flattens the in-use tier rows back into the tier list the save endpoint expects. */
function toTiers(rows: MilitaryOrganisationRow[]): OrganisationTier[] {
  return rows
    .filter((row) => row.inUse)
    .map((row, index) => ({
      tierIndex: index + 1,
      name: row.name,
      // A blank multiplier saves as 1 (the minimum the backend accepts).
      multiplier: row.multiplier === "" ? 1 : row.multiplier,
    }));
}

/**
 * Military Organisation page of the Military Advisor dialog.
 *
 * <remarks>
 * <para>
 * A searchable combo box lists the MilitaryOrganisationalGroup tags; picking one loads that structure (or the
 * code default, if the player hasn't customised it yet) into the tier table below. The page has no Save button
 * of its own — it exposes a `save()` handle (see {@link MilitaryPageHandle}) that the dialog's shared footer
 * Save invokes, so all pages persist together.
 * </para>
 * </remarks>
 */
const MilitaryOrganisationPage = forwardRef<MilitaryPageHandle>(function MilitaryOrganisationPage(_props, ref) {
  const [tags, setTags] = useState<OrganisationTag[]>([]);
  const [selectedTag, setSelectedTag] = useState<OrganisationTag | null>(null);
  const [rows, setRows] = useState<MilitaryOrganisationRow[]>(makeEmptyRows);
  const [isDefault, setIsDefault] = useState(false);
  // Tier cap and per-tier rank titles, derived server-side from the tag's domain rank ladder.
  const [maxTiers, setMaxTiers] = useState<number>(MILITARY_ORGANISATION_ROW_COUNT);
  const [rankTitles, setRankTitles] = useState<string[]>([]);

  const [isLoadingTags, setIsLoadingTags] = useState(true);
  const [isLoadingStructure, setIsLoadingStructure] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedNote, setSavedNote] = useState(false);

  // Load the organisation tags (the combo box feed) once on mount.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        setIsLoadingTags(true);
        const loaded = await getOrganisationTags();
        if (!cancelled) {
          setTags(loaded);
          setError(null);
        }
      } catch {
        if (!cancelled) {
          setError("Could not load organisation tags.");
        }
      } finally {
        if (!cancelled) {
          setIsLoadingTags(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  async function handleSelectTag(tag: OrganisationTag) {
    setSelectedTag(tag);
    setSavedNote(false);

    // Unled tags have no per-player structure at all — their sizing is authored on the tag (catalogue data)
    // and shown read-only below, so there is nothing to load.
    if (tag.leadershipModel === "Unled") {
      setRows(makeEmptyRows());
      setIsDefault(false);
      setError(null);
      return;
    }

    try {
      setIsLoadingStructure(true);
      setError(null);
      const structure = await getOrganisationStructure(tag.id);
      setRows(toRows(structure));
      setIsDefault(structure.isDefault);
      setMaxTiers(structure.maxTiers);
      setRankTitles(structure.rankTitles);
    } catch {
      setError("Could not load the organisation structure.");
    } finally {
      setIsLoadingStructure(false);
    }
  }

  function handleChangeRow(rowIndex: number, patch: Partial<MilitaryOrganisationRow>) {
    setSavedNote(false);
    setRows((previous) => previous.map((row, index) => (index === rowIndex ? { ...row, ...patch } : row)));
  }

  // In Use toggle with the same clear/cascade rules as the officer table — turning a tier off clears it and
  // every tier below it; turning one on restores its default name. Tier 1 can't be turned off (handled by the
  // table disabling its checkbox), so this only ever runs for tiers 2+.
  function handleToggleInUse(rowIndex: number, inUse: boolean) {
    setSavedNote(false);
    setRows((previous) =>
      previous.map((row, index) => {
        if (index < rowIndex) {
          return row;
        }
        if (index === rowIndex) {
          return inUse ? { ...row, inUse: true, name: row.name || defaultTierName(index) } : makeClearedMilitaryOrganisationRow();
        }
        return inUse ? row : makeClearedMilitaryOrganisationRow();
      }),
    );
  }

  // Persists the currently-selected structure. Exposed via the imperative handle so the dialog's footer Save
  // saves this page alongside the others. A no-op (resolves) when no tag is selected, so the shared Save never
  // fails just because this page has nothing loaded. Errors are surfaced here and re-thrown.
  const save = useCallback(async () => {
    // Nothing to persist with no selection, and unled tags are read-only here (no per-player structure).
    if (selectedTag === null || selectedTag.leadershipModel === "Unled") {
      return;
    }
    try {
      setError(null);
      await saveOrganisationStructure(selectedTag.id, toTiers(rows));
      setSavedNote(true);
      setIsDefault(false);
    } catch (err) {
      setError("Could not save the organisation structure.");
      throw err;
    }
  }, [selectedTag, rows]);

  useImperativeHandle(ref, () => ({ save }), [save]);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-slate-800">Military Organisation</h2>
        {savedNote && <span className="text-sm text-green-600">Saved.</span>}
        {error && <span className="text-sm text-red-600">{error}</span>}
      </div>

      {/* Searchable combo box over the MilitaryOrganisationalGroup tags (light-field styling for the dialog). */}
      <div className="flex items-center gap-3">
        <span className="text-sm text-slate-600">Organisation:</span>
        <SearchableComboBox<OrganisationTag>
          items={tags}
          getKey={(tag) => tag.id}
          getPrimaryText={(tag) => tag.displayName}
          getSecondaryText={(tag) => tag.code}
          getSearchText={(tag) => `${tag.displayName} ${tag.code}`}
          placeholder={isLoadingTags ? "Loading…" : "Search organisation groups…"}
          loading={isLoadingTags}
          disabled={isLoadingTags}
          inputClassName="w-64 rounded-md border border-slate-300 px-2 py-1 text-sm text-slate-800 placeholder-slate-400 outline-none focus-visible:ring-2 focus-visible:ring-slate-300"
          onSelect={handleSelectTag}
        />
        {selectedTag && (
          <span className="text-sm font-medium text-slate-700">
            {selectedTag.displayName}
            {isDefault && <span className="ml-2 text-xs font-normal text-amber-600">(default — not yet saved)</span>}
          </span>
        )}
      </div>

      {selectedTag === null ? (
        <p className="text-sm text-slate-500">Choose an organisation group above to edit its structure.</p>
      ) : selectedTag.leadershipModel === "Unled" ? (
        // Unled organisations have no officers and no tier structure; their sizing is authored on the tag
        // itself (base quantum / default / max) and is read-only here. See the design doc's "Leadership
        // model" section.
        <div className="flex flex-col gap-2">
          <p className="text-xs text-slate-500">
            {selectedTag.movementDomain ? `Domain: ${DOMAIN_TYPE_LABELS[selectedTag.movementDomain]} · ` : ""}
            Unled — this organisation has no officers and no tier structure. Units are any whole multiple of
            the base size, up to the maximum; sizes are fixed by the game and cannot be edited.
          </p>
          <div className="flex gap-6 rounded-md border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
            <span>
              Base (quantum): <span className="font-semibold">{selectedTag.unledBaseSize ?? "—"}</span>
            </span>
            <span>
              Default: <span className="font-semibold">{selectedTag.unledDefaultSize ?? "—"}</span>
            </span>
            <span>
              Maximum: <span className="font-semibold">{selectedTag.unledMaxSize ?? "—"}</span>
            </span>
          </div>
        </div>
      ) : isLoadingStructure ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : (
        <>
          <p className="text-xs text-slate-500">
            {selectedTag.movementDomain
              ? `Domain: ${DOMAIN_TYPE_LABELS[selectedTag.movementDomain]} · up to ${maxTiers} tier${maxTiers === 1 ? "" : "s"} (from this domain's officer ranks).`
              : "This organisation group has no movement domain set, so its tiers aren't capped by a rank ladder."}
          </p>
          <MilitaryOrganisationTable
            rows={rows}
            maxTiers={maxTiers}
            rankTitles={rankTitles}
            movementDomain={selectedTag.movementDomain}
            onChangeRow={handleChangeRow}
            onToggleInUse={handleToggleInUse}
          />
        </>
      )}
    </div>
  );
});

export default MilitaryOrganisationPage;
