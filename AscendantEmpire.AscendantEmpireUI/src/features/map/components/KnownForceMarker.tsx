import type { KnownForce } from "@/features/intelligence/api/intelligenceApi";
import { headingDegrees, knownForceOpacity } from "@/features/intelligence/knownForceMarkers";

export interface KnownForceMarkerProps {
  /** Every force last seen on this tile, freshest sighting first. The marker shows the first; the rest only add to the count. */
  knownForces: readonly KnownForce[];
  /** The round being played now, which the marker fades against. Null draws it fresh. */
  currentRoundNumber: number | null;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
}

/**
 * A last-known-position marker: where the player last saw a force, fading with every round since.
 *
 * Drawn only on ground nothing of the player's is watching — the tile decides that — so it never competes with a live sighting. It shows no more than the
 * sighting earned: a force made out only as movement is a question mark, not a guessed unit picture.
 */
export default function KnownForceMarker({ knownForces, currentRoundNumber, unitImageSrcByUnitTemplateId }: KnownForceMarkerProps) {
  const freshest = knownForces[0];

  if (!freshest) {
    return null;
  }

  // The template is null whenever the fidelity did not earn it, and then there is no picture to show.
  const unitImageSrc = freshest.unitTemplateId == null ? undefined : unitImageSrcByUnitTemplateId?.get(freshest.unitTemplateId);
  const isDestroyed = freshest.status === "Destroyed";
  const opacity = knownForceOpacity(freshest.lastSeenRoundNumber, currentRoundNumber);

  return (
    <div className={["known-force", isDestroyed ? "known-force--destroyed" : ""].filter(Boolean).join(" ")} style={{ opacity }} aria-hidden>
      <span className="known-force__outline" />
      {unitImageSrc ? <img className="known-force__image" alt="" src={unitImageSrc} /> : <span className="known-force__unknown">?</span>}
      {isDestroyed ? <span className="known-force__destroyed-mark">✕</span> : null}
      {/* Only a force that left has a heading; one still in view when last seen, or destroyed, has none. */}
      {!isDestroyed && freshest.exitDirection ? (
        <span className="known-force__heading" style={{ transform: `rotate(${headingDegrees(freshest.exitDirection)}deg)` }}>
          ➜
        </span>
      ) : null}
      {knownForces.length > 1 ? <span className="known-force__count">{knownForces.length}</span> : null}
    </div>
  );
}
