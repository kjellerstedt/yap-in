import { describe, expect, it } from "vitest";
import { settingsFromStored } from "../composables/useSettings";
import { dependabotTitle } from "./bots";
import { allGroupIds, buildGroupTree, sortKeepingStacks } from "./grouping";
import { matchesSearch } from "./search";
import { isMyApprovalStale, isOwnPrAwaitingFeedback, staleApprovers, status } from "./status";
import type { PullRequest } from "./types";
import { isHiddenFromList, type ListVisibility } from "./visibility";

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

describe("status", () => {
  it("puts the PR's own problems before its review result", () => {
    expect(status(pullRequest({ reviewDecision: "APPROVED", mergeable: "CONFLICTING" }))).toBe("Merge conflict");
    expect(status(pullRequest({ reviewDecision: "APPROVED", isDraft: true }))).toBe("Draft");
    expect(status(pullRequest({ reviewDecision: "APPROVED" }))).toBe("Approved");
    expect(status(pullRequest({ state: "MERGED", reviewDecision: "APPROVED" }))).toBe("Merged");
  });

  it("asks for your review only until you have reviewed", () => {
    expect(status(pullRequest({ relations: ["review-you"] }))).toBe("Needs your review");
    expect(status(pullRequest({ relations: ["review-you"], myReview: "COMMENTED" }))).toBe("Waiting for review");
  });

  // acme/billing#385: one reviewer requested changes and never dismissed them; another approved two days later.
  const changesRequestedOn23Nov = { login: "toni", state: "CHANGES_REQUESTED", submittedAt: "2026-11-23T09:56:15Z" };
  const approvedOn25Nov = { login: "johannes", state: "APPROVED", submittedAt: "2026-11-25T12:02:45Z" };

  it("reads as approved when an approval came after every request for changes", () => {
    const pr = pullRequest({ reviewDecision: "CHANGES_REQUESTED", reviews: [changesRequestedOn23Nov, approvedOn25Nov] });
    expect(status(pr)).toBe("Approved after changes requested");
  });

  it("keeps changes requested when they came after the approval", () => {
    const approvedOn22Nov = { ...approvedOn25Nov, submittedAt: "2026-11-22T10:00:00Z" };
    const pr = pullRequest({ reviewDecision: "CHANGES_REQUESTED", reviews: [changesRequestedOn23Nov, approvedOn22Nov] });
    expect(status(pr)).toBe("Changes requested");
  });

  it("puts a merge conflict before an approval that came after changes were requested", () => {
    const pr = pullRequest({
      reviewDecision: "CHANGES_REQUESTED", mergeable: "CONFLICTING", reviews: [changesRequestedOn23Nov, approvedOn25Nov],
    });
    expect(status(pr)).toBe("Merge conflict");
  });
});

describe("stale approvals", () => {
  const approvedThenPushed = { myReview: "APPROVED", myReviewAt: "2026-11-20T08:01:49Z", lastCommitAt: "2026-11-20T09:12:39Z" };

  it("counts an approval as stale when commits came after it and the PR is not approved overall", () => {
    expect(isMyApprovalStale(pullRequest(approvedThenPushed))).toBe(true);
  });

  it("keeps an approval that still counts", () => {
    expect(isMyApprovalStale(pullRequest({ ...approvedThenPushed, reviewDecision: "APPROVED" }))).toBe(false);
    expect(isMyApprovalStale(pullRequest({ ...approvedThenPushed, lastCommitAt: "2026-11-20T07:00:00Z" }))).toBe(false);
  });
});

describe("stale approvers", () => {
  // TV4/tag-manager#100: approved at 09:06, two more commits pushed afterwards, GitHub reports REVIEW_REQUIRED.
  const approvedThenPushed = {
    reviews: [{ login: "pelto", state: "APPROVED", submittedAt: "2026-11-26T09:06:03Z" }], lastCommitAt: "2026-11-26T11:03:59Z",
  };

  it("lists reviewers who approved before the newest commit while the PR is not approved overall", () => {
    expect(staleApprovers(pullRequest(approvedThenPushed))).toEqual(["pelto"]);
  });

  it("lists nobody when the approval still counts or came after the newest commit", () => {
    expect(staleApprovers(pullRequest({ ...approvedThenPushed, reviewDecision: "APPROVED" }))).toEqual([]);
    expect(staleApprovers(pullRequest({ ...approvedThenPushed, lastCommitAt: "2026-11-26T08:00:00Z" }))).toEqual([]);
    expect(staleApprovers(pullRequest({ ...approvedThenPushed, state: "MERGED" }))).toEqual([]);
  });
});

