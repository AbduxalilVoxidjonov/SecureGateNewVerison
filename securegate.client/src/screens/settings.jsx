// Settings / Sozlamalar — real API
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar, Field } from "../components/ui";
import { Loading, ErrorBox } from "../components/state";
import { useApi } from "../hooks/useApi";
import { settingsApi, authApi } from "../api/endpoints";
import { useAuth } from "../auth/AuthContext";

const Toggle = ({ on, onToggle }) => (
  <div className={`toggle ${on ? "on" : ""}`} onClick={onToggle} role="switch" aria-checked={on} />
);

const SaveBar = ({ onSave, busy, msg }) => (
  <div className="row" style={{ justifyContent: "flex-end", gap: 10, marginTop: 14 }}>
    {msg && <span style={{ fontSize: 12.5, color: msg.ok ? "var(--accent)" : "var(--danger)" }}>{msg.text}</span>}
    <button className="btn primary" onClick={onSave} disabled={busy}>
      <Icon name="check" size={14} /> {busy ? "Saqlanmoqda..." : "Saqlash"}
    </button>
  </div>
);

const ToggleBlock = ({ title, icon, rows, form, onToggle }) => (
  <div className="card">
    <div className="card-h">
      <h3><Icon name={icon} size={14} style={{ verticalAlign: -2, marginRight: 6, color: "var(--text-2)" }} /> {title}</h3>
    </div>
    <div style={{ padding: "4px 16px" }}>
      {rows.map((row) => (
        <div key={row.k} className="row" style={{ justifyContent: "space-between", padding: "10px 0", borderTop: "1px solid var(--border)" }}>
          <span style={{ fontSize: 13 }}>{row.label}</span>
          <Toggle on={!!form[row.k]} onToggle={() => onToggle(row.k)} />
        </div>
      ))}
    </div>
  </div>
);

// `edits ?? data` patterni — yuklangan ma'lumotni effectsiz tahrirlash uchun.
function useEditable(fetchFn) {
  const q = useApi(fetchFn, []);
  const [edits, setEdits] = useState(null);
  const form = edits ?? q.data;
  const patch = (p) => setEdits({ ...(edits ?? q.data ?? {}), ...p });
  return { ...q, form, patch, setEdits };
}

// ---- Notifications ----
const NotificationsSection = () => {
  const { loading, error, reload, form, patch } = useEditable(() => settingsApi.getNotifications());
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState(null);
  if (loading) return <Loading />;
  if (error) return <ErrorBox error={error} onRetry={reload} />;
  if (!form) return null;
  const toggle = (k) => patch({ [k]: !form[k] });
  const save = async () => {
    setBusy(true); setMsg(null);
    try { await settingsApi.saveNotifications(form); setMsg({ ok: true, text: "Saqlandi" }); }
    catch (e) { setMsg({ ok: false, text: e.message }); } finally { setBusy(false); }
  };
  return (
    <div className="col" style={{ gap: 14 }}>
      <ToggleBlock title="Kanallar" icon="bell" form={form} onToggle={toggle} rows={[
        { k: "inAppEnabled", label: "Brauzer ichidagi bildirishnomalar" },
        { k: "emailEnabled", label: "Email orqali" },
        { k: "smsEnabled", label: "SMS orqali" },
        { k: "telegramEnabled", label: "Telegram orqali" },
        { k: "soundEnabled", label: "Tovushli ogohlantirish" },
      ]} />
      <ToggleBlock title="Hodisalar" icon="flame" form={form} onToggle={toggle} rows={[
        { k: "notifyOnDenied", label: "Ruxsatsiz kirish urinishi" },
        { k: "notifyOnBlocked", label: "Bloklangan foydalanuvchi urinishi" },
        { k: "notifyOnCameraOffline", label: "Kamera oflayn bo'lganda" },
        { k: "notifyOnTurnstileError", label: "Turniket nosozligi" },
        { k: "notifyOnUserCreated", label: "Yangi foydalanuvchi yaratilganda" },
      ]} />
      <div className="card padded">
        <div className="grid-2">
          <Field label="Qabul qiluvchi email"><input className="input mono" value={form.recipientEmail || ""} onChange={(e) => patch({ recipientEmail: e.target.value })} /></Field>
          <Field label="Qabul qiluvchi telefon"><input className="input mono" value={form.recipientPhone || ""} onChange={(e) => patch({ recipientPhone: e.target.value })} /></Field>
        </div>
        <SaveBar onSave={save} busy={busy} msg={msg} />
      </div>
    </div>
  );
};

