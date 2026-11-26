import { STATUS_ORDER, status } from "./status";
import { AGE_ORDER, ageBucket } from "./time";
import type { PullRequest, PullRequestRelation } from "./types";

export const RELATION_NAMES: Record<PullRequestRelation, string> = {
  "review-you": "Review requested (you)",
  "review-team": "Review requested (team)",
  authored: "Authored",
  assigned: "Assigned",
  reviewed: "Reviewed",
  mentioned: "Mentioned",
  involved: "Involved",
};

export const GROUP_FIELD_NAMES = {
  owner: "Organisation",
  repo: "Repository",
  relation: "Relation",
  status: "Status",
  author: "Author",
  label: "Label",
  age: "Last updated",
} as const;

export type GroupField = keyof typeof GROUP_FIELD_NAMES;
export const MAX_GROUP_LEVELS = 3;

export function isGroupField(value: string): value is GroupField {
  return value in GROUP_FIELD_NAMES;
}

export const SORTS = {
  updated: (a: PullRequest, b: PullRequest) => b.updatedAt.localeCompare(a.updatedAt),
  created: (a: PullRequest, b: PullRequest) => b.createdAt.localeCompare(a.createdAt),
  oldest: (a: PullRequest, b: PullRequest) => a.createdAt.localeCompare(b.createdAt),
  size: (a: PullRequest, b: PullRequest) => (b.additions + b.deletions) - (a.additions + a.deletions),
  comments: (a: PullRequest, b: PullRequest) => b.comments.human - a.comments.human,
} as const;

export type SortName = keyof typeof SORTS;

export const SORT_NAMES: Record<SortName, string> = {
  updated: "Recently updated",
  created: "Newest",
  oldest: "Oldest",
  size: "Largest",
  comments: "Most comments",
};

// A PR may sit in several groups of one field (relations, labels).
export function groupKeys(pr: PullRequest, field: GroupField): string[] {
  switch (field) {
    case "repo": return [pr.repo];
    case "owner": return [pr.owner];
    case "relation": return pr.relations.map((relation) => RELATION_NAMES[relation]);
    case "status": return [status(pr)];
    case "author": return [pr.author];
    case "label": return pr.labels.length ? pr.labels.map((label) => label.name) : ["No label"];
    case "age": return [ageBucket(pr.updatedAt)];
  }
}

/** A group's id is its path from the top, e.g. "owner:acme/repo:acme/search"; folded groups are saved by it. */
export function groupSegment(field: GroupField, key: string): string {
  return `${field}:${key}`;
}

export interface GroupNode {
  field: GroupField;
  key: string;
  id: string;
  items: PullRequest[];
  children: GroupNode[] | null;
}

const NATURAL_ORDER: Partial<Record<GroupField, readonly string[]>> = {
  status: STATUS_ORDER,
  relation: Object.values(RELATION_NAMES),
  age: AGE_ORDER,
};

// Fields with a natural order keep it; the rest follow the sort, placed by their first PR (items arrive sorted).
function compareGroups(field: GroupField, sort: SortName) {
  const naturalOrder = NATURAL_ORDER[field];
  return (a: GroupNode, b: GroupNode) => naturalOrder
    ? naturalOrder.indexOf(a.key) - naturalOrder.indexOf(b.key)
    : SORTS[sort](a.items[0]!, b.items[0]!) || a.key.localeCompare(b.key);
}

// A group whose PRs are all done goes after its siblings, which keep their order. Status groups keep their fixed order
// so each status is always in the same place.
export function buildGroupTree(
  prs: PullRequest[], fields: GroupField[], sort: SortName, isDone: (pr: PullRequest) => boolean, parentId = "",
): GroupNode[] | null {
  const [field, ...remainingFields] = fields;
  if (field === undefined) return null;
  const itemsByKey = new Map<string, PullRequest[]>();
  for (const pr of prs) {
    for (const key of groupKeys(pr, field)) itemsByKey.set(key, [...(itemsByKey.get(key) ?? []), pr]);
  }
  const groups = [...itemsByKey]
    .map(([key, items]) => {
      const id = parentId ? `${parentId}/${groupSegment(field, key)}` : groupSegment(field, key);
      return { field, key, id, items, children: buildGroupTree(items, remainingFields, sort, isDone, id) };
    })
    .sort(compareGroups(field, sort));
  if (field === "status") return groups;
  const isAllDone = (group: GroupNode) => group.items.every(isDone);
  return [...groups.filter((group) => isAllDone(group) === false), ...groups.filter(isAllDone)];
}

export function allGroupIds(nodes: GroupNode[] | null): string[] {
  return (nodes ?? []).flatMap((node) => [node.id, ...allGroupIds(node.children)]);
}

// A stack sorts as one item, placed by whichever member sorts first, and lists its members bottom-up.
// Grouping keeps this order, so a stack split across groups stays in stack order within each.
export function sortKeepingStacks(prs: PullRequest[], sort: SortName): PullRequest[] {
  const compare = SORTS[sort];
  const membersByStack = new Map<string, PullRequest[]>();
  for (const pr of prs) {
    const stackKey = pr.stack ? `${pr.repo}:${pr.stack.number}` : pr.id;
    membersByStack.set(stackKey, [...(membersByStack.get(stackKey) ?? []), pr]);
  }
  return [...membersByStack.values()]
    .map((members) => ({
      first: [...members].sort(compare)[0]!,
      members: members.sort((a, b) => (a.stack?.position ?? 0) - (b.stack?.position ?? 0)),
    }))
    .sort((a, b) => compare(a.first, b.first))
    .flatMap((stack) => stack.members);
}
