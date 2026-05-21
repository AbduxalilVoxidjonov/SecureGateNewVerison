// Roles & permissions / Rollar va huquqlar — real API (ruxsatlar katalogi)
import { Icon } from "../components/Icon";
import { Loading, ErrorBox } from "../components/state";
import { useApi } from "../hooks/useApi";
import { adminsApi } from "../api/endpoints";

const ROLES = [
  { n: "Super Admin", c: "warn", icon: "crown", desc: "Tizimning barcha huquqlari, hech qanday cheklov yo'q." },
  { n: "Admin", c: "info", icon: "shield", desc: "Tayinlangan ruxsatlar va kamera guruhlari bo'yicha boshqaruv." },
];

const RolesScreen = () => {
  const { data, loading, error, reload } = useApi(() => adminsApi.permissions(), []);
  const groups = data || [];

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Rollar va huquqlar</h1>
          <div className="page-sub">Role-Based Access Control (RBAC) · ruxsatlar adminlarga biriktiriladi</div>
        </div>
      </div>

      <div className="grid-2" style={{ gridTemplateColumns: "repeat(2, 1fr)", gap: 14 }}>
        {ROLES.map((r) => (
          <div key={r.n} className="card padded">
            <div className="row" style={{ justifyContent: "space-between", marginBottom: 10 }}>
              <div style={{ width: 38, height: 38, borderRadius: 8, background: "var(--bg-2)", display: "grid", placeItems: "center", color: `var(--${r.c === "warn" ? "warn" : "info"})` }}>
                <Icon name={r.icon} size={18} />
              </div>
              <span className={`pill ${r.c}`}>{r.n}</span>
            </div>
            <div style={{ fontWeight: 600, fontSize: 15 }}>{r.n}</div>
            <div className="muted" style={{ fontSize: 12.5, marginTop: 4 }}>{r.desc}</div>
          </div>
        ))}
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="card-h">
          <h3>Ruxsatlar katalogi</h3>
          <button className="btn xs ghost" onClick={reload}><Icon name="refresh" size={12} /></button>
        </div>
        {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : (
          <div style={{ padding: 16 }} className="grid-2">
            {groups.map((g) => (
              <div key={g.groupName} className="card" style={{ padding: 12, background: "var(--bg-0)" }}>
                <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>{g.groupName}</div>
                <div className="col" style={{ gap: 6 }}>
                  {g.permissions.map((p) => (
                    <div key={p.code} className="row" style={{ gap: 8, fontSize: 12.5 }}>
                      <Icon name="check" size={13} color="var(--accent)" />
                      <span>{p.label}</span>
                      <span className="mono faint" style={{ marginLeft: "auto", fontSize: 11 }}>{p.code}</span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

export default RolesScreen;
