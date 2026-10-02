<!--
  SuperAdminAccounts.vue — the Super Admin's "Admin Accounts" page.
  Admin / Doctor and Super Admin accounts: add, edit name and PRC license,
  reset the password, unlock after 5 wrong passwords, activate/deactivate.
  (The Doctor manages the Nurse accounts; the clinic manages parents.)
  Data: GET /api/accounts (this level's accounts only), POST
  /api/accounts/personnel, PUT /api/Users/{id}/admin, POST
  /api/accounts/{id}/reset-password|unlock, PATCH /api/accounts/{id}/status.
-->
<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900">
    <AppSidebar />

    <div class="flex-1 min-w-0 flex flex-col">
      <AppHeader title="Admin Accounts" breadcrumb="Super Admin / Admin Accounts" />

      <main class="p-6 space-y-5">
        <section class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h1 class="text-xl font-bold text-slate-900">Admin Accounts</h1>
            <p class="text-sm text-slate-500">Admin / Doctor and Super Admin accounts. Nurse accounts are managed by the Doctor.</p>
          </div>
          <button @click="openAdd" class="flex items-center gap-2 w-fit rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-700">
            <UserPlus class="w-4 h-4" /> Add Account
          </button>
        </section>

        <p v-if="message" class="rounded-xl border px-4 py-3 text-sm" :class="messageError ? 'border-rose-200 bg-rose-50 text-rose-700' : 'border-emerald-200 bg-emerald-50 text-emerald-800'">{{ message }}</p>

        <div class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th class="px-5 py-3 text-left font-semibold">Name</th>
                <th class="px-3 py-3 text-left font-semibold">Username</th>
                <th class="px-3 py-3 text-left font-semibold">Level</th>
                <th class="px-3 py-3 text-left font-semibold">Status</th>
                <th class="px-3 py-3 text-left font-semibold">Last Sign-in</th>
                <th class="px-5 py-3 text-right font-semibold">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="loading"><td colspan="6" class="px-5 py-8 text-center text-slate-400">Loading…</td></tr>
              <tr v-for="a in accounts" :key="a.accountID" class="border-t border-slate-100">
                <td class="px-5 py-3">
                  <p class="font-semibold text-slate-900">{{ fullName(a) }} <span v-if="a.referenceID === myId" class="text-xs font-normal text-slate-400">(you)</span></p>
                  <p class="text-xs text-slate-500">{{ a.prcNo ? `PRC ${a.prcNo}` : (a.email || '—') }}</p>
                </td>
                <td class="px-3 py-3 text-slate-600 whitespace-nowrap">{{ a.username }}</td>
                <td class="px-3 py-3">
                  <span class="text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap" :class="a.role === 'SuperAdmin' ? 'bg-slate-100 text-slate-700' : 'bg-violet-50 text-violet-700'">{{ userLevel(a.role) }}</span>
                </td>
                <td class="px-3 py-3 whitespace-nowrap">
                  <span class="text-xs font-semibold px-2.5 py-1 rounded-full" :class="a.status === 'Active' ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'">{{ a.status }}</span>
                  <span v-if="a.locked" class="ml-1 text-xs font-semibold px-2.5 py-1 rounded-full bg-rose-50 text-rose-700">Locked</span>
                  <span v-if="a.mustChangePassword" class="ml-1 text-[11px] text-amber-600">new password pending</span>
                </td>
                <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ a.lastLogin ? formatDateTime(a.lastLogin) : 'Never' }}</td>
                <td class="px-5 py-3">
                  <div class="flex flex-wrap justify-end gap-1.5">
                    <button @click="openEdit(a)" class="action">Edit</button>
                    <button @click="resetPassword(a)" class="action">Reset Password</button>
                    <button v-if="a.locked" @click="unlock(a)" class="action action-danger">Unlock</button>
                    <button v-if="a.referenceID !== myId" @click="setStatus(a, a.status !== 'Active')" class="action">{{ a.status === 'Active' ? 'Deactivate' : 'Activate' }}</button>
                  </div>
                </td>
              </tr>
              <tr v-if="!loading && !accounts.length"><td colspan="6" class="px-5 py-8 text-center text-slate-400">No accounts.</td></tr>
            </tbody>
          </table>
        </div>
      </main>
    </div>

    <!-- Add / Edit -->
    <div v-if="form.open" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4" @click.self="form.open = false">
      <div class="w-full max-w-lg rounded-2xl bg-white shadow-2xl">
        <div class="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 class="text-base font-bold">{{ form.editing ? 'Edit Account' : 'Add Account' }}</h2>
          <button @click="form.open = false" class="text-slate-400 hover:text-slate-600">✕</button>
        </div>
        <div class="grid grid-cols-1 gap-4 p-6 sm:grid-cols-2">
          <div v-if="!form.editing" class="sm:col-span-2">
            <label class="lbl">User Level</label>
            <select v-model="form.role" class="fld">
              <option value="Doctor">Admin / Doctor</option>
              <option value="SuperAdmin">Super Admin</option>
            </select>
          </div>
          <div><label class="lbl">First Name *</label><input v-model="form.firstName" class="fld" /></div>
          <div><label class="lbl">Middle Name</label><input v-model="form.middleName" class="fld" /></div>
          <div><label class="lbl">Last Name *</label><input v-model="form.lastName" class="fld" /></div>
          <div v-if="form.role === 'Doctor'"><label class="lbl">PRC License No.</label><input v-model="form.prcNo" class="fld" /></div>
          <div><label class="lbl">Email</label><input v-model="form.email" type="email" class="fld" /></div>
          <div><label class="lbl">Contact No.</label><input v-model="form.contactNo" v-ph-mobile type="tel" class="fld" placeholder="+63 9XX XXX XXXX" /></div>
          <p v-if="!form.editing" class="sm:col-span-2 text-xs text-slate-500">The username and a temporary password are made automatically. They choose their own password at the first sign-in.</p>
          <p v-if="form.error" class="sm:col-span-2 text-sm text-rose-600">{{ form.error }}</p>
        </div>
        <div class="flex justify-end gap-2 border-t border-slate-100 px-6 py-4">
          <button @click="form.open = false" class="rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50">Cancel</button>
          <button @click="save" :disabled="form.saving" class="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-50">{{ form.saving ? 'Saving…' : 'Save' }}</button>
        </div>
      </div>
    </div>

    <!-- Temporary password -->
    <div v-if="temp" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <div class="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl">
        <h2 class="text-base font-bold">{{ temp.title }}</h2>
        <p class="mt-1 text-sm text-slate-600">Give these to {{ temp.name }}. They will choose their own password when they sign in.</p>
        <div class="mt-4 space-y-2 rounded-xl bg-slate-50 p-4 font-mono text-sm">
          <p>Username: <strong>{{ temp.username }}</strong></p>
          <p>Temporary password: <strong>{{ temp.password }}</strong></p>
        </div>
        <p class="mt-2 text-xs text-slate-500">{{ temp.sent }}</p>
        <div class="mt-5 flex justify-end">
          <button @click="temp = null" class="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700">Done</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import axios from 'axios'
