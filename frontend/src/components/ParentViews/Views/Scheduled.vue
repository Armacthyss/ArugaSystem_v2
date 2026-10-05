<!--
  Parent Schedule: the next vaccination day and what is given that day, and
  a calendar of the clinic's vaccination days. The full dose-by-dose history
  is on the Records page ("See full history").
-->
<template>
  <ParentLayout>
    <HeaderNav
      :parent-data="parentData"
      :children="children"
      :unread-count="unreadCount"
      @open-profile="showProfile = true"
      @open-notifications="showNotifications = true"
      @logout="logout"
      @select-child="selectChild"
    />

    <ChildTabs :children="children" :selected-child="selectedChild" @select-child="selectChild" />

    <main>
      <section v-if="childrenLoaded && !children.length" class="rounded-3xl border border-slate-100 bg-white p-6 shadow-sm sm:p-8">
        <h2 class="text-2xl font-black text-slate-900">No children linked yet</h2>
        <p class="mt-1 text-sm text-slate-500">{{ childrenError || 'Please ask the health center staff to link your child to this account.' }}</p>
      </section>

      <div v-else-if="children.length" class="grid grid-cols-1 gap-4 sm:gap-6 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)]">
        <!-- ═════════ NEXT VACCINATION DAY ═════════ -->
        <section class="flex flex-col rounded-3xl bg-emerald-600 p-6 text-white shadow-sm sm:p-8">
          <div class="flex items-start justify-between gap-3">
            <p class="text-xs font-semibold uppercase tracking-wider text-white/80">{{ picked ? picked.label : 'Next vaccination day' }}</p>
            <button v-if="picked" type="button" @click="selectedKey = null" class="shrink-0 text-xs font-semibold text-white/80 hover:text-white hover:underline">← Next visit</button>
          </div>

          <!-- A day the parent tapped on the calendar -->
          <template v-if="picked">
            <h2 class="mt-2 text-3xl font-black leading-tight sm:text-4xl">
              {{ format(picked.date, 'EEEE') }},<br />{{ format(picked.date, 'MMMM d') }}
            </h2>
            <p v-if="picked.hours" class="mt-2 text-base text-white/90 sm:text-lg">{{ picked.hours }}</p>

            <ul v-if="picked.doses.length" class="mt-5 space-y-2">
              <li v-for="item in picked.doses" :key="item.dose.doseId" class="flex items-center justify-between gap-3 rounded-xl bg-white/15 px-4 py-3">
                <div class="min-w-0">
                  <p class="font-bold">{{ item.dose.name }}</p>
                  <p v-if="item.note" class="text-xs text-white/80">{{ item.note }}</p>
                </div>
                <div class="shrink-0 text-right">
                  <p class="text-white/90">Dose {{ item.dose.doseNumber }}</p>
                  <span v-if="item.badge" class="mt-0.5 inline-block rounded-full px-2 py-0.5 text-[10px] font-bold uppercase" :class="item.badgeClass">{{ item.badge }}</span>
                </div>
              </li>
            </ul>
            <p v-else class="mt-5 rounded-xl bg-white/10 px-4 py-3 text-white/90">{{ picked.empty }}</p>

            <button v-if="picked.reminder" type="button" @click="addPickedReminder"
              class="mt-5 w-full rounded-2xl bg-white py-3.5 text-base font-bold text-emerald-700 shadow-sm transition-colors hover:bg-emerald-50">
              Add reminder 🔔
            </button>
          </template>

          <p v-else-if="scheduleLoading && !schedule.length" class="mt-3 text-white/80">Loading…</p>

          <template v-else-if="visit">
            <h2 class="mt-2 text-3xl font-black leading-tight sm:text-4xl">
              {{ format(visit.date, 'EEEE') }},<br />{{ format(visit.date, 'MMMM d') }}
            </h2>
            <p v-if="hours" class="mt-2 text-base text-white/90 sm:text-lg">{{ hours }}</p>
            <p v-if="visit.overdue" class="mt-3 rounded-xl bg-amber-300/20 px-3 py-2 text-sm font-semibold text-amber-50">
              Was due {{ format(visit.firstDue, 'MMM d') }}. Please come on this day.
            </p>

            <ul class="mt-5 space-y-2">
              <li v-for="dose in visit.doses" :key="dose.doseId" class="flex items-center justify-between gap-3 rounded-xl bg-white/15 px-4 py-3">
                <span class="min-w-0 font-bold">{{ dose.name }}</span>
                <span class="shrink-0 text-white/90">Dose {{ dose.doseNumber }}</span>
              </li>
            </ul>

            <button type="button" @click="addReminder"
              class="mt-5 w-full rounded-2xl bg-white py-3.5 text-base font-bold text-emerald-700 shadow-sm transition-colors hover:bg-emerald-50">
              Add reminder 🔔
            </button>
            <p class="mt-2 text-center text-xs text-white/70">Saves this visit to your phone's calendar</p>
          </template>

          <template v-else-if="schedule.length">
            <h2 class="mt-2 text-3xl font-black leading-tight">All vaccines done 🎉</h2>
            <p class="mt-2 text-white/90">{{ selectedChild?.firstName }} has every scheduled vaccine.</p>
          </template>

          <p v-else class="mt-3 text-white/90">No vaccination schedule on file yet.</p>

          <div class="mt-auto pt-6">
            <p class="text-sm text-white/90">{{ completedCount }} of {{ schedule.length }} doses done</p>
            <div class="mt-2 h-2 w-full overflow-hidden rounded-full bg-white/25">
              <div class="h-full rounded-full bg-white transition-all duration-500" :style="{ width: progressPercent + '%' }"></div>
            </div>
          </div>
        </section>

        <!-- ═════════ CALENDAR ═════════ -->
        <section class="rounded-3xl border border-slate-100 bg-white p-4 shadow-sm sm:p-8">
          <div class="mb-4 flex items-center justify-between gap-3 px-1 sm:mb-6">
            <h3 class="text-xl font-black text-slate-900 sm:text-2xl">{{ monthLabel }}</h3>
            <div class="flex gap-1">
              <button type="button" @click="moveMonth(-1)" aria-label="Previous month" class="flex h-9 w-9 items-center justify-center rounded-xl text-xl text-slate-600 hover:bg-slate-100">‹</button>
              <button type="button" @click="moveMonth(1)" aria-label="Next month" class="flex h-9 w-9 items-center justify-center rounded-xl text-xl text-slate-600 hover:bg-slate-100">›</button>
            </div>
          </div>

          <div class="grid grid-cols-7 gap-1 sm:gap-2">
            <div v-for="d in ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']" :key="d" class="pb-1 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-400 sm:text-xs">{{ d }}</div>
            <div v-for="n in monthOffset" :key="'blank-' + n"></div>
            <button v-for="cell in cells" :key="cell.key" type="button" :title="cell.title" :aria-label="cell.title"
              @click="pickDay(cell)"
              class="relative flex h-11 flex-col items-center justify-center rounded-xl text-sm transition-colors sm:h-16 sm:rounded-2xl sm:text-base"
              :class="cellClass(cell)">
              <span>{{ cell.day }}</span>
              <!-- The child's own vaccine days: blue, or red when a dose was missed -->
              <span v-if="cell.dots.length" class="mt-0.5 flex gap-0.5">
                <span v-for="t in cell.dots" :key="t" class="h-2 w-2 rounded-full" :class="cell.visit ? 'bg-white' : DOT[t]"></span>
              </span>
            </button>
          </div>

          <p v-if="!monthHasDoses" class="mt-4 px-1 text-center text-sm text-slate-500">
            No vaccines for {{ selectedChild?.firstName }} in {{ format(shown, 'MMMM') }}.
            <button v-if="visit && !visitInShownMonth" type="button" @click="goToVisit" class="font-semibold text-emerald-700 hover:underline">Go to next visit →</button>
          </p>

          <div class="mt-5 flex flex-wrap gap-x-5 gap-y-2 px-1 text-sm text-slate-600">
            <span class="flex items-center gap-2"><span class="h-3.5 w-3.5 rounded-full bg-emerald-600"></span>Your next visit</span>
            <span class="flex items-center gap-2"><span class="h-3.5 w-3.5 rounded-full bg-emerald-100"></span>Other vaccination days</span>
            <span class="flex items-center gap-2"><span class="h-3.5 w-3.5 rounded-full border-2 border-dashed border-amber-400"></span>Today</span>
            <span class="flex items-center gap-2"><span class="h-2 w-2 rounded-full bg-blue-600"></span>{{ selectedChild?.firstName ? `${selectedChild.firstName}'s vaccine day` : 'Vaccine day' }}</span>
            <span v-if="hasOverdue" class="flex items-center gap-2"><span class="h-2 w-2 rounded-full bg-red-500"></span>Missed</span>
          </div>

          <p class="mt-4 px-1 text-center text-sm text-slate-500">
            <span v-if="clinic?.clinicHoursText">Clinic open {{ compactHours(clinic.clinicHoursText) }} · </span>
            <span v-if="clinic?.hoursText">Vaccinations {{ compactHours(clinic.hoursText) }} · </span>
            <router-link to="/ParentRecords" class="font-semibold text-emerald-700 hover:underline">See full history</router-link>
          </p>
          <p class="mt-1 text-center text-xs text-slate-400">Vaccine stock may change without notice.</p>
        </section>
      </div>
    </main>

    <ProfileModal v-if="showProfile" :parent-data="parentData" :children="children" @close="showProfile = false" />
    <NotificationPanel v-if="showNotifications" :parent-data="parentData" @close="showNotifications = false" />
  </ParentLayout>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { format, getDaysInMonth } from 'date-fns'

