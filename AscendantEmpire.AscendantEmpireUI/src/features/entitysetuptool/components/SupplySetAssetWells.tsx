// SupplySetAssetWells — the member picker for a supply set: two wells side by side, the catalogue on the left and the set's members on the right.
//
// Replaces an "Add Asset" dropdown over the whole catalogue plus a growing table beneath it. Both scaled badly: the dropdown was a long list to hunt
// through, and a set of any size pushed the rest of the dialog off the bottom. Tiles in a fixed, scrollable well stay the same height whatever the set holds.
//
// An Asset sits in exactly one well — adding it to the set takes it out of the catalogue side — so a tile's name identifies it unambiguously. Moving one
// either way works by drag or by double-click; double-click is not a convenience, it is the only way to work the control from the keyboard, since every
// control here is a tile.

import { useState, type DragEvent } from "react";

import AssetIconImage from "@/components/assets/AssetIconImage";
import Tooltip from "@/components/ui/Tooltip";
import { inputClass } from "@/components/ui/FieldRow";
import { rawToScaled, scaledToRaw } from "@/features/unitdesign/types/UnitComponent";

import ScaledNumberField from "./ScaledNumberField";
import type { AssetOption } from "../api/assetApi";

/** One Asset in the set being edited. Amounts are held raw (Scale 1,000) and shown as decimals. */
export interface SupplySetMember {
  assetCode: string;
  assetDisplayName: string;
  amountPerUnitRaw: number;
  selectionWeight: number;
}

/** What a newly added member costs, until the author says otherwise: one of the Asset per unit of the need. */
const DEFAULT_AMOUNT_RAW = scaledToRaw(1);

/** What a newly added member weighs in a Repair draw. Equal odds with every other member. */
const DEFAULT_WEIGHT = 1;

const TILE_ICON_SIZE = 32;

interface SupplySetAssetWellsProps {
  /** The whole Asset catalogue. Those already in the set are drawn from the right-hand well instead. */
  assets: AssetOption[];
  members: SupplySetMember[];
  /** Whether a member's selection weight is editable — only a Repair set is drawn from, so only it reads one. */
  showWeights: boolean;
  onChange: (next: SupplySetMember[]) => void;
}

