<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { toISODate } from '@/utils/format'
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useFloatingMenu } from '@/utils/floatingMenu'
import axios from 'axios'
import AppSidebar from './Components/AppSidebar.vue'
import AppHeader from './Components/AppHeader.vue'
import InventorySummary from '@/components/Shared/InventorySummary.vue'

/* =========================================================================
   API LAYER
   Talks to VaccinesController + VaccineInventoryController from your
   AndroidWebAPI project. Endpoints/contract are unchanged from your code.

   ASSUMPTIONS CARRIED OVER FROM YOUR ORIGINAL FILE (please check these
   against your actual DTOs and change the one-liners if they don't match):

   1. VaccineInventory lifecycle flag is sent/received under JSON key
      `status` (boolean). true = active, false = inactive.

   2. Vaccine itself ALSO has a lifecycle flag. Your posted VaccinesController
      doesn't show the Vaccine model, so I couldn't confirm the exact field
      name. `isVaccineActive()` below checks a few common possibilities
      (`status`, `isActive`, `active`) and falls back to "active" if none of
      them are present, so nothing silently disappears from the dropdown if
      your API doesn't send this field at all. If your Vaccine model uses a
      different property name (e.g. `IsActive` serialized differently),
      update the one line inside isVaccineActive().
========================================================================= */
const API_BASE = `${API_ORIGIN}/api`
const api = {
  getInventory:          ()          => axios.get(`${API_BASE}/VaccineInventory`).then(r => r.data),
  getInventoryByVaccine: (vaccineId) => axios.get(`${API_BASE}/VaccineInventory/vaccine/${vaccineId}`).then(r => r.data),
  createInventory:       (payload)   => axios.post(`${API_BASE}/VaccineInventory`, payload).then(r => r.data),
  updateInventory:       (id, payload) => axios.put(`${API_BASE}/VaccineInventory/${id}`, payload).then(r => r.data),
  getVaccines:           ()          => axios.get(`${API_BASE}/Vaccines`).then(r => r.data),
}

/* -------------------------- Layout state (page-level) -------------------------- */
// Sidebar owns its own collapse state internally now. This page only needs
// to know/track which nav item is active — pass this to AppSidebar as a
// v-model. Swap this for vue-router's current route once that's wired up.
const activeNav = ref('Inventory')
function handleLogout() {
  // Hook up real logout / redirect logic here.
  console.log('logout clicked')
}

/* -------------------------------- Status meta -------------------------------- */
const statusMeta = {
  Healthy:     { tint: 'bg-emerald-50', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  'Low Stock': { tint: 'bg-amber-50',   text: 'text-amber-700',   dot: 'bg-amber-500' },
  Expired:     { tint: 'bg-red-100',    text: 'text-red-800',     dot: 'bg-red-700' },
}

/* ============================= Core data (loaded from API) ============================= */
const inventory = ref([])   // raw VaccineInventory rows
const vaccines = ref([])    // raw Vaccine rows
const isLoading = ref(false)
const isSaving = ref(false)
const error = ref(null)
const summaryKey = ref(0)   // reloads the Inventory Summary with the batches

async function loadAll() {
  isLoading.value = true
  try {
    const inv = await api.getInventory()
    const vax = await api.getVaccines()

    inventory.value = Array.isArray(inv) ? inv : inv.data || inv.items || []
    vaccines.value  = Array.isArray(vax) ? vax : vax.data || vax.items || []
    error.value = null
  } catch (e) {
    console.error(e)
    error.value = 'Failed to load inventory. Please try again.'
  } finally {
    isLoading.value = false
    summaryKey.value++
  }
}
onMounted(loadAll)

/* ------------------------------ Derived helpers ------------------------------ */
function vaccineName(vaccineId) {
  const v = vaccines.value.find(v => v.vaccineID === vaccineId)
  return v ? (v.vaccineName || '—') : '—'
}
function formatDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  return isNaN(d) ? iso : d.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}
// "2027-12-31T00:00:00" -> "2027-12-31". Not through toISOString(): in
// Philippine time (UTC+8) that turns midnight into the previous day, so every
// save moved the expiry date one day earlier.
function toDateInputValue(iso) {
  if (!iso) return ''
  const s = String(iso)
  if (/^\d{4}-\d{2}-\d{2}/.test(s)) return s.slice(0, 10)
  const d = new Date(iso)
  return isNaN(d) ? '' : toISODate(d)
}
const startOfToday = new Date(new Date().toDateString())
function daysUntil(iso) {
  const d = new Date(iso)
  return Math.ceil((d - startOfToday) / 86400000)
}
// Batch status is always derived live from ExpirationDate / CurrentQuantity / MinimumStock.
function computeStatus(item) {
  if (daysUntil(item.expirationDate) < 0) return 'Expired'
  if (item.currentQuantity <= item.minimumStock) return 'Low Stock'
  return 'Healthy'
}
// isActive on a BATCH reads the boolean lifecycle flag (wire key: status).
function isActive(item) {
  return item.status !== false
}
function shouldRecommendDeactivation(item) {
  return isActive(item) && computeStatus(item) === 'Expired' && (item.currentQuantity || 0) === 0
}

// isVaccineActive checks the VACCINE record itself (not a batch). Used to decide
// whether a vaccine should be selectable when receiving a new batch.
// See the assumptions note at the top of this file.
function isVaccineActive(v) {
  if (!v) return false
  if (typeof v.status === 'boolean') return v.status
  if (typeof v.isActive === 'boolean') return v.isActive
  if (typeof v.active === 'boolean') return v.active
  // Field not present on the DTO at all -> treat as active so nothing
  // vanishes from the dropdown just because the API doesn't send this yet.
  return true
}

// Only active vaccines may be picked when registering a new batch.
const activeVaccines = computed(() => vaccines.value.filter(isVaccineActive))

