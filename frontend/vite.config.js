import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// En desarrollo las llamadas a /api se redirigen a la API local (.NET).
const apiTarget = process.env.API_PROXY_TARGET || 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
  test: {
    environment: 'jsdom',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: ['src/lib/**'],
    },
  },
});
