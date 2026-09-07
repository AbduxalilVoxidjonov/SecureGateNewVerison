// Recordings / Yozuvlar tarixi — NVR arxivi (Hikvision) uchun vaqt oralig'i bo'yicha qidiruv.
//
// Backend kontrakti:
//   GET /api/recordings                     -> [{ id, name, cameraCode, location, deviceKind,
//                                                 channelNumber, status, archiveSupported }]
//   GET /api/recordings/camera/{id}?from&to -> { camera, archiveSupported, from, to, segments[], message }
//   GET /api/recordings/camera/{id}/download?from&to -> video/mp4 (from/to MAJBURIY)
//
// Cheklovlar (backend 400 qaytarishidan oldin frontendda tekshiriladi):
//   qidiruv oralig'i <= 7 kun, yuklab olish oralig'i <= 4 soat.
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Toast, Field } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { recordingsApi } from "../api/endpoints";
import { getToken } from "../api/client";
import { fmtDateTime, fmtTime, toNum } from "./utils";

const HOUR_MS = 3_600_000;
const MAX_DOWNLOAD_MS = 4 * HOUR_MS;   // backend: yuklab olish oralig'i max 4 soat
const MAX_SEARCH_MS = 7 * 24 * HOUR_MS; // backend: qidiruv oralig'i max 7 kun

const fmtSize = (bytes) => {
  const n = toNum(bytes, 0);
  if (n <= 0) return "—";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let s = n, u = 0;
  while (s >= 1024 && u < units.length - 1) { s /= 1024; u++; }
  return `${s.toFixed(1)} ${units[u]}`;
};

// 630 -> "10 daq 30 s", 3900 -> "1 soat 5 daq"
const fmtDuration = (secs) => {
  const total = Math.max(0, Math.round(toNum(secs, 0)));
  if (!total) return "—";
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const parts = [];
  if (h) parts.push(`${h} soat`);
  if (m) parts.push(`${m} daq`);
  if (s || parts.length === 0) parts.push(`${s} s`);
  return parts.join(" ");
};

const pad = (n) => String(n).padStart(2, "0");

// Date -> "YYYY-MM-DDTHH:mm" (MAHALLIY vaqt — <input type="datetime-local"> shu formatni kutadi).
const toLocalInput = (d) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

// "YYYY-MM-DDTHH:mm" (mahalliy) -> ISO UTC. NOZIK NUQTA: backend UTC kutadi, input esa
// mahalliy vaqt beradi — `new Date(local)` uni brauzer zonasida talqin qiladi, toISOString() UTC ga o'giradi.
const toUtcIso = (local) => {
  if (!local) return null;
  const d = new Date(local);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
};

// Tez tanlash tugmalari -> [from, to] Date juftligi
const presetRange = (kind) => {
  const now = new Date();
  if (kind === "1h") return [new Date(now.getTime() - HOUR_MS), now];
  if (kind === "24h") return [new Date(now.getTime() - 24 * HOUR_MS), now];
  if (kind === "today") {
    const s = new Date(now); s.setHours(0, 0, 0, 0);
    return [s, now];
  }
  // "kecha": kechagi 00:00 dan bugungi 00:00 gacha
  const s = new Date(now); s.setHours(0, 0, 0, 0); s.setDate(s.getDate() - 1);
  const e = new Date(s); e.setDate(e.getDate() + 1);
  return [s, e];
};

const deviceLabel = (c) => {
  if (!c) return "—";
  if (c.deviceKind === "NvrChannel") {
    const ch = toNum(c.channelNumber, 0);
    return ch > 0 ? `NVR · kanal ${ch}` : "NVR kanali";
  }
  return "IP-kamera";
};

// Sessiya tugagan bo'lsa boshqa (tushunarli) xabar ko'rsatamiz.
const isAuthError = (e) => e?.status === 401;

