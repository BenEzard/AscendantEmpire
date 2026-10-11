import type { CatalogueTechnology, CataloguePrerequisite, TechnologyPrerequisiteKind } from "../../../api/technologyApi";

// Shared fixtures for the technology tree component tests. Not a test file itself — vitest only collects *.test.*.

/** A prerequisite edge, named by the id it points at. */
export function needs(id: number, kind: TechnologyPrerequisiteKind = "Mandatory"): CataloguePrerequisite {
  return {
    prerequisiteTechnologyId: id,
    prerequisiteTechnologyCode: `T${id}`,
    prerequisiteTechnologyDisplayName: `Technology ${id}`,
    prerequisiteKind: kind,
  };
}

/** A catalogue technology with everything defaulted, so each test states only the fields it is about. */
export function tech(id: number, overrides: Partial<CatalogueTechnology> = {}): CatalogueTechnology {
  return {
    id,
    code: `T${id}`,
    displayName: `Technology ${id}`,
    description: null,
    iconPath: "",
    era: "Stone",
    sortOrder: id * 10,
    treeColumn: null,
    treeRow: null,
    directedRequired: 100,
    experienceRequired: null,
    diffusionRequired: null,
    acquisitionCosts: [],
    prerequisites: [],
    grants: [],
    experienceSources: [],
    ...overrides,
  };
}

/**
 * A small catalogue with both kinds of prerequisite in it.
 *
 * Technology 4 needs 2 and 3 outright and either of 1 or 5, which is the one shape that exercises the mandatory and optional headings together.
 */
export function mixedCatalogue(): CatalogueTechnology[] {
  return [
    tech(1),
    tech(2, { prerequisites: [needs(1)] }),
    tech(3, { prerequisites: [needs(1)] }),
    tech(4, { prerequisites: [needs(2), needs(3), needs(1, "Optional"), needs(5, "Optional")] }),
    tech(5, { prerequisites: [needs(1)] }),
    tech(6, { prerequisites: [needs(4)] }),
  ];
}
