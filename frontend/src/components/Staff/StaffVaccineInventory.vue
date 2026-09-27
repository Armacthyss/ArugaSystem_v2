<template>
  <div class="flex h-screen bg-stone-50 antialiased text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">

    <StaffSidebar />

    <!-- MAIN SECTION -->
    <div class="flex-1 flex flex-col overflow-hidden">
      <StaffTopbar title="Vaccine Inventory" breadcrumb="Aruga / Inventory">
        <button @click="openAddModal" :disabled="isLoading" class="bg-emerald-600 text-white px-5 py-2.5 rounded-lg text-xs font-bold hover:bg-emerald-700 transition-all flex items-center gap-2 shadow-sm disabled:opacity-50">
          <Plus class="w-4 h-4" /> ADD NEW BATCH
        </button>
      </StaffTopbar>

      <main class="flex-1 p-8 overflow-y-auto custom-scrollbar">
        <div class="max-w-7xl mx-auto">

          <div class="flex items-center justify-between mb-2">
            <div></div>
            <button
              @click="fetchAll"
              class="text-slate-400 hover:text-emerald-600 transition-colors p-2 rounded-full hover:bg-slate-100"
            >
              <RefreshCw class="w-5 h-5" :class="{ 'animate-spin': isLoading }" />
            </button>
          </div>

          <!-- WEEKLY STOCK CHECK -->
          <div class="bg-white rounded-2xl border border-slate-200 shadow-sm mb-6 overflow-hidden">
            <div class="px-6 py-4 border-b border-slate-100 flex items-start justify-between gap-4 flex-wrap">
              <div>
                <p class="text-sm font-bold text-slate-800">Weekly Stock Check</p>
                <p class="text-[11.5px] text-slate-500 mt-0.5">
                  Sent to the Admission Staff and the Administrator every {{ stockCheck.checkDay || 'Wednesday' }} at 8:00 AM.
                  Suggested orders cover two weeks plus the minimum stock, in case the pharmacy can only deliver next week.
                </p>
              </div>
              <button @click="sendStockCheck" :disabled="sendingCheck" class="shrink-0 text-xs font-bold px-3.5 py-2 rounded-lg border border-emerald-200 text-emerald-700 hover:bg-emerald-50 disabled:opacity-50">
                {{ sendingCheck ? 'Sending…' : 'Send to Staff & Admin now' }}
              </button>
            </div>
            <p v-if="stockCheckMessage" class="px-6 pt-3 text-xs font-semibold text-emerald-700">{{ stockCheckMessage }}</p>
            <div class="overflow-x-auto">
            <table class="w-full text-left text-sm">
              <thead class="text-[10px] uppercase font-bold text-slate-400">
                <tr>
                  <th class="px-6 py-3">Vaccine</th>
                  <th class="px-3 py-3 text-right">On hand</th>
                  <th class="px-3 py-3 text-right">Due this week</th>
                  <th class="px-3 py-3 text-right">Next 2 weeks</th>
                  <th class="px-3 py-3">Status</th>
                  <th class="px-6 py-3 text-right">Order</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="l in stockCheck.lines" :key="l.vaccineID" :class="l.restock ? 'bg-rose-50/40' : ''">
                  <td class="px-6 py-2.5">
                    <p class="font-semibold text-slate-800">{{ l.vaccine }}</p>
                    <p v-if="l.expiringSoon" class="text-[11px] text-amber-700">{{ l.expiringSoon }} doses expire by {{ formatDate(l.nextExpiry) }}: use these first</p>
                  </td>
                  <td class="px-3 py-2.5 text-right font-semibold">{{ l.onHand }}</td>
                  <td class="px-3 py-2.5 text-right text-slate-600">{{ l.dueThisWeek }}</td>
                  <td class="px-3 py-2.5 text-right text-slate-600">{{ l.dueTwoWeeks }}</td>
                  <td class="px-3 py-2.5">
                    <span class="text-[11px] font-bold px-2 py-0.5 rounded-full"
                      :class="l.status === 'OK' ? 'bg-emerald-50 text-emerald-700' : l.status === 'Low' ? 'bg-amber-50 text-amber-700' : 'bg-rose-50 text-rose-700'">
                      {{ l.status }}
                    </span>
                  </td>
                  <td class="px-6 py-2.5 text-right">
                    <span v-if="l.restock" class="font-bold text-rose-700">about {{ l.suggestedOrder }}</span>
                    <span v-else class="text-slate-400">—</span>
                  </td>
                </tr>
                <tr v-if="!stockCheck.lines?.length">
                  <td colspan="6" class="px-6 py-4 text-sm text-slate-400">Loading…</td>
                </tr>
              </tbody>
            </table>
            </div>
          </div>

          <!-- SUMMARY STATS (computed from real inventory data) -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
            <div class="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-4">
              <div class="w-12 h-12 bg-blue-50 text-blue-600 rounded-xl flex items-center justify-center"><Package class="w-6 h-6" /></div>
              <div>
                <p class="text-[10px] font-bold text-slate-400 uppercase">Total Doses</p>
                <p class="text-xl font-bold text-slate-800">{{ totalDoses.toLocaleString() }}</p>
              </div>
            </div>
            <div class="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-4">
              <div class="w-12 h-12 bg-amber-50 text-amber-600 rounded-xl flex items-center justify-center"><AlertTriangle class="w-6 h-6" /></div>
              <div>
                <p class="text-[10px] font-bold text-slate-400 uppercase">Expiring Soon</p>
                <p class="text-xl font-bold text-slate-800">{{ expiringSoonCount }} Batches</p>
              </div>
            </div>
            <div class="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-4">
              <div class="w-12 h-12 bg-rose-50 text-rose-600 rounded-xl flex items-center justify-center"><Trash2 class="w-6 h-6" /></div>
              <div>
                <p class="text-[10px] font-bold text-slate-400 uppercase">Expired Stocks</p>
                <p class="text-xl font-bold text-slate-800">{{ expiredCount }}</p>
              </div>
            </div>
          </div>

          <!-- INVENTORY TABLE -->
          <div class="bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
            <table class="w-full text-left text-sm">
              <thead class="bg-slate-50/50 border-b border-slate-200 text-[11px] uppercase font-bold text-slate-500">
                <tr>
                  <th class="px-6 py-4">Vaccine Name</th>
                  <th class="px-6 py-4">Lot Number</th>
                  <th class="px-6 py-4">Received</th>
                  <th class="px-6 py-4">Expiry Date</th>
                  <th class="px-6 py-4">Stock Level</th>
                  <th class="px-6 py-4 text-right">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="batch in paginatedInventory" :key="batch.inventoryID" class="hover:bg-slate-50/50 transition-colors">
                  <td class="px-6 py-4 font-bold text-slate-800">{{ vaccineName(batch.vaccineID) }}</td>
                  <td class="px-6 py-4">
                    <span class="bg-slate-100 text-slate-600 px-2 py-1 rounded text-[10px] font-mono font-bold tracking-widest uppercase">
                      #{{ batch.lotNumber }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-slate-500">{{ formatDate(batch.receivedDate) }}</td>
                  <td class="px-6 py-4">
                    <span :class="isExpired(batch.expirationDate) ? 'text-rose-600 font-bold' : (isNearExpiry(batch.expirationDate) ? 'text-amber-600 font-bold' : 'text-slate-600')">
                      {{ formatDate(batch.expirationDate) }}
                    </span>
                  </td>
                  <td class="px-6 py-4">
                    <div class="flex items-center gap-2">
                      <div class="w-16 bg-slate-100 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-emerald-500 h-full" :style="{ width: stockBarWidth(batch) + '%' }"></div>
                      </div>
                      <span class="text-xs font-bold">{{ batch.currentQuantity }}</span>
                    </div>
                  </td>
                  <td class="px-6 py-4 text-right">
                    <span :class="statusClass(batch)" class="px-2 py-0.5 rounded-full text-[10px] font-bold uppercase">
                      {{ statusLabel(batch) }}
                    </span>
                  </td>
                </tr>
                <tr v-if="inventory.length === 0 && !isLoading">
                  <td colspan="6" class="px-6 py-20 text-center">
                    <div class="flex flex-col items-center">
                      <Package class="w-12 h-12 text-slate-200 mb-2" />
                      <p class="text-slate-400 font-medium">No inventory batches found.</p>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
            </div>
            <!-- PAGINATION -->
            <div v-if="inventory.length > 0" class="flex items-center justify-between px-6 py-3.5 border-t border-slate-100 flex-wrap gap-3">
              <div class="flex items-center gap-3">
                <div class="flex items-center gap-2 text-xs text-slate-500">
                  <span>Show</span>
                  <select v-model="pageSize" @change="currentPage = 1"
                    class="border border-slate-200 rounded-lg px-2 py-1 text-xs text-slate-600 outline-none bg-white">
                    <option :value="10">10</option>
                    <option :value="20">20</option>
                    <option :value="30">30</option>
                    <option :value="50">50</option>
                  </select>
                  <span>entries</span>
                </div>
                <p class="text-xs text-slate-400">
                  Showing {{ Math.min((currentPage - 1) * pageSize + 1, inventory.length) }}–{{ Math.min(currentPage * pageSize, inventory.length) }}
                  of {{ inventory.length }} batches
                </p>
              </div>
              <div class="flex items-center gap-1">
                <button @click="currentPage--" :disabled="currentPage === 1"
                  class="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
                  <ChevronLeft class="w-4 h-4" />
                </button>
                <template v-for="p in paginationPages" :key="p">
                  <span v-if="p === '...'" class="px-1 text-xs text-slate-400">…</span>
                  <button v-else @click="currentPage = p"
                    class="min-w-[28px] h-7 rounded-lg text-xs font-medium transition-colors"
                    :class="p === currentPage ? 'bg-emerald-600 text-white' : 'text-slate-600 hover:bg-slate-100'">
                    {{ p }}
                  </button>
                </template>
                <button @click="currentPage++" :disabled="currentPage >= totalPages"
                  class="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
                  <ChevronRight class="w-4 h-4" />
                </button>
              </div>
            </div>
          </div>
        </div>
      </main>

      <!-- ADD BATCH MODAL -->
      <div v-if="showAddModal" class="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
        <div class="bg-white w-full max-w-md rounded-2xl shadow-2xl overflow-hidden border border-slate-200">
          <div class="p-6 border-b border-slate-100 flex justify-between items-center">
            <h3 class="font-bold text-slate-800">Register New Batch</h3>
            <button @click="showAddModal = false" class="text-slate-400 hover:text-slate-600"><X class="w-5 h-5" /></button>
          </div>

          <div class="p-6 space-y-4">
            <div>
              <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Vaccine</label>
              <select v-model="batchForm.VaccineID" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
                <option :value="null" disabled>Select a vaccine…</option>
                <option v-for="v in vaccineCatalog" :key="v.vaccineID" :value="v.vaccineID">
                  {{ v.vaccineName || v.name }}
                </option>
              </select>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Lot Number</label>
                <input v-model="batchForm.LotNumber" type="text" placeholder="LOT-99X" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
              </div>
              <div>
                <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Doses Received</label>
                <input v-model.number="batchForm.InitialQuantity" v-digits type="number" min="0" placeholder="0" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Received Date</label>
                <input v-model="batchForm.ReceivedDate" type="date" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
              </div>
              <div>
                <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Expiration Date</label>
                <input v-model="batchForm.ExpirationDate" type="date" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
              </div>
            </div>

            <div>
              <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Manufacturing Date (optional)</label>
              <input v-model="batchForm.ManufacturingDate" type="date" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Minimum Stock</label>
                <input v-model.number="batchForm.MinimumStock" v-digits type="number" min="0" placeholder="e.g. 50" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
              </div>
              <div>
                <label class="block text-[10px] font-bold text-slate-400 uppercase mb-1">Supplier</label>
                <input v-model="batchForm.Supplier" type="text" placeholder="Optional" class="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm focus:border-emerald-500 outline-none">
              </div>
            </div>
          </div>

          <div class="p-4 bg-slate-50 border-t border-slate-100 flex gap-2">
             <button @click="showAddModal = false" class="flex-1 py-2.5 text-xs font-bold text-slate-500 hover:bg-slate-100 rounded-lg">CANCEL</button>
             <button @click="handleSaveBatch" :disabled="isSaving" class="flex-1 py-2.5 text-xs font-bold bg-emerald-600 text-white hover:bg-emerald-700 rounded-lg shadow-sm transition-all disabled:opacity-50">
               {{ isSaving ? 'SAVING…' : 'ADD TO INVENTORY' }}
             </button>
          </div>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { toISODate } from '@/utils/format'
