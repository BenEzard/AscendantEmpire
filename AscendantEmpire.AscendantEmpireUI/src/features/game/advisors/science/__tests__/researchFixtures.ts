import type { PlayerResearchOverview, ResearchableTechnology, ResearchAcquisitionCost, ResearchGrant } from "../scienceApi";

// Shared fixtures for the Science Advisor tests. Not a test file itself — vitest only collects *.test.*.

/**
 * A researchable technology with everything defaulted, so each test states only the fields it is about.
 *
 * The defaults deliberately match the seeded catalogue's shape: a directed-only technology with no acquisition cost,
 * which is what every opening technology in the real catalogue is.
 */
export function researchable(technologyId: number, overrides: Partial<ResearchableTechnology> = {}): ResearchableTechnology {
  return {
    technologyId,
    code: `T${technologyId}`,
    displayName: `Technology ${technologyId}`,
    description: null,
    iconPath: "",
    era: "Stone",
    status: "Available",
    directedRequired: 100,
    experienceRequired: null,
    diffusionRequired: null,
    directedPoints: 0,
    experiencePoints: 0,
    diffusionPoints: 0,
    acquisitionCosts: [],
    grants: [],
    experienceSources: [],
    isCurrentProject: false,
    ...overrides,
  };
}

/** One Asset cost, defaulted so a test names only the asset and the quantity it is about. */
export function cost(assetId: number, assetDisplayName: string, quantity: number): ResearchAcquisitionCost {
  return {
    assetId,
    assetCode: assetDisplayName,
    assetDisplayName,
    // Empty on purpose: AssetIconImage falls back to a glyph, which is what the seeded catalogue's iconless rows produce anyway.
    assetIconPath: "",
    quantity,
  };
}

/** One grant, defaulted to an unlock of an Asset that still resolves. */
export function grant(targetId: number, targetDisplayName: string, overrides: Partial<ResearchGrant> = {}): ResearchGrant {
  return {
    targetType: "Asset",
    targetId,
    targetCode: targetDisplayName,
    targetDisplayName,
    grantEffect: "Unlock",
    ...overrides,
  };
}

/** A research position with nothing selected, carrying whichever technologies the test supplies. */
export function overview(technologies: ResearchableTechnology[], overrides: Partial<PlayerResearchOverview> = {}): PlayerResearchOverview {
  return {
    currentTechnologyId: null,
    currentTechnologyDisplayName: null,
    nextTechnologyId: null,
    nextTechnologyDisplayName: null,
    unspentDirectedPoints: 0,
    unspentDiffusionPoints: 0,
    currentRoundNumber: 1,
    researchable: technologies,
    ...overrides,
  };
}
