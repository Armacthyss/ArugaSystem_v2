<template>
  <Transition name="notif-panel" appear>
    <div class="fixed inset-0 z-200 flex justify-end" @click.self="$emit('close')">
      <div class="absolute inset-0 bg-black/40 backdrop-blur-sm" @click="$emit('close')"></div>
      <div class="relative w-full max-w-100 h-full bg-white shadow-2xl flex flex-col">
        <div class="bg-emerald-600 px-6 py-5 flex items-center justify-between text-white shrink-0">
          <div class="flex items-center gap-3">
            <span class="text-xl">🔔</span>
            <span class="font-bold text-base">Notifications</span>
            <span v-if="unreadCount > 0" class="bg-red-500 text-[10px] px-2.5 py-1 rounded-full font-bold">{{ unreadCount }} NEW</span>
          </div>
          <div class="flex items-center gap-3">
            <button v-if="unreadCount > 0" @click="markAllRead" class="text-[10px] font-bold text-white/70 hover:text-white uppercase tracking-wide transition-colors">Mark all read</button>
            <button @click="$emit('close')" class="w-8 h-8 rounded-xl bg-white/10 hover:bg-white/20 flex items-center justify-center text-lg transition-colors">✕</button>
          </div>
        </div>
        <div class="flex gap-1 p-3 bg-slate-50 border-b border-slate-100 shrink-0">
          <button v-for="tab in notifTabs" :key="tab.key" @click="notifFilter = tab.key"
                  :class="notifFilter === tab.key ? 'bg-emerald-600 text-white shadow-sm' : 'text-slate-500 hover:bg-white'"
                  class="flex-1 py-1.5 rounded-lg text-[9px] font-bold uppercase tracking-wide transition-all">
            {{ tab.label }}<span v-if="tab.count > 0" class="ml-1 opacity-70">({{ tab.count }})</span>
          </button>
        </div>
        <div class="flex-1 overflow-y-auto">
          <div v-if="notifsLoading" class="flex flex-col items-center justify-center h-40 text-slate-400"><p class="text-2xl mb-2 animate-pulse">🔔</p><p class="text-xs font-bold">Loading notifications...</p></div>
          <div v-else-if="filteredNotifications.length === 0" class="flex flex-col items-center justify-center h-40 text-slate-400 px-8 text-center"><p class="text-3xl mb-3">✅</p><p class="text-sm font-bold text-slate-600">All caught up!</p><p class="text-xs text-slate-400 mt-1">No {{ notifFilter === 'all' ? '' : notifFilter + ' ' }}notifications.</p></div>
          <div v-else class="p-3 space-y-2">
            <div v-for="notif in filteredNotifications" :key="notif.notificationID" @click="markRead(notif)"
                :class="notif.isRead ? 'bg-white border-transparent' : 'bg-emerald-50 border-emerald-500/30'"
                class="p-4 rounded-xl border cursor-pointer hover:shadow-sm transition-all">
              <div class="flex items-start gap-3">
                <div :class="notifIconBg(notif.type)" class="w-10 h-10 rounded-xl flex items-center justify-center text-lg shrink-0 shadow-sm">{{ notifIcon(notif.type) }}</div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-start justify-between gap-2">
                    <p class="text-xs font-bold text-slate-800 leading-tight">{{ notif.title }}</p>
                    <span v-if="!notif.isRead" class="w-2 h-2 rounded-full bg-emerald-500 shrink-0 mt-1"></span>
                  </div>
                  <p class="text-[10px] text-slate-500 mt-1 leading-relaxed whitespace-pre-line">{{ notif.message }}</p>
                  <p class="text-[9px] text-slate-400 mt-2 font-bold uppercase tracking-wide">{{ formatRelativeTime(notif.createdAt) }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="shrink-0 px-6 py-4 border-t border-slate-100 bg-slate-50">
          <p class="text-[9px] text-slate-400 text-center font-bold uppercase tracking-wide">Reminders 2 weeks · 1 week · 5 · 3 · 1 day before each vaccine, by app, email and SMS</p>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, onMounted } from 'vue'
import axios from 'axios'

const API_BASE_URL = API_ORIGIN

// parentData is fetched/owned by whichever page renders this component
// (see HARD RULE 3) and passed down as a prop, just for the parentID
// needed to fetch this parent's notifications. This component fetches
// and owns the actual notification list/state itself.
const props = defineProps({
  parentData: { type: Object, default: null },
})

defineEmits(['close'])

const notifications = ref([])
const notifsLoading  = ref(false)
const notifFilter    = ref('all')

const unreadCount = computed(() => notifications.value.filter(n => !n.isRead).length)

