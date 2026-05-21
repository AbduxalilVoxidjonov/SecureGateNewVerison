// Yuklash / xato / bo'sh holat komponentlari
import { Icon } from "./Icon";

export const Loading = ({ label = "Yuklanmoqda..." }) => (
  <div className="row" style={{ gap: 10, padding: 40, justifyContent: "center", color: "var(--text-2)" }}>
    <span className="pulse" style={{ width: 9, height: 9, borderRadius: "50%", background: "var(--accent)" }} />
    {label}
  </div>
);

export const ErrorBox = ({ error, onRetry }) => (
  <div className="card padded error-box">
    <Icon name="alert" size={18} />
    <span style={{ flex: 1 }}>{error?.message || "Ma'lumotni yuklashda xatolik."}</span>
    {onRetry && <button className="btn sm" onClick={onRetry}><Icon name="refresh" size={13} /> Qayta urinish</button>}
  </div>
);

export const Empty = ({ label = "Ma'lumot topilmadi", icon = "archive" }) => (
  <div className="col" style={{ alignItems: "center", gap: 10, padding: 48, color: "var(--text-3)" }}>
    <Icon name={icon} size={28} />
    <span style={{ fontSize: 13 }}>{label}</span>
  </div>
);
