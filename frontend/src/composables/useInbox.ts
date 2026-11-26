import { onBeforeUnmount, ref } from "vue";
import type { InboxView } from "../inbox/types";

// Matches the server's background refresh; polling only re-reads its cache.
const POLL_INTERVAL_MS = 60_000;

async function errorMessage(response: Response): Promise<string> {
  try {
    const body: unknown = await response.json();
    if (typeof body === "object" && body !== null && "error" in body && typeof body.error === "string") return body.error;
  } catch {
    // Not JSON: fall through to the status.
  }
  return `The server answered ${response.status}`;
}

export function useInbox(onLoaded: (inbox: InboxView) => void) {
  const inbox = ref<InboxView | null>(null);
  const isLoading = ref(false);
  const loadError = ref<string | null>(null);

  async function load(rebuild = false): Promise<void> {
    isLoading.value = true;
    try {
      // Without state and days the server lists the open pull requests.
      const response = await fetch(rebuild ? "/api/prs?refresh=1" : "/api/prs");
      if (response.ok === false) throw new Error(await errorMessage(response));
      const loaded = (await response.json()) as InboxView;
      inbox.value = loaded;
      loadError.value = null;
      onLoaded(loaded);
    } catch (error) {
      loadError.value = error instanceof Error ? error.message : String(error);
    } finally {
      isLoading.value = false;
    }
  }

  const pollTimer = setInterval(() => {
    if (document.visibilityState === "visible") void load();
  }, POLL_INTERVAL_MS);
  onBeforeUnmount(() => clearInterval(pollTimer));

  return { inbox, isLoading, loadError, load };
}
