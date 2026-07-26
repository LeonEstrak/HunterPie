import { writable } from 'svelte/store'
import type {
  ApiEvent,
  Chat,
  GameSection,
  GameState,
  Monster,
  Party,
  Player,
  Quest
} from '../api/types'

export type ConnectionState = 'connecting' | 'live' | 'degraded' | 'offline' | 'no-session'

const MAX_EVENTS = 30

export const connection = writable<ConnectionState>('connecting')
export const apiVersion = writable<number | null>(null)
export const gameName = writable<string | null>(null)

export const game = writable<GameSection | null>(null)
export const player = writable<Player | null>(null)
export const party = writable<Party | null>(null)
export const monsters = writable<Monster[]>([])
export const quest = writable<Quest | null>(null)
export const chat = writable<Chat | null>(null)

export const events = writable<ApiEvent[]>([])

export function applySnapshot(data: GameState | null): void {
  if (data === null) {
    clearSession()
    return
  }

  game.set(data.game)
  player.set(data.player)
  party.set(data.party)
  monsters.set(data.monsters ?? [])
  quest.set(data.quest)
  chat.set(data.chat)
}

export function applyStateUpdate(data: Partial<GameState>): void {
  // Sections are replaced wholesale; explicit nulls clear client state
  if ('game' in data) game.set(data.game ?? null)
  if ('player' in data) player.set(data.player ?? null)
  if ('party' in data) party.set(data.party ?? null)
  if ('monsters' in data) monsters.set(data.monsters ?? [])
  if ('quest' in data) quest.set(data.quest ?? null)
  if ('chat' in data) chat.set(data.chat ?? null)
}

export function pushEvent(event: string, data: unknown): void {
  events.update((list) => {
    const next = [...list, { time: Date.now(), event, data } satisfies ApiEvent]
    return next.length > MAX_EVENTS ? next.slice(next.length - MAX_EVENTS) : next
  })
}

export function clearSession(): void {
  game.set(null)
  player.set(null)
  party.set(null)
  monsters.set([])
  quest.set(null)
  chat.set(null)
}
