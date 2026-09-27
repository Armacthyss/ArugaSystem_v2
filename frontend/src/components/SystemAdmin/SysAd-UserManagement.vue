<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, reactive, onMounted } from 'vue'
import { useFloatingMenu } from '@/utils/floatingMenu'
import axios from 'axios'
import AppSidebar from './Components/AppSidebar.vue'
import AppHeader from './Components/AppHeader.vue'

const API_BASE_URL = `${API_ORIGIN}/api`

/* ----------------------------- Layout state (page-level) ------------------------------ */
// Sidebar owns its own collapse state internally now. This page only needs
// to know/track which nav item is active — pass this to AppSidebar as a
// v-model. Swap this for vue-router's current route once that's wired up.
const activeNav = ref('User Management')
function handleLogout() {
  // Hook up real logout / redirect logic here.
  console.log('logout clicked')
}

/* -------------------------------- Role meta -------------------------------- */
// Matches the backend's actual roles. "Parent" comes from AccountType = Parent,
// the rest come from Users.UserType for AccountType = Personnel.
const roleMeta = {
  Parent: { tint: 'bg-teal-50', text: 'text-teal-700' },
  Doctor: { tint: 'bg-emerald-50', text: 'text-emerald-700' },
  Nurse: { tint: 'bg-sky-50', text: 'text-sky-700' },
  Staff: { tint: 'bg-amber-50', text: 'text-amber-700' },
  Administrator: { tint: 'bg-violet-50', text: 'text-violet-700' },
}

const statusMeta = {
  Active: { tint: 'bg-emerald-50', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  Inactive: { tint: 'bg-slate-100', text: 'text-slate-600', dot: 'bg-slate-400' },
}

/* -------------------------------- User data -------------------------------- */
const users = ref([])
const isLoadingUsers = ref(false)
const loadError = ref('')

function formatDate(value) {
  if (!value) return 'Never'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return 'Never'
  return d.toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: 'numeric', minute: '2-digit'
  })
}

function mapAccount(a) {
  return {
    id: a.accountID,
    referenceID: a.referenceID,
    firstName: a.firstName || '',
    lastName: a.lastName || '',
    username: a.username || '',
    email: a.email || '—',
    contact: a.contactNo || '—',
    role: a.role,
    status: a.status, // "Active" | "Inactive" from the API
    mustChangePassword: !!a.mustChangePassword,
    lastLogin: formatDate(a.lastLogin),
    created: formatDate(a.createdAt),
  }
}

async function fetchUsers() {
  isLoadingUsers.value = true
  loadError.value = ''

  try {
    const response = await axios.get(`${API_BASE_URL}/accounts`)
    users.value = response.data.map(mapAccount)
  } catch (error) {
    console.error('Failed to load accounts:', error)
    loadError.value = 'Could not load users. Is the API running?'
  } finally {
    isLoadingUsers.value = false
  }
}

onMounted(fetchUsers)

/* ---------------------------- Toolbar / filters ---------------------------- */
const searchQuery = ref('')
const roleFilter = ref('All Users')
const statusFilter = ref('All')

const filteredUsers = computed(() =>
  users.value.filter((u) => {
    const q = searchQuery.value.trim().toLowerCase()
    const matchesSearch =
      !q ||
      `${u.firstName} ${u.lastName}`.toLowerCase().includes(q) ||
      u.username.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q)
    const matchesRole = roleFilter.value === 'All Users' || u.role === roleFilter.value
    const matchesStatus = statusFilter.value === 'All' || u.status === statusFilter.value
    return matchesSearch && matchesRole && matchesStatus
  })
)

/* -------------------------------- Summary ---------------------------------- */
const summary = computed(() => ({
  total: users.value.length,
  active: users.value.filter((u) => u.status === 'Active').length,
  inactive: users.value.filter((u) => u.status === 'Inactive').length,
  pendingPasswordChange: users.value.filter((u) => u.mustChangePassword).length,
}))

const initials = (u) => `${u.firstName[0] ?? ''}${u.lastName[0] ?? ''}`.toUpperCase()

/* ------------------------------ Row actions menu ---------------------------- */
const { openMenuId, menuStyle, toggleMenu, closeMenu } = useFloatingMenu()

const actionError = ref('')

