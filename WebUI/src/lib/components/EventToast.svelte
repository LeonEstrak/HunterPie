<script lang="ts">
  import { events } from '../stores/session'

  const LABELS: Record<string, (data: any) => string> = {
    'session.start': (d) => `Connected to ${d.game ?? 'game'}`,
    'session.end': () => 'Game closed',
    'quest.start': (d) => `Quest started: ${d.name || `#${d.id}`}`,
    'quest.end': (d) => `Quest ${d.status?.toLowerCase() ?? 'ended'}`,
    'monster.spawn': (d) => `${d.name} appeared`,
    'monster.death': (d) => `${d.name} slain`,
    'monster.capture': (d) => `${d.name} captured`,
    'monster.enrage': (d) => `${d.name} is enraged!`,
    'monster.engaged': (d) => `Engaging ${d.name}`,
    'player.death': () => 'You fainted',
    'player.login': (d) => `Logged in as ${d.name}`,
    'player.logout': () => 'Logged out',
    'party.member.join': (d) => `${d.name} joined the party`,
    'party.member.leave': (d) => `${d.name} left the party`
  }

  const MAX_VISIBLE = 4
  const TOAST_TTL_MS = 6000

  let now = $state(Date.now())

  $effect(() => {
    const timer = setInterval(() => (now = Date.now()), 1000)
    return () => clearInterval(timer)
  })

  let visible = $derived(
    $events
      .filter((e) => LABELS[e.event] && now - e.time < TOAST_TTL_MS)
      .slice(-MAX_VISIBLE)
  )
</script>

{#if visible.length > 0}
  <div class="toasts">
    {#each visible as evt (evt.time + evt.event)}
      <div class="toast">{LABELS[evt.event](evt.data)}</div>
    {/each}
  </div>
{/if}

<style>
  .toasts {
    position: fixed;
    top: calc(0.6rem + env(safe-area-inset-top));
    right: calc(0.6rem + env(safe-area-inset-right));
    display: flex;
    flex-direction: column;
    gap: 0.3rem;
    z-index: 20;
    pointer-events: none;
  }

  .toast {
    background: var(--bg-panel);
    border: 1px solid var(--accent);
    border-radius: 0.35rem;
    padding: 0.35rem 0.7rem;
    font-size: 0.8rem;
    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.5);
    animation: toast-in 200ms ease-out;
  }

  @keyframes toast-in {
    from {
      opacity: 0;
      transform: translateX(1rem);
    }
    to {
      opacity: 1;
      transform: none;
    }
  }
</style>
