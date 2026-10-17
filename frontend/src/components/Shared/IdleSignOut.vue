<!--
  IdleSignOut.vue — signs a Staff / Nurse, Admin / Doctor or Super Admin out
  after 15 minutes without using Aruga, so patient records aren't left open
  on a shared clinic computer (Oct 2026). A warning with a countdown shows
  for the last minute. Placed once in App.vue.

  - The last activity time is kept in localStorage, so using Aruga in one
    tab keeps the other tabs signed in too, and a browser reopened an hour
    later signs out at once.
  - Parents are not signed out: their Check-in page is meant to stay open
    while they wait to be called, often longer than 15 minutes.
  - The Check-in QR page is not an exception: staff print the day's code
    for the entrance instead of leaving the page open on a screen.
-->
<template>
  <Teleport to="body">
    <div v-if="secondsLeft !== null" class="fixed inset-0 z-[400] flex items-center justify-center bg-black/40 p-4" role="alertdialog" aria-modal="true" aria-labelledby="idle-title">
      <div class="w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl text-center">
        <div class="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-amber-50">
          <Clock class="h-6 w-6 text-amber-600" />
        </div>
        <p id="idle-title" class="text-[16px] font-bold text-stone-900">Still there?</p>
        <p class="mt-1.5 text-[13.5px] text-stone-600">
          To protect patient records, you'll be signed out in
          <span class="font-bold text-stone-900">{{ secondsLeft }} second{{ secondsLeft === 1 ? '' : 's' }}</span>
          because Aruga hasn't been used for a while.
        </p>
        <div class="mt-5 flex flex-col gap-2">
          <button ref="stayButton" @click="markActivity(true)" class="w-full rounded-xl bg-emerald-700 py-2.5 text-[14px] font-semibold text-white hover:bg-emerald-800">Stay signed in</button>
          <button @click="signOut" class="w-full rounded-xl py-2 text-[13px] font-medium text-stone-500 hover:bg-stone-50">Sign out now</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, nextTick, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { Clock } from 'lucide-vue-next'
import { isLoggedIn, getRole, logout, LAST_ACTIVITY_KEY } from '@/utils/auth'

const IDLE_LIMIT = 15 * 60 * 1000   // signed out after 15 minutes
const WARN_BEFORE = 60 * 1000       // warning for the last minute
const ROLES = ['Staff', 'SystemAdmin', 'SuperAdmin']

const router = useRouter()
const secondsLeft = ref(null)       // null = no warning showing
const stayButton = ref(null)

const watching = () => isLoggedIn() && ROLES.includes(getRole())

function readLast() {
  try { return Number(localStorage.getItem(LAST_ACTIVITY_KEY)) || 0 } catch { return 0 }
}

// Activity is written at most every 5 seconds (mouse moves fire constantly).
// While the warning shows, only "Stay signed in" (or a key press) keeps the
// session, so a bumped mouse doesn't silently cancel it.
let lastWrite = 0
function markActivity(force = false) {
  if (!isLoggedIn()) return
  if (secondsLeft.value !== null && force !== true) return
  const now = Date.now()
  // Already past the limit (e.g. the browser was reopened later): the next
  // check signs out; moving the mouse doesn't bring the session back.
  const last = readLast()
  if (last && now - last >= IDLE_LIMIT) return
  if (force !== true && now - lastWrite < 5000) return
  lastWrite = now
  try { localStorage.setItem(LAST_ACTIVITY_KEY, String(now)) } catch { /* private mode: timer still runs */ }
  secondsLeft.value = null
}
const onKey = () => markActivity(true)
const onActivity = () => markActivity()

function signOut() {
  secondsLeft.value = null
  const here = router.currentRoute.value.fullPath
  logout()
  router.push({ path: '/', query: { idle: '1', redirect: here } })
}

function tick() {
  if (!watching()) { secondsLeft.value = null; return }
  let last = readLast()
  // Just signed in (sign-out clears the time): start counting from now
  if (!last) { markActivity(true); last = Date.now() }
  const idle = Date.now() - last
  if (idle >= IDLE_LIMIT) return signOut()
  if (idle >= IDLE_LIMIT - WARN_BEFORE) {
    const wasHidden = secondsLeft.value === null
    secondsLeft.value = Math.ceil((IDLE_LIMIT - idle) / 1000)
    if (wasHidden) nextTick(() => stayButton.value?.focus())
  } else {
    secondsLeft.value = null
  }
}

const EVENTS = ['mousemove', 'mousedown', 'wheel', 'touchstart', 'scroll']
let timer = null
onMounted(() => {
  EVENTS.forEach(e => window.addEventListener(e, onActivity, { passive: true, capture: true }))
  window.addEventListener('keydown', onKey, true)
  // Another tab was used or signed out: re-check right away
  window.addEventListener('storage', tick)
  router.afterEach(() => markActivity(true))
  timer = setInterval(tick, 1000)
  tick()
})
onUnmounted(() => {
  EVENTS.forEach(e => window.removeEventListener(e, onActivity, { capture: true }))
  window.removeEventListener('keydown', onKey, true)
  window.removeEventListener('storage', tick)
  clearInterval(timer)
})
</script>
