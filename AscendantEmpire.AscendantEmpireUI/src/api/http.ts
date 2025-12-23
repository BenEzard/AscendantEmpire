import axios from "axios";

// Use env first, fallback to local dev.
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7242",
});
