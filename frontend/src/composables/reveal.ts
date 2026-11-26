import type { Directive } from "vue";

// Rows fade up as they scroll (or unfold) into view, staggered within each batch. Rows that have already animated
// in show at once when the list re-renders (filters, polling), so the reveal never replays.
const revealedPrIds = new Set<string>();

const revealObserver = new IntersectionObserver((entries) => {
  let batchIndex = 0;
  for (const entry of entries) {
    if (entry.isIntersecting === false) continue;
    const row = entry.target as HTMLElement;
    row.style.setProperty("--delay", `${Math.min(batchIndex++ * 40, 400)}ms`);
    row.classList.add("in");
    if (row.dataset.id) revealedPrIds.add(row.dataset.id);
    revealObserver.unobserve(row);
  }
}, { rootMargin: "0px 0px -6% 0px" });

/** v-reveal on a PR row (with data-id). */
export const vReveal: Directive<HTMLElement> = {
  mounted(row) {
    row.classList.add("reveal");
    if (row.dataset.id && revealedPrIds.has(row.dataset.id)) row.classList.add("in");
    else revealObserver.observe(row);
  },
  // Vue rewrites the row's classes when it re-renders, so they are put back before the browser paints.
  updated(row) {
    row.classList.add("reveal");
    if (row.dataset.id && revealedPrIds.has(row.dataset.id)) row.classList.add("in");
  },
  beforeUnmount(row) {
    revealObserver.unobserve(row);
  },
};