import HeaderNav from '../Components/Headernav.vue'
import ParentLayout from '../Components/ParentLayout.vue'
import ChildTabs from '../Components/ChildTabs.vue'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'

import { useParentPortal } from '../Composables/useParentPortal.js'
import {
  planNextVisit, visitHours, isClinicDay, isClosedException, dayOnly, downloadVisitReminder,
} from '../Composables/visitPlan.js'

const {
  parentData, children, childrenLoaded, childrenError, selectedChild,
  unreadCount, clinic, schedule, scheduleLoading,
  init, selectChild, logout,
} = useParentPortal()

const showProfile = ref(false)
const showNotifications = ref(false)

// ── Next visit ─────────────────────────────────────────────────────────
const visit = computed(() => planNextVisit(schedule.value, clinic.value))
const hours = computed(() => visit.value ? visitHours(clinic.value, visit.value.date) : '')

const completedCount = computed(() => schedule.value.filter(v => v.isCompleted).length)
const progressPercent = computed(() =>
  schedule.value.length ? Math.round((completedCount.value / schedule.value.length) * 100) : 0)

// "Mon–Fri · 8:00 AM – 5:00 PM" -> "Mon–Fri 8–5"
const compactHours = text => String(text)
  .replace(/\s*·\s*/g, ' ').replace(/:00/g, '').replace(/\s*(AM|PM)/gi, '').replace(/\s*–\s*/g, '–')