/* ---------------------------- Toolbar / filters ---------------------------- */
// Filters intentionally list ALL vaccines (active + inactive) so existing
// batches under a since-deactivated vaccine are still searchable/visible.
const searchQuery = ref('')
const vaccineFilter = ref('All')
const statusFilter = ref('All')
const expirationFilter = ref('All') // All | Expiring Soon | Expired | Valid
const activeFilter = ref('All')     // All | Active | Inactive (batch lifecycle)

const filteredInventory = computed(() => {
  const list = Array.isArray(inventory.value) ? inventory.value : []
  const q = searchQuery.value.trim().toLowerCase()

  return list.filter(item => {
    const status = computeStatus(item)

    const matchesSearch =
      !q ||
      vaccineName(item.vaccineID).toLowerCase().includes(q) ||
      (item.lotNumber || '').toLowerCase().includes(q) ||
      (item.supplier || '').toLowerCase().includes(q)

    const matchesVaccine = vaccineFilter.value === 'All' || item.vaccineID === vaccineFilter.value
    const matchesStatus  = statusFilter.value === 'All' || status === statusFilter.value
    const matchesActive =
      activeFilter.value === 'All' ||
      (activeFilter.value === 'Active' ? isActive(item) : !isActive(item))

    let matchesExpiration = true
    if (expirationFilter.value !== 'All') {
      const days = daysUntil(item.expirationDate)
      if (expirationFilter.value === 'Expiring Soon') matchesExpiration = days >= 0 && days <= 30
      else if (expirationFilter.value === 'Expired')  matchesExpiration = days < 0
      else if (expirationFilter.value === 'Valid')    matchesExpiration = days > 30
    }

    return matchesSearch && matchesVaccine && matchesStatus && matchesActive && matchesExpiration
  })
})

/* ------------------------- Grouped by vaccine, FEFO within group -------------------------
   e.g.
     BCG Vaccine
       - LOT-88214
       - LOT-51092
     Pentavalent Vaccine
       - LOT-65120
------------------------------------------------------------------------------------------- */
const groupedInventory = computed(() => {
  const map = new Map()
  for (const item of filteredInventory.value) {
    if (!map.has(item.vaccineID)) map.set(item.vaccineID, [])
    map.get(item.vaccineID).push(item)
  }

  const groups = Array.from(map.entries()).map(([vaccineID, batches]) => {
    // First Expired, First Out — earliest expiration date first.
    const sorted = [...batches].sort((a, b) => new Date(a.expirationDate) - new Date(b.expirationDate))
    const activeBatches = sorted.filter(isActive)
    const vaccineRecord = vaccines.value.find(v => v.vaccineID === vaccineID)
    return {
      vaccineID,
      vaccineName: vaccineName(vaccineID),
      vaccineIsActive: isVaccineActive(vaccineRecord),
      batches: sorted,
      batchCount: sorted.length,
      totalDoses: activeBatches.reduce((sum, b) => sum + (b.currentQuantity || 0), 0),
      hasCritical: sorted.some(b => isActive(b) && computeStatus(b) === 'Low Stock'),
      hasExpiring: sorted.some(b => isActive(b) && daysUntil(b.expirationDate) >= 0 && daysUntil(b.expirationDate) <= 30 && computeStatus(b) !== 'Expired'),
      hasExpired: sorted.some(b => computeStatus(b) === 'Expired'),
    }
  })

  groups.sort((a, b) => a.vaccineName.localeCompare(b.vaccineName))
  return groups
})

/* ------------------------------ Pagination (over vaccine groups) ---------------------------- */
const pageSize = ref(25)
const currentPage = ref(1)
const totalGroups = computed(() => groupedInventory.value.length)
const totalBatchesFiltered = computed(() => filteredInventory.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalGroups.value / pageSize.value)))
const paginatedGroups = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return groupedInventory.value.slice(start, start + pageSize.value)
})
function goToPage(p) {
  currentPage.value = Math.min(Math.max(1, p), totalPages.value)
}
watch([searchQuery, vaccineFilter, statusFilter, expirationFilter, activeFilter, pageSize], () => {
  currentPage.value = 1
})

/* ------------------------------ Expand / collapse groups ---------------------------- */
const expandedGroups = ref(new Set())
function toggleGroup(vaccineID) {
  const next = new Set(expandedGroups.value)
  next.has(vaccineID) ? next.delete(vaccineID) : next.add(vaccineID)
  expandedGroups.value = next
}
function expandAll() {
  expandedGroups.value = new Set(groupedInventory.value.map(g => g.vaccineID))
}
function collapseAll() {
  expandedGroups.value = new Set()
}

/* -------------------------------- Dashboard summary (compact strip) ---------------------------------- */
const summary = computed(() => {
  const list = Array.isArray(inventory.value) ? inventory.value : []
  const active = list.filter(isActive)
  const withStatus = active.map(i => ({ i, status: computeStatus(i) }))
  return {
    totalVaccineTypes: new Set(active.map(i => i.vaccineID)).size,
    totalBatches: active.length,
    // Expired doses can't be given, so they aren't "available"
    totalDoses: withStatus.filter(x => x.status !== 'Expired').reduce((sum, x) => sum + (x.i.currentQuantity || 0), 0),
    lowStock: withStatus.filter(x => x.status === 'Low Stock').length,
    expiringSoon: withStatus.filter(x => {
      const d = daysUntil(x.i.expirationDate)
      return d >= 0 && d <= 30 && x.status !== 'Expired'
    }).length,
    expired: withStatus.filter(x => x.status === 'Expired').length,
  }
})

