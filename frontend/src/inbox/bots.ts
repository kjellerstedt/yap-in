import type { PullRequest } from "./types";

export type BotKind = "dependabot" | "aikido";

const BOT_AUTHOR = /\[bot\]$|^(dependabot|renovate|github-actions)/i;

export function isBotAuthor(pr: PullRequest): boolean {
  return BOT_AUTHOR.test(pr.author);
}

// Automated PRs get their own row style. Aikido opens PRs from a shared account, so match its branch prefix instead.
export function botKind(pr: PullRequest): BotKind | null {
  if (/^dependabot/i.test(pr.author)) return "dependabot";
  if (pr.head.startsWith("aikido/")) return "aikido";
  return null;
}

export type SemverBump = "major" | "minor" | "patch";

export type DependabotTitle =
  | { kind: "single"; packageName: string; from: string; to: string; bump: SemverBump }
  | { kind: "group"; groupName: string; updateCount: string | null }
  | { kind: "other"; text: string };

function semverBump(from: string, to: string): SemverBump {
  const [fromParts, toParts] = [from, to].map((version) => version.replace(/^v/, "").split("."));
  if (fromParts?.[0] !== toParts?.[0]) return "major";
  return fromParts?.[1] !== toParts?.[1] ? "minor" : "patch";
}

// Turns "chore(deps): bump nock from 15.0.2 to 15.0.4 in /x" into package + version change.
export function dependabotTitle(title: string): DependabotTitle {
  const single = title.match(/bump (\S+) from (\S+) to (\S+)/i);
  if (single) {
    const [, packageName = "", from = "", to = ""] = single;
    return { kind: "single", packageName, from, to, bump: semverBump(from, to) };
  }
  const group = title.match(/bump the (\S+) group.*?(?:with (\d+) updates?)?$/i);
  if (group) return { kind: "group", groupName: group[1] ?? "", updateCount: group[2] ?? null };
  return { kind: "other", text: title.replace(/^chore\(deps(-dev)?\):\s*/, "") };
}

export function aikidoTitle(title: string): string {
  return title.replace(/^fix(\([^)]*\))?:\s*/i, "");
}