import { ref, reactive, computed, onMounted } from 'vue'
import axios from 'axios'
import { Plus, Package, AlertTriangle, Trash2, X, RefreshCw, ChevronLeft, ChevronRight } from 'lucide-vue-next'
import StaffSidebar from './StaffSidebar.vue'
import StaffTopbar from './StaffTopbar.vue'

// Same API_BASE convention as the rest of the project.
const API_BASE = `${API_ORIGIN}/api`

const inventory = ref([])       // raw VaccineInventory rows from the backend
const vaccineCatalog = ref([])  // Vaccine catalog, used to resolve vaccineID -> name
const isLoading = ref(false)
const isSaving = ref(false)
const showAddModal = ref(false)

// ── PAGINATION ────────────────────────────────────────────────────
const currentPage = ref(1)
const pageSize    = ref(10)
const totalPages  = computed(() => Math.max(1, Math.ceil(inventory.value.length / pageSize.value)))
const paginatedInventory = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return inventory.value.slice(start, start + pageSize.value)
})
const paginationPages = computed(() => {
  const total = totalPages.value
  const cur   = currentPage.value
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const pages = []
  pages.push(1)
  if (cur > 3) pages.push('...')
  for (let p = Math.max(2, cur - 1); p <= Math.min(total - 1, cur + 1); p++) pages.push(p)
  if (cur < total - 2) pages.push('...')
  pages.push(total)
  return pages
})

