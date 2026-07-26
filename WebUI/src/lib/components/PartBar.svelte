<script lang="ts">
  import type { MonsterPart } from '../api/types'

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

<div class="part" class:broken title="{part.name} — HP {part.health.toFixed(0)}/{part.maxHealth.toFixed(0)} · Flinch {part.flinch.toFixed(0)}/{part.maxFlinch.toFixed(0)}{broken ? ` · broken ×${part.breakCount}` : ''}">
  <span class="part-name">{part.name}{#if broken}<sup>×{part.breakCount}</sup>{/if}</span>
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
  .part {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    min-width: 0;
  }

  .part.broken .part-name {
    color: var(--text-dim);
    text-decoration: line-through;
  }

  .part-name {
    font-size: 0.68rem;
    width: 5.2em;
    flex-shrink: 0;
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
    flex: 1;
    min-width: 0;
  }

  .micro-bar {
    height: 0.4rem;
    background: #0a0d12;
    border-radius: 2px;
    overflow: hidden;
  }

  .micro-bar.thin {
    height: 0.2rem;
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
