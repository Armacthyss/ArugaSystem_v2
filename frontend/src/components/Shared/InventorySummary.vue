<!--
  InventorySummary.vue — stock summary per vaccine for a chosen period
  (GET /api/VaccineInventory/summary). Used on Admin → Inventory,
  Staff → Inventory and Staff → Clinic Reports (beneficiary request).

  Per vaccine: doses on hand vs. minimum, received and used in the period,
  expiring within 30 days, expired doses still on the shelf, average use
  per week and about how many weeks the stock will last.
-->
<template>
  <section class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
    <div class="px-5 py-4 border-b border-slate-100 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h2 class="text-sm font-bold text-slate-900">Inventory Summary</h2>
        <p class="text-xs text-slate-500 mt-0.5">{{ periodLabel }} · stock on hand is as of today</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <select v-model="period" class="text-xs rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1.5 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500">
          <option>This Month</option><option>Last Month</option><option>This Year</option><option>Custom</option>
        </select>
        <template v-if="period === 'Custom'">
          <input v-model="customFrom" type="date" class="text-xs rounded-lg border border-slate-200 bg-slate-50 px-2 py-1.5" />
          <input v-model="customTo" type="date" class="text-xs rounded-lg border border-slate-200 bg-slate-50 px-2 py-1.5" />
        </template>
        <button @click="exportCsv" :disabled="!lines.length" class="text-xs font-semibold px-3 py-1.5 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:opacity-40">Export Excel</button>
      </div>
    </div>

    <div v-if="error" class="px-5 py-3 text-xs text-rose-700 bg-rose-50">{{ error }}</div>

    <!-- Totals -->
    <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 border-b border-slate-100">
      <button v-for="t in tiles" :key="t.key" type="button" @click="pickCard(t.key)" class="px-5 py-3 border-r border-slate-100 last:border-r-0" :class="cardClass(activeCard === t.key)">
        <p class="text-[10.5px] font-semibold uppercase tracking-wide text-slate-500">{{ t.label }}</p>
        <p class="text-xl font-extrabold mt-0.5" :class="t.tone">{{ loading ? '…' : t.value }}</p>
      </button>
    </div>

    <div class="overflow-x-auto">
      <table class="w-full text-sm">
        <thead>
          <tr class="bg-slate-50/60 text-[11px] uppercase tracking-wide text-slate-500">
            <SortableTh :sort="lineSort" column="vaccineName" class="text-left font-semibold px-5 py-2.5">Vaccine</SortableTh>
            <SortableTh :sort="lineSort" column="onHand" class="text-right font-semibold px-3 py-2.5">On Hand</SortableTh>
            <SortableTh :sort="lineSort" column="minimumStock" class="text-right font-semibold px-3 py-2.5">Minimum</SortableTh>
            <SortableTh :sort="lineSort" column="receivedInPeriod" class="text-right font-semibold px-3 py-2.5">Received</SortableTh>
            <SortableTh :sort="lineSort" column="usedInPeriod" class="text-right font-semibold px-3 py-2.5">Used</SortableTh>
            <SortableTh v-if="anyRemoved" :sort="lineSort" column="removedInPeriod" class="text-right font-semibold px-3 py-2.5" title="Broken, spoiled, expired and thrown away, lost, or a lower recount">Removed</SortableTh>
            <SortableTh :sort="lineSort" column="expiringSoon" class="text-right font-semibold px-3 py-2.5">Expiring ≤30d</SortableTh>
            <SortableTh :sort="lineSort" column="expiredOnShelf" class="text-right font-semibold px-3 py-2.5">Expired</SortableTh>
            <SortableTh :sort="lineSort" column="averageWeeklyUse" class="text-right font-semibold px-3 py-2.5" title="Average doses given per week over the last 8 weeks">Avg / Week</SortableTh>
            <SortableTh :sort="lineSort" column="weeksLeft" class="text-right font-semibold px-3 py-2.5" title="On hand ÷ average weekly use">Lasts About</SortableTh>
            <SortableTh :sort="lineSort" column="status" class="text-left font-semibold px-5 py-2.5">Status</SortableTh>
          </tr>
        </thead>
        <tbody>
          <tr v-for="l in lineSort.sortedRows.value" :key="l.vaccineID" class="border-t border-slate-100">
            <td class="px-5 py-2.5">
              <p class="font-semibold text-slate-800">{{ l.vaccineName }}</p>
              <p class="text-[11px] text-slate-400">{{ l.activeBatches }} batch{{ l.activeBatches === 1 ? '' : 'es' }} in stock<span v-if="l.nextExpiry"> · next expiry {{ formatDate(l.nextExpiry) }}</span></p>
            </td>
            <td class="px-3 py-2.5 text-right font-bold text-slate-900">{{ l.onHand }}</td>
            <td class="px-3 py-2.5 text-right text-slate-500">{{ l.minimumStock }}</td>
            <td class="px-3 py-2.5 text-right text-slate-600">{{ l.receivedInPeriod }}</td>
            <td class="px-3 py-2.5 text-right text-slate-600">{{ l.usedInPeriod }}</td>
            <td v-if="anyRemoved" class="px-3 py-2.5 text-right" :class="l.removedInPeriod ? 'font-semibold text-rose-700' : 'text-slate-400'">{{ l.removedInPeriod }}</td>
            <td class="px-3 py-2.5 text-right" :class="l.expiringSoon ? 'font-semibold text-orange-700' : 'text-slate-400'">{{ l.expiringSoon }}</td>
            <td class="px-3 py-2.5 text-right" :class="l.expiredOnShelf ? 'font-semibold text-red-700' : 'text-slate-400'">{{ l.expiredOnShelf }}</td>
            <td class="px-3 py-2.5 text-right text-slate-600">{{ l.averageWeeklyUse }}</td>
            <td class="px-3 py-2.5 text-right text-slate-600">{{ lastsLabel(l) }}</td>
            <td class="px-5 py-2.5">
              <span class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap" :class="statusStyle[l.status]">
                <span class="w-1.5 h-1.5 rounded-full" :class="statusDot[l.status]"></span>{{ l.status }}
              </span>
            </td>
          </tr>
          <tr v-if="!loading && !lines.length">
            <td :colspan="anyRemoved ? 11 : 10" class="px-5 py-8 text-center text-sm text-slate-400">No vaccines in stock yet.</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p class="px-5 py-2.5 text-[11px] text-slate-400 border-t border-slate-100">
      Received = doses in batches received in the period. Used = doses given from a clinic batch in the period.
      <template v-if="anyRemoved">Removed = doses taken off with Adjust Stock (broken, spoiled, lost, recount).</template>
      Expired = doses in expired batches that haven't been deactivated yet.
    </p>
  </section>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import axios from 'axios'
