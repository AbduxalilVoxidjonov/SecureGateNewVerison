// Autentifikatsiya konteksti — JWT, joriy foydalanuvchi, login/logout.
import { createContext, useContext, useState, useEffect, useCallback } from "react";
import { authApi } from "../api/endpoints";
import { getToken, setToken, setUnauthorizedHandler } from "../api/client";

const AuthContext = createContext(null);

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => useContext(AuthContext);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  // 401 kelganda — sessiyani tozalaymiz
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setToken(null);
      setUser(null);
    });
  }, []);

  // Boshlanishda — token bo'lsa, foydalanuvchini tiklaymiz
  useEffect(() => {
    let active = true;
    (async () => {
      if (getToken()) {
        try {
          const me = await authApi.me();
          if (active) setUser(me);
        } catch {
          setToken(null);
        }
      }
      if (active) setLoading(false);
    })();
    return () => { active = false; };
  }, []);

  const login = useCallback(async (email, password, rememberMe) => {
    const res = await authApi.login(email, password, rememberMe);
    setToken(res.accessToken);
    setUser(res.user);
    return res;
  }, []);

  const logout = useCallback(async () => {
    try { await authApi.logout(); } catch { /* ignore */ }
    setToken(null);
    setUser(null);
  }, []);

  const hasPermission = useCallback(
    (perm) => !!user && (user.isSuperAdmin || (user.permissions || []).includes(perm)),
    [user]
  );

  return (
    <AuthContext.Provider value={{ user, loading, login, logout, hasPermission }}>
      {children}
    </AuthContext.Provider>
  );
}
