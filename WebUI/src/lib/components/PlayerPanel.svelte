<script lang="ts">
  import { player } from '../stores/session'
  import SharpnessBar from './SharpnessBar.svelte'
  import WeaponGauge from './WeaponGauge.svelte'

  function fraction(current: number | undefined, max: number | undefined): number {
    if (current === undefined || !max) return 0
    return Math.min(1, current / max)
  }
</script>

<section class="player-area panel">
  {#if $player}
    {@const p = $player}
    <header class="player-header">
      <strong class="player-name">{p.name}</strong>
      <span class="ranks num">HR {p.highRank} · MR {p.masterRank}</span>
    </header>

    <div class="vital">
      <div class="bar">
        {#if p.health && p.health.recoverable > 0}
          <div
            class="bar-fill recoverable"
            style:transform="scaleX({fraction(p.health.current + p.health.recoverable, p.health.max)})"
          ></div>
        {/if}
        <div class="bar-fill" style:transform="scaleX({fraction(p.health?.current, p.health?.max)})"></div>
        <span class="bar-label num">{p.health?.current.toFixed(0)} / {p.health?.max.toFixed(0)}</span>
      </div>
    </div>

    <div class="vital">
      <div class="bar stamina">
        <div class="bar-fill stamina-fill" style:transform="scaleX({fraction(p.stamina?.current, p.stamina?.max)})"></div>
        <span class="bar-label num">{p.stamina?.current.toFixed(0)} / {p.stamina?.max.toFixed(0)}</span>
      </div>
    </div>

    {#if p.status}
      <div class="stats num">
        <span title="Raw damage">⚔ {p.status.raw.toFixed(0)}</span>
        <span title="Elemental damage">✦ {p.status.elemental.toFixed(0)}</span>
        <span title="Affinity">◎ {(p.status.affinity * 100).toFixed(0)}%</span>
      </div>
    {/if}

    {#if p.weapon}
      <div class="weapon-block">
        <div class="weapon-name">{p.weapon.id.replace(/([a-z])([A-Z])/g, '$1 $2')}</div>
        {#if p.weapon.sharpness}
          <SharpnessBar sharpness={p.weapon.sharpness} />
        {/if}
        <WeaponGauge weapon={p.weapon} />
      </div>
    {/if}

    {#if p.tools && p.tools.length > 0}
      <div class="chips">
        {#each p.tools as tool, i (i)}
          <div class="chip" class:on-cooldown={tool.cooldown > 0} title="{tool.id} — cooldown {tool.cooldown.toFixed(0)}/{tool.maxCooldown.toFixed(0)}s">
            <span class="chip-label">{tool.id.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>
            <div class="chip-track">
              <div
                class="chip-fill"
                style:transform="scaleX({tool.maxCooldown > 0 ? 1 - tool.cooldown / tool.maxCooldown : 1})"
              ></div>
            </div>
          </div>
        {/each}
      </div>
    {/if}

    {#if p.wirebugs && p.wirebugs.length > 0}
      <div class="chips">
        {#each p.wirebugs as bug, i (i)}
          <div class="chip" class:on-cooldown={!bug.isAvailable} title="Wirebug {i + 1}{bug.isTemporary ? ' (temporary)' : ''}">
            <span class="chip-label">🐛{bug.isTemporary ? '+' : ''}</span>
            <div class="chip-track">
              <div
                class="chip-fill"
                style:transform="scaleX({bug.isAvailable ? 1 : bug.maxCooldown > 0 ? 1 - bug.cooldown / bug.maxCooldown : 0})"
              ></div>
            </div>
          </div>
        {/each}
      </div>
    {/if}

    {#if p.abnormalities.length > 0}
      <div class="chips">
        {#each p.abnormalities.slice(0, 8) as abn (abn.id)}
          <div class="chip abnormality" title="{abn.name}{abn.level > 0 ? ` Lv${abn.level}` : ''} — {abn.isInfinite ? '∞' : `${abn.timer.toFixed(0)}/${abn.maxTimer.toFixed(0)}s`}">
            <span class="chip-label">{abn.name}{abn.level > 0 ? ` ${abn.level}` : ''}</span>
            {#if !abn.isInfinite && abn.maxTimer > 0}
              <div class="chip-track">
                <div class="chip-fill" style:transform="scaleX({abn.timer / abn.maxTimer})"></div>
              </div>
            {/if}
          </div>
        {/each}
      </div>
    {/if}
  {:else}
    <span class="panel-title">Player</span>
    <span class="empty">Waiting for game…</span>
  {/if}
</section>

<style>
  .player-header {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 0.5rem;
  }

  .player-name {
    font-size: 1.05rem;
  }

  .ranks {
    color: var(--text-dim);
    font-size: 0.75rem;
    white-space: nowrap;
  }

  .stamina-fill {
    background: var(--stamina);
  }

  .stats {
    display: flex;
    gap: 0.8rem;
    font-size: 0.8rem;
    color: var(--text-dim);
  }

  .weapon-block {
    display: flex;
    flex-direction: column;
    gap: 0.3rem;
    padding-top: 0.3rem;
    border-top: 1px solid var(--border);
  }

  .weapon-name {
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: var(--accent);
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: 0.25rem;
  }

  .chip {
    display: flex;
    flex-direction: column;
    gap: 1px;
    min-width: 3.2rem;
    padding: 0.15rem 0.35rem;
    background: var(--bg-panel-alt);
    border: 1px solid var(--border);
    border-radius: 0.25rem;
  }

  .chip.on-cooldown {
    opacity: 0.6;
  }

  .chip-label {
    font-size: 0.62rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .chip-track {
    height: 0.16rem;
    background: #0a0d12;
    border-radius: 2px;
    overflow: hidden;
  }

  .chip-fill {
    height: 100%;
    background: var(--buff);
    transform-origin: left;
    transition: transform 180ms linear;
  }

  .empty {
    color: var(--text-dim);
  }
</style>
