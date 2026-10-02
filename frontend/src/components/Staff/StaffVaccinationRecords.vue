<template>
  <div class="flex h-screen bg-stone-50 antialiased text-slate-900" style="font-family: 'Inter','Segoe UI',sans-serif;">

    <StaffSidebar />

    <!-- MAIN -->
    <div class="flex flex-col flex-1 overflow-hidden">

      <!-- TOP BAR -->
      <StaffTopbar title="My Vaccination Records" breadcrumb="Aruga / My Vaccination Records" />

      <!-- PAGE CONTENT -->
      <main class="flex-1 overflow-y-auto p-6">

        <!-- PAGE HEADER -->
        <div class="flex items-center justify-between mb-6">
          <div>
            <p class="text-sm text-slate-500">
              Only the vaccines <strong class="text-slate-700">you</strong> gave are listed here. Other health workers' records stay private;
              a child's full history is on their record in Patient Records.
            </p>
          </div>
        </div>

        <!-- STAT CARDS -->
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <div class="bg-white rounded-xl border border-slate-200 p-5">
            <div class="flex items-center justify-between mb-3">
              <Syringe class="w-5 h-5 text-emerald-600" />
            </div>
            <p class="text-3xl font-bold text-slate-800">{{ stats.total }}</p>
            <p class="text-xs text-slate-400 mt-1">Doses I Gave (all time)</p>
          </div>
          <div class="bg-white rounded-xl border border-slate-200 p-5">
            <div class="flex items-center justify-between mb-3">
              <CheckCircle class="w-5 h-5 text-emerald-500" />
            </div>
            <p class="text-3xl font-bold text-slate-800">{{ stats.today }}</p>
            <p class="text-xs text-slate-400 mt-1">Today</p>
          </div>
          <div class="bg-white rounded-xl border border-slate-200 p-5">
            <div class="flex items-center justify-between mb-3">
              <Clock class="w-5 h-5 text-amber-500" />
            </div>
            <p class="text-3xl font-bold text-slate-800">{{ stats.month }}</p>
            <p class="text-xs text-slate-400 mt-1">This Month</p>
          </div>
          <div class="bg-white rounded-xl border border-slate-200 p-5">
            <div class="flex items-center justify-between mb-3">
              <AlertCircle class="w-5 h-5 text-amber-500" />
            </div>
            <p class="text-3xl font-bold text-slate-800">{{ stats.withRemarks }}</p>
            <p class="text-xs text-slate-400 mt-1">With Remarks / Reactions</p>
          </div>
        </div>

        <!-- FILTER ROW -->
        <div class="flex items-center gap-3 mb-4">
          <!-- Search — filters by child name, vaccine, or batch number -->
          <div class="relative flex-1 max-w-md">
            <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input v-model="searchQuery" type="text"
              placeholder="Search by child name, vaccine, or batch number..."
              class="w-full pl-9 pr-4 py-2 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500 bg-white" />
          </div>

          <!-- Vaccine filter -->
          <select v-model="vaccineFilter"
            class="text-sm border border-slate-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500 bg-white text-slate-600">
            <option value="">All Vaccines</option>
            <option v-for="v in vaccineOptions" :key="v" :value="v">{{ v }}</option>
          </select>

          <!-- Date range -->
          <input v-model="dateFrom" type="date"
            class="text-sm border border-slate-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500 bg-white text-slate-600" />
          <span class="text-slate-400 text-sm">to</span>
          <input v-model="dateTo" type="date"
            class="text-sm border border-slate-200 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500 bg-white text-slate-600" />

          <!-- Clear filters -->
          <button v-if="hasActiveFilters" @click="clearFilters"
            class="flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-700 px-3 py-2 rounded-lg hover:bg-slate-100 transition-colors">
            <X class="w-4 h-4" /> Clear
          </button>
        </div>

        <!-- RESULTS COUNT -->
        <p class="text-xs text-slate-400 mb-3">
          Showing {{ filteredRecords.length }} of {{ records.length }} records
        </p>

        <!-- TABLE -->
        <div class="bg-white rounded-xl border border-slate-200 overflow-hidden">

          <div v-if="loading" class="flex items-center justify-center py-16">
            <Loader2 class="w-5 h-5 animate-spin text-slate-400" />
            <span class="ml-2 text-sm text-slate-400">Loading records...</span>
          </div>

          <div v-else-if="filteredRecords.length === 0" class="flex flex-col items-center justify-center py-16 text-slate-400">
            <FileText class="w-10 h-10 mb-3 opacity-30" />
            <p class="text-sm">No vaccination records found</p>
            <p v-if="hasActiveFilters" class="text-xs mt-1">Try clearing your filters</p>
          </div>

          <div v-else class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-slate-50 border-b border-slate-200">
                <tr class="text-xs text-slate-500 uppercase tracking-wide">
                  <th class="px-5 py-3.5 text-left font-medium">Child Name</th>
                  <th class="px-5 py-3.5 text-left font-medium">Parent</th>
                  <th class="px-5 py-3.5 text-left font-medium">Vaccine Name</th>
                  <th class="px-5 py-3.5 text-left font-medium">Dose</th>
                  <th class="px-5 py-3.5 text-left font-medium">Date Given</th>
                  <th class="px-5 py-3.5 text-left font-medium">Batch No.</th>
                  <th class="px-5 py-3.5 text-left font-medium">Administered By</th>
                  <th class="px-5 py-3.5 text-left font-medium">Status</th>
                  <th class="px-5 py-3.5 text-left font-medium">Remarks</th>
                  <th class="px-5 py-3.5 text-left font-medium">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="rec in paginatedRecords" :key="rec.recordId"
                  class="hover:bg-slate-50 transition-colors">

                  <!-- Child -->
                  <td class="px-5 py-3.5">
                    <div class="flex items-center gap-2.5">
                      <div class="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold text-white shrink-0"
                        :style="{ backgroundColor: avatarColor(rec.childName) }">
                        {{ initials(rec.childName) }}
                      </div>
                      <span class="font-medium text-slate-800">{{ rec.childName }}</span>
                    </div>
                  </td>

                  <td class="px-5 py-3.5 text-slate-500">{{ rec.parentName }}</td>
                  <td class="px-5 py-3.5 font-medium text-slate-700">{{ rec.vaccineName }}</td>
                  <td class="px-5 py-3.5 text-slate-500">{{ rec.doseNumber }}{{ ordinal(rec.doseNumber) }}</td>

                  <!-- Date — highlights if late -->
                  <td class="px-5 py-3.5">
                    <div v-if="rec.dateAdministered">
                      <p class="text-slate-700">{{ formatDate(rec.dateAdministered) }}</p>
                      <!-- ✅ Show "X days late" if administered after scheduled date -->
                      <p v-if="rec.daysLate > 0" class="text-xs text-amber-600 font-medium mt-0.5">
                        {{ rec.daysLate }}d late
                      </p>
                    </div>
                    <span v-else class="text-slate-400">—</span>
                  </td>

                  <td class="px-5 py-3.5 font-mono text-xs text-slate-500">{{ rec.lotNumber || '—' }}</td>
                  <td class="px-5 py-3.5 text-slate-500">{{ rec.administeredByName || '—' }}</td>

                  <!-- Status badge -->
                  <td class="px-5 py-3.5">
                    <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium"
                      :class="statusClass(rec.status)">
                      {{ rec.status }}
                    </span>
                  </td>

                  <td class="px-5 py-3.5 text-slate-500 max-w-[160px] truncate" :title="rec.remarks">
                    {{ rec.remarks || '—' }}
                  </td>

                  <!-- Actions -->
                  <td class="px-5 py-3.5">
                    <div class="flex items-center gap-1.5">
                      <!-- View details -->
                      <button @click="viewRecord(rec)"
                        class="p-1.5 text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 rounded-lg transition-colors"
                        title="View details">
                        <Eye class="w-4 h-4" />
                      </button>
                      <!-- Mark vaccinated — only for Pending records -->
                      <button v-if="rec.status === 'Pending'"
                        @click="openVaccinateModal(rec)"
                        class="p-1.5 text-slate-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg transition-colors"
                        title="Mark vaccinated">
                        <Syringe class="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- PAGINATION -->
          <div v-if="filteredRecords.length > 0"
            class="flex items-center justify-between px-5 py-3.5 border-t border-slate-100 flex-wrap gap-3">
            <!-- Left: entries selector + count -->
            <div class="flex items-center gap-3">
              <div class="flex items-center gap-2 text-xs text-slate-500">
                <span>Show</span>
                <select v-model="pageSize" @change="currentPage = 1"
                  class="border border-slate-200 rounded-lg px-2 py-1 text-xs text-slate-600 outline-none bg-white">
                  <option :value="10">10</option>
                  <option :value="20">20</option>
                  <option :value="50">50</option>
                  <option :value="100">100</option>
                </select>
                <span>entries</span>
              </div>
              <p class="text-xs text-slate-400">
                Showing {{ (currentPage - 1) * pageSize + 1 }}–{{ Math.min(currentPage * pageSize, filteredRecords.length) }}
                of {{ filteredRecords.length }} records
              </p>
            </div>
            <!-- Right: numbered pages -->
            <div class="flex items-center gap-1">
              <button @click="currentPage--" :disabled="currentPage === 1"
                class="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 disabled:opacity-30 disabled:cursor-not-allowed transition-colors">
                <ChevronLeft class="w-4 h-4" />
              </button>
              <template v-for="p in paginationPages" :key="p">
                <span v-if="p === '...'" class="px-1 text-xs text-slate-400">…</span>
                <button v-else @click="currentPage = p"
                  class="min-w-[28px] h-7 rounded-lg text-xs font-medium transition-colors"
                  :class="p === currentPage
                    ? 'bg-emerald-600 text-white'
                    : 'text-slate-600 hover:bg-slate-100'">
                  {{ p }}
                </button>
              </template>
              <button @click="currentPage++" :disabled="currentPage === totalPages"
                class="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 disabled:opacity-30 disabled:cursor-not-allowed transition-colors">
                <ChevronRight class="w-4 h-4" />
              </button>
            </div>
          </div>
        </div>
      </main>
    </div>

    <!-- ── VACCINATE MODAL ──────────────────────────────────────── -->
    <!--
      This modal fires when the doctor clicks "Mark Vaccinated".
      On submit it:
        1. PATCHes /api/vaccinationrecords/{recordId} → sets Status=Completed,
           DateAdministered, LotNumber, Remarks, AdministeredBy, AdministeredByName
        2. The backend then computes the NEXT dose's ScheduledDate using:
             NextScheduledDate = DateAdministered + MinIntervalDays
           (same 28-day DOH rule used on the frontend)
        3. Parent dashboard reads the updated DateAdministered and recalculates
           future doses automatically via computedVaccineList cascade logic.
    -->
    <Transition name="modal">
      <div v-if="showVaccinateModal"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-lg" @click.stop>

          <!-- Modal header -->
          <div class="flex items-center justify-between px-6 py-5 border-b border-slate-100">
            <div>
              <h2 class="text-lg font-bold text-slate-800">Mark as Vaccinated</h2>
              <p v-if="selectedRecord" class="text-sm text-slate-500 mt-0.5">
                {{ selectedRecord.childName }} — {{ selectedRecord.vaccineName }}
                (Dose {{ selectedRecord.doseNumber }})
              </p>
            </div>
            <button @click="closeModal"
              class="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors">
              <X class="w-4 h-4" />
            </button>
          </div>

          <!-- Modal body -->
          <div class="px-6 py-5 space-y-4">

            <!-- ✅ Date administered — pre-filled with today -->
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">
                Date Administered <span class="text-red-500">*</span>
              </label>
              <input v-model="form.dateAdministered" type="date"
                class="w-full border border-slate-200 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              <!-- ✅ Late warning — shown if date is after the original scheduled date -->
              <p v-if="daysLatePreview > 0" class="text-xs text-amber-600 mt-1 font-medium">
                ⚠ This is {{ daysLatePreview }} day{{ daysLatePreview !== 1 ? 's' : '' }} after the scheduled date.
                The next dose will be recalculated from this date ({{ nextDosePreview }}).
              </p>
              <p v-else-if="form.dateAdministered" class="text-xs text-emerald-600 mt-1 font-medium">
                ✓ On time. Next dose scheduled for {{ nextDosePreview }}.
              </p>
            </div>

            <!-- Batch / Lot number -->
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">
                Batch / Lot Number <span class="text-red-500">*</span>
              </label>
              <input v-model="form.lotNumber" type="text" placeholder="e.g. PV2026-A123"
                class="w-full border border-slate-200 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>

            <!-- Administered by — auto-filled from logged-in doctor -->
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">Administered By</label>
              <input :value="doctor.fullName" type="text" disabled
                class="w-full border border-slate-100 rounded-lg px-3 py-2.5 text-sm bg-slate-50 text-slate-500 cursor-not-allowed" />
            </div>

            <!-- Remarks -->
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">Remarks</label>
              <textarea v-model="form.remarks" rows="3"
                placeholder="e.g. No adverse reaction, mild fever possible..."
                class="w-full border border-slate-200 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"></textarea>
            </div>
          </div>

          <!-- Modal footer -->
          <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-slate-100">
            <button @click="closeModal"
              class="px-4 py-2.5 text-sm font-medium text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-lg transition-colors">
              Cancel
            </button>
            <button @click="submitVaccination"
              :disabled="!form.dateAdministered || !form.lotNumber || submitting"
              class="flex items-center gap-2 px-4 py-2.5 text-sm font-medium bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg transition-colors">
              <Loader2 v-if="submitting" class="w-4 h-4 animate-spin" />
              <CheckCircle v-else class="w-4 h-4" />
              {{ submitting ? 'Saving...' : 'Confirm Vaccination' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- ── VIEW RECORD MODAL ────────────────────────────────────── -->
    <Transition name="modal">
      <div v-if="showViewModal"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
        <div v-if="selectedRecord" class="bg-white rounded-2xl shadow-2xl w-full max-w-md" @click.stop>

          <div class="flex items-center justify-between px-6 py-5 border-b border-slate-100">
            <h2 class="text-lg font-bold text-slate-800">Record Details</h2>
            <button @click="showViewModal = false"
              class="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors">
              <X class="w-4 h-4" />
            </button>
          </div>

          <div class="px-6 py-5 space-y-3">
            <!-- Child info -->
            <div class="flex items-center gap-3 pb-4 border-b border-slate-100">
              <div class="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold text-white"
                :style="{ backgroundColor: avatarColor(selectedRecord.childName) }">
                {{ initials(selectedRecord.childName) }}
              </div>
              <div>
                <p class="font-semibold text-slate-800">{{ selectedRecord.childName }}</p>
                <p class="text-xs text-slate-400">Parent: {{ selectedRecord.parentName }}</p>
              </div>
            </div>

            <!-- Record details grid -->
            <div class="grid grid-cols-2 gap-3">
              <div class="p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Vaccine</p>
                <p class="font-medium text-slate-700 text-sm">{{ selectedRecord.vaccineName }}</p>
              </div>
              <div class="p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Dose Number</p>
                <p class="font-medium text-slate-700 text-sm">Dose {{ selectedRecord.doseNumber }}</p>
              </div>
              <div class="p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Date Administered</p>
                <p class="font-medium text-slate-700 text-sm">
                  {{ selectedRecord.dateAdministered ? formatDate(selectedRecord.dateAdministered) : '—' }}
                </p>
                <p v-if="selectedRecord.daysLate > 0" class="text-xs text-amber-600 mt-0.5">
                  {{ selectedRecord.daysLate }}d late
                </p>
              </div>
              <div class="p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Injection Site</p>
                <p class="font-medium text-slate-700 text-sm">{{ selectedRecord.injectionSite || '—' }}</p>
              </div>
              <div class="p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Batch / Lot No.</p>
                <p class="font-medium text-slate-700 text-sm font-mono">{{ selectedRecord.lotNumber || '—' }}</p>
              </div>
              <div class="p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Status</p>
                <span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium"
                  :class="statusClass(selectedRecord.status)">
                  {{ selectedRecord.status }}
                </span>
              </div>
              <div class="col-span-2 p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Administered By</p>
                <p class="font-medium text-slate-700 text-sm">{{ selectedRecord.administeredByName || '—' }}</p>
              </div>
              <div class="col-span-2 p-3 bg-slate-50 rounded-lg">
                <p class="text-xs text-slate-400 mb-1">Remarks</p>
                <p class="text-slate-700 text-sm">{{ selectedRecord.remarks || 'No remarks' }}</p>
              </div>
            </div>
          </div>

          <div class="flex justify-end px-6 py-4 border-t border-slate-100">
            <button @click="showViewModal = false"
              class="px-4 py-2.5 text-sm font-medium text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-lg transition-colors">
              Close
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- TOAST -->
    <Transition name="toast">
      <div v-if="toast.show"
        class="fixed bottom-6 right-6 z-50 flex items-center gap-3 bg-slate-800 text-white text-sm font-medium px-5 py-3.5 rounded-xl shadow-lg">
        <CheckCircle class="w-4 h-4 text-emerald-400 shrink-0" />
        {{ toast.message }}
      </div>
    </Transition>


  </div>
</template>

<!-- StaffVaccinationRecords.vue — /staff/vaccination-records
     The signed-in Nurse's own doses (GET /api/VaccinationRecords/mine). -->

<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { getUser } from '@/utils/auth'
import StaffSidebar from './StaffSidebar.vue'
import StaffTopbar from './StaffTopbar.vue'
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import axios from 'axios'
import {
  Home, Users, Calendar, Syringe, FileText, Settings,
  Search, Bell, LogOut, Loader2, X, CheckCircle,
  Eye, ChevronLeft, ChevronRight, Plus, Clock,
  AlertCircle, ListChecks
} from 'lucide-vue-next'

const API = API_ORIGIN
const router = useRouter()
const route  = useRoute()

// ── AUTH ─────────────────────────────────────────────────────────
const doctor = ref({ userId: '', fullName: '', userType: '', prcNo: '' })

onMounted(() => {
  const u = getUser()
if (!u) { router.push('/'); return }
  doctor.value = {
    userId:   u.UserID,
    fullName: `${u.FirstName} ${u.LastName}`,
    userType: u.UserType,
    prcNo:    u.PRCNo || '',
  }
  fetchRecords()
})

const doctorInitials = computed(() =>
  doctor.value.fullName.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
)


// ── NAV ──────────────────────────────────────────────────────────
// ── DOH VACCINE SCHEDULE ─────────────────────────────────────────
const VACCINE_MASTER = [
  { vaccineId: 1, name: 'BCG Vaccine',                     doses: [{ n: 1, gap: 0   }] },
  { vaccineId: 2, name: 'Hepatitis B Vaccine',             doses: [{ n: 1, gap: 0   }] },
  { vaccineId: 3, name: 'Pentavalent (DPT-Hep B-HIB)',     doses: [{ n: 1, gap: 45  }, { n: 2, gap: 28 }, { n: 3, gap: 28 }] },
  { vaccineId: 4, name: 'Oral Polio Vaccine (OPV)',        doses: [{ n: 1, gap: 45  }, { n: 2, gap: 28 }, { n: 3, gap: 28 }] },
  { vaccineId: 5, name: 'Inactivated Polio Vaccine (IPV)', doses: [{ n: 1, gap: 105 }, { n: 2, gap: 165}] },
  { vaccineId: 6, name: 'Pneumococcal Conj. Vaccine (PCV)',doses: [{ n: 1, gap: 45  }, { n: 2, gap: 28 }, { n: 3, gap: 28 }] },
  { vaccineId: 7, name: 'MMR Vaccine',                     doses: [{ n: 1, gap: 270 }, { n: 2, gap: 90 }] },
]

// ── RECORDS DATA ─────────────────────────────────────────────────
const records = ref([])
const loading = ref(false)

// Only the doses this health worker gave (beneficiary request: health
// workers don't see each other's vaccination lists).
async function fetchRecords() {
  loading.value = true
  try {
    const res = await axios.get(`${API}/api/VaccinationRecords/mine`)
    records.value = res.data.map(r => ({
      recordId:           r.vaccinationRecordID ?? r.recordID ?? r.RecordID,
      childId:            r.childID    ?? r.ChildID,
      childName:          r.childName  ?? r.ChildName  ?? '—',
      parentName:         r.parentName ?? r.ParentName ?? '—',
      vaccineId:          r.vaccineID  ?? r.VaccineID,
      vaccineName:        r.vaccineName ?? r.VaccineName ?? `Vaccine ${r.vaccineID ?? r.VaccineID}`,
      doseNumber:         r.doseNumber ?? r.DoseNumber,
      dateAdministered:   r.vaccinationDate ?? r.dateAdministered ?? r.DateAdministered ?? null,
      scheduledDate:      r.scheduledDate ?? r.ScheduledDate ?? null,
      lotNumber:          r.lotNumber  ?? r.LotNumber  ?? null,
      status:             r.status     ?? r.Status     ?? 'Pending',
      administeredByName: r.administeredByName ?? r.AdministeredByName ?? null,
      remarks:            r.nurseObservation ?? r.remarks ?? r.Remarks ?? null,
      injectionSite:      r.injectionSite ?? null,
      daysLate:           computeDaysLate(
                            r.vaccinationDate ?? r.dateAdministered ?? r.DateAdministered,
                            r.scheduledDate    ?? r.ScheduledDate
                          ),
    }))
  } catch (err) {
    console.error('fetchRecords error:', err)
    // Leave records empty — shows "No vaccination records found" state
  } finally {
    loading.value = false
  }
}

// ── STATS ─────────────────────────────────────────────────────────
const stats = computed(() => {
  const now = new Date()
  const given = records.value.filter(r => r.dateAdministered).map(r => new Date(r.dateAdministered))
  return {
    total: records.value.length,
    today: given.filter(d => d.toDateString() === now.toDateString()).length,
    month: given.filter(d => d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth()).length,
    withRemarks: records.value.filter(r => r.remarks && !/no (adverse )?reaction/i.test(r.remarks)).length,
  }
})

// ── FILTERS ───────────────────────────────────────────────────────
const searchQuery   = ref('')
const statusFilter  = ref('')
const vaccineFilter = ref('')
const dateFrom      = ref('')
const dateTo        = ref('')

const vaccineOptions = computed(() =>
  [...new Set(records.value.map(r => r.vaccineName))].sort()
)

const hasActiveFilters = computed(() =>
  searchQuery.value || statusFilter.value || vaccineFilter.value || dateFrom.value || dateTo.value
)

function clearFilters() {
  searchQuery.value  = ''
  statusFilter.value = ''
  vaccineFilter.value = ''
  dateFrom.value     = ''
  dateTo.value       = ''
}

const filteredRecords = computed(() => {
  let list = records.value
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(r =>
      r.childName?.toLowerCase().includes(q)  ||
      r.parentName?.toLowerCase().includes(q) ||
      r.vaccineName?.toLowerCase().includes(q)||
      r.lotNumber?.toLowerCase().includes(q)
    )
  }
  if (statusFilter.value)  list = list.filter(r => r.status === statusFilter.value)
  if (vaccineFilter.value) list = list.filter(r => r.vaccineName === vaccineFilter.value)
  if (dateFrom.value)      list = list.filter(r => r.dateAdministered >= dateFrom.value)
  if (dateTo.value)        list = list.filter(r => r.dateAdministered <= dateTo.value)

  return [...list].sort((a, b) => {
    if (a.status === 'Pending' && b.status !== 'Pending') return -1
    if (b.status === 'Pending' && a.status !== 'Pending') return 1
    const da = a.dateAdministered || a.scheduledDate || ''
    const db = b.dateAdministered || b.scheduledDate || ''
    return db.localeCompare(da)
  })
})

// ── PAGINATION ────────────────────────────────────────────────────
const currentPage = ref(1)
const pageSize    = ref(10)


const totalPages = computed(() =>
  Math.max(1, Math.ceil(filteredRecords.value.length / pageSize.value))
)
const paginatedRecords = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredRecords.value.slice(start, start + pageSize.value)
})
const paginationPages = computed(() => {
  const total = totalPages.value
  const cur = currentPage.value
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const pages = []
  pages.push(1)
  if (cur > 3) pages.push('...')
  for (let p = Math.max(2, cur - 1); p <= Math.min(total - 1, cur + 1); p++) pages.push(p)
  if (cur < total - 2) pages.push('...')
  pages.push(total)
  return pages
})

