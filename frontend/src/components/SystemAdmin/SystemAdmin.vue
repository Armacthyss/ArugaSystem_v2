<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import AppSidebar from './Components/AppSidebar.vue'
import AppHeader from './Components/AppHeader.vue'
import {
  Users, Baby, Syringe, AlertTriangle, UserPlus, Package,
  FileText, KeyRound, ClipboardList, ChevronRight, CircleCheck,
  CalendarClock, LogIn, Bell, Clock,
} from 'lucide-vue-next'
import { API_BASE, isSameDay, relativeTime, summarizeStock } from '@/utils/format'

const router = useRouter()
const activeNav = ref('Dashboard')
function goTo(route) {
  router.push(route)
}

// ─────────────────────────────────────────────────────────────
// DATA — everything on this page comes from the live database.
// Each source is loaded independently so one failing endpoint only
// blanks its own card instead of the whole dashboard.
// ─────────────────────────────────────────────────────────────
const accounts  = ref([])
const children  = ref([])
const records   = ref([])
const inventory = ref([])
const vaccines  = ref([])
const auditLogs = ref([])
const loading   = ref(true)

async function load(url, target) {
  try {
    const res = await axios.get(`${API_BASE}${url}`)
    target.value = res.data
  } catch (e) {
    console.error(`Dashboard: ${url} failed`, e)
  }
}

onMounted(async () => {
  await Promise.all([
    load('/accounts', accounts),
    load('/Children/overview', children),
    load('/VaccinationRecords/all', records),
    load('/VaccineInventory', inventory),
    load('/Vaccines', vaccines),
    load('/AuditLogs/activity', auditLogs),   // clinic work only (full log: Super Admin)
  ])
  loading.value = false
})

const adminName = (() => {
  try { return JSON.parse(localStorage.getItem('account') || '{}').user?.FirstName || 'Admin' } catch { return 'Admin' }
})()

const greeting = computed(() => {
  const h = new Date().getHours()
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening'
})

const completedRecords = computed(() => records.value.filter(r => (r.status ?? 'Completed') === 'Completed'))

const stock = computed(() => summarizeStock(inventory.value, vaccines.value))
const lowStock = computed(() => stock.value.filter(s => s.status !== 'Good'))

const summaryCards = computed(() => {
  const now = new Date()
  const thisMonth = completedRecords.value.filter(r => {
    const d = new Date(r.vaccinationDate)
    return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth()
  }).length
  return [
    { label: 'Total Users', value: accounts.value.length, description: 'Registered system users', icon: Users, tint: 'bg-teal-50', text: 'text-teal-700' },
    { label: 'Total Patients', value: children.value.length, description: 'Registered children', icon: Baby, tint: 'bg-emerald-50', text: 'text-emerald-700' },
    { label: 'Vaccinations This Month', value: thisMonth, description: 'Vaccinations recorded', icon: Syringe, tint: 'bg-sky-50', text: 'text-sky-700' },
    { label: 'Low Stock Vaccines', value: lowStock.value.length, description: 'Vaccines need attention', icon: AlertTriangle, tint: 'bg-amber-50', text: 'text-amber-700' },
  ]
})

const userBreakdown = computed(() => {
  const count = roles => accounts.value.filter(a => roles.includes(a.role)).length
  return [
    { role: 'Parent / Guardian', count: count(['Parent']) },
    { role: 'Staff / Nurse', count: count(['Nurse', 'Staff', 'Admission', 'Healthcare']) },
    { role: 'Admin / Doctor', count: count(['Doctor', 'Administrator']) },
  ]
})
const maxUserCount = computed(() => Math.max(1, ...userBreakdown.value.map(u => u.count)))

// Last 6 calendar months, oldest first
const vaccinationTrend = computed(() => {
  const now = new Date()
  return Array.from({ length: 6 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - 5 + i, 1)
    const count = completedRecords.value.filter(r => {
      const v = new Date(r.vaccinationDate)
      return v.getFullYear() === d.getFullYear() && v.getMonth() === d.getMonth()
    }).length
    return { month: d.toLocaleDateString('en-PH', { month: 'short' }), count }
  })
})
const maxVaccinationCount = computed(() => Math.max(1, ...vaccinationTrend.value.map(v => v.count)))

