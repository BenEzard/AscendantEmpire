import type { ObservedStructureTile } from "./TileTypes";
import { structureImageSrc } from "@/features/map/util/structureImages";

export interface ObservedStructureProps {
  structure: ObservedStructureTile;
}

/**
 * A structure as it stands on its own tile: the sprite, and a bar while it is still going up.
 *
 * The settlement's name is not drawn here. It is wider than the tile and would be clipped by `.tile`'s `overflow: hidden`, so
 * it belongs to {@link SettlementLabelOverlay}, which draws over the whole grid.
 */
export default function ObservedStructure({ structure }: ObservedStructureProps) {
  const imageSrc = structureImageSrc(structure.structureCode);

  // Only kinds the client has art for are drawn; the rest have nothing to show yet.
  if (!imageSrc) {
    return null;
  }

  const normalizedPercent = Math.max(0, Math.min(100, structure.completionPercent));
  const showCompletionBar = normalizedPercent < 100;

  return (
    <div className="observed-structure" aria-hidden>
      <img className="observed-structure__image" alt="" src={imageSrc} />
      {showCompletionBar ? (
        <div className="observed-structure__completion-bar">
          <div className="observed-structure__completion-fill" style={{ height: `${normalizedPercent}%` }} />
        </div>
      ) : null}
    </div>
  );
}
