import { useRef, useCallback, useState, useEffect, useId, type ReactNode } from "react";
import SliderButton from "./SliderButton";
import SliderLockButton from "./SliderLockButton";
import { useSliderCollection } from "./SliderCollectionContext";

export type LockedState = "unlocked" | "locked" | "systemlocked" | "unlockable" | "systemlockedinvisible";

export interface SliderProps {
  id: number;
  code: string;
  icon?: ReactNode;
  label: string;
  /** Minimum value of the slider range. Defaults to 0. */
  minimumValue?: number;
  /** Maximum value of the slider range. Defaults to 100. */
  maximumValue?: number;
  /** Starting value within the range. Defaults to minimumValue. */
  initialValue?: number;
  onChange?: (value: number) => void;
  lockedState?: LockedState;
  /** Formats the value displayed on the slider button. Defaults to String(value). */
  formatLabel?: (value: number) => string;
  /** Rendered after the lock button within the slider row. Receives the current value. */
  trailing?: (value: number) => ReactNode;
}

const GROOVE_PADDING = 14; // px inset on each side so button never overlaps adjacent labels
const GROOVE_WIDTH = 200;
const LABEL_WIDTH = 110;
const ICON_WIDTH = 32;

export default function Slider({
  id: _id,
  code: _code,
  icon,
  label,
  minimumValue = 0,
  maximumValue = 100,
  initialValue,
  onChange,
  lockedState = "unlocked",
  formatLabel,
  trailing,
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

  // Holds a teardown function for the document-level drag listeners while a drag is in
  // progress, so they can be detached if the slider unmounts mid-drag (otherwise the
  // listeners — and the component closures they capture — would leak).
  const dragTeardownRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    return () => {
      dragTeardownRef.current?.();
      dragTeardownRef.current = null;
    };
  }, []);

  // Internal state used when the slider is standalone (outside a SliderCollection).
  const [internalValue, setInternalValue] = useState(initialValue ?? minimumValue);
  const [internalLockState, setInternalLockState] = useState<LockedState>(lockedState);

  // Register with the collection once on mount; unregister on unmount.
  useEffect(() => {
    if (!register || !unregister) return;
    register(id, _id, _code, initialValue ?? minimumValue, lockedState);
    return () => unregister(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [register, unregister, id]); // initialValue / lockedState intentionally omitted — register once

  // Read current value and lock state from collection, falling back to internal state.
  const entry = collection?.getEntry(id);
  const value = entry?.value ?? internalValue;
  const lockState = entry?.lockState ?? internalLockState;

  const isLocked = lockState !== "unlocked" && lockState !== "unlockable";
  const trackWidth = GROOVE_WIDTH - GROOVE_PADDING * 2;
  const valueRange = maximumValue - minimumValue;
  const buttonLeft = GROOVE_PADDING + ((value - minimumValue) / valueRange) * trackWidth;
  const buttonLabel = formatLabel ? formatLabel(value) : String(value);

  const handleMouseDown = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();

      const handleMouseMove = (ev: MouseEvent) => {
        if (!grooveRef.current) return;
        const rect = grooveRef.current.getBoundingClientRect();
        const x = Math.max(0, Math.min(ev.clientX - rect.left - GROOVE_PADDING, trackWidth));
        const newValue = Math.max(minimumValue, Math.min(Math.round(minimumValue + (x / trackWidth) * valueRange), maximumValue));
        if (requestValueUpdate) {
          requestValueUpdate(id, newValue);
        } else {
          setInternalValue(newValue);
          onChange?.(newValue);
        }
      };

      const detach = () => {
        document.removeEventListener("mousemove", handleMouseMove);
        document.removeEventListener("mouseup", handleMouseUp);
        dragTeardownRef.current = null;
      };

      const handleMouseUp = () => {
        detach();
      };

      document.addEventListener("mousemove", handleMouseMove);
      document.addEventListener("mouseup", handleMouseUp);
      // Remember how to tear these down if we unmount before mouseup fires.
      dragTeardownRef.current = detach;
    },
    [requestValueUpdate, id, onChange, trackWidth, minimumValue, maximumValue, valueRange],
  );

  const handleLockClick = useCallback(() => {
    if (lockState === "systemlocked" || lockState === "systemlockedinvisible" || lockState === "unlockable") return;
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

      <span className="shrink-0 text-sm text-slate-600" style={{ width: LABEL_WIDTH }}>
        {label}
      </span>

      {/* Groove */}
      <div ref={grooveRef} className="relative flex-shrink-0" style={{ width: GROOVE_WIDTH, height: 28 }}>
        {/* Track line — inset by GROOVE_PADDING so it aligns with button travel range */}
        <div className="absolute top-1/2 h-0.5 -translate-y-1/2 rounded-full bg-slate-400" style={{ left: GROOVE_PADDING, right: GROOVE_PADDING }} />
        <SliderButton label={buttonLabel} leftPx={buttonLeft} onMouseDown={handleMouseDown} locked={isLocked} />
      </div>

      <div className="flex items-center gap-1">
        {lockState !== "unlockable" && lockState !== "systemlockedinvisible" && <SliderLockButton state={lockState} onClick={handleLockClick} />}
        {trailing?.(value)}
      </div>
    </div>
  );
}
