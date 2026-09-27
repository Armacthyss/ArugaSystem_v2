<!--
  VaccinationCard.vue — printable immunization record / certificate.
  Route: /print/vaccination-card/:childId   (any signed-in role)

  Opens in its own tab and lays the child's record out on one page:
  details, every scheduled dose with the date given, batch and health
  worker, and the next due dose. "Print" uses the browser's print
  dialog; "Download PDF" saves a PDF file directly (?download=1 does
  this automatically when the page opens).
-->
<template>
  <div class="min-h-screen bg-slate-100 py-8 print:bg-white print:py-0">
    <!-- Toolbar (hidden when printing) -->
    <div class="max-w-4xl mx-auto mb-4 flex items-center justify-between px-4 print:hidden">
      <button @click="goBack" class="text-sm text-slate-600 hover:text-slate-900">← Back</button>
      <div class="flex items-center gap-2">
        <button @click="downloadPdf" :disabled="loading || !!loadError || downloading"
          class="text-sm font-semibold px-4 py-2 rounded-lg border border-emerald-700 text-emerald-800 bg-white hover:bg-emerald-50 disabled:opacity-50">
          {{ downloading ? 'Preparing PDF...' : 'Download PDF' }}
        </button>
        <button @click="printCard" :disabled="loading || !!loadError"
          class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-700 text-white hover:bg-emerald-800 disabled:opacity-50">
          Print
        </button>
      </div>
    </div>

    <div ref="cardEl" class="max-w-4xl mx-auto bg-white shadow-sm border border-slate-200 rounded-xl p-8 print:shadow-none print:border-0 print:rounded-none print:p-0">
      <div v-if="loading" class="py-20 text-center text-sm text-slate-400">Loading record...</div>
      <div v-else-if="loadError" class="py-20 text-center text-sm text-rose-600">{{ loadError }}</div>

      <template v-else>
        <!-- Header -->
        <div class="flex items-center justify-between border-b-2 border-emerald-800 pb-4 mb-5">
          <div class="flex items-center gap-3">
            <img :src="logoIcon" alt="" class="w-12 h-12" />
            <div>
              <p class="text-[11px] uppercase tracking-widest text-slate-500">Republic of the Philippines · Department of Health</p>
              <h1 class="text-xl font-extrabold text-emerald-900">Leveriza Health Center</h1>
              <p class="text-xs text-slate-500">Aruga Pediatric Health Record System</p>
            </div>
          </div>
          <div class="text-right">
            <p class="text-lg font-bold text-slate-900">Child Immunization Record</p>
            <p class="text-xs text-slate-500">Printed {{ printedOn }}</p>
          </div>
        </div>

        <!-- Child details -->
        <div class="grid grid-cols-3 gap-x-6 gap-y-3 text-sm mb-6">
          <div class="col-span-2"><p class="text-[10px] uppercase tracking-wide text-slate-500">Name of Child</p><p class="font-semibold text-slate-900">{{ fullName }}</p></div>
          <div><p class="text-[10px] uppercase tracking-wide text-slate-500">Family No. / Barangay</p><p class="text-slate-900">{{ child.familyNo || '—' }} / {{ child.barangay ?? '—' }}</p></div>
          <div><p class="text-[10px] uppercase tracking-wide text-slate-500">Date of Birth</p><p class="text-slate-900">{{ formatDate(child.birthDate) }}</p></div>
          <div><p class="text-[10px] uppercase tracking-wide text-slate-500">Sex</p><p class="text-slate-900">{{ child.sex || '—' }}</p></div>
          <div><p class="text-[10px] uppercase tracking-wide text-slate-500">Place of Birth</p><p class="text-slate-900">{{ child.placeOfBirth || '—' }}</p></div>
          <div class="col-span-2"><p class="text-[10px] uppercase tracking-wide text-slate-500">Parent / Guardian</p><p class="text-slate-900">{{ guardianText }}</p></div>
          <div><p class="text-[10px] uppercase tracking-wide text-slate-500">Birth Weight / Length</p><p class="text-slate-900">{{ child.birthWeight ? child.birthWeight + ' kg' : '—' }} / {{ child.birthHeight ? child.birthHeight + ' cm' : '—' }}</p></div>
          <div class="col-span-3"><p class="text-[10px] uppercase tracking-wide text-slate-500">Address</p><p class="text-slate-900">{{ child.address || '—' }}</p></div>
          <div v-if="child.allergies" class="col-span-3"><p class="text-[10px] uppercase tracking-wide text-slate-500">Allergies</p><p class="text-rose-700 font-medium">{{ child.allergies }}</p></div>
        </div>

        <!-- Dose table -->
        <table class="w-full text-sm border border-slate-300">
          <thead class="bg-emerald-50">
            <tr>
              <th class="border border-slate-300 px-3 py-2 text-left text-xs">Vaccine</th>
              <th class="border border-slate-300 px-3 py-2 text-center text-xs">Dose</th>
              <th class="border border-slate-300 px-3 py-2 text-left text-xs">Scheduled</th>
              <th class="border border-slate-300 px-3 py-2 text-left text-xs">Date Given</th>
              <th class="border border-slate-300 px-3 py-2 text-left text-xs">Lot No.</th>
              <th class="border border-slate-300 px-3 py-2 text-left text-xs">Given By</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in rows" :key="row.key">
              <td class="border border-slate-300 px-3 py-1.5">{{ row.vaccineName }}</td>
              <td class="border border-slate-300 px-3 py-1.5 text-center">{{ row.doseNumber }}</td>
              <td class="border border-slate-300 px-3 py-1.5 text-slate-500">{{ formatDate(row.scheduledDate) }}</td>
              <td class="border border-slate-300 px-3 py-1.5 font-medium" :class="row.given ? 'text-emerald-800' : (row.overdue ? 'text-rose-600' : 'text-slate-400')">
                {{ row.given ? formatDate(row.givenOn) : (row.overdue ? 'Overdue' : '—') }}
              </td>
              <td class="border border-slate-300 px-3 py-1.5 font-mono text-xs">{{ row.lotNumber || '' }}</td>
              <td class="border border-slate-300 px-3 py-1.5 text-xs">{{ row.givenBy || '' }}</td>
            </tr>
            <tr v-if="rows.length === 0">
              <td colspan="6" class="border border-slate-300 px-3 py-6 text-center text-slate-400">No vaccination schedule found for this child.</td>
            </tr>
          </tbody>
        </table>

        <!-- Summary -->
        <div class="mt-5 grid grid-cols-3 gap-4 text-sm">
          <div class="rounded-lg border border-slate-200 p-3">
            <p class="text-[10px] uppercase tracking-wide text-slate-500">Doses Completed</p>
            <p class="text-lg font-bold text-slate-900">{{ givenCount }} of {{ rows.length }}</p>
          </div>
          <div class="rounded-lg border border-slate-200 p-3">
            <p class="text-[10px] uppercase tracking-wide text-slate-500">Status</p>
            <p class="text-lg font-bold" :class="statusClass">{{ statusLabel }}</p>
          </div>
          <div class="rounded-lg border border-slate-200 p-3">
            <p class="text-[10px] uppercase tracking-wide text-slate-500">Next Due</p>
            <p class="font-semibold text-slate-900">{{ nextDue ? `${nextDue.vaccineName} D${nextDue.doseNumber}` : '—' }}</p>
            <p class="text-xs text-slate-500">{{ nextDue ? formatDate(nextDue.scheduledDate) : '' }}</p>
          </div>
        </div>

        <div class="mt-10 grid grid-cols-2 gap-10 text-xs text-slate-500">
          <div class="border-t border-slate-400 pt-1">Signature over printed name of Health Worker</div>
          <div class="border-t border-slate-400 pt-1">Date</div>
        </div>
        <p class="mt-6 text-[10px] text-slate-400 text-center">
          This record is generated from the Aruga system. Clinic hours and next schedules may change — check the Aruga parent portal for updates.
        </p>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import axios from 'axios'
