<script setup>

import { API_ORIGIN } from '@/utils/apiBase'
import axios from "axios"
import { ref, computed, onMounted } from "vue"
import AppSidebar from "./Components/AppSidebar.vue"
import AppHeader from "./Components/AppHeader.vue"

const props = defineProps({
  // "Admin" | "Staff" | "Parent" (Parent should never be routed here)
  userRole: { type: String, default: "Admin" },
})

const isAdmin = computed(() => props.userRole === "Admin")

/* ------------------------------- API config ------------------------------- */
const scheduleApi = `${API_ORIGIN}/api/ClinicOperatingSchedule`
const exceptionsApi = `${API_ORIGIN}/api/ClinicScheduleExceptions`

/* --------------------------------- Sidebar --------------------------------- */
const isCollapsed = ref(false)
const toggleSidebar = () => (isCollapsed.value = !isCollapsed.value)

const navItems = [
  { label: "Dashboard", icon: "🏠" },
  { label: "User Management", icon: "👥" },
  { label: "Patient Management", icon: "🧒" },
  { label: "Vaccine Management", icon: "💉" },
  { label: "Operating Hours", icon: "🕒" },
  { label: "Inventory", icon: "📦" },
  { label: "Notifications", icon: "🔔" },
  { label: "Reports", icon: "📊" },
  { label: "Audit Logs", icon: "📋" },
  { label: "Settings", icon: "⚙️" },
]
const activeNav = ref("Operating Hours")

/* --------------------------------- Helpers --------------------------------- */
const dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]
const MONTH_LABELS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
]
const WEEKDAY_LABELS = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"]

function pad2(n) {
  return String(n).padStart(2, "0")
}

// "07:00:00" -> "7:00 AM"  (display only — never mutates the underlying value)
function formatTime(ts) {
  if (!ts) return "—"
  const [hStr, mStr] = ts.split(":")
  const h = Number(hStr)
  const m = Number(mStr)
  const period = h >= 12 ? "PM" : "AM"
  const hour12 = h % 12 === 0 ? 12 : h % 12
  return `${hour12}:${pad2(m)} ${period}`
}

// "07:00:00" -> "07:00" (for <input type="time">)
function toInputTime(ts) {
  if (!ts) return ""
  return ts.slice(0, 5)
}

// "07:00" -> "07:00:00" (back to the backend's TimeSpan-friendly string)
function toApiTime(inputVal) {
  if (!inputVal) return null
  return inputVal.length === 5 ? `${inputVal}:00` : inputVal
}

function toMinutes(inputVal) {
  if (!inputVal) return null
  const [h, m] = inputVal.split(":").map(Number)
  return h * 60 + m
}

// yyyy-mm-dd, built from local Y/M/D so it can't drift a day from timezone conversion
function toISODate(date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
}

// "2026-08-26" -> Date at local midnight (avoids the UTC-parse day-shift bug)
function isoToLocalDate(isoStr) {
  if (!isoStr) return null
  const [y, m, d] = isoStr.split("-").map(Number)
  return new Date(y, m - 1, d)
}

function isSameDate(a, b) {
  return !!a && !!b && a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}

function formatDateLong(isoStr) {
  const d = isoToLocalDate(isoStr)
  if (!d) return "—"
  return d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" })
}

/* -------------------------------- Notifications -------------------------------- */
const notification = ref(null) // { type: 'success' | 'error', message: string }
let notificationTimer = null
function notify(type, message) {
  notification.value = { type, message }
  clearTimeout(notificationTimer)
  notificationTimer = setTimeout(() => (notification.value = null), 5000)
}
function extractApiError(err, fallback) {
  return err?.response?.data?.message || err?.response?.data?.title || fallback
}

/* ================================================================
   1. WEEKLY OPERATING SCHEDULE
   ================================================================ */
const weeklySchedule = ref([])
const weeklyLoading = ref(false)
const weeklyLoadError = ref("")

async function loadWeeklySchedule() {
  weeklyLoading.value = true
  weeklyLoadError.value = ""
  try {
    const res = await axios.get(scheduleApi)
    weeklySchedule.value = [...res.data].sort((a, b) => a.dayOfWeek - b.dayOfWeek)
  } catch (err) {
    weeklyLoadError.value = extractApiError(err, "Unable to load the weekly operating schedule.")
  } finally {
    weeklyLoading.value = false
  }
}

const showDayModal = ref(false)
const dayForm = ref(null)
const dayFormError = ref("")
const savingDay = ref(false)

