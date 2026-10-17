<!--
  Profilemodal.vue — the parent's "View Profile" window.
  Reworked Oct 2026 (survey: clearer, more user-friendly): readable text
  instead of tiny grey capitals, the parent's details as one list with icons,
  each child's details shown right away (no arrows to open), and Change
  Password in its own section at the bottom (its form used to open under the
  Children heading).
-->
<template>
  <Transition name="modal-fade" appear>
    <div class="fixed inset-0 z-300 flex items-center justify-center p-3 sm:p-4 bg-black/50 backdrop-blur-sm" @click.self="$emit('close')">
      <div class="relative w-full max-w-2xl bg-white rounded-3xl shadow-2xl overflow-hidden max-h-[92vh] flex flex-col"
        role="dialog" aria-modal="true" aria-labelledby="profile-title">
        <!-- Header -->
        <div class="bg-emerald-600 px-5 py-5 sm:px-7 sm:py-6 flex items-center gap-4 shrink-0">
          <div class="w-12 h-12 sm:w-14 sm:h-14 rounded-full bg-white/20 ring-2 ring-white/30 flex items-center justify-center text-white text-lg sm:text-xl font-bold shrink-0">
            {{ initials }}
          </div>
          <div class="flex-1 min-w-0">
            <p id="profile-title" class="text-white font-bold text-lg sm:text-xl leading-tight break-words">{{ fullName }}</p>
            <p class="text-emerald-50 text-[13px] mt-0.5">My profile · Parent / Guardian</p>
          </div>
          <button @click="$emit('close')" class="w-10 h-10 rounded-xl bg-white/10 hover:bg-white/25 flex items-center justify-center text-white transition-colors shrink-0" aria-label="Close">
            <X class="w-5 h-5" />
          </button>
        </div>

        <div class="overflow-y-auto flex-1 px-5 py-6 sm:px-7 space-y-7">
          <!-- MY DETAILS -->
          <section>
            <h3 class="text-[15px] font-bold text-slate-800 mb-3">My details</h3>
            <dl class="rounded-2xl border border-slate-200 divide-y divide-slate-100">
              <div v-for="row in detailRows" :key="row.label" class="flex items-start gap-3 px-4 py-3">
                <div class="w-9 h-9 rounded-xl bg-emerald-50 text-emerald-700 flex items-center justify-center shrink-0">
                  <component :is="row.icon" class="w-[18px] h-[18px]" />
                </div>
                <div class="min-w-0">
                  <dt class="text-[12px] text-slate-500">{{ row.label }}</dt>
                  <dd class="text-[14px] font-semibold text-slate-800 break-words">{{ row.value }}</dd>
                  <dd v-if="row.extra" class="text-[13px] text-slate-600">{{ row.extra }}</dd>
                </div>
              </div>
            </dl>
            <p class="mt-2.5 flex items-start gap-2 text-[12.5px] text-slate-500">
              <Info class="w-4 h-4 mt-0.5 shrink-0 text-slate-400" />
              Something wrong or changed? Ask the health center staff to update it on your next visit.
            </p>
          </section>

          <!-- MY CHILDREN -->
          <section>
            <h3 class="text-[15px] font-bold text-slate-800 mb-3">
              My children <span class="ml-1 text-[13px] font-semibold text-slate-400">({{ children.length }})</span>
            </h3>
            <div v-if="children.length === 0" class="rounded-2xl border border-dashed border-slate-200 py-8 text-center">
              <p class="text-2xl mb-1">👶</p>
              <p class="text-[14px] font-semibold text-slate-600">No children are linked to your account yet.</p>
              <p class="text-[12.5px] text-slate-500 mt-0.5">The health center staff can add them for you.</p>
            </div>
            <div v-else class="space-y-3">
              <div v-for="child in children" :key="child.childID" class="rounded-2xl border border-slate-200 p-4">
                <div class="flex items-center gap-3">
                  <div class="w-12 h-12 rounded-full bg-emerald-50 flex items-center justify-center text-2xl shrink-0">{{ child.sex === 'Female' ? '👧' : '👶' }}</div>
                  <div class="min-w-0">
                    <p class="text-[15px] font-bold text-slate-800 leading-tight">{{ child.firstName }} {{ child.lastName }}</p>
                    <p class="text-[13px] text-slate-500 mt-0.5">{{ ageLabel(child.birthDate) }} · {{ child.sex || '—' }} · Born {{ formatDisplayDate(child.birthDate) }}</p>
                  </div>
                </div>
                <dl class="mt-3 pt-3 border-t border-slate-100 grid grid-cols-1 sm:grid-cols-2 gap-x-5 gap-y-2.5">
                  <!-- How the signed-in person is related to this child (Mother, Grandmother, Uncle...) -->
                  <div>
                    <dt class="text-[12px] text-slate-500">You are {{ child.firstName }}'s</dt>
                    <dd class="text-[13.5px] font-semibold text-slate-800">{{ child.relationshipType || '—' }}</dd>
                  </div>
                  <div>
                    <dt class="text-[12px] text-slate-500">Parents / guardians on file</dt>
                    <dd class="text-[13.5px] font-semibold text-slate-800">{{ child.guardians ? child.guardians.split('; ').join(', ') : '—' }}</dd>
                  </div>
                  <div>
                    <dt class="text-[12px] text-slate-500">Place of birth</dt>
                    <dd class="text-[13.5px] font-semibold text-slate-800">{{ child.placeOfBirth || '—' }}</dd>
                  </div>
                  <div>
                    <dt class="text-[12px] text-slate-500">Health center</dt>
                    <dd class="text-[13.5px] font-semibold text-slate-800">{{ child.healthCenter || '—' }}<span v-if="child.barangay" class="font-normal text-slate-500"> · {{ barangayLabel(child.barangay) }}</span></dd>
                  </div>
                </dl>
              </div>
            </div>
          </section>

          <!-- PASSWORD -->
          <section>
            <h3 class="text-[15px] font-bold text-slate-800 mb-3">Password</h3>
            <div class="rounded-2xl border border-slate-200">
              <button @click="showChangePw = !showChangePw" :aria-expanded="showChangePw"
                class="w-full flex items-center gap-3 px-4 py-3.5 text-left hover:bg-slate-50 transition-colors" :class="showChangePw ? 'rounded-t-2xl' : 'rounded-2xl'">
                <div class="w-9 h-9 rounded-xl bg-emerald-50 text-emerald-700 flex items-center justify-center shrink-0">
                  <KeyRound class="w-[18px] h-[18px]" />
                </div>
                <div class="flex-1 min-w-0">
                  <p class="text-[14px] font-semibold text-slate-800">Change my password</p>
                  <p class="text-[12.5px] text-slate-500">You'll use the new one the next time you sign in.</p>
                </div>
                <ChevronDown class="w-5 h-5 text-slate-400 transition-transform shrink-0" :class="showChangePw ? 'rotate-180' : ''" />
              </button>

              <div v-if="showChangePw" class="border-t border-slate-100 px-4 py-4 space-y-3.5">
                <div>
                  <label for="pw-current" class="text-[13px] font-medium text-slate-700 block mb-1.5">Current password</label>
                  <input id="pw-current" v-model="pwForm.current" :type="showPw ? 'text' : 'password'" autocomplete="current-password"
                    placeholder="The password you sign in with now"
                    class="w-full px-4 py-3 text-[14px] bg-white border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:border-transparent transition-all" />
                </div>

                <div>
                  <label for="pw-new" class="text-[13px] font-medium text-slate-700 block mb-1.5">New password</label>
                  <input id="pw-new" v-model="pwForm.newPw" :type="showPw ? 'text' : 'password'" autocomplete="new-password"
                    placeholder="Create a strong password"
                    class="w-full px-4 py-3 text-[14px] bg-white border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:border-transparent transition-all" />
                  <!-- Live requirements checklist (always shown, so parents know the rules before typing) -->
                  <ul class="mt-2.5 grid grid-cols-1 sm:grid-cols-2 gap-y-1.5 gap-x-3 px-1">
                    <li v-for="req in pwRequirements" :key="req.label"
                      class="text-[12.5px] flex items-center gap-1.5"
                      :class="req.met ? 'text-emerald-700 font-medium' : 'text-slate-500'">
                      <CircleCheck v-if="req.met" class="w-4 h-4 shrink-0" />
                      <Circle v-else class="w-4 h-4 shrink-0 text-slate-300" />
                      {{ req.label }}
                    </li>
                  </ul>
                </div>

                <div>
                  <label for="pw-confirm" class="text-[13px] font-medium text-slate-700 block mb-1.5">Type the new password again</label>
                  <input id="pw-confirm" v-model="pwForm.confirm" :type="showPw ? 'text' : 'password'" autocomplete="new-password"
                    placeholder="Re-enter new password"
                    class="w-full px-4 py-3 text-[14px] bg-white border rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:border-transparent transition-all"
                    :class="pwForm.confirm && pwForm.confirm !== pwForm.newPw ? 'border-rose-300' : 'border-slate-200'" />
                  <p v-if="pwForm.confirm && pwForm.confirm !== pwForm.newPw" class="mt-1.5 text-[12.5px] text-rose-600">The two new passwords don't match yet.</p>
                </div>

                <label class="flex items-center gap-2 text-[13px] text-slate-600 cursor-pointer select-none w-fit">
                  <input v-model="showPw" type="checkbox" class="w-4 h-4 rounded border-slate-300 text-emerald-600 focus:ring-emerald-600" />
                  Show passwords
                </label>

                <p v-if="pwError" class="flex items-start gap-2 rounded-xl bg-rose-50 px-3 py-2.5 text-[13px] text-rose-700" role="alert">
                  <AlertCircle class="w-4 h-4 mt-0.5 shrink-0" /> {{ pwError }}
                </p>
                <p v-if="pwSuccess" class="flex items-start gap-2 rounded-xl bg-emerald-50 px-3 py-2.5 text-[13px] font-medium text-emerald-800" role="status">
                  <CircleCheck class="w-4 h-4 mt-0.5 shrink-0" /> {{ pwSuccess }}
                </p>

                <button @click="changePassword" :disabled="pwLoading || !canSubmitPw"
                  class="w-full py-3 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-xl text-[14px] font-semibold transition-colors">
                  {{ pwLoading ? 'Saving…' : 'Save new password' }}
                </button>
              </div>
            </div>
          </section>
        </div>

        <div class="shrink-0 px-5 py-4 sm:px-7 border-t border-slate-100 bg-slate-50 flex justify-end">
          <button @click="$emit('close')" class="w-full sm:w-auto bg-emerald-600 hover:bg-emerald-700 text-white px-8 py-2.5 rounded-xl text-[14px] font-semibold transition-colors">Close</button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { ageParts } from '@/utils/format'
