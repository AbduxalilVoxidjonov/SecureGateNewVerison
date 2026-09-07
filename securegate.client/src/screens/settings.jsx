// Settings / Sozlamalar — real API
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar, Field, Toast, Toggle } from "../components/ui";
import { Loading, ErrorBox } from "../components/state";
import { useApi } from "../hooks/useApi";
import useMutation from "../hooks/useMutation";
import { settingsApi, authApi } from "../api/endpoints";
import { useAuth } from "../auth/AuthContext";
import { fmtDateTime, setLoginNotice } from "./utils";

const SaveBar = ({ onSave, busy, okText, error }) => (
  <div className="row" style={{ justifyContent: "flex-end", gap: 10, marginTop: 14 }}>
    {error
      ? <span style={{ fontSize: 12.5, color: "var(--danger)" }}>{error.message}</span>
      : okText && <span style={{ fontSize: 12.5, color: "var(--accent)" }}>{okText}</span>}
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
          <Toggle on={!!form[row.k]} onToggle={() => onToggle(row.k)} label={row.label} />
        </div>
      ))}
    </div>
  </div>
);

// `edits ?? data` patterni — yuklangan ma'lumotni effectsiz tahrirlash uchun.
// MUHIM: muvaffaqiyatli saqlashdan (yoki qayta yuklashdan) keyin `edits` TOZALANADI,
// aks holda eski tahrirlar serverdan kelgan yangi qiymatlar ustidan g'olib chiqib qolardi.
function useEditable(fetchFn) {
  const q = useApi(fetchFn, []);
  const [edits, setEdits] = useState(null);
  const form = edits ?? q.data;
  const patch = (p) => setEdits({ ...(edits ?? q.data ?? {}), ...p });
  const refresh = () => { setEdits(null); q.reload(); };
  return { ...q, form, patch, setEdits, refresh, dirty: edits !== null };
}

// ---- Notifications ----
const NotificationsSection = () => {
  const { loading, error, form, patch, refresh } = useEditable(() => settingsApi.getNotifications());
  const [saved, setSaved] = useState(false);
  const save = useMutation(
    (body) => settingsApi.saveNotifications(body),
    { onSuccess: () => { setSaved(true); refresh(); } }
  );
  if (loading) return <Loading />;
  if (error) return <ErrorBox error={error} onRetry={refresh} />;
  if (!form) return null;
  const toggle = (k) => { setSaved(false); patch({ [k]: !form[k] }); };
  const onSave = () => { setSaved(false); save.run(form); };
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
          <Field label="Qabul qiluvchi email"><input className="input mono" value={form.recipientEmail || ""} onChange={(e) => { setSaved(false); patch({ recipientEmail: e.target.value }); }} /></Field>
          <Field label="Qabul qiluvchi telefon"><input className="input mono" value={form.recipientPhone || ""} onChange={(e) => { setSaved(false); patch({ recipientPhone: e.target.value }); }} /></Field>
        </div>
        <SaveBar onSave={onSave} busy={save.busy} okText={saved ? "Saqlandi" : null} error={save.error} />
      </div>
      <Toast message={save.error?.message} kind="error" onClose={save.reset} />
    </div>
  );
};

// ---- Integrations ----
// Backend sirlarni MASKALANGAN holda qaytaradi ("••••1234") + `hasXxx` bayroqlari bilan.
// Maskalangan/bo'sh qiymat qaytib yuborilsa — server eski sirni saqlab qoladi.
const SecretField = ({ label, value, has, onChange }) => (
  <Field label={label} hint="O'zgartirmasangiz — eski qiymat saqlanadi">
    <div className="col" style={{ gap: 4 }}>
      <input className="input mono" value={value || ""} onChange={onChange} placeholder="••••••••" />
      {has !== undefined && (
        <span className={`pill ${has ? "on" : "off"}`} style={{ alignSelf: "flex-start" }}>
          {has ? "O'rnatilgan" : "O'rnatilmagan"}
        </span>
      )}
    </div>
  </Field>
);

