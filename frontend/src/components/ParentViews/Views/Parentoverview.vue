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
          <div class="space-y-5 animate-in slide-in-from-bottom-4 duration-500">
            <div class="bg-white rounded-xl border border-slate-200 shadow-sm p-8 flex flex-col md:flex-row justify-between items-center gap-6">
              <div>
                <p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-2">Today's Priority Ticket</p>

                <!-- Checked in today -->
                <template v-if="queueStatus.checkedIn">
                  <h2 v-if="queueStatus.myStatus === 'Completed'" class="text-2xl font-black text-slate-800">Visit completed <span class="text-emerald-500">#{{ String(queueStatus.myQueueNumber).padStart(3, '0') }}</span></h2>
                  <h2 v-else class="text-4xl font-black text-slate-800">You are Queue <span class="text-emerald-500">#{{ String(queueStatus.myQueueNumber).padStart(3, '0') }}</span></h2>
                  <p v-if="ticketChildNames" class="text-sm font-bold text-slate-500 mt-1">For {{ ticketChildNames }}</p>
                </template>

                <!-- Account with no child yet -->
                <template v-else-if="childrenLoaded && children.length === 0">
                  <h2 class="text-2xl font-black text-slate-800">No children linked yet</h2>
                  <p class="text-sm font-bold text-slate-500 mt-1">Please ask the clinic staff to link your child to this account.</p>
                </template>

                <!-- Health center closed today (typhoon, holiday...) -->
                <template v-else-if="clinic?.closedToday">
                  <h2 class="text-2xl font-black text-slate-800">Health center closed today</h2>
                  <p class="text-sm font-bold text-slate-500 mt-1">{{ clinic.reason ? `${clinic.reason}. ` : '' }}Next vaccination day: {{ clinic.nextOpenDay }}.</p>
                </template>

                <!-- Not a vaccination day -->
                <template v-else-if="clinic && !clinic.openToday">
                  <h2 class="text-2xl font-black text-slate-800">No vaccinations today</h2>
                  <p class="text-sm font-bold text-slate-500 mt-1">Vaccinations are given {{ clinic.hoursText }}. Next vaccination day: {{ clinic.nextOpenDay }}.</p>
                </template>

                <!-- Clinic day, not checked in yet -->
                <template v-else>
                  <h2 class="text-2xl font-black text-slate-800">Not checked in today</h2>
                  <p v-if="clinic?.checkInOpenNow" class="text-sm font-bold text-slate-500 mt-1">
                    When you arrive at the clinic, open
                    <router-link to="/ParentCheckin" class="text-emerald-600 underline">Check-in</router-link>
                    and scan the QR code at the entrance.
                  </p>
                  <p v-else-if="clinic" class="text-sm font-bold text-slate-500 mt-1">Check-in for vaccinations today is from {{ clinic.opensAt }} until {{ clinic.checkInUntil }}. The next vaccination day is {{ clinic.nextOpenDay }}.</p>
                </template>
              </div>
              <div v-if="showNowServing" class="bg-emerald-50 px-10 py-6 rounded-xl border border-emerald-500/20 text-center min-w-45">
                <p class="text-[10px] font-black text-emerald-600 uppercase mb-1">{{ queueStatus.checkedIn ? 'Now Serving' : 'Now Serving at the Clinic' }}</p>
                <p class="text-5xl font-black text-slate-800">{{ queueStatus.nowServingNumber ? '#' + String(queueStatus.nowServingNumber).padStart(3, '0') : '—' }}</p>
                <p v-if="queueStatus.positionInLine" class="text-[10px] font-bold text-emerald-600 mt-2 uppercase">{{ queueStatus.positionInLine }}{{ queueStatus.positionInLine === 1 ? 'st' : queueStatus.positionInLine === 2 ? 'nd' : queueStatus.positionInLine === 3 ? 'rd' : 'th' }} in line</p>
              </div>
            </div>

            <div v-if="selectedChild" class="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
              <div class="bg-emerald-600 px-8 py-5 flex items-center gap-5">
                <div class="w-14 h-14 rounded-xl bg-white/20 flex items-center justify-center text-3xl shadow">{{ selectedChild.sex === 'Female' ? '👧' : '👶' }}</div>
                <div>
                  <p class="text-white font-bold text-lg leading-tight">{{ selectedChild.firstName }} {{ selectedChild.lastName }}</p>
                  <p class="text-emerald-200 text-[10px] font-bold uppercase mt-0.5">{{ selectedChild.healthCenter || 'Leveriza Health Center' }}</p>
                </div>
                <div v-if="selectedChild.familyNo" class="ml-auto text-right hidden sm:block">
                  <p class="text-[10px] text-white/60 font-black uppercase">Family No.</p>
                  <p class="text-white font-mono text-xs font-bold">{{ selectedChild.familyNo }}</p>
                </div>
              </div>
              <div class="grid grid-cols-2 sm:grid-cols-3 divide-x divide-y divide-slate-50">
                <div class="px-6 py-5"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Date of Birth</p><p class="text-sm font-bold text-slate-800">{{ formatDisplayDate(new Date(selectedChild.birthDate)) }}</p></div>
                <div class="px-6 py-5"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Age</p><p class="text-sm font-bold text-slate-800">{{ childAge }}</p></div>
                <div class="px-6 py-5"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Sex</p><p class="text-sm font-bold text-slate-800">{{ selectedChild.sex || '—' }}</p></div>
                <div class="px-6 py-5"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Birth Weight</p><p class="text-sm font-bold text-slate-800">{{ selectedChild.birthWeight ? Number(selectedChild.birthWeight) + ' kg' : 'Not recorded' }}</p></div>
                <div class="px-6 py-5"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Birth Height</p><p class="text-sm font-bold text-slate-800">{{ selectedChild.birthHeight ? Number(selectedChild.birthHeight) + ' cm' : 'Not recorded' }}</p></div>
                <div class="px-6 py-5">
                  <p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Barangay</p>
                  <p class="text-sm font-bold text-slate-800">{{ selectedChild.barangay || selectedChild.barangayNo || selectedChild.Barangay || '—' }}</p>
                </div>
              </div>
            </div>

            <div class="bg-white rounded-xl border border-slate-200 p-7 shadow-sm">
              <div class="flex justify-between items-center mb-5">
                <h3 class="text-base font-bold text-slate-800">Upcoming Doses</h3>
                <router-link to="/ParentSchedule" class="text-[10px] font-semibold text-emerald-600 uppercase tracking-wide hover:text-emerald-600 transition-colors">View Schedule →</router-link>
              </div>
              <div class="space-y-3">
                <div v-for="vax in upcomingDoses.slice(0, 3)" :key="vax.doseId"
                    class="p-4 bg-slate-50 rounded-xl border border-slate-100 flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <div class="w-10 h-10 bg-emerald-600 rounded-xl flex items-center justify-center text-white text-sm">💉</div>
                    <div>
                      <p class="text-xs font-bold text-slate-800">{{ vax.name }} · Dose {{ vax.doseNumber }}</p>
                      <p class="text-[10px] text-slate-400 mt-0.5">{{ formatDisplayDate(vax.scheduledDate) }}</p>
                    </div>
                  </div>
                  <span v-if="isOverdue(vax)" class="px-3 py-1 bg-red-50 rounded-lg text-[9px] font-black text-red-600 border border-red-200 uppercase">Overdue</span>
                  <span v-else class="px-3 py-1 bg-white rounded-lg text-[9px] font-black text-emerald-600 border border-slate-200 uppercase">Scheduled</span>
                </div>
                <p v-if="upcomingDoses.length > 3" class="text-center text-[10px] text-slate-400 font-bold pt-1">
                  +{{ upcomingDoses.length - 3 }} more doses to go
                </p>
                <p v-if="!timelineLoading && upcomingDoses.length === 0" class="text-xs text-slate-400">
                  {{ children.length === 0 ? 'Doses will appear here once your child is linked to this account.' : 'No upcoming doses on file.' }}
                </p>
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
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { addDays, isMonday, isWednesday, isFriday } from 'date-fns'

