// Markaziy HTTP klient — JWT Bearer, ApiResponse o'rovini ochish, xatolarni boshqarish.
const TOKEN_KEY = "sg.token";

export const getToken = () => {
  try { return localStorage.getItem(TOKEN_KEY); } catch { return null; }
};

export const setToken = (token) => {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  } catch { /* ignore */ }
};

let unauthorizedHandler = null;
export const setUnauthorizedHandler = (fn) => { unauthorizedHandler = fn; };

export class ApiError extends Error {
  constructor(message, status, errors) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.errors = errors || null;
  }
}

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

async function request(method, path, { body, query, isForm } = {}) {
  const headers = {};
  const token = getToken();
  if (token) headers["Authorization"] = `Bearer ${token}`;

  let payload;
  if (isForm) {
    payload = body; // FormData — Content-Type'ni brauzer o'zi qo'yadi (boundary bilan)
  } else if (body !== undefined) {
    headers["Content-Type"] = "application/json";
    payload = JSON.stringify(body);
  }

  const url = `/api${path}${buildQuery(query)}`;
  const res = await fetch(url, { method, headers, body: payload });

  if (res.status === 401) {
    setToken(null);
    if (unauthorizedHandler) unauthorizedHandler();
    throw new ApiError("Avtorizatsiya talab qilinadi.", 401);
  }

  const text = await res.text();
  let json = null;
  if (text) {
    try { json = JSON.parse(text); } catch { json = null; }
  }

  // ApiResponse o'rovi: { success, message, data, errors }
  if (json && typeof json.success === "boolean") {
    if (!json.success) throw new ApiError(json.message || "So'rov bajarilmadi.", res.status, json.errors);
    return json.data;
  }

  if (!res.ok) throw new ApiError(`HTTP ${res.status}`, res.status);
  return json;
}

export const api = {
  get: (path, query) => request("GET", path, { query }),
  post: (path, body) => request("POST", path, { body }),
  put: (path, body) => request("PUT", path, { body }),
  del: (path) => request("DELETE", path),
  postForm: (path, formData) => request("POST", path, { body: formData, isForm: true }),
  putForm: (path, formData) => request("PUT", path, { body: formData, isForm: true }),
};
