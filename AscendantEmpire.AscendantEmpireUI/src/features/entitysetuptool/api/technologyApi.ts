import { http } from "@/api/http";

// The technology catalogue, read-only. These types mirror the AscendantEmpire.FrontendContracts.Technologies.GetTechnologies interface family member for
// member — ITechnologyCatalogue, ICatalogueTechnology, ICataloguePrerequisite, ICatalogueAcquisitionCost, ICatalogueGrant and ICatalogueExperienceSource.
//
// There is no upsert here on purpose. PUT /technology exists and the Entity Setup Tool permission would allow it, but the tree viewer only ever reads, and a
// save path nothing calls is a save path nobody maintains. Authoring still happens through the database seed.

/**
 * Which age a technology belongs to.
 *
 * Mirrors the backend TechnologyEra enum, which the API serialises as its member name rather than its number (JsonStringEnumConverter is registered in
 * Program.cs). Era has exactly one consumer on the backend — the RandomisedEqual requirement generator, which balances per era — so it is a catalogue value
 * to display, not a dimension to lay out or filter by.
 */
export type TechnologyEra = "Stone" | "Bronze" | "Iron" | "Classical" | "Medieval" | "Renaissance" | "Industrial" | "Modern";

/**
 * Whether a prerequisite edge must be satisfied, or merely contributes to an any-of group.
 *
 * Mirrors the backend TechnologyPrerequisiteKind enum, sent as its member name. The rule the two kinds encode: a technology becomes available once **all** of
 * its Mandatory prerequisites are discovered **and**, if it has any Optional prerequisites at all, at least one of those is too. The optional edges of one
 * technology therefore form a single implicit "any one of these" group — there is no way to express two separate any-of groups.
 */
export type TechnologyPrerequisiteKind = "Mandatory" | "Optional";

/** Whether discovering a technology makes its target available or retires it. Mirrors the backend TechnologyGrantEffect enum, sent as its member name. */
export type TechnologyGrantEffect = "Unlock" | "Obsolete";

/**
 * Which catalogue a grant's target lives in.
 *
 * Mirrors the backend TechnologyGrantTargetType enum, sent as its member name. A grant's target id points at a different table for each of these, which is
 * why it carries no foreign key and why the target's code and display name can come back null.
 */
export type TechnologyGrantTargetType = "Asset" | "AssetMetaCategory" | "UnitComponent" | "UnitTemplate" | "MapResourceType";

/** One Asset, and how much of it, a technology costs to unlock. No rows at all means the technology is free. */
export interface CatalogueAcquisitionCost {
  assetId: number;
  assetCode: string;
  assetDisplayName: string;
  quantity: number;
}

/** One technology that must be discovered before another, and whether that requirement is mandatory or one of an any-of group. */
export interface CataloguePrerequisite {
  prerequisiteTechnologyId: number;
  prerequisiteTechnologyCode: string;
  prerequisiteTechnologyDisplayName: string;
  prerequisiteKind: TechnologyPrerequisiteKind;
}

/**
 * One thing a technology unlocks or retires when it is discovered.
 *
 * The target code and display name are **null when the target no longer exists**: a grant carries no foreign key, because it points at a different table per
 * target type, so it can outlive what it points at. The server resolves what it can and reports the rest rather than hiding it, and so does the viewer — a
 * dangling grant is exactly the kind of catalogue damage a developer tool exists to surface.
 */
export interface CatalogueGrant {
  targetType: TechnologyGrantTargetType;
  targetId: number;
  targetCode: string | null;
  targetDisplayName: string | null;
  grantEffect: TechnologyGrantEffect;
}

/** One kind of experience that fills a technology's experience bucket. A mapping only — it carries no quantity. */
export interface CatalogueExperienceSource {
  experienceTypeId: number;
  experienceTypeCode: string;
  experienceTypeDisplayName: string;
}

/**
 * One technology in the catalogue, with everything hanging off it.
 *
 * The three requirement figures are the **catalogue's base** values, never a player's. An unused bucket is null rather than zero, so "does this technology
 * need experience?" and "how much?" are the same question. Anything rendering them has to keep that distinction: a bucket shown as zero reads as "free",
 * which is the opposite of "not part of this technology".
 *
 * The tree column and row are an optional authored position for the tree view. Both are null throughout the seeded catalogue, so the layout computes
 * positions from the prerequisite graph and defers to these only when an author has set them.
 */
export interface CatalogueTechnology {
  id: number;
  /** Unique, hierarchical, and the key both prerequisites and the upsert match on. */
  code: string;
  displayName: string;
  description: string | null;
  iconPath: string;
  era: TechnologyEra;
  sortOrder: number;
  treeColumn: number | null;
  treeRow: number | null;
  /** Base directed-research points required, or null when this technology does not use the directed bucket. */
  directedRequired: number | null;
  /** Base experience points required, or null when this technology does not use the experience bucket. */
  experienceRequired: number | null;
  /** Base diffusion points required, or null when this technology does not use the diffusion bucket. */
  diffusionRequired: number | null;
  acquisitionCosts: CatalogueAcquisitionCost[];
  prerequisites: CataloguePrerequisite[];
  grants: CatalogueGrant[];
  experienceSources: CatalogueExperienceSource[];
}

/**
 * GET /technology — the whole technology catalogue, with each technology's acquisition costs, prerequisites, grants and experience sources.
 *
 * Unwrapped from the response's single technologies array, as every api function here returns the payload rather than the envelope. The read is
 * player-independent while technology customisation is Identical, so one fetch serves the whole page.
 */
export async function getTechnologyCatalogue(signal?: AbortSignal): Promise<CatalogueTechnology[]> {
  const { data } = await http.get<{ technologies: CatalogueTechnology[] }>("/technology", { signal });
  return data.technologies;
}
