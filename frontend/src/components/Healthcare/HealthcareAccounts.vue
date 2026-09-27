<template>
  <div class="flex h-screen bg-slate-50 font-sans antialiased text-slate-900">

    <HealthcareSidebar />

    <!-- MAIN -->
    <div class="flex flex-col flex-1 overflow-hidden">
      <HealthcareHeader />

      <main class="flex-1 overflow-y-auto p-6">
        <div class="mb-6">
          <h1 class="text-2xl font-bold text-slate-800">My Account</h1>
          <p class="text-sm text-slate-500 mt-1">Manage your profile and settings</p>
        </div>

        <div class="grid grid-cols-3 gap-6">

          <!-- LEFT: Profile + Password -->
          <div class="col-span-2 space-y-5">

            <!-- Profile Info -->
            <div class="bg-white rounded-xl border border-slate-200 p-6">
              <h3 class="font-semibold text-slate-800 mb-1">Profile Information</h3>
              <p class="text-xs text-slate-400 mb-4">Name and license number can only be changed by the System Administrator.</p>
              <div v-if="profileLoadError" class="mb-4 text-xs text-red-500">{{ profileLoadError }}</div>
              <div class="grid grid-cols-2 gap-4">
                <!-- Read-only: Full Name -->
                <div>
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">
                    <User class="w-3 h-3 inline mr-1" />Full Name
                  </label>
                  <div class="w-full px-3 py-2.5 text-sm border border-slate-100 rounded-lg bg-slate-50 text-slate-700">
                    {{ doctor.fullName || '—' }}
                  </div>
                </div>
                <!-- Read-only: PRC No. -->
                <div>
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">
                    <BadgeCheck class="w-3 h-3 inline mr-1" />Professional License No.
                  </label>
                  <div class="w-full px-3 py-2.5 text-sm border border-slate-100 rounded-lg bg-slate-50 text-slate-700">
                    {{ doctor.prcNo || '—' }}
                  </div>
                </div>
                <!-- Editable: Email -->
                <div>
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">
                    <Mail class="w-3 h-3 inline mr-1" />Email Address
                  </label>
                  <input v-model="form.email" type="email"
                    class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <!-- Editable: Contact -->
                <div>
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">
                    <Phone class="w-3 h-3 inline mr-1" />Contact Number
                  </label>
                  <input v-model="form.contactNo" v-digits type="text"
                    class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <!-- Read-only: Role -->
                <div class="col-span-2">
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">Role</label>
                  <div class="w-full px-3 py-2.5 text-sm border border-slate-100 rounded-lg bg-slate-50 text-slate-700">
                    {{ doctor.userType || '—' }}
                  </div>
                </div>
              </div>
              <div class="mt-4 flex items-center gap-3">
                <button @click="saveProfile"
                  :disabled="savingProfile"
                  class="flex items-center gap-2 px-4 py-2.5 text-sm font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 disabled:opacity-50 transition-colors">
                  <Save class="w-4 h-4" />
                  {{ savingProfile ? 'Saving…' : 'Save Contact Info' }}
                </button>
                <p v-if="profileSaved" class="text-xs text-emerald-600 font-medium">✓ Changes saved</p>
                <p v-if="profileError" class="text-xs text-red-500">{{ profileError }}</p>
              </div>
            </div>

            <!-- Change Password -->
            <div class="bg-white rounded-xl border border-slate-200 p-6">
              <h3 class="font-semibold text-slate-800 mb-4">
                <Lock class="w-4 h-4 inline mr-1.5 text-slate-400" />Change Password
              </h3>
              <div class="space-y-4">
                <div>
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">Current Password</label>
                  <input v-model="pwForm.current" type="password" placeholder="Enter current password"
                    name="current-password-field" autocomplete="new-password" readonly
                    onfocus="this.removeAttribute('readonly')"
                    class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">New Password</label>
                  <input v-model="pwForm.newPw" type="password" placeholder="Enter new password"
                    name="new-password-field" autocomplete="new-password"
                    class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500" />

                  <ul v-if="pwForm.newPw" class="mt-2 space-y-1">
                    <li v-for="req in pwRequirements" :key="req.label"
                      class="flex items-center gap-1.5 text-xs"
                      :class="req.met ? 'text-emerald-600' : 'text-slate-400'">
                      <Check v-if="req.met" class="w-3.5 h-3.5 shrink-0" />
                      <X v-else class="w-3.5 h-3.5 shrink-0" />
                      {{ req.label }}
                    </li>
                  </ul>
                </div>
                <div>
                  <label class="text-xs font-medium text-slate-500 uppercase tracking-wide block mb-1.5">Confirm New Password</label>
                  <input v-model="pwForm.confirm" type="password" placeholder="Confirm new password"
                    name="confirm-password-field" autocomplete="new-password"
                    class="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <p v-if="pwError" class="text-xs text-red-500">{{ pwError }}</p>
                <p v-if="pwSuccess" class="text-xs text-emerald-600 font-medium">✓ Password updated</p>
              </div>
              <button @click="changePassword" :disabled="pwSaving || !pwRequirementsMet"
                class="mt-4 flex items-center gap-2 px-4 py-2.5 text-sm font-medium text-white bg-slate-700 rounded-lg hover:bg-slate-800 disabled:opacity-50 transition-colors">
                <Lock class="w-4 h-4" />{{ pwSaving ? 'Updating…' : 'Update Password' }}
              </button>
            </div>

          </div>

          <!-- RIGHT: Avatar card + Notification settings -->
          <div class="space-y-5">

            <!-- Avatar Card -->
            <div class="bg-white rounded-xl border border-slate-200 p-6 flex flex-col items-center text-center">
              <div class="w-20 h-20 rounded-full bg-emerald-700 flex items-center justify-center text-2xl font-bold text-white mb-3">
                {{ doctorInitials }}
              </div>
              <p class="font-bold text-slate-800">{{ doctor.fullName }}</p>
              <p class="text-sm text-slate-400 mt-0.5">{{ doctor.userType }}</p>
              <p v-if="doctor.prcNo" class="text-xs text-slate-400 mt-0.5">{{ doctor.prcNo }}</p>
            </div>

            <!-- Notifications (what Aruga actually sends) -->
            <div class="bg-white rounded-xl border border-slate-200 p-5">
              <h3 class="font-semibold text-slate-800 mb-3">
                <Bell class="w-4 h-4 inline mr-1.5 text-slate-400" />Notifications
              </h3>
              <p class="text-sm text-slate-600 leading-relaxed">
                Clinic notices appear in the bell at the top of the page.
              </p>
              <p class="text-[11px] text-slate-400 mt-2 leading-relaxed">
                Vaccination reminders and visit summaries go to parents by in-app notice, email and SMS. Health workers are not sent email or SMS.
              </p>
            </div>

          </div>
        </div>
      </main>
    </div>

  </div>
