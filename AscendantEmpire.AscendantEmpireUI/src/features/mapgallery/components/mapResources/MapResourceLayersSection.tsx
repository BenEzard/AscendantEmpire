import { EditableTable } from "@/components/ui";
import type { MapResourceType, MapResourceTypeLayer, ResourceSummary } from "@/types/map/MapResource";
import { createLayer, renumberLayers } from "../../tools/mapResourceDraft";
import { CompactSelect, PercentInput, WholeNumberInput } from "./mapResourceFields";
import { firstResourceId, resourceOptions } from "./resourceOptions";
import NoResourcesHint from "./NoResourcesHint";

interface MapResourceLayersSectionProps {
  type: MapResourceType;
  resources: ResourceSummary[];
  onChange: (updated: MapResourceType) => void;
}

/**
 * A stratified type's layers, from the top down, reached in order. Each game rolls its own contents for every layer: empty with the given
 * likelihood, otherwise a quantity in the range. The first layer is found through the type's own discovery likelihood; every deeper layer has its
 * own, and a mine level to reach it.
 */
export default function MapResourceLayersSection({ type, resources, onChange }: MapResourceLayersSectionProps) {
  const setLayers = (layers: MapResourceTypeLayer[]) => onChange({ ...type, layers });
  const update = (index: number, patch: Partial<MapResourceTypeLayer>) =>
    setLayers(type.layers.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const firstId = firstResourceId(resources);
  const options = resourceOptions(resources, type.layers.map((row) => row.resourceId));

  return (
    <div className="w-full">
      {firstId === undefined ? <NoResourcesHint /> : null}

      <EditableTable<MapResourceTypeLayer>
        rows={type.layers}
        addLabel="Layer"
        emptyHint="No layers yet."
        addDisabled={firstId === undefined}
        // A new layer goes at the bottom, holding the same Resource as the one above it (or the first listed) until changed.
        onAddRow={() => setLayers([...type.layers, createLayer(type.layers.length + 1, type.layers.at(-1)?.resourceId ?? firstId!)])}
        // Layers are numbered 1, 2, 3… with no gaps, so removing one renumbers those below it.
        onRemoveRow={(index) => setLayers(renumberLayers(type.layers.filter((_, i) => i !== index)))}
        columns={[
          { header: "#", className: "w-6 text-slate-500", renderCell: (row) => row.layerNumber },
          {
            header: "Resource",
            renderCell: (row, index) => (
              <CompactSelect ariaLabel="Resource" value={row.resourceId} options={options} onChange={(value) => update(index, { resourceId: Number(value) })} />
            ),
          },
          {
            header: "Empty",
            renderCell: (row, index) => (
              <PercentInput compact required ariaLabel="Empty likelihood" value={row.emptyLikelihood} onChange={(value) => update(index, { emptyLikelihood: value })} />
            ),
          },
          {
            header: "Qty Min",
            renderCell: (row, index) => (
              <WholeNumberInput compact required ariaLabel="Quantity minimum" value={row.quantityMin} onChange={(value) => update(index, { quantityMin: value })} />
            ),
          },
          {
            header: "Qty Max",
            renderCell: (row, index) => (
              <WholeNumberInput compact required ariaLabel="Quantity maximum" value={row.quantityMax} onChange={(value) => update(index, { quantityMax: value })} />
            ),
          },
          {
            header: "Discovery",
            renderCell: (row, index) =>
              row.layerNumber === 1 ? (
                <span className="text-xs text-slate-400">type's</span>
              ) : (
                <PercentInput compact ariaLabel="Discovery likelihood" value={row.discoveryLikelihood} onChange={(value) => update(index, { discoveryLikelihood: value })} />
              ),
          },
          {
            header: "Mine Lvl",
            renderCell: (row, index) =>
              row.layerNumber === 1 ? (
                <span className="text-xs text-slate-400">none</span>
              ) : (
                <WholeNumberInput compact ariaLabel="Required mine level" value={row.requiredMineLevel} onChange={(value) => update(index, { requiredMineLevel: value })} />
              ),
          },
          {
            header: "Fuzzy Min",
            renderCell: (row, index) => (
              <PercentInput compact required ariaLabel="Fuzziness minimum" value={row.estimationFuzzyFactorMin} onChange={(value) => update(index, { estimationFuzzyFactorMin: value })} />
            ),
          },
          {
            header: "Fuzzy Max",
            renderCell: (row, index) => (
              <PercentInput compact required ariaLabel="Fuzziness maximum" value={row.estimationFuzzyFactorMax} onChange={(value) => update(index, { estimationFuzzyFactorMax: value })} />
            ),
          },
        ]}
      />
    </div>
  );
}
