import { useState, type ReactNode } from "react";
import { AuthContext, type AuthUser } from "@/features/auth/AuthContext";
import { loginRequest, registerRequest, type AuthResponse } from "@/features/auth/authApi";
import { ApiError } from "@/shared/api/apiClient";
import { clearToken, getToken, setToken } from "@/shared/api/token";

const USER_KEY = "bookmydoctor.user";

// Drops a saved session whose token is missing or has expired.
function loadUser(): AuthUser | null {
  try {
    const stored = localStorage.getItem(USER_KEY);
    const user = stored && getToken() ? (JSON.parse(stored) as AuthUser) : null;
    if (user && new Date(user.expiresAt) > new Date()) return user;
  } catch {
    // Unreadable data is treated as signed out.
  }
  clearToken();
  localStorage.removeItem(USER_KEY);
  return null;
}

export default function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(loadUser);

  function start({ token, ...authUser }: AuthResponse) {
    setToken(token);
    localStorage.setItem(USER_KEY, JSON.stringify(authUser));
    setUser(authUser);
    return authUser;
  }

  async function login(username: string, password: string) {
    try {
      return start(await loginRequest(username, password));
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return null;
      throw error;
    }
  }

  async function register(name: string, email: string, password: string) {
    return start(await registerRequest(name, email, password));
  }

  function logout() {
    clearToken();
    localStorage.removeItem(USER_KEY);
    setUser(null);
  }

  return (
    <AuthContext.Provider
      value={{ user, isAdmin: user?.role === "Admin", isPatient: user?.role === "Patient", login, register, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}