function addReminder() {
  if (visit.value && selectedChild.value) {
    downloadVisitReminder({ child: selectedChild.value, visit: visit.value, hours: hours.value })
  }
}

// ── Calendar ───────────────────────────────────────────────────────────
const today = dayOnly(new Date())
const shown = ref(new Date(today.getFullYear(), today.getMonth(), 1))

const dayKey = d => format(d, 'yyyy-MM-dd')

// ── The child's own doses on the calendar ──────────────────────────────
// Every dose sits on its date (the day it was given, or the day it's due),
// so later visits like Penta 3 show too, not only the next visit.
const DOT = { overdue: 'bg-red-500', scheduled: 'bg-blue-600', vaccinated: 'bg-emerald-500' }
const STATUS_ORDER = ['overdue', 'scheduled', 'vaccinated']
const doseStatus = d => d.isCompleted ? 'vaccinated' : d.scheduledDate < today ? 'overdue' : 'scheduled'

const dosesByDay = computed(() => {
  const map = new Map()
  for (const dose of schedule.value) {
    const key = dayKey(dose.scheduledDate)
    if (!map.has(key)) map.set(key, [])
    map.get(key).push(dose)
  }
  for (const list of map.values())
    list.sort((a, b) => STATUS_ORDER.indexOf(doseStatus(a)) - STATUS_ORDER.indexOf(doseStatus(b)))
  return map
})

// ── A tapped day (null = the card shows the next visit) ────────────────
const selectedKey = ref(null)

function pickDay(cell) {
  selectedKey.value = cell.visit || cell.key === selectedKey.value ? null : cell.key
}

function doseItem(dose) {
  const status = doseStatus(dose)
  if (status === 'vaccinated')
    return { dose, note: dose.administeredByName ? `Vaccinated by ${dose.administeredByName}` : '', badge: 'Vaccinated', badgeClass: 'bg-white text-emerald-700' }
  if (status === 'overdue')
    return { dose, note: visit.value ? `Not yet given · please come on ${format(visit.value.date, 'EEEE, MMM d')}` : 'Not yet given', badge: 'Missed', badgeClass: 'bg-red-500 text-white' }
  return { dose, note: '', badge: '', badgeClass: '' }
}

const picked = computed(() => {
  if (!selectedKey.value) return null
  const [y, m, d] = selectedKey.value.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  const doses = dosesByDay.value.get(selectedKey.value) || []
  const statuses = doses.map(doseStatus)
  const clinicDay = isClinicDay(clinic.value, date)
  const closed = isClosedException(clinic.value, date)
  const upcoming = doses.filter(x => doseStatus(x) === 'scheduled')
  return {
    date,
    label: doses.length
      ? (statuses.every(s => s === 'vaccinated') ? 'Vaccinated' : statuses.includes('overdue') ? 'Missed · not yet vaccinated' : 'Later vaccination day')
      : closed ? 'Health center closed' : clinicDay ? 'Vaccination day' : 'No vaccinations',
    hours: clinicDay && !closed && date >= today ? visitHours(clinic.value, date) : '',
    doses: doses.map(doseItem),
    empty: clinicDay && !closed
      ? `No vaccine for ${selectedChild.value?.firstName ?? 'your child'} on this day.`
      : "The health center doesn't give vaccinations on this day.",
    reminder: upcoming.length ? { date, doses: upcoming } : null,
  }
})

