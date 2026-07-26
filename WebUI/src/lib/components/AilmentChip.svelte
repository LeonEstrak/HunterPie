<script lang="ts">
  import type { MonsterAilment } from '../api/types'

  let { ailment }: { ailment: MonsterAilment } = $props()

  let active = $derived(ailment.timer > 0)
  let buildupFraction = $derived(ailment.maxBuildUp > 0 ? ailment.buildUp / ailment.maxBuildUp : 0)
  let timerFraction = $derived(ailment.maxTimer > 0 ? ailment.timer / ailment.maxTimer : 0)
</script>

<div
  class="ailment"
  class:active
  title="{ailment.name} — build-up {ailment.buildUp.toFixed(0)}/{ailment.maxBuildUp.toFixed(0)} · timer {ailment.timer.toFixed(1)}/{ailment.maxTimer.toFixed(0)}s"
>
  <span class="ailment-name">{ailment.name}</span>
  <div class="ailment-track">
    <div
      class="ailment-fill"
      style:transform="scaleX({active ? timerFraction : buildupFraction})"
    ></div>
  </div>
</div>

<style>
  .ailment {
    display: flex;
    flex-direction: column;
    gap: 1px;
    min-width: 3.4rem;
    padding: 0.15rem 0.3rem;
    background: var(--bg-panel-alt);
    border: 1px solid var(--border);
    border-radius: 0.25rem;
  }

  .ailment.active {
    border-color: #7a4fb0;
  }

  .ailment-name {
    font-size: 0.6rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    color: var(--text-dim);
  }

  .ailment.active .ailment-name {
    color: #b98ae0;
  }

  .ailment-track {
    height: 0.18rem;
    background: #0a0d12;
    border-radius: 2px;
    overflow: hidden;
  }

  .ailment-fill {
    height: 100%;
    background: #b98ae0;
    transform-origin: left;
    transition: transform 180ms linear;
  }
</style>
