<script setup lang="ts">
import { computed, ref } from "vue";
import { aikidoTitle, botKind, dependabotTitle } from "../inbox/bots";
import { changesRequestedReviews, isMyApprovalStale, isOwnPrAwaitingFeedback, status, STATUS_BADGE_CLASS } from "../inbox/status";
import { dayAndMonth, relativeTime } from "../inbox/time";
import type { PullRequest } from "../inbox/types";
import { useInboxContext } from "../composables/inboxContext";
import { vReveal } from "../composables/reveal";
import { vTooltip } from "../composables/tooltip";
import ReviewButton from "./ReviewButton.vue";

const props = defineProps<{ pr: PullRequest; depth: number }>();
const { settings, marks, viewer, allPrs } = useInboxContext();

const bot = computed(() => botKind(props.pr));
const isMerged = computed(() => props.pr.state === "MERGED");
const isDone = computed(() => marks.isDone(props.pr));
const isNew = computed(() => marks.isNew(props.pr, viewer()));
const isAwaitingFeedback = computed(() => isOwnPrAwaitingFeedback(props.pr, viewer()));
// Done rows and merged ones keep one line: author and title.
const isCompact = computed(() => isDone.value || isMerged.value);
const dependabot = computed(() => (bot.value === "dependabot" ? dependabotTitle(props.pr.title) : null));
const avatarUrl = computed(() => (props.pr.authorAvatar ? `${props.pr.authorAvatar}&s=32` : null));

// One pill at most: the derived status. "Waiting for review" is the default state, so it stays quiet.
const statusPill = computed(() => {
  const prStatus = status(props.pr);
  if (prStatus === "Waiting for review" || settings.groups.includes("status")) return null;
  const text = prStatus === "Merged" && props.pr.mergedAt ? `merged ${relativeTime(props.pr.mergedAt)}` : prStatus.toLowerCase();
  return { text, badgeClass: STATUS_BADGE_CLASS[prStatus] };
});

const approval = computed(() => {
  if (props.pr.myReview !== "APPROVED" || isMerged.value) return null;
  return isMyApprovalStale(props.pr) ? "re-review" : "you approved";
});

// Position in the stack plus each member's status, so a stack whose bottom is approved but whose top waits on review
// is visible from any row.
const stackTooltip = computed(() => {
  const stack = props.pr.stack;
  if (stack === null) return "";
  const loadedMembers = allPrs().filter((member) => member.repo === props.pr.repo && member.stack?.number === stack.number);
  return Array.from({ length: stack.size }, (_, i) => {
    const position = i + 1;
    const member = loadedMembers.find((candidate) => candidate.stack?.position === position);
    const marker = position === stack.position ? "▸" : "  ";
    return member ? `${marker} #${member.number} ${member.head} — ${status(member).toLowerCase()}` : `${marker} ${position}. not in this view`;
  }).join("\n");
});

// Compact review state; each reviewer is a line in the tooltip.
const reviewSummary = computed(() => {
  const approvedCount = props.pr.reviews.filter((review) => review.state === "APPROVED").length;
  const changesCount = changesRequestedReviews(props.pr).length;
  const requestedCount = props.pr.reviewRequests.length;
  if (approvedCount === 0 && changesCount === 0 && requestedCount === 0) return null;
  const tooltip = [
    ...props.pr.reviews.map((review) => {
      const date = review.submittedAt ? ` · ${dayAndMonth(review.submittedAt)}` : "";
      if (review.state === "APPROVED") return `✓ ${review.login} approved${date}`;
      if (review.state === "CHANGES_REQUESTED") return `✗ ${review.login} requested changes${date}`;
      return `${review.login} ${review.state.toLowerCase().replaceAll("_", " ")}${date}`;
    }),
    ...props.pr.reviewRequests.map((reviewer) =>
      `⏳ ${reviewer.startsWith("team:") ? `${reviewer.slice("team:".length)} (team)` : reviewer} requested`),
  ].join("\n");
  return { approvedCount, changesCount, requestedCount, tooltip };
});

const copyLabel = ref("copy");
async function copyLink() {
  await navigator.clipboard.writeText(props.pr.url);
  copyLabel.value = "copied";
}

function toggleDone() {
  marks.toggleDone(props.pr);
  marks.markSeen(props.pr.id);
}
</script>

