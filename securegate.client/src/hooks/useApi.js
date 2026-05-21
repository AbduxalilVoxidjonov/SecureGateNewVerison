// Oddiy data-fetch hook: loading / error / reload bilan.
/* eslint-disable react-hooks/set-state-in-effect */
import { useState, useEffect, useCallback } from "react";

export function useApi(fetchFn, deps = []) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [tick, setTick] = useState(0);

  const reload = useCallback(() => setTick((t) => t + 1), []);
  // eslint-disable-next-line react-hooks/exhaustive-deps, react-hooks/use-memo
  const run = useCallback(fetchFn, deps);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    Promise.resolve(run())
      .then((d) => { if (active) setData(d); })
      .catch((e) => { if (active) setError(e); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [run, tick]);

  return { data, loading, error, reload, setData };
}
