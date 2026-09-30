<script setup>
import { ref, computed, reactive, watch, onMounted } from 'vue'
import axios from 'axios'
import AppSidebar from './Components/AppSidebar.vue'
import AppHeader from './Components/AppHeader.vue'
import {
  API_BASE, ageLabel, formatDate, formatDateTime, toISODate, downloadCSV, summarizeStock, userLevel,
} from '@/utils/format'

/* -------------------------------- Source data -------------------------------- */
// Loaded once; every report below is computed from these in the browser.
const data = reactive({
  children: [], records: [], inventory: [], vaccines: [], accounts: [],
  queues: [], notifications: [], audit: [], schedule: [],
})
const loading = ref(true)
const loadErrors = ref([])

async function load(key, url, params) {
  try {
    const res = await axios.get(`${API_BASE}${url}`, { params })
    data[key] = res.data
  } catch (e) {
    console.error(`Reports: ${url}`, e)
    loadErrors.value.push(url)
  }
}

onMounted(async () => {
  const yearAgo = new Date(); yearAgo.setFullYear(yearAgo.getFullYear() - 1)
  const inSixtyDays = new Date(); inSixtyDays.setDate(inSixtyDays.getDate() + 60)
  await Promise.all([
    load('children', '/Children/overview'),
    load('records', '/VaccinationRecords/all'),
    load('inventory', '/VaccineInventory'),
    load('vaccines', '/Vaccines'),
    load('accounts', '/accounts'),
    load('queues', '/Queue'),
    load('notifications', '/Notifications', { from: toISODate(yearAgo) }),
    load('schedule', '/VaccinationTimeline/schedule', { from: toISODate(), to: toISODate(inSixtyDays), includeOverdue: true }),
  ])
  loading.value = false
  // Show the selected report straight away instead of an empty preview
  generateReport()
})

const completed = computed(() => data.records.filter(r => (r.status ?? 'Completed') === 'Completed'))

/* -------------------------------- Summary ---------------------------------- */
const summary = computed(() => {
  const now = new Date()
  const thisMonth = completed.value.filter(r => {
    const d = new Date(r.vaccinationDate)
    return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth()
  }).length
  const withSchedule = data.children.filter(c => c.totalDoses > 0)
  const fully = withSchedule.filter(c => c.vaccinationStatus === 'Fully Vaccinated').length
  const onTrack = withSchedule.filter(c => c.vaccinationStatus !== 'Delayed').length
  const stock = summarizeStock(data.inventory, data.vaccines)
  return [
    { label: 'Vaccinations This Month', value: thisMonth, icon: '💉', tint: 'bg-emerald-50' },
    { label: 'Total Registered Children', value: data.children.length, icon: '🧒', tint: 'bg-teal-50' },
    { label: 'On-Schedule Rate', value: withSchedule.length ? `${Math.round(onTrack * 100 / withSchedule.length)}%` : '—', icon: '📈', tint: 'bg-sky-50' },
    { label: 'Delayed Vaccinations', value: data.children.reduce((s, c) => s + (c.overdueDoses || 0), 0), icon: '⚠️', tint: 'bg-rose-50' },
    { label: 'Available Doses', value: stock.reduce((s, v) => s + v.stock, 0), icon: '📦', tint: 'bg-amber-50' },
    { label: 'Fully Vaccinated', value: fully, icon: '✅', tint: 'bg-violet-50' },
  ]
})

/* -------------------------------- Report categories -------------------------------- */
const categories = [
  { name: 'Patient Reports', icon: '🧒', reports: ['Registered Children', 'Children by Vaccination Status', 'Parent-Child Relationship Report'] },
  { name: 'Vaccination Reports', icon: '💉', reports: ['Vaccinations Performed', 'Vaccinations by Month', 'Vaccinations by Vaccine', 'Children Fully Vaccinated', 'Delayed Vaccinations', 'Upcoming Vaccinations'] },
  { name: 'Inventory Reports', icon: '📦', reports: ['Inventory Summary', 'Current Inventory', 'Stock Received', 'Low Stock Vaccines', 'Expired & Expiring Batches'] },
  { name: 'Queue Reports', icon: '🎫', reports: ['Daily Queue Summary'] },
  { name: 'Notification Reports', icon: '🔔', reports: ['Notifications Sent', 'Announcement History'] },
  { name: 'User Reports', icon: '👥', reports: ['Registered Parents', 'Doctors & Nurses'] },
  // Audit Logs and Login History moved to the Super Admin's Audit Logs page
]

const expandedCategory = ref('Vaccination Reports')
const toggleCategory = (name) => (expandedCategory.value = expandedCategory.value === name ? null : name)

const selectedReport = ref('Vaccinations by Month')
const selectReport = (name) => {
  selectedReport.value = name
}

