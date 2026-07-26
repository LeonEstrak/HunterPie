<script lang="ts">
  import { party } from '../stores/session'

  let sorted = $derived($party ? [...$party.members].sort((a, b) => a.slot - b.slot) : [])
  let maxDamage = $derived(Math.max(1, ...sorted.map((m) => m.damage)))
</script>

<section class="party-area panel">
  <span class="panel-title">Party {$party ? `${$party.size}/${$party.maxSize}` : ''}</span>
  {#if $party && sorted.length > 0}
    <div class="members">
      {#each sorted as member (member.slot)}
        <div class="member" class:myself={member.isMyself}>
          <div class="member-info">
            <span class="member-name">
              {member.name}
              {#if member.isMyself}<span class="you">you</span>{/if}
            </span>
            <span class="member-sub">{member.weapon.replace(/([a-z])([A-Z])/g, '$1 $2')} · MR {member.masterRank}</span>
          </div>
          <div class="member-damage">
            <span class="damage-num num">{member.damage.toLocaleString()}</span>
            <div class="damage-track">
              <div class="damage-fill" style:transform="scaleX({member.damage / maxDamage})"></div>
            </div>
          </div>
        </div>
      {/each}
    </div>
  {:else}
    <span class="empty">—</span>
  {/if}
</section>

<style>
  .members {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
    overflow: hidden;
  }

  .member {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.3rem 0.4rem;
    border-radius: 0.3rem;
    background: var(--bg-panel-alt);
  }

  .member.myself {
    border: 1px solid var(--accent);
  }

  .member-info {
    display: flex;
    flex-direction: column;
    min-width: 0;
    flex: 1;
  }

  .member-name {
    font-size: 0.85rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .you {
    font-size: 0.6rem;
    color: var(--accent);
    text-transform: uppercase;
    margin-left: 0.25rem;
  }

  .member-sub {
    font-size: 0.65rem;
    color: var(--text-dim);
  }

  .member-damage {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 2px;
    min-width: 5rem;
  }

  .damage-num {
    font-size: 0.9rem;
    color: var(--accent);
  }

  .damage-track {
    width: 100%;
    height: 0.2rem;
    background: #0a0d12;
    border-radius: 2px;
    overflow: hidden;
  }

  .damage-fill {
    height: 100%;
    background: var(--accent);
    transform-origin: left;
    transition: transform 250ms linear;
  }

  .empty {
    color: var(--text-dim);
  }
</style>
