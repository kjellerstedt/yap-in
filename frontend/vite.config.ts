import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

// The .NET server serves the built page from its wwwroot. In development Vite serves the page and forwards /api to the
// server, presenting the server's own origin: the server only accepts changes from the page it serves.
const serverOrigin = "http://127.0.0.1:4477";

export default defineConfig({
  plugins: [vue()],
  build: { outDir: "../Web/wwwroot", emptyOutDir: true },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: serverOrigin,
        changeOrigin: true,
        headers: { origin: serverOrigin },
      },
    },
  },
});
