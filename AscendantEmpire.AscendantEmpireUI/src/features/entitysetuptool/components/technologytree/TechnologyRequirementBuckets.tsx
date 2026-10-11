/** What each bucket is, in one line, for the hint beside its name. */
const BUCKET_DESCRIPTIONS = {
  directed: "Active research — the deliberate line of study a player chooses.",
  experience: "Passive — earned by doing the kinds of things this technology is about.",
  diffusion: "Passive — picked up through trade and proximity to other empires.",
} as const;

export interface TechnologyRequirementBucketsProps {
  directedRequired: number | null;
  experienceRequired: number | null;
  diffusionRequired: number | null;
  /** How many experience types feed this technology, which is what decides whether its experience bucket can ever fill. */
  experienceSourceCount: number;
}

/**
 * The three research buckets and what each one requires.
 *
 * Its own component, and its own test, because of one distinction: **an unused bucket has no requirement at all, rather than a requirement of zero.** That is
 * the sharpest point in the whole data model and the easiest one for a renderer to destroy — a bucket printed as "0" reads as "free", which is the opposite
 * of "not part of this technology". So an unused bucket is spelled out in words here, and never as a zero, a blank or a dash.
 */
export default function TechnologyRequirementBuckets({
  directedRequired,
  experienceRequired,
  diffusionRequired,
  experienceSourceCount,
}: TechnologyRequirementBucketsProps) {
  const rows = [
    { key: "directed", label: "Directed research", required: directedRequired, description: BUCKET_DESCRIPTIONS.directed },
    { key: "experience", label: "Experience", required: experienceRequired, description: BUCKET_DESCRIPTIONS.experience },
    { key: "diffusion", label: "Diffusion", required: diffusionRequired, description: BUCKET_DESCRIPTIONS.diffusion },
  ];

  // An experience requirement with nothing feeding it can never be filled, so the technology could never be discovered. A free check on the catalogue, and
  // exactly the kind of thing this tool exists to catch.
  const hasUnfeedableExperience = experienceRequired !== null && experienceSourceCount === 0;

  return (
    <div className="flex flex-col gap-2">
      {rows.map((row) => (
        <div key={row.key} className="flex items-baseline justify-between gap-3" data-testid={`technology-bucket-${row.key}`}>
          <span className="text-sm text-slate-600" title={row.description}>
            {row.label}
          </span>

          {row.required === null ? (
            <span className="text-xs text-slate-400 italic">Not used by this technology</span>
          ) : (
            <span className="font-mono text-sm text-slate-900">{row.required.toLocaleString()}</span>
          )}
        </div>
      ))}

      {hasUnfeedableExperience && (
        <p className="text-xs text-[var(--bs-danger)]">
          This technology needs experience, but no experience type feeds it — so its experience bucket can never fill.
        </p>
      )}
    </div>
  );
}
