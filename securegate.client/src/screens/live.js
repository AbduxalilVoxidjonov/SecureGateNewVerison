// Ekranlar uchun umumiy real-time (SignalR) yordamchilari.
//
// Bu yerda faqat toza funksiyalar va kichik hook'lar bor — hub'ning o'zi bilan
// ishlash `hooks/useHub.js` zimmasida (bitta hub = bitta ulanish, ref-count bilan).
import { useCallback, useEffect, useRef } from "react";
import { useHubStatus } from "../hooks/useHub";

// --- Ulanish holati -------------------------------------------------------------

// Uzilishdan keyin qayta ulanganda BIR MARTA `reload()` chaqiradi —
// uzilish paytida o'tkazib yuborilgan hodisalarni qoplash uchun (SignalR
// buferlamaydi: `Clients.All` faqat o'sha paytda ulangan klientlarga boradi).
//
// Sahifa birinchi marta ochilganda (`connecting` -> `connected`) reload QILINMAYDI:
// boshlang'ich ma'lumotni `useApi` allaqachon olgan, ortiqcha so'rov kerak emas.
function useReloadWhenBack(status, reload) {
  // Handler'ni deps'ga qo'shmaymiz — `reload` har renderda yangi bo'lishi mumkin.
  const reloadRef = useRef(reload);
  useEffect(() => { reloadRef.current = reload; });

  const wasConnected = useRef(false);
  const dropped = useRef(false);

  useEffect(() => {
    if (status === "connected") {
      if (dropped.current) {
        dropped.current = false;
        reloadRef.current?.();
      }
      wasConnected.current = true;
    } else if (wasConnected.current) {
      // Bir marta ulangandan keyin har qanday boshqa holat — uzilish.
      dropped.current = true;
    }
  }, [status]);
}

// Bitta hub uchun qulay o'ram: holatni ham qaytaradi (indikator uchun).
export function useReloadOnReconnect(hubKey, reload) {
  const status = useHubStatus(hubKey);
  useReloadWhenBack(status, reload);
  return status;
}

// Hodisa "ro'yxatni qayta o'qish"ni talab qilsa (masalan blok holati o'zgarishi),
// har bir hodisa uchun so'rov yubormaslik kerak — bu throttle qilingan reload.
// Birinchi chaqiruv darhol, keyingilari `ms` oynasining oxirida bitta so'rov bo'lib ketadi.
export function useThrottledReload(reload, ms = 4000) {
  const reloadRef = useRef(reload);
  useEffect(() => { reloadRef.current = reload; });

  const lastAt = useRef(0);
  const timer = useRef(null);

  useEffect(() => () => { if (timer.current) clearTimeout(timer.current); }, []);

  return useCallback(() => {
    if (timer.current) return; // allaqachon rejalashtirilgan
    const wait = ms - (Date.now() - lastAt.current);
    if (wait <= 0) {
      lastAt.current = Date.now();
      reloadRef.current?.();
      return;
    }
    timer.current = setTimeout(() => {
      timer.current = null;
      lastAt.current = Date.now();
      reloadRef.current?.();
    }, wait);
  }, [ms]);
}

// --- Ro'yxatlar -----------------------------------------------------------------

// Yangi yozuvni ro'yxat boshiga qo'yadi: `idOf` bo'yicha dedupe + maksimal uzunlik.
// Backend bitta hodisani bir necha marta yuborishi mumkin (masalan NewAccessLog
// kameraga bog'langan HAR BIR turniket uchun alohida) — dedupe shu sabab shart.
export const prependCapped = (list, item, idOf, max = 50) => {
  const arr = Array.isArray(list) ? list : [];
  const id = idOf(item);
  const rest = id === undefined || id === null ? arr : arr.filter((x) => idOf(x) !== id);
  const next = [item, ...rest];
  return next.length > max ? next.slice(0, max) : next;
};

// --- Yuz qutilari (FaceDetected / FaceFrameProcessed) ---------------------------

