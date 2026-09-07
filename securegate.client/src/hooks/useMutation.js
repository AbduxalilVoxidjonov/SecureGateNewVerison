// Yozish amallari (POST/PUT/DELETE) uchun hook.
//
// Imzo:
//   const { run, busy, error, reset } = useMutation(fn, { onSuccess, onError });
//
//   run(...args) — fn(...args) ni chaqiradi. XATO TASHLAMAYDI (throw qilmaydi):
//                  xatoni `error` holatiga yozadi va opts.onError(err) ni chaqiradi,
//                  muvaffaqiyatda opts.onSuccess(result) ni chaqirib natijani qaytaradi.
//                  Xato bo'lsa `undefined` qaytaradi.
//   busy  — boolean (so'rov ketayotgani)
//   error — Error | null (foydalanuvchiga ko'rsatiladigan matn `error.message` da, o'zbekcha)
//   reset() — error'ni tozalaydi
import { useState, useRef, useEffect, useCallback } from "react";
import { isAbortError } from "../api/client";

function toError(e) {
  if (e instanceof Error) {
    if (!e.message) e.message = "Amalni bajarib bo'lmadi.";
    return e;
  }
  return new Error(typeof e === "string" && e ? e : "Amalni bajarib bo'lmadi.");
}

export default function useMutation(fn, opts) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const fnRef = useRef(fn);
  const optsRef = useRef(opts);
  const mounted = useRef(true);

  useEffect(() => { fnRef.current = fn; optsRef.current = opts; });
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const reset = useCallback(() => setError(null), []);

  const run = useCallback(async (...args) => {
    if (mounted.current) { setBusy(true); setError(null); }
    try {
      const result = await fnRef.current(...args);
      if (mounted.current) setBusy(false);
      optsRef.current?.onSuccess?.(result);
      return result;
    } catch (e) {
      if (mounted.current) setBusy(false);
      if (isAbortError(e)) return undefined; // bekor qilingan — xato emas
      const err = toError(e);
      if (mounted.current) setError(err);
      optsRef.current?.onError?.(err);
      return undefined;
    }
  }, []);

  return { run, busy, error, reset };
}
