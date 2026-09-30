<template>
  <div class="flex h-screen bg-stone-50 antialiased text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">

    <StaffSidebar />

    <!-- MAIN SECTION -->
    <div class="flex-1 flex flex-col overflow-hidden">
      <StaffTopbar title="Calendar" breadcrumb="Aruga / Calendar">
        <button @click="goPrevMonth" class="p-2 hover:bg-slate-100 rounded-lg text-slate-500" aria-label="Previous month"><ChevronLeft class="w-5 h-5" /></button>
        <span class="px-4 py-2 font-bold text-sm w-36 text-center">{{ monthLabel }}</span>
        <button @click="goNextMonth" class="p-2 hover:bg-slate-100 rounded-lg text-slate-500" aria-label="Next month"><ChevronRight class="w-5 h-5" /></button>
        <button @click="goToday" class="ml-2 px-3 py-1.5 text-xs font-bold text-emerald-700 border border-emerald-200 rounded-lg hover:bg-emerald-50">Today</button>
        <button @click="loadMonth" class="ml-2 p-2 hover:bg-slate-100 rounded-lg text-slate-400 hover:text-emerald-600" aria-label="Refresh">
          <RefreshCw class="w-4 h-4" :class="{ 'animate-spin': isLoading }" />
        </button>
      </StaffTopbar>

      <main class="flex-1 p-8 overflow-y-auto custom-scrollbar">
        <div class="max-w-7xl mx-auto grid grid-cols-1 xl:grid-cols-4 gap-6">

          <!-- CALENDAR GRID -->
          <div class="xl:col-span-3">
            <div class="flex flex-wrap items-center gap-4 mb-3 text-[11px] text-slate-500">
              <span class="flex items-center gap-1.5"><span class="w-2.5 h-2.5 rounded-full bg-amber-400"></span> Due for a vaccine</span>
              <span class="flex items-center gap-1.5"><span class="w-2.5 h-2.5 rounded-full bg-emerald-500"></span> Vaccinated</span>
              <span class="flex items-center gap-1.5"><span class="w-2.5 h-2.5 rounded-full bg-slate-300"></span> No vaccinations / closed</span>
            </div>

            <div class="bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden">
              <div class="grid grid-cols-7 bg-slate-50 border-b border-slate-200">
                <div v-for="day in ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']" :key="day"
                  class="py-3 text-center text-[10px] font-bold text-slate-400 uppercase tracking-widest">
                  {{ day }}
                </div>
              </div>

              <div class="grid grid-cols-7 auto-rows-[112px]">
                <button
                  v-for="(cell, idx) in calendarCells"
                  :key="idx"
                  type="button"
                  :disabled="!cell.day"
                  @click="cell.day && (selectedDay = cell.day)"
                  class="border-r border-b border-slate-100 p-2 text-left align-top transition-colors relative overflow-hidden"
                  :class="[
                    !cell.day ? 'bg-slate-50/40 cursor-default' : cell.closed ? 'bg-slate-50 hover:bg-slate-100' : 'hover:bg-emerald-50/40',
                    cell.day === selectedDay ? 'ring-2 ring-inset ring-emerald-500' : '',
                  ]"
                >
                  <div v-if="cell.day" class="flex items-center justify-between">
                    <span class="text-xs font-bold" :class="cell.isToday ? 'text-white bg-emerald-600 rounded-full w-6 h-6 flex items-center justify-center' : 'text-slate-500'">
                      {{ cell.day }}
                    </span>
                    <span v-if="cell.closed" class="text-[9px] font-bold uppercase text-slate-400 truncate max-w-[70%]" :title="cell.closedReason">
                      {{ cell.closedReason || 'No vaccines' }}
                    </span>
                  </div>

                  <div v-if="cell.day" class="mt-1.5 space-y-1">
                    <div v-if="cell.due.length" class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-50 text-amber-700 border border-amber-200 truncate">
                      {{ cell.due.length }} due
                    </div>
                    <div v-if="cell.given.length" class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200 truncate">
                      {{ cell.given.length }} vaccinated
                    </div>
                    <p v-for="n in cell.names" :key="n" class="text-[10px] text-slate-500 truncate">{{ n }}</p>
                  </div>
                </button>
              </div>
            </div>
          </div>

          <!-- DAY DETAILS -->
          <aside class="bg-white border border-slate-200 rounded-2xl shadow-sm p-5 h-fit xl:sticky xl:top-0">
            <p class="text-sm font-bold text-slate-800">{{ selectedLabel }}</p>
            <p v-if="selectedCell?.closed" class="mt-1 text-xs text-slate-500">{{ selectedCell.closedReason ? `Health center closed · ${selectedCell.closedReason}` : 'No vaccinations on this day' }}</p>

            <div class="mt-4">
              <p class="text-[11px] font-bold uppercase tracking-wide text-amber-700 mb-2">Due ({{ selectedCell?.due.length || 0 }})</p>
              <p v-if="!selectedCell?.due.length" class="text-xs text-slate-400">No one is due this day.</p>
              <div v-for="d in selectedCell?.due || []" :key="d.timelineID" class="py-2 border-b border-slate-50 last:border-0">
                <p class="text-xs font-semibold text-slate-800">{{ d.childName }}</p>
                <p class="text-[11px] text-slate-500">{{ d.vaccineName }} · Dose {{ d.doseNumber }}</p>
                <p v-if="d.parentName" class="text-[10.5px] text-slate-400">{{ d.parentName }}{{ d.parentContact ? ` · ${d.parentContact}` : '' }}</p>
              </div>
            </div>

            <div class="mt-5">
              <p class="text-[11px] font-bold uppercase tracking-wide text-emerald-700 mb-2">Vaccinated ({{ selectedCell?.given.length || 0 }})</p>
              <p v-if="!selectedCell?.given.length" class="text-xs text-slate-400">No vaccinations recorded this day.</p>
              <div v-for="(g, i) in selectedCell?.given || []" :key="i" class="py-2 border-b border-slate-50 last:border-0">
                <p class="text-xs font-semibold text-slate-800">{{ g.childName }}</p>
                <p class="text-[11px] text-slate-500">{{ g.vaccineName }} · Dose {{ g.doseNumber }}</p>
                <p v-if="g.administeredByName" class="text-[10.5px] text-slate-400">by {{ g.administeredByName }}</p>
              </div>
            </div>

            <router-link to="/staff/vaccine-schedule" class="mt-5 block text-center text-xs font-semibold text-emerald-700 hover:underline">
              Open Vaccine Schedule to add patients to the queue →
            </router-link>
          </aside>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import axios from 'axios'
