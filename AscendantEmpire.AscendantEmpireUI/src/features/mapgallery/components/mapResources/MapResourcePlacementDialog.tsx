import { useEffect, useState } from "react";
import { Button, Modal } from "@/components/ui";
import type { MapResourceType } from "@/types/map/MapResource";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";
import type { GalleryMapResource, GalleryMapResourceSpecifics } from "../../api/galleryMapResourcesApi";
import { holdsSingleQuantity, kindOf } from "../../tools/mapResourceDraft";
import { hasSettableSpecifics } from "../../tools/mapResourcePlacement";
import MapResourcePlacementSection from "./MapResourcePlacementSection";

/** One placement's specifics, as Save hands them back for writing. */
export type MapResourcePlacementEdit = {
  placementId: number;
  specifics: GalleryMapResourceSpecifics;
};

interface MapResourcePlacementDialogProps {
  /** The tile being edited; the dialog is closed while null. */
  tile: { x: number; y: number } | null;
  /** Every MapResource on that tile, ordered by slot — one or two of them, and empty while the dialog is closed. */
  placements: GalleryMapResource[];
  /** The catalogue by type id, for each placement's name, icon and kind. */
  typesById: ReadonlyMap<number, MapResourceType>;
  /** The tile's Biome and Geography, to find the habitat whose range an empty starting quantity is rolled from. */
  biomeType: BiomeType | null;
  geographyType: CampaignMapGeographyType | null;
  busy: boolean;
  /** What the server said, by placement id, so a refusal shows against the resource it refused. */
  errors: ReadonlyMap<number, string[]>;
  onSave: (edits: MapResourcePlacementEdit[]) => void;
  onDelete: (placementId: number) => void;
  onClose: () => void;
}

/**
 * The MapResources on one tile, a section each, with the specifics of every one editable at once.
 *
 * Scoped to the tile rather than to a single placement because a tile holds at most two MapResources, so both fit without a chooser or a list. Save
 * writes only the sections whose values differ from the server's, so editing one of two resources stays a single request.
 */
export default function MapResourcePlacementDialog({
  tile,
  placements,
  typesById,
  biomeType,
  geographyType,
  busy,
  errors,
  onSave,
  onDelete,
  onClose,
}: MapResourcePlacementDialogProps) {
  const [drafts, setDrafts] = useState<ReadonlyMap<number, GalleryMapResourceSpecifics>>(new Map());

  // Seeded per placement, keeping any edit already in progress: the placements refetch after every write, and a refetch must not discard what is
  // being typed in the other section. A closed dialog is handed no placements, so closing clears the drafts and a cancelled edit cannot linger
  // into the next tile.
  useEffect(() => {
    setDrafts((previous) => {
      const next = new Map<number, GalleryMapResourceSpecifics>();

      for (const placement of placements) {
        next.set(
          placement.id,
          previous.get(placement.id) ?? {
            initialQuantity: placement.initialQuantity,
            distributionScenarioNumber: placement.distributionScenarioNumber,
          },
        );
      }

      // Same placements, same drafts: keep the old map so re-seeding cannot itself cause another render.
      const unchanged = next.size === previous.size && [...next].every(([id, specifics]) => previous.get(id) === specifics);

      return unchanged ? previous : next;
    });
  }, [placements]);

  // Only the values the kind actually uses are sent; the rest go as null, as a placement left to the game to decide does.
  const specificsFor = (placement: GalleryMapResource): GalleryMapResourceSpecifics => {
    const draft = drafts.get(placement.id);
    const type = typesById.get(placement.mapResourceTypeId);
    const kind = type ? kindOf(type) : null;

    return {
      initialQuantity: kind != null && holdsSingleQuantity(kind) ? (draft?.initialQuantity ?? null) : null,
      distributionScenarioNumber: kind === "distribution" ? (draft?.distributionScenarioNumber ?? null) : null,
    };
  };

  const handleSave = () => {
    const edits = placements
      .map((placement) => ({ placement, specifics: specificsFor(placement) }))
      .filter(
        ({ placement, specifics }) =>
          specifics.initialQuantity !== placement.initialQuantity || specifics.distributionScenarioNumber !== placement.distributionScenarioNumber,
      )
      .map(({ placement, specifics }) => ({ placementId: placement.id, specifics }));

    onSave(edits);
  };

  // A tile holding nothing but stratified MapResources has no values to write, so Save would have nothing to do.
  const anythingToSave = placements.some((placement) => hasSettableSpecifics(typesById.get(placement.mapResourceTypeId)));

  return (
    <Modal
      open={tile != null}
      title={tile ? `Map Resources (Tile ${tile.x}, ${tile.y})` : "Map Resources"}
      size="md"
      onClose={() => {
        if (!busy) {
          onClose();
        }
      }}
      footer={
        <>
          <Button variant="secondary" disabled={busy} onClick={onClose}>
            Cancel
          </Button>
          <Button variant="success" disabled={busy || !anythingToSave} onClick={handleSave}>
            {busy ? "Saving..." : "Save"}
          </Button>
        </>
      }>
      <div className="w-full">
        {placements.map((placement, index) => (
          <div key={placement.id} className={index > 0 ? "mt-4 border-t border-slate-200 pt-4" : ""}>
            <MapResourcePlacementSection
              placement={placement}
              type={typesById.get(placement.mapResourceTypeId)}
              biomeType={biomeType}
              geographyType={geographyType}
              specifics={drafts.get(placement.id) ?? { initialQuantity: placement.initialQuantity, distributionScenarioNumber: placement.distributionScenarioNumber }}
              onChange={(specifics) => setDrafts((previous) => new Map(previous).set(placement.id, specifics))}
              errors={errors.get(placement.id) ?? []}
              busy={busy}
              onDelete={() => onDelete(placement.id)}
            />
          </div>
        ))}
      </div>
    </Modal>
  );
}
