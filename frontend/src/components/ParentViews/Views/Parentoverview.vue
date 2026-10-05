<!--
  Parent Overview: only what a parent needs today.
    1. Today at the clinic (queue number / now serving / check-in hint)
    2. The next vaccine, and progress
    3. What's coming up after that
  The child's details are behind "Health info" (ChildTabs.vue), not on the page.
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

    <main class="space-y-4 sm:space-y-6">
      <!-- No child linked yet -->
      <section v-if="childrenLoaded && !children.length" class="rounded-3xl border border-slate-100 bg-white p-6 shadow-sm sm:p-8">
        <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">Welcome to Aruga</p>
        <h2 class="mt-2 text-2xl font-black text-slate-900">No children linked yet</h2>
        <p class="mt-1 text-sm text-slate-500">{{ childrenError || 'Please ask the health center staff to link your child to this account.' }}</p>
      </section>

      <template v-else-if="children.length">
        <!-- ═════════ 1. TODAY AT THE CLINIC ═════════ -->
        <section class="flex flex-col gap-5 rounded-3xl border border-slate-100 bg-white p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8">
          <div class="min-w-0">
            <!-- Checked in today -->
            <template v-if="queue.checkedIn">
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">
                {{ queue.myStatus === 'Completed' ? 'Visit completed today' : 'Your queue number today' }}
              </p>
              <p class="mt-1 text-5xl font-black leading-none text-emerald-600 sm:text-6xl">#{{ pad(queue.myQueueNumber) }}</p>
              <p class="mt-3 text-sm text-slate-600 sm:text-base">{{ ticketLine }}</p>
            </template>

            <!-- Not checked in -->
            <template v-else>
              <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">Today at the clinic</p>
              <h2 class="mt-1 text-2xl font-black text-slate-900 sm:text-3xl">{{ todayTitle }}</h2>
              <p class="mt-2 text-sm text-slate-600 sm:text-base">
                {{ todayText }}
                <router-link v-if="clinic?.checkInOpenNow" to="/ParentCheckin" class="font-semibold text-emerald-700 underline">Open Check-in</router-link>
              </p>
            </template>
          </div>

          <div v-if="showNowServing" class="shrink-0 rounded-2xl bg-emerald-50 px-6 py-4 text-center sm:min-w-52 sm:px-8 sm:py-5">
            <p class="text-xs font-bold uppercase tracking-wider text-emerald-700">Now serving</p>
            <p class="mt-1 text-4xl font-black text-slate-900 sm:text-5xl">{{ queue.nowServingNumber ? '#' + pad(queue.nowServingNumber) : '—' }}</p>
            <p v-if="servingNote" class="mt-1 text-sm font-semibold text-emerald-700">{{ servingNote }}</p>
          </div>
        </section>

        <!-- ═════════ 2. NEXT VACCINE + PROGRESS ═════════ -->
        <div class="grid grid-cols-1 gap-4 sm:gap-6 md:grid-cols-2">
          <section class="rounded-3xl border border-slate-100 bg-white p-6 shadow-sm sm:p-8">
            <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">Next vaccine</p>

            <p v-if="scheduleLoading && !schedule.length" class="mt-3 text-sm text-slate-400">Loading…</p>

            <template v-else-if="visit">
              <h2 class="mt-2 text-2xl font-black text-slate-900 sm:text-3xl">{{ visit.doses[0].name }} · Dose {{ visit.doses[0].doseNumber }}</h2>
              <p v-if="visit.doses.length > 1" class="mt-1 text-sm text-slate-600 sm:text-base">Plus {{ alsoDue }}</p>
              <span class="mt-4 inline-block rounded-2xl px-4 py-2 text-sm font-semibold" :class="visit.overdue ? 'bg-amber-50 text-amber-800' : 'bg-emerald-50 text-emerald-800'">
                {{ dueBadge }}
              </span>
            </template>

            <template v-else-if="schedule.length">
              <h2 class="mt-2 text-2xl font-black text-slate-900">All vaccines done 🎉</h2>
              <p class="mt-1 text-sm text-slate-600">{{ selectedChild?.firstName }} has every scheduled vaccine.</p>
            </template>

            <p v-else class="mt-3 text-sm text-slate-500">No vaccination schedule on file yet.</p>
          </section>

          <section class="rounded-3xl border border-slate-100 bg-white p-6 shadow-sm sm:p-8">
            <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">Progress</p>
            <p class="mt-2 text-5xl font-black leading-none text-slate-900">
              {{ completedCount }}<span class="text-2xl font-bold text-slate-400"> / {{ schedule.length }}</span>
            </p>
            <div class="mt-5 h-2.5 w-full overflow-hidden rounded-full bg-slate-100" role="progressbar" :aria-valuenow="completedCount" :aria-valuemax="schedule.length">
              <div class="h-full rounded-full bg-emerald-500 transition-all duration-500" :style="{ width: progressPercent + '%' }"></div>
            </div>
            <p class="mt-3 text-sm text-slate-600 sm:text-base">doses completed</p>
          </section>
        </div>

        <!-- ═════════ 3. COMING UP ═════════ -->
        <section class="rounded-3xl border border-slate-100 bg-white p-6 shadow-sm sm:p-8">
          <div class="flex items-center justify-between gap-3">
            <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">Coming up</p>
            <router-link to="/ParentSchedule" class="shrink-0 text-sm font-semibold text-emerald-700 hover:underline">See schedule →</router-link>
          </div>
          <ul v-if="comingUp.length" class="mt-3 divide-y divide-slate-100 border-t border-slate-100">
            <li v-for="dose in comingUp" :key="dose.doseId" class="flex items-center justify-between gap-4 py-3.5">
              <span class="min-w-0 text-sm font-semibold text-slate-800 sm:text-base">{{ dose.name }} · Dose {{ dose.doseNumber }}</span>
              <span class="shrink-0 text-sm text-slate-500">{{ formatDate(dose.scheduledDate) }}</span>
            </li>
          </ul>
          <p v-else-if="!scheduleLoading" class="mt-3 border-t border-slate-100 pt-3 text-sm text-slate-500">
            {{ visit ? 'Nothing else is scheduled after the next vaccine.' : 'Nothing scheduled.' }}
          </p>
        </section>
      </template>
    </main>

    <ProfileModal v-if="showProfile" :parent-data="parentData" :children="children" @close="showProfile = false" />
    <NotificationPanel v-if="showNotifications" :parent-data="parentData" @close="showNotifications = false" />
  </ParentLayout>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { format } from 'date-fns'

