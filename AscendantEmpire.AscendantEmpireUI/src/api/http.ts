import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";
import { clearAuth, getRefreshToken, getToken, updateTokens } from "./authToken";
import type { PermissionType } from "@/features/system/types/Permission";

// Use env first, fallback to local dev.
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7242",
  // Fail fast instead of hanging indefinitely if the API is unreachable.
  timeout: 30000,
});

// Attach the bearer token (when logged in) to every outgoing request so protected
// endpoints accept it. Read fresh each time so a login/enter-game/refresh swap takes effect
// immediately without recreating the client.
http.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

type ApiErrorBody = {
  // Single-message shape (camelCase or PascalCase)
  message?: string;
  Message?: string;
  // Array-of-errors shape returned by IApiErrorResult (PascalCase from ASP.NET)
  errors?: string[];
  Errors?: string[];
  // Optional machine-readable detail some endpoints attach to a 4xx (DTApiErrorResult.Detail). Absent on
  // the great majority of errors; its shape is per-endpoint, so callers narrow it themselves.
  detail?: unknown;
  Detail?: unknown;
};

/**
 * A failed API request, carrying the response status and any structured `Detail` the endpoint attached
 * alongside the message.
 *
 * Extends `Error` and keeps `message` as the joined server text, so the many call sites that do
 * `err instanceof Error ? err.message : fallback` are unaffected. Only callers that must *branch* on a
 * failure — rather than just display it — need to narrow to this.
 */
export class ApiError extends Error {
  /** HTTP status of the response, or 0 when the request never got one (network error, timeout). */
  readonly status: number;

  /** The endpoint-specific `Detail` payload, when one was sent. Narrow it against that endpoint's contract. */
  readonly detail: unknown;

  constructor(message: string, status: number, detail: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.detail = detail;
  }
}

// Mirrors the subset of IAuthTokenResult that /auth/refresh returns.
type RefreshResult = {
  token: string;
  refreshToken: string;
  permissions: PermissionType[];
};

// A config we've already attempted to refresh-and-retry, so we don't loop forever.
type RetryableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

// Single-flight guard: while a refresh is in progress, concurrent 401s await this same promise
// instead of each firing their own refresh (which would invalidate one another via rotation).
let refreshPromise: Promise<string> | null = null;

// Whether an unrecoverable 401 has already triggered a reload during this page load. Guards against a
// reload loop when the request that 401'd is one the app fires again as soon as it mounts.
let hasReloadedForAuth = false;

/**
 * Exchanges the stored refresh token for a new access token, persists both, and returns the new
 * access token. Uses a bare axios call so it neither sends the stale bearer nor recurses through
 * these interceptors. Throws when there's no refresh token or the server rejects it.
 */
async function refreshAccessToken(): Promise<string> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) {
    throw new Error("No refresh token");
  }

  const { data } = await axios.post<RefreshResult>(`${http.defaults.baseURL}/auth/refresh`, {
    refreshToken,
  });

  // The server re-reads permissions on every rotation, so this is where a grant or revocation made
  // while the user was logged in finally reaches their browser.
  updateTokens(data.token, data.refreshToken, data.permissions);
  return data.token;
}

// On a 401 we first try to transparently refresh the access token and retry the request once.
// Only if that fails (or there's nothing to refresh with) do we drop auth and bounce to login.
http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiErrorBody>) => {
    const original = error.config as RetryableConfig | undefined;
    const status = error.response?.status;

    const canTryRefresh =
      status === 401 &&
      original != null &&
      original._retried !== true &&
      getRefreshToken() != null;

    if (canTryRefresh && original) {
      // Mark before awaiting so a second 401 on the retried request won't refresh again.
      original._retried = true;
      try {
        // Reuse an in-flight refresh if one exists; otherwise start one.
        refreshPromise = refreshPromise ?? refreshAccessToken();
        await refreshPromise;
        refreshPromise = null;

        // Retry through the instance so the request interceptor attaches the fresh token.
        return http(original);
      } catch {
        refreshPromise = null;
        // Refresh failed — the session is gone (revoked/expired). Clear and return to login.
        clearAuth();
        window.location.reload();
        return Promise.reject(new Error("Session expired"));
      }
    }

    // A 401 we can't refresh away (no refresh token, or the retry also 401'd) means log out. This
    // deliberately doesn't require a token to already be held: a 401 arriving while logged out means
    // something fired a request outside the gate, and the user should land on the login form rather
    // than stare at an error toast with no way forward.
    //
    // The reload is one-shot per page load. Reloading unconditionally on every 401 would loop forever
    // if whatever fired that request fires again on mount.
    if (status === 401 && hasReloadedForAuth === false) {
      hasReloadedForAuth = true;
      clearAuth();
      window.location.reload();
    }

    const body = error.response?.data;

    // Prefer the errors array (IApiErrorResult shape); fall back to single message field.
    const errorsArray = body?.Errors ?? body?.errors;
    const serverMessage =
      errorsArray && errorsArray.length > 0
        ? errorsArray.join(" ")
        : (body?.message ?? body?.Message);

    const message = serverMessage ?? error.message ?? "Request failed";

    // Rejecting with an ApiError rather than a bare Error keeps the status and any structured Detail
    // available to callers that have to act on the failure — e.g. a save that clashes on name answers 409
    // either way, but the caller has to know whether to offer an overwrite or just report the refusal.
    return Promise.reject(new ApiError(message, status ?? 0, body?.Detail ?? body?.detail));
  },
);
