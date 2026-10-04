<template>
  <div class="w-full min-h-screen bg-slate-50 flex justify-center font-sans antialiased text-slate-900">
    <div class="w-full max-w-312.5 px-6 py-6">

      <HeaderNav
        :parent-data="parentData"
        :children="children"
        :unread-count="unreadCount"
        @open-profile="showProfile = true"
        @open-notifications="showNotifications = true"
        @logout="handleLogout"
        @select-child="handleSelectChild"
      />

      <div class="grid grid-cols-12 gap-8">

        <ChildSidebar :children="children" :selected-child="selectedChild" @select-child="handleSelectChild" />

        <main class="col-span-12 lg:col-span-9">
          <div class="animate-in fade-in slide-in-from-right-4 duration-500">
            <div v-if="!selectedChild" class="bg-white rounded-xl p-12 text-center text-slate-400 border border-slate-100 shadow-sm">
              <p class="text-2xl mb-2">👶</p>
              <p class="font-bold text-sm">Select a child from Family Profiles to view their schedule.</p>
            </div>
            <!-- One calendar with every dose of the child. Hovering a day (computer)
                 or tapping it (phone) shows that day's details in the panel,
                 which sits beside the calendar on a computer and below it on a phone. -->
            <div v-else class="space-y-4">
              <div class="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
                <div class="px-6 pt-6 pb-4 border-b border-slate-100 flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <p class="text-[10px] font-bold text-emerald-600 uppercase tracking-[0.2em]">Vaccination Calendar</p>
                    <p class="text-xs text-slate-500 mt-0.5">
                      {{ selectedChild.firstName }} {{ selectedChild.lastName }} ·
                      <span class="font-bold text-emerald-600">{{ vaccinatedCount }} of {{ computedVaccineList.length }}</span> doses vaccinated
                    </p>
                  </div>
                  <div class="flex items-center gap-2">
                    <button @click="goToToday" class="px-3 h-8 rounded-lg bg-slate-50 hover:bg-slate-100 text-[10px] font-bold text-slate-600 uppercase tracking-wide transition-all">Today</button>
                    <button v-if="nextDose" @click="goToDate(nextDose.scheduledDate)" class="px-3 h-8 rounded-lg bg-blue-50 hover:bg-blue-100 text-[10px] font-bold text-blue-700 uppercase tracking-wide transition-all">Next dose →</button>
                  </div>
                </div>

                <div class="grid grid-cols-1 lg:grid-cols-3">
                  <!-- Calendar -->
                  <div class="lg:col-span-2 p-4 sm:p-6 lg:border-r border-slate-100">
                    <div class="flex items-center justify-between mb-4">
                      <button @click="prevMonth" aria-label="Previous month" class="w-9 h-9 rounded-xl bg-slate-50 hover:bg-slate-100 flex items-center justify-center text-slate-500 font-bold transition-all">‹</button>
                      <h3 class="font-bold text-base text-slate-800">{{ calendarMonthLabel }}</h3>
                      <button @click="nextMonth" aria-label="Next month" class="w-9 h-9 rounded-xl bg-slate-50 hover:bg-slate-100 flex items-center justify-center text-slate-500 font-bold transition-all">›</button>
                    </div>
                    <div class="grid grid-cols-7 gap-1 sm:gap-1.5 mb-1.5">
                      <div v-for="(label, i) in ['Sun','Mon','Tue','Wed','Thu','Fri','Sat']" :key="i" class="text-center text-[9px] font-bold text-slate-400 uppercase py-1">{{ label }}</div>
                    </div>
                    <div class="grid grid-cols-7 gap-1 sm:gap-1.5" @mouseleave="hoveredKey = null">
                      <div v-for="n in calendarOffset" :key="'sp-' + n" class="aspect-square"></div>
                      <button v-for="cell in calendarCells" :key="cell.key" type="button"
                        @click="selectedKey = cell.key"
                        @mouseenter="hoveredKey = cell.key"
                        :aria-label="cell.ariaLabel"
                        :class="[cellClass(cell), cell.key === activeKey && !showMonthSummary ? 'ring-2 ring-offset-2 ring-emerald-600' : '', cell.isToday ? 'outline-2 outline-dashed outline-offset-2 outline-amber-500' : '']"
                        class="relative aspect-square rounded-xl flex flex-col items-center justify-center text-xs sm:text-sm font-bold transition-colors">
                        <span>{{ cell.day }}</span>
                        <span v-if="cell.isToday" class="text-[7px] sm:text-[8px] font-black uppercase leading-none mt-0.5 opacity-80">Today</span>
                        <!-- Dots only when a day mixes statuses (the fill shows the main one) -->
                        <span v-if="cell.types.length > 1" class="flex gap-0.5 mt-0.5">
                          <span v-for="t in cell.types" :key="t" class="w-1.5 h-1.5 rounded-full ring-1 ring-white" :class="STATUS[t].dot"></span>
                        </span>
                        <span v-if="cell.events.length > 1" class="absolute -top-1.5 -right-1.5 min-w-4 h-4 px-1 rounded-full bg-slate-800 text-white text-[9px] leading-4 text-center">{{ cell.events.length }}</span>
                      </button>
                    </div>

                    <!-- Legend -->
                    <div class="mt-5 pt-4 border-t border-slate-100 grid grid-cols-2 sm:grid-cols-3 gap-x-4 gap-y-2">
                      <div v-for="item in legend" :key="item.label" class="flex items-center gap-2">
                        <span class="w-4 h-4 rounded-md shrink-0" :class="item.swatch"></span>
                        <span class="text-[10px] text-slate-600 font-bold">{{ item.label }}</span>
                      </div>
                    </div>
                  </div>

                  <!-- Day details. On a computer the panel is pinned to the
                       calendar's height and scrolls inside, so hovering over
                       days never resizes the card. -->
                  <div class="relative bg-slate-50/60 border-t lg:border-t-0 border-slate-100">
                  <div class="p-5 sm:p-6 lg:absolute lg:inset-0 lg:overflow-y-auto">
                    <template v-if="showMonthSummary">
                      <p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide">This Month</p>
                      <h4 class="text-lg font-black text-slate-800 leading-tight">{{ calendarMonthLabel }}</h4>
                    </template>
                    <template v-else>
                    <p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide">{{ hoveredKey && hoveredKey !== selectedKey ? 'Previewing' : 'Selected Day' }}</p>
                    <h4 class="text-lg font-black text-slate-800 leading-tight">{{ activeDayLabel }}</h4>
                    </template>
                    <p v-if="!showMonthSummary" class="text-[11px] font-bold mt-1" :class="activeCell?.closedReason ? 'text-red-600' : activeCell?.isClinicDay ? 'text-emerald-700' : 'text-slate-400'">
                      {{ activeCell?.closedReason ? 'Health center closed' : activeCell?.isClinicDay ? `Vaccination day${clinic?.hoursText ? ' · ' + clinic.hoursText.split('·').slice(1).join('·').trim() : ''}` : 'No vaccinations on this day' }}
                    </p>

                    <p v-if="activeCell?.events.length" class="text-[11px] text-slate-500 mt-3">
                      For <span class="font-bold text-slate-700">{{ selectedChild.firstName }} {{ selectedChild.lastName }}</span>
                    </p>

                    <div class="mt-2 space-y-2">
                      <div v-for="ev in activeCell?.events || []" :key="ev.type + ev.vax.doseId"
                        class="bg-white rounded-xl border-l-4 border border-slate-100 px-4 py-3" :class="STATUS[ev.type].accent">
                        <div class="flex items-start justify-between gap-2">
                          <div class="min-w-0">
                            <p class="text-xs font-bold text-slate-800 leading-tight">{{ ev.vax.name }}</p>
                            <p class="text-[10px] font-bold text-slate-500">Dose {{ ev.vax.doseNumber }}</p>
                          </div>
                          <span class="shrink-0 px-2 py-0.5 rounded-full text-[8px] font-black uppercase" :class="STATUS[ev.type].pill">{{ STATUS[ev.type].short }}</span>
                        </div>
                        <div class="mt-1.5 text-[10px] text-slate-500 space-y-0.5">
                          <template v-if="ev.type === 'vaccinated'">
                            <p v-if="ev.vax.administeredByName">Vaccinated by <span class="font-bold text-slate-700">{{ ev.vax.administeredByName }}</span></p>
                            <p v-if="ev.vax.injectionSite">Injection site: <span class="font-bold text-slate-700">{{ ev.vax.injectionSite }}</span></p>
                            <p v-if="ev.vax.wasLate" class="text-amber-700 font-bold">Was due {{ formatDisplayDate(ev.vax.originalDueDate) }} ({{ ev.vax.daysLate }} {{ ev.vax.daysLate === 1 ? 'day' : 'days' }} late)</p>
                          </template>
                          <p v-else-if="ev.type === 'late'" class="text-amber-700 font-bold">Missed this date · vaccinated {{ formatDisplayDate(ev.vax.administeredDate) }}</p>
                          <p v-else-if="ev.type === 'overdue'" class="text-red-700 font-bold">Not yet vaccinated. Please come on the next vaccination day{{ clinic?.nextOpenDay ? ` (${clinic.nextOpenDay})` : '' }}.</p>
                          <p v-else>If you miss this day, come on any vaccination day until <span class="font-bold text-slate-700">{{ formatDisplayDate(addDays(ev.vax.scheduledDate, 14)) }}</span>.</p>
                        </div>
                      </div>

                      <div v-if="!monthEvents.length" class="text-xs text-slate-500 bg-white rounded-xl border border-dashed border-slate-300 p-4">
                        <p class="font-bold text-slate-700">No vaccines for {{ selectedChild.firstName }} in {{ calendarMonthLabel }}.</p>
                        <p v-if="nextAfterMonth" class="mt-1">
                          Next dose: <span class="font-bold text-slate-700">{{ nextAfterMonth.vax.name }} · Dose {{ nextAfterMonth.vax.doseNumber }}</span>
                          on {{ formatDisplayDate(nextAfterMonth.date) }}.
                          <button @click="goToDate(nextAfterMonth.date)" class="block mt-2 text-[10px] font-bold text-emerald-700 uppercase tracking-wide hover:underline">Go to {{ format(nextAfterMonth.date, 'MMMM') }} →</button>
                        </p>
                        <p v-else-if="computedVaccineList.length && vaccinatedCount === computedVaccineList.length" class="mt-1 text-emerald-700 font-bold">All doses are complete.</p>
                      </div>
                      <p v-else-if="!activeCell?.events.length" class="text-xs text-slate-400 bg-white rounded-xl border border-dashed border-slate-200 p-4">
                        No vaccine for {{ selectedChild.firstName }} on this day.
                        <span class="block mt-1 text-slate-500">Tap a coloured day to see its vaccine details.</span>
                      </p>
                    </div>

                    <!-- This month at a glance -->
                    <div v-if="monthEvents.length" class="mt-6">
                      <p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-2">This Month</p>
                      <div class="space-y-1.5">
                        <button v-for="ev in monthEvents" :key="'m-' + ev.type + ev.vax.doseId" @click="selectedKey = ev.key"
                          class="w-full flex items-center gap-2 px-3 py-2 rounded-lg bg-white border border-slate-100 hover:border-slate-300 text-left transition-colors">
                          <span class="w-2.5 h-2.5 rounded-full shrink-0" :class="STATUS[ev.type].dot"></span>
                          <span class="text-[11px] font-bold text-slate-700 flex-1 truncate">{{ ev.vax.name }} · Dose {{ ev.vax.doseNumber }}</span>
                          <span class="text-[10px] text-slate-400 shrink-0">{{ format(ev.date, 'MMM d') }}</span>
                        </button>
                      </div>
                    </div>
                  </div>
                  </div>
                </div>
              </div>

              <div class="space-y-2">
                <ClinicHoursNote :clinic="clinic" />
                <div class="bg-red-50 border-l-4 border-red-500 px-4 py-3 rounded-r-lg flex items-center gap-2">
                  <span class="text-[9px] font-bold text-red-700 uppercase">⚠ Stocks may change without notice</span>
                </div>
              </div>
            </div>
          </div>
        </main>
      </div>

      <ProfileModal v-if="showProfile" :parent-data="parentData" :children="children" @close="showProfile = false" />
      <NotificationPanel v-if="showNotifications" :parent-data="parentData" @close="showNotifications = false" />

    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { addDays, format, getDaysInMonth, startOfMonth } from 'date-fns'