// ── VACCINATE MODAL ───────────────────────────────────────────────
const showVaccinateModal = ref(false)
const selectedRecord     = ref(null)
const submitting         = ref(false)
const form = ref({ dateAdministered: '', lotNumber: '', remarks: '' })

function openVaccinateModal(rec) {
  selectedRecord.value = rec
  form.value = {
    dateAdministered: new Date().toISOString().split('T')[0],
    lotNumber: '',
    remarks: '',
  }
  showVaccinateModal.value = true
}

function closeModal() {
  showVaccinateModal.value = false
  selectedRecord.value = null
}

// How many days late this administration will be
const daysLatePreview = computed(() => {
  if (!selectedRecord.value?.scheduledDate || !form.value.dateAdministered) return 0
  const diff = Math.round(
    (new Date(form.value.dateAdministered) - new Date(selectedRecord.value.scheduledDate)) / 86400000
  )
  return Math.max(0, diff)
})

// Preview when the next dose will be due (DOH interval rule)
const nextDosePreview = computed(() => {
  if (!selectedRecord.value || !form.value.dateAdministered) return '—'
  const vaccine = VACCINE_MASTER.find(v => v.vaccineId === selectedRecord.value.vaccineId)
  if (!vaccine) return '—'
  const idx      = vaccine.doses.findIndex(d => d.n === selectedRecord.value.doseNumber)
  const nextDose = vaccine.doses[idx + 1]
  if (!nextDose) return 'No more doses for this vaccine'
  const given    = new Date(form.value.dateAdministered)
  const nextDate = new Date(given.getTime() + nextDose.gap * 86400000)
  while (![1, 3, 5].includes(nextDate.getDay())) nextDate.setDate(nextDate.getDate() + 1)
  return nextDate.toLocaleDateString('en-PH', { month: 'short', day: 'numeric', year: 'numeric' })
})

