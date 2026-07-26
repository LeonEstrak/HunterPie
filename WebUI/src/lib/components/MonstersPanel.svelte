<script lang="ts">
  import { monsters } from '../stores/session'
  import MonsterCard from './MonsterCard.svelte'

  const MAX_VISIBLE = 3

  // Targeted monster first, then by remaining HP fraction
  let sorted = $derived(
    [...$monsters].sort((a, b) => {
      const aTargeted = a.target !== 'None' || a.manualTarget !== 'None' ? 1 : 0
      const bTargeted = b.target !== 'None' || b.manualTarget !== 'None' ? 1 : 0

      if (aTargeted !== bTargeted) return bTargeted - aTargeted

      return a.health / (a.maxHealth || 1) - b.health / (b.maxHealth || 1)
    })
  )

  let visible = $derived(sorted.slice(0, MAX_VISIBLE))
  let hidden = $derived(sorted.length - visible.length)
</script>

<section class="monsters-area">
  {#if $monsters.length === 0}
    <div class="panel empty-panel">
      <span class="panel-title">Monsters</span>
      <span class="empty">No monsters tracked</span>
    </div>
  {:else}
    <div class="monster-list">
      {#each visible as monster (monster.index)}
        <MonsterCard {monster} />
      {/each}
      {#if hidden > 0}
        <div class="more-chip">+{hidden} more</div>
      {/if}
    </div>
  {/if}
</section>

<style>
  .monsters-area {
    min-height: 0;
  }

  .monster-list {
    display: flex;
    flex-direction: column;
    gap: 0.6rem;
    height: 100%;
    overflow: hidden;
  }

  .monster-list > :global(.monster) {
    flex: 1;
    min-height: 0;
  }

  .empty-panel {
    height: 100%;
    align-items: center;
    justify-content: center;
  }

  .empty {
    color: var(--text-dim);
  }

  .more-chip {
    align-self: center;
    font-size: 0.7rem;
    color: var(--text-dim);
    background: var(--bg-panel);
    border: 1px solid var(--border);
    border-radius: 1rem;
    padding: 0.15rem 0.7rem;
  }
</style>