function openEditDay(day) {
  if (!isAdmin.value) return
  dayForm.value = {
    scheduleID: day.scheduleID,
    dayOfWeek: day.dayOfWeek,
    isOpen: day.isOpen,
    openingTime: toInputTime(day.openingTime) || "07:00",
    closingTime: toInputTime(day.closingTime) || "12:00",
    queueCutoffTime: toInputTime(day.queueCutoffTime) || "10:00",
  }
  dayFormError.value = ""
  showDayModal.value = true
}
function closeDayModal() {
  showDayModal.value = false
  dayForm.value = null
}

function validateDayForm(f) {
  if (!f.isOpen) return ""
  const open = toMinutes(f.openingTime)
  const close = toMinutes(f.closingTime)
  const cutoff = toMinutes(f.queueCutoffTime)
  if (open === null || close === null || cutoff === null) return "All time fields are required when the day is open."
  if (open >= close) return "Opening time must be earlier than closing time."
  if (cutoff < open || cutoff > close) return "Queue cutoff time must fall between the opening and closing times."
  return ""
}

async function saveDay() {
  const f = dayForm.value
  const validationError = validateDayForm(f)
  if (validationError) {
    dayFormError.value = validationError
    return
  }
  savingDay.value = true
  dayFormError.value = ""
  try {
    const payload = {
      scheduleID: f.scheduleID,
      dayOfWeek: f.dayOfWeek,
      isOpen: f.isOpen,
      openingTime: toApiTime(f.openingTime),
      closingTime: toApiTime(f.closingTime),
      queueCutoffTime: toApiTime(f.queueCutoffTime),
      isActive: true,
    }
    await axios.put(`${scheduleApi}/${f.scheduleID}`, payload)
    notify("success", `${dayNames[f.dayOfWeek]}'s schedule was updated.`)
    closeDayModal()
    await loadWeeklySchedule()
  } catch (err) {
    dayFormError.value = extractApiError(err, "Unable to save this day's schedule.")
  } finally {
    savingDay.value = false
  }
}

/* ================================================================
   2. SCHEDULE EXCEPTIONS
   ================================================================ */
const exceptions = ref([])
const exceptionsLoading = ref(false)
const exceptionsLoadError = ref("")

async function loadExceptions() {
  exceptionsLoading.value = true
  exceptionsLoadError.value = ""
  try {
    const res = await axios.get(exceptionsApi)
    exceptions.value = [...res.data].sort((a, b) => new Date(b.exceptionDate) - new Date(a.exceptionDate))
  } catch (err) {
    exceptionsLoadError.value = extractApiError(err, "Unable to load schedule exceptions.")
  } finally {
    exceptionsLoading.value = false
  }
}

const showExceptionModal = ref(false)
const isEditingException = ref(false)
const exceptionForm = ref(null)
const exceptionFormError = ref("")
const savingException = ref(false)

function blankExceptionForm() {
  return {
    exceptionID: 0,
    exceptionDate: "",
    endDate: "", // optional: same exception on every date up to this one (e.g. a typhoon)
    isOpen: false,
    openingTime: "07:00",
    closingTime: "12:00",
    queueCutoffTime: "10:00",
    reason: "",
  }
}

function openAddException() {
  if (!isAdmin.value) return
  isEditingException.value = false
  exceptionForm.value = blankExceptionForm()
  exceptionFormError.value = ""
  showExceptionModal.value = true
}

function openEditException(exception) {
  if (!isAdmin.value) return
  isEditingException.value = true
  exceptionForm.value = {
    exceptionID: exception.exceptionID,
    exceptionDate: exception.exceptionDate.slice(0, 10),
    isOpen: exception.isOpen,
    openingTime: toInputTime(exception.openingTime) || "07:00",
    closingTime: toInputTime(exception.closingTime) || "12:00",
    queueCutoffTime: toInputTime(exception.queueCutoffTime) || "10:00",
    reason: exception.reason || "",
  }
  exceptionFormError.value = ""
  showExceptionModal.value = true
}

function closeExceptionModal() {
  showExceptionModal.value = false
  exceptionForm.value = null
  showCalendar.value = false
}

function validateExceptionForm(f) {
  if (!f.exceptionDate) return "Exception date is required."
  if (f.endDate && f.endDate < f.exceptionDate) return "The last date can't be before the first date."
  if (!f.reason || !f.reason.trim()) return "Reason is required."
  if (f.isOpen) {
    const open = toMinutes(f.openingTime)
    const close = toMinutes(f.closingTime)
    const cutoff = toMinutes(f.queueCutoffTime)
    if (open === null || close === null || cutoff === null) return "All time fields are required when marking the day open."
    if (open >= close) return "Opening time must be earlier than closing time."
    if (cutoff < open || cutoff > close) return "Queue cutoff time must fall between the opening and closing times."
  }
  return ""
}