const IntegrationsSection = () => {
  const { loading, error, form, patch, refresh } = useEditable(() => settingsApi.getIntegrations());
  const [saved, setSaved] = useState(false);
  const save = useMutation(
    (body) => settingsApi.saveIntegrations(body),
    { onSuccess: () => { setSaved(true); refresh(); } }
  );
  if (loading) return <Loading />;
  if (error) return <ErrorBox error={error} onRetry={refresh} />;
  if (!form) return null;
  const set = (k) => (e) => { setSaved(false); patch({ [k]: e.target.value }); };
  const onSave = () => { setSaved(false); save.run(form); };
  return (
    <div className="col" style={{ gap: 14 }}>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>SMTP (Email)</div>
        <div className="grid-2">
          <Field label="SMTP server"><input className="input" value={form.smtpHost || ""} onChange={set("smtpHost")} /></Field>
          <Field label="Port"><input className="input mono" value={form.smtpPort ?? ""} onChange={(e) => { setSaved(false); patch({ smtpPort: e.target.value ? parseInt(e.target.value) : null }); }} /></Field>
          <Field label="Foydalanuvchi"><input className="input" value={form.smtpUsername || ""} onChange={set("smtpUsername")} /></Field>
          <SecretField label="Parol" value={form.smtpPassword} has={form.hasSmtpPassword} onChange={set("smtpPassword")} />
          <Field label="Yuboruvchi email"><input className="input mono" value={form.smtpFromEmail || ""} onChange={set("smtpFromEmail")} /></Field>
          <label className="check" style={{ alignSelf: "end", paddingBottom: 8 }}><input type="checkbox" checked={!!form.smtpUseSsl} onChange={(e) => { setSaved(false); patch({ smtpUseSsl: e.target.checked }); }} /> SSL/TLS</label>
        </div>
      </div>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>Telegram</div>
        <div className="grid-2">
          <SecretField label="Bot tokeni" value={form.telegramBotToken} has={form.hasTelegramBotToken} onChange={set("telegramBotToken")} />
          <Field label="Chat ID"><input className="input mono" value={form.telegramChatId || ""} onChange={set("telegramChatId")} /></Field>
        </div>
      </div>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>SMS gateway</div>
        <div className="grid-2">
          <Field label="Provayder"><input className="input" value={form.smsProvider || ""} onChange={set("smsProvider")} /></Field>
          <Field label="API URL"><input className="input mono" value={form.smsApiUrl || ""} onChange={set("smsApiUrl")} /></Field>
          <SecretField label="API kalit" value={form.smsApiKey} has={form.hasSmsApiKey} onChange={set("smsApiKey")} />
          <Field label="Yuboruvchi nomi"><input className="input" value={form.smsSender || ""} onChange={set("smsSender")} /></Field>
        </div>
        <SaveBar onSave={onSave} busy={save.busy} okText={saved ? "Saqlandi" : null} error={save.error} />
      </div>
      <Toast message={save.error?.message} kind="error" onClose={save.reset} />
    </div>
  );
};

