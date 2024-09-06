import path from "path"

import { defineConfig } from 'vite'

import mkcert from "vite-plugin-mkcert";
import react from '@vitejs/plugin-react'

 
export default defineConfig({
  server: {
    https: {

    },
    port: 5173,
    strictPort: true, 
    hmr: {
      clientPort: 5173, 
    },
  },
  optimizeDeps: {
    force: true,
  },
  build: {
    outDir: "../wwwroot",
    emptyOutDir: true
  },
  plugins: [
    react(),
    mkcert()
  ],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
})