// Arxiv fayllari bir necha GB bo'lishi mumkin — blob'ga o'qish tab'ni o'ldiradi.
// Shuning uchun to'g'ridan-to'g'ri navigatsiya: token query-string orqali
// (snapshot/stream uchun allaqachon qo'llanadigan naqsh).
const downloadHref = (camId, from, to) => {
  const base = recordingsApi.downloadUrl(camId, from, to);
  const token = getToken();
  if (!token) return null;
  const sep = base.includes("?") ? "&" : "?";
  return `${base}${sep}access_token=${encodeURIComponent(token)}`;
};

const fileNameFor = (cam, from) => {
  const code = cam?.cameraCode || cam?.id || "camera";
  const stamp = (from || "").replace(/[:.]/g, "-");
  return `${code}-${stamp || "archive"}.mp4`;
};

const RecordingsScreen = () => {
  const camsState = useApi((signal) => recordingsApi.list(signal), []);
  const [selectedId, setSelectedId] = useState(null);
  const [msg, setMsg] = useState(null);

  const initial = presetRange("24h");
  const [from, setFrom] = useState(() => toLocalInput(initial[0]));
  const [to, setTo] = useState(() => toLocalInput(initial[1]));

  const fromIso = toUtcIso(from);
  const toIso = toUtcIso(to);
  const rangeMs = fromIso && toIso ? new Date(toIso).getTime() - new Date(fromIso).getTime() : 0;

  const rangeError =
    !fromIso || !toIso ? "Sana va vaqt oralig'ini to'liq kiriting."
      : rangeMs <= 0 ? "\"Dan\" vaqti \"gacha\" vaqtidan oldin bo'lishi kerak."
        : rangeMs > MAX_SEARCH_MS ? "Qidiruv oralig'i 7 kundan oshmasligi kerak."
          : null;

  const rangeTooLongToDownload = rangeMs > MAX_DOWNLOAD_MS;

  const archive = useApi(
    (signal) => (selectedId && !rangeError
      ? recordingsApi.camera(selectedId, fromIso, toIso, signal)
      : Promise.resolve(null)),
    [selectedId, fromIso, toIso, rangeError]
  );

  const cams = camsState.data || [];
  const listCam = cams.find((c) => c.id === selectedId) || null;
  const cam = archive.data?.camera || listCam;
  const camId = cam?.id ?? selectedId;
  const segments = archive.data?.segments || [];
  // Detal javobi kelmaguncha ro'yxatdagi bayroqqa tayanamiz.
  const supported = archive.data ? archive.data.archiveSupported !== false : listCam?.archiveSupported !== false;

  const applyPreset = (kind) => {
    const [a, b] = presetRange(kind);
    setFrom(toLocalInput(a));
    setTo(toLocalInput(b));
  };

  // Token yo'q = sessiya tugagan. Aks holda brauzer faylni o'zi oqim bilan yuklab oladi.
  const guardDownload = (e) => {
    if (!getToken()) {
      e.preventDefault();
      setMsg("Sessiya tugagan — yuklab olish uchun tizimga qayta kiring.");
    }
  };

  const rangeHref = camId != null && !rangeError && !rangeTooLongToDownload
    ? downloadHref(camId, fromIso, toIso)
    : null;

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Yozuvlar tarixi</h1>
          <div className="page-sub">NVR arxividan vaqt oralig'i bo'yicha qidirish va yuklab olish</div>
        </div>
        <button className="btn" onClick={camsState.reload}><Icon name="refresh" size={14} /> Yangilash</button>
      </div>

      <div className="two-col">
        {/* Kameralar ro'yxati */}
        <div className="card">
          <div className="card-h"><h3>Kameralar</h3></div>
          {camsState.loading ? <Loading />
            : camsState.error ? <ErrorBox error={camsState.error} onRetry={camsState.reload} />
              : cams.length === 0 ? <Empty label="Kamera yo'q" icon="camera" /> : (
                <table className="tbl">
                  <thead><tr><th>Kamera</th><th>Turi</th><th>Arxiv</th><th></th></tr></thead>
                  <tbody>
                    {cams.map((c) => (
                      <tr
                        key={c.id}
                        onClick={() => setSelectedId(c.id)}
                        style={{ cursor: "pointer", background: selectedId === c.id ? "var(--bg-3)" : "" }}
                      >
                        <td>
                          <div style={{ fontWeight: 500 }}>{c.name}</div>
                          <div className="mono faint" style={{ fontSize: 11 }}>
                            {c.cameraCode}{c.location ? ` · ${c.location}` : ""}
                          </div>
                        </td>
                        <td style={{ fontSize: 12 }}>{deviceLabel(c)}</td>
                        <td>
                          {c.archiveSupported
                            ? <span className="pill on">Bor</span>
                            : <span className="pill off">Yo'q</span>}
                        </td>
                        <td><Icon name="chevron" size={13} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
        </div>

        {/* Arxiv */}
        <div className="card">
          <div className="card-h">
            <h3>{cam ? `${cam.name} · arxiv` : "Kamerani tanlang"}</h3>
            {cam && <span className="faint" style={{ fontSize: 12 }}>{deviceLabel(cam)}</span>}
          </div>

          {!selectedId ? <Empty label="Chapdan kamera tanlang" icon="film" /> : (
            <div className="col" style={{ gap: 14, padding: 14 }}>
              {/* Vaqt oralig'i tanlagich */}
              <div className="col" style={{ gap: 10 }}>
                <div className="grid-2">
                  <Field label="Dan">
                    <input
                      className="input mono"
                      type="datetime-local"
                      value={from}
                      onChange={(e) => setFrom(e.target.value)}
                    />
                  </Field>
                  <Field label="Gacha">
                    <input
                      className="input mono"
                      type="datetime-local"
                      value={to}
                      onChange={(e) => setTo(e.target.value)}
                    />
                  </Field>
                </div>
                <div className="row" style={{ gap: 6, flexWrap: "wrap" }}>
                  <button type="button" className="btn xs" onClick={() => applyPreset("1h")}>Oxirgi 1 soat</button>
                  <button type="button" className="btn xs" onClick={() => applyPreset("24h")}>Oxirgi 24 soat</button>
                  <button type="button" className="btn xs" onClick={() => applyPreset("today")}>Bugun</button>
                  <button type="button" className="btn xs" onClick={() => applyPreset("yesterday")}>Kecha</button>
                  <div style={{ flex: 1 }} />
                  <button type="button" className="btn xs" onClick={archive.reload} disabled={!!rangeError}>
                    <Icon name="refresh" size={11} /> Qidirish
                  </button>
                </div>
                {rangeError && (
                  <div className="row" style={{ gap: 8, fontSize: 12.5, color: "var(--danger)" }}>
                    <Icon name="alert" size={14} /> {rangeError}
                  </div>
                )}
              </div>

              {/* Butun oraliqni bitta fayl qilib yuklab olish */}
              {supported && !rangeError && (
                <div className="row" style={{ gap: 8, flexWrap: "wrap", alignItems: "center" }}>
                  {rangeHref ? (
                    <a
                      className="btn xs"
                      style={{ textDecoration: "none", color: "var(--text-0)" }}
                      href={rangeHref}
                      download={fileNameFor(cam, fromIso)}
                      onClick={guardDownload}
                    >
                      <Icon name="download" size={11} /> Tanlangan oraliqni yuklab olish
                    </a>
                  ) : (
                    <button type="button" className="btn xs" disabled title="Yuklab olish oralig'i 4 soatdan oshmasligi kerak">
                      <Icon name="download" size={11} /> Tanlangan oraliqni yuklab olish
                    </button>
                  )}
                  {rangeTooLongToDownload && (
                    <span className="faint" style={{ fontSize: 11.5 }}>
                      Oraliq 4 soatdan uzun — butun oraliqni bitta fayl qilib yuklab bo'lmaydi.
                      Quyidagi segmentlarni alohida yuklab oling.
                    </span>
                  )}
                </div>
              )}

              {/* Natija */}
              {archive.loading ? <Loading />
                : isAuthError(archive.error) ? (
                  <div className="card padded error-box">
                    <Icon name="lock" size={18} />
                    <span style={{ flex: 1 }}>Sessiya tugagan — tizimga qayta kiring.</span>
                  </div>
                )
                  : archive.error ? <ErrorBox error={archive.error} onRetry={archive.reload} />
                    : !supported ? (
                      <div className="col" style={{ gap: 10, padding: 16, border: "1px solid var(--border)", borderRadius: 10 }}>
                        <div className="row" style={{ gap: 8, color: "var(--warn)", fontSize: 13 }}>
                          <Icon name="alert" size={16} />
                          <strong>Bu kamera uchun arxiv mavjud emas</strong>
                        </div>
                        <div style={{ fontSize: 12.5, color: "var(--text-2)" }}>
                          {archive.data?.message || "Kamera NVR kanali sifatida sozlanmagan — arxiv faqat NVR kanallari uchun ishlaydi."}
                        </div>
                        <div style={{ fontSize: 12.5, color: "var(--text-2)" }}>
                          <div style={{ marginBottom: 4 }}><strong>Bu kamerani NVR kanali sifatida sozlash:</strong></div>
                          <ol style={{ margin: 0, paddingLeft: 18, lineHeight: 1.7 }}>
                            <li>&laquo;Kameralar&raquo; bo'limiga o'ting va kerakli kamerani tahrirlang.</li>
                            <li>&laquo;Qurilma turi&raquo; maydonida <strong>NVR kanali</strong> ni tanlang.</li>
                            <li><strong>IP manzil</strong> va <strong>Port</strong> ga NVR ning manzilini kiriting (kameraning emas).</li>
                            <li><strong>Kanal raqami</strong> ni NVR dagi kanal tartibiga mos qo'ying (1..64).</li>
                            <li>NVR login/parolini kiriting va saqlang — RTSP havolasini backend o'zi quradi.</li>
                          </ol>
                        </div>
                      </div>
                    )
                      : segments.length === 0 ? (
                        <Empty label={archive.data?.message || "Bu oraliqda yozuv topilmadi"} icon="film" />
                      ) : (
                        <table className="tbl">
                          <thead>
                            <tr><th>Boshlanish</th><th>Tugash</th><th>Davomiylik</th><th>Hajm</th><th></th></tr>
                          </thead>
                          <tbody>
                            {segments.map((s, i) => {
                              const tooLong = toNum(s.durationSeconds, 0) * 1000 > MAX_DOWNLOAD_MS;
                              const href = camId != null && !tooLong && s.startUtc && s.endUtc
                                ? downloadHref(camId, s.startUtc, s.endUtc)
                                : null;
                              return (
                                <tr key={`${s.startUtc || "seg"}-${i}`}>
                                  <td className="mono">{fmtDateTime(s.startUtc)}</td>
                                  <td className="mono">{fmtTime(s.endUtc)}</td>
                                  <td className="mono">{fmtDuration(s.durationSeconds)}</td>
                                  <td className="mono">{fmtSize(s.sizeBytes)}</td>
                                  <td>
                                    {href ? (
                                      <a
                                        className="btn xs"
                                        style={{ textDecoration: "none", color: "var(--text-0)" }}
                                        href={href}
                                        download={fileNameFor(cam, s.startUtc)}
                                        onClick={guardDownload}
                                      >
                                        <Icon name="download" size={11} /> Yuklab olish
                                      </a>
                                    ) : (
                                      <button
                                        type="button"
                                        className="btn xs"
                                        disabled
                                        title={tooLong
                                          ? "Segment 4 soatdan uzun — yuklab olish cheklovi (max 4 soat)"
                                          : "Segment vaqtlari to'liq emas"}
                                      >
                                        <Icon name="download" size={11} /> Yuklab olish
                                      </button>
                                    )}
                                  </td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      )}
            </div>
          )}
        </div>
      </div>

      <Toast message={msg} kind="error" onClose={() => setMsg(null)} />
    </div>
  );
};

export default RecordingsScreen;
