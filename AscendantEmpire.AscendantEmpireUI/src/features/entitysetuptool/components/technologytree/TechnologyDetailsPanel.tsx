import type { ReactNode } from "react";
import { ToggleSwitch } from "@/components/ui";
import type { CatalogueTechnology, TechnologyGrantEffect, TechnologyPrerequisiteKind } from "../../api/technologyApi";
import type { DroppedPrerequisiteReason, LaidOutTechnologyNode } from "../../tools/technologyTreeLayout";
import TechnologyRequirementBuckets from "./TechnologyRequirementBuckets";

/** One technology that needs the selected one, and how it lists the dependency. */
export interface TechnologyDependentRef {
  id: number;
  code: string;
  displayName: string;
  kind: TechnologyPrerequisiteKind;
}

export interface TechnologyDetailsPanelProps {
  technology: CatalogueTechnology | null;
  node: LaidOutTechnologyNode | null;
  dependents: readonly TechnologyDependentRef[];
  /** Prerequisites of this technology that produced no edge, so the panel can say what was named and why it did not resolve. */
  droppedPrerequisites: readonly { prerequisiteTechnologyCode: string; reason: DroppedPrerequisiteReason }[];
  showFullAncestry: boolean;
  onToggleFullAncestry: (showFullAncestry: boolean) => void;
  onNavigate: (id: number) => void;
}

/** How each grant effect is tinted. Unlocking adds something, retiring takes it away, so the two should not look alike. */
const GRANT_EFFECT_CLASSES: Record<TechnologyGrantEffect, string> = {
  Unlock: "bg-emerald-100 text-emerald-800",
  Obsolete: "bg-amber-100 text-amber-900",
};

/** A section heading, with an optional count beside it. */
function SectionHeading({ children, count }: { children: ReactNode; count?: number }) {
  return (
    <h3 className="mb-2 text-xs font-semibold tracking-wide text-slate-500 uppercase">
      {children}
      {/* A literal space ahead of the count, so the accessible name does not run the two together. */}
      {count !== undefined && count > 0 && <span className="font-normal text-slate-400"> ({count})</span>}
    </h3>
  );
}

/** A clickable row naming another technology, which moves the selection to it. */
function TechnologyLink({ code, displayName, onClick }: { code: string; displayName: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="w-full rounded px-2 py-1 text-left text-sm text-slate-700 hover:bg-slate-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-400">
      {displayName}
      <span className="ml-2 font-mono text-[11px] text-slate-400">{code}</span>
    </button>
  );
}

/**
 * Everything the catalogue holds about the selected technology.
 *
 * Where the tree's two line styles are restated in words: prerequisites are split under "All of" and "Any one of", which is the rule the mandatory and
 * optional kinds actually encode. A reader who never looks at the canvas still gets the distinction, and one who does gets it twice.
 *
 * Damage is reported rather than tidied away. A grant whose target has been deleted keeps its row and says so; a prerequisite that named something missing is
 * listed with the code it named. Hiding either would make the viewer agree with a catalogue that is wrong.
 */
