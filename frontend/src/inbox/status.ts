import type { PullRequest } from "./types";

// The order statuses are listed and grouped in. Approved comes after the statuses that still need something.
export const STATUS_ORDER = [
  "Needs your review",
  "Changes requested",
  "Merge conflict",
  "Waiting for review",
  "Draft",
  "Approved after changes requested",
  "Approved",
  "Merged",
  "Closed",
] as const;

export type PullRequestStatus = (typeof STATUS_ORDER)[number];

export const STATUS_TONE: Record<PullRequestStatus, string> = {
  "Needs your review": "var(--accent)",
  "Changes requested": "var(--red)",
  "Merge conflict": "var(--amber)",
  "Waiting for review": "var(--blue)",
  Draft: "var(--muted)",
  Approved: "var(--green)",
  "Approved after changes requested": "var(--green)",
  Merged: "var(--purple)",
  Closed: "var(--muted)",
};

export const STATUS_BADGE_CLASS: Record<PullRequestStatus, string> = {
  "Needs your review": "you",
  "Changes requested": "changes",
  "Merge conflict": "conflict",
  "Waiting for review": "",
  Draft: "draft",
  Approved: "approved",
  "Approved after changes requested": "approved-after-changes",
  Merged: "merged",
  Closed: "draft",
};

export function isPullRequestStatus(value: string): value is PullRequestStatus {
  return (STATUS_ORDER as readonly string[]).includes(value);
}

// The first that applies wins, so a PR's own problems (draft, changes, conflict) come before its review result.
// A conflict wins over an approval that came after changes were requested, because it still blocks merging.
export function status(pr: PullRequest): PullRequestStatus {
  if (pr.state === "MERGED") return "Merged";
  if (pr.state === "CLOSED") return "Closed";
  if (pr.isDraft) return "Draft";
  if (pr.relations.includes("review-you") && pr.myReview === null) return "Needs your review";
  const isApprovedAfterChangesRequested = isApprovedSinceEveryChangesRequested(pr);
  if (pr.reviewDecision === "CHANGES_REQUESTED" && isApprovedAfterChangesRequested === false) return "Changes requested";
  if (pr.mergeable === "CONFLICTING") return "Merge conflict";
  if (pr.reviewDecision === "APPROVED") return "Approved";
  if (isApprovedAfterChangesRequested) return "Approved after changes requested";
  return "Waiting for review";
}

export function changesRequestedReviews(pr: PullRequest): PullRequest["reviews"] {
  return pr.reviews.filter((review) => review.state === "CHANGES_REQUESTED");
}

// GitHub keeps a request for changes in force until its reviewer approves or it is dismissed, even when a later
// review approves. A reviewer saying "lgtm" in a comment does not lift it, so the PR can be ready to merge while GitHub
// still says CHANGES_REQUESTED. Timestamps are GitHub's UTC format, so they compare as text.
function isApprovedSinceEveryChangesRequested(pr: PullRequest): boolean {
  if (pr.reviewDecision !== "CHANGES_REQUESTED") return false;
  const changesRequestedTimes = changesRequestedReviews(pr).map((review) => review.submittedAt);
  // With no visible request for changes, or one without a time, there is nothing to compare an approval against.
  if (changesRequestedTimes.length === 0 || changesRequestedTimes.includes(null)) return false;
  const latestChangesRequestedAt = changesRequestedTimes.reduce((latest, time) => (time! > latest! ? time : latest));
  return pr.reviews.some((review) =>
    review.state === "APPROVED" && review.submittedAt !== null && review.submittedAt > latestChangesRequestedAt!);
}

// Your own PR that nobody has responded to yet, so there is nothing for you to act on. Your own replies don't count as
// feedback; the human comment count includes yours, so a comment from you also ends the wait.
export function isOwnPrAwaitingFeedback(pr: PullRequest, viewer: string): boolean {
  return pr.author === viewer && status(pr) === "Waiting for review" && pr.comments.human === 0 &&
    pr.reviews.every((review) => review.login === viewer);
}

// GitHub keeps listing an approval after new pushes, but a repo may stop counting it (e.g. "approve the latest push").
// It counts as stale when the PR is not approved overall and has commits from after your approval.
export function isMyApprovalStale(pr: PullRequest): boolean {
  return pr.myReview === "APPROVED" && pr.reviewDecision !== "APPROVED" &&
    pr.myReviewAt !== null && pr.lastCommitAt !== null && pr.lastCommitAt > pr.myReviewAt;
}
