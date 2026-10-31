import type { Role } from "@/features/auth/AuthContext";
import { apiFetch } from "@/shared/api/apiClient";

export type AuthResponse = {
  userId: string;
  name: string;
  email: string;
  role: Role;
  token: string;
  expiresAt: string;
};

export const loginRequest = (username: string, password: string) =>
  apiFetch<AuthResponse>("/auth/login", { method: "POST", body: JSON.stringify({ username, password }) });

export const registerRequest = (name: string, email: string, password: string) =>
  apiFetch<AuthResponse>("/auth/register", { method: "POST", body: JSON.stringify({ name, email, password }) });