describe("approved through an outdated approval", () => {
  // TV4/search#270: approved before the last push, GitHub says REVIEW_REQUIRED but the PR is CLEAN to merge.
  const approvedThenPushed = {
    reviews: [{ login: "pelto", state: "APPROVED", submittedAt: "2026-11-26T10:18:39Z" }], lastCommitAt: "2026-11-26T10:52:24Z",
  };

  it("counts as Approved when GitHub says it can be merged", () => {
    expect(status(pullRequest({ ...approvedThenPushed, mergeStateStatus: "CLEAN" }))).toBe("Approved");
  });

  it("keeps waiting when the PR is blocked, behind, or has no approval at all", () => {
    expect(status(pullRequest({ ...approvedThenPushed, mergeStateStatus: "BLOCKED" }))).toBe("Waiting for review");
    expect(status(pullRequest({ ...approvedThenPushed, mergeStateStatus: "BEHIND" }))).toBe("Waiting for review");
    expect(status(pullRequest({ mergeStateStatus: "CLEAN", lastCommitAt: "2026-11-26T10:52:24Z" }))).toBe("Waiting for review");
  });

  it("still reports a conflict", () => {
    expect(status(pullRequest({ ...approvedThenPushed, mergeStateStatus: "CLEAN", mergeable: "CONFLICTING" }))).toBe("Merge conflict");
  });
});

describe("own PR awaiting feedback", () => {
  const ownPr = { author: "octocat", relations: ["authored" as const] };

  it("waits until someone else reviews or comments", () => {
    expect(isOwnPrAwaitingFeedback(pullRequest(ownPr), "octocat")).toBe(true);
    expect(isOwnPrAwaitingFeedback(pullRequest({ ...ownPr, reviews: [{ login: "octocat", state: "COMMENTED", submittedAt: null }] }), "octocat")).toBe(true);
    expect(isOwnPrAwaitingFeedback(pullRequest({ ...ownPr, reviews: [{ login: "anna", state: "COMMENTED", submittedAt: null }] }), "octocat")).toBe(false);
    expect(isOwnPrAwaitingFeedback(pullRequest({ ...ownPr, comments: { human: 1, bot: 0 } }), "octocat")).toBe(false);
  });

  it("ignores bot comments, drafts and other people's PRs", () => {
    expect(isOwnPrAwaitingFeedback(pullRequest({ ...ownPr, comments: { human: 0, bot: 3 } }), "octocat")).toBe(true);
    expect(isOwnPrAwaitingFeedback(pullRequest({ ...ownPr, isDraft: true }), "octocat")).toBe(false);
    expect(isOwnPrAwaitingFeedback(pullRequest(), "octocat")).toBe(false);
  });
});

describe("folding", () => {
  const mine = pullRequest({ id: "MINE", author: "octocat", repo: "acme/billing" });
  const theirs = pullRequest({ id: "THEIRS", repo: "acme/billing" });
  const visibility = (collapsed: string[], overrides: Partial<ListVisibility> = {}): ListVisibility => ({
    groups: ["owner", "repo"], collapsedGroupIds: new Set(collapsed), viewer: "octocat", hideDone: true, isDone: () => false, ...overrides,
  });

  it("keeps your own PRs visible under a folded repo", () => {
    const repoFolded = visibility(["owner:acme/repo:acme/billing"]);
    expect(isHiddenFromList(mine, repoFolded)).toBe(false);
    expect(isHiddenFromList(theirs, repoFolded)).toBe(true);
  });

  it("hides everything, yours included, under a folded organisation", () => {
    expect(isHiddenFromList(mine, visibility(["owner:acme"]))).toBe(true);
  });

  it("hides done PRs only while Hide done is on", () => {
    expect(isHiddenFromList(theirs, visibility([], { isDone: () => true }))).toBe(true);
    expect(isHiddenFromList(theirs, visibility([], { isDone: () => true, hideDone: false }))).toBe(false);
  });

  it("keeps a PR in several groups visible while any of its paths is open", () => {
    const twoLabels = pullRequest({ labels: [{ name: "a", color: "000000" }, { name: "b", color: "000000" }] });
    const labelVisibility = (collapsed: string[]) => visibility(collapsed, { groups: ["label"] });
    expect(isHiddenFromList(twoLabels, labelVisibility(["label:a"]))).toBe(false);
    expect(isHiddenFromList(twoLabels, labelVisibility(["label:a", "label:b"]))).toBe(true);
  });
});