async function saveException() {
  const f = exceptionForm.value
  const validationError = validateExceptionForm(f)
  if (validationError) {
    exceptionFormError.value = validationError
    return
  }
  savingException.value = true
  exceptionFormError.value = ""
  try {
    const payload = {
      exceptionDate: f.exceptionDate,
      isOpen: f.isOpen,
      openingTime: toApiTime(f.openingTime),
      closingTime: toApiTime(f.closingTime),
      queueCutoffTime: toApiTime(f.queueCutoffTime),
      reason: f.reason.trim(),
      isActive: true,
    }
    // The API says how many doses moved and parents were told (closures only).
    const closureSummary = (data) =>
      data?.parentsNotified
        ? ` ${data.parentsNotified} parent(s) notified${data.dosesMoved ? `; ${data.dosesMoved} dose(s) moved to the next vaccination day` : ""}.`
        : ""
    if (isEditingException.value) {
      const res = await axios.put(`${exceptionsApi}/${f.exceptionID}`, { exceptionID: f.exceptionID, ...payload })
      notify("success", "Schedule exception updated." + closureSummary(res.data))
    } else {
      const res = await axios.post(exceptionsApi, { ...payload, endDate: f.endDate || null })
      const days = res.data?.exceptions?.length || 1
      notify("success", (days > 1 ? `Schedule exception added to ${days} dates.` : "Schedule exception created.") + closureSummary(res.data))
    }
    closeExceptionModal()
    await loadExceptions()
  } catch (err) {
    // Surfaces backend messages verbatim, e.g. duplicate-active-exception errors.
    exceptionFormError.value = extractApiError(err, "Unable to save this schedule exception.")
  } finally {
    savingException.value = false
  }
}

const confirmDeleteTarget = ref(null)
const deletingException = ref(false)

function askDeleteException(exception) {
  if (!isAdmin.value) return
  confirmDeleteTarget.value = exception
}
function cancelDelete() {
  confirmDeleteTarget.value = null
}
async function confirmDeleteException() {
  const target = confirmDeleteTarget.value
  if (!target) return
  deletingException.value = true
  try {
    await axios.delete(`${exceptionsApi}/${target.exceptionID}`)
    notify("success", "Schedule exception removed.")
    confirmDeleteTarget.value = null
    await loadExceptions()
  } catch (err) {
    notify("error", extractApiError(err, "Unable to remove this schedule exception."))
  } finally {
    deletingException.value = false
  }
}

/* ------------------------- Calendar date picker (Exception Date) -------------------------
   A small self-contained month-grid picker so admins click a date instead of typing one.
   No new dependency — just plain Date math, styled to match the rest of the page. */
const showCalendar = ref(false)
const calendarCursor = ref(new Date()) // first-of-month currently displayed
const today = new Date()

function openCalendar() {
  const base = exceptionForm.value.exceptionDate ? isoToLocalDate(exceptionForm.value.exceptionDate) : new Date()
  calendarCursor.value = new Date(base.getFullYear(), base.getMonth(), 1)
  showCalendar.value = true
}
function closeCalendar() {
  showCalendar.value = false
}
function prevMonth() {
  const c = calendarCursor.value
  calendarCursor.value = new Date(c.getFullYear(), c.getMonth() - 1, 1)
}
function nextMonth() {
  const c = calendarCursor.value
  calendarCursor.value = new Date(c.getFullYear(), c.getMonth() + 1, 1)
}
function pickDate(date) {
  exceptionForm.value.exceptionDate = toISODate(date)
  showCalendar.value = false
}

const calendarLabel = computed(() => `${MONTH_LABELS[calendarCursor.value.getMonth()]} ${calendarCursor.value.getFullYear()}`)

const calendarDays = computed(() => {
  const c = calendarCursor.value
  const firstOfMonth = new Date(c.getFullYear(), c.getMonth(), 1)
  const gridStart = new Date(c.getFullYear(), c.getMonth(), 1 - firstOfMonth.getDay())
  const days = []
  for (let i = 0; i < 42; i++) {
    days.push(new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i))
  }
  return days
})

const selectedDateObj = computed(() =>
  exceptionForm.value?.exceptionDate ? isoToLocalDate(exceptionForm.value.exceptionDate) : null
)

/* ------------------------- Calendar Overview -------------------------
   A month-view calendar that marks every date with what actually
   governs it that day, so admins can see the schedule at a glance
   instead of cross-referencing the two tables above:
     - "scheduled"  -> a normal weekly-open day, no exception on file
     - "exception"  -> an active exception exists for that date,
                        colored by whether it opens or closes the clinic
     - today is marked independently of the above (a day can be both)
   Purely a read model built from weeklySchedule + exceptions already
   loaded above — it doesn't call any new endpoints. */
