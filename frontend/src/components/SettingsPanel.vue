<script setup lang="ts">
import { computed } from "vue";
import { GROUP_FIELD_NAMES, type GroupField, MAX_GROUP_LEVELS, RELATION_NAMES, SORT_NAMES } from "../inbox/grouping";
import type { PullRequestRelation } from "../inbox/types";
import { useInboxContext } from "../composables/inboxContext";
import type { ThemeOption } from "../composables/useSettings";
import SegmentedControl from "./SegmentedControl.vue";

defineEmits<{ expandAll: []; collapseAll: [] }>();
const { settings, allPrs } = useInboxContext();

const THEME_NAMES: Record<ThemeOption, string> = { system: "System", light: "Light", dark: "Dark" };

const groupFieldPicks = computed(() => (Object.keys(GROUP_FIELD_NAMES) as GroupField[]).map((field) => {
  const level = settings.groups.indexOf(field);
  return { field, level, isFull: level === -1 && settings.groups.length >= MAX_GROUP_LEVELS };
}));

// Each level groups within the one above it, so removing a level drops the levels below it too.
function toggleGroupField(field: GroupField) {
  const level = settings.groups.indexOf(field);
  if (level !== -1) settings.groups = settings.groups.slice(0, level);
  else if (settings.groups.length < MAX_GROUP_LEVELS) settings.groups = [...settings.groups, field];
}

const relationChips = computed(() => {
  const countByRelation: Partial<Record<PullRequestRelation, number>> = {};
  for (const pr of allPrs()) for (const relation of pr.relations) countByRelation[relation] = (countByRelation[relation] ?? 0) + 1;
  return (Object.keys(RELATION_NAMES) as PullRequestRelation[])
    .filter((relation) => countByRelation[relation])
    .map((relation) => ({ relation, name: RELATION_NAMES[relation], count: countByRelation[relation] ?? 0 }));
});

function toggleRelation(relation: PullRequestRelation) {
  settings.relations = settings.relations.includes(relation)
    ? settings.relations.filter((other) => other !== relation)
    : [...settings.relations, relation];
}
</script>

<template>
  <div id="settings-panel" class="settings-panel" role="group" aria-label="Settings">
    <div class="settings-column">
      <section class="settings-section">
        <h2>Group by <small>up to {{ MAX_GROUP_LEVELS }}, in click order</small></h2>
        <div class="group-picks">
          <button
            v-for="pick in groupFieldPicks"
            :key="pick.field"
            type="button"
            class="group-pick"
            :class="{ on: pick.level !== -1 }"
            :aria-pressed="pick.level !== -1"
            :disabled="pick.isFull"
            @click="toggleGroupField(pick.field)"
          ><span class="group-level">{{ pick.level === -1 ? "+" : pick.level + 1 }}</span>{{ GROUP_FIELD_NAMES[pick.field] }}</button>
        </div>
      </section>
    </div>
    <div class="settings-column">
      <section class="settings-section">
        <h2>Sort</h2>
        <div class="sort-options" role="radiogroup" aria-label="Sort">
          <button
            v-for="(name, sort) in SORT_NAMES"
            :key="sort"
            type="button"
            class="sort-option"
            role="radio"
            :aria-checked="settings.sort === sort"
            @click="settings.sort = sort"
          >
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m5 12.5 4.5 4.5L19 7" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" /></svg>{{ name }}
          </button>
        </div>
      </section>
      <section class="settings-section">
        <h2>Relation <small>show only pull requests where you are any of these</small></h2>
        <div class="relation-chips">
          <button
            v-for="chip in relationChips"
            :key="chip.relation"
            type="button"
            class="relation-chip"
            :class="{ on: settings.relations.includes(chip.relation) }"
            :aria-pressed="settings.relations.includes(chip.relation)"
            @click="toggleRelation(chip.relation)"
          >{{ chip.name }}<span class="n">{{ chip.count }}</span></button>
          <span v-if="relationChips.length === 0" class="settings-note">No pull requests loaded</span>
        </div>
      </section>
    </div>
    <div class="settings-footer">
      <span class="settings-label">Theme</span>
      <SegmentedControl v-model="settings.theme" :option-names="THEME_NAMES" label="Theme">
        <template #default="{ option }">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <template v-if="option === 'system'"><rect x="3" y="4" width="18" height="12" rx="2" /><path d="M8 20h8M12 16v4" /></template>
            <template v-else-if="option === 'light'"><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" /></template>
            <path v-else d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
          </svg>
        </template>
      </SegmentedControl>
      <span class="spacer"></span>
      <button type="button" class="text-button" @click="$emit('expandAll')">Expand all <kbd>e</kbd></button>
      <button type="button" class="text-button" @click="$emit('collapseAll')">Collapse all <kbd>c</kbd></button>
    </div>
  </div>
</template>
