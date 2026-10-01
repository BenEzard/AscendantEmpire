import { describe, expect, it } from "vitest";
import { fromRegenerationPercentInput, toRegenerationPercentInput } from "../regenerationPercent";

describe("toRegenerationPercentInput", () => {
  it("shows the stored fraction as a percent", () => {
    expect(toRegenerationPercentInput(0.25)).toBe(25);
  });

  it("keeps float noise out of the input", () => {
    expect(toRegenerationPercentInput(0.07)).toBe(7);
    expect(toRegenerationPercentInput(0.125)).toBe(12.5);
  });

  it("shows nothing when the biome has no Regeneration Percent", () => {
    expect(toRegenerationPercentInput(null)).toBe("");
    expect(toRegenerationPercentInput(undefined)).toBe("");
  });
});

describe("fromRegenerationPercentInput", () => {
  it("stores the typed percent as a fraction", () => {
    expect(fromRegenerationPercentInput("25")).toBe(0.25);
    expect(fromRegenerationPercentInput("7")).toBe(0.07);
    expect(fromRegenerationPercentInput("12.5")).toBe(0.125);
  });

  it("rounds to the thousandths the server keeps", () => {
    expect(fromRegenerationPercentInput("12.34")).toBe(0.123);
  });

  it("stores null for an empty input", () => {
    expect(fromRegenerationPercentInput("")).toBeNull();
    expect(fromRegenerationPercentInput("  ")).toBeNull();
  });

  it("round-trips with the display value", () => {
    expect(fromRegenerationPercentInput(String(toRegenerationPercentInput(0.3)))).toBe(0.3);
  });
});
