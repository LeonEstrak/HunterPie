<script lang="ts">
  import type { Monster } from '../api/types'
  import PartBar from './PartBar.svelte'
  import AilmentChip from './AilmentChip.svelte'

  let { monster }: { monster: Monster } = $props()

  const CROWNS: Record<string, string> = {
    Mini: '👑',
    Silver: '👑',
    Gold: '👑'
  }

  let healthFraction = $derived(monster.maxHealth > 0 ? monster.health / monster.maxHealth : 0)
  let staminaFraction = $derived(monster.maxStamina > 0 ? monster.stamina / monster.maxStamina : 0)
  let capturable = $derived(
    monster.captureThreshold > 0 && monster.health > 0 && healthFraction <= monster.captureThreshold
  )
  let dead = $derived(monster.health <= 0)

  // Only show discovered parts (real values) — most relevant first
  let visibleParts = $derived(
    monster.parts
      .filter((p) => p.maxHealth > 0 || p.maxFlinch > 0 || p.maxSever > 0)
      .sort((a, b) => a.health / (a.maxHealth || 1) - b.health / (b.maxHealth || 1))
      .slice(0, 6)
  )

  let visibleAilments = $derived(
    monster.ailments.filter((a) => a.maxBuildUp > 0 || a.maxTimer > 0).slice(0, 10)
  )
</script>

<article class="monster panel" class:enraged={monster.isEnraged} class:dead>
  <header class="monster-header">
    <div class="monster-title">
      {#if CROWNS[monster.crown]}
        <span class="crown crown-{monster.crown.toLowerCase()}" title="{monster.crown} crown">👑</span>
      {/if}
      <h2>{monster.name}</h2>
      {#if monster.variant !== 'Normal'}
        <span class="variant">{monster.variant}</span>
      {/if}
    </div>
    <div class="monster-flags">
      {#if monster.isEnraged}
        <span class="flag enrage-flag">ENRAGED</span>
      {/if}
      {#if capturable}
        <span class="flag capture-flag">CAPTURABLE</span>
      {/if}
      {#if dead}
        <span class="flag dead-flag">DEAD</span>
      {/if}
    </div>
  </header>

  <div class="bar hp-bar">
    <div class="bar-fill" style:transform="scaleX({healthFraction})"></div>
    {#if monster.captureThreshold > 0}
      <div class="capture-marker" style:left="{(monster.captureThreshold * 100).toFixed(1)}%" title="Capture threshold"></div>
    {/if}
    <span class="bar-label num">{monster.health.toFixed(0)} / {monster.maxHealth.toFixed(0)}</span>
  </div>

  <div class="bar stamina-bar">
    <div class="bar-fill stamina-fill" style:transform="scaleX({staminaFraction})"></div>
    <span class="bar-label num">stamina {monster.stamina.toFixed(0)}</span>
  </div>

  {#if visibleParts.length > 0}
    <div class="parts">
      {#each visibleParts as part, i (`${i}-${part.name}`)}
        <PartBar {part} />
      {/each}
    </div>
  {/if}

  {#if visibleAilments.length > 0}
    <div class="ailments">
      {#each visibleAilments as ailment, i (`${i}-${ailment.name}`)}
        <AilmentChip {ailment} />
      {/each}
    </div>
  {/if}
</article>

<style>
  .monster {
    gap: 0.45rem;
    transition: border-color 300ms;
  }

  .monster.enraged {
    border-color: var(--enrage);
    animation: enrage-pulse 1.2s ease-in-out infinite;
  }

  .monster.dead {
    opacity: 0.55;
  }

  @keyframes enrage-pulse {
    0%, 100% { box-shadow: 0 0 0 0 rgba(214, 69, 69, 0); }
    50% { box-shadow: 0 0 12px 1px rgba(214, 69, 69, 0.45); }
  }

  .monster-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
  }

  .monster-title {
    display: flex;
    align-items: baseline;
    gap: 0.4rem;
    min-width: 0;
  }

  .monster-title h2 {
    font-size: 1.05rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .crown-silver { filter: grayscale(0.6) brightness(1.3); }
  .crown-mini { font-size: 0.75rem; }

  .variant {
    font-size: 0.65rem;
    text-transform: uppercase;
    color: var(--text-dim);
    letter-spacing: 0.06em;
  }

  .monster-flags {
    display: flex;
    gap: 0.3rem;
    flex-shrink: 0;
  }

  .flag {
    font-size: 0.6rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    padding: 0.12rem 0.35rem;
    border-radius: 0.25rem;
  }

  .enrage-flag {
    background: rgba(214, 69, 69, 0.2);
    color: var(--enrage);
    border: 1px solid var(--enrage);
  }

  .capture-flag {
    background: rgba(94, 190, 110, 0.15);
    color: var(--hp);
    border: 1px solid var(--hp);
  }

  .dead-flag {
    background: var(--bg-panel-alt);
    color: var(--text-dim);
    border: 1px solid var(--border);
  }

  .hp-bar {
    height: 1.3rem;
  }

  .stamina-bar {
    height: 0.55rem;
  }

  .stamina-fill {
    background: var(--stamina);
  }

  .stamina-bar .bar-label {
    font-size: 0.55rem;
    justify-content: flex-end;
    padding-right: 0.3rem;
    color: rgba(0, 0, 0, 0.7);
    text-shadow: none;
  }

  .capture-marker {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 2px;
    background: var(--accent);
    opacity: 0.9;
    z-index: 1;
  }

  .parts {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;
  }

  .ailments {
    display: flex;
    flex-wrap: wrap;
    gap: 0.25rem;
  }
</style>
