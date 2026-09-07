// AuthProvider — JWT sessiyasi, joriy foydalanuvchi, login/logout, proaktiv refresh.
import { useState, useEffect, useCallback } from "react";
import { AuthContext } from "./AuthContext";
import { authApi } from "../api/endpoints";
import {
  hasSession,
  setSession,
  setUnauthorizedHandler,
  refreshSession,
  getExpiresAt,
  getRefreshToken,
} from "../api/client";

// Token tugashiga shuncha qolganda proaktiv yangilaymiz (client.js bilan bir xil).
const REFRESH_SKEW_MS = 60_000;
// setTimeout 32-bit chegarasi (~24.8 kun) — undan katta kechikish darhol ishga tushib ketadi.
const MAX_TIMEOUT_MS = 2_000_000_000;

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  // 401 kelganda (refresh ham yordam bermasa) — sessiyani tozalaymiz
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setSession(null);
      setUser(null);
    });
    return () => setUnauthorizedHandler(null);
  }, []);

  // Boshlanishda — saqlangan sessiya bo'lsa, foydalanuvchini tiklaymiz
  useEffect(() => {
    const ctrl = new AbortController();
    let active = true;
    (async () => {
      if (hasSession()) {
        try {
          const me = await authApi.me(ctrl.signal);
          if (active) setUser(me);
        } catch {
          setSession(null);
        }
      }
      if (active) setLoading(false);
    })();
    return () => { active = false; ctrl.abort(); };
  }, []);

  // Proaktiv refresh — access token tugashiga ~60s qolganda.
  useEffect(() => {
    if (!user) return undefined;
    let cancelled = false;
    let timer = null;

    const schedule = () => {
      if (cancelled) return;
      const expiresAt = getExpiresAt();
      if (!expiresAt || !getRefreshToken()) return;
      const delay = Math.min(
        Math.max(expiresAt - Date.now() - REFRESH_SKEW_MS, 0),
        MAX_TIMEOUT_MS
      );
      timer = setTimeout(async () => {
        if (cancelled) return;
        const ok = await refreshSession(); // single-flight — client.js ichida
        if (cancelled) return;
        if (ok) schedule();
        else { setSession(null); setUser(null); } // sessiya tugadi -> login ekrani
      }, delay);
    };

    schedule();
    return () => { cancelled = true; if (timer) clearTimeout(timer); };
  }, [user]);

  const login = useCallback(async (email, password, rememberMe) => {
    const res = await authApi.login(email, password, rememberMe);
    setSession({
      accessToken: res.accessToken,
      refreshToken: res.refreshToken,
      expiresAt: res.expiresAt,
    });
    setUser(res.user);
    return res;
  }, []);

  const logout = useCallback(async () => {
    try { await authApi.logout(); } catch { /* ignore */ }
    setSession(null);
    setUser(null);
  }, []);

  const hasPermission = useCallback(
    (perm) => {
      if (!user) return false;
      if (user.isSuperAdmin) return true;      // SuperAdmin hamma narsani ko'radi
      if (!perm) return true;                  // ruxsat talab qilinmaydi
      return (user.permissions || []).includes(perm);
    },
    [user]
  );

  return (
    <AuthContext.Provider value={{ user, loading, login, logout, hasPermission }}>
      {children}
    </AuthContext.Provider>
  );
}