const notifTabs = computed(() => [
  { key: 'all',      label: 'All',      count: notifications.value.length },
  { key: 'reminder', label: 'Upcoming', count: notifications.value.filter(n => n.type?.startsWith('Reminder')).length },
  { key: 'overdue',  label: 'Overdue',  count: notifications.value.filter(n => n.type?.startsWith('Overdue')).length },
  { key: 'stock',    label: 'Stock',    count: notifications.value.filter(n => n.type?.startsWith('Stock')).length },
  { key: 'unread',   label: 'Unread',   count: unreadCount.value },
])

const filteredNotifications = computed(() => {
  switch (notifFilter.value) {
    case 'reminder': return notifications.value.filter(n => n.type?.startsWith('Reminder'))
    case 'overdue':  return notifications.value.filter(n => n.type?.startsWith('Overdue'))
    case 'stock':    return notifications.value.filter(n => n.type?.startsWith('Stock'))
    case 'unread':   return notifications.value.filter(n => !n.isRead)
    default:
      return [...notifications.value].sort((a, b) => {
        if (!a.isRead && b.isRead) return -1
        if (a.isRead && !b.isRead) return 1
        return new Date(b.createdAt) - new Date(a.createdAt)
      })
  }
})

function notifIcon(type) {
  const m = { ReminderMonth:'📅', Reminder2Week:'📅', ReminderWeek:'⏰', Reminder5Day:'⚡', Reminder3Day:'⚡', ReminderDay:'🔔', Completed:'💉', RecordUpdated:'📝', Announcement:'📢', StockOut:'📦', StockBack:'✅', OverdueMiss:'😟', Overdue5Day:'⚠️', Overdue2Week:'🚨', OverdueUrgent:'🆘', StockAlert:'📦', StockResolved:'✅', ClinicClosed:'⛔' }
  return m[type] ?? '🔔'
}
function notifIconBg(type) {
  const m = { ReminderMonth:'bg-blue-100', Reminder2Week:'bg-blue-100', ReminderWeek:'bg-amber-100', Reminder5Day:'bg-orange-100', Reminder3Day:'bg-orange-100', ReminderDay:'bg-orange-200', Completed:'bg-emerald-100', RecordUpdated:'bg-sky-100', Announcement:'bg-violet-100', StockOut:'bg-red-100', StockBack:'bg-emerald-100', OverdueMiss:'bg-amber-100', Overdue5Day:'bg-orange-200', Overdue2Week:'bg-red-200', OverdueUrgent:'bg-red-400', StockAlert:'bg-red-100', StockResolved:'bg-emerald-100', ClinicClosed:'bg-rose-100' }
  return m[type] ?? 'bg-slate-100'
}
function formatRelativeTime(dateStr) {
  if (!dateStr) return ''
  const diff  = Date.now() - new Date(dateStr).getTime()
  const mins  = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days  = Math.floor(diff / 86400000)
  if (mins < 1)   return 'Just now'
  if (mins < 60)  return `${mins}m ago`
  if (hours < 24) return `${hours}h ago`
  if (days < 7)   return `${days}d ago`
  return new Date(dateStr).toLocaleDateString('en-PH', { month: 'short', day: 'numeric' })
}

async function fetchNotifications() {
  if (!props.parentData?.parentID) return
  notifsLoading.value = true
  try {
    const res = await axios.get(`${API_BASE_URL}/api/Notifications/parent/${props.parentData.parentID}`)
    notifications.value = res.data
  } catch {
    notifications.value = []
  } finally {
    notifsLoading.value = false
  }
}

async function markRead(notif) {
  if (notif.isRead) return
  notif.isRead = true
  try { await axios.patch(`${API_BASE_URL}/api/Notifications/mark-read/${notif.notificationID}`) } catch {}
}
async function markAllRead() {
  if (!props.parentData?.parentID) return
  notifications.value.forEach(n => { n.isRead = true })
  try { await axios.patch(`${API_BASE_URL}/api/Notifications/mark-all-read/${props.parentData.parentID}`) } catch {}
}

onMounted(fetchNotifications)
</script>

<style scoped>
.notif-panel-enter-active, .notif-panel-leave-active { transition: opacity 0.25s ease; }
.notif-panel-enter-active > div:last-child, .notif-panel-leave-active > div:last-child { transition: transform 0.3s cubic-bezier(0.4,0,0.2,1); }
.notif-panel-enter-from, .notif-panel-leave-to { opacity: 0; }
.notif-panel-enter-from > div:last-child, .notif-panel-leave-to > div:last-child { transform: translateX(100%); }
</style>