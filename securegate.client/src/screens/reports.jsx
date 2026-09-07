// Reports / Hisobotlar — real API + SignalR (camera hubi: NewAccessLog)
import { useCallback, useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar, HubPill } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { useHubEvent } from "../hooks/useHub";
import { reportsApi, accessLogsApi } from "../api/endpoints";
import { fmtDateTime, toNum, hasNum } from "./utils";
import { prependCapped, useReloadOnReconnect } from "./live";

const methodLabel = { Face: "Yuz", Card: "Karta", QrCode: "QR", Manual: "Manual" };
const WEEK_LABELS = ["Du", "Se", "Cho", "Pa", "Ju", "Sh", "Ya"];
const PAGE_SIZE = 15;

// NewAccessLog -> jadval satri (REST `accessLogsApi.list` bilan bir xil shakl).
// DIQQAT: `confidence` backendda allaqachon foizda (FaceConfidence = sim * 100).
const toLogRow = (p) => ({
  id: p.id,
  timestamp: p.timestamp,
  userName: p.fullName,
  method: p.method,
  result: p.result,
  faceConfidence: p.confidence,
});

const ReportsScreen = () => {
  const [method, setMethod] = useState("");
  const report = useApi(() => reportsApi.get(), []);
  const logs = useApi(() => accessLogsApi.list({ method: method || undefined, pageSize: PAGE_SIZE }), [method]);

  // Qayta ulanganda ikkala blok ham bir marta qayta o'qiladi.
  const reloadReport = report.reload;
  const reloadLogs = logs.reload;
  const reloadAll = useCallback(() => { reloadReport(); reloadLogs(); }, [reloadReport, reloadLogs]);
  const hubStatus = useReloadOnReconnect("camera", reloadAll);

  // Yangi kirish-chiqish jurnal jadvalining boshiga qo'shiladi — butun sahifa
  // qayta yuklanmaydi. Faol filtrga mos kelmasa e'tiborsiz qoldiriladi.
  useHubEvent("camera", "NewAccessLog", (p) => {
    if (!p || !logs.data) return;
    if (method && p.method !== method) return;
    logs.setData((d) => (d ? {
      ...d,
      items: prependCapped(d.items || [], toLogRow(p), (x) => x.id, PAGE_SIZE),
      totalCount: (d.totalCount ?? 0) + 1,
    } : d));
  });

  // Bitta so'rovning xatosi ikkinchisini o'ldirmasligi kerak — bloklar mustaqil render qilinadi.
  const r = report.data || {};
  const weekly = Array.isArray(r.weeklyData) ? r.weeklyData : [];
  const maxWeekly = Math.max(1, ...weekly.map((v) => toNum(v)));
  const items = logs.data?.items || [];

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Hisobotlar</h1>
          <div className="page-sub">Haftalik xulosa va kirish jurnali</div>
        </div>
        <div className="row">
          <HubPill status={hubStatus} title="camera hub" />
          <button className="btn" onClick={reloadAll}><Icon name="refresh" size={14} /> Yangilash</button>
        </div>
      </div>

      {/* ---- Haftalik xulosa (mustaqil blok) ---- */}
      {report.loading ? <Loading /> : report.error ? <ErrorBox error={report.error} onRetry={report.reload} /> : (
        <>
          <div className="stat-grid">
            <div className="stat">
              <div className="label"><Icon name="check" size={14} /> Haftalik o'tishlar</div>
              <div className="v tnum">{toNum(r.weeklyPassCount)}</div>
              <div className="sub">So'nggi 7 kun</div>
            </div>
            {/* Backend bu maydonni olib tashlashi/`null` qaytarishi mumkin — faqat sonli bo'lsa ko'rsatamiz. */}
            {hasNum(r.averageAttendance) && (
              <div className="stat">
                <div className="label"><Icon name="users" size={14} /> O'rtacha davomat</div>
                <div className="v tnum">{toNum(r.averageAttendance).toFixed(1)}<span style={{ fontSize: 18 }}>%</span></div>
                <div className="sub">Kunlik o'rtacha</div>
              </div>
            )}
            {hasNum(r.lateArrivals) && (
              <div className="stat">
                <div className="label"><Icon name="clock" size={14} /> Kechikishlar</div>
                <div className="v tnum" style={{ color: "var(--warn)" }}>{toNum(r.lateArrivals)}</div>
                <div className="sub">Bu hafta</div>
              </div>
            )}
            <div className="stat">
              <div className="label"><Icon name="ban" size={14} /> Rad etilgan</div>
              <div className="v tnum" style={{ color: "var(--danger)" }}>{toNum(r.deniedCount)}</div>
              <div className="sub">Ruxsatsiz urinishlar</div>
            </div>
          </div>

          <div className="card padded" style={{ marginTop: 14 }}>
            <div style={{ fontSize: 14, fontWeight: 600, marginBottom: 4 }}>Haftalik trafik</div>
            <div className="muted" style={{ fontSize: 12, marginBottom: 14 }}>So'nggi 7 kunlik o'tishlar</div>
            <div className="bar-chart" style={{ height: 140, gridTemplateColumns: `repeat(${Math.max(1, weekly.length)}, 1fr)` }}>
              {weekly.map((v, i) => (
                <div key={WEEK_LABELS[i] || `day-${i}`} className="bar" style={{ height: `${Math.max(4, (toNum(v) / maxWeekly) * 100)}%` }} title={`${toNum(v)}`} />
              ))}
            </div>
            <div className="row mono" style={{ justifyContent: "space-between", marginTop: 6, fontSize: 10.5, color: "var(--text-3)" }}>
              {WEEK_LABELS.map((d) => <span key={d}>{d}</span>)}
            </div>
          </div>
        </>
      )}

      {/* ---- Kirish jurnali (mustaqil blok) ---- */}
      <div className="card" style={{ marginTop: 14 }}>
        <div className="card-h">
          <h3>Kirish jurnali</h3>
          <select className="select" value={method} onChange={(e) => setMethod(e.target.value)} style={{ height: 30 }}>
            <option value="">Barcha usullar</option>
            <option value="Face">Yuz</option>
            <option value="Card">Karta</option>
            <option value="QrCode">QR kod</option>
          </select>
        </div>
        {logs.loading ? <Loading /> : logs.error ? <ErrorBox error={logs.error} onRetry={logs.reload} /> : items.length === 0 ? <Empty label="Jurnal bo'sh" /> : (
          <table className="tbl">
            <thead>
              <tr><th>Vaqt</th><th>Foydalanuvchi</th><th>Usul</th><th>Natija</th><th>Aniqlik</th></tr>
            </thead>
            <tbody>
              {items.map((x) => (
                <tr key={x.id}>
                  <td className="mono faint" style={{ fontSize: 11.5 }}>{fmtDateTime(x.timestamp)}</td>
                  <td>
                    <div className="row" style={{ gap: 8 }}>
                      <Avatar name={x.userName || "Noma'lum"} size="sm" />
                      <span>{x.userName || "Noma'lum"}</span>
                    </div>
                  </td>
                  <td><span className="pill off">{methodLabel[x.method] || x.method}</span></td>
                  <td>
                    {x.result === "Granted" && <span className="pill on">Ruxsat</span>}
                    {x.result === "Denied" && <span className="pill err">Rad etildi</span>}
                    {x.result === "Unknown" && <span className="pill warn">Noma'lum</span>}
                  </td>
                  <td className="mono tnum" style={{ color: x.faceConfidence == null ? "var(--text-3)" : toNum(x.faceConfidence) < 50 ? "var(--warn)" : "var(--accent)" }}>
                    {x.faceConfidence != null ? `${toNum(x.faceConfidence).toFixed(1)}%` : "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};

export default ReportsScreen;
