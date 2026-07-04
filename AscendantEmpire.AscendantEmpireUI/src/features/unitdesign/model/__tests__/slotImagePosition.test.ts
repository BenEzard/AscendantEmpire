import { describe, expect, it } from "vitest";

import { toNormalized, toPixel } from "../slotImagePosition";

const box = { width: 200, height: 100 };

describe("toNormalized", () => {
  it("maps the image center to (0, 0)", () => {
    expect(toNormalized(100, 50, box)).toEqual({ x: 0, y: 0 });
  });

  it("maps the edges to ±1", () => {
    // Left/top corner is (-1, -1); right/bottom corner is (+1, +1).
    expect(toNormalized(0, 0, box)).toEqual({ x: -1, y: -1 });
    expect(toNormalized(200, 100, box)).toEqual({ x: 1, y: 1 });
  });

  it("allows values beyond ±1 for points outside the image", () => {
    // 50px above the top edge is one further half-height up → y = -2.
    expect(toNormalized(100, -50, box)).toEqual({ x: 0, y: -2 });
  });

  it("rounds to 4 decimal places", () => {
    // 133px on a 200px-wide image → (133-100)/100 = 0.33 exactly; use an odd pixel to force rounding.
    const { x } = toNormalized(107, 50, box); // (107-100)/100 = 0.07
    expect(x).toBe(0.07);
    // A sub-0.0001 offset collapses to 0 (and never to -0).
    expect(toNormalized(100 + 0.004, 50, box)).toEqual({ x: 0, y: 0 });
  });

  it("returns the center when the image has no size yet", () => {
    expect(toNormalized(40, 40, { width: 0, height: 0 })).toEqual({ x: 0, y: 0 });
  });
});

describe("toPixel", () => {
  it("is the inverse of toNormalized for in-bounds points", () => {
    for (const point of [
      { px: 100, py: 50 },
      { px: 0, py: 0 },
      { px: 200, py: 100 },
      { px: 160, py: 25 },
    ]) {
      const normalized = toNormalized(point.px, point.py, box);
      const pixel = toPixel(normalized.x, normalized.y, box);
      expect(pixel.x).toBeCloseTo(point.px, 4);
      expect(pixel.y).toBeCloseTo(point.py, 4);
    }
  });
});
