<template>
  <div>
    <!-- 1. TOP HEADER -->
    <header class="relative z-100 flex justify-between items-center mb-6">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 rounded-xl overflow-hidden shadow-lg shrink-0">
          <img :src="logoIcon" alt="Leveriza Health Center" class="w-full h-full object-cover" />
        </div>
        <div>
          <h1 class="text-lg font-bold text-slate-800 leading-none">Aruga</h1>
          <p class="text-[9px] font-black text-emerald-500 uppercase tracking-tighter">Pediatric Portal</p>
        </div>
      </div>

      <div class="flex items-center gap-4">
        <button @click="openNotifications" class="relative w-10 h-10 rounded-full bg-white border border-slate-200 flex items-center justify-center shadow-sm hover:bg-slate-50 transition-colors">
          <span class="text-lg">🔔</span>
          <span v-if="unreadCount > 0" class="absolute -top-1 -right-1 bg-red-500 text-white text-[8px] font-bold px-1.5 py-0.5 rounded-full min-w-4.5 text-center">
            {{ unreadCount > 99 ? '99+' : unreadCount }}
          </span>
        </button>

        <div class="flex items-center gap-3">
          <div class="hidden sm:flex flex-col items-end text-right cursor-pointer hover:opacity-70 transition-opacity" @click="openProfile">
            <span class="font-bold text-sm">{{ parentData?.firstName }} {{ parentData?.lastName }}</span>
            <span class="text-[9px] font-black uppercase text-emerald-500">View Profile</span>
          </div>
          <div class="relative">
            <button @click.stop="toggleProfileMenu" class="w-10 h-10 rounded-full bg-white border border-slate-200 flex items-center justify-center shadow-sm hover:border-emerald-500 transition-all">
              <span class="text-sm">👤</span>
            </button>
            <div v-if="isProfileMenuOpen" class="absolute right-0 top-full mt-2 w-52 bg-white border border-slate-200 rounded-xl shadow-2xl overflow-hidden py-1 z-150">
              <div class="px-4 py-3 border-b border-slate-100">
                <p class="text-xs font-bold text-slate-800">{{ parentData?.firstName }} {{ parentData?.lastName }}</p>
                <p class="text-[10px] text-slate-400 mt-0.5">Parent/Guardian Account</p>
              </div>
              <button @click.stop="handleViewProfile" class="w-full text-left px-4 py-3 text-sm text-slate-700 font-bold hover:bg-slate-50 flex items-center gap-2 transition-colors">
                <span>👤</span> View Profile
              </button>
              <button @click.stop="handleLogoutClick" class="w-full text-left px-4 py-3 text-sm text-red-600 font-bold hover:bg-red-50 flex items-center gap-2 transition-colors">
                <span>🚪</span> Logout
              </button>
            </div>
          </div>
        </div>
      </div>
    </header>

    <!-- 2. MAIN NAVIGATION -->
    <!-- On a phone the tabs share the full width and the search box moves to
         its own line, so nothing runs off the side of the screen -->
    <nav class="flex flex-wrap sm:flex-nowrap items-center justify-between bg-white border border-slate-200/60 p-2 rounded-xl shadow-sm mb-10 sticky top-4 z-50 backdrop-blur-md gap-3">
      <div class="flex gap-1 w-full sm:w-auto shrink-0">
        <router-link v-for="tab in navTabs" :key="tab.path" :to="tab.path"
          :class="isActiveTab(tab.path) ? 'bg-emerald-600 text-white shadow-md' : 'text-slate-400 hover:bg-slate-50'"
          class="flex-1 sm:flex-none text-center px-2 sm:px-5 py-2.5 rounded-lg text-[13px] sm:text-sm font-medium transition-all">
          {{ tab.label }}
        </router-link>
      </div>
      <div class="relative w-full sm:w-auto sm:flex-1 sm:max-w-sm">
        <input v-model="searchQuery" @focus="searchFocused = true" @blur="onSearchBlur"
              type="text" placeholder="Search children..."
              class="w-full bg-slate-100 border-none rounded-lg py-2.5 pl-10 pr-4 text-[11px] focus:ring-2 focus:ring-emerald-600 outline-none transition-all" />
        <span class="absolute left-4 top-2.5 text-xs">🔍</span>
        <div v-if="searchFocused && searchQuery.trim() && searchResults.length > 0"
            class="absolute top-full mt-2 left-0 right-0 bg-white rounded-lg border border-slate-100 shadow-xl overflow-hidden z-200">
          <button v-for="child in searchResults" :key="child.childID" @mousedown.prevent="selectFromSearch(child)"
                  class="w-full px-4 py-3 flex items-center gap-3 hover:bg-slate-50 transition-colors text-left border-b border-slate-50 last:border-0">
            <span class="text-lg">{{ child.sex === 'Female' ? '👧' : '👶' }}</span>
            <div>
              <p class="text-xs font-bold text-slate-800">{{ child.firstName }} {{ child.lastName }}</p>
              <p class="text-[9px] text-slate-400 font-medium">ID: #{{ child.childID.slice(-8) }}</p>
            </div>
            <span class="ml-auto text-[9px] text-emerald-500 font-black uppercase">Select →</span>
          </button>
        </div>
        <div v-else-if="searchFocused && searchQuery.trim() && searchResults.length === 0"
            class="absolute top-full mt-2 left-0 right-0 bg-white rounded-lg border border-slate-100 shadow-xl px-4 py-4 text-center z-200">
          <p class="text-[10px] text-slate-400 font-bold">No children found for "{{ searchQuery }}"</p>
        </div>
      </div>
    </nav>

    <!-- On every parent page: the Data Privacy consent (asked once) and the
         "It's your turn" alert when the Nurse presses Call Next -->
    <PrivacyConsentGate v-if="parentData?.parentID" :parent-id="parentData.parentID" @decline="handleLogoutClick" />
    <CalledAlert v-if="parentData?.parentID" :parent-id="parentData.parentID" :children="children" />
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import logoIcon from '@/assets/logo-icon.svg'
import CalledAlert from './CalledAlert.vue'
import PrivacyConsentGate from './PrivacyConsentGate.vue'

