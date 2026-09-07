// Main app shell: sidebar + topbar + router (real auth + API)
import { useState, useLayoutEffect, useEffect, useRef, Fragment } from "react";
import { Icon } from "./components/Icon";
import { Avatar } from "./components/ui";
import ErrorBoundary from "./components/ErrorBoundary";
import { useAuth } from "./auth/AuthContext";
import { getTheme, setTheme } from "./theme";
import { closeAllHubs } from "./api/hub";
import { useHubStatus } from "./hooks/useHub";

import LoginScreen from "./screens/login";
import DashboardScreen from "./screens/dashboard";
import CamerasScreen from "./screens/cameras";
import RecordingsScreen from "./screens/recordings";
import TurnstilesScreen from "./screens/turnstiles";
import FacesScreen from "./screens/faces";
import UsersScreen from "./screens/users";
import ManagementScreen from "./screens/management";
import StaffScreen from "./screens/staff";
import BlockedScreen from "./screens/blocked";
import ReportsScreen from "./screens/reports";
import RolesScreen from "./screens/roles";
import SettingsScreen from "./screens/settings";

// `perm` — SecureGate.Domain/Auth/Permission.cs dagi enum nomlari.
// perm bo'lmasa (dashboard) — barcha kirgan foydalanuvchilarga ko'rinadi.
const NAV = [
  { group: "Monitoring", items: [
    { k: "dashboard", label: "Bosh sahifa", icon: "home" },
    { k: "cameras", label: "Kameralar", icon: "camera", perm: "CameraView" },
    { k: "recordings", label: "Yozuvlar tarixi", icon: "film", perm: "RecordingsView" },
    { k: "turnstiles", label: "Turniketlar", icon: "door", perm: "TurnstileView" },
  ]},
  { group: "Boshqaruv", items: [
    { k: "faces", label: "Yuz aniqlash", icon: "face", perm: "CameraUserView" },
    { k: "users", label: "Foydalanuvchilar", icon: "users", perm: "UsersView" },
    { k: "staff", label: "Xodimlar", icon: "user", perm: "StaffView" },
    { k: "management", label: "Rahbariyat", icon: "crown", perm: "AdminsManage" },
    { k: "blocked", label: "Bloklangan", icon: "ban", perm: "BlockedManage" },
  ]},
  { group: "Tahlil", items: [
    { k: "reports", label: "Hisobotlar", icon: "chart", perm: "ReportsView" },
  ]},
  { group: "Tizim", items: [
    { k: "roles", label: "Rollar va huquqlar", icon: "shield", perm: "AdminsManage" },
    { k: "settings", label: "Sozlamalar", icon: "settings", perm: "SettingsManage" },
  ]},
];

const SCREENS = {
  dashboard:  { c: DashboardScreen,  crumbs: ["Bosh sahifa"] },
  cameras:    { c: CamerasScreen,    crumbs: ["Monitoring", "Kameralar"], perm: "CameraView" },
  recordings: { c: RecordingsScreen, crumbs: ["Monitoring", "Yozuvlar tarixi"], perm: "RecordingsView" },
  turnstiles: { c: TurnstilesScreen, crumbs: ["Monitoring", "Turniketlar"], perm: "TurnstileView" },
  faces:      { c: FacesScreen,      crumbs: ["Boshqaruv", "Yuz aniqlash"], perm: "CameraUserView" },
  users:      { c: UsersScreen,      crumbs: ["Boshqaruv", "Foydalanuvchilar"], perm: "UsersView" },
  staff:      { c: StaffScreen,      crumbs: ["Boshqaruv", "Xodimlar"], perm: "StaffView" },
  management: { c: ManagementScreen, crumbs: ["Boshqaruv", "Rahbariyat"], perm: "AdminsManage" },
  blocked:    { c: BlockedScreen,    crumbs: ["Boshqaruv", "Bloklangan"], perm: "BlockedManage" },
  reports:    { c: ReportsScreen,    crumbs: ["Tahlil", "Hisobotlar"], perm: "ReportsView" },
  roles:      { c: RolesScreen,      crumbs: ["Tizim", "Rollar va huquqlar"], perm: "AdminsManage" },
  settings:   { c: SettingsScreen,   crumbs: ["Tizim", "Sozlamalar"], perm: "SettingsManage" },
};

// Realtime ulanish indikatori. Faqat tizimga kirgandan keyin render qilinadi —
// shuning uchun hook ishga tushganda token allaqachon mavjud bo'ladi.
// "alert" hubi butun ilova bo'ylab kerak, shuning uchun umumiy ko'rsatkich sifatida shu olindi.
const CONN_UI = {
  connected:    { cls: "on",   title: "Realtime: ulangan" },
  connecting:   { cls: "warn", title: "Realtime: ulanmoqda..." },
  reconnecting: { cls: "warn", title: "Realtime: qayta ulanmoqda..." },
  disconnected: { cls: "err",  title: "Realtime: ulanish yo'q" },
};

const ConnectionDot = () => {
  const status = useHubStatus("alert");
  const ui = CONN_UI[status] || CONN_UI.disconnected;
  return (
    <span
      role="status"
      aria-label={ui.title}
      title={ui.title}
      className={`dot-s ${ui.cls}`}
      style={{ marginRight: 6, flex: "0 0 auto" }}
    />
  );
};

const NoAccess = ({ label }) => (
  <div className="col" style={{ alignItems: "center", gap: 10, padding: 56, color: "var(--text-2)", textAlign: "center" }}>
    <Icon name="ban" size={28} />
    <div style={{ fontSize: 14, fontWeight: 500, color: "var(--text-0)" }}>Ruxsat yo'q</div>
    <div style={{ fontSize: 13, maxWidth: 420 }}>
      Sizda &laquo;{label}&raquo; bo'limini ko'rish uchun ruxsat yo'q.
      Kerak bo'lsa tizim administratoriga murojaat qiling.
    </div>
  </div>
);

