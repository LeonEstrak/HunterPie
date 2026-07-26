import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

// https://vite.dev/config/
export default defineConfig({
  plugins: [svelte()],
  // Relative base so the bundle works when served from any path by HunterPie
  base: './',
  server: {
    proxy: {
      '/api': 'http://127.0.0.1:7273',
      '/ws': {
        target: 'ws://127.0.0.1:7273',
        ws: true
      }
    }
  }
})
