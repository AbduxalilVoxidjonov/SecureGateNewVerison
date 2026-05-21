// Recordings / Yozuvlar tarixi — real API
import { useState } from "react";
import { Icon } from "../components/Icon";
import { Loading, ErrorBox, Empty } from "../components/state";
import { useApi } from "../hooks/useApi";
import { recordingsApi } from "../api/endpoints";
import { getToken } from "../api/client";

const fmtSize = (bytes) => {
  if (!bytes) return "—";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let s = bytes, u = 0;
  while (s >= 1024 && u < units.length - 1) { s /= 1024; u++; }
  return `${s.toFixed(1)} ${units[u]}`;
};

const RecordingsScreen = () => {
  const camsState = useApi(() => recordingsApi.list(), []);
  const [selectedId, setSelectedId] = useState(null);
  const archive = useApi(() => (selectedId ? recordingsApi.camera(selectedId) : Promise.resolve(null)), [selectedId]);

  const cams = camsState.data || [];

  const download = async (camId, date) => {
    try {
      const res = await fetch(recordingsApi.downloadUrl(camId, date), {
        headers: { Authorization: `Bearer ${getToken()}` },
      });
      if (!res.ok) { alert("Yozuv topilmadi yoki yuklab bo'lmadi."); return; }
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url; a.download = `recording-${date}.mp4`;
      document.body.appendChild(a); a.click(); a.remove();
      URL.revokeObjectURL(url);
    } catch { alert("Yuklab olishda xatolik."); }
  };

  return (
    <div className="screen-in">
      <div className="page-head">
        <div>
          <h1 className="page-title">Yozuvlar tarixi</h1>
          <div className="page-sub">Kamera arxivlari (oxirgi 30 kun)</div>
        </div>
        <button className="btn" onClick={camsState.reload}><Icon name="refresh" size={14} /> Yangilash</button>
      </div>

      <div className="two-col">
        {/* Camera list */}
        <div className="card">
          <div className="card-h"><h3>Kameralar</h3></div>
          {camsState.loading ? <Loading /> : camsState.error ? <ErrorBox error={camsState.error} onRetry={camsState.reload} /> : cams.length === 0 ? <Empty label="Kamera yo'q" icon="camera" /> : (
            <table className="tbl">
              <thead><tr><th>Kamera</th><th>Kod</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {cams.map((c) => (
                  <tr key={c.id} onClick={() => setSelectedId(c.id)} style={{ cursor: "pointer", background: selectedId === c.id ? "var(--bg-3)" : "" }}>
                    <td style={{ fontWeight: 500 }}>{c.name}</td>
                    <td className="mono faint">{c.cameraCode}</td>
                    <td>{c.continuousRecording ? <span className="pill on">24/7</span> : <span className="pill off">O'chiq</span>}</td>
                    <td><Icon name="chevron" size={13} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Archive */}
        <div className="card">
          <div className="card-h"><h3>{archive.data?.camera ? `${archive.data.camera.name} · arxiv` : "Kamerani tanlang"}</h3></div>
          {!selectedId ? <Empty label="Chapdan kamera tanlang" icon="film" /> :
            archive.loading ? <Loading /> : archive.error ? <ErrorBox error={archive.error} onRetry={archive.reload} /> : (
            <table className="tbl">
              <thead><tr><th>Sana</th><th>Hajm</th><th>Holat</th><th></th></tr></thead>
              <tbody>
                {(archive.data?.entries || []).map((e) => (
                  <tr key={e.fileName}>
                    <td className="mono">{new Date(e.date).toLocaleDateString("uz-UZ")}</td>
                    <td className="mono">{fmtSize(e.sizeBytes)}</td>
                    <td>{e.exists ? <span className="pill on">Mavjud</span> : <span className="pill off">Yo'q</span>}</td>
                    <td>
                      {e.exists && (
                        <button className="btn xs" onClick={() => download(archive.data.camera.id, e.date.slice(0, 10))}>
                          <Icon name="download" size={11} /> Yuklab olish
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
};

export default RecordingsScreen;