import HeaderNav from '../Components/Headernav.vue'
import ChildSidebar from '../Components/Childsidebar.vue'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'
import ClinicHoursNote from '../Components/ClinicHoursNote.vue'
import { getAccount, logout as authLogout } from '@/utils/auth'
import api from '../Composables/api.js'
import { fetchChildSchedule, buildSchedule } from '../Composables/childSchedule.js'

const router = useRouter()

// ── State ──────────────────────────────────────────────────────────────────
const parentData        = ref(null)
const children           = ref([])
const selectedChild      = ref(null)
const showProfile        = ref(false)
const showNotifications  = ref(false)
const completedRecords   = ref([])
const unreadCount        = ref(0)

// ── Vaccination schedule — from the backend timeline, shared with the
//    other parent pages (Composables/childSchedule.js) ─────────────────────
const scheduleTimeline = ref([])

function formatDisplayDate(date) {
  if (!date) return '—'
  try { return format(new Date(date), 'MMM d, yyyy') } catch { return '—' }
}

const computedVaccineList = computed(() => buildSchedule(scheduleTimeline.value, completedRecords.value))

// ── Calendar ───────────────────────────────────────────────────────────────
// Health-center palette: green / yellow / red on white, vaccination days
// outlined in black. Scheduled stays blue (panel request) so it can't be
// mistaken for vaccinated green.
const STATUS = {
  overdue:    { label: 'Overdue',         dot: 'bg-red-500',     cell: 'bg-red-500 text-white hover:bg-red-600',         pill: 'bg-red-100 text-red-700',         accent: 'border-l-red-500' },
  scheduled:  { label: 'Scheduled',       dot: 'bg-blue-600',    cell: 'bg-blue-600 text-white hover:bg-blue-700',       pill: 'bg-blue-100 text-blue-700',       accent: 'border-l-blue-600' },
  vaccinated: { label: 'Vaccinated',      dot: 'bg-emerald-500', cell: 'bg-emerald-500 text-white hover:bg-emerald-600', pill: 'bg-emerald-100 text-emerald-700', accent: 'border-l-emerald-500' },
  late:       { label: 'Missed due date', dot: 'bg-amber-400',   cell: 'bg-amber-100 text-amber-900 border-2 border-amber-400 hover:bg-amber-200', pill: 'bg-amber-100 text-amber-800', accent: 'border-l-amber-400' },
}
for (const s of Object.values(STATUS)) s.short = s.label
const STATUS_ORDER = ['overdue', 'scheduled', 'vaccinated', 'late']

