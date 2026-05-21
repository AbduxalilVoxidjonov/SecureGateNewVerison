// Cameras / Kameralar — real API
import { useState, useEffect } from "react";
import { Icon } from "../components/Icon";
import { CameraTile, StatusPill, Modal, Field } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { camerasApi } from "../api/endpoints";

// API kamera -> CameraTile kutadigan shaklga moslash
const toTile = (c) => ({
  code: c.cameraCode,
  ip: c.ipAddress || "—",
  status: c.status === "Offline" ? "offline" : "live",
  fps: c.fps,
  faces: [],
});

const CamerasScreen = () => {
  const [group, setGroup] = useState("");
  const [statusF, setStatusF] = useState("");
  const [qInput, setQInput] = useState("");
  const [search, setSearch] = useState("");
  const [view, setView] = useState("grid");
  const [showAdd, setShowAdd] = useState(false);
  const [focus, setFocus] = useState(null);
  const [edit, setEdit] = useState(null);
  const [busy, setBusy] = useState(false);

  // Grid thumbnail (snapshot) ni davriy yangilash uchun
  const [tick, setTick] = useState(0);
  useEffect(() => {
    const id = setInterval(() => setTick((t) => t + 1), 6000);
    return () => clearInterval(id);
  }, []);

  const { data, loading, error, reload } = useApi(
    () => camerasApi.list({ groupId: group || undefined, status: statusF || undefined, search: search || undefined }),
    [group, statusF, search]
  );

  const cameras = data?.cameras || [];
  const groups = data?.groups || [];

  const del = async (c) => {
    if (!window.confirm(`${c.name} o'chirilsinmi?`)) return;
    setBusy(true); try { await camerasApi.remove(c.id); setFocus(null); reload(); } finally { setBusy(false); }
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Kameralar</h1>
          <div className="page-sub">{cameras.length} ta kamera · {cameras.filter((c) => c.status !== "Offline").length} faol</div>
        </div>
        <button className="btn primary" onClick={() => setShowAdd(true)}><Icon name="plus" size={14} /> Yangi kamera</button>
      </div>

      <form onSubmit={(e) => { e.preventDefault(); setSearch(qInput); }} className="card" style={{ padding: 12, marginBottom: 14, display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
        <div className="search" style={{ position: "relative", minWidth: 240 }}>
          <Icon name="search" size={14} />
          <input value={qInput} onChange={(e) => setQInput(e.target.value)} placeholder="Nom, IP bo'yicha izlash..." />
        </div>
        <select className="select" value={group} onChange={(e) => setGroup(e.target.value)}>
          <option value="">Barcha guruhlar</option>
          {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
        </select>
        <select className="select" value={statusF} onChange={(e) => setStatusF(e.target.value)}>
          <option value="">Barcha statuslar</option>
          <option value="Online">Faol</option>
          <option value="Offline">Oflayn</option>
          <option value="Recording">Yozuvda</option>
        </select>
        <button className="btn" type="submit"><Icon name="search" size={13} /></button>
        <div style={{ flex: 1 }} />
        <div className="seg">
          <button type="button" className={view === "grid" ? "on" : ""} onClick={() => setView("grid")}><Icon name="grid" size={12} /></button>
          <button type="button" className={view === "list" ? "on" : ""} onClick={() => setView("list")}><Icon name="list" size={12} /></button>
        </div>
      </form>

      {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : cameras.length === 0 ? <Empty label="Kamera topilmadi" icon="camera" /> : view === "grid" ? (
        <div className="cam-grid">
          {cameras.map((c) => (
            <div key={c.id} className="cam" onClick={() => setFocus(c)}>
              <CameraTile cam={toTile(c)} src={c.status === "Offline" ? undefined : `${camerasApi.snapshotUrl(c.id, 480)}&t=${tick}`} />
              <div className="cam-meta">
                <div>
                  <div className="cam-name">{c.name}</div>
                  <div className="cam-loc mono">{c.cameraCode} · {c.cameraGroup?.name || "Guruhsiz"}</div>
                </div>
                <StatusPill status={c.status} />
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="card">
          <table className="tbl">
            <thead><tr><th>Kamera</th><th>Kod / IP</th><th>Guruh</th><th>Turi</th><th>FPS</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {cameras.map((c) => (
                <tr key={c.id}>
                  <td><div className="row"><Icon name="camera" size={14} /><span style={{ fontWeight: 500 }}>{c.name}</span></div></td>
                  <td className="mono faint">{c.cameraCode} · {c.ipAddress || "—"}</td>
                  <td>{c.cameraGroup?.name || "—"}</td>
                  <td>{c.type === "Turnstile" ? "Turniket" : "Oddiy"}</td>
                  <td className="mono">{c.fps}</td>
                  <td><StatusPill status={c.status} /></td>
                  <td>
                    <div className="row" style={{ gap: 4 }}>
                      <button className="btn xs ghost" onClick={() => setFocus(c)}><Icon name="eye" size={12} /></button>
                      <button className="btn xs ghost" onClick={() => setEdit(c)}><Icon name="edit" size={12} /></button>
                      <button className="btn xs ghost" disabled={busy} onClick={() => del(c)}><Icon name="trash" size={12} /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Detail modal */}
      <Modal open={!!focus} onClose={() => setFocus(null)} wide title={focus ? `${focus.name} · ${focus.cameraCode}` : ""}
        footer={focus && <>
          <button className="btn danger" disabled={busy} onClick={() => del(focus)}><Icon name="trash" size={14} /> O'chirish</button>
          <button className="btn" onClick={() => { setEdit(focus); setFocus(null); }}><Icon name="edit" size={14} /> Tahrirlash</button>
          <button className="btn" onClick={() => setFocus(null)}>Yopish</button>
        </>}>
        {focus && (
          <div className="col" style={{ gap: 14 }}>
            <CameraTile cam={toTile(focus)} src={focus.status === "Offline" ? undefined : camerasApi.streamUrl(focus.id, 960)} />
            <div className="grid-3" style={{ gap: 10, fontSize: 12.5 }}>
              {[
                ["IP", focus.ipAddress || "—"], ["Port", focus.port], ["Guruh", focus.cameraGroup?.name || "—"],
                ["FPS", focus.fps], ["Sifat", focus.quality], ["Holat", focus.status],
                ["Turi", focus.type === "Turnstile" ? "Turniket" : "Oddiy"], ["Model", focus.cameraModel], ["AI", focus.faceRecognitionEnabled ? "Yoqilgan" : "O'chiq"],
              ].map(([k, v]) => (
                <div key={k}><div className="faint" style={{ fontSize: 10, textTransform: "uppercase", letterSpacing: ".06em" }}>{k}</div><div className="mono">{String(v)}</div></div>
              ))}
            </div>
          </div>
        )}
      </Modal>

      <AddCameraModal open={showAdd} groups={groups} onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); reload(); }} />
      {edit && <EditCameraModal key={edit.id} camera={edit} groups={groups} onClose={() => setEdit(null)} onSaved={() => { setEdit(null); reload(); }} />}
    </div>
  );
};

const AddCameraModal = ({ open, groups, onClose, onSaved }) => {
  const [f, setF] = useState({
    name: "", type: "Turnstile", protocol: "RTSP", cameraModel: "Hikvision", quality: "FullHD",
    streamUrl: "", ipAddress: "", port: 554, username: "", password: "", cameraGroupId: "", faceRecognitionEnabled: true,
  });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState(null);
  const [test, setTest] = useState(null);
  const [testing, setTesting] = useState(false);
  // Ulanishga oid maydon o'zgarsa — eski test natijasini tozalaymiz
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setTest(null); };

  const runTest = async () => {
    setTesting(true); setTest(null); setErr(null);
    try {
      const r = await camerasApi.testConnection({
        streamUrl: f.streamUrl || null,
        ipAddress: f.ipAddress || null,
        port: parseInt(f.port) || 554,
        username: f.username || null,
        password: f.password || null,
      });
      setTest(r);
    } catch (e) {
      setTest({ ok: false, message: e.message });
    } finally { setTesting(false); }
  };

  const save = async () => {
    setBusy(true); setErr(null);
    try {
      await camerasApi.create({
        ...f,
        port: parseInt(f.port) || 554,
        cameraGroupId: f.cameraGroupId ? parseInt(f.cameraGroupId) : null,
        streamUrl: f.streamUrl || null,
        ipAddress: f.ipAddress || null,
        username: f.username || null,
        password: f.password || null,
      });
      onSaved();
    } catch (e) { setErr(e.message); } finally { setBusy(false); }
  };

  return (
    <Modal open={open} onClose={onClose} title="Yangi kamera qo'shish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={busy || !f.name} onClick={save}><Icon name="check" size={14} /> {busy ? "..." : "Qo'shish"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        {err && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {err}</div>}
        <div className="grid-2">
          <Field label="Kamera nomi"><input className="input" value={f.name} onChange={set("name")} /></Field>
          <Field label="Guruh"><select className="select" value={f.cameraGroupId} onChange={set("cameraGroupId")}><option value="">— Guruhsiz —</option>{groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select></Field>
        </div>
        <div className="grid-2">
          <Field label="Turi"><select className="select" value={f.type} onChange={set("type")}><option value="Turnstile">Turniket</option><option value="Regular">Oddiy</option></select></Field>
          <Field label="Model"><select className="select" value={f.cameraModel} onChange={set("cameraModel")}><option>Hikvision</option><option>Dahua</option><option>Axis</option><option value="Other">Boshqa</option></select></Field>
        </div>
        <div className="grid-2">
          <Field label="IP manzil"><input className="input mono" value={f.ipAddress} onChange={set("ipAddress")} placeholder="192.168.1.100" /></Field>
          <Field label="Port"><input className="input mono" value={f.port} onChange={set("port")} /></Field>
        </div>
        <div className="grid-2">
          <Field label="Login"><input className="input mono" value={f.username} onChange={set("username")} /></Field>
          <Field label="Parol"><input className="input mono" type="password" value={f.password} onChange={set("password")} /></Field>
        </div>
        <Field label="Stream URL (ixtiyoriy)"><input className="input mono" value={f.streamUrl} onChange={set("streamUrl")} placeholder="rtsp://..." /></Field>
        <div className="col" style={{ gap: 8 }}>
          <button type="button" className="btn" disabled={testing || (!f.streamUrl && !f.ipAddress)} onClick={runTest} style={{ alignSelf: "flex-start" }}>
            <Icon name="refresh" size={14} /> {testing ? "Tekshirilmoqda..." : "Test ulanib ko'rish"}
          </button>
          {test && (
            <div className="row" style={{ gap: 8, fontSize: 13, color: test.ok ? "var(--accent)" : "var(--danger)" }}>
              <Icon name={test.ok ? "check" : "alert"} size={14} />
              <span>{test.message}{test.elapsedMs ? ` · ${test.elapsedMs} ms` : ""}</span>
            </div>
          )}
        </div>
        <div className="grid-2">
          <Field label="Sifat"><select className="select" value={f.quality} onChange={set("quality")}><option value="UHD4K">4K</option><option value="FullHD">Full HD</option><option value="HD">HD</option></select></Field>
          <Field label="Protokol"><select className="select" value={f.protocol} onChange={set("protocol")}><option>RTSP</option><option>ONVIF</option><option value="HTTP">HTTP</option><option>RTMP</option></select></Field>
        </div>
        <label className="check"><input type="checkbox" checked={f.faceRecognitionEnabled} onChange={(e) => setF({ ...f, faceRecognitionEnabled: e.target.checked })} /> Yuzni tanishni yoqish</label>
      </div>
    </Modal>
  );
};

const EditCameraModal = ({ camera, groups, onClose, onSaved }) => {
  const [f, setF] = useState({
    cameraCode: camera.cameraCode || "",
    name: camera.name || "",
    type: camera.type || "Turnstile",
    cameraModel: camera.cameraModel || "Hikvision",
    quality: camera.quality || "FullHD",
    protocol: camera.protocol || "RTSP",
    status: camera.status || "Online",
    streamUrl: camera.streamUrl || "",
    aiStreamUrl: camera.aiStreamUrl || "",
    ipAddress: camera.ipAddress || "",
    port: camera.port ?? 554,
    username: camera.username || "",
    password: "",
    fps: camera.fps ?? 25,
    cameraGroupId: camera.cameraGroupId ? String(camera.cameraGroupId) : "",
    faceRecognitionEnabled: !!camera.faceRecognitionEnabled,
    continuousRecording: !!camera.continuousRecording,
    motionDetection: !!camera.motionDetection,
  });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState(null);
  const [test, setTest] = useState(null);
  const [testing, setTesting] = useState(false);
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setTest(null); };
  const setChk = (k) => (e) => setF({ ...f, [k]: e.target.checked });

  const runTest = async () => {
    setTesting(true); setTest(null); setErr(null);
    try {
      const r = await camerasApi.testConnection({
        streamUrl: f.streamUrl || null,
        aiStreamUrl: f.aiStreamUrl || null,
        ipAddress: f.ipAddress || null,
        port: parseInt(f.port) || 554,
        username: f.username || null,
        password: f.password || null,
      });
      setTest(r);
    } catch (e) {
      setTest({ ok: false, message: e.message });
    } finally { setTesting(false); }
  };

  const save = async () => {
    setBusy(true); setErr(null);
    try {
      await camerasApi.update(camera.id, {
        cameraCode: f.cameraCode,
        name: f.name,
        type: f.type,
        cameraModel: f.cameraModel,
        quality: f.quality,
        protocol: f.protocol,
        status: f.status,
        streamUrl: f.streamUrl || null,
        aiStreamUrl: f.aiStreamUrl || null,
        ipAddress: f.ipAddress || null,
        port: parseInt(f.port) || 554,
        username: f.username || null,
        password: f.password || null,
        fps: parseInt(f.fps) || 25,
        cameraGroupId: f.cameraGroupId ? parseInt(f.cameraGroupId) : null,
        faceRecognitionEnabled: f.faceRecognitionEnabled,
        continuousRecording: f.continuousRecording,
        motionDetection: f.motionDetection,
      });
      onSaved();
    } catch (e) { setErr(e.message); } finally { setBusy(false); }
  };

  return (
    <Modal open onClose={onClose} title={`Kamerani tahrirlash · ${camera.cameraCode}`}
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={busy || !f.name || !f.cameraCode} onClick={save}><Icon name="check" size={14} /> {busy ? "..." : "Saqlash"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        {err && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {err}</div>}
        <div className="grid-2">
          <Field label="Kamera nomi"><input className="input" value={f.name} onChange={set("name")} /></Field>
          <Field label="Kamera kodi"><input className="input mono" value={f.cameraCode} onChange={set("cameraCode")} /></Field>
        </div>
        <div className="grid-2">
          <Field label="Guruh"><select className="select" value={f.cameraGroupId} onChange={set("cameraGroupId")}><option value="">— Guruhsiz —</option>{groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select></Field>
          <Field label="Holat"><select className="select" value={f.status} onChange={set("status")}><option value="Online">Faol</option><option value="Offline">Oflayn</option><option value="Recording">Yozuvda</option></select></Field>
        </div>
        <div className="grid-2">
          <Field label="Turi"><select className="select" value={f.type} onChange={set("type")}><option value="Turnstile">Turniket</option><option value="Regular">Oddiy</option></select></Field>
          <Field label="Model"><select className="select" value={f.cameraModel} onChange={set("cameraModel")}><option>Hikvision</option><option>Dahua</option><option>Axis</option><option value="Other">Boshqa</option></select></Field>
        </div>
        <div className="grid-2">
          <Field label="IP manzil"><input className="input mono" value={f.ipAddress} onChange={set("ipAddress")} placeholder="192.168.1.100" /></Field>
          <Field label="Port"><input className="input mono" value={f.port} onChange={set("port")} /></Field>
        </div>
        <div className="grid-2">
          <Field label="Login"><input className="input mono" value={f.username} onChange={set("username")} /></Field>
          <Field label="Parol" hint="Bo'sh qoldirilsa — eski parol saqlanadi"><input className="input mono" type="password" value={f.password} onChange={set("password")} placeholder="••••••" /></Field>
        </div>
        <Field label="Stream URL (main — yozib olish uchun)"><input className="input mono" value={f.streamUrl} onChange={set("streamUrl")} placeholder="rtsp://..." /></Field>
        <Field label="AI Stream URL (sub-stream — yuz tanish uchun)"><input className="input mono" value={f.aiStreamUrl} onChange={set("aiStreamUrl")} placeholder="rtsp://.../102" /></Field>
        <div className="col" style={{ gap: 8 }}>
          <button type="button" className="btn" disabled={testing || (!f.streamUrl && !f.ipAddress)} onClick={runTest} style={{ alignSelf: "flex-start" }}>
            <Icon name="refresh" size={14} /> {testing ? "Tekshirilmoqda..." : "Test ulanib ko'rish"}
          </button>
          {test && (
            <div className="row" style={{ gap: 8, fontSize: 13, color: test.ok ? "var(--accent)" : "var(--danger)" }}>
              <Icon name={test.ok ? "check" : "alert"} size={14} />
              <span>{test.message}{test.elapsedMs ? ` · ${test.elapsedMs} ms` : ""}</span>
            </div>
          )}
        </div>
        <div className="grid-3">
          <Field label="Sifat"><select className="select" value={f.quality} onChange={set("quality")}><option value="UHD4K">4K</option><option value="FullHD">Full HD</option><option value="HD">HD</option></select></Field>
          <Field label="Protokol"><select className="select" value={f.protocol} onChange={set("protocol")}><option>RTSP</option><option>ONVIF</option><option value="HTTP">HTTP</option><option>RTMP</option></select></Field>
          <Field label="FPS"><input className="input mono" value={f.fps} onChange={set("fps")} /></Field>
        </div>
        <div className="col" style={{ gap: 10 }}>
          <label className="check"><input type="checkbox" checked={f.faceRecognitionEnabled} onChange={setChk("faceRecognitionEnabled")} /> Yuzni tanishni yoqish</label>
          <label className="check"><input type="checkbox" checked={f.continuousRecording} onChange={setChk("continuousRecording")} /> Uzluksiz yozib olish</label>
          <label className="check"><input type="checkbox" checked={f.motionDetection} onChange={setChk("motionDetection")} /> Harakatni aniqlash</label>
        </div>
      </div>
    </Modal>
  );
};

export default CamerasScreen;
