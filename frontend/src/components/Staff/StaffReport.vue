<template>
  <div class="flex h-screen bg-stone-50 antialiased text-stone-900 print:h-auto print:bg-white" style="font-family: 'Inter','Segoe UI',sans-serif;">

    <div class="print:hidden contents"><StaffSidebar /></div>

    <!-- MAIN SECTION -->
    <div class="flex-1 flex flex-col overflow-hidden print:overflow-visible">
      <div class="print:hidden">
        <StaffTopbar title="Clinic Reports" breadcrumb="Aruga / Reports">
          <select v-model="year" class="bg-white border border-slate-200 text-xs font-bold px-3 py-2 rounded-lg outline-none focus:border-emerald-500">
            <option v-for="y in yearOptions" :key="y" :value="y">Year {{ y }}</option>
          </select>
          <button @click="printReport" class="bg-emerald-600 text-white px-4 py-2 rounded-lg text-xs font-bold hover:bg-emerald-700 transition-colors flex items-center gap-2">
            <FileDown class="w-3.5 h-3.5" /> DOWNLOAD PDF
          </button>
        </StaffTopbar>
      </div>

      <main class="flex-1 p-8 overflow-y-auto custom-scrollbar print:p-0 print:overflow-visible">
        <div class="max-w-7xl mx-auto space-y-6">

          <div class="hidden print:block mb-2">
            <p class="text-xs text-slate-500">Leveriza Health Center · Aruga Pediatric Health Record System</p>
            <h1 class="text-xl font-bold">Clinic Vaccination Report — {{ year }}</h1>
          </div>

          <div v-if="loadError" class="bg-rose-50 border border-rose-100 text-rose-700 text-sm rounded-xl px-4 py-3">{{ loadError }}</div>

          <!-- KPI ROW -->
          <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
            <div v-for="k in kpis" :key="k.label" class="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm">
              <p class="text-[10px] font-bold uppercase tracking-wide text-slate-500">{{ k.label }}</p>
              <p class="text-2xl font-extrabold text-slate-800 mt-1">{{ loading ? '…' : k.value }}</p>
            </div>
          </div>

          <!-- TOP ROW: TWO CHARTS -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">

            <!-- 1. VACCINATION PER AGE -->
            <div class="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
              <div class="flex justify-between items-start mb-6">
                <div>
                  <h3 class="font-bold text-slate-800">Vaccination per Age</h3>
                  <p class="text-xs text-slate-500">Doses given in {{ year }}, by the child's age on the day</p>
                </div>
                <BarChart3 class="w-5 h-5 text-emerald-500" />
              </div>
              <div class="h-64 flex items-end justify-around gap-2 px-2">
                <div v-for="b in byAge" :key="b.label" class="w-full flex flex-col items-center justify-end h-full gap-1">
                  <span class="text-[10px] font-bold text-slate-600">{{ b.value }}</span>
                  <div class="w-full bg-emerald-100 rounded-t-lg transition-all hover:bg-emerald-500" :style="{ height: barHeight(b.value, maxAge) }"></div>
                </div>
              </div>
              <div class="flex justify-around mt-4 text-[10px] font-bold text-slate-400 uppercase">
                <span v-for="b in byAge" :key="b.label" class="w-full text-center">{{ b.label }}</span>
              </div>
            </div>

            <!-- 2. VACCINE COVERAGE REPORT -->
            <div class="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
              <div class="flex justify-between items-start mb-6">
                <div>
                  <h3 class="font-bold text-slate-800">Vaccine Coverage Report</h3>
                  <p class="text-xs text-slate-500">Total doses per vaccine type in {{ year }}</p>
                </div>
                <Syringe class="w-5 h-5 text-emerald-500" />
              </div>
              <div class="h-64 flex items-end justify-around gap-3 px-2">
                <div v-for="b in byVaccine" :key="b.label" class="w-full flex flex-col items-center justify-end h-full gap-1">
                  <span class="text-[10px] font-bold text-slate-600">{{ b.value }}</span>
                  <div class="w-full bg-slate-200 rounded-t-lg transition-all hover:bg-emerald-500" :style="{ height: barHeight(b.value, maxVaccine) }"></div>
                </div>
              </div>
              <div class="flex justify-around mt-4 text-[10px] font-bold text-slate-400 uppercase">
                <span v-for="b in byVaccine" :key="b.label" class="w-full text-center">{{ b.label }}</span>
              </div>
            </div>
          </div>

          <!-- 3. VACCINATION PER MONTH (YEAR) -->
          <div class="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
            <div class="flex justify-between items-start mb-6">
              <div>
                <h3 class="font-bold text-slate-800">Vaccination per Month ({{ year }})</h3>
                <p class="text-xs text-slate-500">Doses administered each month</p>
              </div>
              <CalendarDays class="w-5 h-5 text-emerald-500" />
            </div>
            <div class="h-64 flex items-end justify-between gap-2 px-2 border-b border-slate-100">
              <div v-for="m in byMonth" :key="m.label" class="w-full flex flex-col items-center justify-end h-full gap-1">
                <span class="text-[10px] font-bold text-slate-600">{{ m.value || '' }}</span>
                <div class="w-full bg-emerald-600/20 rounded-t transition-all hover:bg-emerald-600" :style="{ height: barHeight(m.value, maxMonth) }"></div>
              </div>
            </div>
            <div class="flex justify-between mt-4 text-[10px] font-bold text-slate-400 uppercase px-1">
              <span v-for="m in byMonth" :key="m.label" class="w-full text-center">{{ m.label }}</span>
            </div>
          </div>

          <!-- 4. INVENTORY SUMMARY -->
          <InventorySummary class="print:break-inside-avoid" />

        </div>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import axios from 'axios'
