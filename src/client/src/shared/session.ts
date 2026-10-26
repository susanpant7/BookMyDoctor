import { createContext, createElement, useContext, useState, type ReactNode } from "react";

const SESSION_KEY = "bookmydoctor.session";
const LEGACY_SESSION_KEY = "bookmydoctor.user";

export type SessionUser = {
  userId: string;
  name: string;
  email: string;
  token: string;
  expiresAt: string;
};

type SessionContextValue = {
  session: SessionUser | null;
  setSession: (user: SessionUser | null) => void;
};

const SessionContext = createContext<SessionContextValue | null>(null);

function readSession(): SessionUser | null {
  localStorage.removeItem(LEGACY_SESSION_KEY);
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) {
    return null;
  }

  try {
    const session = JSON.parse(raw) as SessionUser;
    // Drop sessions saved before tokens existed, or whose token has expired.
    if (!session.token || new Date(session.expiresAt) <= new Date()) {
      localStorage.removeItem(SESSION_KEY);
      return null;
    }
    return session;
  } catch {
    localStorage.removeItem(SESSION_KEY);
    return null;
  }
}

export function getAccessToken(): string | null {
  return readSession()?.token ?? null;
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSessionState] = useState<SessionUser | null>(readSession);

  function setSession(user: SessionUser | null) {
    if (user) {
      localStorage.setItem(SESSION_KEY, JSON.stringify(user));
    } else {
      localStorage.removeItem(SESSION_KEY);
      localStorage.removeItem(LEGACY_SESSION_KEY);
    }
    setSessionState(user);
  }

  return createElement(SessionContext.Provider, { value: { session, setSession } }, children);
}

export function useSession() {
  const context = useContext(SessionContext);
  if (!context) {
    throw new Error("useSession must be used within SessionProvider");
  }
  return context;
}