import { API_BASE, formatDate, toISODate, downloadCSV } from '@/utils/format'
import SortableTh from '@/components/Shared/SortableTh.vue'
import { useTableSort, useCardFilter, cardClass } from '@/utils/tableSort'

const props = defineProps({
  // Bump this after receiving or editing a batch to reload the summary
  refreshKey: { type: Number, default: 0 },
})

const period = ref('This Month')
const customFrom = ref('')
const customTo = ref('')
const lines = ref([])
const totals = ref({})
const loading = ref(false)
const error = ref('')

function range() {
  const now = new Date()
  switch (period.value) {
    case 'Last Month': return { from: new Date(now.getFullYear(), now.getMonth() - 1, 1), to: new Date(now.getFullYear(), now.getMonth(), 0) }
    case 'This Year': return { from: new Date(now.getFullYear(), 0, 1), to: now }
    case 'Custom': return { from: customFrom.value ? new Date(customFrom.value) : new Date(now.getFullYear(), now.getMonth(), 1), to: customTo.value ? new Date(customTo.value) : now }
    default: return { from: new Date(now.getFullYear(), now.getMonth(), 1), to: now }
  }
}

const periodLabel = computed(() => {
  const { from, to } = range()
  return `${formatDate(from)} – ${formatDate(to)}`
})

