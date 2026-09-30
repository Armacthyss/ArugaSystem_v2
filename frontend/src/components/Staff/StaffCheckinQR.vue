<!--
  StaffCheckinQR.vue  —  /staff/checkin-qr
  Today's check-in QR for the clinic entrance (Figure 15 of the study).
  Print it, or leave this page open on a screen facing the waiting area.
  Parents scan it with their phone camera (it opens the Check-in page), use
  the Scan button on the Check-in page, or type the 6-character code printed
  under it.
-->
<template>
  <div class="flex h-screen bg-stone-50 antialiased text-stone-900 print:block print:h-auto print:bg-white" style="font-family: 'Inter','Segoe UI',sans-serif;">

    <StaffSidebar class="print:hidden" />

    <div class="flex-1 flex flex-col overflow-hidden print:overflow-visible">
      <StaffTopbar class="print:hidden" title="Check-in QR" breadcrumb="Aruga / Queue / Check-in QR" />

      <main class="flex-1 p-8 overflow-y-auto print:p-0 print:overflow-visible">
        <div class="max-w-5xl mx-auto grid grid-cols-1 lg:grid-cols-5 gap-6 print:block">

          <!-- THE POSTER -->
          <section class="lg:col-span-3 bg-white border border-stone-200 rounded-2xl shadow-sm p-8 text-center print:border-0 print:shadow-none print:p-0">
            <p class="text-xs font-bold uppercase tracking-widest text-emerald-700">Leveriza Health Center</p>
            <h1 class="text-3xl font-black text-stone-800 mt-1">Scan to Check In</h1>
            <p class="text-sm text-stone-500 mt-1">{{ todayLabel }}</p>

            <div v-if="loading" class="py-24 text-sm text-stone-400">Loading today's code…</div>

            <div v-else-if="error" class="my-10 rounded-xl bg-amber-50 border border-amber-200 p-6 text-sm text-amber-800">
              {{ error }}
            </div>

            <template v-else-if="qr">
              <div v-if="checkinLink" ref="qrBox" class="mx-auto my-6 w-[300px] h-[300px] [&>svg]:w-full [&>svg]:h-full"></div>
              <div v-else class="my-10 rounded-xl bg-amber-50 border border-amber-200 p-6 text-sm text-amber-800">
                Phones can't open "localhost". Connect this computer to the same Wi-Fi (or hotspot) as the phones, then press Refresh.
              </div>

              <p class="text-xs text-stone-500">
                Can't scan? Go to <b v-if="phoneHost">{{ phoneHost }}</b><template v-else>the Aruga parent portal</template>, sign in, open <b>Check In</b> and type:
              </p>
              <p class="mt-2 text-5xl font-black tracking-[0.3em] text-stone-800">{{ qr.shortCode }}</p>

              <p class="mt-6 text-xs text-stone-500">
                Valid today from {{ formatTime(qr.validFrom) }} until {{ formatTime(qr.validUntil) }} (queue cut-off)
              </p>
            </template>
          </section>

          <!-- STAFF CONTROLS -->
          <aside class="lg:col-span-2 space-y-4 print:hidden">
            <div class="bg-white border border-stone-200 rounded-2xl shadow-sm p-6">
              <h2 class="font-bold text-stone-800">How it works</h2>
              <ol class="mt-3 space-y-2 text-sm text-stone-600 list-decimal list-inside">
                <li>Print this page or show it on a screen at the entrance.</li>
                <li>Parents scan it with their phone camera, or with <b>Scan the Clinic QR Code</b> in Aruga's Check-in tab.</li>
                <li>They pick which children they brought, and get a queue number.</li>
                <li>They appear in <b>Today's Queue</b> on your dashboard.</li>
              </ol>
              <p class="mt-3 text-xs text-stone-400">A new code is made every clinic day, so yesterday's code (or a photo of it) can't be used to queue from home.</p>

              <div class="mt-5 flex gap-2">
                <button @click="printPoster" :disabled="!qr" class="flex-1 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 text-white text-sm font-semibold py-2.5">
                  Print
                </button>
                <button @click="load" class="flex-1 rounded-xl border border-stone-200 hover:bg-stone-50 text-sm font-semibold py-2.5">
                  Refresh
                </button>
              </div>
            </div>

            <!-- The address inside the QR (phones must be on the same network) -->
            <div class="bg-white border border-stone-200 rounded-2xl shadow-sm p-6">
              <h2 class="font-bold text-stone-800">Address phones open</h2>
              <p class="mt-1 text-xs text-stone-500">Phones must be on the same Wi-Fi (or hotspot) as this computer.</p>
              <p v-if="phoneOrigin" class="mt-3 font-mono text-sm font-semibold text-stone-800 break-all">{{ phoneOrigin }}</p>
              <p v-else class="mt-3 text-xs font-semibold text-amber-700">No network found. Connect to Wi-Fi or a hotspot, then press Refresh.</p>
              <template v-if="isLocalhost && lanAddresses.length > 1">
                <div class="mt-3 flex flex-wrap gap-2">
                  <button v-for="a in lanAddresses" :key="a" @click="chooseLan(a)"
                    class="rounded-lg border px-2.5 py-1 text-xs font-medium"
                    :class="a === chosenLan ? 'border-emerald-600 bg-emerald-50 text-emerald-700' : 'border-stone-200 text-stone-600 hover:bg-stone-50'">
                    {{ a }}
                  </button>
                </div>
                <p class="mt-2 text-[11px] text-stone-400">This computer is on more than one network; pick the one the phones are on.</p>
              </template>
            </div>

            <div class="bg-white border border-stone-200 rounded-2xl shadow-sm p-6">
              <div class="flex items-start justify-between gap-4">
                <div>
                  <h2 class="font-bold text-stone-800">Require QR for parent check-in</h2>
                  <p class="mt-1 text-xs text-stone-500">
                    {{ required
                      ? 'On: parents must scan this code (or type it) to check in from their phone.'
                      : 'Off: parents can check in from their phone without the code.' }}
                    Walk-ins without a phone can always be added from the dashboard.
                  </p>
                </div>
                <button
                  @click="toggleRequired"
                  :disabled="savingSetting"
                  class="relative h-6 w-11 shrink-0 rounded-full transition-colors disabled:opacity-50"
                  :class="required ? 'bg-emerald-600' : 'bg-stone-300'"
                  :aria-pressed="required"
                  aria-label="Require QR for parent check-in"
                >
                  <span class="absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-all" :class="required ? 'left-5.5' : 'left-0.5'"></span>
                </button>
              </div>
              <p v-if="settingMessage" class="mt-3 text-xs font-semibold text-emerald-700">{{ settingMessage }}</p>
            </div>
          </aside>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, nextTick } from 'vue'
