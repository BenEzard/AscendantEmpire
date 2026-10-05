import { useEffect, useState } from "react";
import { Alert, Button, FieldRow, Modal, inputClass } from "@/components/ui";
import type { MapResourceType } from "@/types/map/MapResource";
import type { GalleryMapResource, GalleryMapResourceSpecifics } from "../../api/galleryMapResourcesApi";
import { holdsSingleQuantity, kindOf } from "../../tools/mapResourceDraft";
import { habitatFor } from "../../tools/mapResourcePlacement";
import MapResourceIcon from "./MapResourceIcon";
import { WholeNumberInput } from "./mapResourceFields";

interface MapResourcePlacementDialogProps {
  /** The placement being edited; the dialog is closed while null. */
  placement: GalleryMapResource | null;
  /** Its type, from the catalogue. Undefined only if the catalogue has not loaded. */
  type: MapResourceType | undefined;
  /** The tile's Biome and Geography, to find the habitat whose range an empty starting quantity is rolled from. */
  biomeType: number | null;
  geographyType: number | null;
  /** How the tile reads, e.g. "Hilly Forest". */
  tileDescription: string;
  busy: boolean;
  errors: string[];
  onSave: (specifics: GalleryMapResourceSpecifics) => void;
  onDelete: () => void;
  onClose: () => void;
}

/**
 * The specifics of one MapResource placed on the map. What can be set depends on the type's kind: a starting quantity for a type holding a single
 * quantity, or a Distribution's scenario. Anything left empty is decided when a game is created - the quantity rolled from the habitat's range, the
 * scenario picked weighted by Biome. A stratified MapResource has nothing to set: its layers are always rolled.
 */
export default function MapResourcePlacementDialog({
  placement,
  type,
  biomeType,
  geographyType,
  tileDescription,
  busy,
  errors,
  onSave,
  onDelete,
  onClose,
}: MapResourcePlacementDialogProps) {
  const [initialQuantity, setInitialQuantity] = useState<number | null>(null);
  const [scenarioNumber, setScenarioNumber] = useState<number | null>(null);

  // Taken from the placement each time a different one opens, so a cancelled edit does not linger into the next.
  useEffect(() => {
    setInitialQuantity(placement?.initialQuantity ?? null);
    setScenarioNumber(placement?.distributionScenarioNumber ?? null);
  }, [placement]);

  const kind = type ? kindOf(type) : null;
  const habitat = type ? habitatFor(type, biomeType, geographyType) : undefined;

  const handleSave = () => {
    onSave({
      initialQuantity: kind && holdsSingleQuantity(kind) ? initialQuantity : null,
      distributionScenarioNumber: kind === "distribution" ? scenarioNumber : null,
    });
  };

  return (
    <Modal
      open={placement != null}
      title="Map Resource"
      size="md"
      onClose={() => {
        if (!busy) onClose();
      }}
      footer={
        <>
          <div className="mr-auto">
            <Button variant="danger" disabled={busy} onClick={onDelete}>
              Delete
            </Button>
          </div>
          <Button variant="secondary" disabled={busy} onClick={onClose}>
            Cancel
          </Button>
          <Button variant="success" disabled={busy || kind === "stratified"} onClick={handleSave}>
            {busy ? "Saving..." : "Save"}
          </Button>
        </>
      }>
      {placement ? (
        <div className="w-full">
          <div className="mb-4 flex items-center gap-3">
            <MapResourceIcon iconPath={type?.iconPath ?? null} size={32} />
            <div>
              <div className="font-semibold">{type?.displayName ?? `MapResource ${placement.mapResourceTypeId}`}</div>
              <div className="text-xs text-slate-500">
                Tile ({placement.xCoord}, {placement.yCoord}) · {tileDescription} · slot {placement.slot}
              </div>
            </div>
          </div>

          {errors.length > 0 ? (
            <Alert variant="danger">
              <ul className="list-disc pl-5">
                {errors.map((message, i) => (
                  <li key={i}>{message}</li>
                ))}
              </ul>
            </Alert>
          ) : null}

          {kind && holdsSingleQuantity(kind) ? (
            <>
              <FieldRow label="Starting Quantity" hint="(Empty: rolled per game)">
                <WholeNumberInput
                  value={initialQuantity}
                  ariaLabel="Starting quantity"
                  placeholder={
                    habitat?.initialQuantityMin != null && habitat.initialQuantityMax != null
                      ? `Roll ${habitat.initialQuantityMin}–${habitat.initialQuantityMax}`
                      : "Roll per game"
                  }
                  onChange={setInitialQuantity}
                />
              </FieldRow>
              {kind === "renewable" && habitat?.maximumQuantity != null ? (
                <p className="text-xs text-slate-500">Grows to at most {habitat.maximumQuantity} here.</p>
              ) : null}
            </>
          ) : null}

          {kind === "distribution" && type ? (
            <FieldRow label="Scenario">
              <select
                className={inputClass}
                value={scenarioNumber ?? ""}
                onChange={(e) => setScenarioNumber(e.target.value === "" ? null : Number(e.target.value))}>
                <option value="">Picked per game (weighted by Biome)</option>
                {type.distributionScenarios.map((scenario) => (
                  <option key={scenario.scenarioNumber} value={scenario.scenarioNumber}>
                    {`Scenario ${scenario.scenarioNumber}${scenario.displayName ? ` – ${scenario.displayName}` : ""}`}
                  </option>
                ))}
              </select>
            </FieldRow>
          ) : null}

          {kind === "stratified" ? (
            <p className="text-sm text-slate-600">A stratified MapResource's layers are rolled when each game is created, so there is nothing to set here.</p>
          ) : null}
        </div>
      ) : null}
    </Modal>
  );
}