const batchForm = reactive({
  VaccineID: null, LotNumber: '', InitialQuantity: null,
  ReceivedDate: '', ExpirationDate: '', ManufacturingDate: '', MinimumStock: null, Supplier: '',
})

const openAddModal = () => {
  Object.assign(batchForm, {
    VaccineID: null, LotNumber: '', InitialQuantity: null,
    ReceivedDate: '', ExpirationDate: '', ManufacturingDate: '', MinimumStock: null, Supplier: '',
  })
  showAddModal.value = true
  if (vaccineCatalog.value.length === 0) fetchVaccineCatalog()
}

// ── DATA FETCHING ─────────────────────────────────────────────────
// VaccineInventoryController: GET /api/VaccineInventory (list), POST /api/VaccineInventory (create).
const fetchInventory = async () => {
  isLoading.value = true
  try {
    const res = await axios.get(`${API_BASE}/VaccineInventory`)
    inventory.value = res.data
  } catch (err) {
    console.error('Fetch inventory error:', err)
  } finally {
    isLoading.value = false
  }
}

// Vaccine catalog is a separate table (VaccineInventory only stores a VaccineID FK,
// not a name) — same catalog endpoint the Doctor-side vaccine pickers use.
const fetchVaccineCatalog = async () => {
  try {
    const res = await axios.get(`${API_BASE}/Vaccines/with-doses`)
    vaccineCatalog.value = res.data
  } catch (err) {
    console.error('Fetch vaccine catalog error:', err)
  }
}