/* -------------------------------- Filters -------------------------------- */
const filters = reactive({
  period: 'This Month', customFrom: '', customTo: '', barangay: 'All Barangays', vaccine: 'All Vaccines', ageGroups: [],
})

const barangayOptions = computed(() => ['All Barangays', ...[...new Set(data.children.map(c => c.barangay).filter(Boolean))].sort((a, b) => a - b).map(String)])
const vaccineOptions = computed(() => ['All Vaccines', ...data.vaccines.map(v => v.vaccineName)])
const ageGroupOptions = ['0–6 Months', '7–12 Months', '1–2 Years', '3–5 Years']

const toggleAgeGroup = (group) => {
  const idx = filters.ageGroups.indexOf(group)
  if (idx === -1) filters.ageGroups.push(group)
  else filters.ageGroups.splice(idx, 1)
}

const resetFilters = () => {
  Object.assign(filters, { period: 'This Month', customFrom: '', customTo: '', barangay: 'All Barangays', vaccine: 'All Vaccines', ageGroups: [] })
}

function periodRange() {
  const today = new Date(); today.setHours(0, 0, 0, 0)
  const end = new Date(today); end.setDate(end.getDate() + 1)
  switch (filters.period) {
    case 'Today': return { start: today, end, label: formatDate(today) }
    case 'This Week': { const s = new Date(today); s.setDate(today.getDate() - today.getDay()); return { start: s, end, label: `${formatDate(s)} – ${formatDate(today)}` } }
    case 'This Month': { const s = new Date(today.getFullYear(), today.getMonth(), 1); return { start: s, end, label: today.toLocaleDateString('en-PH', { month: 'long', year: 'numeric' }) } }
    case 'This Year': { const s = new Date(today.getFullYear(), 0, 1); return { start: s, end, label: String(today.getFullYear()) } }
    case 'All Time': return { start: new Date(2000, 0, 1), end: new Date(2100, 0, 1), label: 'All records' }
    default: {
      const s = filters.customFrom ? new Date(filters.customFrom) : new Date(2000, 0, 1)
      const e = filters.customTo ? new Date(filters.customTo) : new Date(today)
      e.setDate(e.getDate() + 1)
      return { start: s, end: e, label: `${filters.customFrom || '—'} to ${filters.customTo || '—'}` }
    }
  }
}

const inPeriod = (value, range) => { const d = new Date(value); return d >= range.start && d < range.end }

function ageMonths(birth) {
  const b = new Date(birth), n = new Date()
  return (n.getFullYear() - b.getFullYear()) * 12 + (n.getMonth() - b.getMonth()) - (n.getDate() < b.getDate() ? 1 : 0)
}
function ageGroupOf(birth) {
  const m = ageMonths(birth)
  if (m <= 6) return '0–6 Months'
  if (m <= 12) return '7–12 Months'
  if (m <= 24) return '1–2 Years'
  return '3–5 Years'
}

// Child-level filters (barangay + age group) shared by several reports
const childById = computed(() => Object.fromEntries(data.children.map(c => [c.childID, c])))
function childPasses(childId) {
  const c = childById.value[childId]
  if (!c) return filters.barangay === 'All Barangays' && !filters.ageGroups.length
  if (filters.barangay !== 'All Barangays' && String(c.barangay) !== filters.barangay) return false
  if (filters.ageGroups.length && !filters.ageGroups.includes(ageGroupOf(c.birthDate))) return false
  return true
}
const vaccinePasses = name => filters.vaccine === 'All Vaccines' || name === filters.vaccine

/* -------------------------------- Report engine -------------------------------- */
// Each report returns { columns, rows, chart?: { title, points: [{label, value}] } }
function monthlyChart(dates, title = 'Monthly Trend') {
  const now = new Date()
  const points = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - 5 + i, 1)
    return {
      label: d.toLocaleDateString('en-PH', { month: 'short' }),
      value: dates.filter(x => { const v = new Date(x); return v.getFullYear() === d.getFullYear() && v.getMonth() === d.getMonth() }).length,
    }
  })
  return { title, points }
}
function countChart(items, keyFn, title) {
  const map = {}
  items.forEach(i => { const k = keyFn(i) || '—'; map[k] = (map[k] || 0) + 1 })
  return { title, points: Object.entries(map).sort((a, b) => b[1] - a[1]).slice(0, 8).map(([label, value]) => ({ label, value })) }
}