import HeaderNav from '../Components/Headernav.vue'
import ParentLayout from '../Components/ParentLayout.vue'
import ChildTabs from '../Components/ChildTabs.vue'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'

import api from '../Composables/api.js'
import { useParentPortal } from '../Composables/useParentPortal.js'
import { planNextVisit, relativeDay, ordinal, dayOnly } from '../Composables/visitPlan.js'

const {
  parentData, children, childrenLoaded, childrenError, selectedChild,
  unreadCount, clinic, schedule, scheduleLoading,
  init, selectChild, logout,
} = useParentPortal()

const showProfile = ref(false)
const showNotifications = ref(false)

const pad = n => String(n ?? '').padStart(3, '0')
const formatDate = d => format(new Date(d), 'MMM d, yyyy')

// ── Next vaccine, progress, coming up ──────────────────────────────────
const visit = computed(() => planNextVisit(schedule.value, clinic.value))

const alsoDue = computed(() => {
  const rest = visit.value.doses.slice(1).map(d => `${d.name} · Dose ${d.doseNumber}`)
  return rest.length > 1 ? `${rest.slice(0, -1).join(', ')} and ${rest[rest.length - 1]}` : rest[0]
})

const dueBadge = computed(() => {
  const v = visit.value
  if (v.overdue) return `Due ${format(v.firstDue, 'MMM d')} · come on the next vaccination day`
  const isToday = dayOnly(v.date).getTime() === dayOnly(new Date()).getTime()
  if (isToday) return clinic.value?.checkInUntil ? `Due today · check in by ${clinic.value.checkInUntil}` : 'Due today'
  return `Due ${format(v.date, 'EEE, MMM d')} · ${relativeDay(v.date)}`
})

