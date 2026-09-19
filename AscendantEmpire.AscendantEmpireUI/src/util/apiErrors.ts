import axios from "axios";

/**
 * The messages to show for a failed request: the server's `errors` array when it sent one (every non-success response from the API does), otherwise
 * whatever describes the failure best.
 */
export function apiErrorMessages(error: unknown): string[] {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (Array.isArray(data?.errors)) return data.errors as string[];
    if (typeof data === "string" && data !== "") return [data];
    if (data?.message) return [data.message as string];
    if (error.message) return [error.message];
  } else if (error instanceof Error) {
    return [error.message];
  }

  return ["An unknown error occurred."];
}
