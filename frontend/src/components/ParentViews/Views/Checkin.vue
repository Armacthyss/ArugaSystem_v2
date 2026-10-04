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
          <div class="animate-in fade-in zoom-in-95 duration-500 space-y-4">

            <!-- Children load error -->
            <div v-if="childrenError" class="bg-red-50 border border-red-200 rounded-xl p-5 flex items-center gap-3">
              <span>⚠️</span>
              <p class="text-[11px] text-red-700 font-bold">{{ childrenError }}</p>
            </div>

            <!-- No linked children -->
            <div v-else-if="!childrenLoading && children.length === 0" class="bg-amber-50 border border-amber-200 rounded-xl p-5 flex items-center gap-3">
              <span>⚠️</span>
              <p class="text-[11px] text-amber-700 font-bold">No children are linked to this account yet. Please contact the clinic to link a child before checking in.</p>
            </div>

            <!-- ═══════════════ TODAY'S TICKET ═══════════════ -->
            <div v-if="myStatus?.checkedIn" class="bg-white rounded-xl border border-slate-200 shadow-sm p-8">
              <div class="text-center mb-6">
                <div class="w-16 h-16 rounded-full bg-emerald-50 flex items-center justify-center text-3xl mx-auto mb-3">
                  {{ ticketIcon }}
                </div>
                <h2 class="text-xl font-bold text-slate-800">{{ ticketTitle }}</h2>
                <p class="text-3xl font-black text-emerald-600 mt-2">Queue #{{ String(myStatus.myQueueNumber).padStart(3, '0') }}</p>
                <p class="text-sm text-slate-500 mt-2">{{ ticketMessage }}</p>
                <p v-if="myStatus.nowServingNumber && isWaiting" class="text-xs text-slate-400 mt-1">
                  Now serving #{{ String(myStatus.nowServingNumber).padStart(3, '0') }}
                </p>
              </div>

              <div class="space-y-3">
                <div v-for="item in ticketChildren" :key="item.child.childID"
                     class="p-4 bg-slate-50 rounded-xl border border-slate-100 flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <div class="w-10 h-10 rounded-xl bg-emerald-50 flex items-center justify-center text-xl shrink-0">
                      {{ item.child.sex === 'Female' ? '👧' : '👶' }}
                    </div>
                    <div>
                      <p class="text-xs font-bold text-slate-800">{{ item.child.firstName }} {{ item.child.lastName }}</p>
                      <p v-if="item.nextDose" class="text-[10px] text-slate-400 mt-0.5">
                        Due: {{ item.nextDose.name }} · Dose {{ item.nextDose.doseNumber }} · {{ formatDisplayDate(item.nextDose.scheduledDate) }}
                      </p>
                      <p v-else class="text-[10px] text-slate-400 mt-0.5">No pending doses on file</p>
                    </div>
                  </div>
                </div>
              </div>

              <p class="text-[10px] text-slate-400 text-center mt-6">This page updates by itself. Keep it open while you wait.</p>
            </div>

            <!-- ═══════════════ CHECK IN ═══════════════ -->
            <div v-else-if="!statusLoading" class="bg-slate-800 rounded-xl p-8 sm:p-10 text-center text-white relative overflow-hidden shadow-2xl">
              <div class="absolute inset-0 opacity-5" style="background-image: radial-gradient(circle, #10b981 1px, transparent 1px); background-size: 24px 24px;"></div>
              <div class="relative max-w-md mx-auto">
                <h2 class="text-2xl font-bold mb-2">Check In</h2>

                <!-- Clinic closed / check-in not open right now -->
                <template v-if="clinic && !clinic.checkInOpenNow">
                  <div class="w-20 h-20 mx-auto my-6 rounded-full bg-white/5 border-2 border-white/20 flex items-center justify-center text-4xl">🕒</div>
                  <p class="text-white/80 text-sm">{{ closedMessage }}</p>
                </template>

                <!-- No child has a vaccine due today -->
                <template v-else-if="!childrenLoading && children.length > 0 && !canScanQr">
                  <div class="w-20 h-20 mx-auto my-6 rounded-full bg-white/5 border-2 border-white/20 flex items-center justify-center text-4xl">✅</div>
                  <p class="text-white/80 text-sm">No vaccine is due today for your {{ children.length === 1 ? 'child' : 'children' }}, so there's no need to check in.</p>
                  <p v-if="nextDueNote" class="text-white/50 text-xs mt-2">{{ nextDueNote }}</p>
                </template>

                <!-- QR check-in -->
                <template v-else-if="qrRequired">
                  <p class="text-white/60 text-xs mb-6">
                    When you arrive at Leveriza Health Center, scan the QR code posted at the entrance,
                    or type the 6-letter code printed under it.
                  </p>

                  <div v-show="scanning" class="mx-auto mb-4 w-full max-w-xs overflow-hidden rounded-xl border-2 border-emerald-500 bg-black">
                    <div id="checkin-qr-reader" class="w-full"></div>
                  </div>

                  <div v-if="!scanning" class="w-24 h-24 mx-auto mb-6 rounded-2xl bg-emerald-500/10 border-2 border-emerald-500 flex items-center justify-center text-5xl">
                    📷
                  </div>

                  <button
                    v-if="!scanning"
                    @click="scanQr"
                    :disabled="childrenLoading || children.length === 0 || validating || readingPhoto"
                    class="w-full px-10 py-3.5 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 disabled:cursor-not-allowed text-white rounded-lg font-medium text-sm transition-all shadow-lg"
                  >
                    {{ readingPhoto ? 'Reading the QR code…' : 'Scan the Clinic QR Code' }}
                  </button>
                  <button
                    v-else
                    @click="stopScanner"
                    class="w-full px-10 py-3 bg-white/10 hover:bg-white/20 text-white rounded-lg font-medium text-sm transition-all"
                  >
                    Stop Camera
                  </button>
                  <p v-if="!liveCamera && !readingPhoto" class="text-[11px] text-white/50 mt-2">
                    Your camera will open. Take a photo of the QR code to check in.
                  </p>
                  <input ref="photoInput" type="file" accept="image/*" capture="environment" class="hidden" @change="onQrPhoto" />

                  <div class="flex items-center gap-3 my-5 text-white/30 text-[10px] font-bold uppercase">
                    <span class="h-px flex-1 bg-white/10"></span> or <span class="h-px flex-1 bg-white/10"></span>
                  </div>

                  <form class="flex gap-2" @submit.prevent="useTypedCode">
                    <input
                      v-model="typedCode"
                      maxlength="12"
                      placeholder="Code under the QR"
                      autocapitalize="characters"
                      class="flex-1 min-w-0 rounded-lg bg-white/10 border border-white/20 px-4 py-3 text-center font-bold tracking-[0.3em] uppercase placeholder:tracking-normal placeholder:font-normal placeholder:text-white/40 focus:outline-none focus:border-emerald-400"
                    />
                    <button
                      type="submit"
                      :disabled="!typedCode.trim() || validating || children.length === 0"
                      class="px-5 rounded-lg bg-white text-slate-800 text-sm font-bold disabled:opacity-40"
                    >
                      {{ validating ? '…' : 'Go' }}
                    </button>
                  </form>
                </template>

                <!-- QR switched off by the clinic -->
                <template v-else>
                  <p class="text-white/50 text-xs mb-8">Tap below to check in your child for today's visit.</p>
                  <div class="w-24 h-24 mx-auto mb-8 rounded-full bg-emerald-500/10 border-2 border-emerald-500 flex items-center justify-center text-5xl">
                    🩺
                  </div>
                  <button
                    @click="openChildSelection"
                    :disabled="childrenLoading || children.length === 0"
                    class="px-10 py-3.5 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 disabled:cursor-not-allowed text-white rounded-lg font-medium text-sm transition-all shadow-lg"
                  >
                    Check In Now
                  </button>
                </template>

                <p v-if="qrError" class="text-red-300 text-xs font-bold mt-4">{{ qrError }}</p>
              </div>
            </div>

            <ClinicHoursNote :clinic="clinic" show-check-in />
          </div>
        </main>
      </div>

      <!-- ═══════════════ CHILD SELECTION MODAL ═══════════════ -->
      <div v-if="showChildSelection" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4">
        <div class="bg-white rounded-xl w-full max-w-md p-6 shadow-2xl">
          <h3 class="text-lg font-bold text-slate-800 mb-1">Who are you checking in for?</h3>
          <p class="text-xs text-slate-400 mb-5">Select one or more children linked to your account.</p>

          <div class="space-y-2 max-h-80 overflow-y-auto mb-5">
            <button
              v-for="child in children"
              :key="child.childID"
              type="button"
              @click="toggleChildSelection(child)"
              :disabled="!isChildDue(child)"
              class="w-full flex items-center gap-3 p-3 rounded-xl border transition-all text-left disabled:opacity-50 disabled:cursor-not-allowed"
              :class="isChildSelected(child) ? 'border-emerald-500 bg-emerald-50' : 'border-slate-100 hover:border-slate-200'"
            >
              <div class="w-9 h-9 rounded-xl bg-white border border-slate-100 flex items-center justify-center text-lg shrink-0">
                {{ child.sex === 'Female' ? '👧' : '👶' }}
              </div>
              <div class="flex-1 min-w-0">
                <p class="text-xs font-bold text-slate-800 truncate">{{ child.firstName }} {{ child.lastName }}</p>
                <p v-if="!isChildDue(child)" class="text-[10px] text-slate-400">No vaccine due today</p>
                <p v-else-if="childrenDueCount[child.childID] > 1" class="text-[10px] text-emerald-600">
                  {{ childrenDueCount[child.childID] }} vaccines due
                </p>
                <p v-else-if="childrenNextDose[child.childID]" class="text-[10px] text-emerald-600">
                  Due: {{ childrenNextDose[child.childID].name }} · Dose {{ childrenNextDose[child.childID].doseNumber }}
                </p>
                <p v-else class="text-[10px] text-slate-400">{{ child.relationshipType || 'Linked child' }}</p>
              </div>
              <div
                class="w-5 h-5 rounded-md border-2 flex items-center justify-center shrink-0"
                :class="isChildSelected(child) ? 'bg-emerald-500 border-emerald-500' : 'border-slate-300'"
              >
                <span v-if="isChildSelected(child)" class="text-white text-[10px] font-bold">✓</span>
              </div>
            </button>
          </div>

          <p v-if="selectedChildren.length === 0" class="text-[10px] text-slate-400 font-bold mb-3">Select at least one child to continue.</p>
          <p v-if="checkInError" class="text-[10px] text-red-500 font-bold mb-3">{{ checkInError }}</p>

          <div class="flex gap-3">
            <button
              @click="cancelChildSelection"
              class="flex-1 px-4 py-2.5 text-sm font-medium text-slate-600 border border-slate-200 rounded-lg hover:bg-slate-50 transition-colors"
            >
              Cancel
            </button>
            <button
              @click="confirmChildren"
              :disabled="selectedChildren.length === 0 || confirmLoading"
              class="flex-1 px-4 py-2.5 text-sm font-medium text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-lg transition-colors"
            >
              {{ confirmLoading ? 'Confirming…' : 'Confirm' }}
            </button>
          </div>
        </div>
      </div>

      <ProfileModal v-if="showProfile" :parent-data="parentData" :children="children" @close="showProfile = false" />
      <NotificationPanel v-if="showNotifications" :parent-data="parentData" @close="showNotifications = false" />

    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { format } from 'date-fns'
