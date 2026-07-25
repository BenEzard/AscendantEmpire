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
  /** Whether the tag is system-created (seeded/built-in) rather than user-authored. Defaults to false. */
  systemCreated: boolean;
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
    systemCreated: false,
  };
}