// ── WEEKLY STOCK CHECK ────────────────────────────────────────────
// GET /api/VaccineInventory/stock-check: per vaccine on hand vs. due, and a
// suggested order. The same check is sent to Staff & Admin every check day.
const stockCheck = ref({ lines: [] })
const sendingCheck = ref(false)
const stockCheckMessage = ref('')

const fetchStockCheck = async () => {
  try {
    stockCheck.value = (await axios.get(`${API_BASE}/VaccineInventory/stock-check`)).data
  } catch (err) {
    console.error('Stock check error:', err)
  }
}

const sendStockCheck = async () => {
  sendingCheck.value = true
  stockCheckMessage.value = ''
  try {
    stockCheckMessage.value = (await axios.post(`${API_BASE}/VaccineInventory/stock-check/send`)).data.message
  } catch (err) {
    stockCheckMessage.value = err.response?.data?.message || 'Could not send the stock check.'
  } finally {
    sendingCheck.value = false
  }
}

const fetchAll = () => {
  fetchInventory()
  fetchVaccineCatalog()
  fetchStockCheck()
}

const handleSaveBatch = async () => {
  if (!batchForm.VaccineID || !batchForm.LotNumber || !batchForm.ExpirationDate) {
    alert('Vaccine, Lot Number, and Expiration Date are required.')
    return
  }
  isSaving.value = true
  try {
    const initialQty = batchForm.InitialQuantity ?? 0
    const payload = {
      vaccineID: batchForm.VaccineID,
      lotNumber: batchForm.LotNumber,
      initialQuantity: initialQty,
      currentQuantity: initialQty, // a brand-new batch starts fully stocked
      minimumStock: batchForm.MinimumStock ?? 0,
      expirationDate: batchForm.ExpirationDate,
      manufacturingDate: batchForm.ManufacturingDate || null,
      receivedDate: batchForm.ReceivedDate || toISODate(),
      supplier: batchForm.Supplier || null,
      status: true,
    }
    await axios.post(`${API_BASE}/VaccineInventory`, payload)
    showAddModal.value = false
    fetchAll()
  } catch (err) {
    console.error('Save batch error:', err)
    alert(err.response?.data?.message || 'Could not save this batch. Check your connection and try again.')
  } finally {
    isSaving.value = false
  }
}

