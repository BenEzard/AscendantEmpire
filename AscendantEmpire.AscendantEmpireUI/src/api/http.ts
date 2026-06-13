import axios, { AxiosError } from "axios";

// Use env first, fallback to local dev.
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7242",
  // Fail fast instead of hanging indefinitely if the API is unreachable.
  timeout: 30000,
});

// Normalize failed responses to a plain Error carrying the server-provided message
// (when present) so callers / react-query surface something meaningful in `error.message`.
http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<{ message?: string }>) => {
    const serverMessage = error.response?.data?.message;
    const message = serverMessage ?? error.message ?? "Request failed";
    return Promise.reject(new Error(message));
  },
);