// Totals shown above an inventory report
function inventoryTiles(batches) {
  const today = toISODate()
  const usable = batches.filter(i => i.status && String(i.expirationDate).slice(0, 10) >= today)
  const days = i => (new Date(i.expirationDate) - new Date()) / 86400000
  return [
    { label: 'Doses On Hand', value: usable.reduce((s, i) => s + i.currentQuantity, 0) },
    { label: 'Batches In Stock', value: usable.filter(i => i.currentQuantity > 0).length },
    { label: 'Low / Out of Stock Batches', value: usable.filter(i => i.currentQuantity < i.minimumStock).length },
    { label: 'Doses Expiring ≤30 Days', value: usable.filter(i => days(i) <= 30).reduce((s, i) => s + i.currentQuantity, 0) },
    { label: 'Expired Doses on Shelf', value: batches.filter(i => i.status && String(i.expirationDate).slice(0, 10) < today).reduce((s, i) => s + i.currentQuantity, 0) },
  ]
}

// Inventory Summary: per vaccine, from GET /api/VaccineInventory/summary
async function buildInventorySummary(range) {
  const to = new Date(range.end); to.setDate(to.getDate() - 1)
  const from = range.start.getFullYear() < 2001 ? new Date(2020, 0, 1) : range.start
  const res = await axios.get(`${API_BASE}/VaccineInventory/summary`, { params: { from: toISODate(from), to: toISODate(to) } })
  const lines = res.data.lines.filter(l => vaccinePasses(l.vaccineName))
  const t = res.data.totals
  const lasts = l => l.weeksLeft == null ? (l.onHand ? 'No recent use' : '—') : l.weeksLeft < 1 ? 'Under 1 week' : l.weeksLeft > 52 ? 'Over 1 year' : `${Math.floor(l.weeksLeft)} week(s)`
  return {
    tiles: [
      { label: 'Doses On Hand', value: t.onHand },
      { label: 'Received in Period', value: t.received },
      { label: 'Used in Period', value: t.used },
      { label: 'Expiring ≤30 Days', value: t.expiringSoon },
      { label: 'Expired on Shelf', value: t.expiredOnShelf },
      { label: 'Low / Out of Stock Vaccines', value: t.lowOrOut },
    ],
    columns: ['Vaccine', 'On Hand', 'Minimum', 'Received', 'Used', 'Expiring ≤30d', 'Expired', 'Avg / Week', 'Lasts About', 'Next Expiry', 'Status'],
    rows: lines.map(l => [l.vaccineName, l.onHand, l.minimumStock, l.receivedInPeriod, l.usedInPeriod, l.expiringSoon, l.expiredOnShelf, l.averageWeeklyUse, lasts(l), l.nextExpiry ? formatDate(l.nextExpiry) : '—', l.status]),
    chart: { title: 'Doses On Hand per Vaccine', points: lines.map(l => ({ label: l.abbreviation || l.vaccineName, value: l.onHand })) },
  }
}

