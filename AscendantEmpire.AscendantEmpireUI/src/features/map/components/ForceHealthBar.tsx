export interface ForceHealthBarProps {
  healthy: number;
  injured: number;
  initialStrength: number;
}

function clampPercent(value: number): number {
  return Math.max(0, Math.min(100, value));
}

/** A Force's healthy, injured and lost share of its initial strength, as the map draws it above the unit. */
export default function ForceHealthBar({ healthy, injured, initialStrength }: ForceHealthBarProps) {
  const strength = Math.max(0, initialStrength);
  const healthyCount = Math.max(0, healthy);
  const injuredCount = Math.max(0, injured);
  const healthyPercent = strength > 0 ? clampPercent((healthyCount / strength) * 100) : 0;
  const remainingAfterHealthy = Math.max(0, 100 - healthyPercent);
  const injuredPercent = strength > 0 ? Math.min(clampPercent((injuredCount / strength) * 100), remainingAfterHealthy) : 0;
  const deadPercent = Math.max(0, 100 - healthyPercent - injuredPercent);
  const isFullyHealthy = strength > 0 && healthyCount >= strength;

  return (
    <div className="observed-force__health-bar">
      {healthyPercent > 0 ? (
        <div className="observed-force__health-fill observed-force__health-fill--healthy" style={{ width: `${isFullyHealthy ? 100 : healthyPercent}%` }} />
      ) : null}
      {!isFullyHealthy && injuredPercent > 0 ? (
        <div className="observed-force__health-fill observed-force__health-fill--injured" style={{ width: `${injuredPercent}%` }} />
      ) : null}
      {!isFullyHealthy && deadPercent > 0 ? (
        <div className="observed-force__health-fill observed-force__health-fill--dead" style={{ width: `${deadPercent}%` }} />
      ) : null}
    </div>
  );
}
