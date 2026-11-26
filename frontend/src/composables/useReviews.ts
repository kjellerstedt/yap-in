import { reactive, ref } from "vue";
import type { ReviewToolResponse, StartReviewResponse } from "../inbox/types";

// Kept for this page load only: the review pages started, by PR, and the PRs still starting one.
// A page cannot open a background tab, so a ready review becomes a link on the row to Cmd- or middle-click.
export function useReviews() {
  const reviewUrlByPrUrl = reactive(new Map<string, string>());
  const startingPrUrls = reactive(new Set<string>());
  // What setup chose for the Review button; null until the server has said.
  const reviewTool = ref<string | null>(null);

  void fetch("/api/review-tool")
    .then((response) => (response.ok ? (response.json() as Promise<ReviewToolResponse>) : null))
    .then((body) => {
      if (body !== null) reviewTool.value = body.reviewTool;
    })
    .catch(() => undefined);

  async function start(prUrl: string): Promise<void> {
    startingPrUrls.add(prUrl);
    try {
      const response = await fetch(`/api/reviews?${new URLSearchParams({ url: prUrl })}`, { method: "POST" });
      const body = (await response.json()) as Partial<StartReviewResponse> & { error?: string };
      if (response.ok === false) throw new Error(body.error ?? `The server answered ${response.status}`);
      // Only a local review server is linked to, whatever the response holds.
      if (typeof body.reviewUrl === "string" && body.reviewUrl.startsWith("http://localhost:")) reviewUrlByPrUrl.set(prUrl, body.reviewUrl);
      else alert(`Agent session started in ${body.sessionHost}, but the review tool gave no review page. Carry on in the session.\n\n${body.toolOutput ?? ""}`);
    } catch (error) {
      alert(`Review failed to start: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      startingPrUrls.delete(prUrl);
    }
  }

  return { reviewUrlByPrUrl, startingPrUrls, reviewTool, start };
}

export type Reviews = ReturnType<typeof useReviews>;
