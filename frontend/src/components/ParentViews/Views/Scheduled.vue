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
          <div class="animate-in fade-in slide-in-from-right-4 duration-500">
            <div v-if="!selectedChild" class="bg-white rounded-xl p-12 text-center text-slate-400 border border-slate-100 shadow-sm">
              <p class="text-2xl mb-2">👶</p>
              <p class="font-bold text-sm">Select a child from Family Profiles to view their schedule.</p>
            </div>
            <!-- Side by side on a computer; on a phone the dose list comes first
                 (scrolling on its own) and the calendar below it -->
            <div v-else class="flex flex-col lg:flex-row gap-5 lg:items-start">
              <div class="w-full lg:w-[42%] shrink-0 flex flex-col bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden max-h-[55vh] lg:max-h-[680px]">
                <div class="px-6 pt-6 pb-4 border-b border-slate-50">
                  <p class="text-[10px] font-bold text-emerald-500 uppercase tracking-[0.2em]">Vaccination Schedule</p>
                  <p class="text-xs text-slate-400 mt-0.5">{{ selectedChild.firstName }} · {{ computedVaccineList.length }} doses</p>
                </div>
                <div class="overflow-y-auto flex-1 p-4 space-y-2">
                  <template v-for="(group, gIdx) in groupedVaccineList" :key="gIdx">
                    <p class="text-[9px] font-bold text-slate-700 uppercase tracking-widest px-2 pt-3 pb-1 first:pt-0">{{ group.name }}</p>
                    <button v-for="vax in group.doses" :key="vax.doseId" @click="selectedVax = vax"
                      :class="selectedVax?.doseId === vax.doseId ? 'bg-emerald-600 text-white shadow-md' : 'bg-slate-50 hover:bg-slate-100 text-slate-700'"
                      class="w-full px-4 py-3 rounded-xl flex items-center justify-between transition-all text-left">
                      <div class="flex items-center gap-3">
                        <span :class="selectedVax?.doseId === vax.doseId ? 'bg-white/20' : vax.isCompleted ? 'bg-emerald-100' : 'bg-white'" class="w-7 h-7 rounded-lg flex items-center justify-center text-xs shadow-sm shrink-0">
                          {{ vax.isCompleted ? '✅' : '💉' }}
                        </span>
                        <div>
                          <p class="text-[11px] font-bold leading-tight" :class="selectedVax?.doseId === vax.doseId ? 'text-white' : 'text-slate-700'">
                            Dose {{ vax.doseNumber }}
                            <span v-if="vax.isCompleted" class="ml-1 text-[8px] font-bold text-emerald-600 bg-emerald-100 px-1.5 py-0.5 rounded-full uppercase">Taken</span>
                          </p>
                          <div v-if="vax.isCompleted && vax.wasLate" class="mt-0.5 space-y-0.5">
                            <p class="text-[9px]" :class="selectedVax?.doseId === vax.doseId ? 'text-white/50' : 'text-slate-400'">
                              Due: {{ formatDisplayDate(vax.originalDueDate) }}
                              <span class="text-amber-500 font-bold ml-1">+{{ vax.daysLate }}d late</span>
                            </p>
                            <p class="text-[10px] font-bold" :class="selectedVax?.doseId === vax.doseId ? 'text-white/80' : 'text-slate-600'">Given: {{ formatDisplayDate(vax.administeredDate) }}</p>
                          </div>
                          <p v-else-if="vax.isCompleted" class="text-[10px] font-bold mt-0.5" :class="selectedVax?.doseId === vax.doseId ? 'text-white/80' : 'text-slate-600'">Given: {{ formatDisplayDate(vax.administeredDate) }}</p>
                          <p v-else class="text-[10px] mt-0.5" :class="selectedVax?.doseId === vax.doseId ? 'text-white/70' : 'text-slate-400'">{{ formatDisplayDate(vax.scheduledDate) }}</p>
                        </div>
                      </div>
                      <span v-if="selectedVax?.doseId === vax.doseId" class="text-[9px] font-bold text-white/80 uppercase tracking-wide shrink-0">Viewing →</span>
                      <span v-else-if="vax.isCompleted" class="text-[9px] font-bold text-emerald-600 uppercase shrink-0">✓ Done</span>
                    </button>
                  </template>
                </div>
              </div>
              <div class="w-full lg:flex-1 flex flex-col gap-4 lg:sticky lg:top-22">
                <div class="bg-white rounded-xl border border-slate-200 shadow-sm p-7">
                  <div v-if="selectedVax" class="flex items-center gap-3 mb-6 pb-5 border-b border-slate-50">
                    <div class="w-9 h-9 bg-emerald-600 rounded-xl flex items-center justify-center text-white text-sm">💉</div>
                    <div>
                      <p class="text-sm font-bold text-slate-800 leading-tight">{{ selectedVax.name }}</p>
                      <p class="text-[10px] text-emerald-500 font-bold uppercase">Dose {{ selectedVax.doseNumber }}</p>
                    </div>
                    <span class="ml-auto text-[10px] font-bold text-emerald-600 bg-emerald-600/10 px-3 py-1.5 rounded-full uppercase">
                      {{ selectedVax.isCompleted ? formatDisplayDate(selectedVax.administeredDate) : formatDisplayDate(selectedVax.scheduledDate) }}
                    </span>
                  </div>
                  <div class="flex items-center justify-between mb-5">
                    <button @click="prevMonth" class="w-8 h-8 rounded-xl bg-slate-50 hover:bg-slate-100 flex items-center justify-center text-slate-500 font-bold transition-all text-sm">‹</button>
                    <h3 class="font-bold text-base text-slate-800">{{ calendarMonthLabel }}</h3>
                    <button @click="nextMonth" class="w-8 h-8 rounded-xl bg-slate-50 hover:bg-slate-100 flex items-center justify-center text-slate-500 font-bold transition-all text-sm">›</button>
                  </div>
                  <div class="grid grid-cols-7 gap-1 mb-2">
                    <div v-for="(label, i) in ['S','M','T','W','T','F','S']" :key="i" class="text-center text-[9px] font-bold text-slate-300 uppercase py-1">{{ label }}</div>
                  </div>
                  <div class="grid grid-cols-7 gap-1">
                    <div v-for="n in calendarOffset" :key="'sp-' + n" class="aspect-square"></div>
                    <div v-for="day in calendarDaysInMonth" :key="day" :class="getDayClass(day)" class="aspect-square flex items-center justify-center rounded-xl text-[11px] font-bold transition-all">{{ day }}</div>
                  </div>
                  <div class="mt-5 flex flex-wrap items-center justify-center gap-4 border-t border-slate-50 pt-5">
                    <div class="flex items-center gap-1.5"><div class="w-2.5 h-2.5 rounded-full bg-emerald-600"></div><span class="text-[9px] text-slate-400 font-bold uppercase">Scheduled</span></div>
                    <div class="flex items-center gap-1.5"><div class="w-2.5 h-2.5 rounded-full bg-emerald-500"></div><span class="text-[9px] text-slate-400 font-bold uppercase">Given</span></div>
                    <div class="flex items-center gap-1.5"><div class="w-2.5 h-2.5 rounded-full bg-red-400"></div><span class="text-[9px] text-slate-400 font-bold uppercase">Missed Due Date</span></div>
                    <div class="flex items-center gap-1.5"><div class="w-2.5 h-2.5 rounded-full bg-emerald-500/40 border border-emerald-500/40"></div><span class="text-[9px] text-slate-400 font-bold uppercase">Catch-up Window</span></div>
                    <div class="flex items-center gap-1.5"><div class="w-2.5 h-2.5 rounded-full bg-slate-100"></div><span class="text-[9px] text-slate-400 font-bold uppercase">Vaccination Day</span></div>
                  </div>
                </div>
                <div class="space-y-2">
                  <ClinicHoursNote :clinic="clinic" />
                  <div class="bg-red-50 border-l-4 border-red-500 px-4 py-3 rounded-r-lg flex items-center gap-2">
                    <span class="text-[9px] font-bold text-red-700 uppercase">⚠ Stocks may change without notice</span>
                  </div>
                  <div v-if="selectedVax && !selectedVax.isCompleted" class="bg-emerald-50 border-l-4 border-emerald-500 px-4 py-3 rounded-r-lg">
                    <p class="text-[10px] text-emerald-600 font-bold uppercase mb-0.5">Catch-up Window</p>
                    <p class="text-[10px] text-slate-500">{{ formatDisplayDate(selectedVax.scheduledDate) }} <span class="text-slate-300 mx-1">→</span> {{ formatDisplayDate(windowEndDate) }}</p>
                  </div>
                </div>
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
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { addDays, format, getDaysInMonth, startOfMonth } from 'date-fns'
import HeaderNav from '../Components/Headernav.vue'
import ChildSidebar from '../Components/Childsidebar.vue'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'
import ClinicHoursNote from '../Components/ClinicHoursNote.vue'
import { getAccount, logout as authLogout } from '@/utils/auth'
import api from '../Composables/api.js'
import { fetchChildSchedule, buildSchedule } from '../Composables/childSchedule.js'

