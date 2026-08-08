import type { ObservedTroopTile } from "./TileTypes";

export interface ObservedTroopProps {
  troop: ObservedTroopTile;
  troopInstanceCount: number;
  isSelected?: boolean;
  troopImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
}

function clampPercent(value: number): number {
  return Math.max(0, Math.min(100, value));
}

function getUnitMarkerCount(unitCount: number): number {
  if (unitCount <= 1) return 0;
  if (unitCount <= 5) return 1;
  if (unitCount <= 10) return 2;
  if (unitCount <= 15) return 3;
  return 4;
}

export default function ObservedTroop({ troop, troopInstanceCount, isSelected = false, troopImageSrcByUnitTemplateId }: ObservedTroopProps) {
  const troopImageSrc = troopImageSrcByUnitTemplateId?.get(troop.UnitTemplateId);

  if (!troopImageSrc) return null;

  const initialStrength = Math.max(0, troop.initialStrength);
  const healthyTroops = Math.max(0, troop.healthyTroops);
  const injuredTroops = Math.max(0, troop.injuredTroops);
  const healthyPercent = initialStrength > 0 ? clampPercent((healthyTroops / initialStrength) * 100) : 0;
  const remainingAfterHealthy = Math.max(0, 100 - healthyPercent);
  const injuredPercent = initialStrength > 0 ? Math.min(clampPercent((injuredTroops / initialStrength) * 100), remainingAfterHealthy) : 0;
  const deadPercent = Math.max(0, 100 - healthyPercent - injuredPercent);
  const isFullyHealthy = initialStrength > 0 && healthyTroops >= initialStrength;
  const showHealthySegment = healthyPercent > 0;
  const showInjuredSegment = !isFullyHealthy && injuredPercent > 0;
  const showDeadSegment = !isFullyHealthy && deadPercent > 0;
  const unitMarkerCount = getUnitMarkerCount(troopInstanceCount);

  return (
    <div className={["observed-troop", isSelected ? "observed-troop--selected" : ""].filter(Boolean).join(" ")} aria-hidden>
      <div className="observed-troop__health-bar">
        {showHealthySegment ? (
          <div
            className="observed-troop__health-fill observed-troop__health-fill--healthy"
            style={{ width: `${isFullyHealthy ? 100 : healthyPercent}%` }}
          />
        ) : null}
        {showInjuredSegment ? (
          <div className="observed-troop__health-fill observed-troop__health-fill--injured" style={{ width: `${injuredPercent}%` }} />
        ) : null}
        {showDeadSegment ? (
          <div className="observed-troop__health-fill observed-troop__health-fill--dead" style={{ width: `${deadPercent}%` }} />
        ) : null}
      </div>
      <div className="observed-troop__body">
        {isSelected ? (
          <>
            <span className="observed-troop__selection-corner observed-troop__selection-corner--top-left" />
            <span className="observed-troop__selection-corner observed-troop__selection-corner--top-right" />
            <span className="observed-troop__selection-corner observed-troop__selection-corner--bottom-left" />
            <span className="observed-troop__selection-corner observed-troop__selection-corner--bottom-right" />
          </>
        ) : null}
        {unitMarkerCount > 0 ? (
          <div className="observed-troop__marker-column">
            {Array.from({ length: unitMarkerCount }, (_, index) => (
              <span key={index} className="observed-troop__marker" />
            ))}
          </div>
        ) : null}
        <img className="observed-troop__image" alt="" src={troopImageSrc} />
      </div>
    </div>
  );
}
