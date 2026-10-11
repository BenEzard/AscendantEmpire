import { act, renderHook } from "@testing-library/react";
import type { KeyboardEvent } from "react";
import { describe, expect, it, vi } from "vitest";
import { useSvgViewport } from "../useSvgViewport";

// The viewport arithmetic: fitting, clamping, anchoring and centring.
//
// Driven through the hook's commands rather than through real wheel and pointer events. jsdom has no layout — every element measures zero by zero — and no
// pointer capture, so a synthetic gesture would exercise the event plumbing against numbers that do not exist. The container size is injected instead, which
// is what the initialContainerSize option is for, and the maths underneath wheel and drag is reached through zoomBy and centreOn directly.

/** A pane twice as wide as it is tall, so a mistake that confuses the two axes shows up as a wrong number rather than the same one. */
const CONTAINER = { width: 1000, height: 500 };

/** Content that fits without hitting either scale limit, so the fitting tests measure fitting and not clamping. */
const CONTENT = { width: 2000, height: 500 };

function renderViewport(content = CONTENT) {
  return renderHook(() => useSvgViewport(content, { initialContainerSize: CONTAINER }));
}

/** A keyboard event with just the parts the handler reads, plus a spy on preventDefault. */
function keyEvent(key: string) {
  const preventDefault = vi.fn();

  return { event: { key, preventDefault } as unknown as KeyboardEvent, preventDefault };
}

// ─── Fitting ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("useSvgViewport fitting", () => {
  it("opens on a view containing the whole canvas", () => {
    const { result } = renderViewport();

    // Fitted on the first render, because you should see the shape of the tree before you try to read any of it.
    expect(result.current.viewport.x).toBeLessThanOrEqual(0);
    expect(result.current.viewport.y).toBeLessThanOrEqual(0);
    expect(result.current.viewport.x + result.current.viewport.width).toBeGreaterThanOrEqual(CONTENT.width);
    expect(result.current.viewport.y + result.current.viewport.height).toBeGreaterThanOrEqual(CONTENT.height);
  });

  it("keeps the container's aspect ratio, so nothing is stretched", () => {
    const { result } = renderViewport();

    expect(result.current.viewport.height / result.current.viewport.width).toBeCloseTo(CONTAINER.height / CONTAINER.width);
  });

  it("centres content that is shorter than the view it is fitted into", () => {
    const { result } = renderViewport();

    // Fitting a 2000 × 500 canvas into a 2:1 pane gives a 2000 × 1000 view, so the content sits in the middle of it with 250 spare above and below.
    expect(result.current.viewport.width).toBe(2000);
    expect(result.current.viewport.height).toBe(1000);
    expect(result.current.viewport.y).toBe(-250);
  });

  it("reports the viewBox as the viewport it just described", () => {
    const { result } = renderViewport();
    const { x, y, width, height } = result.current.viewport;

    expect(result.current.viewBox).toBe(`${x} ${y} ${width} ${height}`);
  });

  it("does nothing when there is no content yet, rather than dividing by zero", () => {
    const { result } = renderViewport({ width: 0, height: 0 });

    act(() => result.current.fitToContent());

    expect(Number.isFinite(result.current.viewport.width)).toBe(true);
    expect(Number.isFinite(result.current.scale)).toBe(true);
  });
});

