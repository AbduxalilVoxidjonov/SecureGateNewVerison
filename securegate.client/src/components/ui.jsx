// Shared UI primitives
import { useState, useEffect, useRef, useCallback } from "react";
import { createPortal } from "react-dom";
import { Icon } from "./Icon";
import { useClock } from "./useClock";

export const Avatar = ({ name, size = "md" }) => {
  const initials = (name || "??").split(" ").filter(Boolean).slice(0, 2).map(w => w[0]).join("").toUpperCase();
  // Stable hue from name
  let hue = 0;
  for (let i = 0; i < (name || "").length; i++) hue = (hue + name.charCodeAt(i) * 7) % 360;
  const cls = size === "sm" ? "av sm" : size === "lg" ? "av lg" : "av";
  return (
    <span className={cls} style={{ background: `linear-gradient(135deg, oklch(0.42 0.11 ${hue}), oklch(0.55 0.13 ${(hue + 40) % 360}))` }}>
      {initials}
    </span>
  );
};

export const StatusPill = ({ status }) => {
  const map = {
    // Mock / generic
    live: { cls: "on", label: "Faol" },
    open: { cls: "on", label: "Ochiq" },
    active: { cls: "on", label: "Faol" },
    offline: { cls: "off", label: "Offline" },
    warn: { cls: "warn", label: "Xatolik" },
    maintenance: { cls: "warn", label: "Xizmatda" },
    blocked: { cls: "err", label: "Blok" },
    // API enum qiymatlari (string)
    Online: { cls: "on", label: "Faol" },
    Offline: { cls: "off", label: "Oflayn" },
    Recording: { cls: "info", label: "Yozuvda" },
    Blocked: { cls: "err", label: "Bloklangan" },
    Active: { cls: "on", label: "Faol" },
    New: { cls: "info", label: "Yangi" },
    Archived: { cls: "off", label: "Arxiv" },
    Granted: { cls: "on", label: "Ruxsat" },
    Denied: { cls: "err", label: "Rad etildi" },
    Unknown: { cls: "warn", label: "Noma'lum" },
  };
  const s = map[status] || { cls: "off", label: status };
  return <span className={`pill ${s.cls}`}>{s.label}</span>;
};

