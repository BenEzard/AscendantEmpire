// SupplySetManagerDialog — the supply set catalogue editor. A set names the Assets one of a force's needs may be met with, and a unit component then
// requires a quantity of a set rather than of one Asset, so an army can march on whatever the map actually grows.
//
// Self-contained like the other manager dialogs on this tool: it loads the catalogue and the Asset list itself, holds an editable draft of the selected
// set, and nothing reaches the server until Save. The picker names which saved set is being edited; New starts one the catalogue does not hold yet.

import { useEffect, useState } from "react";

import ToolbarExclusiveButtons, { type ToolbarExclusiveItem } from "@/components/ui/toolbar/ToolbarExclusiveButtons";
import { Button, CodeField, ConfirmDialog, FieldRow, Modal, inputClass } from "@/components/ui";

import SupplySetAssetWells, { type SupplySetMember } from "./SupplySetAssetWells";
import { supplyTypeIcons } from "./supplyTypeIcons";

import {
  createSupplySet,
  getAssetOptions,
  getSupplySets,
  updateSupplySet,
  type AssetOption,
  type SupplySetView,
  type SupplyType,
  type UpsertSupplySet,
  type UpsertSupplySetAsset,
} from "../api/supplySetApi";

const SUPPLY_TYPES: SupplyType[] = ["Food", "Fuel", "Repair"];

/** What a quantity of each kind of set is measured against, shown beside the amount column. */
const SUPPLY_TYPE_BASIS: Record<SupplyType, string> = {
  Food: "per troop",
  Fuel: "per movement point",
  Repair: "per damage point",
};

/** The draft value standing for "author a new set" rather than an existing set's id. */
const NEW_SET = "new";

interface SupplySetDraft {
  id: number | null;
  code: string;
  displayName: string;
  supplyType: SupplyType;
  members: SupplySetMember[];
}

interface SupplySetManagerDialogProps {
  open: boolean;
  onCancel: () => void;
  onSaveSuccess: (message: string) => void;
}

function emptyDraft(): SupplySetDraft {
  return { id: null, code: "", displayName: "", supplyType: "Food", members: [] };
}

function toDraft(set: SupplySetView): SupplySetDraft {
  return {
    id: set.id,
    code: set.code,
    displayName: set.displayName,
    supplyType: set.supplyType,
    members: set.assets.map((asset) => ({
      assetCode: asset.assetCode,
      assetDisplayName: asset.assetDisplayName,
      amountPerUnitRaw: asset.amountPerUnit,
      selectionWeight: asset.selectionWeight,
    })),
  };
}

function toPayload(draft: SupplySetDraft): UpsertSupplySet {
  const assets: UpsertSupplySetAsset[] = draft.members.map((member) => ({
    assetCode: member.assetCode,
    amountPerUnit: member.amountPerUnitRaw,
    selectionWeight: member.selectionWeight,
  }));

  return { code: draft.code.trim(), displayName: draft.displayName.trim(), supplyType: draft.supplyType, assets };
}

/**
 * The rules the API refuses a set on, checked here so a save that cannot succeed never leaves the browser.
 */
function validate(draft: SupplySetDraft): string | null {
  if (draft.displayName.trim() === "") {
    return "A supply set needs a display name.";
  }

  if (draft.code.trim() === "") {
    return "A supply set needs a code.";
  }

  if (draft.members.length === 0) {
    return "A supply set must hold at least one Asset.";
  }

  if (draft.members.some((member) => member.amountPerUnitRaw <= 0)) {
    return "Every Asset needs an amount above 0.";
  }

  // Only a repair draw reads the weights, and a set drawn from needs something it can land on.
  if (draft.supplyType === "Repair" && draft.members.every((member) => member.selectionWeight <= 0)) {
    return "A Repair set is drawn from, so at least one Asset must carry a weight above 0.";
  }

  return null;
}

