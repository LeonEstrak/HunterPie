# HunterPie API

HunterPie exposes all live game metrics through a local HTTP/WebSocket API
so external tools (WebUIs, stream overlays, Discord bots, ...) can consume
them. The server is self-contained (no extra runtime requirements) and
starts together with HunterPie.

- **Default address:** `http://0.0.0.0:7273` (reachable from the network)
- **Protocol:** JSON over HTTP (REST snapshots) + WebSocket (live push)
- **CORS:** all origins allowed — browser-hosted WebUIs work out of the box
- **Auth:** optional bearer token (empty by default = open access)

Configuration lives in HunterPie's settings under **API** (or
`config.json` → `api`): enable, bind-all-interfaces, port, auth token and
broadcast interval. Changes other than the broadcast interval require a
restart. Binding to all interfaces triggers a one-time Windows Firewall
prompt.

---

## REST endpoints

All endpoints are `GET`-only and return JSON (camelCase, enums as strings).

| Endpoint | Description |
|---|---|
| `GET /` | Index of available endpoints |
| `GET /api/v1/status` | HunterPie version, API version, uptime, connected WS clients, game session info |
| `GET /api/v1/game` | Time elapsed, world time, HUD state |
| `GET /api/v1/player` | Full player state (see below) |
| `GET /api/v1/party` | Party size and members |
| `GET /api/v1/monsters` | All monsters with parts/ailments |
| `GET /api/v1/monsters/{index}` | Single monster by its runtime index |
| `GET /api/v1/quest` | Active quest (204 when none) |
| `GET /api/v1/chat` | Chat open state + last 50 messages |
| `GET /ws` | WebSocket upgrade |

**Error responses:** `{"error":"..."}` with status `404` (not found),
`401` (bad/missing token), `405` (non-GET), `503` (`no_active_session` —
HunterPie is running but no game is connected).

## WebSocket protocol (`/ws`)

Messages are JSON text frames.

**On connect**, the server sends:

```jsonc
{"type":"hello","data":{"apiVersion":1,"game":"MonsterHunterWilds"}}  // game: null when idle
{"type":"snapshot","data":{ /* all sections, or null when no game */ }}
```

**Live updates** are pushed every *broadcast interval* (default 200 ms),
containing only the sections that changed since the last push. Clients
should replace whole sections:

```jsonc
{"type":"state","data":{"player":{...},"monsters":[...]}}
```

Sections: `game`, `player`, `party`, `monsters`, `quest`, `chat`.
A section explicitly set to `null` (e.g. quest ended) must be cleared
client-side.

**Discrete events** are pushed immediately as they happen:

```jsonc
{"type":"event","event":"monster.spawn","data":{"index":0,"id":24,"name":"Rathalos"}}
```

| Event | `data` payload |
|---|---|
| `session.start` / `session.end` | `{game}` / `{}` |
| `quest.start` | `{id, name}` |
| `quest.end` | `{status, timeElapsedSeconds}` |
| `monster.spawn` / `monster.death` / `monster.capture` / `monster.enrage` | `{index, id, name}` |
| `player.login` | `{name}` |
| `player.logout` / `player.death` | `{}` |
| `party.member.join` / `party.member.leave` | `{name, slot}` |
| `chat.message` | full `ChatMessage` object |

**Client → server:** send `{"type":"ping"}` to receive `{"type":"pong"}`.
Protocol-level ping/pong is also handled.

## Data model (sections)

### `game`
```jsonc
{"timeElapsed": 123.4, "worldTime": "14:32", "isHudOpen": true}
```

### `player`
```jsonc
{
  "name": "Hunter", "highRank": 12, "masterRank": 34,
  "stageId": 5, "inHuntingZone": true,
  "position": {"x": 0.0, "y": 0.0, "z": 0.0},
  "health": {"current": 100, "max": 100, "heal": 0, "recoverable": 0, "maxPossible": 200},
  "stamina": {"current": 150, "max": 150, "maxPossible": 150},
  "status": {"raw": 210, "elemental": 45, "affinity": 0.25},
  "weapon": {
    "id": "LongSword",
    "sharpness": {"level": "White", "current": 30, "max": 90, "threshold": 10, "thresholds": [40, 30, 20]},
    "longSword": {"spiritLevel": 3, "spiritBuildUp": 0.4, "maxSpiritBuildUp": 1.0, ...},
    // one of: longSword, chargeBlade, dualBlades, insectGlaive, switchAxe
  },
  "tools": [{"id": "Mantle", "timer": 90, "maxTimer": 90, "cooldown": 0, "maxCooldown": 300}],  // MHW/MHWilds
  "wirebugs": [{"isAvailable": true, "isTemporary": false, "timer": 0, "maxTimer": 0, "cooldown": 3.2, "maxCooldown": 5}],  // MHR only
  "abnormalities": [{"id": "...", "name": "Attack Up", "icon": "...", "type": "Buff",
                     "timer": 60, "maxTimer": 60, "isInfinite": false, "level": 1, "isBuildUp": false}]
}
```

### `monsters` (array)
```jsonc
[{
  "index": 0, "id": 24, "name": "Rathalos", "variant": "Normal", "crown": "Silver",
  "health": 16000, "maxHealth": 16000, "stamina": 400, "maxStamina": 500,
  "isEnraged": false, "captureThreshold": 0.3,
  "target": "None", "manualTarget": "None",
  "position": {"x": 0, "y": 0, "z": 0},
  "weaknesses": ["Dragon", "Thunder"], "types": ["FlyingWyvern"],
  "parts": [{"id": "0", "name": "Head", "type": "Breakable", "health": 120, "maxHealth": 500,
             "flinch": 30, "maxFlinch": 200, "sever": 0, "maxSever": 0,
             "tenderize": 0, "maxTenderize": 45, "breakCount": 0}],
  "ailments": [{"id": "1", "name": "Poison", "counter": 0, "timer": 0, "maxTimer": 30,
                "buildUp": 50, "maxBuildUp": 200}]
}]
```
`index` is the runtime spawn-order index (stable per session); `id` is the
in-game monster id (not unique across monsters).

### `party`
```jsonc
{"size": 2, "maxSize": 4,
 "members": [{"name": "Hunter", "masterRank": 34, "damage": 12345, "weapon": "LongSword",
              "slot": 0, "isMyself": true, "type": "Player",
              "status": {"raw": 210, "elemental": 45, "affinity": 0.25}}]}
```

### `quest`
```jsonc
{"id": 12345, "name": "The Scorching Blade", "type": "Normal", "status": "InProgress",
 "deaths": 0, "maxDeaths": 3, "level": "HighRank", "stars": 7, "timeLeftSeconds": 3000}
```

### `chat`
```jsonc
{"isOpen": false,
 "messages": [{"message": "gl hf", "author": "Hunter", "type": "Player", "playerSlot": 0}]}
```

## Notes for consumers

- Monster/part/ailment state updates arrive with the whole `monsters`
  section — replace it wholesale rather than merging individual fields.
- The `weapon` sub-object matching the current weapon type is populated;
  others are omitted. `sharpness` is present for melee weapons.
- `tools` is present on Monster Hunter World/Wilds, `wirebugs` on Rise.
- Update frequency is bounded by HunterPie's polling rate and the
  broadcast interval; sections without changes are not resent.
