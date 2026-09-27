<script setup>
import { ref, computed, reactive, onMounted } from 'vue'
import { useFloatingMenu } from '@/utils/floatingMenu'
import axios from 'axios'
import AppHeader from './Components/AppHeader.vue'
import AppSidebar from './Components/AppSidebar.vue'
import { API_BASE, ageLabel, formatDate, isSameDay, toISODate, downloadCSV } from '@/utils/format'

/* -------------------------------- Status meta -------------------------------- */
const vaccMeta = {
  'Fully Vaccinated': { tint: 'bg-emerald-50', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  'Upcoming': { tint: 'bg-amber-50', text: 'text-amber-700', dot: 'bg-amber-500' },
  'Partially Vaccinated': { tint: 'bg-sky-50', text: 'text-sky-700', dot: 'bg-sky-500' },
  'Delayed': { tint: 'bg-rose-50', text: 'text-rose-600', dot: 'bg-rose-500' },
}
const metaFor = s => vaccMeta[s] || vaccMeta.Upcoming

/* -------------------------------- Patient data (GET /api/Children/overview) -------------------------------- */
const patients = ref([])
const loading = ref(false)
const loadError = ref('')
const toast = ref('')

function flash(msg) {
  toast.value = msg
  setTimeout(() => { toast.value = '' }, msg.includes('password') ? 15000 : 3500)
}

async function fetchPatients() {
  loading.value = true
  loadError.value = ''
  try {
    const res = await axios.get(`${API_BASE}/Children/overview`)
    patients.value = res.data.map(c => ({
      ...c,
      id: c.childID,
      patientId: `PT-${c.childID.slice(-8).toUpperCase()}`,
      age: ageLabel(c.birthDate),
      lastVaccination: c.lastVaccinationDate ? formatDate(c.lastVaccinationDate) : '—',
      parentDisplay: c.parentName || '—',
    }))
    if (selectedPatient.value) {
      selectedPatient.value = patients.value.find(p => p.id === selectedPatient.value.id) || null
    }
  } catch (e) {
    console.error('fetchPatients:', e)
    loadError.value = 'Could not load patients. Check that the API is running.'
  } finally {
    loading.value = false
  }
}

onMounted(fetchPatients)

/* ---------------------------- Toolbar / filters ---------------------------- */
const searchQuery = ref('')
const vaccFilter = ref('All')
const sexFilter = ref('All')

const filteredPatients = computed(() =>
  patients.value.filter((p) => {
    const q = searchQuery.value.trim().toLowerCase()
    const fullName = `${p.firstName} ${p.lastName}`.toLowerCase()
    const matchesSearch =
      !q || fullName.includes(q) || (p.familyNo || '').toLowerCase().includes(q) || String(p.barangay ?? '').includes(q) || (p.parentName || '').toLowerCase().includes(q)
    const matchesVacc = vaccFilter.value === 'All' || p.vaccinationStatus === vaccFilter.value
    const matchesSex = sexFilter.value === 'All' || p.sex === sexFilter.value
    return matchesSearch && matchesVacc && matchesSex
  })
)

/* -------------------------------- Summary ---------------------------------- */
const summary = computed(() => {
  const inAWeek = new Date(); inAWeek.setDate(inAWeek.getDate() + 7)
  const now = new Date()
  return {
    total: patients.value.length,
    full: patients.value.filter(p => p.vaccinationStatus === 'Fully Vaccinated').length,
    partial: patients.value.filter(p => p.vaccinationStatus === 'Partially Vaccinated').length,
    newThisMonth: patients.value.filter(p => {
      const d = new Date(p.createdAt)
      return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth()
    }).length,
    dueThisWeek: patients.value.filter(p => p.nextDueDate && new Date(p.nextDueDate) <= inAWeek && p.vaccinationStatus !== 'Delayed').length,
    delayed: patients.value.filter(p => p.vaccinationStatus === 'Delayed').length,
  }
})

const initials = (p) => `${p.firstName?.[0] ?? ''}${p.lastName?.[0] ?? ''}`.toUpperCase()

const exportPatients = () => {
  downloadCSV(`patients-${toISODate()}.csv`, [
    ['Family No.', 'Barangay', 'First Name', 'Middle Name', 'Last Name', 'Birth Date', 'Sex', 'Parent / Guardian', 'Contact', 'Vaccination Status', 'Doses Completed', 'Last Vaccination', 'Next Due', 'Next Due Date'],
    ...filteredPatients.value.map(p => [
      p.familyNo || '', p.barangay ?? '', p.firstName, p.middleName, p.lastName, formatDate(p.birthDate), p.sex, p.parentName, p.parentContact,
      p.vaccinationStatus, `${p.completedDoses}/${p.totalDoses}`, p.lastVaccination, p.nextVaccine, p.nextDueDate ? formatDate(p.nextDueDate) : '',
    ]),
  ])
}

/* ------------------------------ Row actions menu ---------------------------- */
const { openMenuId, menuStyle, toggleMenu, closeMenu } = useFloatingMenu()

const printCard = (patient) => {
  closeMenu()
  window.open(`/print/vaccination-card/${patient.id}`, '_blank')
}

/* -------------------------------- Profile drawer ---------------------------- */
const showDrawer = ref(false)
const selectedPatient = ref(null)
const history = ref([])
const loadingHistory = ref(false)

const openDrawer = async (patient) => {
  selectedPatient.value = patient
  showDrawer.value = true
  closeMenu()
  history.value = []
  loadingHistory.value = true
  try {
    const res = await axios.get(`${API_BASE}/VaccinationRecords/child/${patient.id}`)
    history.value = res.data
      .filter(r => (r.status ?? 'Completed') === 'Completed')
      .sort((a, b) => new Date(b.vaccinationDate) - new Date(a.vaccinationDate))
  } catch (e) {
    console.error('history:', e)
  } finally {
    loadingHistory.value = false
  }
}
const closeDrawer = () => (showDrawer.value = false)

const unlinkParent = async (link) => {
  if (!confirm(`Unlink ${link.name} from ${selectedPatient.value.firstName}? They will no longer see this child's record.`)) return
  try {
    await axios.delete(`${API_BASE}/ChildParentRelationships/${link.relationshipID}`)
    flash(`${link.name} unlinked.`)
    await fetchPatients()
  } catch (e) {
    flash(e.response?.data?.message || 'Could not unlink this parent.')
  }
}

/* -------------------------------- Parents (for register / link) -------------------------------- */
const parents = ref([])
async function ensureParents() {
  if (parents.value.length) return
  try {
    const res = await axios.get(`${API_BASE}/Parents/all`)
    parents.value = res.data.map(p => ({
      id: p.parentID,
      name: `${p.firstName} ${p.lastName}`.trim(),
      email: p.email,
      contactNo: p.contactNo,
    }))
  } catch (e) {
    console.error('parents:', e)
  }
}
const parentMatches = (q) => {
  const s = (q || '').trim().toLowerCase()
  if (!s) return []
  return parents.value.filter(p => p.name.toLowerCase().includes(s) || (p.email || '').toLowerCase().includes(s)).slice(0, 8)
}

/* --------------------------------- Register modal --------------------------------- */
const showRegisterModal = ref(false)
const parentMode = ref('search')
const saving = ref(false)
const formError = ref('')
const blankRegister = () => ({
  firstName: '', middleName: '', lastName: '', birthDate: '', sex: 'Male',
  birthWeight: '', birthHeight: '', placeOfBirth: '', address: '', allergies: '', familyNo: '', barangay: '',
  parentSearch: '', selectedParent: null,
  newParentFirstName: '', newParentLastName: '', newParentContact: '', newParentEmail: '',
  relationship: 'Mother',
})
const registerForm = reactive(blankRegister())

const openRegisterModal = async () => {
  Object.assign(registerForm, blankRegister())
  parentMode.value = 'search'
  formError.value = ''
  showRegisterModal.value = true
  await ensureParents()
}

const registerPatient = async () => {
  formError.value = ''
  if (!registerForm.firstName.trim() || !registerForm.lastName.trim() || !registerForm.birthDate) {
    formError.value = 'First name, last name and birth date are required.'
    return
  }
  saving.value = true
  try {
    let parentId = registerForm.selectedParent?.id
    let newParentPassword = ''
    if (parentMode.value === 'new') {
      if (!registerForm.newParentFirstName.trim() || !registerForm.newParentLastName.trim() || !registerForm.newParentContact.trim()) {
        formError.value = "Enter the parent's first name, last name and contact number."
        saving.value = false
        return
      }
      const hasEmail = !!registerForm.newParentEmail.trim()
      const res = await axios.post(`${API_BASE}/Parents`, {
        firstName: registerForm.newParentFirstName,
        lastName: registerForm.newParentLastName,
        contactNo: registerForm.newParentContact,
        email: hasEmail ? registerForm.newParentEmail : null,
        address: registerForm.address || null,
        createLogin: hasEmail,
      })
      parentId = res.data.parentID ?? res.data.parent?.parentID ?? res.data.ParentID
      newParentPassword = res.data.temporaryPassword || ''
      parents.value = []
    }
    if (!parentId) {
      formError.value = 'Search and select the parent/guardian, or register a new one.'
      saving.value = false
      return
    }

    await axios.post(`${API_BASE}/Children`, {
      firstName: registerForm.firstName,
      middleName: registerForm.middleName || null,
      lastName: registerForm.lastName,
      birthDate: registerForm.birthDate,
      sex: registerForm.sex,
      placeOfBirth: registerForm.placeOfBirth || null,
      address: registerForm.address || null,
      allergies: registerForm.allergies || null,
      healthCenter: 'Leveriza Health Center',
      familyNo: registerForm.familyNo?.trim() || null,
      barangay: registerForm.barangay === '' || registerForm.barangay == null ? null : Number(registerForm.barangay),
      birthWeight: registerForm.birthWeight ? Number(registerForm.birthWeight) : null,
      birthHeight: registerForm.birthHeight ? Number(registerForm.birthHeight) : null,
      parents: [{ parentID: parentId, relationshipType: registerForm.relationship, isPrimaryContact: true, canReceiveNotifications: true }],
    })
    showRegisterModal.value = false
    flash(newParentPassword
      ? `${registerForm.firstName} ${registerForm.lastName} registered. Parent login: ${registerForm.newParentEmail} / temporary password ${newParentPassword} (they will be asked to change it).`
      : `${registerForm.firstName} ${registerForm.lastName} registered — vaccination schedule generated.`)
    await fetchPatients()
  } catch (e) {
    formError.value = e.response?.data?.message || 'Could not register this child.'
  } finally {
    saving.value = false
  }
}

/* --------------------------------- Edit modal --------------------------------- */
const showEditModal = ref(false)
const editForm = reactive({})

const openEditModal = (patient) => {
  closeMenu()
  formError.value = ''
  Object.assign(editForm, {
    id: patient.id,
    firstName: patient.firstName, middleName: patient.middleName || '', lastName: patient.lastName,
    birthDate: String(patient.birthDate).slice(0, 10), sex: patient.sex || 'Male',
    placeOfBirth: patient.placeOfBirth || '', address: patient.address || '', allergies: patient.allergies || '',
    birthWeight: patient.birthWeight ?? '', birthHeight: patient.birthHeight ?? '', barangay: patient.barangay, familyNo: patient.familyNo || '',
  })
  showEditModal.value = true
}

const saveEdit = async () => {
  formError.value = ''
  if (!editForm.firstName.trim() || !editForm.lastName.trim()) {
    formError.value = 'First and last name are required.'
    return
  }
  saving.value = true
  try {
    await axios.put(`${API_BASE}/Children/${editForm.id}`, {
      firstName: editForm.firstName,
      middleName: editForm.middleName || null,
      lastName: editForm.lastName,
      birthDate: editForm.birthDate,
      sex: editForm.sex,
      placeOfBirth: editForm.placeOfBirth || null,
      address: editForm.address || null,
      allergies: editForm.allergies || null,
      barangay: editForm.barangay === '' || editForm.barangay == null ? null : Number(editForm.barangay),
      familyNo: editForm.familyNo?.trim() || null,
      healthCenter: 'Leveriza Health Center',
      birthWeight: editForm.birthWeight === '' ? null : Number(editForm.birthWeight),
      birthHeight: editForm.birthHeight === '' ? null : Number(editForm.birthHeight),
    })
    showEditModal.value = false
    flash('Patient information updated.')
    await fetchPatients()
  } catch (e) {
    formError.value = e.response?.data?.message || 'Could not save changes.'
  } finally {
    saving.value = false
  }
}

/* --------------------------------- Link parent modal --------------------------------- */
const showLinkModal = ref(false)
const linkForm = reactive({ childId: null, childName: '', search: '', selected: null, relationship: 'Mother', primary: false })

const openLinkModal = async (patient) => {
  closeMenu()
  formError.value = ''
  Object.assign(linkForm, { childId: patient.id, childName: `${patient.firstName} ${patient.lastName}`, search: '', selected: null, relationship: 'Mother', primary: !patient.parents?.length })
  showLinkModal.value = true
  await ensureParents()
}

const saveLink = async () => {
  formError.value = ''
  if (!linkForm.selected) { formError.value = 'Select a parent/guardian.'; return }
  saving.value = true
  try {
    await axios.post(`${API_BASE}/ChildParentRelationships`, {
      childID: linkForm.childId,
      parentID: linkForm.selected.id,
      relationshipType: linkForm.relationship,
      isPrimaryContact: linkForm.primary,
      canReceiveNotifications: true,
    })
    showLinkModal.value = false
    flash(`${linkForm.selected.name} linked to ${linkForm.childName}.`)
    await fetchPatients()
  } catch (e) {
    formError.value = e.response?.data?.message || 'Could not link this parent.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900" @click="closeMenu">
    <AppSidebar />
    <div class="flex-1 min-w-0 flex flex-col">
      <AppHeader title="Patient Management" breadcrumb="System Administration / Patient Management" />

      <main class="p-6 space-y-6">
        <div v-if="toast" class="bg-emerald-50 border border-emerald-100 text-emerald-800 text-sm rounded-xl px-4 py-3">{{ toast }}</div>

        <!-- Summary cards -->
        <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
          <div v-for="card in [
              { label: 'Total Registered', value: summary.total, icon: '🧒', tint: 'bg-teal-50' },
              { label: 'Fully Vaccinated', value: summary.full, icon: '✅', tint: 'bg-emerald-50' },
              { label: 'In Progress', value: summary.partial, icon: '💉', tint: 'bg-sky-50' },
              { label: 'New This Month', value: summary.newThisMonth, icon: '🆕', tint: 'bg-violet-50' },
              { label: 'Due This Week', value: summary.dueThisWeek, icon: '📅', tint: 'bg-amber-50' },
              { label: 'Delayed Vaccinations', value: summary.delayed, icon: '⚠️', tint: 'bg-rose-50' },
            ]" :key="card.label" class="min-w-0 bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
            <div class="flex items-start justify-between gap-2">
              <p class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ card.label }}</p>
              <div :class="card.tint" class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 text-sm">{{ card.icon }}</div>
            </div>
            <p class="mt-2 text-2xl font-extrabold text-slate-900">{{ card.value }}</p>
          </div>
        </section>

        <!-- Toolbar -->
        <section class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="flex flex-col lg:flex-row lg:items-center gap-3">
            <div class="relative flex-1 min-w-0">
              <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-sm" aria-hidden="true">🔍</span>
              <input v-model="searchQuery" type="text" placeholder="Search by child name, Family No., barangay, or parent name..."
                class="w-full pl-9 pr-3 py-2 text-sm rounded-lg border border-slate-200 bg-slate-50 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white transition-colors" />
            </div>
            <select v-model="vaccFilter" class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white">
              <option value="All">All Vaccination Status</option>
              <option>Fully Vaccinated</option>
              <option>Partially Vaccinated</option>
              <option>Delayed</option>
              <option>Upcoming</option>
            </select>
            <select v-model="sexFilter" class="text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white">
              <option value="All">All Sexes</option>
              <option>Male</option>
              <option>Female</option>
            </select>
            <div class="flex items-center gap-2 shrink-0">
              <button @click="exportPatients" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">Export</button>
              <button @click.stop="openRegisterModal" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors">+ Register Child</button>
            </div>
          </div>
        </section>

        <!-- Patient table -->
        <section class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-200 bg-slate-50/60">
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Family No.</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Child Name</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Age</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Sex</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Parent / Guardian</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Vaccination Status</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Progress</th>
                  <th class="text-left font-semibold text-slate-500 text-xs uppercase tracking-wide px-3 py-3">Last Vaccination</th>
                  <th class="text-right font-semibold text-slate-500 text-xs uppercase tracking-wide px-5 py-3">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="patient in filteredPatients" :key="patient.id" class="border-b border-slate-100 last:border-0 hover:bg-slate-50 transition-colors">
                  <td class="px-5 py-3 whitespace-nowrap"><p class="text-sm font-medium text-slate-800">{{ patient.familyNo || '—' }}</p><p class="text-[11px] text-slate-400">Brgy {{ patient.barangay ?? '—' }}</p></td>
                  <td class="px-3 py-3 whitespace-nowrap">
                    <button @click.stop="openDrawer(patient)" class="flex items-center gap-3 text-left">
                      <div class="w-8 h-8 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-xs font-bold shrink-0">{{ initials(patient) }}</div>
                      <span class="font-semibold text-slate-900 hover:text-emerald-700">{{ patient.firstName }} {{ patient.lastName }}</span>
                    </button>
                  </td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ patient.age }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ patient.sex || '—' }}</td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ patient.parentDisplay }}</td>
                  <td class="px-3 py-3">
                    <span :class="[metaFor(patient.vaccinationStatus).tint, metaFor(patient.vaccinationStatus).text]" class="inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full whitespace-nowrap">
                      <span :class="metaFor(patient.vaccinationStatus).dot" class="w-1.5 h-1.5 rounded-full"></span>
                      {{ patient.vaccinationStatus }}
                    </span>
                  </td>
                  <td class="px-3 py-3 whitespace-nowrap">
                    <div class="flex items-center gap-2">
                      <div class="w-16 h-1.5 rounded-full bg-slate-100 overflow-hidden"><div class="h-full bg-emerald-500 rounded-full" :style="{ width: patient.completion + '%' }"></div></div>
                      <span class="text-xs text-slate-500">{{ patient.completedDoses }}/{{ patient.totalDoses }}</span>
                    </div>
                  </td>
                  <td class="px-3 py-3 text-slate-500 whitespace-nowrap">{{ patient.lastVaccination }}</td>
                  <td class="px-5 py-3 text-right relative">
                    <button @click.stop="toggleMenu(patient.id, $event)" class="text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg w-8 h-8 inline-flex items-center justify-center transition-colors">⋮</button>
                    <Teleport to="body">
                    <div v-if="openMenuId === patient.id" @click.stop :style="menuStyle" class="fixed z-50 w-56 bg-white border border-slate-200 rounded-lg shadow-md py-1 text-left">
                      <button @click="openDrawer(patient)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50">View Patient Profile</button>
                      <button @click="openEditModal(patient)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50">Edit Patient Information</button>
                      <button @click="printCard(patient)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50">Print Vaccination Card</button>
                      <div class="my-1 border-t border-slate-100"></div>
                      <button @click="openLinkModal(patient)" class="w-full text-left px-3.5 py-2 text-sm text-slate-700 hover:bg-slate-50">Link Parent / Guardian</button>
                    </div>
                    </Teleport>
                  </td>
                </tr>
                <tr v-if="filteredPatients.length === 0">
                  <td colspan="9" class="px-5 py-12 text-center text-sm" :class="loadError ? 'text-rose-500' : 'text-slate-400'">
                    {{ loading ? 'Loading patients...' : (loadError || 'No patients match your search or filters.') }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </main>
    </div>

    <!-- ============================ PATIENT PROFILE DRAWER ============================ -->
    <transition name="fade">
      <div v-if="showDrawer" class="fixed inset-0 bg-slate-900/30 z-40" @click="closeDrawer"></div>
    </transition>
    <transition name="slide">
      <aside v-if="showDrawer && selectedPatient" class="fixed top-0 right-0 h-screen w-full max-w-xl bg-white border-l border-slate-200 shadow-lg z-50 flex flex-col" @click.stop>
        <div class="h-[70px] flex items-center justify-between px-6 border-b border-slate-200 shrink-0">
          <h2 class="text-sm font-bold text-slate-900">Patient Profile</h2>
          <button @click="closeDrawer" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50">✕</button>
        </div>

        <div class="flex-1 overflow-y-auto p-6 space-y-6">
          <div class="bg-slate-50 rounded-xl border border-slate-200 p-5">
            <div class="flex items-center gap-4 mb-4">
              <div class="w-16 h-16 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-lg font-bold shrink-0">{{ initials(selectedPatient) }}</div>
              <div class="min-w-0">
                <p class="text-base font-bold text-slate-900 truncate">{{ selectedPatient.firstName }} {{ selectedPatient.middleName }} {{ selectedPatient.lastName }}</p>
                <p class="text-xs text-slate-500">Family No. {{ selectedPatient.familyNo || '—' }} · Brgy {{ selectedPatient.barangay ?? '—' }}</p>
              </div>
            </div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-2">Child Information</h3>
            <div class="grid grid-cols-2 gap-x-4 gap-y-3">
              <div><p class="text-xs text-slate-500">Birth Date</p><p class="text-sm font-medium text-slate-900">{{ formatDate(selectedPatient.birthDate) }}</p></div>
              <div><p class="text-xs text-slate-500">Age</p><p class="text-sm font-medium text-slate-900">{{ selectedPatient.age }}</p></div>
              <div><p class="text-xs text-slate-500">Sex</p><p class="text-sm font-medium text-slate-900">{{ selectedPatient.sex || '—' }}</p></div>
              <div><p class="text-xs text-slate-500">Birth Weight / Length</p><p class="text-sm font-medium text-slate-900">{{ selectedPatient.birthWeight ? selectedPatient.birthWeight + ' kg' : '—' }} / {{ selectedPatient.birthHeight ? selectedPatient.birthHeight + ' cm' : '—' }}</p></div>
              <div><p class="text-xs text-slate-500">Place of Birth</p><p class="text-sm font-medium text-slate-900">{{ selectedPatient.placeOfBirth || '—' }}</p></div>
              <div><p class="text-xs text-slate-500">Allergies</p><p class="text-sm font-medium" :class="selectedPatient.allergies ? 'text-rose-600' : 'text-slate-900'">{{ selectedPatient.allergies || 'None recorded' }}</p></div>
              <div><p class="text-xs text-slate-500">Existing Conditions</p><p class="text-sm font-medium" :class="selectedPatient.existingConditions ? 'text-rose-600' : 'text-slate-900'">{{ selectedPatient.existingConditions || 'None recorded' }}</p></div>
              <div class="col-span-2"><p class="text-xs text-slate-500">Address</p><p class="text-sm font-medium text-slate-900">{{ selectedPatient.address || '—' }}</p></div>
            </div>
          </div>

          <div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Parent / Guardian</h3>
            <div class="bg-slate-50 rounded-lg divide-y divide-slate-200">
              <div v-for="link in selectedPatient.parents" :key="link.relationshipID" class="flex items-center justify-between px-4 py-3 gap-3">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-slate-900">{{ link.name }} <span v-if="link.isPrimaryContact" class="text-[10px] font-bold text-emerald-700 uppercase ml-1">Primary</span></p>
                  <p class="text-xs text-slate-500">{{ link.relationshipType }} · {{ link.contactNo || 'no contact' }}<span v-if="link.email"> · {{ link.email }}</span></p>
                </div>
                <button @click="unlinkParent(link)" class="text-xs font-semibold px-2.5 py-1 rounded-lg border border-rose-200 text-rose-600 hover:bg-rose-50 shrink-0">Unlink</button>
              </div>
              <p v-if="!selectedPatient.parents?.length" class="px-4 py-3 text-sm text-slate-400">No parent linked.</p>
            </div>
            <button @click="closeDrawer(); openLinkModal(selectedPatient)" class="mt-3 text-xs font-semibold px-3 py-1.5 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">+ Link Parent / Guardian</button>
          </div>

          <div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Vaccination Overview</h3>
            <div class="bg-slate-50 rounded-lg p-4 space-y-4">
              <div>
                <div class="flex items-center justify-between mb-1.5">
                  <span class="text-xs text-slate-500">Vaccination Progress</span>
                  <span class="text-xs font-semibold text-emerald-700">{{ selectedPatient.completedDoses }}/{{ selectedPatient.totalDoses }} doses · {{ selectedPatient.completion }}%</span>
                </div>
                <div class="w-full h-2 rounded-full bg-slate-200 overflow-hidden"><div class="h-full bg-emerald-500 rounded-full" :style="{ width: selectedPatient.completion + '%' }"></div></div>
              </div>
              <div class="grid grid-cols-2 gap-x-4 gap-y-3">
                <div><p class="text-xs text-slate-500">Latest Vaccine</p><p class="text-sm font-medium text-slate-900">{{ selectedPatient.lastVaccine || '—' }}</p></div>
                <div><p class="text-xs text-slate-500">Next Scheduled Vaccine</p><p class="text-sm font-medium text-slate-900">{{ selectedPatient.nextVaccine || '—' }}</p><p v-if="selectedPatient.nextDueDate" class="text-xs text-slate-500">{{ formatDate(selectedPatient.nextDueDate) }}</p></div>
              </div>
            </div>
          </div>

          <div>
            <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Vaccination History</h3>
            <div v-if="loadingHistory" class="text-sm text-slate-400">Loading...</div>
            <div v-else-if="history.length === 0" class="text-sm text-slate-400">No doses recorded yet.</div>
            <div v-else class="space-y-2">
              <div v-for="h in history" :key="h.vaccinationRecordID" class="flex items-start justify-between gap-3 bg-slate-50 rounded-lg px-4 py-2.5">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-slate-900">{{ h.vaccineName }} — Dose {{ h.doseNumber }}</p>
                  <p class="text-xs text-slate-500">{{ h.administeredByName || 'Historical record' }}<span v-if="h.lotNumber"> · Lot {{ h.lotNumber }}</span></p>
                  <p v-if="h.nurseObservation" class="text-xs text-slate-500 italic mt-0.5">{{ h.nurseObservation }}</p>
                </div>
                <span class="text-xs text-slate-500 whitespace-nowrap">{{ formatDate(h.vaccinationDate) }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="border-t border-slate-200 p-4 flex items-center gap-2 shrink-0">
          <button @click="closeDrawer(); openEditModal(selectedPatient)" class="flex-1 text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700">Edit Information</button>
          <button @click="printCard(selectedPatient)" class="flex-1 text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">Print Vaccination Card</button>
        </div>
      </aside>
    </transition>

    <!-- ============================ REGISTER CHILD MODAL ============================ -->
    <transition name="fade">
      <div v-if="showRegisterModal" class="fixed inset-0 bg-slate-900/40 z-50 flex items-center justify-center p-4" @click.self="showRegisterModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-2xl max-h-[90vh] overflow-y-auto" @click.stop>
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Register Child</h2>
            <button @click="showRegisterModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50">✕</button>
          </div>

          <div class="p-6 space-y-6">
            <div>
              <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Child Information</h3>
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">First Name *</label><input v-model="registerForm.firstName" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Middle Name</label><input v-model="registerForm.middleName" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Last Name *</label><input v-model="registerForm.lastName" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Birth Date *</label><input v-model="registerForm.birthDate" type="date" :max="toISODate()" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Sex</label><select v-model="registerForm.sex" class="field"><option>Male</option><option>Female</option></select></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Place of Birth</label><input v-model="registerForm.placeOfBirth" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Birth Weight (kg)</label><input v-model="registerForm.birthWeight" v-digits.decimal type="number" step="0.01" min="0.5" max="7" placeholder="e.g. 3.2" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Birth Length (cm)</label><input v-model="registerForm.birthHeight" v-digits.decimal type="number" step="0.1" min="25" max="65" placeholder="e.g. 50" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Allergies</label><input v-model="registerForm.allergies" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Family No.</label><input v-model="registerForm.familyNo" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Barangay</label><input v-model="registerForm.barangay" v-digits type="number" class="field" /></div>
                <div class="sm:col-span-3"><label class="block text-xs font-semibold text-slate-500 mb-1.5">Address</label><input v-model="registerForm.address" type="text" maxlength="70" class="field" /></div>
              </div>
            </div>

            <div>
              <h3 class="text-xs font-bold uppercase tracking-wide text-slate-500 mb-3">Parent / Guardian</h3>
              <div class="flex gap-2 mb-3">
                <button @click="parentMode = 'search'" :class="parentMode === 'search' ? 'bg-emerald-600 text-white' : 'border border-slate-200 text-slate-600 hover:bg-slate-50'" class="text-xs font-semibold px-3 py-1.5 rounded-lg">Search Existing Parent</button>
                <button @click="parentMode = 'new'" :class="parentMode === 'new' ? 'bg-emerald-600 text-white' : 'border border-slate-200 text-slate-600 hover:bg-slate-50'" class="text-xs font-semibold px-3 py-1.5 rounded-lg">Register New Parent</button>
              </div>

              <div v-if="parentMode === 'search'">
                <div v-if="registerForm.selectedParent" class="flex items-center justify-between bg-emerald-50 border border-emerald-100 rounded-lg px-3 py-2.5">
                  <span class="text-sm font-medium text-emerald-800">{{ registerForm.selectedParent.name }} <span class="text-xs text-emerald-600">{{ registerForm.selectedParent.email }}</span></span>
                  <button @click="registerForm.selectedParent = null" class="text-xs text-emerald-700 hover:underline">Change</button>
                </div>
                <template v-else>
                  <input v-model="registerForm.parentSearch" type="text" placeholder="Search by name or email..." class="field" />
                  <div v-if="parentMatches(registerForm.parentSearch).length" class="mt-2 border border-slate-200 rounded-lg divide-y divide-slate-100">
                    <button v-for="p in parentMatches(registerForm.parentSearch)" :key="p.id" @click="registerForm.selectedParent = p" class="w-full text-left px-3 py-2 hover:bg-slate-50 text-sm">
                      {{ p.name }} <span class="text-xs text-slate-400">{{ p.email || p.contactNo }}</span>
                    </button>
                  </div>
                </template>
              </div>
              <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">First Name *</label><input v-model="registerForm.newParentFirstName" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Last Name *</label><input v-model="registerForm.newParentLastName" type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Contact Number *</label><input v-model="registerForm.newParentContact" v-digits type="text" class="field" /></div>
                <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Email (for portal login)</label><input v-model="registerForm.newParentEmail" type="email" class="field" /></div>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
                <div>
                  <label class="block text-xs font-semibold text-slate-500 mb-1.5">Relationship</label>
                  <select v-model="registerForm.relationship" class="field"><option>Mother</option><option>Father</option><option>Guardian</option></select>
                </div>
              </div>
            </div>
            <p v-if="formError" class="text-xs text-rose-500">{{ formError }}</p>
          </div>

          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showRegisterModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">Cancel</button>
            <button @click="registerPatient" :disabled="saving" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50">{{ saving ? 'Saving...' : 'Register Patient' }}</button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ EDIT MODAL ============================ -->
    <transition name="fade">
      <div v-if="showEditModal" class="fixed inset-0 bg-slate-900/40 z-50 flex items-center justify-center p-4" @click.self="showEditModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-2xl max-h-[90vh] overflow-y-auto" @click.stop>
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Edit Patient Information</h2>
            <button @click="showEditModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50">✕</button>
          </div>
          <div class="p-6 grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">First Name *</label><input v-model="editForm.firstName" type="text" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Middle Name</label><input v-model="editForm.middleName" type="text" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Last Name *</label><input v-model="editForm.lastName" type="text" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Birth Date</label><input v-model="editForm.birthDate" type="date" :max="toISODate()" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Sex</label><select v-model="editForm.sex" class="field"><option>Male</option><option>Female</option></select></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Place of Birth</label><input v-model="editForm.placeOfBirth" type="text" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Birth Weight (kg)</label><input v-model="editForm.birthWeight" v-digits.decimal type="number" step="0.01" min="0.5" max="7" placeholder="e.g. 3.2" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Birth Length (cm)</label><input v-model="editForm.birthHeight" v-digits.decimal type="number" step="0.1" min="25" max="65" placeholder="e.g. 50" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Allergies</label><input v-model="editForm.allergies" type="text" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Family No.</label><input v-model="editForm.familyNo" type="text" class="field" /></div>
            <div><label class="block text-xs font-semibold text-slate-500 mb-1.5">Barangay</label><input v-model="editForm.barangay" v-digits type="number" class="field" /></div>
            <div class="sm:col-span-3"><label class="block text-xs font-semibold text-slate-500 mb-1.5">Address</label><input v-model="editForm.address" type="text" maxlength="70" class="field" /></div>
            <p v-if="formError" class="sm:col-span-3 text-xs text-rose-500">{{ formError }}</p>
          </div>
          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showEditModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">Cancel</button>
            <button @click="saveEdit" :disabled="saving" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50">{{ saving ? 'Saving...' : 'Save Changes' }}</button>
          </div>
        </div>
      </div>
    </transition>

    <!-- ============================ LINK PARENT MODAL ============================ -->
    <transition name="fade">
      <div v-if="showLinkModal" class="fixed inset-0 bg-slate-900/40 z-50 flex items-center justify-center p-4" @click.self="showLinkModal = false">
        <div class="bg-white rounded-xl shadow-lg w-full max-w-md" @click.stop>
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 class="text-base font-bold text-slate-900">Link Parent / Guardian</h2>
            <button @click="showLinkModal = false" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50">✕</button>
          </div>
          <div class="p-6 space-y-4">
            <p class="text-sm text-slate-600">Child: <span class="font-semibold text-slate-900">{{ linkForm.childName }}</span></p>
            <div v-if="linkForm.selected" class="flex items-center justify-between bg-emerald-50 border border-emerald-100 rounded-lg px-3 py-2.5">
              <span class="text-sm font-medium text-emerald-800">{{ linkForm.selected.name }}</span>
              <button @click="linkForm.selected = null" class="text-xs text-emerald-700 hover:underline">Change</button>
            </div>
            <template v-else>
              <input v-model="linkForm.search" type="text" placeholder="Search parent by name or email..." class="field" />
              <div v-if="parentMatches(linkForm.search).length" class="border border-slate-200 rounded-lg divide-y divide-slate-100">
                <button v-for="p in parentMatches(linkForm.search)" :key="p.id" @click="linkForm.selected = p" class="w-full text-left px-3 py-2 hover:bg-slate-50 text-sm">
                  {{ p.name }} <span class="text-xs text-slate-400">{{ p.email || p.contactNo }}</span>
                </button>
              </div>
            </template>
            <div>
              <label class="block text-xs font-semibold text-slate-500 mb-1.5">Relationship</label>
              <select v-model="linkForm.relationship" class="field"><option>Mother</option><option>Father</option><option>Guardian</option></select>
            </div>
            <label class="flex items-center gap-2 text-sm text-slate-700">
              <input v-model="linkForm.primary" type="checkbox" class="w-4 h-4 rounded border-slate-300 text-emerald-600" />
              Primary contact (receives reminders)
            </label>
            <p v-if="formError" class="text-xs text-rose-500">{{ formError }}</p>
          </div>
          <div class="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-200">
            <button @click="showLinkModal = false" class="text-sm font-semibold px-4 py-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50">Cancel</button>
            <button @click="saveLink" :disabled="saving" class="text-sm font-semibold px-4 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50">{{ saving ? 'Saving...' : 'Link' }}</button>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<style scoped>
.field {
  width: 100%;
  font-size: 0.875rem;
  border-radius: 0.5rem;
  border: 1px solid rgb(226 232 240);
  background: rgb(248 250 252);
  padding: 0.625rem 0.75rem;
  outline: none;
  transition: background-color 0.15s, box-shadow 0.15s;
}
.field:focus {
  background: white;
  box-shadow: 0 0 0 2px rgb(16 185 129);
}
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.slide-enter-active, .slide-leave-active { transition: transform 0.25s ease; }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }
@media (prefers-reduced-motion: reduce) {
  * { transition-duration: 0.01ms !important; }
}
</style>
