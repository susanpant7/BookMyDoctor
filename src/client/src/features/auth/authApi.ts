import { api } from "../../shared/api";
import type { SessionUser } from "../../shared/session";

export function login(email: string, password: string) {
  return api<SessionUser>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password })
  });
}

export function register(name: string, email: string, password: string) {
  return api<SessionUser>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ name, email, password })
  });
}
