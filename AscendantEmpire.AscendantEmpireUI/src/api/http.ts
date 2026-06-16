import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";
import { clearAuth, getRefreshToken, getToken, updateTokens } from "./authToken";

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
};

// Mirrors the subset of IAuthTokenResult that /auth/refresh returns.
type RefreshResult = {
  token: string;
  refreshToken: string;
};

// A config we've already attempted to refresh-and-retry, so we don't loop forever.
type RetryableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

// Single-flight guard: while a refresh is in progress, concurrent 401s await this same promise
// instead of each firing their own refresh (which would invalidate one another via rotation).
let refreshPromise: Promise<string> | null = null;

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

  updateTokens(data.token, data.refreshToken);
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

    // A 401 we can't refresh away (no refresh token, or the retry also 401'd) means log out.
    if (status === 401 && getToken()) {
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
    return Promise.reject(new Error(message));
  },
);