</template>

<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { getUser } from '@/utils/auth'
import HealthcareSidebar from './Components/HealthcareSidebar.vue'
import HealthcareHeader from './Components/HealthcareHeader.vue'
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import axios from 'axios'
import {
  Home, Users, Calendar, Syringe, FileText, Settings,
  Search, Bell, LogOut, Save, Lock, User, Mail, Phone, BadgeCheck, Check, X,
  ListChecks
} from 'lucide-vue-next'

const API = API_ORIGIN

const router = useRouter()
const route  = useRoute()

const doctor = ref({ userId: '', fullName: '', userType: '', prcNo: '' })
const form   = ref({ firstName: '', middleName: '', lastName: '', prcNo: '', email: '', contactNo: '', address: '' })
const pwForm = ref({ current: '', newPw: '', confirm: '' })
const pwError       = ref('')
const pwSaving      = ref(false)
const pwSuccess     = ref(false)
const savingProfile = ref(false)
const profileSaved  = ref(false)
const profileError  = ref('')
const profileLoadError = ref('')

// Live checklist shown under New Password. Also gates the submit
// button, and changePassword() re-checks the same rules server-side
// of the click (not just relying on the disabled attribute).
const pwRequirements = computed(() => {
  const pw = pwForm.value.newPw
  return [
    { label: 'At least 8 characters', met: pw.length >= 8 },
    { label: 'One uppercase letter',  met: /[A-Z]/.test(pw) },
    { label: 'One lowercase letter',  met: /[a-z]/.test(pw) },
    { label: 'One number',            met: /[0-9]/.test(pw) },
    { label: 'One special character', met: /[^A-Za-z0-9]/.test(pw) },
  ]
})
const pwRequirementsMet = computed(() => pwRequirements.value.every(r => r.met))