// ---- API / Webhook ----
// GET /api/settings/api endi kalitni AVTOMATIK yaratmaydi (apiKey: null, hasApiKey: false bo'lishi mumkin).
// Kalit faqat POST /api/settings/api/regenerate-key orqali yaratiladi.
const ApiSection = () => {
  const { loading, error, form, patch, refresh } = useEditable(() => settingsApi.getApi());
  const [saved, setSaved] = useState(false);
  const save = useMutation(
    (body) => settingsApi.saveWebhook(body),
    { onSuccess: () => { setSaved(true); refresh(); } }
  );
  // Natijani argument sifatida kutmaymiz — yangi kalitni GET qayta yuklab beradi.
  const regen = useMutation(() => settingsApi.regenerateKey(), { onSuccess: refresh });

  if (loading) return <Loading />;
  if (error) return <ErrorBox error={error} onRetry={refresh} />;
  if (!form) return null;

  const set = (k) => (e) => { setSaved(false); patch({ [k]: e.target.value }); };
  const tg = (k) => (e) => { setSaved(false); patch({ [k]: e.target.checked }); };
  const onSave = () => { setSaved(false); save.run(form); };

  const hasKey = form.hasApiKey !== undefined ? !!form.hasApiKey : !!form.apiKey;

  return (
    <div className="col" style={{ gap: 14 }}>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 4 }}>API kaliti</div>
        <div className="muted" style={{ fontSize: 12, marginBottom: 12 }}>Tashqi tizimlar bilan integratsiya uchun</div>
        <div className="card" style={{ background: "var(--bg-0)", padding: 12 }}>
          {hasKey ? (
            <>
              <div className="row" style={{ justifyContent: "space-between", gap: 8 }}>
                <div className="mono" style={{ fontSize: 13, wordBreak: "break-all" }}>{form.apiKey || "••••••••"}</div>
                <button className="btn xs ghost" disabled={regen.busy} onClick={() => { if (window.confirm("Eski kalit ishlamay qoladi. Yangi kalit yaratilsinmi?")) regen.run(); }} title="Qayta yaratish">
                  <Icon name="refresh" size={12} />
                </button>
              </div>
              {form.apiKeyCreatedAt && <div className="faint" style={{ fontSize: 11.5, marginTop: 4 }}>Yaratilgan: {fmtDateTime(form.apiKeyCreatedAt)}</div>}
            </>
          ) : (
            <div className="row" style={{ justifyContent: "space-between", gap: 10, flexWrap: "wrap" }}>
              <span className="faint" style={{ fontSize: 12.5 }}>API kalit hali yaratilmagan.</span>
              <button className="btn primary sm" disabled={regen.busy} onClick={() => regen.run()}>
                <Icon name="key" size={13} /> {regen.busy ? "Yaratilmoqda..." : "Kalit yaratish"}
              </button>
            </div>
          )}
          {regen.error && <div className="row" style={{ gap: 8, marginTop: 8, color: "var(--danger)", fontSize: 12.5 }}><Icon name="alert" size={13} /> {regen.error.message}</div>}
        </div>
      </div>
      <div className="card padded">
        <div style={{ fontWeight: 600, marginBottom: 12 }}>Webhook</div>
        <Field label="Webhook URL"><input className="input mono" value={form.webhookUrl || ""} onChange={set("webhookUrl")} placeholder="https://..." /></Field>
        <div style={{ height: 10 }} />
        <Field label="Maxfiy kalit (HMAC)" hint="O'zgartirmasangiz — eski qiymat saqlanadi"><input className="input mono" value={form.webhookSecret || ""} onChange={set("webhookSecret")} placeholder="••••••••" /></Field>
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
        <SaveBar onSave={onSave} busy={save.busy} okText={saved ? "Saqlandi" : null} error={save.error} />
      </div>
      <Toast message={save.error?.message || regen.error?.message} kind="error" onClose={() => { save.reset(); regen.reset(); }} />
    </div>
  );
};

// ---- Profile ----
const ProfileSection = () => {
  const { user, logout } = useAuth();
  const [cur, setCur] = useState("");
  const [next, setNext] = useState("");
  const [done, setDone] = useState(false);
  // Parol o'zgargach backend BARCHA eski tokenlarni bekor qiladi —
  // sessiyani yopib, login ekraniga qaytaramiz (xabar login.jsx da ko'rsatiladi).
  const change = useMutation(
    (a, b) => authApi.changePassword(a, b),
    {
      onSuccess: () => {
        setDone(true); setCur(""); setNext("");
        setLoginNotice("Parol o'zgartirildi — yangi parol bilan qaytadan kiring.");
        logout();
      },
    }
  );
  const onSave = () => { setDone(false); change.run(cur, next); };
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
          <Field label="Joriy parol"><input className="input" type="password" autoComplete="current-password" value={cur} onChange={(e) => { setDone(false); setCur(e.target.value); }} /></Field>
          <Field label="Yangi parol"><input className="input" type="password" autoComplete="new-password" value={next} onChange={(e) => { setDone(false); setNext(e.target.value); }} /></Field>
        </div>
        <SaveBar onSave={onSave} busy={change.busy} okText={done ? "Parol o'zgartirildi — qaytadan kirish..." : null} error={change.error} />
      </div>
      <Toast message={change.error?.message} kind="error" onClose={change.reset} />
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
