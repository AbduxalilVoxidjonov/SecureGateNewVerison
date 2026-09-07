// Cameras / Kameralar — real API + SignalR (camera hubi: yuz qutilari)
import { useState, useEffect, useRef, useMemo, useCallback, memo } from "react";
import { Icon } from "../components/Icon";
import { CameraTile, CameraStage, StatusPill, HubPill, Modal, Field, Toast } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { useHubEvent } from "../hooks/useHub";
import useMutation from "../hooks/useMutation";
import { camerasApi } from "../api/endpoints";
import { dropStaleFaces, faceFromEvent, mergeFace, syncFaces, useReloadOnReconnect } from "./live";

// --- Jonli oqim byudjeti --------------------------------------------------------
// KRITIK: brauzer HTTP/1.1 da bitta origin uchun ~6 ta parallel ulanish beradi va
// har bir MJPEG oqimi (<img src=".../stream">) shu ulanishlardan BITTASINI
// uzluksiz band qiladi — oqim hech qachon tugamaydi. Agar 12 kamerali gridda
// hamma plitka jonli bo'lsa, hovuz to'lib qoladi va API so'rovlari (hatto
// snapshot ham) navbatda muzlab qoladi. Docker'da TLS yo'q, TLS'siz brauzer h2c
// (HTTP/2 cleartext) ishlatmaydi — ya'ni HTTP/2 multiplexing bu yerda yordam bermaydi.
//
// Shuning uchun ikki chegara qo'yilgan:
//   1) bir vaqtda ko'pi bilan MAX_LIVE_TILES ta plitka jonli oqimda bo'ladi;
//   2) jonli bo'lish huquqini faqat EKRANDA KO'RINIB turgan plitkalar oladi
//      (IntersectionObserver) — ekrandan chiqqani snapshot rejimiga qaytadi va
//      <img> DOM'dan olib tashlanib, oqim ulanishi darhol bo'shatiladi.
// Qolgan plitkalar snapshot rejimida: qisqa muddatli so'rov, ulanishni band qilmaydi.
const MAX_LIVE_TILES = 4;

// Grid thumbnail (snapshot) yangilanish davri — jonli oqim yo'q plitkalar uchun.
const SNAPSHOT_MS = 3000;

// Grid plitkasi uchun oqim/snapshot kengligi (backend `w` parametri).
const TILE_WIDTH = 480;

// API kamera -> CameraTile kutadigan shaklga moslash.
// `faces` — SignalR (FaceDetected) dan kelgan, foizga normalizatsiya qilingan qutilar.
const toTile = (c, faces) => ({
  code: c.cameraCode,
  ip: c.ipAddress || "—",
  status: c.status === "Offline" ? "offline" : "live",
  fps: c.fps,
  faces: faces || [],
});

// Backend'da StreamUrl / AiStreamUrl / IpAddress uchun [RegularExpression] bor:
// `null` validatsiyadan o'tadi, ammo `""` (va `" "`) 400 beradi.
// Shuning uchun: satrni trim qilamiz va bo'sh bo'lsa maydonni payload'ga UMUMAN qo'shmaymiz.
const trimStr = (v) => (typeof v === "string" ? v.trim() : v);
const filled = (v) => !!trimStr(v);
// Bo'sh bo'lmasa qo'shadi, bo'sh bo'lsa maydonni tashlab ketadi (hech qachon "" yubormaydi).
const setIfFilled = (obj, key, v) => { const t = trimStr(v); if (t) obj[key] = t; return obj; };
// Prefill qilingan maydon (masalan IP) ataylab tozalansa — null yuboriladi (null regex'dan o'tadi).
const orNull = (v) => trimStr(v) || null;

// --- NVR kanali -----------------------------------------------------------------
const MIN_CHANNEL = 1;
const MAX_CHANNEL = 64;

// Kanal raqami faqat haqiqiy son bo'lsa (va 1..64 oralig'ida) qabul qilinadi.
const parseChannel = (v) => {
  const n = parseInt(v, 10);
  return Number.isFinite(n) && n >= MIN_CHANNEL && n <= MAX_CHANNEL ? n : null;
};

