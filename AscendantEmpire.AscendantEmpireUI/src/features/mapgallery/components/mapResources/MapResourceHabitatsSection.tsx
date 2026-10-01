import { EditableTable, type EditableTableColumn } from "@/components/ui";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { MapResourceHabitat, MapResourceType } from "@/types/map/MapResource";
import { canSpread, createHabitat, grows, holdsSingleQuantity, kindOf } from "../../tools/mapResourceDraft";
import { CompactSelect, PercentInput, WholeNumberInput } from "./mapResourceFields";

interface MapResourceHabitatsSectionProps {
  type: MapResourceType;
  biomes: Biome[];
  geographies: Geography[];
  onChange: (updated: MapResourceType) => void;
}

// The select's stand-in for a null Geography, which a <select> value cannot carry.
const ANY_GEOGRAPHY = "any";

/**
 * Where the type can exist, and its values there, per Biome and optionally one Geography. No habitat means the type can never exist on, or spread
 * onto, that tile. A Geography-specific habitat wins over the Biome-wide one.
 *
 * Only the columns the type's kind uses are shown: a single-quantity type starts from an initial range, a type that grows has a ceiling and growth,
 * and only a renewable type can spread (all three spread values, or none).
 */
export default function MapResourceHabitatsSection({ type, biomes, geographies, onChange }: MapResourceHabitatsSectionProps) {
  const kind = kindOf(type);

  const setHabitats = (habitats: MapResourceHabitat[]) => onChange({ ...type, habitats });
  const update = (index: number, patch: Partial<MapResourceHabitat>) =>
    setHabitats(type.habitats.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const paintableBiomes = biomes.filter((b) => b.isPaintableOnCampaignMap).sort((a, b) => a.name.localeCompare(b.name));
  const biomeOptions = paintableBiomes.map((b) => ({ value: b.biomeType, label: b.name }));
  const geographyOptions = [
    { value: ANY_GEOGRAPHY, label: "Any" },
    ...geographies
      .filter((g) => g.isPaintableOnCampaignMap)
      .sort((a, b) => a.relativeElevation - b.relativeElevation)
      .map((g) => ({ value: g.campaignMapGeographyType, label: g.name })),
  ];

  // New rows take the first Biome with no Biome-wide habitat yet; a Geography-specific one can then be added by changing its Geography.
  const biomeWide = new Set(type.habitats.filter((h) => h.geographyType == null).map((h) => h.biomeType));
  const nextBiome = paintableBiomes.find((b) => biomeWide.has(b.biomeType) === false)?.biomeType ?? paintableBiomes[0]?.biomeType;

  const columns: EditableTableColumn<MapResourceHabitat>[] = [
    {
      header: "Biome",
      renderCell: (row, index) => (
        <CompactSelect ariaLabel="Biome" value={row.biomeType} options={biomeOptions} onChange={(value) => update(index, { biomeType: Number(value) })} />
      ),
    },
    {
      header: "Geography",
      renderCell: (row, index) => (
        <CompactSelect
          ariaLabel="Geography"
          value={row.geographyType ?? ANY_GEOGRAPHY}
          options={geographyOptions}
          onChange={(value) => update(index, { geographyType: value === ANY_GEOGRAPHY ? null : Number(value) })}
        />
      ),
    },
  ];

  if (holdsSingleQuantity(kind)) {
    columns.push(
      {
        header: "Start Min",
        renderCell: (row, index) => (
          <WholeNumberInput compact ariaLabel="Initial quantity minimum" value={row.initialQuantityMin} onChange={(value) => update(index, { initialQuantityMin: value })} />
        ),
      },
      {
        header: "Start Max",
        renderCell: (row, index) => (
          <WholeNumberInput compact ariaLabel="Initial quantity maximum" value={row.initialQuantityMax} onChange={(value) => update(index, { initialQuantityMax: value })} />
        ),
      },
    );
  }

  if (grows(kind)) {
    columns.push(
      {
        header: kind === "distribution" ? "Max Total" : "Maximum",
        renderCell: (row, index) => (
          <WholeNumberInput compact ariaLabel="Maximum quantity" value={row.maximumQuantity} onChange={(value) => update(index, { maximumQuantity: value })} />
        ),
      },
      {
        header: "Growth",
        renderCell: (row, index) => (
          <PercentInput compact ariaLabel="Growth percentage" value={row.growthPercentage} onChange={(value) => update(index, { growthPercentage: value })} />
        ),
      },
    );
  }

  if (canSpread(kind)) {
    columns.push(
      {
        header: "Spread",
        renderCell: (row, index) => (
          <PercentInput compact ariaLabel="Spread likelihood" placeholder="none" value={row.spreadLikelihood} onChange={(value) => update(index, { spreadLikelihood: value })} />
        ),
      },
      {
        header: "Limit Min",
        renderCell: (row, index) => (
          <WholeNumberInput compact ariaLabel="Spread limit minimum" value={row.spreadLimitMin} onChange={(value) => update(index, { spreadLimitMin: value })} />
        ),
      },
      {
        header: "Limit Max",
        renderCell: (row, index) => (
          <WholeNumberInput compact ariaLabel="Spread limit maximum" value={row.spreadLimitMax} onChange={(value) => update(index, { spreadLimitMax: value })} />
        ),
      },
    );
  }

  return (
    <div className="w-full">
      {kind === "stratified" ? (
        <p className="mb-2 text-xs text-slate-500">A stratified Map Resource only needs to know where it can exist: its quantities are on the Layers page.</p>
      ) : null}

      <EditableTable<MapResourceHabitat>
        rows={type.habitats}
        addLabel="Habitat"
        emptyHint="No habitats yet: the Map Resource cannot exist anywhere."
        addDisabled={nextBiome === undefined}
        onAddRow={() => setHabitats([...type.habitats, createHabitat(kind, nextBiome!)])}
        onRemoveRow={(index) => setHabitats(type.habitats.filter((_, i) => i !== index))}
        columns={columns}
      />
    </div>
  );
}