import { UserPlus } from 'lucide-vue-next'
import AppSidebar from '@/components/SystemAdmin/Components/AppSidebar.vue'
import AppHeader from '@/components/SystemAdmin/Components/AppHeader.vue'
import { API_BASE, userLevel } from '@/utils/format'
import { getUser } from '@/utils/auth'
import { askConfirm } from '@/utils/dialog'
import { phMobileError } from '@/utils/phone'

const accounts = ref([])
const loading = ref(false)
const message = ref('')
const messageError = ref(false)
const temp = ref(null)
const myId = getUser()?.UserID

const fullName = a => [a.firstName, a.middleName, a.lastName].filter(Boolean).join(' ')
const formatDateTime = v => new Date(v).toLocaleString('en-PH', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' })

function flash(text, isError = false) {
  message.value = text
  messageError.value = isError
  setTimeout(() => { if (message.value === text) message.value = '' }, 6000)
}

async function load() {
  loading.value = true
  try {
    const res = await axios.get(`${API_BASE}/accounts`)
    // Super Admins first, then Doctors, by name
    accounts.value = res.data.sort((a, b) =>
      (a.role === 'SuperAdmin' ? 0 : 1) - (b.role === 'SuperAdmin' ? 0 : 1) || fullName(a).localeCompare(fullName(b)))
  } catch (e) {
    flash(e.response?.data?.message || 'Could not load the accounts.', true)
  } finally {
    loading.value = false
  }
}

const sentNote = d => d.emailed || d.texted
  ? `Also sent by ${[d.emailed && 'email', d.texted && 'text'].filter(Boolean).join(' and ')}.`
  : 'Not sent by email or text (no email/number on file, or messaging is off), so give it in person.'

/* ── Add / Edit ───────────────────────────────────────── */
const form = reactive({ open: false, editing: null, saving: false, error: '', role: 'Doctor', firstName: '', middleName: '', lastName: '', prcNo: '', email: '', contactNo: '', address: null })

function openAdd() {
  Object.assign(form, { open: true, editing: null, saving: false, error: '', role: 'Doctor', firstName: '', middleName: '', lastName: '', prcNo: '', email: '', contactNo: '', address: null })
}
function openEdit(a) {
  Object.assign(form, {
    open: true, editing: a, saving: false, error: '', role: a.role === 'SuperAdmin' ? 'SuperAdmin' : 'Doctor',
    firstName: a.firstName || '', middleName: a.middleName || '', lastName: a.lastName || '',
    prcNo: a.prcNo || '', email: a.email || '', contactNo: a.contactNo || '', address: a.address ?? null,
  })
}

async function save() {
  form.error = ''
  if (!form.firstName.trim() || !form.lastName.trim()) { form.error = 'First and last name are required.'; return }
  const phoneError = phMobileError(form.contactNo)
  if (phoneError) { form.error = phoneError; return }
  form.saving = true
  try {
    if (form.editing) {
      await axios.put(`${API_BASE}/Users/${form.editing.referenceID}/admin`, {
        firstName: form.firstName, middleName: form.middleName || null, lastName: form.lastName,
        prcNo: form.role === 'Doctor' ? (form.prcNo || null) : null,
        email: form.email || null, contactNo: form.contactNo || null, address: form.address,
      })
      flash(`${form.firstName} ${form.lastName}'s account was updated.`)
    } else {
      const res = await axios.post(`${API_BASE}/accounts/personnel`, {
        role: form.role, firstName: form.firstName, middleName: form.middleName || null, lastName: form.lastName,
        licenseNumber: form.role === 'Doctor' ? (form.prcNo || null) : null,
        email: form.email || null, contactNo: form.contactNo || null,
      })
      const acc = res.data.account
      temp.value = {
        title: `${userLevel(form.role)} account created`, name: `${acc.firstName ?? form.firstName} ${acc.lastName ?? form.lastName}`,
        username: acc.username ?? acc.Username, password: res.data.temporaryPassword, sent: sentNote(res.data),
      }
    }
    form.open = false
    await load()
  } catch (e) {
    form.error = e.response?.data?.message || 'Could not save. Please try again.'
  } finally {
    form.saving = false
  }
}

/* ── Actions ──────────────────────────────────────────── */
async function resetPassword(a) {
  const ok = await askConfirm({
    title: `Reset the password for ${fullName(a)}?`,
    message: 'They get a temporary password and choose a new one when they next sign in. This also unlocks the account.',
    confirmText: 'Reset Password',
  })
  if (!ok) return
  try {
    const res = await axios.post(`${API_BASE}/accounts/${a.accountID}/reset-password`)
    temp.value = { title: 'Password reset', name: fullName(a), username: a.username, password: res.data.temporaryPassword, sent: sentNote(res.data) }
    await load()
  } catch (e) {
    flash(e.response?.data?.message || 'Could not reset the password.', true)
  }
}

async function unlock(a) {
  try {
    await axios.post(`${API_BASE}/accounts/${a.accountID}/unlock`)
    flash(`${fullName(a)} can sign in again.`)
    await load()
  } catch (e) {
    flash(e.response?.data?.message || 'Could not unlock the account.', true)
  }
}

async function setStatus(a, active) {
  const ok = await askConfirm(active
    ? { title: `Activate ${fullName(a)}?`, message: 'They will be able to sign in again.', confirmText: 'Activate' }
    : { title: `Deactivate ${fullName(a)}?`, message: "They won't be able to sign in until the account is activated again.", confirmText: 'Deactivate', tone: 'danger' })
  if (!ok) return
  try {
    await axios.patch(`${API_BASE}/accounts/${a.accountID}/status`, { status: active })
    flash(`${fullName(a)} is now ${active ? 'active' : 'inactive'}.`)
    await load()
  } catch (e) {
    flash(e.response?.data?.message || 'Could not change the status.', true)
  }
}

onMounted(load)
</script>

<style scoped>
.action {
  border: 1px solid #e2e8f0; border-radius: .5rem; padding: .375rem .625rem;
  font-size: .75rem; font-weight: 600; color: #475569; background: #fff;
}
.action:hover { background: #f8fafc; }
.action-danger { color: #be123c; border-color: #fecdd3; }
.lbl { display: block; margin-bottom: .375rem; font-size: .75rem; font-weight: 600; color: #64748b; }
.fld {
  width: 100%; border: 1px solid #e2e8f0; border-radius: .5rem; background: #f8fafc;
  padding: .625rem .75rem; font-size: .875rem;
}
.fld:focus { outline: none; background: #fff; box-shadow: 0 0 0 2px #10b981; }
</style>
