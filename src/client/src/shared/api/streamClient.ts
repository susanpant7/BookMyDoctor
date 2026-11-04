import { API_URL, ApiError } from "@/shared/api/apiClient";
import { getToken } from "@/shared/api/token";

// Calls an endpoint that answers while it works, one JSON object per line (for example progress lines, and a last line
// with the result or an error). Every line is handed to onEvent as it arrives. A request that is refused outright (not
// logged in, a wrong request) throws an ApiError like apiFetch does.
export async function streamEvents<Event>(path: string, init: RequestInit, onEvent: (event: Event) => void): Promise<void> {
  const headers = new Headers(init.headers);
  if (init.body) headers.set("Content-Type", "application/json");
  const token = getToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${API_URL}/api${path}`, { ...init, headers });
  if (!response.ok || !response.body) {
    const problem = (await response.json().catch(() => null)) as { message?: string } | null;
    throw new ApiError(response.status, problem?.message);
  }

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
  let buffered = "";
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffered += value;
    const lines = buffered.split("\n");
    buffered = lines.pop() ?? "";
    for (const line of lines.filter(Boolean)) onEvent(JSON.parse(line) as Event);
  }
  if (buffered.trim()) onEvent(JSON.parse(buffered) as Event);
}
