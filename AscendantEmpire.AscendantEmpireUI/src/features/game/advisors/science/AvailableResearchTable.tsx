import { FaCheck } from "react-icons/fa6";

import { Tooltip } from "@/components/ui";
import AssetIconImage from "@/features/game/assets/AssetIconImage";
import type { ResearchableTechnology, ResearchGrant } from "./scienceApi";

/**
 * The three research buckets, in bar order, each with the colour it is drawn in and what it is in one line.
 *
 * Colour is the only thing telling the three segments apart inside the bar, so every segment also carries a tooltip and a figure underneath
 * it — the bar is a summary, never the sole carrier of the numbers.
 */
const BUCKETS = [
  {
    key: "directed",
    label: "Directed",
    // Blue, green and yellow as specified. Fixed hex rather than palette tokens, because the three have to stay distinguishable from each
    // other rather than tracking any one semantic colour.
    filledColour: "#2563eb",
    emptyColour: "#bfdbfe",
    description: "Directed research — the deliberate line of study you choose.",
  },
  {
    key: "experience",
    label: "Experience",
    filledColour: "#16a34a",
    emptyColour: "#bbf7d0",
    description: "Experience — earned passively by doing the kinds of things this technology is about.",
  },
  {
    key: "diffusion",
    label: "Diffusion",
    filledColour: "#ca8a04",
    emptyColour: "#fef08a",
    description: "Diffusion — picked up passively through trade and proximity to other empires.",
  },
] as const;

type BucketKey = (typeof BUCKETS)[number]["key"];

/** The requirement and the progress for one bucket. A null requirement means the technology does not use that bucket at all. */
function bucketFigures(technology: ResearchableTechnology, bucket: BucketKey): { required: number | null; points: number } {
  switch (bucket) {
    case "directed":
      return { required: technology.directedRequired, points: technology.directedPoints };

    case "experience":
      return { required: technology.experienceRequired, points: technology.experiencePoints };

    case "diffusion":
      return { required: technology.diffusionRequired, points: technology.diffusionPoints };
  }
}

/** One bucket that the technology actually uses, ready to draw. */
interface ActiveBucket {
  key: BucketKey;
  label: string;
  filledColour: string;
  emptyColour: string;
  description: string;
  required: number;
  points: number;
  /** This bucket's share of the whole bar, as a percentage of the technology's total requirement across its used buckets. */
  widthPercent: number;
  /** How much of this bucket's own segment is filled, as a percentage of the segment. */
  filledPercent: number;
}

/**
 * The buckets a technology uses, sized against each other.
 *
 * **A bucket the technology does not use is absent from the bar entirely**, not a zero-width segment and not an empty one. That is the
 * sharpest point in the data model — an unused bucket has no requirement at all rather than a requirement of zero — and a bar that showed a
 * flat green stripe for "no experience needed" would say the opposite of what is true.
 *
 * Segment widths are each bucket's share of the technology's total requirement, so a technology needing 500 directed and 250 experience
 * draws two thirds blue and one third green. Each segment then fills left to right as its own points accumulate.
 */
function activeBuckets(technology: ResearchableTechnology): ActiveBucket[] {
  const used = BUCKETS.flatMap((bucket) => {
    const { required, points } = bucketFigures(technology, bucket.key);

    // Strictly null, not falsy: a requirement of zero is a real figure the model can express, and it must not be dropped as if unused.
    if (required === null) {
      return [];
    }

    return [{ ...bucket, required, points }];
  });

  const totalRequired = used.reduce((running, bucket) => running + bucket.required, 0);

  return used.map((bucket) => ({
    ...bucket,
    // An even split when every requirement is zero, so the bar still shows which buckets are in play rather than collapsing to nothing.
    widthPercent: totalRequired === 0 ? 100 / used.length : (bucket.required / totalRequired) * 100,
    filledPercent: bucket.required === 0 ? 100 : Math.min(bucket.points / bucket.required, 1) * 100,
  }));
}

/**
 * The stacked research bar, plus the figures beneath each segment.
 *
 * Laid out as a grid rather than a flex row, and both rows share one track definition, so each figure stays under the segment it describes
 * however the segments are sized. The grid is also what lets each segment carry the app's own Tooltip: Tooltip wraps its child in a span, and
 * a wrapper in a flex row would make the segment's width resolve against the span instead of the bar and collapse it — where a grid track
 * sizes whatever item lands in it, wrapper included.
 */
