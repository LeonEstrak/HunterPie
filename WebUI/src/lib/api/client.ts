import { get } from 'svelte/store'
import {
  apiVersion,
  applySnapshot,
  applyStateUpdate,
  clearSession,
  connection,
  gameName,
  pushEvent
} from '../stores/session'
import type { GameState, WsMessage } from './types'

/**
 * API base host. Empty means same-origin (production: served by HunterPie
 * itself; dev: Vite proxy). Override with ?api=host:port for standalone
 * hosting; persisted in localStorage.
 */
function resolveApiBase(): string {
  const params = new URLSearchParams(location.search)
  const fromUrl = params.get('api')

  if (fromUrl) {
    localStorage.setItem('hunterpie.api', fromUrl)
    return fromUrl
  }

  return localStorage.getItem('hunterpie.api') ?? ''
}

const apiBase = resolveApiBase()

function httpUrl(path: string): string {
  return apiBase ? `http://${apiBase}${path}` : path
}

function wsUrl(): string {
  if (apiBase) return `ws://${apiBase}/ws`

  const protocol = location.protocol === 'https:' ? 'wss' : 'ws'
  return `${protocol}://${location.host}/ws`
}

const RECONNECT_DELAYS = [1000, 2000, 4000, 8000, 15000]
const POLL_INTERVAL = 2000
const WS_FAILURES_BEFORE_POLLING = 3

let ws: WebSocket | null = null
let reconnectAttempts = 0
let reconnectTimer: ReturnType<typeof setTimeout> | null = null
let pollTimer: ReturnType<typeof setInterval> | null = null
let started = false

export function start(): void {
  if (started) return
  started = true
  connectWebSocket()
}

function connectWebSocket(): void {
  connection.set('connecting')

  try {
    ws = new WebSocket(wsUrl())
  } catch {
    scheduleReconnect()
    return
  }

  ws.onopen = () => {
    reconnectAttempts = 0
    connection.set('live')
    stopPolling()
  }

  ws.onmessage = (msg) => {
    try {
      handleMessage(JSON.parse(msg.data) as WsMessage)
    } catch {
      // malformed message, ignore
    }
  }

  ws.onclose = () => {
    ws = null
    scheduleReconnect()
  }

  ws.onerror = () => {
    ws?.close()
  }
}

function handleMessage(message: WsMessage): void {
  switch (message.type) {
    case 'hello':
      apiVersion.set(message.data.apiVersion)
      gameName.set(message.data.game)
      if (message.data.game === null) connection.set('no-session')
      break

    case 'snapshot':
      applySnapshot(message.data)
      connection.set(message.data === null ? 'no-session' : 'live')
      break

    case 'state':
      applyStateUpdate(message.data)
      connection.set('live')
      break

    case 'event': {
      const name = message.event
      pushEvent(name, message.data)

      if (name === 'session.start') {
        gameName.set((message.data as { game?: string }).game ?? null)
        connection.set('live')
      } else if (name === 'session.end') {
        clearSession()
        connection.set('no-session')
      }
      break
    }

    case 'pong':
      break
  }
}

function scheduleReconnect(): void {
  if (reconnectTimer !== null) return

  reconnectAttempts += 1

  if (reconnectAttempts >= WS_FAILURES_BEFORE_POLLING) {
    connection.set('degraded')
    startPolling()
  }

  const delay = RECONNECT_DELAYS[Math.min(reconnectAttempts - 1, RECONNECT_DELAYS.length - 1)]
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null
    connectWebSocket()
  }, delay)
}

// --- REST fallback (degraded mode) ---

function startPolling(): void {
  if (pollTimer !== null) return

  pollTimer = setInterval(pollOnce, POLL_INTERVAL)
  void pollOnce()
}

function stopPolling(): void {
  if (pollTimer !== null) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

async function pollOnce(): Promise<void> {
  try {
    const statusResponse = await fetch(httpUrl('/api/v1/status'))

    if (!statusResponse.ok) return

    const status = await statusResponse.json()
    apiVersion.set(status.apiVersion)

    if (!status.game?.connected) {
      gameName.set(null)
      clearSession()
      connection.set('no-session')
      return
    }

    gameName.set(status.game.type ?? null)

    const sections: Partial<GameState> = {}

    const [gameRes, playerRes, partyRes, monstersRes, questRes] = await Promise.all([
      fetch(httpUrl('/api/v1/game')),
      fetch(httpUrl('/api/v1/player')),
      fetch(httpUrl('/api/v1/party')),
      fetch(httpUrl('/api/v1/monsters')),
      fetch(httpUrl('/api/v1/quest'))
    ])

    if (gameRes.ok) sections.game = await gameRes.json()
    if (playerRes.ok) sections.player = await playerRes.json()
    if (partyRes.ok) sections.party = await partyRes.json()
    if (monstersRes.ok) sections.monsters = await monstersRes.json()
    sections.quest = questRes.status === 200 ? await questRes.json() : null

    applyStateUpdate(sections)
    connection.set('degraded')
  } catch {
    connection.set('offline')
  }
}

export function currentConnectionLabel(): string {
  return get(connection)
}
