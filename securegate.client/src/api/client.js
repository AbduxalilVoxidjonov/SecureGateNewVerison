// Markaziy HTTP klient — JWT Bearer, ApiResponse o'rovini ochish, xatolarni boshqarish,
// token yangilash (single-flight refresh), so'rov timeouti va bekor qilish (AbortSignal).
//
// XAVFSIZLIK ESLATMASI:
// Token asosiy manba sifatida MODUL DARAJASIDAGI o'zgaruvchida saqlanadi; localStorage
// faqat sahifa yangilanganda sessiyani tiklash uchun "oyna" sifatida ishlatiladi.
// Bu XSS'ni to'xtatmaydi — to'liq yechim uchun backend'da HttpOnly cookie + stream-ticket
// endpointi kerak (hozircha <img>/<video> uchun token query-string orqali uzatiladi).

const TOKEN_KEY = "sg.token";
const REFRESH_KEY = "sg.refresh";
const EXPIRES_KEY = "sg.expires";

// Access token tugashiga shuncha qolganda proaktiv yangilaymiz.
const REFRESH_SKEW_MS = 60_000;
const DEFAULT_TIMEOUT_MS = 30_000;
export const LONG_TIMEOUT_MS = 60_000;

const readLS = (k) => {
  try { return localStorage.getItem(k); } catch { return null; }
};
const writeLS = (k, v) => {
  try {
    if (v === null || v === undefined || v === "") localStorage.removeItem(k);
    else localStorage.setItem(k, String(v));
  } catch { /* ignore */ }
};

// --- Sessiya holati (asosiy manba — shu yerda) ---------------------------------
let accessToken = readLS(TOKEN_KEY);
let refreshToken = readLS(REFRESH_KEY);
let expiresAt = Number(readLS(EXPIRES_KEY)) || 0; // epoch ms

export const getToken = () => accessToken;
export const getRefreshToken = () => refreshToken;
export const getExpiresAt = () => expiresAt;
export const hasSession = () => !!accessToken;

/** Login/refresh javobidan butun sessiyani o'rnatadi. `null` — sessiyani tozalaydi. */
export const setSession = (session) => {
  if (!session) {
    accessToken = null;
    refreshToken = null;
    expiresAt = 0;
    writeLS(TOKEN_KEY, null);
    writeLS(REFRESH_KEY, null);
    writeLS(EXPIRES_KEY, null);
    return;
  }
  accessToken = session.accessToken || null;
  refreshToken = session.refreshToken || null;
  expiresAt = session.expiresAt ? new Date(session.expiresAt).getTime() : 0;
  if (!Number.isFinite(expiresAt)) expiresAt = 0;
  writeLS(TOKEN_KEY, accessToken);
  writeLS(REFRESH_KEY, refreshToken);
  writeLS(EXPIRES_KEY, expiresAt || null);
};

/** Faqat access tokenni o'rnatadi (moslik uchun). */
export const setToken = (token) => {
  if (!token) { setSession(null); return; }
  accessToken = token;
  writeLS(TOKEN_KEY, token);
};

let unauthorizedHandler = null;
export const setUnauthorizedHandler = (fn) => { unauthorizedHandler = fn; };

const notifyUnauthorized = () => {
  setSession(null);
  if (unauthorizedHandler) unauthorizedHandler();
};

export class ApiError extends Error {
  constructor(message, status, errors) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.errors = errors || null;
  }
}

export const isAbortError = (e) =>
  !!e && (e.name === "AbortError" || e.name === "CanceledError");

function buildQuery(query) {
  if (!query) return "";
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(query)) {
    if (v === undefined || v === null || v === "") continue;
    params.append(k, v);
  }
  const s = params.toString();
  return s ? `?${s}` : "";
}

function combineSignals(signal, timeoutMs) {
  const timeout = AbortSignal.timeout(timeoutMs);
  if (!signal) return timeout;
  if (typeof AbortSignal.any === "function") return AbortSignal.any([signal, timeout]);
  return signal; // eski brauzerlar: hech bo'lmasa bekor qilish ishlasin
}

/** HTTP status + javob tanasidan foydalanuvchiga tushunarli xabar yasaydi. */
function messageFor(res, json) {
  // ApiResponse o'rovi (backend o'z o'zbekcha xabarini yuboradi) — ustuvor.
  if (json && typeof json.message === "string" && json.message.trim()) return json.message;

  switch (res.status) {
    case 403: return "Bu amal uchun ruxsatingiz yo'q.";
    case 423: return "Akkaunt vaqtincha bloklandi. Keyinroq urinib ko'ring.";
    case 404: return "So'ralgan ma'lumot topilmadi.";
    case 408: return "So'rov vaqti tugadi. Qayta urinib ko'ring.";
    case 429: return "So'rovlar juda ko'p. Biroz kuting.";
    default: break;
  }

  // ProblemDetails: { type, title, status, detail }
  if (json) {
    if (typeof json.detail === "string" && json.detail.trim()) return json.detail;
    if (typeof json.title === "string" && json.title.trim()) return json.title;
  }

  if (res.status >= 500) return "Serverda xatolik yuz berdi. Keyinroq urinib ko'ring.";
  if (res.statusText) return res.statusText;
  return `HTTP ${res.status}`;
}

