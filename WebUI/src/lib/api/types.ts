// Type definitions mirroring the HunterPie API protocol
// (HunterPie/Features/Api/README.md). Enums arrive as strings.

export interface Vec3 { x: number; y: number; z: number }

export interface GameSection {
  timeElapsed: number
  worldTime: string
  isHudOpen: boolean
}

export interface Health {
  current: number; max: number; heal: number; recoverable: number; maxPossible: number
}

export interface Stamina { current: number; max: number; maxPossible: number }

export interface PlayerStatus { raw: number; elemental: number; affinity: number }

export interface Sharpness {
  level: string; current: number; max: number; threshold: number; thresholds: number[] | null
}

export interface LongSword {
  spiritLevel: number; spiritBuildUp: number; maxSpiritBuildUp: number
  spiritRegenerationTimer: number; maxSpiritRegenerationTimer: number
  spiritLevelTimer: number; maxSpiritLevelTimer: number
}

export interface ChargeBlade {
  shieldBuff: number; swordBuff: number; axeBuff: number
  chargeBuildUp: number; maxChargeBuildUp: number
  charge: string; phials: number; maxPhials: number
}

export interface DualBlades {
  isDemonMode: boolean; isArchDemonMode: boolean
  demonBuildUp: number; maxDemonBuildUp: number
  piercingBindTimer: number; maxPiercingBindTimer: number
}

export interface InsectGlaive {
  primaryExtract: string; secondaryExtract: string; chargeType: string
  attackTimer: number; speedTimer: number; defenseTimer: number
  kinsectStamina: number; kinsectMaxStamina: number; kinsectCharge: number
}

export interface SwitchAxe {
  buildUp: number; maxBuildUp: number; lowBuildUp: number
  chargeTimer: number; maxChargeTimer: number
  chargeBuildUp: number; maxChargeBuildUp: number
  slamBuffTimer: number; maxSlamBuffTimer: number
}

export interface Weapon {
  id: string
  sharpness: Sharpness | null
  longSword: LongSword | null
  chargeBlade: ChargeBlade | null
  dualBlades: DualBlades | null
  insectGlaive: InsectGlaive | null
  switchAxe: SwitchAxe | null
}

export interface Abnormality {
  id: string; name: string; icon: string; type: string
  timer: number; maxTimer: number; isInfinite: boolean; level: number; isBuildUp: boolean
}

export interface SpecializedTool {
  id: string; cooldown: number; maxCooldown: number; timer: number; maxTimer: number
}

export interface Wirebug {
  isAvailable: boolean; isTemporary: boolean
  timer: number; maxTimer: number; cooldown: number; maxCooldown: number
}

export interface Player {
  name: string; highRank: number; masterRank: number
  stageId: number; inHuntingZone: boolean
  position: Vec3 | null
  health: Health | null
  stamina: Stamina | null
  status: PlayerStatus | null
  weapon: Weapon | null
  tools: SpecializedTool[] | null
  wirebugs: Wirebug[] | null
  abnormalities: Abnormality[]
}

export interface MonsterPart {
  id: string; name: string; type: string
  health: number; maxHealth: number
  flinch: number; maxFlinch: number
  sever: number; maxSever: number
  tenderize: number; maxTenderize: number
  breakCount: number
}

export interface MonsterAilment {
  id: string; name: string; counter: number
  timer: number; maxTimer: number
  buildUp: number; maxBuildUp: number
}

export interface Monster {
  index: number; id: number; name: string
  variant: string; crown: string
  health: number; maxHealth: number
  stamina: number; maxStamina: number
  isEnraged: boolean; captureThreshold: number
  target: string; manualTarget: string
  /** True when the player is inferred to be engaged with this monster */
  isEngaged: boolean
  position: Vec3 | null
  weaknesses: string[]; types: string[]
  parts: MonsterPart[]; ailments: MonsterAilment[]
}

export interface PartyMember {
  name: string; masterRank: number; damage: number
  weapon: string; slot: number; isMyself: boolean; type: string
  status: PlayerStatus | null
}

export interface Party { size: number; maxSize: number; members: PartyMember[] }

export interface Quest {
  id: number; name: string; type: string; status: string
  deaths: number; maxDeaths: number; level: string; stars: number
  timeLeftSeconds: number
}

export interface ChatMessage { message: string; author: string; type: string; playerSlot: number }

export interface Chat { isOpen: boolean; messages: ChatMessage[] }

export interface GameState {
  game: GameSection | null
  player: Player | null
  party: Party | null
  monsters: Monster[] | null
  quest: Quest | null
  chat: Chat | null
}

// WebSocket protocol messages
export type WsMessage =
  | { type: 'hello'; data: { apiVersion: number; game: string | null } }
  | { type: 'snapshot'; data: GameState | null }
  | { type: 'state'; data: Partial<GameState> }
  | { type: 'event'; event: string; data: unknown }
  | { type: 'pong' }

export interface ApiEvent {
  time: number
  event: string
  data: unknown
}

export interface StatusResponse {
  hunterPieVersion: string
  apiVersion: number
  uptimeSeconds: number
  webSocketClients: number
  game: { connected: boolean; type?: string; processName?: string; processId?: number }
}
