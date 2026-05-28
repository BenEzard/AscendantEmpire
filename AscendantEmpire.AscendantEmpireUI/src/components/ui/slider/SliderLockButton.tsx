import type { LockedState } from "./Slider";

interface SliderLockButtonProps {
  state: LockedState;
  onClick: () => void;
}

const TOOLTIP_LABELS: Record<LockedState, string> = {
  unlocked: "Unlocked",
  locked: "Locked",
  systemlocked: "Permanently Locked",
  unlockable: "",
  systemlockedinvisible: "",
};

function LockIcon({ state }: { state: LockedState }) {
  const colorClass = state === "unlocked" ? "text-slate-400" : state === "locked" ? "text-yellow-600" : "text-black";
  const isLocked = state !== "unlocked";

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={`h-4 w-4 shrink-0 ${colorClass}`}>
      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
      {isLocked ? <path d="M7 11V7a5 5 0 0 1 10 0v4" /> : <path d="M7 11V7a5 5 0 0 1 9.9-1" />}
    </svg>
  );
}

export default function SliderLockButton({ state, onClick }: SliderLockButtonProps) {
  const isSystemLocked = state === "systemlocked";
  const isLocked = state !== "unlocked";

  return (
    <div className="flex items-center justify-center">
      <button
        type="button"
        onClick={onClick}
        title={TOOLTIP_LABELS[state]}
        className={`flex items-center justify-center ${isSystemLocked ? "cursor-default" : "cursor-pointer hover:opacity-75"}`}
        aria-label={isLocked ? "Unlock slider" : "Lock slider"}>
        <LockIcon state={state} />
      </button>
    </div>
  );
}
