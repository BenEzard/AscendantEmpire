// WeaponAmmunitionEditor — the kinds of ammunition a weapon accepts.
//
// Sits inside WeaponProperties, so it serves a Weapon component's Properties page and a Body's inherent attack alike. The Assets themselves are authored in
// the Asset Manager and referenced here by code, so an option always resolves on save.
//
// Controlled: the chosen options come in as a prop and every edit goes straight back out through onChange, like SupplySection.

import { useMemo } from "react";
import { HiXMark } from "react-icons/hi2";

import AssetIconImage from "@/components/assets/AssetIconImage";
import { MAX_AMMUNITION_OPTIONS, type WeaponAmmunitionOption } from "@/features/unitdesign/types/UnitComponent";

import type { AssetOption } from "../../../api/assetApi";

const ICON_SIZE = 20;

/** Asset codes under this category are the ones a weapon may be loaded with. */
const AMMUNITION_CODE_PREFIX = "Weapons.Ammunition.";

interface WeaponAmmunitionEditorProps {
  /** The weapon's current options (the committed value). */
  options: WeaponAmmunitionOption[];
  onChange: (next: WeaponAmmunitionOption[]) => void;
  /** The Asset catalogue to choose from. */
  assets: AssetOption[];
  /** While true, the picker shows a busy message instead of the list. */
  loading?: boolean;
  /** When set, shown instead of the list. */
  error?: string | null;
}

export default function WeaponAmmunitionEditor({
  options,
  onChange,
  assets,
  loading = false,
  error = null,
}: WeaponAmmunitionEditorProps) {
  const assetByCode = useMemo(() => {
    const map = new Map<string, AssetOption>();
    for (const asset of assets) {
      map.set(asset.code, asset);
    }
    return map;
  }, [assets]);

  // Every grade of a graded Asset shares one display name, so a name alone can leave two entries the author cannot tell apart. Where that happens the code
  // disambiguates, since it is unique across the catalogue.
  const labelByCode = useMemo(() => {
    const nameCounts = new Map<string, number>();
    for (const asset of assets) {
      nameCounts.set(asset.displayName, (nameCounts.get(asset.displayName) ?? 0) + 1);
    }

    const map = new Map<string, string>();
    for (const asset of assets) {
      const shared = (nameCounts.get(asset.displayName) ?? 0) > 1;
      map.set(asset.code, shared ? `${asset.displayName} (${asset.code})` : asset.displayName);
    }
    return map;
  }, [assets]);

  // Only ammunition Assets are offered, and only those the weapon does not already name: a second row for one Asset would double what an attack costs rather
  // than express anything. An option already stored against some other Asset still renders below — the filter governs what can be added, not what is shown.
  const selectableAssets = useMemo(() => {
    const named = new Set(options.map((option) => option.assetCode));
    return assets.filter((asset) => asset.code.startsWith(AMMUNITION_CODE_PREFIX) && !named.has(asset.code));
  }, [assets, options]);

  const full = options.length >= MAX_AMMUNITION_OPTIONS;

  const addOption = (assetCode: string) => {
    if (assetCode === "" || full || options.some((option) => option.assetCode === assetCode)) {
      return;
    }

    onChange([...options, { assetCode }]);
  };

  const removeOption = (assetCode: string) => {
    onChange(options.filter((option) => option.assetCode !== assetCode));
  };

  return (
    <div className="mt-3">
      <p className="mb-2 text-xs text-slate-400">
        Ammunition this weapon accepts. An attack spends one unit of whichever kind is loaded; leave it empty for a weapon that needs none.
      </p>

      <select
        aria-label="Add ammunition"
        className="mb-3 w-72 rounded-md border border-slate-300 px-2 py-2 text-sm"
        value=""
        disabled={full || loading || error != null}
        onChange={(e) => {
          addOption(e.target.value);
          e.target.value = "";
        }}>
        <option value="">{full ? `At most ${MAX_AMMUNITION_OPTIONS} kinds` : loading ? "Loading Assets…" : (error ?? "Select an Asset…")}</option>
        {selectableAssets.map((asset) => (
          <option key={asset.code} value={asset.code}>
            {labelByCode.get(asset.code) ?? asset.displayName}
          </option>
        ))}
      </select>

      {options.length > 0 && (
        <ul className="w-72 text-sm">
          {options.map((option) => {
            // A code with no matching Asset can occur while the catalogue is still loading, or on a weapon whose Asset has since been renamed; it renders by
            // its raw code rather than disappearing.
            const asset = assetByCode.get(option.assetCode);
            const label = labelByCode.get(option.assetCode) ?? option.assetCode;

            return (
              <li key={option.assetCode} className="flex items-center gap-1.5 py-1">
                <AssetIconImage iconPath={asset?.iconPath ?? ""} size={ICON_SIZE} />
                <span className="flex-1">{label}</span>
                <button
                  type="button"
                  className="text-slate-400 hover:text-slate-700"
                  aria-label={`Remove ${label}`}
                  onClick={() => removeOption(option.assetCode)}>
                  <HiXMark size={16} aria-hidden />
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