import logoIcon from '@/assets/logo-icon.svg'
import { API_BASE, formatDate, toISODate, withRelationship, guardiansOf } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const childId = route.params.childId

const child = ref({})

// "Maria Santos (Mother), Rosario Santos (Grandmother)"
const guardianText = computed(() => {
  const list = guardiansOf(child.value.parents)
  return list.length ? list.map(g => withRelationship(g.name, g.relationship)).join(', ') : (child.value.parentName || '—')
})
const timeline = ref([])
const records = ref([])
const loading = ref(true)
const loadError = ref('')
const printedOn = formatDate(new Date())

onMounted(async () => {
  try {
    const [c, t, r] = await Promise.all([
      axios.get(`${API_BASE}/Children/${childId}`),
      axios.get(`${API_BASE}/VaccinationTimeline/child/${childId}`),
      axios.get(`${API_BASE}/VaccinationRecords/child/${childId}`),
    ])
    child.value = c.data
    timeline.value = t.data
    records.value = r.data.filter(x => (x.status ?? 'Completed') === 'Completed')
    document.title = `Immunization Record — ${fullName.value}`
  } catch (e) {
    console.error('VaccinationCard:', e)
    loadError.value = 'Could not load this child\'s record.'
  } finally {
    loading.value = false
  }
  if (loadError.value) return
  if (route.query.download === '1') { await nextTick(); downloadPdf() }
  else if (route.query.print === '1') setTimeout(() => window.print(), 400)
})