import { Html5Qrcode } from 'html5-qrcode'
import { BrowserQRCodeReader } from '@zxing/browser'

import HeaderNav from '../Components/Headernav.vue'
import ChildSidebar from '../Components/Childsidebar.vue'
import ProfileModal from '../Components/Profilemodal.vue'
import NotificationPanel from '../Components/Notificationpanel.vue'
import ClinicHoursNote from '../Components/ClinicHoursNote.vue'

import { getAccount, logout as authLogout } from '@/utils/auth'
import api from '../Composables/api.js'
import { fetchChildSchedule, buildSchedule } from '../Composables/childSchedule.js'

const router = useRouter()
const route = useRoute()

// =====================================================
// SESSION / PARENT (same pattern as ParentOverview.vue)
// =====================================================

const parentData = ref(null)

function loadParentSession() {
  const savedAccount = getAccount()

  if (!savedAccount) {
    router.push('/')
    return false
  }

  // Support both possible backend response structures
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
    return false
  }

  return true
}

function handleLogout() {
  authLogout()
  router.push('/')
}

// =====================================================
// CHILDREN (retrieved from the backend — DB relationship
// is the source of truth, not localStorage)
// =====================================================

const children = ref([])
const childrenLoading = ref(true)
const childrenError = ref('')

// Kept only for the sidebar/header "currently viewing" UX —
// this selection has no bearing on who can check in.
const selectedChild = ref(null)

