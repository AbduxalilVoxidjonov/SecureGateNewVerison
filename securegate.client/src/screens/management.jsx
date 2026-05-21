// Management / Rahbariyat (adminlar) — real API
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar, Modal, Field } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { adminsApi } from "../api/endpoints";

const ManagementScreen = () => {
  const { data, loading, error, reload } = useApi(() => adminsApi.list(), []);
  const [showAdd, setShowAdd] = useState(false);
  const [resetFor, setResetFor] = useState(null);
  const [busy, setBusy] = useState(false);
  const admins = data || [];

  const toggle = async (a) => { setBusy(true); try { await adminsApi.toggleActive(a.id); reload(); } finally { setBusy(false); } };
  const del = async (a) => {
    if (!window.confirm(`${a.fullName} o'chirilsinmi?`)) return;
    setBusy(true); try { await adminsApi.remove(a.id); reload(); } finally { setBusy(false); }
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Rahbariyat</h1>
          <div className="page-sub">{admins.length} ta admin akkaunt · rollar va ruxsatlar</div>
        </div>
        <button className="btn primary" onClick={() => setShowAdd(true)}><Icon name="plus" size={14} /> Yangi admin</button>
      </div>

      {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : admins.length === 0 ? <Empty label="Admin yo'q" icon="crown" /> : (
        <div className="cam-grid" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
          {admins.map((m) => (
            <div key={m.id} className="card padded">
              <div className="row" style={{ gap: 12, marginBottom: 12 }}>
                <Avatar name={m.fullName} size="lg" />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 600 }} className="truncate">{m.fullName}</div>
                  <div className="faint mono truncate" style={{ fontSize: 12 }}>{m.email}</div>
                </div>
                <span className={`pill ${m.isSuperAdmin ? "warn" : "info"}`}>
                  {m.isSuperAdmin && <Icon name="crown" size={10} />} {m.isSuperAdmin ? "Super Admin" : "Admin"}
                </span>
              </div>
              <div className="row" style={{ gap: 14, fontSize: 12.5, color: "var(--text-2)" }}>
                <span><Icon name="key" size={12} style={{ verticalAlign: -2 }} /> {m.permissionCount} ruxsat</span>
                <span>{m.isActive ? <span className="pill on">Faol</span> : <span className="pill err">Bloklangan</span>}</span>
              </div>
              {!m.isSuperAdmin && (
                <div className="row" style={{ gap: 6, marginTop: 14, paddingTop: 12, borderTop: "1px solid var(--border)" }}>
                  <button className="btn sm" style={{ flex: 1 }} disabled={busy} onClick={() => toggle(m)}>
                    <Icon name={m.isActive ? "lock" : "unlock"} size={12} /> {m.isActive ? "Bloklash" : "Faollashtirish"}
                  </button>
                  <button className="btn sm ghost" title="Parolni tiklash" onClick={() => setResetFor(m)}><Icon name="refresh" size={12} /></button>
                  <button className="btn sm ghost" title="O'chirish" disabled={busy} onClick={() => del(m)}><Icon name="trash" size={12} /></button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      <AddAdminModal open={showAdd} onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); reload(); }} />
      <ResetPasswordModal admin={resetFor} onClose={() => setResetFor(null)} />
    </div>
  );
};

const AddAdminModal = ({ open, onClose, onSaved }) => {
  const staff = useApi(() => (open ? adminsApi.availableStaff() : Promise.resolve([])), [open]);
  const groups = useApi(() => (open ? adminsApi.cameraGroups() : Promise.resolve([])), [open]);
  const perms = useApi(() => (open ? adminsApi.permissions() : Promise.resolve([])), [open]);
  const [staffId, setStaffId] = useState("");
  const [password, setPassword] = useState("");
  const [selPerms, setSelPerms] = useState([]);
  const [selGroups, setSelGroups] = useState([]);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState(null);

  const togglePerm = (code) => setSelPerms((p) => p.includes(code) ? p.filter((x) => x !== code) : [...p, code]);
  const toggleGroup = (id) => setSelGroups((g) => g.includes(id) ? g.filter((x) => x !== id) : [...g, id]);

  const save = async () => {
    setBusy(true); setErr(null);
    try {
      await adminsApi.create({
        staffId: staffId ? parseInt(staffId) : null,
        password,
        confirmPassword: password,
        selectedPermissions: selPerms,
        selectedCameraGroupIds: selGroups,
      });
      onSaved();
    } catch (e) { setErr(e.message); } finally { setBusy(false); }
  };

  return (
    <Modal open={open} onClose={onClose} wide title="Yangi admin yaratish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={busy || !staffId || password.length < 8} onClick={save}><Icon name="check" size={14} /> {busy ? "..." : "Yaratish"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        {err && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {err}</div>}
        <div className="grid-2">
          <Field label="Xodim" hint="Admin akkaunti shu xodimga biriktiriladi">
            <select className="select" value={staffId} onChange={(e) => setStaffId(e.target.value)}>
              <option value="">— Xodimni tanlang —</option>
              {(staff.data || []).map((s) => <option key={s.id} value={s.id}>{s.fullName} · {s.position}</option>)}
            </select>
          </Field>
          <Field label="Parol" hint="Kamida 8 belgi"><input className="input" type="password" value={password} onChange={(e) => setPassword(e.target.value)} /></Field>
        </div>

        <div>
          <label style={{ fontSize: 11.5, color: "var(--text-2)", textTransform: "uppercase", letterSpacing: ".04em" }}>Ruxsatlar</label>
          <div className="col" style={{ gap: 10, marginTop: 8 }}>
            {(perms.data || []).map((grp) => (
              <div key={grp.groupName} className="card" style={{ padding: 10, background: "var(--bg-0)" }}>
                <div style={{ fontSize: 12, fontWeight: 600, marginBottom: 6 }}>{grp.groupName}</div>
                <div className="grid-2" style={{ gap: 4 }}>
                  {grp.permissions.map((p) => (
                    <label key={p.code} className="check"><input type="checkbox" checked={selPerms.includes(p.code)} onChange={() => togglePerm(p.code)} /> <span style={{ fontSize: 12.5 }}>{p.label}</span></label>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div>
          <label style={{ fontSize: 11.5, color: "var(--text-2)", textTransform: "uppercase", letterSpacing: ".04em" }}>Kamera guruhlari (bo'sh = barchasi)</label>
          <div className="grid-2" style={{ gap: 6, marginTop: 8 }}>
            {(groups.data || []).map((g) => (
              <label key={g.id} className="check card padded" style={{ padding: 10, cursor: "pointer" }}>
                <input type="checkbox" checked={selGroups.includes(g.id)} onChange={() => toggleGroup(g.id)} /> <span>{g.name}</span>
              </label>
            ))}
          </div>
        </div>
      </div>
    </Modal>
  );
};

const ResetPasswordModal = ({ admin, onClose }) => {
  const [pw, setPw] = useState("");
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState(null);
  const save = async () => {
    setBusy(true); setMsg(null);
    try { await adminsApi.resetPassword(admin.id, { newPassword: pw, confirmPassword: pw }); setMsg({ ok: true, text: "Parol o'zgartirildi" }); setPw(""); }
    catch (e) { setMsg({ ok: false, text: e.message }); } finally { setBusy(false); }
  };
  return (
    <Modal open={!!admin} onClose={onClose} title="Parolni tiklash"
      footer={<>
        <button className="btn" onClick={onClose}>Yopish</button>
        <button className="btn primary" disabled={busy || pw.length < 8} onClick={save}><Icon name="check" size={14} /> Saqlash</button>
      </>}>
      {admin && (
        <div className="col" style={{ gap: 12 }}>
          <div className="faint">{admin.fullName} ({admin.email}) uchun yangi parol</div>
          {msg && <div style={{ fontSize: 13, color: msg.ok ? "var(--accent)" : "var(--danger)" }}>{msg.text}</div>}
          <Field label="Yangi parol" hint="Kamida 8 belgi"><input className="input" type="password" value={pw} onChange={(e) => setPw(e.target.value)} /></Field>
        </div>
      )}
    </Modal>
  );
};

export default ManagementScreen;
