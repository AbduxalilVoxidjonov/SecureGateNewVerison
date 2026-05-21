// Shared UI primitives
import { useState, useEffect } from "react";
import { Icon } from "./Icon";

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

export const Modal = ({ open, onClose, title, children, footer, wide }) => {
  useEffect(() => {
    if (!open) return;
    const handler = (e) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [open, onClose]);
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

export const Sparkline = ({ data = [], color = "var(--accent)", w = 80, h = 28 }) => {
  const max = Math.max(...data, 1);
  const min = Math.min(...data, 0);
  const pts = data.map((v, i) => {
    const x = (i / (data.length - 1)) * w;
    const y = h - ((v - min) / (max - min || 1)) * (h - 2) - 1;
    return `${x.toFixed(1)},${y.toFixed(1)}`;
  }).join(" ");
  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`}>
      <polyline points={pts} fill="none" stroke={color} strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
};

export const Donut = ({ segments = [], size = 96, stroke = 12 }) => {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const total = segments.reduce((a, s) => a + s.value, 0) || 1;
  // Precompute each arc's length + cumulative offset (no mutation during render).
  const arcs = segments.map((s, i) => ({
    len: (s.value / total) * c,
    offset: segments.slice(0, i).reduce((a, p) => a + (p.value / total) * c, 0),
    color: s.color,
  }));
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
      <circle cx={size/2} cy={size/2} r={r} fill="none" stroke="var(--bg-2)" strokeWidth={stroke} />
      {arcs.map((a, i) => (
        <circle key={i}
          cx={size/2} cy={size/2} r={r}
          fill="none" stroke={a.color} strokeWidth={stroke}
          strokeDasharray={`${a.len} ${c - a.len}`}
          strokeDashoffset={-a.offset}
          transform={`rotate(-90 ${size/2} ${size/2})`}
          strokeLinecap="butt"
        />
      ))}
    </svg>
  );
};

// `src` berilsa — haqiqiy video (MJPEG oqim yoki yangilanuvchi snapshot) ko'rsatiladi.
// Berilmasa — eski dekorativ ko'rinish (skan animatsiyasi) qoladi.
export const CameraTile = ({ cam, src, showFaces = true, compact = false }) => {
  const [now, setNow] = useState(new Date());
  const [loaded, setLoaded] = useState(false);
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);
  const ts = now.toLocaleTimeString("en-GB", { hour12: false });
  const date = now.toISOString().slice(0,10);
  const offline = cam.status === "offline";
  const showVideo = !!src && !offline;
  return (
    <div className={`cam-feed ${offline ? "offline" : ""}`} style={ compact ? { aspectRatio: "16/9" } : {} }>
      {showVideo && (
        <img className="cam-video" src={src} alt={cam.code}
             style={{ opacity: loaded ? 1 : 0 }}
             onLoad={() => setLoaded(true)}
             onError={() => setLoaded(false)} />
      )}
      <div className="scan" style={(offline || (showVideo && loaded)) ? { opacity: 0 } : {}} />
      <div className="corners">
        <span className="corner tl"></span>
        <span className="corner tr"></span>
        <span className="corner bl"></span>
        <span className="corner br"></span>
      </div>
      <div className="ident">{cam.code} · {cam.ip}</div>
      <div className="ts mono">{date} {ts}</div>
      {offline ? (
        <div className="placeholder">⌀ SIGNAL YO'Q</div>
      ) : (
        <>
          {showVideo && !loaded && <div className="placeholder">⌀ ULANMOQDA...</div>}
          {showFaces && cam.faces && cam.faces.map((f, i) => (
            <div key={i} className={`face-box ${f.unknown ? "unknown" : ""}`}
                 style={{ left: `${f.x}%`, top: `${f.y}%`, width: `${f.w}%`, height: `${f.h}%` }}>
              <span className="tag">{f.name} · {f.conf}%</span>
            </div>
          ))}
          <div className="live">
            <span className="live-pill">
              <span className="pulse"></span> {showVideo && loaded ? "LIVE" : "ONLAYN"} · {cam.fps}fps
            </span>
          </div>
        </>
      )}
    </div>
  );
};

export const Field = ({ label, hint, children }) => (
  <div className="field">
    <label>{label}</label>
    {children}
    {hint && <div className="faint" style={{ fontSize: 11.5 }}>{hint}</div>}
  </div>
);
