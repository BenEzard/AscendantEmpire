// Pure-function tests for the Tag model's two conditional rules. Both are exercised through the editor
// (TagSetupDialog), but they're plain functions over a Tag, so they're tested directly here rather than
// by driving the dialog's DOM. Their failure mode is a payload the backend rejects, so the cases below
// mirror the backend's own rules (TagValidation).

import { describe, expect, it } from "vitest";

import { createEmptyTag, normaliseTag, validateTag, type Tag } from "../Tag";

// A valid, minimally-filled tag. Individual tests override just the fields they're about.
function makeTag(overrides: Partial<Tag> = {}): Tag {
  return { ...createEmptyTag(), code: "amphibious", displayName: "Amphibious", ...overrides };
}

// A MilitaryOrganisationalGroup tag with a complete, coherent unled sizing (base 10, default 20, max 100).
function makeUnledTag(overrides: Partial<Tag> = {}): Tag {
  return makeTag({
    tagType: "MilitaryOrganisationalGroup",
    leadershipModel: "Unled",
    unledBaseSize: 10,
    unledDefaultSize: 20,
    unledMaxSize: 100,
    ...overrides,
  });
}

// ─── validateTag: the always-required fields ──────────────────────────────────

describe("validateTag: required fields", () => {
  it("accepts a tag with both a display name and a code", () => {
    expect(validateTag(makeTag())).toEqual({});
  });

  it("requires a display name", () => {
    expect(validateTag(makeTag({ displayName: "" })).displayName).toBeDefined();
  });

  it("requires a code", () => {
    expect(validateTag(makeTag({ code: "" })).code).toBeDefined();
  });

  it("treats whitespace-only values as blank, so a space cannot satisfy the requirement", () => {
    const errors = validateTag(makeTag({ displayName: "   ", code: "\t" }));

    expect(errors.displayName).toBeDefined();
    expect(errors.code).toBeDefined();
  });

  it("does not require a description, which is optional on the backend too", () => {
    expect(validateTag(makeTag({ description: "" }))).toEqual({});
  });

  it("reports every problem at once rather than stopping at the first", () => {
    const errors = validateTag(makeUnledTag({ displayName: "", code: "", unledBaseSize: null }));

    expect(errors.displayName).toBeDefined();
    expect(errors.code).toBeDefined();
    expect(errors.unledSizes).toBeDefined();
  });
});

// ─── validateTag: the unled sizing rules ──────────────────────────────────────

describe("validateTag: unled sizing", () => {
  it("accepts a coherent set of sizes", () => {
    expect(validateTag(makeUnledTag()).unledSizes).toBeUndefined();
  });

  it("ignores sizing entirely for a Commissioned tag, which is sized by its rank ladder instead", () => {
    const commissioned = makeTag({ tagType: "MilitaryOrganisationalGroup", leadershipModel: "Commissioned" });

    expect(validateTag(commissioned)).toEqual({});
  });

  // All three are required together — an Unled tag authors its own sizing, so a partial set is unusable.
  it.each([["unledBaseSize"], ["unledDefaultSize"], ["unledMaxSize"]] as const)("requires %s", (field) => {
    expect(validateTag(makeUnledTag({ [field]: null })).unledSizes).toBeDefined();
  });

  // Zero is rejected the same way a missing value is: a size of 0 would make the tag meaningless (and
  // a zero base would divide by zero in the multiple check below).
  it.each([["unledBaseSize"], ["unledDefaultSize"], ["unledMaxSize"]] as const)("rejects a zero %s", (field) => {
    expect(validateTag(makeUnledTag({ [field]: 0 })).unledSizes).toBeDefined();
  });

  it("rejects a base larger than the default", () => {
    expect(validateTag(makeUnledTag({ unledBaseSize: 30, unledDefaultSize: 20 })).unledSizes).toBeDefined();
  });

  it("rejects a default larger than the max", () => {
    expect(validateTag(makeUnledTag({ unledDefaultSize: 200, unledMaxSize: 100 })).unledSizes).toBeDefined();
  });

  it("allows the sizes to be equal, since base ≤ default ≤ max is inclusive", () => {
    expect(validateTag(makeUnledTag({ unledBaseSize: 10, unledDefaultSize: 10, unledMaxSize: 10 })).unledSizes).toBeUndefined();
  });

  it("requires the default to be a whole multiple of the base", () => {
    // 25 sits between base and max but isn't a multiple of 10, so it isn't a legal unit size.
    expect(validateTag(makeUnledTag({ unledBaseSize: 10, unledDefaultSize: 25 })).unledSizes).toBeDefined();
  });

  it("does not require the max to be a whole multiple of the base, which only caps the size", () => {
    expect(validateTag(makeUnledTag({ unledBaseSize: 10, unledDefaultSize: 20, unledMaxSize: 95 })).unledSizes).toBeUndefined();
  });
});