async function setStatus(user, status) {
  closeMenu()
  actionError.value = ''

  const confirmed = window.confirm(
    status === 'Active'
      ? `Activate ${user.firstName} ${user.lastName}?`
      : `Deactivate ${user.firstName} ${user.lastName}? They won't be able to log in.`
  )
  if (!confirmed) return

  try {
    await axios.patch(`${API_BASE_URL}/accounts/${user.id}/status`, {
      status: status === 'Active'
    })
    user.status = status
  } catch (error) {
    console.error('Failed to update status:', error)
    actionError.value = 'Could not update this user\u2019s status. Please try again.'
  }
}

async function resetPassword(user) {
  closeMenu()
  actionError.value = ''

  const confirmed = window.confirm(
    `Reset the password for ${user.firstName} ${user.lastName}? They'll need to set a new one on next login.`
  )
  if (!confirmed) return

  try {
    const response = await axios.post(`${API_BASE_URL}/accounts/${user.id}/reset-password`)
    user.mustChangePassword = true
    tempPasswordResult.value = {
      name: `${user.firstName} ${user.lastName}`,
      username: user.username,
      password: response.data.temporaryPassword,
      emailed: !!response.data.emailed,
      texted: !!response.data.texted,
    }
    showTempPasswordModal.value = true
  } catch (error) {
    console.error('Failed to reset password:', error)
    actionError.value = 'Could not reset this user\u2019s password. Please try again.'
  }
}

/* -------------------------------- Edit staff profile ---------------------------- */
// Name and PRC license number are admin-only (healthcare workers can only
// change their contact details on My Account). PUT /api/Users/{id}/admin
const showEditModal = ref(false)
const savingEdit = ref(false)
const editError = ref('')
const editForm = reactive({
  userID: null, role: '', firstName: '', middleName: '', lastName: '',
  prcNo: '', email: '', contactNo: '', address: '',
})

async function openEditModal(user) {
  closeMenu()
  editError.value = ''
  Object.assign(editForm, {
    userID: user.referenceID, role: user.role,
    firstName: user.firstName, middleName: '', lastName: user.lastName,
    prcNo: '', email: user.email === '—' ? '' : user.email,
    contactNo: user.contact === '—' ? '' : user.contact, address: '',
  })
  showEditModal.value = true
  try {
    const { data } = await axios.get(`${API_BASE_URL}/Users/${user.referenceID}`)
    Object.assign(editForm, {
      firstName: data.firstName || '', middleName: data.middleName || '', lastName: data.lastName || '',
      prcNo: data.prcNo || '', email: data.email || '', contactNo: data.contactNo || '', address: data.address || '',
    })
  } catch (error) {
    console.error('Failed to load profile:', error)
    editError.value = 'Could not load the full profile — some fields may be blank.'
  }
}

async function saveEdit() {
  editError.value = ''
  if (!editForm.firstName.trim() || !editForm.lastName.trim()) {
    editError.value = 'First and last name are required.'
    return
  }
  savingEdit.value = true
  try {
    const { data } = await axios.put(`${API_BASE_URL}/Users/${editForm.userID}/admin`, {
      firstName: editForm.firstName, middleName: editForm.middleName, lastName: editForm.lastName,
      prcNo: editForm.prcNo, email: editForm.email, contactNo: editForm.contactNo, address: editForm.address,
    })
    const row = users.value.find(u => u.referenceID === editForm.userID)
    if (row) {
      row.firstName = data.firstName
      row.lastName = data.lastName
      row.email = data.email || '—'
      row.contact = data.contactNo || '—'
    }
    showEditModal.value = false
  } catch (error) {
    editError.value = error.response?.data?.message || 'Could not save changes. Please try again.'
  } finally {
    savingEdit.value = false
  }
}

/* -------------------------------- Details drawer ---------------------------- */
const showDrawer = ref(false)
const selectedUser = ref(null)
const openDrawer = (user) => {
  selectedUser.value = user
  showDrawer.value = true
  closeMenu()
}
const closeDrawer = () => (showDrawer.value = false)

/* ------------------------- Temporary password modal ------------------------- */
const showTempPasswordModal = ref(false)
const tempPasswordResult = ref(null) // { name, password }
const copyStatus = ref('')

async function copyTempPassword() {
  if (!tempPasswordResult.value) return
  try {
    await navigator.clipboard.writeText(tempPasswordResult.value.password)
    copyStatus.value = 'Copied!'
    setTimeout(() => (copyStatus.value = ''), 1500)
  } catch {
    copyStatus.value = 'Could not copy — please select it manually.'
  }
}

function closeTempPasswordModal() {
  showTempPasswordModal.value = false
  tempPasswordResult.value = null
  copyStatus.value = ''
}