// FIX: submitVaccination now actually calls the API.
// PATCH /api/VaccinationRecords/{recordId} updates:
//   Status             = 'Completed'
//   DateAdministered   = form.dateAdministered
//   LotNumber          = form.lotNumber
//   Remarks            = form.remarks
//   AdministeredBy     = doctor.userId   (GUID FK → dbo.Users)
//   AdministeredByName = doctor.fullName (string stored for parent display)
//
// The parent dashboard (ParentHomepage.vue) reads AdministeredByName
// from this record — so as soon as this saves, the parent sees
// who gave the vaccine and when.
async function submitVaccination() {
  if (!form.value.dateAdministered || !form.value.lotNumber || !selectedRecord.value) return
  submitting.value = true
  try {
    const payload = {
      dateAdministered:   form.value.dateAdministered,
      lotNumber:          form.value.lotNumber,
      remarks:            form.value.remarks || 'No adverse reaction',
      administeredBy:     doctor.value.userId,    // GUID → AdministeredBy (FK to Users)
      administeredByName: doctor.value.fullName,  // string → AdministeredByName (shown to parent)
      status:             'Completed',
    }

    // ── ACTUAL API CALL ─────────────────────────────────────────
    await axios.patch(
      `${API}/api/VaccinationRecords/${selectedRecord.value.recordId}`,
      payload
    )
    // ────────────────────────────────────────────────────────────

    // Optimistic UI update — reflect change immediately without full reload
    const idx = records.value.findIndex(r => r.recordId === selectedRecord.value.recordId)
    if (idx !== -1) {
      records.value[idx] = {
        ...records.value[idx],
        status:             'Completed',
        dateAdministered:   form.value.dateAdministered,
        lotNumber:          form.value.lotNumber,
        remarks:            form.value.remarks || 'No adverse reaction',
        administeredByName: doctor.value.fullName,
        daysLate:           daysLatePreview.value,
      }
    }

    const name = selectedRecord.value.childName
    closeModal()
    showToast(`${name}'s vaccination recorded — visible on parent dashboard now`)
  } catch (err) {
    console.error('submitVaccination error:', err)
    showToast('Failed to save. Please try again.')
  } finally {
    submitting.value = false
  }
}

