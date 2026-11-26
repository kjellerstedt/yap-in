<script setup lang="ts">
import { computed, nextTick } from "vue";
import { buildGroupTree, type GroupNode } from "../inbox/grouping";
import { STATUS_TONE, isPullRequestStatus } from "../inbox/status";
import { isOwnPr } from "../inbox/visibility";
import { useInboxContext } from "../composables/inboxContext";
import PullRequestRow from "./PullRequestRow.vue";

defineOptions({ name: "GroupList" });

const props = defineProps<{ nodes: GroupNode[]; depth: number }>();
const { settings, marks, viewer } = useInboxContext();

interface RenderedGroup {
  node: GroupNode;
  label: string;
  isFolded: boolean;
  // A folded group that holds your own PRs stays open showing only those; unfolding it shows the rest.
  // Folding a top-level group (e.g. an organisation) hides everything in it, yours included.
  showsOnlyOwnPrs: boolean;
  shownPrs: GroupNode["items"];
  shownChildren: GroupNode[] | null;
  tone: string | null;
}

const renderedGroups = computed<RenderedGroup[]>(() => {
  const groups = props.nodes.map((node) => {
    const isFolded = marks.collapsedGroupIds.has(node.id);
    const ownPrs = isFolded && props.depth > 0 ? node.items.filter((pr) => isOwnPr(pr, viewer())) : [];
    const showsOnlyOwnPrs = ownPrs.length > 0;
    return {
      node,
      // Under an organisation group, "acme/search" reads better as "search".
      label: node.field === "repo" && settings.groups.slice(0, props.depth).includes("owner") ? (node.key.split("/")[1] ?? node.key) : node.key,
      isFolded,
      showsOnlyOwnPrs,
      shownPrs: showsOnlyOwnPrs ? ownPrs : node.items,
      shownChildren: node.children && showsOnlyOwnPrs
        ? buildGroupTree(ownPrs, settings.groups.slice(props.depth + 1), settings.sort, marks.isDone, node.id)
        : node.children,
      tone: node.field === "status" && isPullRequestStatus(node.key) ? STATUS_TONE[node.key] : null,
    };
  });
  // Folded groups sink, except ones still showing your own PRs. Status groups keep their fixed order so each status
  // is always in the same place.
  if (props.nodes[0]?.field === "status") return groups;
  const sinks = (group: RenderedGroup) => group.isFolded && (props.depth === 0 || group.showsOnlyOwnPrs === false);
  return [...groups.filter((group) => sinks(group) === false), ...groups.filter(sinks)];
});

const headerElement = () => document.getElementById("header");

// Opening a group moves it up to its sorted place, so follow it there. Folding a header pinned at the top sends it
// down; show the group that takes its place rather than leaving you mid-list.
async function toggleGroup(group: RenderedGroup, event: MouseEvent) {
  if ((event.target as HTMLElement).closest("a")) return;
  event.preventDefault();
  const details = (event.currentTarget as HTMLElement).parentElement as HTMLDetailsElement;
  const summary = event.currentTarget as HTMLElement;
  const isPinned = details.getBoundingClientRect().top < summary.getBoundingClientRect().top - 1;
  const groupIdToFollow = group.isFolded || group.showsOnlyOwnPrs
    ? group.node.id
    : isPinned ? ((details.nextElementSibling as HTMLElement | null)?.dataset.key ?? group.node.id) : null;

  marks.setGroupFolded(group.node.id, group.isFolded === false);
  await nextTick();
  if (groupIdToFollow === null) return;
  const target = document.querySelector<HTMLElement>(`#list details[data-key="${CSS.escape(groupIdToFollow)}"]`);
  if (target === null) return;
  const top = target.getBoundingClientRect().top;
  const headerBottom = headerElement()?.getBoundingClientRect().bottom ?? 0;
  if (top < headerBottom || top > innerHeight) target.scrollIntoView({ block: "start", behavior: "smooth" });
}
</script>

<template>
  <details
    v-for="group in renderedGroups"
    :key="group.node.id"
    class="group"
    :class="{ done: group.node.items.every(marks.isDone), 'shows-only-own': group.showsOnlyOwnPrs }"
    :style="{ '--depth': depth, ...(group.tone ? { '--tone': group.tone } : {}) }"
    :data-field="group.node.field"
    :data-key="group.node.id"
    :open="group.isFolded === false || group.showsOnlyOwnPrs"
  >
    <summary @click="toggleGroup(group, $event)">
      <svg class="chev" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 4 4 4-4 4" /></svg>
      <span class="label">{{ group.label }}</span>
      <span class="count">{{ group.node.items.length }}</span>
      <a v-if="group.node.field === 'repo'" class="sub" :href="`https://github.com/${group.node.key}/pulls`" target="_blank" rel="noopener">open on GitHub ↗</a>
    </summary>
    <GroupList v-if="group.shownChildren" :nodes="group.shownChildren" :depth="depth + 1" />
    <div v-else-if="depth >= 2" class="rows">
      <PullRequestRow v-for="pr in group.shownPrs" :key="pr.id" :pr="pr" :depth="depth" />
    </div>
    <template v-else>
      <PullRequestRow v-for="pr in group.shownPrs" :key="pr.id" :pr="pr" :depth="depth" />
    </template>
  </details>
</template>