// Quti yangilanmasa qancha vaqtdan keyin o'chadi (FaceFrameProcessed yo'qolsa
// yoki worker to'xtab qolsa — qutilar ekranda muzlab qolmasin).
export const FACE_TTL_MS = 2500;

// Bitta kamera uchun saqlanadigan maksimal quti soni.
const MAX_FACES_PER_CAMERA = 12;

const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const num = (v) => (Number.isFinite(Number(v)) ? Number(v) : 0);

// Backend qutini KADR PIKSELIDA yuboradi: `box:{x,y,w,h,fw,fh}` (fw/fh — kadr o'lchami).
// `ui.jsx` dagi overlay esa foizda ishlaydi (left/top/width/height: %) —
// shu yerda normalizatsiya qilinadi va quti kadr chegarasidan chiqmasligi ta'minlanadi.
//
// Kalit backend bilan bir xil bo'lishi SHART (FaceFrameProcessed.activeKeys):
//   tanilgan -> "Student:12", noma'lum -> "Unknown" (barcha noma'lum yuzlar uchun bitta kalit).
export const faceFromEvent = (p, now = Date.now()) => {
  const b = p?.box;
  if (!b) return null;
  const fw = num(b.fw);
  const fh = num(b.fh);
  if (fw <= 0 || fh <= 0) return null;

  const x = clamp((num(b.x) / fw) * 100, 0, 100);
  const y = clamp((num(b.y) / fh) * 100, 0, 100);
  const w = clamp((num(b.w) / fw) * 100, 0, 100 - x);
  const h = clamp((num(b.h) / fh) * 100, 0, 100 - y);
  if (w <= 0 || h <= 0) return null;

  const unknown = !!p.isUnknown || p.personType === "Unknown";
  return {
    key: unknown ? "Unknown" : `${p.personType}:${p.personId}`,
    x, y, w, h,
    unknown,
    name: p.name || "Noma'lum",
    // FaceDetected.confidence — kosinus o'xshashligi (0..1), foizga o'giramiz.
    conf: Math.round(clamp(num(p.confidence) * 100, 0, 100)),
    at: now,
  };
};

// Yangi quti: tanilgan odamning eski qutisi almashtiriladi, "Unknown" esa
// qo'shiladi (bir kadrda bir nechta noma'lum yuz bo'lishi mumkin, kaliti bitta).
export const mergeFace = (list, face) => {
  const arr = Array.isArray(list) ? list : [];
  const next = face.key === "Unknown" ? arr.slice() : arr.filter((f) => f.key !== face.key);
  next.push(face);
  return next.length > MAX_FACES_PER_CAMERA ? next.slice(next.length - MAX_FACES_PER_CAMERA) : next;
};

// FaceFrameProcessed.activeKeys — kadr oxirida aktiv kalitlar (takrorlanishi mumkin).
// Ro'yxatda bo'lmagan qutilar TTL ni kutmasdan darhol o'chiriladi.
export const syncFaces = (list, activeKeys) => {
  const arr = Array.isArray(list) ? list : [];
  if (arr.length === 0) return arr;

  const left = new Map();
  for (const k of activeKeys || []) left.set(k, (left.get(k) || 0) + 1);

  // Oxiridan boshlaymiz — bir xil kalitli qutilardan eng yangilari qoladi.
  const kept = [];
  for (let i = arr.length - 1; i >= 0; i--) {
    const n = left.get(arr[i].key) || 0;
    if (n > 0) { left.set(arr[i].key, n - 1); kept.push(arr[i]); }
  }
  if (kept.length === arr.length) return arr; // o'zgarish yo'q — qayta render qilmaymiz
  kept.reverse();
  return kept;
};

// TTL bo'yicha eskirgan qutilarni olib tashlaydi.
export const dropStaleFaces = (byCamera, ttl = FACE_TTL_MS, now = Date.now()) => {
  let changed = false;
  const next = {};
  for (const [camId, list] of Object.entries(byCamera)) {
    const kept = list.filter((f) => now - f.at < ttl);
    if (kept.length !== list.length) changed = true;
    if (kept.length) next[camId] = kept;
  }
  return changed ? next : byCamera;
};