import { barangayLabel } from '@/utils/barangays'
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, onMounted, onUnmounted } from 'vue'
import axios from 'axios'
import { format } from 'date-fns'
import { X, Mail, Phone, MapPin, Info, KeyRound, ChevronDown, CircleCheck, Circle, AlertCircle } from 'lucide-vue-next'

const API_BASE_URL = API_ORIGIN

// parentData and children are fetched/owned by whichever page renders this
// component (see HARD RULE 3) and passed down as props. The page controls
// visibility with v-if="showProfile", so this component just emits 'close'.
const props = defineProps({
  parentData: { type: Object, default: null },
  children: { type: Array, default: () => [] },
})

const emit = defineEmits(['close'])

// The page's copy of the parent's details is from sign-in; load the current
// ones so edits the clinic staff made since then show up here too.
const latest = ref(null)
const info = computed(() => ({ ...props.parentData, ...(latest.value || {}) }))

// Escape closes the window, like the X and Close buttons
const onKey = (e) => { if (e.key === 'Escape') emit('close') }
onMounted(async () => {
  window.addEventListener('keydown', onKey)
  if (!props.parentData?.parentID) return
  try {
    latest.value = (await axios.get(`${API_BASE_URL}/api/Parents/${props.parentData.parentID}`)).data
  } catch (e) {
    console.error('Profilemodal: could not load current details', e)
  }
})
onUnmounted(() => window.removeEventListener('keydown', onKey))