function buildReport(name, range) {
  const recs = completed.value.filter(r => childPasses(r.childID) && vaccinePasses(r.vaccineName))
  const kids = data.children.filter(c => childPasses(c.childID))

  switch (name) {
    case 'Registered Children': {
      const list = kids.filter(c => inPeriod(c.createdAt, range))
      return {
        columns: ['Patient ID', 'Child', 'Birth Date', 'Age', 'Sex', 'Barangay', 'Parent / Guardian', 'Registered'],
        rows: list.map(c => [`PT-${c.childID.slice(-8).toUpperCase()}`, `${c.firstName} ${c.lastName}`, formatDate(c.birthDate), ageLabel(c.birthDate), c.sex || '—', c.barangay ?? '—', c.parentName || '—', formatDate(c.createdAt)]),
        chart: monthlyChart(kids.map(c => c.createdAt), 'Registrations per Month'),
      }
    }
    case 'Children by Vaccination Status':
      return {
        columns: ['Child', 'Age', 'Status', 'Doses Completed', 'Overdue Doses', 'Next Vaccine', 'Next Due'],
        rows: kids.map(c => [`${c.firstName} ${c.lastName}`, ageLabel(c.birthDate), c.vaccinationStatus, `${c.completedDoses}/${c.totalDoses}`, c.overdueDoses, c.nextVaccine || '—', c.nextDueDate ? formatDate(c.nextDueDate) : '—']),
        chart: countChart(kids, c => c.vaccinationStatus, 'Children per Status'),
      }
    case 'Parent-Child Relationship Report':
      return {
        columns: ['Child', 'Parent / Guardian', 'Relationship', 'Primary Contact', 'Contact No.', 'Email'],
        rows: kids.flatMap(c => (c.parents?.length ? c.parents : [null]).map(p => [
          `${c.firstName} ${c.lastName}`, p?.name || 'Not linked', p?.relationshipType || '—', p?.isPrimaryContact ? 'Yes' : 'No', p?.contactNo || '—', p?.email || '—',
        ])),
      }
    case 'Vaccinations Performed': {
      const list = recs.filter(r => inPeriod(r.vaccinationDate, range)).sort((a, b) => new Date(b.vaccinationDate) - new Date(a.vaccinationDate))
      return {
        columns: ['Date', 'Child', 'Vaccine', 'Dose', 'Site', 'Lot No.', 'Administered By', 'Remarks'],
        rows: list.map(r => [formatDate(r.vaccinationDate), r.childName, r.vaccineName, r.doseNumber, r.injectionSite || '—', r.lotNumber || 'Historical', r.administeredByName || '—', r.nurseObservation || '']),
        chart: monthlyChart(recs.map(r => r.vaccinationDate), 'Vaccinations per Month'),
      }
    }
    case 'Vaccinations by Month': {
      const list = recs.filter(r => inPeriod(r.vaccinationDate, range))
      const map = {}
      list.forEach(r => {
        const d = new Date(r.vaccinationDate)
        const key = d.toLocaleDateString('en-PH', { month: 'long', year: 'numeric' })
        map[key] = map[key] || { count: 0, children: new Set(), sort: d.getFullYear() * 12 + d.getMonth() }
        map[key].count++
        map[key].children.add(r.childID)
      })
      return {
        columns: ['Month', 'Doses Given', 'Children Vaccinated'],
        rows: Object.entries(map).sort((a, b) => b[1].sort - a[1].sort).map(([k, v]) => [k, v.count, v.children.size]),
        chart: monthlyChart(recs.map(r => r.vaccinationDate), 'Vaccinations per Month'),
      }
    }
    case 'Vaccinations by Vaccine': {
      const list = recs.filter(r => inPeriod(r.vaccinationDate, range))
      const map = {}
      list.forEach(r => { map[r.vaccineName] = map[r.vaccineName] || {}; map[r.vaccineName][r.doseNumber] = (map[r.vaccineName][r.doseNumber] || 0) + 1 })
      return {
        columns: ['Vaccine', 'Dose 1', 'Dose 2', 'Dose 3', 'Total'],
        rows: Object.entries(map).map(([v, d]) => [v, d[1] || 0, d[2] || 0, d[3] || 0, Object.values(d).reduce((s, x) => s + x, 0)]).sort((a, b) => b[4] - a[4]),
        chart: countChart(list, r => r.vaccineName, 'Doses per Vaccine'),
      }
    }
    case 'Children Fully Vaccinated': {
      const list = kids.filter(c => c.vaccinationStatus === 'Fully Vaccinated')
      return {
        columns: ['Child', 'Birth Date', 'Age', 'Doses', 'Last Vaccination', 'Parent / Guardian'],
        rows: list.map(c => [`${c.firstName} ${c.lastName}`, formatDate(c.birthDate), ageLabel(c.birthDate), c.totalDoses, c.lastVaccinationDate ? formatDate(c.lastVaccinationDate) : '—', c.parentName || '—']),
      }
    }
    case 'Delayed Vaccinations': {
      const list = data.schedule.filter(s => s.displayStatus === 'Overdue' && childPasses(s.childID) && vaccinePasses(s.vaccineName))
      return {
        columns: ['Child', 'Vaccine', 'Dose', 'Was Due', 'Days Overdue', 'Parent / Guardian', 'Contact'],
        rows: list.map(s => [s.childName, s.vaccineName, s.doseNumber, formatDate(s.scheduledDate), Math.floor((Date.now() - new Date(s.scheduledDate)) / 86400000), s.parentName || '—', s.parentContact || '—']),
        chart: countChart(list, s => s.vaccineName, 'Overdue Doses per Vaccine'),
      }
    }
    case 'Upcoming Vaccinations': {
      const list = data.schedule.filter(s => (s.displayStatus === 'Upcoming' || s.displayStatus === 'Due Today') && childPasses(s.childID) && vaccinePasses(s.vaccineName))
      return {
        columns: ['Scheduled', 'Child', 'Vaccine', 'Dose', 'Parent / Guardian', 'Contact'],
        rows: list.map(s => [formatDate(s.scheduledDate), s.childName, s.vaccineName, s.doseNumber, s.parentName || '—', s.parentContact || '—']),
        chart: countChart(list, s => s.vaccineName, 'Upcoming Doses per Vaccine (next 60 days)'),
      }
    }
    case 'Current Inventory':
    case 'Low Stock Vaccines':
    case 'Expired & Expiring Batches': {
      const vname = Object.fromEntries(data.vaccines.map(v => [v.vaccineID, v.vaccineName]))
      const today = toISODate()
      let list = data.inventory.filter(i => vaccinePasses(vname[i.vaccineID]))
      if (name === 'Low Stock Vaccines') list = list.filter(i => i.status && i.currentQuantity < i.minimumStock)
      if (name === 'Expired & Expiring Batches') list = list.filter(i => (new Date(i.expirationDate) - new Date()) / 86400000 <= 30)
      const statusOf = i => String(i.expirationDate).slice(0, 10) < today ? 'Expired' : !i.status ? 'Inactive' : i.currentQuantity === 0 ? 'Out of Stock' : i.currentQuantity < i.minimumStock ? 'Low Stock' : 'Good'
      return {
        tiles: inventoryTiles(data.inventory.filter(i => vaccinePasses(vname[i.vaccineID]))),
        columns: ['Vaccine', 'Lot No.', 'Remaining', 'Initial', 'Minimum', 'Received', 'Expires', 'Status'],
        rows: list.map(i => [vname[i.vaccineID] || i.vaccineID, i.lotNumber, i.currentQuantity, i.initialQuantity, i.minimumStock, formatDate(i.receivedDate), formatDate(i.expirationDate), statusOf(i)]),
        chart: { title: 'Doses Remaining per Vaccine', points: summarizeStock(data.inventory, data.vaccines).filter(s => vaccinePasses(s.name)).map(s => ({ label: s.abbreviation || s.name, value: s.stock })) },
      }
    }
    case 'Stock Received': {
      const vname = Object.fromEntries(data.vaccines.map(v => [v.vaccineID, v.vaccineName]))
      const list = data.inventory.filter(i => vaccinePasses(vname[i.vaccineID]) && inPeriod(i.receivedDate, range))
        .sort((a, b) => new Date(b.receivedDate) - new Date(a.receivedDate))
      return {
        tiles: [
          { label: 'Batches Received', value: list.length },
          { label: 'Doses Received', value: list.reduce((s, i) => s + i.initialQuantity, 0) },
          { label: 'Still On Hand', value: list.reduce((s, i) => s + i.currentQuantity, 0) },
        ],
        columns: ['Received', 'Vaccine', 'Lot No.', 'Doses Received', 'Remaining', 'Supplier', 'Manufactured', 'Expires'],
        rows: list.map(i => [formatDate(i.receivedDate), vname[i.vaccineID] || i.vaccineID, i.lotNumber, i.initialQuantity, i.currentQuantity, i.supplier || '—', i.manufacturingDate ? formatDate(i.manufacturingDate) : '—', formatDate(i.expirationDate)]),
        chart: {
          title: 'Doses Received per Vaccine',
          points: Object.entries(list.reduce((m, i) => { const k = vname[i.vaccineID] || i.vaccineID; m[k] = (m[k] || 0) + i.initialQuantity; return m }, {}))
            .sort((a, b) => b[1] - a[1]).map(([label, value]) => ({ label, value })),
        },
      }
    }
    case 'Daily Queue Summary': {
      const list = data.queues.filter(q => inPeriod(q.queueDate, range))
      const map = {}
      list.forEach(q => {
        const k = toISODate(new Date(q.queueDate))
        map[k] = map[k] || { visits: 0, children: 0, completed: 0, waiting: 0 }
        map[k].visits++
        map[k].children += q.children?.length || 0
        if ((q.status || '').toLowerCase() === 'completed') map[k].completed++
        else map[k].waiting++
      })
      return {
        columns: ['Date', 'Visits', 'Children Seen', 'Completed', 'Not Completed'],
        rows: Object.entries(map).sort((a, b) => b[0].localeCompare(a[0])).map(([d, v]) => [formatDate(d), v.visits, v.children, v.completed, v.waiting]),
        chart: countChart(list, q => q.status, 'Visits by Status'),
      }
    }
    case 'Notifications Sent':
    case 'Announcement History': {
      let list = data.notifications.filter(n => inPeriod(n.createdAt, range))
      if (name === 'Announcement History') list = list.filter(n => n.type === 'Announcement')
      return {
        columns: ['Sent', 'Title', 'Recipient', 'Child', 'Type', 'Read'],
        rows: list.map(n => [formatDateTime(n.createdAt), n.title, n.recipient, n.child, n.category, n.isRead ? 'Yes' : 'No']),
        chart: countChart(list, n => n.category, 'Notifications by Type'),
      }
    }
    case 'Registered Parents': {
      const list = data.accounts.filter(a => a.role === 'Parent')
      return {
        columns: ['Name', 'Email / Username', 'Contact', 'Status', 'Last Login', 'Created'],
        rows: list.map(a => [`${a.firstName} ${a.lastName}`, a.username, a.contactNo || '—', a.status, a.lastLogin ? formatDateTime(a.lastLogin) : 'Never', formatDate(a.createdAt)]),
        chart: monthlyChart(list.map(a => a.createdAt), 'New Parent Accounts per Month'),
      }
    }
    case 'Doctors & Nurses': {
      const list = data.accounts.filter(a => a.role !== 'Parent')
      return {
        columns: ['Name', 'User Level', 'Position', 'Username', 'Contact', 'Status', 'Last Login'],
        rows: list.map(a => [`${a.firstName} ${a.lastName}`, userLevel(a.role), a.role, a.username, a.contactNo || '—', a.status, a.lastLogin ? formatDateTime(a.lastLogin) : 'Never']),
        chart: countChart(list, a => userLevel(a.role), 'Accounts by User Level'),
      }
    }
    default:
      return { columns: [], rows: [] }
  }
}

