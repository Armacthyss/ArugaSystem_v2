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
          <p class="text-xs font-semibold uppercase tracking-wider text-white/80">Next vaccination day</p>

          <p v-if="scheduleLoading && !schedule.length" class="mt-3 text-white/80">Loading…</p>

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
            <div v-for="cell in cells" :key="cell.key" :title="cell.title" :aria-label="cell.title"
              class="relative flex h-11 flex-col items-center justify-center rounded-xl text-sm sm:h-16 sm:rounded-2xl sm:text-base"
              :class="cellClass(cell)">
              <span>{{ cell.day }}</span>
              <span v-if="cell.clinicDay && !cell.visit" class="mt-0.5 h-1.5 w-1.5 rounded-full bg-emerald-500"></span>
            </div>
          </div>

          <div class="mt-5 flex flex-wrap gap-x-5 gap-y-2 px-1 text-sm text-slate-600">
            <span class="flex items-center gap-2"><span class="h-3.5 w-3.5 rounded-full bg-emerald-600"></span>Your next visit</span>
            <span class="flex items-center gap-2"><span class="h-3.5 w-3.5 rounded-full bg-emerald-100"></span>Other vaccination days</span>
            <span class="flex items-center gap-2"><span class="h-3.5 w-3.5 rounded-full border-2 border-dashed border-amber-400"></span>Today</span>
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

// Open the month of the next visit whenever it changes (e.g. another child)
watch(() => visit.value?.date?.getTime(), t => {
  const d = t ? new Date(t) : today
  shown.value = new Date(d.getFullYear(), d.getMonth(), 1)
}, { immediate: true })

function moveMonth(step) {
  shown.value = new Date(shown.value.getFullYear(), shown.value.getMonth() + step, 1)
}

const monthLabel = computed(() => format(shown.value, 'MMMM yyyy'))
const monthOffset = computed(() => shown.value.getDay())

const cells = computed(() => {
  const y = shown.value.getFullYear(), m = shown.value.getMonth()
  const visitTime = visit.value ? dayOnly(visit.value.date).getTime() : null
  return Array.from({ length: getDaysInMonth(shown.value) }, (_, i) => {
    const date = new Date(y, m, i + 1)
    const cell = {
      key: format(date, 'yyyy-MM-dd'),
      day: i + 1,
      isToday: date.getTime() === today.getTime(),
      visit: date.getTime() === visitTime,
      clinicDay: isClinicDay(clinic.value, date),
      closed: isClosedException(clinic.value, date),
    }
    cell.title = `${format(date, 'EEEE, MMMM d')}${cell.visit ? ' · your next visit' : cell.closed ? ' · health center closed' : cell.clinicDay ? ' · vaccination day' : ''}`
    return cell
  })
})

function cellClass(cell) {
  // Today: a dashed amber outline (outside the green box when today is the visit)
  const todayMark = cell.isToday
    ? (cell.visit ? ' outline-2 outline-dashed outline-amber-400 outline-offset-2' : ' outline-2 outline-dashed outline-amber-400 -outline-offset-2 text-slate-900')
    : ''
  if (cell.visit) return 'bg-emerald-600 font-bold text-white shadow-sm' + todayMark
  if (cell.clinicDay) return 'bg-emerald-50 font-semibold text-slate-800' + todayMark
  if (cell.closed) return 'text-slate-300 line-through' + todayMark
  return 'text-slate-400' + todayMark
}

onMounted(init)
</script>