import { Syringe, FileDown, BarChart3, CalendarDays } from 'lucide-vue-next'
import StaffSidebar from './StaffSidebar.vue'
import StaffTopbar from './StaffTopbar.vue'
import InventorySummary from '@/components/Shared/InventorySummary.vue'
import { API_BASE } from '@/utils/format'

// Every chart is computed from GET /api/VaccinationRecords/all (doses
// actually given) and GET /api/Children/all (for each child's birth date).
const records = ref([])
const birthDates = ref({})
const loading = ref(true)
const loadError = ref('')

onMounted(async () => {
  try {
    const [r, c] = await Promise.all([
      axios.get(`${API_BASE}/VaccinationRecords/all`),
      axios.get(`${API_BASE}/Children/all`),
    ])
    records.value = r.data.filter(x => (x.status ?? 'Completed') === 'Completed')
    birthDates.value = Object.fromEntries(c.data.map(ch => [ch.childID, ch.birthDate]))
  } catch (e) {
    console.error('StaffReport:', e)
    loadError.value = 'Could not load report data. Check that the API is running.'
  } finally {
    loading.value = false
  }
})

const year = ref(new Date().getFullYear())
const yearOptions = computed(() => {
  const years = new Set(records.value.map(r => new Date(r.vaccinationDate).getFullYear()))
  years.add(new Date().getFullYear())
  return [...years].sort((a, b) => b - a)
})

const yearRecords = computed(() => records.value.filter(r => new Date(r.vaccinationDate).getFullYear() === year.value))

// Age of the child (in months) on the day the dose was given
function ageMonthsAt(birth, on) {
  const b = new Date(birth), d = new Date(on)
  return (d.getFullYear() - b.getFullYear()) * 12 + (d.getMonth() - b.getMonth()) - (d.getDate() < b.getDate() ? 1 : 0)
}

const AGE_BUCKETS = [
  { label: 'Birth', test: m => m < 1 },
  { label: '1–6 mo', test: m => m >= 1 && m <= 6 },
  { label: '7–12 mo', test: m => m >= 7 && m <= 12 },
  { label: '1–2 yr', test: m => m >= 13 && m <= 24 },
  { label: '2 yr+', test: m => m > 24 },
]

const byAge = computed(() => AGE_BUCKETS.map(b => ({
  label: b.label,
  value: yearRecords.value.filter(r => {
    const birth = birthDates.value[r.childID]
    return birth && b.test(ageMonthsAt(birth, r.vaccinationDate))
  }).length,
})))

const SHORT = { 'Hepatitis B': 'HepB', 'Pentavalent (DPT-HepB-Hib)': 'Penta', 'Oral Polio Vaccine': 'OPV', 'Inactivated Polio Vaccine': 'IPV', 'Pneumococcal Conjugate Vaccine': 'PCV', 'Measles, Mumps, Rubella Vaccine': 'MMR' }
const byVaccine = computed(() => {
  const map = {}
  yearRecords.value.forEach(r => { const k = SHORT[r.vaccineName] || r.vaccineName; map[k] = (map[k] || 0) + 1 })
  return Object.entries(map).map(([label, value]) => ({ label, value })).sort((a, b) => b.value - a.value)
})

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const byMonth = computed(() => MONTHS.map((label, i) => ({
  label,
  value: yearRecords.value.filter(r => new Date(r.vaccinationDate).getMonth() === i).length,
})))

const maxAge = computed(() => Math.max(1, ...byAge.value.map(b => b.value)))
const maxVaccine = computed(() => Math.max(1, ...byVaccine.value.map(b => b.value)))
const maxMonth = computed(() => Math.max(1, ...byMonth.value.map(b => b.value)))
const barHeight = (value, max) => `${Math.max(value ? 4 : 1, (value / max) * 88)}%`

const kpis = computed(() => [
  { label: `Doses Given (${year.value})`, value: yearRecords.value.length },
  { label: 'Children Vaccinated', value: new Set(yearRecords.value.map(r => r.childID)).size },
  { label: 'This Month', value: year.value === new Date().getFullYear() ? byMonth.value[new Date().getMonth()].value : '—' },
  { label: 'Most Given Vaccine', value: byVaccine.value[0]?.label || '—' },
])

function printReport() { window.print() }
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #E2E8F0; border-radius: 10px; }
</style>