function ResearchProgressBar({ technology }: { technology: ResearchableTechnology }) {
  const buckets = activeBuckets(technology);

  // Guarded by the database constraint CK_Technology_AtLeastOneBucket, so this is a broken row rather than a state to design for — said
  // plainly instead of drawing an empty bar that would read as "no research needed".
  if (buckets.length === 0) {
    return (
      <p className="text-xs text-[var(--bs-danger)]" data-testid={`research-no-buckets-${technology.technologyId}`}>
        This technology requires no research at all, which should not be possible.
      </p>
    );
  }

  // One track per used bucket, sized by that bucket's share. Shared by the bar and the figures below it, so the two can never drift apart.
  const gridTemplateColumns = buckets.map((bucket) => `${bucket.widthPercent}fr`).join(" ");

  return (
    <div className="flex flex-col gap-1" data-testid={`research-bar-${technology.technologyId}`}>
      <div className="grid h-3 overflow-hidden rounded" style={{ gridTemplateColumns }} data-testid={`research-bar-track-${technology.technologyId}`}>
        {buckets.map((bucket) => (
          // h-full on the wrapper as well as the segment: the wrapper is the grid item, and it has to fill the row before the segment inside
          // it has a height to fill in turn.
          <Tooltip
            key={bucket.key}
            className="h-full"
            content={`${bucket.description} ${bucket.points.toLocaleString()} of ${bucket.required.toLocaleString()}.`}>
            <div
              className="h-full w-full"
              style={{ backgroundColor: bucket.emptyColour }}
              data-testid={`research-bar-segment-${technology.technologyId}-${bucket.key}`}>
              {/* The filled part of this bucket's own segment. Grows as research accumulates, so the whole bar fills as the technology
                  approaches discovery. */}
              <div className="h-full" style={{ width: `${bucket.filledPercent}%`, backgroundColor: bucket.filledColour }} />
            </div>
          </Tooltip>
        ))}
      </div>

      <div className="grid text-[10px] leading-none" style={{ gridTemplateColumns }}>
        {buckets.map((bucket) => (
          <div
            key={bucket.key}
            className="overflow-hidden text-center whitespace-nowrap text-slate-600"
            data-testid={`research-bucket-${technology.technologyId}-${bucket.key}`}>
            {bucket.points.toLocaleString()} / {bucket.required.toLocaleString()}
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * What a technology costs to unlock, as one icon per Asset with its quantity beneath.
 *
 * Free is stated in words rather than left as an empty row: "no acquisition cost" is what lets a technology be chosen at all right now, so
 * an empty space would read as information the server failed to send.
 */
function AcquisitionCosts({ technology }: { technology: ResearchableTechnology }) {
  if (technology.acquisitionCosts.length === 0) {
    return <span className="text-xs text-slate-500">Free</span>;
  }

  return (
    <div className="flex flex-wrap items-start gap-1">
      {technology.acquisitionCosts.map((cost) => (
        <Tooltip key={cost.assetId} content={`${cost.quantity.toLocaleString()} × ${cost.assetDisplayName}`}>
          {/* A button in shape rather than in function: it carries the icon and the quantity as one unit and is not pressable, because
              paying a cost is not yet something a player does — the server refuses any technology that carries one. */}
          <span className="flex w-11 flex-col items-center gap-0.5 rounded border border-slate-200 bg-slate-50 px-1 py-1">
            <AssetIconImage iconPath={cost.assetIconPath} size={20} alt={cost.assetDisplayName} />
            <span className="font-mono text-[10px] leading-none text-slate-700">{cost.quantity.toLocaleString()}</span>
          </span>
        </Tooltip>
      ))}
    </div>
  );
}

/** How a grant reads: the thing it names, or its raw target when the catalogue row it pointed at has gone. */
function grantLabel(grant: ResearchGrant): string {
  const target = grant.targetDisplayName ?? `${grant.targetType} ${grant.targetId} (no longer exists)`;

  // Obsolete is called out; Unlock is the ordinary case and needs no prefix.
  return grant.grantEffect === "Obsolete" ? `Retires ${target}` : target;
}

/**
 * What discovering the technology gives.
 *
 * Nothing in the seeded catalogue authors a grant — what a technology gives is carried as prose in its description for now — so the empty
 * case is the one every card hits today, and it says so rather than leaving the row blank.
 */
function Grants({ technology }: { technology: ResearchableTechnology }) {
  if (technology.grants.length === 0) {
    return <span className="text-xs text-slate-400 italic">Not yet recorded</span>;
  }

  return (
    <div className="flex flex-wrap gap-1">
      {technology.grants.map((grant) => (
        <span
          key={`${grant.targetType}-${grant.targetId}`}
          className={`rounded px-1.5 py-0.5 text-[11px] ${
            grant.grantEffect === "Obsolete" ? "bg-amber-50 text-amber-800" : "bg-emerald-50 text-emerald-800"
          }`}>
          {grantLabel(grant)}
        </span>
      ))}
    </div>
  );
}

/**
 * A label and its row of content, so the rows below the bar line up as a pair of columns.
 *
 * Centred rather than top-aligned, because the content is a different height in every row — a line of text for a free technology, a row of
 * 40px icon tiles for one that costs something, a wrap of chips for grants. Aligning to the top left the single-line cases sitting visibly
 * above their label; centring lines them up whatever the row holds.
 */
function CardRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2">
      <span className="w-12 shrink-0 text-xs font-medium text-slate-500">{label}</span>
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}

export interface AvailableResearchTableProps {
  /** The researchable technologies, in the order the server returned them — array order is render order. */
  technologies: ResearchableTechnology[];
  /** Called with the technology to research. */
  onSelect: (technologyId: number) => void;
  /** The technology a selection is in flight for, so only its own button shows as busy. */
  selectingTechnologyId?: number | null;
}

/**
 * The technologies the player could research now, as a grid of cards.
 *
 * Presentational: data in, a callback out, no fetching and no state of its own. That is what makes it testable, since this project has no
 * network mocking — a component that loaded its own data could only be exercised through the hook.
 *
 * Every card here is researchable by definition. Locked and Discovered technologies never arrive, because the endpoint serves only what the
 * player can choose between, so there is no filtering or greying-out to do.
 */
export default function AvailableResearchTable({ technologies, onSelect, selectingTechnologyId = null }: AvailableResearchTableProps) {
  if (technologies.length === 0) {
    return (
      <p className="p-4 text-sm text-slate-500" data-testid="available-research-empty">
        Nothing can be researched yet. Every technology is still waiting on prerequisites you have not discovered.
      </p>
    );
  }

  return (
    // Four across once there is room for it, stepping down rather than letting the cards squeeze: at four columns a card is about 330px in
    // the dialog's width, which is the narrowest the title, the bar figures and a row of cost icons all stay legible at.
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4" data-testid="available-research-grid">
      {technologies.map((technology) => {
        const isSelecting = selectingTechnologyId === technology.technologyId;

        return (
          <div
            key={technology.technologyId}
            className={`flex flex-col gap-2 rounded-lg border bg-white p-3 ${
              technology.isCurrentProject ? "border-sky-400 ring-1 ring-sky-200" : "border-slate-200"
            }`}
            data-testid={`research-card-${technology.technologyId}`}>
            {/* Title and the select tick on one line, so the action sits with the thing it acts on however tall the card grows. */}
            <div className="flex items-start gap-2">
              {/* The display name alone. The code is the catalogue's match key rather than something a player needs, so it stays out of the
                  card — it is still served on the row for anything that has to identify a technology precisely. */}
              <div className="min-w-0 flex-1 truncate text-sm font-medium text-slate-900" title={technology.displayName}>
                {technology.displayName}
              </div>

              {/* The current project is marked rather than offered again: choosing it a second time would be a no-op the player could only
                  read as the button not working. */}
              {technology.isCurrentProject ? (
                <span className="shrink-0 text-xs font-medium text-sky-600" data-testid={`research-current-${technology.technologyId}`}>
                  Researching
                </span>
              ) : (
                // shrink-0 goes on the Tooltip wrapper as well as the button, because the wrapper is what the header row sees as its flex
                // item once the tooltip is in place.
                <Tooltip content={`Research ${technology.displayName}`} className="shrink-0">
                  <button
                    type="button"
                    className="shrink-0 rounded-md border border-slate-300 p-1.5 text-slate-600 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                    // One request at a time: a second press mid-flight would race the first, and the player would have no way to tell which
                    // choice won.
                    disabled={selectingTechnologyId !== null}
                    aria-label={`Research ${technology.displayName}`}
                    aria-busy={isSelecting}
                    onClick={() => onSelect(technology.technologyId)}
                    data-testid={`research-select-${technology.technologyId}`}>
                    <FaCheck className={isSelecting ? "animate-pulse" : undefined} />
                  </button>
                </Tooltip>
              )}
            </div>

            <ResearchProgressBar technology={technology} />

            <CardRow label="Cost:">
              <AcquisitionCosts technology={technology} />
            </CardRow>

            <CardRow label="Grants:">
              <Grants technology={technology} />
            </CardRow>
          </div>
        );
      })}
    </div>
  );
}
