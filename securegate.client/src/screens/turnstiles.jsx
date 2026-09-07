// Turnstiles / Turniketlar — real API + SignalR (turnstile hubi)
import { useState } from "react";
import { Icon } from "../components/Icon";
import { StatusPill, HubPill, Modal, Field, Toast } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { useHubEvent } from "../hooks/useHub";
import useMutation from "../hooks/useMutation";
import { useAuth } from "../auth/AuthContext";
import { turnstilesApi } from "../api/endpoints";
import { fmtDateTime, fmtTime } from "./utils";
import { useReloadOnReconnect } from "./live";

// Detal oynasidagi jonli jurnal uzunligi.
const MAX_LIVE_LOGS = 30;

const REASON_MIN = 5;
const REASON_MAX = 500;

const TurnstilesScreen = () => {
  const { user } = useAuth();
  const isSuperAdmin = !!user?.isSuperAdmin;
  const { data, loading, error, reload, setData } = useApi(() => turnstilesApi.list(), []);
  const [detailId, setDetailId] = useState(null);
  const [showAdd, setShowAdd] = useState(false);
  const [showEmergency, setShowEmergency] = useState(false);
  const turnstiles = data || [];

  // Har bir mutatsiya xatosi ko'rinadigan bo'lishi shart — bu jismoniy kirish nazorati.
  const act = useMutation((fn, id) => fn(id), { onSuccess: reload });
  const emergency = useMutation(
    (reason) => turnstilesApi.emergencyOpen(reason),
    { onSuccess: () => { setShowEmergency(false); reload(); } }
  );

  const busy = act.busy || emergency.busy;
  const mutError = act.error || emergency.error;
  const clearMutError = () => { act.reset(); emergency.reset(); };

  // Uzilish paytida o'tkazib yuborilgan status o'zgarishlarini qoplash uchun —
  // qayta ulanganda bir marta to'liq qayta o'qish.
  const hubStatus = useReloadOnReconnect("turnstile", reload);

  // DIQQAT: bu hodisa OBYEKT emas, IKKI ALOHIDA ARGUMENT bilan keladi.
  // Faqat o'zgargan turniket satri yangilanadi (butun ro'yxat qayta o'qilmaydi).
  useHubEvent("turnstile", "TurnstileStatusChanged", (id, status) => {
    if (id == null || !status) return;
    setData((list) => (Array.isArray(list)
      ? list.map((t) => (t.id === id && t.status !== status ? { ...t, status } : t))
      : list));
  });

  // Favqulodda ochish boshqa admin tomonidan bajarilishi mumkin — hamma
  // turniket holati birdaniga o'zgaradi, shuning uchun ro'yxatni qayta o'qiymiz.
  useHubEvent("turnstile", "EmergencyOpen", () => reload());

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Turniketlar</h1>
          <div className="page-sub">
            {turnstiles.filter((t) => t.status === "Online").length} faol · {turnstiles.filter((t) => t.status === "Blocked").length} bloklangan
          </div>
        </div>
        <div className="row">
          <HubPill status={hubStatus} title="turnstile hub" />
          <button className="btn" onClick={() => setShowAdd(true)}><Icon name="plus" size={14} /> Yangi turniket</button>
          {/* Endpoint [SuperAdminOnly] — boshqalarga tugma umuman ko'rsatilmaydi. */}
          {isSuperAdmin && (
            <button className="btn danger" disabled={busy} onClick={() => { emergency.reset(); setShowEmergency(true); }}>
              <Icon name="alert" size={14} /> EMERGENCY — Hammasini ochish
            </button>
          )}
        </div>
      </div>

      {/* Amal bajarilmasa admin buni ALBATTA ko'rishi kerak — banner + toast */}
      {mutError && (
        <div style={{ marginBottom: 12 }}>
          <ErrorBox error={mutError} onRetry={clearMutError} />
        </div>
      )}

      {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : turnstiles.length === 0 ? <Empty label="Turniket yo'q" icon="door" /> : (
        <div className="col" style={{ gap: 12 }}>
          {turnstiles.map((t) => (
            <div key={t.id} className="card" style={{ padding: 14 }}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr auto", gap: 14, alignItems: "center" }}>
                <div className="row" style={{ gap: 14 }}>
                  <div className="feed-icon" style={{ width: 42, height: 42, background: "var(--bg-2)" }}><Icon name="door" size={20} /></div>
                  <div>
                    <div className="row" style={{ gap: 8 }}>
                      <span style={{ fontSize: 14, fontWeight: 600 }}>{t.name}</span>
                      <StatusPill status={t.status} />
                    </div>
                    <div className="mono faint" style={{ fontSize: 11.5, marginTop: 2 }}>{t.location || "—"} · {t.linkedCamera?.cameraCode || "kamerasiz"}</div>
                    <div className="row mono tnum" style={{ gap: 16, marginTop: 6, fontSize: 12 }}>
                      <span style={{ color: "var(--accent)" }}>↪ {t.todayPassCount} o'tish</span>
                      <span style={{ color: "var(--danger)" }}>✕ {t.todayDenyCount} rad</span>
                      <span className="faint">uptime {t.uptime}</span>
                    </div>
                  </div>
                </div>
                <div className="col" style={{ gap: 6 }}>
                  <div className="row" style={{ gap: 6 }}>
                    <button className="btn sm primary" disabled={busy} onClick={() => act.run(turnstilesApi.open, t.id)}><Icon name="unlock" size={12} /> Ochish</button>
                    <button className="btn sm" disabled={busy} onClick={() => act.run(turnstilesApi.close, t.id)}><Icon name="lock" size={12} /> Yopish</button>
                  </div>
                  <div className="row" style={{ gap: 6 }}>
                    {t.status === "Blocked"
                      ? <button className="btn sm" disabled={busy} onClick={() => act.run(turnstilesApi.unblock, t.id)} style={{ flex: 1 }}><Icon name="unlock" size={12} /> Blokdan chiqarish</button>
                      : <button className="btn sm" disabled={busy} onClick={() => act.run(turnstilesApi.block, t.id)} style={{ flex: 1 }}><Icon name="ban" size={12} /> Bloklash</button>}
                    <button className="btn sm ghost" onClick={() => setDetailId(t.id)}><Icon name="eye" size={12} /></button>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {detailId != null && <DetailModal key={detailId} id={detailId} onClose={() => setDetailId(null)} />}
      {showAdd && <AddTurnstileModal onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); reload(); }} />}
      {showEmergency && <EmergencyModal mutation={emergency} onClose={() => { emergency.reset(); setShowEmergency(false); }} />}

      <Toast message={mutError?.message} kind="error" onClose={clearMutError} />
    </div>
  );
};

// Favqulodda ochish — backend majburiy `reason` (5-500 belgi) talab qiladi va chaqiruvni audit qiladi.
const EmergencyModal = ({ mutation, onClose }) => {
  const [reason, setReason] = useState("");
  const trimmed = reason.trim();
  const tooShort = trimmed.length < REASON_MIN;
  const tooLong = trimmed.length > REASON_MAX;

  return (
    <Modal open onClose={onClose} title="Favqulodda ochish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn danger" disabled={mutation.busy || tooShort || tooLong} onClick={() => mutation.run(trimmed)}>
          <Icon name="alert" size={14} /> {mutation.busy ? "Ochilmoqda..." : "Hammasini ochish"}
        </button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        <div className="row" style={{ gap: 10, padding: 12, borderRadius: 8, background: "var(--bg-0)", border: "1px solid var(--danger)", color: "var(--danger)", fontSize: 13 }}>
          <Icon name="alert" size={16} />
          <span>Bu amal <b>BARCHA turniketlarni ochadi</b>. Chaqiruv audit qilinadi: kim, qachon, qaysi sabab va IP manzil yozib qo'yiladi.</span>
        </div>
        {mutation.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {mutation.error.message}</div>}
        <Field label="Sabab" hint={`Kamida ${REASON_MIN}, ko'pi bilan ${REASON_MAX} belgi · ${trimmed.length}/${REASON_MAX}`}>
          <textarea
            className="input"
            rows={3}
            style={{ height: "auto", padding: "8px 10px", resize: "vertical" }}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Masalan: Yong'in signali ishga tushdi, binoni evakuatsiya qilish"
          />
        </Field>
      </div>
    </Modal>
  );
};

const DetailModal = ({ id, onClose }) => {
  const { data, loading, error, reload, setData } = useApi(() => turnstilesApi.get(id), [id]);
  const t = data?.turnstile;

  // Oyna ochiq turganda kelgan jonli jurnal yozuvlari (REST yozuvlari ustida ko'rsatiladi).
  const [liveLogs, setLiveLogs] = useState([]);

  useReloadOnReconnect("turnstile", reload);

  // Ochiq oynadagi status ham jonli yangilanadi (ikki alohida argument).
  useHubEvent("turnstile", "TurnstileStatusChanged", (statusId, status) => {
    if (statusId !== id || !status) return;
    setData((d) => (d?.turnstile ? { ...d, turnstile: { ...d.turnstile, status } } : d));
  });

  // TurnstileLog — uchta alohida argument: (id, matn, ISO vaqt).
  useHubEvent("turnstile", "TurnstileLog", (logId, message, timeUtc) => {
    if (logId !== id || !message) return;
    setLiveLogs((prev) => [{ message, time: timeUtc, seq: (prev[0]?.seq ?? 0) + 1 }, ...prev].slice(0, MAX_LIVE_LOGS));
  });

  return (
    <Modal open onClose={onClose} wide title={t ? t.name : "Turniket"}
      footer={<button className="btn" onClick={onClose}>Yopish</button>}>
      {loading ? <Loading /> : error ? <ErrorBox error={error} /> : t && (
        <div className="col" style={{ gap: 14 }}>
          <div className="grid-3" style={{ gap: 10, fontSize: 12.5 }}>
            {[["Joylashuv", t.location || "—"], ["IP", t.ipAddress || "—"], ["Status", t.status],
              ["Bugun o'tish", t.todayPassCount], ["Bugun rad", t.todayDenyCount], ["Uptime", t.uptime]].map(([k, v]) => (
              <div key={k}><div className="faint" style={{ fontSize: 10, textTransform: "uppercase" }}>{k}</div><div className="mono">{String(v)}</div></div>
            ))}
          </div>
          {liveLogs.length > 0 && (
            <div className="card">
              <div className="card-h"><h3>Jonli jurnal</h3><span className="muted" style={{ fontSize: 12 }}>{liveLogs.length} ta yangi</span></div>
              {liveLogs.map((l) => (
                <div key={l.seq} className="row" style={{ padding: "8px 16px", borderBottom: "1px solid var(--border)", gap: 10, fontSize: 13 }}>
                  <span className="mono faint" style={{ fontSize: 11, width: 90 }}>{fmtTime(l.time)}</span>
                  <span style={{ flex: 1 }}>{l.message}</span>
                </div>
              ))}
            </div>
          )}

          <div className="card">
            <div className="card-h"><h3>So'ngi kirish-chiqishlar</h3></div>
            {(data.recentLogs || []).length === 0 ? <Empty label="Yozuv yo'q" /> : (data.recentLogs || []).map((r) => (
              <div key={r.id} className="row" style={{ padding: "10px 16px", borderBottom: "1px solid var(--border)", gap: 10, fontSize: 13 }}>
                <span className="mono faint" style={{ fontSize: 11, width: 130 }}>{fmtDateTime(r.timestamp)}</span>
                <span style={{ flex: 1, fontWeight: 500 }}>{r.userName || "Noma'lum"}</span>
                {r.result === "Granted" ? <span className="pill on">Ruxsat</span> : r.result === "Denied" ? <span className="pill err">Rad</span> : <span className="pill warn">?</span>}
              </div>
            ))}
          </div>
        </div>
      )}
    </Modal>
  );
};

// Har safar yangi mount bo'ladi (shartli render) — forma qiymatlari eskisidan qolmaydi.
const AddTurnstileModal = ({ onClose, onSaved }) => {
  const [f, setF] = useState({ name: "", location: "", model: "ZKTeco", type: "Tripod", direction: "Bidirectional", ipAddress: "", port: 4370 });
  const [test, setTest] = useState(null);
  // Ulanishga oid maydon o'zgarsa — eski test natijasini tozalaymiz
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setTest(null); };

  const create = useMutation(
    (body) => turnstilesApi.create(body),
    { onSuccess: () => onSaved() }
  );

  // Test natijasi (muvaffaqiyat ham, xato ham) shu yerda ko'rsatiladi.
  const conn = useMutation(
    (body) => turnstilesApi.testConnection(body),
    {
      onSuccess: (r) => setTest(r || { ok: true, message: "Ulanish muvaffaqiyatli." }),
      onError: (e) => setTest({ ok: false, message: e.message }),
    }
  );

  const runTest = () => {
    setTest(null); create.reset();
    conn.run({ ipAddress: f.ipAddress || null, port: parseInt(f.port) || 4370 });
  };

  const save = () => create.run({
    ...f,
    port: parseInt(f.port) || 4370,
    location: f.location || null,
    ipAddress: f.ipAddress || null,
    faceRecognitionEnabled: true,
    rfidEnabled: true,
    qrCodeEnabled: false,
  });

  return (
    <Modal open onClose={onClose} title="Yangi turniket qo'shish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={create.busy || !f.name} onClick={save}><Icon name="check" size={14} /> {create.busy ? "..." : "Qo'shish"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        {create.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {create.error.message}</div>}
        <div className="grid-2">
          <Field label="Nomi"><input className="input" value={f.name} onChange={set("name")} /></Field>
          <Field label="Joylashuv"><input className="input" value={f.location} onChange={set("location")} /></Field>
        </div>
        <div className="grid-2">
          <Field label="Model"><select className="select" value={f.model} onChange={set("model")}><option>ZKTeco</option><option>Hikvision</option><option>Dahua</option></select></Field>
          <Field label="Tur"><select className="select" value={f.type} onChange={set("type")}><option value="Tripod">Tripod</option><option value="SpeedGate">Speed gate</option><option value="FlapBarrier">Flap barrier</option><option value="FullHeight">Full height</option></select></Field>
        </div>
        <div className="grid-2">
          <Field label="Yo'nalish"><select className="select" value={f.direction} onChange={set("direction")}><option value="Bidirectional">Ikki tomonlama</option><option value="EntryOnly">Faqat kirish</option><option value="ExitOnly">Faqat chiqish</option></select></Field>
          <Field label="IP manzil"><input className="input mono" value={f.ipAddress} onChange={set("ipAddress")} /></Field>
        </div>
        <div className="col" style={{ gap: 8 }}>
          <button type="button" className="btn" disabled={conn.busy || !f.ipAddress} onClick={runTest} style={{ alignSelf: "flex-start" }}>
            <Icon name="refresh" size={14} /> {conn.busy ? "Tekshirilmoqda..." : "Test ulanib ko'rish"}
          </button>
          {test && (
            <div className="row" style={{ gap: 8, fontSize: 13, color: test.ok ? "var(--accent)" : "var(--danger)" }}>
              <Icon name={test.ok ? "check" : "alert"} size={14} />
              <span>{test.message}{test.elapsedMs ? ` · ${test.elapsedMs} ms` : ""}</span>
            </div>
          )}
        </div>
      </div>
    </Modal>
  );
};

export default TurnstilesScreen;
