// Face detection / Yuz aniqlash — kameralarda aniqlangan odamlar
// (real API + SignalR: camera hubi, NewSighting)
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar, HubPill, Toast } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { useHubEvent } from "../hooks/useHub";
import useMutation from "../hooks/useMutation";
import { cameraUsersApi } from "../api/endpoints";
import { fmtDateTime, toNum } from "./utils";
import { prependCapped, useReloadOnReconnect } from "./live";

const typeLabel = { Unknown: "Noma'lum", Student: "O'quvchi", Teacher: "O'qituvchi", Staff: "Xodim", Guest: "Mehmon" };
const PAGE_SIZE = 15;

// NewSighting -> jadval satri (REST `cameraUsersApi.list` items bilan bir xil shakl).
// `confidence` backendda foizda saqlanadi (Confidence = sim * 100).
const toSightingRow = (p) => ({
  id: p.id,
  fullName: p.fullName,
  userType: p.userType,
  camera: { id: p.cameraId, name: p.cameraName },
  detectedAt: p.detectedAt,
  confidence: p.confidence,
  isReviewed: false,
});

const FacesScreen = () => {
  const [cameraId, setCameraId] = useState("");
  const [userType, setUserType] = useState("");
  const [reviewedOnly, setReviewedOnly] = useState(false);
  const [page, setPage] = useState(1);

  const { data, loading, error, reload, setData } = useApi(
    () => cameraUsersApi.list({
      cameraId: cameraId || undefined,
      userType: userType || undefined,
      reviewedOnly: reviewedOnly || undefined,
      page, pageSize: PAGE_SIZE,
    }),
    [cameraId, userType, reviewedOnly, page]
  );

  const items = data?.items || [];
  const cameras = data?.cameras || [];
  const totalPages = data?.totalPages || 1;

  const review = useMutation(
    (id, reviewed) => cameraUsersApi.markReviewed(id, reviewed),
    { onSuccess: reload }
  );

  // Uzilish paytida o'tkazib yuborilgan yozuvlarni qoplash uchun —
  // qayta ulanganda bir marta qayta o'qish.
  const hubStatus = useReloadOnReconnect("camera", reload);

  // Yangi kuzatuv — jadval boshiga qo'shiladi (sahifa qayta yuklanmaydi).
  // Faqat 1-sahifada va joriy filtrga mos kelsa: 2-sahifada turgan yozuvlar
  // siljib ketmasligi, filtr esa buzilmasligi kerak.
  useHubEvent("camera", "NewSighting", (p) => {
    if (!p || !data) return;
    if (page !== 1 || reviewedOnly) return;
    if (cameraId && String(p.cameraId) !== String(cameraId)) return;
    if (userType && p.userType !== userType) return;
    setData((d) => (d ? {
      ...d,
      items: prependCapped(d.items || [], toSightingRow(p), (x) => x.id, PAGE_SIZE),
      totalCount: (d.totalCount ?? 0) + 1,
      todayCount: (d.todayCount ?? 0) + 1,
      unknownCount: (d.unknownCount ?? 0) + (p.isUnknown ? 1 : 0),
    } : d));
  });

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Yuz aniqlash</h1>
          <div className="page-sub">Kameralarda aniqlangan odamlar</div>
        </div>
        <div className="row">
          <HubPill status={hubStatus} title="camera hub" />
          <button className="btn" onClick={reload}><Icon name="refresh" size={14} /> Yangilash</button>
        </div>
      </div>

      {review.error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorBox error={review.error} onRetry={review.reset} />
        </div>
      )}

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
                  <td className="mono faint" style={{ fontSize: 11.5 }}>{fmtDateTime(x.detectedAt)}</td>
                  <td className="mono tnum" style={{ color: x.confidence == null ? "var(--text-3)" : x.confidence < 50 ? "var(--warn)" : "var(--accent)" }}>
                    {x.confidence != null ? `${toNum(x.confidence).toFixed(1)}%` : "—"}
                  </td>
                  <td>{x.isReviewed ? <span className="pill on">Ko'rilgan</span> : <span className="pill off">Yangi</span>}</td>
                  <td>
                    <button className="btn xs ghost" disabled={review.busy} title="Ko'rilgan deb belgilash" onClick={() => review.run(x.id, !x.isReviewed)}>
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

      <Toast message={review.error?.message} kind="error" onClose={review.reset} />
    </div>
  );
};

export default FacesScreen;
