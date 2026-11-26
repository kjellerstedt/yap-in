import { inject, type InjectionKey, provide } from "vue";
import type { PullRequest } from "../inbox/types";
import type { Reviews } from "./useReviews";
import type { InboxMarks } from "./useInboxMarks";
import type { Settings } from "./useSettings";

/** What every group and row needs from the page: settings, your marks, review state and the loaded inbox. */
export interface InboxContext {
  settings: Settings;
  marks: InboxMarks;
  reviews: Reviews;
  viewer: () => string;
  allPrs: () => PullRequest[];
}

const INBOX_CONTEXT: InjectionKey<InboxContext> = Symbol("inbox context");

export function provideInboxContext(context: InboxContext): void {
  provide(INBOX_CONTEXT, context);
}

export function useInboxContext(): InboxContext {
  const context = inject(INBOX_CONTEXT);
  if (context === undefined) throw new Error("useInboxContext is used outside the inbox page");
  return context;
}
