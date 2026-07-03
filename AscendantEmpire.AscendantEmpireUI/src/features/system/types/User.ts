// Client-side model for an admin-facing user in the developer System tool.
//
// Mirrors the backend GetUsers contract (AscendantEmpire.BackendContracts.Authentication.GetUsers.AuthUser)
// and the InactiveReasonType domain enum (AscendantEmpire.Domain.Enums.Authentication.InactiveReasonType).
//
// IMPORTANT: the API serializes enums as their NUMERIC value (System.Text.Json default — there is no
// JsonStringEnumConverter registered in AscendantEmpire.Api). So InactiveReasonType travels over the
// wire as a number both inbound (GET /auth/users) and outbound (POST /auth/users/inactive). We model
// it numerically here so values round-trip without translation.

/**
 * The reason an account is inactive, or `Active` while it is still usable. Values match the C#
 * `InactiveReasonType` enum exactly (PreActivation=0, Active=1, Quit=2, Banned=3, Deleted=4).
 */
export const InactiveReason = {
  PreActivation: 0,
  Active: 1,
  Quit: 2,
  Banned: 3,
  Deleted: 4,
} as const;

/** A valid inactive-reason value (one of the numeric members of `InactiveReason`). */
export type InactiveReasonType = (typeof InactiveReason)[keyof typeof InactiveReason];

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