async function fetchChildren() {
  if (!parentData.value?.parentID) {
    console.error('No ParentID available')
    childrenLoading.value = false
    return
  }

  childrenLoading.value = true
  childrenError.value = ''

  try {
    const response = await api.get(`/Parents/dashboard/${parentData.value.parentID}`)

    children.value = (response.data ?? []).map(child => {
      const relationshipType = child.relationshipType ?? child.RelationshipType
      const relatedParentName = child.parentFullName ?? child.ParentFullName

      return {
        childID: child.childID ?? child.ChildID,
        firstName: child.firstName ?? child.FirstName,
        middleName: child.middleName ?? child.MiddleName,
        lastName: child.lastName ?? child.LastName,
        birthDate: child.birthDate ?? child.BirthDate,
        placeOfBirth: child.placeOfBirth ?? child.PlaceOfBirth,
        sex: child.sex ?? child.Sex,
        barangay: child.barangay ?? child.Barangay,
        familyNo: child.familyNo ?? child.FamilyNo,
        address: child.address ?? child.Address,
        healthCenter: child.healthCenter ?? child.HealthCenter,
        relationshipType,
        isPrimaryContact: child.isPrimaryContact ?? child.IsPrimaryContact,
        canReceiveNotifications: child.canReceiveNotifications ?? child.CanReceiveNotifications,
        // Everyone linked to the child: "Maria Santos (Mother); Rosario Santos (Grandmother)"
        guardians: child.guardians ?? child.Guardians,

        // Same derivation as ParentOverview.vue — the dashboard endpoint only
        // returns the relationship for the currently logged-in parent, so we
        // can only fill in whichever of these matches their own relationshipType.
        motherName: relationshipType === 'Mother' ? relatedParentName : undefined,
        fatherName: relationshipType === 'Father' ? relatedParentName : undefined,
        guardianName: relationshipType === 'Guardian' ? relatedParentName : undefined,
      }
    })

    // Load vaccination status for all children to determine if QR scanner should be available
    await Promise.all(children.value.map(child => fetchUpcomingDoses(child.childID)))
  } catch (err) {
    console.error('Failed to load parent children:', err)
    children.value = []
    childrenError.value = 'Unable to load your children right now. Please try again later.'
  } finally {
    childrenLoading.value = false
  }
}

