// Pure-function tests for the shared dialog title helper. The rule it encodes is small but easy to get
// wrong when spelled out by hand in each dialog — specifically the blank-name fallback, whose failure mode
// is a title ending in a dangling colon while a record is still being named.

import { describe, expect, it } from "vitest";

import { dialogTitle } from "../dialogTitle";

describe("dialogTitle", () => {
  it("appends the record name after a colon", () => {
    expect(dialogTitle("Tag Type Setup", "Role")).toBe("Tag Type Setup: Role");
  });

  it("falls back to the bare base title when no record is selected", () => {
    expect(dialogTitle("Tag Type Setup", null)).toBe("Tag Type Setup");
    expect(dialogTitle("Tag Type Setup", undefined)).toBe("Tag Type Setup");
  });

  it("falls back to the bare base title for a record that is not named yet", () => {
    expect(dialogTitle("Component Setup", "")).toBe("Component Setup");
    expect(dialogTitle("Component Setup", "   ")).toBe("Component Setup");
  });

  it("trims surrounding whitespace off the record name", () => {
    expect(dialogTitle("Tag Setup", "  Amphibious  ")).toBe("Tag Setup: Amphibious");
  });

  it("keeps whitespace inside the record name", () => {
    expect(dialogTitle("Permissions", "ben smith")).toBe("Permissions: ben smith");
  });
});