// ── VIEW MODAL ────────────────────────────────────────────────────
const showViewModal = ref(false)
function viewRecord(rec) {
  selectedRecord.value = rec
  showViewModal.value = true
}

// ── HELPERS ───────────────────────────────────────────────────────
function computeDaysLate(dateAdministered, scheduledDate) {
  if (!dateAdministered || !scheduledDate) return 0
  return Math.max(0, Math.round(
    (new Date(dateAdministered) - new Date(scheduledDate)) / 86400000
  ))
}

function formatDate(dateStr) {
  if (!dateStr) return '—'
  return new Date(dateStr).toLocaleDateString('en-PH', {
    month: 'short', day: 'numeric', year: 'numeric'
  })
}

function ordinal(n) {
  const s = ['th','st','nd','rd'], v = n % 100
  return s[(v - 20) % 10] || s[v] || s[0]
}

function statusClass(status) {
  return {
    Completed: 'bg-emerald-100 text-emerald-700',
    Pending:   'bg-amber-100 text-amber-700',
    Missed:    'bg-red-100 text-red-600',
    Scheduled: 'bg-blue-100 text-blue-700',
  }[status] || 'bg-slate-100 text-slate-600'
}

const AVATAR_COLORS = ['#4a7c59','#6b7c45','#8b5e3c','#4a6fa5','#7b4f8e','#5a7a6b','#8b6914']
function avatarColor(name) {
  if (!name) return AVATAR_COLORS[0]
  return AVATAR_COLORS[name.charCodeAt(0) % AVATAR_COLORS.length]
}
function initials(name) {
  if (!name) return '?'
  return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
}

const toast = ref({ show: false, message: '' })
function showToast(msg) {
  toast.value = { show: true, message: msg }
  setTimeout(() => toast.value.show = false, 4000)
}

</script>

<style scoped>
.modal-enter-active, .modal-leave-active { transition: opacity 0.2s ease; }
.modal-enter-from, .modal-leave-to       { opacity: 0; }
.toast-enter-active, .toast-leave-active { transition: all 0.3s ease; }
.toast-enter-from  { opacity: 0; transform: translateY(12px); }
.toast-leave-to    { opacity: 0; transform: translateY(12px); }

</style>