// SignalR klient infratuzilmasi — har bir hub uchun BITTA umumiy ulanish (ref-count bilan).
//
// Nima uchun modul darajasida singleton?
//   Bir nechta ekran/komponent bir xil hubga obuna bo'lishi mumkin (masalan kamera
//   ro'yxati + jonli ko'rish). Har biri o'z WebSocket'ini ochsa — server ham, brauzer
//   ham ortiqcha yuklanadi. Shuning uchun hub bo'yicha bitta HubConnection saqlanadi
//   va obunachilar soni (ref-count) hisoblanadi. Oxirgi obunachi ketganda ulanish
//   yopiladi (kichik kechikish bilan — pastdagi CLOSE_GRACE_MS izohiga qarang).
//
// Token: `accessTokenFactory` har `start()` va har `reconnect`da qayta chaqiriladi,
// shuning uchun bu yerda token QOTIRIB QO'YILMAYDI — har safar client.js dagi joriy
// token o'qiladi, kerak bo'lsa oldindan yangilanadi (single-flight refresh).
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import {
  getToken,
  getExpiresAt,
  getRefreshToken,
  refreshSession,
} from "./client";

/** Qo'llab-quvvatlanadigan hublar. Backend: Program.cs `MapHub<...>("/hubs/...")`. */
export const HUB_KEYS = ["camera", "turnstile", "alert", "dashboard"];

const HUB_PATHS = {
  camera: "/hubs/camera",
  turnstile: "/hubs/turnstile",
  alert: "/hubs/alert",
  dashboard: "/hubs/dashboard",
};

// withAutomaticReconnect jadvali (ms). Jadval tugagach SignalR qayta urinishni
// TO'XTATADI — cheksiz sikl bo'lmaydi, holat `disconnected` bo'lib qoladi.
const RETRY_DELAYS_MS = [0, 2000, 5000, 10000, 30000];

// Ref-count 0 ga tushganda ulanishni darhol emas, shuncha kutib yopamiz.
// Sabablari:
//   1) React StrictMode (dev) effektlarni mount → unmount → mount qilib ikki marta
//      ishga tushiradi; darhol yopilsa ulanish ochilib-yopilib shovqin qiladi.
//   2) Ekranlar almashganda (unmount → mount) ulanish bekorga uzilmasin.
const CLOSE_GRACE_MS = 500;

// Token tugashiga shuncha qolganda ulanishdan oldin proaktiv yangilaymiz
// (client.js dagi REFRESH_SKEW_MS bilan bir xil).
const REFRESH_SKEW_MS = 60_000;

/**
 * Har bir hub uchun holat:
 *  connection   — HubConnection (bir marta yaratiladi, stop/start qayta ishlatiladi)
 *  refCount     — faol obunachilar soni
 *  status       — "connected" | "connecting" | "reconnecting" | "disconnected"
 *  statusSubs   — holat o'zgarishini kuzatuvchilar (useSyncExternalStore uchun)
 *  handlers     — Map<eventName, Set<fn>>; har event nomi uchun BITTA `.on` dispatcher
 *  bound        — `.on` qilingan event nomlari (qayta ro'yxatdan o'tkazmaslik uchun)
 *  starting     — davom etayotgan start() Promise'i (ikki marta start bo'lmasin)
 *  closeTimer   — kechiktirilgan yopish taymeri
 *  retryTimer   — dastlabki ulanish muvaffaqiyatsiz bo'lsa — qayta urinish taymeri
 *  retryIndex   — RETRY_DELAYS_MS bo'yicha joriy qadam
 *  error        — oxirgi xato (konsolga emas, so'ralganda beriladi)
 */
const hubs = new Map();

const isHubKey = (key) => Object.prototype.hasOwnProperty.call(HUB_PATHS, key);

function getEntry(hubKey) {
  if (!isHubKey(hubKey)) {
    throw new Error(`Noma'lum hub: "${hubKey}". Ruxsat etilgan: ${HUB_KEYS.join(", ")}`);
  }
  let entry = hubs.get(hubKey);
  if (!entry) {
    entry = {
      key: hubKey,
      connection: null,
      refCount: 0,
      status: "disconnected",
      statusSubs: new Set(),
      handlers: new Map(),
      bound: new Set(),
      starting: null,
      closeTimer: null,
      retryTimer: null,
      retryIndex: 0,
      error: null,
    };
    hubs.set(hubKey, entry);
  }
  return entry;
}

function setStatus(entry, status) {
  if (entry.status === status) return;
  entry.status = status;
  for (const fn of entry.statusSubs) fn();
}

