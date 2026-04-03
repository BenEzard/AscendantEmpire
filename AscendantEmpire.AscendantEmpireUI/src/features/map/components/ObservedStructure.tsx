import type { ObservedStructureTile } from "./TileTypes";
import woodenWatchTowerImage from "@/assets/structures/wooden-watch-tower_40px.png";

export interface ObservedStructureProps {
  structure: ObservedStructureTile;
}

export default function ObservedStructure({ structure }: ObservedStructureProps) {
  if (structure.structureId !== 1) return null;

  const normalizedPercent = Math.max(0, Math.min(100, structure.completionPercent));
  const showCompletionBar = normalizedPercent < 100;

  return (
    <div className="observed-structure" aria-hidden>
      <img className="observed-structure__image" alt="" src={woodenWatchTowerImage} />
      {showCompletionBar ? (
        <div className="observed-structure__completion-bar">
          <div className="observed-structure__completion-fill" style={{ height: `${normalizedPercent}%` }} />
        </div>
      ) : null}
    </div>
  );
}
