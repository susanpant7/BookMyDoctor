import { getToken } from "@/shared/api/token";

export class ApiError extends Error {
  status: number
  detail?: string

  constructor(status: number, detail?: string) {
    super(detail ?? `Request failed with status ${status}`);
    this.status = status;
    this.detail = detail;
  }
}

// Empty by default: the API is then on the same address as the page (the Vite dev server forwards /api and /hubs).
// When the client is hosted separately, VITE_API_URL is the address of the API, without a trailing slash.
export const API_URL = ((import.meta.env.VITE_API_URL as string | undefined) ?? "").replace(/\/$/, "");

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  // A FormData body sets its own Content-Type (with the multipart boundary).
  if (init.body && !(init.body instanceof FormData)) headers.set("Content-Type", "application/json");

  const token = getToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${API_URL}/api${path}`, { ...init, headers });
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { message?: string } | null;
    throw new ApiError(response.status, problem?.message);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
