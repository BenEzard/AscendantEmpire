import { EditableTable, HintIcon, type EditableTableColumn } from "@/components/ui";
import type { Biome, BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType, Geography } from "@/types/map/Geography";
import type { MapResourceHabitat, MapResourceType } from "@/types/map/MapResource";
import { canSpread, createHabitat, grows, holdsSingleQuantity, kindOf, type MapResourceKind } from "../../tools/mapResourceDraft";
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
 * One line of explanation per value column the table is showing, for the tooltip beside the section's blurb.
 */
function columnHintsFor(kind: MapResourceKind): string {
  const lines: string[] = [];

  if (holdsSingleQuantity(kind)) {
    lines.push("Start Min / Start Max — the range an instance's starting quantity is rolled from when the game is created.");
  }

  if (grows(kind)) {
    lines.push(
      kind === "distribution"
        ? "Max Total — the ceiling the instance's Assets add up to. Growth stops there."
        : "Max Cap — the ceiling an instance grows to. Growth stops there, and only an instance at its cap can spread.",
    );
    lines.push("Growth — the share of its current quantity an instance adds each round, up to its cap.");
  }

  if (canSpread(kind)) {
    lines.push("Spread — the chance each round that an instance at its cap spreads onto one random adjacent tile.");
    lines.push(
      "Limit Min / Limit Max — the range an instance's spread limit is rolled from: how many more times it may spread.\n" +
        "    Each spread spends one, and the instance it creates inherits what is left, so each generation spreads less than the last.",
    );
  }

  return lines.join("\n");
}

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
        <CompactSelect
          ariaLabel="Biome"
          value={row.biomeType}
          options={biomeOptions}
          // Cast, not Number(): a BiomeType is an enum name, so the <select>'s string value already is one. Coercing it gave NaN, which matched
          // no option, so the select silently snapped back to the first Biome and the choice never stuck.
          onChange={(value) => update(index, { biomeType: value as BiomeType })}
        />
      ),
    },
    {
      header: "Geography",
      renderCell: (row, index) => (
        <CompactSelect
          ariaLabel="Geography"
          value={row.geographyType ?? ANY_GEOGRAPHY}
          options={geographyOptions}
          onChange={(value) => update(index, { geographyType: value === ANY_GEOGRAPHY ? null : (value as CampaignMapGeographyType) })}
        />
      ),
    },
  ];

  if (holdsSingleQuantity(kind)) {
    columns.push(
      {
        header: "Start Min",
        renderCell: (row, index) => (
          <WholeNumberInput
            compact
            ariaLabel="Initial quantity minimum"
            value={row.initialQuantityMin}
            onChange={(value) => update(index, { initialQuantityMin: value })}
          />
        ),
      },
      {
        header: "Start Max",
        renderCell: (row, index) => (
          <WholeNumberInput
            compact
            ariaLabel="Initial quantity maximum"
            value={row.initialQuantityMax}
            onChange={(value) => update(index, { initialQuantityMax: value })}
          />
        ),
      },
    );
  }

  if (grows(kind)) {
    columns.push(
      {
        // "Max Cap" rather than "Maximum": the value is the ceiling growth stops at, not the largest quantity that may be authored.
        header: kind === "distribution" ? "Max Total" : "Max Cap",
        renderCell: (row, index) => (
          <WholeNumberInput
            compact
            ariaLabel="Maximum quantity"
            value={row.maximumQuantity}
            onChange={(value) => update(index, { maximumQuantity: value })}
          />
        ),
      },
      {
        header: "Growth",
        renderCell: (row, index) => (
          <PercentInput
            compact
            ariaLabel="Growth percentage"
            value={row.growthPercentage}
            onChange={(value) => update(index, { growthPercentage: value })}
          />
        ),
      },
    );
  }

  if (canSpread(kind)) {
    columns.push(
      {
        header: "Spread",
        renderCell: (row, index) => (
          <PercentInput
            compact
            ariaLabel="Spread likelihood"
            placeholder="0"
            value={row.spreadLikelihood}
            onChange={(value) => update(index, { spreadLikelihood: value })}
          />
        ),
      },
      {
        header: "Limit Min",
        renderCell: (row, index) => (
          <WholeNumberInput
            compact
            ariaLabel="Spread limit minimum"
            value={row.spreadLimitMin}
            onChange={(value) => update(index, { spreadLimitMin: value })}
          />
        ),
      },
      {
        header: "Limit Max",
        renderCell: (row, index) => (
          <WholeNumberInput
            compact
            ariaLabel="Spread limit maximum"
            value={row.spreadLimitMax}
            onChange={(value) => update(index, { spreadLimitMax: value })}
          />
        ),
      },
    );
  }

  return (
    <div className="w-full">
      {/* The icon sits on the blurb's line and explains the value columns, which are not self-describing and change with the type's kind. */}
      <p className="mb-3 flex items-center gap-1.5 text-sm text-slate-500">
        Specify the habitats where this Map Resource can exist.
        <HintIcon hint={columnHintsFor(kind)} />
      </p>

      {kind === "stratified" ? (
        <p className="mb-2 text-xs text-slate-500">
          A stratified Map Resource only needs to know where it can exist: its quantities are on the Layers page.
        </p>
      ) : null}

      <EditableTable<MapResourceHabitat>
        rows={type.habitats}
        addLabel="Habitat"
        emptyHint='No habitats exist. Press the "+" in the top right of this table to add some.'
        addDisabled={nextBiome === undefined}
        onAddRow={() => setHabitats([...type.habitats, createHabitat(kind, nextBiome!)])}
        onRemoveRow={(index) => setHabitats(type.habitats.filter((_, i) => i !== index))}
        columns={columns}
      />
    </div>
  );
}