// ---- Integrations ----
const IntegrationsSection = () => {
  const { loading, error, reload, form, patch } = useEditable(() => settingsApi.getIntegrations());
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState(null);
  if (loading) return <Loading />;
  if (error) return <ErrorBox error={error} onRetry={reload} />;
  if (!form) return null;
  const set = (k) => (e) => patch({ [k]: e.target.value });
  const save = async () => {
    setBusy(true); setMsg(null);
    try { await settingsApi.saveIntegrations(form); setMsg({ ok: true, text: "Saqlandi" }); }
    catch (e) { setMsg({ ok: false, text: e.message }); } finally { setBusy(false); }
  };
  return (
    <div className="col" style={{ gap: 14 }}>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>SMTP (Email)</div>
        <div className="grid-2">
          <Field label="SMTP server"><input className="input" value={form.smtpHost || ""} onChange={set("smtpHost")} /></Field>
          <Field label="Port"><input className="input mono" value={form.smtpPort ?? ""} onChange={(e) => patch({ smtpPort: e.target.value ? parseInt(e.target.value) : null })} /></Field>
          <Field label="Foydalanuvchi"><input className="input" value={form.smtpUsername || ""} onChange={set("smtpUsername")} /></Field>
          <Field label="Parol"><input className="input" type="password" value={form.smtpPassword || ""} onChange={set("smtpPassword")} /></Field>
          <Field label="Yuboruvchi email"><input className="input mono" value={form.smtpFromEmail || ""} onChange={set("smtpFromEmail")} /></Field>
          <label className="check" style={{ alignSelf: "end", paddingBottom: 8 }}><input type="checkbox" checked={!!form.smtpUseSsl} onChange={(e) => patch({ smtpUseSsl: e.target.checked })} /> SSL/TLS</label>
        </div>
      </div>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>Telegram</div>
        <div className="grid-2">
          <Field label="Bot tokeni"><input className="input mono" value={form.telegramBotToken || ""} onChange={set("telegramBotToken")} /></Field>
          <Field label="Chat ID"><input className="input mono" value={form.telegramChatId || ""} onChange={set("telegramChatId")} /></Field>
        </div>
      </div>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>SMS gateway</div>
        <div className="grid-2">
          <Field label="Provayder"><input className="input" value={form.smsProvider || ""} onChange={set("smsProvider")} /></Field>
          <Field label="API URL"><input className="input mono" value={form.smsApiUrl || ""} onChange={set("smsApiUrl")} /></Field>
          <Field label="API kalit"><input className="input mono" type="password" value={form.smsApiKey || ""} onChange={set("smsApiKey")} /></Field>
          <Field label="Yuboruvchi nomi"><input className="input" value={form.smsSender || ""} onChange={set("smsSender")} /></Field>
        </div>
        <SaveBar onSave={save} busy={busy} msg={msg} />
      </div>
    </div>
  );
};

// ---- API / Webhook ----
const ApiSection = () => {
  const { loading, error, reload, form, patch } = useEditable(() => settingsApi.getApi());
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState(null);
  if (loading) return <Loading />;
  if (error) return <ErrorBox error={error} onRetry={reload} />;
  if (!form) return null;
  const set = (k) => (e) => patch({ [k]: e.target.value });
  const tg = (k) => (e) => patch({ [k]: e.target.checked });
  const save = async () => {
    setBusy(true); setMsg(null);
    try { await settingsApi.saveWebhook(form); setMsg({ ok: true, text: "Saqlandi" }); }
    catch (e) { setMsg({ ok: false, text: e.message }); } finally { setBusy(false); }
  };
  const regenerate = async () => {
    const res = await settingsApi.regenerateKey();
    patch({ apiKey: res.apiKey, apiKeyCreatedAt: res.createdAt });
  };
  return (
    <div className="col" style={{ gap: 14 }}>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 4 }}>API kaliti</div>
        <div className="muted" style={{ fontSize: 12, marginBottom: 12 }}>Tashqi tizimlar bilan integratsiya uchun</div>
        <div className="card" style={{ background: "var(--bg-0)", padding: 12 }}>
          <div className="row" style={{ justifyContent: "space-between", gap: 8 }}>
            <div className="mono" style={{ fontSize: 13, wordBreak: "break-all" }}>{form.apiKey}</div>
            <button className="btn xs ghost" onClick={regenerate} title="Qayta yaratish"><Icon name="refresh" size={12} /></button>
          </div>
          {form.apiKeyCreatedAt && <div className="faint" style={{ fontSize: 11.5, marginTop: 4 }}>Yaratilgan: {new Date(form.apiKeyCreatedAt).toLocaleString("uz-UZ")}</div>}
        </div>
      </div>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>Webhook</div>
        <Field label="Webhook URL"><input className="input mono" value={form.webhookUrl || ""} onChange={set("webhookUrl")} placeholder="https://..." /></Field>
        <div style={{ height: 10 }} />
        <Field label="Maxfiy kalit (HMAC)"><input className="input mono" value={form.webhookSecret || ""} onChange={set("webhookSecret")} /></Field>
        <label className="check" style={{ marginTop: 10 }}><input type="checkbox" checked={!!form.webhookEnabled} onChange={tg("webhookEnabled")} /> Webhookni yoqish</label>
        <div style={{ marginTop: 12, fontSize: 11.5, color: "var(--text-2)", textTransform: "uppercase", letterSpacing: ".04em" }}>Hodisalar</div>
        <div className="grid-2" style={{ gap: 6, marginTop: 8 }}>
          {[
            ["subscribeAccessGranted", "access.granted"],
            ["subscribeAccessDenied", "access.denied"],
            ["subscribeCameraOffline", "camera.offline"],
            ["subscribeTurnstileError", "turnstile.error"],
            ["subscribeUserBlocked", "user.blocked"],
          ].map(([k, label]) => (
            <label key={k} className="check"><input type="checkbox" checked={!!form[k]} onChange={tg(k)} /> <span className="mono" style={{ fontSize: 12 }}>{label}</span></label>
          ))}
        </div>
        <SaveBar onSave={save} busy={busy} msg={msg} />
      </div>
    </div>
  );
};

