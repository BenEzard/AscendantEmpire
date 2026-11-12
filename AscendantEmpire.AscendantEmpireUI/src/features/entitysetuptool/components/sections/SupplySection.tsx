// SupplySection — the component dialog's "Supply" page. A Body states what it consumes as a quantity of a supply set: the set names which Assets will do,
// so a requirement for human food is satisfied by whatever the tile actually grows.
//
// Like TagsSection, the sets themselves are authored elsewhere (the Supply Sets dialog) and referenced here by code, so a requirement always resolves on
// save. What the quantity is measured against comes from the set's type — a Food set is per troop, Fuel per movement point, Repair per damage point.

import { useMemo } from "react";
import { HiXMark } from "react-icons/hi2";

import { inputClass } from "@/components/ui";
import { rawToScaled, scaledToRaw, type ComponentType, type UnitComponentSupplyRequirement } from "@/features/unitdesign/types/UnitComponent";

import type { SupplySetView, SupplyType } from "../../api/supplySetApi";

/** What one unit of each kind of need is measured against. */
const SUPPLY_TYPE_BASIS: Record<SupplyType, string> = {
  Food: "per troop",
  Fuel: "per movement point",
  Repair: "per damage point",
};

interface SupplySectionProps {
  /** The component's type. Only a Body may carry a requirement, so every other type gets the explanation instead of the editor. */
  componentType: ComponentType;
  /** The component's current requirements (the committed value). */
  requirements: UnitComponentSupplyRequirement[];
  onChange: (next: UnitComponentSupplyRequirement[]) => void;
  /** The supply set catalogue to choose from. */
  availableSets: SupplySetView[];
  /** While true, the picker shows a busy message instead of the list. */
  loading?: boolean;
  /** When set, shown instead of the list. */
  error?: string | null;
}

export default function SupplySection({
  componentType,
  requirements,
  onChange,
  availableSets,
  loading = false,
  error = null,
}: SupplySectionProps) {
  const setByCode = useMemo(() => {
    const map = new Map<string, SupplySetView>();
    for (const set of availableSets) {
      map.set(set.code, set);
    }
    return map;
  }, [availableSets]);

  // The picker only offers sets the component does not already require, since two rows for one set would double the need rather than express anything.
  const selectableSets = useMemo(() => {
    const required = new Set(requirements.map((requirement) => requirement.supplySetCode));
    return availableSets.filter((set) => !required.has(set.code));
  }, [availableSets, requirements]);

  if (componentType !== "Body") {
    return (
      <div className="w-full max-w-xl">
        <p className="text-sm text-slate-500">
          Supply needs sit on the body a Unit is built around, so only a Body carries them. A weapon's ammunition is authored on the weapon itself.
        </p>
      </div>
    );
  }

  const addRequirement = (supplySetCode: string) => {
    if (supplySetCode === "" || requirements.some((requirement) => requirement.supplySetCode === supplySetCode)) {
      return;
    }

    onChange([...requirements, { supplySetCode, quantity: scaledToRaw(1) }]);
  };

  const updateQuantity = (supplySetCode: string, raw: string) => {
    const quantity = raw.trim() === "" ? 0 : scaledToRaw(Number(raw));

    onChange(requirements.map((requirement) => (requirement.supplySetCode === supplySetCode ? { ...requirement, quantity } : requirement)));
  };

  const removeRequirement = (supplySetCode: string) => {
    onChange(requirements.filter((requirement) => requirement.supplySetCode !== supplySetCode));
  };

  return (
    <div className="w-full max-w-xl">
      <p className="mb-2 text-xs text-slate-400">
        What this body consumes. A set names the Assets that will do, so the requirement is for food rather than for one particular foodstuff.
      </p>

      <div className="mb-3">
        <select
          aria-label="Add a supply set"
          className="w-72 rounded-md border border-slate-300 px-2 py-2 text-sm"
          value=""
          disabled={loading || error != null}
          onChange={(e) => {
            addRequirement(e.target.value);
            e.target.value = "";
          }}>
          <option value="">{loading ? "Loading supply sets…" : (error ?? "Select a supply set…")}</option>
          {selectableSets.map((set) => (
            <option key={set.code} value={set.code}>
              {set.displayName}
            </option>
          ))}
        </select>
      </div>

      {requirements.length === 0 ? (
        <p className="text-sm text-slate-500">No supply needs yet.</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-slate-500">
              <th className="py-1">Supply Set</th>
              <th className="py-1">Quantity</th>
              <th className="py-1">Basis</th>
              <th className="py-1" />
            </tr>
          </thead>
          <tbody>
            {requirements.map((requirement) => {
              // A code with no matching set can occur while the catalogue is still loading, or on a component whose set has since been renamed; it renders
              // by its raw code rather than disappearing.
              const set = setByCode.get(requirement.supplySetCode);
              const label = set?.displayName ?? requirement.supplySetCode;

              return (
                <tr key={requirement.supplySetCode}>
                  <td className="py-1 pr-2">{label}</td>
                  <td className="py-1 pr-2">
                    <input
                      aria-label={`Quantity of ${label}`}
                      className={inputClass}
                      type="number"
                      step="0.001"
                      min={0}
                      value={String(rawToScaled(requirement.quantity))}
                      onChange={(e) => updateQuantity(requirement.supplySetCode, e.target.value)}
                    />
                  </td>
                  <td className="py-1 pr-2 text-slate-500">{set != null ? SUPPLY_TYPE_BASIS[set.supplyType] : "—"}</td>
                  <td className="py-1">
                    <button
                      type="button"
                      className="text-slate-400 hover:text-slate-700"
                      aria-label={`Remove ${label}`}
                      onClick={() => removeRequirement(requirement.supplySetCode)}>
                      <HiXMark size={16} aria-hidden />
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}
