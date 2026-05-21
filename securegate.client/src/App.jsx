// Main app shell: sidebar + topbar + router (real auth + API)
import { useState, Fragment } from "react";
import { Icon } from "./components/Icon";
import { Avatar } from "./components/ui";
import { useAuth } from "./auth/AuthContext";
import { getTheme, setTheme } from "./theme";

import LoginScreen from "./screens/login";
import DashboardScreen from "./screens/dashboard";
import CamerasScreen from "./screens/cameras";
import RecordingsScreen from "./screens/recordings";
import TurnstilesScreen from "./screens/turnstiles";
import FacesScreen from "./screens/faces";
import UsersScreen from "./screens/users";
import ManagementScreen from "./screens/management";
import BlockedScreen from "./screens/blocked";
import ReportsScreen from "./screens/reports";
import RolesScreen from "./screens/roles";
import SettingsScreen from "./screens/settings";

const NAV = [
  { group: "Monitoring", items: [
    { k: "dashboard", label: "Bosh sahifa", icon: "home" },
    { k: "cameras", label: "Kameralar", icon: "camera" },
    { k: "recordings", label: "Yozuvlar tarixi", icon: "film" },
    { k: "turnstiles", label: "Turniketlar", icon: "door" },
  ]},
  { group: "Boshqaruv", items: [
    { k: "faces", label: "Yuz aniqlash", icon: "face" },
    { k: "users", label: "Foydalanuvchilar", icon: "users" },
    { k: "management", label: "Rahbariyat", icon: "crown" },
    { k: "blocked", label: "Bloklangan", icon: "ban" },
  ]},
  { group: "Tahlil", items: [
    { k: "reports", label: "Hisobotlar", icon: "chart" },
  ]},
  { group: "Tizim", items: [
    { k: "roles", label: "Rollar va huquqlar", icon: "shield" },
    { k: "settings", label: "Sozlamalar", icon: "settings" },
  ]},
];

const SCREENS = {
  dashboard:  { c: DashboardScreen,  crumbs: ["Bosh sahifa"] },
  cameras:    { c: CamerasScreen,    crumbs: ["Monitoring", "Kameralar"] },
  recordings: { c: RecordingsScreen, crumbs: ["Monitoring", "Yozuvlar tarixi"] },
  turnstiles: { c: TurnstilesScreen, crumbs: ["Monitoring", "Turniketlar"] },
  faces:      { c: FacesScreen,      crumbs: ["Boshqaruv", "Yuz aniqlash"] },
  users:      { c: UsersScreen,      crumbs: ["Boshqaruv", "Foydalanuvchilar"] },
  management: { c: ManagementScreen, crumbs: ["Boshqaruv", "Rahbariyat"] },
  blocked:    { c: BlockedScreen,    crumbs: ["Boshqaruv", "Bloklangan"] },
  reports:    { c: ReportsScreen,    crumbs: ["Tahlil", "Hisobotlar"] },
  roles:      { c: RolesScreen,      crumbs: ["Tizim", "Rollar va huquqlar"] },
  settings:   { c: SettingsScreen,   crumbs: ["Tizim", "Sozlamalar"] },
};

const App = () => {
  const { user, loading, logout } = useAuth();
  const [screen, setScreen] = useState(() => {
    try { return localStorage.getItem("sg.screen") || "dashboard"; } catch { return "dashboard"; }
  });
  const [theme, setThemeState] = useState(getTheme);
  const toggleTheme = () => {
    const next = theme === "dark" ? "light" : "dark";
    setThemeState(next);
    setTheme(next);
  };

  const goTo = (k) => {
    setScreen(k);
    try { localStorage.setItem("sg.screen", k); } catch { /* ignore */ }
    setTimeout(() => {
      const el = document.querySelector(".content");
      if (el) el.scrollTop = 0;
    }, 0);
  };

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
  const primaryRole = (user.roles && user.roles[0]) || "Foydalanuvchi";

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

        <div className="nav">
          {NAV.map(g => (
            <div key={g.group} className="nav-group">
              <div className="nav-label">{g.group}</div>
              {g.items.map(it => (
                <div key={it.k}
                     className={`nav-item ${screen === it.k ? "active" : ""}`}
                     onClick={() => goTo(it.k)}>
                  <Icon name={it.icon} size={16}/>
                  <span>{it.label}</span>
                </div>
              ))}
            </div>
          ))}
        </div>

        <div className="side-foot">
          <Avatar name={user.fullName}/>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="who-name truncate">{user.fullName}</div>
            <div className="who-role">
              <Icon name="crown" size={9} style={{ verticalAlign: 0, marginRight: 2, color: "var(--warn)" }}/>
              {primaryRole}
            </div>
          </div>
          <button className="icon-btn" style={{ width: 28, height: 28 }} title="Chiqish" onClick={logout}>
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
          <div className="search">
            <Icon name="search" size={14}/>
            <input placeholder="Foydalanuvchi, kamera, yozuv qidirish..." />
            <span className="kbd">⌘K</span>
          </div>
          <button className="icon-btn" title={theme === "dark" ? "Yorug' mavzu" : "Qorong'i mavzu"} onClick={toggleTheme}>
            <Icon name={theme === "dark" ? "sun" : "moon"} size={15}/>
          </button>
          <button className="icon-btn" title="Bildirishnomalar">
            <Icon name="bell" size={15}/>
            <span className="dot"/>
          </button>
          <button className="icon-btn" title="Yordam">
            <Icon name="book" size={15}/>
          </button>
          <button className="icon-btn" title="Sozlamalar" onClick={() => goTo("settings")}>
            <Icon name="settings" size={15}/>
          </button>
        </div>
        <div className="content">
          <Screen goTo={goTo} />
        </div>
      </main>
    </div>
  );
};

export default App;
