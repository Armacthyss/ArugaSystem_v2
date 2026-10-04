<template>
  <div class="w-full min-h-screen bg-slate-50 flex justify-center font-sans antialiased text-slate-900">
    <div class="w-full max-w-312.5 px-6 py-6">

      <HeaderNav
        :parent-data="parentData"
        :children="children"
        :unread-count="unreadCount"
        @open-profile="showProfile = true"
        @open-notifications="showNotifications = true"
        @logout="handleLogout"
        @select-child="handleSelectChild"
      />

      <div class="grid grid-cols-12 gap-8">

        <ChildSidebar :children="children" :selected-child="selectedChild" @select-child="handleSelectChild" />

        <main class="col-span-12 lg:col-span-9">
          <div class="space-y-5 animate-in fade-in slide-in-from-right-4 duration-500">
            <div class="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
              <div class="px-8 py-6 border-b border-slate-50 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div class="flex items-center gap-4">
                  <div v-if="selectedChild" class="w-12 h-12 shrink-0 rounded-xl bg-emerald-50 border border-emerald-500/20 flex items-center justify-center text-2xl">{{ selectedChild.sex === 'Female' ? '👧' : '👶' }}</div>
                  <div>
                    <p class="text-[10px] font-bold text-emerald-600 uppercase tracking-[0.2em]">Vaccination History</p>
                    <h2 class="text-2xl font-black text-slate-800 leading-tight">{{ selectedChild?.firstName }} {{ selectedChild?.lastName }}</h2>
                    <p class="text-[11px] font-bold text-slate-400 mt-0.5">{{ vaccinationHistory.length }} record{{ vaccinationHistory.length !== 1 ? 's' : '' }}</p>
                  </div>
                </div>
                <div class="flex flex-wrap gap-1 bg-slate-50 p-1 rounded-xl">
                  <button v-for="f in ['All', 'Completed', 'Scheduled', 'Overdue']" :key="f" @click="recordFilter = f"
                          :class="recordFilter === f ? 'bg-white shadow-sm text-slate-800' : 'text-slate-500 hover:text-slate-700'"
                          class="px-3 py-1.5 rounded-lg text-[10px] uppercase font-bold transition-all flex items-center gap-1.5">
                    {{ f }}
                    <span v-if="f !== 'All'" class="min-w-5 h-5 px-1.5 rounded-full text-[11px] font-black leading-5 text-center"
                          :class="recordStats[f.toLowerCase()] === 0 ? 'bg-slate-200 text-slate-500'
                                : f === 'Completed' ? 'bg-emerald-500 text-white'
                                : f === 'Scheduled' ? 'bg-blue-600 text-white'
                                : 'bg-red-500 text-white'">{{ recordStats[f.toLowerCase()] }}</span>
                  </button>
                </div>
              </div>
              <div v-if="recordsLoading" class="py-16 text-center text-slate-400"><p class="text-2xl mb-2 animate-pulse">💉</p><p class="text-xs font-bold">Loading records...</p></div>
              <div v-else-if="filteredRecords.length === 0" class="py-16 text-center text-slate-400"><p class="text-2xl mb-2">📋</p><p class="text-xs font-bold">No {{ recordFilter === 'All' ? '' : recordFilter.toLowerCase() + ' ' }}vaccination records found.</p></div>
              <div v-else class="overflow-x-auto">
                <table class="w-full text-left text-[11px] border-collapse">
                  <thead><tr class="bg-slate-50 text-slate-400 font-medium uppercase tracking-wide text-[9px]">
                    <th class="px-6 py-3">Date</th>
                    <th class="px-6 py-3">Vaccine</th>
                    <th class="px-6 py-3">Dose</th>
                    <th class="px-6 py-3">Status</th>
                    <th class="px-6 py-3">Site</th>
                    <th class="px-6 py-3">Vaccinated By</th>
                    <th class="px-6 py-3">Lot #</th>
                  </tr></thead>
                  <tbody>
                    <tr v-for="(rec, i) in filteredRecords" :key="i" class="border-t border-slate-50 hover:bg-slate-50 transition-colors">
                      <td class="px-6 py-4 whitespace-nowrap text-slate-500">
                        {{ rec.dateAdministered
                            ? formatDisplayDate(new Date(rec.dateAdministered))
                            : rec.scheduledDate
                              ? formatDisplayDate(new Date(rec.scheduledDate)) + ' (scheduled)'
                              : '—' }}
                      </td>
                      <td class="px-6 py-4 font-bold text-slate-800">{{ rec.vaccineName }}</td>
                      <td class="px-6 py-4 text-slate-500">Dose {{ rec.doseNumber }}</td>
                      <td class="px-6 py-4"><span :class="getStatusClass(rec.status)" class="px-3 py-1 rounded-full text-[9px] font-bold uppercase">{{ rec.status }}</span></td>
                      <td class="px-6 py-4 text-slate-600 whitespace-nowrap">{{ rec.injectionSite || '—' }}</td>
                      <td class="px-6 py-4 text-slate-500 italic">{{ rec.administeredByName || '—' }}</td>
                      <td class="px-6 py-4 font-mono text-[10px] text-slate-400">{{ rec.lotNumber || '—' }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div class="px-8 py-5 bg-slate-50 border-t border-slate-100 flex justify-between items-center">
                <p class="text-[10px] text-slate-400 font-medium">Print or save the full immunization record as a PDF</p>
                <button @click="downloadRecord" :disabled="!selectedChild" class="bg-emerald-600 hover:bg-emerald-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50">Download PDF</button>
              </div>
            </div>
          </div>
        </main>
      </div>

      <ProfileModal v-if="showProfile" :parent-data="parentData" :children="children" @close="showProfile = false" />
      <NotificationPanel v-if="showNotifications" :parent-data="parentData" @close="showNotifications = false" />

    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { addDays, format, isMonday, isWednesday, isFriday } from 'date-fns'
import HeaderNav from '../Components/Headernav.vue'
import ChildSidebar from '../Components/Childsidebar.vue'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'
import { getAccount, logout as authLogout } from '@/utils/auth'
import api from '../Composables/api.js'
import { fetchChildSchedule, buildSchedule } from '../Composables/childSchedule.js'

const router = useRouter()

// ── State ──────────────────────────────────────────────────────────────────
const parentData        = ref(null)
const children           = ref([])
const selectedChild      = ref(null)

// Opens the immunization record and saves it as a PDF file; that page
// also has a Print button for a paper copy.
function downloadRecord() {
  if (!selectedChild.value) return
  window.open(`/print/vaccination-card/${selectedChild.value.childID}?download=1`, '_blank')
}
const showProfile        = ref(false)
const showNotifications  = ref(false)
const unreadCount        = ref(0)

// Records
// completedRecords = raw Completed rows from DB
// vaccinationHistory = completedRecords + computed upcoming/overdue rows
const completedRecords   = ref([])
const vaccinationHistory = ref([])
const recordsLoading     = ref(false)
const recordFilter       = ref('All')
const recordStats        = ref({ completed: 0, scheduled: 0, overdue: 0 })

// ── Vaccination schedule — from the backend timeline, shared with the
//    other parent pages (Composables/childSchedule.js) ─────────────────────
const scheduleTimeline = ref([])

function formatDisplayDate(date) {
  if (!date) return '—'
  try { return format(new Date(date), 'MMM d, yyyy') } catch { return '—' }
}
function getStatusClass(status) {
  if (status === 'Completed') return 'bg-emerald-100 text-emerald-700'
  if (status === 'Overdue')   return 'bg-red-100 text-red-700'
  if (status === 'Scheduled') return 'bg-blue-100 text-blue-700'
  return 'bg-amber-100 text-amber-700'
}

const computedVaccineList = computed(() => buildSchedule(scheduleTimeline.value, completedRecords.value))

const filteredRecords = computed(() => {
  if (recordFilter.value === 'All') return vaccinationHistory.value
  return vaccinationHistory.value.filter(r => r.status === recordFilter.value)
})

// ── Actions ────────────────────────────────────────────────────────────────
function handleLogout() {
  authLogout()
  router.push('/')
}

function handleSelectChild(child) {
  selectedChild.value = child
  localStorage.setItem('selectedParentChild', JSON.stringify(child))
  fetchRecords(child.childID)
}

async function fetchRecords(childId) {
  if (!childId) return
  recordsLoading.value = true
  try {
    const { timeline, records } = await fetchChildSchedule(childId)
    scheduleTimeline.value = timeline
    completedRecords.value = records.filter(r => (r.status ?? 'Completed') === 'Completed')
    await new Promise(r => setTimeout(r, 0))

    const today = new Date()
    today.setHours(0, 0, 0, 0)
    const upcomingRows = computedVaccineList.value
      .filter(v => !v.isCompleted)
      .map(v => ({
        vaccineID:          v.vaccineId,
        vaccineName:        v.name,
        doseNumber:         v.doseNumber,
        dateAdministered:   null,
        scheduledDate:      v.scheduledDate,
        status:             v.scheduledDate < today ? 'Overdue' : 'Scheduled',
        administeredByName: null,
        lotNumber:          null,
      }))

    vaccinationHistory.value = [...completedRecords.value, ...upcomingRows].sort((a, b) => {
      const dA = a.dateAdministered ? new Date(a.dateAdministered) : new Date(a.scheduledDate)
      const dB = b.dateAdministered ? new Date(b.dateAdministered) : new Date(b.scheduledDate)
      return dA - dB
    })

    recordStats.value = {
      completed: completedRecords.value.length,
      scheduled: upcomingRows.filter(r => r.status === 'Scheduled').length,
      overdue:   upcomingRows.filter(r => r.status === 'Overdue').length,
    }
  } catch (err) {
    console.error('fetchRecords error:', err)
    completedRecords.value   = []
    vaccinationHistory.value = []
    recordStats.value = { completed: 0, scheduled: 0, overdue: 0 }
  } finally {
    recordsLoading.value = false
  }
}

async function fetchUnreadCount() {
  if (!parentData.value?.parentID) return
  try {
    const res = await api.get(`/Notifications/parent/${parentData.value.parentID}`)
    unreadCount.value = res.data.filter(n => !n.isRead).length
  } catch {
    unreadCount.value = 0
  }
}

// ── Lifecycle ──────────────────────────────────────────────────────────────
onMounted(async () => {
  const savedAccount = getAccount()
  if (!savedAccount) { router.push('/'); return }

  const rawUser = savedAccount.user ?? savedAccount

  // Backend now returns PascalCase (ParentID, FirstName, LastName, Email, ...).
  // Normalize to the camelCase shape every Parent component expects.
  parentData.value = {
    ...rawUser,
    parentID: rawUser.parentID ?? rawUser.ParentID,
    firstName: rawUser.firstName ?? rawUser.FirstName,
    middleName: rawUser.middleName ?? rawUser.MiddleName,
    lastName: rawUser.lastName ?? rawUser.LastName,
    email: rawUser.email ?? rawUser.Email,
    contactNo: rawUser.contactNo ?? rawUser.ContactNo,
    barangayNo: rawUser.barangayNo ?? rawUser.BarangayNo,
    address: rawUser.address ?? rawUser.Address,
  }

  if (!parentData.value?.parentID) {
    console.error('Invalid parent session:', savedAccount)
    router.push('/')
    return
  }

  try {
    const res = await api.get(`/Parents/dashboard/${parentData.value.parentID}`)
    children.value = res.data.map(child => {
      const relationshipType = child.relationshipType ?? child.RelationshipType
      const relatedParentName = child.parentFullName ?? child.ParentFullName

      return {
        childID: child.ChildID ?? child.childID,
        firstName: child.FirstName ?? child.firstName,
        middleName: child.MiddleName ?? child.middleName,
        lastName: child.LastName ?? child.lastName,
        birthDate: child.BirthDate ?? child.birthDate,
        placeOfBirth: child.PlaceOfBirth ?? child.placeOfBirth,
        sex: child.Sex ?? child.sex,
        barangay: child.Barangay ?? child.barangay,
        familyNo: child.FamilyNo ?? child.familyNo,
        address: child.Address ?? child.address,
        healthCenter: child.HealthCenter ?? child.healthCenter,
        relationshipType,
        isPrimaryContact: child.IsPrimaryContact ?? child.isPrimaryContact,
        canReceiveNotifications: child.CanReceiveNotifications ?? child.canReceiveNotifications,
        // Everyone linked to the child: "Maria Santos (Mother); Rosario Santos (Grandmother)"
        guardians: child.Guardians ?? child.guardians,

        // The dashboard endpoint doesn't return MotherName/FatherName/GuardianName
        // fields directly — it only returns relationshipType + parentFullName for
        // the currently logged-in parent, so derive which of the three this is.
        motherName: relationshipType === 'Mother' ? relatedParentName : undefined,
        fatherName: relationshipType === 'Father' ? relatedParentName : undefined,
        guardianName: relationshipType === 'Guardian' ? relatedParentName : undefined,
      }
    })
  } catch (err) {
    console.error('Error fetching parent dashboard:', err)
    children.value = []
  }

  const savedChildRaw = localStorage.getItem('selectedParentChild')
  if (savedChildRaw) {
    try {
      const savedChild = JSON.parse(savedChildRaw)
      selectedChild.value = children.value.find(c => c.childID === savedChild.childID) || children.value[0] || null
    } catch {
      selectedChild.value = children.value[0] || null
    }
  } else {
    selectedChild.value = children.value[0] || null
  }

  if (selectedChild.value) {
    localStorage.setItem('selectedParentChild', JSON.stringify(selectedChild.value))
    await fetchRecords(selectedChild.value.childID)
  }

  await fetchUnreadCount()
})
</script>