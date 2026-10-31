import { createContext } from "react";

export type Role = "Admin" | "Patient" | "Doctor";

// For a doctor, email holds their username.
export type AuthUser = {
  userId: string;
  name: string;
  email: string;
  role: Role;
  expiresAt: string;
};

export type AuthContextValue = {
  user: AuthUser | null;
  isAdmin: boolean;
  isPatient: boolean;
  isDoctor: boolean;
  // Returns null when the username or password is wrong.
  login: (username: string, password: string) => Promise<AuthUser | null>;
  register: (name: string, email: string, password: string) => Promise<AuthUser>;
  logout: () => void;
};

export const AuthContext = createContext<AuthContextValue | null>(null);
