import Tooltip from "./Tooltip";

interface BadgeProps {
  label: string;
  textColor: string;
  backgroundColor: string;
  tooltipText?: string;
}

export default function Badge({ label, textColor, backgroundColor, tooltipText }: BadgeProps) {
  return (
    <Tooltip content={tooltipText}>
      <span className="inline-block rounded px-2 py-0.5 text-xs font-semibold" style={{ color: textColor, backgroundColor }}>
        {label}
      </span>
    </Tooltip>
  );
}