import HeaderNav from '../Components/Headernav.vue'
import ChildSidebar from '../Components/Childsidebar.vue'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'

import { getAccount, logout as authLogout } from '@/utils/auth'
import api from '../Composables/api.js'
import { fetchChildSchedule, buildSchedule } from '../Composables/childSchedule.js'

const router = useRouter()

// =====================================================
// SESSION
// =====================================================

const account = ref(null)
const parentData = ref(null)


// =====================================================
// PARENT / CHILDREN
// =====================================================

const children = ref([])
const childrenLoaded = ref(false)
const selectedChild = ref(null)


// =====================================================
// VACCINATION SCHEDULE (shared with Schedule / Records /
// Check-in via Composables/childSchedule.js, so
// "upcoming doses" always means the same thing everywhere)
// =====================================================

const completedRecords = ref([])
const timelineLoading = ref(false)

// Schedule comes from the backend timeline (see Composables/childSchedule.js).
const scheduleTimeline = ref([])

const computedVaccineList = computed(() => buildSchedule(scheduleTimeline.value, completedRecords.value))

async function fetchCompletedRecords(childId) {
  if (!childId) { completedRecords.value = []; scheduleTimeline.value = []; return }
  timelineLoading.value = true
  try {
    const { timeline, records } = await fetchChildSchedule(childId)
    scheduleTimeline.value = timeline
    completedRecords.value = records
  } catch (error) {
    console.error('Failed to load vaccination schedule:', error)
    completedRecords.value = []
    scheduleTimeline.value = []
  } finally {
    timelineLoading.value = false
  }
}

