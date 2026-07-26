<script lang="ts">
  import { onMount } from 'svelte'
  import { start } from './lib/api/client'
  import { connection, monsters, player, party } from './lib/stores/session'
  import HeaderBar from './lib/components/HeaderBar.svelte'

  onMount(() => {
    start()
  })
</script>

<div class="dashboard">
  <div class="header-area">
    <HeaderBar />
  </div>

  <section class="player-area panel">
    <span class="panel-title">Player</span>
    {#if $player}
      <div>{$player.name} · HR {$player.highRank} · MR {$player.masterRank}</div>
      <div class="bar">
        <div class="bar-fill" style:transform="scaleX({($player.health?.current ?? 0) / ($player.health?.max || 1)})"></div>
        <span class="bar-label num">{$player.health?.current?.toFixed(0)} / {$player.health?.max?.toFixed(0)}</span>
      </div>
      <pre class="debug">{JSON.stringify($player.weapon, null, 1)}</pre>
    {:else}
      <span class="empty">Waiting for game…</span>
    {/if}
  </section>

  <section class="monsters-area panel">
    <span class="panel-title">Monsters ({$monsters.length})</span>
    {#each $monsters as monster (monster.index)}
      <div class="monster-row">
        <strong>{monster.name}</strong>
        <div class="bar">
          <div class="bar-fill" style:transform="scaleX({monster.health / (monster.maxHealth || 1)})"></div>
          <span class="bar-label num">{monster.health.toFixed(0)} / {monster.maxHealth.toFixed(0)}</span>
        </div>
      </div>
    {:else}
      <span class="empty">No monsters</span>
    {/each}
  </section>

  <section class="party-area panel">
    <span class="panel-title">Party</span>
    {#if $party}
      {#each $party.members as member (member.slot)}
        <div>{member.name} — {member.weapon} — <span class="num">{member.damage}</span></div>
      {:else}
        <span class="empty">Solo</span>
      {/each}
    {:else}
      <span class="empty">—</span>
    {/if}
  </section>
</div>

{#if $connection === 'offline'}
  <div class="offline-overlay">
    <p>Cannot reach the HunterPie API</p>
    <p class="hint">Is HunterPie running?</p>
  </div>
{/if}

<style>
  .debug {
    font-size: 0.65rem;
    color: var(--text-dim);
    overflow: hidden;
  }

  .empty {
    color: var(--text-dim);
    font-size: 0.85rem;
  }

  .monster-row {
    display: flex;
    flex-direction: column;
    gap: 0.3rem;
  }

  .offline-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    background: rgba(13, 17, 23, 0.92);
    font-size: 1.1rem;
    z-index: 10;
  }

  .hint {
    color: var(--text-dim);
    font-size: 0.85rem;
  }
</style>
