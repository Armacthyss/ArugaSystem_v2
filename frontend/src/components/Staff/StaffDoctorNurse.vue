<template>
  <div class="flex h-screen bg-stone-50 antialiased text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">

    <StaffSidebar />

    <!-- MAIN SECTION -->
    <div class="flex-1 flex flex-col overflow-hidden">
      <StaffTopbar title="Doctor / Nurse" breadcrumb="Aruga / Healthcare Workers" />

      <main class="flex-1 p-8 overflow-y-auto custom-scrollbar">
        <div class="max-w-7xl mx-auto">

          <p class="mb-5 text-sm text-slate-500">
            Healthcare workers who can see patients today. New accounts are created by the System Administrator in User Management.
          </p>

          <!-- FILTERS & SEARCH -->
          <div class="mb-6 flex justify-between items-center gap-4 flex-wrap">
            <div class="relative">
              <Search class="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
              <input v-model="search" type="text" placeholder="Search by name or PRC..." class="pl-10 pr-4 py-2 bg-white border border-slate-200 rounded-xl text-sm focus:border-emerald-500 outline-none w-80 transition-all">
            </div>
            <div class="flex gap-2">
              <button v-for="tab in tabs" :key="tab.key" @click="activeTab = tab.key"
                class="px-4 py-2 text-xs font-bold border rounded-lg transition-colors"
                :class="activeTab === tab.key ? 'bg-emerald-600 text-white border-emerald-600' : 'bg-white border-slate-200 text-slate-600 hover:bg-slate-50'">
                {{ tab.label }} <span class="opacity-70">({{ tab.count }})</span>
              </button>
            </div>
          </div>

          <!-- STAFF TABLE -->
          <div class="bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
            <table class="w-full text-left text-sm">
              <thead class="bg-slate-50/50 border-b border-slate-200 text-[11px] uppercase font-bold text-slate-500">
                <tr>
                  <th class="px-6 py-4">Employee Name</th>
                  <th class="px-6 py-4">User Type</th>
                  <th class="px-6 py-4">Contact Number</th>
                  <th class="px-6 py-4">PRC License No.</th>
                  <th class="px-6 py-4">Account</th>
                  <th class="px-6 py-4">Last Sign-in</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="staff in filtered" :key="staff.userID" class="hover:bg-slate-50/50 transition-colors">
                  <td class="px-6 py-4">
                    <div class="flex items-center gap-3">
                      <div class="w-9 h-9 rounded-full bg-slate-100 flex items-center justify-center text-slate-500 font-bold text-xs border border-slate-200">
                        {{ (staff.firstName || '?')[0] }}{{ (staff.lastName || '')[0] }}
                      </div>
                      <div>
                        <p class="font-bold text-slate-800">{{ staff.lastName }}, {{ staff.firstName }} {{ staff.middleName ? staff.middleName[0] + '.' : '' }}</p>
                        <p class="text-[10px] text-slate-400 font-medium tracking-wide">{{ staff.email || staff.username }}</p>
                      </div>
                    </div>
                  </td>
                  <td class="px-6 py-4">
                    <span :class="staff.position === 'Doctor' ? 'bg-indigo-50 text-indigo-600 border-indigo-100' : 'bg-emerald-50 text-emerald-600 border-emerald-100'"
                      class="px-2.5 py-1 rounded-full text-[10px] font-bold border uppercase">
                      {{ staff.position }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-slate-600 font-medium">{{ staff.contactNo || '—' }}</td>
                  <td class="px-6 py-4">
                    <code v-if="staff.prcNo" class="text-xs font-bold text-slate-500 bg-slate-100 px-2 py-1 rounded tracking-widest">{{ staff.prcNo }}</code>
                    <span v-else class="text-slate-400">—</span>
                  </td>
                  <td class="px-6 py-4">
                    <span class="text-xs font-semibold" :class="staff.status === 'Active' ? 'text-emerald-700' : 'text-slate-400'">{{ staff.status }}</span>
                  </td>
                  <td class="px-6 py-4 text-slate-500 text-xs">{{ staff.lastLogin ? relativeTime(staff.lastLogin) : 'Never' }}</td>
                </tr>
                <tr v-if="filtered.length === 0">
                  <td colspan="6" class="px-6 py-12 text-center text-sm" :class="loadError ? 'text-rose-500' : 'text-slate-400'">
                    {{ loading ? 'Loading...' : (loadError || 'No healthcare workers match your search.') }}
                  </td>
                </tr>
              </tbody>
            </table>
            </div>
          </div>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import axios from 'axios'
import { Search } from 'lucide-vue-next'
import StaffSidebar from './StaffSidebar.vue'
import StaffTopbar from './StaffTopbar.vue'
import { API_BASE, relativeTime } from '@/utils/format'

// GET /api/Users?position=Doctor,Nurse — the Healthcare Worker directory.
const staffMembers = ref([])
const loading = ref(true)
const loadError = ref('')

onMounted(async () => {
  try {
    const res = await axios.get(`${API_BASE}/Users`, { params: { position: 'Doctor,Nurse' } })
    staffMembers.value = res.data
  } catch (e) {
    console.error('StaffDoctorNurse:', e)
    loadError.value = 'Could not load healthcare workers. Check that the API is running.'
  } finally {
    loading.value = false
  }
})

const search = ref('')
const activeTab = ref('all')

const tabs = computed(() => [
  { key: 'all', label: 'All', count: staffMembers.value.length },
  { key: 'Doctor', label: 'Doctors', count: staffMembers.value.filter(s => s.position === 'Doctor').length },
  { key: 'Nurse', label: 'Nurses', count: staffMembers.value.filter(s => s.position === 'Nurse').length },
])

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  return staffMembers.value.filter(s => {
    if (activeTab.value !== 'all' && s.position !== activeTab.value) return false
    if (!q) return true
    return `${s.firstName} ${s.lastName}`.toLowerCase().includes(q) || (s.prcNo || '').toLowerCase().includes(q)
  })
})
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #E2E8F0; border-radius: 10px; }
</style>