const fullName = computed(() =>
  [info.value?.firstName, info.value?.middleName, info.value?.lastName].filter(Boolean).join(' ') || 'My profile')
const initials = computed(() =>
  `${info.value?.firstName?.[0] || ''}${info.value?.lastName?.[0] || ''}`.toUpperCase() || '?')

const detailRows = computed(() => {
  const brgy = info.value?.barangayNo || info.value?.barangay
  return [
    { icon: Mail, label: 'Email', value: info.value?.email || '—' },
    { icon: Phone, label: 'Mobile number', value: info.value?.contactNo || info.value?.contactNumber || '—' },
    { icon: MapPin, label: 'Home address', value: info.value?.address || '—', extra: brgy ? barangayLabel(brgy) : '' },
  ]
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
const ageLabel = (birthDate) => {
  const age = calculateAge(birthDate)
  return age === '—' ? 'Age unknown' : `${age} old`
}

// ── Change Password ──────────────────────────────────────────────────────
const showChangePw = ref(false)
const showPw    = ref(false)
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
    { label: 'One special character (e.g. @ ! #)',  met: /[^A-Za-z0-9]/.test(pw) },
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
  if (!pwValid.value)        { pwError.value = 'The new password does not meet the requirements above.'; return }
  if (pwForm.value.newPw !== pwForm.value.confirm) { pwError.value = 'The two new passwords do not match.'; return }

  pwLoading.value = true
  try {
    const token = localStorage.getItem('authToken')
    await axios.post(`${API_BASE_URL}/api/auth/change-password`, {
      currentPassword: pwForm.value.current,
      newPassword:     pwForm.value.newPw,
    }, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    pwSuccess.value = 'Password changed. Use the new one the next time you sign in.'
    pwForm.value    = { current: '', newPw: '', confirm: '' }
    showPw.value    = false
    setTimeout(() => { pwSuccess.value = ''; showChangePw.value = false }, 3000)
  } catch (err) {
    pwError.value = err.response?.data?.message || 'Your current password is incorrect.'
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