// Forma yuborishga tayyormi (NVR kanali tanlangan bo'lsa kanal raqami majburiy).
const nvrValid = (f) => f.deviceKind !== "NvrChannel" || parseChannel(f.channelNumber) !== null;

// "NVR · kanal 3" / "IP-kamera"
const deviceLabel = (c) => {
  if (c?.deviceKind !== "NvrChannel") return "IP-kamera";
  const ch = parseChannel(c.channelNumber);
  return ch ? `NVR · kanal ${ch}` : "NVR kanali";
};

const NVR_HINT = "NVR kanali uchun to'liq RTSP URL kiritish shart emas — IP manzil, port va kanal raqami yetarli, RTSP havolasini backend Hikvision shabloni bo'yicha o'zi quradi.";

const CamerasScreen = () => {
  const [group, setGroup] = useState("");
  const [statusF, setStatusF] = useState("");
  const [qInput, setQInput] = useState("");
  const [search, setSearch] = useState("");
  const [view, setView] = useState("grid");
  const [showAdd, setShowAdd] = useState(false);
  const [focus, setFocus] = useState(null);
  const [edit, setEdit] = useState(null);
  // Oqim to'liq ekrandami — `Escape` konflikti uchun (pastda izoh).
  // CameraStage unmount bo'lganda (modal yopilganda) o'zi `false` qaytaradi.
  const [camFull, setCamFull] = useState(false);

  // Snapshot rejimidagi plitkalarni davriy yangilash uchun (jonli oqimga tegmaydi).
  const [tick, setTick] = useState(0);
  useEffect(() => {
    if (view !== "grid") return undefined; // ro'yxat ko'rinishida rasm umuman yo'q
    const id = setInterval(() => setTick((t) => t + 1), SNAPSHOT_MS);
    return () => clearInterval(id);
  }, [view]);

  const { data, loading, error, reload } = useApi(
    () => camerasApi.list({ groupId: group || undefined, status: statusF || undefined, search: search || undefined }),
    [group, statusF, search]
  );

  const cameras = useMemo(() => data?.cameras || [], [data]);
  const groups = data?.groups || [];

  // Uzilishdan keyin qayta ulanganda ro'yxatni bir marta qayta o'qiymiz
  // (uzilish paytida o'zgargan statuslar yo'qolmasin).
  const hubStatus = useReloadOnReconnect("camera", reload);

  // --- Yuz qutilari (SignalR) ---------------------------------------------------
  // { [cameraId]: [ {key,x,y,w,h,name,conf,unknown,at}, ... ] } — foizda.
  const [faces, setFaces] = useState({});

  useHubEvent("camera", "FaceDetected", (p) => {
    const f = faceFromEvent(p);
    if (!f || p?.cameraId == null) return;
    setFaces((prev) => ({ ...prev, [p.cameraId]: mergeFace(prev[p.cameraId], f) }));
  });

  // Kadr oxirida — hozir mavjud bo'lmagan qutilar TTL ni kutmasdan o'chadi.
  useHubEvent("camera", "FaceFrameProcessed", (p) => {
    if (p?.cameraId == null) return;
    setFaces((prev) => {
      const cur = prev[p.cameraId];
      if (!cur || cur.length === 0) return prev;
      const next = syncFaces(cur, p.activeKeys);
      if (next === cur) return prev;
      const out = { ...prev };
      if (next.length) out[p.cameraId] = next;
      else delete out[p.cameraId];
      return out;
    });
  });

  // Zaxira tozalash: FaceFrameProcessed yetib kelmasa (uzilish, worker to'xtashi)
  // qutilar ekranda muzlab qolmasligi uchun ~2.5 s dan keyin o'chadi.
  useEffect(() => {
    const id = setInterval(() => setFaces((prev) => dropStaleFaces(prev)), 1000);
    return () => clearInterval(id);
  }, []);

  // --- Qaysi plitka jonli oqimda? ------------------------------------------------
  const [visibleIds, setVisibleIds] = useState(() => new Set());

  const setTileVisible = useCallback((id, isVisible) => {
    setVisibleIds((prev) => {
      if (prev.has(id) === isVisible) return prev;
      const next = new Set(prev);
      if (isVisible) next.add(id); else next.delete(id);
      return next;
    });
  }, []);

  const modalOpen = !!focus;
  const liveIds = useMemo(() => {
    const out = new Set();
    if (view !== "grid") return out;
    // Detal modali ochiq bo'lsa, CameraStage bitta ulanishni o'zi band qiladi —
    // grid uchun byudjetni bittaga kamaytiramiz.
    const budget = modalOpen ? MAX_LIVE_TILES - 1 : MAX_LIVE_TILES;
    for (const c of cameras) {
      if (out.size >= budget) break;
      if (c.status === "Offline") continue;
      if (visibleIds.has(c.id)) out.add(c.id);
    }
    return out;
  }, [cameras, visibleIds, view, modalOpen]);

  const remove = useMutation(
    (id) => camerasApi.remove(id),
    { onSuccess: () => { setFocus(null); reload(); } }
  );
  const busy = remove.busy;

  const del = (c) => {
    if (!window.confirm(`${c.name} o'chirilsinmi?`)) return;
    remove.run(c.id);
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Kameralar</h1>
          <div className="page-sub">
            {cameras.length} ta kamera · {cameras.filter((c) => c.status !== "Offline").length} faol
            {view === "grid" && ` · ${liveIds.size}/${MAX_LIVE_TILES} jonli oqim`}
          </div>
        </div>
        <div className="row">
          <HubPill status={hubStatus} title="Yuz aniqlash hodisalari (camera hub)" />
          <button className="btn primary" onClick={() => setShowAdd(true)}><Icon name="plus" size={14} /> Yangi kamera</button>
        </div>
      </div>

      {remove.error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorBox error={remove.error} onRetry={remove.reset} />
        </div>
      )}

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
            <CameraGridTile key={c.id} cam={c} faces={faces[c.id]} live={liveIds.has(c.id)}
                            tick={tick} onVisible={setTileVisible} onOpen={setFocus} />
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
                  <td>{c.groupName || c.cameraGroup?.name || "—"}</td>
                  <td>
                    <div>{c.type === "Turnstile" ? "Turniket" : "Oddiy"}</div>
                    <div className="faint" style={{ fontSize: 11 }}>{deviceLabel(c)}</div>
                  </td>
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
      {/* To'liq ekran faol bo'lganda `Escape` faqat to'liq ekrandan chiqarsin —
          modal yopilib ketmasin (`escDisabled` — Modal'ning ixtiyoriy yangi propi). */}
      <Modal open={!!focus} onClose={() => setFocus(null)} wide escDisabled={camFull}
        title={focus ? `${focus.name} · ${focus.cameraCode}` : ""}
        footer={focus && <>
          <button className="btn danger" disabled={busy} onClick={() => del(focus)}><Icon name="trash" size={14} /> O'chirish</button>
          <button className="btn" onClick={() => { setEdit(focus); setFocus(null); }}><Icon name="edit" size={14} /> Tahrirlash</button>
          <button className="btn" onClick={() => setFocus(null)}>Yopish</button>
        </>}>
        {focus && (
          <div className="col" style={{ gap: 14 }}>
            <CameraStage
              cam={toTile(focus, faces[focus.id])}
              offline={focus.status === "Offline"}
              label={`${focus.name} · ${focus.cameraCode}`}
              baseWidth={960}
              srcFor={(w) => camerasApi.streamUrl(focus.id, w)}
              onFullscreenChange={setCamFull}
            />
            <div className="grid-3" style={{ gap: 10, fontSize: 12.5 }}>
              {[
                ["IP", focus.ipAddress || "—"], ["Port", focus.port], ["Guruh", focus.groupName || focus.cameraGroup?.name || "—"],
                ["FPS", focus.fps], ["Sifat", focus.quality], ["Holat", focus.status],
                ["Turi", focus.type === "Turnstile" ? "Turniket" : "Oddiy"], ["Model", focus.cameraModel], ["AI", focus.faceRecognitionEnabled ? "Yoqilgan" : "O'chiq"],
                ["Qurilma", deviceLabel(focus)],
                ["O'lcham", focus.resolution || "—"], ["Login/parol", focus.hasCredentials === undefined ? "—" : focus.hasCredentials ? "O'rnatilgan" : "O'rnatilmagan"],
              ].map(([k, v]) => (
                <div key={k}><div className="faint" style={{ fontSize: 10, textTransform: "uppercase", letterSpacing: ".06em" }}>{k}</div><div className="mono">{String(v)}</div></div>
              ))}
            </div>
          </div>
        )}
      </Modal>

      {showAdd && <AddCameraModal groups={groups} onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); reload(); }} />}
      {edit && <EditCameraModal key={edit.id} camera={edit} groups={groups} onClose={() => setEdit(null)} onSaved={() => { setEdit(null); reload(); }} />}

      <Toast message={remove.error?.message} kind="error" onClose={remove.reset} />
    </div>
  );
};

