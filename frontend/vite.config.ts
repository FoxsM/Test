import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// В dev фронтенд ходит в API через прокси того же origin — CORS в разработке не нужен вовсе.
// В docker-compose статику отдаёт nginx и проксирует /api на контейнер api (см. nginx.conf).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5080",
        changeOrigin: true,
      },
    },
  },
});