function addPickedReminder() {
  if (picked.value?.reminder && selectedChild.value) {
    downloadVisitReminder({ child: selectedChild.value, visit: picked.value.reminder, hours: picked.value.hours })
  }
}

// ── Month shown ────────────────────────────────────────────────────────
const isInShownMonth = date => date.getFullYear() === shown.value.getFullYear() && date.getMonth() === shown.value.getMonth()
const visitInShownMonth = computed(() => !!visit.value && isInShownMonth(visit.value.date))
const monthDoses = computed(() =>
  schedule.value.filter(x => isInShownMonth(x.scheduledDate)).sort((a, b) => a.scheduledDate - b.scheduledDate))
const monthHasDoses = computed(() => monthDoses.value.length > 0)
// The red "Missed" mark only needs explaining when the child has one
const hasOverdue = computed(() => schedule.value.some(x => doseStatus(x) === 'overdue'))

// Open the month of the next visit whenever it changes (e.g. another child)
function goToVisit() {
  const d = visit.value?.date ?? today
  shown.value = new Date(d.getFullYear(), d.getMonth(), 1)
  selectedKey.value = null
}
watch(() => visit.value?.date?.getTime(), goToVisit, { immediate: true })
watch(() => selectedChild.value?.childID, () => { selectedKey.value = null })

// The card follows the vaccinations while browsing: the month of the next
// visit shows the next visit; another month with vaccinations shows its
// first one (still to come, else the first); a month without any keeps
// whatever the card already shows until a month with vaccinations comes.
function moveMonth(step) {
  shown.value = new Date(shown.value.getFullYear(), shown.value.getMonth() + step, 1)
  if (visitInShownMonth.value) { selectedKey.value = null; return }
  const first = monthDoses.value.find(x => !x.isCompleted) ?? monthDoses.value[0]
  if (first) selectedKey.value = dayKey(first.scheduledDate)
}

const monthLabel = computed(() => format(shown.value, 'MMMM yyyy'))
const monthOffset = computed(() => shown.value.getDay())

const cells = computed(() => {
  const y = shown.value.getFullYear(), m = shown.value.getMonth()
  const visitTime = visit.value ? dayOnly(visit.value.date).getTime() : null
  return Array.from({ length: getDaysInMonth(shown.value) }, (_, i) => {
    const date = new Date(y, m, i + 1)
    const key = dayKey(date)
    const doses = dosesByDay.value.get(key) || []
    const cell = {
      key,
      day: i + 1,
      isToday: date.getTime() === today.getTime(),
      visit: date.getTime() === visitTime,
      clinicDay: isClinicDay(clinic.value, date),
      closed: isClosedException(clinic.value, date),
      doses,
      // One mark per day: red if a dose there was missed, else blue
      dots: !doses.length ? [] : doses.some(x => doseStatus(x) === 'overdue') ? ['overdue'] : ['scheduled'],
      selected: key === selectedKey.value,
    }
    const what = doses.map(x => `${x.name} dose ${x.doseNumber} (${doseStatus(x)})`).join(', ')
    cell.title = `${format(date, 'EEEE, MMMM d')}${cell.visit ? ' · your next visit' : ''}${what ? ` · ${what}` : cell.closed ? ' · health center closed' : cell.clinicDay ? ' · vaccination day' : ''}`
    return cell
  })
})

function cellClass(cell) {
  // Today: a dashed amber outline (outside the green box when today is the visit)
  const todayMark = cell.isToday
    ? (cell.visit ? ' outline-2 outline-dashed outline-amber-400 outline-offset-2' : ' outline-2 outline-dashed outline-amber-400 -outline-offset-2 text-slate-900')
    : ''
  const selectedMark = cell.selected ? ' ring-2 ring-emerald-600 ring-offset-2' : ''
  if (cell.visit) return 'bg-emerald-600 font-bold text-white shadow-sm hover:bg-emerald-700' + todayMark
  if (cell.clinicDay) return `bg-emerald-50 ${cell.doses.length ? 'font-black text-slate-900' : 'font-semibold text-slate-800'} hover:bg-emerald-100` + selectedMark + todayMark
  if (cell.closed) return 'text-slate-300 line-through hover:bg-slate-50' + selectedMark + todayMark
  if (cell.doses.length) return 'font-bold text-slate-800 hover:bg-slate-50' + selectedMark + todayMark
  return 'text-slate-400 hover:bg-slate-50' + selectedMark + todayMark
}

onMounted(init)
</script>
