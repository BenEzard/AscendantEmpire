import { HiTrash } from "react-icons/hi2";
import { Alert, FieldRow, Tooltip, inputClass } from "@/components/ui";
import { formatNumberWithCommas } from "@/util/number";
import type { MapResourceType } from "@/types/map/MapResource";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";
import type { GalleryMapResource, GalleryMapResourceSpecifics } from "../../api/galleryMapResourcesApi";
import { holdsSingleQuantity, kindOf } from "../../tools/mapResourceDraft";
import { habitatFor } from "../../tools/mapResourcePlacement";
import MapResourceIcon from "@/components/map/MapResourceIcon";
import { WholeNumberInput } from "./mapResourceFields";

interface MapResourcePlacementSectionProps {
  placement: GalleryMapResource;
  /** Its type, from the catalogue. Undefined only if the catalogue has not loaded. */
  type: MapResourceType | undefined;
  /** The tile's Biome and Geography, to find the habitat whose range an empty starting quantity is rolled from. */
  biomeType: BiomeType | null;
  geographyType: CampaignMapGeographyType | null;
  /** The edited values, held by the dialog so Save can send every section at once. */
  specifics: GalleryMapResourceSpecifics;
  onChange: (specifics: GalleryMapResourceSpecifics) => void;
  /** Whatever the server said about this placement alone. */
  errors: string[];
  busy: boolean;
  onDelete: () => void;
}

/**
 * One MapResource on the tile: its name, what can be set on it, and its own Delete.
 *
 * What can be set depends on the type's kind — a starting quantity for a type holding a single quantity, a Distribution's scenario, and nothing at all
 * for a stratified one. Anything left empty is decided when a game is created.
 *
 * Delete sits here rather than in the dialog's footer because a tile can hold two resources, and a footer button could not say which one it meant.
 */
export default function MapResourcePlacementSection({
  placement,
  type,
  biomeType,
  geographyType,
  specifics,
  onChange,
  errors,
  busy,
  onDelete,
}: MapResourcePlacementSectionProps) {
  const kind = type ? kindOf(type) : null;
  const habitat = type ? habitatFor(type, biomeType, geographyType) : undefined;
  const name = type?.displayName ?? `MapResource ${placement.mapResourceTypeId}`;

  return (
    <section aria-label={name}>
      <div className="mb-3 flex items-center gap-3">
        <MapResourceIcon iconPath={type?.iconPath ?? null} size={40} />
        <div className="min-w-0">
          <div className="font-semibold text-slate-800">{name}</div>
          <div className="text-xs text-slate-500">Slot {placement.slot}</div>
        </div>
        {/* ml-auto belongs on the Tooltip, not the button: the Tooltip wraps its child in a span, and that span is the flex item. */}
        <Tooltip content={`Remove ${name} from this tile`} className="ml-auto">
          <button
            type="button"
            className="text-slate-400 hover:text-[var(--bs-danger)] focus:outline-none disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:text-slate-400"
            aria-label={`Delete ${name}`}
            disabled={busy}
            onClick={onDelete}>
            <HiTrash size={18} aria-hidden />
          </button>
        </Tooltip>
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
              value={specifics.initialQuantity}
              ariaLabel={`Starting quantity for ${type?.displayName ?? "this MapResource"}`}
              placeholder={
                habitat?.initialQuantityMin != null && habitat.initialQuantityMax != null
                  ? `Roll ${formatNumberWithCommas(habitat.initialQuantityMin)}–${formatNumberWithCommas(habitat.initialQuantityMax)}`
                  : "Roll per game"
              }
              onChange={(initialQuantity) => onChange({ ...specifics, initialQuantity })}
            />
          </FieldRow>
          {kind === "renewable" && habitat?.maximumQuantity != null ? (
            <p className="text-xs text-slate-500">Grows to at most {formatNumberWithCommas(habitat.maximumQuantity)} here.</p>
          ) : null}
        </>
      ) : null}

      {kind === "distribution" && type ? (
        <FieldRow label="Scenario">
          <select
            className={inputClass}
            aria-label={`Scenario for ${type.displayName}`}
            value={specifics.distributionScenarioNumber ?? ""}
            onChange={(e) => onChange({ ...specifics, distributionScenarioNumber: e.target.value === "" ? null : Number(e.target.value) })}>
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
    </section>
  );
}
