<script lang="ts">
  import type { Weapon } from '../api/types'

  let { weapon }: { weapon: Weapon } = $props()

  const SPIRIT_COLORS = ['#4a4a4a', '#e8e8e8', '#f0b429', '#d64545']
</script>

{#if weapon.longSword}
  {@const ls = weapon.longSword}
  <div class="gauge" title="Long Sword spirit gauge">
    <div class="pips">
      {#each [1, 2, 3] as level}
        <span class="pip" style:background={ls.spiritLevel >= level ? SPIRIT_COLORS[level] : 'transparent'}></span>
      {/each}
    </div>
    <div class="mini-bar">
      <div class="mini-fill" style:transform="scaleX({ls.maxSpiritBuildUp > 0 ? ls.spiritBuildUp / ls.maxSpiritBuildUp : 0})" style:background="#d64545"></div>
    </div>
    {#if ls.spiritLevel > 0 && ls.maxSpiritLevelTimer > 0}
      <div class="mini-bar">
        <div class="mini-fill" style:transform="scaleX({ls.spiritLevelTimer / ls.maxSpiritLevelTimer})" style:background={SPIRIT_COLORS[ls.spiritLevel]}></div>
      </div>
    {/if}
  </div>
{:else if weapon.chargeBlade}
  {@const cb = weapon.chargeBlade}
  <div class="gauge" title="Charge Blade phials (charge: {cb.charge})">
    <div class="pips">
      {#each Array(cb.maxPhials) as _, i}
        <span
          class="pip phial"
          class:charged={i < cb.phials}
          class:overcharged={cb.charge === 'Yellow' || cb.charge === 'Red'}
        ></span>
      {/each}
    </div>
    <div class="buffs">
      {#if cb.shieldBuff > 0}<span class="buff">🛡 {cb.shieldBuff.toFixed(0)}s</span>{/if}
      {#if cb.swordBuff > 0}<span class="buff">🗡 {cb.swordBuff.toFixed(0)}s</span>{/if}
      {#if cb.axeBuff > 0}<span class="buff">🪓 {cb.axeBuff.toFixed(0)}s</span>{/if}
    </div>
  </div>
{:else if weapon.dualBlades}
  {@const db = weapon.dualBlades}
  <div class="gauge" title="Dual Blades demon gauge">
    <div class="demon-modes">
      {#if db.isDemonMode}<span class="buff demon">DEMON</span>{/if}
      {#if db.isArchDemonMode}<span class="buff archdemon">ARCHDEMON</span>{/if}
    </div>
    <div class="mini-bar">
      <div class="mini-fill" style:transform="scaleX({db.maxDemonBuildUp > 0 ? db.demonBuildUp / db.maxDemonBuildUp : 0})" style:background="#b98ae0"></div>
    </div>
  </div>
{:else if weapon.insectGlaive}
  {@const ig = weapon.insectGlaive}
  <div class="gauge" title="Insect Glaive extracts & kinsect">
    <div class="extracts">
      <span class="extract" class:active={ig.primaryExtract === 'Red' || ig.secondaryExtract === 'Red'} style:--c="#d64545">R</span>
      <span class="extract" class:active={ig.primaryExtract === 'White' || ig.secondaryExtract === 'White'} style:--c="#e8e8e8">W</span>
      <span class="extract" class:active={ig.primaryExtract === 'Orange' || ig.secondaryExtract === 'Orange'} style:--c="#d08030">O</span>
    </div>
    <div class="mini-bar">
      <div class="mini-fill" style:transform="scaleX({ig.kinsectMaxStamina > 0 ? ig.kinsectStamina / ig.kinsectMaxStamina : 0})" style:background="#5ebe6e"></div>
    </div>
  </div>
{:else if weapon.switchAxe}
  {@const sa = weapon.switchAxe}
  <div class="gauge" title="Switch Axe amp gauge">
    <div class="mini-bar tall">
      <div class="mini-fill" style:transform="scaleX({sa.maxBuildUp > 0 ? sa.buildUp / sa.maxBuildUp : 0})" style:background="#5ea9be"></div>
    </div>
    {#if sa.maxChargeTimer > 0 && sa.chargeTimer > 0}
      <div class="mini-bar">
        <div class="mini-fill" style:transform="scaleX({sa.chargeTimer / sa.maxChargeTimer})" style:background="#f0b429"></div>
      </div>
    {/if}
  </div>
{/if}

<style>
  .gauge {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .pips {
    display: flex;
    gap: 0.25rem;
  }

  .pip {
    width: 0.8rem;
    height: 0.8rem;
    border-radius: 50%;
    border: 1px solid var(--border);
  }

  .pip.phial.charged {
    background: #e8c93a;
  }

  .pip.phial.charged.overcharged {
    background: #d64545;
  }

  .mini-bar {
    height: 0.35rem;
    background: #0a0d12;
    border-radius: 2px;
    overflow: hidden;
  }

  .mini-bar.tall {
    height: 0.5rem;
  }

  .mini-fill {
    height: 100%;
    transform-origin: left;
    transition: transform 150ms linear;
  }

  .buffs,
  .demon-modes,
  .extracts {
    display: flex;
    gap: 0.35rem;
    font-size: 0.7rem;
  }

  .buff {
    color: var(--buff);
  }

  .buff.demon { color: #d64545; font-weight: 700; }
  .buff.archdemon { color: #b98ae0; font-weight: 700; }

  .extract {
    width: 1.1rem;
    height: 1.1rem;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    border: 1px solid var(--border);
    font-size: 0.6rem;
    font-weight: 700;
    color: var(--text-dim);
  }

  .extract.active {
    background: var(--c);
    color: #000;
    border-color: var(--c);
  }
</style>