/* --------------------------------- Add modal --------------------------------- */
const showAddModal = ref(false)
const isCreating = ref(false)
const createError = ref('')

const addForm = reactive({
  role: 'Parent',
  firstName: '',
  middleName: '',
  lastName: '',
  licenseNumber: '',
  email: '',
  contactNo: '',
  address: '',
  barangayNo: '',
})

const isPersonnelRole = computed(() => addForm.role !== 'Parent')
const showLicenseField = computed(() => addForm.role === 'Doctor' || addForm.role === 'Nurse')

const openAddModal = () => {
 Object.assign(addForm, {
  role: 'Parent',
  firstName: '',
  middleName: '',
  lastName: '',
  licenseNumber: '',
  email: '',
  contactNo: '',
  address: '',
  barangayNo: '',
})
  createError.value = ''
  showAddModal.value = true
}

const canCreate = computed(() => {
  if (!addForm.firstName.trim() || !addForm.lastName.trim()) return false
  if (!addForm.email.trim() || !addForm.contactNo.trim()) return false
  return true
})

async function createUser() {
  createError.value = ''

  if (!canCreate.value) {
    createError.value = 'Please fill in all required fields.'
    return
  }

  isCreating.value = true

  try {
   let temporaryPassword = ''
let generatedUsername = ''
let createdName = `${addForm.firstName} ${addForm.lastName}`
let emailed = false
let texted = false

    if (addForm.role === 'Parent') {
      const response = await axios.post(`${API_BASE_URL}/Parents`, {
        firstName: addForm.firstName,
        middleName: addForm.middleName || null,
        lastName: addForm.lastName,
        email: addForm.email,
        contactNo: addForm.contactNo,
        barangayNo: addForm.barangayNo || null,
        address: addForm.address || null,
      })
      temporaryPassword = response.data.temporaryPassword
      generatedUsername = addForm.email
      emailed = !!response.data.emailed
      texted = !!response.data.texted
    } else {
      const response = await axios.post(`${API_BASE_URL}/accounts/personnel`, {
  firstName: addForm.firstName,
  middleName: addForm.middleName || null,
  lastName: addForm.lastName,
  role: addForm.role,
  licenseNumber: showLicenseField.value ? (addForm.licenseNumber || null) : null,
  email: addForm.email,
  contactNo: addForm.contactNo,
  address: addForm.address || null,
})

generatedUsername = response.data.account?.username || ''
temporaryPassword = response.data.temporaryPassword
emailed = !!response.data.emailed
texted = !!response.data.texted
    }

    showAddModal.value = false
    await fetchUsers()

    tempPasswordResult.value = {
  name: createdName,
  username: generatedUsername,
  password: temporaryPassword,
  emailed,
  texted,
}
    showTempPasswordModal.value = true

  } catch (error) {
    console.error('Failed to create user:', error)
    if (error.response?.status === 409) {
      createError.value = error.response.data?.message || 'That username or email is already in use.'
    } else if (error.response?.status === 400) {
      createError.value = error.response.data?.message || 'Please check the form and try again.'
    } else {
      createError.value = 'Could not create this account. Please try again.'
    }
  } finally {
    isCreating.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900" @click="closeMenu">
    <!-- ============================ SIDEBAR (shared component) ============================ -->
    <AppSidebar v-model:active-nav="activeNav" @logout="handleLogout" />

    <!-- ============================ MAIN ============================ -->
    <div class="flex-1 min-w-0 flex flex-col">
      <AppHeader title="User Management" breadcrumb="Dashboard / User Management" />

      <!-- Content -->
      <main class="p-6 space-y-6">
        <!-- Load error -->
        <div v-if="loadError" class="rounded-xl bg-red-50 border border-red-200 px-4 py-3 flex items-center justify-between">
          <p class="text-red-600 text-sm">{{ loadError }}</p>
          <button @click="fetchUsers" class="text-sm font-semibold text-red-700 hover:underline shrink-0">Retry</button>
        </div>

        <!-- Action error (status/reset) -->
        <div v-if="actionError" class="rounded-xl bg-red-50 border border-red-200 px-4 py-3">
          <p class="text-red-600 text-sm">{{ actionError }}</p>
        </div>

        <!-- Summary cards -->
        <section class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Total Users</p>
              <div class="bg-teal-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">👥</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.total }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Active Users</p>
              <div class="bg-emerald-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">✅</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.active }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Inactive Users</p>
              <div class="bg-slate-100 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">💤</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.inactive }}</p>
          </div>
          <div class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Awaiting Password Change</p>
              <div class="bg-amber-50 w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">🔑</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ summary.pendingPasswordChange }}</p>
          </div>
        </section>

        <!-- Toolbar -->
        <section class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="flex flex-col lg:flex-row lg:items-center gap-3">
            <div class="relative flex-1 min-w-0">
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-sm" aria-hidden="true">🔍</span>
              <input
                v-model="searchQuery"
                type="text"
                placeholder="Search by name, username, or email..."
                class="w-full pl-9 pr-3 py-2 text-sm rounded-lg border border-slate-200 bg-slate-50 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
              />
            </div>

            <select
              v-model="roleFilter"
              class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option>All Users</option>
              <option>Parent</option>
              <option>Doctor</option>
              <option>Nurse</option>
              <option>Staff</option>
              <option>Administrator</option>
            </select>

            <select
              v-model="statusFilter"
              class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors"
            >
              <option>All</option>
              <option>Active</option>
              <option>Inactive</option>
            </select>

            <div class="flex items-center gap-2 shrink-0">
              <button @click="fetchUsers" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500">
                Refresh
              </button>
              <button
                @click="openAddModal"
                class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                + Add User
              </button>
            </div>
          </div>
        </section>

        <!-- User table -->
        <section class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-200 bg-slate-50/60">
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Profile</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Full Name</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Username</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Role</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Status</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Last Login</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Created Date</th>
                  <th class="text-right font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="isLoadingUsers">
                  <td colspan="8" class="px-5 py-12 text-center text-sm text-slate-400">Loading users…</td>
                </tr>

                <tr
                  v-for="user in filteredUsers"
                  v-else
                  :key="user.id"
                  class="border-b border-slate-100 last:border-0 hover:bg-slate-50 transition-colors"
                >
                  <td class="px-5 py-3">
                    <div class="w-9 h-9 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-xs font-bold">
                      {{ initials(user) }}
                    </div>
                  </td>
                  <td class="px-3 py-3 font-semibold text-slate-900 whitespace-nowrap">{{ user.firstName }} {{ user.lastName }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ user.username || '—' }}</td>
                  <td class="px-3 py-3">
                    <span :class="[roleMeta[user.role]?.tint, roleMeta[user.role]?.text]" class="text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap">
                      {{ user.role }}
                    </span>
                  </td>
                  <td class="px-3 py-3">
                    <span :class="[statusMeta[user.status]?.tint, statusMeta[user.status]?.text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap">
                      <span :class="statusMeta[user.status]?.dot" class="w-1.5 h-1.5 rounded-full"></span>
                      {{ user.status }}
                    </span>
                  </td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ user.lastLogin }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ user.created }}</td>
                  <td class="px-5 py-3 text-right relative">
                    <button
                      @click.stop="toggleMenu(user.id, $event)"
                      class="text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg w-8 h-8 inline-flex items-center justify-center transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                    >
                      ⋮
                    </button>

                    <Teleport to="body">
                    <div
                      v-if="openMenuId === user.id"
                      @click.stop
                      :style="menuStyle" class="fixed z-50 w-48 bg-white border border-slate-200 rounded-lg shadow-md py-1 text-left"
                    >
                      <button @click="openDrawer(user)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors">View Details</button>
                      <button v-if="user.role !== 'Parent'" @click="openEditModal(user)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors">Edit Profile</button>
                      <button @click="resetPassword(user)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors">Reset Password</button>
                      <div class="my-1 border-t border-slate-100"></div>
                      <button v-if="user.status !== 'Active'" @click="setStatus(user, 'Active')" class="w-full text-left px-3.5 py-2 text-sm text-emerald-700 hover:bg-emerald-50 transition-colors">Activate</button>
                      <button v-if="user.status === 'Active'" @click="setStatus(user, 'Inactive')" class="w-full text-left px-3.5 py-2 text-sm text-slate-600 hover:bg-slate-50 transition-colors">Deactivate</button>
                    </div>
                    </Teleport>
                  </td>
                </tr>

                <tr v-if="!isLoadingUsers && filteredUsers.length === 0">
                  <td colspan="8" class="px-5 py-12 text-center text-sm text-slate-400">No users match your search or filters.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </main>
    </div>

    <!-- ============================ DETAILS DRAWER ============================ -->
    <transition name="fade">
      <div v-if="showDrawer" class="fixed inset-0 bg-slate-900/30 z-40" @click="closeDrawer"></div>
    </transition>
    <transition name="slide">
      <aside v-if="showDrawer" class="fixed top-0 right-0 h-screen w-full max-w-sm bg-white border-l border-slate-200 shadow-lg z-50 flex flex-col">
        <div class="h-[70px] flex items-center justify-between px-5 border-b border-slate-200 shrink-0">
          <h2 class="text-sm font-bold text-slate-900">User Details</h2>
          <button @click="closeDrawer" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
        </div>

        <div v-if="selectedUser" class="flex-1 overflow-y-auto p-6 space-y-6">
          <div class="flex flex-col items-center text-center gap-3">
            <div class="w-16 h-16 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-lg font-bold">
              {{ initials(selectedUser) }}
            </div>
            <div>
              <p class="text-base font-bold text-slate-900">{{ selectedUser.firstName }} {{ selectedUser.lastName }}</p>
              <span :class="[roleMeta[selectedUser.role]?.tint, roleMeta[selectedUser.role]?.text]" class="mt-1 inline-block text-xs font-semibold px-2.5 py-1 rounded-full">
                {{ selectedUser.role }}
              </span>
            </div>
          </div>

          <div class="bg-slate-50 rounded-lg divide-y divide-slate-200">
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Username</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedUser.username || '—' }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Email</span>
              <span class="text-sm font-medium text-slate-900 truncate ml-4">{{ selectedUser.email }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Contact Number</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedUser.contact }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Account Status</span>
              <span :class="[statusMeta[selectedUser.status]?.tint, statusMeta[selectedUser.status]?.text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full">
                <span :class="statusMeta[selectedUser.status]?.dot" class="w-1.5 h-1.5 rounded-full"></span>
                {{ selectedUser.status }}
              </span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Needs Password Change</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedUser.mustChangePassword ? 'Yes' : 'No' }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Date Created</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedUser.created }}</span>
            </div>
            <div class="flex items-center justify-between px-4 py-3">
              <span class="text-xs text-slate-500">Last Login</span>
              <span class="text-sm font-medium text-slate-900">{{ selectedUser.lastLogin }}</span>
            </div>
          </div>
        </div>

        <div class="border-t border-slate-200 p-4 flex items-center gap-2 shrink-0">
          <button v-if="selectedUser?.role !== 'Parent'" @click="closeDrawer(); openEditModal(selectedUser)" class="flex-1 text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors">Edit Profile</button>
          <button @click="resetPassword(selectedUser)" class="flex-1 text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Reset Password</button>
          <button @click="closeDrawer" class="text-sm font-semibold px-4 py-2 rounded-lg text-slate-500 hover:bg-slate-50 transition-colors">Close</button>
        </div>
      </aside>
    </transition>

    <!-- ============================ EDIT PROFILE MODAL ============================ -->
    <transition name="fade">
      <div v-if="showEditModal" class="fixed inset-0 bg-slate-900/40 z-[60] flex items-center justify-center p-4" @click.self="showEditModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-lg max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <div>
              <h2 class="text-base font-bold text-slate-900">Edit Profile</h2>
              <p class="text-xs text-slate-500">{{ editForm.role }} account</p>
            </div>
            <button @click="showEditModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
          </div>
          <div class="p-6 grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">First Name *</label>
              <input v-model="editForm.firstName" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Middle Name</label>
              <input v-model="editForm.middleName" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Last Name *</label>
              <input v-model="editForm.lastName" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white" />
            </div>
            <div v-if="editForm.role === 'Doctor' || editForm.role === 'Nurse'" class="sm:col-span-3">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Professional License No. (PRC)</label>
              <input v-model="editForm.prcNo" v-digits type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white" />
            </div>
            <div class="sm:col-span-2">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Email</label>
              <input v-model="editForm.email" type="email" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Contact No.</label>
              <input v-model="editForm.contactNo" v-digits type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white" />
            </div>
            <div class="sm:col-span-3">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Address</label>
              <input v-model="editForm.address" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white" />
            </div>
            <p class="sm:col-span-3 text-xs text-slate-400">The role can't be changed here. Create a new account if someone changes position.</p>
            <p v-if="editError" class="sm:col-span-3 text-xs text-rose-500">{{ editError }}</p>
          </div>
          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showEditModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">Cancel</button>
            <button @click="saveEdit" :disabled="savingEdit" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50">{{ savingEdit ? 'Saving...' : 'Save Changes' }}</button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ ADD USER MODAL ============================ -->
    <transition name="fade">
      <div v-if="showAddModal" class="fixed inset-0 bg-slate-900/40 z-40 flex items-center justify-center p-4" @click.self="showAddModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-2xl max-h-[90vh] overflow-y-auto">
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Add User</h2>
            <button @click="showAddModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 transition-colors">✕</button>
          </div>

          <div class="p-6 grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="sm:col-span-2">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Role</label>
              <select v-model="addForm.role" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors">
                <option>Parent</option>
                <option>Doctor</option>
                <option>Nurse</option>
                <option>Staff</option>
              </select>
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">First Name</label>
              <input v-model="addForm.firstName" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Middle Name</label>
              <input v-model="addForm.middleName" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Last Name</label>
              <input v-model="addForm.lastName" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>

            

            <!-- Doctor/Nurse-only: License number -->
            <div v-if="showLicenseField">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">License / PRC Number</label>
              <input v-model="addForm.licenseNumber" v-digits type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Email</label>
              <input v-model="addForm.email" type="email" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Contact Number</label>
              <input v-model="addForm.contactNo" v-digits type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>

            <!-- Parent-only: Barangay -->
            <div v-if="!isPersonnelRole">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Barangay No.</label>
              <input v-model="addForm.barangayNo" v-digits type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>

            <div :class="isPersonnelRole ? 'sm:col-span-2' : ''">
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Address</label>
              <input v-model="addForm.address" type="text" class="w-full text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>

            <div v-if="createError" class="sm:col-span-2 rounded-lg bg-red-50 border border-red-200 px-3.5 py-2.5">
              <p class="text-red-600 text-xs">{{ createError }}</p>
            </div>

            <p class="sm:col-span-2 text-xs text-slate-400">
              A temporary password will be generated automatically once you create this account.
            </p>
          </div>

          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showAddModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Cancel</button>
            <button
              @click="createUser"
              :disabled="!canCreate || isCreating"
              class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:bg-slate-300 transition-colors"
            >
              {{ isCreating ? 'Creating…' : 'Create User' }}
            </button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ======================= TEMPORARY PASSWORD MODAL ======================= -->
    <transition name="fade">
      <div v-if="showTempPasswordModal" class="fixed inset-0 bg-slate-900/40 z-50 flex items-center justify-center p-4" @click.self="closeTempPasswordModal">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-sm p-6">
          <div class="w-12 h-12 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center text-xl mb-4">🔑</div>
          <h2 class="text-base font-bold text-slate-900 mb-1">Temporary password ready</h2>
          <p class="text-sm text-slate-500 mb-4">
            Share this with <span class="font-semibold text-slate-700">{{ tempPasswordResult?.name }}</span> — it's shown only once here, so copy it now.
          </p>

          <!-- GENERATED USERNAME -->
