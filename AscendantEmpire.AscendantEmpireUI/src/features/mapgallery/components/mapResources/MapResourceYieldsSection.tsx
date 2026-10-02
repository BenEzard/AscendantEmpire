import { EditableTable } from "@/components/ui";
import type { MapResourceType, MapResourceTypeYield, AssetSummary } from "@/types/map/MapResource";
import { CompactSelect, WholeNumberInput } from "./mapResourceFields";
import { firstAssetId, assetOptions } from "./assetOptions";
import NoAssetsHint from "./NoAssetsHint";

interface MapResourceYieldsSectionProps {
  type: MapResourceType;
  assets: AssetSummary[];
  onChange: (updated: MapResourceType) => void;
}

/**
 * What one unit extracted gives, per Asset. An Asset off by default (e.g. a Deer's pelt) is only given once a game mechanic switches it on
 * for a Player; until then it is lost, though the unit still comes off the tile.
 */
export default function MapResourceYieldsSection({ type, assets, onChange }: MapResourceYieldsSectionProps) {
  const setYields = (yields: MapResourceTypeYield[]) => onChange({ ...type, yields });
  const update = (index: number, patch: Partial<MapResourceTypeYield>) =>
    setYields(type.yields.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  // Each Asset appears at most once, so new rows take the first Asset not yet used.
  const used = new Set(type.yields.map((row) => row.assetId));
  const nextAssetId = assets.find((asset) => used.has(asset.id) === false)?.id;
  const options = assetOptions(assets, type.yields.map((row) => row.assetId));

  return (
    <div className="w-full">
      {firstAssetId(assets) === undefined ? <NoAssetsHint /> : null}

      <EditableTable<MapResourceTypeYield>
        rows={type.yields}
        addLabel="Yield"
        emptyHint="No yields yet."
        addDisabled={nextAssetId === undefined}
        onAddRow={() => setYields([...type.yields, { assetId: nextAssetId!, amountPerUnit: 1, isEnabledByDefault: true }])}
        onRemoveRow={(index) => setYields(type.yields.filter((_, i) => i !== index))}
        columns={[
          {
            header: "Resource",
            renderCell: (row, index) => (
              <CompactSelect ariaLabel="Resource" value={row.assetId} options={options} onChange={(value) => update(index, { assetId: Number(value) })} />
            ),
          },
          {
            header: "Per Unit",
            className: "w-24",
            renderCell: (row, index) => (
              <WholeNumberInput compact required ariaLabel="Amount per unit" value={row.amountPerUnit} onChange={(value) => update(index, { amountPerUnit: value })} />
            ),
          },
          {
            header: "On by Default",
            className: "w-28 text-center",
            renderCell: (row, index) => (
              <input
                type="checkbox"
                aria-label="On by default"
                checked={row.isEnabledByDefault}
                onChange={(e) => update(index, { isEnabledByDefault: e.target.checked })}
              />
            ),
          },
        ]}
      />
    </div>
  );
}