/* ------------------------------ Alerts panel (active batches only, sorted by urgency) ---------------------------- */
const criticalStock = computed(() =>
  inventory.value.filter(i => isActive(i) && computeStatus(i) === 'Low Stock')
    .sort((a, b) => (a.currentQuantity - a.minimumStock) - (b.currentQuantity - b.minimumStock))
)
const expiringSoonList = computed(() =>
  inventory.value.filter(i => isActive(i) && (() => {
      const d = daysUntil(i.expirationDate)
      return d >= 0 && d <= 30 && computeStatus(i) !== 'Expired'
    })())
    .sort((a, b) => daysUntil(a.expirationDate) - daysUntil(b.expirationDate))
)
const expiredList = computed(() =>
  inventory.value.filter(i => isActive(i) && computeStatus(i) === 'Expired')
    .sort((a, b) => daysUntil(b.expirationDate) - daysUntil(a.expirationDate))
)

/* ------------------------------ Row actions menu ---------------------------- */
const { openMenuId, menuStyle, toggleMenu, closeMenu } = useFloatingMenu()

/* --------------------------------- Batch details drawer --------------------------------- */
const showDrawer = ref(false)
const selectedBatch = ref(null)
function openDrawer(item) {
  selectedBatch.value = item
  showDrawer.value = true
  closeMenu()
}
function closeDrawer() {
  showDrawer.value = false
}

/* --------------------------------- Receive New Batch modal --------------------------------- */
const showReceiveModal = ref(false)
const formError = ref(null)
const receiveForm = reactive({
  vaccineID: '', lotNumber: '', initialQuantity: '', minimumStock: '',
  expirationDate: '', manufacturingDate: '', receivedDate: '', supplier: '',
})
function openReceiveModal() {
  Object.assign(receiveForm, {
    vaccineID: '', lotNumber: '', initialQuantity: '', minimumStock: '',
    expirationDate: '', manufacturingDate: '', receivedDate: '', supplier: '',
  })
  formError.value = null
  showReceiveModal.value = true
  closeMenu()
}
async function submitReceiveStock() {
  if (!receiveForm.vaccineID || !receiveForm.lotNumber || !receiveForm.initialQuantity
      || !receiveForm.minimumStock || !receiveForm.expirationDate || !receiveForm.receivedDate) {
    formError.value = 'Please fill all required fields.'
    return
  }
  if (receiveForm.manufacturingDate && receiveForm.manufacturingDate >= receiveForm.expirationDate) {
    formError.value = 'The manufacturing date must be before the expiration date.'
    return
  }
  // Belt-and-suspenders: re-check the chosen vaccine is still active right before
  // submit, in case the underlying data changed while the modal was open.
  const chosen = vaccines.value.find(v => v.vaccineID === receiveForm.vaccineID)
  if (!isVaccineActive(chosen)) {
    formError.value = 'This vaccine is inactive and cannot receive new batches.'
    return
  }

  isSaving.value = true
  try {
    const qty = Number(receiveForm.initialQuantity)
    await api.createInventory({
      vaccineID: receiveForm.vaccineID,
      lotNumber: receiveForm.lotNumber,
      initialQuantity: qty,
      currentQuantity: qty, // a fresh batch always starts full
      minimumStock: Number(receiveForm.minimumStock),
      expirationDate: receiveForm.expirationDate,
      manufacturingDate: receiveForm.manufacturingDate || null,
      receivedDate: receiveForm.receivedDate,
      supplier: receiveForm.supplier || '',
      status: true, // new batches are active by default
    })
    await loadAll()
    expandedGroups.value = new Set([...expandedGroups.value, receiveForm.vaccineID])
    showReceiveModal.value = false
    formError.value = null
  } catch (e) {
    formError.value = `Failed to receive batch: ${e.response?.data?.message || e.message}`
  } finally {
    isSaving.value = false
  }
}

/* --------------------------------- Alert Staff modal --------------------------------- */
// The Doctor gets the low-stock alerts and tells the Staff / Nurses what to
// request or order. Pre-filled from the stock check; the Doctor can edit it.
const showAlertModal = ref(false)
const alertMessage = ref('')
const alertError = ref(null)
const alertSent = ref('')
const isSendingAlert = ref(false)

async function openAlertModal() {
  alertError.value = null
  alertSent.value = ''
  alertMessage.value = ''
  showAlertModal.value = true
  closeMenu()
  try {
    const { data } = await axios.get(`${API_BASE}/VaccineInventory/stock-check`)
    const restock = (data.lines || []).filter(l => l.restock)
    if (restock.length) {
      alertMessage.value = 'Please request more stock from the pharmacy: ' +
        restock.map(l => `${l.short} (${l.onHand} left, order about ${l.suggestedOrder})`).join('; ') + '.'
    }
  } catch { /* the Doctor can still type the message */ }
}

async function sendAlert() {
  if (!alertMessage.value.trim()) {
    alertError.value = 'Please write what the staff should request or order.'
    return
  }
  isSendingAlert.value = true
  alertError.value = null
  try {
    const { data } = await axios.post(`${API_BASE}/VaccineInventory/alert-staff`, { message: alertMessage.value.trim() })
    alertSent.value = data.message
    setTimeout(() => { showAlertModal.value = false }, 1800)
  } catch (e) {
    alertError.value = e.response?.data?.message || 'Could not send the alert. Please try again.'
  } finally {
    isSendingAlert.value = false
  }
}