function handleSelectChild(child) {
  // Purely a sidebar/header viewing convenience — does NOT
  // determine who is eligible to check in.
  selectedChild.value = child
}

// =====================================================
// NOTIFICATIONS
// =====================================================

const unreadCount = ref(0)

async function fetchUnreadCount() {
  if (!parentData.value?.parentID) return

  try {
    const response = await api.get(`/Notifications/parent/${parentData.value.parentID}`)
    unreadCount.value = (response.data ?? []).filter(n => !n.isRead).length
  } catch (err) {
    console.error('Failed to load notification count:', err)
    unreadCount.value = 0
  }
}

// =====================================================
// UI PANELS
// =====================================================

const showProfile = ref(false)
const showNotifications = ref(false)

// =====================================================
// CLINIC HOURS + QR SETTING
// =====================================================

const clinic = ref(null)        // GET /ClinicOperatingSchedule/today
const qrRequired = ref(true)    // GET /QueueQRCode/settings

const closedMessage = computed(() => {
  const c = clinic.value
  if (!c) return ''
  if (c.closedToday) {
    return `The health center is closed today${c.reason ? ` (${c.reason})` : ''}. The next vaccination day is ${c.nextOpenDay}.`
  }
  if (!c.openToday) {
    return `There are no vaccinations today. The next vaccination day is ${c.nextOpenDay}.`
  }
  if (c.checkInNotYetOpen) {
    return `Today is a vaccination day. Check-in opens at ${c.opensAt} and runs until ${c.checkInUntil}.`
  }
  return `Check-in for vaccinations today closed at ${c.checkInUntil}. The next vaccination day is ${c.nextOpenDay}.`
})

