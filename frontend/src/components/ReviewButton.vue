<script setup lang="ts">
import { computed } from "vue";
import type { PullRequest } from "../inbox/types";
import { useInboxContext } from "../composables/inboxContext";

const props = defineProps<{ pr: PullRequest }>();
const { reviews, marks } = useInboxContext();

const reviewUrl = computed(() => reviews.reviewUrlByPrUrl.get(props.pr.url) ?? null);
const isStarting = computed(() => reviews.startingPrUrls.has(props.pr.url));
const opensGitHub = computed(() => reviews.reviewTool.value === "github");

function startReview() {
  marks.markSeen(props.pr.id);
  void reviews.start(props.pr.url);
}
</script>

<template>
  <a v-if="opensGitHub" class="review-control" :href="`${pr.url}/files`" target="_blank" rel="noopener" title="Open this PR's changes on GitHub" @click="marks.markSeen(pr.id)">Review ↗</a>
  <a v-else-if="reviewUrl" class="review-control ready" :href="reviewUrl" target="_blank" rel="noopener" title="Cmd- or middle-click to open in a background tab">Open review ↗</a>
  <button v-else-if="isStarting" class="review-control" disabled>Starting review…</button>
  <button v-else class="review-control" title="Start a code review of this PR with an agent session attached" @click="startReview">Review</button>
</template>
