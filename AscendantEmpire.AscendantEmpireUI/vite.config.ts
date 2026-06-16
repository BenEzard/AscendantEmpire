/// <reference types="vitest/config" />
// Import defineConfig from 'vitest/config' (not 'vite') so the `test` block below is typed.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
   alias: {
     "@": path.resolve(__dirname, "src"),
   },
 },

  // Forces Vite to check timestamps on every request
  server: {
    watch: {
      usePolling: true, // Ensures file edits are always detected
      interval: 100,    // Poll every 100ms (safe default)
    },
  },

  // Optional: disables heavy caching between runs
  optimizeDeps: {
    force: true,
  },

  // Multi-page app: each feature has its own HTML entry so a browser tab only
  // ever loads/mounts one feature. The root index.html is a tiny landing page.
  build: {
    rollupOptions: {
      input: {
        landing: path.resolve(__dirname, "index.html"),
        game: path.resolve(__dirname, "game/index.html"),
        mapeditor: path.resolve(__dirname, "mapeditor/index.html"),
        matchmaker: path.resolve(__dirname, "matchmaker/index.html"),
      },
    },
  },

  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
    },
  },
});