function errorsFor(json) {
  if (!json) return null;
  if (json.errors) return json.errors;
  return null;
}

// --- Token yangilash (single-flight) -------------------------------------------
let refreshPromise = null;

async function performRefresh() {
  if (!refreshToken) return false;
  let res;
  try {
    res = await fetch("/api/auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      // DIQQAT: backend kontrakti — faqat { refreshToken }, javobda yangi
      // accessToken + yangi refreshToken (rotatsiya).
      body: JSON.stringify({ refreshToken }),
      signal: AbortSignal.timeout(DEFAULT_TIMEOUT_MS),
    });
  } catch {
    return false;
  }
  if (!res.ok) return false;

  let json = null;
  try { json = await res.json(); } catch { /* ignore */ }
  if (!json) return false;

  // ApiResponse o'rovi bo'lishi ham, bo'lmasligi ham mumkin.
  const payload = (typeof json.success === "boolean") ? json.data : json;
  if (!payload || !payload.accessToken) return false;

  setSession({
    accessToken: payload.accessToken,
    // Rotatsiya: yangi refresh token kelmasa, eskisini saqlab qolamiz.
    refreshToken: payload.refreshToken || refreshToken,
    expiresAt: payload.expiresAt,
  });
  return true;
}

/** Parallel chaqiruvlar bitta umumiy refresh Promise'ni kutadi. */
export function refreshSession() {
  if (!refreshPromise) {
    refreshPromise = performRefresh().finally(() => { refreshPromise = null; });
  }
  return refreshPromise;
}

/** Token tugashiga <60s qolgan bo'lsa — oldindan yangilaydi. */
async function ensureFreshToken() {
  if (refreshPromise) { await refreshPromise; return; }
  if (!accessToken || !refreshToken || !expiresAt) return;
  if (expiresAt - Date.now() > REFRESH_SKEW_MS) return;
  const ok = await refreshSession();
  if (!ok) notifyUnauthorized();
}

// --- So'rov --------------------------------------------------------------------
async function send(method, path, { body, query, isForm, signal, timeoutMs }) {
  const headers = {};
  if (accessToken) headers["Authorization"] = `Bearer ${accessToken}`;

  let payload;
  if (isForm) {
    payload = body; // FormData — Content-Type'ni brauzer o'zi qo'yadi (boundary bilan)
  } else if (body !== undefined) {
    headers["Content-Type"] = "application/json";
    payload = JSON.stringify(body);
  }

  const url = `/api${path}${buildQuery(query)}`;
  try {
    return await fetch(url, {
      method,
      headers,
      body: payload,
      signal: combineSignals(signal, timeoutMs),
    });
  } catch (e) {
    if (isAbortError(e)) throw e;                       // chaqiruvchi bekor qildi
    if (e && e.name === "TimeoutError") {
      throw new ApiError("So'rov vaqti tugadi. Qayta urinib ko'ring.", 0);
    }
    throw new ApiError("Serverga ulanib bo'lmadi.", 0); // TypeError: Failed to fetch
  }
}

async function request(method, path, options = {}) {
  const opts = { timeoutMs: DEFAULT_TIMEOUT_MS, ...options };

  await ensureFreshToken();

  let res = await send(method, path, opts);

  // 401 — bir marta refresh qilib, so'rovni qayta urinamiz (single-flight).
  if (res.status === 401 && refreshToken) {
    const ok = await refreshSession();
    if (ok) res = await send(method, path, opts);
  }

  if (res.status === 401) {
    notifyUnauthorized();
    throw new ApiError("Avtorizatsiya talab qilinadi.", 401);
  }

  const text = await res.text();
  let json = null;
  if (text) {
    try { json = JSON.parse(text); } catch { json = null; }
  }

  // ApiResponse o'rovi: { success, message, data, errors }
  if (json && typeof json.success === "boolean") {
    if (!json.success) throw new ApiError(messageFor(res, json), res.status, errorsFor(json));
    return json.data;
  }

  if (!res.ok) throw new ApiError(messageFor(res, json), res.status, errorsFor(json));
  return json;
}

export const api = {
  get: (path, query, signal) => request("GET", path, { query, signal }),
  post: (path, body, signal) => request("POST", path, { body, signal }),
  put: (path, body, signal) => request("PUT", path, { body, signal }),
  del: (path, signal) => request("DELETE", path, { signal }),
  postForm: (path, formData, signal) => request("POST", path, { body: formData, isForm: true, signal }),
  putForm: (path, formData, signal) => request("PUT", path, { body: formData, isForm: true, signal }),
  // Uzoq davom etadigan amallar (masalan test-connection) uchun kengaytirilgan timeout.
  postLong: (path, body, signal) =>
    request("POST", path, { body, signal, timeoutMs: LONG_TIMEOUT_MS }),
  getLong: (path, query, signal) =>
    request("GET", path, { query, signal, timeoutMs: LONG_TIMEOUT_MS }),
};
