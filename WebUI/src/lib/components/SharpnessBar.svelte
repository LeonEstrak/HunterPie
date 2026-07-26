<script lang="ts">
  import type { Sharpness } from '../api/types'

  let { sharpness }: { sharpness: Sharpness } = $props()

  // Segment colors in level order (Red → Purple)
  const LEVEL_COLORS = ['#c93a3a', '#d08030', '#d8c13a', '#4fae4f', '#3a6fd8', '#e8e8e8', '#a05fd0']

  interface Segment {
    color: string
    width: number
  }

  let segments = $derived.by((): Segment[] => {
    const thresholds = (sharpness.thresholds ?? []).filter((t) => t > 0)
    const max = sharpness.max > 0 ? sharpness.max : thresholds[thresholds.length - 1] ?? 1

    if (thresholds.length === 0) return [{ color: LEVEL_COLORS[0], width: 1 }]

    const result: Segment[] = []
    let previous = 0

    for (const boundary of thresholds) {
      result.push({ color: LEVEL_COLORS[Math.min(result.length, LEVEL_COLORS.length - 1)], width: (boundary - previous) / max })
      previous = boundary
    }

    return result.filter((s) => s.width > 0)
  })

  let markerPosition = $derived(Math.min(1, sharpness.max > 0 ? sharpness.current / sharpness.max : 0))
</script>

<div class="sharpness" title="Sharpness: {sharpness.level} ({sharpness.current}/{sharpness.max})">
  <div class="segments">
    {#each segments as segment}
      <div class="segment" style:flex-grow={segment.width} style:background={segment.color}></div>
    {/each}
    <div class="marker" style:left="{(markerPosition * 100).toFixed(1)}%"></div>
  </div>
</div>

<style>
  .sharpness {
    padding: 2px 0;
  }

  .segments {
    position: relative;
    display: flex;
    height: 0.5rem;
    border-radius: 2px;
    overflow: hidden;
    background: #0a0d12;
  }

  .segment {
    height: 100%;
    opacity: 0.9;
  }

  .segment + .segment {
    border-left: 1px solid rgba(0, 0, 0, 0.5);
  }

  .marker {
    position: absolute;
    top: -1px;
    bottom: -1px;
    width: 2px;
    background: #fff;
    box-shadow: 0 0 3px #000;
    transition: left 180ms linear;
  }
</style>
