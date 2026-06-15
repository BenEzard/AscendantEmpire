import axios, { AxiosError } from "axios";
import { clearAuth, getToken } from "./authToken";

// Use env first, fallback to local dev.
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7242",
  // Fail fast instead of hanging indefinitely if the API is unreachable.
  timeout: 30000,
});

// Attach the bearer token (when logged in) to every outgoing request so protected
// endpoints accept it. Read fresh each time so a login/enter-game swap takes effect
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

// Normalize failed responses to a plain Error carrying the server-provided message
// (when present) so callers / react-query surface something meaningful in `error.message`.
http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiErrorBody>) => {
    // A 401 means our token is missing/expired/invalid. If we were holding one, drop it and
    // send the user back to the login screen (the gate shows it once there's no token).
    if (error.response?.status === 401 && getToken()) {
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
