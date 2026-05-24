import { useRef, useCallback, useState, useEffect, useId, type ReactNode } from "react";
import SliderButton from "./SliderButton";
import SliderLockButton from "./SliderLockButton";
import { useSliderCollection } from "./SliderCollectionContext";
import NumberValueField from "../NumberValueField";

export type LockedState = "unlocked" | "locked" | "systemlocked";

export interface SliderProps {
  id: number;
  code: string;
  icon?: ReactNode;
  label: string;
  /** Starting position in the range 0–100. Defaults to 0. */
  initialValue?: number;
  /** Total resources added per turn; used to compute the allocated amount displayed beside the groove. */
  resourcesAddedPerTurn?: number;
  onChange?: (value: number) => void;
  maxConsumptionLimit?: number;
  lockedState?: LockedState;
}

const GROOVE_PADDING = 14; // px inset on each side so button never overlaps adjacent labels
const GROOVE_WIDTH = 200;
const LABEL_WIDTH = 110;
const VALUE_WIDTH = 56;
const ICON_WIDTH = 32;
const ALLOCATED_TOOLTIP = "Allocated per turn";
const MAX_CONSUMPTION_TOOLTIP = "Max consumption";

export default function Slider({
  id: _id,
  code: _code,
  icon,
  label,
  initialValue = 0,
  resourcesAddedPerTurn,
  onChange,
  maxConsumptionLimit,
  lockedState = "unlocked",
}: SliderProps) {
  const id = useId();
  const collection = useSliderCollection();
  // Extract stable callbacks so they can be used as effect / memo deps without
  // causing re-runs every time the collection state changes (getEntry changes).
  const register = collection?.register;
  const unregister = collection?.unregister;
  const requestValueUpdate = collection?.requestValueUpdate;
  const requestLockToggle = collection?.requestLockToggle;

  const grooveRef = useRef<HTMLDivElement>(null);

  // Internal state used when the slider is standalone (outside a SliderCollection).
  const [internalValue, setInternalValue] = useState(initialValue);
  const [internalLockState, setInternalLockState] = useState<LockedState>(lockedState);

  // Register with the collection once on mount; unregister on unmount.
  useEffect(() => {
    if (!register || !unregister) return;
    register(id, initialValue, lockedState);
    return () => unregister(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [register, unregister, id]); // initialValue / lockedState intentionally omitted — register once

  // Read current value and lock state from collection, falling back to internal state.
  const entry = collection?.getEntry(id);
  const value = entry?.value ?? internalValue;
  const lockState = entry?.lockState ?? internalLockState;

  const isLocked = lockState !== "unlocked";
  const trackWidth = GROOVE_WIDTH - GROOVE_PADDING * 2;
  const buttonLeft = GROOVE_PADDING + (value / 100) * trackWidth;
  const allocatedResource = Math.round((value / 100) * (resourcesAddedPerTurn ?? 0));

  const handleMouseDown = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();

      const handleMouseMove = (ev: MouseEvent) => {
        if (!grooveRef.current) return;
        const rect = grooveRef.current.getBoundingClientRect();
        const x = Math.max(0, Math.min(ev.clientX - rect.left - GROOVE_PADDING, trackWidth));
        const pct = Math.round((x / trackWidth) * 100);
        if (requestValueUpdate) {
          requestValueUpdate(id, pct);
        } else {
          setInternalValue(pct);
          onChange?.(pct);
        }
      };

      const handleMouseUp = () => {
        document.removeEventListener("mousemove", handleMouseMove);
        document.removeEventListener("mouseup", handleMouseUp);
      };

      document.addEventListener("mousemove", handleMouseMove);
      document.addEventListener("mouseup", handleMouseUp);
    },
    [requestValueUpdate, id, onChange, trackWidth],
  );

  const handleLockClick = useCallback(() => {
    if (lockState === "systemlocked") return;
    if (requestLockToggle) {
      requestLockToggle(id);
    } else {
      setInternalLockState((prev) => (prev === "unlocked" ? "locked" : "unlocked"));
    }
  }, [requestLockToggle, id, lockState]);

  return (
    <div className="flex items-center gap-3">
      {/* Icon — always reserves fixed width so columns align when some rows have no icon */}
      <span className="flex shrink-0 items-center justify-center" style={{ width: ICON_WIDTH }}>
        {icon}
      </span>

      <span className="shrink-0 text-sm" style={{ width: LABEL_WIDTH }}>
        {label}
      </span>

      {/* Groove */}
      <div ref={grooveRef} className="relative flex-shrink-0" style={{ width: GROOVE_WIDTH, height: 28 }}>
        {/* Track line — inset by GROOVE_PADDING so it aligns with button travel range */}
        <div className="absolute top-1/2 h-0.5 -translate-y-1/2 rounded-full bg-slate-400" style={{ left: GROOVE_PADDING, right: GROOVE_PADDING }} />
        <SliderButton value={value} leftPx={buttonLeft} onMouseDown={handleMouseDown} locked={isLocked} />
      </div>

      {resourcesAddedPerTurn != null && <NumberValueField actualNumber={allocatedResource} tooltip={ALLOCATED_TOOLTIP} width={VALUE_WIDTH} />}
      {maxConsumptionLimit != null && <NumberValueField actualNumber={maxConsumptionLimit} tooltip={MAX_CONSUMPTION_TOOLTIP} width={VALUE_WIDTH} />}

      <SliderLockButton state={lockState} onClick={handleLockClick} />
    </div>
  );
}
