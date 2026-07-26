<script lang="ts">
  import { onMount } from 'svelte'
  import { start } from './lib/api/client'
  import { connection } from './lib/stores/session'
  import HeaderBar from './lib/components/HeaderBar.svelte'
  import MonstersPanel from './lib/components/MonstersPanel.svelte'
  import PlayerPanel from './lib/components/PlayerPanel.svelte'
  import PartyPanel from './lib/components/PartyPanel.svelte'
  import EventToast from './lib/components/EventToast.svelte'

  onMount(() => {
    start()
  })
</script>

<div class="dashboard">
  <div class="header-area">
    <HeaderBar />
  </div>

  <PlayerPanel />
  <MonstersPanel />
  <PartyPanel />
</div>

<EventToast />

{#if $connection === 'offline'}
  <div class="offline-overlay">
    <p>Cannot reach the HunterPie API</p>
    <p class="hint">Is HunterPie running?</p>
  </div>
{/if}

<style>
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