// ── DISPLAY HELPERS ───────────────────────────────────────────────
const vaccineName = (vaccineID) => {
  const v = vaccineCatalog.value.find(x => x.vaccineID === vaccineID)
  return v ? (v.vaccineName || v.name) : `Vaccine #${vaccineID}`
}

const formatDate = (d) => {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}

const isExpired = (dateStr) => {
  if (!dateStr) return false
  return new Date(dateStr) < new Date()
}

const isNearExpiry = (dateStr) => {
  if (!dateStr) return false
  const expiry = new Date(dateStr)
  const today = new Date()
  const diffInMonths = (expiry.getFullYear() - today.getFullYear()) * 12 + (expiry.getMonth() - today.getMonth())
  return diffInMonths >= 0 && diffInMonths < 3
}

const stockBarWidth = (batch) => {
  const max = Math.max(batch.initialQuantity || batch.currentQuantity || 1, 1)
  return Math.min(100, Math.round((batch.currentQuantity / max) * 100))
}

const statusLabel = (batch) => {
  if (isExpired(batch.expirationDate)) return 'Expired'
  if (batch.currentQuantity <= (batch.minimumStock ?? 0)) return 'Low Stock'
  return 'In Stock'
}

const statusClass = (batch) => {
  if (isExpired(batch.expirationDate)) return 'bg-slate-200 text-slate-600'
  if (batch.currentQuantity <= (batch.minimumStock ?? 0)) return 'bg-rose-50 text-rose-600'
  return 'bg-emerald-50 text-emerald-600'
}

// ── SUMMARY STATS (real, computed from inventory.value) ────────────
const totalDoses = computed(() => inventory.value.reduce((sum, b) => sum + (b.currentQuantity || 0), 0))
const expiringSoonCount = computed(() => inventory.value.filter(b => isNearExpiry(b.expirationDate)).length)
const expiredCount = computed(() => inventory.value.filter(b => isExpired(b.expirationDate)).length)

onMounted(fetchAll)
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #E2E8F0; border-radius: 10px; }
</style>