export default function TechnologyDetailsPanel({
  technology,
  node,
  dependents,
  droppedPrerequisites,
  showFullAncestry,
  onToggleFullAncestry,
  onNavigate,
}: TechnologyDetailsPanelProps) {
  if (technology === null || node === null) {
    return (
      <div className="flex h-full items-center justify-center p-6 text-center" data-testid="technology-details-panel">
        <p className="text-sm text-slate-400">Pick a technology to see its prerequisites, research cost and grants.</p>
      </div>
    );
  }

  const mandatoryPrerequisites = technology.prerequisites.filter((prerequisite) => prerequisite.prerequisiteKind === "Mandatory");
  const optionalPrerequisites = technology.prerequisites.filter((prerequisite) => prerequisite.prerequisiteKind === "Optional");
  const mandatoryDependents = dependents.filter((dependent) => dependent.kind === "Mandatory");
  const optionalDependents = dependents.filter((dependent) => dependent.kind === "Optional");

  return (
    <div className="flex h-full flex-col gap-5 overflow-y-auto p-4" data-testid="technology-details-panel">
      <header>
        <h2 className="text-lg font-semibold text-slate-900">{technology.displayName}</h2>
        <p className="font-mono text-xs text-slate-400">{technology.code}</p>

        {/* Era is a catalogue value shown for completeness, not a grouping. Its only consumer is the requirement generator that balances per era, and the
            domain enum's own documentation says it is deliberately not a UI dimension — so it is reported here and used nowhere else. */}
        <p className="mt-2 text-xs text-slate-500">
          Era <span className="font-medium text-slate-700">{technology.era}</span>
        </p>
      </header>

      {technology.description !== null && technology.description !== "" && (
        <section>
          <SectionHeading>Description</SectionHeading>
          <p className="text-sm whitespace-pre-line text-slate-700">{technology.description}</p>
        </section>
      )}

      <section>
        <SectionHeading>Research required</SectionHeading>
        <TechnologyRequirementBuckets
          directedRequired={technology.directedRequired}
          experienceRequired={technology.experienceRequired}
          diffusionRequired={technology.diffusionRequired}
          experienceSourceCount={technology.experienceSources.length}
        />
      </section>

      <section>
        <SectionHeading>Acquisition cost</SectionHeading>
        {technology.acquisitionCosts.length === 0 ? (
          <p className="text-sm text-slate-500">Free — no acquisition cost.</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {technology.acquisitionCosts.map((cost) => (
              <li key={cost.assetId} className="text-sm text-slate-700">
                {cost.quantity.toLocaleString()} × {cost.assetDisplayName}
                <span className="ml-2 font-mono text-[11px] text-slate-400">{cost.assetCode}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section>
        <SectionHeading>Prerequisites</SectionHeading>

        {technology.prerequisites.length === 0 && droppedPrerequisites.length === 0 && (
          <p className="text-sm text-slate-500">Available from the first turn.</p>
        )}

        {mandatoryPrerequisites.length > 0 && (
          <div className="mb-3">
            <p className="mb-1 text-xs font-medium text-slate-600">All of — every one must be discovered</p>
            <div className="flex flex-col">
              {mandatoryPrerequisites.map((prerequisite) => (
                <TechnologyLink
                  key={prerequisite.prerequisiteTechnologyId}
                  code={prerequisite.prerequisiteTechnologyCode}
                  displayName={prerequisite.prerequisiteTechnologyDisplayName}
                  onClick={() => onNavigate(prerequisite.prerequisiteTechnologyId)}
                />
              ))}
            </div>
          </div>
        )}

        {optionalPrerequisites.length > 0 && (
          <div className="mb-3">
            <p className="mb-1 text-xs font-medium text-slate-600">Any one of — one is enough</p>
            <div className="flex flex-col">
              {optionalPrerequisites.map((prerequisite) => (
                <TechnologyLink
                  key={prerequisite.prerequisiteTechnologyId}
                  code={prerequisite.prerequisiteTechnologyCode}
                  displayName={prerequisite.prerequisiteTechnologyDisplayName}
                  onClick={() => onNavigate(prerequisite.prerequisiteTechnologyId)}
                />
              ))}
            </div>
          </div>
        )}

        {droppedPrerequisites.map((dropped) => (
          <p key={`${dropped.prerequisiteTechnologyCode}-${dropped.reason}`} className="text-xs text-[var(--bs-danger)]">
            {dropped.reason === "self"
              ? `Lists itself (${dropped.prerequisiteTechnologyCode}) as a prerequisite, which was ignored.`
              : `Needs ${dropped.prerequisiteTechnologyCode}, which is not in the catalogue.`}
          </p>
        ))}
      </section>

      <section>
        <SectionHeading count={dependents.length}>Leads to</SectionHeading>

        {dependents.length === 0 ? (
          <p className="text-sm text-slate-500">Nothing depends on this technology.</p>
        ) : (
          <>
            {mandatoryDependents.length > 0 && (
              <div className="mb-3">
                <p className="mb-1 text-xs font-medium text-slate-600">Requires this</p>
                <div className="flex flex-col">
                  {mandatoryDependents.map((dependent) => (
                    <TechnologyLink key={dependent.id} code={dependent.code} displayName={dependent.displayName} onClick={() => onNavigate(dependent.id)} />
                  ))}
                </div>
              </div>
            )}

            {optionalDependents.length > 0 && (
              <div>
                <p className="mb-1 text-xs font-medium text-slate-600">Counts as one of its options</p>
                <div className="flex flex-col">
                  {optionalDependents.map((dependent) => (
                    <TechnologyLink key={dependent.id} code={dependent.code} displayName={dependent.displayName} onClick={() => onNavigate(dependent.id)} />
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </section>

      <section>
        <SectionHeading count={technology.grants.length}>Grants</SectionHeading>

        {technology.grants.length === 0 ? (
          <p className="text-sm text-slate-500">Nothing is unlocked or retired by discovering this.</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {technology.grants.map((grant) => (
              <li key={`${grant.targetType}-${grant.targetId}`} className="text-sm text-slate-700">
                <span className={`rounded px-2 py-0.5 text-[11px] font-semibold ${GRANT_EFFECT_CLASSES[grant.grantEffect]}`}>{grant.grantEffect}</span>
                <span className="ml-2">{grant.targetDisplayName ?? `${grant.targetType} #${grant.targetId}`}</span>
                <span className="ml-2 text-[11px] text-slate-400">{grant.targetType}</span>

                {/* A grant carries no foreign key, so it can outlive what it points at. Keeping the row and naming the problem is the whole value here. */}
                {grant.targetCode === null && <span className="ml-2 text-[11px] text-[var(--bs-danger)]">target no longer exists</span>}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section>
        <SectionHeading count={technology.experienceSources.length}>Experience sources</SectionHeading>

        {technology.experienceSources.length === 0 ? (
          <p className="text-sm text-slate-500">No experience type feeds this technology.</p>
        ) : (
          <div className="flex flex-wrap gap-1">
            {technology.experienceSources.map((source) => (
              <span key={source.experienceTypeId} className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                {source.experienceTypeDisplayName}
              </span>
            ))}
          </div>
        )}
      </section>

      <section className="mt-auto border-t border-slate-200 pt-3">
        {/* The id is the switch's own DOM handle, not a technology id — this panel has exactly one switch, so a constant is all it needs. */}
        <ToggleSwitch id={0} value={showFullAncestry} onChange={(_id, value) => onToggleFullAncestry(value)} label="Show full ancestry" />

        <p className="mt-1 text-[11px] text-slate-400">
          Highlight everything this technology ultimately rests on, and everything that ultimately rests on it, rather than only one step either way.
        </p>

        {/* Small and quiet: useful when the tree looks wrong, noise the rest of the time. */}
        <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-1 text-[11px] text-slate-400">
          <dt>Column</dt>
          <dd className="text-right">{node.column}</dd>
          <dt>Depth in tree</dt>
          <dd className="text-right">{node.depth}</dd>
        </dl>

        {node.earliestDepth < node.depth && (
          <p className="mt-2 text-[11px] text-slate-500">
            An optional route reaches this as early as column {node.earliestDepth}. It is drawn at column {node.column} so every prerequisite arrow still
            points forwards.
          </p>
        )}

        {node.isInCycle && <p className="mt-2 text-[11px] text-[var(--bs-danger)]">This technology sits on a prerequisite cycle.</p>}
      </section>
    </div>
  );
}
