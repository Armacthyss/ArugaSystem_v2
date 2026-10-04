<template>
  <Transition name="modal-fade" appear>
    <div class="fixed inset-0 z-300 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm" @click.self="$emit('close')">
      <div class="relative w-full max-w-2xl bg-white rounded-xl shadow-2xl overflow-hidden max-h-[90vh] flex flex-col">
        <div class="bg-emerald-600 px-8 py-7 flex items-center gap-5 shrink-0">
          <div class="w-16 h-16 rounded-xl bg-white/20 flex items-center justify-center text-4xl shadow-lg shrink-0">👤</div>
          <div class="flex-1 min-w-0">
            <p class="text-white font-bold text-xl leading-tight">
              {{ info?.firstName }} {{ info?.middleName ? info.middleName + ' ' : '' }}{{ info?.lastName }}
            </p>
            <p class="text-emerald-100 text-xs font-medium mt-1">Parent/Guardian Account · Aruga Pediatric Portal</p>
          </div>
          <button @click="$emit('close')" class="w-9 h-9 rounded-xl bg-white/10 hover:bg-white/25 flex items-center justify-center text-white text-lg transition-all shrink-0">✕</button>
        </div>
        <div class="overflow-y-auto flex-1 p-8 space-y-8">
          <div>
            <p class="text-[10px] font-semibold text-emerald-600 uppercase tracking-wide mb-4">Your Information</p>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="bg-slate-50 rounded-xl px-5 py-4"><p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Email Address</p><p class="text-sm font-bold text-slate-800 break-all">{{ info?.email || '—' }}</p></div>
              <div class="bg-slate-50 rounded-xl px-5 py-4"><p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Contact Number</p><p class="text-sm font-bold text-slate-800">{{ info?.contactNo || info?.contactNumber || '—' }}</p></div>
              <div class="bg-slate-50 rounded-xl px-5 py-4"><p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Barangay</p><p class="text-sm font-bold text-slate-800">{{ info?.barangayNo || info?.barangay || '—' }}</p></div>
              <div class="bg-slate-50 rounded-xl px-5 py-4"><p class="text-[10px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Address</p><p class="text-sm font-bold text-slate-800">{{ info?.address || '—' }}</p></div>
            </div>
          </div>

          <!-- SURVEY MODE ONLY: send this account's reminder to the tester's own email/phone -->
          <div v-if="surveyMode && children.length" class="rounded-xl border border-amber-300 bg-amber-50 p-5">
            <p class="text-sm font-bold text-amber-900">📨 Try the notifications on your own phone and email</p>
            <p class="text-[11px] text-amber-900/80 mt-1">
              Type your email and/or mobile number. You'll get {{ notifyChildName }}'s next vaccine reminder by
              <b>email</b>, <b>text message</b> and here in the <b>app (🔔)</b>. Your details are used once and are not saved.
            </p>
            <div class="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-3">
              <input v-model="notify.email" type="email" placeholder="Your email (optional)"
                class="w-full px-3 py-2.5 text-sm bg-white border border-amber-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-400" />
              <input v-model="notify.phone" type="tel" placeholder="Your mobile no., e.g. 0917 123 4567"
                class="w-full px-3 py-2.5 text-sm bg-white border border-amber-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-400" />
            </div>
            <select v-if="children.length > 1" v-model="notify.childId"
              class="mt-3 w-full px-3 py-2.5 text-sm bg-white border border-amber-200 rounded-lg">
              <option v-for="c in children" :key="c.childID" :value="c.childID">Reminder for {{ c.firstName }} {{ c.lastName }}</option>
            </select>
            <button @click="sendNotifyMe" :disabled="notify.loading || (!notify.email.trim() && !notify.phone.trim())"
              class="mt-3 w-full py-2.5 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg text-sm font-bold transition-colors">
              {{ notify.loading ? 'Sending…' : 'Send the reminder to me' }}
            </button>
            <p v-if="notify.error" class="text-xs text-red-600 font-bold mt-2">⚠ {{ notify.error }}</p>
            <ul v-if="notify.result" class="mt-2 text-xs font-bold space-y-0.5">
              <li class="text-emerald-700">✓ In the app: check the 🔔 bell</li>
              <li v-if="notify.result.email" :class="notify.result.email === 'sent' ? 'text-emerald-700' : 'text-red-600'">
                {{ notify.result.email === 'sent' ? '✓ Email sent: check your inbox (and Spam)' : '✗ Email: ' + notify.result.email }}
              </li>
              <li v-if="notify.result.sms" :class="notify.result.sms === 'sent' ? 'text-emerald-700' : 'text-red-600'">
                {{ notify.result.sms === 'sent' ? '✓ Text sent: it may take a minute to arrive' : '✗ Text: ' + notify.result.sms }}
              </li>
            </ul>
          </div>

          <!-- CHANGE PASSWORD -->
          <div>
            <button @click="showChangePw = !showChangePw"
              class="w-full flex items-center justify-between px-5 py-4 bg-slate-50 hover:bg-slate-100 rounded-xl transition-colors text-left group">
              <div class="flex items-center gap-3">
                <div class="w-9 h-9 rounded-xl bg-emerald-600/10 flex items-center justify-center text-lg">🔒</div>
                <div>
                  <p class="text-sm font-bold text-slate-800">Change Password</p>
                  <p class="text-[10px] text-slate-400">Update your account password</p>
                </div>
              </div>
              <span class="text-slate-400 text-xs font-bold transition-transform"
                :class="showChangePw ? 'rotate-180' : ''">▼</span>
            </button>
            <div>
              <div class="flex items-center justify-between mb-4">
                <p class="text-[10px] font-semibold text-emerald-600 uppercase tracking-wide">Children</p>
                <span class="text-[9px] font-black text-white bg-emerald-600 px-2.5 py-1 rounded-full">{{ children.length }} registered</span>
              </div>

              <div v-if="showChangePw" class="mt-3 space-y-3 px-1">
                <!-- Current Password -->
                <div>
                  <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">
                    Current Password
                  </label>
                  <input v-model="pwForm.current" type="password"
                    placeholder="Enter your current password"
                    class="w-full px-4 py-3 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:border-transparent transition-all" />
                </div>

                <!-- New Password -->
                <div>
                  <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">
                    New Password
                  </label>
                  <input v-model="pwForm.newPw" type="password"
                    placeholder="Create a strong password"
                    class="w-full px-4 py-3 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:border-transparent transition-all" />

                  <!-- Live requirements checklist -->
                  <div v-if="pwForm.newPw" class="mt-2.5 grid grid-cols-1 sm:grid-cols-2 gap-y-1.5 gap-x-3 px-1">
                    <p v-for="req in pwRequirements" :key="req.label"
                      class="text-[11px] font-medium flex items-center gap-1.5"
                      :class="req.met ? 'text-emerald-600' : 'text-slate-400'">
                      <span>{{ req.met ? '✓' : '○' }}</span>{{ req.label }}
                    </p>
                  </div>
                </div>

                <!-- Confirm Password -->
                <div>
                  <label class="text-xs font-medium text-slate-600 uppercase tracking-wide block mb-1.5">
                    Confirm New Password
                  </label>
                  <input v-model="pwForm.confirm" type="password"
                    placeholder="Re-enter new password"
                    class="w-full px-4 py-3 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:border-transparent transition-all" />
                </div>

                <!-- Error / Success -->
                <p v-if="pwError"   class="text-xs text-red-500 font-bold px-1">⚠ {{ pwError }}</p>
                <p v-if="pwSuccess" class="text-xs text-emerald-600 font-bold px-1">{{ pwSuccess }}</p>

                <!-- Submit -->
                <button @click="changePassword" :disabled="pwLoading || !canSubmitPw"
                  class="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg text-sm font-medium transition-colors flex items-center justify-center gap-2">
                  <span v-if="pwLoading">Updating…</span>
                  <span v-else>🔒 Update Password</span>
                </button>

                <p class="text-[9px] text-slate-400 text-center font-bold px-2">
                  After changing your password, you'll need to log in again next time.
                </p>
              </div>
            </div>
            <div v-if="children.length === 0" class="text-center py-8 text-slate-400"><p class="text-2xl mb-2">👶</p><p class="text-sm font-bold">No children registered yet.</p></div>
            <div v-else class="space-y-3">
              <div v-for="child in children" :key="child.childID" class="border border-slate-100 rounded-xl overflow-hidden">
                <button @click="toggleChildCard(child.childID)" class="w-full flex items-center gap-4 px-5 py-4 bg-slate-50 hover:bg-slate-100 transition-colors text-left">
                  <div class="w-10 h-10 rounded-xl bg-emerald-600/10 flex items-center justify-center text-xl shrink-0">{{ child.sex === 'Female' ? '👧' : '👶' }}</div>
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-bold text-slate-800 leading-tight">{{ child.firstName }} {{ child.lastName }}</p>
                    <p class="text-[10px] text-slate-400 mt-0.5">{{ formatDisplayDate(new Date(child.birthDate)) }} · {{ child.sex || '—' }}</p>
                  </div>
                  <span class="text-slate-400 text-xs font-bold">{{ expandedChildren.has(child.childID) ? '▲' : '▼' }}</span>
                </button>
                <div v-if="expandedChildren.has(child.childID)" class="px-5 py-4 grid grid-cols-1 sm:grid-cols-3 gap-3 bg-white border-t border-slate-50">
                  <!-- How the logged-in person is related to this child (Mother, Grandmother, Uncle...) -->
                  <div class="bg-slate-50 rounded-xl px-4 py-3"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">You Are {{ child.firstName }}'s</p><p class="text-sm font-bold text-slate-700">{{ child.relationshipType || '—' }}</p></div>
                  <div class="bg-slate-50 rounded-xl px-4 py-3 sm:col-span-2"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Parents / Guardians</p><p class="text-sm font-bold text-slate-700">{{ child.guardians ? child.guardians.split('; ').join(', ') : '—' }}</p></div>
                  <div class="bg-slate-50 rounded-xl px-4 py-3"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Place of Birth</p><p class="text-sm font-bold text-slate-700">{{ child.placeOfBirth || '—' }}</p></div>
                  <div class="bg-slate-50 rounded-xl px-4 py-3"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Health Center</p><p class="text-sm font-bold text-slate-700">{{ child.healthCenter || '—' }}</p></div>
                  <div class="bg-slate-50 rounded-xl px-4 py-3"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Barangay</p><p class="text-sm font-bold text-slate-700">{{ child.barangay || '—' }}</p></div>
                  <div class="bg-slate-50 rounded-xl px-4 py-3"><p class="text-[9px] font-semibold text-slate-400 uppercase tracking-wide mb-1">Age</p><p class="text-sm font-bold text-slate-700">{{ calculateAge(child.birthDate) }}</p></div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="shrink-0 px-8 py-5 border-t border-slate-100 bg-slate-50 flex items-center justify-between gap-4">
          <p class="text-[10px] text-slate-400 font-bold">To update your information, please contact the clinic staff.</p>
          <button @click="$emit('close')" class="bg-emerald-600 hover:bg-emerald-700 text-white px-6 py-2.5 rounded-lg text-sm font-medium transition-colors shrink-0">Close</button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { ageParts } from '@/utils/format'
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, onMounted } from 'vue'
import axios from 'axios'
import { format } from 'date-fns'

