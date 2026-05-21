// Face detection / Yuz aniqlash — kameralarda aniqlangan odamlar (real API)
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { cameraUsersApi } from "../api/endpoints";

const typeLabel = { Unknown: "Noma'lum", Student: "O'quvchi", Teacher: "O'qituvchi", Staff: "Xodim", Guest: "Mehmon" };

const FacesScreen = () => {
  const [cameraId, setCameraId] = useState("");
  const [userType, setUserType] = useState("");
  const [reviewedOnly, setReviewedOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);

  const { data, loading, error, reload } = useApi(
    () => cameraUsersApi.list({
      cameraId: cameraId || undefined,
      userType: userType || undefined,
      reviewedOnly: reviewedOnly || undefined,
      page, pageSize: 15,
    }),
    [cameraId, userType, reviewedOnly, page]
  );

  const items = data?.items || [];
  const cameras = data?.cameras || [];
  const totalPages = data?.totalPages || 1;

  const markReviewed = async (item) => {
    setBusy(true);
    try { await cameraUsersApi.markReviewed(item.id, !item.isReviewed); reload(); } finally { setBusy(false); }
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Yuz aniqlash</h1>
          <div className="page-sub">Kameralarda aniqlangan odamlar</div>
        </div>
        <button className="btn" onClick={reload}><Icon name="refresh" size={14} /> Yangilash</button>
      </div>

      <div className="stat-grid">
        <div className="stat"><div className="label"><Icon name="face" size={14} /> Jami aniqlangan</div><div className="v tnum">{data?.totalCount ?? 0}</div></div>
        <div className="stat"><div className="label"><Icon name="clock" size={14} /> Bugun</div><div className="v tnum">{data?.todayCount ?? 0}</div></div>
        <div className="stat"><div className="label"><Icon name="alert" size={14} /> Noma'lum</div><div className="v tnum" style={{ color: "var(--warn)" }}>{data?.unknownCount ?? 0}</div></div>
        <div className="stat"><div className="label"><Icon name="users" size={14} /> Noyob odamlar</div><div className="v tnum">{data?.uniquePeopleCount ?? 0}</div></div>
      </div>

      <div className="card" style={{ padding: 12, margin: "14px 0", display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
        <select className="select" value={cameraId} onChange={(e) => { setPage(1); setCameraId(e.target.value); }}>
          <option value="">Barcha kameralar</option>
          {cameras.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select className="select" value={userType} onChange={(e) => { setPage(1); setUserType(e.target.value); }}>
          <option value="">Barcha turlar</option>
          {Object.entries(typeLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <label className="check"><input type="checkbox" checked={reviewedOnly} onChange={(e) => { setPage(1); setReviewedOnly(e.target.checked); }} /> Faqat ko'rib chiqilgan</label>
      </div>

      <div className="card">
        {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : items.length === 0 ? <Empty label="Aniqlangan yuzlar yo'q" icon="face" /> : (
          <table className="tbl">
            <thead>
              <tr><th>Odam</th><th>Turi</th><th>Kamera</th><th>Vaqt</th><th>Aniqlik</th><th>Holat</th><th></th></tr>
            </thead>
            <tbody>
              {items.map((x) => (
                <tr key={x.id}>
                  <td>
                    <div className="row" style={{ gap: 10 }}>
                      {x.userType === "Unknown"
                        ? <div className="feed-icon warn" style={{ width: 28, height: 28 }}><Icon name="alert" size={13} /></div>
                        : <Avatar name={x.fullName} />}
                      <span style={{ fontWeight: 500, color: x.userType === "Unknown" ? "var(--warn)" : undefined }}>{x.fullName || "Noma'lum"}</span>
                    </div>
                  </td>
                  <td><span className={`pill ${x.userType === "Unknown" ? "warn" : "off"}`}>{typeLabel[x.userType] || x.userType}</span></td>
                  <td className="mono faint">{x.camera?.name || x.camera?.cameraCode || "—"}</td>
                  <td className="mono faint" style={{ fontSize: 11.5 }}>{new Date(x.detectedAt).toLocaleString("uz-UZ")}</td>
                  <td className="mono tnum" style={{ color: x.confidence == null ? "var(--text-3)" : x.confidence < 50 ? "var(--warn)" : "var(--accent)" }}>
                    {x.confidence != null ? `${x.confidence.toFixed(1)}%` : "—"}
                  </td>
                  <td>{x.isReviewed ? <span className="pill on">Ko'rilgan</span> : <span className="pill off">Yangi</span>}</td>
                  <td>
                    <button className="btn xs ghost" disabled={busy} title="Ko'rilgan deb belgilash" onClick={() => markReviewed(x)}>
                      <Icon name={x.isReviewed ? "x" : "check"} size={12} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {totalPages > 1 && (
          <div className="row" style={{ justifyContent: "space-between", padding: "12px 16px", borderTop: "1px solid var(--border)" }}>
            <span className="muted" style={{ fontSize: 12 }}>{page} / {totalPages} sahifa</span>
            <div className="row" style={{ gap: 4 }}>
              <button className="btn xs" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>‹ Oldingi</button>
              <button className="btn xs" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Keyingi ›</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default FacesScreen;