const statusMeta = {
  Good: { tint: 'bg-emerald-50', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  'Low Stock': { tint: 'bg-amber-50', text: 'text-amber-700', dot: 'bg-amber-500' },
  Critical: { tint: 'bg-red-100', text: 'text-red-800', dot: 'bg-red-700' },
}
const vaccineInventory = computed(() => stock.value)

const moduleIcon = {
  'User Management': UserPlus,
  'Patient Management': Baby,
  Inventory: Package,
  Vaccination: Syringe,
  Authentication: LogIn,
  Notifications: Bell,
  Reports: FileText,
}

const recentActivity = computed(() =>
  auditLogs.value.slice(0, 6).map(l => ({
    icon: moduleIcon[l.module] || CalendarClock,
    description: `${l.action} — ${l.module}`,
    detail: `${l.user} · ${l.affectedRecord !== '—' ? l.affectedRecord : l.description}`,
    time: relativeTime(l.timestamp),
  }))
)

const quickActions = [
  { label: 'Add User', icon: UserPlus, route: '/admin/user-management' },
  { label: 'Add Patient', icon: Baby, route: '/admin/patients' },
  { label: 'Add Vaccine', icon: Syringe, route: '/admin/vaccines' },
  { label: 'Manage Inventory', icon: Package, route: '/admin/inventory' },
  { label: 'View Reports', icon: ClipboardList, route: '/admin/reports' },
]

// Things that need the administrator's attention, computed live.
const notifications = computed(() => {
  const list = []
  if (lowStock.value.length)
    list.push({ icon: AlertTriangle, text: `${lowStock.value.length} vaccine(s) running low: ${lowStock.value.map(s => s.abbreviation || s.name).join(', ')}`, tint: 'text-amber-600' })
  const expiring = stock.value.reduce((s, v) => s + v.expiringSoon, 0)
  if (expiring)
    list.push({ icon: Clock, text: `${expiring} batch(es) expire within 30 days`, tint: 'text-amber-600' })
  const mustChange = accounts.value.filter(a => a.mustChangePassword).length
  if (mustChange)
    list.push({ icon: KeyRound, text: `${mustChange} account(s) still need to set their own password`, tint: 'text-sky-600' })
  const delayed = children.value.filter(c => c.vaccinationStatus === 'Delayed').length
  if (delayed)
    list.push({ icon: CalendarClock, text: `${delayed} child(ren) have overdue vaccinations`, tint: 'text-rose-600' })
  if (!list.length)
    list.push({ icon: CircleCheck, text: 'Everything looks good — nothing needs attention', tint: 'text-emerald-600' })
  return list
})

const todaysActivity = computed(() => [
  { label: 'New Users', value: accounts.value.filter(a => isSameDay(a.createdAt)).length },
  { label: 'New Patients', value: children.value.filter(c => isSameDay(c.createdAt)).length },
  { label: 'Vaccinations Recorded', value: completedRecords.value.filter(r => isSameDay(r.vaccinationDate)).length },
  { label: 'Inventory Updates', value: auditLogs.value.filter(l => l.module === 'Inventory' && isSameDay(l.timestamp)).length },
])
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900">
    <AppSidebar />

    <div class="flex-1 min-w-0 flex flex-col">
      <AppHeader title="Dashboard" breadcrumb="Admin / Dashboard" />

      <main class="p-6 space-y-6">
        <!-- Welcome / system status -->
        <section class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h1 class="text-xl font-bold text-slate-900">{{ greeting }}, {{ adminName }}</h1>
            <p class="text-sm text-slate-500">Here's an overview of the Aruga system today.</p>
          </div>
          <div class="flex items-center gap-2 text-xs font-medium text-emerald-700 bg-emerald-50 border border-emerald-100 rounded-full px-3 py-1.5 w-fit">
            <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
            System Operational
          </div>
        </section>

        <!-- Overview cards -->
        <section class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
          <div v-for="card in summaryCards" :key="card.label" class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ card.label }}</p>
              <div :class="card.tint" class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0">
                <component :is="card.icon" :class="card.text" class="w-4 h-4" />
              </div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ card.value }}</p>
            <p class="mt-1 text-xs text-slate-500">{{ card.description }}</p>
          </div>
        </section>

        <section class="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
          <!-- Vaccination activity -->
          <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-1">Vaccination Activity</h2>
            <p class="text-xs text-slate-500 mb-5">Vaccinations recorded over the past 6 months</p>
            <div class="flex items-end gap-4 h-48">
              <div v-for="point in vaccinationTrend" :key="point.month" class="flex-1 flex flex-col items-center gap-2 h-full justify-end">
                <span class="text-xs font-semibold text-slate-600">{{ point.count }}</span>
                <div
                  class="w-full max-w-10 bg-emerald-500 rounded-t-md"
                  :style="{ height: (point.count / maxVaccinationCount) * 100 + '%' }"
                ></div>
                <span class="text-xs text-slate-400">{{ point.month }}</span>
              </div>
            </div>
          </div>

          <!-- User overview -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-4">User Overview</h2>
            <div class="space-y-4">
              <div v-for="entry in userBreakdown" :key="entry.role">
                <div class="flex items-center justify-between text-xs mb-1.5">
                  <span class="font-medium text-slate-600">{{ entry.role }}</span>
                  <span class="font-semibold text-slate-900">{{ entry.count }}</span>
                </div>
                <div class="w-full h-2 rounded-full bg-slate-100">
                  <div class="h-2 rounded-full bg-emerald-500" :style="{ width: (entry.count / maxUserCount) * 100 + '%' }"></div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section class="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
          <!-- Vaccine inventory -->
          <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
            <div class="flex items-center justify-between px-5 py-4 border-b border-slate-200">
              <h2 class="text-sm font-bold text-slate-900">Vaccine Inventory</h2>
              <button @click="goTo('/admin/inventory')" class="text-xs font-semibold text-emerald-700 hover:text-emerald-800 flex items-center gap-1">
                View Inventory
                <ChevronRight class="w-3.5 h-3.5" />
              </button>
            </div>
            <div class="max-h-72 overflow-y-auto divide-y divide-slate-100">
              <div v-for="vaccine in vaccineInventory" :key="vaccine.name" class="flex items-center justify-between px-5 py-3">
                <span class="text-sm font-medium text-slate-900">{{ vaccine.name }}</span>
                <div class="flex items-center gap-4">
                  <span class="text-xs text-slate-500 whitespace-nowrap">{{ vaccine.stock }} doses</span>
                  <span :class="[statusMeta[vaccine.status].tint, statusMeta[vaccine.status].text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap">
                    <span :class="statusMeta[vaccine.status].dot" class="w-1.5 h-1.5 rounded-full"></span>
                    {{ vaccine.status }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Notifications -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-4">Notifications</h2>
            <div class="space-y-3">
              <div v-for="(note, idx) in notifications" :key="idx" class="flex items-start gap-2.5">
                <component :is="note.icon" :class="note.tint" class="w-4 h-4 mt-0.5 shrink-0" />
                <p class="text-sm text-slate-700">{{ note.text }}</p>
              </div>
            </div>
            <button @click="goTo('/admin/notifications')" class="mt-4 w-full text-xs font-semibold text-center px-3 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">
              View All Notifications
            </button>
          </div>
        </section>

        <section class="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
          <!-- Recent system activity -->
          <div class="lg:col-span-2 bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-4">Recent System Activity</h2>
            <div class="space-y-4">
              <div v-for="(activity, idx) in recentActivity" :key="idx" class="flex items-start gap-3">
                <div class="w-8 h-8 rounded-lg bg-slate-50 flex items-center justify-center shrink-0">
                  <component :is="activity.icon" class="w-4 h-4 text-slate-500" />
                </div>
                <div class="min-w-0">
                  <p class="text-sm font-medium text-slate-900">{{ activity.description }}</p>
                  <p class="text-xs text-slate-500">{{ activity.detail }} · {{ activity.time }}</p>
                </div>
              </div>
            </div>
          </div>

          <!-- Quick actions -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-4">Quick Actions</h2>
            <div class="grid grid-cols-1 gap-2">
              <button
                v-for="action in quickActions"
                :key="action.label"
                @click="goTo(action.route)"
                class="flex items-center gap-2.5 rounded-lg border border-slate-200 px-3.5 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 transition-colors"
              >
                <component :is="action.icon" class="w-4 h-4 text-emerald-600 shrink-0" />
                {{ action.label }}
              </button>
            </div>
          </div>
        </section>

        <!-- Today's activity summary -->
        <section class="bg-white border border-slate-200 rounded-xl shadow-sm px-5 py-4">
          <h2 class="text-xs font-semibold uppercase tracking-wide text-slate-500 mb-3">Today's Activity</h2>
          <div class="flex flex-wrap gap-x-8 gap-y-2">
            <div v-for="item in todaysActivity" :key="item.label" class="flex items-center gap-2">
              <span class="text-xs text-slate-500">{{ item.label }}</span>
              <span class="text-sm font-bold text-slate-900">{{ item.value }}</span>
            </div>
          </div>
        </section>
      </main>
    </div>
  </div>
</template>