<template>
  <div
    v-reveal
    class="pr"
    :class="[bot ? `bot ${bot}` : '', { done: isDone, merged: isMerged, compact: isCompact, 'awaiting-feedback': isAwaitingFeedback }]"
    :data-id="pr.id"
    :style="{ '--depth': depth }"
  >
    <span class="ci" :class="pr.checks ?? 'none'" :title="`Checks: ${pr.checks ?? 'none'}`"></span>
    <div>
      <div class="title">
        <span v-if="bot === null" class="compact-author"><img v-if="avatarUrl" class="avatar" :src="avatarUrl" alt="">{{ pr.author }}</span>
        <a :href="pr.url" target="_blank" rel="noopener" :title="pr.title" @click="marks.markSeen(pr.id)" @auxclick="marks.markSeen(pr.id)">
          <template v-if="dependabot?.kind === 'single'">
            <span class="pkg">{{ dependabot.packageName }}</span>
            <span class="ver"> {{ dependabot.from }} → <span class="bump" :class="dependabot.bump" :title="`${dependabot.bump} update`">{{ dependabot.to }}</span></span>
          </template>
          <template v-else-if="dependabot?.kind === 'group'">
            <span class="pkg">{{ dependabot.groupName }}</span>
            <span class="ver"> group{{ dependabot.updateCount ? ` · ${dependabot.updateCount} updates` : "" }}</span>
          </template>
          <template v-else-if="dependabot?.kind === 'other'">{{ dependabot.text }}</template>
          <template v-else-if="bot === 'aikido'">{{ aikidoTitle(pr.title) }}</template>
          <template v-else>{{ pr.title }}</template>
        </a>
        <span class="num"> #{{ pr.number }}</span>
        <span v-if="approval === 're-review'" class="badge re-review" title="You approved before the latest commits, and that approval no longer counts">re-review</span>
        <span v-else-if="approval" class="badge approved-by-you" title="You approved this PR">you approved</span>
        <span v-if="statusPill" class="badge" :class="statusPill.badgeClass">{{ statusPill.text }}</span>
        <span v-if="pr.stack" v-tooltip="stackTooltip" class="badge stack" role="img">#{{ pr.stack.number }} · {{ pr.stack.position }}/{{ pr.stack.size }}</span>
      </div>
      <div class="line2">
        <a v-if="settings.groups.includes('repo') === false" :href="`https://github.com/${pr.repo}/pulls`" target="_blank" rel="noopener">{{ pr.repo }}</a>
        <span v-if="bot" class="botname">{{ bot }}</span>
        <span v-else><img v-if="avatarUrl" class="avatar" :src="avatarUrl" alt="">{{ pr.author }}</span>
        <span v-if="bot === null" class="mono branch" :title="`${pr.head} → ${pr.base}`">{{ pr.head }}</span>
        <span v-if="reviewSummary" v-tooltip="reviewSummary.tooltip" class="reviews" role="img">
          <span v-if="reviewSummary.approvedCount" class="add">✓{{ reviewSummary.approvedCount }}</span>
          <span v-if="reviewSummary.changesCount" class="del"> ✗{{ reviewSummary.changesCount }}</span>
          <span v-if="reviewSummary.requestedCount">
            <svg class="meta-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 3h12M6 21h12M7 3v3.5l5 5.5 5-5.5V3M7 21v-3.5l5-5.5 5 5.5V21" /></svg>{{ reviewSummary.requestedCount }}
          </span>
        </span>
        <span v-if="pr.labels.length" v-tooltip="pr.labels.map((label) => label.name).join(', ')" class="labels" role="img">
          <svg class="meta-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 4a1 1 0 0 1 1-1h7l10 10-8 8L3 11Z" /><circle cx="7.5" cy="7.5" r="1.5" /></svg>{{ pr.labels.length }}
        </span>
      </div>
    </div>
    <div class="right">
      <div>
        <button v-if="isNew" class="badge new" title="New since you last looked. Click to mark as seen" @click="marks.markSeen(pr.id)">new</button>
        <span :title="`Updated ${pr.updatedAt}`">{{ relativeTime(pr.updatedAt) }}</span>
      </div>
      <div class="diff-line">
        <span class="mono">
          <span class="add">+{{ pr.additions }}</span> <span class="del">−{{ pr.deletions }}</span>
          <template v-if="pr.comments.human"> · <span v-tooltip="`${pr.comments.human} human comments`" role="img">💬 {{ pr.comments.human }}</span></template>
          <template v-if="pr.comments.bot"> · <span v-tooltip="`${pr.comments.bot} bot comments`" role="img">🤖 {{ pr.comments.bot }}</span></template>
        </span>
        <ReviewButton v-if="isMerged === false" :pr="pr" />
      </div>
      <div v-if="isMerged === false" class="actions">
        <button v-if="isNew" class="badge new" title="New since you last looked. Click to mark as seen" @click="marks.markSeen(pr.id)">new</button>
        <button title="Copy link" @click="copyLink">{{ copyLabel }}</button>
        <button @click="toggleDone">{{ isDone ? "undo" : "done" }}</button>
      </div>
    </div>
  </div>
</template>
