<script setup lang="ts">
import { computed } from "vue";
import { STATUS_ORDER, STATUS_TONE, type PullRequestStatus } from "../inbox/status";
import { useInboxContext } from "../composables/inboxContext";

const props = defineProps<{
  visibleCountByStatus: Partial<Record<PullRequestStatus, number>>;
  hiddenCountByStatus: Partial<Record<PullRequestStatus, number>>;
}>();
const { settings } = useInboxContext();

const cards = computed(() => STATUS_ORDER
  .filter((prStatus) => props.visibleCountByStatus[prStatus] || props.hiddenCountByStatus[prStatus] ||
    settings.statuses.includes(prStatus) || settings.excludedStatuses.includes(prStatus))
  .map((prStatus) => ({
    prStatus,
    visibleCount: props.visibleCountByStatus[prStatus] ?? 0,
    hiddenCount: props.hiddenCountByStatus[prStatus] ?? 0,
    label: prStatus === "Merged" ? "Merged in last 12h" : prStatus,
  })));

// A status is shown-only or hidden, never both, so turning one on clears the other.
function toggle(prStatus: PullRequestStatus, list: "statuses" | "excludedStatuses") {
  const opposingList = list === "statuses" ? "excludedStatuses" : "statuses";
  settings[opposingList] = settings[opposingList].filter((other) => other !== prStatus);
  settings[list] = settings[list].includes(prStatus) ? settings[list].filter((other) => other !== prStatus) : [...settings[list], prStatus];
}
</script>

<template>
  <div id="stats" class="stats">
    <button
      v-for="card in cards"
      :key="card.prStatus"
      class="stat"
      :class="{ on: settings.statuses.includes(card.prStatus), excluded: settings.excludedStatuses.includes(card.prStatus) }"
      :style="{ '--tone': STATUS_TONE[card.prStatus] }"
      title="Click: show only this status. Right-click: hide it."
      @click="toggle(card.prStatus, 'statuses')"
      @contextmenu.prevent="toggle(card.prStatus, 'excludedStatuses')"
    >
      <span class="n">{{ card.visibleCount }}<span v-if="card.hiddenCount" class="hidden-count">({{ card.hiddenCount }} hidden)</span></span>
      <span class="t">{{ card.label }}</span>
    </button>
  </div>
</template>
