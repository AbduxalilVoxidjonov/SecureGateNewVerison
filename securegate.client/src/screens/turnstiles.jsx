// Turnstiles / Turniketlar — real API
import { useState } from "react";
import { Icon } from "../components/Icon";
import { StatusPill, Modal, Field } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { turnstilesApi } from "../api/endpoints";

const TurnstilesScreen = () => {
  const { data, loading, error, reload } = useApi(() => turnstilesApi.list(), []);
  const [detailId, setDetailId] = useState(null);
  const [showAdd, setShowAdd] = useState(false);
  const [busy, setBusy] = useState(false);
  const turnstiles = data || [];

  const act = async (id, fn) => { setBusy(true); try { await fn(id); reload(); } finally { setBusy(false); } };
  const emergency = async () => {
    if (!window.confirm("Barcha turniketlar favqulodda ochilsinmi?")) return;
    setBusy(true); try { await turnstilesApi.emergencyOpen(); reload(); } finally { setBusy(false); }
  };

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
          <button className="btn" onClick={() => setShowAdd(true)}><Icon name="plus" size={14} /> Yangi turniket</button>
          <button className="btn danger" disabled={busy} onClick={emergency}><Icon name="alert" size={14} /> EMERGENCY — Hammasini ochish</button>
        </div>
      </div>

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
                    <button className="btn sm primary" disabled={busy} onClick={() => act(t.id, turnstilesApi.open)}><Icon name="unlock" size={12} /> Ochish</button>
                    <button className="btn sm" disabled={busy} onClick={() => act(t.id, turnstilesApi.close)}><Icon name="lock" size={12} /> Yopish</button>
                  </div>
                  <div className="row" style={{ gap: 6 }}>
                    {t.status === "Blocked"
                      ? <button className="btn sm" disabled={busy} onClick={() => act(t.id, turnstilesApi.unblock)} style={{ flex: 1 }}><Icon name="unlock" size={12} /> Blokdan chiqarish</button>
                      : <button className="btn sm" disabled={busy} onClick={() => act(t.id, turnstilesApi.block)} style={{ flex: 1 }}><Icon name="ban" size={12} /> Bloklash</button>}
                    <button className="btn sm ghost" onClick={() => setDetailId(t.id)}><Icon name="eye" size={12} /></button>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      <DetailModal id={detailId} onClose={() => setDetailId(null)} />
      <AddTurnstileModal open={showAdd} onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); reload(); }} />
    </div>
  );
};

const DetailModal = ({ id, onClose }) => {
  const { data, loading, error } = useApi(() => (id ? turnstilesApi.get(id) : Promise.resolve(null)), [id]);
  const t = data?.turnstile;
  return (
    <Modal open={!!id} onClose={onClose} wide title={t ? t.name : "Turniket"}
      footer={<button className="btn" onClick={onClose}>Yopish</button>}>
      {loading ? <Loading /> : error ? <ErrorBox error={error} /> : t && (
        <div className="col" style={{ gap: 14 }}>
          <div className="grid-3" style={{ gap: 10, fontSize: 12.5 }}>
            {[["Joylashuv", t.location || "—"], ["IP", t.ipAddress || "—"], ["Status", t.status],
              ["Bugun o'tish", t.todayPassCount], ["Bugun rad", t.todayDenyCount], ["Uptime", t.uptime]].map(([k, v]) => (
              <div key={k}><div className="faint" style={{ fontSize: 10, textTransform: "uppercase" }}>{k}</div><div className="mono">{String(v)}</div></div>
            ))}
          </div>
          <div className="card">
            <div className="card-h"><h3>So'ngi kirish-chiqishlar</h3></div>
            {(data.recentLogs || []).length === 0 ? <Empty label="Yozuv yo'q" /> : (data.recentLogs || []).map((r) => (
              <div key={r.id} className="row" style={{ padding: "10px 16px", borderBottom: "1px solid var(--border)", gap: 10, fontSize: 13 }}>
                <span className="mono faint" style={{ fontSize: 11, width: 130 }}>{new Date(r.timestamp).toLocaleString("uz-UZ")}</span>
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

const AddTurnstileModal = ({ open, onClose, onSaved }) => {
  const [f, setF] = useState({ name: "", location: "", model: "ZKTeco", type: "Tripod", direction: "Bidirectional", ipAddress: "", port: 4370 });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState(null);
  const [test, setTest] = useState(null);
  const [testing, setTesting] = useState(false);
  // Ulanishga oid maydon o'zgarsa — eski test natijasini tozalaymiz
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setTest(null); };

  const runTest = async () => {
    setTesting(true); setTest(null); setErr(null);
    try {
      const r = await turnstilesApi.testConnection({ ipAddress: f.ipAddress || null, port: parseInt(f.port) || 4370 });
      setTest(r);
    } catch (e) {
      setTest({ ok: false, message: e.message });
    } finally { setTesting(false); }
  };

  const save = async () => {
    setBusy(true); setErr(null);
    try { await turnstilesApi.create({ ...f, port: parseInt(f.port) || 4370, location: f.location || null, ipAddress: f.ipAddress || null, faceRecognitionEnabled: true, rfidEnabled: true, qrCodeEnabled: false }); onSaved(); }
    catch (e) { setErr(e.message); } finally { setBusy(false); }
  };
  return (
    <Modal open={open} onClose={onClose} title="Yangi turniket qo'shish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={busy || !f.name} onClick={save}><Icon name="check" size={14} /> {busy ? "..." : "Qo'shish"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        {err && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {err}</div>}
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
          <button type="button" className="btn" disabled={testing || !f.ipAddress} onClick={runTest} style={{ alignSelf: "flex-start" }}>
            <Icon name="refresh" size={14} /> {testing ? "Tekshirilmoqda..." : "Test ulanib ko'rish"}
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
