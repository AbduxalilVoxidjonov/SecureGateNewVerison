// Users / Foydalanuvchilar (o'quvchilar) — real API
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Avatar, StatusPill, Modal, Field, Toast } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import useMutation from "../hooks/useMutation";
import { usersApi } from "../api/endpoints";

const genderLabel = { Male: "Erkak", Female: "Ayol" };

const UsersScreen = () => {
  const [qInput, setQInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [showAdd, setShowAdd] = useState(false);
  const [blockUser, setBlockUser] = useState(null);

  const { data, loading, error, reload } = useApi(
    () => usersApi.list({ search: search || undefined, status: status || undefined, page, pageSize: 10 }),
    [search, status, page]
  );

  const items = data?.items || [];
  const totalPages = data?.totalPages || 1;

  const unblock = useMutation((id) => usersApi.unblock(id), { onSuccess: reload });
  const remove = useMutation((id) => usersApi.remove(id), { onSuccess: reload });

  const busy = unblock.busy || remove.busy;
  const mutError = unblock.error || remove.error;
  const clearMutError = () => { unblock.reset(); remove.reset(); };

  const applySearch = (e) => { e.preventDefault(); setPage(1); setSearch(qInput); };

  const doDelete = (u) => {
    if (!window.confirm(`${u.fullName} o'chirilsinmi?`)) return;
    remove.run(u.id);
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Foydalanuvchilar</h1>
          <div className="page-sub">{data?.totalCount ?? 0} ta foydalanuvchi</div>
        </div>
        <button className="btn primary" onClick={() => setShowAdd(true)}><Icon name="plus" size={14} /> Yangi foydalanuvchi</button>
      </div>

      {mutError && (
        <div style={{ marginBottom: 12 }}>
          <ErrorBox error={mutError} onRetry={clearMutError} />
        </div>
      )}

      <form onSubmit={applySearch} className="card" style={{ padding: 12, marginBottom: 14, display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
        <div className="search" style={{ position: "relative", minWidth: 280 }}>
          <Icon name="search" size={14} />
          <input value={qInput} onChange={(e) => setQInput(e.target.value)} placeholder="F.I.Sh yoki telefon..." />
        </div>
        <select className="select" value={status} onChange={(e) => { setPage(1); setStatus(e.target.value); }}>
          <option value="">Barcha statuslar</option>
          <option value="Active">Faol</option>
          <option value="Blocked">Bloklangan</option>
          <option value="New">Yangi</option>
          <option value="Archived">Arxivlangan</option>
        </select>
        <button className="btn" type="submit"><Icon name="search" size={13} /> Qidirish</button>
      </form>

      <div className="card">
        {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : items.length === 0 ? <Empty label="Foydalanuvchi topilmadi" icon="users" /> : (
          <table className="tbl">
            <thead>
              <tr><th>Foydalanuvchi</th><th>ID</th><th>Telefon</th><th>Jinsi</th><th>Yuz tanish</th><th>Status</th><th style={{ width: 140 }}></th></tr>
            </thead>
            <tbody>
              {items.map((u) => (
                <tr key={u.id}>
                  <td>
                    <div className="row" style={{ gap: 10 }}>
                      <Avatar name={u.fullName} />
                      <div>
                        <div style={{ fontWeight: 500 }}>{u.fullName}</div>
                        <div className="faint mono" style={{ fontSize: 11 }}>#{String(u.id).padStart(4, "0")}</div>
                      </div>
                    </div>
                  </td>
                  <td className="mono faint">{u.studentId || "—"}</td>
                  <td className="mono">{u.phone || "—"}</td>
                  <td>{genderLabel[u.gender] || u.gender}</td>
                  <td>{u.faceRecognitionEnabled ? <span className="pill on">Yoqilgan</span> : <span className="pill off">O'chiq</span>}</td>
                  <td><StatusPill status={u.status} /></td>
                  <td>
                    <div className="row" style={{ gap: 4 }}>
                      {u.status === "Blocked"
                        ? <button className="btn xs ghost" title="Blokdan chiqarish" disabled={busy} onClick={() => unblock.run(u.id)}><Icon name="unlock" size={12} /></button>
                        : <button className="btn xs ghost" title="Bloklash" onClick={() => setBlockUser(u)}><Icon name="lock" size={12} /></button>}
                      <button className="btn xs ghost" title="O'chirish" disabled={busy} onClick={() => doDelete(u)}><Icon name="trash" size={12} /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {totalPages > 1 && (
          <div className="row" style={{ justifyContent: "space-between", padding: "12px 16px", borderTop: "1px solid var(--border)" }}>
            <span className="muted" style={{ fontSize: 12 }}>{page} / {totalPages} sahifa</span>
            <div className="row" style={{ gap: 4 }}>
              <button className="btn xs" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>‹ Oldingi</button>
              <button className="btn xs" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Keyingi ›</button>
            </div>
          </div>
        )}
      </div>

      {showAdd && <AddUserModal onClose={() => setShowAdd(false)} onSaved={() => { setShowAdd(false); setPage(1); reload(); }} />}
      {blockUser && <BlockUserModal key={blockUser.id} user={blockUser} onClose={() => setBlockUser(null)} onSaved={() => { setBlockUser(null); reload(); }} />}

      <Toast message={mutError?.message} kind="error" onClose={clearMutError} />
    </div>
  );
};

// ---- Add user modal (multipart) ----
// Shartli render — modal har ochilganda yangi mount bo'ladi, eski qiymatlar qolmaydi.
const AddUserModal = ({ onClose, onSaved }) => {
  const [f, setF] = useState({ firstName: "", lastName: "", gender: "Male", phone: "", parentPhone: "", address: "", dateOfBirth: "" });
  const [photo, setPhoto] = useState(null);
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value });

  const create = useMutation((fd) => usersApi.create(fd), { onSuccess: () => onSaved() });

  const save = () => {
    const fd = new FormData();
    fd.append("FirstName", f.firstName);
    fd.append("LastName", f.lastName);
    fd.append("Gender", f.gender);
    if (f.phone) fd.append("Phone", f.phone);
    if (f.parentPhone) fd.append("ParentPhone", f.parentPhone);
    if (f.address) fd.append("Address", f.address);
    if (f.dateOfBirth) fd.append("DateOfBirth", f.dateOfBirth);
    fd.append("FaceRecognitionEnabled", "true");
    if (photo) fd.append("PhotoFile", photo);
    create.run(fd);
  };

  return (
    <Modal open onClose={onClose} wide title="Yangi foydalanuvchi qo'shish"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn primary" disabled={create.busy} onClick={save}><Icon name="check" size={14} /> {create.busy ? "Saqlanmoqda..." : "Saqlash"}</button>
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
        </div>
        <div className="col" style={{ gap: 12 }}>
          {create.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {create.error.message}</div>}
          <div className="grid-2">
            <Field label="Ism"><input className="input" value={f.firstName} onChange={set("firstName")} /></Field>
            <Field label="Familiya"><input className="input" value={f.lastName} onChange={set("lastName")} /></Field>
          </div>
          <div className="grid-2">
            <Field label="Telefon"><input className="input mono" value={f.phone} onChange={set("phone")} placeholder="+998 90 ___ __ __" /></Field>
            <Field label="Ota-ona telefoni"><input className="input mono" value={f.parentPhone} onChange={set("parentPhone")} /></Field>
          </div>
          <div className="grid-2">
            <Field label="Tug'ilgan sana"><input className="input mono" type="date" value={f.dateOfBirth} onChange={set("dateOfBirth")} /></Field>
            <Field label="Jinsi"><select className="select" value={f.gender} onChange={set("gender")}><option value="Male">Erkak</option><option value="Female">Ayol</option></select></Field>
          </div>
          <Field label="Manzil"><input className="input" value={f.address} onChange={set("address")} /></Field>
        </div>
      </div>
    </Modal>
  );
};

// ---- Block user modal ----
const BlockUserModal = ({ user, onClose, onSaved }) => {
  const [reason, setReason] = useState("");
  const [duration, setDuration] = useState("1 hafta");

  const block = useMutation((body) => usersApi.block(user.id, body), { onSuccess: () => onSaved() });

  return (
    <Modal open onClose={onClose} title="Foydalanuvchini bloklash"
      footer={<>
        <button className="btn" onClick={onClose}>Bekor</button>
        <button className="btn danger" disabled={block.busy || !reason} onClick={() => block.run({ reason, duration })}><Icon name="lock" size={14} /> {block.busy ? "..." : "Bloklash"}</button>
      </>}>
      <div className="col" style={{ gap: 14 }}>
        <div className="row" style={{ gap: 12, padding: 12, background: "var(--bg-0)", borderRadius: 8 }}>
          <Avatar name={user.fullName} size="lg" />
          <div><div style={{ fontWeight: 500 }}>{user.fullName}</div><div className="mono faint" style={{ fontSize: 12 }}>{user.phone || "—"}</div></div>
        </div>
        {block.error && <div className="row" style={{ gap: 8, color: "var(--danger)", fontSize: 13 }}><Icon name="alert" size={14} /> {block.error.message}</div>}
        <Field label="Bloklash sababi"><input className="input" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Sababni kiriting..." /></Field>
        <Field label="Muddat">
          <div className="row" style={{ gap: 6 }}>
            {["1 kun", "1 hafta", "1 oy", "Doimiy"].map((d) => (
              <button key={d} type="button" className="btn sm" style={d === duration ? { background: "var(--bg-3)" } : {}} onClick={() => setDuration(d)}>{d}</button>
            ))}
          </div>
        </Field>
      </div>
    </Modal>
  );
};

export default UsersScreen;
