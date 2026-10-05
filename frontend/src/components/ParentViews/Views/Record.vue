<template>
  <ParentLayout>
    <HeaderNav
      :parent-data="parentData"
      :children="children"
      :unread-count="unreadCount"
      @open-profile="showProfile = true"
      @open-notifications="showNotifications = true"
      @logout="handleLogout"
      @select-child="handleSelectChild"
    />

    <ChildTabs :children="children" :selected-child="selectedChild" @select-child="handleSelectChild" />

    <main>
      <section class="overflow-hidden rounded-3xl border border-slate-100 bg-white shadow-sm">
        <!-- Title + filters -->
        <div class="flex flex-col gap-4 border-b border-slate-100 p-5 sm:p-8 lg:flex-row lg:items-center lg:justify-between">
          <div class="min-w-0">
            <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">Vaccination history</p>
            <h2 class="mt-1 truncate text-2xl font-black text-slate-900">{{ selectedChild?.firstName }} {{ selectedChild?.lastName }}</h2>
            <p class="text-sm text-slate-500">{{ vaccinationHistory.length }} record{{ vaccinationHistory.length !== 1 ? 's' : '' }}</p>
          </div>
          <div class="grid grid-cols-2 gap-1 rounded-2xl bg-slate-50 p-1 sm:flex sm:flex-wrap">
            <button v-for="f in ['All', 'Completed', 'Scheduled', 'Overdue']" :key="f" @click="recordFilter = f"
                    :class="recordFilter === f ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'"
                    class="flex items-center justify-center gap-1.5 rounded-xl px-3 py-2 text-xs font-bold uppercase transition-all">
              {{ f }}
              <span v-if="f !== 'All'" class="h-5 min-w-5 rounded-full px-1.5 text-center text-[11px] font-black leading-5"
                    :class="recordStats[f.toLowerCase()] === 0 ? 'bg-slate-200 text-slate-500'
                          : f === 'Completed' ? 'bg-emerald-500 text-white'
                          : f === 'Scheduled' ? 'bg-blue-600 text-white'
                          : 'bg-red-500 text-white'">{{ recordStats[f.toLowerCase()] }}</span>
            </button>
          </div>
        </div>

        <div v-if="recordsLoading" class="py-16 text-center text-slate-400"><p class="mb-2 animate-pulse text-2xl">💉</p><p class="text-sm font-bold">Loading records…</p></div>
        <div v-else-if="filteredRecords.length === 0" class="py-16 text-center text-slate-400"><p class="mb-2 text-2xl">📋</p><p class="text-sm font-bold">No {{ recordFilter === 'All' ? '' : recordFilter.toLowerCase() + ' ' }}vaccination records found.</p></div>

        <template v-else>
          <!-- Phone: one card per dose -->
          <ul class="divide-y divide-slate-100 sm:hidden">
            <li v-for="(rec, i) in filteredRecords" :key="'m' + i" class="px-5 py-4">
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <p class="font-bold text-slate-900">{{ rec.vaccineName }} · Dose {{ rec.doseNumber }}</p>
                  <p class="mt-0.5 text-sm text-slate-500">{{ recordDate(rec) }}</p>
                </div>
                <span :class="getStatusClass(rec.status)" class="shrink-0 rounded-full px-3 py-1 text-[10px] font-bold uppercase">{{ rec.status }}</span>
              </div>
              <p v-if="rec.dateAdministered" class="mt-2 text-xs text-slate-500">
                <span v-if="rec.injectionSite">{{ rec.injectionSite }} · </span>
                <span v-if="rec.administeredByName">By {{ rec.administeredByName }}</span>
                <span v-if="rec.lotNumber"> · Lot {{ rec.lotNumber }}</span>
              </p>
            </li>
          </ul>

          <!-- Tablet and computer: table -->
          <div class="hidden overflow-x-auto sm:block">
            <table class="w-full border-collapse text-left text-sm">
              <thead><tr class="bg-slate-50 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                <th class="px-6 py-3">Date</th>
                <th class="px-6 py-3">Vaccine</th>
                <th class="px-6 py-3">Dose</th>
                <th class="px-6 py-3">Status</th>
                <th class="px-6 py-3">Site</th>
                <th class="px-6 py-3">Vaccinated by</th>
                <th class="px-6 py-3">Lot #</th>
              </tr></thead>
              <tbody>
                <tr v-for="(rec, i) in filteredRecords" :key="i" class="border-t border-slate-100 transition-colors hover:bg-slate-50">
                  <td class="whitespace-nowrap px-6 py-4 text-slate-500">{{ recordDate(rec) }}</td>
                  <td class="px-6 py-4 font-bold text-slate-900">{{ rec.vaccineName }}</td>
                  <td class="whitespace-nowrap px-6 py-4 text-slate-500">Dose {{ rec.doseNumber }}</td>
                  <td class="px-6 py-4"><span :class="getStatusClass(rec.status)" class="rounded-full px-3 py-1 text-[10px] font-bold uppercase">{{ rec.status }}</span></td>
                  <td class="whitespace-nowrap px-6 py-4 text-slate-600">{{ rec.injectionSite || '—' }}</td>
                  <td class="px-6 py-4 text-slate-500">{{ rec.administeredByName || '—' }}</td>
                  <td class="px-6 py-4 font-mono text-xs text-slate-400">{{ rec.lotNumber || '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>

        <div class="flex flex-col gap-3 border-t border-slate-100 bg-slate-50 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-8 sm:py-5">
          <p class="text-sm text-slate-500">Print or save the full immunization record as a PDF</p>
          <button @click="downloadRecord" :disabled="!selectedChild" class="w-full rounded-xl bg-emerald-600 px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-emerald-700 disabled:opacity-50 sm:w-auto">Download PDF</button>
        </div>
      </section>
    </main>

    <ProfileModal v-if="showProfile" :parent-data="parentData" :children="children" @close="showProfile = false" />
    <NotificationPanel v-if="showNotifications" :parent-data="parentData" @close="showNotifications = false" />
  </ParentLayout>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { format } from 'date-fns'
import HeaderNav from '../Components/Headernav.vue'
import ParentLayout from '../Components/ParentLayout.vue'
import ChildTabs from '../Components/ChildTabs.vue'
import { mapChild } from '../Composables/useParentPortal.js'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'
import { getAccount, logout as authLogout } from '@/utils/auth'
import api from '../Composables/api.js'
import { fetchChildSchedule, buildSchedule } from '../Composables/childSchedule.js'

const router = useRouter()

// ── State ──────────────────────────────────────────────────────────────────
const parentData        = ref(null)
const children           = ref([])
const selectedChild      = ref(null)

// Opens the immunization record and saves it as a PDF file; that page
// also has a Print button for a paper copy.
function downloadRecord() {
  if (!selectedChild.value) return
  window.open(`/print/vaccination-card/${selectedChild.value.childID}?download=1`, '_blank')
}
const showProfile        = ref(false)
const showNotifications  = ref(false)
const unreadCount        = ref(0)

// Records
// completedRecords = raw Completed rows from DB
// vaccinationHistory = completedRecords + computed upcoming/overdue rows
const completedRecords   = ref([])
const vaccinationHistory = ref([])
const recordsLoading     = ref(false)
const recordFilter       = ref('All')
const recordStats        = ref({ completed: 0, scheduled: 0, overdue: 0 })

// ── Vaccination schedule — from the backend timeline, shared with the
//    other parent pages (Composables/childSchedule.js) ─────────────────────
const scheduleTimeline = ref([])

function formatDisplayDate(date) {
  if (!date) return '—'
  try { return format(new Date(date), 'MMM d, yyyy') } catch { return '—' }
}
// A given dose shows its date; a planned one its due date
function recordDate(rec) {
  if (rec.dateAdministered) return formatDisplayDate(new Date(rec.dateAdministered))
  if (rec.scheduledDate) return 'Due ' + formatDisplayDate(new Date(rec.scheduledDate))
  return '—'
}
function getStatusClass(status) {
  if (status === 'Completed') return 'bg-emerald-100 text-emerald-700'
  if (status === 'Overdue')   return 'bg-red-100 text-red-700'
  if (status === 'Scheduled') return 'bg-blue-100 text-blue-700'
  return 'bg-amber-100 text-amber-700'
}

const computedVaccineList = computed(() => buildSchedule(scheduleTimeline.value, completedRecords.value))

const filteredRecords = computed(() => {
  if (recordFilter.value === 'All') return vaccinationHistory.value
  return vaccinationHistory.value.filter(r => r.status === recordFilter.value)
})

// ── Actions ────────────────────────────────────────────────────────────────
function handleLogout() {
  authLogout()
  router.push('/')
}

function handleSelectChild(child) {
  selectedChild.value = child
  localStorage.setItem('selectedParentChild', JSON.stringify(child))
  fetchRecords(child.childID)
}

async function fetchRecords(childId) {
  if (!childId) return
  recordsLoading.value = true
  try {
    const { timeline, records } = await fetchChildSchedule(childId)
    scheduleTimeline.value = timeline
    completedRecords.value = records.filter(r => (r.status ?? 'Completed') === 'Completed')
    await new Promise(r => setTimeout(r, 0))

    const today = new Date()
    today.setHours(0, 0, 0, 0)
    const upcomingRows = computedVaccineList.value
      .filter(v => !v.isCompleted)
      .map(v => ({
        vaccineID:          v.vaccineId,
        vaccineName:        v.name,
        doseNumber:         v.doseNumber,
        dateAdministered:   null,
        scheduledDate:      v.scheduledDate,
        status:             v.scheduledDate < today ? 'Overdue' : 'Scheduled',
        administeredByName: null,
        lotNumber:          null,
      }))

    vaccinationHistory.value = [...completedRecords.value, ...upcomingRows].sort((a, b) => {
      const dA = a.dateAdministered ? new Date(a.dateAdministered) : new Date(a.scheduledDate)
      const dB = b.dateAdministered ? new Date(b.dateAdministered) : new Date(b.scheduledDate)
      return dA - dB
    })

    recordStats.value = {
      completed: completedRecords.value.length,
      scheduled: upcomingRows.filter(r => r.status === 'Scheduled').length,
      overdue:   upcomingRows.filter(r => r.status === 'Overdue').length,
    }
  } catch (err) {
    console.error('fetchRecords error:', err)
    completedRecords.value   = []
    vaccinationHistory.value = []
    recordStats.value = { completed: 0, scheduled: 0, overdue: 0 }
  } finally {
    recordsLoading.value = false
  }
}

async function fetchUnreadCount() {
  if (!parentData.value?.parentID) return
  try {
    const res = await api.get(`/Notifications/parent/${parentData.value.parentID}`)
    unreadCount.value = res.data.filter(n => !n.isRead).length
  } catch {
    unreadCount.value = 0
  }
}

// ── Lifecycle ──────────────────────────────────────────────────────────────
onMounted(async () => {
  const savedAccount = getAccount()
  if (!savedAccount) { router.push('/'); return }

  const rawUser = savedAccount.user ?? savedAccount

  // Backend now returns PascalCase (ParentID, FirstName, LastName, Email, ...).
  // Normalize to the camelCase shape every Parent component expects.
  parentData.value = {
    ...rawUser,
    parentID: rawUser.parentID ?? rawUser.ParentID,
    firstName: rawUser.firstName ?? rawUser.FirstName,
    middleName: rawUser.middleName ?? rawUser.MiddleName,
    lastName: rawUser.lastName ?? rawUser.LastName,
    email: rawUser.email ?? rawUser.Email,
    contactNo: rawUser.contactNo ?? rawUser.ContactNo,
    barangayNo: rawUser.barangayNo ?? rawUser.BarangayNo,
    address: rawUser.address ?? rawUser.Address,
  }

  if (!parentData.value?.parentID) {
    console.error('Invalid parent session:', savedAccount)
    router.push('/')
    return
  }

  try {
    const res = await api.get(`/Parents/dashboard/${parentData.value.parentID}`)
    children.value = (res.data ?? []).map(mapChild)
  } catch (err) {
    console.error('Error fetching parent dashboard:', err)
    children.value = []
  }

  const savedChildRaw = localStorage.getItem('selectedParentChild')
  if (savedChildRaw) {
    try {
      const savedChild = JSON.parse(savedChildRaw)
      selectedChild.value = children.value.find(c => c.childID === savedChild.childID) || children.value[0] || null
    } catch {
      selectedChild.value = children.value[0] || null
    }
  } else {
    selectedChild.value = children.value[0] || null
  }

  if (selectedChild.value) {
    localStorage.setItem('selectedParentChild', JSON.stringify(selectedChild.value))
    await fetchRecords(selectedChild.value.childID)
  }

  await fetchUnreadCount()
})
</script>