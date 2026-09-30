<script setup>
import { ref, computed, reactive, onMounted } from 'vue'
import { useFloatingMenu } from '@/utils/floatingMenu'
import axios from 'axios'
import AppSidebar from './Components/AppSidebar.vue'
import AppHeader from './Components/AppHeader.vue'
import { API_BASE, formatDate, isSameDay, toISODate, downloadCSV } from '@/utils/format'
import { askConfirm } from '@/utils/dialog'

/* -------------------------------- Status meta -------------------------------- */
// In-app notifications are delivered the moment they're created, so the
// status shown is Read / Delivered (read = the recipient opened it).
const statusMeta = {
  Read: { tint: 'bg-emerald-50', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  Delivered: { tint: 'bg-sky-50', text: 'text-sky-700', dot: 'bg-sky-500' },
}

const typeOptions = computed(() => [...new Set(notifications.value.map(n => n.type))].sort())

/* -------------------------------- Notification data (GET /api/Notifications) -------------------------------- */
const notifications = ref([])
const loading = ref(false)
const loadError = ref('')
const actionMessage = ref('')

async function fetchNotifications() {
  loading.value = true
  loadError.value = ''
  try {
    const res = await axios.get(`${API_BASE}/Notifications`)
    notifications.value = res.data.map(n => ({
      id: n.notificationID,
      title: n.title,
      recipient: n.recipient,
      recipientType: n.recipientType,
      child: n.child,
      type: n.category,
      rawType: n.type,
      scheduledDate: n.scheduledDate ? formatDate(n.scheduledDate) : '—',
      sentDate: formatDate(n.createdAt),
      createdAt: n.createdAt,
      status: n.isRead ? 'Read' : 'Delivered',
      message: n.message,
      deliveryMethod: 'In-App',
      opened: n.isRead,
    }))
  } catch (e) {
    console.error('fetchNotifications:', e)
    loadError.value = 'Could not load notifications. Check that the API is running.'
  } finally {
    loading.value = false
  }
}

onMounted(() => { fetchNotifications(); loadChannels() })

function flash(msg) {
  actionMessage.value = msg
  setTimeout(() => { actionMessage.value = '' }, 3500)
}

const exportLogs = () => {
  downloadCSV(`notification-log-${toISODate()}.csv`, [
    ['Title', 'Recipient', 'Child', 'Type', 'Scheduled Date', 'Sent Date', 'Status', 'Message'],
    ...filteredNotifications.value.map(n => [n.title, n.recipient, n.child, n.type, n.scheduledDate, n.sentDate, n.status, n.message]),
  ])
}

/* ---------------------------- Toolbar / filters ---------------------------- */
const searchQuery = ref('')
const typeFilter = ref('All')
const statusFilter = ref('All')

const filteredNotifications = computed(() =>
  notifications.value.filter((n) => {
    const q = searchQuery.value.trim().toLowerCase()
    const matchesSearch =
      !q || n.recipient.toLowerCase().includes(q) || n.child.toLowerCase().includes(q) || n.title.toLowerCase().includes(q)
    const matchesType = typeFilter.value === 'All' || n.type === typeFilter.value
    const matchesStatus = statusFilter.value === 'All' || n.status === statusFilter.value
    return matchesSearch && matchesType && matchesStatus
  })
)

// Render in pages of 50 so a long log stays fast.
const shownCount = ref(50)
const pagedNotifications = computed(() => filteredNotifications.value.slice(0, shownCount.value))

/* -------------------------------- Summary ---------------------------------- */
const summary = computed(() => {
  const all = notifications.value
  const sentToday = all.filter(n => isSameDay(n.createdAt)).length
  const reminders = all.filter(n => n.type === 'Vaccination Reminder').length
  const unread = all.filter(n => !n.opened).length
  const missed = all.filter(n => n.type === 'Missed Vaccination').length
  const manual = all.filter(n => n.type === 'Announcement').length
  const readRate = all.length ? Math.round((all.filter(n => n.opened).length / all.length) * 100) : 0
  return { sentToday, reminders, unread, missed, manual, readRate }
})

/* ------------------------------ Row actions menu ---------------------------- */
const { openMenuId, menuStyle, toggleMenu, closeMenu } = useFloatingMenu()

const resend = async (n) => {
  closeMenu()
  try {
    await axios.post(`${API_BASE}/Notifications/${n.id}/resend`)
    flash(`Resent "${n.title}" to ${n.recipient}.`)
    await fetchNotifications()
  } catch (e) {
    flash(e.response?.data?.message || 'Could not resend this notification.')
  }
}

const deleteNotification = async (n) => {
  closeMenu()
  const ok = await askConfirm({
    title: 'Delete this notification?',
    message: `"${n.title}" for ${n.recipient}. The recipient will no longer see it.`,
    confirmText: 'Delete',
    tone: 'danger',
  })
  if (!ok) return
  try {
    await axios.delete(`${API_BASE}/Notifications/${n.id}`)
    notifications.value = notifications.value.filter(x => x.id !== n.id)
    if (selectedNotification.value?.id === n.id) closeDrawer()
    flash('Notification deleted.')
  } catch (e) {
    flash(e.response?.data?.message || 'Could not delete this notification.')
  }
}

/* -------------------------------- Details drawer ---------------------------- */
const showDrawer = ref(false)
const selectedNotification = ref(null)
const openDrawer = (n) => {
  selectedNotification.value = n
  showDrawer.value = true
  closeMenu()
}
const closeDrawer = () => (showDrawer.value = false)

/* --------------------------------- Create announcement modal --------------------------------- */
// POST /api/Notifications/broadcast — one in-app notification per parent,
// plus email / SMS when ticked (and set up on the server).
const showAnnounceModal = ref(false)
const sending = ref(false)
const announceError = ref('')
const parents = ref([])
const parentSearch = ref('')
const announceForm = reactive({
  title: '', message: '', recipients: 'All Parents', sendEmail: true, sendSms: false, selectedParentIds: [],
})

// Which channels the server can actually send through (appsettings.json)
const channels = ref({ inApp: true, email: false, sms: false })
const loadChannels = async () => {
  try {
    channels.value = (await axios.get(`${API_BASE}/Notifications/channels`)).data
  } catch (e) {
    console.error('load channels:', e)
  }
}

const openAnnounceModal = async () => {
  Object.assign(announceForm, { title: '', message: '', recipients: 'All Parents', sendEmail: true, sendSms: false, selectedParentIds: [] })
  announceError.value = ''
  parentSearch.value = ''
  showAnnounceModal.value = true
  loadChannels()
  if (!parents.value.length) {
    try {
      const res = await axios.get(`${API_BASE}/Parents/all`)
      parents.value = res.data.map(p => ({
        id: p.parentID ?? p.ParentID,
        name: `${p.firstName ?? p.FirstName ?? ''} ${p.lastName ?? p.LastName ?? ''}`.trim(),
        email: p.email ?? p.Email,
      }))
    } catch (e) {
      console.error('load parents:', e)
    }
  }
}

const filteredParents = computed(() => {
  const q = parentSearch.value.trim().toLowerCase()
  return parents.value.filter(p => !q || p.name.toLowerCase().includes(q) || (p.email || '').toLowerCase().includes(q)).slice(0, 50)
})

const toggleParent = (id) => {
  const i = announceForm.selectedParentIds.indexOf(id)
  if (i === -1) announceForm.selectedParentIds.push(id)
  else announceForm.selectedParentIds.splice(i, 1)
}

const sendAnnouncement = async () => {
  announceError.value = ''
  if (!announceForm.title.trim() || !announceForm.message.trim()) {
    announceError.value = 'Please enter a title and a message.'
    return
  }
  if (announceForm.recipients === 'Selected Parents' && announceForm.selectedParentIds.length === 0) {
    announceError.value = 'Select at least one parent.'
    return
  }
  sending.value = true
  try {
    const res = await axios.post(`${API_BASE}/Notifications/broadcast`, {
      title: announceForm.title,
      message: announceForm.message,
      parentIDs: announceForm.recipients === 'Selected Parents' ? announceForm.selectedParentIds : null,
      sendEmail: announceForm.sendEmail,
      sendSms: announceForm.sendSms,
    })
    showAnnounceModal.value = false
    const extra = [
      announceForm.sendEmail ? `${res.data.emails} by email` : null,
      announceForm.sendSms ? `${res.data.texts} by SMS` : null,
    ].filter(Boolean).join(', ')
    flash(`Announcement sent to ${res.data.recipients} parent(s)${extra ? ` (${extra})` : ''}.`)
    await fetchNotifications()
  } catch (e) {
    announceError.value = e.response?.data?.message || 'Could not send the announcement.'
  } finally {
    sending.value = false
  }
}

/* --------------------------------- Reminder schedule (read-only) --------------------------------- */
// These are the thresholds the backend's NotificationGeneratorService
// actually uses when it runs each morning.
const showSettingsModal = ref(false)
const reminderSchedule = {
  before: ['14 days before', '7 days before', '5 days before', '3 days before', '1 day before'],
  after: ['1 day after (missed)', '5 days after', '14 days after', '30 days after (urgent)'],
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900" @click="closeMenu">
    <!-- ============================ SIDEBAR ============================ -->
    <AppSidebar/>

    <!-- ============================ MAIN ============================ -->
    <div class="flex-1 min-w-0 flex flex-col">
      <!-- Top navbar -->
      <AppHeader
  title="Notification Logs"
  breadcrumb="Admin / Notification Logs"
  user-initials="RM"
/>

      <!-- Content -->
      <main class="p-6 space-y-6">
        <div v-if="actionMessage" class="bg-emerald-50 border border-emerald-100 text-emerald-800 text-sm rounded-xl px-4 py-3">{{ actionMessage }}</div>
        <!-- Summary cards -->
        <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Sent Today</p>
              <div class="bg-emerald-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">📨</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.sentToday }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Vaccine Reminders</p>
              <div class="bg-sky-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">🗓️</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.reminders }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Unread</p>
              <div class="bg-amber-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">⏳</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.unread }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Missed-Dose Follow-ups</p>
              <div class="bg-rose-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">⚠️</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.missed }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Announcements</p>
              <div class="bg-teal-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">📢</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.manual }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Read Rate</p>
              <div class="bg-violet-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">📈</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.readRate }}%</p>
          </div>
        </section>

        <!-- Toolbar -->
        <section class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="flex flex-col lg:flex-row lg:items-center gap-3">
            <div class="relative flex-1 min-w-0">
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-sm" aria-hidden="true">🔍</span>
              <input
                v-model="searchQuery"
                type="text"
                placeholder="Search by parent name, child name, or notification title..."
                class="w-full pl-9 pr-3 py-2 text-sm rounded-lg border border-slate-200 bg-slate-50 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
              />
            </div>

            <select
              v-model="typeFilter"
              class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option value="All">All Types</option>
              <option v-for="t in typeOptions" :key="t">{{ t }}</option>
            </select>

            <select
              v-model="statusFilter"
              class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option value="All">All Status</option>
              <option>Delivered</option>
              <option>Read</option>
            </select>

            <div class="flex items-center gap-2 shrink-0 flex-wrap">
              <button @click="showSettingsModal = true" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
                Reminder Schedule
              </button>
              <button @click="exportLogs" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
                Export Logs
              </button>
              <button
                @click="openAnnounceModal"
                class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                + Create Announcement
              </button>
            </div>
          </div>
        </section>

        <!-- Notification table -->
        <section class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-200 bg-slate-50/60">
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Notification Title</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Recipient</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Type</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Scheduled Date</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Sent Date</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Status</th>
                  <th class="text-right font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="n in pagedNotifications"
                  :key="n.id"
                  class="border-b border-slate-100 last:border-0 hover:bg-slate-50 transition-colors"
                >
                  <td class="px-5 py-3">
                    <p class="font-semibold text-slate-900 whitespace-nowrap">{{ n.title }}</p>
                    <p class="text-xs text-slate-400 whitespace-nowrap">{{ n.child !== '—' ? n.child : 'Broadcast' }}</p>
                  </td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ n.recipient }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ n.type }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ n.scheduledDate }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ n.sentDate }}</td>
                  <td class="px-3 py-3">
                    <span :class="[(statusMeta[n.status] || statusMeta.Delivered).tint, (statusMeta[n.status] || statusMeta.Delivered).text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap">
                      <span :class="(statusMeta[n.status] || statusMeta.Delivered).dot" class="w-1.5 h-1.5 rounded-full"></span>
                      {{ n.status }}
                    </span>
                  </td>
                  <td class="px-5 py-3 text-right relative">
                    <button
                      @click.stop="toggleMenu(n.id, $event)"
                      class="text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg w-8 h-8 inline-flex items-center justify-center transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                    >
                      ⋮
                    </button>

                    <Teleport to="body">
                    <div
                      v-if="openMenuId === n.id"
                      @click.stop
                      :style="menuStyle" class="fixed z-50 w-52 bg-white border border-slate-200 rounded-lg shadow-md py-1 text-left"
                    >
                      <button @click="openDrawer(n)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors">View Notification</button>
                      <button @click="resend(n)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors">Resend</button>
                      <div class="my-1 border-t border-slate-100"></div>
                      <button @click="deleteNotification(n)" class="w-full text-left px-3.5 py-2 text-sm text-rose-600 hover:bg-rose-50 transition-colors">Delete</button>
                    </div>
                    </Teleport>
                  </td>
                </tr>

                <tr v-if="filteredNotifications.length > pagedNotifications.length">
                  <td colspan="7" class="px-5 py-3 text-center">
                    <button @click="shownCount += 50" class="text-xs font-semibold text-emerald-700 hover:text-emerald-800">
                      Show more ({{ filteredNotifications.length - pagedNotifications.length }} remaining)
                    </button>
                  </td>
                </tr>

                <tr v-if="filteredNotifications.length === 0">
                  <td colspan="7" class="px-5 py-12 text-center text-sm" :class="loadError ? 'text-rose-500' : 'text-slate-400'">{{ loading ? 'Loading notifications...' : (loadError || 'No notifications match your search or filters.') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Notification History -->
        <section class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <div class="px-5 py-4 border-b border-slate-200">
            <h2 class="text-sm font-bold text-slate-900">Notification History</h2>
            <p class="text-xs text-slate-500 mt-0.5">The 20 most recent notifications across all types.</p>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-200 bg-slate-50/60">
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Recipient</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Title</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Date</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Status</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Opened</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="n in notifications.slice(0, 20)"
                  :key="'h-' + n.id"
                  class="border-b border-slate-100 last:border-0 hover:bg-slate-50 transition-colors"
                >
                  <td class="px-5 py-3 text-slate-700 whitespace-nowrap">{{ n.recipient }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ n.title }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ n.sentDate !== '—' ? n.sentDate : n.scheduledDate }}</td>
                  <td class="px-3 py-3">
                    <span :class="[(statusMeta[n.status] || statusMeta.Delivered).tint, (statusMeta[n.status] || statusMeta.Delivered).text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap">
                      <span :class="(statusMeta[n.status] || statusMeta.Delivered).dot" class="w-1.5 h-1.5 rounded-full"></span>
                      {{ n.status }}
                    </span>
                  </td>
                  <td class="px-5 py-3">
                    <span :class="n.opened ? 'text-emerald-700' : 'text-slate-400'" class="text-xs font-semibold">{{ n.opened ? 'Yes' : 'No' }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </main>
    </div>

    <!-- ============================ NOTIFICATION DETAILS DRAWER ============================ -->
    <transition name="fade">
      <div v-if="showDrawer" class="fixed inset-0 bg-slate-900/30 z-40" @click="closeDrawer"></div>
    </transition>
    <transition name="slide">
      <aside v-if="showDrawer" class="fixed top-0 right-0 h-screen w-full max-w-sm bg-white border-l border-slate-200 shadow-lg z-50 flex flex-col">
        <div class="h-[70px] flex items-center justify-between px-5 border-b border-slate-200 shrink-0">
          <h2 class="text-sm font-bold text-slate-900">Notification Details</h2>
          <button @click="closeDrawer" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
        </div>

        <div v-if="selectedNotification" class="flex-1 overflow-y-auto p-6 space-y-6">
          <div>
            <span :class="[(statusMeta[selectedNotification.status] || statusMeta.Delivered).tint, (statusMeta[selectedNotification.status] || statusMeta.Delivered).text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full mb-3">
              <span :class="(statusMeta[selectedNotification.status] || statusMeta.Delivered).dot" class="w-1.5 h-1.5 rounded-full"></span>
              {{ selectedNotification.status }}
            </span>
            <p class="text-base font-bold text-slate-900">{{ selectedNotification.title }}</p>
          </div>

          <div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-2">Message Content</h3>
            <p class="text-sm text-slate-700 leading-relaxed bg-slate-50 rounded-lg p-4 whitespace-pre-line">{{ selectedNotification.message }}</p>
          </div>

          <div class="bg-slate-50 rounded-lg divide-y divide-slate-200">
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Recipient</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedNotification.recipient }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Child</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedNotification.child }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Notification Type</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedNotification.type }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Scheduled Date</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedNotification.scheduledDate }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Sent Date</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedNotification.sentDate }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Delivery Method</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedNotification.deliveryMethod }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Opened</span>
              <span :class="selectedNotification.opened ? 'text-emerald-700' : 'text-slate-400'" class="text-sm font-semibold">{{ selectedNotification.opened ? 'Yes' : 'No' }}</span>
            </div>
          </div>
        </div>

        <div class="border-t border-slate-200 p-4 flex items-center gap-2 shrink-0">
          <button @click="resend(selectedNotification)" class="flex-1 text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors">Resend</button>
          <button @click="closeDrawer" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Close</button>
        </div>
      </aside>
    </transition>

    <!-- ============================ CREATE ANNOUNCEMENT MODAL ============================ -->
    <transition name="fade">
      <div v-if="showAnnounceModal" class="fixed inset-0 bg-slate-900/40 z-40 flex items-center justify-center p-4" @click.self="showAnnounceModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-2xl max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Create Announcement</h2>
            <button @click="showAnnounceModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
          </div>

          <div class="p-6 space-y-4">
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Title</label>
              <input v-model="announceForm.title" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Message</label>
              <textarea v-model="announceForm.message" rows="4" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors resize-none"></textarea>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label class="block text-xs font-semibold text-slate-500 mb-1.5">Recipients</label>
                <select v-model="announceForm.recipients" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors">
                  <option>All Parents</option>
                  <option>Selected Parents</option>
                </select>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-500 mb-1.5">Send by</label>
                <div class="space-y-1.5 text-sm text-slate-700">
                  <label class="flex items-center gap-2 opacity-70">
                    <input type="checkbox" checked disabled class="w-4 h-4 rounded border-slate-300 text-emerald-600" /> In-app (always)
                  </label>
                  <label class="flex items-center gap-2 cursor-pointer">
                    <input v-model="announceForm.sendEmail" type="checkbox" class="w-4 h-4 rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" /> Email
                    <span v-if="!channels.email" class="text-[10px] text-amber-600">(not set up yet)</span>
                  </label>
                  <label class="flex items-center gap-2 cursor-pointer">
                    <input v-model="announceForm.sendSms" type="checkbox" class="w-4 h-4 rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" /> SMS (text)
                    <span v-if="!channels.sms" class="text-[10px] text-amber-600">(not set up yet)</span>
                  </label>
                </div>
              </div>
            </div>

            <div v-if="announceForm.recipients === 'Selected Parents'">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">
                Select Parents <span class="font-normal text-slate-400">({{ announceForm.selectedParentIds.length }} selected)</span>
              </label>
              <input v-model="parentSearch" type="text" placeholder="Search by name or email..."
                class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 mb-2 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
              <div class="max-h-48 overflow-y-auto border border-slate-200 rounded-lg divide-y divide-slate-100">
                <label v-for="p in filteredParents" :key="p.id" class="flex items-center gap-3 px-3 py-2 cursor-pointer hover:bg-slate-50">
                  <input type="checkbox" :checked="announceForm.selectedParentIds.includes(p.id)" @change="toggleParent(p.id)"
                    class="w-4 h-4 rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" />
                  <span class="text-sm text-slate-700">{{ p.name }}</span>
                  <span class="text-xs text-slate-400 ml-auto truncate">{{ p.email }}</span>
                </label>
                <p v-if="filteredParents.length === 0" class="px-3 py-3 text-xs text-slate-400">No parents found.</p>
              </div>
            </div>

            <p class="text-xs text-slate-400">
              Sent right away to each parent's portal, and by email / SMS to parents who have an email address / mobile number on file.
              <span v-if="(announceForm.sendEmail && !channels.email) || (announceForm.sendSms && !channels.sms)" class="text-amber-600">
                Email and SMS only go out once they're set up in the backend's appsettings.json (see the setup guide).
              </span>
            </p>
            <p v-if="announceError" class="text-xs text-rose-500">{{ announceError }}</p>
          </div>

          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showAnnounceModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Cancel</button>
            <button @click="sendAnnouncement" :disabled="sending" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50 transition-colors">{{ sending ? 'Sending...' : 'Send' }}</button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ NOTIFICATION SETTINGS MODAL ============================ -->
    <transition name="fade">
      <div v-if="showSettingsModal" class="fixed inset-0 bg-slate-900/40 z-40 flex items-center justify-center p-4" @click.self="showSettingsModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-lg max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Reminder Schedule</h2>
            <button @click="showSettingsModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
          </div>

          <div class="p-6 space-y-6">
            <p class="text-sm text-slate-600">
              Reminders are generated automatically every morning (8:00 AM) for each child's next due dose,
              and follow-ups are sent when a dose is missed. Each dose gets at most one reminder per step.
              They go to every linked parent's notification bell and by email, and by SMS as one text per child per step
              that lists all of that visit's vaccines. Texts stop for the day once the daily limit (TextBee: 50) is reached;
              the most urgent ones ("due tomorrow", "missed yesterday") are sent first.
            </p>
            <div class="flex flex-wrap gap-2 text-xs font-semibold">
              <span class="px-3 py-1.5 rounded-lg bg-emerald-50 text-emerald-700">In-app: on</span>
              <span class="px-3 py-1.5 rounded-lg" :class="channels.email ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'">
                Email: {{ channels.email ? 'on' : 'not set up' }}
              </span>
              <span class="px-3 py-1.5 rounded-lg" :class="channels.sms ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'">
                SMS: {{ channels.sms ? `on (${channels.smsProvider})` : 'not set up' }}
              </span>
            </div>
            <div>
              <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Before the due date</h3>
              <div class="flex flex-wrap gap-2">
                <span v-for="r in reminderSchedule.before" :key="r" class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-sky-50 text-sky-700">{{ r }}</span>
              </div>
            </div>
            <div>
              <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">After a missed dose</h3>
              <div class="flex flex-wrap gap-2">
                <span v-for="r in reminderSchedule.after" :key="r" class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-rose-50 text-rose-700">{{ r }}</span>
              </div>
            </div>
            <div>
              <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Also sent automatically</h3>
              <ul class="text-sm text-slate-600 space-y-1 list-disc pl-5">
                <li>"Vaccine administered" to the parent when a dose is recorded, with the next due date; when the visit is finished, one text lists the vaccines given and the next vaccination date</li>
                <li>"Record updated" to the parent (app, email and SMS) when a child's details are changed, so they can report a mistake</li>
                <li>"Temporarily unavailable" to parents of children due for a vaccine that ran out, and "available again" once it's restocked</li>
                <li>"Health center closed" to every parent when a closed day is added under Operating Hours (by email and SMS too for families with a child due that day, with the new date)</li>
                <li>Low-stock alerts to the Doctor and Nurses when a batch drops below its minimum</li>
                <li>Weekly stock check every Wednesday to the Doctor and Nurses</li>
              </ul>
            </div>
          </div>

          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showSettingsModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors">Close</button>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.slide-enter-active, .slide-leave-active { transition: transform 0.25s ease; }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }

@media (prefers-reduced-motion: reduce) {
  * { transition-duration: 0.01ms !important; }
}
</style>