// =====================================================
// QUEUE TICKET (Today's Priority Ticket card)
// =====================================================

const queueStatus = ref({ checkedIn: false, myQueueNumber: null, myStatus: null, nowServingNumber: null, positionInLine: null, childIDs: [] })
let queuePollHandle = null

// Clinic hours for today (GET /ClinicOperatingSchedule/today): on a closed
// day there is no queue to show.
const clinic = ref(null)

async function fetchClinicToday() {
  try {
    clinic.value = (await api.get('/ClinicOperatingSchedule/today')).data
  } catch (error) {
    console.error('Failed to load clinic hours:', error)
  }
}

// "Now serving" only means something on a clinic day (or once checked in).
const showNowServing = computed(() =>
  queueStatus.value.checkedIn ||
  (!!queueStatus.value.nowServingNumber && children.value.length > 0 && clinic.value?.openToday !== false))

const ticketChildNames = computed(() =>
  (queueStatus.value.childIDs || [])
    .map(id => children.value.find(c => String(c.childID).toLowerCase() === String(id).toLowerCase()))
    .filter(Boolean)
    .map(c => c.firstName)
    .join(', '))

async function fetchQueueStatus() {
  if (!parentData.value?.parentID) return
  try {
    const response = await api.get(`/Queue/my-status/${parentData.value.parentID}`)
    queueStatus.value = {
      checkedIn: response.data.checkedIn ?? response.data.CheckedIn,
      myQueueNumber: response.data.myQueueNumber ?? response.data.MyQueueNumber,
      myStatus: response.data.myStatus ?? response.data.MyStatus,
      nowServingNumber: response.data.nowServingNumber ?? response.data.NowServingNumber,
      positionInLine: response.data.positionInLine ?? response.data.PositionInLine,
      childIDs: response.data.childIDs ?? response.data.ChildIDs ?? [],
    }
  } catch (error) {
    console.error('Failed to load queue status:', error)
  }
}