// ---- Profile ----
const ProfileSection = () => {
  const { user } = useAuth();
  const [cur, setCur] = useState("");
  const [next, setNext] = useState("");
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState(null);
  const change = async () => {
    setBusy(true); setMsg(null);
    try { await authApi.changePassword(cur, next); setMsg({ ok: true, text: "Parol o'zgartirildi" }); setCur(""); setNext(""); }
    catch (e) { setMsg({ ok: false, text: e.message }); } finally { setBusy(false); }
  };
  return (
    <div className="card padded">
      <div className="row" style={{ gap: 16, marginBottom: 18 }}>
        <Avatar name={user.fullName} size="lg" />
        <div>
          <div style={{ fontWeight: 600, fontSize: 16 }}>{user.fullName}</div>
          <div className="muted" style={{ fontSize: 12 }}>{user.email} · {(user.roles || []).join(", ")}</div>
        </div>
      </div>
      <div className="grid-2" style={{ gap: 14 }}>
        <Field label="Email"><input className="input mono" value={user.email} readOnly /></Field>
        <Field label="Rol"><input className="input" value={(user.roles || []).join(", ")} readOnly /></Field>
      </div>
      <div style={{ borderTop: "1px solid var(--border)", marginTop: 18, paddingTop: 18 }}>
        <div style={{ fontWeight: 600, marginBottom: 10 }}>Parolni o'zgartirish</div>
        <div className="grid-2" style={{ gap: 14 }}>
          <Field label="Joriy parol"><input className="input" type="password" value={cur} onChange={(e) => setCur(e.target.value)} /></Field>
          <Field label="Yangi parol"><input className="input" type="password" value={next} onChange={(e) => setNext(e.target.value)} /></Field>
        </div>
        <SaveBar onSave={change} busy={busy} msg={msg} />
      </div>
    </div>
  );
};

const SECTIONS = [
  { k: "notifications", label: "Bildirishnomalar", icon: "bell", c: NotificationsSection },
  { k: "integrations", label: "Integratsiyalar", icon: "webhook", c: IntegrationsSection },
  { k: "api", label: "API va Webhooks", icon: "api", c: ApiSection },
  { k: "profile", label: "Profil", icon: "user", c: ProfileSection },
];

const SettingsScreen = () => {
  const [section, setSection] = useState("notifications");
  const Section = (SECTIONS.find((s) => s.k === section) || SECTIONS[0]).c;
  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Sozlamalar</h1>
          <div className="page-sub">Bildirishnoma, integratsiya, API va profil</div>
        </div>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "220px 1fr", gap: 16 }}>
        <div className="card" style={{ padding: 6, height: "fit-content" }}>
          {SECTIONS.map((s) => (
            <div key={s.k} className={`nav-item ${section === s.k ? "active" : ""}`} onClick={() => setSection(s.k)}>
              <Icon name={s.icon} size={15} /><span>{s.label}</span>
            </div>
          ))}
        </div>
        <div><Section /></div>
      </div>
    </div>
  );
};

export default SettingsScreen;