describe("grouping", () => {
  it("nests groups by path and keeps statuses in their fixed order", () => {
    const tree = buildGroupTree([
      pullRequest({ id: "A", reviewDecision: "APPROVED" }),
      pullRequest({ id: "B", mergeable: "CONFLICTING" }),
    ], ["owner", "status"], "updated", () => false);

    expect(tree?.map((node) => node.id)).toEqual(["owner:acme"]);
    expect(tree?.[0]?.children?.map((node) => node.key)).toEqual(["Merge conflict", "Approved"]);
    expect(allGroupIds(tree)).toEqual(["owner:acme", "owner:acme/status:Merge conflict", "owner:acme/status:Approved"]);
  });

  it("puts groups whose PRs are all done after the others at every level, except statuses", () => {
    const infra = (id: string, updatedAt: string) => pullRequest({ id, updatedAt, repo: "acme/infra", reviewDecision: "APPROVED" });
    const doneIds = new Set(["INFRA_1", "INFRA_2", "OTHER_ORG"]);
    const tree = buildGroupTree([
      infra("INFRA_1", "2026-11-20T12:00:00Z"),
      infra("INFRA_2", "2026-11-20T11:00:00Z"),
      pullRequest({ id: "SEARCH", updatedAt: "2026-11-20T10:00:00Z", mergeable: "CONFLICTING" }),
      pullRequest({ id: "OTHER_ORG", updatedAt: "2026-11-20T13:00:00Z", owner: "globex", repo: "globex/web" }),
    ], ["owner", "repo", "status"], "updated", (pr) => doneIds.has(pr.id));

    expect(tree?.map((node) => node.key)).toEqual(["acme", "globex"]);
    expect(tree?.[0]?.children?.map((node) => node.key)).toEqual(["acme/search", "acme/infra"]);
    const searchStatuses = buildGroupTree([
      pullRequest({ id: "APPROVED", reviewDecision: "APPROVED" }),
      pullRequest({ id: "CONFLICT", mergeable: "CONFLICTING" }),
    ], ["status"], "updated", (pr) => pr.id === "CONFLICT");
    expect(searchStatuses?.map((node) => node.key)).toEqual(["Merge conflict", "Approved"]);
  });

  it("lists a stack bottom-up where its first member sorts", () => {
    const stacked = (id: string, position: number, updatedAt: string) =>
      pullRequest({ id, updatedAt, stack: { number: 7, size: 2, position } });
    const sorted = sortKeepingStacks([
      pullRequest({ id: "LONE", updatedAt: "2026-11-20T10:00:00Z" }),
      stacked("TOP", 2, "2026-11-20T11:00:00Z"),
      stacked("BOTTOM", 1, "2026-11-19T00:00:00Z"),
    ], "updated");
    expect(sorted.map((pr) => pr.id)).toEqual(["BOTTOM", "TOP", "LONE"]);
  });
});

describe("search", () => {
  it("combines words, qualifiers and negation", () => {
    const pr = pullRequest({ labels: [{ name: "bug", color: "ff0000" }] });
    expect(matchesSearch(pr, "tags repo:search label:bug")).toBe(true);
    expect(matchesSearch(pr, "-author:anna")).toBe(false);
    expect(matchesSearch(pr, "is:waiting")).toBe(true);
  });
});

describe("dependabot titles", () => {
  it("reads the package and how big the version change is", () => {
    expect(dependabotTitle("chore(deps): bump nock from 15.0.2 to 16.0.0 in /x")).toEqual(
      { kind: "single", packageName: "nock", from: "15.0.2", to: "16.0.0", bump: "major" });
  });
});

describe("saved settings", () => {
  it("keeps valid saved values and drops ones this page no longer knows", () => {
    const settings = settingsFromStored({
      groups: ["owner", "repo", "status", "age"], sort: "nonsense", state: "merged", days: "45",
      statuses: ["CI failing", "Approved"], excludedStatuses: ["Approved after changes requested"], relations: ["authored", "<script>"],
    });
    expect(settings.groups).toEqual(["owner", "repo", "status"]);
    expect(settings.sort).toBe("updated");
    expect(settings).not.toHaveProperty("state");
    expect(settings).not.toHaveProperty("days");
    expect(settings.theme).toBe("system");
    expect(settings.statuses).toEqual(["Approved"]);
    expect(settings.excludedStatuses).toEqual(["Approved after changes requested"]);
    expect(settings.relations).toEqual(["authored"]);
  });

  it("keeps a saved theme choice and ignores an unknown one", () => {
    expect(settingsFromStored({ theme: "dark" }).theme).toBe("dark");
    expect(settingsFromStored({ theme: "sepia" }).theme).toBe("system");
  });

  it("migrates the earliest single group setting and hides Merged when nothing was saved", () => {
    expect(settingsFromStored({ group: "repo" }).groups).toEqual(["repo"]);
    expect(settingsFromStored(undefined).excludedStatuses).toEqual(["Merged"]);
  });
});