/* -------------------------------- Generate / preview -------------------------------- */
const generatedReport = ref(null)
const isGenerating = ref(false)
const page = ref(1)
const pageSize = 15

const preparedBy = (() => {
  try {
    const u = JSON.parse(localStorage.getItem('account') || '{}').user || {}
    return [u.FirstName, u.LastName].filter(Boolean).join(' ') || 'Administrator'
  } catch { return 'Administrator' }
})()

const reportError = ref('')
let generation = 0
const generateReport = () => {
  if (loading.value) return
  const mine = ++generation
  isGenerating.value = true
  reportError.value = ''
  page.value = 1
  // Let the spinner paint before computing large reports.
  setTimeout(async () => {
    const range = periodRange()
    try {
      const result = selectedReport.value === 'Inventory Summary'
        ? await buildInventorySummary(range)
        : buildReport(selectedReport.value, range)
      if (mine !== generation) return   // a newer report was asked for meanwhile
      generatedReport.value = {
        name: selectedReport.value,
        dateGenerated: formatDateTime(new Date()),
        preparedBy,
        dateRange: range.label,
        totalRecords: result.rows.length,
        ...result,
      }
    } catch (e) {
      console.error('generateReport:', e)
      if (mine === generation) { generatedReport.value = null; reportError.value = 'Could not build this report. Check that the API is running, then try again.' }
    } finally {
      if (mine === generation) isGenerating.value = false
    }
  }, 150)
}

