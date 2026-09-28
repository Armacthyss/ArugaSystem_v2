<!--
  CalledAlert.vue — "It's your turn" alert on every parent page.

  Leveriza has one vaccination room. When the Nurse presses Call Next, the
  family's queue entry becomes In Progress (GET /api/Queue/my-status →
  called = true). This shows a full-screen banner with a chime (and a
  vibration on Android) so the parent knows to go inside. "Call Again"
  changes calledAt, which shows the banner again. The parent also gets one
  SMS from the API, in case the phone is in their pocket.

  Browsers only allow sound after the page was tapped once; the first tap
  anywhere on the page unlocks it.
-->
<template>
  <Teleport to="body">
    <div v-if="visible" class="fixed inset-0 z-[300] flex items-center justify-center bg-emerald-900/70 p-4" role="alertdialog" aria-modal="true" aria-labelledby="called-title">
      <div class="w-full max-w-sm rounded-2xl bg-white p-7 text-center shadow-2xl called-pop">
        <div class="mx-auto mb-4 flex h-20 w-20 items-center justify-center rounded-full bg-emerald-100 text-4xl called-ring">🔔</div>
        <p class="text-[11px] font-black uppercase tracking-widest text-emerald-600">Leveriza Health Center</p>
        <h2 id="called-title" class="mt-1 text-2xl font-black text-slate-800">It's your turn!</h2>
        <p class="mt-2 text-4xl font-black text-emerald-600">Queue #{{ String(status.myQueueNumber).padStart(3, '0') }}</p>
        <p class="mt-3 text-sm text-slate-600">
          Please bring <strong class="text-slate-800">{{ childNames }}</strong> inside the
          <strong class="text-slate-800">vaccination room</strong> now.
        </p>
        <button @click="dismiss" class="mt-6 w-full rounded-xl bg-emerald-600 py-3.5 text-sm font-bold text-white hover:bg-emerald-700">
          OK, we're going in
        </button>
      </div>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import api from '../Composables/api'

const props = defineProps({
  parentId: { type: String, default: null },
  children: { type: Array, default: () => [] },
})

const status = ref({})
const visible = ref(false)
const DISMISSED_KEY = 'arugaCalledDismissed'

const childNames = computed(() => {
  const ids = (status.value.childIDs || []).map(id => String(id).toLowerCase())
  const names = props.children
    .filter(c => ids.includes(String(c.childID).toLowerCase()))
    .map(c => c.firstName)
  return names.length ? names.join(' and ') : 'your child'
})

// calledAt identifies one call; a "Call Again" gives a new one
const callKey = computed(() => (status.value.called ? `${status.value.myQueueNumber}|${status.value.calledAt}` : null))

function readDismissed() {
  try { return sessionStorage.getItem(DISMISSED_KEY) } catch { return null }
}

async function refresh() {
  if (!props.parentId) return
  try {
    const res = await api.get(`/Queue/my-status/${props.parentId}`)
    status.value = res.data || {}
  } catch {
    // Offline for a moment: try again on the next poll
  }
}

watch(callKey, (key) => {
  if (key && key !== readDismissed()) {
    visible.value = true
    ring()
  } else if (!key) {
    visible.value = false
  }
})

function dismiss() {
  visible.value = false
  try { sessionStorage.setItem(DISMISSED_KEY, callKey.value || '') } catch { /* private mode */ }
  document.title = originalTitle
}

// ── Sound, vibration and a blinking tab title ──────────────────────────
let audioCtx = null
function unlockAudio() {
  try {
    audioCtx ||= new (window.AudioContext || window.webkitAudioContext)()
    if (audioCtx.state === 'suspended') audioCtx.resume()
  } catch { /* no Web Audio */ }
}

function ring() {
  navigator.vibrate?.([400, 200, 400, 200, 400])
  blinkTitle()
  if (!audioCtx) return
  const start = audioCtx.currentTime
  // Three rising chimes, twice
  ;[0, 0.25, 0.5, 1.2, 1.45, 1.7].forEach((offset, i) => {
    const osc = audioCtx.createOscillator()
    const gain = audioCtx.createGain()
    osc.type = 'sine'
    osc.frequency.value = [660, 880, 1100][i % 3]
    gain.gain.setValueAtTime(0.0001, start + offset)
    gain.gain.exponentialRampToValueAtTime(0.4, start + offset + 0.02)
    gain.gain.exponentialRampToValueAtTime(0.0001, start + offset + 0.22)
    osc.connect(gain).connect(audioCtx.destination)
    osc.start(start + offset)
    osc.stop(start + offset + 0.25)
  })
}

const originalTitle = document.title
let titleTimer = null
function blinkTitle() {
  clearInterval(titleTimer)
  let on = false
  titleTimer = setInterval(() => {
    if (!visible.value) { clearInterval(titleTimer); document.title = originalTitle; return }
    on = !on
    document.title = on ? '🔔 Your turn!' : originalTitle
  }, 1000)
}

// Every 5 s while checked in and not finished; every 30 s otherwise (to
// notice a check-in made by the staff)
let timer = null
function schedule() {
  clearTimeout(timer)
  const active = status.value.checkedIn && status.value.myStatus !== 'Completed'
  timer = setTimeout(async () => { await refresh(); schedule() }, active ? 5000 : 30000)
}

onMounted(async () => {
  window.addEventListener('pointerdown', unlockAudio, { passive: true })
  window.addEventListener('keydown', unlockAudio)
  await refresh()
  schedule()
})

onBeforeUnmount(() => {
  clearTimeout(timer)
  clearInterval(titleTimer)
  document.title = originalTitle
  window.removeEventListener('pointerdown', unlockAudio)
  window.removeEventListener('keydown', unlockAudio)
})
</script>

<style scoped>
.called-pop { animation: pop 0.35s ease-out; }
.called-ring { animation: ring 1.2s ease-in-out infinite; }
@keyframes pop { from { transform: scale(0.9); opacity: 0; } to { transform: scale(1); opacity: 1; } }
@keyframes ring { 0%, 100% { transform: rotate(0); } 10%, 30% { transform: rotate(-14deg); } 20%, 40% { transform: rotate(14deg); } 50% { transform: rotate(0); } }
@media (prefers-reduced-motion: reduce) { .called-pop, .called-ring { animation: none; } }
</style>
