// Login ekrani — JWT autentifikatsiya
import { useState } from "react";
import { Icon } from "../components/Icon";
import { useAuth } from "../auth/AuthContext";
import { takeLoginNotice } from "./utils";

const LoginScreen = () => {
  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(true);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  // Sessiya tashqaridan yopilgan bo'lsa (masalan parol o'zgartirilgach) — bir martalik xabar.
  const [notice, setNotice] = useState(takeLoginNotice);

  const submit = async (e) => {
    e.preventDefault();
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      await login(email, password, rememberMe);
    } catch (err) {
      setError(err?.message || "Kirib bo'lmadi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{
      height: "100vh", display: "grid", placeItems: "center",
      background: "radial-gradient(80% 80% at 50% 0%, oklch(0.22 0.03 200), var(--bg-0))",
    }}>
      <form onSubmit={submit} className="card padded" style={{ width: 380, padding: 28 }}>
        <div className="row" style={{ gap: 12, marginBottom: 22 }}>
          <div className="brand-mark" style={{ width: 40, height: 40 }}>
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 2L3 6v6c0 5 3.5 9 9 10 5.5-1 9-5 9-10V6z" />
              <circle cx="12" cy="11" r="3" />
            </svg>
          </div>
          <div>
            <div style={{ fontWeight: 600, fontSize: 18, letterSpacing: "-0.01em" }}>SecureGate</div>
            <div className="faint" style={{ fontSize: 12 }}>Kirish boshqaruv tizimi</div>
          </div>
        </div>

        <div className="col" style={{ gap: 14 }}>
          <div className="field">
            <input className="input" type="email" value={email} autoComplete="username"
              onChange={(e) => setEmail(e.target.value)} placeholder="Email" required />
          </div>
          <div className="field">
            <input className="input" type="password" value={password} autoComplete="current-password"
              onChange={(e) => setPassword(e.target.value)} placeholder="Parol" required />
          </div>

          <label className="check">
            <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} />
            <span>Meni eslab qol</span>
          </label>

          {notice && !error && (
            <div className="row" style={{ gap: 8, padding: "9px 12px", borderRadius: 8, fontSize: 13,
              background: "var(--bg-1)", color: "var(--text-1)", border: "1px solid var(--border-strong)" }}>
              <Icon name="check" size={15} /> {notice}
            </div>
          )}

          {error && (
            <div className="row" style={{ gap: 8, padding: "9px 12px", borderRadius: 8, fontSize: 13,
              background: "oklch(0.28 0.10 25)", color: "oklch(0.92 0.06 25)", border: "1px solid oklch(0.40 0.12 25)" }}>
              <Icon name="alert" size={15} /> {error}
            </div>
          )}

          <button className="btn primary" type="submit" disabled={busy} style={{ height: 40, justifyContent: "center", marginTop: 4 }}>
            {busy ? "Kirilmoqda..." : <><Icon name="lock" size={15} /> Kirish</>}
          </button>
        </div>

        <div className="faint" style={{ fontSize: 11, marginTop: 18, textAlign: "center" }}>
          SecureGate v2.4 · Production
        </div>
      </form>
    </div>
  );
};

export default LoginScreen;
