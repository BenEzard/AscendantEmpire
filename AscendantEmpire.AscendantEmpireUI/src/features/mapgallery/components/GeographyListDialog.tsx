import { Button, Modal } from "@/components/ui";
import { useMemo } from "react";
import type { Geography } from "@/types/map/Geography";
import { iconMap } from "./mapEditorIcons";

interface GeographyListDialogProps {
  show: boolean;
  geographies: Geography[];
  onClose: () => void;
}

/**
 * A read-only reference table of the geography catalogue, opened from the Map Editor's Geographies button. The catalogue
 * itself is authored in the Entity Setup Tool, so nothing here is editable - this exists purely so a map designer can look
 * up what a geography costs and does without leaving the editor.
 *
 * Hand-rolls its table for the same reasons as BiomeListDialog: the shared tables are dark-themed and built around
 * activatable or add/remove rows, neither of which suits a fixed read-only grid on the modal's white panel.
 */
export default function GeographyListDialog({ show, geographies, onClose }: GeographyListDialogProps) {
  // Fixed alphabetical order - the table offers no sorting or filtering of its own.
  const orderedGeographies = useMemo(() => geographies.slice().sort((a, b) => a.name.localeCompare(b.name)), [geographies]);

  return (
    <Modal
      open={show}
      title="Geographies"
      size="md"
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
              <th className="px-2 py-2 text-center">Icon</th>
              <th className="px-2 py-2">Name</th>
              <th className="px-2 py-2 text-center whitespace-nowrap">Movement Cost</th>
              <th className="px-2 py-2 text-center">Water</th>
              <th className="px-2 py-2 text-center">Traversable</th>
            </tr>
          </thead>

          <tbody>
            {orderedGeographies.length === 0 ? (
              <tr>
                <td className="px-2 py-4 text-slate-500" colSpan={5}>
                  No geographies.
                </td>
              </tr>
            ) : (
              orderedGeographies.map((geography) => {
                // The stored icon is a key into the editor's icon map; an unknown or empty key simply shows nothing,
                // exactly as it does on the map itself.
                const iconKey = (geography.mapEditorIcon ?? "").trim();
                const Icon = iconKey ? iconMap[iconKey] : undefined;

                return (
                  <tr key={geography.campaignMapGeographyType} className="border-b border-slate-100">
                    <td className="px-2 py-1.5 text-center">
                      <span className="inline-flex h-3.5 w-3.5 items-center justify-center">
                        {Icon ? <Icon aria-hidden size={14} className="text-slate-700" /> : null}
                      </span>
                    </td>
                    <td className="px-2 py-1.5">{geography.name}</td>
                    <td className="px-2 py-1.5 text-center">{geography.movementCost}</td>
                    {/*
                      The two flags that govern what the ground does, beside the cost that no longer answers either question: water
                      decides which movement domains may enter and which Habitats a water resource may be authored against, and
                      traversable decides whether anything can enter at all.
                    */}
                    <td className="px-2 py-1.5 text-center">{geography.isWater ? "Yes" : "No"}</td>
                    <td className="px-2 py-1.5 text-center">{geography.isTraversable ? "Yes" : "No"}</td>
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