// Grid plitkasi.
//   `live`  — ota-komponent bergan ruxsat: bu plitka jonli MJPEG oqimida.
//   `tick`  — snapshot rejimidagi kadrni yangilash uchun hisoblagich.
// Ko'rinish holati IntersectionObserver bilan kuzatiladi: ekrandan chiqqan plitka
// ota-komponentga xabar beradi (jonli byudjet boshqasiga o'tadi) va o'zi ham
// snapshot so'ramay qo'yadi. Plitka jonlidan snapshot rejimiga o'tganda
// `CameraTile` <img> elementini qayta yaratadi — eski MJPEG ulanishi uziladi.
// `memo` — FaceDetected sekundiga bir necha marta keladi: faqat qutisi
// o'zgargan plitka qayta chiziladi (qolgan proplar barqaror).
const CameraGridTile = memo(({ cam, faces, live, tick, onVisible, onOpen }) => {
  const hostRef = useRef(null);
  const [onScreen, setOnScreen] = useState(true);

  useEffect(() => {
    const node = hostRef.current;
    const id = cam.id;
    if (!node || typeof IntersectionObserver === "undefined") {
      // Eski brauzer — hamma plitka "ko'rinadi" deb hisoblanadi (byudjet baribir cheklaydi).
      onVisible(id, true);
      return () => onVisible(id, false);
    }
    const obs = new IntersectionObserver(
      (entries) => {
        for (const e of entries) {
          setOnScreen(e.isIntersecting);
          onVisible(id, e.isIntersecting);
        }
      },
      // Biroz oldinroq boshlansin — skroll paytida bo'sh plitka ko'rinmasin.
      { rootMargin: "150px 0px", threshold: 0.01 }
    );
    obs.observe(node);
    return () => { obs.disconnect(); onVisible(id, false); };
  }, [cam.id, onVisible]);

  const offline = cam.status === "Offline";
  const streaming = live && !offline;
  const src = offline || !onScreen
    ? undefined
    : streaming
      ? camerasApi.streamUrl(cam.id, TILE_WIDTH)
      : `${camerasApi.snapshotUrl(cam.id, TILE_WIDTH)}&t=${tick}`;

  return (
    <div ref={hostRef} className="cam" onClick={() => onOpen(cam)}>
      <CameraTile cam={toTile(cam, faces)} src={src} streaming={streaming} />
      <div className="cam-meta">
        <div>
          <div className="cam-name">{cam.name}</div>
          <div className="cam-loc mono">{cam.cameraCode} · {cam.groupName || cam.cameraGroup?.name || "Guruhsiz"}</div>
        </div>
        <StatusPill status={cam.status} />
      </div>
    </div>
  );
});
CameraGridTile.displayName = "CameraGridTile";