/* --------------------------------- Edit modal (restricted field set) --------------------------------- */
const showEditModal = ref(false)
const editForm = reactive({
  id: null, lotNumber: '', minimumStock: '', expirationDate: '', manufacturingDate: '', supplier: '', isActive: true, raw: null,
})
function openEditModal(item) {
  Object.assign(editForm, {
    id: item.inventoryID,
    lotNumber: item.lotNumber,
    minimumStock: item.minimumStock,
    expirationDate: toDateInputValue(item.expirationDate),
    manufacturingDate: item.manufacturingDate ? toDateInputValue(item.manufacturingDate) : '',
    supplier: item.supplier || '',
    isActive: isActive(item),
    raw: item,
  })
  formError.value = null
  showEditModal.value = true
  closeMenu()
}
async function submitEdit() {
  if (!editForm.lotNumber || editForm.minimumStock === '' || !editForm.expirationDate) {
    formError.value = 'Please fill all required fields.'
    return
  }
  isSaving.value = true
  try {
    const raw = editForm.raw || {}
    // VaccineID / InitialQuantity / CurrentQuantity / ReceivedDate stay untouched here —
    // dose counts should only change through vaccination-administration actions.
    await api.updateInventory(editForm.id, {
      inventoryID: editForm.id,
      vaccineID: raw.vaccineID,
      lotNumber: editForm.lotNumber,
      initialQuantity: raw.initialQuantity,
      currentQuantity: raw.currentQuantity,
      minimumStock: Number(editForm.minimumStock),
      expirationDate: editForm.expirationDate,
      manufacturingDate: editForm.manufacturingDate || null,
      receivedDate: raw.receivedDate,
      supplier: editForm.supplier || '',
      status: editForm.isActive,
    })
    await loadAll()
    showEditModal.value = false
    formError.value = null
  } catch (e) {
    formError.value = `Failed to update batch: ${e.response?.data?.message || e.message}`
  } finally {
    isSaving.value = false
  }
}

