// API service for the Science Advisor: where the calling player's research stands, and what they choose to put it into.
// Thin wrappers over the shared axios `http` client, which carries the game token — the backend reads the game and player
// off it, so nothing player-scoped travels in these payloads.
//
// These types mirror the AscendantEmpire.FrontendContracts.Technologies.GetPlayerResearch interface family member for
// member — IPlayerResearchOverview and IResearchableTechnology — plus ICatalogueAcquisitionCost and
// ICatalogueExperienceSource, which the research read reuses from the GetTechnologies family rather than redeclaring.
//
// Enums round-trip as their string names (the API registers a JsonStringEnumConverter), so TechnologyStatus arrives as
// "Available" or "Unlocked" and passes straight through without translation.

import { http } from "@/api/http";

import type { TechnologyEra, TechnologyGrantEffect, TechnologyGrantTargetType } from "@/features/entitysetuptool/api/technologyApi";

/**
 * How far the player has got with one technology, for the two statuses this endpoint serves.
 *
 * The full backend enum has four members. Locked and Discovered never reach here: the read returns only what the player
 * could research now, and neither of those is — one has unmet prerequisites, the other is finished. Narrowing the union
 * rather than mirroring all four is what makes "is this researchable?" unaskable on the client, because everything in
 * hand already is.
 */
export type ResearchStatus = "Available" | "Unlocked";

/** One Asset, and how much of it, a technology costs to unlock. No entries at all means the technology is free. */
export interface ResearchAcquisitionCost {
  assetId: number;
  assetCode: string;
  assetDisplayName: string;
  /** The Asset's catalogue image, as a root-relative URL. Empty where it has no icon, which AssetIconImage renders as a glyph. */
  assetIconPath: string;
  quantity: number;
}

/**
 * One thing discovering a technology unlocks or retires.
 *
 * The target code and display name are **null when the target no longer exists**: a grant carries no foreign key, because it points at a
 * different table per target type, so it can outlive what it points at. The server resolves what it can and reports the rest rather than
 * hiding it, and so does the card.
 *
 * Empty for every technology in the seeded catalogue, which authors no grants at all — what a technology gives is carried as text in its
 * description for now.
 */
export interface ResearchGrant {
  targetType: TechnologyGrantTargetType;
  targetId: number;
  targetCode: string | null;
  targetDisplayName: string | null;
  grantEffect: TechnologyGrantEffect;
}

/** One kind of experience that fills a technology's experience bucket. A mapping only — it carries no quantity. */
export interface ResearchExperienceSource {
  experienceTypeId: number;
  experienceTypeCode: string;
  experienceTypeDisplayName: string;
}

/**
 * One technology the player could put research into right now.
 *
 * The three requirement figures are **this player's own**, not the catalogue's base numbers, and an unused bucket is
 * null rather than zero — so "does this technology need experience?" and "how much?" are the same question. Anything
 * rendering them has to keep that apart: a bucket shown as 0 reads as "free", which is the opposite of "not part of
 * this technology".
 */
export interface ResearchableTechnology {
  technologyId: number;
  /** Unique and hierarchical; the key the catalogue and its prerequisites match on. */
  code: string;
  displayName: string;
  description: string | null;
  iconPath: string;
  era: TechnologyEra;
  status: ResearchStatus;
  /** This player's directed-research requirement, or null when this technology does not use the directed bucket. */
  directedRequired: number | null;
  /** This player's experience requirement, or null when this technology does not use the experience bucket. */
  experienceRequired: number | null;
  /** This player's diffusion requirement, or null when this technology does not use the diffusion bucket. */
  diffusionRequired: number | null;
  directedPoints: number;
  experiencePoints: number;
  diffusionPoints: number;
  acquisitionCosts: ResearchAcquisitionCost[];
  /** What discovering it unlocks or retires. Empty throughout the seeded catalogue, which authors no grants. */
  grants: ResearchGrant[];
  experienceSources: ResearchExperienceSource[];
  /** Whether this is already the technology directed research is going into. */
  isCurrentProject: boolean;
}

/** Where the calling player's research stands (mirrors IPlayerResearchOverview). */
export interface PlayerResearchOverview {
  /** The current project, or null when none is selected — which is a real state, not a missing value. */
  currentTechnologyId: number | null;
  currentTechnologyDisplayName: string | null;
  /** The project to switch to automatically when the current one completes, or null when none is pre-chosen. */
  nextTechnologyId: number | null;
  nextTechnologyDisplayName: string | null;
  unspentDirectedPoints: number;
  unspentDiffusionPoints: number;
  /** The Round the game's campaign lane is on. */
  currentRoundNumber: number;
  /** Everything researchable now, in era and then sort order. Array order is render order. */
  researchable: ResearchableTechnology[];
}

/**
 * GET /technology/research — the calling player's research position.
 *
 * Unlike the catalogue read this is player-specific and moves as the game runs, so it is not cacheable for the life of
 * a game. Returned as the payload rather than the envelope, as every api function here does.
 */
export async function getPlayerResearch(signal?: AbortSignal): Promise<PlayerResearchOverview> {
  const { data } = await http.get<PlayerResearchOverview>("/technology/research", { signal });
  return data;
}

/**
 * PUT /technology/research-project — point directed research at a technology, or pass null to stop researching.
 *
 * Choosing an Available technology also settles its acquisition cost and unlocks it, because directed research cannot
 * begin until that cost is paid. The server refuses a technology that actually carries a cost, since nothing spends
 * from a player's Asset banks yet.
 */
export async function setResearchProject(technologyId: number | null): Promise<void> {
  await http.put("/technology/research-project", { technologyId });
}
