// API service for the dev System tool's user administration. Thin wrappers over the shared axios
// `http` client, one per backend endpoint on AuthController (GET /auth/users, POST /auth/users/inactive).

import { http } from "@/api/http";

import type { PermissionType } from "../types/Permission";
import type { AuthUser, InactiveReasonType } from "../types/User";

/**
 * Get every user. The backend returns a bare collection (IReadOnlyCollection<AuthUser>), not an
 * envelope, so we return the response body directly.
 */
export async function getUsers(): Promise<AuthUser[]> {
  const { data } = await http.get<AuthUser[]>("/auth/users");
  return data;
}

/**
 * Set a user's inactive reason. Sending `Active` reactivates the account (the backend clears the
 * inactive timestamp in that case). Responds 204 No Content on success.
 */
export async function setUserInactive(userId: number, reason: InactiveReasonType): Promise<void> {
  await http.post("/auth/users/inactive", { userId, inactiveReasonType: reason });
}

/**
 * Replace a user's permissions. The list is the complete desired state, so anything omitted is revoked
 * (an empty list strips them all). Responds 204 No Content on success.
 *
 * The change reaches the affected user on their next token refresh rather than immediately — their
 * current access token keeps the permissions it was minted with until it expires.
 */
export async function setUserPermissions(userId: number, permissions: PermissionType[]): Promise<void> {
  await http.put("/auth/users/permissions", { userId, permissions });
}