const fullName = computed(() => [child.value.firstName, child.value.middleName, child.value.lastName].filter(Boolean).join(' '))

// One row per scheduled dose, plus any recorded dose that isn't on the
// timeline (e.g. a walk-in extra dose).
const rows = computed(() => {
  const today = toISODate()
  const byKey = new Map(records.value.map(r => [`${r.vaccineID}-${r.doseNumber}`, r]))
  const list = timeline.value.map(t => {
    const rec = byKey.get(`${t.vaccineID}-${t.doseNumber}`)
    byKey.delete(`${t.vaccineID}-${t.doseNumber}`)
    return {
      key: `${t.vaccineID}-${t.doseNumber}`,
      vaccineName: t.vaccineName,
      doseNumber: t.doseNumber,
      scheduledDate: t.scheduledDate,
      given: !!rec || t.status === 'Completed',
      givenOn: rec?.vaccinationDate,
      lotNumber: rec?.lotNumber,
      givenBy: rec?.administeredByName,
      overdue: !rec && t.status !== 'Completed' && String(t.scheduledDate).slice(0, 10) < today,
    }
  })
  for (const rec of byKey.values()) {
    list.push({
      key: `${rec.vaccineID}-${rec.doseNumber}`, vaccineName: rec.vaccineName, doseNumber: rec.doseNumber,
      scheduledDate: null, given: true, givenOn: rec.vaccinationDate, lotNumber: rec.lotNumber, givenBy: rec.administeredByName, overdue: false,
    })
  }
  return list.sort((a, b) => new Date(a.scheduledDate || a.givenOn) - new Date(b.scheduledDate || b.givenOn))
})

const givenCount = computed(() => rows.value.filter(r => r.given).length)
const overdueCount = computed(() => rows.value.filter(r => r.overdue).length)
const nextDue = computed(() => rows.value.find(r => !r.given && !r.overdue) || rows.value.find(r => !r.given))

const statusLabel = computed(() =>
  rows.value.length && givenCount.value === rows.value.length ? 'Fully Immunized'
    : overdueCount.value ? `${overdueCount.value} Overdue`
    : 'On Schedule')
const statusClass = computed(() =>
  statusLabel.value === 'Fully Immunized' ? 'text-emerald-700' : overdueCount.value ? 'text-rose-600' : 'text-sky-700')

function printCard() { window.print() }

// Renders the card to an image and saves it as an A4 PDF file
// (continues onto more pages if the dose table is long).
const cardEl = ref(null)
const downloading = ref(false)
async function downloadPdf() {
  if (!cardEl.value || downloading.value) return
  downloading.value = true
  try {
    const [{ default: html2canvas }, { jsPDF }] = await Promise.all([import('html2canvas-pro'), import('jspdf')])
    const canvas = await html2canvas(cardEl.value, { scale: 2, backgroundColor: '#ffffff', useCORS: true })
    const pdf = new jsPDF({ unit: 'mm', format: 'a4' })
    const margin = 10
    const fullW = pdf.internal.pageSize.getWidth()
    const pageW = fullW - margin * 2
    const pageH = pdf.internal.pageSize.getHeight() - margin * 2
    const imgH = canvas.height * pageW / canvas.width
    const img = canvas.toDataURL('image/png')
    for (let y = 0; y < imgH; y += pageH) {
      if (y > 0) pdf.addPage()
      pdf.addImage(img, 'PNG', margin, margin - y, pageW, imgH)
      // White out the top/bottom margins so each page shows only its own slice.
      pdf.setFillColor(255, 255, 255)
      pdf.rect(0, 0, fullW, margin, 'F')
      pdf.rect(0, margin + pageH, fullW, margin, 'F')
    }
    const safeName = (fullName.value || 'Child').replace(/[^A-Za-z0-9]+/g, '_')
    pdf.save(`Immunization_Record_${safeName}.pdf`)
  } catch (e) {
    console.error('VaccinationCard PDF:', e)
    alert('Could not create the PDF. You can use Print and choose "Save as PDF" instead.')
  } finally {
    downloading.value = false
  }
}
function goBack() {
  if (window.history.length > 1) router.back()
  else window.close()
}
</script>

<style>
@media print {
  @page { size: A4; margin: 14mm; }
}
</style>
