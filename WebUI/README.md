# HunterPie WebUI

Mobile/tablet-friendly single-screen dashboard for the HunterPie API.
Svelte 5 + TypeScript + Vite, zero runtime UI dependencies (~23 KB gzipped).

## Consumes

- `GET /api/v1/*` — REST snapshots (fallback/polling mode)
- `/ws` — WebSocket live stream (`hello` → `snapshot` → `state` section
  pushes + discrete `event`s)

## Develop

```bash
npm install
npm run dev        # http://localhost:5173, proxies /api + /ws to 127.0.0.1:7273
npm run mock       # optional: mock API server on :7273 for game-less dev
npm run check      # svelte-check + tsc
```

If the API is on another host: open the UI with `?api=host:7273`
(persisted in localStorage).

## Build & distribution

```bash
npm run build      # outputs dist/ with relative base (works from any path)
```

`Scripts/linux-compiler/publish-release.sh` builds the WebUI and copies
`dist/` into the HunterPie package as `WebUI/` — the API server serves it
at `http://<host>:7273/`. The folder is loose content (like `Themes/`):
replacing it updates the UI without rebuilding HunterPie.

## Layout

`100dvh` CSS grid, no scrolling: header (game/quest/timer/connection),
monsters (dominant, ≤3 cards), player (vitals/weapon/tools/abnormalities),
party. Density adapts at 1100px and 700px breakpoints; phone layout
collapses detail sections instead of scrolling.