const completedCount = computed(() => schedule.value.filter(v => v.isCompleted).length)
const progressPercent = computed(() =>
  schedule.value.length ? Math.round((completedCount.value / schedule.value.length) * 100) : 0)

const comingUp = computed(() => (visit.value?.later ?? []).slice(0, 3))

// ── Today at the clinic (queue ticket) ─────────────────────────────────
const queue = ref({ checkedIn: false, myQueueNumber: null, myStatus: null, nowServingNumber: null, positionInLine: null, childIDs: [] })
let pollHandle = null

async function loadQueue() {
  if (!parentData.value?.parentID) return
  try {
    const d = (await api.get(`/Queue/my-status/${parentData.value.parentID}`)).data
    queue.value = {
      checkedIn: d.checkedIn ?? d.CheckedIn,
      myQueueNumber: d.myQueueNumber ?? d.MyQueueNumber,
      myStatus: d.myStatus ?? d.MyStatus,
      nowServingNumber: d.nowServingNumber ?? d.NowServingNumber,
      positionInLine: d.positionInLine ?? d.PositionInLine,
      childIDs: d.childIDs ?? d.ChildIDs ?? [],
    }
  } catch (err) {
    console.error('Could not load the queue status:', err)
  }
}

const inRoom = computed(() => /inprogress/i.test(String(queue.value.myStatus || '').replace(/\s+/g, '')))

const ticketNames = computed(() => {
  const ids = (queue.value.childIDs || []).map(id => String(id).toLowerCase())
  const names = children.value.filter(c => ids.includes(String(c.childID).toLowerCase())).map(c => c.firstName)
  return names.length > 1 ? `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}` : names[0] || ''
})

const ticketLine = computed(() => {
  const q = queue.value
  const forWho = ticketNames.value ? `For ${ticketNames.value}` : ''
  let where
  if (q.myStatus === 'Completed') where = 'See Records for today’s vaccines'
  else if (inRoom.value) where = 'It’s your turn: please go inside the vaccination room'
  else if (q.positionInLine === 1) where = 'You’re next'
  else if (q.positionInLine) where = `${ordinal(q.positionInLine)} in line`
  else where = 'Please wait to be called'
  return [forWho, where].filter(Boolean).join(' · ')
})

// Only on a clinic day (or once checked in) does "now serving" mean anything
const showNowServing = computed(() => {
  const q = queue.value
  if (q.checkedIn) return q.myStatus !== 'Completed'
  return !!q.nowServingNumber && clinic.value?.openToday !== false
})

const servingNote = computed(() => {
  const q = queue.value
  if (!q.checkedIn) return ''
  if (inRoom.value) return 'That’s you!'
  return 'Please stay nearby'
})

const todayTitle = computed(() => {
  const c = clinic.value
  if (c?.closedToday) return 'Health center closed today'
  if (c && !c.openToday) return 'No vaccinations today'
  return 'Not checked in today'
})

const todayText = computed(() => {
  const c = clinic.value
  if (!c) return ''
  if (c.closedToday) return `${c.reason ? c.reason + '. ' : ''}Next vaccination day: ${c.nextOpenDay}.`
  if (!c.openToday) return `Next vaccination day: ${c.nextOpenDay}.`
  if (c.checkInOpenNow) return 'When you arrive, scan the QR code at the entrance.'
  if (c.checkInNotYetOpen) return `Check-in opens at ${c.opensAt} and runs until ${c.checkInUntil}.`
  return `Check-in closed at ${c.checkInUntil}. Next vaccination day: ${c.nextOpenDay}.`
})

onMounted(async () => {
  if (!(await init())) return
  await loadQueue()
  pollHandle = setInterval(loadQueue, 5000)   // keeps "now serving" live
})

onUnmounted(() => clearInterval(pollHandle))
</script>
