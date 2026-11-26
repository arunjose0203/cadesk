import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// In development the API runs on http://localhost:5080 (see the API's launchSettings.json).
export default defineConfig({
  plugins: [tailwindcss(), react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
      '/dev': 'http://localhost:5080',
    },
  },
})
