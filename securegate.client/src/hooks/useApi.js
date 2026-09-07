// Oddiy data-fetch hook: loading / error / reload bilan.
//
// Imzo: useApi(fetchFn, deps) -> { data, loading, error, reload, setData }
//   fetchFn(signal) — AbortSignal argument sifatida uzatiladi (ixtiyoriy ishlatiladi).
//   deps o'zgarganda eski `data` darhol tozalanadi (eski sarlavha/nom ko'rinib qolmasin).
import { useState, useEffect, useRef, useCallback } from "react";
import { isAbortError } from "../api/client";

// deps massividan barqaror string kalit yasaydi (massivning o'zi har renderda yangi).
function keyOf(deps) {
  let out = "";
  for (const d of deps) {
    if (d === null || d === undefined) out += String(d);
    else if (typeof d === "object") { try { out += JSON.stringify(d); } catch { out += "[obj]"; } }
    else out += String(d);
    out += "\u0001";
  }
  return out;
}

export function useApi(fetchFn, deps = []) {
  // Holat kalit bilan birga saqlanadi — natija qaysi so'rovga tegishli ekani shundan bilinadi.
  const [state, setState] = useState({ key: null, data: null, error: null, loading: true });
  const [tick, setTick] = useState(0);

  const requestKey = `${tick}\u0002${keyOf(deps)}`;

  // fetchFn har renderda yangi funksiya — uni ref'da ushlaymiz, deps'ga qo'shmaymiz.
  const fnRef = useRef(fetchFn);
  useEffect(() => { fnRef.current = fetchFn; });

  // Joriy kalitni setData uchun ref'da saqlaymiz.
  const keyRef = useRef(requestKey);
  useEffect(() => { keyRef.current = requestKey; }, [requestKey]);

  useEffect(() => {
    const ctrl = new AbortController();
    let active = true;

    Promise.resolve(fnRef.current(ctrl.signal))
      .then((d) => {
        if (active) setState({ key: requestKey, data: d, error: null, loading: false });
      })
      .catch((e) => {
        if (!active || isAbortError(e)) return; // bekor qilingan so'rov — xato emas
        setState({ key: requestKey, data: null, error: e, loading: false });
      });

    return () => { active = false; ctrl.abort(); };
  }, [requestKey]);

  const reload = useCallback(() => setTick((t) => t + 1), []);

  const setData = useCallback((next) => {
    setState((s) => ({
      key: keyRef.current,
      error: null,
      loading: false,
      data: typeof next === "function" ? next(s.data) : next,
    }));
  }, []);

  // Kalit mos kelmasa — bu eski so'rovning natijasi; render paytida yangi so'rov
  // holatini ko'rsatamiz (data null, loading true). setState render'da chaqirilmaydi.
  const fresh = state.key === requestKey;

  return {
    data: fresh ? state.data : null,
    loading: fresh ? state.loading : true,
    error: fresh ? state.error : null,
    reload,
    setData,
  };
}
