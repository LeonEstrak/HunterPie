// Mock HunterPie API server for WebUI development without a running game.
// Speaks the exact WebSocket protocol on :7273 and cycles a scripted hunt.
//
//   node mock/mock-server.mjs
//
// Requires: npm install (ws is a devDependency).
import { WebSocketServer } from 'ws'

const PORT = 7273

const state = {
  game: { timeElapsed: 0, worldTime: '10:00', isHudOpen: true },
  player: {
    name: 'Mock Hunter', highRank: 31, masterRank: 1, stageId: 5, inHuntingZone: true,
    position: { x: 0, y: 0, z: 0 },
    health: { current: 100, max: 100, heal: 100, recoverable: 0, maxPossible: 150 },
    stamina: { current: 150, max: 150, maxPossible: 150 },
    status: { raw: 210, elemental: 45, affinity: 0.25 },
    weapon: {
      id: 'LongSword',
      sharpness: { level: 'White', current: 30, max: 90, threshold: 10, thresholds: [40, 30, 20, 10] },
      longSword: { spiritLevel: 1, spiritBuildUp: 0.4, maxSpiritBuildUp: 1, spiritRegenerationTimer: 0, maxSpiritRegenerationTimer: 5, spiritLevelTimer: 30, maxSpiritLevelTimer: 45 },
      chargeBlade: null, dualBlades: null, insectGlaive: null, switchAxe: null
    },
    tools: [{ id: 'Mantle', cooldown: 30, maxCooldown: 300, timer: 90, maxTimer: 90 }],
    wirebugs: null,
    abnormalities: [
      { id: '1', name: 'Attack Up', icon: '', type: 'Buff', timer: 60, maxTimer: 60, isInfinite: false, level: 1, isBuildUp: false }
    ]
  },
  party: {
    size: 2, maxSize: 4,
    members: [
      { name: 'Mock Hunter', masterRank: 1, damage: 1200, weapon: 'LongSword', slot: 0, isMyself: true, type: 'Player', status: null },
      { name: 'Buddy', masterRank: 40, damage: 980, weapon: 'Hammer', slot: 1, isMyself: false, type: 'Player', status: null }
    ]
  },
  monsters: [
    {
      index: 0, id: 24, name: 'Rathalos', variant: 'Normal', crown: 'Silver',
      health: 16000, maxHealth: 16000, stamina: 400, maxStamina: 500,
      isEnraged: false, captureThreshold: 0.3, target: 'None', manualTarget: 'None',
      position: { x: 0, y: 0, z: 0 }, weaknesses: ['Dragon', 'Thunder'], types: ['FlyingWyvern'],
      parts: [
        { id: '0', name: 'Head', type: 'Breakable', health: 500, maxHealth: 500, flinch: 0, maxFlinch: 200, sever: 0, maxSever: 0, tenderize: 0, maxTenderize: 45, breakCount: 0 },
        { id: '1', name: 'Tail', type: 'Severable', health: 300, maxHealth: 300, flinch: 0, maxFlinch: 150, sever: 0, maxSever: 400, tenderize: 0, maxTenderize: 30, breakCount: 0 }
      ],
      ailments: [
        { id: '1', name: 'Poison', counter: 0, timer: 0, maxTimer: 30, buildUp: 50, maxBuildUp: 200 }
      ]
    }
  ],
  quest: { id: 331, name: 'The Scorching Blade', type: 'Hunt', status: 'InProgress', deaths: 0, maxDeaths: 3, level: 'LowRank', stars: 3, timeLeftSeconds: 3000 },
  chat: { isOpen: false, messages: [] }
}

const dirty = new Set()
const eventQueue = []

function mark(section) { dirty.add(section) }

function tick() {
  // Simulate a hunt: monster HP drops, player takes damage, quest timer runs
  const m = state.monsters[0]
  if (m) {
    m.health = Math.max(0, m.health - 120 - Math.random() * 200)
    m.stamina = Math.max(0, m.stamina - 5)
    m.parts[0].health = Math.max(0, m.parts[0].health - 25)
    if (m.health > 0 && m.health < m.maxHealth * 0.5 && !m.isEnraged) {
      m.isEnraged = true
      eventQueue.push({ type: 'event', event: 'monster.enrage', data: { index: 0, id: m.id, name: m.name } })
    }
    state.party.members[0].damage += 120
    state.party.members[1].damage += 95
    mark('monsters')
    mark('party')
  }

  state.player.stamina.current = Math.max(50, state.player.stamina.current - 2)
  state.quest.timeLeftSeconds = Math.max(0, state.quest.timeLeftSeconds - 0.4)
  state.game.timeElapsed += 0.4
  mark('player')
  mark('quest')
  mark('game')
}

const wss = new WebSocketServer({ port: PORT, path: '/ws' })

wss.on('connection', (ws) => {
  ws.send(JSON.stringify({ type: 'hello', data: { apiVersion: 1, game: 'MonsterHunterWorld' } }))
  ws.send(JSON.stringify({ type: 'snapshot', data: state }))

  ws.on('message', (raw) => {
    const text = raw.toString()
    if (text.includes('"ping"')) ws.send(JSON.stringify({ type: 'pong' }))
  })
})

setInterval(() => {
  tick()

  for (const client of wss.clients) {
    for (const evt of eventQueue.splice(0)) client.send(JSON.stringify(evt))

    if (dirty.size === 0) continue

    const data = {}
    for (const section of dirty) data[section] = state[section]
    client.send(JSON.stringify({ type: 'state', data }))
  }

  dirty.clear()
}, 400)

console.log(`Mock HunterPie WS server on ws://127.0.0.1:${PORT}/ws`)