// ─── normaliseTag ─────────────────────────────────────────────────────────────

describe("normaliseTag", () => {
  it("keeps every organisation-group field on a MilitaryOrganisationalGroup Unled tag", () => {
    const tag = makeUnledTag({ movementDomain: "Land" });

    expect(normaliseTag(tag)).toEqual(tag);
  });

  it("clears the domain, leadership model and sizes when the type is not an organisation group", () => {
    // The state left behind by switching an authored organisation tag to a plain Classification.
    const normalised = normaliseTag(makeUnledTag({ tagType: "Classification", movementDomain: "Land" }));

    expect(normalised.movementDomain).toBeNull();
    expect(normalised.leadershipModel).toBeNull();
    expect(normalised.unledBaseSize).toBeNull();
    expect(normalised.unledDefaultSize).toBeNull();
    expect(normalised.unledMaxSize).toBeNull();
  });

  it("clears the sizes but keeps the domain when an organisation tag turns Commissioned", () => {
    const normalised = normaliseTag(makeUnledTag({ leadershipModel: "Commissioned", movementDomain: "Land" }));

    expect(normalised.leadershipModel).toBe("Commissioned");
    expect(normalised.movementDomain).toBe("Land");
    expect(normalised.unledBaseSize).toBeNull();
    expect(normalised.unledDefaultSize).toBeNull();
    expect(normalised.unledMaxSize).toBeNull();
  });

  it("clears the sizes when an organisation tag has no leadership model chosen yet", () => {
    const normalised = normaliseTag(makeUnledTag({ leadershipModel: null }));

    expect(normalised.unledBaseSize).toBeNull();
    expect(normalised.unledMaxSize).toBeNull();
  });

  it("leaves the fields that are valid on every tag type untouched", () => {
    const normalised = normaliseTag(makeUnledTag({ tagType: "Movement", description: "Crosses water", systemCreated: true }));

    expect(normalised.code).toBe("amphibious");
    expect(normalised.displayName).toBe("Amphibious");
    expect(normalised.description).toBe("Crosses water");
    expect(normalised.tagType).toBe("Movement");
    expect(normalised.systemCreated).toBe(true);
  });

  it("is idempotent, so re-normalising an already normalised tag changes nothing further", () => {
    const once = normaliseTag(makeUnledTag({ tagType: "Combat", movementDomain: "Land" }));

    expect(normaliseTag(once)).toEqual(once);
  });

  it("does not mutate the tag it is given", () => {
    const tag = makeUnledTag({ tagType: "Classification", movementDomain: "Land" });

    normaliseTag(tag);

    expect(tag.movementDomain).toBe("Land");
    expect(tag.leadershipModel).toBe("Unled");
    expect(tag.unledBaseSize).toBe(10);
  });

  it("produces a tag that passes validation once the inapplicable sizing is dropped", () => {
    // An incoherent unled sizing stops mattering the moment the tag is no longer Unled.
    const normalised = normaliseTag(makeUnledTag({ tagType: "Classification", unledBaseSize: 999 }));

    expect(validateTag(normalised)).toEqual({});
  });
});