const App = () => {
  const { user, loading, logout, hasPermission } = useAuth();
  const [screen, setScreen] = useState(() => {
    try { return localStorage.getItem("sg.screen") || "dashboard"; } catch { return "dashboard"; }
  });
  const [theme, setThemeState] = useState(getTheme);
  const contentRef = useRef(null);

  const toggleTheme = () => {
    const next = theme === "dark" ? "light" : "dark";
    setThemeState(next);
    setTheme(next);
  };

  const goTo = (k) => {
    setScreen(k);
    try { localStorage.setItem("sg.screen", k); } catch { /* ignore */ }
  };

  // Sessiya tugaganda (logout yoki 401 -> AuthProvider user'ni tozalaydi) barcha
  // SignalR ulanishlarini yopamiz. Qayta kirilganda ekranlar mount bo'lib,
  // ulanishlar yangi token bilan o'zi qaytadan ochiladi.
  useEffect(() => {
    if (!user) closeAllHubs();
  }, [user]);

  // Ekran almashganda kontentni tepaga qaytaramiz (setTimeout kerak emas).
  useLayoutEffect(() => {
    if (contentRef.current) contentRef.current.scrollTop = 0;
  }, [screen]);

  if (loading) {
    return (
      <div style={{ height: "100vh", display: "grid", placeItems: "center", color: "var(--text-2)" }}>
        <div className="row" style={{ gap: 10 }}>
          <span className="pulse" style={{ width: 10, height: 10, borderRadius: "50%", background: "var(--accent)" }} />
          Yuklanmoqda...
        </div>
      </div>
    );
  }

  if (!user) return <LoginScreen />;

  const cur = SCREENS[screen] || SCREENS.dashboard;
  const Screen = cur.c;
  const allowed = hasPermission(cur.perm);
  const primaryRole = (user.roles && user.roles[0]) || "Foydalanuvchi";

  // Ruxsatga qarab menyuni filtrlaymiz; bo'sh guruhlar ko'rsatilmaydi.
  const nav = NAV
    .map(g => ({ ...g, items: g.items.filter(it => hasPermission(it.perm)) }))
    .filter(g => g.items.length > 0);

  return (
    <div className="app">
      {/* Sidebar */}
      <aside className="side">
        <div className="side-header">
          <div className="brand-mark">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 2L3 6v6c0 5 3.5 9 9 10 5.5-1 9-5 9-10V6z"/>
              <circle cx="12" cy="11" r="3"/>
            </svg>
          </div>
          <div style={{ flex: 1 }}>
            <div className="brand-name">SecureGate</div>
            <div className="brand-sub mono">v2.4 · Production</div>
          </div>
          <div className="live-pill" style={{ height: 22, padding: "0 7px", fontSize: 10 }}>
            <span className="pulse"/>
          </div>
        </div>

        <nav className="nav" aria-label="Asosiy menyu">
          {nav.map(g => (
            <div key={g.group} className="nav-group">
              <div className="nav-label">{g.group}</div>
              {g.items.map(it => (
                <button key={it.k}
                        type="button"
                        aria-current={screen === it.k ? "page" : undefined}
                        className={`nav-item ${screen === it.k ? "active" : ""}`}
                        onClick={() => goTo(it.k)}>
                  <Icon name={it.icon} size={16}/>
                  <span>{it.label}</span>
                </button>
              ))}
            </div>
          ))}
        </nav>

        <div className="side-foot">
          <Avatar name={user.fullName}/>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="who-name truncate">{user.fullName}</div>
            <div className="who-role">
              <Icon name="crown" size={9} style={{ verticalAlign: 0, marginRight: 2, color: "var(--warn)" }}/>
              {primaryRole}
            </div>
          </div>
          <button type="button" className="icon-btn" style={{ width: 28, height: 28 }} title="Chiqish" onClick={logout}>
            <Icon name="boltOff" size={13}/>
          </button>
        </div>
      </aside>

      {/* Main */}
      <main className="main">
        <div className="topbar">
          <div className="crumbs">
            {cur.crumbs.map((c, i, arr) => (
              <Fragment key={i}>
                <span className={i === arr.length - 1 ? "here" : ""}>{c}</span>
                {i < arr.length - 1 && <Icon name="chevron" size={11} className="sep"/>}
              </Fragment>
            ))}
          </div>
          <div style={{ flex: 1 }} />
          <ConnectionDot />
          <button type="button" className="icon-btn"
                  title={theme === "dark" ? "Yorug' mavzu" : "Qorong'i mavzu"} onClick={toggleTheme}>
            <Icon name={theme === "dark" ? "sun" : "moon"} size={15}/>
          </button>
          {hasPermission("SettingsManage") && (
            <button type="button" className="icon-btn" title="Sozlamalar" onClick={() => goTo("settings")}>
              <Icon name="settings" size={15}/>
            </button>
          )}
        </div>
        <div className="content" ref={contentRef}>
          {/* Ekran darajasidagi chegara — bitta ekran yiqilsa, sidebar ishlab turadi.
              key={screen} — ekran almashganda chegara tiklanadi. */}
          <ErrorBoundary key={screen}>
            {allowed
              ? <Screen goTo={goTo} />
              : <NoAccess label={cur.crumbs[cur.crumbs.length - 1]} />}
          </ErrorBoundary>
        </div>
      </main>
    </div>
  );
};

export default App;
