// Blocked users / Bloklangan foydalanuvchilar — real API + SignalR (alert/camera hublari)
import { Icon } from "../components/Icon";
import { Avatar, HubPill, Toast } from "../components/ui";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { useHubEvent } from "../hooks/useHub";
import useMutation from "../hooks/useMutation";
import { blockedApi, usersApi } from "../api/endpoints";
import { useReloadOnReconnect, useThrottledReload } from "./live";

const BlockedScreen = () => {
  const { data, loading, error, reload } = useApi(() => blockedApi.list({ pageSize: 100 }), []);
  const items = data?.items || [];

  const unblock = useMutation((id) => usersApi.unblock(id), { onSuccess: reload });

  // Bu ekranda "yangi yozuv" hodisasi yo'q: blok holati boshqa joyda o'zgaradi
  // (UsersService bloklash/blokdan chiqarishda `NewAlert` yuboradi) va rad etilgan
  // urinish ham blok bilan bog'liq bo'lishi mumkin. Shuning uchun ro'yxat qayta
  // o'qiladi — lekin har hodisada emas, throttle bilan (5 s da ko'pi bilan bitta so'rov).
  const refresh = useThrottledReload(reload, 5000);
  const hubStatus = useReloadOnReconnect("alert", reload);

  useHubEvent("alert", "NewAlert", () => refresh());
  useHubEvent("alert", "BlockedAccessAttempt", () => refresh());
  useHubEvent("camera", "NewAccessLog", (p) => { if (p && p.granted === false) refresh(); });

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Bloklangan foydalanuvchilar</h1>
          <div className="page-sub">Jami {data?.totalCount ?? items.length} ta</div>
        </div>
        <div className="row">
          <HubPill status={hubStatus} title="alert hub" />
          <button className="btn" onClick={reload}><Icon name="refresh" size={14} /> Yangilash</button>
        </div>
      </div>

      {unblock.error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorBox error={unblock.error} onRetry={unblock.reset} />
        </div>
      )}

      {loading ? <Loading /> : error ? <ErrorBox error={error} onRetry={reload} /> : items.length === 0 ? <Empty label="Bloklangan foydalanuvchi yo'q" icon="ban" /> : (
        <div className="col" style={{ gap: 12 }}>
          {items.map((b) => (
            <div key={b.id} className="card" style={{ padding: 14 }}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr auto", gap: 14, alignItems: "center" }}>
                <div className="row" style={{ gap: 14 }}>
                  <Avatar name={b.fullName} size="lg" />
                  <div>
                    <div className="row" style={{ gap: 8 }}>
                      <span style={{ fontWeight: 600 }}>{b.fullName}</span>
                      <span className="pill err">Bloklangan</span>
                    </div>
                    <div className="row mono faint" style={{ gap: 14, fontSize: 11.5, marginTop: 6 }}>
                      <span>ID: <span style={{ color: "var(--text-1)" }}>#{String(b.id).padStart(4, "0")}</span></span>
                      <span>Telefon: <span style={{ color: "var(--text-1)" }}>{b.phone || "—"}</span></span>
                    </div>
                  </div>
                </div>
                <button className="btn primary sm" disabled={unblock.busy} onClick={() => unblock.run(b.id)}><Icon name="unlock" size={12} /> Blokdan chiqarish</button>
              </div>
            </div>
          ))}
        </div>
      )}

      <Toast message={unblock.error?.message} kind="error" onClose={unblock.reset} />
    </div>
  );
};

export default BlockedScreen;
