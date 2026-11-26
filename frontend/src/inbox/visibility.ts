import { type GroupField, groupKeys, groupSegment } from "./grouping";
import type { PullRequest } from "./types";

export interface FoldState {
  groups: GroupField[];
  collapsedGroupIds: ReadonlySet<string>;
}

// A PR under several groups (relations, labels) stays visible while any of its group paths is fully unfolded.
export function isInFoldedGroup(pr: PullRequest, foldState: FoldState): boolean {
  if (foldState.groups.length === 0) return false;
  let groupPaths: string[][] = [[]];
  for (const field of foldState.groups) {
    groupPaths = groupPaths.flatMap((path) => groupKeys(pr, field).map((key) => [...path, groupSegment(field, key)]));
  }
  return groupPaths.every((path) => path.some((_, i) => foldState.collapsedGroupIds.has(path.slice(0, i + 1).join("/"))));
}

export function isInFoldedTopLevelGroup(pr: PullRequest, foldState: FoldState): boolean {
  const [topField] = foldState.groups;
  return topField !== undefined && groupKeys(pr, topField).every((key) => foldState.collapsedGroupIds.has(groupSegment(topField, key)));
}

export function isOwnPr(pr: PullRequest, viewer: string): boolean {
  return pr.author === viewer;
}

export interface ListVisibility extends FoldState {
  viewer: string;
  hideDone: boolean;
  isDone: (pr: PullRequest) => boolean;
}

// Your own PRs stay listed under a folded group, except when the top-level group (e.g. an organisation) is folded.
export function isHiddenFromList(pr: PullRequest, visibility: ListVisibility): boolean {
  if (visibility.hideDone && visibility.isDone(pr)) return true;
  if (isInFoldedGroup(pr, visibility) === false) return false;
  return isOwnPr(pr, visibility.viewer) === false || isInFoldedTopLevelGroup(pr, visibility);
}
