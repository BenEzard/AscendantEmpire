// Editable client-side model for a global Tag in the developer Entity Setup Tool.
//
// Mirrors the backend Domain entity (AscendantEmpire.Domain.Entities.UnitDesigns.Tag) and the
// UpsertTagDto / TagView contracts, so a Tag here maps straight onto the create/update payloads.

/**
 * Whether a tag is a structural classification or a bonus. Names match the C# `TagType` enum exactly
 * so the value round-trips to the backend without translation (same convention as `ComponentType`).
 */
export type TagType = "Ability" | "Combat" | "Classification" | "Movement" | "MilitaryOrganisationalGroup";

// Order follows the backend enum so dropdowns read the same as the design doc.
export const TAG_TYPES: readonly TagType[] = ["Ability", "Combat", "Classification", "Movement", "MilitaryOrganisationalGroup"];

import type { DomainType } from "./UnitComponent";

/**
 * How formations under a MilitaryOrganisationalGroup tag are led. Names match the C# `LeadershipModel`
 * enum exactly so the value round-trips to the backend without translation.
 */
export type LeadershipModel = "Commissioned" | "Unled";

export const LEADERSHIP_MODELS: readonly LeadershipModel[] = ["Commissioned", "Unled"];

/**
 * A single editable tag. `id` is null for a not-yet-saved tag (the backend assigns it on create).
 */
export interface Tag {
  id: number | null;
  /** Unique, stable key for the tag, e.g. "amphibious", "stealth". */
  code: string;
  displayName: string;
  /**
   * Optional free-text description. Held as a string here (empty when blank) so the textarea stays a
   * controlled input; the commit maps a blank value to null before sending (see TagUpsert).
   */
  description: string;
  tagType: TagType;
  /**
   * Movement domain — only meaningful for a MilitaryOrganisationalGroup tag (ties an organisation hierarchy
   * to a domain). Null for every other tag type. The backend rejects a domain set on a non-organisation tag.
   */
  movementDomain: DomainType | null;
  /**
   * Leadership model — only meaningful for a MilitaryOrganisationalGroup tag; null for every other tag type.
   * "Unled" formations have no officers and are sized by the three unled values below.
   */
  leadershipModel: LeadershipModel | null;
  /** The indivisible group size (quantum) for an Unled tag; null otherwise. */
  unledBaseSize: number | null;
  /** The size an unled unit takes when created without an explicit choice; a multiple of the base. */
  unledDefaultSize: number | null;
  /** The largest a single unled unit may be — the authored guardrail replacing the rank ceiling. */
  unledMaxSize: number | null;
  /** Whether the tag is system-created (seeded/built-in) rather than user-authored. Defaults to false. */
  systemCreated: boolean;
}

/** The tag fields that can carry a validation message. `unledSizes` covers all three size inputs together. */
export type TagFieldKey = "displayName" | "code" | "unledSizes";

/** Validation messages keyed by field; an empty object means the tag is valid. */
export type TagFieldErrors = Partial<Record<TagFieldKey, string>>;

/**
 * Required-field validation for a tag, as a fast local pre-check before committing. The backend applies
 * the same rules (see TagValidation), so this is a convenience for the editor rather than the source of
 * truth. Description is intentionally unchecked: it's optional (nullable column, no server-side rule).
 *
 * An Unled organisation tag authors its own sizing, so all three values are required and must cohere.
 */
export function validateTag(tag: Tag): TagFieldErrors {
  const errors: TagFieldErrors = {};

  if (tag.displayName.trim() === "") {
    errors.displayName = "Display Name is required.";
  }
  if (tag.code.trim() === "") {
    errors.code = "Code is required.";
  }

  if (tag.leadershipModel === "Unled") {
    const { unledBaseSize, unledDefaultSize, unledMaxSize } = tag;
    if (!unledBaseSize || !unledDefaultSize || !unledMaxSize) {
      errors.unledSizes = "An Unled tag requires all three sizes (base, default, max), each above 0.";
    } else if (unledBaseSize > unledDefaultSize || unledDefaultSize > unledMaxSize) {
      errors.unledSizes = "Sizes must satisfy base ≤ default ≤ max.";
    } else if (unledDefaultSize % unledBaseSize !== 0) {
      errors.unledSizes = "The default size must be a whole multiple of the base size.";
    }
  }

  return errors;
}

/**
 * Nulls out every field that doesn't apply to the tag's current shape, enforcing the two conditional
 * invariants the backend rejects payloads for:
 *
 *  - a movement domain and leadership model are only valid on a MilitaryOrganisationalGroup tag;
 *  - the three unled sizes are only valid while that tag's leadership model is Unled.
 *
 * Written as a whole-tag normalisation rather than a per-transition clear so the invariant is stated
 * once and holds no matter which field was just edited. It is idempotent — normalising an already
 * normalised tag returns an equivalent tag.
 */
export function normaliseTag(tag: Tag): Tag {
  const organisationGroup = tag.tagType === "MilitaryOrganisationalGroup";
  const unled = organisationGroup && tag.leadershipModel === "Unled";

  return {
    ...tag,
    movementDomain: organisationGroup ? tag.movementDomain : null,
    leadershipModel: organisationGroup ? tag.leadershipModel : null,
    unledBaseSize: unled ? tag.unledBaseSize : null,
    unledDefaultSize: unled ? tag.unledDefaultSize : null,
    unledMaxSize: unled ? tag.unledMaxSize : null,
  };
}

/** A blank tag for the "Add Tag" flow — defaults to a Classification. */
export function createEmptyTag(): Tag {
  return {
    id: null,
    code: "",
    displayName: "",
    description: "",
    tagType: "Classification",
    movementDomain: null,
    leadershipModel: null,
    unledBaseSize: null,
    unledDefaultSize: null,
    unledMaxSize: null,
    systemCreated: false,
  };
}