// =====================================================
// NOTIFICATIONS
// =====================================================

const unreadCount = ref(0)


// =====================================================
// UI
// =====================================================

const showProfile = ref(false)
const showNotifications = ref(false)


// =====================================================
// SELECTED CHILD
// =====================================================

const selectedChildStorageKey = 'selectedParentChild'


// =====================================================
// HELPERS
// =====================================================

function formatDisplayDate(date) {
  if (!date) return '—'

  const parsed = new Date(date)

  if (Number.isNaN(parsed.getTime())) {
    return '—'
  }

  return parsed.toLocaleDateString('en-PH', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}


function calculateAge(birthDate) {
  if (!birthDate) return '—'

  const birth = new Date(birthDate)
  const today = new Date()

  if (Number.isNaN(birth.getTime())) {
    return '—'
  }

  let years =
    today.getFullYear() -
    birth.getFullYear()

  let months =
    today.getMonth() -
    birth.getMonth()

  if (
    months < 0 ||
    (
      months === 0 &&
      today.getDate() < birth.getDate()
    )
  ) {
    years--
  }

  months =
    (
      today.getMonth() -
      birth.getMonth() +
      12
    ) % 12

  if (years === 0) {
    return `${months} month${months !== 1 ? 's' : ''}`
  }

  if (months === 0) {
    return `${years} year${years !== 1 ? 's' : ''}`
  }

  return `${years} year${years !== 1 ? 's' : ''} ${months} month${months !== 1 ? 's' : ''}`
}


// =====================================================
// COMPUTED
// =====================================================

const childAge = computed(() => {
  return calculateAge(
    selectedChild.value?.birthDate
  )
})


// Same rule as the Records page: a dose not given by its date is overdue.
function isOverdue(vax) {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return !vax.isCompleted && vax.scheduledDate < today
}

const upcomingDoses = computed(() => {
  return computedVaccineList.value
    .filter(item => !item.isCompleted)
    .sort((a, b) => a.scheduledDate - b.scheduledDate)
})


// =====================================================
// LOAD LOGGED-IN PARENT
// =====================================================

function loadParentSession() {
  const savedAccount = getAccount()

  if (!savedAccount) {
    router.push('/')
    return false
  }

  account.value = savedAccount

  // Support both possible backend response structures
  const rawUser = savedAccount.user ?? savedAccount

  // Backend now returns PascalCase (ParentID, FirstName, LastName, Email, ...).
  // Normalize to the camelCase shape every Parent component (Headernav,
  // ChildSidebar, ProfileModal, etc.) already expects, same ?? pattern used
  // for the children map below.
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
    console.error(
      'Invalid parent session:',
      savedAccount
    )

    router.push('/')
    return false
  }

  return true
}

// =====================================================
// LOAD CHILDREN
// =====================================================

