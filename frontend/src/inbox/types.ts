// Mirrors API/Responses/PullRequestResponse.cs. PullRequestResponseShapeTests (in Tests/IntegrationTests) reads the
// field names below and fails when the two drift apart, so keep one field per line.

export type PullRequestRelation = "authored" | "review-you" | "review-team" | "reviewed" | "assigned" | "mentioned" | "involved";

export interface InboxView {
  viewer: string;
  fetchedAt: string;
  prs: PullRequest[];
}

export interface PullRequest {
  id: string;
  number: number;
  title: string;
  url: string;
  isDraft: boolean;
  state: "OPEN" | "MERGED" | "CLOSED";
  createdAt: string;
  updatedAt: string;
  mergedAt: string | null;
  closedAt: string | null;
  additions: number;
  deletions: number;
  changedFiles: number;
  mergeable: string;
  mergeStateStatus: string;
  reviewDecision: string | null;
  head: string;
  base: string;
  author: string;
  authorAvatar: string | null;
  repo: string;
  owner: string;
  isPrivate: boolean;
  comments: { human: number; bot: number };
  labels: { name: string; color: string }[];
  assignees: string[];
  reviewRequests: string[];
  reviews: { login: string; state: string; submittedAt: string | null }[];
  myReview: string | null;
  myReviewAt: string | null;
  lastCommitAt: string | null;
  checks: string | null;
  stack: { number: number; size: number; position: number } | null;
  relations: PullRequestRelation[];
}

export interface StartReviewResponse {
  reviewUrl: string | null;
  sessionHost: string;
  toolOutput: string | null;
}

export interface ReviewToolResponse {
  /** "github": the Review button links to the PR's changes; any other tool starts a review. */
  reviewTool: string;
}
