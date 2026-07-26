<script lang="ts">
  import { onDestroy, onMount } from 'svelte'
  import { apiVersion, connection, game, gameName, quest } from '../stores/session'

  const GAME_LABELS: Record<string, string> = {
    MonsterHunterWorld: 'MH: World',
    MonsterHunterRise: 'MH: Rise',
    MonsterHunterWilds: 'MH: Wilds'
  }

  let now = $state(Date.now())
  let timer: ReturnType<typeof setInterval>

  onMount(() => {
    timer = setInterval(() => (now = Date.now()), 1000)
  })

  onDestroy(() => clearInterval(timer))

  function formatTimeLeft(seconds: number): string {
    const total = Math.max(0, Math.floor(seconds))
    const m = Math.floor(total / 60)
    const s = total % 60
    return `${m}:${s.toString().padStart(2, '0')}`
  }

  const CONNECTION_LABELS: Record<string, string> = {
    connecting: 'connecting…',
    live: 'live',
    degraded: 'polling',
    offline: 'offline',
    'no-session': 'no game'
  }

  let questFetchedAt = $state(Date.now())

  let questTimeLeft = $derived(
    $quest ? formatTimeLeft($quest.timeLeftSeconds - (now - questFetchedAt) / 1000) : null
  )

  $effect(() => {
    if ($quest) questFetchedAt = Date.now()
  })
</script>

<header class="panel header">
  <div class="brand">
    <span class="logo">HunterPie</span>
    {#if $gameName}
      <span class="game-name">{GAME_LABELS[$gameName] ?? $gameName}</span>
    {/if}
  </div>

  <div class="quest-info">
    {#if $quest}
      <span class="quest-name">{$quest.name || `Quest #${$quest.id}`}</span>
      <span class="badge">{$quest.type}</span>
      <span class="num quest-timer">{questTimeLeft}</span>
      <span class="deaths" title="Faints">
        {#each Array($quest.maxDeaths) as _, i}
          <span class:fainted={i < $quest.deaths}>☠</span>
        {/each}
      </span>
    {:else}
      <span class="dim">No active quest</span>
    {/if}
  </div>

  <div class="status">
    {#if $game}
      <span class="dim num" title="World time">{$game.worldTime}</span>
    {/if}
    <span class="conn conn-{$connection}" title="API v{$apiVersion ?? '?'}">
      ● {CONNECTION_LABELS[$connection]}
    </span>
  </div>
</header>

<style>
  .header {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }

  .brand {
    display: flex;
    align-items: baseline;
    gap: 0.5rem;
  }

  .logo {
    font-weight: 700;
    color: var(--accent);
    letter-spacing: 0.03em;
  }

  .game-name {
    font-size: 0.8rem;
    color: var(--text-dim);
  }

  .quest-info {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    min-width: 0;
    flex: 1;
    justify-content: center;
  }

  .quest-name {
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .badge {
    font-size: 0.65rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    background: var(--bg-panel-alt);
    border: 1px solid var(--border);
    border-radius: 0.25rem;
    padding: 0.1rem 0.35rem;
    color: var(--text-dim);
  }

  .quest-timer {
    font-size: 1.1rem;
    font-weight: 600;
    color: var(--accent);
  }

  .deaths {
    letter-spacing: 0.1em;
    opacity: 0.35;
  }

  .deaths .fainted {
    opacity: 1;
    color: var(--enrage);
  }

  .status {
    display: flex;
    align-items: center;
    gap: 0.7rem;
  }

  .dim {
    color: var(--text-dim);
    font-size: 0.8rem;
  }

  .conn {
    font-size: 0.75rem;
    white-space: nowrap;
  }

  .conn-live { color: var(--hp); }
  .conn-degraded { color: var(--stamina); }
  .conn-offline, .conn-no-session { color: var(--enrage); }
  .conn-connecting { color: var(--text-dim); }

  /* Phone: shrink to a single compact strip */
  @media (max-width: 700px) {
    .header {
      gap: 0.4rem;
      padding: 0.4rem 0.5rem;
    }

    .game-name,
    .status .dim,
    .badge {
      display: none;
    }

    .logo {
      font-size: 0.8rem;
    }

    .quest-name {
      font-size: 0.75rem;
      max-width: 10em;
    }

    .quest-timer {
      font-size: 0.85rem;
    }

    .conn {
      font-size: 0.65rem;
    }
  }
</style>