// `escDisabled` — ixtiyoriy: `true` bo'lsa `Escape` modalni YOPMAYDI.
// Kerak bo'ladi, masalan, modal ichidagi element to'liq ekranda bo'lsa:
// u holda `Escape` faqat to'liq ekrandan chiqarishi kerak, modalni emas.
// Prop ixtiyoriy va default `false` — mavjud chaqiruvlar (10+ joy) o'zgarishsiz ishlaydi.
export const Modal = ({ open, onClose, title, children, footer, wide, escDisabled = false }) => {
  useEffect(() => {
    if (!open || escDisabled) return;
    const handler = (e) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [open, onClose, escDisabled]);
  if (!open) return null;
  return (
    <div className="modal-wrap" onClick={onClose}>
      <div className={`modal ${wide ? "wide" : ""}`} onClick={e => e.stopPropagation()}>
        <div className="modal-head">
          <h3>{title}</h3>
          <button className="icon-btn" onClick={onClose} style={{ width: 28, height: 28 }}>
            <Icon name="x" size={15} />
          </button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-foot">{footer}</div>}
      </div>
    </div>
  );
};

// Vaqt yozuvi alohida kichik komponent — sekundlik yangilanish butun
// CameraTile'ni emas, faqat shu matnni qayta chizadi (umumiy soat: useClock).
const TileClock = () => {
  const now = useClock();
  return (
    <div className="ts mono">
      {now.toLocaleDateString("en-CA")} {now.toLocaleTimeString("en-GB", { hour12: false })}
    </div>
  );
};

// `src` berilsa — haqiqiy video (MJPEG oqim yoki yangilanuvchi snapshot) ko'rsatiladi.
// Berilmasa — eski dekorativ ko'rinish (skan animatsiyasi) qoladi.
//
// `streaming` — `src` uzluksiz MJPEG oqimimi (true) yoki davriy yangilanadigan
// bitta kadrmi (false). Faqat ko'rsatkich uchun emas: rejim o'zgarganda <img>
// elementi `key` orqali qayta yaratiladi, ya'ni eski MJPEG ulanishi darhol uziladi.
export const CameraTile = ({ cam, src, showFaces = true, compact = false, streaming = false }) => {
  const offline = cam.status === "offline";
  const showVideo = !!src && !offline;
  const mode = streaming ? "stream" : "snap";

  // Yuklanish holati rejim bilan birga saqlanadi — rejim almashganda (yangi <img>)
  // "loaded" eskisidan qolib ketmaydi. Snapshot rejimida esa `src` har tikda
  // o'zgarsa ham holat "snap" bo'lib qoladi (miltillash yo'q).
  const [loadedMode, setLoadedMode] = useState(null);
  const loaded = loadedMode === mode;

  // MJPEG oqim <img> tirik turgan ekan ochiq qoladi. Element DOM'dan olib
  // tashlanganda brauzer odatda ulanishni uzadi, lekin buni kafolatlash uchun
  // `src` ni ataylab olib tashlaymiz — backend'dagi ref-count darhol bo'shaydi
  // va ortiqcha RTSP sessiyasi osilib qolmaydi.
  // (React 19: ref callback tozalash funksiyasini qaytarishi mumkin.)
  const imgRef = useCallback((node) => {
    if (!node) return undefined;
    return () => { node.onerror = null; node.removeAttribute("src"); };
  }, []);

  return (
    <div className={`cam-feed ${offline ? "offline" : ""}`} style={ compact ? { aspectRatio: "16/9" } : {} }>
      {showVideo && (
        <img key={mode} ref={imgRef} className="cam-video" src={src} alt={cam.code}
             style={{ opacity: loaded ? 1 : 0 }}
             onLoad={() => setLoadedMode(mode)}
             onError={() => setLoadedMode(null)} />
      )}
      <div className="scan" style={(offline || (showVideo && loaded)) ? { opacity: 0 } : {}} />
      <div className="corners">
        <span className="corner tl"></span>
        <span className="corner tr"></span>
        <span className="corner bl"></span>
        <span className="corner br"></span>
      </div>
      <div className="ident">{cam.code} · {cam.ip}</div>
      <TileClock />
      {offline ? (
        <div className="placeholder">⌀ SIGNAL YO'Q</div>
      ) : (
        <>
          {showVideo && !loaded && <div className="placeholder">⌀ ULANMOQDA...</div>}
          {showFaces && cam.faces && cam.faces.map((f, i) => (
            <div key={f.key ? `${f.key}#${i}` : i} className={`face-box ${f.unknown ? "unknown" : ""}`}
                 style={{ left: `${f.x}%`, top: `${f.y}%`, width: `${f.w}%`, height: `${f.h}%` }}>
              <span className="tag">{f.name} · {f.conf}%</span>
            </div>
          ))}
          <div className="live">
            {/* Jonli oqim va snapshot rejimi vizual farqlanadi — foydalanuvchi
                qaysi plitka haqiqiy oqimda ekanini ko'rib turishi kerak. */}
            <span className={`live-pill ${streaming && loaded ? "" : "snap"}`}>
              <span className="pulse"></span> {streaming && loaded ? "JONLI" : showVideo && loaded ? "KADR" : "ONLAYN"} · {cam.fps}fps
            </span>
          </div>
        </>
      )}
    </div>
  );
};

// --- To'liq ekran (Fullscreen API) ------------------------------------------
// Standart API + Safari uchun `webkit` prefiksli fallback.
const fsElement = () =>
  (typeof document === "undefined"
    ? null
    : document.fullscreenElement || document.webkitFullscreenElement || null);

const fsSupported = () => {
  if (typeof document === "undefined") return false;
  const el = document.documentElement;
  const enabled = document.fullscreenEnabled ?? document.webkitFullscreenEnabled;
  return !!(enabled && (el.requestFullscreen || el.webkitRequestFullscreen));
};

// requestFullscreen eski Safari'da Promise qaytarmaydi — Promise.resolve bilan o'raymiz.
const fsEnter = (el) => {
  const fn = el && (el.requestFullscreen || el.webkitRequestFullscreen);
  if (!fn) return Promise.resolve();
  return Promise.resolve(fn.call(el)).catch(() => {});
};

const fsExit = () => {
  const fn = document.exitFullscreen || document.webkitExitFullscreen;
  if (!fn || !fsElement()) return Promise.resolve();
  return Promise.resolve(fn.call(document)).catch(() => {});
};

// To'liq ekranda 960px oqim upscale bo'lib xira ko'rinadi — ekranning fizik
// pikselidan kelib chiqib kattaroq kenglik so'raymiz (backend uchun 1920 chegara).
const fullscreenWidth = () => {
  if (typeof window === "undefined") return 1920;
  const css = window.screen?.width || window.innerWidth || 1280;
  return Math.min(1920, Math.max(960, Math.round(css * (window.devicePixelRatio || 1))));
};

// Oqim + to'liq ekran boshqaruvi.
//   srcFor(width) -> oqim URL'i (kenglik bo'yicha). Faqat shu funksiya chaqiriladi,
//   shuning uchun `ui.jsx` API endpointlariga bog'lanib qolmaydi.
//   To'liq ekranga BUTUN modal emas, faqat shu konteyner chiqadi.
export const CameraStage = ({ cam, srcFor, baseWidth = 960, offline = false, label, onFullscreenChange }) => {
  const hostRef = useRef(null);
  const [full, setFull] = useState(false);
  const [canFs] = useState(fsSupported);
  const [fsWidth] = useState(fullscreenWidth);

  // Holatni faqat brauzer hodisasidan olamiz — F11, Escape yoki brauzer UI
  // orqali chiqilsa ham sinxron qoladi.
  useEffect(() => {
    const sync = () => setFull(!!hostRef.current && fsElement() === hostRef.current);
    document.addEventListener("fullscreenchange", sync);
    document.addEventListener("webkitfullscreenchange", sync);
    sync();
    return () => {
      document.removeEventListener("fullscreenchange", sync);
      document.removeEventListener("webkitfullscreenchange", sync);
    };
  }, []);

  // Modal yopilib komponent unmount bo'lsa — to'liq ekran osilib qolmasin.
  useEffect(() => {
    const node = hostRef.current;
    return () => { if (fsElement() === node) fsExit(); };
  }, []);

  // Ota-komponentga xabar (masalan Modal'da `Escape` ni bloklash uchun).
  // Cleanup — unmount'da bayroq `true` bo'lib osilib qolmasligi kafolati.
  useEffect(() => {
    onFullscreenChange?.(full);
    return () => onFullscreenChange?.(false);
  }, [full, onFullscreenChange]);

  const toggle = useCallback(() => {
    const node = hostRef.current;
    if (!node || offline) return;
    if (fsElement() === node) fsExit();
    else fsEnter(node);
  }, [offline]);

  // MUHIM: `key` o'zgarmaydi va <img> qayta mount bo'lmaydi — React faqat
  // `src` atributini almashtiradi, brauzer eski MJPEG ulanishini uzib yangisini
  // ochadi. Shu sabab ikkita parallel ulanish (ortiqcha RTSP sessiyasi) qolmaydi.
  const src = offline ? undefined : srcFor(full ? fsWidth : baseWidth);
  const title = full ? "To'liq ekrandan chiqish" : "To'liq ekran";

  return (
    <div ref={hostRef} className={`cam-stage ${full ? "is-fs" : ""}`}
         onDoubleClick={offline ? undefined : toggle}>
      <CameraTile cam={cam} src={src} streaming={!offline} />
      {/* Tugma ustidagi ikki marta bosish sahnaning onDoubleClick'iga o'tmasin
          (aks holda tugma bosilishi bilan birga ikki marta almashinardi). */}
      <div className="cam-stage-bar" onDoubleClick={(e) => e.stopPropagation()}>
        {full && label && <span className="cam-stage-label mono">{label}</span>}
        <button type="button" className="cam-stage-btn" onClick={toggle}
                disabled={offline || !canFs} title={title} aria-label={title} aria-pressed={full}>
          <Icon name={full ? "minimize" : "maximize"} size={13} />
          <span>{full ? "Chiqish" : "To'liq ekran"}</span>
        </button>
      </div>
    </div>
  );
};

// SignalR ulanish holati indikatori. HAQIQIY holatni ko'rsatadi (bezak emas):
// `status` ni ekran `useHubStatus(hubKey)` dan oladi va shu yerga uzatadi —
// shuning uchun `ui.jsx` hub implementatsiyasiga bog'lanmaydi.
const HUB_LABEL = {
  connected: "Jonli",
  connecting: "Ulanmoqda…",
  reconnecting: "Qayta ulanmoqda…",
  disconnected: "Aloqa yo'q",
};

export const HubPill = ({ status, title }) => {
  const cls = status === "connected" ? "" : status === "disconnected" ? "dead" : "pending";
  return (
    <span className={`live-pill ${cls}`} title={title} role="status">
      <span className="pulse"></span>{HUB_LABEL[status] || "Aloqa yo'q"}
    </span>
  );
};

export const Field = ({ label, hint, children }) => (
  <div className="field">
    <label>{label}</label>
    {children}
    {hint && <div className="faint" style={{ fontSize: 11.5 }}>{hint}</div>}
  </div>
);

// Klaviatura bilan boshqariladigan toggle (Space/Enter).
export const Toggle = ({ on, onToggle, disabled = false, label }) => (
  <button
    type="button"
    role="switch"
    aria-checked={!!on}
    aria-label={label}
    disabled={disabled}
    className={`toggle ${on ? "on" : ""}`}
    onClick={disabled ? undefined : onToggle}
  />
);

// Barcha Toast'lar bitta umumiy konteynerga (portal) chiqadi — bir vaqtda
// bir nechtasi ochilsa ustma-ust tushmay, pastdan yuqoriga stack bo'ladi.
let toastHost = null;
function getToastHost() {
  if (typeof document === "undefined") return null;
  if (!toastHost || !toastHost.isConnected) {
    toastHost = document.createElement("div");
    toastHost.className = "toast-host";
    document.body.appendChild(toastHost);
  }
  return toastHost;
}

// Qisqa xabar (xato yoki muvaffaqiyat).
//   message bo'sh/null/undefined -> hech nima render qilinmaydi.
//   onClose berilsa: 6 soniyadan keyin o'zi yopiladi (to'planib qolmasin) va
//   yopish tugmasi ko'rinadi. Odatda `useMutation` ning `reset` iga ulanadi.
export function Toast({ message, kind = "error", onClose }) {
  useEffect(() => {
    if (!message || !onClose) return undefined;
    const id = setTimeout(onClose, 6000);
    return () => clearTimeout(id);
  }, [message, onClose]);

  if (!message) return null;

  const node = (
    <div className={`toast ${kind === "success" ? "success" : "error"}`}
         role={kind === "success" ? "status" : "alert"}>
      <Icon name={kind === "success" ? "check" : "alert"} size={15} />
      <span style={{ flex: 1, minWidth: 0 }}>{message}</span>
      {onClose && (
        <button type="button" className="icon-btn" style={{ width: 24, height: 24 }}
                title="Yopish" onClick={onClose}>
          <Icon name="x" size={13} />
        </button>
      )}
    </div>
  );

  const host = getToastHost();
  return host ? createPortal(node, host) : node;
}
