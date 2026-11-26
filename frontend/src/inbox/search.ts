import { status } from "./status";
import type { PullRequest } from "./types";

// Plain words plus repo:, author:, label:, is: (draft or a status) and -negation.
export function matchesSearch(pr: PullRequest, query: string): boolean {
  const haystack = `${pr.title} ${pr.repo} #${pr.number} ${pr.author} ${pr.head} ${pr.labels.map((label) => label.name).join(" ")}`.toLowerCase();
  return query.toLowerCase().split(/\s+/).filter(Boolean).every((rawTerm) => {
    const negated = rawTerm.startsWith("-") && rawTerm.length > 1;
    const term = negated ? rawTerm.slice(1) : rawTerm;
    const [key, ...rest] = term.split(":");
    const value = rest.join(":");
    let hit: boolean;
    if (value && key === "repo") hit = pr.repo.toLowerCase().includes(value);
    else if (value && key === "author") hit = pr.author.toLowerCase().includes(value);
    else if (value && key === "label") hit = pr.labels.some((label) => label.name.toLowerCase().includes(value));
    else if (value && key === "is") hit = value === "draft" ? pr.isDraft : status(pr).toLowerCase().includes(value);
    else hit = haystack.includes(term);
    return negated ? hit === false : hit;
  });
}