// Shartli render — modal har ochilganda toza mount bo'ladi (RTSP paroli eskisidan qolmaydi).
const AddCameraModal = ({ groups, onClose, onSaved }) => {
  const [f, setF] = useState({
    name: "", type: "Turnstile", protocol: "RTSP", cameraModel: "Hikvision", quality: "FullHD",
    streamUrl: "", ipAddress: "", port: 554, username: "", password: "", cameraGroupId: "", faceRecognitionEnabled: true,
    deviceKind: "Camera", channelNumber: "",
  });
  const [test, setTest] = useState(null);
  // Ulanishga oid maydon o'zgarsa — eski test natijasini tozalaymiz
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setTest(null); };

  const create = useMutation((body) => camerasApi.create(body), { onSuccess: () => onSaved() });

  // Test natijasi (muvaffaqiyat ham, xato ham) shu yerda ko'rsatiladi.
  const conn = useMutation(
    (body) => camerasApi.testConnection(body),
    {
      onSuccess: (r) => setTest(r || { ok: true, message: "Ulanish muvaffaqiyatli." }),
      onError: (e) => setTest({ ok: false, message: e.message }),
    }
  );

  const runTest = () => {
    setTest(null); create.reset();
    conn.run({
      streamUrl: orNull(f.streamUrl),
      ipAddress: orNull(f.ipAddress),
      port: parseInt(f.port) || 554,
      username: orNull(f.username),
      password: orNull(f.password),
    });
  };

  const save = () => {
    const body = {
      name: trimStr(f.name),
      type: f.type,
      protocol: f.protocol,
      cameraModel: f.cameraModel,
      quality: f.quality,
      port: parseInt(f.port) || 554,
      cameraGroupId: f.cameraGroupId ? parseInt(f.cameraGroupId) : null,
      faceRecognitionEnabled: f.faceRecognitionEnabled,
      deviceKind: f.deviceKind,
    };
    // Kanal raqami faqat NVR kanali uchun va faqat haqiqiy son bo'lsa yuboriladi
    // (bo'sh satr / NaN umuman qo'shilmaydi).
    const ch = f.deviceKind === "NvrChannel" ? parseChannel(f.channelNumber) : null;
    if (ch !== null) body.channelNumber = ch;
    // "" hech qachon yuborilmaydi — [RegularExpression] uni rad etadi.
    setIfFilled(body, "streamUrl", f.streamUrl);
    setIfFilled(body, "ipAddress", f.ipAddress);
    setIfFilled(body, "username", f.username);
    setIfFilled(body, "password", f.password);
    create.run(body);
  };

  return (
    <Modal open onClose={onClose} title="Yangi kamera qo'shish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={create.busy || !filled(f.name) || !nvrValid(f)} onClick={save}><Icon name="check" size={14} /> {create.busy ? "..." : "Qo'shish"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        {create.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {create.error.message}</div>}
        <div className="grid-2">
          <Field label="Kamera nomi"><input className="input" value={f.name} onChange={set("name")} /></Field>
          <Field label="Guruh"><select className="select" value={f.cameraGroupId} onChange={set("cameraGroupId")}><option value="">— Guruhsiz —</option>{groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select></Field>
        </div>
        <div className="grid-2">
          <Field label="Turi"><select className="select" value={f.type} onChange={set("type")}><option value="Turnstile">Turniket</option><option value="Regular">Oddiy</option></select></Field>
          <Field label="Model"><select className="select" value={f.cameraModel} onChange={set("cameraModel")}><option>Hikvision</option><option>Dahua</option><option>Axis</option><option value="Other">Boshqa</option></select></Field>
        </div>
        <div className="grid-2">
          <Field label="Qurilma turi">
            <select className="select" value={f.deviceKind} onChange={set("deviceKind")}>
              <option value="Camera">IP-kamera</option>
              <option value="NvrChannel">NVR kanali</option>
            </select>
          </Field>
          {f.deviceKind === "NvrChannel" && (
            <Field label="Kanal raqami" hint={`${MIN_CHANNEL}..${MAX_CHANNEL}`}>
              <input className="input mono" type="number" min={MIN_CHANNEL} max={MAX_CHANNEL} value={f.channelNumber} onChange={set("channelNumber")} placeholder="1" />
            </Field>
          )}
        </div>
        {f.deviceKind === "NvrChannel" && (
          <div className="faint" style={{ fontSize: 11.5 }}>{NVR_HINT}</div>
        )}
        <div className="grid-2">
          <Field label={f.deviceKind === "NvrChannel" ? "NVR IP manzili" : "IP manzil"}><input className="input mono" value={f.ipAddress} onChange={set("ipAddress")} placeholder="192.168.1.100" /></Field>
          <Field label="Port"><input className="input mono" value={f.port} onChange={set("port")} /></Field>
        </div>
        <div className="grid-2">
          <Field label="Login"><input className="input mono" autoComplete="off" value={f.username} onChange={set("username")} /></Field>
          <Field label="Parol"><input className="input mono" type="password" autoComplete="new-password" value={f.password} onChange={set("password")} /></Field>
        </div>
        <Field label="Stream URL (ixtiyoriy)"><input className="input mono" value={f.streamUrl} onChange={set("streamUrl")} placeholder="rtsp://..." /></Field>
        <div className="col" style={{ gap: 8 }}>
          <button type="button" className="btn" disabled={conn.busy || (!filled(f.streamUrl) && !filled(f.ipAddress))} onClick={runTest} style={{ alignSelf: "flex-start" }}>
            <Icon name="refresh" size={14} /> {conn.busy ? "Tekshirilmoqda..." : "Test ulanib ko'rish"}
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

// MUHIM: backend CameraResponseDto endi maxfiy maydonlarni QAYTARMAYDI
// (password, username, streamUrl, aiStreamUrl). Shuning uchun bu maydonlar bo'sh boshlanadi
// va faqat foydalanuvchi qiymat kiritsa payload'ga qo'shiladi — bo'sh qoldirilsa server eskisini saqlaydi.
const EditCameraModal = ({ camera, groups, onClose, onSaved }) => {
  const [f, setF] = useState({
    cameraCode: camera.cameraCode || "",
    name: camera.name || "",
    type: camera.type || "Turnstile",
    cameraModel: camera.cameraModel || "Hikvision",
    quality: camera.quality || "FullHD",
    protocol: camera.protocol || "RTSP",
    status: camera.status || "Online",
    streamUrl: "",
    aiStreamUrl: "",
    deviceKind: camera.deviceKind === "NvrChannel" ? "NvrChannel" : "Camera",
    channelNumber: camera.channelNumber == null ? "" : String(camera.channelNumber),
    ipAddress: camera.ipAddress || "",
    port: camera.port ?? 554,
    username: "",
    password: "",
    fps: camera.fps ?? 25,
    cameraGroupId: camera.cameraGroupId ? String(camera.cameraGroupId) : "",
    faceRecognitionEnabled: !!camera.faceRecognitionEnabled,
    continuousRecording: !!camera.continuousRecording,
    motionDetection: !!camera.motionDetection,
  });
  const [test, setTest] = useState(null);
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setTest(null); };
  const setChk = (k) => (e) => setF({ ...f, [k]: e.target.checked });

  const update = useMutation((body) => camerasApi.update(camera.id, body), { onSuccess: () => onSaved() });

  // Test natijasi (muvaffaqiyat ham, xato ham) shu yerda ko'rsatiladi.
  const conn = useMutation(
    (body) => camerasApi.testConnection(body),
    {
      onSuccess: (r) => setTest(r || { ok: true, message: "Ulanish muvaffaqiyatli." }),
      onError: (e) => setTest({ ok: false, message: e.message }),
    }
  );

  const runTest = () => {
    setTest(null); update.reset();
    conn.run({
      streamUrl: orNull(f.streamUrl),
      aiStreamUrl: orNull(f.aiStreamUrl),
      ipAddress: orNull(f.ipAddress),
      port: parseInt(f.port) || 554,
      username: orNull(f.username),
      password: orNull(f.password),
    });
  };

  const save = () => {
    const body = {
      cameraCode: trimStr(f.cameraCode),
      name: trimStr(f.name),
      type: f.type,
      cameraModel: f.cameraModel,
      quality: f.quality,
      protocol: f.protocol,
      status: f.status,
      // IP DTO'da qaytadi va formada oldindan to'ldirilgan — ataylab tozalansa null bilan o'chiriladi.
      ipAddress: orNull(f.ipAddress),
      port: parseInt(f.port) || 554,
      fps: parseInt(f.fps) || 25,
      cameraGroupId: f.cameraGroupId ? parseInt(f.cameraGroupId) : null,
      faceRecognitionEnabled: f.faceRecognitionEnabled,
      continuousRecording: f.continuousRecording,
      motionDetection: f.motionDetection,
      deviceKind: f.deviceKind,
      // NVR kanali -> haqiqiy son; oddiy kameraga qaytarilsa -> null (eski kanal tozalanadi).
      // Hech qachon "" yuborilmaydi.
      channelNumber: f.deviceKind === "NvrChannel" ? parseChannel(f.channelNumber) : null,
    };
    // Maxfiy/qaytarilmaydigan maydonlar: faqat to'ldirilgan bo'lsa yuboriladi.
    // trim() SHART: " " truthy, lekin [RegularExpression] uni rad etib 400 qaytaradi.
    setIfFilled(body, "streamUrl", f.streamUrl);
    setIfFilled(body, "aiStreamUrl", f.aiStreamUrl);
    setIfFilled(body, "username", f.username);
    setIfFilled(body, "password", f.password);
    update.run(body);
  };

  const keepHint = "Bo'sh qoldirilsa — eski qiymat saqlanadi";

  return (
    <Modal open onClose={onClose} title={`Kamerani tahrirlash · ${camera.cameraCode}`}
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={update.busy || !filled(f.name) || !filled(f.cameraCode) || !nvrValid(f)} onClick={save}><Icon name="check" size={14} /> {update.busy ? "..." : "Saqlash"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        {update.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {update.error.message}</div>}
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
          <Field label="Qurilma turi">
            <select className="select" value={f.deviceKind} onChange={set("deviceKind")}>
              <option value="Camera">IP-kamera</option>
              <option value="NvrChannel">NVR kanali</option>
            </select>
          </Field>
          {f.deviceKind === "NvrChannel" && (
            <Field label="Kanal raqami" hint={`${MIN_CHANNEL}..${MAX_CHANNEL}`}>
              <input className="input mono" type="number" min={MIN_CHANNEL} max={MAX_CHANNEL} value={f.channelNumber} onChange={set("channelNumber")} placeholder="1" />
            </Field>
          )}
        </div>
        {f.deviceKind === "NvrChannel" && (
          <div className="faint" style={{ fontSize: 11.5 }}>{NVR_HINT}</div>
        )}
        <div className="grid-2">
          <Field label={f.deviceKind === "NvrChannel" ? "NVR IP manzili" : "IP manzil"}><input className="input mono" value={f.ipAddress} onChange={set("ipAddress")} placeholder="192.168.1.100" /></Field>
          <Field label="Port"><input className="input mono" value={f.port} onChange={set("port")} /></Field>
        </div>
        <div className="grid-2">
          <Field label="Login" hint={keepHint}>
            <div className="col" style={{ gap: 4 }}>
              <input className="input mono" autoComplete="off" value={f.username} onChange={set("username")} placeholder="••••••" />
              {/* CameraResponseDto.hasCredentials — login/parol o'rnatilganmi (qiymatlarning o'zi qaytarilmaydi). */}
              {camera.hasCredentials !== undefined && (
                <span className={`pill ${camera.hasCredentials ? "on" : "off"}`} style={{ alignSelf: "flex-start" }}>
                  {camera.hasCredentials ? "O'rnatilgan" : "O'rnatilmagan"}
                </span>
              )}
            </div>
          </Field>
          <Field label="Parol" hint={keepHint}><input className="input mono" type="password" autoComplete="new-password" value={f.password} onChange={set("password")} placeholder="••••••" /></Field>
        </div>
        <Field label="Stream URL (main — yozib olish uchun)" hint={keepHint}><input className="input mono" value={f.streamUrl} onChange={set("streamUrl")} placeholder="rtsp://..." /></Field>
        <Field label="AI Stream URL (sub-stream — yuz tanish uchun)" hint={keepHint}><input className="input mono" value={f.aiStreamUrl} onChange={set("aiStreamUrl")} placeholder="rtsp://.../102" /></Field>
        <div className="col" style={{ gap: 8 }}>
          <button type="button" className="btn" disabled={conn.busy || (!filled(f.streamUrl) && !filled(f.ipAddress))} onClick={runTest} style={{ alignSelf: "flex-start" }}>
            <Icon name="refresh" size={14} /> {conn.busy ? "Tekshirilmoqda..." : "Test ulanib ko'rish"}
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