async function loadClinicInfo() {
  try {
    const [today, settings] = await Promise.all([
      api.get('/ClinicOperatingSchedule/today'),
      api.get('/QueueQRCode/settings'),
    ])
    clinic.value = today.data
    qrRequired.value = !!settings.data?.isEnabled
  } catch (err) {
    console.error('Failed to load clinic hours:', err)
  }
}

// =====================================================
// TODAY'S TICKET (already checked in?)
// =====================================================

const myStatus = ref(null)
const statusLoading = ref(true)
const ticketChildren = ref([])
let pollTimer = null

const isWaiting = computed(() => (myStatus.value?.myStatus || '') === 'Waiting')
const isAtStation = computed(() => /inprogress/i.test((myStatus.value?.myStatus || '').replace(/\s+/g, '')))
const isDone = computed(() => (myStatus.value?.myStatus || '') === 'Completed')

const ticketIcon = computed(() => (isDone.value ? '🎉' : isAtStation.value ? '🩺' : '✅'))
const ticketTitle = computed(() => (isDone.value ? 'Visit Completed' : isAtStation.value ? 'It’s Your Turn' : 'You’re Checked In'))
const ticketMessage = computed(() => {
  const s = myStatus.value
  if (!s) return ''
  if (isDone.value) return 'Thank you for visiting Leveriza Health Center! See the Records page for today’s vaccines.'
  if (isAtStation.value) return 'Please go inside the vaccination room now.'
  if (s.positionInLine === 1) return 'You’re next! Please stay nearby.'
  if (s.positionInLine) return `There ${s.positionInLine - 1 === 1 ? 'is 1 family' : `are ${s.positionInLine - 1} families`} ahead of you. Please wait to be called.`
  return 'Please wait to be called.'
})

async function loadMyStatus() {
  if (!parentData.value?.parentID) return
  try {
    const res = await api.get(`/Queue/my-status/${parentData.value.parentID}`)
    const firstLoad = !myStatus.value?.checkedIn && res.data?.checkedIn
    myStatus.value = res.data
    if (firstLoad || (res.data?.checkedIn && ticketChildren.value.length === 0)) await loadTicketChildren()
  } catch (err) {
    console.error('Failed to load queue status:', err)
  } finally {
    statusLoading.value = false
  }
}

async function loadTicketChildren() {
  const ids = (myStatus.value?.childIDs || []).map(String).map(s => s.toLowerCase())
  const list = children.value.filter(c => ids.includes(String(c.childID).toLowerCase()))
  const summaries = []
  for (const child of list) {
    const upcoming = await fetchUpcomingDoses(child.childID)
    summaries.push({ child, nextDose: upcoming[0] ?? null })
  }
  ticketChildren.value = summaries
}

// =====================================================
// QR SCAN / TYPED CODE
// =====================================================

const qrError = ref('')
const typedCode = ref('')
const validating = ref(false)
const scanning = ref(false)
const validCode = ref('')     // today's code, once the clinic QR checked out
let scanner = null

// Browsers only show a live camera inside a page on a secure (https) site.
// On the clinic's local address (http://192.168.x.x) the same button opens the
// phone's camera to take a photo of the QR instead, and the code is read from
// the photo. (Scanning the QR with the camera app also works: its link opens
// this page with the code filled in.)
const liveCamera = ref(typeof window !== 'undefined' && window.isSecureContext && !!navigator.mediaDevices?.getUserMedia)
const photoInput = ref(null)
const readingPhoto = ref(false)

function scanQr() {
  qrError.value = ''
  if (liveCamera.value) startScanner()
  else photoInput.value?.click()
}

async function onQrPhoto(event) {
  const file = event.target.files?.[0]
  event.target.value = ''       // so the same photo can be chosen again
  if (!file) return
  readingPhoto.value = true
  try {
    const text = await readQrFromPhoto(file)
    if (text) validateCode(text)
    else qrError.value = 'No QR code was found in that photo. Hold the phone closer so the QR fills most of the picture, or type the code under it.'
  } catch (err) {
    console.error('QR photo error:', err)
    qrError.value = 'That photo couldn’t be read. Please try again, or type the code under the QR.'
  } finally {
    readingPhoto.value = false
  }
}

