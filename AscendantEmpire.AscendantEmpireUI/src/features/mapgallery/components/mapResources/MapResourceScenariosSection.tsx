import { Button, EditableTable } from "@/components/ui";
import { HiTrash } from "react-icons/hi2";
import type { Biome } from "@/types/map/Biome";
import type { MapResourceDistributionScenario, MapResourceType, ResourceSummary } from "@/types/map/MapResource";
import { nextNumber } from "../../tools/mapResourceDraft";
import { CompactSelect, WholeNumberInput } from "./mapResourceFields";
import { firstResourceId, resourceOptions } from "./resourceOptions";
import NoResourcesHint from "./NoResourcesHint";

type ScenarioAmount = MapResourceDistributionScenario["amounts"][number];
type ScenarioWeight = MapResourceDistributionScenario["biomeWeights"][number];

interface MapResourceScenariosSectionProps {
  type: MapResourceType;
  resources: ResourceSummary[];
  biomes: Biome[];
  onChange: (updated: MapResourceType) => void;
}

/**
 * A Distribution's scenarios: each a set of starting amounts, one of which a tile is created from. A scenario is picked at game creation,
 * weighted by the tile's Biome, unless the map editor pinned one; a Biome with no weight never gets that scenario at random.
 */
export default function MapResourceScenariosSection({ type, resources, biomes, onChange }: MapResourceScenariosSectionProps) {
  const scenarios = type.distributionScenarios;

  const setScenarios = (distributionScenarios: MapResourceDistributionScenario[]) => onChange({ ...type, distributionScenarios });
  const update = (index: number, patch: Partial<MapResourceDistributionScenario>) =>
    setScenarios(scenarios.map((scenario, i) => (i === index ? { ...scenario, ...patch } : scenario)));

  const paintableBiomes = biomes.filter((b) => b.isPaintableOnCampaignMap).sort((a, b) => a.name.localeCompare(b.name));
  const biomeOptions = paintableBiomes.map((b) => ({ value: b.biomeType, label: b.name }));

  const addScenario = () =>
    setScenarios([
      ...scenarios,
      { scenarioNumber: nextNumber(scenarios.map((s) => s.scenarioNumber)), displayName: null, amounts: [], biomeWeights: [] },
    ]);

  return (
    <div className="w-full space-y-4">
      {firstResourceId(resources) === undefined ? <NoResourcesHint /> : null}

      {scenarios.map((scenario, index) => {
        // Each Resource and each Biome appears at most once in a scenario, so new rows take the first one not yet used.
        const usedResources = new Set(scenario.amounts.map((a) => a.resourceId));
        const nextResourceId = resources.find((r) => usedResources.has(r.id) === false)?.id;
        const usedBiomes = new Set(scenario.biomeWeights.map((w) => w.biomeType));
        const nextBiome = paintableBiomes.find((b) => usedBiomes.has(b.biomeType) === false)?.biomeType;
        const options = resourceOptions(resources, scenario.amounts.map((a) => a.resourceId));

        const setAmount = (row: number, patch: Partial<ScenarioAmount>) =>
          update(index, { amounts: scenario.amounts.map((a, i) => (i === row ? { ...a, ...patch } : a)) });
        const setWeight = (row: number, patch: Partial<ScenarioWeight>) =>
          update(index, { biomeWeights: scenario.biomeWeights.map((w, i) => (i === row ? { ...w, ...patch } : w)) });

        return (
          <section key={scenario.scenarioNumber} className="rounded-md border border-slate-300 p-3">
            <div className="mb-2 flex items-center gap-2">
              <span className="text-sm font-semibold whitespace-nowrap">Scenario {scenario.scenarioNumber}</span>
              <input
                className="w-full rounded border border-slate-300 px-1.5 py-1 text-xs"
                placeholder="Name (optional)"
                aria-label={`Scenario ${scenario.scenarioNumber} name`}
                value={scenario.displayName ?? ""}
                onChange={(e) => update(index, { displayName: e.target.value === "" ? null : e.target.value })}
              />
              {/* Removing a scenario a map still places is refused by the server on save, with a message saying so. */}
              <button
                type="button"
                className="text-slate-500 hover:text-red-600"
                aria-label={`Remove scenario ${scenario.scenarioNumber}`}
                onClick={() => setScenarios(scenarios.filter((_, i) => i !== index))}>
                <HiTrash />
              </button>
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <EditableTable<ScenarioAmount>
                rows={scenario.amounts}
                addLabel="Resource"
                emptyHint="No Resources yet."
                addDisabled={nextResourceId === undefined}
                onAddRow={() => update(index, { amounts: [...scenario.amounts, { resourceId: nextResourceId!, amount: 1 }] })}
                onRemoveRow={(row) => update(index, { amounts: scenario.amounts.filter((_, i) => i !== row) })}
                columns={[
                  {
                    header: "Resource",
                    renderCell: (row, rowIndex) => (
                      <CompactSelect ariaLabel="Resource" value={row.resourceId} options={options} onChange={(value) => setAmount(rowIndex, { resourceId: Number(value) })} />
                    ),
                  },
                  {
                    header: "Amount",
                    className: "w-24",
                    renderCell: (row, rowIndex) => (
                      <WholeNumberInput compact required ariaLabel="Amount" value={row.amount} onChange={(value) => setAmount(rowIndex, { amount: value })} />
                    ),
                  },
                ]}
              />

              <EditableTable<ScenarioWeight>
                rows={scenario.biomeWeights}
                addLabel="Biome weight"
                emptyHint="Never picked at random."
                addDisabled={nextBiome === undefined}
                onAddRow={() => update(index, { biomeWeights: [...scenario.biomeWeights, { biomeType: nextBiome!, weight: 1 }] })}
                onRemoveRow={(row) => update(index, { biomeWeights: scenario.biomeWeights.filter((_, i) => i !== row) })}
                columns={[
                  {
                    header: "Biome",
                    renderCell: (row, rowIndex) => (
                      <CompactSelect ariaLabel="Biome" value={row.biomeType} options={biomeOptions} onChange={(value) => setWeight(rowIndex, { biomeType: Number(value) })} />
                    ),
                  },
                  {
                    header: "Weight",
                    className: "w-20",
                    renderCell: (row, rowIndex) => (
                      <WholeNumberInput compact required ariaLabel="Weight" value={row.weight} onChange={(value) => setWeight(rowIndex, { weight: value })} />
                    ),
                  },
                ]}
              />
            </div>
          </section>
        );
      })}

      <Button variant="secondary" onClick={addScenario}>
        Add Scenario
      </Button>
    </div>
  );
}
