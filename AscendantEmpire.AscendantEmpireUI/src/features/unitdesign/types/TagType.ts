// Editable client-side model for a Tag Type's metadata in the developer Entity Setup Tool.
//
// Mirrors the backend TagTypeInfo entity and the TagTypeView / UpsertTagTypeDto contracts. The set of
// tag types is fixed by the `TagType` enum (see ./Tag), so these rows are edited in place, not created.

import type { TagType } from "./Tag";

/**
 * A single tag type's editable metadata. `tagTypeId` is the enum value (its stable key); `colour` and
 * `icon` are optional presentation fields.
 */
export interface TagTypeInfo {
  /** Which tag type this row describes — the enum name (round-trips to the backend as-is). */
  tagTypeId: TagType;
  displayName: string;
  description: string;
  /** Stored/retrieved only — no behaviour is attached to this flag yet. */
  isInheritedToBase: boolean;
  /** Optional HTML colour string, e.g. "#3366cc". */
  colour: string | null;
  /** Optional icon identifier string. */
  icon: string | null;
}
