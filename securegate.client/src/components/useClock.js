// Butun ilova uchun BITTA sekundlik interval.
//
// Har bir komponent o'z setInterval'ini yaratsa, 24 kamerali gridda sekundiga
// 24 ta taymer va 24 ta re-render bo'ladi. Bu yerda bitta umumiy manba bor:
// obuna bo'lganlar soni 0 ga tushsa, interval to'xtaydi.
//
//   const now = useClock();   // Date — har sekundda yangilanadi
import { useSyncExternalStore } from "react";

const listeners = new Set();
let timer = null;
let snapshot = new Date(); // barqaror havola — tik orasida o'zgarmaydi

function tick() {
  snapshot = new Date();
  for (const l of listeners) l();
}

function subscribe(listener) {
  listeners.add(listener);
  if (timer === null) timer = setInterval(tick, 1000);
  return () => {
    listeners.delete(listener);
    if (listeners.size === 0 && timer !== null) {
      clearInterval(timer);
      timer = null;
    }
  };
}

const getSnapshot = () => snapshot;

export function useClock() {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

export default useClock;