<div class="mb-3">
  <p class="text-xs font-semibold text-slate-500 mb-1.5">
    Username
  </p>

  <div class="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-3">
    <code class="flex-1 text-sm font-mono font-semibold text-slate-900 select-all">
      {{ tempPasswordResult?.username }}
    </code>
  </div>
</div>

<!-- TEMPORARY PASSWORD -->
<div class="mb-3">
  <p class="text-xs font-semibold text-slate-500 mb-1.5">
    Temporary Password
  </p>

  <div class="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-3">
    <code class="flex-1 text-sm font-mono font-semibold text-slate-900 tracking-wide select-all">
      {{ tempPasswordResult?.password }}
    </code>

    <button
      @click="copyTempPassword"
      class="text-xs font-semibold px-2.5 py-1.5 rounded-md bg-white border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors shrink-0"
    >
      Copy
    </button>
  </div>
</div>
          <p v-if="tempPasswordResult?.emailed || tempPasswordResult?.texted" class="text-xs text-emerald-700 mb-2">{{ tempPasswordResult.emailed && tempPasswordResult.texted ? 'A copy was also sent to their email and by text message.' : tempPasswordResult.emailed ? 'A copy was also sent to their email address.' : 'A copy was also sent to them by text message.' }}</p>
          <p v-if="copyStatus" class="text-xs text-emerald-600 mb-4">{{ copyStatus }}</p>
          <p v-else class="text-xs text-transparent mb-4">placeholder</p>

          <button @click="closeTempPasswordModal" class="w-full text-sm font-semibold px-4 py-2.5 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors">
            Done
          </button>
        </div>
      </div>
    </transition>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.slide-enter-active, .slide-leave-active { transition: transform 0.25s ease; }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }

@media (prefers-reduced-motion: reduce) {
  * { transition-duration: 0.01ms !important; }
}
</style>