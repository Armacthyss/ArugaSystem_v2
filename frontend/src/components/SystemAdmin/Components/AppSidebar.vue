<script setup>

import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import logoIcon from '@/assets/logo-icon.svg'

import {
  House,
  Users,
  Baby,
  Syringe,
  Package,
  Bell,
  Clock,
  BarChart3,
  ClipboardList,
  LogOut,
  ChevronLeft
} from 'lucide-vue-next'

import { logout } from '@/utils/auth'

const router = useRouter()

const props = defineProps({
  navItems: {
    type: Array,
    default: () => ([
      { label: 'Dashboard',          icon: House,          to: '/system-admin/home' },
      { label: 'User Management',    icon: Users,         to: '/system-admin/user-management' },
      { label: 'Patient Management', icon: Baby,          to: '/system-admin/patients' },
      { label: 'Vaccine Management', icon: Syringe,       to: '/system-admin/vaccines' },
      { label: 'Inventory',          icon: Package,       to: '/system-admin/inventory' },
      { label: 'Notifications',      icon: Bell,          to: '/system-admin/notifications' },
      { label: 'Operating Hours',    icon: Clock,         to: '/system-admin/operating-hours' },
      { label: 'Reports',            icon: BarChart3,     to: '/system-admin/reports' },
      { label: 'Audit Logs',         icon: ClipboardList, to: '/system-admin/audit-logs' },
    ]),
  },

  // Optional overrides; by default the signed-in account is shown
  userName: { type: String, default: '' },
  userRole: { type: String, default: '' },
  userInitials: { type: String, default: '' },
})

const account = computed(() => {
  try {
    return JSON.parse(localStorage.getItem('account') || 'null')
  } catch {
    return null
  }
})

const loggedInUser = computed(() => account.value?.user || {})

// Login.vue stores FirstName / LastName (PascalCase); accept both spellings
const firstName = computed(() => loggedInUser.value.FirstName || loggedInUser.value.firstName || '')
const lastName = computed(() => loggedInUser.value.LastName || loggedInUser.value.lastName || '')

const displayName = computed(() => {
  if (props.userName) return props.userName
  const fullName = [firstName.value, lastName.value].filter(Boolean).join(' ')
  return fullName || loggedInUser.value.username || 'Administrator'
})

const displayRole = computed(() => {
  if (props.userRole) return props.userRole
  const position = loggedInUser.value.UserType || loggedInUser.value.position
  // Admin level: the Doctor (older accounts may be an "Administrator")
  if (account.value?.role === 'SystemAdmin') return position === 'Doctor' ? 'Admin / Doctor' : 'Administrator'
  return position || account.value?.role || 'User'
})

const displayInitials = computed(() => {
  if (props.userInitials) return props.userInitials
  const initials = `${firstName.value[0] || ''}${lastName.value[0] || ''}`.toUpperCase()
  return initials || 'SA'
})

function handleLogout() {
  logout()
  router.push('/')
}

const isCollapsed = ref(false)

const toggleSidebar = () => {
  isCollapsed.value = !isCollapsed.value
}

</script>

<template>
  <aside
    :class="[isCollapsed ? 'w-20' : 'w-65']"
    class="hidden md:flex flex-col shrink-0 sticky top-0 h-screen bg-white border-r border-slate-200 transition-all duration-300 ease-in-out"
  >
    <div class="h-17.5 flex items-center gap-3 px-5 border-b border-slate-200 shrink-0">
      <div class="w-9 h-9 rounded-lg overflow-hidden shrink-0">
        <img :src="logoIcon" alt="Leveriza Health Center" class="w-full h-full object-cover" />
      </div>
      <span v-if="!isCollapsed" class="font-bold text-slate-900 tracking-tight whitespace-nowrap overflow-hidden">Aruga</span>
    </div>

    <nav class="flex-1 overflow-y-auto py-4 px-3 space-y-1">
     <RouterLink
  v-for="item in navItems"
  :key="item.label"
  :to="item.to"
  class="w-full flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
  :class="[
    $route.path === item.to
      ? 'bg-emerald-50 text-emerald-700'
      : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'
  ]"
>
       <component
  :is="item.icon"
  class="w-5 h-5 shrink-0"
  :stroke-width="1.8"
/>
        <span v-if="!isCollapsed" class="truncate">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div class="border-t border-slate-200 p-3 shrink-0 space-y-2">
      <div class="flex items-center gap-3 px-2 py-2">
        <div class="w-9 h-9 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-xs font-bold shrink-0">{{ displayInitials }}</div>
        <div v-if="!isCollapsed" class="min-w-0">
          <p class="text-sm font-semibold text-slate-900 truncate">{{ displayName }}</p>
          <p class="text-xs text-slate-500 truncate">{{ displayRole }}</p>
        </div>
      </div>
      <button
        @click="handleLogout"
        class="w-full flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-rose-600 hover:bg-rose-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-400"
      >
        <LogOut
  class="w-5 h-5 shrink-0"
  :stroke-width="1.8"
/>
        <span v-if="!isCollapsed">Log out</span>
      </button>
      <button @click="toggleSidebar" class="w-full flex items-center justify-center rounded-lg px-3 py-2 text-xs font-medium text-slate-400 hover:bg-slate-50 hover:text-slate-600 transition-colors">
       <ChevronLeft
  :class="isCollapsed ? 'rotate-180' : ''"
  class="w-4 h-4 transition-transform"
/>
      </button>
    </div>
  </aside>
</template>