onMounted(() => {
  const u = getUser()
if (!u) { router.push('/'); return }
  doctor.value = {
    userId:   u.UserID,
    fullName: `${u.FirstName} ${u.LastName}`,
    userType: u.UserType && u.UserType !== 'Healthcare' ? u.UserType : 'Healthcare Worker',
    prcNo:    u.PRCNo || '',
  }
  form.value.firstName = u.FirstName || ''
  form.value.lastName  = u.LastName || ''
  form.value.prcNo     = doctor.value.prcNo
  loadProfile()
})

const doctorInitials = computed(() =>
  doctor.value.fullName.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
)

// ── Profile — GET / PUT /api/Users/{id} ─────────────────────
async function loadProfile() {
  profileLoadError.value = ''
  try {
    const { data } = await axios.get(`${API}/api/Users/${doctor.value.userId}`)
    form.value = {
      firstName:  data.firstName || '',
      middleName: data.middleName || '',
      lastName:   data.lastName || '',
      prcNo:      data.prcNo || '',
      email:      data.email || '',
      contactNo:  data.contactNo || '',
      address:    data.address || '',
    }
    if (data.position) doctor.value.userType = data.position
    // Name/PRC may have been changed by the administrator since login.
    doctor.value.fullName = `${data.firstName} ${data.lastName}`
    doctor.value.prcNo = data.prcNo || ''
  } catch (e) {
    console.error('loadProfile:', e)
    profileLoadError.value = 'Could not load your saved profile — showing the details from your login.'
  }
}

async function saveProfile() {
  profileError.value = ''
  savingProfile.value = true
  try {
    // Contact details only — name and PRC number are admin-managed.
    await axios.put(`${API}/api/Users/${doctor.value.userId}`, {
      email: form.value.email,
      contactNo: form.value.contactNo,
      address: form.value.address,
    })
    profileSaved.value = true
    setTimeout(() => profileSaved.value = false, 3000)
  } catch (e) {
    profileError.value = e.response?.data?.message || 'Could not save your profile. Please try again.'
  } finally {
    savingProfile.value = false
  }
}

async function changePassword() {
  pwError.value = ''
  pwSuccess.value = false
  if (!pwForm.value.current) { pwError.value = 'Please enter your current password.'; return }
  if (!pwRequirementsMet.value) { pwError.value = 'New password does not meet all requirements.'; return }
  if (pwForm.value.newPw !== pwForm.value.confirm) { pwError.value = 'Passwords do not match.'; return }

  pwSaving.value = true
  try {
    await axios.patch(`${API}/api/Users/${doctor.value.userId}/change-password`, {
      currentPassword: pwForm.value.current,
      newPassword: pwForm.value.newPw,
    })
    pwSuccess.value = true
    pwForm.value = { current: '', newPw: '', confirm: '' }
    setTimeout(() => pwSuccess.value = false, 3000)
  } catch (err) {
    pwError.value = err.response?.data?.message || 'Current password is incorrect, or the update failed.'
  } finally {
    pwSaving.value = false
  }
}

</script>

<style scoped>
</style>