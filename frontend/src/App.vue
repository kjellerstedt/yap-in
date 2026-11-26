<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watchEffect } from "vue";
import { botKind, isBotAuthor } from "./inbox/bots";
import { allGroupIds, buildGroupTree, sortKeepingStacks } from "./inbox/grouping";
import { matchesSearch } from "./inbox/search";
import { status, type PullRequestStatus } from "./inbox/status";
import { relativeTime } from "./inbox/time";
import type { PullRequest } from "./inbox/types";
import { isHiddenFromList, type ListVisibility } from "./inbox/visibility";
import { provideInboxContext } from "./composables/inboxContext";
import { useReviews } from "./composables/useReviews";
import { useInbox } from "./composables/useInbox";
import { useInboxMarks } from "./composables/useInboxMarks";
import { useDocumentTheme } from "./composables/useDocumentTheme";
import { useSettings } from "./composables/useSettings";
import GroupList from "./components/GroupList.vue";
import PullRequestRow from "./components/PullRequestRow.vue";
import SettingsPanel from "./components/SettingsPanel.vue";
import StatusCards from "./components/StatusCards.vue";

const HIDE_TOGGLES = [
  { setting: "hideDrafts", name: "Drafts" },
  { setting: "hideBots", name: "Bots" },
  { setting: "hideDone", name: "Done" },
] as const;

const settings = useSettings();
useDocumentTheme(settings);
const marks = useInboxMarks();
const reviews = useReviews();
const { inbox, isLoading, loadError, load } = useInbox((loaded) => {
  marks.refreshSeen(loaded.prs);
  marks.markApprovedDone(loaded.prs);
});
const viewer = () => inbox.value?.viewer ?? "";
const allPrs = () => inbox.value?.prs ?? [];
provideInboxContext({ settings, marks, reviews, viewer, allPrs });

const searchText = ref("");
const searchInput = ref<HTMLInputElement | null>(null);
const header = ref<HTMLElement | null>(null);
const toolbar = ref<HTMLElement | null>(null);
const settingsPill = ref<HTMLButtonElement | null>(null);
const isSettingsOpen = ref(false);

// The toolbar filters except "Hide done": the status cards count done PRs as hidden rather than leaving them out.
const prsMatchingFiltersOtherThanDone = computed(() => {
  const query = searchText.value.trim();
  return allPrs().filter((pr) =>
    (settings.hideDrafts === false || pr.isDraft === false) &&
    (settings.hideBots === false || (isBotAuthor(pr) === false && botKind(pr) === null)) &&
    (settings.relations.length === 0 || pr.relations.some((relation) => settings.relations.includes(relation))) &&
    (query === "" || matchesSearch(pr, query)));
});

const listVisibility = computed<ListVisibility>(() => ({
  groups: settings.groups,
  collapsedGroupIds: marks.collapsedGroupIds,
  viewer: viewer(),
  hideDone: settings.hideDone,
  isDone: marks.isDone,
}));

// Each card counts the PRs you can see; done PRs and those in folded groups are counted as hidden.
const countsByStatus = computed(() => {
  const visibleCountByStatus: Partial<Record<PullRequestStatus, number>> = {};
  const hiddenCountByStatus: Partial<Record<PullRequestStatus, number>> = {};
  for (const pr of prsMatchingFiltersOtherThanDone.value) {
    const counts = isHiddenFromList(pr, listVisibility.value) ? hiddenCountByStatus : visibleCountByStatus;
    counts[status(pr)] = (counts[status(pr)] ?? 0) + 1;
  }
  return { visibleCountByStatus, hiddenCountByStatus };
});

const listedPrs = computed(() => sortKeepingStacks(
  prsMatchingFiltersOtherThanDone.value.filter((pr) =>
    (settings.hideDone === false || marks.isDone(pr) === false) &&
    (settings.statuses.length === 0 || settings.statuses.includes(status(pr))) &&
    settings.excludedStatuses.includes(status(pr)) === false),
  settings.sort,
));

const groupTree = computed(() => buildGroupTree(listedPrs.value, settings.groups, settings.sort, marks.isDone));

// The pill's badge counts the panel's filters, which can't be seen while it's closed.
const panelFiltersOnCount = computed(() => settings.relations.length);

const metaText = computed(() => {
  if (inbox.value === null) return "Loading…";
  const repositoryCount = new Set(listedPrs.value.map((pr) => pr.repo)).size;
  return `${listedPrs.value.length} open across ${repositoryCount} repositories · @${inbox.value.viewer} · fetched ${relativeTime(inbox.value.fetchedAt)}`;
});

watchEffect(() => {
  const visibleNewPrCount = listedPrs.value.filter((pr: PullRequest) =>
    marks.isNew(pr, viewer()) && isHiddenFromList(pr, listVisibility.value) === false).length;
  document.title = visibleNewPrCount > 0 ? `(${visibleNewPrCount}) yap-in` : "yap-in";
});

function setAllGroupsFolded(folded: boolean) {
  marks.setAllGroupsFolded(allGroupIds(groupTree.value), folded);
}

async function toggleSettings(event: MouseEvent) {
  isSettingsOpen.value = !isSettingsOpen.value;
  // A keyboard press (no pointer clicks) moves focus into the panel, which sits after the bar in tab order anyway.
  if (isSettingsOpen.value && event.detail === 0) {
    await nextTick();
    document.querySelector<HTMLElement>("#settings-panel button:not([disabled])")?.focus();
  }
}

