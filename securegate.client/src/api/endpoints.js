// Backend API endpointlari — domen bo'yicha guruhlangan funksiyalar.
import { api, getToken } from "./client";

// <img>/<video> header yubora olmaydi — token query-string orqali uzatiladi.
// TODO(backend): qisqa muddatli "stream ticket" endpointi qo'shilsa, token
// URL'da umuman ko'rinmaydi (loglar/Referer orqali sizib chiqmaydi).
const withToken = (path, w) => {
  const params = new URLSearchParams();
  const t = getToken();
  if (t) params.set("access_token", t);
  if (w) params.set("w", String(w));
  const q = params.toString();
  return `/api${path}${q ? `?${q}` : ""}`;
};

export const authApi = {
  login: (email, password, rememberMe = false) =>
    api.post("/auth/login", { email, password, rememberMe }),
  me: (signal) => api.get("/auth/me", undefined, signal),
  logout: () => api.post("/auth/logout"),
  changePassword: (currentPassword, newPassword) =>
    api.post("/auth/change-password", { currentPassword, newPassword }),
};

export const dashboardApi = {
  get: (signal) => api.get("/dashboard", undefined, signal),
};

export const camerasApi = {
  list: (query, signal) => api.get("/cameras", query, signal),
  create: (body) => api.post("/cameras", body),
  update: (id, body) => api.put(`/cameras/${id}`, body),
  remove: (id) => api.del(`/cameras/${id}`),
  // Kamera bilan ulanishni tekshirish sekin bo'lishi mumkin — uzunroq timeout.
  testConnection: (body) => api.postLong("/cameras/test-connection", body),
  // Jonli MJPEG oqim (modal uchun) va bitta kadr (grid thumbnail uchun)
  streamUrl: (id, w) => withToken(`/cameras/${id}/stream`, w),
  snapshotUrl: (id, w) => withToken(`/cameras/${id}/snapshot`, w),
};

export const usersApi = {
  list: (query, signal) => api.get("/users", query, signal),
  create: (formData) => api.postForm("/users", formData),
  remove: (id) => api.del(`/users/${id}`),
  block: (id, body) => api.post(`/users/${id}/block`, body),
  unblock: (id) => api.post(`/users/${id}/unblock`),
};

export const staffApi = {
  list: (signal) => api.get("/staff", undefined, signal),
  create: (formData) => api.postForm("/staff", formData),
  update: (id, formData) => api.putForm(`/staff/${id}`, formData),
  remove: (id) => api.del(`/staff/${id}`),
};

export const turnstilesApi = {
  list: (signal) => api.get("/turnstiles", undefined, signal),
  get: (id, signal) => api.get(`/turnstiles/${id}`, undefined, signal),
  create: (body) => api.post("/turnstiles", body),
  open: (id) => api.post(`/turnstiles/${id}/open`),
  close: (id) => api.post(`/turnstiles/${id}/close`),
  block: (id) => api.post(`/turnstiles/${id}/block`),
  unblock: (id) => api.post(`/turnstiles/${id}/unblock`),
  emergencyOpen: (reason, signal) => api.post("/turnstiles/emergency-open", { reason }, signal),
  testConnection: (body) => api.postLong("/turnstiles/test-connection", body),
};

export const accessLogsApi = {
  list: (query, signal) => api.get("/access-logs", query, signal),
};

export const blockedApi = {
  list: (query, signal) => api.get("/blocked", query, signal),
};

export const cameraUsersApi = {
  list: (query, signal) => api.get("/camera-users", query, signal),
  markReviewed: (id, reviewed = true) => api.post(`/camera-users/${id}/reviewed?reviewed=${reviewed}`),
};

export const reportsApi = {
  get: (signal) => api.get("/reports", undefined, signal),
};

// DIQQAT: backend sozlamalar javobida sirlarni maskalab yuboradi
// (masalan "••••1234" va hasSmtpPassword kabi bayroqlar) — bu yerda
// qattiq kodlangan maydon nomlari yo'q, javob shakli o'zgarsa ham buzilmaydi.
export const settingsApi = {
  getNotifications: (signal) => api.get("/settings/notifications", undefined, signal),
  saveNotifications: (body) => api.put("/settings/notifications", body),
  getIntegrations: (signal) => api.get("/settings/integrations", undefined, signal),
  saveIntegrations: (body) => api.put("/settings/integrations", body),
  getApi: (signal) => api.get("/settings/api", undefined, signal),
  saveWebhook: (body) => api.put("/settings/api/webhook", body),
  regenerateKey: () => api.post("/settings/api/regenerate-key"),
};

export const adminsApi = {
  list: (signal) => api.get("/admins", undefined, signal),
  availableStaff: (signal) => api.get("/admins/available-staff", undefined, signal),
  cameraGroups: (signal) => api.get("/admins/camera-groups", undefined, signal),
  get: (id, signal) => api.get(`/admins/${id}`, undefined, signal),
  create: (body) => api.post("/admins", body),
  update: (id, body) => api.put(`/admins/${id}`, body),
  remove: (id) => api.del(`/admins/${id}`),
  resetPassword: (id, body) => api.post(`/admins/${id}/reset-password`, body),
  toggleActive: (id) => api.post(`/admins/${id}/toggle-active`),
  permissions: (signal) => api.get("/admins/permissions", undefined, signal),
};

// NVR arxivi: vaqt oralig'i bilan ishlaydi (eski `date=yyyy-MM-dd` o'rniga from/to).
// from/to — ISO-8601 UTC satrlari ("2026-09-07T09:00:00.000Z"); backend UTC kutadi.
export const recordingsApi = {
  list: (signal) => api.get("/recordings", undefined, signal),
  // from/to ixtiyoriy — berilmasa backend oxirgi 24 soatni qaytaradi.
  // `api.get` query'ni buildQuery orqali yig'adi va undefined/null/"" larni tashlab ketadi.
  camera: (id, from, to, signal) => api.get(`/recordings/camera/${id}`, { from, to }, signal),
  // Yuklab olishda from/to MAJBURIY (backend 400 qaytaradi). Bu yerda faqat URL yasaladi —
  // token `recordings.jsx` dagi downloadHref() naqshi bilan qo'shiladi.
  downloadUrl: (id, from, to) => {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    const q = params.toString();
    return `/api/recordings/camera/${id}/download${q ? `?${q}` : ""}`;
  },
};