import { ChevronLeft, ChevronRight, RefreshCw } from 'lucide-vue-next'
import StaffSidebar from './StaffSidebar.vue'
import StaffTopbar from './StaffTopbar.vue'
import { API_BASE, toISODate } from '@/utils/format'

const isLoading = ref(false)
const due = ref([])          // VaccinationTimeline/schedule rows not yet given
const given = ref([])        // VaccinationRecords/all rows
const weekly = ref([])       // ClinicOperatingSchedule
const exceptions = ref([])   // ClinicScheduleExceptions (holidays / special openings)

const today = new Date()
const viewYear = ref(today.getFullYear())
const viewMonth = ref(today.getMonth()) // 0-indexed
const selectedDay = ref(today.getDate())

const monthLabel = computed(() =>
  new Date(viewYear.value, viewMonth.value, 1).toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
)

const goPrevMonth = () => {
  if (viewMonth.value === 0) { viewMonth.value = 11; viewYear.value-- } else { viewMonth.value-- }
  selectedDay.value = 1
}
const goNextMonth = () => {
  if (viewMonth.value === 11) { viewMonth.value = 0; viewYear.value++ } else { viewMonth.value++ }
  selectedDay.value = 1
}
const goToday = () => {
  viewYear.value = today.getFullYear()
  viewMonth.value = today.getMonth()
  selectedDay.value = today.getDate()
}

async function loadMonth() {
  isLoading.value = true
  const from = toISODate(new Date(viewYear.value, viewMonth.value, 1))
  const to = toISODate(new Date(viewYear.value, viewMonth.value + 1, 0))
  try {
    const [sched, recs, week, exc] = await Promise.all([
      axios.get(`${API_BASE}/VaccinationTimeline/schedule`, { params: { from, to, includeOverdue: false } }),
      axios.get(`${API_BASE}/VaccinationRecords/all`),
      axios.get(`${API_BASE}/ClinicOperatingSchedule`),
      axios.get(`${API_BASE}/ClinicScheduleExceptions`),
    ])
    due.value = sched.data.filter(r => r.displayStatus !== 'Completed')
    given.value = recs.data
    weekly.value = week.data
    exceptions.value = exc.data.filter(e => e.isActive !== false)
  } catch (err) {
    console.error('Calendar load error:', err)
  } finally {
    isLoading.value = false
  }
}

const sameDay = (value, y, m, d) => {
  const x = new Date(value)
  return x.getFullYear() === y && x.getMonth() === m && x.getDate() === d
}

function closedInfo(date) {
  const exc = exceptions.value.find(e => sameDay(e.exceptionDate, date.getFullYear(), date.getMonth(), date.getDate()))
  if (exc) return exc.isOpen ? { closed: false } : { closed: true, reason: exc.reason || 'Closed' }
  const day = weekly.value.find(s => s.dayOfWeek === date.getDay())
  return { closed: !day || !day.isOpen, reason: '' }
}

const calendarCells = computed(() => {
  const y = viewYear.value
  const m = viewMonth.value
  const startWeekday = new Date(y, m, 1).getDay()
  const daysInMonth = new Date(y, m + 1, 0).getDate()

  const cells = []
  for (let i = 0; i < startWeekday; i++) cells.push({ day: null, due: [], given: [], names: [] })
  for (let d = 1; d <= daysInMonth; d++) {
    const date = new Date(y, m, d)
    const dayDue = due.value.filter(r => sameDay(r.scheduledDate, y, m, d))
    const dayGiven = given.value.filter(r => sameDay(r.dateAdministered ?? r.vaccinationDate, y, m, d))
    const { closed, reason } = closedInfo(date)
    const names = [...new Set([...dayDue, ...dayGiven].map(r => r.childName))].slice(0, 2)
    cells.push({
      day: d,
      isToday: sameDay(today, y, m, d),
      closed,
      closedReason: reason,
      due: dayDue,
      given: dayGiven,
      names,
    })
  }
  while (cells.length % 7 !== 0) cells.push({ day: null, due: [], given: [], names: [] })
  return cells
})

const selectedCell = computed(() => calendarCells.value.find(c => c.day === selectedDay.value))
const selectedLabel = computed(() =>
  new Date(viewYear.value, viewMonth.value, selectedDay.value || 1)
    .toLocaleDateString('en-PH', { weekday: 'long', month: 'long', day: 'numeric' }))

watch([viewYear, viewMonth], loadMonth)
onMounted(loadMonth)
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #E2E8F0; border-radius: 10px; }
</style>
