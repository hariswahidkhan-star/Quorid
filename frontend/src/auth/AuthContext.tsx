import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { apiFetch, getToken, setToken, type AuthResponse, type UserSummary } from '../api/client';

const USER_KEY = 'quorid.user';
const REFRESH_KEY = 'quorid.refreshToken';

export interface RegisterInput {
  tenantName: string;
  entityName: string;
  fullName: string;
  email: string;
  password: string;
}

interface AuthState {
  user: UserSummary | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

function loadUser(): UserSummary | null {
  const raw = localStorage.getItem(USER_KEY);
  return raw ? (JSON.parse(raw) as UserSummary) : null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSummary | null>(() => (getToken() ? loadUser() : null));

  const value = useMemo<AuthState>(() => {
    function persist(res: AuthResponse) {
      setToken(res.accessToken);
      localStorage.setItem(REFRESH_KEY, res.refreshToken);
      localStorage.setItem(USER_KEY, JSON.stringify(res.user));
      setUser(res.user);
    }

    return {
      user,
      isAuthenticated: !!user,
      async login(email: string, password: string) {
        const res = await apiFetch<AuthResponse>('/api/auth/login', {
          method: 'POST',
          body: JSON.stringify({ email, password }),
        });
        persist(res);
      },
      async register(input: RegisterInput) {
        const res = await apiFetch<AuthResponse>('/api/auth/register', {
          method: 'POST',
          body: JSON.stringify(input),
        });
        persist(res);
      },
      logout() {
        setToken(null);
        localStorage.removeItem(REFRESH_KEY);
        localStorage.removeItem(USER_KEY);
        setUser(null);
      },
    };
  }, [user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
}