const overviewCursor = ref(new Date(today.getFullYear(), today.getMonth(), 1))
const overviewLabel = computed(() => `${MONTH_LABELS[overviewCursor.value.getMonth()]} ${overviewCursor.value.getFullYear()}`)

function prevOverviewMonth() {
  const c = overviewCursor.value
  overviewCursor.value = new Date(c.getFullYear(), c.getMonth() - 1, 1)
}
function nextOverviewMonth() {
  const c = overviewCursor.value
  overviewCursor.value = new Date(c.getFullYear(), c.getMonth() + 1, 1)
}

const overviewDays = computed(() => {
  const c = overviewCursor.value
  const firstOfMonth = new Date(c.getFullYear(), c.getMonth(), 1)
  const gridStart = new Date(c.getFullYear(), c.getMonth(), 1 - firstOfMonth.getDay())
  const days = []
  for (let i = 0; i < 42; i++) {
    days.push(new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i))
  }
  return days
})

// Active exceptions indexed by ISO date for O(1) lookup while rendering the grid.
const activeExceptionsByDate = computed(() => {
  const map = {}
  for (const ex of exceptions.value) {
    if (ex.isActive) map[ex.exceptionDate.slice(0, 10)] = ex
  }
  return map
})

// What governs a given date: an active exception wins, otherwise the weekly schedule.
function dayStatus(date) {
  const iso = toISODate(date)
  const exception = activeExceptionsByDate.value[iso]
  if (exception) {
    return { kind: "exception", isOpen: exception.isOpen, reason: exception.reason }
  }
  const weekly = weeklySchedule.value.find((d) => d.dayOfWeek === date.getDay())
  if (weekly && weekly.isOpen) {
    return { kind: "scheduled", isOpen: true }
  }
  return { kind: "closed" }
}

function overviewCellTooltip(date) {
  const status = dayStatus(date)
  const label = date.toLocaleDateString(undefined, { weekday: "long", month: "short", day: "numeric" })
  if (status.kind === "exception") {
    return `${label} — Exception (${status.isOpen ? "Open" : "Closed"})${status.reason ? ": " + status.reason : ""}`
  }
  if (status.kind === "scheduled") return `${label} — Vaccination day`
  return `${label} — No vaccinations`
}

// Admin convenience: click a date to add or edit its exception, instead of
// having to find it in the table below. View-only users just see the tooltip.
function onOverviewDayClick(date) {
  if (!isAdmin.value) return
  const iso = toISODate(date)
  const existing = activeExceptionsByDate.value[iso]
  if (existing) {
    openEditException(existing)
  } else {
    openAddException()
    exceptionForm.value.exceptionDate = iso
  }
}

// General clinic hours (backend setting "Clinic:Hours"), shown to parents
// next to the vaccination hours.
const clinicHoursText = ref("")
async function loadClinicHoursText() {
  try {
    clinicHoursText.value = (await axios.get(`${scheduleApi}/today`)).data?.clinicHoursText || ""
  } catch {
    clinicHoursText.value = ""
  }
}

/* --------------------------------- Init --------------------------------- */
onMounted(() => {
  loadWeeklySchedule()
  loadExceptions()
  loadClinicHoursText()
})
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900">
    <!-- ============================ SIDEBAR ============================ -->
    <AppSidebar/>
    <!-- ============================ MAIN ============================ -->
    <div class="flex-1 min-w-0 flex flex-col">
      <!-- Top navbar -->
      <AppHeader
  title="Operating Hours"
  breadcrumb="Admin / Operating Hours"
  user-initials="RM"
