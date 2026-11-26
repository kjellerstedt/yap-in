import { watchEffect } from "vue";
import type { Settings } from "./useSettings";

// No attribute means the OS decides (see the light tokens in styles.css). index.html sets it before the first paint.
export function useDocumentTheme(settings: Settings): void {
  watchEffect(() => {
    if (settings.theme === "system") delete document.documentElement.dataset.theme;
    else document.documentElement.dataset.theme = settings.theme;
  });
}
