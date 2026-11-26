// Browser storage can be blocked, cleared or hold values from an older version of the page, so every read falls back
// and is checked by its caller.

export function readStored(key: string): unknown {
  try {
    const raw = localStorage.getItem(key);
    return raw === null ? undefined : JSON.parse(raw);
  } catch {
    return undefined;
  }
}

export function writeStored(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Not saving is fine: the page keeps working for this visit.
  }
}

export function isRecordOfStrings(value: unknown): value is Record<string, string> {
  return typeof value === "object" && value !== null && Object.values(value).every((entry) => typeof entry === "string");
}

export function isRecordOfNumbers(value: unknown): value is Record<string, number> {
  return typeof value === "object" && value !== null && Object.values(value).every((entry) => typeof entry === "number");
}

export function stringsIn(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === "string") : [];
}