export default function SupplySetManagerDialog({ open, onCancel, onSaveSuccess }: SupplySetManagerDialogProps) {
  const [sets, setSets] = useState<SupplySetView[]>([]);
  const [assets, setAssets] = useState<AssetOption[]>([]);
  const [draft, setDraft] = useState<SupplySetDraft>(emptyDraft);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Loads the catalogue and the Asset list each time the dialog opens, so a set seeded or edited elsewhere since is picked up.
  useEffect(() => {
    if (!open) {
      return;
    }

    let cancelled = false;

    const load = async () => {
      setError(null);

      try {
        const [loadedSets, loadedAssets] = await Promise.all([getSupplySets(), getAssetOptions()]);

        if (cancelled) {
          return;
        }

        setSets(loadedSets);
        setAssets(loadedAssets);
        setDraft(loadedSets.length > 0 ? toDraft(loadedSets[0]) : emptyDraft());
      } catch {
        if (!cancelled) {
          setError("Could not load the supply set catalogue.");
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, [open]);

  // The type the user has asked for but not yet confirmed. A set satisfies one need, so its members only make sense under the type they were chosen for —
  // "1.67 Berries per troop" says nothing about a movement point — and changing it empties the set. That is destructive, so it is asked about first.
  const [pendingSupplyType, setPendingSupplyType] = useState<SupplyType | null>(null);

  const showWeights = draft.supplyType === "Repair";

  const requestSupplyType = (next: SupplyType) => {
    if (next === draft.supplyType) {
      return;
    }

    // Nothing to lose yet, so no question worth asking.
    if (draft.members.length === 0) {
      setDraft((prev) => ({ ...prev, supplyType: next }));
      return;
    }

    setPendingSupplyType(next);
  };

  const confirmSupplyType = () => {
    if (pendingSupplyType == null) {
      return;
    }

    setDraft((prev) => ({ ...prev, supplyType: pendingSupplyType, members: [] }));
    setPendingSupplyType(null);
  };

  const selectSet = (value: string) => {
    // Any unanswered type change belonged to the set being left behind.
    setPendingSupplyType(null);

    if (value === NEW_SET) {
      setDraft(emptyDraft());
      setError(null);
      return;
    }

    const selected = sets.find((set) => String(set.id) === value);

    if (selected != null) {
      setDraft(toDraft(selected));
      setError(null);
    }
  };

  // One mutually-exclusive icon button per supply type, as the Component Setup dialog does for Component Type. The tooltip carries the basis too, since that
  // is what choosing a type actually decides; the aria-label is the bare type name, because an icon-only button has no other accessible name.
  const supplyTypeItems: ToolbarExclusiveItem[] = SUPPLY_TYPES.map((supplyType) => {
    const Icon = supplyTypeIcons[supplyType];
    return {
      type: "button",
      key: supplyType,
      icon: <Icon size={18} aria-hidden />,
      tooltip: `${supplyType} — ${SUPPLY_TYPE_BASIS[supplyType]}`,
      ariaLabel: supplyType,
    };
  });

  const save = async () => {
    const problem = validate(draft);

    if (problem != null) {
      setError(problem);
      return;
    }

    setSaving(true);
    setError(null);

    try {
      const saved = draft.id == null ? await createSupplySet(toPayload(draft)) : await updateSupplySet(draft.id, toPayload(draft));

      setSets((prev) => [...prev.filter((set) => set.id !== saved.id), saved].sort((left, right) => left.code.localeCompare(right.code)));
      setDraft(toDraft(saved));
      onSaveSuccess(`Supply set '${saved.displayName}' saved.`);
    } catch (err) {
      // The API names what is wrong — a code already taken, an Asset that does not resolve — and the http layer joins those messages onto the error, so
      // showing a generic failure would throw away the only thing the user can act on.
      setError(err instanceof Error ? err.message : "Saving the supply set failed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      title="Supply Sets"
      onClose={onCancel}
      size="xl"
      footer={
        <>
          {/* The save gate's reason, beside the Save it refuses rather than in a band above the form. The footer is a fixed-height row, so an error
              arriving cannot resize the dialog, and `truncate` stops a long one from doing it either — the whole message stays on the title. */}
          <p role="alert" className="mr-auto min-w-0 truncate text-sm text-[var(--bs-danger)]" title={error ?? undefined}>
            {error}
          </p>

          <Button variant="secondary" onClick={onCancel}>
            Cancel / Close
          </Button>
          <Button variant="success" onClick={() => void save()} disabled={saving}>
            Save
          </Button>
        </>
      }>
      {/* One grid for everything above the wells, in two columns across the dialog's width. The picker shares it rather than sitting in a header row of its
          own: two column systems stacked left the fields looking unrelated to the set they belong to. Display Name and Code sit side by side, since the
          code derives from the name. */}
      <div className="mb-3 grid grid-cols-1 gap-x-8 md:grid-cols-2">
        <FieldRow label="Supply Set">
          {/* New sits against the picker whose selection it clears, so the two read as one control: edit a saved set, or start one. */}
          <div className="flex items-center gap-2">
            <select
              aria-label="Supply Set"
              className={`${inputClass} min-w-0 flex-1`}
              value={draft.id == null ? "" : String(draft.id)}
              disabled={sets.length === 0}
              onChange={(e) => selectSet(e.target.value)}>
              {/* What an unsaved draft selects. `hidden` keeps it out of the dropdown, so the list stays the saved sets alone, while the closed picker still
                  reads blank — a select whose value matches no option falls back to showing the first one, naming a set the form is not editing. */}
              <option value="" hidden />
              {sets.map((set) => (
                <option key={set.id} value={String(set.id)}>
                  {set.displayName}
                </option>
              ))}
            </select>

            <Button variant="primary" className="shrink-0" onClick={() => selectSet(NEW_SET)}>
              New
            </Button>
          </div>
        </FieldRow>

        <FieldRow label="Supply Type" hint="Decides what a requirement's quantity is measured against">
          <ToolbarExclusiveButtons items={supplyTypeItems} value={draft.supplyType} onChange={(next) => requestSupplyType(next as SupplyType)} />
        </FieldRow>

        <FieldRow label="Display Name">
          <input
            aria-label="Display name"
            className={inputClass}
            value={draft.displayName}
            placeholder="e.g. Human Food"
            onChange={(e) => setDraft((prev) => ({ ...prev, displayName: e.target.value }))}
          />
        </FieldRow>

        <FieldRow label="Code" hint="The stable key a component's requirement names, e.g. human-food">
          <CodeField
            // Keyed on the set so switching sets remounts the field: CodeField decides whether a code is hand-edited from its first render's props, and a
            // mounted one would carry the previous set's verdict over.
            key={draft.id ?? NEW_SET}
            value={draft.code}
            source={draft.displayName}
            placeholder="supply set code, e.g. human-food"
            onChange={(code) => setDraft((prev) => ({ ...prev, code }))}
          />
        </FieldRow>
      </div>

      <p className="mb-1 text-xs text-slate-500">
        Any one of these Assets satisfies the need. The amount is how much of it one unit of the need costs, {SUPPLY_TYPE_BASIS[draft.supplyType]}.
      </p>

      <SupplySetAssetWells
        assets={assets}
        members={draft.members}
        showWeights={showWeights}
        onChange={(members) => setDraft((prev) => ({ ...prev, members }))}
      />

      <ConfirmDialog
        open={pendingSupplyType != null}
        title="Change supply type"
        message={
          <>
            A supply set satisfies one need, so its Assets belong to its type. Changing <strong>{draft.displayName || "this set"}</strong> from{" "}
            <strong>{draft.supplyType}</strong> to <strong>{pendingSupplyType}</strong> removes the {draft.members.length} Asset
            {draft.members.length === 1 ? "" : "s"} it holds.
          </>
        }
        confirmLabel="Change and clear"
        onConfirm={confirmSupplyType}
        onCancel={() => setPendingSupplyType(null)}
      />
    </Modal>
  );
}
