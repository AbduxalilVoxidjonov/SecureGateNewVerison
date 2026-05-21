// Mavzu (yorug'/qorong'i) boshqaruvi — <html data-theme="..."> orqali.
// Default: qorong'i (dark). localStorage'da `sg.theme` kalitida saqlanadi.
const KEY = "sg.theme";

export const getTheme = () => {
  try { return localStorage.getItem(KEY) === "light" ? "light" : "dark"; } catch { return "dark"; }
};

export const applyTheme = (t) => {
  document.documentElement.dataset.theme = t;
};

export const setTheme = (t) => {
  try { localStorage.setItem(KEY, t); } catch { /* ignore */ }
  applyTheme(t);
};

// Modul import qilinishi bilan darhol qo'llaymiz — sahifa "miltillashi" (flash) bo'lmasligi uchun.
applyTheme(getTheme());
