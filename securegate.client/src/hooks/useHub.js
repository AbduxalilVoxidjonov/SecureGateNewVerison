// SignalR hublari uchun React hook'lari.
// Ulanishlarni boshqarish `../api/hub.js` da (hub bo'yicha bitta ulanish + ref-count).
import { useCallback, useEffect, useRef, useSyncExternalStore } from "react";
import {
  subscribeHubEvent,
  subscribeHubStatus,
  getHubStatus,
  invokeHub,
} from "../api/hub";

/**
 * Hub eventiga obuna bo'ladi. Komponent unmount bo'lganda obuna olib tashlanadi.
 *
 * `handler`ni memoize qilish SHART EMAS: ichkarida ref orqali har doim eng oxirgi
 * versiyasi chaqiriladi, shuning uchun har renderda obuna off/on bo'lmaydi.
 *
 * @param {"camera"|"turnstile"|"alert"|"dashboard"} hubKey
 * @param {string} eventName  Server yuboradigan event nomi (masalan "FaceDetected")
 * @param {(payload:any, ...rest:any[])=>void} handler
 */
export function useHubEvent(hubKey, eventName, handler) {
  const handlerRef = useRef(handler);

  // Har renderdan keyin eng oxirgi handler'ni saqlaymiz (obunaga tegmasdan).
  useEffect(() => {
    handlerRef.current = handler;
  });

  useEffect(() => {
    if (!hubKey || !eventName) return undefined;
    return subscribeHubEvent(hubKey, eventName, (...args) => {
      const fn = handlerRef.current;
      if (typeof fn === "function") fn(...args);
    });
  }, [hubKey, eventName]);
}

/**
 * Hub ulanishining joriy holati. Hook faol ekan, ulanish tirik ushlab turiladi.
 * @param {"camera"|"turnstile"|"alert"|"dashboard"} hubKey
 * @returns {"connected"|"connecting"|"reconnecting"|"disconnected"}
 */
export function useHubStatus(hubKey) {
  const subscribe = useCallback(
    (onChange) => subscribeHubStatus(hubKey, onChange),
    [hubKey]
  );
  const snapshot = useCallback(() => getHubStatus(hubKey), [hubKey]);
  // SSR yo'q, lekin useSyncExternalStore uchun server-snapshot ham beramiz.
  return useSyncExternalStore(subscribe, snapshot, snapshot);
}

/**
 * Server metodini chaqirish uchun barqaror funksiya (masalan turniketni ochish).
 * Ulanish yo'q bo'lsa Promise rad etiladi — chaqiruvchi xatoni ko'rsatishi kerak.
 * @param {"camera"|"turnstile"|"alert"|"dashboard"} hubKey
 * @returns {(methodName:string, ...args:any[])=>Promise<any>}
 */
export function useHubInvoke(hubKey) {
  // Chaqirish uchun ulanish kerak — hook faol ekan uni tirik ushlab turamiz.
  useEffect(() => {
    if (!hubKey) return undefined;
    return subscribeHubStatus(hubKey, () => {});
  }, [hubKey]);

  return useCallback(
    (methodName, ...args) => invokeHub(hubKey, methodName, ...args),
    [hubKey]
  );
}