async function fetchChildren() {

  if (!parentData.value?.parentID) {
    console.error('No ParentID available')
    return
  }

  try {

    console.log(
      'Fetching children for ParentID:',
      parentData.value.parentID
    )

    const response = await api.get(
      `/Parents/dashboard/${parentData.value.parentID}`
    )

    console.log(
      'Parent dashboard response:',
      response.data
    )

    children.value =
      (response.data ?? []).map(child => {
        const relationshipType =
          child.relationshipType ??
          child.RelationshipType

        const relatedParentName =
          child.parentFullName ??
          child.ParentFullName

        return {
          childID:
            child.childID ??
            child.ChildID,

          firstName:
            child.firstName ??
            child.FirstName,

          middleName:
            child.middleName ??
            child.MiddleName,

          lastName:
            child.lastName ??
            child.LastName,

          birthDate:
            child.birthDate ??
            child.BirthDate,

          placeOfBirth:
            child.placeOfBirth ??
            child.PlaceOfBirth,

          sex:
            child.sex ??
            child.Sex,

          barangay:
            child.barangay ??
            child.Barangay,

          familyNo:
            child.familyNo ??
            child.FamilyNo,

          address:
            child.address ??
            child.Address,

          healthCenter:
            child.healthCenter ??
            child.HealthCenter,

          birthWeight:
            child.birthWeight ??
            child.BirthWeight,

          birthHeight:
            child.birthHeight ??
            child.BirthHeight,

          relationshipType,

          isPrimaryContact:
            child.isPrimaryContact ??
            child.IsPrimaryContact,

          canReceiveNotifications:
            child.canReceiveNotifications ??
            child.CanReceiveNotifications,

          // Everyone linked to the child: "Maria Santos (Mother); Rosario Santos (Grandmother)"
          guardians:
            child.guardians ??
            child.Guardians,

          // The dashboard endpoint only returns the relationship for the
          // currently logged-in parent (Maria), so we can only fill in
          // whichever one of these matches her own RelationshipType —
          // co-parents/guardians linked by someone else aren't in this
          // response and would need a separate lookup to display.
          motherName:   relationshipType === 'Mother'   ? relatedParentName : undefined,
          fatherName:   relationshipType === 'Father'   ? relatedParentName : undefined,
          guardianName: relationshipType === 'Guardian' ? relatedParentName : undefined,
        }
      })

    console.log(
      'Loaded children:',
      children.value
    )

  } catch (error) {

    console.error(
      'Failed to load parent children:',
      error
    )

    children.value = []
  }
}
// =====================================================
// RESTORE SELECTED CHILD
// =====================================================

function restoreSelectedChild() {

  if (children.value.length === 0) {
    selectedChild.value = null
    return
  }

  const saved =
    localStorage.getItem(
      selectedChildStorageKey
    )

  if (!saved) {
    selectedChild.value =
      children.value[0]

    return
  }

  try {

    const savedChild =
      JSON.parse(saved)

    selectedChild.value =
      children.value.find(
        child =>
          child.childID ===
          savedChild.childID
      ) ??
      children.value[0]

  } catch {

    selectedChild.value =
      children.value[0]
  }
}


// =====================================================
// SELECT CHILD
// =====================================================

async function handleSelectChild(child) {

  selectedChild.value = child

  localStorage.setItem(
    selectedChildStorageKey,
    JSON.stringify(child)
  )

  await fetchCompletedRecords(
    child.childID
  )
}


// =====================================================
// LOAD UNREAD NOTIFICATION COUNT
// =====================================================

async function fetchUnreadCount() {

  if (!parentData.value?.parentID) {
    return
  }

  try {

    const response = await api.get(
      `/Notifications/parent/${parentData.value.parentID}`
    )

    unreadCount.value =
      (response.data ?? [])
        .filter(notification =>
          !notification.isRead
        )
        .length

  } catch (error) {

    console.error(
      'Failed to load notification count:',
      error
    )

    unreadCount.value = 0
  }
}


// =====================================================
// LOGOUT
// =====================================================

function handleLogout() {

  authLogout()

  router.push('/')
}


// =====================================================
// INITIAL LOAD
// =====================================================

onMounted(async () => {

  // 1. Identify currently logged-in parent
  const validSession =
    loadParentSession()

  if (!validSession) {
    return
  }


  // 2. Load all children (and today's clinic hours alongside)
  fetchClinicToday()
  await fetchChildren()
  childrenLoaded.value = true


  // 3. Restore selected child
  restoreSelectedChild()


  // 4. Load vaccination records for the selected child
  if (selectedChild.value) {

    await fetchCompletedRecords(
      selectedChild.value.childID
    )
  }


  // 5. Load unread notifications
  await fetchUnreadCount()

  // 6. Load queue ticket status, then poll so "Now Serving" stays live
  await fetchQueueStatus()
  queuePollHandle = setInterval(fetchQueueStatus, 5000)
})

onUnmounted(() => {
  if (queuePollHandle) clearInterval(queuePollHandle)
})
</script>