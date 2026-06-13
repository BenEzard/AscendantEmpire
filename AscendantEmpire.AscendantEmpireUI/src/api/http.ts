import axios, { AxiosError } from "axios";

// Use env first, fallback to local dev.
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7242",
  // Fail fast instead of hanging indefinitely if the API is unreachable.
  timeout: 30000,
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