/>
      <!-- Page content -->
      <main class="flex-1 p-6 space-y-6">
        <!-- Inline notification -->
        <div
          v-if="notification"
          :class="notification.type === 'success' ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-rose-50 text-rose-700 border-rose-200'"
          class="rounded-lg border px-4 py-3 text-sm font-medium flex items-start justify-between gap-4"
        >
          <span>{{ notification.message }}</span>
          <button class="text-current opacity-60 hover:opacity-100" @click="notification = null" aria-label="Dismiss">✕</button>
        </div>

        <!-- ======================= CALENDAR + WEEKLY SCHEDULE ======================= -->
        <div class="grid grid-cols-1 xl:grid-cols-2 gap-6 items-start">
          <!-- ------------- Calendar Overview ------------- -->
          <section class="bg-white rounded-xl border border-slate-200 shadow-sm">
            <div class="px-6 py-4 border-b border-slate-200 flex items-center justify-between gap-4 flex-wrap">
              <div>
                <h2 class="text-sm font-bold text-slate-900">Calendar Overview</h2>
                <p class="text-xs text-slate-500 mt-0.5">What's in effect each day — weekly schedule and exceptions combined.</p>
              </div>
              <div class="flex items-center gap-1">
                <button
                  type="button"
                  class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-500 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  @click="prevOverviewMonth"
                  aria-label="Previous month"
                >
                  ‹
                </button>
                <span class="text-sm font-semibold text-slate-900 w-32 text-center">{{ overviewLabel }}</span>
                <button
                  type="button"
                  class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-500 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  @click="nextOverviewMonth"
                  aria-label="Next month"
                >
                  ›
                </button>
              </div>
            </div>

            <!-- Legend -->
            <div class="px-6 pt-4 flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-slate-600">
              <span class="inline-flex items-center gap-1.5">
                <span class="w-3.5 h-3.5 rounded bg-sky-200 border border-sky-300"></span> Vaccination Day
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="w-3.5 h-3.5 rounded ring-2 ring-amber-400"></span> Today
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="w-3.5 h-3.5 rounded bg-emerald-600"></span> Exception — Open
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="w-3.5 h-3.5 rounded bg-rose-500"></span> Exception — Closed
              </span>
            </div>

            <div class="px-6 pb-6 pt-3">
              <div class="grid grid-cols-7 gap-1.5 mb-1.5">
                <span v-for="wd in WEEKDAY_LABELS" :key="wd" class="text-[11px] font-semibold text-slate-400 text-center py-1">{{ wd }}</span>
              </div>
              <div class="grid grid-cols-7 gap-1.5">
                <button
                  v-for="d in overviewDays"
                  :key="d.toISOString()"
                  type="button"
                  :title="overviewCellTooltip(d)"
                  :disabled="!isAdmin"
                  @click="onOverviewDayClick(d)"
                  :class="[
                    d.getMonth() !== overviewCursor.getMonth() ? 'opacity-35' : '',
                    isAdmin ? 'cursor-pointer' : 'cursor-default',
                    dayStatus(d).kind === 'exception'
                      ? dayStatus(d).isOpen
                        ? 'bg-emerald-600 text-white hover:bg-emerald-700'
                        : 'bg-rose-500 text-white hover:bg-rose-600'
                      : dayStatus(d).kind === 'scheduled'
                      ? 'bg-sky-200 text-sky-900 border border-sky-300 hover:bg-sky-300'
                      : 'bg-slate-50 text-slate-500 hover:bg-slate-100',
                    isSameDate(d, today) ? 'ring-2 ring-amber-400 ring-offset-1' : '',
                  ]"
                  class="relative h-11 rounded-lg text-xs font-semibold flex items-center justify-center transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 disabled:hover:bg-slate-50"
                >
                  {{ d.getDate() }}
                </button>
              </div>
              <p v-if="isAdmin" class="text-xs text-slate-400 mt-3">Click a date to add or edit its exception.</p>
            </div>
          </section>

          <!-- ------------- Weekly Schedule ------------- -->
          <section class="bg-white rounded-xl border border-slate-200 shadow-sm">
            <div class="px-6 py-4 border-b border-slate-200">
              <h2 class="text-sm font-bold text-slate-900">Weekly Vaccination Schedule</h2>
              <p class="text-xs text-slate-500 mt-0.5">
                The days and hours vaccinations are given. Due dates, parent check-in and reminders follow this schedule.
                <span v-if="clinicHoursText">Parents also see the general clinic hours: {{ clinicHoursText }}.</span>
              </p>
            </div>

            <div v-if="weeklyLoading" class="px-6 py-10 text-center text-sm text-slate-500">Loading weekly schedule…</div>

            <div v-else-if="weeklyLoadError" class="px-6 py-8 text-center">
              <p class="text-sm text-rose-600 font-medium">{{ weeklyLoadError }}</p>
              <button class="mt-3 text-sm font-medium text-emerald-700 hover:underline" @click="loadWeeklySchedule">Try again</button>
            </div>

            <div v-else class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wide border-b border-slate-200">
                    <th class="px-5 py-3">Day</th>
                    <th class="px-3 py-3">Status</th>
                    <th class="px-3 py-3">Opening</th>
                    <th class="px-3 py-3">Closing</th>
                    <th class="px-3 py-3">Cutoff</th>
                    <th v-if="isAdmin" class="px-3 py-3 text-right pr-5">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="day in weeklySchedule" :key="day.scheduleID" class="border-b border-slate-100 last:border-0">
                    <td class="px-5 py-3.5 font-medium text-slate-900 whitespace-nowrap">{{ dayNames[day.dayOfWeek] }}</td>
                    <td class="px-3 py-3.5">
                      <span
                        v-if="day.isOpen"
                        class="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 text-emerald-700 text-xs font-semibold px-2.5 py-1"
                      >
                        <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span> Open
                      </span>
                      <span
                        v-else
                        class="inline-flex items-center gap-1.5 rounded-full bg-slate-100 text-slate-600 text-xs font-semibold px-2.5 py-1"
                      >
                        <span class="w-1.5 h-1.5 rounded-full bg-slate-400"></span> Closed
                      </span>
                    </td>
                    <td class="px-3 py-3.5 text-slate-700 whitespace-nowrap">{{ day.isOpen ? formatTime(day.openingTime) : "—" }}</td>
                    <td class="px-3 py-3.5 text-slate-700 whitespace-nowrap">{{ day.isOpen ? formatTime(day.closingTime) : "—" }}</td>
                    <td class="px-3 py-3.5 text-slate-700 whitespace-nowrap">{{ day.isOpen ? formatTime(day.queueCutoffTime) : "—" }}</td>
                    <td v-if="isAdmin" class="px-3 py-3.5 text-right pr-5">
                      <button
                        class="text-xs font-semibold text-emerald-700 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 rounded"
                        @click="openEditDay(day)"
                      >
                        Edit
                      </button>
                    </td>
                  </tr>
                  <tr v-if="weeklySchedule.length === 0">
                    <td :colspan="isAdmin ? 6 : 5" class="px-5 py-8 text-center text-sm text-slate-500">
                      No weekly schedule configured yet.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
        </div>

        <!-- ======================= SCHEDULE EXCEPTIONS ======================= -->
        <section class="bg-white rounded-xl border border-slate-200 shadow-sm">
          <div class="px-6 py-4 border-b border-slate-200 flex items-center justify-between gap-4 flex-wrap">
            <div>
              <h2 class="text-sm font-bold text-slate-900">Schedule Exceptions</h2>
              <p class="text-xs text-slate-500 mt-0.5">Dates the health center is closed (typhoon, holiday) or extra vaccination days. Parents are notified when the health center is closed.</p>
            </div>
            <button
              v-if="isAdmin"
              class="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 text-white text-sm font-semibold px-4 py-2 hover:bg-emerald-700 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              @click="openAddException"
            >
              <span aria-hidden="true">＋</span> Add Exception
            </button>
          </div>

          <div v-if="exceptionsLoading" class="px-6 py-10 text-center text-sm text-slate-500">Loading schedule exceptions…</div>

          <div v-else-if="exceptionsLoadError" class="px-6 py-8 text-center">
            <p class="text-sm text-rose-600 font-medium">{{ exceptionsLoadError }}</p>
            <button class="mt-3 text-sm font-medium text-emerald-700 hover:underline" @click="loadExceptions">Try again</button>
          </div>

          <div v-else class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-xs font-semibold text-slate-500 uppercase tracking-wide border-b border-slate-200">
                  <th class="px-6 py-3">Date</th>
                  <th class="px-4 py-3">Status</th>
                  <th class="px-4 py-3">Opening</th>
                  <th class="px-4 py-3">Closing</th>
                  <th class="px-4 py-3">Queue Cutoff</th>
                  <th class="px-4 py-3">Reason</th>
                  <th class="px-4 py-3">Active</th>
                  <th v-if="isAdmin" class="px-4 py-3 text-right pr-6">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="ex in exceptions" :key="ex.exceptionID" class="border-b border-slate-100 last:border-0">
                  <td class="px-6 py-3.5 font-medium text-slate-900">{{ formatDateLong(ex.exceptionDate.slice(0, 10)) }}</td>
                  <td class="px-4 py-3.5">
                    <span
                      v-if="ex.isOpen"
                      class="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 text-emerald-700 text-xs font-semibold px-2.5 py-1"
                    >
                      <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span> Open
                    </span>
                    <span
                      v-else
                      class="inline-flex items-center gap-1.5 rounded-full bg-slate-100 text-slate-600 text-xs font-semibold px-2.5 py-1"
                    >
                      <span class="w-1.5 h-1.5 rounded-full bg-slate-400"></span> Closed
                    </span>
                  </td>
                  <td class="px-4 py-3.5 text-slate-700">{{ ex.isOpen ? formatTime(ex.openingTime) : "—" }}</td>
                  <td class="px-4 py-3.5 text-slate-700">{{ ex.isOpen ? formatTime(ex.closingTime) : "—" }}</td>
                  <td class="px-4 py-3.5 text-slate-700">{{ ex.isOpen ? formatTime(ex.queueCutoffTime) : "—" }}</td>
                  <td class="px-4 py-3.5 text-slate-700 max-w-[220px] truncate" :title="ex.reason">{{ ex.reason || "—" }}</td>
                  <td class="px-4 py-3.5">
                    <span
                      :class="ex.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'"
                      class="rounded-full text-xs font-semibold px-2.5 py-1"
                    >
                      {{ ex.isActive ? "Active" : "Inactive" }}
                    </span>
                  </td>
                  <td v-if="isAdmin" class="px-4 py-3.5 text-right pr-6 whitespace-nowrap">
                    <button
                      class="text-xs font-semibold text-emerald-700 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 rounded mr-3"
                      @click="openEditException(ex)"
                    >
                      Edit
                    </button>
                    <button
                      class="text-xs font-semibold text-rose-600 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-400 rounded"
                      @click="askDeleteException(ex)"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
                <tr v-if="exceptions.length === 0">
                  <td :colspan="isAdmin ? 8 : 7" class="px-6 py-8 text-center text-sm text-slate-500">
                    No schedule exceptions yet.
                    <span v-if="isAdmin">Use “Add Exception” to override a specific date.</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </main>
    </div>

    <!-- ======================= EDIT DAY MODAL ======================= -->
    <div v-if="showDayModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4" @click.self="closeDayModal">
      <div class="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div class="px-6 py-4 border-b border-slate-200">
          <h3 class="text-sm font-bold text-slate-900">Edit {{ dayNames[dayForm.dayOfWeek] }}</h3>
        </div>
        <div class="px-6 py-5 space-y-4">
          <p v-if="dayFormError" class="text-sm text-rose-600 bg-rose-50 border border-rose-200 rounded-lg px-3 py-2">{{ dayFormError }}</p>

          <label class="flex items-center gap-2 text-sm font-medium text-slate-700">
            <input type="checkbox" v-model="dayForm.isOpen" class="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" />
            Vaccinations are given on this day
          </label>

          <div v-if="dayForm.isOpen" class="grid grid-cols-1 gap-4">
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1">Opening Time</label>
              <input type="time" v-model="dayForm.openingTime" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1">Closing Time</label>
              <input type="time" v-model="dayForm.closingTime" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1">Queue Cutoff Time</label>
              <input type="time" v-model="dayForm.queueCutoffTime" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>
          </div>
          <p v-else class="text-xs text-slate-500">No vaccinations on this day. Doses due on it move to the next vaccination day.</p>
        </div>
        <div class="px-6 py-4 border-t border-slate-200 flex justify-end gap-3">
          <button class="text-sm font-semibold text-slate-600 px-4 py-2 rounded-lg hover:bg-slate-50" @click="closeDayModal">Cancel</button>
          <button
            class="text-sm font-semibold text-white bg-emerald-600 px-4 py-2 rounded-lg hover:bg-emerald-700 disabled:opacity-60"
            :disabled="savingDay"
            @click="saveDay"
          >
            {{ savingDay ? "Saving…" : "Save Changes" }}
          </button>
        </div>
      </div>
    </div>

    <!-- ======================= ADD/EDIT EXCEPTION MODAL ======================= -->
    <div v-if="showExceptionModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4" @click.self="closeExceptionModal">
      <div class="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div class="px-6 py-4 border-b border-slate-200">
          <h3 class="text-sm font-bold text-slate-900">{{ isEditingException ? "Edit Exception" : "Add Exception" }}</h3>
        </div>
        <div class="px-6 py-5 space-y-4">
          <p v-if="exceptionFormError" class="text-sm text-rose-600 bg-rose-50 border border-rose-200 rounded-lg px-3 py-2">{{ exceptionFormError }}</p>

          <!-- Calendar date picker -->
          <div class="relative">
            <label class="block text-xs font-semibold text-slate-500 mb-1">Exception Date</label>
            <button
              type="button"
              class="w-full flex items-center justify-between rounded-lg border border-slate-300 px-3 py-2 text-sm text-left hover:border-emerald-400 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              @click="showCalendar ? closeCalendar() : openCalendar()"
            >
              <span :class="exceptionForm.exceptionDate ? 'text-slate-900' : 'text-slate-400'">
                {{ exceptionForm.exceptionDate ? formatDateLong(exceptionForm.exceptionDate) : "Select a date" }}
              </span>
              <span aria-hidden="true" class="text-slate-400">📅</span>
            </button>

            <!-- click-away layer -->
            <div v-if="showCalendar" class="fixed inset-0 z-10" @click="closeCalendar"></div>

            <div
              v-if="showCalendar"
              class="absolute z-20 top-full mt-2 w-72 bg-white rounded-xl border border-slate-200 shadow-lg p-3"
            >
              <div class="flex items-center justify-between mb-2">
                <button
                  type="button"
                  class="w-7 h-7 rounded-lg flex items-center justify-center text-slate-500 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  @click="prevMonth"
                  aria-label="Previous month"
                >
                  ‹
                </button>
                <span class="text-sm font-semibold text-slate-900">{{ calendarLabel }}</span>
                <button
                  type="button"
                  class="w-7 h-7 rounded-lg flex items-center justify-center text-slate-500 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  @click="nextMonth"
                  aria-label="Next month"
                >
                  ›
                </button>
              </div>

              <div class="grid grid-cols-7 gap-1 mb-1">
                <span v-for="wd in WEEKDAY_LABELS" :key="wd" class="text-[10px] font-semibold text-slate-400 text-center py-1">{{ wd }}</span>
              </div>

              <div class="grid grid-cols-7 gap-1">
                <button
                  v-for="d in calendarDays"
                  :key="d.toISOString()"
                  type="button"
                  @click="pickDate(d)"
                  :class="[
                    d.getMonth() === calendarCursor.getMonth() ? 'text-slate-900' : 'text-slate-300',
                    isSameDate(d, selectedDateObj) ? 'bg-emerald-600 text-white hover:bg-emerald-600' : 'hover:bg-emerald-50',
                    isSameDate(d, today) && !isSameDate(d, selectedDateObj) ? 'ring-1 ring-emerald-400' : '',
                  ]"
                  class="h-8 w-8 rounded-lg text-xs font-medium flex items-center justify-center transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  {{ d.getDate() }}
                </button>
              </div>
            </div>
          </div>

          <!-- Several days at once (new exceptions only), e.g. a typhoon -->
          <div v-if="!isEditingException">
            <label class="block text-xs font-semibold text-slate-500 mb-1">Until (optional)</label>
            <input
              type="date"
              v-model="exceptionForm.endDate"
              :min="exceptionForm.exceptionDate || undefined"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
            <p class="text-[11px] text-slate-400 mt-1">For more than one day, e.g. a typhoon closing the health center Monday to Wednesday.</p>
          </div>

          <label class="flex items-center gap-2 text-sm font-medium text-slate-700">
            <input type="checkbox" v-model="exceptionForm.isOpen" class="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" />
            Open for vaccinations on this date
          </label>

          <div v-if="exceptionForm.isOpen" class="grid grid-cols-1 gap-4">
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1">Opening Time</label>
              <input type="time" v-model="exceptionForm.openingTime" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1">Closing Time</label>
              <input type="time" v-model="exceptionForm.closingTime" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1">Queue Cutoff Time</label>
              <input type="time" v-model="exceptionForm.queueCutoffTime" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>
          </div>
          <p v-else class="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
            The health center is closed. When you save, every parent is notified in the app, and families with a child
            due on this date also get an email and SMS with the child's new date. Doses due on this date move to the next vaccination day.
          </p>

          <div>
            <label class="block text-xs font-semibold text-slate-500 mb-1">Reason</label>
            <input
              type="text"
              v-model="exceptionForm.reason"
              placeholder="e.g. Typhoon, Holiday"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
        </div>
        <div class="px-6 py-4 border-t border-slate-200 flex justify-end gap-3">
          <button class="text-sm font-semibold text-slate-600 px-4 py-2 rounded-lg hover:bg-slate-50" @click="closeExceptionModal">Cancel</button>
          <button
            class="text-sm font-semibold text-white bg-emerald-600 px-4 py-2 rounded-lg hover:bg-emerald-700 disabled:opacity-60"
            :disabled="savingException"
            @click="saveException"
          >
            {{ savingException ? "Saving…" : isEditingException ? "Save Changes" : "Create Exception" }}
          </button>
        </div>
      </div>
    </div>

    <!-- ======================= DELETE CONFIRM ======================= -->
    <div v-if="confirmDeleteTarget" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4" @click.self="cancelDelete">
      <div class="bg-white rounded-xl shadow-xl w-full max-w-sm">
        <div class="px-6 py-5">
          <h3 class="text-sm font-bold text-slate-900">Remove this exception?</h3>
          <p class="text-sm text-slate-600 mt-2">
            {{ formatDateLong(confirmDeleteTarget.exceptionDate.slice(0, 10)) }} — {{ confirmDeleteTarget.reason || "No reason given" }}.
            Doses already moved off this date keep their new date.
          </p>
        </div>
        <div class="px-6 py-4 border-t border-slate-200 flex justify-end gap-3">
          <button class="text-sm font-semibold text-slate-600 px-4 py-2 rounded-lg hover:bg-slate-50" @click="cancelDelete">Cancel</button>
          <button
            class="text-sm font-semibold text-white bg-rose-600 px-4 py-2 rounded-lg hover:bg-rose-700 disabled:opacity-60"
            :disabled="deletingException"
            @click="confirmDeleteException"
          >
            {{ deletingException ? "Removing…" : "Remove" }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>