const legend = [
  { label: 'Scheduled',                    swatch: 'bg-blue-600' },
  { label: 'Vaccinated',                   swatch: 'bg-emerald-500' },
  { label: 'Overdue (not yet vaccinated)', swatch: 'bg-red-500' },
  { label: 'Missed due date',              swatch: 'bg-amber-100 border-2 border-amber-400' },
  { label: 'Vaccination day',              swatch: 'bg-white border-2 border-slate-800' },
  { label: 'Today',                        swatch: 'bg-white border-2 border-dashed border-amber-500' },
]

const dayKey = d => format(new Date(d), 'yyyy-MM-dd')
const today = new Date(); today.setHours(0, 0, 0, 0)

const vaccinatedCount = computed(() => computedVaccineList.value.filter(v => v.isCompleted).length)
const nextDose = computed(() => computedVaccineList.value.find(v => !v.isCompleted) ?? null)

// Every calendar mark of the child's schedule: { key, date, type, vax }
const allEvents = computed(() => {
  const events = []
  for (const vax of computedVaccineList.value) {
    if (vax.isCompleted) {
      events.push({ type: 'vaccinated', date: vax.scheduledDate, vax })
      if (vax.wasLate) events.push({ type: 'late', date: vax.originalDueDate, vax })
    } else {
      events.push({ type: vax.scheduledDate < today ? 'overdue' : 'scheduled', date: vax.scheduledDate, vax })
    }
  }
  return events
    .map(e => ({ ...e, key: dayKey(e.date) }))
    .sort((a, b) => a.date - b.date || STATUS_ORDER.indexOf(a.type) - STATUS_ORDER.indexOf(b.type))
})

