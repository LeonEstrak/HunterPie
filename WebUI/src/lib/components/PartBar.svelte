<script lang="ts">
  import type { MonsterPart } from '../api/types'
  import { prettifyName } from '../api/format'

  let { part }: { part: MonsterPart } = $props()

  const TYPE_COLORS: Record<string, string> = {
    Breakable: 'var(--buff)',
    Severable: 'var(--stamina)',
    Flinch: 'var(--hp)'
  }

  let healthFraction = $derived(part.maxHealth > 0 ? part.health / part.maxHealth : 0)
  let flinchFraction = $derived(part.maxFlinch > 0 ? part.flinch / part.maxFlinch : 0)
  let severFraction = $derived(part.maxSever > 0 ? part.sever / part.maxSever : 0)
  let broken = $derived(part.breakCount > 0)
</script>

<div
  class="part-chip"
  class:broken
  title="{prettifyName(part.name)} — HP {part.health.toFixed(0)}/{part.maxHealth.toFixed(0)} · Flinch {part.flinch.toFixed(0)}/{part.maxFlinch.toFixed(0)}{broken ? ` · broken ×${part.breakCount}` : ''}"
>
  <span class="part-name">{prettifyName(part.name)}{#if broken}<sup>×{part.breakCount}</sup>{/if}</span>
  <div class="micro-bars">
    <div class="micro-bar">
      <div
        class="micro-fill"
        style:transform="scaleX({healthFraction})"
        style:background={TYPE_COLORS[part.type] ?? 'var(--buff)'}
      ></div>
    </div>
    <div class="micro-bar thin">
      <div class="micro-fill flinch" style:transform="scaleX({part.maxSever > 0 ? severFraction : flinchFraction})"></div>
    </div>
  </div>
</div>

<style>
  .part-chip {
    display: flex;
    flex-direction: column;
    gap: 2px;
    flex: 1 1 5.5rem;
    min-width: 5.5rem;
    padding: 0.25rem 0.4rem;
    background: var(--bg-panel-alt);
    border: 1px solid var(--border);
    border-radius: 0.25rem;
  }

  .part-chip.broken .part-name {
    color: var(--text-dim);
    text-decoration: line-through;
  }

  .part-name {
    font-size: 0.68rem;
    line-height: 1.15;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .part-name sup {
    color: var(--accent);
  }

  .micro-bars {
    display: flex;
    flex-direction: column;
    gap: 1px;
  }

  .micro-bar {
    height: 0.32rem;
    background: #0a0d12;
    border-radius: 2px;
    overflow: hidden;
  }

  .micro-bar.thin {
    height: 0.16rem;
  }

  .micro-fill {
    height: 100%;
    transform-origin: left;
    transition: transform 180ms linear;
  }

  .micro-fill.flinch {
    background: var(--text-dim);
  }
</style>
