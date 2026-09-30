<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import { Bell, Settings, KeyRound, Clock, LogOut, ArrowRight } from 'lucide-vue-next'
import { getUser, getRole, logout } from '@/utils/auth'

// The Super Admin gets no clinic alerts or clinic settings
const isSuperAdmin = getRole() === 'SuperAdmin'
import { API_BASE, relativeTime } from '@/utils/format'

defineProps({
  title: { type: String, required: true },
  breadcrumb: { type: String, default: '' },
})

// Pages may still listen for these; the header now handles both itself.
const emit = defineEmits(['notification-click', 'settings-click'])

const router = useRouter()
const user = computed(() => getUser() || {})

const userInitials = computed(() => {
  const first = (user.value.FirstName || user.value.firstName || '')[0] || ''
  const last = (user.value.LastName || user.value.lastName || '')[0] || ''
  return `${first}${last}`.toUpperCase() || 'SA'
})

// Which dropdown is open: 'bell' | 'settings' | null
const open = ref(null)
const toggle = (key) => {
  open.value = open.value === key ? null : key
  if (key === 'bell') { emit('notification-click'); if (open.value) loadNotifications() }
  if (key === 'settings') emit('settings-click')
}
const closeAll = (event) => {
  if (event.target.closest('.header-trigger') || event.target.closest('.header-panel')) return
  open.value = null
}

// ── The admin's own alerts (e.g. low vaccine stock) ──────────
const notifications = ref([])
const unread = computed(() => notifications.value.filter(n => !n.isRead).length)
let poll = null

async function loadNotifications() {
  if (!user.value.UserID || isSuperAdmin) return
  try {
    const res = await axios.get(`${API_BASE}/Notifications/user/${user.value.UserID}`)
    notifications.value = res.data.slice(0, 20)
  } catch {
    notifications.value = []
  }
}

async function markAllRead() {
  notifications.value.forEach(n => { n.isRead = true })
  try { await axios.patch(`${API_BASE}/Notifications/mark-all-read/user/${user.value.UserID}`) } catch { /* shown as read anyway */ }
}

function go(path) {
  open.value = null
  router.push(path)
}

function signOut() {
  logout()
  router.push('/')
}

onMounted(() => {
  loadNotifications()
  poll = setInterval(loadNotifications, 60000)
  document.addEventListener('click', closeAll)
})
onUnmounted(() => {
  clearInterval(poll)
  document.removeEventListener('click', closeAll)
})
</script>

<template>
  <header
    class="h-17.5 sticky top-0 z-20 bg-white border-b border-slate-200 flex items-center justify-between px-6 gap-4"
  >
    <!-- Page title -->
    <div class="min-w-0">
      <h1 class="text-lg font-bold text-slate-900 truncate">
        {{ title }}
      </h1>

      <p
        v-if="breadcrumb"
        class="text-xs text-slate-500 truncate"
      >
        {{ breadcrumb }}
      </p>
    </div>

    <!-- Right side -->
    <div class="flex items-center gap-3 shrink-0">

      <!-- Notifications -->
      <div v-if="!isSuperAdmin" class="relative">
        <button
          @click.stop="toggle('bell')"
          class="header-trigger relative w-9 h-9 rounded-lg flex items-center justify-center text-slate-500 hover:bg-slate-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          aria-label="Notifications"
        >
          <Bell class="w-5 h-5" :stroke-width="1.8" />
          <span
            v-if="unread > 0"
            class="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-rose-500 ring-2 ring-white"
          ></span>
        </button>

        <div v-if="open === 'bell'" class="header-panel absolute right-0 top-11 z-50 w-80 rounded-xl border border-slate-200 bg-white shadow-lg overflow-hidden">
          <div class="flex items-center justify-between px-4 py-3 border-b border-slate-100">
            <p class="text-sm font-semibold text-slate-900">My Alerts</p>
            <button v-if="unread > 0" @click="markAllRead" class="text-[11px] font-semibold text-emerald-700 hover:underline">Mark all read</button>
          </div>
          <div class="max-h-72 overflow-y-auto">
            <div
              v-for="n in notifications"
              :key="n.notificationID"
              @click="['StockCheck', 'LowStock'].includes(n.type) && go('/system-admin/inventory')"
              class="px-4 py-3 border-b border-slate-50 last:border-0"
              :role="['StockCheck', 'LowStock'].includes(n.type) ? 'button' : undefined"
              :style="['StockCheck', 'LowStock'].includes(n.type) ? 'cursor:pointer' : ''"
              :class="n.isRead ? '' : 'bg-emerald-50/50'"
            >
              <p class="text-[12.5px] font-semibold text-slate-800">{{ n.title }}</p>
              <p class="text-[11.5px] text-slate-500 mt-0.5 whitespace-pre-line">{{ n.message }}</p>
              <p class="text-[10px] text-slate-400 mt-1">{{ relativeTime(n.createdAt) }}</p>
            </div>
            <p v-if="notifications.length === 0" class="px-4 py-6 text-center text-xs text-slate-400">
              No alerts. The weekly stock check and low-stock warnings show up here.
            </p>
          </div>
          <button @click="go('/system-admin/notifications')" class="w-full flex items-center justify-center gap-1.5 px-4 py-2.5 text-xs font-semibold text-emerald-700 border-t border-slate-100 hover:bg-slate-50">
            Parent notifications & announcements <ArrowRight class="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      <!-- Settings -->
      <div class="relative">
        <button
          @click.stop="toggle('settings')"
          class="header-trigger w-9 h-9 rounded-lg flex items-center justify-center text-slate-500 hover:bg-slate-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          aria-label="Settings"
        >
          <Settings class="w-5 h-5" :stroke-width="1.8" />
        </button>

        <div v-if="open === 'settings'" class="header-panel absolute right-0 top-11 z-50 w-56 rounded-xl border border-slate-200 bg-white shadow-lg overflow-hidden py-1">
          <button @click="go('/ChangePassword')" class="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50">
            <KeyRound class="w-4 h-4 text-slate-400" /> Change my password
          </button>
          <button v-if="!isSuperAdmin" @click="go('/system-admin/operating-hours')" class="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm text-slate-700 hover:bg-slate-50">
            <Clock class="w-4 h-4 text-slate-400" /> Clinic operating hours
          </button>
          <button @click="signOut" class="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm text-rose-700 hover:bg-rose-50 border-t border-slate-100">
            <LogOut class="w-4 h-4" /> Log out
          </button>
        </div>
      </div>

      <!-- Logged-in user avatar -->
      <div
        class="w-9 h-9 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-xs font-bold shrink-0"
        :title="`${user.FirstName || ''} ${user.LastName || ''}`.trim()"
      >
        {{ userInitials }}
      </div>

    </div>
  </header>
</template>
