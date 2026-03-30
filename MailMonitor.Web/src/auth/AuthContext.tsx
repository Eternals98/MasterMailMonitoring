import { createContext, ReactNode, useContext, useMemo, useState } from "react";

const AUTH_STORAGE_KEY = "mailmonitor.auth.session";
const DEFAULT_AUTH_USERNAME = "admin";
const DEFAULT_AUTH_PASSWORD = "mailmonitor123";

export interface AuthSession {
  username: string;
  loggedInAtIso: string;
}

interface LoginPayload {
  username: string;
  password: string;
}

interface LoginResult {
  ok: boolean;
  message?: string;
}

interface AuthContextValue {
  session: AuthSession | null;
  isAuthenticated: boolean;
  login: (payload: LoginPayload) => LoginResult;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function readStoredSession(): AuthSession | null {
  try {
    const rawValue = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!rawValue) {
      return null;
    }

    const parsed = JSON.parse(rawValue) as Partial<AuthSession>;
    if (!parsed.username || !parsed.loggedInAtIso) {
      return null;
    }

    return {
      username: parsed.username,
      loggedInAtIso: parsed.loggedInAtIso
    };
  } catch {
    return null;
  }
}

function getExpectedCredentials(): { username: string; password: string } {
  const username = import.meta.env.VITE_AUTH_USERNAME?.trim() || DEFAULT_AUTH_USERNAME;
  const password = import.meta.env.VITE_AUTH_PASSWORD?.trim() || DEFAULT_AUTH_PASSWORD;
  return { username, password };
}

export function AuthProvider({ children }: { children: ReactNode }): JSX.Element {
  const [session, setSession] = useState<AuthSession | null>(() => readStoredSession());

  function login(payload: LoginPayload): LoginResult {
    const username = payload.username.trim();
    const password = payload.password;

    if (!username || !password) {
      return {
        ok: false,
        message: "Usuario y clave son obligatorios."
      };
    }

    const expected = getExpectedCredentials();
    if (username !== expected.username || password !== expected.password) {
      return {
        ok: false,
        message: "Credenciales invalidas."
      };
    }

    const nextSession: AuthSession = {
      username,
      loggedInAtIso: new Date().toISOString()
    };

    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(nextSession));
    setSession(nextSession);

    return { ok: true };
  }

  function logout(): void {
    localStorage.removeItem(AUTH_STORAGE_KEY);
    setSession(null);
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      isAuthenticated: session !== null,
      login,
      logout
    }),
    [session]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth debe usarse dentro de AuthProvider.");
  }

  return context;
}
