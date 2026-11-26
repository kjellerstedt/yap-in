<script setup lang="ts" generic="T extends string">
import { computed } from "vue";

const props = defineProps<{ optionNames: Record<T, string>; label: string }>();
const selected = defineModel<T>({ required: true });
const options = computed(() => Object.keys(props.optionNames) as T[]);
</script>

<template>
  <div class="segmented" role="radiogroup" :aria-label="label">
    <button
      v-for="option in options"
      :key="option"
      type="button"
      role="radio"
      :aria-checked="option === selected"
      @click="selected = option"
    ><slot :option="option" />{{ optionNames[option] }}</button>
  </div>
</template>