import axios from 'axios'
import { BrowserQRCodeSvgWriter } from '@zxing/browser'
import StaffSidebar from './StaffSidebar.vue'
import StaffTopbar from './StaffTopbar.vue'
import { API_BASE, formatTime } from '@/utils/format'

const qr = ref(null)
const loading = ref(true)
const error = ref('')
const qrBox = ref(null)

const required = ref(true)
const savingSetting = ref(false)
const settingMessage = ref('')

const todayLabel = computed(() =>
  new Date().toLocaleDateString('en-PH', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' }))

// The address phones open. If staff use Aruga as "localhost", a phone can't
// (localhost on a phone is the phone itself), so the QR uses this computer's
// Wi-Fi address instead; the dev server lists it at /__lan (vite.config.js).
const isLocalhost = ['localhost', '127.0.0.1', '[::1]'].includes(window.location.hostname)
const lanAddresses = ref([])
const chosenLan = ref('')
const LAN_KEY = 'aruga.checkinQrAddress'

const phoneOrigin = computed(() => {
  if (!isLocalhost) return window.location.origin
  if (!chosenLan.value) return ''
  return `${window.location.protocol}//${chosenLan.value}${window.location.port ? `:${window.location.port}` : ''}`
})
const phoneHost = computed(() => phoneOrigin.value.replace(/^https?:\/\//, ''))

async function loadLanAddresses() {
  if (!isLocalhost) return
  try {
    const res = await fetch('/__lan')
    const list = res.ok ? await res.json() : []
    lanAddresses.value = Array.isArray(list) ? list : []
  } catch {
    lanAddresses.value = []
  }
  let saved = null
  try { saved = localStorage.getItem(LAN_KEY) } catch { /* storage blocked */ }
  chosenLan.value = lanAddresses.value.includes(saved) ? saved : (lanAddresses.value[0] || '')
}

async function chooseLan(address) {
  chosenLan.value = address
  try { localStorage.setItem(LAN_KEY, address) } catch { /* storage blocked */ }
  await nextTick()
  drawQr()
}

// The QR opens the parent Check-in page with today's code already filled in
const checkinLink = computed(() => qr.value && phoneOrigin.value ? `${phoneOrigin.value}/ParentCheckin?code=${qr.value.token}` : '')

async function load() {
  loading.value = true
  error.value = ''
  await loadLanAddresses()
  try {
    const [code, settings] = await Promise.all([
      axios.post(`${API_BASE}/QueueQRCode/generate`),
      axios.get(`${API_BASE}/QueueQRCode/settings`),
    ])
    required.value = !!settings.data?.isEnabled
    if (code.data.available === false) {
      qr.value = null
      error.value = code.data.message
    } else {
      qr.value = code.data
    }
  } catch (err) {
    qr.value = null
    error.value = err.response?.data?.message || 'Could not load today’s check-in code.'
  } finally {
    loading.value = false
  }
  await nextTick()
  drawQr()
}

function drawQr() {
  if (!qrBox.value || !checkinLink.value) return
  qrBox.value.innerHTML = ''
  const svg = new BrowserQRCodeSvgWriter().write(checkinLink.value, 300, 300)
  qrBox.value.appendChild(svg)
}

async function toggleRequired() {
  savingSetting.value = true
  settingMessage.value = ''
  try {
    const res = await axios.put(`${API_BASE}/QueueQRCode/settings`, { isEnabled: !required.value })
    required.value = !!res.data.isEnabled
    settingMessage.value = res.data.message
  } catch (err) {
    settingMessage.value = err.response?.data?.message || 'Could not save the setting.'
  } finally {
    savingSetting.value = false
  }
}

function printPoster() {
  window.print()
}

onMounted(load)
</script>
