import { formatCompactAmount } from "../number";

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
