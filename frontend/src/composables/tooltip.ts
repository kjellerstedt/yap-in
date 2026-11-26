import type { Directive } from "vue";

// Native title tooltips are slow to appear and did not show at all on the row's summaries, so those get their own.
// Hover only: one tooltip element serves the whole page. Screen readers get the same text as the host's aria-label,
// so give the host a role that takes a label (e.g. role="img"). Lines break where the text has "\n", and leading
// spaces are kept.
const HOVER_DELAY_MS = 120;
const VIEWPORT_MARGIN_PX = 8;
const HOST_GAP_PX = 6;

const textByHost = new WeakMap<HTMLElement, string>();
let tooltipElement: HTMLElement | null = null;
let shownHost: HTMLElement | null = null;
let hoverTimer: ReturnType<typeof setTimeout> | undefined;

function sharedTooltipElement(): HTMLElement {
  if (tooltipElement) return tooltipElement;
  tooltipElement = document.createElement("div");
  tooltipElement.className = "tooltip";
  tooltipElement.setAttribute("aria-hidden", "true");
  tooltipElement.hidden = true;
  document.body.append(tooltipElement);
  // The tooltip is placed once when shown, so it would drift from its host on scroll.
  window.addEventListener("scroll", () => { if (shownHost) hide(shownHost); }, { capture: true, passive: true });
  return tooltipElement;
}

function show(host: HTMLElement): void {
  const text = textByHost.get(host);
  if (!text) return;
  const tooltip = sharedTooltipElement();
  tooltip.textContent = text;
  tooltip.hidden = false;
  shownHost = host;
  placeBelowOrAbove(host, tooltip);
}

function hide(host: HTMLElement): void {
  clearTimeout(hoverTimer);
  if (shownHost !== host || tooltipElement === null) return;
  tooltipElement.hidden = true;
  shownHost = null;
}

// Centred under the host, above it when there is no room below, and kept inside the viewport.
function placeBelowOrAbove(host: HTMLElement, tooltip: HTMLElement): void {
  const hostRect = host.getBoundingClientRect();
  const tooltipRect = tooltip.getBoundingClientRect();
  const maxLeft = window.innerWidth - tooltipRect.width - VIEWPORT_MARGIN_PX;
  const centredLeft = hostRect.left + hostRect.width / 2 - tooltipRect.width / 2;
  const belowTop = hostRect.bottom + HOST_GAP_PX;
  const fitsBelow = belowTop + tooltipRect.height <= window.innerHeight - VIEWPORT_MARGIN_PX;
  tooltip.style.left = `${Math.max(VIEWPORT_MARGIN_PX, Math.min(centredLeft, maxLeft))}px`;
  tooltip.style.top = `${fitsBelow ? belowTop : hostRect.top - HOST_GAP_PX - tooltipRect.height}px`;
}

function setText(host: HTMLElement, text: string): void {
  textByHost.set(host, text);
  host.setAttribute("aria-label", text);
}

/** v-tooltip="text": shows the text on hover, and sets it as the host's aria-label. */
export const vTooltip: Directive<HTMLElement, string> = {
  mounted(host, binding) {
    setText(host, binding.value);
    host.addEventListener("mouseenter", () => {
      clearTimeout(hoverTimer);
      hoverTimer = setTimeout(() => show(host), HOVER_DELAY_MS);
    });
    host.addEventListener("mouseleave", () => hide(host));
  },
  updated(host, binding) {
    setText(host, binding.value);
    if (shownHost === host && tooltipElement) tooltipElement.textContent = binding.value;
  },
  beforeUnmount(host) {
    hide(host);
  },
};
