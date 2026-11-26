import { reactive, watch } from "vue";
import { type GroupField, isGroupField, MAX_GROUP_LEVELS, SORTS, type SortName } from "../inbox/grouping";
import { isPullRequestStatus, type PullRequestStatus } from "../inbox/status";
import type { PullRequestRelation } from "../inbox/types";
import { readStored, stringsIn, writeStored } from "./storage";

export const THEME_OPTIONS = ["system", "light", "dark"] as const;
export type ThemeOption = (typeof THEME_OPTIONS)[number];

export interface Settings {
  groups: GroupField[];
  sort: SortName;
  theme: ThemeOption;
  hideDrafts: boolean;
  hideBots: boolean;
  hideDone: boolean;
  relations: PullRequestRelation[];
  statuses: PullRequestStatus[];
  excludedStatuses: PullRequestStatus[];
}

const SETTINGS_KEY = "yap-in:settings";
const RELATIONS: readonly PullRequestRelation[] = ["authored", "review-you", "review-team", "reviewed", "assigned", "mentioned", "involved"];

const DEFAULT_SETTINGS: Settings = {
  groups: ["owner", "repo", "status"],
  sort: "updated",
  theme: "system",
  hideDrafts: false,
  hideBots: false,
  hideDone: true,
  relations: [],
  statuses: [],
  excludedStatuses: ["Merged"],
};

// Saved settings come from an earlier visit, possibly an older page, so each one is checked and anything unknown
// falls back to its default (e.g. the removed "CI failing" status).
export function settingsFromStored(stored: unknown): Settings {
  const saved = typeof stored === "object" && stored !== null ? (stored as Record<string, unknown>) : {};
  // The earliest page saved a single `group` instead of a list of levels.
  const savedGroups = typeof saved.group === "string" ? (saved.group === "none" ? [] : [saved.group]) : saved.groups;
  const groups = Array.isArray(savedGroups)
    ? [...new Set(stringsIn(savedGroups).filter(isGroupField))].slice(0, MAX_GROUP_LEVELS)
    : DEFAULT_SETTINGS.groups;
  const pick = <T extends string>(value: unknown, allowed: readonly T[], fallback: T): T =>
    typeof value === "string" && (allowed as readonly string[]).includes(value) ? (value as T) : fallback;
  const flag = (value: unknown, fallback: boolean) => (typeof value === "boolean" ? value : fallback);

  return {
    groups,
    sort: pick(saved.sort, Object.keys(SORTS) as SortName[], DEFAULT_SETTINGS.sort),
    theme: pick(saved.theme, THEME_OPTIONS, DEFAULT_SETTINGS.theme),
    hideDrafts: flag(saved.hideDrafts, DEFAULT_SETTINGS.hideDrafts),
    hideBots: flag(saved.hideBots, DEFAULT_SETTINGS.hideBots),
    hideDone: flag(saved.hideDone, DEFAULT_SETTINGS.hideDone),
    relations: stringsIn(saved.relations).filter((relation): relation is PullRequestRelation => RELATIONS.includes(relation as PullRequestRelation)),
    statuses: stringsIn(saved.statuses).filter(isPullRequestStatus),
    excludedStatuses: "excludedStatuses" in saved
      ? stringsIn(saved.excludedStatuses).filter(isPullRequestStatus)
      : DEFAULT_SETTINGS.excludedStatuses,
  };
}

export function useSettings(): Settings {
  const settings = reactive(settingsFromStored(readStored(SETTINGS_KEY)));
  watch(settings, () => writeStored(SETTINGS_KEY, settings), { deep: true });
  return settings;
}

