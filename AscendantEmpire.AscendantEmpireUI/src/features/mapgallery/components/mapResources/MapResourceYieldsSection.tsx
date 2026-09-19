import { EditableTable } from "@/components/ui";
import type { MapResourceType, MapResourceTypeYield, ResourceSummary } from "@/types/map/MapResource";
import { CompactSelect, WholeNumberInput } from "./mapResourceFields";
import { firstResourceId, resourceOptions } from "./resourceOptions";
import NoResourcesHint from "./NoResourcesHint";

interface MapResourceYieldsSectionProps {
  type: MapResourceType;
  resources: ResourceSummary[];
  onChange: (updated: MapResourceType) => void;
}

/**
 * What one unit extracted gives, per Resource. A Resource off by default (e.g. a Deer's pelt) is only given once a game mechanic switches it on
 * for a Player; until then it is lost, though the unit still comes off the tile.
 */
export default function MapResourceYieldsSection({ type, resources, onChange }: MapResourceYieldsSectionProps) {
  const setYields = (yields: MapResourceTypeYield[]) => onChange({ ...type, yields });
  const update = (index: number, patch: Partial<MapResourceTypeYield>) =>
    setYields(type.yields.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  // Each Resource appears at most once, so new rows take the first Resource not yet used.
  const used = new Set(type.yields.map((row) => row.resourceId));
  const nextResourceId = resources.find((resource) => used.has(resource.id) === false)?.id;
  const options = resourceOptions(resources, type.yields.map((row) => row.resourceId));

  return (
    <div className="w-full">
      {firstResourceId(resources) === undefined ? <NoResourcesHint /> : null}

      <EditableTable<MapResourceTypeYield>
        rows={type.yields}
        addLabel="Yield"
        emptyHint="No yields yet."
        addDisabled={nextResourceId === undefined}
        onAddRow={() => setYields([...type.yields, { resourceId: nextResourceId!, amountPerUnit: 1, isEnabledByDefault: true }])}
        onRemoveRow={(index) => setYields(type.yields.filter((_, i) => i !== index))}
        columns={[
          {
            header: "Resource",
            renderCell: (row, index) => (
              <CompactSelect ariaLabel="Resource" value={row.resourceId} options={options} onChange={(value) => update(index, { resourceId: Number(value) })} />
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
