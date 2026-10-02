<!--
  SuperAdminHome.vue — the Super Admin's dashboard ("System Status").
  The Super Admin is the development team (technical adviser, Oct 2026):
  they watch over the system and the Admin / Doctor accounts, not patients.
  Data: GET /api/SystemStatus (counts only) and GET /api/AuditLogs.
-->
<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900">
    <AppSidebar />

    <div class="flex-1 min-w-0 flex flex-col">
      <AppHeader title="System Status" breadcrumb="Super Admin / System Status" />

      <main class="p-6 space-y-6">
        <section class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h1 class="text-xl font-bold text-slate-900">{{ greeting }}, {{ firstName }}</h1>
            <p class="text-sm text-slate-500">The Aruga system at a glance. Patient records are only for the clinic's Doctors and Nurses.</p>
          </div>
          <button @click="load" :disabled="loading" class="flex items-center gap-2 w-fit rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-50">
            <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': loading }" /> Refresh
          </button>
        </section>

        <p v-if="error" class="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{{ error }}</p>

        <!-- Accounts by level -->
        <section class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
          <div v-for="a in status?.accounts || []" :key="a.level" class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ a.level }}</p>
              <div class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0" :class="levelLook[a.level]?.bg">
                <component :is="levelLook[a.level]?.icon" class="w-4 h-4" :class="levelLook[a.level]?.text" />
              </div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ a.total }}</p>
            <p class="mt-1 text-xs text-slate-500">{{ a.active }} active<span v-if="a.total - a.active"> · {{ a.total - a.active }} inactive</span></p>
          </div>
        </section>

        <section class="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
          <!-- Security today -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-4">Sign-ins Today</h2>
            <dl class="space-y-3 text-sm">
              <div class="flex items-center justify-between"><dt class="text-slate-500">Successful sign-ins</dt><dd class="font-bold text-slate-900">{{ status?.signInsToday ?? '—' }}</dd></div>
              <div class="flex items-center justify-between"><dt class="text-slate-500">Failed sign-ins</dt><dd class="font-bold" :class="status?.failedSignInsToday ? 'text-amber-600' : 'text-slate-900'">{{ status?.failedSignInsToday ?? '—' }}</dd></div>
              <div class="flex items-center justify-between"><dt class="text-slate-500">Locked accounts (5 wrong passwords)</dt><dd class="font-bold" :class="status?.lockedAccounts ? 'text-rose-600' : 'text-slate-900'">{{ status?.lockedAccounts ?? '—' }}</dd></div>
              <div class="flex items-center justify-between"><dt class="text-slate-500">Inactive accounts</dt><dd class="font-bold text-slate-900">{{ status?.inactiveAccounts ?? '—' }}</dd></div>
            </dl>
            <RouterLink to="/super-admin/accounts" class="mt-4 inline-flex items-center gap-1 text-xs font-semibold text-emerald-700 hover:underline">Manage Admin accounts <ArrowRight class="w-3.5 h-3.5" /></RouterLink>
          </div>

          <!-- Services -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-4">Services</h2>
            <ul class="space-y-3 text-sm">
              <li v-for="s in services" :key="s.label" class="flex items-start gap-3">
                <span class="mt-1 h-2.5 w-2.5 shrink-0 rounded-full" :class="s.on ? 'bg-emerald-500' : 'bg-slate-300'"></span>
                <div class="min-w-0">
                  <p class="font-semibold text-slate-800">{{ s.label }} <span class="font-normal text-slate-500">· {{ s.on ? 'On' : 'Off' }}</span></p>
                  <p class="text-xs text-slate-500">{{ s.detail }}</p>
                </div>
              </li>
            </ul>
          </div>

          <!-- Recent security events -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-5">
            <h2 class="text-sm font-bold text-slate-900 mb-1">Recent Account & Sign-in Events</h2>
            <p class="text-xs text-slate-500 mb-4">Failed sign-ins and account changes, last 7 days</p>
            <ul class="space-y-3">
              <li v-for="e in events" :key="e.id" class="flex items-start gap-3">
                <span class="mt-1.5 h-2 w-2 shrink-0 rounded-full" :class="e.status === 'Success' ? 'bg-sky-400' : 'bg-amber-500'"></span>
                <div class="min-w-0">
                  <p class="text-[13px] font-medium text-slate-800 truncate">{{ e.action }} · {{ e.affectedRecord }}</p>
                  <p class="text-xs text-slate-500 truncate">{{ e.user }} ({{ e.role }}) · {{ relativeTime(e.timestamp) }}</p>
                </div>
              </li>
              <li v-if="!events.length" class="text-xs text-slate-400">Nothing in the last 7 days.</li>
            </ul>
            <RouterLink to="/super-admin/audit-logs" class="mt-4 inline-flex items-center gap-1 text-xs font-semibold text-emerald-700 hover:underline">Open the audit logs <ArrowRight class="w-3.5 h-3.5" /></RouterLink>
          </div>
        </section>

        <!-- Send a test email / text to check Gmail and TextBee on this server -->
        <section class="bg-white border border-slate-200 rounded-xl shadow-sm p-5">
          <h2 class="text-sm font-bold text-slate-900 mb-1 flex items-center gap-2"><Send class="w-4 h-4 text-emerald-600" /> Send a test message</h2>
          <p class="text-xs text-slate-500 mb-4">Sends one email and/or one text right now, so you can check that notifications work on this server. Use your own address and number.</p>
          <form class="flex flex-col md:flex-row md:items-end gap-3" @submit.prevent="sendTest">
            <label class="flex-1 text-xs font-medium text-slate-600">
              Email address
              <input v-model="testEmail" type="email" placeholder="you@gmail.com" class="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </label>
            <label class="flex-1 text-xs font-medium text-slate-600">
              Mobile number
              <input v-model="testPhone" type="tel" placeholder="0917 123 4567" class="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </label>
            <button type="submit" :disabled="testing || (!testEmail.trim() && !testPhone.trim())" class="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed">
              {{ testing ? 'Sending…' : 'Send test' }}
            </button>
          </form>
          <ul v-if="testResults.length || testError" class="mt-4 space-y-2 text-sm">
            <li v-if="testError" class="rounded-lg bg-rose-50 border border-rose-100 px-3 py-2 text-rose-700">{{ testError }}</li>
            <li v-for="r in testResults" :key="r.label" class="rounded-lg border px-3 py-2"
              :class="r.sent ? 'bg-emerald-50 border-emerald-100 text-emerald-800' : 'bg-rose-50 border-rose-100 text-rose-700'">
              <span class="font-semibold">{{ r.label }}:</span> {{ r.sent ? 'sent. Check your inbox / phone.' : r.error }}
            </li>
          </ul>
        </section>

        <!-- What this level can do -->
        <section class="rounded-xl border border-emerald-100 bg-emerald-50/60 p-5 text-sm text-emerald-900">
          <p class="font-semibold mb-1 flex items-center gap-2"><ShieldCheck class="w-4 h-4" /> Super Admin access</p>
          <p class="text-emerald-800">
            You manage the system: the Admin / Doctor accounts and the audit logs. Patient records, vaccinations,
            the queue and clinic settings stay with the clinic's Doctors and Nurses (Data Privacy Act of 2012, Republic Act No. 10173).
          </p>
        </section>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import axios from 'axios'
