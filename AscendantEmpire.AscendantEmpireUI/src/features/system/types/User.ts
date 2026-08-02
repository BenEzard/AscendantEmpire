// Client-side model for an admin-facing user in the developer System tool.
//
// Mirrors the backend GetUsers contract (AscendantEmpire.BackendContracts.Authentication.GetUsers.AuthUser)
// and the InactiveReasonType domain enum (AscendantEmpire.Domain.Enums.Authentication.InactiveReasonType).
//
// IMPORTANT: the API serializes enums as their NAME, not their numeric value — AscendantEmpire.Api/Program.cs
// registers a JsonStringEnumConverter. So InactiveReasonType travels as e.g. "Active" both inbound
// (GET /auth/users) and outbound (POST /auth/users/inactive), and is modelled as a string union here so values
// round-trip without translation.

import type { PermissionType } from "./Permission";

/**
 * The reason an account is inactive, or `Active` while it is still usable. Names match the C#
 * `InactiveReasonType` enum exactly.
 */
export type InactiveReasonType = "PreActivation" | "Active" | "Quit" | "Banned" | "Deleted";

/**
 * The reason values by name.
 *
 * Kept as an object so call sites read as `InactiveReason.Banned` rather than a bare string literal, which makes a
 * typo a compile error instead of a value that silently never matches.
 */
export const InactiveReason = {
  PreActivation: "PreActivation",
  Active: "Active",
  Quit: "Quit",
  Banned: "Banned",
  Deleted: "Deleted",
} as const satisfies Record<InactiveReasonType, InactiveReasonType>;

/**
 * A single user as returned by GET /auth/users. Field names are camelCase to match the JSON the API
 * emits. `inactiveAt` is null while the account is active.
 */
export interface AuthUser {
  id: number;
  username: string;
  email: string | null;
  createdAt: string;
  inactiveAt: string | null;
  inactiveReasonType: InactiveReasonType;
  /** The capabilities currently granted. Returned with the list so the permissions dialog opens on data already held. */
  permissions: PermissionType[];
}

/** Human-readable label for every reason, used when rendering a user's current status. */
export const REASON_LABELS: Record<InactiveReasonType, string> = {
  [InactiveReason.PreActivation]: "Pre-Activation",
  [InactiveReason.Active]: "Active",
  [InactiveReason.Quit]: "Quit",
  [InactiveReason.Banned]: "Banned",
  [InactiveReason.Deleted]: "Deleted",
};

/** A reason the admin can apply to a user from the table's action menu. */
export interface InactivationReasonOption {
  reason: InactiveReasonType;
  label: string;
  /** Destructive reasons (Banned/Deleted) require a confirmation step before they are applied. */
  destructive: boolean;
}

/**
 * The inactivation reasons offered per user, in menu order. `PreActivation` is intentionally excluded
 * because it is a system-assigned state, not something an admin sets manually. Reactivation (sending
 * `Active`) is handled separately by the table since it is only shown for already-inactive users.
 */
export const INACTIVATION_REASONS: readonly InactivationReasonOption[] = [
  { reason: InactiveReason.Quit, label: "Quit", destructive: false },
  { reason: InactiveReason.Banned, label: "Banned", destructive: true },
  { reason: InactiveReason.Deleted, label: "Deleted", destructive: true },
];

/** True when the user's account is currently active (not inactivated for any reason). */
export function isActive(user: AuthUser): boolean {
  return user.inactiveReasonType === InactiveReason.Active;
}
