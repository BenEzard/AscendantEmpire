import { FieldRow } from "@/components/ui";
import type { MapResourceType } from "@/types/map/MapResource";
import { PercentSlider, WholeNumberInput } from "./mapResourceFields";

interface MapResourceRenewableSectionProps {
  type: MapResourceType;
  onChange: (updated: MapResourceType) => void;
}

// The exhaustion rules spelled out, rather than named. The field labels say *which* value this is but nothing about what
// happens when it runs out, and the two only make sense together: the reseed roll is taken once per surviving Round, so
// a designer setting either one needs to know both. Authored with explicit line breaks because the tooltip bubble uses
// whitespace-pre and never wraps on its own (see Tooltip).
const SURVIVAL_ROUNDS_HINT = [
  "How many Rounds an exhausted instance stays dormant before it is deleted for good.",
  "It takes one Reseed Likelihood roll in each of those Rounds; once they have all failed,",
  "the instance is removed and its tile slot freed. There is no further Round after the last.",
  "",
  "While dormant it still holds one of the tile's two slots, so it keeps other MapResources",
  "from spreading onto the tile.",
].join("\n");

const RESEED_LIKELIHOOD_HINT = [
  "The chance, rolled once in each dormant Round, that an exhausted instance comes back.",
  "It returns at Growth % of its Maximum Quantity for the tile, then grows as normal.",
  "",
  "Each Round rolls separately, so the chance of recovering at all compounds over the",
  "Survival Rounds: 10% across 5 Rounds recovers about 41% of the time.",
  "",
  "A neighbouring instance of the same type spreading onto the tile revives it outright,",
  "without a roll.",
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

  return (
    <div className="w-full max-w-xl">
      <p className="mb-4 text-sm text-slate-500">
        An exhausted instance goes dormant rather than vanishing, holding its tile slot and taking one reseed roll in each Round it survives.
      </p>

      <FieldRow label="Survival Rounds" hint={SURVIVAL_ROUNDS_HINT}>
        <WholeNumberInput value={type.survivalRounds} onChange={(value) => set("survivalRounds", value)} />
      </FieldRow>

      <FieldRow label="Reseed Likelihood" hint={RESEED_LIKELIHOOD_HINT}>
        <PercentSlider value={type.reseedLikelihood} ariaLabel="Reseed likelihood" onChange={(value) => set("reseedLikelihood", value)} />
      </FieldRow>
    </div>
  );
}