/**
 * Har ulanish/qayta ulanishda chaqiriladi. Token tugash arafasida bo'lsa —
 * avval yangilaymiz (client.js single-flight refresh), keyin eng yangisini beramiz.
 */
async function accessTokenFactory() {
  const expiresAt = getExpiresAt();
  if (getRefreshToken() && expiresAt && expiresAt - Date.now() < REFRESH_SKEW_MS) {
    try { await refreshSession(); } catch { /* start() o'zi 401 bilan yiqiladi */ }
  }
  return getToken() || "";
}

function createConnection(entry) {
  const conn = new HubConnectionBuilder()
    .withUrl(HUB_PATHS[entry.key], { accessTokenFactory })
    .withAutomaticReconnect(RETRY_DELAYS_MS)
    .configureLogging(import.meta.env?.DEV ? LogLevel.Warning : LogLevel.None)
    .build();

  conn.onreconnecting((err) => {
    entry.error = err || null;
    setStatus(entry, "reconnecting");
  });

  conn.onreconnected(() => {
    entry.error = null;
    entry.retryIndex = 0;
    setStatus(entry, "connected");
  });

  // Jadval tugagach (yoki stop() chaqirilgach) shu yerga tushamiz.
  conn.onclose((err) => {
    entry.error = err || entry.error;
    entry.starting = null;
    setStatus(entry, "disconnected");
  });

  // Server OnConnectedAsync da yuboradigan "Connected" xabari — hech kim
  // obuna bo'lmasa SignalR uni "handler yo'q" deb ogohlantiradi. Bo'sh handler
  // bilan jimgina yutamiz (bu diagnostik xabar, UI uchun emas).
  conn.on("Connected", () => {});

  return conn;
}

/** Event nomi uchun bitta dispatcher — obunachilar Set orqali kelib-ketaveradi. */
function bindEvent(entry, eventName) {
  if (entry.bound.has(eventName)) return;
  entry.bound.add(eventName);
  entry.connection.on(eventName, (...args) => {
    const set = entry.handlers.get(eventName);
    if (!set || set.size === 0) return;
    // Nusxa: handler ichida obunani bekor qilish xavfsiz bo'lsin.
    for (const fn of [...set]) {
      try { fn(...args); } catch (e) { entry.error = e; }
    }
  });
}

function clearTimers(entry) {
  if (entry.closeTimer) { clearTimeout(entry.closeTimer); entry.closeTimer = null; }
  if (entry.retryTimer) { clearTimeout(entry.retryTimer); entry.retryTimer = null; }
}

/**
 * Ulanishni ishga tushiradi. Dastlabki `start()` muvaffaqiyatsiz bo'lsa —
 * `withAutomaticReconnect` ishlamaydi (u faqat O'RNATILGAN ulanish uzilganda
 * ishlaydi), shuning uchun bu yerda AYNAN SHU jadval bo'yicha cheklangan
 * qayta urinish qilamiz. Jadval tugagach — `disconnected` va TO'XTAYMIZ.
 */
function ensureStarted(entry) {
  if (entry.refCount <= 0) return;
  if (entry.starting) return;
  if (entry.connection && entry.connection.state !== HubConnectionState.Disconnected) return;
  if (!getToken()) { setStatus(entry, "disconnected"); return; } // sessiya yo'q — urinmaymiz

  if (!entry.connection) entry.connection = createConnection(entry);
  for (const eventName of entry.handlers.keys()) bindEvent(entry, eventName);

  setStatus(entry, entry.retryIndex === 0 ? "connecting" : "reconnecting");

  entry.starting = entry.connection
    .start()
    .then(() => {
      entry.starting = null;
      entry.error = null;
      entry.retryIndex = 0;
      // Kutilmagan holat: start paytida oxirgi obunachi ketgan bo'lishi mumkin.
      if (entry.refCount <= 0) { void stopNow(entry); return; }
      setStatus(entry, "connected");
    })
    .catch((err) => {
      entry.starting = null;
      entry.error = err;
      if (entry.refCount <= 0) { setStatus(entry, "disconnected"); return; }

      const delay = RETRY_DELAYS_MS[entry.retryIndex];
      if (delay === undefined) {
        // Jadval tugadi — cheksiz urinmaymiz (CPU/tarmoqni yemasin).
        entry.retryIndex = 0;
        setStatus(entry, "disconnected");
        return;
      }
      entry.retryIndex += 1;
      setStatus(entry, "reconnecting");
      entry.retryTimer = setTimeout(() => {
        entry.retryTimer = null;
        ensureStarted(entry);
      }, delay);
    });
}

