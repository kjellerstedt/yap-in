const DAY_MS = 86_400_000;

export const AGE_ORDER = ["Today", "This week", "This month", "Older"] as const;
export type AgeBucket = (typeof AGE_ORDER)[number];

export function ageBucket(iso: string, now = Date.now()): AgeBucket {
  const days = (now - Date.parse(iso)) / DAY_MS;
  if (days < 1) return "Today";
  if (days < 7) return "This week";
  return days < 30 ? "This month" : "Older";
}

const UNITS: [string, number][] = [["y", 31_536_000], ["mo", 2_592_000], ["d", 86_400], ["h", 3_600], ["m", 60]];

export function relativeTime(iso: string, now = Date.now()): string {
  const seconds = (now - Date.parse(iso)) / 1000;
  for (const [unit, unitSeconds] of UNITS) {
    if (seconds >= unitSeconds) return `${Math.floor(seconds / unitSeconds)}${unit} ago`;
  }
  return "just now";
}

// A fixed locale, so the page reads "23 Nov" whatever the browser's language.
export function dayAndMonth(iso: string): string {
  return new Date(iso).toLocaleDateString("en-GB", { day: "numeric", month: "short" });
}
