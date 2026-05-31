export interface CircularProgressProps {
  /** Value between 0 and 1 */
  value: number;
  /** Diameter of the circle in pixels. Default: 48 */
  size?: number;
  /** Stroke thickness in pixels. Default: 4 */
  strokeWidth?: number;
  /** Show percentage label in the centre. Default: true */
  showLabel?: boolean;
}

export default function CircularProgress({ value, size = 48, strokeWidth = 4, showLabel = true }: CircularProgressProps) {
  const clamped = Math.min(Math.max(value, 0), 1);
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference * (1 - clamped);
  const center = size / 2;

  return (
    <div className="relative inline-flex items-center justify-center" style={{ width: size, height: size }}>
      <svg width={size} height={size} style={{ transform: "rotate(-90deg)" }}>
        {/* Track */}
        <circle cx={center} cy={center} r={radius} fill="none" stroke="currentColor" strokeWidth={strokeWidth} className="text-slate-200" />
        {/* Fill */}
        <circle
          cx={center}
          cy={center}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          strokeLinecap="round"
          className="text-[var(--bs-primary)] transition-[stroke-dashoffset] duration-300"
        />
      </svg>
      {showLabel && (
        <span
          className="absolute inset-0 flex items-center justify-center font-semibold text-slate-700 leading-none pointer-events-none"
          style={{ fontSize: Math.max(size * 0.22, 9) }}>
          {Math.round(clamped * 100)}%
        </span>
      )}
    </div>
  );
}