function closeSettingsReturningFocus() {
  isSettingsOpen.value = false;
  settingsPill.value?.focus();
}

function closeSettingsOnClickOutside(event: MouseEvent) {
  if (isSettingsOpen.value === false) return;
  const target = event.target as Element;
  if (target.closest("#settings-panel") || settingsPill.value?.contains(target)) return;
  // A click on the search or a hide toggle keeps the focus it gave them.
  if (toolbar.value?.contains(target)) isSettingsOpen.value = false;
  else closeSettingsReturningFocus();
}

function closeSettingsWhenFocusLeaves(event: FocusEvent) {
  if (isSettingsOpen.value && toolbar.value?.contains(event.target as Node) === false) isSettingsOpen.value = false;
}

function onKeydown(event: KeyboardEvent) {
  const target = event.target as HTMLElement;
  if (event.key === "Escape" && isSettingsOpen.value) {
    closeSettingsReturningFocus();
    return;
  }
  if (target.matches("input, select")) {
    if (event.key === "Escape") target.blur();
    return;
  }
  if (event.metaKey || event.ctrlKey || event.altKey) return;
  if (event.key === "/") { event.preventDefault(); searchInput.value?.focus(); }
  if (event.key === "r") void load(true);
  if (event.key === "e") setAllGroupsFolded(false);
  if (event.key === "c") setAllGroupsFolded(true);
}

// Sticky group headers sit below the header, whose height depends on how the toolbar wraps. The open settings panel
// floats out of the header, so it doesn't count.
const headerResizeObserver = new ResizeObserver(([entry]) => {
  const height = entry?.borderBoxSize[0]?.blockSize;
  if (height !== undefined) document.documentElement.style.setProperty("--header-h", `${height}px`);
});

onMounted(() => {
  document.addEventListener("keydown", onKeydown);
  document.addEventListener("click", closeSettingsOnClickOutside);
  document.addEventListener("focusin", closeSettingsWhenFocusLeaves);
  if (header.value) headerResizeObserver.observe(header.value);
  void load();
});
onBeforeUnmount(() => {
  document.removeEventListener("keydown", onKeydown);
  document.removeEventListener("click", closeSettingsOnClickOutside);
  document.removeEventListener("focusin", closeSettingsWhenFocusLeaves);
  headerResizeObserver.disconnect();
});
</script>

<template>
  <div class="loading" :class="{ on: isLoading }"></div>
  <header id="header" ref="header">
    <div ref="toolbar" class="toolbar" :class="{ open: isSettingsOpen }">
      <div class="toolbar-row">
        <span class="brand">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="6" cy="6" r="2.5" /><circle cx="18" cy="18" r="2.5" /><circle cx="6" cy="18" r="2.5" /><path d="M6 8.5v7M18 15.5V10a3 3 0 0 0-3-3h-4" /><path d="m13 5-2 2 2 2" /></svg>
          yap-in
        </span>
        <label class="search">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></svg>
          <input id="q" ref="searchInput" v-model="searchText" type="search" placeholder="Filter: text, repo:, author:, label:, -term" aria-label="Filter pull requests" autocomplete="off">
          <kbd>/</kbd>
        </label>
        <div class="hide-toggles" role="group" aria-label="Hide">
          <span class="hide-label" aria-hidden="true">Hide</span>
          <button
            v-for="hideToggle in HIDE_TOGGLES"
            :key="hideToggle.setting"
            type="button"
            :title="`Hide ${hideToggle.name.toLowerCase()}`"
            :aria-pressed="settings[hideToggle.setting]"
            @click="settings[hideToggle.setting] = !settings[hideToggle.setting]"
          >{{ hideToggle.name }}</button>
        </div>
        <button
          ref="settingsPill"
          type="button"
          class="settings-pill"
          aria-controls="settings-panel"
          :aria-expanded="isSettingsOpen"
          @click="toggleSettings"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z" /></svg>
          Settings
          <span v-if="panelFiltersOnCount" class="settings-badge" title="Relation filters on">{{ panelFiltersOnCount }}</span>
        </button>
        <button type="button" class="icon-button" :class="{ loading: isLoading }" title="Refresh (r)" aria-label="Refresh" :disabled="isLoading" @click="load(true)">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20 12a8 8 0 1 1-2.34-5.66" /><path d="M20 4v5h-5" /></svg>
        </button>
      </div>
      <SettingsPanel v-if="isSettingsOpen" @expand-all="setAllGroupsFolded(false)" @collapse-all="setAllGroupsFolded(true)" />
    </div>
  </header>
  <main :class="{ dimmed: isSettingsOpen }">
    <section class="hero">
      <h1>Yapping from your pull requests<span class="dim">, in one place.</span></h1>
      <p>{{ metaText }}</p>
      <StatusCards v-bind="countsByStatus" />
    </section>
    <div id="list">
      <div v-if="loadError && inbox === null" class="error">Failed to load PRs:
{{ loadError }}</div>
      <div v-else-if="inbox && listedPrs.length === 0" class="empty">Nothing here. Inbox zero 🎉</div>
      <div v-else-if="groupTree === null && inbox" class="group">
        <PullRequestRow v-for="pr in listedPrs" :key="pr.id" :pr="pr" :depth="0" />
      </div>
      <GroupList v-else-if="groupTree" :nodes="groupTree" :depth="0" />
    </div>
  </main>
</template>
