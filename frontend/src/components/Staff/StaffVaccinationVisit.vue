<!--
  StaffVaccinationVisit.vue — the vaccination visit screen (Staff / Nurse).
  Reached from the Dashboard's Vaccination Room card → "Start Vaccinating":
    /staff/vaccination/:queueId?child=<childID>

  Shows the child's doses that are due or overdue (from the vaccination
  timeline), lets the worker record one or several of them in the same
  visit, and shows the next dose date the backend's recalculation engine
  assigned. "Complete Visit" closes the queue entry.
-->
<template>
  <div class="flex h-screen bg-stone-50 antialiased text-slate-900" style="font-family: 'Inter','Segoe UI',sans-serif;">
    <StaffSidebar />

    <div class="flex flex-col flex-1 overflow-hidden">
      <StaffTopbar title="Vaccination Visit" breadcrumb="Aruga / Dashboard / Vaccination Room" />

      <main class="flex-1 overflow-y-auto p-6">
        <button @click="router.push('/staff/dashboard')"
          class="flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-800 mb-4 transition-colors">
          <ArrowLeft class="w-4 h-4" /> Back to Dashboard
        </button>

        <div v-if="loadError" class="flex items-start gap-2 p-4 bg-red-50 border border-red-100 rounded-xl text-sm text-red-700">
          <AlertCircle class="w-4 h-4 shrink-0 mt-0.5" />
          <span>{{ loadError }}</span>
        </div>

        <div v-else-if="loading" class="flex items-center justify-center py-20">
          <Loader2 class="w-5 h-5 animate-spin text-slate-400" />
          <span class="ml-2 text-sm text-slate-400">Loading patient...</span>
        </div>

        <!-- Family strip: every child in this visit and how far along they are -->
        <div v-if="!loadError && family.length > 1" class="mb-5 flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-3">
          <span class="text-xs font-semibold uppercase tracking-wide text-slate-500 mr-1">
            Child {{ currentIndex + 1 }} of {{ family.length }}
          </span>
          <button v-for="c in family" :key="c.childID" @click="goToChild(c)"
            class="flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors"
            :class="c.childID === childId
              ? 'border-emerald-600 bg-emerald-600 text-white'
              : progress[c.childID]?.state === 'done'
                ? 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'">
            <Check v-if="progress[c.childID]?.state === 'done'" class="w-3.5 h-3.5" />
            {{ c.name }}
            <span v-if="progress[c.childID]?.due" class="font-normal opacity-80">· {{ progress[c.childID].due }} due</span>
          </button>
        </div>

        <div v-if="!loadError && !loading && child" class="grid grid-cols-3 gap-6">

          <!-- LEFT: patient + doses -->
          <div class="col-span-2 space-y-5">

            <!-- Patient card -->
            <div class="bg-white rounded-xl border border-slate-200 p-5">
              <div class="flex items-center gap-4">
                <div class="w-14 h-14 rounded-full flex items-center justify-center text-lg font-bold text-white shrink-0"
                  :style="{ backgroundColor: avatarColor(child.name) }">
                  {{ initials(child.name) }}
                </div>
                <div class="flex-1 min-w-0">
                  <p class="text-lg font-bold text-slate-800">{{ child.name }}</p>
                  <p class="text-sm text-slate-500">
                    {{ child.sex || '—' }} · {{ child.ageLabel }} · Born {{ formatDate(child.birthDate) }}
                  </p>
                  <p v-if="visit?.requestBy" class="text-xs text-slate-500 mt-0.5">
                    Checked in by: <span class="font-medium text-slate-700">{{ withRelationship(visit.requestBy, visit.requestByRelationship) }}</span>
                  </p>
                  <p class="text-xs text-slate-400 mt-0.5">
                    Parents / Guardians: {{ child.guardians?.length ? child.guardians.map(g => withRelationship(g.name, g.relationship)).join(', ') : (child.parentName || '—') }}
                  </p>
                </div>
                <div class="text-right">
                  <p class="text-[10px] font-black text-slate-400 uppercase tracking-widest">Queue</p>
                  <p class="text-2xl font-black text-slate-800">#{{ queueNumber || '—' }}</p>
                </div>
              </div>

              <div v-if="child.allergies || child.existingConditions" class="mt-4 flex items-start gap-2 p-3 bg-red-50 border border-red-100 rounded-lg">
                <AlertTriangle class="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
                <div class="text-sm text-red-700 space-y-0.5">
                  <p v-if="child.allergies"><span class="font-semibold">Allergies:</span> {{ child.allergies }}</p>
                  <p v-if="child.existingConditions"><span class="font-semibold">Existing conditions:</span> {{ child.existingConditions }}</p>
                </div>
              </div>

              <div v-if="!inRoom" class="mt-4 flex items-start gap-2 p-3 bg-amber-50 border border-amber-100 rounded-lg">
                <AlertCircle class="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
                <p class="text-sm text-amber-800">{{ roomNotice }}</p>
              </div>
            </div>

            <!-- Due / overdue doses -->
            <div class="bg-white rounded-xl border border-slate-200">
              <div class="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
                <div>
                  <h2 class="font-semibold text-slate-800">Vaccines Due This Visit</h2>
                  <p class="text-xs text-slate-400 mt-0.5">Due today or overdue, from the child's vaccination schedule</p>
                </div>
                <button @click="openManual" :disabled="!inRoom"
                  class="disabled:opacity-40 disabled:cursor-not-allowed text-xs border border-slate-200 text-slate-600 hover:bg-slate-50 px-3 py-1.5 rounded-lg font-medium transition-colors">
                  + Other vaccine
                </button>
              </div>

              <div v-if="dueDoses.length === 0 && givenThisVisit.length === 0" class="px-5 py-10 text-center text-sm text-slate-400">
                No doses are due for this child today.
                <span v-if="nextUpcoming">Next: <strong class="text-slate-600">{{ nextUpcoming.vaccineName }} Dose {{ nextUpcoming.doseNumber }}</strong> on {{ formatDate(nextUpcoming.scheduledDate) }}.</span>
              </div>

              <div class="divide-y divide-slate-50">
                <!-- Recorded during this visit -->
                <div v-for="g in givenThisVisit" :key="'g' + g.key" class="px-5 py-3.5 flex items-center justify-between bg-emerald-50/40">
                  <div class="flex items-center gap-3">
                    <CheckCircle class="w-5 h-5 text-emerald-600 shrink-0" />
                    <div>
                      <p class="text-sm font-medium text-slate-800">{{ g.vaccineName }} — Dose {{ g.doseNumber }}</p>
                      <p class="text-xs text-slate-500">Batch {{ g.lotNumber }}<span v-if="g.site"> · {{ g.site }}</span><span v-if="g.nextLabel"> · Next dose: <strong class="text-emerald-700">{{ g.nextLabel }}</strong></span></p>
                    </div>
                  </div>
                  <span class="text-xs font-semibold text-emerald-700">Recorded</span>
                </div>

                <!-- Still to give -->
                <div v-for="d in dueDoses" :key="d.timelineID" class="px-5 py-3.5 flex items-center justify-between">
                  <div>
                    <p class="text-sm font-medium text-slate-800">{{ d.vaccineName }} — Dose {{ d.doseNumber }}</p>
                    <p class="text-xs" :class="isOverdue(d) ? 'text-red-500 font-medium' : 'text-slate-400'">
                      {{ isOverdue(d) ? `Overdue — was due ${formatDate(d.scheduledDate)}` : 'Due today' }}
                    </p>
                  </div>
                  <button @click="openRecord(d)" :disabled="!inRoom"
                    class="disabled:opacity-40 disabled:cursor-not-allowed text-xs bg-emerald-600 hover:bg-emerald-700 text-white px-3 py-1.5 rounded-lg font-medium transition-colors flex items-center gap-1.5">
                    <Syringe class="w-3.5 h-3.5" /> Record Dose
                  </button>
                </div>
              </div>
            </div>

            <!-- Visit actions: next child first; Complete Visit when every
                 child of the family is done (or on the last child) -->
            <div class="flex items-center justify-end gap-3 flex-wrap">
              <button @click="router.push('/staff/dashboard')"
                class="px-4 py-2.5 text-sm font-medium text-slate-600 border border-slate-200 rounded-lg hover:bg-slate-50 transition-colors">
                Save &amp; Return to Dashboard
              </button>
              <button v-if="nextChild" @click="completeVisit" :disabled="completing || !inRoom"
                class="px-4 py-2.5 text-sm font-medium text-slate-600 border border-slate-200 rounded-lg hover:bg-slate-50 disabled:opacity-50 transition-colors"
                title="Ends the visit for the whole family">
                Complete Visit
              </button>
              <button v-if="nextChild" @click="goToChild(nextChild)"
                class="px-5 py-2.5 text-sm font-semibold text-white bg-emerald-700 rounded-lg hover:bg-emerald-800 transition-colors flex items-center gap-2">
                Done with {{ firstName(child.name) }} <ArrowRight class="w-4 h-4" /> Next: {{ firstName(nextChild.name) }}
              </button>
              <button v-else @click="completeVisit" :disabled="completing || !inRoom"
                class="px-5 py-2.5 text-sm font-semibold text-white bg-slate-800 rounded-lg hover:bg-slate-900 disabled:opacity-50 transition-colors flex items-center gap-2">
                <Loader2 v-if="completing" class="w-4 h-4 animate-spin" />
                <Check v-else class="w-4 h-4" />
                Complete Visit
              </button>
            </div>
          </div>

          <!-- RIGHT: history -->
          <div class="bg-white rounded-xl border border-slate-200 p-5 h-fit">
            <h2 class="font-semibold text-slate-800 mb-1">Immunization History</h2>
            <p class="text-xs text-slate-400 mb-4">Check remarks for past adverse reactions</p>
            <div v-if="history.length === 0" class="text-sm text-slate-400 text-center py-6">No doses recorded yet</div>
            <div v-else class="space-y-3 max-h-[60vh] overflow-y-auto pr-1">
              <div v-for="h in history" :key="h.vaccinationRecordID" class="border-l-2 pl-3"
                :class="hasReaction(h) ? 'border-amber-400' : 'border-emerald-300'">
                <p class="text-sm font-medium text-slate-800">{{ h.vaccineName }} — Dose {{ h.doseNumber }}</p>
                <p class="text-xs text-slate-400">{{ formatDate(h.vaccinationDate) }}<span v-if="h.injectionSite"> · <span class="font-medium text-slate-600">{{ h.injectionSite }}</span></span><span v-if="h.administeredByName"> · {{ h.administeredByName }}</span></p>
                <p v-if="h.nurseObservation" class="text-xs mt-0.5" :class="hasReaction(h) ? 'text-amber-700' : 'text-slate-500'">{{ h.nurseObservation }}</p>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>

    <!-- RECORD DOSE MODAL -->
    <Transition name="fade">
      <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <div class="absolute inset-0 bg-black/40 backdrop-blur-sm" @click="closeModal"></div>
        <div class="relative bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 z-10">
          <div class="flex items-center justify-between mb-5">
            <div>
              <h3 class="text-lg font-bold text-slate-800">Record Vaccination</h3>
              <p class="text-sm text-slate-400 mt-0.5">{{ child?.name }}</p>
            </div>
            <button @click="closeModal" class="text-slate-400 hover:text-slate-600"><X class="w-5 h-5" /></button>
          </div>

          <div class="space-y-4">
            <template v-if="form.manual">
              <div>
                <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">Vaccine <span class="text-red-400">*</span></label>
                <select v-model="form.vaccineId" @change="onManualVaccineChange"
                  class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500">
                  <option value="" disabled>Select a vaccine...</option>
                  <option v-for="v in vaccineList" :key="v.vaccineID" :value="v.vaccineID">{{ v.vaccineName }}</option>
                </select>
              </div>
              <div>
                <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">Dose <span class="text-red-400">*</span></label>
                <select v-model="form.doseNumber"
                  class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500">
                  <option value="" disabled>Select dose...</option>
                  <option v-for="d in manualDoses" :key="d" :value="d">Dose {{ d }}</option>
                </select>
              </div>
            </template>
            <div v-else class="bg-slate-50 rounded-xl p-3">
              <p class="text-sm font-semibold text-slate-800">{{ form.vaccineName }} — Dose {{ form.doseNumber }}</p>
            </div>

            <div>
              <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">Date Administered <span class="text-red-400">*</span></label>
              <input type="date" v-model="form.date" :max="todayISO"
                class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500" />
            </div>

            <div>
              <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">Vaccine Batch <span class="text-red-400">*</span></label>
              <select v-model="form.inventoryId"
                class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500">
                <option value="" disabled>{{ loadingInventory ? 'Loading batches...' : 'Select a batch...' }}</option>
                <option v-for="inv in inventoryOptions" :key="inv.inventoryId" :value="inv.inventoryId">
                  {{ inv.lotNumber }} — exp {{ formatDate(inv.expirationDate) }} ({{ inv.currentQuantity }} left)
                </option>
              </select>
              <p v-if="!loadingInventory && form.vaccineId && inventoryOptions.length === 0" class="text-xs text-red-500 mt-1">
                No usable stock for this vaccine — the parent should be advised to return on another date.
              </p>
              <p v-else class="text-xs text-slate-400 mt-1">Earliest-expiring batch is selected first.</p>
            </div>

            <div>
              <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">Injection Site <span class="text-red-400">*</span></label>
              <div class="grid grid-cols-2 gap-2">
                <button v-for="s in siteChoices" :key="s" type="button" @click="form.site = s"
                  class="px-3 py-2 text-sm rounded-lg border transition-colors text-left"
                  :class="form.site === s ? 'border-emerald-500 bg-emerald-50 text-emerald-800 font-semibold' : 'border-slate-200 text-slate-600 hover:bg-slate-50'">
                  {{ s }}
                </button>
              </div>
              <p class="text-xs text-slate-400 mt-1">
                {{ previousSite || 'Where the dose was given, so any swelling can be traced to this vaccine.' }}
              </p>
            </div>

            <div>
              <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">Remarks / Adverse Reactions</label>
              <textarea v-model="form.remarks" rows="3" placeholder="e.g. No adverse reaction observed"
                class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"></textarea>
            </div>

            <p v-if="submitError" class="text-xs text-red-500">{{ submitError }}</p>
          </div>

          <div class="flex items-start gap-2 mt-4 p-3 bg-amber-50 rounded-lg border border-amber-100">
            <Info class="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
            <p class="text-xs text-amber-700">
              Saved under <strong>{{ workerName }}</strong> and visible to the child's parent. The next dose date is recalculated automatically from today's date.
            </p>
          </div>

          <div class="flex gap-3 mt-5">
            <button @click="closeModal" class="flex-1 px-4 py-2.5 text-sm font-medium text-slate-600 border border-slate-200 rounded-lg hover:bg-slate-50">Cancel</button>
            <button @click="submitDose" :disabled="!canSubmit || submitting"
              class="flex-1 px-4 py-2.5 text-sm font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2">
              <Loader2 v-if="submitting" class="w-4 h-4 animate-spin" />
              <CheckCircle v-else class="w-4 h-4" />
              {{ submitting ? 'Saving...' : 'Confirm & Save' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- TOAST -->
    <Transition name="fade">
      <div v-if="toast" class="fixed bottom-6 right-6 z-50 bg-emerald-600 text-white px-5 py-3.5 rounded-xl shadow-lg flex items-center gap-3 text-sm font-medium">
        <CheckCircle class="w-5 h-5" /> {{ toast }}
      </div>
    </Transition>
  </div>
</template>

<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import axios from 'axios'
import {
  ArrowLeft, ArrowRight, AlertCircle, AlertTriangle, Loader2, CheckCircle, Check, Info, Syringe, X,
} from 'lucide-vue-next'
import { getUser } from '@/utils/auth'
import StaffSidebar from './StaffSidebar.vue'
import StaffTopbar from './StaffTopbar.vue'
import { withRelationship, guardiansOf } from '@/utils/format'
import { visitProgress, unfinishedChildren } from '@/utils/visitProgress'
import { askConfirm } from '@/utils/dialog'
import { sitesForRoute, defaultSite } from '@/utils/injectionSites'

const router = useRouter()
const route  = useRoute()
const API = API_ORIGIN

const queueId = computed(() => route.params.queueId)
const childId = computed(() => route.query.child)

const user = getUser() || {}
const workerName = `${user.FirstName || ''} ${user.LastName || ''}`.trim()
const todayISO = toISODate(new Date())

const loading     = ref(true)
const loadError   = ref('')
const child       = ref(null)
const queueNumber = ref('')
const visit = ref(null)

// Doses can only be recorded while this family is in the vaccination room,
// i.e. after Call Next (the backend enforces the same rule).
const inRoom = computed(() => (visit.value?.status || '').toLowerCase().replace(/\s+/g, '') === 'inprogress')
const roomNotice = computed(() => {
  const v = visit.value
  if (!v) return 'This visit could not be found in today’s queue.'
  const s = (v.status || '').toLowerCase()
  if (s === 'completed') return 'This visit is already completed.'
  return 'This family hasn’t been called into the vaccination room yet. Press Call Next on the Dashboard first.'
})
const timeline    = ref([])
const history     = ref([])
const vaccineList = ref([])
const givenThisVisit = ref([])

// ── The family's other children ──────────────────────────────
// A parent can bring several children in one visit. The strip at the top
// shows each child's progress; "Done with … → Next" opens the next child,
// and Complete Visit (on the last child) warns about anyone left.
const family = computed(() => visit.value?.children || [])
const progress = ref({})
const currentIndex = computed(() => family.value.findIndex(c => c.childID === childId.value))
const nextChild = computed(() => {
  const list = family.value
  const i = currentIndex.value
  // the next child after this one who still has vaccines due, else the next in line
  const after = [...list.slice(i + 1), ...list.slice(0, Math.max(i, 0))]
  return after.find(c => progress.value[c.childID]?.state !== 'done') || null
})
async function refreshProgress() {
  progress.value = await visitProgress(family.value)
}
function goToChild(c) {
  if (c.childID !== childId.value) router.push(`/staff/vaccination/${queueId.value}?child=${c.childID}`)
}

// Same page, another child of the family: load that child
watch(childId, (id, old) => { if (id && id !== old) loadVisit() })

onMounted(loadVisit)

async function loadVisit() {
  if (!queueId.value || !childId.value) {
    loadError.value = 'Missing queue or patient reference — go back to the Dashboard and start vaccinating from the Vaccination Room card.'
    loading.value = false
    return
  }
  loading.value = true
  loadError.value = ''
  givenThisVisit.value = []
  try {
    const [childRes, queueRes] = await Promise.all([
      axios.get(`${API}/api/Children/${childId.value}`),
      axios.get(`${API}/api/Queue/${queueId.value}`).catch(() => null),
    ])
    const c = childRes.data
    child.value = {
      childID: c.childID,
      name: `${c.firstName} ${c.lastName}`.trim(),
      sex: c.sex,
      birthDate: c.birthDate,
      ageLabel: ageLabel(c.birthDate),
      parentName: c.parentName,
      guardians: guardiansOf(c.parents),
      allergies: c.allergies && !/^(none|n\/a|none known)$/i.test(c.allergies.trim()) ? c.allergies : null,
      existingConditions: c.existingConditions && !/^(none|n\/a|none known)$/i.test(c.existingConditions.trim()) ? c.existingConditions : null,
    }
    queueNumber.value = queueRes?.data?.queueNumber ?? ''
    visit.value = queueRes?.data ?? null
    await Promise.all([loadTimeline(), loadHistory(), vaccineList.value.length ? null : loadVaccines(), refreshProgress()])
  } catch (e) {
    console.error('load visit:', e)
    loadError.value = 'Could not load this patient. Go back to the Dashboard and try again.'
  } finally {
    loading.value = false
  }
}

async function loadTimeline() {
  const res = await axios.get(`${API}/api/VaccinationTimeline/child/${childId.value}`)
  timeline.value = res.data
}

async function loadHistory() {
  const res = await axios.get(`${API}/api/VaccinationRecords/child/${childId.value}`)
  history.value = [...res.data]
    .filter(r => (r.status ?? 'Completed') === 'Completed')
    .sort((a, b) => new Date(b.vaccinationDate) - new Date(a.vaccinationDate))
}

async function loadVaccines() {
  const res = await axios.get(`${API}/api/Vaccines/with-doses`)
  vaccineList.value = res.data
}

// Due today or earlier and not yet given.
const dueDoses = computed(() =>
  timeline.value
    .filter(t => (t.status === 'Pending' || t.status === 'Missed') && dateOnly(t.scheduledDate) <= todayISO)
    .sort((a, b) => new Date(a.scheduledDate) - new Date(b.scheduledDate))
)

const nextUpcoming = computed(() =>
  timeline.value
    .filter(t => t.status === 'Pending' && dateOnly(t.scheduledDate) > todayISO)
    .sort((a, b) => new Date(a.scheduledDate) - new Date(b.scheduledDate))[0] || null
)

function isOverdue(t) { return dateOnly(t.scheduledDate) < todayISO }

// ─────────────────────────────────────────────────────────────
// RECORD DOSE
// ─────────────────────────────────────────────────────────────
const showModal   = ref(false)
const submitting  = ref(false)
const submitError = ref('')
const inventoryOptions = ref([])
const loadingInventory = ref(false)
const form = ref({})

function blankForm() {
  return { manual: false, vaccineId: '', vaccineName: '', doseNumber: '', date: todayISO, inventoryId: '', remarks: '', site: '' }
}

// ── Injection site (City Hall: to trace a swelling to the vaccine) ──
const routeOf = (vaccineId) => vaccineList.value.find(v => v.vaccineID === Number(vaccineId))?.administrationRoute
const siteChoices = computed(() => sitesForRoute(routeOf(form.value.vaccineId)))
// Where the previous dose of this vaccine went, so the Nurse can alternate sides
const previousSite = computed(() => {
  const prev = history.value.find(h => h.vaccineID === Number(form.value.vaccineId) && h.doseNumber === Number(form.value.doseNumber) - 1)
  return prev?.injectionSite ? `Dose ${prev.doseNumber} was given in the ${prev.injectionSite.toLowerCase()}.` : ''
})

function openRecord(dose) {
  form.value = {
    ...blankForm(), vaccineId: dose.vaccineID, vaccineName: dose.vaccineName, doseNumber: dose.doseNumber,
    site: defaultSite(routeOf(dose.vaccineID)),
  }
  submitError.value = ''
  showModal.value = true
  fetchInventory(dose.vaccineID)
}

function openManual() {
  form.value = { ...blankForm(), manual: true }
  inventoryOptions.value = []
  submitError.value = ''
  showModal.value = true
}

function closeModal() { showModal.value = false }

const manualDoses = computed(() => {
  const v = vaccineList.value.find(v => v.vaccineID === form.value.vaccineId)
  const n = v?.doses?.length || v?.numberOfRequiredDoses || 1
  return Array.from({ length: n }, (_, i) => i + 1)
})

function onManualVaccineChange() {
  const v = vaccineList.value.find(v => v.vaccineID === form.value.vaccineId)
  form.value.vaccineName = v?.vaccineName || ''
  form.value.doseNumber = ''
  form.value.site = defaultSite(v?.administrationRoute)
  fetchInventory(form.value.vaccineId)
}

// Usable batches only (active, in stock, not expired), earliest expiry first.
async function fetchInventory(vaccineId) {
  form.value.inventoryId = ''
  inventoryOptions.value = []
  if (!vaccineId) return
  loadingInventory.value = true
  try {
    const res = await axios.get(`${API}/api/VaccineInventory/vaccine/${vaccineId}`)
    inventoryOptions.value = res.data
      .filter(i => i.status && i.currentQuantity > 0 && dateOnly(i.expirationDate) >= todayISO)
      .sort((a, b) => new Date(a.expirationDate) - new Date(b.expirationDate))
      .map(i => ({ inventoryId: i.inventoryID, lotNumber: i.lotNumber, expirationDate: i.expirationDate, currentQuantity: i.currentQuantity }))
    if (inventoryOptions.value.length) form.value.inventoryId = inventoryOptions.value[0].inventoryId
  } catch (e) {
    console.error('fetchInventory:', e)
  } finally {
    loadingInventory.value = false
  }
}

const canSubmit = computed(() =>
  form.value.vaccineId && form.value.doseNumber && form.value.date && form.value.inventoryId && form.value.site
)

async function submitDose() {
  if (!canSubmit.value) return
  submitting.value = true
  submitError.value = ''
  const f = form.value
  const batch = inventoryOptions.value.find(i => i.inventoryId === f.inventoryId)
  try {
    await axios.post(`${API}/api/VaccinationRecords`, {
      childID: childId.value,
      vaccineID: Number(f.vaccineId),
      doseNumber: Number(f.doseNumber),
      vaccinationDate: f.date,
      inventoryID: Number(f.inventoryId),
      administeredByUserID: user.UserID,   // the server uses the signed-in Nurse
      nurseObservation: f.remarks || null,
      injectionSite: f.site,
    })

    await Promise.all([loadTimeline(), loadHistory(), refreshProgress()])

    // Show the date the recalculation engine just assigned to the next dose.
    const next = timeline.value
      .filter(t => t.vaccineID === Number(f.vaccineId) && t.doseNumber > Number(f.doseNumber) && t.status === 'Pending')
      .sort((a, b) => a.doseNumber - b.doseNumber)[0]

    givenThisVisit.value.push({
      key: `${f.vaccineId}-${f.doseNumber}`,
      vaccineName: f.vaccineName,
      doseNumber: f.doseNumber,
      lotNumber: batch?.lotNumber || '—',
      site: f.site,
      nextLabel: next ? `Dose ${next.doseNumber} on ${formatDate(next.scheduledDate)}` : '',
    })

    showModal.value = false
    showToast(`${f.vaccineName} Dose ${f.doseNumber} recorded for ${child.value.name}`)
  } catch (e) {
    console.error('submitDose:', e)
    submitError.value = e.response?.data?.message || 'Failed to save. Please try again.'
  } finally {
    submitting.value = false
  }
}

// ─────────────────────────────────────────────────────────────
// COMPLETE VISIT — closes the queue entry (the whole family's visit)
// so the Nurse can call the next family.
// ─────────────────────────────────────────────────────────────
const completing = ref(false)

async function completeVisit() {
  // Everyone in the family who still has vaccines due today
  const left = [
    ...(dueDoses.value.length ? [`${child.value.name} (${dueDoses.value.length} vaccine${dueDoses.value.length === 1 ? '' : 's'})`] : []),
    ...unfinishedChildren(family.value, progress.value, childId.value),
  ]
  if (left.length && !(await askConfirm({
    title: 'Complete the visit anyway?',
    message: 'These are still due today and not recorded:',
    details: left,
    confirmText: 'Complete Anyway',
    cancelText: 'Go Back',
    tone: 'danger',
  }))) return
  completing.value = true
  try {
    await axios.patch(`${API}/api/Queue/${queueId.value}/complete`)
    showToast('Visit completed')
    setTimeout(() => router.push('/staff/dashboard'), 700)
  } catch (e) {
    console.error('completeVisit:', e)
    showToast('Could not complete the visit. Please try again.')
  } finally {
    completing.value = false
  }
}

// ─────────────────────────────────────────────────────────────
// HELPERS
// ─────────────────────────────────────────────────────────────
const toast = ref('')
function showToast(msg) {
  toast.value = msg
  setTimeout(() => { toast.value = '' }, 3500)
}

const firstName = (name) => (name || '').split(' ')[0]

function hasReaction(h) {
  const t = (h.nurseObservation || '').toLowerCase()
  return !!t && !/no (adverse )?reaction/.test(t)
}

function toISODate(d) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
function dateOnly(value) { return value ? String(value).slice(0, 10) : '' }

function formatDate(value) {
  if (!value) return '—'
  return new Date(value).toLocaleDateString('en-PH', { month: 'short', day: 'numeric', year: 'numeric' })
}

function ageLabel(birth) {
  if (!birth) return '—'
  const b = new Date(birth), now = new Date()
  const days = Math.floor((now - b) / 86400000)
  if (days < 31) return `${days} day${days === 1 ? '' : 's'} old`
  let months = (now.getFullYear() - b.getFullYear()) * 12 + (now.getMonth() - b.getMonth())
  if (now.getDate() < b.getDate()) months--
  if (months < 24) return `${months} month${months === 1 ? '' : 's'}`
  const years = Math.floor(months / 12)
  return `${years} yr${years === 1 ? '' : 's'}`
}

const AVATAR_COLORS = ['#4a7c59','#6b7c45','#8b5e3c','#4a6fa5','#7b4f8e','#5a7a6b','#8b6914']
function avatarColor(name) { return name ? AVATAR_COLORS[name.charCodeAt(0) % AVATAR_COLORS.length] : AVATAR_COLORS[0] }
function initials(name) { return name ? name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2) : '?' }
</script>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
