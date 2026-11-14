import { describeEstimatedAmount, formatCompactAmount, formatEstimatedAmount } from "../number";

describe("formatCompactAmount", () => {
  /** Always rounded down, so a tile never promises food or ore it doesn't have. */
  it.each([
    [0, "0"],
    [334.9, "334"],
    [999.99, "999"],
    [1000, "1k"],
    [1299, "1.2k"],
    [12345, "12.3k"],
  ])("formats %d as %s", (value, expected) => {
    expect(formatCompactAmount(value)).toBe(expected);
  });
});

describe("formatEstimatedAmount", () => {
  it("prefixes an estimate with a tilde, so a fuzzed figure never reads as exact", () => {
    expect(formatEstimatedAmount(1299)).toBe("~1.2k");
  });

  it("writes the figure exactly as the compact formatter would, so the two never disagree on a tile", () => {
    expect(formatEstimatedAmount(334.9)).toBe(`~${formatCompactAmount(334.9)}`);
  });
});

describe("describeEstimatedAmount", () => {
  it("names an estimate in words for a screen reader, the glyph being for the eye only", () => {
    expect(describeEstimatedAmount(1299)).toBe("about 1.2k");
  });
});
