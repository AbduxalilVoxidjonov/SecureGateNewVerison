// Dashboard / Bosh sahifa — real API
import { Icon } from "../components/Icon";
import { Loading, ErrorBox } from "../components/state";
import { useApi } from "../hooks/useApi";
import { dashboardApi } from "../api/endpoints";

const feedIconFor = (type) => {
  switch (type) {
    case "good": return { name: "check", cls: "ok" };
    case "deny": return { name: "lock", cls: "err" };
    case "warn": return { name: "alert", cls: "warn" };
    case "info": return { name: "film", cls: "info" };
    default: return { name: "clock", cls: "" };
  }
};

const DashboardScreen = ({ goTo }) => {
  const { data, loading, error, reload } = useApi(() => dashboardApi.get(), []);

  if (loading) return <Loading />;
  if (error) return <ErrorBox error={error} onRetry={reload} />;

  const d = data || {};
  const hourly = d.hourlyPassData || [];
  const activity = d.recentActivity || [];
  const turnstiles = d.popularTurnstiles || [];
  const alerts = d.recentAlerts || [];
  const maxHourly = Math.max(1, ...hourly);

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Bosh sahifa</h1>
          <div className="page-sub">Real-time monitoring · Bugun, {new Date().toLocaleDateString("uz-UZ", { day: "numeric", month: "long", year: "numeric" })}</div>
        </div>
        <div className="row">
          <span className="live-pill"><span className="pulse"></span>Live</span>
          <button className="btn" onClick={reload}><Icon name="refresh" size={14} /> Yangilash</button>
        </div>
      </div>

      {/* Stat cards */}
      <div className="stat-grid">
        <div className="stat">
          <div className="label"><Icon name="users" size={14} /> Faol foydalanuvchilar</div>
          <div className="v tnum">{d.activeStudentCount ?? 0}</div>
          <div className="sub">Tizimda ro'yxatdan o'tgan</div>
        </div>
        <div className="stat">
          <div className="label"><Icon name="door" size={14} /> Bugungi o'tishlar</div>
          <div className="v tnum">{d.todayPassCount ?? 0}</div>
          <div className="sub">Kirish-chiqish amaliyotlari</div>
        </div>
        <div className="stat">
          <div className="label"><Icon name="camera" size={14} /> Faol kameralar</div>
          <div className="v tnum">{d.activeCameraCount ?? 0}<span style={{ fontSize: 16, color: "var(--text-3)", fontWeight: 400 }}> / {d.totalCameraCount ?? 0}</span></div>
          <div className="sub">{(d.totalCameraCount ?? 0) - (d.activeCameraCount ?? 0)} oflayn</div>
        </div>
        <div className="stat">
          <div className="label"><Icon name="alert" size={14} /> Ogohlantirishlar</div>
          <div className="v tnum" style={{ color: d.newAlertCount ? "var(--warn)" : undefined }}>{d.alertCount ?? 0}</div>
          <div className="sub">{d.newAlertCount ?? 0} ta yangi</div>
        </div>
      </div>

      <div className="two-col" style={{ marginTop: 16 }}>
        {/* Left: activity + chart */}
        <div>
          <div className="card">
            <div className="card-h">
              <h3><Icon name="flame" size={14} style={{ verticalAlign: -2, marginRight: 6, color: "var(--warn)" }} /> Real-time faoliyat</h3>
              <span className="muted" style={{ fontSize: 12 }}>So'ngi amaliyotlar</span>
            </div>
            <div style={{ maxHeight: 420, overflow: "auto" }}>
              {activity.length === 0 && <div className="faint" style={{ padding: 20, textAlign: "center" }}>Faoliyat yo'q</div>}
              {activity.map((a, i) => {
                const ic = feedIconFor(a.type);
                return (
                  <div key={i} className="feed-item">
                    <div className="t">{a.time}</div>
                    <div className={`feed-icon ${ic.cls}`}><Icon name={ic.name} size={13} /></div>
                    <div>
                      <div className="feed-who">{a.userName}</div>
                      <div className="feed-where">{a.action}</div>
                    </div>
                    <span />
                  </div>
                );
              })}
            </div>
          </div>

          <div className="card" style={{ marginTop: 16, padding: 18 }}>
            <div className="row" style={{ justifyContent: "space-between", marginBottom: 14 }}>
              <div>
                <div style={{ fontSize: 14, fontWeight: 600 }}>Bugungi trafik · 24 soat</div>
                <div className="muted" style={{ fontSize: 12 }}>Soatlik o'tishlar hajmi</div>
              </div>
            </div>
            <div className="bar-chart">
              {hourly.map((v, i) => (
                <div key={i} className={`bar ${i > new Date().getHours() ? "muted" : ""}`}
                  style={{ height: `${Math.max(4, (v / maxHourly) * 100)}%` }} title={`${i}:00 · ${v}`} />
              ))}
            </div>
            <div className="row mono" style={{ justifyContent: "space-between", marginTop: 6, fontSize: 10.5, color: "var(--text-3)" }}>
              <span>00:00</span><span>06:00</span><span>12:00</span><span>18:00</span><span>24:00</span>
            </div>
          </div>
        </div>

        {/* Right: turnstiles + alerts + quick actions */}
        <div>
          <div className="card padded">
            <div className="row" style={{ justifyContent: "space-between", marginBottom: 14 }}>
              <div style={{ fontSize: 14, fontWeight: 600 }}>Mashhur turniketlar</div>
              <button className="btn xs ghost" onClick={() => goTo("turnstiles")}>Boshqarish <Icon name="chevron" size={11} /></button>
            </div>
            <div className="col" style={{ gap: 12 }}>
              {turnstiles.length === 0 && <div className="faint" style={{ fontSize: 12 }}>Ma'lumot yo'q</div>}
              {turnstiles.map((t, i) => (
                <div key={i}>
                  <div className="row" style={{ justifyContent: "space-between", marginBottom: 6 }}>
                    <span style={{ fontSize: 13 }}>{t.name}</span>
                    <span className="mono tnum" style={{ fontSize: 12, color: "var(--text-2)" }}>{t.count}</span>
                  </div>
                  <div className="bar-track"><div className="bar-fill" style={{ width: `${t.percentage}%` }} /></div>
                </div>
              ))}
            </div>
          </div>

          <div className="card" style={{ marginTop: 16 }}>
            <div className="card-h"><h3>So'ngi ogohlantirishlar</h3></div>
            {alerts.length === 0 && <div className="faint" style={{ padding: 16, fontSize: 12 }}>Ogohlantirish yo'q</div>}
            {alerts.map((al) => (
              <div key={al.id} className="row" style={{ gap: 10, padding: "10px 16px", borderBottom: "1px solid var(--border)" }}>
                <div className={`feed-icon ${al.type === "Danger" ? "err" : al.type === "Warning" ? "warn" : al.type === "Success" ? "ok" : "info"}`} style={{ width: 26, height: 26 }}>
                  <Icon name="alert" size={12} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div className="truncate" style={{ fontSize: 13, fontWeight: 500 }}>{al.title}</div>
                  <div className="faint truncate" style={{ fontSize: 11.5 }}>{al.message}</div>
                </div>
              </div>
            ))}
          </div>

          <div className="card padded" style={{ marginTop: 16 }}>
            <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 12 }}>Tezkor amallar</div>
            <div className="grid-2" style={{ gap: 8 }}>
              <button className="btn" onClick={() => goTo("cameras")} style={{ justifyContent: "flex-start" }}><Icon name="plus" size={14} /> Kamera</button>
              <button className="btn" onClick={() => goTo("users")} style={{ justifyContent: "flex-start" }}><Icon name="plus" size={14} /> Foydalanuvchi</button>
              <button className="btn" onClick={() => goTo("turnstiles")} style={{ justifyContent: "flex-start" }}><Icon name="unlock" size={14} /> Turniketlar</button>
              <button className="btn" onClick={() => goTo("reports")} style={{ justifyContent: "flex-start" }}><Icon name="chart" size={14} /> Hisobot</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default DashboardScreen;