const router = useRouter()

// ── State ──────────────────────────────────────────────────────────────────
const parentData        = ref(null)
const children           = ref([])
const selectedChild      = ref(null)
const showProfile        = ref(false)
const showNotifications  = ref(false)
const completedRecords   = ref([])
const unreadCount        = ref(0)

// ── Vaccination schedule — from the backend timeline, shared with the
//    other parent pages (Composables/childSchedule.js) ─────────────────────
const scheduleTimeline = ref([])

function formatDisplayDate(date) {
  if (!date) return '—'
  try { return format(new Date(date), 'MMM d, yyyy') } catch { return '—' }
}

const computedVaccineList = computed(() => buildSchedule(scheduleTimeline.value, completedRecords.value))

const groupedVaccineList = computed(() => {
  const map = new Map()
  for (const vax of computedVaccineList.value) {
    if (!map.has(vax.name)) map.set(vax.name, { name: vax.name, doses: [] })
    map.get(vax.name).doses.push(vax)
  }
  return Array.from(map.values())
})

// ── Calendar ───────────────────────────────────────────────────────────────
const selectedVax = ref(null)
const calYear     = ref(new Date().getFullYear())
const calMonth    = ref(new Date().getMonth())

const calendarMonthLabel  = computed(() => format(new Date(calYear.value, calMonth.value, 1), 'MMMM yyyy'))
const calendarDaysInMonth = computed(() => getDaysInMonth(new Date(calYear.value, calMonth.value, 1)))
const calendarOffset      = computed(() => startOfMonth(new Date(calYear.value, calMonth.value, 1)).getDay())
const windowEndDate       = computed(() => selectedVax.value ? addDays(selectedVax.value.scheduledDate, 14) : null)

