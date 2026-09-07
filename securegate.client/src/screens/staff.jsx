// Staff / Xodimlar — real API (StaffController: /api/staff)
// Bu ekran bo'lmasa UI orqali birinchi adminni yaratib bo'lmaydi:
// "Rahbariyat" ekrani yangi admin uchun MAVJUD xodimni tanlashni talab qiladi.
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar, Modal, Field, Toast } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import useMutation from "../hooks/useMutation";
import { staffApi } from "../api/endpoints";
import { fmtDateTime } from "./utils";

// Backend enum'lari string sifatida keladi/yuboriladi (JsonStringEnumConverter).
const DEPARTMENTS = [
  ["Administration", "Direksiya"],
  ["Accounting", "Hisobxona"],
  ["Technical", "Texnik xizmat"],
  ["Kitchen", "Oshxona"],
  ["Security", "Qo'riqlash"],
  ["Medical", "Tibbiyot"],
];
const SHIFTS = [
  ["Day", "Kunduzgi (08:00-17:00)"],
  ["Night", "Tungi (20:00-08:00)"],
  ["FullTime", "24/7"],
];
const ACCESS_LEVELS = [
  ["Standard", "Standart"],
  ["High", "Yuqori"],
  ["Full", "To'liq"],
];
const STATUSES = [
  ["Active", "Faol"],
  ["OnLeave", "Ta'tilda"],
  ["Dismissed", "Ishdan bo'shatilgan"],
];

const label = (pairs, v) => (pairs.find(([k]) => k === v) || [null, v || "—"])[1];
const statusPill = { Active: "on", OnLeave: "warn", Dismissed: "err" };

const PAGE_SIZE = 10;

