import type { KnownStructure } from "@/features/intelligence/api/intelligenceApi";
import { knownForceOpacity } from "@/features/intelligence/knownForceMarkers";
import { structureImageSrc } from "@/features/map/util/structureImages";

export interface KnownStructureMarkerProps {
  /** Every structure last seen on this tile, freshest sighting first. The marker shows the first; the rest only add to the count. */
  knownStructures: readonly KnownStructure[];
  /** The round being played now, which the marker fades against. Null draws it fresh. */
  currentRoundNumber: number | null;
}

/**
 * A last-known structure marker: what the player last saw of a structure, fading with every round since.
 *
 * Drawn only on ground nothing of the player's is watching — the tile decides that — so it never competes with the live structure. It shows no more than the
 * sighting earned: a structure whose kind was not made out is a question mark, and its progress is only drawn when that was made out too.
 */
export default function KnownStructureMarker({ knownStructures, currentRoundNumber }: KnownStructureMarkerProps) {
  const freshest = knownStructures[0];

  if (!freshest) {
    return null;
  }

  const isKindKnown = freshest.structureId != null;
  const imageSrc = structureImageSrc(freshest.structureId);
  const isRazed = freshest.status === "Razed";
  // Both kinds of memory fade on the same curve, so a structure and a force seen in the same round look equally old.
  const opacity = knownForceOpacity(freshest.lastSeenRoundNumber, currentRoundNumber);

  // A razed structure is not being built any more, and a progress that was never disclosed must not be drawn as some guessed amount.
  const completionPercent = freshest.completionPercent == null ? null : Math.max(0, Math.min(100, freshest.completionPercent));
  const showCompletionBar = !isRazed && completionPercent != null && completionPercent < 100;

  return (
    <div className={["known-structure", isRazed ? "known-structure--razed" : ""].filter(Boolean).join(" ")} style={{ opacity }} aria-hidden>
      <span className="known-structure__outline" />
      {imageSrc ? (
        <img className="known-structure__image" alt="" src={imageSrc} />
      ) : isKindKnown ? (
        // The kind was made out, but the client has no art for it yet: a plain building, not a question mark that would say the kind is unknown.
        <span className="known-structure__generic">⌂</span>
      ) : (
        <span className="known-structure__unknown">?</span>
      )}
      {isRazed ? <span className="known-structure__razed-mark">✕</span> : null}
      {showCompletionBar ? (
        <div className="known-structure__completion-bar">
          <div className="known-structure__completion-fill" style={{ height: `${completionPercent}%` }} />
        </div>
      ) : null}
      {knownStructures.length > 1 ? <span className="known-structure__count">{knownStructures.length}</span> : null}
    </div>
  );
}