const API_BASE_URL = API_ORIGIN

// parentData and children are fetched/owned by whichever page renders this
// component (see HARD RULE 3) and passed down as props. The page controls
// visibility with v-if="showProfile", so this component just emits 'close'.
const props = defineProps({
  parentData: { type: Object, default: null },
  children: { type: Array, default: () => [] },
})

defineEmits(['close'])

// The page's copy of the parent's details is from sign-in; load the current
// ones so edits the clinic staff made since then show up here too.
const latest = ref(null)
const info = computed(() => ({ ...props.parentData, ...(latest.value || {}) }))
onMounted(async () => {
  if (!props.parentData?.parentID) return
  try {
    latest.value = (await axios.get(`${API_BASE_URL}/api/Parents/${props.parentData.parentID}`)).data
  } catch (e) {
    console.error('Profilemodal: could not load current details', e)
  }
})

function formatDisplayDate(date) {
  if (!date) return '—'
  try { return format(new Date(date), 'MMM d, yyyy') } catch { return '—' }
}

// Same logic as ParentOverview.vue's calculateAge, kept in sync so the
// displayed age format matches everywhere in the parent portal.
function calculateAge(birthDate) {
  // A month only counts once its day is reached (see ageParts in utils/format)
  const a = ageParts(birthDate)
  if (!a) return '—'
  const { years, months, days } = a
  const s = (n, w) => `${n} ${w}${n !== 1 ? 's' : ''}`
  if (years === 0 && months === 0) return s(days, 'day')
  if (years === 0) return s(months, 'month')
  if (months === 0) return s(years, 'year')
  return `${s(years, 'year')} ${s(months, 'month')}`
}