const eventsByDay = computed(() => {
  const map = new Map()
  for (const e of allEvents.value) {
    if (!map.has(e.key)) map.set(e.key, [])
    map.get(e.key).push(e)
  }
  return map
})

const calYear  = ref(today.getFullYear())
const calMonth = ref(today.getMonth())
const selectedKey = ref(dayKey(today))
const hoveredKey  = ref(null)
// Hovering previews a day on a computer; tapping (or clicking) keeps it.
const activeKey = computed(() => hoveredKey.value ?? selectedKey.value)

const calendarMonthLabel = computed(() => format(new Date(calYear.value, calMonth.value, 1), 'MMMM yyyy'))
const calendarOffset     = computed(() => startOfMonth(new Date(calYear.value, calMonth.value, 1)).getDay())

function describeDay(date) {
  const key = dayKey(date)
  const exception = (clinic.value?.exceptions || []).find(e => e.date === key)
  const events = eventsByDay.value.get(key) || []
  return {
    key,
    date,
    day: date.getDate(),
    events,
    types: STATUS_ORDER.filter(t => events.some(e => e.type === t)),
    isToday: key === dayKey(today),
    isClinicDay: isClinicDay(date),
    closedReason: exception && !exception.isOpen,
  }
}

const calendarCells = computed(() => {
  const days = getDaysInMonth(new Date(calYear.value, calMonth.value, 1))
  return Array.from({ length: days }, (_, i) => {
    const cell = describeDay(new Date(calYear.value, calMonth.value, i + 1))
    const what = cell.events.map(e => `${STATUS[e.type].label}: ${e.vax.name} dose ${e.vax.doseNumber}`).join('; ')
    cell.ariaLabel = `${format(cell.date, 'MMMM d')}${what ? ` — ${what}` : cell.isClinicDay ? ' — vaccination day' : ''}`
    return cell
  })
})