// Phone photos are 12+ megapixels: too big for a canvas on some iPhones and
// slow to search, so the QR is read from smaller copies (a few sizes, in case
// it is small in the picture).
async function readQrFromPhoto(file) {
  const image = await loadImage(file)
  const reader = new BrowserQRCodeReader()
  for (const size of [1200, 800, 2000]) {
    const scale = Math.min(1, size / Math.max(image.naturalWidth, image.naturalHeight))
    const canvas = document.createElement('canvas')
    canvas.width = Math.round(image.naturalWidth * scale)
    canvas.height = Math.round(image.naturalHeight * scale)
    canvas.getContext('2d').drawImage(image, 0, 0, canvas.width, canvas.height)
    try {
      return reader.decodeFromCanvas(canvas).getText()
    } catch { /* no QR found at this size */ }
  }
  return null
}

function loadImage(file) {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const image = new Image()
    image.onload = () => { URL.revokeObjectURL(url); resolve(image) }
    image.onerror = () => { URL.revokeObjectURL(url); reject(new Error('Not an image')) }
    image.src = url
  })
}

// The QR holds a link like …/ParentCheckin?code=XYZ; accept that or a bare code
function extractCode(text) {
  const raw = String(text || '').trim()
  const m = raw.match(/[?&]code=([^&#]+)/i)
  return (m ? decodeURIComponent(m[1]) : raw).replace(/[\s-]/g, '')
}

async function validateCode(raw) {
  const code = extractCode(raw)
  if (!code) return
  qrError.value = ''
  validating.value = true
  try {
    await api.get('/QueueQRCode/validate', { params: { code } })
    validCode.value = code
    openChildSelection()
  } catch (err) {
    qrError.value = err.response?.data?.message || 'Could not check the code. Please try again.'
  } finally {
    validating.value = false
  }
}

function useTypedCode() {
  validateCode(typedCode.value)
}

async function startScanner() {
  qrError.value = ''
  scanning.value = true
  await nextTick()
  try {
    scanner = new Html5Qrcode('checkin-qr-reader')
    await scanner.start(
      { facingMode: 'environment' },
      { fps: 10, qrbox: { width: 220, height: 220 } },
      async text => {
        await stopScanner()
        validateCode(text)
      },
      () => {} // "no QR in this frame" — ignore
    )
  } catch (err) {
    console.error('Camera error:', err)
    scanning.value = false
    scanner = null
    // Camera blocked or missing: the next tap takes a photo instead
    liveCamera.value = false
    qrError.value = 'The camera couldn’t be opened here. Tap the button again to take a photo of the QR instead, or type the code under it.'
  }
}

async function stopScanner() {
  try {
    if (scanner?.isScanning) await scanner.stop()
    scanner?.clear()
  } catch { /* already stopped */ }
  scanner = null
  scanning.value = false
}

// =====================================================
// CHILD SELECTION MODAL
// =====================================================

const showChildSelection = ref(false)
const selectedChildren = ref([])
const confirmLoading = ref(false)

function openChildSelection() {
  qrError.value = ''
  if (children.value.length === 0) {
    qrError.value = 'No children are linked to this account.'
    return
  }
  const due = children.value.filter(isChildDue)
  selectedChildren.value = due.length === 1 ? [due[0]] : []
  checkInError.value = ''
  showChildSelection.value = true
}

function toggleChildSelection(child) {
  if (!isChildDue(child)) return
  const index = selectedChildren.value.findIndex(c => c.childID === child.childID)

  if (index >= 0) {
    selectedChildren.value.splice(index, 1)
  } else {
    selectedChildren.value.push(child)
  }
}

function isChildSelected(child) {
  return selectedChildren.value.some(c => c.childID === child.childID)
}

function cancelChildSelection() {
  selectedChildren.value = []
  showChildSelection.value = false
}

// =====================================================
// VACCINATION (DOH schedule) — loaded for every linked
// child, to tell who has a dose due today.
// =====================================================

const childrenDueStatus = ref({})  // { childID: has a dose due today or earlier }
const childrenNextDose = ref({})   // { childID: next pending dose }
const childrenDueCount = ref({})   // { childID: number of doses due now }

function formatDisplayDate(date) {
  if (!date) return '—'
  try { return format(new Date(date), 'MMM d, yyyy') } catch { return '—' }
}

// A dose can be given once its date is reached: due today, or missed earlier
function isDoseDue(scheduledDate) {
  if (!scheduledDate) return false
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const scheduled = new Date(scheduledDate)
  scheduled.setHours(0, 0, 0, 0)
  return scheduled <= today
}

// Next due doses for a child, from the backend timeline (shared with the
// other parent pages via Composables/childSchedule.js).
async function fetchUpcomingDoses(childId) {
  if (!childId) return []
  try {
    const { timeline, records } = await fetchChildSchedule(childId)
    const doses = buildSchedule(timeline, records)
      .filter(d => !d.isCompleted)
      .sort((a, b) => a.scheduledDate - b.scheduledDate)

    childrenDueStatus.value[childId] = doses.length > 0 && isDoseDue(doses[0].scheduledDate)
    childrenNextDose.value[childId] = doses[0] ?? null
    childrenDueCount.value[childId] = doses.filter(d => isDoseDue(d.scheduledDate)).length

    return doses
  } catch (err) {
    console.error('fetchUpcomingDoses error:', err)
    // Schedule couldn't be loaded: don't lock the parent out; the nurse checks at the station
    childrenDueStatus.value[childId] = true
    childrenNextDose.value[childId] = null
    return []
  }
}

function isChildDue(child) {
  return childrenDueStatus.value[child.childID] === true
}

// Check-in opens when at least one child has a dose due today (or missed earlier)
const canScanQr = computed(() => children.value.some(isChildDue))

// Shown when nothing is due: the soonest upcoming dose across the children
const nextDueNote = computed(() => {
  const next = children.value
    .map(child => ({ child, dose: childrenNextDose.value[child.childID] }))
    .filter(x => x.dose)
    .sort((a, b) => a.dose.scheduledDate - b.dose.scheduledDate)[0]
  if (!next) return ''
  return `Next: ${next.child.firstName}'s ${next.dose.name} (Dose ${next.dose.doseNumber}) on ${formatDisplayDate(next.dose.scheduledDate)}.`
})

// =====================================================
// CONFIRM CHILDREN → POST /api/Queue with today's code
// =====================================================

const checkInError = ref('')

async function confirmChildren() {
  if (selectedChildren.value.length === 0) return

  confirmLoading.value = true
  checkInError.value = ''

  try {
    await api.post('/Queue', {
      ParentID: parentData.value.parentID,
      ChildIDs: selectedChildren.value.map(child => child.childID),
      QrCode: validCode.value || null,
    })
  } catch (err) {
    // 409 = already checked in today; just show the existing ticket
    if (err.response?.status !== 409) {
      console.error('Check-in failed:', err)
      checkInError.value = err.response?.data?.message || 'Could not check in — please try again.'
      confirmLoading.value = false
      return
    }
  }

  showChildSelection.value = false
  typedCode.value = ''
  confirmLoading.value = false
  // Drop ?code= from the address bar so a refresh doesn't re-submit it
  if (route.query.code) router.replace({ path: route.path })
  await loadMyStatus()
  await loadTicketChildren()
}

// =====================================================
// LIFECYCLE
// =====================================================

onMounted(async () => {
  const validSession = loadParentSession()
  if (!validSession) return

  await Promise.all([fetchChildren(), loadClinicInfo()])
  await loadMyStatus()
  fetchUnreadCount()

  // Opened by scanning the clinic QR with the phone camera
  if (!myStatus.value?.checkedIn && route.query.code && canScanQr.value && clinic.value?.checkInOpenNow !== false) {
    validateCode(String(route.query.code))
  }

  pollTimer = setInterval(() => {
    if (myStatus.value?.checkedIn && !isDone.value) loadMyStatus()
  }, 10000)
})

onBeforeUnmount(() => {
  clearInterval(pollTimer)
  stopScanner()
})
</script>
