import { ApiError } from "@/shared/api/apiClient";

const unreachable = "Cannot reach the server. Make sure the API is running, then try again.";

/** Turns a failed request into a message that is safe and useful to show to the user. */
export function errorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    if (error.detail) return error.detail;
    if (error.status === 401 || error.status === 403) return "You don't have permission to do this. Try signing in again.";
    if (error.status === 502 || error.status === 503 || error.status === 504) return unreachable;
    return fallback;
  }

  // fetch rejects with a TypeError when the server cannot be reached at all.
  return error instanceof TypeError ? unreachable : fallback;
}
