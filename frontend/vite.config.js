import { fileURLToPath, URL } from 'node:url'
import os from 'node:os'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import tailwindcss from '@tailwindcss/vite'

// The ASP.NET API. The site passes every /api/... request on to it, so phones
// only need to reach this dev server (the API itself stays on this computer).
const API_TARGET = process.env.ARUGA_API || 'http://localhost:57147'

const apiProxy = {
  '/api': {
    target: API_TARGET,
    changeOrigin: true,
    xfwd: true, // tells the API the phone's address, for the audit log
  },
}

// This computer's addresses on the local network (e.g. 192.168.1.5). The
// check-in QR uses one of them: a QR that says "localhost" would make a phone
// look for Aruga on the phone itself. Virtual adapters (WSL, VirtualBox...)
// aren't reachable from phones, so they're skipped.
function lanAddresses() {
  return Object.entries(os.networkInterfaces())
    .filter(([name]) => !/vEthernet|WSL|VirtualBox|VMware|Hyper-V|docker|vbox|Loopback/i.test(name))
    .flatMap(([, list]) => list || [])
    .filter(a => a.family === 'IPv4' && !a.internal && /^(10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.)/.test(a.address))
    .map(a => a.address)
}

// GET /__lan -> ["192.168.1.5", ...]  (read by the Staff Check-in QR page)
const lanAddressEndpoint = {
  name: 'aruga-lan-addresses',
  configureServer(server) {
    server.middlewares.use('/__lan', (req, res) => {
      res.setHeader('Content-Type', 'application/json')
      res.end(JSON.stringify(lanAddresses()))
    })
  },
  configurePreviewServer(server) {
    server.middlewares.use('/__lan', (req, res) => {
      res.setHeader('Content-Type', 'application/json')
      res.end(JSON.stringify(lanAddresses()))
    })
  },
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    // The Vue DevTools button floats over every page (and over content on
    // phones), so it's off unless you start the site with VUE_DEVTOOLS=on,
    // e.g. in PowerShell:  $env:VUE_DEVTOOLS="on"; npm run dev
    process.env.VUE_DEVTOOLS === 'on' && vueDevTools(),
    tailwindcss(),
    lanAddressEndpoint,
  ].filter(Boolean),
  // host: true = also reachable from phones on the same Wi-Fi
  // (open the "Network" address `npm run dev` prints)
  server: { host: true, port: 5173, proxy: apiProxy },
  preview: { host: true, proxy: apiProxy },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    },
  },
})