// Clinic (vaccination) days come from the admin's Operating Hours: the open
// weekdays, with holidays / special openings taking priority.
const clinic = ref(null)   // GET /ClinicOperatingSchedule/today

function isClinicDay(date) {
  const c = clinic.value
  if (!c) return false
  const key = format(date, 'yyyy-MM-dd')
  const exception = (c.exceptions || []).find(e => e.date === key)
  if (exception) return exception.isOpen
  return (c.openDays || []).includes(date.getDay())
}

async function fetchClinicHours() {
  try {
    clinic.value = (await api.get('/ClinicOperatingSchedule/today')).data
  } catch (err) {
    console.error('Failed to load clinic hours:', err)
  }
}

function prevMonth() { calMonth.value === 0 ? (calMonth.value = 11, calYear.value--) : calMonth.value-- }
function nextMonth() { calMonth.value === 11 ? (calMonth.value = 0, calYear.value++) : calMonth.value++ }

function getDayClass(day) {
  if (!selectedVax.value) return 'text-slate-200'
  const date      = new Date(calYear.value, calMonth.value, day)
  const scheduled = selectedVax.value.scheduledDate
  const winEnd    = windowEndDate.value
  const clinicDay = isClinicDay(date)
  const sameDay   = (a, b) => a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()

  if (selectedVax.value.wasLate && selectedVax.value.originalDueDate && sameDay(date, selectedVax.value.originalDueDate))
    return 'bg-red-400 text-white shadow-lg scale-110 font-bold z-10 cursor-default'
  if (selectedVax.value.isCompleted && selectedVax.value.administeredDate && sameDay(date, selectedVax.value.administeredDate))
    return 'bg-emerald-500 text-white shadow-lg scale-110 font-bold z-10 cursor-default'
  if (!selectedVax.value.isCompleted && sameDay(date, scheduled))
    return 'bg-emerald-600 text-white shadow-lg scale-110 font-bold z-10 cursor-default'
  if (!selectedVax.value.isCompleted && clinicDay && winEnd && date > scheduled && date <= winEnd)
    return 'bg-emerald-500/30 text-slate-700 border-b-2 border-emerald-500 cursor-pointer'
  if (clinicDay) return 'bg-slate-100 text-slate-400 cursor-pointer'
  return 'text-slate-300 pointer-events-none'
}

// ── Watchers ───────────────────────────────────────────────────────────────
watch(computedVaccineList, (list) => {
  if (list.length && (!selectedVax.value || !list.find(v => v.doseId === selectedVax.value?.doseId)))
    selectedVax.value = list[0]
}, { immediate: true })

watch(selectedVax, (vax) => {
  if (vax?.scheduledDate) {
    calYear.value  = new Date(vax.scheduledDate).getFullYear()
    calMonth.value = new Date(vax.scheduledDate).getMonth()
  }
}, { immediate: true })

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
  try {
    const { timeline, records } = await fetchChildSchedule(childId)
    scheduleTimeline.value = timeline
    completedRecords.value = records
  } catch (err) {
    console.error('fetchRecords error:', err)
    completedRecords.value = []
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
  fetchClinicHours()

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