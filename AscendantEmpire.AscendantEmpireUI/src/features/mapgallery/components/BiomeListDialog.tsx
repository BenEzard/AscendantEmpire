import { Button, Modal } from "@/components/ui";
import { useMemo } from "react";
import type { Biome } from "@/types/map/Biome";
import { isValidColor } from "@/util/color";
import { toRegenerationPercentInput } from "@/util/regenerationPercent";

interface BiomeListDialogProps {
  show: boolean;
  biomes: Biome[];
  onClose: () => void;
}

/** Shown in place of a value the biome doesn't have, matching how the other read-only tables render an absent number. */
const EMPTY_VALUE = "—";

/** The biome fields holding a plain numeric value, which is all the resource columns ever read. */
type NumericBiomeField = { [K in keyof Biome]: Biome[K] extends number | null ? K : never }[keyof Biome];

/** The seven resource yields, in the order the table lays them out after the Regeneration Percent column. */
const RESOURCE_COLUMNS: ReadonlyArray<{ header: string; field: NumericBiomeField }> = [
  { header: "Forage", field: "baseForageValue" },
  { header: "Hunt", field: "baseHuntValue" },
  { header: "Fish", field: "baseFishValue" },
  { header: "Fodder", field: "baseFodderValue" },
  { header: "Bone", field: "baseBoneValue" },
  { header: "Wood", field: "baseWoodValue" },
  { header: "Stone", field: "baseStoneValue" },
];

/** Renders a nullable yield, falling back to an em dash when the biome doesn't produce that resource at all. */
function formatResourceValue(value: number | null): string {
  return value == null ? EMPTY_VALUE : String(value);
}

/**
 * A read-only reference table of the biome catalogue, opened from the Map Editor's Biomes button. The catalogue itself is
 * authored in the Entity Setup Tool, so nothing here is editable - this exists purely so a map designer can look up what a
 * biome yields without leaving the editor.
 *
 * Hand-rolls its table rather than reusing SearchTable (dark-themed, and built around an activatable row) or EditableTable
 * (built around add/remove rows), neither of which suits a fixed read-only grid on the modal's white panel.
 */
export default function BiomeListDialog({ show, biomes, onClose }: BiomeListDialogProps) {
  // Fixed alphabetical order - the table offers no sorting or filtering of its own.
  const orderedBiomes = useMemo(() => biomes.slice().sort((a, b) => a.name.localeCompare(b.name)), [biomes]);

  return (
    <Modal
      open={show}
      title="Biomes"
      size="xl"
      onClose={onClose}
      footer={
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      }>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <th className="px-2 py-2">Colour</th>
              <th className="px-2 py-2">Name</th>
              <th className="px-2 py-2 text-right whitespace-nowrap">Regeneration %</th>
              {RESOURCE_COLUMNS.map((column) => (
                <th key={column.field} className="px-2 py-2 text-right">
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>

          <tbody>
            {orderedBiomes.length === 0 ? (
              <tr>
                <td className="px-2 py-4 text-slate-500" colSpan={3 + RESOURCE_COLUMNS.length}>
                  No biomes.
                </td>
              </tr>
            ) : (
              orderedBiomes.map((biome) => {
                // An unset or unparseable colour still needs a swatch, so fall back to white rather than leaving a gap.
                const swatchColor = isValidColor(biome.mapEditorHtmlColor) ? biome.mapEditorHtmlColor : "#ffffff";

                // Stored as a fraction (0.25) but read as a percent (25); "" when the biome has nothing to regrow.
                const regenerationPercent = toRegenerationPercentInput(biome.regenerationPercent);

                return (
                  <tr key={biome.biomeType} className="border-b border-slate-100">
                    <td className="px-2 py-1.5">
                      <span
                        className="inline-block h-3.5 w-3.5 shrink-0 rounded-sm border border-slate-400"
                        style={{ backgroundColor: swatchColor }}
                        aria-hidden="true"
                      />
                    </td>
                    <td className="px-2 py-1.5">{biome.name}</td>
                    <td className="px-2 py-1.5 text-right whitespace-nowrap">
                      {regenerationPercent === "" ? EMPTY_VALUE : `${regenerationPercent}%`}
                    </td>
                    {RESOURCE_COLUMNS.map((column) => (
                      <td key={column.field} className="px-2 py-1.5 text-right">
                        {formatResourceValue(biome[column.field])}
                      </td>
                    ))}
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </Modal>
  );
}