const StaffScreen = () => {
  const { data, loading, error, reload } = useApi(() => staffApi.list(), []);
  const [qInput, setQInput] = useState("");
  const [search, setSearch] = useState("");
  const [department, setDepartment] = useState("");
  const [page, setPage] = useState(1);
  const [showAdd, setShowAdd] = useState(false);
  const [edit, setEdit] = useState(null);

  const remove = useMutation((id) => staffApi.remove(id), { onSuccess: reload });

  // Backend GET /api/staff sahifalashsiz to'liq ro'yxat qaytaradi — filtr/sahifalash mijozda.
  const all = Array.isArray(data) ? data : [];
  const q = search.trim().toLowerCase();
  const filtered = all.filter((s) => {
    if (department && s.department !== department) return false;
    if (!q) return true;
    return `${s.fullName || ""} ${s.position || ""} ${s.phone || ""}`.toLowerCase().includes(q);
  });
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const items = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  const applySearch = (e) => { e.preventDefault(); setPage(1); setSearch(qInput); };

  const doDelete = (s) => {
    if (!window.confirm(`${s.fullName} o'chirilsinmi?`)) return;
    remove.run(s.id);
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Xodimlar</h1>
          <div className="page-sub">{all.length} ta xodim · admin akkauntlari shu ro'yxatdan biriktiriladi</div>
        </div>
        <button className="btn primary" onClick={() => setShowAdd(true)}><Icon name="plus" size={14} /> Yangi xodim</button>
      </div>

      {remove.error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorBox error={remove.error} onRetry={remove.reset} />
        </div>
      )}

      <form onSubmit={applySearch} className="card" style={{ padding: 12, marginBottom: 14, display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
        <div className="search" style={{ position: "relative", minWidth: 280 }}>
          <Icon name="search" size={14} />
          <input value={qInput} onChange={(e) => setQInput(e.target.value)} placeholder="F.I.O, lavozim yoki telefon..." />
        </div>
        <select className="select" value={department} onChange={(e) => { setPage(1); setDepartment(e.target.value); }}>
          <option value="">Barcha bo'limlar</option>
          {DEPARTMENTS.map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <button className="btn" type="submit"><Icon name="search" size={13} /> Qidirish</button>
      </form>

      <div className="card">
        {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : items.length === 0 ? <Empty label="Xodim topilmadi" icon="users" /> : (
          <table className="tbl">
            <thead>
              <tr><th>Xodim</th><th>Lavozim</th><th>Bo'lim</th><th>Smena</th><th>Telefon</th><th>Kirish darajasi</th><th>Yuz tanish</th><th>Status</th><th style={{ width: 110 }}></th></tr>
            </thead>
            <tbody>
              {items.map((s) => (
                <tr key={s.id}>
                  <td>
                    <div className="row" style={{ gap: 10 }}>
                      <Avatar name={s.fullName} />
                      <div>
                        <div style={{ fontWeight: 500 }}>{s.fullName}</div>
                        <div className="faint mono" style={{ fontSize: 11 }}>#{String(s.id).padStart(4, "0")}</div>
                      </div>
                    </div>
                  </td>
                  <td>{s.position || "—"}</td>
                  <td>{label(DEPARTMENTS, s.department)}</td>
                  <td className="faint" style={{ fontSize: 12.5 }}>{label(SHIFTS, s.shift)}</td>
                  <td className="mono">{s.phone || "—"}</td>
                  <td>{label(ACCESS_LEVELS, s.accessLevel)}</td>
                  <td>{s.faceRecognitionEnabled ? <span className="pill on">Yoqilgan</span> : <span className="pill off">O'chiq</span>}</td>
                  <td><span className={`pill ${statusPill[s.status] || "off"}`}>{label(STATUSES, s.status)}</span></td>
                  <td>
                    <div className="row" style={{ gap: 4 }}>
                      <button className="btn xs ghost" title="Tahrirlash" onClick={() => setEdit(s)}><Icon name="edit" size={12} /></button>
                      <button className="btn xs ghost" title="O'chirish" disabled={remove.busy} onClick={() => doDelete(s)}><Icon name="trash" size={12} /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {totalPages > 1 && (
          <div className="row" style={{ justifyContent: "space-between", padding: "12px 16px", borderTop: "1px solid var(--border)" }}>
            <span className="muted" style={{ fontSize: 12 }}>{safePage} / {totalPages} sahifa · {filtered.length} ta natija</span>
            <div className="row" style={{ gap: 4 }}>
              <button className="btn xs" disabled={safePage <= 1} onClick={() => setPage(safePage - 1)}>‹ Oldingi</button>
              <button className="btn xs" disabled={safePage >= totalPages} onClick={() => setPage(safePage + 1)}>Keyingi ›</button>
            </div>
          </div>
        )}
      </div>

      {showAdd && <AddStaffModal onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); setPage(1); reload(); }} />}
      {edit && <EditStaffModal key={edit.id} staff={edit} onClose={() => setEdit(null)} onSaved={() => { setEdit(null); reload(); }} />}

      <Toast message={remove.error?.message} kind="error" onClose={remove.reset} />
    </div>
  );
};

// ---- Add (multipart/form-data → StaffCreateViewModel) ----
// Shartli render — modal har ochilganda toza mount bo'ladi.
const AddStaffModal = ({ onClose, onSaved }) => {
  const [f, setF] = useState({ fullName: "", position: "", department: "Administration", shift: "Day", phone: "", accessLevel: "Standard" });
  const [photo, setPhoto] = useState(null);
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value });

  const create = useMutation((fd) => staffApi.create(fd), { onSuccess: () => onSaved() });

  const save = () => {
    const fd = new FormData();
    fd.append("FullName", f.fullName);
    fd.append("Position", f.position);
    fd.append("Department", f.department);
    fd.append("Shift", f.shift);
    fd.append("AccessLevel", f.accessLevel);
    if (f.phone) fd.append("Phone", f.phone);
    if (photo) fd.append("PhotoFile", photo);
    create.run(fd);
  };

  return (
    <Modal open onClose={onClose} wide title="Yangi xodim qo'shish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={create.busy || !f.fullName || !f.position || !photo} onClick={save}>
          <Icon name="check" size={14} /> {create.busy ? "Saqlanmoqda..." : "Saqlash"}
        </button>
      </>}>
      <div style={{ display: "grid", gridTemplateColumns: "200px 1fr", gap: 20 }}>
        <div>
          <label style={{ fontSize: 11.5, color: "var(--text-2)", textTransform: "uppercase", letterSpacing: ".04em" }}>Yuz rasmi</label>
          <label className="placeholder-box" style={{ aspectRatio: "3/4", marginTop: 8, cursor: "pointer" }}>
            <div>
              <Icon name="plus" size={28} color="var(--text-3)" />
              <div style={{ marginTop: 8 }}>{photo ? photo.name : "RASM YUKLANG"}</div>
            </div>
            <input type="file" accept="image/*" style={{ display: "none" }} onChange={(e) => setPhoto(e.target.files?.[0] || null)} />
          </label>
          <div className="faint" style={{ fontSize: 11.5, marginTop: 6 }}>Rasm majburiy — yuz tanish uchun ishlatiladi.</div>
        </div>
        <div className="col" style={{ gap: 12 }}>
          {create.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {create.error.message}</div>}
          <div className="grid-2">
            <Field label="F.I.O"><input className="input" value={f.fullName} onChange={set("fullName")} /></Field>
            <Field label="Lavozim"><input className="input" value={f.position} onChange={set("position")} /></Field>
          </div>
          <div className="grid-2">
            <Field label="Bo'lim"><select className="select" value={f.department} onChange={set("department")}>{DEPARTMENTS.map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
            <Field label="Smena"><select className="select" value={f.shift} onChange={set("shift")}>{SHIFTS.map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
          </div>
          <div className="grid-2">
            <Field label="Telefon"><input className="input mono" value={f.phone} onChange={set("phone")} placeholder="+998 90 ___ __ __" /></Field>
            <Field label="Kirish darajasi"><select className="select" value={f.accessLevel} onChange={set("accessLevel")}>{ACCESS_LEVELS.map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
          </div>
        </div>
      </div>
    </Modal>
  );
};

// ---- Edit (multipart/form-data → StaffEditViewModel) ----
const EditStaffModal = ({ staff, onClose, onSaved }) => {
  const [f, setF] = useState({
    fullName: staff.fullName || "",
    position: staff.position || "",
    department: staff.department || "Administration",
    shift: staff.shift || "Day",
    phone: staff.phone || "",
    accessLevel: staff.accessLevel || "Standard",
    status: staff.status || "Active",
    faceRecognitionEnabled: !!staff.faceRecognitionEnabled,
  });
  const [photo, setPhoto] = useState(null);
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value });

  const update = useMutation((fd) => staffApi.update(staff.id, fd), { onSuccess: () => onSaved() });

  const save = () => {
    const fd = new FormData();
    fd.append("Id", String(staff.id));
    fd.append("FullName", f.fullName);
    fd.append("Position", f.position);
    fd.append("Department", f.department);
    fd.append("Shift", f.shift);
    fd.append("AccessLevel", f.accessLevel);
    fd.append("Status", f.status);
    fd.append("FaceRecognitionEnabled", f.faceRecognitionEnabled ? "true" : "false");
    if (f.phone) fd.append("Phone", f.phone);
    if (staff.photoPath) fd.append("PhotoPath", staff.photoPath);
    // Yangi rasm yuborilmasa — server eskisini saqlab qoladi.
    if (photo) fd.append("PhotoFile", photo);
    update.run(fd);
  };

  return (
    <Modal open onClose={onClose} wide title={`Xodimni tahrirlash · ${staff.fullName}`}
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={update.busy || !f.fullName || !f.position} onClick={save}>
          <Icon name="check" size={14} /> {update.busy ? "Saqlanmoqda..." : "Saqlash"}
        </button>
      </>}>
      <div style={{ display: "grid", gridTemplateColumns: "200px 1fr", gap: 20 }}>
        <div>
          <label style={{ fontSize: 11.5, color: "var(--text-2)", textTransform: "uppercase", letterSpacing: ".04em" }}>Yangi yuz rasmi</label>
          <label className="placeholder-box" style={{ aspectRatio: "3/4", marginTop: 8, cursor: "pointer" }}>
            <div>
              <Icon name="plus" size={28} color="var(--text-3)" />
              <div style={{ marginTop: 8 }}>{photo ? photo.name : "RASMNI ALMASHTIRISH"}</div>
            </div>
            <input type="file" accept="image/*" style={{ display: "none" }} onChange={(e) => setPhoto(e.target.files?.[0] || null)} />
          </label>
          <div className="faint" style={{ fontSize: 11.5, marginTop: 6 }}>Bo'sh qoldirilsa — joriy rasm saqlanadi.</div>
        </div>
        <div className="col" style={{ gap: 12 }}>
          {update.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {update.error.message}</div>}
          <div className="grid-2">
            <Field label="F.I.O"><input className="input" value={f.fullName} onChange={set("fullName")} /></Field>
            <Field label="Lavozim"><input className="input" value={f.position} onChange={set("position")} /></Field>
          </div>
          <div className="grid-2">
            <Field label="Bo'lim"><select className="select" value={f.department} onChange={set("department")}>{DEPARTMENTS.map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
            <Field label="Smena"><select className="select" value={f.shift} onChange={set("shift")}>{SHIFTS.map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
          </div>
          <div className="grid-2">
            <Field label="Telefon"><input className="input mono" value={f.phone} onChange={set("phone")} /></Field>
            <Field label="Kirish darajasi"><select className="select" value={f.accessLevel} onChange={set("accessLevel")}>{ACCESS_LEVELS.map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
          </div>
          <div className="grid-2">
            <Field label="Holat"><select className="select" value={f.status} onChange={set("status")}>{STATUSES.map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
            <label className="check" style={{ alignSelf: "end", paddingBottom: 8 }}>
              <input type="checkbox" checked={f.faceRecognitionEnabled} onChange={(e) => setF({ ...f, faceRecognitionEnabled: e.target.checked })} /> Yuz tanishni yoqish
            </label>
          </div>
          <div className="row faint" style={{ gap: 14, fontSize: 11.5 }}>
            <span>Yaratilgan: {fmtDateTime(staff.createdAt)}</span>
            <span>So'nggi o'tish: {fmtDateTime(staff.lastAccessTime)}</span>
          </div>
        </div>
      </div>
    </Modal>
  );
};

export default StaffScreen;