// ── Props / Emits ─────────────────────────────────────────────────────────
// parentData, children, and unreadCount are all owned/fetched by whichever
// page renders this component (see HARD RULE 3) and passed down as props.
const props = defineProps({
  parentData: { type: Object, default: null },
  children: { type: Array, default: () => [] },
  unreadCount: { type: Number, default: 0 },
})

const emit = defineEmits(['open-profile', 'open-notifications', 'logout', 'select-child'])

const route = useRoute()

// ── Nav tabs ───────────────────────────────────────────────────────────────
const navTabs = [
  { label: 'Overview', path: '/ParentOverview' },
  { label: 'Check-in', path: '/ParentCheckin' },
  { label: 'Schedule', path: '/ParentSchedule' },
  { label: 'Records', path: '/ParentRecords' },
]

function isActiveTab(path) {
  return route.path === path
}

// ── Profile dropdown ───────────────────────────────────────────────────────
const isProfileMenuOpen = ref(false)

function toggleProfileMenu() {
  isProfileMenuOpen.value = !isProfileMenuOpen.value
}

function openProfile() {
  emit('open-profile')
}

function handleViewProfile() {
  emit('open-profile')
  isProfileMenuOpen.value = false
}

function handleLogoutClick() {
  isProfileMenuOpen.value = false
  emit('logout')
}

// ── Notifications ────────────────────────────────────────────────────────
function openNotifications() {
  emit('open-notifications')
}

// ── Child search ─────────────────────────────────────────────────────────
const searchQuery = ref('')
const searchFocused = ref(false)

const searchResults = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return []
  return props.children.filter(c => `${c.firstName} ${c.lastName}`.toLowerCase().includes(q)).slice(0, 6)
})

function onSearchBlur() {
  setTimeout(() => { searchFocused.value = false }, 150)
}

function selectFromSearch(child) {
  emit('select-child', child)
  searchQuery.value = ''
  searchFocused.value = false
}
</script>