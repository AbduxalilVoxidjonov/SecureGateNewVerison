// Backend API endpointlari — domen bo'yicha guruhlangan funksiyalar.
import { api, getToken } from "./client";

// <img>/<video> header yubora olmaydi — token query-string orqali uzatiladi.
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
  me: () => api.get("/auth/me"),
  logout: () => api.post("/auth/logout"),
  changePassword: (currentPassword, newPassword) =>
    api.post("/auth/change-password", { currentPassword, newPassword }),
};

export const dashboardApi = {
  get: () => api.get("/dashboard"),
};

export const camerasApi = {
  list: (query) => api.get("/cameras", query),
  get: (id) => api.get(`/cameras/${id}`),
  create: (body) => api.post("/cameras", body),
  update: (id, body) => api.put(`/cameras/${id}`, body),
  remove: (id) => api.del(`/cameras/${id}`),
  testConnection: (body) => api.post("/cameras/test-connection", body),
  // Jonli MJPEG oqim (modal uchun) va bitta kadr (grid thumbnail uchun)
  streamUrl: (id, w) => withToken(`/cameras/${id}/stream`, w),
  snapshotUrl: (id, w) => withToken(`/cameras/${id}/snapshot`, w),
};

export const cameraGroupsApi = {
  list: () => api.get("/camera-groups"),
  simple: () => api.get("/camera-groups/simple"),
  newForm: () => api.get("/camera-groups/new"),
  get: (id) => api.get(`/camera-groups/${id}`),
  create: (body) => api.post("/camera-groups", body),
  update: (id, body) => api.put(`/camera-groups/${id}`, body),
  remove: (id) => api.del(`/camera-groups/${id}`),
};

export const usersApi = {
  list: (query) => api.get("/users", query),
  get: (id) => api.get(`/users/${id}`),
  create: (formData) => api.postForm("/users", formData),
  update: (id, formData) => api.putForm(`/users/${id}`, formData),
  remove: (id) => api.del(`/users/${id}`),
  block: (id, body) => api.post(`/users/${id}/block`, body),
  unblock: (id) => api.post(`/users/${id}/unblock`),
  turnstiles: () => api.get("/users/turnstiles"),
};

export const staffApi = {
  list: () => api.get("/staff"),
  get: (id) => api.get(`/staff/${id}`),
  create: (formData) => api.postForm("/staff", formData),
  update: (id, formData) => api.putForm(`/staff/${id}`, formData),
  remove: (id) => api.del(`/staff/${id}`),
};

export const turnstilesApi = {
  list: () => api.get("/turnstiles"),
  get: (id) => api.get(`/turnstiles/${id}`),
  create: (body) => api.post("/turnstiles", body),
  open: (id) => api.post(`/turnstiles/${id}/open`),
  close: (id) => api.post(`/turnstiles/${id}/close`),
  block: (id) => api.post(`/turnstiles/${id}/block`),
  unblock: (id) => api.post(`/turnstiles/${id}/unblock`),
  emergencyOpen: () => api.post("/turnstiles/emergency-open"),
  testConnection: (body) => api.post("/turnstiles/test-connection", body),
};

export const accessLogsApi = {
  list: (query) => api.get("/access-logs", query),
  get: (id) => api.get(`/access-logs/${id}`),
};

export const blockedApi = {
  list: (query) => api.get("/blocked", query),
};

export const cameraUsersApi = {
  list: (query) => api.get("/camera-users", query),
  get: (id) => api.get(`/camera-users/${id}`),
  stats: (query) => api.get("/camera-users/stats", query),
  markReviewed: (id, reviewed = true) => api.post(`/camera-users/${id}/reviewed?reviewed=${reviewed}`),
  remove: (id) => api.del(`/camera-users/${id}`),
};

export const reportsApi = {
  get: () => api.get("/reports"),
};

export const settingsApi = {
  getNotifications: () => api.get("/settings/notifications"),
  saveNotifications: (body) => api.put("/settings/notifications", body),
  getIntegrations: () => api.get("/settings/integrations"),
  saveIntegrations: (body) => api.put("/settings/integrations", body),
  getApi: () => api.get("/settings/api"),
  saveWebhook: (body) => api.put("/settings/api/webhook", body),
  regenerateKey: () => api.post("/settings/api/regenerate-key"),
};

export const adminsApi = {
  list: () => api.get("/admins"),
  availableStaff: () => api.get("/admins/available-staff"),
  cameraGroups: () => api.get("/admins/camera-groups"),
  get: (id) => api.get(`/admins/${id}`),
  create: (body) => api.post("/admins", body),
  update: (id, body) => api.put(`/admins/${id}`, body),
  remove: (id) => api.del(`/admins/${id}`),
  resetPassword: (id, body) => api.post(`/admins/${id}/reset-password`, body),
  toggleActive: (id) => api.post(`/admins/${id}/toggle-active`),
  permissions: () => api.get("/admins/permissions"),
};

export const recordingsApi = {
  list: () => api.get("/recordings"),
  camera: (id) => api.get(`/recordings/camera/${id}`),
  downloadUrl: (id, date) => `/api/recordings/camera/${id}/download?date=${encodeURIComponent(date)}`,
};