async function load() {
  loading.value = true
  error.value = ''
  const { from, to } = range()
  try {
    const res = await axios.get(`${API_BASE}/VaccineInventory/summary`, { params: { from: toISODate(from), to: toISODate(to) } })
    lines.value = res.data.lines
    totals.value = res.data.totals
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not load the inventory summary.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch([period, customFrom, customTo, () => props.refreshKey], load)

const tiles = computed(() => [
  { key: 'all', label: 'Doses On Hand', value: totals.value.onHand ?? 0, tone: 'text-slate-900' },
  { key: 'received', label: 'Received', value: totals.value.received ?? 0, tone: 'text-slate-900' },
  { key: 'used', label: 'Used', value: totals.value.used ?? 0, tone: 'text-slate-900' },
  { key: 'expiring', label: 'Expiring ≤30 Days', value: totals.value.expiringSoon ?? 0, tone: totals.value.expiringSoon ? 'text-orange-700' : 'text-slate-900' },
  { key: 'expired', label: 'Expired on Shelf', value: totals.value.expiredOnShelf ?? 0, tone: totals.value.expiredOnShelf ? 'text-red-700' : 'text-slate-900' },
  { key: 'low', label: 'Low / Out of Stock', value: totals.value.lowOrOut ?? 0, tone: totals.value.lowOrOut ? 'text-amber-700' : 'text-slate-900' },
])

// Click a total to list only those vaccines, a heading to sort
const { activeCard, pickCard, applyCard } = useCardFilter({
  received: (l) => l.receivedInPeriod > 0,
  used: (l) => l.usedInPeriod > 0,
  expiring: (l) => l.expiringSoon > 0,
  expired: (l) => l.expiredOnShelf > 0,
  low: (l) => l.status !== 'Good',
})
const lineSort = useTableSort(computed(() => applyCard(lines.value)))

// The Removed column only shows when Adjust Stock was used in the period
const anyRemoved = computed(() => lines.value.some(l => l.removedInPeriod > 0))

const statusStyle = { Good: 'bg-emerald-50 text-emerald-700', 'Low Stock': 'bg-amber-50 text-amber-700', 'Out of Stock': 'bg-red-50 text-red-700' }
const statusDot = { Good: 'bg-emerald-500', 'Low Stock': 'bg-amber-500', 'Out of Stock': 'bg-red-600' }

function lastsLabel(l) {
  if (l.weeksLeft == null) return l.onHand ? 'No recent use' : '—'
  if (l.weeksLeft < 1) return 'Under 1 week'
  if (l.weeksLeft > 52) return 'Over 1 year'
  return `${Math.floor(l.weeksLeft)} week${Math.floor(l.weeksLeft) === 1 ? '' : 's'}`
}

function exportCsv() {
  downloadCSV(`inventory-summary-${toISODate()}.csv`, [
    ['Inventory Summary'], [`Period: ${periodLabel.value}`], [],
    ['Vaccine', 'On Hand', 'Minimum', 'Received', 'Used', 'Removed', 'Expiring <=30 days', 'Expired on shelf', 'Avg per week', 'Lasts about', 'Next expiry', 'Status'],
    ...lines.value.map(l => [l.vaccineName, l.onHand, l.minimumStock, l.receivedInPeriod, l.usedInPeriod, l.removedInPeriod ?? 0, l.expiringSoon, l.expiredOnShelf, l.averageWeeklyUse, lastsLabel(l), l.nextExpiry ? formatDate(l.nextExpiry) : '', l.status]),
  ])
}
</script>
