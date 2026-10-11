import { FieldRow, WholeNumberSlider } from "@/components/ui";
import type { MapResourceType } from "@/types/map/MapResource";
import { PercentSlider } from "./mapResourceFields";

// Survival Rounds is dragged rather than typed: the useful range is small and known, and the server now takes anything from 0 up, so a slider
// cannot produce a value a save would refuse. 20 Rounds is well past anything the design calls for and keeps the knob's steps readable.
const SURVIVAL_ROUNDS_MIN = 0;
const SURVIVAL_ROUNDS_MAX = 20;

interface MapResourceRenewableSectionProps {
  type: MapResourceType;
  onChange: (updated: MapResourceType) => void;
}

// The exhaustion rules spelled out, rather than named. The field labels say *which* value this is but nothing about what
// happens when it runs out, and the two only make sense together: the reseed roll is taken once per surviving Round, so
// a designer setting either one needs to know both. Authored with explicit line breaks because the tooltip bubble uses
// whitespace-pre and never wraps on its own (see Tooltip).
const SURVIVAL_ROUNDS_HINT = [
  "How many Rounds an exhausted instance stays dormant before it is deleted for good?",
  "",
  "At 0 it is deleted the moment it is exhausted, never going dormant — so it is never",
  "rolled for reseeding, and the Reseed Likelihood below has nothing to apply to.",
].join("\n");

const RESEED_LIKELIHOOD_HINT = [
  "The chance, rolled once in each dormant Round, that an exhausted instance comes back.",
  "It returns at Growth % of its Maximum Quantity for the tile, then grows as normal.",
  "",
  "Each Round rolls separately, so the chance of recovering at all compounds over the",
  "Survival Rounds: 10% across 5 Rounds recovers about 41% of the time.",
].join("\n");

/** Replaces the hint above when nothing ever goes dormant, so the row says why it is greyed rather than leaving the designer to guess. */
const RESEED_NEVER_ROLLED_HINT = [
  "Not used while Survival Rounds is 0: an instance deleted the moment it is exhausted",
  "never has a dormant Round to be rolled in.",
  "",
  "The value is kept, because a renewable MapResourceType must carry one either way.",
].join("\n");

/**
 * What happens to an instance of a growing type once it is exhausted: how long it stays dormant, and its chance of coming back in each of
 * those Rounds.
 * <remarks>
 * Only the kinds that grow (Renewable and Distribution — both carry IsRenewable) have these, and the server insists the pair is set exactly
 * when IsRenewable is, so the dialog shows this page only for those kinds and withKind gives both a default on the way in. The intro line
 * restates that pairing, because the two fields are useless apart and this page is the only place either appears.
 * </remarks>
 */
export default function MapResourceRenewableSection({ type, onChange }: MapResourceRenewableSectionProps) {
  const set = <K extends keyof MapResourceType>(field: K, value: MapResourceType[K]) => onChange({ ...type, [field]: value });

  // 0 Survival Rounds means an exhausted instance is deleted on the spot, so there is never a dormant Round for the reseed roll to happen in.
  const neverDormant = type.survivalRounds === 0;

  return (
    <div className="w-full max-w-xl">
      <p className="mb-4 text-sm text-slate-500">An exhausted Map Resource instance has a chance of re-seeding itself.</p>

      <FieldRow label="Survival Rounds" hint={SURVIVAL_ROUNDS_HINT}>
        <WholeNumberSlider
          value={type.survivalRounds}
          min={SURVIVAL_ROUNDS_MIN}
          max={SURVIVAL_ROUNDS_MAX}
          unitLabel={type.survivalRounds === 1 ? "Round" : "Rounds"}
          ariaLabel="Survival Rounds"
          onChange={(value) => set("survivalRounds", value)}
        />
      </FieldRow>

      {/* Greyed rather than hidden or cleared when nothing goes dormant: the server still wants the pair set on a renewable type, so the value
          stays as it is and only stops meaning anything. Hiding the row would make the save's requirement invisible. */}
      <FieldRow label="Reseed Likelihood" hint={neverDormant ? RESEED_NEVER_ROLLED_HINT : RESEED_LIKELIHOOD_HINT}>
        <PercentSlider
          value={type.reseedLikelihood}
          disabled={neverDormant}
          ariaLabel="Reseed likelihood"
          onChange={(value) => set("reseedLikelihood", value)}
        />
      </FieldRow>

      {neverDormant ? (
        <p className="text-xs text-slate-500">With 0 Survival Rounds an exhausted instance is deleted at once, so it is never rolled to reseed.</p>
      ) : null}
    </div>
  );
}
