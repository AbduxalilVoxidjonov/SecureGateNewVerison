// Blocked users / Bloklangan foydalanuvchilar — real API
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { blockedApi, usersApi } from "../api/endpoints";

const BlockedScreen = () => {
  const { data, loading, error, reload } = useApi(() => blockedApi.list({ pageSize: 100 }), []);
  const [busy, setBusy] = useState(false);
  const items = data?.items || [];

  const unblock = async (u) => {
    setBusy(true);
    try { await usersApi.unblock(u.id); reload(); } finally { setBusy(false); }
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Bloklangan foydalanuvchilar</h1>
          <div className="page-sub">Jami {data?.totalCount ?? items.length} ta</div>
        </div>
        <button className="btn" onClick={reload}><Icon name="refresh" size={14} /> Yangilash</button>
      </div>

      {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : items.length === 0 ? <Empty label="Bloklangan foydalanuvchi yo'q" icon="ban" /> : (
        <div className="col" style={{ gap: 12 }}>
          {items.map((b) => (
            <div key={b.id} className="card" style={{ padding: 14 }}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr auto", gap: 14, alignItems: "center" }}>
                <div className="row" style={{ gap: 14 }}>
                  <Avatar name={b.fullName} size="lg" />
                  <div>
                    <div className="row" style={{ gap: 8 }}>
                      <span style={{ fontWeight: 600 }}>{b.fullName}</span>
                      <span className="pill err">Bloklangan</span>
                    </div>
                    <div className="row mono faint" style={{ gap: 14, fontSize: 11.5, marginTop: 6 }}>
                      <span>ID: <span style={{ color: "var(--text-1)" }}>#{String(b.id).padStart(4, "0")}</span></span>
                      <span>Telefon: <span style={{ color: "var(--text-1)" }}>{b.phone || "—"}</span></span>
                    </div>
                  </div>
                </div>
                <button className="btn primary sm" disabled={busy} onClick={() => unblock(b)}><Icon name="unlock" size={12} /> Blokdan chiqarish</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default BlockedScreen;
