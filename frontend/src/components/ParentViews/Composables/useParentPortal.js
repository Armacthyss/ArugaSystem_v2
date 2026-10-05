// Everything the parent pages share: the signed-in parent, their children,
// the selected child (remembered between pages), that child's vaccination
// schedule, today's clinic hours and the unread notification count.
// Used by the Overview and Schedule pages.
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getAccount, logout as authLogout } from '@/utils/auth'
import api from './api'
import { fetchChildSchedule, buildSchedule } from './childSchedule'

const SELECTED_KEY = 'selectedParentChild'

// The backend may answer in camelCase or PascalCase; the pages use camelCase.
export function mapChild(child) {
  const pick = (a, b) => child[a] ?? child[b]
  return {
    childID: pick('childID', 'ChildID'),
    firstName: pick('firstName', 'FirstName'),
    middleName: pick('middleName', 'MiddleName'),
    lastName: pick('lastName', 'LastName'),
    birthDate: pick('birthDate', 'BirthDate'),
    placeOfBirth: pick('placeOfBirth', 'PlaceOfBirth'),
    sex: pick('sex', 'Sex'),
    barangay: pick('barangay', 'Barangay'),
    familyNo: pick('familyNo', 'FamilyNo'),
    address: pick('address', 'Address'),
    healthCenter: pick('healthCenter', 'HealthCenter'),
    birthWeight: pick('birthWeight', 'BirthWeight'),
    birthHeight: pick('birthHeight', 'BirthHeight'),
    relationshipType: pick('relationshipType', 'RelationshipType'),
    isPrimaryContact: pick('isPrimaryContact', 'IsPrimaryContact'),
    canReceiveNotifications: pick('canReceiveNotifications', 'CanReceiveNotifications'),
    // "Maria Santos (Mother); Rosario Santos (Grandmother)"
    guardians: pick('guardians', 'Guardians'),
  }
}

// The parent saved at sign-in (Login.vue), normalized to camelCase
export function readParentSession() {
  const saved = getAccount()
  if (!saved) return null
  const u = saved.user ?? saved
  const parent = {
    ...u,
    parentID: u.parentID ?? u.ParentID,
    firstName: u.firstName ?? u.FirstName,
    middleName: u.middleName ?? u.MiddleName,
    lastName: u.lastName ?? u.LastName,
    email: u.email ?? u.Email,
    contactNo: u.contactNo ?? u.ContactNo,
    barangayNo: u.barangayNo ?? u.BarangayNo,
    address: u.address ?? u.Address,
  }
  return parent.parentID ? parent : null
}

export function useParentPortal() {
  const router = useRouter()

  const parentData = ref(null)
  const children = ref([])
  const childrenLoaded = ref(false)
  const childrenError = ref('')
  const selectedChild = ref(null)
  const unreadCount = ref(0)
  const clinic = ref(null)          // GET /ClinicOperatingSchedule/today

  const timeline = ref([])
  const records = ref([])
  const scheduleLoading = ref(false)
  const schedule = computed(() => buildSchedule(timeline.value, records.value))

  // A slow answer for a child the parent already switched away from is ignored
  let request = 0
  async function loadSchedule(childId) {
    const mine = ++request
    scheduleLoading.value = true
    try {
      const result = await fetchChildSchedule(childId)
      if (mine !== request) return
      records.value = result.records
      timeline.value = result.timeline
    } catch (err) {
      console.error('Could not load the vaccination schedule:', err)
      if (mine === request) { timeline.value = []; records.value = [] }
    } finally {
      if (mine === request) scheduleLoading.value = false
    }
  }

  async function selectChild(child) {
    if (!child) return
    selectedChild.value = child
    try { localStorage.setItem(SELECTED_KEY, JSON.stringify(child)) } catch { /* storage blocked */ }
    await loadSchedule(child.childID)
  }

  async function loadClinic() {
    try {
      clinic.value = (await api.get('/ClinicOperatingSchedule/today')).data
    } catch (err) {
      console.error('Could not load clinic hours:', err)
    }
  }

  async function loadUnreadCount() {
    if (!parentData.value?.parentID) return
    try {
      const res = await api.get(`/Notifications/parent/${parentData.value.parentID}`)
      unreadCount.value = (res.data ?? []).filter(n => !n.isRead).length
    } catch {
      unreadCount.value = 0
    }
  }

  function savedChildId() {
    try { return JSON.parse(localStorage.getItem(SELECTED_KEY) || 'null')?.childID ?? null } catch { return null }
  }

  // Returns false when there is no valid parent session (the page stops)
  async function init() {
    parentData.value = readParentSession()
    if (!parentData.value) {
      router.push('/')
      return false
    }

    loadClinic()
    loadUnreadCount()

    try {
      const res = await api.get(`/Parents/dashboard/${parentData.value.parentID}`)
      children.value = (res.data ?? []).map(mapChild)
    } catch (err) {
      console.error('Could not load the children:', err)
      children.value = []
      childrenError.value = 'Your children could not be loaded right now. Please try again later.'
    } finally {
      childrenLoaded.value = true
    }

    const remembered = children.value.find(c => c.childID === savedChildId())
    await selectChild(remembered ?? children.value[0])
    return true
  }

  function logout() {
    authLogout()
    router.push('/')
  }

  return {
    parentData, children, childrenLoaded, childrenError, selectedChild,
    unreadCount, clinic, schedule, scheduleLoading,
    init, selectChild, loadClinic, logout,
  }
}
