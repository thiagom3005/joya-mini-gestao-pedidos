import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Desenvolvimento local (spec §6.6): o proxy do Vite faz o mesmo que o nginx do container, encaminhando /api/* para a
// API e removendo o prefixo. Com isso, frontend e API ficam na mesma origem e não há CORS.
const destinoDaApi = process.env['API_URL'] ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: destinoDaApi,
        changeOrigin: true,
        rewrite: (caminho) => caminho.replace(/^\/api/, ''),
      },
    },
  },
})
