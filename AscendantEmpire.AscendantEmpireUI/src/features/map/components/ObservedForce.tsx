import ForceHealthBar from "./ForceHealthBar";
import type { ObservedForceTile } from "./TileTypes";

export interface ObservedForceProps {
  force: ObservedForceTile;
  unitInstanceCount: number;
  isSelected?: boolean;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
}

function getUnitMarkerCount(unitCount: number): number {
  if (unitCount <= 1) return 0;
  if (unitCount <= 5) return 1;
  if (unitCount <= 10) return 2;
  if (unitCount <= 15) return 3;
  return 4;
}

export default function ObservedForce({ force, unitInstanceCount, isSelected = false, unitImageSrcByUnitTemplateId }: ObservedForceProps) {
  const unitImageSrc = unitImageSrcByUnitTemplateId?.get(force.unitTemplateId);

  if (!unitImageSrc) return null;

  const unitMarkerCount = getUnitMarkerCount(unitInstanceCount);

  return (
    <div className={["observed-force", isSelected ? "observed-force--selected" : ""].filter(Boolean).join(" ")} aria-hidden>
      <ForceHealthBar healthy={force.healthyTroops} injured={force.injuredTroops} initialStrength={force.initialStrength} />
      <div className="observed-force__body">
        {isSelected ? (
          <>
            <span className="observed-force__selection-corner observed-force__selection-corner--top-left" />
            <span className="observed-force__selection-corner observed-force__selection-corner--top-right" />
            <span className="observed-force__selection-corner observed-force__selection-corner--bottom-left" />
            <span className="observed-force__selection-corner observed-force__selection-corner--bottom-right" />
          </>
        ) : null}
        {unitMarkerCount > 0 ? (
          <div className="observed-force__marker-column">
            {Array.from({ length: unitMarkerCount }, (_, index) => (
              <span key={index} className="observed-force__marker" />
            ))}
          </div>
        ) : null}
        <img className="observed-force__image" alt="" src={unitImageSrc} />
      </div>
    </div>
  );
}