// ── Survey mode only: "Send the reminder to me" ─────────────────────────
// (POST /api/survey/notify-me; the box is hidden when survey mode is off)
const surveyMode = ref(false)
axios.get(`${API_BASE_URL}/api/auth/survey`).then(r => { surveyMode.value = !!r.data?.enabled }).catch(() => {})

const notify = ref({ email: '', phone: '', childId: props.children[0]?.childID ?? null, loading: false, error: '', result: null })
const notifyChildName = computed(() => {
  const c = props.children.find(x => x.childID === notify.value.childId) ?? props.children[0]
  return c ? c.firstName : 'your child'
})

async function sendNotifyMe() {
  const n = notify.value
  n.error = ''
  n.result = null
  n.loading = true
  try {
    const token = localStorage.getItem('authToken')
    n.result = (await axios.post(`${API_BASE_URL}/api/survey/notify-me`, {
      childId: n.childId ?? props.children[0]?.childID,
      email: n.email.trim() || null,
      phone: n.phone.trim() || null,
    }, { headers: token ? { Authorization: `Bearer ${token}` } : {} })).data
  } catch (err) {
    n.error = err.response?.data?.message || 'Could not send. Please try again.'
  } finally {
    n.loading = false
  }
}

// ── Children accordion ──────────────────────────────────────────────────
const expandedChildren = ref(new Set())

