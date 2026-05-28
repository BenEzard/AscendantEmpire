/// <reference types="vitest" />
import { defineConfig } from 'vite'
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