/* --------------------------------- Activate / Deactivate batch (replaces Delete) --------------------------------- */
// Inventory batches are never deleted — they stay for reports, history, and audit logs.
const showDeactivateModal = ref(false)
const deactivateTarget = ref(null)
function confirmDeactivate(item) {
  deactivateTarget.value = item
  showDeactivateModal.value = true
  closeMenu()
}
async function submitDeactivate() {
  if (!deactivateTarget.value) return
  await setActiveState(deactivateTarget.value, false)
  showDeactivateModal.value = false
  deactivateTarget.value = null
}
async function activateBatch(item) {
  await setActiveState(item, true)
  closeMenu()
}
async function setActiveState(item, nextActive) {
  isSaving.value = true
  try {
    await api.updateInventory(item.inventoryID, {
      inventoryID: item.inventoryID,
      vaccineID: item.vaccineID,
      lotNumber: item.lotNumber,
      initialQuantity: item.initialQuantity,
      currentQuantity: item.currentQuantity,
      minimumStock: item.minimumStock,
      expirationDate: item.expirationDate,
      manufacturingDate: item.manufacturingDate || null,
      receivedDate: item.receivedDate,
      supplier: item.supplier || '',
      status: nextActive,
    })
    await loadAll()
    error.value = null
  } catch (e) {
    error.value = `Failed to update batch status: ${e.response?.data?.message || e.message}`
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900" @click="closeMenu">
    <!-- ============================ SIDEBAR (shared component) ============================ -->
    <AppSidebar v-model:active-nav="activeNav" @logout="handleLogout" />

    <!-- ============================ MAIN ============================ -->
    <div class="flex-1 min-w-0 flex flex-col">
      <AppHeader title="Inventory Management" breadcrumb="Admin / Inventory" />

      <main class="p-6 space-y-5">
        <div v-if="error" class="rounded-lg bg-rose-50 border border-rose-200 px-4 py-3 text-sm text-rose-700 flex items-center justify-between">
          <span>{{ error }}</span>
          <button @click="loadAll" class="font-semibold underline shrink-0 ml-3">Retry</button>
        </div>

        <!-- ============ Compact overview strip ============ -->
        <section class="bg-white border border-slate-200 rounded-xl shadow-sm px-5 py-3.5">
          <div class="flex flex-wrap items-center gap-x-8 gap-y-2">
            <div class="flex items-center gap-2">
              <span class="text-xs font-medium text-slate-500">Vaccine Types</span>
              <span class="text-base font-bold text-slate-900">{{ summary.totalVaccineTypes }}</span>
            </div>
            <div class="w-px h-5 bg-slate-200 hidden sm:block" />
            <div class="flex items-center gap-2">
              <span class="text-xs font-medium text-slate-500">Active Batches</span>
              <span class="text-base font-bold text-slate-900">{{ summary.totalBatches }}</span>
            </div>
            <div class="w-px h-5 bg-slate-200 hidden sm:block" />
            <div class="flex items-center gap-2">
              <span class="text-xs font-medium text-slate-500">Available Doses</span>
              <span class="text-base font-bold text-slate-900">{{ summary.totalDoses }}</span>
            </div>
            <div class="w-px h-5 bg-slate-200 hidden sm:block" />
            <div class="flex items-center gap-2">
              <span class="w-1.5 h-1.5 rounded-full bg-amber-500"></span>
              <span class="text-xs font-medium text-slate-500">Low Stock</span>
              <span class="text-base font-bold text-amber-700">{{ summary.lowStock }}</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="w-1.5 h-1.5 rounded-full bg-orange-500"></span>
              <span class="text-xs font-medium text-slate-500">Expiring ≤30d</span>
              <span class="text-base font-bold text-orange-700">{{ summary.expiringSoon }}</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="w-1.5 h-1.5 rounded-full bg-red-600"></span>
              <span class="text-xs font-medium text-slate-500">Expired</span>
              <span class="text-base font-bold text-red-700">{{ summary.expired }}</span>
            </div>
          </div>
        </section>

        <!-- ============ Inventory summary per vaccine ============ -->
        <InventorySummary :refresh-key="summaryKey" />

        <!-- ============ Toolbar ============ -->
        <section class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="flex flex-col lg:flex-row lg:items-center gap-3">
            <div class="relative flex-1 min-w-0">
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-sm" aria-hidden="true">🔍</span>
              <input
                v-model="searchQuery"
                type="text"
                placeholder="Search vaccine, lot number, or supplier..."
                class="w-full pl-9 pr-3 py-2 text-sm rounded-lg border border-slate-200 bg-slate-50 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
              />
            </div>

            <select v-model="vaccineFilter" class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors">
              <option value="All">All Vaccines</option>
              <option v-for="v in vaccines" :key="v.vaccineID" :value="v.vaccineID">
                {{ v.vaccineName }}{{ !isVaccineActive(v) ? ' (inactive)' : '' }}
              </option>
            </select>

            <select v-model="statusFilter" class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors">
              <option value="All">All Status</option>
              <option>Healthy</option>
              <option>Low Stock</option>
              <option>Expired</option>
            </select>

            <select v-model="expirationFilter" class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors">
              <option value="All">All Expirations</option>
              <option value="Expiring Soon">Expiring within 30 Days</option>
              <option value="Expired">Expired</option>
              <option value="Valid">Valid (30+ Days)</option>
            </select>

            <select v-model="activeFilter" class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors">
              <option value="All">Active + Inactive</option>
              <option value="Active">Active only</option>
              <option value="Inactive">Inactive only</option>
            </select>

            <div class="flex items-center gap-2 shrink-0">
              <button
                @click="openAlertModal"
                class="text-sm font-semibold px-4 py-2 rounded-lg border border-amber-300 text-amber-800 bg-amber-50 hover:bg-amber-100 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-400"
              >
                Alert Staff
              </button>
              <button
                @click="openReceiveModal"
                class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                + Receive New Batch
              </button>
            </div>
          </div>
        </section>

        <!-- ============ Table + Alerts ============ -->
        <section class="grid grid-cols-1 2xl:grid-cols-[minmax(0,1fr)_20rem] gap-6 items-start">
          <!-- Grouped inventory -->
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
            <div class="flex items-center justify-between px-5 py-3 border-b border-slate-200 bg-slate-50/60">
              <p class="text-xs font-semibold text-slate-500">
                {{ totalGroups }} vaccine{{ totalGroups === 1 ? '' : 's' }} · {{ totalBatchesFiltered }} batch{{ totalBatchesFiltered === 1 ? '' : 'es' }}
              </p>
              <div class="flex items-center gap-3">
                <button @click="expandAll" class="text-xs font-semibold text-emerald-700 hover:text-emerald-800">Expand all</button>
                <span class="text-slate-300">|</span>
                <button @click="collapseAll" class="text-xs font-semibold text-slate-500 hover:text-slate-700">Collapse all</button>
              </div>
            </div>

            <div v-if="isLoading && inventory.length === 0" class="px-5 py-12 text-center text-sm text-slate-400">Loading inventory…</div>

            <div v-else-if="paginatedGroups.length === 0" class="px-5 py-12 text-center text-sm text-slate-400">
              {{ inventory.length === 0 ? 'No inventory batches yet. Click "Receive New Batch" to add one.' : 'No batches match your search or filters.' }}
            </div>

            <div v-else class="divide-y divide-slate-100">
              <div v-for="group in paginatedGroups" :key="group.vaccineID">
                <!-- Group header: e.g. "BCG Vaccine" containing its lots -->
                <button
                  @click="toggleGroup(group.vaccineID)"
                  class="w-full flex items-center gap-3 px-5 py-3.5 hover:bg-slate-50 transition-colors text-left"
                >
                  <span
                    class="text-slate-400 transition-transform text-xs shrink-0"
                    :class="expandedGroups.has(group.vaccineID) ? 'rotate-90' : ''"
                  >▶</span>
                  <span class="font-semibold text-slate-900 text-sm">{{ group.vaccineName }}</span>
                  <span v-if="!group.vaccineIsActive" class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-slate-200 text-slate-500 whitespace-nowrap">Vaccine inactive</span>
                  <span class="text-xs text-slate-400">
                    {{ group.batchCount }} batch{{ group.batchCount === 1 ? '' : 'es' }} · {{ group.totalDoses }} doses
                  </span>
                  <span class="flex-1"></span>
                  <span v-if="group.hasExpired" class="text-xs font-semibold px-2 py-0.5 rounded-full bg-red-100 text-red-800">Expired</span>
                  <span v-else-if="group.hasCritical" class="text-xs font-semibold px-2 py-0.5 rounded-full bg-amber-50 text-amber-700">Low stock</span>
                  <span v-else-if="group.hasExpiring" class="text-xs font-semibold px-2 py-0.5 rounded-full bg-orange-50 text-orange-700">Expiring soon</span>
                </button>

                <!-- Batch rows (lots) under this vaccine group -->
                <div v-if="expandedGroups.has(group.vaccineID)" class="bg-slate-50/40 overflow-x-auto">
                  <table class="w-full text-sm">
                    <thead>
                      <tr class="text-left text-xs uppercase tracking-wide text-slate-400 whitespace-nowrap">
                        <th class="font-medium pl-12 pr-3 py-2">Lot Number</th>
                        <th class="font-medium px-3 py-2">Current / Min</th>
                        <th class="font-medium px-3 py-2">Manufactured</th>
                        <th class="font-medium px-3 py-2">Expiration</th>
                        <th class="font-medium px-3 py-2">Supplier</th>
                        <th class="font-medium px-3 py-2">Status</th>
                        <th class="font-medium px-3 py-2">Lifecycle</th>
                        <th class="font-medium pr-5 py-2 text-right">Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr
                        v-for="(item, idx) in group.batches"
                        :key="item.inventoryID"
                        class="border-t border-slate-100 hover:bg-white transition-colors"
                        :class="!isActive(item) ? 'opacity-60' : ''"
                      >
                        <td class="pl-12 pr-3 py-2.5">
                          <button @click.stop="openDrawer(item)" class="flex items-center gap-2 hover:underline decoration-slate-300 underline-offset-2">
                            <span class="font-mono text-xs text-slate-600 whitespace-nowrap">{{ item.lotNumber }}</span>
                            <span v-if="idx === 0 && isActive(item)" class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 whitespace-nowrap">Use next</span>
                          </button>
                        </td>
                        <td class="px-3 py-2.5 whitespace-nowrap">
                          <span class="font-semibold text-slate-900">{{ item.currentQuantity }}</span>
                          <span class="text-slate-400"> / {{ item.minimumStock }}</span>
                        </td>
                        <td class="px-3 py-2.5 whitespace-nowrap text-slate-500">{{ item.manufacturingDate ? formatDate(item.manufacturingDate) : '—' }}</td>
                        <td class="px-3 py-2.5 whitespace-nowrap text-slate-600">{{ formatDate(item.expirationDate) }}</td>
                        <td class="px-3 py-2.5 min-w-[10rem] max-w-[14rem] text-slate-500 leading-snug">{{ item.supplier || '—' }}</td>
                        <td class="px-3 py-2.5">
                          <span :class="[statusMeta[computeStatus(item)].tint, statusMeta[computeStatus(item)].text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap">
                            <span :class="statusMeta[computeStatus(item)].dot" class="w-1.5 h-1.5 rounded-full"></span>
                            {{ computeStatus(item) }}
                          </span>
                        </td>
                        <td class="px-3 py-2.5">
                          <span
                            class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap"
                            :class="isActive(item) ? 'bg-slate-100 text-slate-600' : 'bg-slate-200 text-slate-500'"
                          >
                            {{ isActive(item) ? 'Active' : 'Inactive' }}
                          </span>
                          <span v-if="shouldRecommendDeactivation(item)" class="block text-[11px] text-amber-700 mt-1">Recommend deactivating</span>
                        </td>
                        <td class="pr-5 py-2.5 text-right relative">
                          <button
                            @click.stop="toggleMenu(item.inventoryID, $event)"
                            class="text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg w-8 h-8 inline-flex items-center justify-center transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                          >⋮</button>

                          <Teleport to="body">
                          <div
                            v-if="openMenuId === item.inventoryID"
                            @click.stop
                            :style="menuStyle" class="fixed z-50 w-44 bg-white border border-slate-200 rounded-lg shadow-md py-1 text-left"
                          >
                            <button @click="openDrawer(item)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors">View Details</button>
                            <button @click="openEditModal(item)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors">Edit Batch</button>
                            <button
                              v-if="isActive(item)"
                              @click="confirmDeactivate(item)"
                              class="w-full text-left px-3.5 py-2 text-sm text-amber-700 hover:bg-amber-50 transition-colors"
                            >Deactivate</button>
                            <button
                              v-else
                              @click="activateBatch(item)"
                              class="w-full text-left px-3.5 py-2 text-sm text-emerald-700 hover:bg-emerald-50 transition-colors"
                            >Activate</button>
                          </div>
                          </Teleport>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <!-- Pagination -->
            <div class="flex flex-wrap items-center justify-between gap-3 px-5 py-3 border-t border-slate-200">
              <div class="flex items-center gap-2 text-xs text-slate-500">
                <span>Show</span>
                <select v-model.number="pageSize" class="text-xs rounded-lg border border-slate-200 bg-slate-50 px-2 py-1 focus:outline-none focus:ring-2 focus:ring-emerald-500">
                  <option :value="10">10</option>
                  <option :value="25">25</option>
                  <option :value="50">50</option>
                  <option :value="100">100</option>
                </select>
                <span>per page · {{ totalGroups }} vaccine{{ totalGroups === 1 ? '' : 's' }} total</span>
              </div>
              <div class="flex items-center gap-2">
                <button
                  @click="goToPage(currentPage - 1)"
                  :disabled="currentPage <= 1"
                  class="text-xs font-semibold px-3 py-1.5 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >Previous</button>
                <span class="text-xs text-slate-500 px-1">Page {{ currentPage }} of {{ totalPages }}</span>
                <button
                  @click="goToPage(currentPage + 1)"
                  :disabled="currentPage >= totalPages"
                  class="text-xs font-semibold px-3 py-1.5 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >Next</button>
              </div>
            </div>
          </div>

          <!-- Inventory Alerts panel -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm p-5 space-y-5">
            <h2 class="text-sm font-bold text-slate-900">Inventory Alerts</h2>

            <div>
              <p class="text-xs font-bold uppercase tracking-wide text-red-600 mb-2">Critical Stock</p>
              <div v-if="criticalStock.length === 0" class="text-xs text-slate-400">No critical batches.</div>
              <div v-for="i in criticalStock" :key="'c-' + i.inventoryID" class="rounded-lg bg-red-50 px-3 py-2.5 mb-2 last:mb-0">
                <p class="text-sm font-semibold text-slate-900">{{ vaccineName(i.vaccineID) }}</p>
                <div class="flex items-center justify-between mt-0.5">
                  <span class="text-xs text-slate-500 font-mono">{{ i.lotNumber }}</span>
                  <span class="text-xs font-semibold text-red-700">{{ i.currentQuantity }} left</span>
                </div>
              </div>
            </div>

            <div>
              <p class="text-xs font-bold uppercase tracking-wide text-amber-600 mb-2">Expiring Soon</p>
              <div v-if="expiringSoonList.length === 0" class="text-xs text-slate-400">Nothing expiring within 30 days.</div>
              <div v-for="i in expiringSoonList" :key="'e-' + i.inventoryID" class="rounded-lg bg-amber-50 px-3 py-2.5 mb-2 last:mb-0">
                <p class="text-sm font-semibold text-slate-900">{{ vaccineName(i.vaccineID) }}</p>
                <div class="flex items-center justify-between mt-0.5">
                  <span class="text-xs text-slate-500 font-mono">{{ i.lotNumber }}</span>
                  <span class="text-xs font-semibold text-amber-700">{{ daysUntil(i.expirationDate) }} days left</span>
                </div>
              </div>
            </div>

            <div>
              <p class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-2">Expired</p>
              <div v-if="expiredList.length === 0" class="text-xs text-slate-400">No expired batches.</div>
              <div v-for="i in expiredList" :key="'x-' + i.inventoryID" class="rounded-lg bg-slate-100 px-3 py-2.5 mb-2 last:mb-0">
                <p class="text-sm font-semibold text-slate-900">{{ vaccineName(i.vaccineID) }}</p>
                <div class="flex items-center justify-between mt-0.5">
                  <span class="text-xs text-slate-500 font-mono">{{ i.lotNumber }}</span>
                  <span class="text-xs font-semibold text-slate-600">{{ i.currentQuantity }} left</span>
                </div>
              </div>
            </div>
          </div>
        </section>
      </main>
    </div>

    <!-- ============================ ALERT STAFF MODAL ============================ -->
    <transition name="fade">
      <div v-if="showAlertModal" class="fixed inset-0 bg-slate-900/40 z-40 flex items-center justify-center p-4" @click.self="showAlertModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-lg">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <div>
              <h2 class="text-base font-bold text-slate-900">Alert Staff</h2>
              <p class="text-xs text-slate-500">Every Staff / Nurse gets this in their notification bell.</p>
            </div>
            <button @click="showAlertModal = false" class="text-slate-400 hover:text-slate-600 text-lg leading-none" aria-label="Close">✕</button>
          </div>
          <div v-if="alertSent" class="px-6 py-8 text-center">
            <p class="text-sm font-semibold text-emerald-700">{{ alertSent }}</p>
          </div>
          <div v-else class="px-6 py-5 space-y-3">
            <label for="alert-message" class="block text-xs font-medium text-slate-600">What should the staff request or order?</label>
            <textarea
              id="alert-message"
              v-model="alertMessage"
              rows="5"
              maxlength="500"
              placeholder="e.g. Please request 30 doses of BCG from the pharmacy before Friday."
              class="w-full text-sm rounded-lg border border-slate-200 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            ></textarea>
            <p class="text-[11px] text-slate-400 text-right">{{ alertMessage.length }}/500</p>
            <p v-if="alertError" class="text-xs text-rose-600 bg-rose-50 border border-rose-100 rounded-lg px-3 py-2">{{ alertError }}</p>
          </div>
          <div v-if="!alertSent" class="flex justify-end gap-2 px-6 py-4 border-t border-slate-200 bg-slate-50 rounded-b-xl">
            <button @click="showAlertModal = false" class="px-4 py-2 text-sm font-medium text-slate-600 rounded-lg hover:bg-slate-100">Cancel</button>
            <button
              @click="sendAlert"
              :disabled="isSendingAlert || !alertMessage.trim()"
              class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {{ isSendingAlert ? 'Sending…' : 'Send to Staff' }}
            </button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ RECEIVE NEW BATCH MODAL ============================ -->
    <transition name="fade">
      <div v-if="showReceiveModal" class="fixed inset-0 bg-slate-900/40 z-40 flex items-center justify-center p-4" @click.self="showReceiveModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-2xl max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Receive New Batch</h2>
            <button @click="showReceiveModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
          </div>
          <p class="px-6 pt-4 text-xs text-slate-500">Receiving stock always creates a new batch — it never overwrites an existing lot.</p>

          <div class="p-6 grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="sm:col-span-2">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Vaccine</label>
              <!-- Only ACTIVE vaccines are selectable here. Inactive vaccines are
                   filtered out before they ever reach this dropdown. -->
              <select v-model="receiveForm.vaccineID" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors">
                <option value="" disabled>Select a vaccine…</option>
                <option v-for="v in activeVaccines" :key="v.vaccineID" :value="v.vaccineID">{{ v.vaccineName }}</option>
              </select>
              <p v-if="activeVaccines.length === 0" class="text-xs text-rose-600 mt-1.5">
                No active vaccines available. Activate a vaccine under Vaccine Management before receiving new stock.
              </p>
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Lot Number</label>
              <input v-model="receiveForm.lotNumber" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Initial Quantity</label>
              <input v-model="receiveForm.initialQuantity" v-digits type="number" min="1" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Minimum Stock</label>
              <input v-model="receiveForm.minimumStock" v-digits type="number" min="0" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Expiration Date</label>
              <input v-model="receiveForm.expirationDate" type="date" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Manufacturing Date <span class="font-normal text-slate-400">(optional)</span></label>
              <input v-model="receiveForm.manufacturingDate" type="date" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Received Date</label>
              <input v-model="receiveForm.receivedDate" type="date" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Supplier</label>
              <input v-model="receiveForm.supplier" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div v-if="formError" class="sm:col-span-2 rounded-lg bg-rose-50 px-3 py-2.5 text-xs text-rose-700">{{ formError }}</div>
          </div>

          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showReceiveModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Cancel</button>
            <button :disabled="isSaving || activeVaccines.length === 0" @click="submitReceiveStock" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors disabled:opacity-50">
              {{ isSaving ? 'Saving…' : 'Receive Batch' }}
            </button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ EDIT MODAL (restricted fields) ============================ -->
    <transition name="fade">
      <div v-if="showEditModal" class="fixed inset-0 bg-slate-900/40 z-40 flex items-center justify-center p-4" @click.self="showEditModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-lg max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Edit Batch</h2>
            <button @click="showEditModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
          </div>

          <div class="p-6 grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="sm:col-span-2">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Lot Number</label>
              <input v-model="editForm.lotNumber" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Minimum Stock</label>
              <input v-model="editForm.minimumStock" v-digits type="number" min="0" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Expiration Date</label>
              <input v-model="editForm.expirationDate" type="date" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Manufacturing Date <span class="font-normal text-slate-400">(optional)</span></label>
              <input v-model="editForm.manufacturingDate" type="date" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div class="sm:col-span-2">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Supplier</label>
              <input v-model="editForm.supplier" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div class="sm:col-span-2 flex items-center gap-2 pt-1">
              <input id="editIsActive" v-model="editForm.isActive" type="checkbox" class="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500" />
              <label for="editIsActive" class="text-sm font-medium text-slate-700">Active (available for vaccination)</label>
            </div>
            <p class="sm:col-span-2 text-xs text-slate-400 -mt-2">Current quantity and initial quantity aren't editable here — they update automatically through vaccination administration.</p>
            <div v-if="formError" class="sm:col-span-2 rounded-lg bg-rose-50 px-3 py-2.5 text-xs text-rose-700">{{ formError }}</div>
          </div>

          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showEditModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Cancel</button>
            <button :disabled="isSaving" @click="submitEdit" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors disabled:opacity-50">
              {{ isSaving ? 'Saving…' : 'Save Changes' }}
            </button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ DEACTIVATE CONFIRM MODAL ============================ -->
    <transition name="fade">
      <div v-if="showDeactivateModal" class="fixed inset-0 bg-slate-900/40 z-40 flex items-center justify-center p-4" @click.self="showDeactivateModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-sm">
          <div class="p-6 text-center">
            <div class="w-12 h-12 rounded-full bg-amber-50 text-amber-600 flex items-center justify-center mx-auto mb-3 text-xl">⏸️</div>
            <p class="text-sm font-bold text-slate-900 mb-1">Deactivate this batch?</p>
            <p class="text-xs text-slate-500">
              Lot <span class="font-mono">{{ deactivateTarget?.lotNumber }}</span> ({{ vaccineName(deactivateTarget?.vaccineID) }}) will no longer be available for vaccination, but stays in reports and audit history. You can reactivate it anytime.
            </p>
          </div>
          <div class="flex items-center justify-center gap-2 px-6 pb-6">
            <button @click="showDeactivateModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Cancel</button>
            <button :disabled="isSaving" @click="submitDeactivate" class="text-sm font-semibold px-4 py-2 rounded-lg bg-amber-600 text-white hover:bg-amber-700 transition-colors disabled:opacity-50">
              {{ isSaving ? 'Saving…' : 'Deactivate Batch' }}
            </button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ BATCH DETAILS DRAWER ============================ -->
    <transition name="fade">
      <div v-if="showDrawer" class="fixed inset-0 bg-slate-900/30 z-40" @click="closeDrawer"></div>
    </transition>
    <transition name="slide">
      <aside v-if="showDrawer" class="fixed top-0 right-0 h-screen w-full max-w-lg bg-white border-l border-slate-200 shadow-lg z-50 flex flex-col">
        <div class="h-[70px] flex items-center justify-between px-6 border-b border-slate-200 shrink-0">
          <h2 class="text-sm font-bold text-slate-900">Batch Details</h2>
          <button @click="closeDrawer" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
        </div>

        <div v-if="selectedBatch" class="flex-1 overflow-y-auto p-6 space-y-4">
          <div class="bg-slate-50 rounded-xl border border-slate-200 p-5">
            <div class="flex items-start justify-between gap-3 mb-4">
              <div>
                <p class="text-base font-bold text-slate-900">{{ vaccineName(selectedBatch.vaccineID) }}</p>
                <p class="text-xs text-slate-500 font-mono mt-0.5">{{ selectedBatch.lotNumber }}</p>
              </div>
              <span :class="[statusMeta[computeStatus(selectedBatch)].tint, statusMeta[computeStatus(selectedBatch)].text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full shrink-0">
                <span :class="statusMeta[computeStatus(selectedBatch)].dot" class="w-1.5 h-1.5 rounded-full"></span>
                {{ computeStatus(selectedBatch) }}
              </span>
            </div>
            <div class="grid grid-cols-2 gap-x-4 gap-y-3">
              <div><p class="text-xs text-slate-500">Supplier</p><p class="text-sm font-medium text-slate-900">{{ selectedBatch.supplier || '—' }}</p></div>
              <div><p class="text-xs text-slate-500">Lifecycle</p><p class="text-sm font-medium text-slate-900">{{ isActive(selectedBatch) ? 'Active' : 'Inactive' }}</p></div>
              <div><p class="text-xs text-slate-500">Initial Quantity</p><p class="text-sm font-medium text-slate-900">{{ selectedBatch.initialQuantity }}</p></div>
              <div><p class="text-xs text-slate-500">Current Quantity</p><p class="text-sm font-medium text-slate-900">{{ selectedBatch.currentQuantity }}</p></div>
              <div><p class="text-xs text-slate-500">Minimum Stock</p><p class="text-sm font-medium text-slate-900">{{ selectedBatch.minimumStock }}</p></div>
              <div><p class="text-xs text-slate-500">Expiration Date</p><p class="text-sm font-medium text-slate-900">{{ formatDate(selectedBatch.expirationDate) }}</p></div>
              <div><p class="text-xs text-slate-500">Manufacturing Date</p><p class="text-sm font-medium text-slate-900">{{ selectedBatch.manufacturingDate ? formatDate(selectedBatch.manufacturingDate) : '—' }}</p></div>
              <div><p class="text-xs text-slate-500">Received Date</p><p class="text-sm font-medium text-slate-900">{{ formatDate(selectedBatch.receivedDate) }}</p></div>
            </div>
          </div>
          <p class="text-xs text-slate-400">
            Detailed movement/audit history isn't wired up here yet — your current API doesn't expose a per-batch history
            endpoint. Add one on the backend (e.g. an AuditLog tied to InventoryID) and this drawer can list it here.
          </p>
        </div>

        <div class="border-t border-slate-200 p-4 flex items-center gap-2 shrink-0">
          <button @click="closeDrawer(); openEditModal(selectedBatch)" class="flex-1 text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors">Edit Batch</button>
          <button @click="closeDrawer" class="text-sm font-semibold px-4 py-2 rounded-lg text-slate-500 hover:bg-slate-50 transition-colors">Close</button>
        </div>
      </aside>
    </transition>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.slide-enter-active, .slide-leave-active { transition: transform 0.25s ease; }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }

@media (prefers-reduced-motion: reduce) {
  * { transition-duration: 0.01ms !important; }
}
</style>