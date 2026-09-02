import { useEffect, useRef } from "react";
import type { MoveDirection } from "../api/forceMovementApi";

const INPUT_TAGS = ["INPUT", "TEXTAREA", "SELECT"];

/**
 * Which direction each numpad key steps in, laid out as the keys sit on the pad.
 *
 * Keyed on `event.code` rather than `event.key` deliberately: a numpad key only reports `"1"`..`"9"` as its `key` while NumLock
 * is on, and reports `"End"`, `"ArrowDown"` and friends when it is off. The `code` is the physical key and never changes.
 *
 * Numpad5 is absent — the centre key is "stay put", which is not a move.
 */
const DIRECTION_BY_KEY_CODE: Readonly<Record<string, MoveDirection>> = {
  Numpad7: "NorthWest",
  Numpad8: "North",
  Numpad9: "NorthEast",
  Numpad4: "West",
  Numpad6: "East",
  Numpad1: "SouthWest",
  Numpad2: "South",
  Numpad3: "SouthEast",
};

/**
 * Turns numpad presses into single-tile moves for the selected force.
 *
 * Only fires with exactly one force selected. Ordering a whole selection at once is a group move, which is judged per force and
 * has to report which of them could not go — a later ticket. Rather than move an arbitrary one of several and leave the rest
 * behind, this says so and does nothing.
 */
export function useNumpadMovement({
  enabled,
  selectedForceCount,
  onStep,
  onUnsupportedSelection,
}: {
  /** Whether the map is showing a game at all. */
  enabled: boolean;
  selectedForceCount: number;
  onStep: (direction: MoveDirection) => void;
  onUnsupportedSelection: () => void;
}) {
  // Held in refs so the listener always reads the current selection without being torn down and re-registered on every render —
  // the same reason useDialogKeyboardShortcuts keeps its active section in one.
  const selectedForceCountRef = useRef(selectedForceCount);
  selectedForceCountRef.current = selectedForceCount;

  const onStepRef = useRef(onStep);
  onStepRef.current = onStep;

  const onUnsupportedSelectionRef = useRef(onUnsupportedSelection);
  onUnsupportedSelectionRef.current = onUnsupportedSelection;

  useEffect(() => {
    if (!enabled) return;

    const handler = (event: KeyboardEvent) => {
      // A modified numpad press is someone else's shortcut, not a move.
      if (event.ctrlKey || event.metaKey || event.altKey) return;

      const direction = DIRECTION_BY_KEY_CODE[event.code];
      if (direction == null) return;

      // Typing a number into a field is not an order to march.
      const target = event.target;
      if (target instanceof HTMLElement) {
        if (target.isContentEditable || INPUT_TAGS.includes(target.tagName)) return;
      }

      if (selectedForceCountRef.current === 0) return;

      // Claim the key only once we know we are acting on it, so an unhandled press still reaches the browser.
      event.preventDefault();

      if (selectedForceCountRef.current > 1) {
        onUnsupportedSelectionRef.current();
        return;
      }

      onStepRef.current(direction);
    };

    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [enabled]);
}

/** Exposed for tests, so the key-to-direction mapping can be pinned without simulating a keyboard. */
export const numpadDirectionForKeyCode = (code: string): MoveDirection | undefined => DIRECTION_BY_KEY_CODE[code];
