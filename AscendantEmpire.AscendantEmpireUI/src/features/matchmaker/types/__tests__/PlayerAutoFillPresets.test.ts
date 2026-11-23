import { PLAYER_AUTO_FILL_PRESETS, resolvePlayerAutoFillPreset } from "../PlayerAutoFillPresets";

describe("resolvePlayerAutoFillPreset", () => {
  it.each(PLAYER_AUTO_FILL_PRESETS.map((preset) => [preset.rulerName, preset] as const))(
    "finds the portrait and faction colour %s names, so a renamed file or swatch fails here rather than auto-filling a blank",
    (_name, preset) => {
      expect(() => resolvePlayerAutoFillPreset(preset)).not.toThrow();
    },
  );

  it("turns the portrait label and colour name into the stored path and value", () => {
    const resolved = resolvePlayerAutoFillPreset(PLAYER_AUTO_FILL_PRESETS[0]);

    expect(resolved.rulerPortraitPath).toBe("/portraits/01_western_king.png");
    expect(resolved.factionColour).toBe("#8200db");
  });

  it("refuses a portrait that belongs to the other gender", () => {
    expect(() => resolvePlayerAutoFillPreset({ ...PLAYER_AUTO_FILL_PRESETS[0], rulerPortraitLabel: "Egyptian Queen" })).toThrow(/portrait/);
  });
});
