import { FaRegEnvelope } from "react-icons/fa6";

export interface NotificationButtonProps {
  visible: boolean;
  count: number;
  topPx: number;
  onClick: () => void;
}

export default function NotificationButton({ visible, count, topPx, onClick }: NotificationButtonProps) {
  if (!visible) return null;

  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={`Notifications (${count})`}
      className={[
        "fixed z-[1060]",
        "right-3",
        "rounded-full",
        "bg-[var(--bs-secondary)] text-white",
        "shadow-xl",
        "w-11 h-11",
        "flex items-center justify-center",
        "hover:opacity-90",
        "focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-slate-400",
      ].join(" ")}
      style={{ top: topPx }}>
      <span className="relative">
        <FaRegEnvelope />
        <span
          className={[
            "absolute",
            "-top-2 -right-3",
            "min-w-5 h-5",
            "px-1",
            "rounded-full",
            "bg-[var(--bs-danger)] text-white",
            "text-[11px]",
            "flex items-center justify-center",
          ].join(" ")}
          aria-label={`${count} notifications`}>
          {count}
        </span>
      </span>
    </button>
  );
}