// The day shown in the details panel (it may be in another month after
// "This month" / "Next dose" jumps, so it's described on its own).
const activeCell = computed(() => {
  const [y, m, d] = activeKey.value.split('-').map(Number)
  return describeDay(new Date(y, m - 1, d))
})
const activeDayLabel = computed(() => format(activeCell.value.date, 'EEEE, MMMM d, yyyy'))
// A month with no vaccines shows a month summary until a day is hovered/tapped
const showMonthSummary = computed(() => !monthEvents.value.length && !hoveredKey.value &&
  selectedKey.value === dayKey(new Date(calYear.value, calMonth.value, 1)))

const monthEvents = computed(() =>
  allEvents.value.filter(e => e.date.getFullYear() === calYear.value && e.date.getMonth() === calMonth.value))

function cellClass(cell) {
  const top = cell.types[0]
  if (top) return STATUS[top].cell + ' shadow-sm cursor-pointer'
  if (cell.closedReason) return 'bg-slate-50 text-slate-300 line-through cursor-pointer'
  if (cell.isClinicDay) return 'bg-white text-slate-900 border-2 border-slate-800 hover:bg-slate-50 cursor-pointer'
  return 'text-slate-300 hover:bg-slate-50 cursor-pointer'
}

function goToDate(date) {
  const d = new Date(date)
  calYear.value = d.getFullYear()
  calMonth.value = d.getMonth()
  selectedKey.value = dayKey(d)
}
function goToToday() { goToDate(today) }

