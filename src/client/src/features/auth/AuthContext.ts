import { createContext } from "react";

export type AuthUser = {
  userId: string;
  name: string;
  email: string;
  role: "Admin" | "Patient";
  expiresAt: string;
};

export type AuthContextValue = {
  user: AuthUser | null;
  isAdmin: boolean;
  isPatient: boolean;
  // Returns null when the username or password is wrong.
  login: (username: string, password: string) => Promise<AuthUser | null>;
  register: (name: string, email: string, password: string) => Promise<AuthUser>;
  logout: () => void;
};

export const AuthContext = createContext<AuthContextValue | null>(null);