// ─── Zooming ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("useSvgViewport zooming", () => {
  it("zooms in on a factor above one and out on a factor below", () => {
    const { result } = renderViewport();
    const fittedScale = result.current.scale;

    act(() => result.current.zoomBy(1.5));
    expect(result.current.scale).toBeGreaterThan(fittedScale);

    act(() => result.current.zoomBy(1 / 1.5));
    expect(result.current.scale).toBeCloseTo(fittedScale);
  });

  it("clamps at the maximum scale however hard it is pushed", () => {
    const { result } = renderViewport();

    act(() => result.current.zoomBy(1000));

    expect(result.current.scale).toBeCloseTo(2.5);
  });

  it("clamps at the minimum scale however far it is pulled", () => {
    const { result } = renderViewport();

    act(() => result.current.zoomBy(0.0001));

    expect(result.current.scale).toBeCloseTo(0.15);
  });

  it("keeps the point at the middle of the view under the middle of the view", () => {
    // With no layout to measure, an anchored zoom falls back to the centre of the view — which is the same arithmetic the cursor anchor uses, so this is
    // what pins it.
    const { result } = renderViewport();
    const centreBefore = {
      x: result.current.viewport.x + result.current.viewport.width / 2,
      y: result.current.viewport.y + result.current.viewport.height / 2,
    };

    act(() => result.current.zoomBy(2, { clientX: 123, clientY: 456 }));

    expect(result.current.viewport.x + result.current.viewport.width / 2).toBeCloseTo(centreBefore.x);
    expect(result.current.viewport.y + result.current.viewport.height / 2).toBeCloseTo(centreBefore.y);
  });

  it("holds the anchor exactly at the clamp boundary instead of drifting", () => {
    const { result } = renderViewport();
    const centreBefore = {
      x: result.current.viewport.x + result.current.viewport.width / 2,
      y: result.current.viewport.y + result.current.viewport.height / 2,
    };

    // Asking for far more zoom than the limit allows: the width is clamped first, so the anchor still lands where it should.
    act(() => result.current.zoomBy(1000));

    expect(result.current.scale).toBeCloseTo(2.5);
    expect(result.current.viewport.x + result.current.viewport.width / 2).toBeCloseTo(centreBefore.x);
    expect(result.current.viewport.y + result.current.viewport.height / 2).toBeCloseTo(centreBefore.y);
  });

  it("returns to natural size at the canvas's top-left corner when reset", () => {
    const { result } = renderViewport();

    act(() => result.current.zoomBy(2));
    act(() => result.current.resetZoom());

    expect(result.current.scale).toBe(1);
    expect(result.current.viewport.x).toBe(0);
    expect(result.current.viewport.y).toBe(0);
  });
});

// ─── Centring ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("useSvgViewport centring", () => {
  it("brings a content point to the middle of the view without changing the zoom", () => {
    const { result } = renderViewport();
    const widthBefore = result.current.viewport.width;

    act(() => result.current.centreOn({ x: 1500, y: 300 }));

    expect(result.current.viewport.x + result.current.viewport.width / 2).toBeCloseTo(1500);
    expect(result.current.viewport.y + result.current.viewport.height / 2).toBeCloseTo(300);
    expect(result.current.viewport.width).toBe(widthBefore);
  });
});

// ─── Keyboard ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("useSvgViewport keyboard", () => {
  it("pans on the arrow keys, by a fraction of the view rather than a fixed number of pixels", () => {
    const { result } = renderViewport();
    const before = result.current.viewport;

    act(() => result.current.handleKeyDown(keyEvent("ArrowRight").event));
    expect(result.current.viewport.x).toBeCloseTo(before.x + before.width * 0.1);

    act(() => result.current.handleKeyDown(keyEvent("ArrowDown").event));
    expect(result.current.viewport.y).toBeCloseTo(before.y + before.height * 0.1);
  });

  it("fits on 0 and resets on 1", () => {
    const { result } = renderViewport();

    act(() => result.current.handleKeyDown(keyEvent("1").event));
    expect(result.current.scale).toBe(1);

    act(() => result.current.handleKeyDown(keyEvent("0").event));
    expect(result.current.viewport.width).toBe(2000);
  });

  it("zooms on plus and minus", () => {
    const { result } = renderViewport();
    const fittedScale = result.current.scale;

    act(() => result.current.handleKeyDown(keyEvent("+").event));
    expect(result.current.scale).toBeGreaterThan(fittedScale);

    act(() => result.current.handleKeyDown(keyEvent("-").event));
    expect(result.current.scale).toBeCloseTo(fittedScale);
  });

  it("leaves a key it does not handle to whatever else is listening", () => {
    const { result } = renderViewport();
    const handled = keyEvent("0");
    const ignored = keyEvent("Escape");

    act(() => result.current.handleKeyDown(handled.event));
    act(() => result.current.handleKeyDown(ignored.event));

    // Escape in particular has to reach the viewer, which closes on it.
    expect(handled.preventDefault).toHaveBeenCalled();
    expect(ignored.preventDefault).not.toHaveBeenCalled();
  });
});