function toggleChildCard(childId) {
  const next = new Set(expandedChildren.value)
  next.has(childId) ? next.delete(childId) : next.add(childId)
  expandedChildren.value = next
}

// ── Change Password ──────────────────────────────────────────────────────
const showChangePw = ref(false)
const pwForm    = ref({ current: '', newPw: '', confirm: '' })
const pwError   = ref('')
const pwSuccess = ref('')
const pwLoading = ref(false)

// Password strength requirements — mirrored server-side in the
// change-password endpoint, since a client-side check alone can be bypassed.
const pwRequirements = computed(() => {
  const pw = pwForm.value.newPw
  return [
    { label: 'At least 8 characters',  met: pw.length >= 8 },
    { label: 'One uppercase letter',   met: /[A-Z]/.test(pw) },
    { label: 'One lowercase letter',   met: /[a-z]/.test(pw) },
    { label: 'One number',             met: /[0-9]/.test(pw) },
    { label: 'One special character',  met: /[^A-Za-z0-9]/.test(pw) },
  ]
})
const pwValid = computed(() => pwRequirements.value.every(r => r.met))
const canSubmitPw = computed(() =>
  !!pwForm.value.current && pwValid.value && pwForm.value.newPw === pwForm.value.confirm
)

async function changePassword() {
  pwError.value   = ''
  pwSuccess.value = ''
  if (!pwForm.value.current) { pwError.value = 'Please enter your current password.'; return }
  if (!pwValid.value)        { pwError.value = 'New password does not meet the requirements below.'; return }
  if (pwForm.value.newPw !== pwForm.value.confirm) { pwError.value = 'Passwords do not match.'; return }

  pwLoading.value = true
  try {
    const token = localStorage.getItem('authToken')
    await axios.post(`${API_BASE_URL}/api/auth/change-password`, {
      currentPassword: pwForm.value.current,
      newPassword:     pwForm.value.newPw,
    }, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    pwSuccess.value = '✓ Password changed successfully!'
    pwForm.value    = { current: '', newPw: '', confirm: '' }
    setTimeout(() => { pwSuccess.value = ''; showChangePw.value = false }, 2500)
  } catch (err) {
    pwError.value = err.response?.data?.message || 'Current password is incorrect.'
  } finally {
    pwLoading.value = false
  }
}
</script>

<style scoped>
.modal-fade-enter-active, .modal-fade-leave-active { transition: opacity 0.2s ease; }
.modal-fade-enter-from, .modal-fade-leave-to { opacity: 0; }
.modal-fade-enter-active > div, .modal-fade-leave-active > div { transition: transform 0.25s cubic-bezier(0.4,0,0.2,1); }
.modal-fade-enter-from > div, .modal-fade-leave-to > div { transform: scale(0.95); }
</style>