import { RefreshCw, ArrowRight, ShieldCheck, UserCog, Stethoscope, Users, Baby, Send } from 'lucide-vue-next'
import AppSidebar from '@/components/SystemAdmin/Components/AppSidebar.vue'
import AppHeader from '@/components/SystemAdmin/Components/AppHeader.vue'
import { API_BASE, relativeTime } from '@/utils/format'
import { getUser } from '@/utils/auth'

const status = ref(null)
const logs = ref([])
const loading = ref(false)
const error = ref('')

const firstName = (getUser()?.FirstName || '').split(' ')[0] || 'Super Admin'
const greeting = (() => {
  const h = new Date().getHours()
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening'
})()

const levelLook = {
  'Super Admin': { icon: UserCog, bg: 'bg-slate-100', text: 'text-slate-700' },
  'Admin / Doctor': { icon: Stethoscope, bg: 'bg-violet-50', text: 'text-violet-700' },
  'Staff / Nurse': { icon: Users, bg: 'bg-sky-50', text: 'text-sky-700' },
  'Parent': { icon: Baby, bg: 'bg-emerald-50', text: 'text-emerald-700' },
}

const services = computed(() => {
  const m = status.value?.messaging || {}
  return [
    { label: 'Email', on: !!m.email, detail: m.email ? 'Gmail account set in appsettings.json' : 'No email password in appsettings.json' },
    { label: 'SMS', on: !!m.sms, detail: m.sms ? `${m.smsProvider}: ${m.smsSentToday} of ${m.smsDailyLimit} texts used today` : 'No SMS key in appsettings.json' },
    { label: 'CAPTCHA', on: !!status.value?.captcha, detail: "\"I'm not a robot\" on Sign In and Forgot Password" },
  ]
})

// Failed sign-ins and account changes (not everyday successful sign-ins)
const events = computed(() => logs.value
  .filter(l => l.module === 'User Management' || (l.module === 'Authentication' && l.status !== 'Success'))
  .slice(0, 8))

async function load() {
  loading.value = true
  error.value = ''
  try {
    const weekAgo = new Date(); weekAgo.setDate(weekAgo.getDate() - 7)
    const from = `${weekAgo.getFullYear()}-${String(weekAgo.getMonth() + 1).padStart(2, '0')}-${String(weekAgo.getDate()).padStart(2, '0')}`
    const [s, l] = await Promise.all([
      axios.get(`${API_BASE}/SystemStatus`),
      axios.get(`${API_BASE}/AuditLogs`, { params: { from } }),
    ])
    status.value = s.data
    logs.value = l.data
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not load the system status.'
  } finally {
    loading.value = false
  }
}

onMounted(load)

// ── Send a test message ──────────────────────────────────────
const testEmail = ref('')
const testPhone = ref('')
const testing = ref(false)
const testResults = ref([])
const testError = ref('')

async function sendTest() {
  testing.value = true
  testError.value = ''
  testResults.value = []
  try {
    const { data } = await axios.post(`${API_BASE}/SystemStatus/test-message`, {
      email: testEmail.value.trim() || null,
      phone: testPhone.value.trim() || null,
    })
    if (data.email) testResults.value.push({ label: 'Email', ...data.email })
    if (data.sms) testResults.value.push({ label: 'SMS', ...data.sms })
    load()   // refresh the texts-used-today count
  } catch (e) {
    testError.value = e.response?.data?.message || 'Could not send the test.'
  } finally {
    testing.value = false
  }
}
</script>