// Clinic (vaccination) days come from the admin's Operating Hours: the open
// weekdays, with holidays / special openings taking priority.
const clinic = ref(null)   // GET /ClinicOperatingSchedule/today

function isClinicDay(date) {
  const c = clinic.value
  if (!c) return false
  const key = format(date, 'yyyy-MM-dd')
  const exception = (c.exceptions || []).find(e => e.date === key)
  if (exception) return exception.isOpen
  return (c.openDays || []).includes(date.getDay())
}

async function fetchClinicHours() {
  try {
    clinic.value = (await api.get('/ClinicOperatingSchedule/today')).data
  } catch (err) {
    console.error('Failed to load clinic hours:', err)
  }
}

// Moving to another month opens that month's vaccines right away: the first
// dose still to come (or the first dose of the month), else the 1st, where
// the panel says the month has none and when the next one is.
function showMonth() {
  hoveredKey.value = null
  const first = monthEvents.value.find(e => e.type === 'scheduled' || e.type === 'overdue') ?? monthEvents.value[0]
  selectedKey.value = first ? first.key : dayKey(new Date(calYear.value, calMonth.value, 1))
}
function prevMonth() { calMonth.value === 0 ? (calMonth.value = 11, calYear.value--) : calMonth.value--; showMonth() }
function nextMonth() { calMonth.value === 11 ? (calMonth.value = 0, calYear.value++) : calMonth.value++; showMonth() }

// For a month without vaccines: the next dose after it (if any)
const nextAfterMonth = computed(() => {
  const end = new Date(calYear.value, calMonth.value + 1, 1)
  return allEvents.value.find(e => e.date >= end && (e.type === 'scheduled' || e.type === 'overdue')) ?? null
})

// ── Watchers ───────────────────────────────────────────────────────────────
// When a child's schedule loads, open the calendar on their next dose
// (or today, once every dose is done).
watch(scheduleTimeline, () => {
  goToDate(nextDose.value?.scheduledDate ?? today)
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
  try {
    const { timeline, records } = await fetchChildSchedule(childId)
    completedRecords.value = records
    scheduleTimeline.value = timeline   // last: its watcher reads the finished schedule
  } catch (err) {
    console.error('fetchRecords error:', err)
    completedRecords.value = []
    scheduleTimeline.value = []
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
  fetchClinicHours()

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
    children.value = res.data.map(child => {
      const relationshipType = child.relationshipType ?? child.RelationshipType
      const relatedParentName = child.parentFullName ?? child.ParentFullName

      return {
        childID: child.ChildID ?? child.childID,
        firstName: child.FirstName ?? child.firstName,
        middleName: child.MiddleName ?? child.middleName,
        lastName: child.LastName ?? child.lastName,
        birthDate: child.BirthDate ?? child.birthDate,
        placeOfBirth: child.PlaceOfBirth ?? child.placeOfBirth,
        sex: child.Sex ?? child.sex,
        barangay: child.Barangay ?? child.barangay,
        familyNo: child.FamilyNo ?? child.familyNo,
        address: child.Address ?? child.address,
        healthCenter: child.HealthCenter ?? child.healthCenter,
        relationshipType,
        isPrimaryContact: child.IsPrimaryContact ?? child.isPrimaryContact,
        canReceiveNotifications: child.CanReceiveNotifications ?? child.canReceiveNotifications,
        // Everyone linked to the child: "Maria Santos (Mother); Rosario Santos (Grandmother)"
        guardians: child.Guardians ?? child.guardians,

        // The dashboard endpoint doesn't return MotherName/FatherName/GuardianName
        // fields directly — it only returns relationshipType + parentFullName for
        // the currently logged-in parent, so derive which of the three this is.
        motherName: relationshipType === 'Mother' ? relatedParentName : undefined,
        fatherName: relationshipType === 'Father' ? relatedParentName : undefined,
        guardianName: relationshipType === 'Guardian' ? relatedParentName : undefined,
      }
    })
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