export default function SupplySetAssetWells({ assets, members, showWeights, onChange }: SupplySetAssetWellsProps) {
  const [search, setSearch] = useState("");

  // Which member the Amount and Weight fields edit. Null means they instead hold what the next added member will get.
  const [selectedAssetCode, setSelectedAssetCode] = useState<string | null>(null);
  const [pendingAmountRaw, setPendingAmountRaw] = useState(DEFAULT_AMOUNT_RAW);
  const [pendingWeight, setPendingWeight] = useState(DEFAULT_WEIGHT);

  const memberCodes = new Set(members.map((member) => member.assetCode));
  const selected = members.find((member) => member.assetCode === selectedAssetCode) ?? null;

  const needle = search.trim().toLowerCase();
  const available = assets.filter((asset) => {
    if (memberCodes.has(asset.code)) {
      return false;
    }

    // Code as well as name, because a code says where the Asset sits in the hierarchy and that is often what the author remembers.
    return needle === "" || asset.displayName.toLowerCase().includes(needle) || asset.code.toLowerCase().includes(needle);
  });

  const addMember = (assetCode: string) => {
    const asset = assets.find((candidate) => candidate.code === assetCode);

    if (asset == null || memberCodes.has(assetCode)) {
      return;
    }

    onChange([
      ...members,
      {
        assetCode: asset.code,
        assetDisplayName: asset.displayName,
        amountPerUnitRaw: pendingAmountRaw,
        selectionWeight: pendingWeight,
      },
    ]);
  };

  const removeMember = (assetCode: string) => {
    if (!memberCodes.has(assetCode)) {
      return;
    }

    if (selectedAssetCode === assetCode) {
      setSelectedAssetCode(null);
    }

    onChange(members.filter((member) => member.assetCode !== assetCode));
  };

  const patchSelected = (patch: Partial<SupplySetMember>) => {
    onChange(members.map((member) => (member.assetCode === selectedAssetCode ? { ...member, ...patch } : member)));
  };

  const handleAmountChange = (nextRaw: number) => {
    if (selected == null) {
      setPendingAmountRaw(nextRaw);
      return;
    }

    patchSelected({ amountPerUnitRaw: nextRaw });
  };

  const handleWeightChange = (nextWeight: number) => {
    if (selected == null) {
      setPendingWeight(nextWeight);
      return;
    }

    patchSelected({ selectionWeight: nextWeight });
  };

  // The dragged Asset's code is all either well needs: the destination decides what happens to it, so the drag carries no notion of where it came from.
  const startDrag = (event: DragEvent, assetCode: string) => {
    event.dataTransfer.setData("text/plain", assetCode);
    event.dataTransfer.effectAllowed = "move";
  };

  const allowDrop = (event: DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
  };

  return (
    // Fixed height, with each well's tile area taking what the controls above it leave. An empty set and a full one occupy the same space, so neither adding
    // members nor switching to a Repair set (which brings a Weight field with it) moves the dialog's edges.
    <div className="grid h-72 grid-cols-3 rounded-md border border-slate-200">
      {/* Catalogue side. Also a drop target, so dragging a member out of the set is the mirror of dragging one in. */}
      <div
        role="group"
        aria-label="Asset catalogue"
        className="col-span-2 flex min-h-0 flex-col border-r border-slate-200 p-2"
        onDragOver={allowDrop}
        onDrop={(event) => {
          event.preventDefault();
          removeMember(event.dataTransfer.getData("text/plain"));
        }}>
        <input
          aria-label="Search Assets"
          className={`${inputClass} mb-2 shrink-0`}
          placeholder="Search Assets by name or code"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />

        <div className="flex min-h-0 flex-1 flex-wrap content-start gap-1 overflow-y-auto">
          {available.length === 0 ? (
            <p className="px-1 text-xs text-slate-400">{assets.length === 0 ? "No Assets to choose from." : "No Asset matches that search."}</p>
          ) : (
            available.map((asset) => (
              <Tooltip key={asset.code} content={asset.displayName}>
                <button
                  type="button"
                  draggable
                  aria-label={asset.displayName}
                  className="rounded-md border border-slate-300 bg-white p-1 hover:bg-slate-50"
                  onDragStart={(event) => startDrag(event, asset.code)}
                  onDoubleClick={() => addMember(asset.code)}>
                  <AssetIconImage iconPath={asset.iconPath} size={TILE_ICON_SIZE} />
                </button>
              </Tooltip>
            ))
          )}
        </div>
      </div>

      {/* The set's members. The Amount and Weight fields above edit whichever member is selected, or seed the next one added when none is. */}
      <div
        role="group"
        aria-label="Assets in this set"
        className="col-span-1 flex min-h-0 flex-col p-2"
        onDragOver={allowDrop}
        onDrop={(event) => {
          event.preventDefault();
          addMember(event.dataTransfer.getData("text/plain"));
        }}>
        <div className="mb-2 flex shrink-0 items-center gap-2">
          <span className="text-xs font-medium text-slate-500">Amount</span>
          {/* Sized to the number rather than to the well: ScaledNumberField is `w-full`, and a decimal in a box the width of the column read as a text field. */}
          <div className="w-24">
            <ScaledNumberField
              ariaLabel="Amount"
              rawValue={selected?.amountPerUnitRaw ?? pendingAmountRaw}
              nullable={false}
              onChange={(next) => handleAmountChange(next ?? 0)}
            />
          </div>
        </div>

        {showWeights ? (
          <div className="mb-2 flex shrink-0 items-center gap-2">
            <span className="text-xs font-medium text-slate-500">Weight</span>
            <input
              aria-label="Weight"
              type="number"
              step="1"
              className={`${inputClass} w-24`}
              value={selected?.selectionWeight ?? pendingWeight}
              onChange={(event) => handleWeightChange(event.target.value.trim() === "" ? 0 : Number(event.target.value))}
            />
          </div>
        ) : null}

        <div className="flex min-h-0 flex-1 flex-wrap content-start gap-1 overflow-y-auto">
          {members.length === 0 ? (
            <p className="px-1 text-xs text-slate-400">Drag an Asset here, or double-click one.</p>
          ) : (
            members.map((member) => {
              const isSelected = member.assetCode === selectedAssetCode;
              const asset = assets.find((candidate) => candidate.code === member.assetCode);

              return (
                <Tooltip key={member.assetCode} content={member.assetDisplayName}>
                  <button
                    type="button"
                    draggable
                    aria-pressed={isSelected}
                    aria-label={`${member.assetDisplayName} ${rawToScaled(member.amountPerUnitRaw)}`}
                    className={[
                      "flex flex-col items-center rounded-md border p-1",
                      isSelected
                        ? "border-[var(--bs-primary)] bg-[color-mix(in_srgb,var(--bs-primary)_12%,transparent)]"
                        : "border-slate-300 bg-white hover:bg-slate-50",
                    ].join(" ")}
                    onDragStart={(event) => startDrag(event, member.assetCode)}
                    onClick={() => setSelectedAssetCode(isSelected ? null : member.assetCode)}
                    onDoubleClick={() => removeMember(member.assetCode)}>
                    <AssetIconImage iconPath={asset?.iconPath ?? ""} size={TILE_ICON_SIZE} />
                    <span className="text-xs tabular-nums text-slate-600">
                      {rawToScaled(member.amountPerUnitRaw)}
                      {showWeights ? ` ×${member.selectionWeight}` : ""}
                    </span>
                  </button>
                </Tooltip>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
}