// The preview follows the selected report and filters by itself
watch([selectedReport, () => JSON.stringify(filters)], generateReport)

const maxChartValue = computed(() => Math.max(1, ...(generatedReport.value?.chart?.points || []).map(p => p.value)))
const totalPages = computed(() => Math.max(1, Math.ceil((generatedReport.value?.rows.length || 0) / pageSize)))
const pagedRows = computed(() => (generatedReport.value?.rows || []).slice((page.value - 1) * pageSize, page.value * pageSize))

const exportExcel = () => {
  const r = generatedReport.value
  downloadCSV(`${r.name.toLowerCase().replace(/[^a-z0-9]+/g, '-')}-${toISODate()}.csv`, [
    [r.name], [`Period: ${r.dateRange}`], [`Generated: ${r.dateGenerated} by ${r.preparedBy}`], [],
    ...(r.tiles?.length ? [...r.tiles.map(t => [t.label, t.value]), []] : []),
    r.columns, ...r.rows,
  ])
}
// Printing shows the full report (all rows) on its own page; "Export PDF"
// uses the same print view — choose "Save as PDF" in the print dialog.
const printing = ref(false)
const printReport = () => {
  printing.value = true
  setTimeout(() => { window.print(); printing.value = false }, 100)
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900 print:bg-white">
    <div class="print:hidden contents"><AppSidebar /></div>

    <div class="flex-1 min-w-0 flex flex-col">
      <div class="print:hidden"><AppHeader title="Reports" breadcrumb="Admin / Reports" /></div>

      <main class="p-6 space-y-6 print:p-0">
        <div v-if="loadErrors.length" class="print:hidden bg-amber-50 border border-amber-100 text-amber-800 text-sm rounded-xl px-4 py-3">
          Some data could not be loaded ({{ loadErrors.join(', ') }}). Related reports may be incomplete.
        </div>

        <!-- Summary cards -->
        <section class="print:hidden grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
          <div v-for="card in summary" :key="card.label" class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ card.label }}</p>
              <div :class="card.tint" class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">{{ card.icon }}</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ loading ? '…' : card.value }}</p>
          </div>
        </section>

        <section class="grid grid-cols-1 xl:grid-cols-4 gap-6 items-start">
          <!-- LEFT PANEL -->
          <div class="xl:col-span-1 space-y-6 print:hidden">
            <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-4">
              <h2 class="text-sm font-bold text-slate-900 mb-3">Report Categories</h2>
              <div class="space-y-1">
                <div v-for="cat in categories" :key="cat.name">
                  <button @click="toggleCategory(cat.name)" class="w-full flex items-center justify-between gap-2 rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
                    <span class="flex items-center gap-2 min-w-0"><span class="text-base shrink-0">{{ cat.icon }}</span><span class="truncate">{{ cat.name }}</span></span>
                    <span :class="expandedCategory === cat.name ? 'rotate-90' : ''" class="text-slate-400 text-xs transition-transform shrink-0">▶</span>
                  </button>
                  <div v-if="expandedCategory === cat.name" class="pl-9 pr-2 py-1 space-y-0.5">
                    <button v-for="r in cat.reports" :key="r" @click="selectReport(r)"
                      :class="selectedReport === r ? 'bg-emerald-50 text-emerald-700 font-semibold' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800'"
                      class="w-full text-left rounded-lg px-2.5 py-1.5 text-xs">{{ r }}</button>
                  </div>
                </div>
              </div>
            </div>

            <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-4 space-y-4">
              <h2 class="text-sm font-bold text-slate-900">Filters</h2>
              <div>
                <label class="block text-xs font-semibold text-slate-500 mb-1.5">Report Period</label>
                <select v-model="filters.period" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
                  <option>Today</option><option>This Week</option><option>This Month</option><option>This Year</option><option>All Time</option><option>Custom Date Range</option>
                </select>
              </div>
              <div v-if="filters.period === 'Custom Date Range'" class="grid grid-cols-2 gap-2">
                <input v-model="filters.customFrom" type="date" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-2" />
                <input v-model="filters.customTo" type="date" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-2" />
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-500 mb-1.5">Barangay <span class="text-slate-400 font-normal">(optional)</span></label>
                <select v-model="filters.barangay" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
                  <option v-for="b in barangayOptions" :key="b" :value="b">{{ b === 'All Barangays' ? b : `Barangay ${b}` }}</option>
                </select>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-500 mb-1.5">Vaccine <span class="text-slate-400 font-normal">(optional)</span></label>
                <select v-model="filters.vaccine" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
                  <option v-for="v in vaccineOptions" :key="v">{{ v }}</option>
                </select>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-500 mb-1.5">Age Group</label>
                <div class="flex flex-wrap gap-1.5">
                  <button v-for="g in ageGroupOptions" :key="g" @click="toggleAgeGroup(g)"
                    :class="filters.ageGroups.includes(g) ? 'bg-emerald-600 text-white border-emerald-600' : 'border-slate-200 text-slate-600 hover:bg-slate-50'"
                    class="text-xs font-semibold px-2.5 py-1.5 rounded-lg border">{{ g }}</button>
                </div>
              </div>
              <div class="flex items-center gap-2 pt-1">
                <button @click="generateReport" :disabled="loading" class="flex-1 text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50">
                  {{ loading ? 'Loading data...' : 'Refresh Report' }}
                </button>
                <button @click="resetFilters" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">Reset</button>
              </div>
            </div>
          </div>

          <!-- REPORT PREVIEW -->
          <div class="xl:col-span-3 bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden print:col-span-4 print:border-0 print:shadow-none">
            <div class="px-6 py-4 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3">
              <div>
                <p class="hidden print:block text-xs text-slate-500">Leveriza Health Center · Aruga Pediatric Health Record System</p>
                <h2 class="text-base font-bold text-slate-900">{{ selectedReport }}</h2>
                <p class="text-xs text-slate-500 mt-0.5 print:hidden">Report Preview</p>
              </div>
              <div class="flex items-center gap-2 print:hidden">
                <button @click="printReport" :disabled="!generatedReport" :class="generatedReport ? 'text-slate-600 hover:bg-slate-50' : 'text-slate-300 cursor-not-allowed'" class="text-xs font-semibold px-3 py-1.5 rounded-lg border border-slate-200">Print</button>
                <button @click="printReport" :disabled="!generatedReport" :class="generatedReport ? 'text-slate-600 hover:bg-slate-50' : 'text-slate-300 cursor-not-allowed'" class="text-xs font-semibold px-3 py-1.5 rounded-lg border border-slate-200" title="Choose 'Save as PDF' in the print dialog">Export PDF</button>
                <button @click="exportExcel" :disabled="!generatedReport" :class="generatedReport ? 'text-slate-600 hover:bg-slate-50' : 'text-slate-300 cursor-not-allowed'" class="text-xs font-semibold px-3 py-1.5 rounded-lg border border-slate-200">Export Excel</button>
              </div>
            </div>

            <div v-if="!generatedReport && !isGenerating" class="flex flex-col items-center justify-center text-center py-24 px-6">
              <div class="w-14 h-14 rounded-xl bg-slate-50 flex items-center justify-center text-2xl mb-4">📄</div>
              <p class="text-sm font-semibold text-slate-700">No report generated yet</p>
              <p class="text-xs max-w-xs mt-1" :class="reportError ? 'text-rose-600' : 'text-slate-400'">{{ reportError || (loading ? 'Loading the clinic data…' : 'Select a report from the categories panel.') }}</p>
            </div>

            <div v-else-if="isGenerating" class="flex flex-col items-center justify-center text-center py-24 px-6">
              <div class="w-8 h-8 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin mb-4"></div>
              <p class="text-sm text-slate-500">Generating report...</p>
            </div>

            <div v-else class="p-6 space-y-6">
              <div class="bg-slate-50 rounded-lg p-4 grid grid-cols-2 sm:grid-cols-5 gap-4">
                <div><p class="text-xs text-slate-500">Report Name</p><p class="text-sm font-semibold text-slate-900 mt-0.5">{{ generatedReport.name }}</p></div>
                <div><p class="text-xs text-slate-500">Date Generated</p><p class="text-sm font-semibold text-slate-900 mt-0.5">{{ generatedReport.dateGenerated }}</p></div>
                <div><p class="text-xs text-slate-500">Prepared By</p><p class="text-sm font-semibold text-slate-900 mt-0.5">{{ generatedReport.preparedBy }}</p></div>
                <div><p class="text-xs text-slate-500">Date Range</p><p class="text-sm font-semibold text-slate-900 mt-0.5">{{ generatedReport.dateRange }}</p></div>
                <div><p class="text-xs text-slate-500">Total Records</p><p class="text-sm font-semibold text-slate-900 mt-0.5">{{ generatedReport.totalRecords }}</p></div>
              </div>

              <div v-if="generatedReport.tiles?.length" class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
                <div v-for="t in generatedReport.tiles" :key="t.label" class="rounded-lg border border-slate-200 px-4 py-3">
                  <p class="text-[10.5px] font-semibold uppercase tracking-wide text-slate-500">{{ t.label }}</p>
                  <p class="text-xl font-extrabold text-slate-900 mt-0.5">{{ t.value }}</p>
                </div>
              </div>

              <div v-if="generatedReport.chart?.points?.length">
                <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">{{ generatedReport.chart.title }}</h3>
                <div class="bg-slate-50 rounded-lg p-5">
                  <div class="flex items-end justify-between gap-3 h-40">
                    <div v-for="d in generatedReport.chart.points" :key="d.label" class="flex-1 flex flex-col items-center justify-end h-full gap-2 min-w-0">
                      <span class="text-xs font-semibold text-slate-600">{{ d.value }}</span>
                      <div class="w-full max-w-12 bg-emerald-500 rounded-t-md" :style="{ height: Math.max(2, (d.value / maxChartValue) * 100) + '%' }"></div>
                      <span class="text-[11px] text-slate-400 truncate max-w-full" :title="d.label">{{ d.label }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <div>
                <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Record Detail</h3>
                <div class="border border-slate-200 rounded-lg overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead>
                      <tr class="border-b border-slate-200 bg-slate-50/60">
                        <th v-for="c in generatedReport.columns" :key="c" class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-4 py-2.5 whitespace-nowrap">{{ c }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(row, idx) in (printing ? generatedReport.rows : pagedRows)" :key="idx" class="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                        <td v-for="(cell, ci) in row" :key="ci" class="px-4 py-2.5 whitespace-nowrap" :class="ci === 0 ? 'font-medium text-slate-900' : 'text-slate-500'">{{ cell }}</td>
                      </tr>
                      <tr v-if="!generatedReport.rows.length">
                        <td :colspan="generatedReport.columns.length" class="px-4 py-8 text-center text-slate-400">No records for the selected period and filters.</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <div v-if="generatedReport.rows.length > pageSize" class="flex items-center justify-between mt-3 print:hidden">
                  <p class="text-xs text-slate-400">
                    Showing {{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, generatedReport.rows.length) }} of {{ generatedReport.rows.length }} records
                  </p>
                  <div class="flex items-center gap-1">
                    <button @click="page = Math.max(1, page - 1)" :disabled="page === 1" class="w-7 h-7 rounded-lg border border-slate-200 text-slate-500 text-xs hover:bg-slate-50 disabled:opacity-40">‹</button>
                    <span class="text-xs text-slate-500 px-2">Page {{ page }} of {{ totalPages }}</span>
                    <button @click="page = Math.min(totalPages, page + 1)" :disabled="page === totalPages" class="w-7 h-7 rounded-lg border border-slate-200 text-slate-500 text-xs hover:bg-slate-50 disabled:opacity-40">›</button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>
      </main>
    </div>
  </div>
</template>

<style scoped>
@media (prefers-reduced-motion: reduce) {
  * { transition-duration: 0.01ms !important; animation-duration: 0.01ms !important; }
}
</style>
