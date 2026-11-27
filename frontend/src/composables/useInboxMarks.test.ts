import { describe, expect, it } from "vitest";
import type { PullRequest } from "../inbox/types";
import { useInboxMarks } from "./useInboxMarks";

function pullRequest(overrides: Partial<PullRequest> = {}): PullRequest {
  return {
    id: "PR_1", number: 1, title: "Add search tags", url: "https://github.com/acme/search/pull/1", isDraft: false, state: "OPEN",
    createdAt: "2026-11-19T08:00:00Z", updatedAt: "2026-11-20T09:00:00Z", mergedAt: null, closedAt: null,
    additions: 10, deletions: 2, changedFiles: 1, mergeable: "MERGEABLE", mergeStateStatus: "CLEAN", reviewDecision: "REVIEW_REQUIRED",
    head: "feat/tags", base: "main", author: "anna", authorAvatar: null, repo: "acme/search", owner: "acme", isPrivate: true,
    comments: { human: 0, bot: 0 }, labels: [], assignees: [], reviewRequests: [], reviews: [],
    myReview: null, myReviewAt: null, lastCommitAt: null, checks: "SUCCESS", stack: null, relations: ["involved"],
    ...overrides,
  };
}

// The first load marks everything then in the inbox as seen, so a pull request that arrives afterwards is the new one.
function marksAfterFirstLoad() {
  const marks = useInboxMarks();
  marks.refreshSeen([]);
  return marks;
}

describe("isNew", () => {
  it("flags a pull request that arrived after the first load", () => {
    expect(marksAfterFirstLoad().isNew(pullRequest(), "kjellerstedt")).toBe(true);
  });

  it("does not flag one you approved, since you have obviously seen it", () => {
    expect(marksAfterFirstLoad().isNew(pullRequest({ myReview: "APPROVED" }), "kjellerstedt")).toBe(false);
  });

  it("still flags one you only commented on or asked changes for", () => {
    const marks = marksAfterFirstLoad();
    expect(marks.isNew(pullRequest({ myReview: "COMMENTED" }), "kjellerstedt")).toBe(true);
    expect(marks.isNew(pullRequest({ myReview: "CHANGES_REQUESTED" }), "kjellerstedt")).toBe(true);
  });

  it("does not flag your own pull request or a merged one", () => {
    const marks = marksAfterFirstLoad();
    expect(marks.isNew(pullRequest({ author: "kjellerstedt" }), "kjellerstedt")).toBe(false);
    expect(marks.isNew(pullRequest({ state: "MERGED" }), "kjellerstedt")).toBe(false);
  });
});
