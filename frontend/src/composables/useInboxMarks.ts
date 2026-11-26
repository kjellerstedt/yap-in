import { reactive, ref } from "vue";
import { status } from "../inbox/status";
import type { PullRequest } from "../inbox/types";
import { isRecordOfNumbers, isRecordOfStrings, readStored, stringsIn, writeStored } from "./storage";

const COLLAPSED_KEY = "yap-in:collapsed";
// { [prId]: updatedAt }: a PR resurfaces when it is updated after being marked done.
const DONE_KEY = "yap-in:done";
// PR ids already marked done for your approval, so an undo sticks instead of being re-applied on the next load.
const AUTO_DONE_APPROVED_KEY = "yap-in:auto-done-approved";
// { [prId]: lastSeenAt }. Absent until the first load, which marks everything then in the inbox as seen.
const SEEN_KEY = "yap-in:seen";
const SEEN_RETENTION_MS = 60 * 86_400_000;

/** What you have done to pull requests on this page: folded groups, done, seen. Kept in the browser. */
export function useInboxMarks() {
  const collapsedGroupIds = reactive(new Set(stringsIn(readStored(COLLAPSED_KEY))));
  const storedDone = readStored(DONE_KEY);
  const doneUpdatedAtById = reactive<Record<string, string>>(isRecordOfStrings(storedDone) ? storedDone : {});
  const autoDoneApprovedIds = new Set(stringsIn(readStored(AUTO_DONE_APPROVED_KEY)));
  const storedSeen = readStored(SEEN_KEY);
  const seenAtById = ref<Record<string, number> | null>(isRecordOfNumbers(storedSeen) ? storedSeen : null);

  const saveCollapsed = () => writeStored(COLLAPSED_KEY, [...collapsedGroupIds]);
  const saveDone = () => writeStored(DONE_KEY, doneUpdatedAtById);
  const saveSeen = () => writeStored(SEEN_KEY, seenAtById.value);

  function isDone(pr: PullRequest): boolean {
    const doneUpdatedAt = doneUpdatedAtById[pr.id];
    return doneUpdatedAt !== undefined && doneUpdatedAt >= pr.updatedAt;
  }

  function toggleDone(pr: PullRequest): void {
    if (isDone(pr)) delete doneUpdatedAtById[pr.id];
    else doneUpdatedAtById[pr.id] = pr.updatedAt;
    saveDone();
  }

  function isNew(pr: PullRequest, viewer: string): boolean {
    return seenAtById.value !== null && seenAtById.value[pr.id] === undefined && pr.author !== viewer && pr.state !== "MERGED";
  }

  function markSeen(prId: string): void {
    if (seenAtById.value === null || seenAtById.value[prId] !== undefined) return;
    seenAtById.value[prId] = Date.now();
    saveSeen();
  }

  // Bumps lastSeenAt for PRs still in the inbox so pruning only drops ones that have left it.
  function refreshSeen(prs: PullRequest[]): void {
    const now = Date.now();
    const firstLoad = seenAtById.value === null;
    const seen = seenAtById.value ?? {};
    for (const pr of prs) if (firstLoad || seen[pr.id] !== undefined) seen[pr.id] = now;
    for (const [id, lastSeenAt] of Object.entries(seen)) if (now - lastSeenAt > SEEN_RETENTION_MS) delete seen[id];
    seenAtById.value = seen;
    saveSeen();
  }

  // Once per PR: when your approval leaves it Approved overall, it is marked done. Approved by you but still waiting
  // on other reviewers stays in view. An approval after changes were requested counts too, since your part is done.
  // Like a manual done, it resurfaces if the PR is updated afterwards.
  function markApprovedDone(prs: PullRequest[]): void {
    for (const pr of prs) {
      const prStatus = status(pr);
      const isApproved = prStatus === "Approved" || prStatus === "Approved after changes requested";
      if (pr.myReview !== "APPROVED" || isApproved === false || autoDoneApprovedIds.has(pr.id)) continue;
      autoDoneApprovedIds.add(pr.id);
      doneUpdatedAtById[pr.id] ??= pr.updatedAt;
    }
    saveDone();
    writeStored(AUTO_DONE_APPROVED_KEY, [...autoDoneApprovedIds]);
  }

  function setGroupFolded(groupId: string, folded: boolean): void {
    if (folded) collapsedGroupIds.add(groupId);
    else collapsedGroupIds.delete(groupId);
    saveCollapsed();
  }

  function setAllGroupsFolded(groupIds: string[], folded: boolean): void {
    for (const groupId of groupIds) {
      if (folded) collapsedGroupIds.add(groupId);
      else collapsedGroupIds.delete(groupId);
    }
    saveCollapsed();
  }

  return { collapsedGroupIds, isDone, toggleDone, isNew, markSeen, refreshSeen, markApprovedDone, setGroupFolded, setAllGroupsFolded };
}

export type InboxMarks = ReturnType<typeof useInboxMarks>;
