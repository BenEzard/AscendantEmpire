import { renderHook } from "@testing-library/react";
import { numpadDirectionForKeyCode, useNumpadMovement } from "../useNumpadMovement";
import type { MoveDirection } from "../../api/forceMovementApi";

/** Dispatches a keydown on the window, optionally aimed at a different target. */
function pressKey(code: string, options: { target?: HTMLElement; ctrlKey?: boolean } = {}) {
  const event = new KeyboardEvent("keydown", { code, ctrlKey: options.ctrlKey ?? false, bubbles: true, cancelable: true });

  (options.target ?? window).dispatchEvent(event);

  return event;
}

/** Mounts the hook, defaulting to an open game with exactly one force selected. */
function renderNumpadMovement(overrides: Partial<Parameters<typeof useNumpadMovement>[0]> = {}) {
  const onStep = vi.fn();
  const onUnsupportedSelection = vi.fn();

  const view = renderHook(() =>
    useNumpadMovement({
      enabled: true,
      selectedForceCount: 1,
      onStep,
      onUnsupportedSelection,
      ...overrides,
    }),
  );

  return { onStep, onUnsupportedSelection, ...view };
}

describe("useNumpadMovement", () => {
  it("maps each numpad key to the direction it sits in on the pad", () => {
    // Pinned as a table rather than through the DOM: this mapping is the whole point of the hook, and getting north
    // and south the wrong way round would send every force the opposite way with nothing to catch it.
    const expected: Record<string, MoveDirection> = {
      Numpad7: "NorthWest",
      Numpad8: "North",
      Numpad9: "NorthEast",
      Numpad4: "West",
      Numpad6: "East",
      Numpad1: "SouthWest",
      Numpad2: "South",
      Numpad3: "SouthEast",
    };

    for (const [code, direction] of Object.entries(expected)) {
      expect(numpadDirectionForKeyCode(code)).toBe(direction);
    }
  });

  it("ignores the centre key, which means stay put rather than move", () => {
    expect(numpadDirectionForKeyCode("Numpad5")).toBeUndefined();
  });

  it("reads the physical key rather than the character, so it works with NumLock off", () => {
    const { onStep } = renderNumpadMovement();

    // With NumLock off the browser reports key "End" for this physical key, and only `code` still says Numpad1.
    pressKey("Numpad1");

    expect(onStep).toHaveBeenCalledWith("SouthWest");
  });

  it("does not act on the number row, which is a different physical key", () => {
    const { onStep } = renderNumpadMovement();

    pressKey("Digit8");

    expect(onStep).not.toHaveBeenCalled();
  });

  it("steps the selected force and claims the key so the browser does not also act on it", () => {
    const { onStep } = renderNumpadMovement();

    const event = pressKey("Numpad6");

    expect(onStep).toHaveBeenCalledWith("East");
    expect(event.defaultPrevented).toBe(true);
  });

  it("does nothing at all with no force selected, and leaves the key for anything else listening", () => {
    const { onStep, onUnsupportedSelection } = renderNumpadMovement({ selectedForceCount: 0 });

    const event = pressKey("Numpad6");

    expect(onStep).not.toHaveBeenCalled();
    expect(onUnsupportedSelection).not.toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(false);
  });

  it("refuses to move an arbitrary one of several selected forces, and says why", () => {
    const { onStep, onUnsupportedSelection } = renderNumpadMovement({ selectedForceCount: 3 });

    pressKey("Numpad6");

    expect(onStep).not.toHaveBeenCalled();
    expect(onUnsupportedSelection).toHaveBeenCalledOnce();
  });

  it("leaves typing alone, so a number entered in a field is not read as an order to march", () => {
    const { onStep } = renderNumpadMovement();

    const input = document.createElement("input");
    document.body.appendChild(input);

    pressKey("Numpad6", { target: input });

    expect(onStep).not.toHaveBeenCalled();

    input.remove();
  });

  it("leaves modified presses to whatever shortcut owns them", () => {
    const { onStep } = renderNumpadMovement();

    pressKey("Numpad6", { ctrlKey: true });

    expect(onStep).not.toHaveBeenCalled();
  });

  it("listens only while a game is open", () => {
    const { onStep } = renderNumpadMovement({ enabled: false });

    pressKey("Numpad6");

    expect(onStep).not.toHaveBeenCalled();
  });

  it("stops listening once unmounted, so a closed map does not keep moving forces", () => {
    const { onStep, unmount } = renderNumpadMovement();

    unmount();
    pressKey("Numpad6");

    expect(onStep).not.toHaveBeenCalled();
  });

  it("sees the current selection without being re-registered, so a selection change mid-session still lands", () => {
    const onStep = vi.fn();
    const onUnsupportedSelection = vi.fn();

    const { rerender } = renderHook(
      ({ count }: { count: number }) =>
        useNumpadMovement({ enabled: true, selectedForceCount: count, onStep, onUnsupportedSelection }),
      { initialProps: { count: 1 } },
    );

    rerender({ count: 2 });
    pressKey("Numpad6");

    expect(onStep).not.toHaveBeenCalled();
    expect(onUnsupportedSelection).toHaveBeenCalledOnce();
  });
});
