// Ekranlar uchun umumiy yordamchilar: vaqt formatlash, raqam guard, barqaror ro'yxat kalitlari.
// Backend endi hamma joyda ISO-8601 UTC yuboradi ("2026-09-07T12:34:56.0000000Z"),
// lekin eski javoblar tayyor "HH:mm:ss" satr bo'lishi mumkin — ikkalasi ham qo'llab-quvvatlanadi.

const LOCALE = "uz-UZ";

const parseDate = (v) => {
  if (v === null || v === undefined || v === "") return null;
  const d = v instanceof Date ? v : new Date(v);
  return Number.isNaN(d.getTime()) ? null : d;
};

// Faqat vaqt: 12:34:56
export const fmtTime = (v, fallback = "—") => {
  const d = parseDate(v);
  if (d) return d.toLocaleTimeString(LOCALE);
  return typeof v === "string" && v ? v : fallback;
};

// Sana + vaqt
export const fmtDateTime = (v, fallback = "—") => {
  const d = parseDate(v);
  if (d) return d.toLocaleString(LOCALE);
  return typeof v === "string" && v ? v : fallback;
};

// Faqat sana
export const fmtDate = (v, fallback = "—") => {
  const d = parseDate(v);
  if (d) return d.toLocaleDateString(LOCALE);
  return typeof v === "string" && v ? v : fallback;
};

// "yyyy-MM-dd" — download so'rovi uchun
export const isoDay = (v) => {
  const d = parseDate(v);
  if (d) return d.toISOString().slice(0, 10);
  return typeof v === "string" ? v.slice(0, 10) : "";
};

// Backend son o'rniga string/null qaytarsa ham yiqilmaslik uchun.
export const toNum = (v, fallback = 0) => {
  if (v === null || v === undefined || v === "") return fallback;
  const n = Number(v);
  return Number.isFinite(n) ? n : fallback;
};

// Maydon umuman mavjud va sonli ekanini tekshirish (blokni render qilish/qilmaslik uchun).
export const hasNum = (v) => {
  if (v === null || v === undefined || v === "") return false;
  return Number.isFinite(Number(v));
};

// Backend ViewModel'larida Id yo'q ro'yxatlar uchun barqaror kompozit kalit.
// Bir xil kalit takrorlansa — oxiriga hisoblagich qo'shiladi (React duplicate-key ogohlantirishining oldini oladi).
export const stableKeys = (list, keyOf) => {
  const seen = new Map();
  return list.map((item, i) => {
    const raw = keyOf(item, i);
    const base = raw === undefined || raw === null || raw === "" ? `idx-${i}` : String(raw);
    const n = (seen.get(base) || 0) + 1;
    seen.set(base, n);
    return { item, key: n > 1 ? `${base}#${n}` : base };
  });
};

// Sessiya yopilishidan oldin login ekranida ko'rsatiladigan bir martalik xabar
// (masalan parol o'zgargach barcha tokenlar bekor bo'lganda).
const LOGIN_NOTICE_KEY = "sg.loginNotice";

export const setLoginNotice = (text) => {
  try { sessionStorage.setItem(LOGIN_NOTICE_KEY, text); } catch { /* ignore */ }
};

// O'qiydi va darhol tozalaydi — xabar faqat bir marta ko'rinadi.
export const takeLoginNotice = () => {
  try {
    const v = sessionStorage.getItem(LOGIN_NOTICE_KEY);
    if (v) sessionStorage.removeItem(LOGIN_NOTICE_KEY);
    return v || null;
  } catch { return null; }
};
