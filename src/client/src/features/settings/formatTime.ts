// 850 ms, or 2.4 s.
export const formatTime = (ms: number) => (ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1)} s`);