async function stopNow(entry) {
  clearTimers(entry);
  entry.retryIndex = 0;
  const conn = entry.connection;
  if (!conn) { setStatus(entry, "disconnected"); return; }
  try { await conn.stop(); } catch (e) { entry.error = e; }
  entry.starting = null;
  setStatus(entry, "disconnected");
}

function acquire(entry) {
  entry.refCount += 1;
  if (entry.closeTimer) { clearTimeout(entry.closeTimer); entry.closeTimer = null; }
  // Avval uzilib qolgan bo'lsa (jadval tugagan), yangi obunachi kelishi —
  // qayta urinish uchun sabab.
  if (entry.refCount === 1 && entry.status === "disconnected") entry.retryIndex = 0;
  ensureStarted(entry);
}

function release(entry) {
  entry.refCount -= 1;
  if (entry.refCount > 0) return;
  entry.refCount = 0;
  if (entry.closeTimer) clearTimeout(entry.closeTimer);
  // Kechiktirilgan yopish — StrictMode ning mount→unmount→mount sikli va
  // ekranlar almashuvi ulanishni uzmasin.
  entry.closeTimer = setTimeout(() => {
    entry.closeTimer = null;
    if (entry.refCount > 0) return;
    void stopNow(entry);
  }, CLOSE_GRACE_MS);
}

// --- Ommaviy API ---------------------------------------------------------------

/**
 * Hub eventiga obuna bo'ladi va ulanishni tirik ushlab turadi (ref-count +1).
 * @param {string} hubKey
 * @param {string} eventName
 * @param {(...args:any[])=>void} fn
 * @returns {() => void} obunani bekor qiluvchi funksiya
 */
export function subscribeHubEvent(hubKey, eventName, fn) {
  const entry = getEntry(hubKey);
  let set = entry.handlers.get(eventName);
  if (!set) {
    set = new Set();
    entry.handlers.set(eventName, set);
  }
  set.add(fn);
  if (entry.connection) bindEvent(entry, eventName);
  acquire(entry);

  let done = false;
  return () => {
    if (done) return;
    done = true;
    set.delete(fn);
    release(entry);
  };
}

/**
 * Faqat ulanish holatini kuzatadi (ref-count +1 — ya'ni ulanishni ham ushlab turadi).
 * @param {string} hubKey
 * @param {() => void} onChange
 * @returns {() => void}
 */
export function subscribeHubStatus(hubKey, onChange) {
  const entry = getEntry(hubKey);
  entry.statusSubs.add(onChange);
  acquire(entry);

  let done = false;
  return () => {
    if (done) return;
    done = true;
    entry.statusSubs.delete(onChange);
    release(entry);
  };
}

/** @returns {"connected"|"connecting"|"reconnecting"|"disconnected"} */
export function getHubStatus(hubKey) {
  if (!isHubKey(hubKey)) return "disconnected";
  const entry = hubs.get(hubKey);
  return entry ? entry.status : "disconnected";
}

/** Oxirgi xato (bo'lsa) — konsolga yozilmaydi, so'ralganda beriladi. */
export function getHubError(hubKey) {
  if (!isHubKey(hubKey)) return null;
  const entry = hubs.get(hubKey);
  return entry ? entry.error : null;
}

/**
 * Server metodini chaqiradi (masalan TurnstileHub.OpenTurnstile).
 * Ulanish tayyor bo'lmasa xato tashlaydi — chaqiruvchi uni ushlashi kerak.
 */
export async function invokeHub(hubKey, methodName, ...args) {
  const entry = getEntry(hubKey);
  if (!entry.connection || entry.connection.state !== HubConnectionState.Connected) {
    throw new Error("Serverga ulanish yo'q. Qayta urinib ko'ring.");
  }
  return entry.connection.invoke(methodName, ...args);
}

/**
 * Uzilib qolgan (jadvali tugagan) hubni qo'lda qayta ulash.
 * Obunachisi bo'lmasa hech narsa qilmaydi.
 */
export function retryHub(hubKey) {
  const entry = getEntry(hubKey);
  entry.retryIndex = 0;
  clearTimers(entry);
  ensureStarted(entry);
}

/**
 * Barcha ulanishlarni yopadi — logout / sessiya tugashi uchun.
 * Ref-count nolga tushiriladi; keyin foydalanuvchi qayta kirsa, komponentlar
 * qayta mount bo'lganda ulanishlar yangi token bilan qaytadan ochiladi.
 */
export function closeAllHubs() {
  for (const entry of hubs.values()) {
    entry.refCount = 0;
    void stopNow(entry);
  }
}
